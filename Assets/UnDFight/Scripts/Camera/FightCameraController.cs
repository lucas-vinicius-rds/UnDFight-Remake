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

        private float _currentDistance;

        public void Configure(Transform p1, Transform p2, Transform midpoint, CinemachineCamera vcam, CinemachinePositionComposer composer)
        {
            fighter1Transform = p1;
            fighter2Transform = p2;
            focusMidpoint = midpoint;
            cinemachineCamera = vcam;
            positionComposer = composer;
            _currentDistance = minDistance;
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

            float separationX = Mathf.Abs(p1.x - p2.x);
            float separationY = Mathf.Abs(p1.y - p2.y);
            float desiredDistance = Mathf.Clamp(minDistance + (separationX + separationY * 0.5f) * 0.48f + distancePadding * 0.25f, minDistance, maxDistance);

            _currentDistance = Mathf.Lerp(_currentDistance, desiredDistance, 1f - Mathf.Exp(-zoomSmoothSpeed * dt));

            float halfViewWidth = _currentDistance * 0.45f;
            float clampedMin = Mathf.Min(stageMinX + halfViewWidth, 0f);
            float clampedMax = Mathf.Max(stageMaxX - halfViewWidth, 0f);

            float midX = Mathf.Clamp((p1.x + p2.x) * 0.5f, clampedMin, clampedMax);
            float midY = Mathf.Max(0f, (p1.y + p2.y) * 0.5f) + heightOffset;

            focusMidpoint.position = new Vector3(midX, midY, 0f);

            if (positionComposer != null)
            {
                positionComposer.CameraDistance = _currentDistance;
            }
        }
    }
}
