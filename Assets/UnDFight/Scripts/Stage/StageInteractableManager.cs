using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnDFight
{
    /// <summary>
    /// Generic, reusable scene coordinator for PhysX stage interactions (breakable props, debris bounds).
    /// Auto-discovers all StagePropBase and IStageResettable components in the scene, builds invisible
    /// StagePhysics floor/walls aligned with FightSimulation, triggers nearby props on strong hits or
    /// fighter knockdowns without affecting 60 Hz determinism, and resets everything on MatchManager.StartRound().
    /// </summary>
    public class StageInteractableManager : MonoBehaviour
    {
        [Header("Scene References (Auto-Resolved if Empty)")]
        [SerializeField] private FightSimulation simulation;
        [SerializeField] private MatchManager matchManager;

        [Header("StagePhysics Bounds")]
        [SerializeField] private bool ensureStagePhysicsBounds = true;
        [SerializeField, Min(4f)] private float boundsDepthZ = 14f;
        [SerializeField, Min(1f)] private float boundsPaddingX = 6f;
        [SerializeField, Min(4f)] private float wallsHeight = 12f;

        [Header("Training Controls")]
        [SerializeField] private bool allowRKeyResetInTraining = true;

        private readonly List<StagePropBase> _props = new List<StagePropBase>();
        private readonly List<StageThrowableProp> _throwables = new List<StageThrowableProp>();
        private readonly List<StageDynamicProp> _dynamicProps = new List<StageDynamicProp>();
        private readonly List<StageHangingProp> _hangingProps = new List<StageHangingProp>();
        private readonly List<StageDebrisFragment> _debrisFragments = new List<StageDebrisFragment>();
        private readonly List<IStageResettable> _resettables = new List<IStageResettable>();

        private Transform _physicsBoundsRoot;
        private BoxCollider _groundCollider;
        private BoxCollider _leftWallCollider;
        private BoxCollider _rightWallCollider;

        private bool _bound;

        public FightSimulation Simulation => simulation;
        public MatchManager MatchManager => matchManager;
        public IReadOnlyList<StagePropBase> DiscoveredProps => _props;
        public IReadOnlyList<StageThrowableProp> ThrowableProps => _throwables;
        public IReadOnlyList<StageDynamicProp> DynamicProps => _dynamicProps;
        public IReadOnlyList<StageHangingProp> HangingProps => _hangingProps;
        public IReadOnlyList<StageDebrisFragment> DebrisFragments => _debrisFragments;
        public IReadOnlyList<IStageResettable> Resettables => _resettables;
        public BoxCollider GroundPhysicsCollider => _groundCollider;
        public BoxCollider LeftWallPhysicsCollider => _leftWallCollider;
        public BoxCollider RightWallPhysicsCollider => _rightWallCollider;

        private void Awake()
        {
            StagePhysicsLayers.ApplyCollisionIsolation();
            ResolveReferences();
            RefreshDiscoveredProps();
            if (ensureStagePhysicsBounds)
            {
                EnsureStagePhysicsBounds();
            }
        }

        private void OnEnable()
        {
            BindEvents();
        }

        private void OnDisable()
        {
            UnbindEvents();
        }

        public void Configure(FightSimulation targetSim, MatchManager targetMatchManager = null)
        {
            UnbindEvents();
            simulation = targetSim;
            if (targetMatchManager != null)
            {
                matchManager = targetMatchManager;
            }
            StagePhysicsLayers.ApplyCollisionIsolation();
            RefreshDiscoveredProps();
            if (ensureStagePhysicsBounds)
            {
                EnsureStagePhysicsBounds();
            }
            BindEvents();
        }

        /// <summary>
        /// Auto-discovers all StagePropBase, StageDynamicProp, StageHangingProp, StageDebrisFragment,
        /// and IStageResettable components in the active scene.
        /// </summary>
        public void RefreshDiscoveredProps()
        {
            _props.Clear();
            _throwables.Clear();
            _dynamicProps.Clear();
            _hangingProps.Clear();
            _debrisFragments.Clear();
            _resettables.Clear();

            StagePropBase[] foundProps = FindObjectsByType<StagePropBase>(FindObjectsInactive.Include);
            for (int i = 0; i < foundProps.Length; i++)
            {
                StagePropBase prop = foundProps[i];
                if (prop == null)
                {
                    continue;
                }
                if (prop is StageBreakableProp breakable)
                {
                    breakable.InitializeIfNeeded();
                }
                else if (prop is StageThrowableProp throwable)
                {
                    throwable.InitializeIfNeeded();
                    _throwables.Add(throwable);
                }
                _props.Add(prop);
                _resettables.Add(prop);
            }

            StageDynamicProp[] foundDynamic = FindObjectsByType<StageDynamicProp>(FindObjectsInactive.Include);
            for (int i = 0; i < foundDynamic.Length; i++)
            {
                StageDynamicProp dyn = foundDynamic[i];
                if (dyn == null) continue;
                dyn.CacheInitialTransform();
                dyn.EnsureComponents();
                _dynamicProps.Add(dyn);
                if (!_resettables.Contains(dyn)) _resettables.Add(dyn);
            }

            StageHangingProp[] foundHanging = FindObjectsByType<StageHangingProp>(FindObjectsInactive.Include);
            for (int i = 0; i < foundHanging.Length; i++)
            {
                StageHangingProp hang = foundHanging[i];
                if (hang == null) continue;
                hang.EnsureComponents();
                hang.CacheInitialTransform();
                _hangingProps.Add(hang);
                if (!_resettables.Contains(hang)) _resettables.Add(hang);
            }

            StageDebrisFragment[] foundDebris = FindObjectsByType<StageDebrisFragment>(FindObjectsInactive.Include);
            for (int i = 0; i < foundDebris.Length; i++)
            {
                if (foundDebris[i] != null)
                {
                    _debrisFragments.Add(foundDebris[i]);
                }
            }

            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is IStageResettable resettable && !_resettables.Contains(resettable))
                {
                    _resettables.Add(resettable);
                }
            }
        }

        /// <summary>
        /// Propagates a physical radial shockwave impulse to active debris fragments, StageDynamicProps,
        /// and StageHangingProps within radius. Never affects deterministic fighters.
        /// </summary>
        public int ApplyStageShockwave(Vector3 origin, float radius, float force)
        {
            if (radius <= 0.01f || force <= 0.01f)
            {
                return 0;
            }

            int affected = 0;

            for (int i = 0; i < _debrisFragments.Count; i++)
            {
                StageDebrisFragment frag = _debrisFragments[i];
                if (frag == null || !frag.IsLaunched || !frag.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (TryComputeShockwaveImpulse(origin, frag.transform.position, radius, force, 0.35f, out Vector3 impulse))
                {
                    frag.ApplyImpulse(impulse, ForceMode.Impulse);
                    affected++;
                }
            }

            for (int i = 0; i < _dynamicProps.Count; i++)
            {
                StageDynamicProp dyn = _dynamicProps[i];
                if (dyn == null || !dyn.gameObject.activeInHierarchy)
                {
                    continue;
                }

                if (TryComputeShockwaveImpulse(origin, dyn.transform.position, radius, force, 0.30f, out Vector3 impulse))
                {
                    dyn.ApplyImpulse(impulse, ForceMode.Impulse);
                    affected++;
                }
            }

            for (int i = 0; i < _hangingProps.Count; i++)
            {
                StageHangingProp hang = _hangingProps[i];
                if (hang == null || !hang.gameObject.activeInHierarchy || hang.SwingBody == null)
                {
                    continue;
                }

                if (TryComputeShockwaveImpulse(origin, hang.SwingBody.worldCenterOfMass, radius * 1.45f, force * 0.85f, 0.08f, out Vector3 impulse))
                {
                    hang.ApplyImpulse(impulse, ForceMode.Impulse);
                    affected++;
                }
            }

            return affected;
        }

        private static bool TryComputeShockwaveImpulse(
            Vector3 origin,
            Vector3 targetPos,
            float radius,
            float force,
            float upwardLift,
            out Vector3 impulse)
        {
            Vector3 delta = targetPos - origin;
            float dist = delta.magnitude;
            if (dist > radius)
            {
                impulse = Vector3.zero;
                return false;
            }

            float falloff = Mathf.Clamp01(1f - dist / radius);
            Vector3 dir = dist > 0.05f ? (delta / dist) : new Vector3(1f, 0.35f, 0.15f).normalized;
            dir.y = Mathf.Max(dir.y, upwardLift);
            impulse = dir.normalized * (force * Mathf.Max(0.22f, falloff));
            return true;
        }

        public StageThrowableProp FindNearestReadyThrowable(FighterController fighter)
        {
            return FindNearestReadyThrowableInList(_throwables, fighter);
        }

        public static StageThrowableProp FindNearestReadyThrowableInScene(FighterController fighter)
        {
            if (fighter == null)
            {
                return null;
            }

            StageInteractableManager manager = FindAnyObjectByType<StageInteractableManager>();
            if (manager != null)
            {
                StageThrowableProp managed = manager.FindNearestReadyThrowable(fighter);
                if (managed != null)
                {
                    return managed;
                }
            }

            StageThrowableProp[] all = FindObjectsByType<StageThrowableProp>(FindObjectsInactive.Exclude);
            return FindNearestReadyThrowableInList(all, fighter);
        }

        private static StageThrowableProp FindNearestReadyThrowableInList(IReadOnlyList<StageThrowableProp> list, FighterController fighter)
        {
            if (fighter == null || list == null || list.Count == 0)
            {
                return null;
            }

            StageThrowableProp best = null;
            float bestDist = float.MaxValue;
            float fighterX = fighter.SimPosition.x;

            for (int i = 0; i < list.Count; i++)
            {
                StageThrowableProp prop = list[i];
                if (prop == null || !prop.IsFighterInRange(fighter))
                {
                    continue;
                }

                float dx = Mathf.Abs(prop.transform.position.x - fighterX);
                if (dx < bestDist)
                {
                    bestDist = dx;
                    best = prop;
                }
            }

            return best;
        }

        /// <summary>
        /// Ensures an invisible floor and side boundary walls exist on the StagePhysics layer
        /// aligned with FightSimulation GroundY and StageMinX/StageMaxX.
        /// </summary>
        public void EnsureStagePhysicsBounds()
        {
            float groundY = simulation != null ? simulation.GroundY : 0f;
            float minX = simulation != null ? simulation.StageMinX : -8f;
            float maxX = simulation != null ? simulation.StageMaxX : 8f;

            if (_physicsBoundsRoot == null)
            {
                Transform existing = transform.Find("StagePhysicsBounds");
                if (existing != null)
                {
                    _physicsBoundsRoot = existing;
                }
                else
                {
                    var go = new GameObject("StagePhysicsBounds");
                    go.transform.SetParent(transform, false);
                    _physicsBoundsRoot = go.transform;
                }
            }

            int stagePhysicsLayer = StagePhysicsLayers.StagePhysicsLayer;
            _physicsBoundsRoot.gameObject.layer = stagePhysicsLayer;

            float widthX = Mathf.Max(2f, (maxX - minX) + boundsPaddingX * 2f);
            float centerX = (minX + maxX) * 0.5f;
            float centerZ = 1.5f;

            _groundCollider = EnsureBoxCollider(
                "StagePhysics_Ground",
                stagePhysicsLayer,
                new Vector3(centerX, groundY - 0.5f, centerZ),
                new Vector3(widthX, 1.0f, boundsDepthZ));

            _leftWallCollider = EnsureBoxCollider(
                "StagePhysics_WallLeft",
                stagePhysicsLayer,
                new Vector3(minX - 0.5f, groundY + wallsHeight * 0.5f, centerZ),
                new Vector3(1.0f, wallsHeight, boundsDepthZ));

            _rightWallCollider = EnsureBoxCollider(
                "StagePhysics_WallRight",
                stagePhysicsLayer,
                new Vector3(maxX + 0.5f, groundY + wallsHeight * 0.5f, centerZ),
                new Vector3(1.0f, wallsHeight, boundsDepthZ));
        }

        public void ResetAllProps()
        {
            for (int i = 0; i < _resettables.Count; i++)
            {
                _resettables[i]?.ResetProp();
            }
        }

        private void Update()
        {
            if (!allowRKeyResetInTraining)
            {
                return;
            }

            bool isTraining = (matchManager != null && matchManager.IsTrainingMode) || GameSessionState.IsTrainingMode;
            if (!isTraining || (simulation != null && simulation.IsPaused))
            {
                return;
            }

            Keyboard kb = Keyboard.current;
            if (kb != null && kb.rKey.wasPressedThisFrame)
            {
                ResetAllProps();
            }
        }

        private void ResolveReferences()
        {
            if (simulation == null)
            {
                simulation = FindAnyObjectByType<FightSimulation>();
            }
            if (matchManager == null)
            {
                matchManager = FindAnyObjectByType<MatchManager>();
            }
        }

        private void BindEvents()
        {
            if (_bound)
            {
                return;
            }

            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnCombatHit += HandleCombatHit;
                simulation.OnFighterGroundImpact -= HandleFighterGroundImpact;
                simulation.OnFighterGroundImpact += HandleFighterGroundImpact;
            }

            if (matchManager != null)
            {
                matchManager.OnRoundStarted -= HandleRoundStarted;
                matchManager.OnRoundStarted += HandleRoundStarted;
            }

            _bound = true;
        }

        private void UnbindEvents()
        {
            if (simulation != null)
            {
                simulation.OnCombatHit -= HandleCombatHit;
                simulation.OnFighterGroundImpact -= HandleFighterGroundImpact;
            }

            if (matchManager != null)
            {
                matchManager.OnRoundStarted -= HandleRoundStarted;
            }

            _bound = false;
        }

        private void HandleRoundStarted()
        {
            ResetAllProps();
        }

        private void HandleCombatHit(CombatHitEvent hit)
        {
            if (hit.Outcome == HitOutcomeType.Blocked || hit.Outcome == HitOutcomeType.ThrowTech)
            {
                return;
            }

            bool isStrongImpact =
                hit.IsSpecial ||
                hit.Outcome == HitOutcomeType.KnockDown ||
                (hit.Move != null &&
                 (hit.Move.attackType == AttackType.HeavyPunch ||
                  hit.Move.attackType == AttackType.HeavyKick ||
                  hit.Move.attackType == AttackType.Special ||
                  hit.Move.causesKnockdown));

            int propDamage = isStrongImpact ? 2 : 1;
            float pushDirX = hit.Attacker != null
                ? hit.Attacker.FacingSign
                : (Mathf.Abs(hit.KnockbackVelocity) > 0.01f ? Mathf.Sign(hit.KnockbackVelocity) : 1f);

            Vector2 attackerPos = hit.Attacker != null ? hit.Attacker.SimPosition : hit.ContactPoint;
            Vector2 defenderPos = hit.Defender != null ? hit.Defender.SimPosition : hit.ContactPoint;
            int simTick = simulation != null ? simulation.CurrentTick : -1;

            for (int i = 0; i < _props.Count; i++)
            {
                StagePropBase prop = _props[i];
                if (prop == null || prop.IsBroken)
                {
                    continue;
                }

                prop.TryHandleCombatImpact(
                    hit.ContactPoint,
                    propDamage,
                    pushDirX,
                    isStrongImpact,
                    attackerPos,
                    defenderPos,
                    simTick);
            }

            if (isStrongImpact)
            {
                float radius = hit.IsSpecial ? 4.8f : 3.2f;
                float shockForce = hit.IsSpecial ? 6.0f : 3.4f;
                ApplyStageShockwave(new Vector3(hit.ContactPoint.x, hit.ContactPoint.y, 0f), radius, shockForce);
            }
        }

        private void HandleFighterGroundImpact(FighterController fighter)
        {
            if (fighter == null)
            {
                return;
            }

            Vector2 pos = fighter.SimPosition;
            float pushDirX = Mathf.Abs(fighter.SimVelocity.x) > 0.05f
                ? Mathf.Sign(fighter.SimVelocity.x)
                : -fighter.FacingSign;
            int simTick = simulation != null ? simulation.CurrentTick : -1;

            for (int i = 0; i < _props.Count; i++)
            {
                StagePropBase prop = _props[i];
                if (prop == null || prop.IsBroken)
                {
                    continue;
                }

                prop.TryHandleKnockdownImpact(pos, pushDirX, simTick);
            }

            ApplyStageShockwave(new Vector3(pos.x, pos.y + 0.12f, 0f), radius: 3.8f, force: 4.5f);
        }

        private BoxCollider EnsureBoxCollider(string childName, int layer, Vector3 localPos, Vector3 size)
        {
            Transform child = _physicsBoundsRoot.Find(childName);
            GameObject go;
            if (child != null)
            {
                go = child.gameObject;
            }
            else
            {
                go = new GameObject(childName);
                go.transform.SetParent(_physicsBoundsRoot, false);
            }

            go.layer = layer;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            BoxCollider box = go.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = go.AddComponent<BoxCollider>();
            }
            box.isTrigger = false;
            box.center = Vector3.zero;
            box.size = size;
            return box;
        }
    }
}
