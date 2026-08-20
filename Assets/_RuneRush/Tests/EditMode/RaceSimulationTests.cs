using System;
using System.Linq;
using NUnit.Framework;

namespace RuneRush.Tests
{
    public sealed class RaceSimulationTests
    {
        [Test]
        public void StartBuildsCompleteDeterministicRaceLayout()
        {
            RaceSimulation simulation = CreateRacingSimulation();

            Assert.That(simulation.Racers.Count, Is.EqualTo(5));
            Assert.That(simulation.Pickups.Count, Is.EqualTo(12));
            Assert.That(simulation.Obstacles.Count, Is.EqualTo(18));
            Assert.That(simulation.Pads.Count, Is.EqualTo(5));
            Assert.That(simulation.Obstacles.Count(value => value.Kind == ObstacleKind.Crate), Is.EqualTo(6));
            Assert.That(simulation.Obstacles.Count(value => value.Kind == ObstacleKind.Barricade), Is.EqualTo(4));
            Assert.That(simulation.Obstacles.Count(value => value.Kind == ObstacleKind.Oil), Is.EqualTo(3));
            Assert.That(simulation.Obstacles.Count(value => value.Kind == ObstacleKind.Spinner), Is.EqualTo(3));
            Assert.That(simulation.Obstacles.Count(value => value.Kind == ObstacleKind.Gate), Is.EqualTo(2));
            Assert.That(simulation.Player.Nitro, Is.EqualTo(64f));
        }

        [Test]
        public void AuthoredLayoutCanIntentionallyRemoveEveryRaceObject()
        {
            var layout = new RaceLayout
            {
                UseAuthoredPickups = true,
                UseAuthoredObstacles = true,
                UseAuthoredPads = true
            };

            var simulation = new RaceSimulation(1000f, layout);
            simulation.Start(Difficulty.Pro, true, layout);

            Assert.That(simulation.Pickups, Is.Empty);
            Assert.That(simulation.Obstacles, Is.Empty);
            Assert.That(simulation.Pads, Is.Empty);
        }

        [Test]
        public void CountdownEntersRaceAndPauseFreezesState()
        {
            var simulation = new RaceSimulation(1000f);
            simulation.Start(Difficulty.Rookie);

            for (int frame = 0; frame < 80 && simulation.Phase == RacePhase.Countdown; frame += 1)
            {
                simulation.Step(0.05f, RaceInputFrame.Idle);
            }

            Assert.That(simulation.Phase, Is.EqualTo(RacePhase.Racing));
            Assert.That(simulation.Countdown, Is.Zero);
            Assert.That(simulation.DrainEvents().Any(value => value.Code == RaceEventCode.RaceStarted), Is.True);

            simulation.Player.Progress = 120f;
            simulation.Player.Speed = 36f;
            float elapsed = simulation.Stats.Elapsed;
            float gameTime = simulation.GameTime;
            simulation.SetPaused(true);
            simulation.Step(0.05f, ThrottleInput());

            Assert.That(simulation.Phase, Is.EqualTo(RacePhase.Paused));
            Assert.That(simulation.GameTime, Is.EqualTo(gameTime));
            Assert.That(simulation.Stats.Elapsed, Is.EqualTo(elapsed));
            Assert.That(simulation.Player.Progress, Is.EqualTo(120f));
            Assert.That(simulation.Player.Speed, Is.EqualTo(36f));
        }

        [Test]
        public void ThrottleNitroDriftAndSlipstreamUpdatePlayerSystems()
        {
            RaceSimulation simulation = CreateRacingSimulation();
            RemoveTrackInteractions(simulation);
            simulation.Racers.Clear();

            for (int frame = 0; frame < 20; frame += 1)
            {
                simulation.Step(0.05f, ThrottleInput());
            }

            Assert.That(simulation.Player.Speed, Is.GreaterThan(20f));
            Assert.That(simulation.Player.Progress, Is.GreaterThan(10f));
            Assert.That(simulation.Stats.TopSpeed, Is.EqualTo(simulation.Player.Speed * 3.6f).Within(0.001f));

            float nitroBefore = simulation.Player.Nitro;
            RaceInputFrame nitro = ThrottleInput();
            nitro.Nitro = true;
            for (int frame = 0; frame < 10; frame += 1)
            {
                simulation.Step(0.05f, nitro);
            }
            Assert.That(simulation.Player.Nitro, Is.LessThan(nitroBefore));

            RaceInputFrame drift = ThrottleInput();
            drift.Drift = true;
            for (int frame = 0; frame < 55; frame += 1)
            {
                drift.Steer = frame / 8 % 2 == 0 ? 0.7f : -0.7f;
                simulation.Step(0.05f, drift);
            }

            Assert.That(simulation.Player.DriftCharge, Is.GreaterThan(10f));
            int scoreBeforeRelease = simulation.Player.Score;
            simulation.Step(0.05f, ThrottleInput());
            Assert.That(simulation.Player.DriftBoostUntil, Is.GreaterThan(simulation.GameTime));
            Assert.That(simulation.Player.Score, Is.GreaterThan(scoreBeforeRelease));

            RacerState slipTarget = new RacerState
            {
                Id = "slip-target",
                Name = "TARGET",
                Progress = simulation.Player.Progress + 20f,
                Lane = simulation.Player.Lane,
                TargetLane = simulation.Player.Lane,
                BaseSpeed = 0f,
                Skill = 1f,
                Aggression = 0f,
                NextDecision = float.PositiveInfinity,
                NextAttack = float.PositiveInfinity
            };
            simulation.Racers.Add(slipTarget);
            simulation.Player.Speed = 0f;
            for (int frame = 0; frame < 20; frame += 1)
            {
                simulation.Step(0.05f, RaceInputFrame.Idle);
            }
            Assert.That(simulation.Player.Slipstream, Is.GreaterThan(0.72f));
        }

