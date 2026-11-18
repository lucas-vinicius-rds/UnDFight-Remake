using UnityEngine;

namespace UnDFight
{
    public enum HitHeight
    {
        High = 0,
        Mid = 1,
        Low = 2,
        Overhead = 3,
        Unblockable = 4
    }

    public enum HitOutcomeType
    {
        None = 0,
        Hit = 1,
        Blocked = 2,
        KnockDown = 3,
        ThrowTech = 4
    }

    public enum MatchPhase
    {
        RoundIntro = 0,
        Fighting = 1,
        RoundOver = 2,
        MatchOver = 3
    }

    public enum SpecialMechanicType
    {
        None = 0,
        ArmoredSmash = 1,
        MultiHitRush = 2,
        ProjectileShot = 3,
        AntiAirSpin = 4
    }

    public struct CombatHitEvent
    {
        public FighterController Attacker;
        public FighterController Defender;
        public MoveData Move;
        public HitOutcomeType Outcome;
        public Vector2 ContactPoint;
        public int DamageDealt;
        public int HitstopTicks;
        public float KnockbackVelocity;
        public bool WasTrade;
        public int ComboCount;
        public int ComboDamage;
        public bool IsSpecial;
        public bool IsExMove;
        public bool WasArmorAbsorbed;
        public string ComboStringName;
    }
}
