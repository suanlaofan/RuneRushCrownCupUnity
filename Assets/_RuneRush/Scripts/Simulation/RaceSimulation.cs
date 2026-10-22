using System;
using System.Collections.Generic;
using System.Globalization;

namespace RuneRush
{
    public sealed class RaceSimulation
    {
        public const int TotalLaps = 3;
        public const float RoadHalfWidth = 9f;
        public const float FixedStep = 1f / 60f;

        private static readonly ItemType[] AllItems =
        {
            ItemType.Nitro,
            ItemType.Shield,
            ItemType.Emp,
            ItemType.Pulse,
            ItemType.Mine,
            ItemType.Repair
        };

        private static readonly AiTemplate[] AiTemplates =
        {
            new AiTemplate("ROWAN", 0xC43F53, 65f, 0.98f, 0.74f),
            new AiTemplate("VESPER", 0xF3BF55, 66f, 0.95f, 0.90f),
            new AiTemplate("BRAN", 0x79D9FF, 63.5f, 0.90f, 0.58f),
            new AiTemplate("ISOLDE", 0x9B7ACB, 64.2f, 0.92f, 0.68f),
            new AiTemplate("CEDRIC", 0xE8E2D1, 62.8f, 0.88f, 0.52f)
        };

        private readonly List<RaceEvent> _events = new List<RaceEvent>(16);
        private readonly Dictionary<string, float> _lastAiCollision = new Dictionary<string, float>();
        private SeededRandom _random;
        private int _eventId;
        private int _previousLap;
        private bool _previousItemUse;
        private bool _previousReset;

        public float TrackLength { get; }
        public PlayerState Player { get; private set; }
        public List<RacerState> Racers { get; } = new List<RacerState>(5);
        public List<PickupState> Pickups { get; } = new List<PickupState>(12);
        public List<ObstacleState> Obstacles { get; } = new List<ObstacleState>(18);
        public List<BoostPadState> Pads { get; } = new List<BoostPadState>(5);
        public List<MineState> Mines { get; } = new List<MineState>();
        public RaceStats Stats { get; private set; }
        public RacePhase Phase { get; private set; }
        public Difficulty Difficulty { get; private set; }
        public float GameTime { get; private set; }
        public float Countdown { get; private set; }
        public int Rank { get; private set; }
        public int Lap { get; private set; }
        public int Checkpoint { get; private set; }
        public string Winner { get; private set; }

        public RaceSimulation(float trackLength)
            : this(trackLength, null)
        {
        }

        public RaceSimulation(float trackLength, RaceLayout layout)
        {
            if (trackLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(trackLength), "Track length must be positive.");
            }

            TrackLength = trackLength;
            _random = new SeededRandom();
            Player = CreatePlayer();
            Stats = CreateStats();
            Winner = string.Empty;
            Difficulty = Difficulty.Pro;
            Phase = RacePhase.Menu;
            Rank = 1;
            Lap = 1;
            _previousLap = 1;
            PopulateRacers();
            PopulateRaceLayout(layout);
        }

        public static DifficultyConfig GetDifficultyConfig(Difficulty difficulty)
        {
            switch (difficulty)
            {
                case Difficulty.Rookie:
                    return new DifficultyConfig(0.88f, 0.90f, 0.72f, 0.055f, "Squire");
                case Difficulty.Apex:
                    return new DifficultyConfig(1.045f, 1.08f, 1.24f, 0.095f, "Lord");
                default:
                    return new DifficultyConfig(0.97f, 1f, 1f, 0.075f, "Knight");
            }
        }

        public void Start(Difficulty difficulty = Difficulty.Pro, bool skipCountdown = false, RaceLayout layout = null)
        {
            Difficulty = difficulty;
            uint seedOffset = difficulty == Difficulty.Rookie ? 1u : difficulty == Difficulty.Apex ? 3u : 2u;
            _random = new SeededRandom(0x0051F15Eu + seedOffset);
            Player = CreatePlayer();
            Racers.Clear();
            Pickups.Clear();
            Obstacles.Clear();
            Pads.Clear();
            Mines.Clear();
            PopulateRacers();
            PopulateRaceLayout(layout);
            Stats = CreateStats();
            Phase = skipCountdown ? RacePhase.Racing : RacePhase.Countdown;
            Countdown = skipCountdown ? 0f : 3.6f;
            GameTime = 0f;
            Rank = 1;
            Lap = 1;
            _previousLap = 1;
            Checkpoint = 0;
            Winner = string.Empty;
            _events.Clear();
            _previousItemUse = false;
            _previousReset = false;
            _lastAiCollision.Clear();
            PushEvent(
                RaceEventKind.Info,
                RaceEventCode.RaceInitialized,
                "Runes Lit",
                GetDifficultyConfig(difficulty).Label + " Trials");

            if (skipCountdown)
            {
                PushEvent(RaceEventKind.Positive, RaceEventCode.RaceStarted, "Starting Horn", "Crown Cup Begins");
            }
        }

        public void ReturnToMenu()
        {
            Phase = RacePhase.Menu;
            Player.Speed = 0f;
            _previousItemUse = false;
            _previousReset = false;
        }

        public void SetPaused(bool paused)
        {
            if (paused && (Phase == RacePhase.Racing || Phase == RacePhase.Countdown))
            {
                Phase = RacePhase.Paused;
            }
            else if (!paused && Phase == RacePhase.Paused)
            {
                Phase = Countdown > 0f ? RacePhase.Countdown : RacePhase.Racing;
            }
        }

