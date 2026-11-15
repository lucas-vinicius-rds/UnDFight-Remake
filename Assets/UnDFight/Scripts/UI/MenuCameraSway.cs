using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Applies a subtle, smooth cinematic drift to the Main Menu 3D diorama camera.
    /// </summary>
    public class MenuCameraSway : MonoBehaviour
    {
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.08f, 0.035f, 0.06f);
        [SerializeField] private Vector3 rotationAmplitudeEuler = new Vector3(0.45f, 0.90f, 0f);
        [SerializeField] private float frequencyHz = 0.22f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;
        private bool _captured;

        private void OnEnable()
        {
            CaptureOriginIfNeeded();
        }

        public void CaptureOriginIfNeeded()
        {
            if (_captured) return;
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
            _captured = true;
        }

        private void Update()
        {
            CaptureOriginIfNeeded();
            float t = Time.unscaledTime * frequencyHz * Mathf.PI * 2f;
            float s1 = Mathf.Sin(t);
            float c1 = Mathf.Cos(t * 0.73f);

            transform.localPosition = _basePosition + new Vector3(
                positionAmplitude.x * s1,
                positionAmplitude.y * c1,
                positionAmplitude.z * Mathf.Sin(t * 0.52f));

            transform.localRotation = _baseRotation * Quaternion.Euler(
                rotationAmplitudeEuler.x * c1,
                rotationAmplitudeEuler.y * s1,
                rotationAmplitudeEuler.z * s1);
        }
    }
}
