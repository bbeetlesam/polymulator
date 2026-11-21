using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    /// <summary>
    /// Elevated 2.5D isometric camera with horizontal player tracking, impulse trauma,
    /// subtle right-drag look-around peek (+/- 24 deg), [C] recenter, and scroll zoom when idle.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class RectCameraController : MonoBehaviour
    {
        [Header("Target")]
        public Transform target;

        [Header("Fixed Framing (grid units)")]
        [Min(0.1f)] public float orthographicSize = 3.85f;
        public float yaw = 40f;
        [Range(15f, 85f)] public float pitch = 35f;
        [Tooltip("Horizontal distance from the focus point on the XZ plane. Independent of cameraHeight.")]
        [Min(0f)] public float cameraDistance = 25f;
        [Tooltip("Vertical offset above the focus point. Independent of cameraDistance and pitch.")]
        [Min(0f)] public float cameraHeight = 18f;
        public float focusHeight = 0f;

        [Header("Follow & Peek")]
        public bool followTarget = true;
        [Min(0f)] public float followSmoothTime = 0.15f;
        public float minOrthoSize = 2.8f;
        public float maxOrthoSize = 5.4f;
        public float maxPeekYaw = 24f;
        public float maxPeekPitch = 7f;

        private Camera cam;
        private Quaternion fixedRotation;
        private Vector3 positionOffset;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private float impulseTrauma;
        private float targetOrthoSize;
        private float currentOrthoSize;
        private float targetPeekYaw;
        private float currentPeekYaw;
        private float targetPeekPitch;
        private float currentPeekPitch;
        private ObjectCarryController carryController;
        private TelekinesisController telekinesisController;
        private static RectCameraController activeInstance;

        public static void AddImpulse(float strength)
        {
            if (activeInstance != null)
                activeInstance.impulseTrauma = Mathf.Clamp(activeInstance.impulseTrauma + strength, 0f, 0.25f);
        }

        private void Awake()
        {
            activeInstance = this;
            cam = GetComponent<Camera>();
            currentOrthoSize = targetOrthoSize = orthographicSize;
            ApplyFraming(yaw, pitch, currentOrthoSize);
        }

        private void OnDestroy()
        {
            if (activeInstance == this) activeInstance = null;
        }

        private void Start()
        {
            if (target == null)
            {
                RectController rect = Object.FindFirstObjectByType<RectController>();
                if (rect != null) target = rect.transform;
            }
            carryController = Object.FindFirstObjectByType<ObjectCarryController>();
            telekinesisController = Object.FindFirstObjectByType<TelekinesisController>();

            SnapToTarget();
        }

        private void LateUpdate()
        {
            if (!followTarget || target == null) return;

            HandlePeekAndZoomInput();

            Vector3 desiredFocus = GetDesiredFocus();
            focus = followSmoothTime > 0f
                ? Vector3.SmoothDamp(focus, desiredFocus, ref focusVelocity, followSmoothTime)
                : desiredFocus;

            Vector3 shakeOffset = Vector3.zero;
            if (impulseTrauma > 0.0005f)
            {
                float s = impulseTrauma * GridSettings.Unit;
                shakeOffset = new Vector3(
                    Mathf.Sin(Time.time * 47f) * s,
                    Mathf.Cos(Time.time * 53f) * s * 0.6f,
                    Mathf.Sin(Time.time * 39f) * s);
                impulseTrauma = Mathf.MoveTowards(impulseTrauma, 0f, Time.deltaTime * 0.85f);
            }

            float blend = 1f - Mathf.Exp(-12f * Time.deltaTime);
            currentPeekYaw = Mathf.Lerp(currentPeekYaw, targetPeekYaw, blend);
            currentPeekPitch = Mathf.Lerp(currentPeekPitch, targetPeekPitch, blend);
            currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, blend);
            ApplyFraming(yaw + currentPeekYaw, pitch + currentPeekPitch, currentOrthoSize);

            transform.SetPositionAndRotation(focus + positionOffset + shakeOffset, fixedRotation);
        }

        private void HandlePeekAndZoomInput()
        {
            if (Time.timeScale <= 0f) return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                Vector2 delta = mouse.delta.ReadValue();
                targetPeekYaw = Mathf.Clamp(targetPeekYaw + delta.x * 0.16f, -maxPeekYaw, maxPeekYaw);
                targetPeekPitch = Mathf.Clamp(targetPeekPitch - delta.y * 0.08f, -maxPeekPitch, maxPeekPitch);
            }

            bool manipulatingObject = (carryController != null && carryController.IsCarrying) ||
                                      (telekinesisController != null && telekinesisController.HasTether);
            if (!manipulatingObject && mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                    targetOrthoSize = Mathf.Clamp(targetOrthoSize - Mathf.Sign(scrollY) * 0.28f, minOrthoSize, maxOrthoSize);
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                targetPeekYaw = 0f;
                targetPeekPitch = 0f;
                targetOrthoSize = orthographicSize;
            }
        }

        private void ApplyFraming(float activeYaw, float activePitch, float activeOrthoGrid)
        {
            if (cam == null) cam = GetComponent<Camera>();

            cam.orthographic = true;
            cam.orthographicSize = GridSettings.ToWorld(activeOrthoGrid);
            cam.nearClipPlane = GridSettings.ToWorld(0.1f);
            cam.farClipPlane = Mathf.Max(cam.nearClipPlane + 1f, GridSettings.ToWorld(250f));

            fixedRotation = Quaternion.Euler(activePitch, activeYaw, 0f);

            Vector3 horizontal = Quaternion.Euler(0f, activeYaw, 0f) * Vector3.back * GridSettings.ToWorld(cameraDistance);
            positionOffset = horizontal + Vector3.up * GridSettings.ToWorld(cameraHeight);
            transform.rotation = fixedRotation;
        }

        private void ApplyFixedFraming() => ApplyFraming(yaw, pitch, orthographicSize);

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