        public void Step(float deltaTime, RaceInputFrame input)
        {
            float dt = RaceMath.Clamp(deltaTime, 0f, 0.05f);
            if (Phase == RacePhase.Menu || Phase == RacePhase.Paused || Phase == RacePhase.Finished)
            {
                _previousItemUse = input.UseItem;
                _previousReset = input.Reset;
                return;
            }

            GameTime += dt;
            if (Phase == RacePhase.Countdown)
            {
                int before = (int)Math.Ceiling(Countdown);
                Countdown -= dt;
                int after = (int)Math.Ceiling(Countdown);
                if (after < before && after > 0)
                {
                    PushEvent(
                        RaceEventKind.Info,
                        RaceEventCode.CountdownTick,
                        after.ToString(CultureInfo.InvariantCulture),
                        "Grip the Reins");
                }

                if (Countdown <= 0f)
                {
                    Countdown = 0f;
                    Phase = RacePhase.Racing;
                    PushEvent(RaceEventKind.Positive, RaceEventCode.RaceStarted, "Starting Horn", "Crown Cup Begins");
                }

                _previousItemUse = input.UseItem;
                _previousReset = input.Reset;
                return;
            }

            Stats.Elapsed += dt;
            Stats.CurrentLapTime += dt;
            UpdatePlayer(dt, input);
            UpdateAi(dt);
            UpdatePickups();
            UpdateObstacles();
            UpdateMines();
            UpdateRaceState();
            _previousItemUse = input.UseItem;
            _previousReset = input.Reset;
        }

        public RaceEvent[] DrainEvents()
        {
            RaceEvent[] result = _events.ToArray();
            _events.Clear();
            return result;
        }

        public RaceSnapshot CreateSnapshot()
        {
            return new RaceSnapshot
            {
                Phase = Phase,
                Difficulty = Difficulty,
                GameTime = GameTime,
                Countdown = Countdown,
                Rank = Rank,
                Lap = Lap,
                Checkpoint = Checkpoint,
                Winner = Winner,
                Player = Player,
                Racers = Racers,
                Stats = Stats
            };
        }

        public int GetPlayerRank()
        {
            int rank = 1;
            for (int index = 0; index < Racers.Count; index += 1)
            {
                if (Racers[index].Progress > Player.Progress)
                {
                    rank += 1;
                }
            }

            return rank;
        }

        public float GetObstacleLane(ObstacleState obstacle)
        {
            if (obstacle == null)
            {
                throw new ArgumentNullException(nameof(obstacle));
            }

            if (obstacle.Kind == ObstacleKind.Gate)
            {
                return obstacle.Lane + (float)Math.Sin(GameTime * 1.05f + obstacle.Phase) * 5.2f;
            }

            if (obstacle.Kind == ObstacleKind.Spinner)
            {
                return obstacle.Lane + (float)Math.Sin(GameTime * 2.2f + obstacle.Phase) * 2.1f;
            }

            return obstacle.Lane;
        }

        public void GrantItem(ItemType item)
        {
            Player.Item = item;
            Player.HasItem = true;
        }

        public bool UseHeldItem()
        {
            return Player.HasItem && UseItem(Player.Item);
        }

        public bool UseItem(ItemType type)
        {
            if (GameTime < Player.ItemCooldownUntil || Phase != RacePhase.Racing)
            {
                return false;
            }

            Player.HasItem = false;
            Player.ItemCooldownUntil = GameTime + 0.45f;
            Stats.ItemsUsed += 1;

            switch (type)
            {
                case ItemType.Nitro:
                    Player.Nitro = RaceMath.Clamp(Player.Nitro + 62f, 0f, 100f);
                    Player.BoostUntil = Math.Max(Player.BoostUntil, GameTime + 1.25f);
                    PushEvent(RaceEventKind.Item, RaceEventCode.NitroPotionUsed, "Gale Potion", "+62 Gale");
                    break;

                case ItemType.Shield:
                    Player.ShieldUntil = GameTime + 7f;
                    PushEvent(RaceEventKind.Item, RaceEventCode.ShieldActivated, "Guardian Ward", "Ward lasts 7s");
                    break;

                case ItemType.Repair:
                    Player.Damage = Math.Max(0f, Player.Damage - 42f);
                    PushEvent(RaceEventKind.Positive, RaceEventCode.RepairUsed, "Cleric's Grace", "Armor Repaired");
                    break;

                case ItemType.Emp:
                    UseEmp();
                    break;

                case ItemType.Pulse:
                    UsePulse();
                    break;

                case ItemType.Mine:
                    Mines.Add(new MineState
                    {
                        Id = "player-mine-" + GameTime.ToString("F3", CultureInfo.InvariantCulture),
                        Progress = Player.Progress - 7f,
                        Lane = Player.Lane,
                        Owner = Player.Id,
                        ExpiresAt = GameTime + 22f,
                        Active = true
                    });
                    PushEvent(RaceEventKind.Item, RaceEventCode.MineDropped, "Curse Stone Dropped", "Trap Armed");
                    break;
            }

            return true;
        }

        public void ResetPlayer()
        {
            Player.Lane = 0f;
            Player.Speed *= 0.35f;
            Player.SpinUntil = 0f;
            Player.Damage = RaceMath.Clamp(Player.Damage + 3f, 0f, 100f);
            PushEvent(RaceEventKind.Info, RaceEventCode.ResetToTrack, "Rewind Beacon", "Rewind penalty taken");
        }

