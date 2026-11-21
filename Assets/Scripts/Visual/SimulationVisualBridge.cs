using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Updates global URP shader coordinates so ChamberGrid renders subtle reactive rings
    /// around Rect and Anchor without modifying per-renderer MaterialPropertyBlocks.
    /// </summary>
    public sealed class SimulationVisualBridge : MonoBehaviour
    {
        private static readonly int PlayerWorldPosId = Shader.PropertyToID("_SimPlayerWorldPos");
        private static readonly int AnchorWorldPosId = Shader.PropertyToID("_SimAnchorWorldPos");
        private static readonly int ModeTintId = Shader.PropertyToID("_SimModeTint");

        public Transform playerTransform;
        public Transform anchorTransform;
        public SimulationModeController modeController;

        private void Start()
        {
            if (playerTransform == null)
            {
                RectController rect = Object.FindFirstObjectByType<RectController>();
                if (rect != null) playerTransform = rect.transform;
            }

            if (modeController == null)
                modeController = Object.FindFirstObjectByType<SimulationModeController>();

            if (anchorTransform == null)
            {
                AnchorVisualRig anchorRig = Object.FindFirstObjectByType<AnchorVisualRig>();
                if (anchorRig != null) anchorTransform = anchorRig.transform;
            }
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

            Color modeTint = modeController != null ? modeController.ModeColor : PaletteSettings.Sapphire;
            modeTint.a = 1f;
            Shader.SetGlobalVector(ModeTintId, modeTint);
        }

        private void OnDisable()
        {
            Shader.SetGlobalVector(PlayerWorldPosId, Vector4.zero);
            Shader.SetGlobalVector(AnchorWorldPosId, Vector4.zero);
        }
    }
}
