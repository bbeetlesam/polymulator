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
        private Quaternion rootRestLocalRot = Quaternion.identity;
        private Vector3 headRestLocalPos;
        private Quaternion headRestLocalRot = Quaternion.identity;
        private float stretchImpulse;
        private float squashImpulse;
        private float pitchImpulse;
        private float yawImpulse;
        private float rollImpulse;
        private float surgeZImpulse;
        private float walkPhase;
        private float currentPitch;
        private float currentRoll;
        private float currentYaw;
        private float currentHeadYaw;
        private float currentHeadPitch;
        private float accelPitchOffset;
        private float prevSpeedNorm;
        private int airborneFrames;

        public static void NotifyActionStretch(Transform target, float strength = 0.14f)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerStretch(strength);
        }

        public static void NotifyCarryAction(Transform target, bool pickingUp)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerCarry(pickingUp);
        }

        public static void NotifyRotateSnap(Transform target, float directionSign = 1f)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerRotateSnap(directionSign);
        }

        public static void NotifyThrowWhip(Transform target)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerThrowWhip();
        }

        public static void NotifyPartitionChop(Transform target, Vector3 worldSplitAxis)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerPartitionChop(worldSplitAxis);
        }

        public static void NotifyFusionPulse(Transform target)
        {
            if (target == null) return;
            RectVisualRig rig = target.GetComponentInChildren<RectVisualRig>();
            if (rig != null) rig.TriggerFusionPulse();
        }

        public void TriggerStretch(float strength = 0.14f)
        {
            stretchImpulse = Mathf.Clamp(Mathf.Max(stretchImpulse, strength), 0f, 0.28f);
        }

        public void TriggerSquash(float strength = 0.14f)
        {
            squashImpulse = Mathf.Clamp(Mathf.Max(squashImpulse, strength), 0f, 0.24f);
        }

        public void TriggerCarry(bool pickingUp)
        {
            if (pickingUp)
            {
                TriggerSquash(0.11f);
                pitchImpulse = Mathf.Max(pitchImpulse, 8f);
                surgeZImpulse = Mathf.Max(surgeZImpulse, 0.045f);
            }
            else
            {
                TriggerStretch(0.08f);
                pitchImpulse = Mathf.Min(pitchImpulse, -5f);
            }
        }

        public void TriggerRotateSnap(float directionSign = 1f)
        {
            float sign = directionSign >= 0f ? 1f : -1f;
            yawImpulse = Mathf.Clamp(yawImpulse + sign * 15f, -24f, 24f);
            rollImpulse = Mathf.Clamp(rollImpulse - sign * 8.5f, -16f, 16f);
            TriggerStretch(0.08f);
        }

        public void TriggerThrowWhip()
        {
            pitchImpulse = 16f;
            surgeZImpulse = 0.085f;
            TriggerStretch(0.18f);
        }

        public void TriggerPartitionChop(Vector3 worldSplitAxis)
        {
            Vector3 localAxis = transform.InverseTransformDirection(worldSplitAxis.sqrMagnitude > 0.0001f
                ? worldSplitAxis.normalized
                : transform.right);
            float yawSign = Mathf.Abs(localAxis.x) > 0.25f ? Mathf.Sign(localAxis.x) : 1f;
            float rollAngle = Mathf.Abs(localAxis.y) > 0.6f ? 12f : yawSign * -11f;

            pitchImpulse = 17f;
            yawImpulse = yawSign * 14f;
            rollImpulse = rollAngle;
            surgeZImpulse = 0.09f;
            TriggerStretch(0.21f);
        }

        public void TriggerFusionPulse()
        {
            squashImpulse = 0.14f;
            stretchImpulse = 0.19f;
            pitchImpulse = -9f;
            surgeZImpulse = -0.055f;
        }

        private void Awake()
        {
            rectController = GetComponent<RectController>();
            modeController = GetComponent<SimulationModeController>();

            if (visualRoot != null)
            {
                rootRestLocalPos = visualRoot.localPosition;
                rootRestLocalRot = visualRoot.localRotation;
            }
            if (headPivot != null)
            {
                headRestLocalPos = headPivot.localPosition;
                headRestLocalRot = headPivot.localRotation;
            }
            airborneFrames = 0;
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
            if (dt <= 0.00001f) return;

            float speedNorm = rectController != null ? rectController.SpeedNormalized : 0f;
            float turnRate = rectController != null ? rectController.AngularTurnRate : 0f;
            bool isGrounded = rectController == null || rectController.IsGrounded;

            // Landing squash when touching down onto floor or platform after genuine airtime
            if (isGrounded)
            {
                if (airborneFrames >= 2)
                    TriggerSquash(0.16f);
                airborneFrames = 0;
            }
            else if (rectController.enabled)
            {
                airborneFrames++;
            }

            // Acceleration surge & braking overshoot follow-through
            float rawAccel = Mathf.Clamp((speedNorm - prevSpeedNorm) / dt, -4.5f, 4.5f);
            prevSpeedNorm = speedNorm;
            accelPitchOffset = Mathf.Lerp(accelPitchOffset, rawAccel * 1.45f, 1f - Mathf.Exp(-11f * dt));

            walkPhase += dt * Mathf.Lerp(idleBobFrequency, moveBobFrequency, speedNorm);
            float bobAmp = Mathf.Lerp(GridSettings.ToWorld(idleBobAmplitude), GridSettings.ToWorld(moveBobAmplitude), speedNorm);
            float bobOffset = Mathf.Sin(walkPhase) * bobAmp;

            float decayRot = 1f - Mathf.Exp(-14f * dt);
            pitchImpulse = Mathf.MoveTowards(pitchImpulse, 0f, dt * 62f);
            yawImpulse = Mathf.MoveTowards(yawImpulse, 0f, dt * 78f);
            rollImpulse = Mathf.MoveTowards(rollImpulse, 0f, dt * 52f);
            surgeZImpulse = Mathf.MoveTowards(surgeZImpulse, 0f, dt * 0.42f);

            float goalPitch = speedNorm * maxForwardPitch + accelPitchOffset + pitchImpulse;
            float goalRoll = Mathf.Clamp(-turnRate * 0.045f, -maxTurnRoll, maxTurnRoll) + rollImpulse;
            currentPitch = Mathf.Lerp(currentPitch, goalPitch, decayRot);
            currentRoll = Mathf.Lerp(currentRoll, goalRoll, decayRot);
            currentYaw = Mathf.Lerp(currentYaw, yawImpulse, 1f - Mathf.Exp(-18f * dt));

            Vector3 localOffset = Vector3.up * bobOffset + Vector3.forward * GridSettings.ToWorld(surgeZImpulse);
            visualRoot.localPosition = rootRestLocalPos + localOffset;
            visualRoot.localRotation = rootRestLocalRot * Quaternion.Euler(currentPitch, currentYaw, currentRoll);

            float netScaleDelta = stretchImpulse - squashImpulse;
            if (Mathf.Abs(netScaleDelta) > 0.001f || stretchImpulse > 0.001f || squashImpulse > 0.001f)
            {
                float sy = Mathf.Clamp(1f + netScaleDelta, 0.78f, 1.30f);
                float sxz = 1f / Mathf.Sqrt(sy);
                visualRoot.localScale = new Vector3(sxz, sy, sxz);
                stretchImpulse = Mathf.MoveTowards(stretchImpulse, 0f, dt * 1.25f);
                squashImpulse = Mathf.MoveTowards(squashImpulse, 0f, dt * 1.45f);
            }
            else
            {
                visualRoot.localScale = Vector3.one;
            }

            if (headPivot != null)
            {
                float headSecondaryBob = Mathf.Sin(walkPhase - 0.6f) * GridSettings.ToWorld(0.015f);
                float goalHeadYaw = Mathf.Clamp(turnRate * 0.05f, -14f, 14f) + currentYaw * 0.45f;
                float goalHeadPitch = speedNorm * 4f + pitchImpulse * 0.35f;
                currentHeadYaw = Mathf.Lerp(currentHeadYaw, goalHeadYaw, 1f - Mathf.Exp(-12f * dt));
                currentHeadPitch = Mathf.Lerp(currentHeadPitch, goalHeadPitch, 1f - Mathf.Exp(-12f * dt));

                headPivot.localPosition = headRestLocalPos + Vector3.up * headSecondaryBob;
                headPivot.localRotation = headRestLocalRot * Quaternion.Euler(currentHeadPitch, currentHeadYaw, 0f);
            }
        }
    }
}
