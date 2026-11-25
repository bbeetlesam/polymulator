using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Updates global URP shader coordinates so ChamberGrid renders reactive rings,
    /// Limited-mode orthogonal movement clue grid tiles, carry target tile highlight,
    /// and cleanly dissolves grid lines in Flexible mode without per-renderer MaterialPropertyBlocks.
    /// </summary>
    public sealed class SimulationVisualBridge : MonoBehaviour
    {
        private static readonly int PlayerWorldPosId = Shader.PropertyToID("_SimPlayerWorldPos");
        private static readonly int AnchorWorldPosId = Shader.PropertyToID("_SimAnchorWorldPos");
        private static readonly int ModeTintId = Shader.PropertyToID("_SimModeTint");
        private static readonly int RippleOriginId = Shader.PropertyToID("_SimRippleOrigin");
        private static readonly int RippleParamsId = Shader.PropertyToID("_SimRippleParams");
        private static readonly int RippleColorId = Shader.PropertyToID("_SimRippleColor");
        private static readonly int FlexGridFadeId = Shader.PropertyToID("_SimFlexGridFade");
        private static readonly int LimitedClueActiveId = Shader.PropertyToID("_SimLimitedClueActive");
        private static readonly int CarryTargetPosId = Shader.PropertyToID("_SimCarryTargetPos");
        private static readonly int CarryClueColorId = Shader.PropertyToID("_SimCarryClueColor");

        private static SimulationVisualBridge activeBridge;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeBridge = null;
        }

        public Transform playerTransform;
        public Transform anchorTransform;
        public SimulationModeController modeController;
        public ObjectCarryController carryController;

        private Vector3 rippleOrigin;
        private Color rippleColor;
        private float rippleMaxRadius;
        private float ripplePeakStrength;
        private float rippleElapsed = -1f;
        private float rippleDuration = 0.72f;
        private float flexGridFade;

        /// <summary>Emits a GPU floor grid shockwave ripple without mutating per-renderer materials.</summary>
        public static void TriggerFloorRipple(Vector3 worldPos, Color color, float maxRadiusGrid = 6.5f,
            float strength = 1.15f, float durationSeconds = 0.72f)
        {
            if (activeBridge == null) return;
            activeBridge.rippleOrigin = worldPos;
            activeBridge.rippleColor = color;
            activeBridge.rippleMaxRadius = Mathf.Max(1.5f, maxRadiusGrid);
            activeBridge.ripplePeakStrength = Mathf.Clamp(strength, 0.1f, 2.2f);
            activeBridge.rippleDuration = Mathf.Max(0.2f, durationSeconds);
            activeBridge.rippleElapsed = 0f;
        }

        private void Awake()
        {
            activeBridge = this;
        }

        private void Start()
        {
            if (playerTransform == null)
            {
                RectController rect = Object.FindFirstObjectByType<RectController>();
                if (rect != null) playerTransform = rect.transform;
            }

            if (modeController == null)
                modeController = Object.FindFirstObjectByType<SimulationModeController>();

            if (carryController == null)
                carryController = Object.FindFirstObjectByType<ObjectCarryController>();

            if (anchorTransform == null)
            {
                AnchorVisualRig anchorRig = Object.FindFirstObjectByType<AnchorVisualRig>();
                if (anchorRig != null) anchorTransform = anchorRig.transform;
            }

            flexGridFade = (modeController != null && modeController.IsFlexible) ? 1f : 0f;
            Shader.SetGlobalFloat(FlexGridFadeId, flexGridFade);
            Shader.SetGlobalFloat(LimitedClueActiveId, (modeController != null && modeController.IsLimited) ? 1f : 0f);
        }

        private void LateUpdate()
        {
            if (playerTransform != null)
            {
                Vector3 p = playerTransform.position;
                Shader.SetGlobalVector(PlayerWorldPosId, new Vector4(p.x, p.y, p.z, 1f));
            }
            else
            {
                Shader.SetGlobalVector(PlayerWorldPosId, Vector4.zero);
            }

            if (anchorTransform != null)
            {
                Vector3 a = anchorTransform.position;
                Shader.SetGlobalVector(AnchorWorldPosId, new Vector4(a.x, a.y, a.z, 1f));
            }
            else
            {
                Shader.SetGlobalVector(AnchorWorldPosId, Vector4.zero);
            }

            bool isFlexible = modeController != null && modeController.IsFlexible;
            flexGridFade = Mathf.MoveTowards(flexGridFade, isFlexible ? 1f : 0f, Time.deltaTime * 5.2f);
            Shader.SetGlobalFloat(FlexGridFadeId, flexGridFade);
            Shader.SetGlobalFloat(LimitedClueActiveId, (modeController != null && modeController.IsLimited) ? 1f : 0f);

            Color modeTint = modeController != null ? modeController.ModeColor : PaletteSettings.Sapphire;
            modeTint.a = 1f;
            Shader.SetGlobalVector(ModeTintId, modeTint);

            Color carryCol = PaletteSettings.Yellow;
            carryCol.a = 1f;
            Shader.SetGlobalVector(CarryClueColorId, carryCol);

            // Keep ChamberGrid free of competing tile drop clues so PlacementGhost is the single 2D floor indicator.
            Shader.SetGlobalVector(CarryTargetPosId, Vector4.zero);

            UpdateFloorRipple();
        }

        private void UpdateFloorRipple()
        {
            if (rippleElapsed < 0f)
            {
                Shader.SetGlobalVector(RippleOriginId, Vector4.zero);
                Shader.SetGlobalVector(RippleParamsId, Vector4.zero);
                return;
            }

            rippleElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(rippleElapsed / rippleDuration);
            float eased = 1f - Mathf.Pow(1f - t, 2.6f);
            float radius = Mathf.Lerp(0.35f, rippleMaxRadius, eased);
            float width = Mathf.Lerp(0.45f, 1.35f, eased);
            float fade = (1f - t) * (1f - t) * ripplePeakStrength;

            Shader.SetGlobalVector(RippleOriginId, new Vector4(rippleOrigin.x, rippleOrigin.y, rippleOrigin.z, 1f));
            Shader.SetGlobalVector(RippleParamsId, new Vector4(radius, width, fade, t));
            Shader.SetGlobalVector(RippleColorId, rippleColor);

            if (t >= 1f) rippleElapsed = -1f;
        }

        private void OnDisable()
        {
            if (activeBridge == this) activeBridge = null;
            Shader.SetGlobalVector(PlayerWorldPosId, Vector4.zero);
            Shader.SetGlobalVector(AnchorWorldPosId, Vector4.zero);
            Shader.SetGlobalVector(RippleOriginId, Vector4.zero);
            Shader.SetGlobalVector(RippleParamsId, Vector4.zero);
            Shader.SetGlobalVector(CarryTargetPosId, Vector4.zero);
            Shader.SetGlobalFloat(FlexGridFadeId, 0f);
            Shader.SetGlobalFloat(LimitedClueActiveId, 0f);
        }
    }
}