        [Test]
        public void ShieldConsumesHitsWithoutHullOrSpeedLoss()
        {
            RaceSimulation simulation = CreateRacingSimulation();
            simulation.Pickups.Clear();
            simulation.Pads.Clear();
            simulation.Racers.Clear();
            while (simulation.Obstacles.Count > 1)
            {
                simulation.Obstacles.RemoveAt(simulation.Obstacles.Count - 1);
            }

            ObstacleState obstacle = simulation.Obstacles[0];
            obstacle.Progress = 100f;
            obstacle.Lane = 0f;
            simulation.Player.Progress = 100f;
            simulation.Player.Lane = 0f;
            simulation.Player.Speed = 44f;
            simulation.Player.Damage = 5f;
            simulation.GrantItem(ItemType.Shield);
            Assert.That(simulation.UseHeldItem(), Is.True);

            simulation.Step(0f, RaceInputFrame.Idle);

            Assert.That(simulation.Player.Damage, Is.EqualTo(5f));
            Assert.That(simulation.Player.Speed, Is.EqualTo(44f));
            Assert.That(simulation.Player.ShieldUntil, Is.EqualTo(5.2f).Within(0.0001f));
            Assert.That(simulation.DrainEvents().Any(value => value.Code == RaceEventCode.ShieldBlocked), Is.True);
        }

        [Test]
        public void EmpAndPulseUseCircularRangeAndTargeting()
        {
            RaceSimulation simulation = CreateRacingSimulation();
            RemoveTrackInteractions(simulation);
            while (simulation.Racers.Count > 2)
            {
                simulation.Racers.RemoveAt(simulation.Racers.Count - 1);
            }

            simulation.Player.Progress = 100f;
            RacerState nearby = simulation.Racers[0];
            RacerState distant = simulation.Racers[1];
            nearby.Progress = 220f;
            nearby.Speed = 50f;
            distant.Progress = 250f;
            distant.Speed = 50f;

            simulation.GrantItem(ItemType.Emp);
            Assert.That(simulation.UseHeldItem(), Is.True);
            Assert.That(nearby.DisabledUntil, Is.EqualTo(2.8f).Within(0.0001f));
            Assert.That(nearby.Speed, Is.EqualTo(31f).Within(0.0001f));
            Assert.That(distant.DisabledUntil, Is.Zero);
            Assert.That(simulation.Player.Score, Is.EqualTo(300));

            for (int frame = 0; frame < 10; frame += 1)
            {
                simulation.Step(0.05f, RaceInputFrame.Idle);
            }
            nearby.Progress = simulation.Player.Progress + 20f;
            nearby.Speed = 40f;
            nearby.ShieldUntil = simulation.GameTime + 5f;
            distant.Progress = simulation.Player.Progress + 80f;
            simulation.GrantItem(ItemType.Pulse);
            Assert.That(simulation.UseHeldItem(), Is.True);
            Assert.That(nearby.ShieldUntil, Is.Zero);
            Assert.That(simulation.DrainEvents().Any(value => value.Code == RaceEventCode.PulseBlocked), Is.True);
        }

        [Test]
        public void PlayerMineDisablesRivalAndAwardsScore()
        {
            RaceSimulation simulation = CreateRacingSimulation();
            RemoveTrackInteractions(simulation);
            while (simulation.Racers.Count > 1)
            {
                simulation.Racers.RemoveAt(simulation.Racers.Count - 1);
            }

            simulation.Player.Progress = 100f;
            simulation.Player.Lane = 2f;
            RacerState rival = simulation.Racers[0];
            rival.Progress = 93f;
            rival.Lane = 2f;
            rival.TargetLane = 2f;
            rival.Speed = 40f;
            rival.NextDecision = float.PositiveInfinity;
            rival.NextAttack = float.PositiveInfinity;

            simulation.GrantItem(ItemType.Mine);
            Assert.That(simulation.UseHeldItem(), Is.True);
            Assert.That(simulation.Mines.Count, Is.EqualTo(1));
            simulation.Step(0f, RaceInputFrame.Idle);

            Assert.That(simulation.Mines.Count, Is.Zero);
            Assert.That(rival.DisabledUntil, Is.EqualTo(1.8f).Within(0.0001f));
            Assert.That(rival.Speed, Is.EqualTo(18.4f).Within(0.0001f));
            Assert.That(rival.Damage, Is.EqualTo(15f));
            Assert.That(simulation.Player.Score, Is.EqualTo(750));
        }

