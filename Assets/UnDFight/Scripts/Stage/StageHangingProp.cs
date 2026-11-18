using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Reusable suspended stage prop (hanging sign, ceiling lamp, banner, clothesline) driven by
    /// a real NVIDIA PhysX HingeJoint connecting a kinematic anchor to a dynamic StageDebris swinging body.
    /// Reacts naturally to aerodynamic wind gusts and combat shockwaves, and resets cleanly each round.
    /// </summary>
    public class StageHangingProp : MonoBehaviour, IStageResettable
    {
        [Header("PhysX Hinge Setup")]
        [SerializeField] private Rigidbody anchorBody;
        [SerializeField] private Rigidbody swingBody;
        [SerializeField] private HingeJoint hingeJoint;
        [SerializeField] private StageWindAffectable windAffectable;

        [Header("Pendulum Tuning")]
        [SerializeField] private Vector3 hingeAxis = Vector3.forward;
        [SerializeField] private Vector3 localPivotOnSwingBody = new Vector3(0f, 0.35f, 0f);
        [SerializeField, Min(0.1f)] private float swingMass = 1.4f;
        [SerializeField, Min(0f)] private float linearDamping = 0.25f;
        [SerializeField, Min(0f)] private float angularDamping = 0.85f;
        [SerializeField] private bool useSpring = true;
        [SerializeField, Min(0f)] private float springStrength = 6.5f;
        [SerializeField, Min(0f)] private float springDamper = 1.6f;
        [SerializeField] private bool useLimits = true;
        [SerializeField, Range(10f, 85f)] private float maxSwingAngle = 55f;

        private Vector3 _initialSwingLocalPos;
        private Quaternion _initialSwingLocalRot;
        private bool _cachedInitial;

        public Rigidbody AnchorBody => anchorBody;
        public Rigidbody SwingBody => swingBody;
        public HingeJoint Hinge => hingeJoint;
        public StageWindAffectable WindAffectable => windAffectable;

        private void Awake()
        {
            EnsureComponents();
            CacheInitialTransform();
        }

        public void CacheInitialTransform()
        {
            if (_cachedInitial || swingBody == null)
            {
                return;
            }

            _initialSwingLocalPos = swingBody.transform.localPosition;
            _initialSwingLocalRot = swingBody.transform.localRotation;
            _cachedInitial = true;
        }

        public void EnsureComponents()
        {
            int debrisLayer = LayerMask.NameToLayer(StagePhysicsLayers.StageDebrisLayerName);

            if (anchorBody == null)
            {
                anchorBody = GetComponent<Rigidbody>();
            }
            if (anchorBody == null)
            {
                anchorBody = gameObject.AddComponent<Rigidbody>();
            }
            anchorBody.isKinematic = true;
            anchorBody.useGravity = false;
            anchorBody.interpolation = RigidbodyInterpolation.None;

            if (swingBody == null)
            {
                Transform child = transform.Find("HangingBody");
                if (child != null)
                {
                    swingBody = child.GetComponent<Rigidbody>();
                    if (swingBody == null)
                    {
                        swingBody = child.gameObject.AddComponent<Rigidbody>();
                    }
                }
                else
                {
                    var bodyGo = new GameObject("HangingBody");
                    bodyGo.transform.SetParent(transform, false);
                    bodyGo.transform.localPosition = new Vector3(0f, -localPivotOnSwingBody.y, 0f);
                    bodyGo.transform.localRotation = Quaternion.identity;
                    swingBody = bodyGo.AddComponent<Rigidbody>();
                }
            }

            if (debrisLayer >= 0)
            {
                swingBody.gameObject.layer = debrisLayer;
            }
            swingBody.isKinematic = false;
            swingBody.useGravity = true;
            swingBody.mass = Mathf.Max(0.1f, swingMass);
            swingBody.linearDamping = linearDamping;
            swingBody.angularDamping = angularDamping;
            swingBody.sleepThreshold = 0.02f;
            swingBody.interpolation = RigidbodyInterpolation.Interpolate;

            if (swingBody.GetComponent<Collider>() == null)
            {
                var box = swingBody.gameObject.AddComponent<BoxCollider>();
                box.size = new Vector3(0.95f, 0.45f, 0.08f);
                box.isTrigger = false;
            }

            if (hingeJoint == null)
            {
                hingeJoint = swingBody.GetComponent<HingeJoint>();
            }
            if (hingeJoint == null)
            {
                hingeJoint = swingBody.gameObject.AddComponent<HingeJoint>();
            }

            hingeJoint.connectedBody = anchorBody;
            hingeJoint.axis = hingeAxis.sqrMagnitude > 1e-6f ? hingeAxis.normalized : Vector3.forward;
            hingeJoint.anchor = localPivotOnSwingBody;
            hingeJoint.autoConfigureConnectedAnchor = false;
            hingeJoint.connectedAnchor = Vector3.zero;
            hingeJoint.useSpring = useSpring;
            hingeJoint.spring = new JointSpring
            {
                spring = springStrength,
                damper = springDamper,
                targetPosition = 0f
            };
            hingeJoint.useLimits = useLimits;
            hingeJoint.limits = new JointLimits
            {
                min = -maxSwingAngle,
                max = maxSwingAngle,
                bounciness = 0.1f
            };

            if (windAffectable == null)
            {
                windAffectable = swingBody.GetComponent<StageWindAffectable>();
            }
            if (windAffectable == null)
            {
                windAffectable = swingBody.gameObject.AddComponent<StageWindAffectable>();
                windAffectable.Configure(cd: 1.25f, areaM2: 0.36f, sensitivity: 1.4f, wakeThreshold: 1.0f, bodyOverride: swingBody);
            }
            else
            {
                windAffectable.TargetBody = swingBody;
            }
        }

        public void ApplyImpulse(Vector3 impulse, ForceMode mode = ForceMode.Impulse)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            EnsureComponents();
            if (swingBody != null && !swingBody.isKinematic)
            {
                swingBody.WakeUp();
                swingBody.AddForce(impulse, mode);
            }
        }

        public void ResetProp()
        {
            EnsureComponents();
            CacheInitialTransform();

            if (swingBody == null)
            {
                return;
            }

            swingBody.transform.localPosition = _initialSwingLocalPos;
            swingBody.transform.localRotation = _initialSwingLocalRot;
            if (!swingBody.isKinematic)
            {
                swingBody.linearVelocity = Vector3.zero;
                swingBody.angularVelocity = Vector3.zero;
            }
            swingBody.position = swingBody.transform.position;
            swingBody.rotation = swingBody.transform.rotation;
            Physics.SyncTransforms();
            swingBody.Sleep();
        }
    }
}
