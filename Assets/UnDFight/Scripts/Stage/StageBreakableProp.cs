using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Generic, reusable breakable stage prop driven by combat hits or fighter knockdowns.
    /// Uses a pre-warmed pool of 5..8 PhysX debris fragments on the StageDebris layer
    /// and resets cleanly at the start of each round or in Training mode.
    /// </summary>
    public class StageBreakableProp : StagePropBase
    {
        [Header("Integrity & Combat Trigger")]
        [SerializeField, Min(1)] private int maxIntegrity = 2;
        [SerializeField, Min(0.2f)] private float triggerRadiusX = 1.55f;
        [SerializeField, Min(0.1f)] private float maxZDistanceFromFightPlane = 1.85f;
        [SerializeField] private float fightPlaneZ = 0f;
        [SerializeField, Min(0)] private int impactCooldownTicks = 2;

        [Header("Visuals & Debris Pool")]
        [SerializeField] private StagePropMaterialType materialType = StagePropMaterialType.WoodOrCrate;
        [SerializeField] private GameObject intactVisualRoot;
        [SerializeField] private Collider intactCollider;
        [SerializeField] private Transform debrisPoolRoot;
        [SerializeField, Range(5, 8)] private int generatedFragmentCount = 6;

        [Header("PhysX Break Tuning")]
        [SerializeField, Min(0f)] private float explosionForce = 4.8f;
        [SerializeField, Min(0.2f)] private float explosionRadius = 1.4f;
        [SerializeField, Min(0f)] private float directionalImpulse = 3.2f;
        [SerializeField, Min(0f)] private float upwardImpulse = 2.6f;
        [SerializeField, Min(0f)] private float torqueStrength = 9.0f;
        [SerializeField, Min(0.4f)] private float fragmentFreezeSeconds = 2.6f;
        [SerializeField, Min(0.8f)] private float fragmentHideSeconds = 4.2f;

        [Header("Training Auto-Respawn")]
        [SerializeField] private bool autoRespawnInTraining = true;
        [SerializeField, Min(1f)] private float trainingRespawnSeconds = 7.5f;

        private readonly List<StageDebrisFragment> _fragments = new List<StageDebrisFragment>();

        private int _currentIntegrity;
        private bool _isBroken;
        private bool _initialized;
        private int _lastImpactTick = -1000;
        private float _brokenElapsed;
        private float _wobbleTimer;
        private float _wobbleDir = 1f;
        private Quaternion _initialIntactLocalRot = Quaternion.identity;

        public override bool IsBroken => _isBroken;
        public int CurrentIntegrity => _currentIntegrity;
        public int MaxIntegrity => maxIntegrity;
        public float TriggerRadiusX => triggerRadiusX;
        public float MaxZDistanceFromFightPlane => maxZDistanceFromFightPlane;
        public StagePropMaterialType MaterialType => materialType;
        public IReadOnlyList<StageDebrisFragment> Fragments => _fragments;

        public event Action<StageBreakableProp> OnPropBroken;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        public void InitializeIfNeeded()
        {
            if (_initialized)
            {
                return;
            }

            _currentIntegrity = Mathf.Max(1, maxIntegrity);
            CacheIntactVisuals(intactVisualRoot, debrisPoolRoot, out _initialIntactLocalRot);
            EnsureDebrisPoolFragments(intactVisualRoot, ref debrisPoolRoot, _fragments, generatedFragmentCount);

            for (int i = 0; i < _fragments.Count; i++)
            {
                if (_fragments[i] != null)
                {
                    _fragments[i].ResetFragment();
                }
            }

            SetIntactVisualsVisible(intactVisualRoot, intactCollider, true);
            _initialized = true;
        }

        public void ConfigureForRuntime(
            int integrity,
            float radiusX,
            float zTolerance,
            StagePropMaterialType matType)
        {
            maxIntegrity = Mathf.Max(1, integrity);
            triggerRadiusX = Mathf.Max(0.2f, radiusX);
            maxZDistanceFromFightPlane = Mathf.Max(0.1f, zTolerance);
            materialType = matType;
            _initialized = false;
            InitializeIfNeeded();
        }

        public override bool TryHandleCombatImpact(
            Vector2 contactPoint,
            int impactDamage,
            float pushDirX,
            bool isHeavyOrSpecial,
            Vector2 attackerPos,
            Vector2 defenderPos,
            int simulationTick = -1)
        {
            InitializeIfNeeded();

            if (_isBroken || impactDamage <= 0)
            {
                return false;
            }

            if (simulationTick >= 0 && simulationTick - _lastImpactTick < impactCooldownTicks)
            {
                return false;
            }

            Vector3 propPos = transform.position;
            float dz = Mathf.Abs(propPos.z - fightPlaneZ);
            if (dz > maxZDistanceFromFightPlane)
            {
                return false;
            }

            float minDx = Mathf.Min(
                Mathf.Abs(contactPoint.x - propPos.x),
                Mathf.Abs(defenderPos.x - propPos.x));
            if (minDx > triggerRadiusX)
            {
                return false;
            }

            if (simulationTick >= 0)
            {
                _lastImpactTick = simulationTick;
            }
            _currentIntegrity = Mathf.Max(0, _currentIntegrity - impactDamage);

            float dirSign = Mathf.Abs(pushDirX) > 0.01f
                ? Mathf.Sign(pushDirX)
                : (propPos.x >= contactPoint.x ? 1f : -1f);

            if (_currentIntegrity <= 0)
            {
                BreakProp(contactPoint, dirSign);
            }
            else
            {
                TriggerIntactImpactFeedback(contactPoint, dirSign, isHeavyOrSpecial);
            }

            return true;
        }

        public void ForceBreak(float pushDirX = 1f)
        {
            InitializeIfNeeded();
            if (_isBroken)
            {
                return;
            }

            _currentIntegrity = 0;
            Vector3 p = transform.position;
            BreakProp(new Vector2(p.x, p.y + 0.5f), Mathf.Abs(pushDirX) > 0.01f ? Mathf.Sign(pushDirX) : 1f);
        }

        public override void ResetProp()
        {
            InitializeIfNeeded();

            _currentIntegrity = Mathf.Max(1, maxIntegrity);
            _isBroken = false;
            _brokenElapsed = 0f;
            _wobbleTimer = 0f;
            _lastImpactTick = -1000;

            if (intactVisualRoot != null)
            {
                intactVisualRoot.transform.localRotation = _initialIntactLocalRot;
            }

            for (int i = 0; i < _fragments.Count; i++)
            {
                if (_fragments[i] != null)
                {
                    _fragments[i].ResetFragment();
                }
            }

            SetIntactVisualsVisible(intactVisualRoot, intactCollider, true);
        }

        private void Update()
        {
            if (_wobbleTimer > 0f && !_isBroken && intactVisualRoot != null)
            {
                _wobbleTimer = Mathf.Max(0f, _wobbleTimer - Time.deltaTime);
                float t = _wobbleTimer / 0.24f;
                float angle = Mathf.Sin((1f - t) * Mathf.PI * 3.5f) * 7.5f * t * _wobbleDir;
                intactVisualRoot.transform.localRotation = _initialIntactLocalRot * Quaternion.Euler(0f, 0f, -angle);
                if (_wobbleTimer <= 0f)
                {
                    intactVisualRoot.transform.localRotation = _initialIntactLocalRot;
                }
            }

            if (_isBroken && autoRespawnInTraining && GameSessionState.IsTrainingMode)
            {
                _brokenElapsed += Time.deltaTime;
                if (_brokenElapsed >= trainingRespawnSeconds)
                {
                    ResetProp();
                }
            }
        }

        private void TriggerIntactImpactFeedback(Vector2 contactPoint, float dirSign, bool isHeavyOrSpecial)
        {
            _wobbleTimer = 0.24f;
            _wobbleDir = dirSign;

            Vector3 burstPos = GetWorldVisualCenter(intactVisualRoot);
            CombatVfxController vfx = ResolveVfxController();
            if (vfx != null)
            {
                vfx.SpawnSparkBurst(new Vector2(burstPos.x, burstPos.y), HitOutcomeType.Hit, isHeavyOrSpecial);
            }

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.PlayPropImpact(materialType);
            }
        }

        private void BreakProp(Vector2 impactOrigin2D, float pushDirX)
        {
            _isBroken = true;
            _brokenElapsed = 0f;
            _wobbleTimer = 0f;
            if (intactVisualRoot != null)
            {
                intactVisualRoot.transform.localRotation = _initialIntactLocalRot;
            }

            SetIntactVisualsVisible(intactVisualRoot, intactCollider, false);

            Vector3 worldCenter = GetWorldVisualCenter(intactVisualRoot);
            Vector3 explosionOrigin = new Vector3(
                Mathf.Lerp(impactOrigin2D.x, worldCenter.x, 0.35f),
                Mathf.Max(transform.position.y + 0.1f, worldCenter.y - 0.15f),
                worldCenter.z - 0.15f);

            LaunchDebrisBurst(
                _fragments,
                explosionOrigin,
                pushDirX,
                explosionForce,
                explosionRadius,
                directionalImpulse,
                upwardImpulse,
                torqueStrength,
                fragmentFreezeSeconds,
                fragmentHideSeconds);

            CombatVfxController vfx = ResolveVfxController();
            if (vfx != null)
            {
                vfx.SpawnSparkBurst(new Vector2(worldCenter.x, worldCenter.y), HitOutcomeType.KnockDown, isSpecial: false);
            }

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.PlayPropBreak(materialType);
            }

            OnPropBroken?.Invoke(this);
        }
    }
}