        private void UpdatePlayer(float dt, RaceInputFrame input)
        {
            DifficultyConfig config = GetDifficultyConfig(Difficulty);
            bool controlsDisabled = Player.DisabledUntil > GameTime;
            float steer = controlsDisabled ? 0f : RaceMath.Clamp(input.Steer, -1f, 1f);
            if (Player.SpinUntil > GameTime)
            {
                steer = (float)Math.Sin(GameTime * 16f) * 0.9f;
            }

            float hullPenalty = 1f - Player.Damage * 0.0022f;
            float maxSpeed = Player.BaseSpeed * hullPenalty;
            float acceleration = controlsDisabled ? 0f : input.Throttle * 30f;
            acceleration -= input.Brake * 49f;
            acceleration -= 1.1f + Player.Speed * 0.013f;

            bool usingNitro = input.Nitro && Player.Nitro > 0.4f && Player.Speed > 7f && !controlsDisabled;
            if (usingNitro)
            {
                Player.Nitro = Math.Max(0f, Player.Nitro - 22f * dt);
                acceleration += 31f;
                maxSpeed += 24f;
            }
            else
            {
                Player.Nitro = Math.Min(100f, Player.Nitro + 1.5f * dt);
            }

            if (Player.BoostUntil > GameTime || Player.DriftBoostUntil > GameTime)
            {
                acceleration += 18f;
                maxSpeed += 14f;
            }

            bool offroad = Math.Abs(Player.Lane) > RoadHalfWidth - 0.7f;
            if (offroad)
            {
                acceleration -= 24f;
                maxSpeed = Math.Min(maxSpeed, 34f);
            }

            Player.Drifting = input.Drift
                && Math.Abs(steer) > 0.2f
                && Player.Speed > 18f
                && !offroad;
            float steerRate = Player.Drifting ? 13.2f : 8.4f;
            Player.Lane += steer * steerRate * (0.35f + Player.Speed / 90f) * dt;

            if (Player.Drifting)
            {
                Player.DriftCharge = RaceMath.Clamp(
                    Player.DriftCharge + Math.Abs(steer) * Player.Speed * 0.23f * dt,
                    0f,
                    100f);
                Player.Nitro = RaceMath.Clamp(
                    Player.Nitro + Math.Abs(steer) * 5.4f * dt,
                    0f,
                    100f);
                Player.Speed = Math.Max(0f, Player.Speed - 2.2f * dt);
                Stats.DriftDistance += Player.Speed * dt;
                Player.ComboExpires = GameTime + 2.2f;
            }
            else if (Player.DriftCharge > 0f)
            {
                float charge = Player.DriftCharge;
                if (charge > 10f)
                {
                    float duration = RaceMath.Clamp(charge / 34f, 0.35f, 2.35f);
                    Player.DriftBoostUntil = GameTime + duration;
                    int basePoints = RaceMath.JavaScriptRound(charge * 18f);
                    Player.Score += RaceMath.JavaScriptRound(charge * 18f * Player.Combo);
                    PushEvent(
                        RaceEventKind.Positive,
                        RaceEventCode.DriftBoost,
                        charge > 55f ? "Extreme Drift" : "Wind Drift",
                        "+" + basePoints.ToString(CultureInfo.InvariantCulture) + " Glory");
                }

                Player.DriftCharge = 0f;
            }

            if (Math.Abs(Player.Lane) > RoadHalfWidth + 1.25f)
            {
                Player.Lane = Math.Sign(Player.Lane) * (RoadHalfWidth + 1.25f);
                if (Player.CollisionCooldownUntil < GameTime)
                {
                    HitPlayer(7f * config.Damage, 0.73f, "Rail Crash", RaceEventCode.ObstacleHit);
                    Player.CollisionCooldownUntil = GameTime + 0.7f;
                }
            }

            float slipTarget = HasSlipstreamTarget() ? 1f : 0f;
            Player.Slipstream = RaceMath.Damp(
                Player.Slipstream,
                slipTarget,
                slipTarget > 0f ? 1.8f : 3.4f,
                dt);
            if (Player.Slipstream > 0.72f)
            {
                acceleration += 7.5f * Player.Slipstream;
                maxSpeed += 5f;
            }

            Player.Speed = RaceMath.Clamp(Player.Speed + acceleration * dt, 0f, maxSpeed);
            Player.Progress += Player.Speed * dt;
            Player.Lane = RaceMath.Clamp(Player.Lane, -RoadHalfWidth - 1.25f, RoadHalfWidth + 1.25f);
            Stats.TopSpeed = Math.Max(Stats.TopSpeed, Player.Speed * 3.6f);

            if (Player.ComboExpires < GameTime)
            {
                Player.Combo = RaceMath.Damp(Player.Combo, 1f, 3f, dt);
            }

            if (input.UseItem && !_previousItemUse)
            {
                UseHeldItem();
            }

            if (input.Reset && !_previousReset)
            {
                ResetPlayer();
            }
        }

