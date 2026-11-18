using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Pooled physical debris shard driven by NVIDIA PhysX on the StageDebris layer.
    /// When persistUntilReset is true (default), remains a real dynamic Rigidbody for the entire round
    /// using PhysX Auto-Sleep so fighters, wind gusts, and combat shockwaves can kick or scatter it,
    /// then resets cleanly at MatchManager.StartRound() or Training R-key reset.
    /// </summary>
    public class StageDebrisFragment : MonoBehaviour
    {
        [SerializeField] private Rigidbody body;
        [SerializeField] private Collider fragmentCollider;
        [SerializeField] private StageWindAffectable windAffectable;
        [SerializeField] private bool persistUntilReset = true;
        [SerializeField, Min(0.01f)] private float autoSleepThreshold = 0.08f;
        [SerializeField, Min(0f)] private float dynamicLinearDamping = 0.45f;
        [SerializeField, Min(0f)] private float dynamicAngularDamping = 0.65f;
        [SerializeField] private float fallOutMinY = -5f;

        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private Vector3 _initialLocalScale;
        private bool _cachedInitialTransform;
        private bool _isLaunched;
        private bool _isFrozen;
        private float _elapsed;
        private float _freezeAfterSeconds = 2.6f;
        private float _hideAfterSeconds = 4.2f;

        public Rigidbody Body => body;
        public Collider FragmentCollider => fragmentCollider;
        public StageWindAffectable WindAffectable => windAffectable;
        public bool PersistUntilReset { get => persistUntilReset; set => persistUntilReset = value; }
        public bool IsLaunched => _isLaunched;
        public bool IsFrozen => _isFrozen;

        private void Awake()
        {
            EnsureComponents();
            CacheInitialTransform();
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
            if (body == null)
            {
                body = GetComponent<Rigidbody>();
            }
            if (body == null)
            {
                body = gameObject.AddComponent<Rigidbody>();
            }

            if (fragmentCollider == null)
            {
                fragmentCollider = GetComponent<Collider>();
            }
            if (fragmentCollider == null)
            {
                fragmentCollider = gameObject.AddComponent<BoxCollider>();
            }

            body.useGravity = true;
            body.sleepThreshold = autoSleepThreshold;
            body.linearDamping = dynamicLinearDamping;
            body.angularDamping = dynamicAngularDamping;
            body.collisionDetectionMode = CollisionDetectionMode.Discrete;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            gameObject.layer = StagePhysicsLayers.StageDebrisLayer;

            if (windAffectable == null)
            {
                windAffectable = GetComponent<StageWindAffectable>();
            }
            if (windAffectable == null)
            {
                windAffectable = gameObject.AddComponent<StageWindAffectable>();
                windAffectable.Configure(cd: 1.05f, areaM2: 0.09f, sensitivity: 0.90f, wakeThreshold: 4.8f, bodyOverride: body);
            }
            else
            {
                windAffectable.TargetBody = body;
            }
        }

        /// <summary>
        /// Wakes a live dynamic debris fragment and applies an external PhysX impulse (shockwave, kick, wind).
        /// </summary>
        public void ApplyImpulse(Vector3 impulse, ForceMode mode = ForceMode.Impulse)
        {
            if (!_isLaunched || !gameObject.activeInHierarchy || body == null || body.isKinematic)
            {
                return;
            }

            body.WakeUp();
            body.AddForce(impulse, mode);
        }

        public void Launch(
            Vector3 explosionOrigin,
            float explosionForce,
            float explosionRadius,
            Vector3 directionalImpulse,
            float torqueStrength,
            float freezeAfterSeconds,
            float hideAfterSeconds)
        {
            CacheInitialTransform();
            EnsureComponents();

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;

            _freezeAfterSeconds = Mathf.Max(0.4f, freezeAfterSeconds);
            _hideAfterSeconds = Mathf.Max(_freezeAfterSeconds + 0.25f, hideAfterSeconds);
            _elapsed = 0f;
            _isFrozen = false;
            _isLaunched = true;

            gameObject.SetActive(true);

            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            if (fragmentCollider != null)
            {
                fragmentCollider.enabled = true;
            }

            body.AddExplosionForce(explosionForce, explosionOrigin, Mathf.Max(0.25f, explosionRadius), 0.35f, ForceMode.Impulse);
            body.AddForce(directionalImpulse, ForceMode.Impulse);

            Vector3 randomTorque = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f),
                Random.Range(-1f, 1f)).normalized * torqueStrength;
            body.AddTorque(randomTorque, ForceMode.Impulse);
        }

        public void ResetFragment()
        {
            CacheInitialTransform();
            _isLaunched = false;
            _isFrozen = false;
            _elapsed = 0f;

            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.Sleep();
                body.isKinematic = false;
            }

            if (fragmentCollider != null)
            {
                fragmentCollider.enabled = true;
            }

            transform.localPosition = _initialLocalPosition;
            transform.localRotation = _initialLocalRotation;
            transform.localScale = _initialLocalScale;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_isLaunched)
            {
                return;
            }

            // Safety net against falling beneath the stage floor
            if (transform.position.y < fallOutMinY)
            {
                if (body != null)
                {
                    if (!body.isKinematic)
                    {
                        body.linearVelocity = Vector3.zero;
                        body.angularVelocity = Vector3.zero;
                    }
                    body.Sleep();
                }
                _isLaunched = false;
                gameObject.SetActive(false);
                return;
            }

            _elapsed += Time.deltaTime;
            if (persistUntilReset)
            {
                // Persist as real dynamic Rigidbodies for the entire round using PhysX Auto-Sleep.
                return;
            }

            if (!_isFrozen && _elapsed >= _freezeAfterSeconds)
            {
                FreezePhysics();
            }

            if (_elapsed >= _hideAfterSeconds)
            {
                _isLaunched = false;
                gameObject.SetActive(false);
            }
        }

        private void FreezePhysics()
        {
            _isFrozen = true;
            if (body != null)
            {
                if (!body.isKinematic)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
                body.isKinematic = true;
                body.Sleep();
            }

            if (fragmentCollider != null)
            {
                fragmentCollider.enabled = false;
            }
        }
    }
}
