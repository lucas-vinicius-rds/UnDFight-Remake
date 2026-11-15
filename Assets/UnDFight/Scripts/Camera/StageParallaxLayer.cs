using UnityEngine;

namespace UnDFight
{
    /// <summary>
    /// Applies subtle depth parallax along X/Y to distant stage layers as the 2.5D fight camera pans.
    /// </summary>
    public class StageParallaxLayer : MonoBehaviour
    {
        [SerializeField, Range(0f, 0.9f)] private float parallaxFactorX = 0.2f;
        [SerializeField, Range(0f, 0.5f)] private float parallaxFactorY = 0.05f;
        [SerializeField] private Transform cameraTransform;

        private Vector3 _initialPosition;
        private Vector3 _initialCameraPosition;
        private bool _initialized;

        public void Configure(float factorX, float factorY = 0.05f, Transform targetCamera = null)
        {
            parallaxFactorX = Mathf.Clamp(factorX, 0f, 0.9f);
            parallaxFactorY = Mathf.Clamp(factorY, 0f, 0.5f);
            cameraTransform = targetCamera;
        }

        private void Start()
        {
            InitializeOrigin();
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                InitializeOrigin();
                if (!_initialized) return;
            }

            if (cameraTransform == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam == null) return;
                cameraTransform = mainCam.transform;
            }

            Vector3 camDelta = cameraTransform.position - _initialCameraPosition;
            transform.position = new Vector3(
                _initialPosition.x + camDelta.x * parallaxFactorX,
                _initialPosition.y + camDelta.y * parallaxFactorY,
                _initialPosition.z);
        }

        private void InitializeOrigin()
        {
            if (cameraTransform == null)
            {
                Camera mainCam = Camera.main;
                if (mainCam != null) cameraTransform = mainCam.transform;
            }
            if (cameraTransform == null) return;

            _initialPosition = transform.position;
            _initialCameraPosition = cameraTransform.position;
            _initialized = true;
        }
    }
}
