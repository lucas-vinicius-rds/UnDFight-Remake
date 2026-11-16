using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    public enum StageThrowableState
    {
        Ready = 0,
        Grabbed = 1,
        Thrown = 2,
        Spent = 3,
        Broken = 4
    }

    /// <summary>
    /// Generic, reusable Mortal Kombat-style interactive stage object that fighters can grab
    /// and hurl toward the opponent via real NVIDIA PhysX projectile physics (Rigidbody + AddForce +
    /// AddTorque + OnTriggerEnter on the defender's hurtboxCapsule).
    /// </summary>
    public class StageThrowableProp : StagePropBase
    {
        [Header("Interaction Range")]
        [SerializeField, Min(0.3f)] private float interactionRadiusX = 1.65f;
        [SerializeField, Min(0.2f)] private float maxZDistanceFromFightPlane = 2.0f;
        [SerializeField] private float fightPlaneZ = 0f;
        [SerializeField, Min(0.2f)] private float promptHeightOffset = 1.05f;
        [SerializeField] private string promptLabel = "ARREMESSAR";

        [Header("Projectile Combat Payload")]
        [SerializeField, Min(1)] private int damage = 85;
        [SerializeField, Min(0)] private int chipDamage = 12;
        [SerializeField, Min(0)] private int hitstopTicks = 5;
        [SerializeField, Min(1)] private int hitstunTicks = 18;
        [SerializeField, Min(1)] private int blockstunTicks = 11;
        [SerializeField, Min(0f)] private float knockback = 3.2f;
        [SerializeField] private HitHeight hitHeight = HitHeight.Mid;
        [SerializeField] private bool causesKnockdown = true;

        [Header("PhysX Throw Tuning")]
        [SerializeField, Min(1f)] private float launchHorizontalImpulse = 12.5f;
        [SerializeField, Min(0f)] private float launchUpwardImpulse = 1.6f;
        [SerializeField] private Vector3 launchTorque = new Vector3(0f, 2f, -14f);
        [SerializeField, Min(0.1f)] private float projectileMass = 1.0f;
        [SerializeField, Min(0.5f)] private float maxFlightSeconds = 3.5f;

        [Header("Visuals, Break & Debris Pool")]
        [SerializeField] private StagePropMaterialType materialType = StagePropMaterialType.Plastic;
        [SerializeField] private bool breakOnFighterHit = true;
        [SerializeField] private bool breakOnStageHit = false;
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider propCollider;
        [SerializeField] private GameObject intactVisualRoot;
        [SerializeField] private Transform debrisPoolRoot;
        [SerializeField, Range(5, 8)] private int generatedFragmentCount = 6;
        [SerializeField, Min(0f)] private float explosionForce = 4.5f;
        [SerializeField, Min(0.2f)] private float explosionRadius = 1.35f;
        [SerializeField, Min(0f)] private float debrisDirectionalImpulse = 2.8f;
        [SerializeField, Min(0f)] private float debrisUpwardImpulse = 2.4f;
        [SerializeField, Min(0f)] private float debrisTorqueStrength = 8.5f;
        [SerializeField, Min(0.4f)] private float fragmentFreezeSeconds = 2.6f;
        [SerializeField, Min(0.8f)] private float fragmentHideSeconds = 4.2f;

        [Header("Training Auto-Respawn")]
        [SerializeField] private bool autoRespawnInTraining = true;
        [SerializeField, Min(1f)] private float trainingRespawnSeconds = 7.5f;

        private readonly List<StageDebrisFragment> _fragments = new List<StageDebrisFragment>();

        private StageThrowableState _state = StageThrowableState.Ready;
        private FighterController _thrower;
        private int _throwFacingSign = 1;
        private bool _hitResolved;
        private bool _initialized;
        private float _flightElapsed;
        private float _spentOrBrokenElapsed;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private Quaternion _initialIntactLocalRot = Quaternion.identity;
        private MoveData _runtimeMoveData;

        public StageThrowableState State => _state;
        public override bool IsBroken => _state == StageThrowableState.Broken;
        public bool IsAvailableForInteraction => _state == StageThrowableState.Ready;
        public Rigidbody Body => body;
        public Collider PropCollider => propCollider;
        public float InteractionRadiusX => interactionRadiusX;
        public float MaxZDistanceFromFightPlane => maxZDistanceFromFightPlane;
        public string PromptLabel => string.IsNullOrWhiteSpace(promptLabel) ? "ARREMESSAR" : promptLabel;
        public int Damage => damage;
        public bool CausesKnockdown => causesKnockdown;
        public StagePropMaterialType MaterialType => materialType;
        public FighterController CurrentThrower => _thrower;
        public IReadOnlyList<StageDebrisFragment> Fragments => _fragments;

        public event Action<StageThrowableProp, FighterController> OnPropThrown;
        public event Action<StageThrowableProp, FighterController, HitOutcomeType> OnPropHitFighter;

        private void Awake()
        {
            InitializeIfNeeded();
        }

        private void OnDestroy()
        {
            if (_runtimeMoveData != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(_runtimeMoveData);
                }
                else
                {
                    DestroyImmediate(_runtimeMoveData);
                }
                _runtimeMoveData = null;
            }
        }

        public void InitializeIfNeeded()
        {
            if (_initialized)
            {
                return;
            }

            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;

            EnsurePhysicsComponents();
            CacheIntactVisuals(intactVisualRoot, debrisPoolRoot, out _initialIntactLocalRot);
            EnsureDebrisPoolFragments(intactVisualRoot, ref debrisPoolRoot, _fragments, generatedFragmentCount);

            for (int i = 0; i < _fragments.Count; i++)
            {
                if (_fragments[i] != null)
                {
                    _fragments[i].ResetFragment();
                }
            }

            ApplyReadyPhysicsState();
            SetIntactVisualsVisible(intactVisualRoot, propCollider, true);
            _state = StageThrowableState.Ready;
            _initialized = true;
        }

        public void ConfigureForRuntime(
            int projectileDamage,
            bool knockdown,
            bool shatterOnFighter,
            bool shatterOnStage,
            StagePropMaterialType matType,
            float radiusX = 1.65f,
            float zTolerance = 2.0f)
        {
            damage = Mathf.Max(1, projectileDamage);
            causesKnockdown = knockdown;
            breakOnFighterHit = shatterOnFighter;
            breakOnStageHit = shatterOnStage;
            materialType = matType;
            interactionRadiusX = Mathf.Max(0.3f, radiusX);
            maxZDistanceFromFightPlane = Mathf.Max(0.2f, zTolerance);
            _initialized = false;
            if (_runtimeMoveData != null)
            {
                if (Application.isPlaying) Destroy(_runtimeMoveData);
                else DestroyImmediate(_runtimeMoveData);
                _runtimeMoveData = null;
            }
            InitializeIfNeeded();
        }

        public bool IsFighterInRange(FighterController fighter)
        {
            InitializeIfNeeded();
            if (_state != StageThrowableState.Ready || fighter == null || fighter.CurrentHealth <= 0)
            {
                return false;
            }

            Vector3 p = transform.position;
            if (Mathf.Abs(p.z - fightPlaneZ) > maxZDistanceFromFightPlane)
            {
                return false;
            }

            return Mathf.Abs(p.x - fighter.SimPosition.x) <= interactionRadiusX;
        }

        public Vector3 GetPromptWorldPosition()
        {
            return transform.position + Vector3.up * promptHeightOffset;
        }

        public bool BeginGrab(FighterController thrower)
        {
            InitializeIfNeeded();
            if (_state != StageThrowableState.Ready || thrower == null)
            {
                return false;
            }

            _state = StageThrowableState.Grabbed;
            _thrower = thrower;
            _throwFacingSign = thrower.FacingSign >= 0 ? 1 : -1;
            _hitResolved = false;
            _flightElapsed = 0f;
            _spentOrBrokenElapsed = 0f;

            EnsurePhysicsComponents();
            body.isKinematic = true;
            body.useGravity = false;
            body.constraints = RigidbodyConstraints.None;
            if (propCollider != null)
            {
                propCollider.enabled = true;
                propCollider.isTrigger = true;
            }

            SyncBodyTransform(thrower.GetThrowHandWorldPosition(fightPlaneZ), Quaternion.identity);
            return true;
        }

        public void UpdateGrabTransform(Vector3 worldPos)
        {
            if (_state != StageThrowableState.Grabbed)
            {
                return;
            }

            transform.position = worldPos;
            if (body != null)
            {
                body.position = worldPos;
            }
        }

        public void CancelGrabAndDrop()
        {
            if (_state != StageThrowableState.Grabbed)
            {
                return;
            }

            _state = StageThrowableState.Spent;
            _hitResolved = true;
            _spentOrBrokenElapsed = 0f;

            EnsurePhysicsComponents();
            if (propCollider != null)
            {
                propCollider.isTrigger = false;
                propCollider.enabled = true;
            }
            body.constraints = RigidbodyConstraints.None;
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.down * 1.2f;
        }

        public bool LaunchProjectile(FighterController thrower, int facingSign)
        {
            InitializeIfNeeded();
            if (_state != StageThrowableState.Ready && _state != StageThrowableState.Grabbed)
            {
                return false;
            }

            _thrower = thrower;
            _throwFacingSign = facingSign >= 0 ? 1 : -1;
            _state = StageThrowableState.Thrown;
            _hitResolved = false;
            _flightElapsed = 0f;
            _spentOrBrokenElapsed = 0f;

            EnsurePhysicsComponents();
            gameObject.layer = StagePhysicsLayers.StageProjectileLayer;

            Vector3 launchPos = thrower != null
                ? thrower.GetThrowHandWorldPosition(fightPlaneZ)
                : new Vector3(transform.position.x, Mathf.Max(transform.position.y, 0.95f), fightPlaneZ);
            SyncBodyTransform(launchPos, Quaternion.identity);

            if (propCollider != null)
            {
                propCollider.enabled = true;
                propCollider.isTrigger = false;
            }

            body.isKinematic = false;
            body.useGravity = true;
            body.constraints = RigidbodyConstraints.FreezePositionZ;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();

            Vector3 impulse = new Vector3(_throwFacingSign * launchHorizontalImpulse, launchUpwardImpulse, 0f);
            Vector3 torque = new Vector3(
                launchTorque.x,
                launchTorque.y * _throwFacingSign,
                launchTorque.z * _throwFacingSign);

            body.AddForce(impulse, ForceMode.Impulse);
            body.AddTorque(torque, ForceMode.Impulse);

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.PlayPropImpact(materialType);
            }

            OnPropThrown?.Invoke(this, _thrower);
            return true;
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
            // Keep throwable stage props intact for tactical grab/throw until hurled.
            return false;
        }

        public override void ResetProp()
        {
            InitializeIfNeeded();

            _state = StageThrowableState.Ready;
            _thrower = null;
            _throwFacingSign = 1;
            _hitResolved = false;
            _flightElapsed = 0f;
            _spentOrBrokenElapsed = 0f;

            for (int i = 0; i < _fragments.Count; i++)
            {
                if (_fragments[i] != null)
                {
                    _fragments[i].ResetFragment();
                }
            }

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;

            if (intactVisualRoot != null)
            {
                intactVisualRoot.transform.localRotation = _initialIntactLocalRot;
            }

            ApplyReadyPhysicsState();
            SetIntactVisualsVisible(intactVisualRoot, propCollider, true);
            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (_state == StageThrowableState.Thrown)
            {
                _flightElapsed += Time.deltaTime;
                if (_flightElapsed >= maxFlightSeconds)
                {
                    _state = StageThrowableState.Spent;
                    _hitResolved = true;
                }
            }
            else if ((_state == StageThrowableState.Spent || _state == StageThrowableState.Broken) &&
                     autoRespawnInTraining &&
                     GameSessionState.IsTrainingMode)
            {
                _spentOrBrokenElapsed += Time.deltaTime;
                if (_spentOrBrokenElapsed >= trainingRespawnSeconds)
                {
                    ResetProp();
                }
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            ResolveTriggerOverlap(other);
        }

        public void ResolveTriggerOverlap(Collider other)
        {
            if (_state != StageThrowableState.Thrown || _hitResolved || other == null || other == propCollider)
            {
                return;
            }

            FighterController target = other.GetComponentInParent<FighterController>();
            if (target == null || target == _thrower || target.IsInvulnerable)
            {
                return;
            }

            _hitResolved = true;

            MoveData moveData = GetOrCreateProjectileMoveData();
            target.ApplyIncomingAttack(
                moveData,
                _throwFacingSign,
                out HitOutcomeType outcome,
                out int damageDealt,
                out float knockbackVelocity,
                out int comboCount,
                out int comboDamage);

            Vector3 cpWorld = other.ClosestPoint(transform.position);
            Vector2 contactPoint = new Vector2(cpWorld.x, cpWorld.y);

            FightSimulation sim = ResolveSimulation();
            if (sim != null)
            {
                sim.NotifyExternalProjectileImpact(
                    _thrower,
                    target,
                    moveData,
                    contactPoint,
                    outcome,
                    damageDealt,
                    knockbackVelocity,
                    comboCount,
                    comboDamage);
            }
            else
            {
                CombatVfxController vfx = ResolveVfxController();
                vfx?.SpawnSparkBurst(contactPoint, outcome, isSpecial: false);
                if (Application.isPlaying)
                {
                    FightAudioManager.Instance?.PlayPropImpact(materialType);
                }
            }

            OnPropHitFighter?.Invoke(this, target, outcome);

            if (breakOnFighterHit)
            {
                ShatterAtCurrentPosition(contactPoint, _throwFacingSign);
            }
            else
            {
                _state = StageThrowableState.Spent;
                _spentOrBrokenElapsed = 0f;
                if (body != null && !body.isKinematic)
                {
                    body.linearVelocity = new Vector3(-_throwFacingSign * 2.4f, 2.2f, 0.7f);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            Vector2 origin = collision.contactCount > 0
                ? new Vector2(collision.GetContact(0).point.x, collision.GetContact(0).point.y)
                : new Vector2(transform.position.x, transform.position.y);
            ResolveStageCollision(origin);
        }

        public void ResolveStageCollision(Vector2 impactOrigin)
        {
            if (_state != StageThrowableState.Thrown || _hitResolved)
            {
                return;
            }

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.PlayPropImpact(materialType);
            }

            if (breakOnStageHit)
            {
                _hitResolved = true;
                ShatterAtCurrentPosition(impactOrigin, _throwFacingSign);
            }
            else
            {
                _hitResolved = true;
                _state = StageThrowableState.Spent;
                _spentOrBrokenElapsed = 0f;
                if (body != null)
                {
                    body.constraints = RigidbodyConstraints.None;
                }
            }
        }

        private void ShatterAtCurrentPosition(Vector2 impactOrigin2D, float pushDirX)
        {
            _state = StageThrowableState.Broken;
            _spentOrBrokenElapsed = 0f;

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.useGravity = false;
            }

            SetIntactVisualsVisible(intactVisualRoot, propCollider, false);

            Vector3 worldCenter = GetWorldVisualCenter(intactVisualRoot);
            Vector3 explosionOrigin = new Vector3(
                Mathf.Lerp(impactOrigin2D.x, worldCenter.x, 0.35f),
                Mathf.Max(0.12f, worldCenter.y - 0.1f),
                worldCenter.z - 0.1f);

            LaunchDebrisBurst(
                _fragments,
                explosionOrigin,
                pushDirX,
                explosionForce,
                explosionRadius,
                debrisDirectionalImpulse,
                debrisUpwardImpulse,
                debrisTorqueStrength,
                fragmentFreezeSeconds,
                fragmentHideSeconds);

            CombatVfxController vfx = ResolveVfxController();
            vfx?.SpawnSparkBurst(new Vector2(worldCenter.x, worldCenter.y), HitOutcomeType.KnockDown, isSpecial: false);

            if (Application.isPlaying)
            {
                FightAudioManager.Instance?.PlayPropBreak(materialType);
            }
        }

        private void EnsurePhysicsComponents()
        {
            gameObject.layer = StagePhysicsLayers.StageProjectileLayer;

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }
            body.mass = Mathf.Max(0.1f, projectileMass);
            body.interpolation = RigidbodyInterpolation.Interpolate;

            if (propCollider == null)
            {
                propCollider = GetComponent<Collider>();
            }
            if (propCollider == null)
            {
                var box = gameObject.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, 0.35f, 0f);
                box.size = new Vector3(0.45f, 0.70f, 0.45f);
                propCollider = box;
            }
        }

        private void ApplyReadyPhysicsState()
        {
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.constraints = RigidbodyConstraints.None;
                body.isKinematic = true;
                body.useGravity = false;
                body.Sleep();
                body.position = transform.position;
                body.rotation = transform.rotation;
            }

            if (propCollider != null)
            {
                propCollider.isTrigger = true;
                propCollider.enabled = true;
            }
        }

        private void SyncBodyTransform(Vector3 worldPos, Quaternion worldRot)
        {
            transform.SetPositionAndRotation(worldPos, worldRot);
            if (body != null)
            {
                body.position = worldPos;
                body.rotation = worldRot;
            }
            Physics.SyncTransforms();
        }

        private MoveData GetOrCreateProjectileMoveData()
        {
            if (_runtimeMoveData != null)
            {
                return _runtimeMoveData;
            }

            _runtimeMoveData = ScriptableObject.CreateInstance<MoveData>();
            _runtimeMoveData.hideFlags = HideFlags.DontSave;
            _runtimeMoveData.Configure(
                nameValue: PromptLabel,
                type: AttackType.HeavyPunch,
                startup: 4,
                active: 4,
                recovery: 12,
                dmg: damage,
                hitstun: hitstunTicks,
                blockstun: blockstunTicks,
                hitstop: hitstopTicks,
                kb: knockback,
                height: hitHeight,
                knockdown: causesKnockdown,
                centerOffset: Vector2.zero,
                size: Vector2.one,
                chip: chipDamage);
            return _runtimeMoveData;
        }

        private FightSimulation ResolveSimulation()
        {
            if (_thrower != null && _thrower.Simulation != null)
            {
                return _thrower.Simulation;
            }
            return FindAnyObjectByType<FightSimulation>();
        }
    }
}
