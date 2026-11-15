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

        [Header("2D XY Hitbox (Local +X Facing, HurtboxScale = 1.0)")]
        public Vector2 hitboxCenterOffset = new Vector2(0.68f, 1.18f);
        public Vector2 hitboxSize = new Vector2(0.78f, 0.42f);

        public int FirstActiveTick => startupTicks + 1;
        public int LastActiveTick => startupTicks + activeTicks;
        public int TotalTicks => startupTicks + activeTicks + recoveryTicks;

        /// <summary>
        /// Returns true when 1-based attack stateTicks falls within [startupTicks + 1 .. startupTicks + activeTicks].
        /// </summary>
        public bool IsActiveTick(int stateTicks)
        {
            return stateTicks >= FirstActiveTick && stateTicks <= LastActiveTick;
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
            int chip = 0)
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
            hitboxCenterOffset = centerOffset;
            hitboxSize = size;
        }
    }
}
