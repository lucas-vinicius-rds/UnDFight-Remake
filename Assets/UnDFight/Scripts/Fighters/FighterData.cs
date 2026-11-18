using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Immutable-at-runtime configuration asset describing a playable fighter's physical & combat stats.
    /// </summary>
    [CreateAssetMenu(fileName = "FighterData_New", menuName = "UnDFight/Fighter Data")]
    public class FighterData : ScriptableObject
    {
        [Header("Identity")]
        public string fighterName = "Fighter";
        public FightingArchetype archetype = FightingArchetype.HeavyBrawler;
        public GameObject prefab;
        public Sprite portrait;

        [Header("Physical Dimensions (Reference: 1.81 m / 92 kg)")]
        [Min(0.5f)] public float heightMeters = 1.81f;
        [Min(20f)] public float weightKg = 92f;
        [Tooltip("True when the FBX mesh was already exported at heightMeters in Blender (avoids double-scaling visualRoot).")]
        public bool fbxHeightPrebaked = false;

        [Header("Combat & Movement Base Stats (at Reference Weight)")]
        [Min(100)] public int maxHealth = 1000;
        [Min(0.5f)] public float walkForwardSpeed = 3.7f;
        [Min(0.5f)] public float walkBackwardSpeed = 2.8f;
        [Min(0.5f)] public float crouchWalkForwardSpeed = 2.0f;
        [Min(0.5f)] public float crouchWalkBackwardSpeed = 1.55f;
        [Min(1f)] public float dashForwardSpeed = 7.2f;
        [Min(1)] public int dashForwardTicks = 14;
        [Min(1f)] public float dashBackwardSpeed = 6.2f;
        [Min(1)] public int dashBackwardTicks = 16;
        [Min(0)] public int backdashInvulnerableTicks = 5;
        [Min(1f)] public float jumpForce = 11.8f;
        [Min(1f)] public float baseGravity = 30.0f;

        [Header("Collision / Hurtbox")]
        [Range(0.3f, 0.9f)] public float crouchHeightRatio = 0.62f;
        [Min(0.1f)] public float pushboxRadius = 0.36f;

        public const int ExSuperCost = 50;
        public const int SpecialSuperCost = 100;

        [Header("Moveset (60 Hz Frame Data)")]
        public MoveData lightPunch;
        public MoveData heavyPunch;
        public MoveData lightKick;
        public MoveData heavyKick;
        public MoveData crouchKick;
        public MoveData jumpAttack;
        public MoveData throwMove;
        public MoveData exHeavyPunch;
        public MoveData exHeavyKick;
        public MoveData specialMove;
        public MoveData comboPunchFinisher;
        public MoveData comboKickFinisher;

        [Header("Combo Strings (MK-Style Dial-a-Combo)")]
        public ComboStringDefinition[] comboStrings;

        public WeightClass WeightCategory => FighterScaling.GetWeightClass(weightKg);
        public float ModelScale => FighterScaling.ComputeModelScale(heightMeters);
        public float VisualScale => fbxHeightPrebaked ? 1.0f : ModelScale;
        public float HurtboxScale => FighterScaling.ComputeHurtboxScale(heightMeters);
        public float EffectiveGravity => FighterScaling.ComputeEffectiveGravity(baseGravity, weightKg);
        public float EffectiveWalkForwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(walkForwardSpeed, weightKg);
        public float EffectiveWalkBackwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(walkBackwardSpeed, weightKg);
        public float EffectiveCrouchWalkForwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(crouchWalkForwardSpeed, weightKg);
        public float EffectiveCrouchWalkBackwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(crouchWalkBackwardSpeed, weightKg);
        public float EffectiveDashForwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(dashForwardSpeed, weightKg);
        public float EffectiveDashBackwardSpeed => FighterScaling.ComputeEffectiveWalkSpeed(dashBackwardSpeed, weightKg);
        public float KnockbackMultiplier => FighterScaling.ComputeKnockbackMultiplier(weightKg);

        public MoveData GetMoveData(AttackType attackType)
        {
            return attackType switch
            {
                AttackType.LightPunch => lightPunch,
                AttackType.HeavyPunch => heavyPunch,
                AttackType.LightKick => lightKick,
                AttackType.HeavyKick => heavyKick,
                AttackType.CrouchKick => crouchKick,
                AttackType.JumpAttack => jumpAttack,
                AttackType.Throw => throwMove,
                AttackType.ExHeavyPunch => exHeavyPunch,
                AttackType.ExHeavyKick => exHeavyKick,
                AttackType.Special => specialMove,
                AttackType.ComboPunchFinisher => comboPunchFinisher,
                AttackType.ComboKickFinisher => comboKickFinisher,
                _ => null
            };
        }

        public MoveData GetExMoveData(AttackType baseAttack)
        {
            return baseAttack switch
            {
                AttackType.HeavyPunch or AttackType.ExHeavyPunch => exHeavyPunch,
                AttackType.HeavyKick or AttackType.ExHeavyKick => exHeavyKick,
                _ => null
            };
        }
    }
}
