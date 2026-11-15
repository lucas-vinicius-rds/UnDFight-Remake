using UnityEngine;

namespace UnDFight
{
    public enum HitHeight
    {
        High = 0,
        Mid = 1,
        Low = 2
    }

    public enum HitOutcomeType
    {
        None = 0,
        Hit = 1,
        Blocked = 2,
        KnockDown = 3
    }

    public enum MatchPhase
    {
        RoundIntro = 0,
        Fighting = 1,
        RoundOver = 2,
        MatchOver = 3
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
    }
}
