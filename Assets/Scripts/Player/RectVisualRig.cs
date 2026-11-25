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
        public Transform leftArmPivot;
        public Transform rightArmPivot;
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

        [Header("Articulated Arm Motion")]
        public float carryArmPitch = -164f;
        public float telekinesisArmPitch = -86f;
        public float fusingArmPitch = -102f;
        public float armBlendSpeed = 14f;

        private RectController rectController;
        private SimulationModeController modeController;
        private ObjectCarryController carryController;
        private TelekinesisController telekinesisController;
        private PartitionFusionController partitionFusionController;
        private Vector3 rootRestLocalPos;
        private Quaternion rootRestLocalRot = Quaternion.identity;
        private Vector3 headRestLocalPos;
        private Quaternion headRestLocalRot = Quaternion.identity;
        private Vector3 leftArmRestLocalPos;
        private Vector3 rightArmRestLocalPos;
        private float currentLeftArmPitch;
        private float currentRightArmPitch;
        private float currentLeftArmRoll;
        private float currentRightArmRoll;
        private float currentArmLiftY;
        private float armPitchImpulse;
        private float armRollImpulse;
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
                armPitchImpulse = -22f;
            }
            else
            {
                TriggerStretch(0.08f);
                pitchImpulse = Mathf.Min(pitchImpulse, -5f);
                armPitchImpulse = 18f;
            }
        }

        public void TriggerRotateSnap(float directionSign = 1f)
        {
            float sign = directionSign >= 0f ? 1f : -1f;
            yawImpulse = Mathf.Clamp(yawImpulse + sign * 15f, -24f, 24f);
            rollImpulse = Mathf.Clamp(rollImpulse - sign * 8.5f, -16f, 16f);
            armRollImpulse = sign * 14f;
            TriggerStretch(0.08f);
        }

        public void TriggerThrowWhip()
        {
            pitchImpulse = 16f;
            surgeZImpulse = 0.085f;
            armPitchImpulse = 58f;
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
            armPitchImpulse = 34f;
            TriggerStretch(0.21f);
        }

        public void TriggerFusionPulse()
        {
            squashImpulse = 0.14f;
            stretchImpulse = 0.19f;
            pitchImpulse = -9f;
            surgeZImpulse = -0.055f;
            armPitchImpulse = -24f;
            armRollImpulse = 26f;
        }

        private void Awake()
        {
            rectController = GetComponent<RectController>();
            modeController = GetComponent<SimulationModeController>();
            carryController = GetComponent<ObjectCarryController>();
            telekinesisController = GetComponent<TelekinesisController>();
            partitionFusionController = GetComponent<PartitionFusionController>();

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

            EnsureArticulatedArms();
            if (leftArmPivot != null) leftArmRestLocalPos = leftArmPivot.localPosition;
            if (rightArmPivot != null) rightArmRestLocalPos = rightArmPivot.localPosition;
            airborneFrames = 0;
        }

        /// <summary>
        /// Guarantees articulated robot arms exist on both rebuilt and pre-existing scenes,
        /// reusing Rect's Catppuccin Mocha chassis/trim/accent materials for SRP Batcher safety.
        /// </summary>
        private void EnsureArticulatedArms()
        {
            if (visualRoot == null) return;

            if (leftArmPivot == null) leftArmPivot = visualRoot.Find("Left Arm Pivot");
            if (rightArmPivot == null) rightArmPivot = visualRoot.Find("Right Arm Pivot");
            if (leftArmPivot != null && rightArmPivot != null) return;

            Material primaryMat = ResolveVisualMaterial("Torso Chassis", PaletteSettings.Text);
            Material darkMat = ResolveVisualMaterial("Torso Dark Trim", PaletteSettings.Surface0);
            Material accentMat = limitedAccentMaterial != null
                ? limitedAccentMaterial
                : ResolveVisualMaterial("Chest Core", PaletteSettings.ModeLimited);

            var addedAccents = new System.Collections.Generic.List<Renderer>(2);
            if (leftArmPivot == null)
                leftArmPivot = CreateProceduralArm("Left Arm Pivot", -0.405f, primaryMat, darkMat, accentMat, addedAccents);
            if (rightArmPivot == null)
                rightArmPivot = CreateProceduralArm("Right Arm Pivot", 0.405f, primaryMat, darkMat, accentMat, addedAccents);

            if (addedAccents.Count > 0)
            {
                int prevLen = modeAccentRenderers != null ? modeAccentRenderers.Length : 0;
                Renderer[] merged = new Renderer[prevLen + addedAccents.Count];
                for (int i = 0; i < prevLen; i++) merged[i] = modeAccentRenderers[i];
                for (int i = 0; i < addedAccents.Count; i++) merged[prevLen + i] = addedAccents[i];
                modeAccentRenderers = merged;
            }
        }

        private Material ResolveVisualMaterial(string childName, Color fallbackColor)
        {
            if (visualRoot != null)
            {
                Transform child = visualRoot.Find(childName);
                if (child != null)
                {
                    Renderer r = child.GetComponent<Renderer>();
                    if (r != null && r.sharedMaterial != null) return r.sharedMaterial;
                }
            }
            return InteractableObject.GetSharedSurfaceMaterial(fallbackColor);
        }

        private Transform CreateProceduralArm(string pivotName, float offsetXGrid,
            Material primaryMat, Material darkMat, Material accentMat,
            System.Collections.Generic.List<Renderer> accentOut)
        {
            GameObject pivotGo = new GameObject(pivotName);
            pivotGo.layer = visualRoot.gameObject.layer;
            pivotGo.transform.SetParent(visualRoot, false);
            pivotGo.transform.localPosition = new Vector3(
                GridSettings.ToWorld(offsetXGrid),
                GridSettings.ToWorld(0.53f),
                GridSettings.ToWorld(0.02f));

            Mesh chamferCube = FindLoadedMesh("ChamferCube");
            Mesh octagonPrism = FindLoadedMesh("OctagonPrism");

            AddArmPart(pivotGo.transform, "Shoulder Joint", PrimitiveType.Cylinder, octagonPrism,
                new Vector3(0f, -0.01f, 0f), new Vector3(0.14f, 0.08f, 0.14f), darkMat);
            AddArmPart(pivotGo.transform, "Upper Arm", PrimitiveType.Cube, chamferCube,
                new Vector3(0f, -0.12f, 0f), new Vector3(0.13f, 0.21f, 0.13f), primaryMat);
            AddArmPart(pivotGo.transform, "Elbow Joint", PrimitiveType.Cylinder, octagonPrism,
                new Vector3(0f, -0.23f, 0.01f), new Vector3(0.125f, 0.065f, 0.125f), darkMat);
            AddArmPart(pivotGo.transform, "Forearm Cuff", PrimitiveType.Cube, chamferCube,
                new Vector3(0f, -0.34f, 0.02f), new Vector3(0.145f, 0.21f, 0.15f), primaryMat);
            Renderer glowR = AddArmPart(pivotGo.transform, "Arm Grip Glow", PrimitiveType.Cylinder, octagonPrism,
                new Vector3(0f, -0.44f, 0.025f), new Vector3(0.155f, 0.032f, 0.155f), accentMat);
            if (glowR != null) accentOut.Add(glowR);
            AddArmPart(pivotGo.transform, "Clamp Front", PrimitiveType.Cube, chamferCube,
                new Vector3(0f, -0.48f, 0.062f), new Vector3(0.095f, 0.07f, 0.042f), darkMat);
            AddArmPart(pivotGo.transform, "Clamp Back", PrimitiveType.Cube, chamferCube,
                new Vector3(0f, -0.48f, -0.012f), new Vector3(0.095f, 0.07f, 0.042f), darkMat);

            return pivotGo.transform;
        }

        private Renderer AddArmPart(Transform parent, string partName, PrimitiveType fallbackType, Mesh preferredMesh,
            Vector3 localPosGrid, Vector3 localScaleGrid, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(fallbackType);
            go.name = partName;
            go.layer = parent.gameObject.layer;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosGrid * GridSettings.Unit;
            float sy = (fallbackType == PrimitiveType.Cylinder && preferredMesh == null)
                ? localScaleGrid.y * 0.5f
                : localScaleGrid.y;
            go.transform.localScale = new Vector3(localScaleGrid.x, sy, localScaleGrid.z) * GridSettings.Unit;

            if (preferredMesh != null)
            {
                MeshFilter mf = go.GetComponent<MeshFilter>();
                if (mf != null) mf.sharedMesh = preferredMesh;
            }
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            return r;
        }

        private static Mesh FindLoadedMesh(string meshName)
        {
            Mesh[] all = Resources.FindObjectsOfTypeAll<Mesh>();
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name == meshName) return all[i];
            }
            return null;
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

            UpdateArticulatedArms(dt, speedNorm);
        }

        private void UpdateArticulatedArms(float dt, float speedNorm)
        {
            if (leftArmPivot == null && rightArmPivot == null) return;

            bool isFusing = partitionFusionController != null && partitionFusionController.IsFusionStaging;
            bool isCarrying = carryController != null && carryController.IsCarrying;
            bool isTethering = telekinesisController != null && telekinesisController.HasTether;

            armPitchImpulse = Mathf.MoveTowards(armPitchImpulse, 0f, dt * 165f);
            armRollImpulse = Mathf.MoveTowards(armRollImpulse, 0f, dt * 120f);

            float goalLeftPitch;
            float goalRightPitch;
            float goalLeftRoll;
            float goalRightRoll;
            float goalLiftY;

            if (isFusing)
            {
                float pulse = Mathf.Sin(Time.unscaledTime * 5.2f) * 5.5f;
                goalLeftPitch = fusingArmPitch + pulse + armPitchImpulse;
                goalRightPitch = fusingArmPitch + pulse + armPitchImpulse;
                goalLeftRoll = -16f - armRollImpulse * 0.4f;
                goalRightRoll = 16f + armRollImpulse * 0.4f;
                goalLiftY = GridSettings.ToWorld(0.03f);
            }
            else if (isCarrying)
            {
                // Raise both arms overhead to cradle the carried object with responsive carry micro-bounce
                float carryBounce = Mathf.Sin(walkPhase) * (1.8f + speedNorm * 3.2f);
                goalLeftPitch = carryArmPitch + carryBounce + armPitchImpulse;
                goalRightPitch = carryArmPitch + carryBounce + armPitchImpulse;
                goalLeftRoll = 12f + armRollImpulse * 0.35f;
                goalRightRoll = -12f - armRollImpulse * 0.35f;
                goalLiftY = GridSettings.ToWorld(0.055f);
            }
            else if (isTethering)
            {
                float channelFloat = Mathf.Sin(Time.time * 4.4f) * 4.5f;
                goalLeftPitch = telekinesisArmPitch + channelFloat + armPitchImpulse;
                goalRightPitch = telekinesisArmPitch - channelFloat * 0.7f + armPitchImpulse;
                goalLeftRoll = -11f;
                goalRightRoll = 11f;
                goalLiftY = GridSettings.ToWorld(0.025f);
            }
            else
            {
                float walkSwing = Mathf.Sin(walkPhase) * (22f * speedNorm);
                float idleBreath = Mathf.Sin(walkPhase * 0.85f) * 2.5f;
                goalLeftPitch = walkSwing + idleBreath + armPitchImpulse;
                goalRightPitch = -walkSwing + idleBreath + armPitchImpulse;
                goalLeftRoll = -10f - speedNorm * 4f;
                goalRightRoll = 10f + speedNorm * 4f;
                goalLiftY = 0f;
            }

            float armBlend = 1f - Mathf.Exp(-armBlendSpeed * dt);
            currentLeftArmPitch = Mathf.Lerp(currentLeftArmPitch, goalLeftPitch, armBlend);
            currentRightArmPitch = Mathf.Lerp(currentRightArmPitch, goalRightPitch, armBlend);
            currentLeftArmRoll = Mathf.Lerp(currentLeftArmRoll, goalLeftRoll, armBlend);
            currentRightArmRoll = Mathf.Lerp(currentRightArmRoll, goalRightRoll, armBlend);
            currentArmLiftY = Mathf.Lerp(currentArmLiftY, goalLiftY, armBlend);

            if (leftArmPivot != null)
            {
                leftArmPivot.localPosition = leftArmRestLocalPos + Vector3.up * currentArmLiftY;
                leftArmPivot.localRotation = Quaternion.Euler(currentLeftArmPitch, 0f, currentLeftArmRoll);
            }
            if (rightArmPivot != null)
            {
                rightArmPivot.localPosition = rightArmRestLocalPos + Vector3.up * currentArmLiftY;
                rightArmPivot.localRotation = Quaternion.Euler(currentRightArmPitch, 0f, currentRightArmRoll);
            }
        }
    }
}
