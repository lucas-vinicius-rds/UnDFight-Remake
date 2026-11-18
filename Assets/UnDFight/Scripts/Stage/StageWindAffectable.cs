using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Aerodynamic drag receiver component for PhysX stage objects (StageDebrisFragment,
    /// StageDynamicProp, StageHangingProp). Consumed by StageWindController in FixedUpdate
    /// using the real drag equation: F_drag = 0.5 * rho * Cd * A * |v_rel| * v_rel.
    /// </summary>
    public class StageWindAffectable : MonoBehaviour
    {
        private static readonly List<StageWindAffectable> s_activeAffectables = new List<StageWindAffectable>();

        [SerializeField] private Rigidbody targetBody;
        [SerializeField, Min(0.05f)] private float dragCoefficient = 1.05f;
        [SerializeField, Min(0.005f)] private float crossSectionArea = 0.14f;
        [SerializeField, Min(0f)] private float windSensitivity = 1.0f;
        [SerializeField, Min(0f)] private float wakeGustThreshold = 2.4f;

        public static IReadOnlyList<StageWindAffectable> ActiveAffectables => s_activeAffectables;

        public Rigidbody TargetBody
        {
            get
            {
                if (targetBody == null)
                {
                    targetBody = GetComponent<Rigidbody>();
                }
                return targetBody;
            }
            set => targetBody = value;
        }

        public float DragCoefficient { get => dragCoefficient; set => dragCoefficient = Mathf.Max(0.05f, value); }
        public float CrossSectionArea { get => crossSectionArea; set => crossSectionArea = Mathf.Max(0.005f, value); }
        public float WindSensitivity { get => windSensitivity; set => windSensitivity = Mathf.Max(0f, value); }
        public float WakeGustThreshold { get => wakeGustThreshold; set => wakeGustThreshold = Mathf.Max(0f, value); }

        public void Configure(
            float cd,
            float areaM2,
            float sensitivity = 1.0f,
            float wakeThreshold = 2.4f,
            Rigidbody bodyOverride = null)
        {
            dragCoefficient = Mathf.Max(0.05f, cd);
            crossSectionArea = Mathf.Max(0.005f, areaM2);
            windSensitivity = Mathf.Max(0f, sensitivity);
            wakeGustThreshold = Mathf.Max(0f, wakeThreshold);
            if (bodyOverride != null)
            {
                targetBody = bodyOverride;
            }
            else if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
        }

        private void Awake()
        {
            if (targetBody == null)
            {
                targetBody = GetComponent<Rigidbody>();
            }
        }

        private void OnEnable()
        {
            if (!s_activeAffectables.Contains(this))
            {
                s_activeAffectables.Add(this);
            }
        }

        private void OnDisable()
        {
            s_activeAffectables.Remove(this);
        }

        private void OnDestroy()
        {
            s_activeAffectables.Remove(this);
        }
    }
}