        private void UpdateAi(float dt)
        {
            DifficultyConfig config = GetDifficultyConfig(Difficulty);
            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                if (racer.Finished)
                {
                    continue;
                }

                float playerDelta = Player.Progress - racer.Progress;
                float rubber = RaceMath.Clamp(playerDelta / TrackLength, -0.12f, 0.15f)
                    * config.RubberBand
                    * 8f;
                float curve = CurveDemand(racer.Progress + racer.Speed * 0.85f);
                float damagePenalty = 1f - racer.Damage * 0.0018f;
                float targetSpeed = racer.BaseSpeed
                    * config.AiSpeed
                    * damagePenalty
                    * (1f - curve * (0.24f / config.AiGrip));
                targetSpeed *= 1f + rubber;
                if (racer.DisabledUntil > GameTime)
                {
                    targetSpeed *= 0.35f;
                }

                if (GameTime > racer.NextDecision)
                {
                    ObstacleState obstacle = ClosestObstacleAhead(racer.Progress, 42f);
                    if (obstacle != null && Math.Abs(GetObstacleLane(obstacle) - racer.Lane) < 2.5f)
                    {
                        racer.TargetLane = RaceMath.Clamp(
                            racer.Lane + (racer.Lane > 0f ? -4.5f : 4.5f),
                            -6.7f,
                            6.7f);
                    }
                    else
                    {
                        racer.TargetLane = _random.Range(-6.1f, 6.1f);
                    }

                    racer.NextDecision = GameTime + _random.Range(1.4f, 3.5f) / racer.Skill;
                }

                float acceleration = racer.DisabledUntil > GameTime ? -18f : 15f + racer.Skill * 5f;
                racer.Speed += RaceMath.Clamp(
                    targetSpeed - racer.Speed,
                    -28f * dt,
                    acceleration * dt);
                racer.Speed = Math.Max(0f, racer.Speed);
                racer.Lane = RaceMath.Damp(racer.Lane, racer.TargetLane, 0.85f + racer.Skill, dt);
                racer.Progress += racer.Speed * dt;

                if (GameTime > racer.NextAttack)
                {
                    AiAttack(racer);
                    racer.NextAttack = GameTime + _random.Range(12f, 22f) / racer.Aggression;
                }
            }

