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
        private static Mesh s_sharedCubeMesh;

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
        private readonly List<Renderer> _cachedIntactRenderers = new List<Renderer>();
        private readonly List<Collider> _cachedIntactColliders = new List<Collider>();

        private int _currentIntegrity;
        private bool _isBroken;
        private bool _initialized;
        private int _lastImpactTick = -1000;
        private float _brokenElapsed;
        private float _wobbleTimer;
        private float _wobbleDir = 1f;
        private Quaternion _initialIntactLocalRot = Quaternion.identity;
        private CombatVfxController _vfxController;

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
            CacheIntactVisuals();
            EnsureDebrisPool();

            for (int i = 0; i < _fragments.Count; i++)
            {
                if (_fragments[i] != null)
                {
                    _fragments[i].ResetFragment();
                }
            }

            SetIntactVisible(true);
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

            SetIntactVisible(true);
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

            Vector3 burstPos = GetWorldVisualCenter();
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

            SetIntactVisible(false);

            Vector3 worldCenter = GetWorldVisualCenter();
            Vector3 explosionOrigin = new Vector3(
                Mathf.Lerp(impactOrigin2D.x, worldCenter.x, 0.35f),
                Mathf.Max(transform.position.y + 0.1f, worldCenter.y - 0.15f),
                worldCenter.z - 0.15f);
            Vector3 pushVec = new Vector3(pushDirX * directionalImpulse, upwardImpulse, UnityEngine.Random.Range(0.2f, 0.8f));

            for (int i = 0; i < _fragments.Count; i++)
            {
                StageDebrisFragment frag = _fragments[i];
                if (frag == null)
                {
                    continue;
                }

                Vector3 jitter = new Vector3(
                    UnityEngine.Random.Range(-0.55f, 0.55f),
                    UnityEngine.Random.Range(0.1f, 0.65f),
                    UnityEngine.Random.Range(-0.45f, 0.65f));
                frag.Launch(
                    explosionOrigin,
                    explosionForce,
                    explosionRadius,
                    pushVec + jitter,
                    torqueStrength,
                    fragmentFreezeSeconds,
                    fragmentHideSeconds);
            }

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

        private void CacheIntactVisuals()
        {
            _cachedIntactRenderers.Clear();
            _cachedIntactColliders.Clear();

            if (intactVisualRoot != null)
            {
                _initialIntactLocalRot = intactVisualRoot.transform.localRotation;
                return;
            }

            Transform poolTransform = debrisPoolRoot != null ? debrisPoolRoot : transform.Find("DebrisPool");
            Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < allRenderers.Length; i++)
            {
                Renderer r = allRenderers[i];
                if (r == null) continue;
                if (poolTransform != null && r.transform.IsChildOf(poolTransform)) continue;
                _cachedIntactRenderers.Add(r);
            }

            Collider[] allColliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider c = allColliders[i];
                if (c == null) continue;
                if (poolTransform != null && c.transform.IsChildOf(poolTransform)) continue;
                _cachedIntactColliders.Add(c);
            }
        }

        private void SetIntactVisible(bool visible)
        {
            if (intactVisualRoot != null)
            {
                intactVisualRoot.SetActive(visible);
            }
            else
            {
                for (int i = 0; i < _cachedIntactRenderers.Count; i++)
                {
                    if (_cachedIntactRenderers[i] != null)
                    {
                        _cachedIntactRenderers[i].enabled = visible;
                    }
                }

                for (int i = 0; i < _cachedIntactColliders.Count; i++)
                {
                    if (_cachedIntactColliders[i] != null)
                    {
                        _cachedIntactColliders[i].enabled = visible;
                    }
                }
            }

            if (intactCollider != null)
            {
                intactCollider.enabled = visible;
            }
        }

        private void EnsureDebrisPool()
        {
            _fragments.Clear();

            if (debrisPoolRoot == null)
            {
                Transform existingPool = transform.Find("DebrisPool");
                if (existingPool != null)
                {
                    debrisPoolRoot = existingPool;
                }
            }

            if (debrisPoolRoot != null)
            {
                debrisPoolRoot.GetComponentsInChildren(true, _fragments);
            }

            if (_fragments.Count > 0)
            {
                for (int i = 0; i < _fragments.Count; i++)
                {
                    _fragments[i].EnsureComponents();
                    _fragments[i].CacheInitialTransform();
                }
                return;
            }

            // Generic fallback pool so dragging this component onto any scene mesh works immediately
            var poolGo = new GameObject("DebrisPool");
            poolGo.transform.SetParent(transform, false);
            debrisPoolRoot = poolGo.transform;

            Bounds localBounds = ComputeIntactBoundsLocal(out Material primaryMat);
            int count = Mathf.Clamp(generatedFragmentCount, 5, 8);
            Mesh cubeMesh = GetSharedCubeMesh();

            for (int i = 0; i < count; i++)
            {
                var shardGo = new GameObject($"Fragment_{i}");
                shardGo.transform.SetParent(debrisPoolRoot, false);
                shardGo.layer = StagePhysicsLayers.StageDebrisLayer;

                float nx = ((i % 3) - 1) * 0.28f;
                float ny = 0.25f + (i / 3) * 0.28f;
                float nz = ((i % 2) == 0 ? -0.18f : 0.18f);
                shardGo.transform.localPosition = new Vector3(
                    localBounds.center.x + localBounds.size.x * nx,
                    Mathf.Max(0.12f, localBounds.min.y + localBounds.size.y * ny),
                    localBounds.center.z + localBounds.size.z * nz);
                shardGo.transform.localRotation = Quaternion.Euler(i * 19f, i * 37f, i * 11f);
                shardGo.transform.localScale = new Vector3(
                    Mathf.Clamp(localBounds.size.x * 0.36f, 0.08f, 0.36f),
                    Mathf.Clamp(localBounds.size.y * 0.16f, 0.04f, 0.16f),
                    Mathf.Clamp(localBounds.size.z * 0.32f, 0.07f, 0.30f));

                var mf = shardGo.AddComponent<MeshFilter>();
                mf.sharedMesh = cubeMesh;

                var mr = shardGo.AddComponent<MeshRenderer>();
                if (primaryMat != null)
                {
                    mr.sharedMaterial = primaryMat;
                }

                shardGo.AddComponent<BoxCollider>();
                shardGo.AddComponent<Rigidbody>();
                var frag = shardGo.AddComponent<StageDebrisFragment>();
                frag.EnsureComponents();
                frag.CacheInitialTransform();
                _fragments.Add(frag);
            }
        }

        private Bounds ComputeIntactBoundsLocal(out Material primaryMaterial)
        {
            primaryMaterial = null;
            Transform searchRoot = intactVisualRoot != null ? intactVisualRoot.transform : transform;
            Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);

            bool hasBounds = false;
            Bounds combined = new Bounds(Vector3.up * 0.35f, new Vector3(0.5f, 0.5f, 0.5f));

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                if (debrisPoolRoot != null && r.transform.IsChildOf(debrisPoolRoot)) continue;

                if (primaryMaterial == null && r.sharedMaterial != null)
                {
                    primaryMaterial = r.sharedMaterial;
                }

                Bounds b = r.bounds;
                Vector3 localMin = transform.InverseTransformPoint(b.min);
                Vector3 localMax = transform.InverseTransformPoint(b.max);
                if (!hasBounds)
                {
                    combined = new Bounds((localMin + localMax) * 0.5f, Vector3.zero);
                    hasBounds = true;
                }
                combined.Encapsulate(localMin);
                combined.Encapsulate(localMax);
            }

            if (combined.size.sqrMagnitude < 0.01f)
            {
                combined = new Bounds(Vector3.up * 0.35f, new Vector3(0.5f, 0.5f, 0.5f));
            }
            return combined;
        }

        private Vector3 GetWorldVisualCenter()
        {
            if (intactVisualRoot != null)
            {
                Renderer[] renderers = intactVisualRoot.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length > 0)
                {
                    Bounds b = renderers[0].bounds;
                    for (int i = 1; i < renderers.Length; i++)
                    {
                        b.Encapsulate(renderers[i].bounds);
                    }
                    return b.center;
                }
            }

            return transform.position + Vector3.up * 0.45f;
        }

        private CombatVfxController ResolveVfxController()
        {
            if (_vfxController == null && Application.isPlaying)
            {
                _vfxController = FindAnyObjectByType<CombatVfxController>();
            }
            return _vfxController;
        }

        private static Mesh GetSharedCubeMesh()
        {
            if (s_sharedCubeMesh != null)
            {
                return s_sharedCubeMesh;
            }

            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s_sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
            {
                Destroy(temp);
            }
            else
            {
                DestroyImmediate(temp);
            }
            return s_sharedCubeMesh;
        }
    }
}
