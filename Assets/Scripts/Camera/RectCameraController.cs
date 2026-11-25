using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    /// <summary>
    /// Elevated 2.5D isometric camera with arrival zoom-in -> smooth zoom-out reveal on RECT,
    /// horizontal player tracking, impulse trauma, unbounded right-drag horizontal orbit around RECT,
    /// [C] recenter, and scroll zoom when idle.
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

        [Header("Spawn Arrival Zoom")]
        public bool introZoomOnStart = true;
        public bool introStartBehindRect = true;
        [Range(5f, 40f)] public float introStartPitch = 22f;
        [Min(1.5f)] public float introStartDistance = 5.6f;
        [Min(0.4f)] public float introStartHeight = 3.0f;
        [Min(0.8f)] public float introStartOrthoSize = 2.65f;
        [Min(0f)] public float introHoldSeconds = 0.08f;
        [Min(0.2f)] public float introZoomOutSeconds = 1.05f;

        public float CurrentActiveYaw { get; private set; }
        public float CurrentActivePitch { get; private set; }
        public bool IsIntroPlaying => introZoomActive;

        [Header("Follow & Orbit")]
        public bool followTarget = true;
        [Min(0f)] public float followSmoothTime = 0.15f;
        public float minOrthoSize = 2.8f;
        public float maxOrthoSize = 5.4f;

        private Camera cam;
        private Quaternion fixedRotation;
        private Vector3 positionOffset;
        private Vector3 focus;
        private Vector3 focusVelocity;
        private float impulseTrauma;
        private float targetOrthoSize;
        private float currentOrthoSize;
        private float targetYawOffset;
        private float currentYawOffset;
        private bool introZoomActive;
        private float introElapsed;
        private ObjectCarryController carryController;
        private TelekinesisController telekinesisController;
        private static RectCameraController activeInstance;

        public static void AddImpulse(float strength)
        {
            if (activeInstance != null && !SimulationSettings.ReduceMotion)
                activeInstance.impulseTrauma = Mathf.Clamp(activeInstance.impulseTrauma + strength, 0f, 0.25f);
        }

        private void Awake()
        {
            activeInstance = this;
            cam = GetComponent<Camera>();
            currentOrthoSize = targetOrthoSize = orthographicSize;
            CurrentActiveYaw = yaw;
            CurrentActivePitch = pitch;
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

            if (introZoomOnStart && !SimulationSettings.ReduceMotion)
            {
                introZoomActive = true;
                introElapsed = 0f;
                currentOrthoSize = introStartOrthoSize;
                targetOrthoSize = orthographicSize;
            }
            else
            {
                introZoomActive = false;
                currentOrthoSize = targetOrthoSize = orthographicSize;
            }

            SnapToTarget();

            if (introZoomActive && introStartBehindRect && target != null)
            {
                float startYaw = target.eulerAngles.y;
                CurrentActiveYaw = startYaw;
                CurrentActivePitch = introStartPitch;
                focus = GetDesiredFocus();
                ApplyFraming(startYaw, introStartPitch, currentOrthoSize, introStartDistance, introStartHeight);
                transform.SetPositionAndRotation(focus + positionOffset, fixedRotation);
            }
            else
            {
                CurrentActiveYaw = yaw;
                CurrentActivePitch = pitch;
            }

            if (target != null)
                SimulationVFX.PlaySpawnAperture(target.position, PaletteSettings.Sapphire);
        }

        private void LateUpdate()
        {
            if (!followTarget || target == null) return;

            HandleOrbitAndZoomInput();

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
            currentYawOffset = WrapYawOffset(Mathf.LerpAngle(currentYawOffset, targetYawOffset, blend));

            float activeYaw = yaw + currentYawOffset;
            float activePitch = pitch;
            float activeDist = cameraDistance;
            float activeHeight = cameraHeight;

            if (introZoomActive)
            {
                // Clamp frame spikes from scene deserialization so players always experience the arrival close-up.
                introElapsed += Mathf.Min(Time.unscaledDeltaTime, 0.045f);
                float eased = 0f;
                if (introElapsed < introHoldSeconds)
                {
                    currentOrthoSize = introStartOrthoSize;
                }
                else
                {
                    float u = Mathf.Clamp01((introElapsed - introHoldSeconds) / Mathf.Max(0.1f, introZoomOutSeconds));
                    eased = 1f - Mathf.Pow(1f - u, 3f);
                    currentOrthoSize = Mathf.Lerp(introStartOrthoSize, orthographicSize, eased);
                    if (u >= 1f)
                    {
                        introZoomActive = false;
                        currentOrthoSize = targetOrthoSize = orthographicSize;
                    }
                }

                if (introStartBehindRect && target != null)
                {
                    float behindYaw = target.eulerAngles.y;
                    activeYaw = Mathf.LerpAngle(behindYaw, yaw + currentYawOffset, eased);
                    activePitch = Mathf.Lerp(introStartPitch, pitch, eased);
                    activeDist = Mathf.Lerp(introStartDistance, cameraDistance, eased);
                    activeHeight = Mathf.Lerp(introStartHeight, cameraHeight, eased);
                }
            }
            else
            {
                currentOrthoSize = Mathf.Lerp(currentOrthoSize, targetOrthoSize, blend);
            }

            CurrentActiveYaw = activeYaw;
            CurrentActivePitch = activePitch;
            ApplyFraming(activeYaw, activePitch, currentOrthoSize, activeDist, activeHeight);
            transform.SetPositionAndRotation(focus + positionOffset + shakeOffset, fixedRotation);
        }

        private void HandleOrbitAndZoomInput()
        {
            if (Time.timeScale <= 0f) return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (mouse != null && mouse.rightButton.isPressed)
            {
                float sens = SimulationSettings.CameraSensitivity;
                targetYawOffset = WrapYawOffset(targetYawOffset + mouse.delta.ReadValue().x * 0.16f * sens);
            }

            bool manipulatingObject = (carryController != null && carryController.IsCarrying) ||
                                      (telekinesisController != null && telekinesisController.HasTether);
            if (!manipulatingObject && mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    introZoomActive = false;
                    targetOrthoSize = Mathf.Clamp(targetOrthoSize - Mathf.Sign(scrollY) * 0.28f, minOrthoSize, maxOrthoSize);
                }
            }

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                introZoomActive = false;
                targetYawOffset = 0f;
                targetOrthoSize = orthographicSize;
            }
        }

        private static float WrapYawOffset(float degrees)
            => Mathf.Repeat(degrees + 180f, 360f) - 180f;

        private void ApplyFraming(float activeYaw, float activePitch, float activeOrthoGrid)
            => ApplyFraming(activeYaw, activePitch, activeOrthoGrid, cameraDistance, cameraHeight);

        private void ApplyFraming(float activeYaw, float activePitch, float activeOrthoGrid, float distGrid, float heightGrid)
        {
            if (cam == null) cam = GetComponent<Camera>();

            cam.orthographic = true;
            cam.orthographicSize = GridSettings.ToWorld(activeOrthoGrid);
            cam.nearClipPlane = GridSettings.ToWorld(0.1f);
            cam.farClipPlane = Mathf.Max(cam.nearClipPlane + 1f, GridSettings.ToWorld(250f));

            fixedRotation = Quaternion.Euler(activePitch, activeYaw, 0f);

            Vector3 horizontal = Quaternion.Euler(0f, activeYaw, 0f) * Vector3.back * GridSettings.ToWorld(distGrid);
            positionOffset = horizontal + Vector3.up * GridSettings.ToWorld(heightGrid);
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
