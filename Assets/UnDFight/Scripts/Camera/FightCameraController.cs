using Unity.Cinemachine;
using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// 2.5D side-scrolling fighting camera using Cinemachine.
    /// Focuses on the midpoint of both fighters, zooms dynamically with distance,
    /// and clamps horizontal travel to configured stage bounds.
    /// </summary>
    public class FightCameraController : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private Transform fighter1Transform;
        [SerializeField] private Transform fighter2Transform;
        [SerializeField] private Transform focusMidpoint;

        [Header("Cinemachine")]
        [SerializeField] private CinemachineCamera cinemachineCamera;
        [SerializeField] private CinemachinePositionComposer positionComposer;

        [Header("Framing & Dynamic Zoom")]
        [SerializeField] private float minDistance = 6.5f;
        [SerializeField] private float maxDistance = 12.0f;
        [SerializeField] private float distancePadding = 2.6f;
        [SerializeField] private float heightOffset = 1.35f;
        [SerializeField] private float stageMinX = -8.0f;
        [SerializeField] private float stageMaxX = 8.0f;
        [SerializeField] private float zoomSmoothSpeed = 10f;

        [Header("Impact Camera Shake")]
        [SerializeField] private float maxShakeOffset = 0.25f;

        private float _currentDistance;
        private float _shakeIntensity;
        private float _shakeDuration;
        private float _shakeElapsed;
        private float _shakePhase;
        private Transform _superFocusTarget;
        private float _superFocusRemaining;

        public float CurrentShakeOffsetMagnitude { get; private set; }
        public float CurrentDistance => _currentDistance;
        public bool IsSuperCameraActive => _superFocusRemaining > 0f && _superFocusTarget != null;

        public void TriggerSuperCameraFocus(Transform target, float durationSeconds)
        {
            if (target == null || durationSeconds <= 0f)
            {
                return;
            }

            _superFocusTarget = target;
            _superFocusRemaining = Mathf.Max(_superFocusRemaining, durationSeconds);
        }

        public void TriggerImpactShake(float intensity, float duration)
        {
            if (intensity <= 0f || duration <= 0f)
            {
                return;
            }

            float clamped = Mathf.Min(intensity, maxShakeOffset);
            if (clamped >= _shakeIntensity * (1f - (_shakeDuration > 0f ? _shakeElapsed / _shakeDuration : 1f)))
            {
                _shakeIntensity = clamped;
                _shakeDuration = duration;
                _shakeElapsed = 0f;
            }
        }

        public void Configure(Transform p1, Transform p2, Transform midpoint, CinemachineCamera vcam, CinemachinePositionComposer composer)
        {
            fighter1Transform = p1;
            fighter2Transform = p2;
            focusMidpoint = midpoint;
            cinemachineCamera = vcam;
            positionComposer = composer;
            _currentDistance = minDistance;
        }

        public void ConfigureTargets(Transform p1, Transform p2, Transform midpoint)
        {
            fighter1Transform = p1;
            fighter2Transform = p2;
            focusMidpoint = midpoint;
            _currentDistance = minDistance;
        }

        public void SetTargets(Transform p1, Transform p2)
        {
            fighter1Transform = p1;
            fighter2Transform = p2;
        }

        private void Awake()
        {
            _currentDistance = minDistance;
            if (cinemachineCamera != null && positionComposer == null)
            {
                positionComposer = cinemachineCamera.GetComponent<CinemachinePositionComposer>();
            }
        }

        public void StepTick(float dt)
        {
            if (fighter1Transform == null || fighter2Transform == null || focusMidpoint == null)
            {
                return;
            }

            Vector3 p1 = fighter1Transform.position;
            Vector3 p2 = fighter2Transform.position;

            float desiredDistance;
            float targetMidX;
            float targetMidY;
            float smoothRate = zoomSmoothSpeed;

            if (_superFocusRemaining > 0f && _superFocusTarget != null)
            {
                _superFocusRemaining = Mathf.Max(0f, _superFocusRemaining - dt);
                desiredDistance = Mathf.Max(3.6f, minDistance * 0.62f);
                targetMidX = Mathf.Clamp(_superFocusTarget.position.x, stageMinX + 1.8f, stageMaxX - 1.8f);
                targetMidY = Mathf.Max(0f, _superFocusTarget.position.y) + heightOffset * 0.92f;
                smoothRate = zoomSmoothSpeed * 2.4f;
                if (_superFocusRemaining <= 0f)
                {
                    _superFocusTarget = null;
                }
            }
            else
            {
                float separationX = Mathf.Abs(p1.x - p2.x);
                float separationY = Mathf.Abs(p1.y - p2.y);
                desiredDistance = Mathf.Clamp(minDistance + (separationX + separationY * 0.5f) * 0.48f + distancePadding * 0.25f, minDistance, maxDistance);

                float halfViewWidth = Mathf.Max(desiredDistance, _currentDistance) * 0.45f;
                float clampedMin = Mathf.Min(stageMinX + halfViewWidth, 0f);
                float clampedMax = Mathf.Max(stageMaxX - halfViewWidth, 0f);

                targetMidX = Mathf.Clamp((p1.x + p2.x) * 0.5f, clampedMin, clampedMax);
                targetMidY = Mathf.Max(0f, (p1.y + p2.y) * 0.5f) + heightOffset;
            }

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-smoothRate * dt));

            Vector2 shakeOffset = EvaluateShakeOffset(dt);
            Vector3 currentPos = focusMidpoint.position;
            float nextX = Mathf.Lerp(currentPos.x, targetMidX, 1f - Mathf.Exp(-smoothRate * dt)) + shakeOffset.x;
            float nextY = Mathf.Lerp(currentPos.y, targetMidY, 1f - Mathf.Exp(-smoothRate * dt)) + shakeOffset.y;
            focusMidpoint.position = new Vector3(nextX, nextY, 0f);

            if (positionComposer != null)
            {
                positionComposer.CameraDistance = _currentDistance;
            }
        }

        private Vector2 EvaluateShakeOffset(float dt)
        {
            if (_shakeDuration <= 0f || _shakeElapsed >= _shakeDuration)
            {
                CurrentShakeOffsetMagnitude = 0f;
                return Vector2.zero;
            }

            _shakeElapsed += dt;
            float progress = Mathf.Clamp01(_shakeElapsed / _shakeDuration);
            float envelope = 1f - progress;
            _shakePhase += dt * 95f;

            float sx = Mathf.Sin(_shakePhase) * _shakeIntensity * envelope;
            float sy = Mathf.Cos(_shakePhase * 1.37f) * (_shakeIntensity * 0.65f) * envelope;
            var offset = new Vector2(sx, sy);
            CurrentShakeOffsetMagnitude = offset.magnitude;
            return offset;
        }
    }
}
