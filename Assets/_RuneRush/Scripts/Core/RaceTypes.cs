using System;
using System.Collections.Generic;

namespace RuneRush
{
    public enum Difficulty
    {
        Rookie,
        Pro,
        Apex
    }

    public enum RacePhase
    {
        Menu,
        Countdown,
        Racing,
        Paused,
        Finished
    }

    public enum ItemType
    {
        Nitro,
        Shield,
        Emp,
        Pulse,
        Mine,
        Repair
    }

    public enum ObstacleKind
    {
        Barricade,
        Crate,
        Oil,
        Spinner,
        Gate
    }

    public enum PadKind
    {
        Boost,
        Repair
    }

    public enum RaceEventKind
    {
        Info,
        Positive,
        Danger,
        Item,
        Lap,
        Finish
    }

    public enum RaceEventCode
    {
        RaceInitialized,
        CountdownTick,
        RaceStarted,
        NitroPotionUsed,
        ShieldActivated,
        RepairUsed,
        EmpUsed,
        PulseBlocked,
        PulseHit,
        PulseMissed,
        MineDropped,
        ResetToTrack,
        DriftBoost,
        ItemPickedUp,
        BoostPadTriggered,
        RepairPadTriggered,
        OilSpin,
        ObstacleHit,
        NearMiss,
        MineHitPlayer,
        MineHitRival,
        AiMineDropped,
        AiBindCast,
        ShieldBlocked,
        RacerCollision,
        LapCompleted,
        RaceFinished
    }

    [Serializable]
    public struct RaceInputFrame
    {
        public float Throttle;
        public float Brake;
        public float Steer;
        public bool Drift;
        public bool Nitro;
        public bool UseItem;
        public bool Reset;

        public static RaceInputFrame Idle
        {
            get { return default; }
        }
    }

    [Serializable]
    public class RacerState
    {
        public string Id = string.Empty;
        public string Name = string.Empty;
        public int Color;
        public float Progress;
        public float Speed;
        public float Lane;
        public float TargetLane;
        public float BaseSpeed;
        public float Skill;
        public float Aggression;
        public float Damage;
        public float DisabledUntil;
        public float ShieldUntil;
        public bool Finished;
        public float FinishTime;
        public float NextDecision;
        public float NextAttack;
    }

    [Serializable]
    public sealed class PlayerState : RacerState
    {
        public float Nitro;
        public float DriftCharge;
        public bool Drifting;
        public float DriftBoostUntil;
        public float BoostUntil;
        public float Slipstream;
        public bool HasItem;
        public ItemType Item;
        public float ItemCooldownUntil;
        public float SpinUntil;
        public float CollisionCooldownUntil;
        public float Combo;
        public float ComboExpires;
        public int Score;
    }

    [Serializable]
    public sealed class PickupState
    {
        public string Id = string.Empty;
        public float Progress;
        public float Lane;
        public bool Active;
        public float RespawnAt;
    }

    [Serializable]
    public sealed class ObstacleState
    {
        public string Id = string.Empty;
        public ObstacleKind Kind;
        public float Progress;
        public float Lane;
        public float Phase;
        public int LastHitLap;
        public int LastPassLap;
    }

    [Serializable]
    public sealed class MineState
    {
        public string Id = string.Empty;
        public float Progress;
        public float Lane;
        public string Owner = string.Empty;
        public float ExpiresAt;
        public bool Active;
    }

    [Serializable]
    public sealed class BoostPadState
    {
        public string Id = string.Empty;
        public float Progress;
        public float Lane;
        public PadKind Kind;
        public int LastLap;
    }

    /// <summary>
    /// Scene-authoring data expressed in track-relative coordinates.  It deliberately
    /// contains no Unity object references so the deterministic race simulation can
    /// stay independent from the Unity scene.
    /// </summary>
    [Serializable]
    public sealed class RaceLayout
    {
        public bool UseAuthoredPickups;
        public bool UseAuthoredObstacles;
        public bool UseAuthoredPads;
        public List<RacePickupLayout> Pickups = new List<RacePickupLayout>();
        public List<RaceObstacleLayout> Obstacles = new List<RaceObstacleLayout>();
        public List<RacePadLayout> Pads = new List<RacePadLayout>();
    }

    [Serializable]
    public sealed class RacePickupLayout
    {
        public string Id = string.Empty;
        public float ProgressRatio;
        public float Lane;
    }

    [Serializable]
    public sealed class RaceObstacleLayout
    {
        public string Id = string.Empty;
        public ObstacleKind Kind;
        public float ProgressRatio;
        public float Lane;
        public float Phase;
    }

    [Serializable]
    public sealed class RacePadLayout
    {
        public string Id = string.Empty;
        public PadKind Kind;
        public float ProgressRatio;
        public float Lane;
    }

    [Serializable]
    public sealed class RaceStats
    {
        public float Elapsed;
        public float CurrentLapTime;
        public float LastLap;
        public float BestLap;
        public float TopSpeed;
        public int NearMisses;
        public int ItemsUsed;
        public int RivalsHit;
        public float DriftDistance;
    }

    [Serializable]
    public sealed class RaceEvent
    {
        public int Id;
        public RaceEventKind Kind;
        public RaceEventCode Code;
        public string Message = string.Empty;
        public string Detail = string.Empty;

        public RaceEvent(int id, RaceEventKind kind, RaceEventCode code, string message, string detail = "")
        {
            Id = id;
            Kind = kind;
            Code = code;
            Message = message ?? string.Empty;
            Detail = detail ?? string.Empty;
        }
    }

    [Serializable]
    public struct DifficultyConfig
    {
        public float AiSpeed;
        public float AiGrip;
        public float Damage;
        public float RubberBand;
        public string Label;

        public DifficultyConfig(float aiSpeed, float aiGrip, float damage, float rubberBand, string label)
        {
            AiSpeed = aiSpeed;
            AiGrip = aiGrip;
            Damage = damage;
            RubberBand = rubberBand;
            Label = label;
        }
    }

    public sealed class RaceSnapshot
    {
        public RacePhase Phase;
        public Difficulty Difficulty;
        public float GameTime;
        public float Countdown;
        public int Rank;
        public int Lap;
        public int Checkpoint;
        public string Winner = string.Empty;
        public PlayerState Player;
        public IReadOnlyList<RacerState> Racers;
        public RaceStats Stats;
    }
}
