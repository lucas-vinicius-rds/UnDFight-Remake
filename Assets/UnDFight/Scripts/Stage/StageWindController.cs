using System.Collections.Generic;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Reusable aerodynamic wind field controller for any UnDFight arena.
    /// Computes an organic wind velocity vector (base speed + Perlin/sine gusts) in FixedUpdate
    /// and applies real quadratic aerodynamic drag F_drag = 0.5 * rho * Cd * A * |v_rel| * v_rel
    /// to every active StageWindAffectable Rigidbody while preserving PhysX Auto-Sleep under weak breezes.
    /// </summary>
    public class StageWindController : MonoBehaviour
    {
        public const float StandardAirDensity = 1.225f;

        [Header("Aerodynamic Wind Field")]
        [SerializeField] private bool windEnabled = true;
        [SerializeField] private Vector3 windDirection = new Vector3(1f, 0f, 0.12f);
        [SerializeField, Min(0f)] private float baseWindSpeed = 0.55f;
        [SerializeField, Min(0f)] private float gustAmplitude = 0.45f;
        [SerializeField, Min(0.01f)] private float gustFrequency = 0.22f;
        [SerializeField, Min(0.1f)] private float airDensity = StandardAirDensity;

        private readonly List<StageWindAffectable> _overrideAffectables = new List<StageWindAffectable>();
        private float _elapsedTime;
        private Vector3 _currentWindVelocity;

        public bool WindEnabled { get => windEnabled; set => windEnabled = value; }
        public Vector3 WindDirection { get => windDirection; set => windDirection = value.sqrMagnitude > 1e-6f ? value.normalized : Vector3.right; }
        public float BaseWindSpeed { get => baseWindSpeed; set => baseWindSpeed = Mathf.Max(0f, value); }
        public float GustAmplitude { get => gustAmplitude; set => gustAmplitude = Mathf.Max(0f, value); }
        public float GustFrequency { get => gustFrequency; set => gustFrequency = Mathf.Max(0.01f, value); }
        public float AirDensity { get => airDensity; set => airDensity = Mathf.Max(0.1f, value); }

        public Vector3 CurrentWindVelocity => _currentWindVelocity;
        public float CurrentWindSpeed => _currentWindVelocity.magnitude;

        private void Awake()
        {
            _currentWindVelocity = ComputeWindVelocity(0f);
        }

        public void Configure(
            Vector3 direction,
            float baseSpeed,
            float gustAmp,
            float gustFreq,
            float rho = StandardAirDensity,
            bool enabled = true)
        {
            windDirection = direction.sqrMagnitude > 1e-6f ? direction.normalized : Vector3.right;
            baseWindSpeed = Mathf.Max(0f, baseSpeed);
            gustAmplitude = Mathf.Max(0f, gustAmp);
            gustFrequency = Mathf.Max(0.01f, gustFreq);
            airDensity = Mathf.Max(0.1f, rho);
            windEnabled = enabled;
            _currentWindVelocity = ComputeWindVelocity(_elapsedTime);
        }

        public void ApplyWeatherPreset(StageWeatherVariant variant)
        {
            switch (variant)
            {
                case StageWeatherVariant.Night:
                    baseWindSpeed = 1.05f;
                    gustAmplitude = 0.75f;
                    gustFrequency = 0.30f;
                    break;
                case StageWeatherVariant.RainyNight:
                    baseWindSpeed = 6.2f;
                    gustAmplitude = 4.5f;
                    gustFrequency = 0.65f;
                    break;
                default:
                    baseWindSpeed = 0.55f;
                    gustAmplitude = 0.45f;
                    gustFrequency = 0.22f;
                    break;
            }
            _currentWindVelocity = ComputeWindVelocity(_elapsedTime);
        }

        /// <summary>
        /// Evaluates instantaneous wind velocity (m/s) combining base flow with Perlin + sine gusts.
        /// </summary>
        public Vector3 ComputeWindVelocity(float timeSeconds)
        {
            if (!windEnabled)
            {
                return Vector3.zero;
            }

            Vector3 dir = windDirection.sqrMagnitude > 1e-6f ? windDirection.normalized : Vector3.right;
            float perlin = Mathf.PerlinNoise(timeSeconds * gustFrequency + 1.37f, 0.73f); // [0..1]
            float sine = 0.5f + 0.5f * Mathf.Sin(timeSeconds * gustFrequency * Mathf.PI * 2f); // [0..1]
            float gustFactor = 0.6f * perlin + 0.4f * sine;

            float speed = Mathf.Max(0f, baseWindSpeed + gustAmplitude * gustFactor);

            // Subtle cross-flow swirl in Z so gusts feel organic
            float lateralSway = (Mathf.PerlinNoise(0.29f, timeSeconds * gustFrequency * 0.8f) - 0.5f) * 0.25f;
            Vector3 effectiveDir = (dir + new Vector3(0f, 0f, lateralSway)).normalized;
            return effectiveDir * speed;
        }

        /// <summary>
        /// Real quadratic aerodynamic drag equation:
        /// v_rel = windVelocity - bodyVelocity
        /// F_drag = 0.5 * rho * Cd * A * sensitivity * |v_rel| * v_rel
        /// </summary>
        public static Vector3 ComputeDragForce(
            Vector3 windVelocity,
            Vector3 bodyVelocity,
            float airDensityKgM3,
            float dragCoefficient,
            float crossSectionAreaM2,
            float sensitivity = 1.0f)
        {
            Vector3 vRel = windVelocity - bodyVelocity;
            float speed = vRel.magnitude;
            if (speed < 1e-4f || sensitivity <= 0f)
            {
                return Vector3.zero;
            }

            float coeff = 0.5f * airDensityKgM3 * dragCoefficient * crossSectionAreaM2 * sensitivity;
            return coeff * speed * vRel;
        }

        public void RefreshAffectablesFromScene()
        {
            _overrideAffectables.Clear();
            StageWindAffectable[] found = FindObjectsByType<StageWindAffectable>(FindObjectsInactive.Exclude);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] != null)
                {
                    _overrideAffectables.Add(found[i]);
                }
            }
        }

        private void FixedUpdate()
        {
            StepWind(Time.fixedDeltaTime);
        }

        /// <summary>
        /// Steps wind field evaluation and applies aerodynamic drag forces to all active StageWindAffectable bodies.
        /// Exposed publicly for deterministic Edit Mode testing.
        /// </summary>
        public void StepWind(float dt, bool useSceneDiscoveryFallback = false)
        {
            _elapsedTime += Mathf.Max(0f, dt);
            _currentWindVelocity = ComputeWindVelocity(_elapsedTime);

            if (!windEnabled || _currentWindVelocity.sqrMagnitude < 1e-6f)
            {
                return;
            }

            float windSpeed = _currentWindVelocity.magnitude;
            IReadOnlyList<StageWindAffectable> list = StageWindAffectable.ActiveAffectables;
            if (useSceneDiscoveryFallback || list.Count == 0)
            {
                RefreshAffectablesFromScene();
                list = _overrideAffectables;
            }

            for (int i = 0; i < list.Count; i++)
            {
                StageWindAffectable affectable = list[i];
                if (affectable == null || !affectable.isActiveAndEnabled)
                {
                    continue;
                }

                Rigidbody rb = affectable.TargetBody;
                if (rb == null || rb.isKinematic)
                {
                    continue;
                }

                if (rb.IsSleeping())
                {
                    if (windSpeed < affectable.WakeGustThreshold)
                    {
                        continue;
                    }
                    rb.WakeUp();
                }

                Vector3 dragForce = ComputeDragForce(
                    _currentWindVelocity,
                    rb.linearVelocity,
                    airDensity,
                    affectable.DragCoefficient,
                    affectable.CrossSectionArea,
                    affectable.WindSensitivity);

                if (dragForce.sqrMagnitude > 1e-8f)
                {
                    rb.AddForce(dragForce, ForceMode.Force);
                }
            }
        }
    }
}
