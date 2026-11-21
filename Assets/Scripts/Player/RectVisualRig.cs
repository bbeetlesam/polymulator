using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Procedural motion and mode-accent visual rig for RECT:
    /// idle breathing hover bob, movement pitch lean, turn bank roll,
    /// squash-and-stretch action punches, and SRP-Batcher clean accent material swaps
    /// between Limited (Sapphire) and Flexible (Mauve) modes.
    /// </summary>
    public sealed class RectVisualRig : MonoBehaviour
    {
        [Header("Rig References")]
        public Transform visualRoot;
        public Transform headPivot;
        public Renderer[] modeAccentRenderers;
        public Material limitedAccentMaterial;
        public Material flexibleAccentMaterial;

        [Header("Procedural Motion")]
        public float idleBobAmplitude = 0.035f;
        public float idleBobFrequency = 2.6f;
        public float moveBobAmplitude = 0.045f;
        public float moveBobFrequency = 9.5f;
        public float maxForwardPitch = 9.5f;
        public float maxTurnRoll = 11f;

        private RectController rectController;
        private SimulationModeController modeController;
        private Vector3 rootRestLocalPos;
        private Vector3 headRestLocalPos;
        private float stretchImpulse;
        private float walkPhase;
        private float currentPitch;
        private float currentRoll;

        public static void NotifyActionStretch(Transform target, float strength = 0.14f)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerStretch(strength);
        }

        public void TriggerStretch(float strength = 0.14f)
        {
            stretchImpulse = Mathf.Clamp(Mathf.Max(stretchImpulse, strength), 0f, 0.28f);
        }

        private void Awake()
        {
            rectController = GetComponent<RectController>();
            modeController = GetComponent<SimulationModeController>();

            if (visualRoot != null)
                rootRestLocalPos = visualRoot.localPosition;
            if (headPivot != null)
                headRestLocalPos = headPivot.localPosition;
        }

        private void OnEnable()
        {
            if (modeController != null)
                modeController.OnModeChanged += HandleModeChanged;
            ApplyModeAccent();
        }

        private void OnDisable()
        {
            if (modeController != null)
                modeController.OnModeChanged -= HandleModeChanged;
        }

        private void HandleModeChanged(SimulationMode mode)
        {
            ApplyModeAccent();
            TriggerStretch(0.16f);
        }

        public void ApplyModeAccent()
        {
            if (modeAccentRenderers == null || modeAccentRenderers.Length == 0) return;

            bool isFlexible = modeController != null && modeController.IsFlexible;
            Material targetMat = isFlexible ? flexibleAccentMaterial : limitedAccentMaterial;
            if (targetMat == null) return;

            for (int i = 0; i < modeAccentRenderers.Length; i++)
            {
                Renderer r = modeAccentRenderers[i];
                if (r != null) r.sharedMaterial = targetMat;
            }
        }

        private void LateUpdate()
        {
            if (visualRoot == null) return;

            float dt = Time.deltaTime;
            float speedNorm = rectController != null ? rectController.SpeedNormalized : 0f;
            float turnRate = rectController != null ? rectController.AngularTurnRate : 0f;

            walkPhase += dt * Mathf.Lerp(idleBobFrequency, moveBobFrequency, speedNorm);
            float bobAmp = Mathf.Lerp(GridSettings.ToWorld(idleBobAmplitude), GridSettings.ToWorld(moveBobAmplitude), speedNorm);
            float bobOffset = Mathf.Sin(walkPhase) * bobAmp;

            float goalPitch = speedNorm * maxForwardPitch;
            float goalRoll = Mathf.Clamp(-turnRate * 0.045f, -maxTurnRoll, maxTurnRoll);
            currentPitch = Mathf.Lerp(currentPitch, goalPitch, 1f - Mathf.Exp(-12f * dt));
            currentRoll = Mathf.Lerp(currentRoll, goalRoll, 1f - Mathf.Exp(-12f * dt));

            visualRoot.localPosition = rootRestLocalPos + Vector3.up * bobOffset;
            visualRoot.localRotation = Quaternion.Euler(currentPitch, 0f, currentRoll);

            if (stretchImpulse > 0.001f)
            {
                float sy = 1f + stretchImpulse;
                float sxz = 1f / Mathf.Sqrt(sy);
                visualRoot.localScale = new Vector3(sxz, sy, sxz);
                stretchImpulse = Mathf.MoveTowards(stretchImpulse, 0f, dt * 1.15f);
            }
            else
            {
                visualRoot.localScale = Vector3.one;
            }

            if (headPivot != null)
            {
                float headSecondaryBob = Mathf.Sin(walkPhase - 0.6f) * GridSettings.ToWorld(0.015f);
                headPivot.localPosition = headRestLocalPos + Vector3.up * headSecondaryBob;
            }
        }
    }
}
