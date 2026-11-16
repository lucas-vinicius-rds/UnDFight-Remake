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
        /// Auto-discovers all StagePropBase and IStageResettable components in the active scene.
        /// </summary>
        public void RefreshDiscoveredProps()
        {
            _props.Clear();
            _throwables.Clear();
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

            MonoBehaviour[] allBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
            for (int i = 0; i < allBehaviours.Length; i++)
            {
                if (allBehaviours[i] is IStageResettable resettable && !_resettables.Contains(resettable))
                {
                    _resettables.Add(resettable);
                }
            }
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
            if (hit.Outcome == HitOutcomeType.Blocked)
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