        [Test]
        public void PickupsPadsObstaclesAndMovingLanesAreActive()
        {
            RaceSimulation simulation = CreateRacingSimulation();
            simulation.Racers.Clear();
            simulation.Obstacles.Clear();
            simulation.Pads.Clear();
            PickupState pickup = simulation.Pickups[0];
            simulation.Player.Progress = pickup.Progress;
            simulation.Player.Lane = pickup.Lane;
            simulation.Step(0f, RaceInputFrame.Idle);

            Assert.That(simulation.Player.HasItem, Is.True);
            Assert.That(pickup.Active, Is.False);
            Assert.That(simulation.Player.Score, Is.EqualTo(100));

            RaceSimulation moving = CreateRacingSimulation();
            moving.Racers.Clear();
            moving.Pickups.Clear();
            moving.Pads.Clear();
            ObstacleState gate = moving.Obstacles.First(value => value.Kind == ObstacleKind.Gate);
            float firstLane = moving.GetObstacleLane(gate);
            for (int frame = 0; frame < 20; frame += 1)
            {
                moving.Step(0.05f, RaceInputFrame.Idle);
            }
            Assert.That(moving.GetObstacleLane(gate), Is.Not.EqualTo(firstLane).Within(0.001f));
        }

        [Test]
        public void RankLapsAndFinishUseAbsoluteProgress()
        {
            const float trackLength = 500f;
            RaceSimulation simulation = CreateRacingSimulation(trackLength);
            RemoveTrackInteractions(simulation);
            float finishDistance = trackLength * RaceSimulation.TotalLaps;

            float[] progress = { 130f, 120f, 90f, 80f, 70f };
            simulation.Player.Progress = 100f;
            for (int index = 0; index < simulation.Racers.Count; index += 1)
            {
                RacerState racer = simulation.Racers[index];
                racer.Progress = progress[index];
                racer.Lane = 6f;
                racer.TargetLane = 6f;
                racer.NextDecision = float.PositiveInfinity;
                racer.NextAttack = float.PositiveInfinity;
            }
            Assert.That(simulation.GetPlayerRank(), Is.EqualTo(3));

            simulation.Racers[0].Progress = finishDistance + 5f;
            for (int index = 1; index < simulation.Racers.Count; index += 1)
            {
                simulation.Racers[index].Progress = 0f;
            }

            simulation.Player.Progress = trackLength + 1f;
            simulation.Step(0f, RaceInputFrame.Idle);
            Assert.That(simulation.Lap, Is.EqualTo(2));
            simulation.Player.Progress = trackLength * 2f + 1f;
            simulation.Step(0f, RaceInputFrame.Idle);
            Assert.That(simulation.Lap, Is.EqualTo(3));
            simulation.Stats.CurrentLapTime = 31.25f;
            simulation.Player.Progress = finishDistance;
            simulation.Step(0f, RaceInputFrame.Idle);

            Assert.That(simulation.Phase, Is.EqualTo(RacePhase.Finished));
            Assert.That(simulation.Player.Finished, Is.True);
            Assert.That(simulation.Rank, Is.EqualTo(2));
            Assert.That(simulation.Winner, Is.EqualTo(simulation.Racers[0].Name));
            Assert.That(simulation.Stats.LastLap, Is.EqualTo(31.25f));
            Assert.That(simulation.Stats.BestLap, Is.EqualTo(31.25f));
            Assert.That(simulation.DrainEvents().Count(value => value.Code == RaceEventCode.LapCompleted), Is.EqualTo(3));
        }

        private static RaceSimulation CreateRacingSimulation(float trackLength = 1000f)
        {
            var simulation = new RaceSimulation(trackLength);
            simulation.Start(Difficulty.Pro, true);
            simulation.DrainEvents();
            return simulation;
        }

        private static void RemoveTrackInteractions(RaceSimulation simulation)
        {
            simulation.Pickups.Clear();
            simulation.Obstacles.Clear();
            simulation.Pads.Clear();
        }

        private static RaceInputFrame ThrottleInput()
        {
            RaceInputFrame input = RaceInputFrame.Idle;
            input.Throttle = 1f;
            return input;
        }
    }
}