            ResolveAiCollisions();
        }

        private void UpdatePickups()
        {
            int lapIndex = (int)Math.Floor(Math.Max(0f, Player.Progress) / TrackLength);
            for (int index = 0; index < Pickups.Count; index += 1)
            {
                PickupState pickup = Pickups[index];
                if (!pickup.Active && GameTime >= pickup.RespawnAt)
                {
                    pickup.Active = true;
                }

                if (!pickup.Active || Player.HasItem)
                {
                    continue;
                }

                float targetProgress = pickup.Progress + lapIndex * TrackLength;
                float delta = targetProgress - Player.Progress;
                if (Math.Abs(delta) < 2.4f && Math.Abs(pickup.Lane - Player.Lane) < 2.25f)
                {
                    pickup.Active = false;
                    pickup.RespawnAt = GameTime + 8.5f;
                    GrantItem(RollItem());
                    Player.Score += 100;
                    PushEvent(
                        RaceEventKind.Item,
                        RaceEventCode.ItemPickedUp,
                        GetItemLabel(Player.Item),
                        "Relic Collected");
                }
            }

            for (int index = 0; index < Pads.Count; index += 1)
            {
                BoostPadState pad = Pads[index];
                float targetProgress = pad.Progress + lapIndex * TrackLength;
                if (pad.LastLap == lapIndex
                    || Math.Abs(targetProgress - Player.Progress) > 2.6f
                    || Math.Abs(pad.Lane - Player.Lane) > 2.4f)
                {
                    continue;
                }

                pad.LastLap = lapIndex;
                if (pad.Kind == PadKind.Boost)
                {
                    Player.BoostUntil = Math.Max(Player.BoostUntil, GameTime + 1.75f);
                    Player.Nitro = RaceMath.Clamp(Player.Nitro + 12f, 0f, 100f);
                    Player.Score += 250;
                    PushEvent(RaceEventKind.Positive, RaceEventCode.BoostPadTriggered, "Wind Rune", "+12 Gale");
                }
                else
                {
                    float repaired = Math.Min(20f, Player.Damage);
                    Player.Damage -= repaired;
                    PushEvent(
                        RaceEventKind.Positive,
                        RaceEventCode.RepairPadTriggered,
                        "Healing Grove",
                        "-" + RaceMath.JavaScriptRound(repaired).ToString(CultureInfo.InvariantCulture) + " Damage");
                }
            }
        }

        private void UpdateObstacles()
        {
            int playerLap = (int)Math.Floor(Math.Max(0f, Player.Progress) / TrackLength);
            for (int index = 0; index < Obstacles.Count; index += 1)
            {
                ObstacleState obstacle = Obstacles[index];
                float targetProgress = obstacle.Progress + playerLap * TrackLength;
                float delta = targetProgress - Player.Progress;
                float laneGap = Math.Abs(GetObstacleLane(obstacle) - Player.Lane);
                if (Math.Abs(delta) < 2.25f
                    && laneGap < GetObstacleRadius(obstacle.Kind)
                    && obstacle.LastHitLap != playerLap)
                {
                    obstacle.LastHitLap = playerLap;
                    if (obstacle.Kind == ObstacleKind.Oil)
                    {
                        if (!AbsorbWithShield())
                        {
                            Player.SpinUntil = GameTime + 1.35f;
                            Player.Speed *= 0.7f;
                            Player.Combo = 1f;
                            PushEvent(RaceEventKind.Danger, RaceEventCode.OilSpin, "Cursed Mire", "Countersteer!");
                        }
                    }
                    else
                    {
                        float damage = GetObstacleDamage(obstacle.Kind);
                        float speedMultiplier = obstacle.Kind == ObstacleKind.Crate ? 0.76f : 0.58f;
                        HitPlayer(
                            damage * GetDifficultyConfig(Difficulty).Damage,
                            speedMultiplier,
                            "Obstacle Crash",
                            RaceEventCode.ObstacleHit);
                    }
                }
                else if (delta < -2.1f
                    && delta > -6f
                    && obstacle.LastPassLap != playerLap
                    && obstacle.LastHitLap != playerLap
                    && laneGap < GetObstacleRadius(obstacle.Kind) + 2.15f)
                {
                    obstacle.LastPassLap = playerLap;
                    Stats.NearMisses += 1;
                    Player.Combo = RaceMath.Clamp(Player.Combo + 0.25f, 1f, 4f);
                    Player.ComboExpires = GameTime + 4f;
                    int points = RaceMath.JavaScriptRound(400f * Player.Combo);
                    Player.Score += points;
                    PushEvent(
                        RaceEventKind.Positive,
                        RaceEventCode.NearMiss,
                        "Near Miss",
                        "Combo x" + Player.Combo.ToString("F2", CultureInfo.InvariantCulture)
                            + " · +" + points.ToString(CultureInfo.InvariantCulture) + " Glory");
                }
            }
        }

        private void UpdateMines()
        {
            for (int mineIndex = 0; mineIndex < Mines.Count; mineIndex += 1)
            {
                MineState mine = Mines[mineIndex];
                if (!mine.Active || mine.ExpiresAt < GameTime)
                {
                    mine.Active = false;
                    continue;
                }

                if (!string.Equals(mine.Owner, Player.Id, StringComparison.Ordinal))
                {
                    float delta = RaceMath.SignedTrackDelta(Player.Progress, mine.Progress, TrackLength);
                    if (Math.Abs(delta) < 2.2f && Math.Abs(Player.Lane - mine.Lane) < 2f)
                    {
                        mine.Active = false;
                        if (!AbsorbWithShield())
                        {
                            HitPlayer(
                                14f * GetDifficultyConfig(Difficulty).Damage,
                                0.54f,
                                "Curse Stone Burst",
                                RaceEventCode.MineHitPlayer);
                            Player.SpinUntil = GameTime + 1.1f;
                        }
                    }
                }
                else
                {
                    for (int racerIndex = 0; racerIndex < Racers.Count; racerIndex += 1)
                    {
                        RacerState racer = Racers[racerIndex];
                        if (Math.Abs(RaceMath.SignedTrackDelta(racer.Progress, mine.Progress, TrackLength)) < 2.3f
                            && Math.Abs(racer.Lane - mine.Lane) < 2f)
                        {
                            mine.Active = false;
                            racer.Speed *= 0.46f;
                            racer.DisabledUntil = GameTime + 1.8f;
                            racer.Damage = RaceMath.Clamp(racer.Damage + 15f, 0f, 100f);
                            Stats.RivalsHit += 1;
                            Player.Score += 750;
                            PushEvent(RaceEventKind.Item, RaceEventCode.MineHitRival, "Curse Stone Triggered", racer.Name);
                            break;
                        }
                    }
                }
            }

            Mines.RemoveAll(mine => !mine.Active || mine.ExpiresAt < GameTime);
        }

        private void UpdateRaceState()
        {
            Rank = GetPlayerRank();
            Lap = RaceMath.Clamp(
                (int)Math.Floor(Math.Max(0f, Player.Progress) / TrackLength) + 1,
                1,
                TotalLaps);
            Checkpoint = (int)Math.Floor(RaceMath.Wrap(Player.Progress, TrackLength) / TrackLength * 4f);

            if (Lap > _previousLap)
            {
                Stats.LastLap = Stats.CurrentLapTime;
                Stats.BestLap = Stats.BestLap == 0f
                    ? Stats.LastLap
                    : Math.Min(Stats.BestLap, Stats.LastLap);
                Stats.CurrentLapTime = 0f;
                _previousLap = Lap;
                Player.Score += 1500;
                PushEvent(
                    RaceEventKind.Lap,
                    RaceEventCode.LapCompleted,
                    "Lap " + Lap.ToString(CultureInfo.InvariantCulture)
                        + "/" + TotalLaps.ToString(CultureInfo.InvariantCulture),
                    "Last lap " + Stats.LastLap.ToString("F2", CultureInfo.InvariantCulture) + "s");
            }

            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                if (!racer.Finished && racer.Progress >= TrackLength * TotalLaps)
                {
                    racer.Finished = true;
                    racer.FinishTime = Stats.Elapsed;
                    if (string.IsNullOrEmpty(Winner))
                    {
                        Winner = racer.Name;
                    }
                }
            }

            if (Player.Progress >= TrackLength * TotalLaps)
            {
                if (!Player.Finished)
                {
                    Stats.LastLap = Stats.CurrentLapTime;
                    Stats.BestLap = Stats.BestLap == 0f
                        ? Stats.LastLap
                        : Math.Min(Stats.BestLap, Stats.LastLap);
                    Stats.CurrentLapTime = 0f;
                    PushEvent(
                        RaceEventKind.Lap,
                        RaceEventCode.LapCompleted,
                        "Finished " + TotalLaps.ToString(CultureInfo.InvariantCulture)
                            + "/" + TotalLaps.ToString(CultureInfo.InvariantCulture),
                        "Final lap " + Stats.LastLap.ToString("F2", CultureInfo.InvariantCulture) + "s");
                }

                Player.Finished = true;
                Player.FinishTime = Stats.Elapsed;
                if (string.IsNullOrEmpty(Winner))
                {
                    Winner = Player.Name;
                }

                Rank = GetPlayerRank();
                Phase = RacePhase.Finished;
                Player.Speed = Math.Max(0f, Player.Speed);
                Player.Score += Math.Max(0, 7 - Rank) * 2500;
                PushEvent(
                    RaceEventKind.Finish,
                    RaceEventCode.RaceFinished,
                    Rank == 1
                        ? "Crown Cup Won"
                        : "Finished #" + Rank.ToString(CultureInfo.InvariantCulture),
                    Player.Score.ToString("N0", CultureInfo.InvariantCulture) + " Glory");
            }
        }

        private void ResolveAiCollisions()
        {
            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                float delta = RaceMath.SignedTrackDelta(Player.Progress, racer.Progress, TrackLength);
                if (Math.Abs(delta) >= 3.1f || Math.Abs(Player.Lane - racer.Lane) >= 1.75f)
                {
                    continue;
                }

                float lastCollision;
                if (!_lastAiCollision.TryGetValue(racer.Id, out lastCollision))
                {
                    lastCollision = -100f;
                }

                if (GameTime - lastCollision <= 0.8f)
                {
                    continue;
                }

                _lastAiCollision[racer.Id] = GameTime;
                float side = Player.Lane <= racer.Lane ? -1f : 1f;
                Player.Lane += side * 0.75f;
                racer.Lane -= side * 0.55f;
                racer.Speed *= 0.84f;
                HitPlayer(
                    4.5f * GetDifficultyConfig(Difficulty).Damage,
                    0.88f,
                    "Collided with " + racer.Name,
                    RaceEventCode.RacerCollision);
            }
        }

        private void AiAttack(RacerState racer)
        {
            float delta = RaceMath.SignedTrackDelta(racer.Progress, Player.Progress, TrackLength);
            if (delta > 5f && delta < 88f && racer.Aggression > 0.6f)
            {
                if (_random.NextFloat() < 0.5f)
                {
                    Mines.Add(new MineState
                    {
                        Id = racer.Id + "-mine-" + GameTime.ToString("F2", CultureInfo.InvariantCulture),
                        Progress = racer.Progress - 6f,
                        Lane = racer.Lane,
                        Owner = racer.Id,
                        ExpiresAt = GameTime + 18f,
                        Active = true
                    });
                    PushEvent(
                        RaceEventKind.Danger,
                        RaceEventCode.AiMineDropped,
                        racer.Name + " dropped a Curse Stone",
                        "Change lanes!");
                }
                else if (!AbsorbWithShield())
                {
                    Player.DisabledUntil = GameTime + 1.15f;
                    Player.Speed *= 0.84f;
                    PushEvent(
                        RaceEventKind.Danger,
                        RaceEventCode.AiBindCast,
                        racer.Name + " cast a Binding Charm",
                        "Will shaken");
                }
            }
            else if (delta < -8f && delta > -115f && _random.NextFloat() < 0.44f)
            {
                racer.ShieldUntil = GameTime + 5f;
            }
        }

        private void UseEmp()
        {
            int hits = 0;
            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                if (Math.Abs(RaceMath.SignedTrackDelta(Player.Progress, racer.Progress, TrackLength)) < 145f)
                {
                    racer.DisabledUntil = GameTime + 2.8f;
                    racer.Speed *= 0.62f;
                    hits += 1;
                }
            }

            Stats.RivalsHit += hits;
            Player.Score += hits * 300;
            PushEvent(
                RaceEventKind.Item,
                RaceEventCode.EmpUsed,
                "Silence Rune",
                hits.ToString(CultureInfo.InvariantCulture) + " rivals bound");
        }

        private void UsePulse()
        {
            RacerState target = null;
            float nearest = float.PositiveInfinity;
            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                float delta = RaceMath.SignedTrackDelta(Player.Progress, racer.Progress, TrackLength);
                if (delta > 0f && delta < 175f && delta < nearest)
                {
                    target = racer;
                    nearest = delta;
                }
            }

            if (target == null)
            {
                Player.Nitro = RaceMath.Clamp(Player.Nitro + 18f, 0f, 100f);
                PushEvent(RaceEventKind.Info, RaceEventCode.PulseMissed, "Griffin Arrow Missed", "Gale refunded");
                return;
            }

            if (target.ShieldUntil > GameTime)
            {
                target.ShieldUntil = 0f;
                PushEvent(
                    RaceEventKind.Item,
                    RaceEventCode.PulseBlocked,
                    "Arrow Blocked",
                    target.Name + "'s ward shattered");
                return;
            }

            target.Speed *= 0.48f;
            target.DisabledUntil = GameTime + 1.5f;
            target.Damage = RaceMath.Clamp(target.Damage + 16f, 0f, 100f);
            Stats.RivalsHit += 1;
            Player.Score += 650;
            PushEvent(RaceEventKind.Item, RaceEventCode.PulseHit, "Griffin Arrow Hit", target.Name);
        }

        private void HitPlayer(float damage, float speedMultiplier, string source, RaceEventCode code)
        {
            if (AbsorbWithShield())
            {
                return;
            }

            Player.Damage = RaceMath.Clamp(Player.Damage + damage, 0f, 100f);
            Player.Speed *= speedMultiplier;
            Player.Combo = 1f;
            Player.ComboExpires = 0f;
            PushEvent(
                RaceEventKind.Danger,
                code,
                source,
                "Armor " + RaceMath.JavaScriptRound(100f - Player.Damage).ToString(CultureInfo.InvariantCulture) + "%");
        }

        private bool AbsorbWithShield()
        {
            if (Player.ShieldUntil <= GameTime)
            {
                return false;
            }

            Player.ShieldUntil = Math.Max(GameTime, Player.ShieldUntil - 1.8f);
            PushEvent(RaceEventKind.Positive, RaceEventCode.ShieldBlocked, "Ward Absorbed Impact", "Ward weakened");
            return true;
        }

        private bool HasSlipstreamTarget()
        {
            for (int index = 0; index < Racers.Count; index += 1)
            {
                RacerState racer = Racers[index];
                float delta = RaceMath.SignedTrackDelta(Player.Progress, racer.Progress, TrackLength);
                if (delta > 7f && delta < 38f && Math.Abs(racer.Lane - Player.Lane) < 1.65f)
                {
                    return true;
                }
            }

            return false;
        }

        private ItemType RollItem()
        {
            int rank = GetPlayerRank();
            float value = _random.NextFloat();
            if (rank >= 5)
            {
                if (value < 0.24f) return ItemType.Emp;
                if (value < 0.48f) return ItemType.Pulse;
                if (value < 0.69f) return ItemType.Nitro;
                if (value < 0.84f) return ItemType.Shield;
                return ItemType.Repair;
            }

            if (rank <= 2)
            {
                if (value < 0.27f) return ItemType.Mine;
                if (value < 0.51f) return ItemType.Shield;
                if (value < 0.73f) return ItemType.Nitro;
                if (value < 0.87f) return ItemType.Pulse;
                return ItemType.Repair;
            }

            return AllItems[Math.Min(AllItems.Length - 1, (int)(value * AllItems.Length))];
        }

        private ObstacleState ClosestObstacleAhead(float progress, float distance)
        {
            ObstacleState closest = null;
            float closestDelta = float.PositiveInfinity;
            for (int index = 0; index < Obstacles.Count; index += 1)
            {
                ObstacleState obstacle = Obstacles[index];
                float delta = RaceMath.SignedTrackDelta(progress, obstacle.Progress, TrackLength);
                if (delta > 0f && delta < distance && delta < closestDelta)
                {
                    closest = obstacle;
                    closestDelta = delta;
                }
            }

            return closest;
        }

        private float CurveDemand(float progress)
        {
            float normalized = RaceMath.Wrap(progress, TrackLength) / TrackLength;
            float value = Math.Abs(
                (float)Math.Sin(normalized * Math.PI * 6f) * 0.55f
                + (float)Math.Sin(normalized * Math.PI * 14f + 0.7f) * 0.35f);
            return RaceMath.Clamp(value, 0f, 1f);
        }

        private static float GetObstacleRadius(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Barricade: return 2.3f;
                case ObstacleKind.Crate: return 1.6f;
                case ObstacleKind.Oil: return 1.7f;
                case ObstacleKind.Spinner: return 2.4f;
                case ObstacleKind.Gate: return 2.1f;
                default: return 1.6f;
            }
        }

        private static float GetObstacleDamage(ObstacleKind kind)
        {
            switch (kind)
            {
                case ObstacleKind.Crate: return 8f;
                case ObstacleKind.Barricade: return 13f;
                case ObstacleKind.Spinner: return 17f;
                case ObstacleKind.Gate: return 15f;
                default: return 8f;
            }
        }

        private static string GetItemLabel(ItemType item)
        {
            switch (item)
            {
                case ItemType.Nitro: return "Gale Potion";
                case ItemType.Shield: return "Guardian Ward";
                case ItemType.Emp: return "Silence Rune";
                case ItemType.Pulse: return "Griffin Arrow";
                case ItemType.Mine: return "Curse Stone";
                case ItemType.Repair: return "Cleric's Grace";
                default: return item.ToString();
            }
        }

        private PlayerState CreatePlayer()
        {
            return new PlayerState
            {
                Id = "player",
                Name = "YOU",
                Color = 0x79D9FF,
                Progress = 0f,
                Speed = 0f,
                Lane = 0f,
                TargetLane = 0f,
                BaseSpeed = 70f,
                Skill = 1f,
                Aggression = 1f,
                Damage = 0f,
                DisabledUntil = 0f,
                ShieldUntil = 0f,
                Finished = false,
                FinishTime = 0f,
                NextDecision = 0f,
                NextAttack = 0f,
                Nitro = 64f,
                DriftCharge = 0f,
                Drifting = false,
                DriftBoostUntil = 0f,
                BoostUntil = 0f,
                Slipstream = 0f,
                HasItem = false,
                Item = ItemType.Nitro,
                ItemCooldownUntil = 0f,
                SpinUntil = 0f,
                CollisionCooldownUntil = 0f,
                Combo = 1f,
                ComboExpires = 0f,
                Score = 0
            };
        }

        private void PopulateRacers()
        {
            float[] lanes = { -3.8f, 3.8f, -1.7f, 1.8f, 0f };
            for (int index = 0; index < AiTemplates.Length; index += 1)
            {
                AiTemplate template = AiTemplates[index];
                Racers.Add(new RacerState
                {
                    Id = "ai-" + index.ToString(CultureInfo.InvariantCulture),
                    Name = template.Name,
                    Color = template.Color,
                    Progress = -(index + 1) * 6.5f,
                    Speed = 0f,
                    Lane = lanes[index],
                    TargetLane = lanes[index],
                    BaseSpeed = template.Speed,
                    Skill = template.Skill,
                    Aggression = template.Aggression,
                    Damage = 0f,
                    DisabledUntil = 0f,
                    ShieldUntil = 0f,
                    Finished = false,
                    FinishTime = 0f,
                    NextDecision = 1.3f + index * 0.41f,
                    NextAttack = 14f + index * 3.1f
                });
            }
        }

        private void PopulatePickups()
        {
            float[] lanes = { -5.7f, 0f, 5.7f, -2.8f, 3.2f, 0f, -5f, 5f, -1.5f, 2.4f, -4f, 4f };
            for (int index = 0; index < lanes.Length; index += 1)
            {
                Pickups.Add(new PickupState
                {
                    Id = "pickup-" + index.ToString(CultureInfo.InvariantCulture),
                    Progress = TrackLength * (0.055f + index * 0.078f),
                    Lane = lanes[index],
                    Active = true,
                    RespawnAt = 0f
                });
            }
        }

        private void PopulateObstacles()
        {
            AddObstacle(0.09f, -4.6f, ObstacleKind.Crate);
            AddObstacle(0.13f, 3.8f, ObstacleKind.Barricade);
            AddObstacle(0.17f, 0.5f, ObstacleKind.Oil);
            AddObstacle(0.22f, 0f, ObstacleKind.Spinner);
            AddObstacle(0.28f, -3.4f, ObstacleKind.Crate);
            AddObstacle(0.31f, 4.8f, ObstacleKind.Barricade);
            AddObstacle(0.37f, 0f, ObstacleKind.Gate);
            AddObstacle(0.43f, -5.2f, ObstacleKind.Oil);
            AddObstacle(0.48f, 2.4f, ObstacleKind.Crate);
            AddObstacle(0.54f, 0f, ObstacleKind.Spinner);
            AddObstacle(0.59f, 4.5f, ObstacleKind.Barricade);
            AddObstacle(0.64f, -3.5f, ObstacleKind.Crate);
            AddObstacle(0.69f, 0f, ObstacleKind.Gate);
            AddObstacle(0.74f, 4.2f, ObstacleKind.Oil);
            AddObstacle(0.79f, -4.8f, ObstacleKind.Barricade);
            AddObstacle(0.84f, 1.8f, ObstacleKind.Crate);
            AddObstacle(0.89f, 0f, ObstacleKind.Spinner);
            AddObstacle(0.94f, -2.4f, ObstacleKind.Crate);
        }

        private void AddObstacle(float ratio, float lane, ObstacleKind kind)
        {
            int index = Obstacles.Count;
            Obstacles.Add(new ObstacleState
            {
                Id = "obstacle-" + index.ToString(CultureInfo.InvariantCulture),
                Progress = TrackLength * ratio,
                Lane = lane,
                Kind = kind,
                Phase = index * 1.71f,
                LastHitLap = -1,
                LastPassLap = -1
            });
        }

        private void PopulatePads()
        {
            Pads.Add(CreatePad("boost-1", 0.185f, -3.2f, PadKind.Boost));
            Pads.Add(CreatePad("boost-2", 0.505f, 3.2f, PadKind.Boost));
            Pads.Add(CreatePad("boost-3", 0.815f, 0f, PadKind.Boost));
            Pads.Add(CreatePad("repair-1", 0.345f, 5.8f, PadKind.Repair));
            Pads.Add(CreatePad("repair-2", 0.675f, -5.8f, PadKind.Repair));
        }

        private BoostPadState CreatePad(string id, float ratio, float lane, PadKind kind)
        {
            return new BoostPadState
            {
                Id = id,
                Progress = TrackLength * ratio,
                Lane = lane,
                Kind = kind,
                LastLap = -1
            };
        }

        private void PopulateRaceLayout(RaceLayout layout)
        {
            if (layout != null && layout.UseAuthoredPickups && layout.Pickups != null)
            {
                for (int index = 0; index < layout.Pickups.Count; index += 1)
                {
                    RacePickupLayout source = layout.Pickups[index];
                    if (source == null) continue;
                    Pickups.Add(new PickupState
                    {
                        Id = string.IsNullOrWhiteSpace(source.Id) ? "pickup-" + index.ToString(CultureInfo.InvariantCulture) : source.Id,
                        Progress = TrackLength * RaceMath.Clamp(source.ProgressRatio, 0f, 1f),
                        Lane = RaceMath.Clamp(source.Lane, -RoadHalfWidth, RoadHalfWidth),
                        Active = true,
                        RespawnAt = 0f
                    });
                }
            }
            else
            {
                PopulatePickups();
            }

            if (layout != null && layout.UseAuthoredObstacles && layout.Obstacles != null)
            {
                for (int index = 0; index < layout.Obstacles.Count; index += 1)
                {
                    RaceObstacleLayout source = layout.Obstacles[index];
                    if (source == null) continue;
                    Obstacles.Add(new ObstacleState
                    {
                        Id = string.IsNullOrWhiteSpace(source.Id) ? "obstacle-" + index.ToString(CultureInfo.InvariantCulture) : source.Id,
                        Kind = source.Kind,
                        Progress = TrackLength * RaceMath.Clamp(source.ProgressRatio, 0f, 1f),
                        Lane = RaceMath.Clamp(source.Lane, -RoadHalfWidth, RoadHalfWidth),
                        Phase = source.Phase,
                        LastHitLap = -1,
                        LastPassLap = -1
                    });
                }
            }
            else
            {
                PopulateObstacles();
            }

            if (layout != null && layout.UseAuthoredPads && layout.Pads != null)
            {
                for (int index = 0; index < layout.Pads.Count; index += 1)
                {
                    RacePadLayout source = layout.Pads[index];
                    if (source == null) continue;
                    Pads.Add(new BoostPadState
                    {
                        Id = string.IsNullOrWhiteSpace(source.Id) ? "pad-" + index.ToString(CultureInfo.InvariantCulture) : source.Id,
                        Kind = source.Kind,
                        Progress = TrackLength * RaceMath.Clamp(source.ProgressRatio, 0f, 1f),
                        Lane = RaceMath.Clamp(source.Lane, -RoadHalfWidth, RoadHalfWidth),
                        LastLap = -1
                    });
                }
            }
            else
            {
                PopulatePads();
            }
        }

        private static RaceStats CreateStats()
        {
            return new RaceStats();
        }

        private void PushEvent(
            RaceEventKind kind,
            RaceEventCode code,
            string message,
            string detail = "")
        {
            _events.Add(new RaceEvent(++_eventId, kind, code, message, detail));
            if (_events.Count > 16)
            {
                _events.RemoveAt(0);
            }
        }

        private readonly struct AiTemplate
        {
            public readonly string Name;
            public readonly int Color;
            public readonly float Speed;
            public readonly float Skill;
            public readonly float Aggression;

            public AiTemplate(string name, int color, float speed, float skill, float aggression)
            {
                Name = name;
                Color = color;
                Speed = speed;
                Skill = skill;
                Aggression = aggression;
            }
        }
    }
}
