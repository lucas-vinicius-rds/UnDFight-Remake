using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Single source of truth for height and weight scaling formulas across models, hurtboxes, and physics.
    /// </summary>
    public static class FighterScaling
    {
        public const float ReferenceHeightMeters = 1.81f;
        public const float ReferenceWeightKg = 92.0f;

        // Adjustable formula exponents & clamps
        public const float GravityWeightExponent = 0.35f;
        public const float WalkSpeedWeightExponent = -0.35f;
        public const float KnockbackWeightExponent = -0.60f;
        public const float MinScaleFactor = 0.5f;
        public const float MaxScaleFactor = 2.0f;

        /// <summary>
        /// Visual model uniform scale relative to ReferenceHeightMeters (1.81 m = 1.0).
        /// </summary>
        public static float ComputeModelScale(float heightMeters)
        {
            float safeHeight = Mathf.Max(0.1f, heightMeters);
            return Mathf.Clamp(safeHeight / ReferenceHeightMeters, MinScaleFactor, MaxScaleFactor);
        }

        /// <summary>
        /// Hurtbox uniform scale relative to ReferenceHeightMeters (1.81 m = 1.0).
        /// </summary>
        public static float ComputeHurtboxScale(float heightMeters)
        {
            return ComputeModelScale(heightMeters);
        }

        /// <summary>
        /// Effective gravity scaled by weight: heavier fighters fall faster.
        /// </summary>
        public static float ComputeEffectiveGravity(float baseGravity, float weightKg)
        {
            float ratio = Mathf.Max(1f, weightKg) / ReferenceWeightKg;
            return baseGravity * Mathf.Pow(ratio, GravityWeightExponent);
        }

        /// <summary>
        /// Effective walk speed scaled by weight: heavier fighters move slightly slower.
        /// </summary>
        public static float ComputeEffectiveWalkSpeed(float baseSpeed, float weightKg)
        {
            float ratio = Mathf.Max(1f, weightKg) / ReferenceWeightKg;
            return baseSpeed * Mathf.Pow(ratio, WalkSpeedWeightExponent);
        }

        /// <summary>
        /// Knockback multiplier scaled by weight: heavier fighters receive less knockback.
        /// </summary>
        public static float ComputeKnockbackMultiplier(float weightKg)
        {
            float ratio = Mathf.Max(1f, weightKg) / ReferenceWeightKg;
            return Mathf.Pow(ratio, KnockbackWeightExponent);
        }
    }
}
