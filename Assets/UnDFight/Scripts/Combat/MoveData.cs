using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Immutable-at-runtime frame data and deterministic 2D XY hitbox definition for a combat move (60 Hz ticks).
    /// </summary>
    [CreateAssetMenu(fileName = "MoveData_New", menuName = "UnDFight/Move Data")]
    public class MoveData : ScriptableObject
    {
        [Header("Identity")]
        public string moveName = "Move";
        public AttackType attackType = AttackType.LightPunch;

        [Header("Frame Data (60 Hz Ticks)")]
        [Min(1)] public int startupTicks = 4;
        [Min(1)] public int activeTicks = 3;
        [Min(1)] public int recoveryTicks = 9;

        [Header("Combat Properties")]
        [Min(0)] public int damage = 50;
        [Min(0)] public int chipDamage = 0;
        [Min(1)] public int hitstunTicks = 12;
        [Min(1)] public int blockstunTicks = 8;
        [Min(0)] public int hitstopTicks = 4;
        [Min(0f)] public float knockback = 1.4f;
        [Range(0.1f, 1f)] public float blockKnockbackFactor = 0.5f;
        public HitHeight hitHeight = HitHeight.Mid;
        public bool causesKnockdown = false;
        public bool isThrow = false;
        public bool airOnly = false;

        [Header("2D XY Hitbox (Local +X Facing, HurtboxScale = 1.0)")]
        public Vector2 hitboxCenterOffset = new Vector2(0.68f, 1.18f);
        public Vector2 hitboxSize = new Vector2(0.78f, 0.42f);

        [Header("Optional Forward Lunge")]
        [Min(0f)] public float lungeSpeed = 0f;

        [Header("Special / EX Mechanics (60 Hz)")]
        public SpecialMechanicType specialMechanic = SpecialMechanicType.None;
        [Min(0)] public int superCost = 0;
        public bool isExMove = false;
        [Min(0)] public int armorStartTick = 0;
        [Min(0)] public int armorEndTick = 0;
        public bool antiAirInvulnerable = false;
        [Min(0)] public int antiAirStartTick = 0;
        [Min(0)] public int antiAirEndTick = 0;
        [Min(0)] public int secondActiveStartTick = 0;
        [Min(0)] public int secondActiveEndTick = 0;
        [Min(0)] public int secondHitDamage = 0;
        public bool spawnsProjectile = false;
        [Min(0.1f)] public float projectileSpeed = 9.5f;
        [Min(1)] public int projectileLifetimeTicks = 45;
        public Vector2 projectileSpawnOffset = new Vector2(0.70f, 1.15f);
        public Vector2 projectileHitboxSize = new Vector2(0.55f, 0.45f);
        [Min(0)] public int superFreezeTicks = 0;

        public int FirstActiveTick => startupTicks + 1;
        public int LastActiveTick => startupTicks + activeTicks;
        public int TotalTicks => startupTicks + activeTicks + recoveryTicks;

        public bool IsPrimaryActiveTick(int stateTicks)
        {
            return stateTicks >= FirstActiveTick && stateTicks <= LastActiveTick;
        }

        public bool IsSecondaryActiveTick(int stateTicks)
        {
            return secondActiveStartTick > 0 && stateTicks >= secondActiveStartTick && stateTicks <= secondActiveEndTick;
        }

        /// <summary>
        /// Returns true when 1-based attack stateTicks falls within primary or secondary active windows.
        /// </summary>
        public bool IsActiveTick(int stateTicks)
        {
            return IsPrimaryActiveTick(stateTicks) || IsSecondaryActiveTick(stateTicks);
        }

        public bool HasArmorAtTick(int stateTicks)
        {
            return armorStartTick > 0 && armorEndTick >= armorStartTick && stateTicks >= armorStartTick && stateTicks <= armorEndTick;
        }

        public bool HasAntiAirInvulnerabilityAtTick(int stateTicks)
        {
            if (!antiAirInvulnerable)
            {
                return false;
            }
            if (antiAirEndTick > 0 && antiAirEndTick >= antiAirStartTick)
            {
                return stateTicks >= antiAirStartTick && stateTicks <= antiAirEndTick;
            }
            return stateTicks <= LastActiveTick;
        }

        /// <summary>
        /// Computes the world-space XY AABB hitbox for a fighter at simPosition facing +X or -X.
        /// </summary>
        public Rect GetWorldHitboxRect(Vector2 simPosition, int facingSign, float hurtboxScale)
        {
            float sign = facingSign >= 0 ? 1f : -1f;
            float scale = Mathf.Max(0.1f, hurtboxScale);
            float width = Mathf.Max(0.05f, hitboxSize.x) * scale;
            float height = Mathf.Max(0.05f, hitboxSize.y) * scale;
            float centerX = simPosition.x + sign * hitboxCenterOffset.x * scale;
            float centerY = simPosition.y + hitboxCenterOffset.y * scale;
            return new Rect(centerX - width * 0.5f, centerY - height * 0.5f, width, height);
        }

        public void Configure(
            string nameValue,
            AttackType type,
            int startup,
            int active,
            int recovery,
            int dmg,
            int hitstun,
            int blockstun,
            int hitstop,
            float kb,
            HitHeight height,
            bool knockdown,
            Vector2 centerOffset,
            Vector2 size,
            int chip = 0,
            float lungeSpeedValue = 0f,
            bool isThrowValue = false,
            bool airOnlyValue = false)
        {
            moveName = nameValue;
            attackType = type;
            startupTicks = Mathf.Max(1, startup);
            activeTicks = Mathf.Max(1, active);
            recoveryTicks = Mathf.Max(1, recovery);
            damage = Mathf.Max(0, dmg);
            chipDamage = Mathf.Max(0, chip);
            hitstunTicks = Mathf.Max(1, hitstun);
            blockstunTicks = Mathf.Max(1, blockstun);
            hitstopTicks = Mathf.Max(0, hitstop);
            knockback = Mathf.Max(0f, kb);
            hitHeight = height;
            causesKnockdown = knockdown;
            isThrow = isThrowValue;
            airOnly = airOnlyValue;
            hitboxCenterOffset = centerOffset;
            hitboxSize = size;
            lungeSpeed = Mathf.Max(0f, lungeSpeedValue);
            specialMechanic = SpecialMechanicType.None;
            superCost = 0;
            isExMove = false;
            armorStartTick = 0;
            armorEndTick = 0;
            antiAirInvulnerable = false;
            antiAirStartTick = 0;
            antiAirEndTick = 0;
            secondActiveStartTick = 0;
            secondActiveEndTick = 0;
            secondHitDamage = 0;
            spawnsProjectile = false;
            projectileSpeed = 9.5f;
            projectileLifetimeTicks = 45;
            projectileSpawnOffset = new Vector2(0.70f, 1.15f);
            projectileHitboxSize = new Vector2(0.55f, 0.45f);
            superFreezeTicks = 0;
        }
    }
}
