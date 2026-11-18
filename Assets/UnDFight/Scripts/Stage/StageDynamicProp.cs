using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Reusable loose dynamic stage prop (cans, loose bottles, cones, small boxes) living on StageDebris.
    /// Driven by real NVIDIA PhysX with Auto-Sleep, reacts to FighterDebrisPusher, aerodynamic wind,
    /// and combat shockwaves, emits soft impact audio on strong collisions, and resets cleanly per round.
    /// </summary>
    public class StageDynamicProp : MonoBehaviour, IStageResettable
    {
        [Header("PhysX Components")]
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider propCollider;
        [SerializeField] private StageWindAffectable windAffectable;

        [Header("Physical Properties")]
        [SerializeField] private StagePropMaterialType materialType = StagePropMaterialType.ConcreteOrMetal;
        [SerializeField, Min(0.05f)] private float mass = 0.28f;
        [SerializeField, Min(0.01f)] private float sleepThreshold = 0.08f;
        [SerializeField, Min(0f)] private float linearDamping = 0.35f;
        [SerializeField, Min(0f)] private float angularDamping = 0.55f;
        [SerializeField] private float fallOutMinY = -5f;

        [Header("Audio Feedback")]
        [SerializeField, Min(0.2f)] private float impactSoundVelocityThreshold = 1.8f;
        [SerializeField, Min(0.05f)] private float impactSoundCooldown = 0.18f;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _cachedInitialTransform;
        private float _lastImpactSoundTime = -10f;
        private bool _fellOutOfBounds;

        public Rigidbody Body => body;
        public Collider PropCollider => propCollider;
        public StageWindAffectable WindAffectable => windAffectable;
        public StagePropMaterialType MaterialType => materialType;

        private void Awake()
        {
            CacheInitialTransform();
            EnsureComponents();
        }

        public void CacheInitialTransform()
        {
            if (_cachedInitialTransform)
            {
                return;
            }

            _initialLocalPosition = transform.localPosition;
            _initialLocalRotation = transform.localRotation;
            _initialLocalScale = transform.localScale;
            _cachedInitialTransform = true;
        }

        public void EnsureComponents()
        {
            int debrisLayer = LayerMask.NameToLayer(StagePhysicsLayers.StageDebrisLayerName);
            if (debrisLayer >= 0)
            {
                gameObject.layer = debrisLayer;
            }

            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            body.mass = Mathf.Max(0.05f, mass);
            body.isKinematic = false;
            body.useGravity = true;
            body.sleepThreshold = sleepThreshold;
            body.linearDamping = linearDamping;
            body.angularDamping = angularDamping;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;

            if (propCollider == null)
            {
                propCollider = GetComponent<Collider>();
            }
            if (propCollider == null)
            {
                propCollider = gameObject.AddComponent<BoxCollider>();
            }
            propCollider.isTrigger = false;

            if (windAffectable == null)
            {
                windAffectable = GetComponent<StageWindAffectable>();
            }
            if (windAffectable == null)
            {
                windAffectable = gameObject.AddComponent<StageWindAffectable>();
                windAffectable.Configure(cd: 1.05f, areaM2: 0.12f, sensitivity: 1.25f, wakeThreshold: 2.0f, bodyOverride: body);
            }
            else
            {
                windAffectable.TargetBody = body;
            }
        }

        public void ApplyImpulse(Vector3 impulse, ForceMode mode = ForceMode.Impulse)
        {
            if (!gameObject.activeInHierarchy || _fellOutOfBounds)
            {
                return;
            }

            EnsureComponents();
            if (body != null && !body.isKinematic)
            {
                body.WakeUp();
                body.AddForce(impulse, mode);
            }
        }

        public void ResetProp()
        {
            CacheInitialTransform();
            EnsureComponents();

            _fellOutOfBounds = false;
            gameObject.SetActive(true);

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;

            if (propCollider != null)
            {
                propCollider.enabled = true;
            }

            if (body != null)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.position = transform.position;
                body.rotation = transform.rotation;
                body.Sleep();
            }

            Physics.SyncTransforms();
        }

        private void Update()
        {
            if (_fellOutOfBounds)
            {
                return;
            }

            if (transform.position.y < fallOutMinY)
            {
                _fellOutOfBounds = true;
                if (body != null)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.Sleep();
                }
                gameObject.SetActive(false);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!Application.isPlaying || collision == null)
            {
                return;
            }

            if (collision.relativeVelocity.magnitude < impactSoundVelocityThreshold)
            {
                return;
            }

            float now = Time.time;
            if ((now - _lastImpactSoundTime) < impactSoundCooldown)
            {
                return;
            }

            _lastImpactSoundTime = now;
            FightAudioManager.Instance?.PlayPropImpact(materialType);
        }
    }
}
