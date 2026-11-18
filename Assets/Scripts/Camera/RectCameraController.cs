using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Fixed elevated 2.5D camera. Orientation never changes and the camera only
    /// tracks the player on the horizontal plane so the whole simulation area stays readable.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RectCameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Fixed Framing (grid units)")]
        [Min(0.1f)] public float orthographicSize = 3f;
        public float yaw = 40f;
        [Range(15f, 85f)] public float pitch = 35f;
        [Tooltip("Horizontal distance from the focus point on the XZ plane. Independent of cameraHeight.")]
        [Min(0f)] public float cameraDistance = 25f;
        [Tooltip("Vertical offset above the focus point. Independent of cameraDistance and pitch.")]
        [Min(0f)] public float cameraHeight = 18f;
        public float focusHeight = 0f;

        [Header("Follow")]
        public bool followTarget = true;
        [Min(0f)] public float followSmoothTime = 0.15f;

        private Camera cam;
        private Quaternion fixedRotation;
        private Vector3 positionOffset;
        private Vector3 focus;
        private Vector3 focusVelocity;

        private void Awake()
        {
            cam = GetComponent<Camera>();
            ApplyFixedFraming();
        }

        private void Start()
        {
            if (target == null)
            {
                RectController rect = Object.FindFirstObjectByType<RectController>();
                if (rect != null) target = rect.transform;
            }

            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (!followTarget || target == null) return;

            Vector3 desiredFocus = GetDesiredFocus();
            focus = followSmoothTime > 0f
                ? Vector3.SmoothDamp(focus, desiredFocus, ref focusVelocity, followSmoothTime)
                : desiredFocus;

            // Position may follow the player horizontally; rotation is always fixed.
            transform.SetPositionAndRotation(focus + positionOffset, fixedRotation);
        }

        private void ApplyFixedFraming()
        {
            if (cam == null) cam = GetComponent<Camera>();

            cam.orthographic = true;
            cam.orthographicSize = GridSettings.ToWorld(orthographicSize);
            cam.nearClipPlane = GridSettings.ToWorld(0.1f);
            cam.farClipPlane = Mathf.Max(cam.nearClipPlane + 1f, GridSettings.ToWorld(250f));

            fixedRotation = Quaternion.Euler(pitch, yaw, 0f);

            // Height and horizontal distance are deliberately separate so either can be
            // adjusted in the Inspector without changing the other or the view angle.
            Vector3 horizontal = Quaternion.Euler(0f, yaw, 0f) * Vector3.back * GridSettings.ToWorld(cameraDistance);
            positionOffset = horizontal + Vector3.up * GridSettings.ToWorld(cameraHeight);
            transform.rotation = fixedRotation;
        }

        private void SnapToTarget()
        {
            focus = GetDesiredFocus();
            focusVelocity = Vector3.zero;
            transform.SetPositionAndRotation(focus + positionOffset, fixedRotation);
        }

        private Vector3 GetDesiredFocus()
        {
            float height = GridSettings.ToWorld(focusHeight);
            if (target == null) return new Vector3(0f, height, 0f);
            return new Vector3(target.position.x, height, target.position.z);
        }

        private void OnValidate()
        {
            orthographicSize = Mathf.Max(0.1f, orthographicSize);
            cameraDistance = Mathf.Max(0f, cameraDistance);
            cameraHeight = Mathf.Max(0f, cameraHeight);
            followSmoothTime = Mathf.Max(0f, followSmoothTime);
            if (cam == null) cam = GetComponent<Camera>();
            if (cam == null) return;

            ApplyFixedFraming();
            if (target != null) SnapToTarget();
        }
    }
}
