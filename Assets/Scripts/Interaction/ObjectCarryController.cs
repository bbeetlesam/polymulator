using UnityEngine;

namespace Polymulator
{
    public class ObjectCarryController : MonoBehaviour
    {
        [Header("Carry Anchor (grid units)")]
        public Transform carryAnchor;
        [Tooltip("Height of the carried object's centre above Rect's feet. Rect is 1 grid unit tall, so 1.7 clears the head.")]
        public float fallbackCarryHeight = 1.7f;
        [Tooltip("Horizontal offset from Rect. Zero keeps the object directly overhead rather than in front.")]
        public float fallbackCarryForward = 0f;

        [Header("Limited Mode: Place, Rotate & Throw (grid units)")]
        [Tooltip("Seconds for the object to move from overhead to the placement point.")]
        [Min(0.05f)] public float dropDuration = 0.35f;
        [Min(0.1f)] public float defaultDropDistance = 1.3f;
        [Min(0.1f)] public float minDropDistance = 0.9f;
        [Min(1f)] public float maxDropDistance = 2.6f;
        [Min(1f)] public float throwForwardRange = 4.6f;
        [Min(0.2f)] public float throwPeakHeight = 1.15f;
        [Min(0.05f)] public float rotateStepDuration = 0.14f;
        public PlacementGhost placementGhost;

        private InteractableObject carried;
        private Transform originalParent;
        private SimulationModeController modeController;
        private float heldYawDegrees;
        private float visualYawDegrees;
        private float yawAnimStart;
        private float yawAnimTarget;
        private float yawAnimElapsed = 1f;

        private const int ThrowArcSteps = 24;
        private const float ThrowArcTimeStep = 0.055f;
        private readonly Vector3[] throwArcBuffer = new Vector3[ThrowArcSteps];
        private readonly Vector3[] throwRingBuffer = new Vector3[24];
        private LineRenderer throwArcLine;
        private LineRenderer throwLandingRing;
        private Material throwArcMaterial;
        private bool hasPredictedLanding;
        private Vector3 predictedLandingPoint;
        private float predictedRangeGrid;

        public bool IsCarrying => carried != null;
        public bool IsDropping => false;
        public InteractableObject CarriedObject => carried;
        public string CarriedObjectName => carried != null ? carried.displayName : string.Empty;
        public float CurrentDropDistanceGrid => placementGhost != null ? placementGhost.forwardDistance : defaultDropDistance;
        public float CurrentHeldYawDegrees => heldYawDegrees;

        public bool TryGetPredictedDropPosition(out Vector3 dropWorldPos)
        {
            if (carried == null)
            {
                dropWorldPos = Vector3.zero;
                return false;
            }
            dropWorldPos = placementGhost != null
                ? placementGhost.GetPlacementPosition(carried)
                : FallbackDropPosition(carried);
            return true;
        }

        public bool TryGetPredictedThrowLanding(out Vector3 landingPoint, out float horizontalRangeGrid)
        {
            landingPoint = predictedLandingPoint;
            horizontalRangeGrid = predictedRangeGrid;
            return IsCarrying && hasPredictedLanding;
        }

        public event System.Action<InteractableObject> OnPickedUp;
        public event System.Action<InteractableObject> OnDropped;
        public event System.Action<InteractableObject, float> OnRotated;
        public event System.Action<InteractableObject> OnThrown;

        private void Awake()
        {
            modeController = GetComponent<SimulationModeController>();

            if (carryAnchor == null)
            {
                GameObject anchor = new GameObject("Carry Anchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(
                    0f,
                    GridSettings.ToWorld(fallbackCarryHeight),
                    GridSettings.ToWorld(fallbackCarryForward));
                carryAnchor = anchor.transform;
            }

            if (placementGhost == null)
            {
                GameObject ghost = new GameObject("Placement Ghost");
                placementGhost = ghost.AddComponent<PlacementGhost>();
            }
            placementGhost.Initialize(transform);
            placementGhost.forwardDistance = defaultDropDistance;

            EnsureThrowPreviewVisuals();
        }

        public bool TryPickup(InteractableObject target)
        {
            if (carried != null || target == null || !target.CanBePickedUp || carryAnchor == null)
                return false;

            Rigidbody targetBody = target.Body;
            if (targetBody == null) return false;

            carried = target;
            originalParent = target.transform.parent;
            heldYawDegrees = SnapRightAngle(target.transform.eulerAngles.y);
            visualYawDegrees = heldYawDegrees;
            yawAnimElapsed = 1f;

            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.isKinematic = true;
            targetBody.detectCollisions = false;
            targetBody.interpolation = RigidbodyInterpolation.None;
            target.SetCollidersEnabled(false);
            target.SetHighlight(false, false);

            target.transform.SetParent(carryAnchor, true);
            target.transform.localPosition = Vector3.zero;
            target.transform.rotation = Quaternion.Euler(0f, heldYawDegrees, 0f);

            target.TriggerPulse(0.15f, 0.22f);
            RectVisualRig.NotifyCarryAction(transform, true);
            SimulationVFX.Burst(target.transform.position, target.AssignedColor, 14, 2.0f);
            SimulationVFX.Ring(transform.position, PaletteSettings.Green, 1.35f, 0.32f);
            SimulationAudio.PlayPickup(target.transform.position);

            if (placementGhost != null)
            {
                placementGhost.ClearOverrides();
                placementGhost.SetFollowMode(PlacementFollowMode.DropInFrontOfRect);
                placementGhost.forwardDistance = defaultDropDistance;
                placementGhost.SetTargetRotation(Quaternion.Euler(0f, heldYawDegrees, 0f));
                placementGhost.SetAccentOverride(PaletteSettings.Green);
                placementGhost.Show(target);
            }
            OnPickedUp?.Invoke(target);
            return true;
        }

        /// <summary>Rotates the carried object by a crisp 90° yaw increment (Limited Mode Rotasi).</summary>
        public bool TryRotateStep(float stepDegrees = 90f)
        {
            if (carried == null || !carried.canRotate) return false;

            yawAnimStart = visualYawDegrees;
            heldYawDegrees = Mathf.Repeat(heldYawDegrees + stepDegrees, 360f);
            yawAnimTarget = yawAnimStart + stepDegrees;
            yawAnimElapsed = 0f;

            if (placementGhost != null)
                placementGhost.SetTargetRotation(Quaternion.Euler(0f, heldYawDegrees, 0f));

            carried.TriggerPulse(0.10f, 0.16f);
            RectVisualRig.NotifyRotateSnap(transform, 1f);
            SimulationVFX.Ring(carried.transform.position, PaletteSettings.Yellow, 1.1f, 0.28f);
            SimulationAudio.PlayRotate(carried.transform.position);
            OnRotated?.Invoke(carried, heldYawDegrees);
            return true;
        }

        /// <summary>Adjusts the Limited forward placement distance (Translasi fine control).</summary>
        public void AdjustDropReach(float deltaGrid)
        {
            if (carried == null || placementGhost == null) return;
            placementGhost.forwardDistance = Mathf.Clamp(
                placementGhost.forwardDistance + deltaGrid,
                minDropDistance,
                maxDropDistance);
        }

        /// <summary>Launches the carried object forward along Rect's heading to throwForwardRange GU.</summary>
        public bool TryThrow()
        {
            if (carried == null || !carried.canThrow) return false;

            InteractableObject thrown = carried;
            carried = null;

            HideThrowPreview();
            if (placementGhost != null)
            {
                placementGhost.ClearOverrides();
                placementGhost.Hide();
            }

            ComputeThrowKinematics(thrown, out Vector3 launchPos, out Vector3 launchVelocity);

            thrown.transform.SetParent(originalParent, true);
            originalParent = null;
            Quaternion launchRot = Quaternion.Euler(0f, heldYawDegrees, 0f);
            thrown.transform.position = launchPos;
            thrown.transform.rotation = launchRot;
            thrown.SetCollidersEnabled(true);

            Rigidbody body = thrown.Body;
            if (body != null)
            {
                body.position = launchPos;
                body.rotation = launchRot;
                Physics.SyncTransforms();
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = launchVelocity;
                body.angularVelocity = transform.right * 1.4f;
                body.WakeUp();
            }

            thrown.TriggerPulse(0.16f, 0.22f);
            RectVisualRig.NotifyThrowWhip(transform);
            RectCameraController.AddImpulse(0.06f);
            SimulationVFX.Burst(launchPos, PaletteSettings.Sapphire, 20, 3.0f);
            SimulationVFX.Ring(launchPos, PaletteSettings.Sky, 1.65f, 0.36f);
            SimulationAudio.PlayThrow(launchPos);
            OnThrown?.Invoke(thrown);
            return true;
        }

        private void ComputeThrowKinematics(InteractableObject target, out Vector3 launchPos, out Vector3 launchVelocity)
        {
            Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.001f) forward = Vector3.forward;
            forward.Normalize();

            launchPos = transform.position + forward * GridSettings.ToWorld(0.9f) +
                        Vector3.up * GridSettings.ToWorld(Mathf.Max(1.05f, fallbackCarryHeight * 0.72f));
            if (target != null && target.TryGetPlanarHalfExtents(out float hx, out float hz))
                launchPos = WorldBounds.ClampInside(launchPos, hx, hz);

            float g = Mathf.Abs(Physics.gravity.y);
            float arcHeight = GridSettings.ToWorld(throwPeakHeight);
            float vy = Mathf.Sqrt(2f * g * Mathf.Max(0.1f, arcHeight));
            float tUp = vy / g;
            float dropY = Mathf.Max(0.15f, launchPos.y + arcHeight - GridSettings.ToWorld(0.5f));
            float tDown = Mathf.Sqrt(2f * dropY / g);
            float flightTime = Mathf.Max(0.2f, tUp + tDown);
            float horizSpeed = GridSettings.ToWorld(throwForwardRange) / flightTime;
            launchVelocity = forward * horizSpeed + Vector3.up * vy;
        }

        private static float SnapRightAngle(float degrees)
        {
            return Mathf.Repeat(Mathf.Round(degrees / 90f) * 90f, 360f);
        }

        /// <summary>Legacy alias forwarding to instant non-locking placement.</summary>
        public bool BeginDrop() => DropInFront();

        /// <summary>
        /// Places the carried object cleanly onto the previewed spot without stalling movement,
        /// preserving the 90-degree held yaw orientation.
        /// </summary>
        public bool DropInFront()
        {
            if (carried == null) return false;

            InteractableObject dropping = carried;
            carried = null;
            HideThrowPreview();

            Vector3 destination = placementGhost != null
                ? placementGhost.GetPlacementPosition(dropping)
                : FallbackDropPosition(dropping);
            Quaternion targetDropRotation = Quaternion.Euler(0f, heldYawDegrees, 0f);

            if (placementGhost != null)
            {
                placementGhost.ClearOverrides();
                placementGhost.Hide();
            }

            dropping.transform.SetParent(originalParent, true);
            originalParent = null;
            dropping.transform.position = destination;
            dropping.transform.rotation = targetDropRotation;
            dropping.SetCollidersEnabled(true);

            Rigidbody body = dropping.Body;
            float mass = 1.5f;
            if (body != null)
            {
                mass = body.mass;
                body.position = destination;
                body.rotation = targetDropRotation;
                Physics.SyncTransforms();
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            dropping.TriggerPulse(0.10f, 0.18f);
            SimulationVFX.Burst(destination, PaletteSettings.Green, 12, 1.8f);
            SimulationVFX.Ring(destination, dropping.AssignedColor, 1.25f, 0.30f);
            SimulationVisualBridge.TriggerFloorRipple(destination, dropping.AssignedColor,
                Mathf.Clamp(3.2f + mass * 1.1f, 3.2f, 6.0f), 0.72f, 0.52f);
            SimulationAudio.PlayDrop(destination, mass);
            RectVisualRig.NotifyCarryAction(transform, false);
            RectCameraController.AddImpulse(0.03f);
            OnDropped?.Invoke(dropping);
            return true;
        }

        private Vector3 FallbackDropPosition(InteractableObject target)
        {
            Vector3 halfExtents = Vector3.one * 0.5f;
            if (target != null && target.TryGetHalfExtents(out Vector3 extents)) halfExtents = extents;

            Vector3 position = transform.position + transform.forward * GridSettings.ToWorld(defaultDropDistance);
            position.y = halfExtents.y + GridSettings.ToWorld(0.02f);
            return WorldBounds.ClampInside(position, halfExtents.x, halfExtents.z);
        }

        /// <summary>Immediate release returning the released object for Partition/Fusion replacement.</summary>
        public InteractableObject ReleaseAndReturn()
        {
            InteractableObject obj = carried;
            Release();
            return obj;
        }

        /// <summary>Immediate release, used for forced cleanup rather than player input.</summary>
        public void Release()
        {
            if (carried == null) return;

            InteractableObject released = carried;
            carried = null;
            HideThrowPreview();

            if (released.TryGetPlanarHalfExtents(out float halfExtentX, out float halfExtentZ))
                released.transform.position = WorldBounds.ClampInside(
                    released.transform.position, halfExtentX, halfExtentZ);

            released.transform.SetParent(originalParent, true);
            originalParent = null;
            released.SetCollidersEnabled(true);

            Rigidbody body = released.Body;
            if (body != null)
            {
                body.position = released.transform.position;
                body.rotation = released.transform.rotation;
                Physics.SyncTransforms();
                body.detectCollisions = true;
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            if (placementGhost != null) placementGhost.Hide();
        }

        /// <summary>
        /// Re-anchors the held object each frame, keeps it inside bounds, and renders
        /// the ballistic Throw Trajectory Arc preview in Limited Mode.
        /// </summary>
        private void LateUpdate()
        {
            if (carried == null)
            {
                HideThrowPreview();
                return;
            }

            carried.transform.localPosition = Vector3.zero;

            if (yawAnimElapsed < 1f)
            {
                yawAnimElapsed = Mathf.Min(1f, yawAnimElapsed + Time.deltaTime / Mathf.Max(0.02f, rotateStepDuration));
                float eased = 1f - Mathf.Pow(1f - yawAnimElapsed, 3f);
                visualYawDegrees = Mathf.Lerp(yawAnimStart, yawAnimTarget, eased);
                if (yawAnimElapsed >= 1f) visualYawDegrees = heldYawDegrees;
            }
            else
            {
                visualYawDegrees = heldYawDegrees;
            }

            carried.transform.rotation = Quaternion.Euler(0f, visualYawDegrees, 0f);

            if (carried.TryGetPlanarHalfExtents(out float halfExtentX, out float halfExtentZ))
                carried.transform.position = WorldBounds.ClampInside(
                    carried.transform.position, halfExtentX, halfExtentZ);

            if (placementGhost != null && placementGhost.Visible)
            {
                placementGhost.SetTargetRotation(Quaternion.Euler(0f, visualYawDegrees, 0f));
                placementGhost.SyncNow();
            }

            // Focus on a single clean 2D floor footprint indicator while carrying (Throw [F]/LMB stays active).
            HideThrowPreview();
        }

        private void EnsureThrowPreviewVisuals()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            throwArcMaterial = new Material(unlitShader) { hideFlags = HideFlags.HideAndDontSave };
            if (throwArcMaterial.HasProperty("_Cull")) throwArcMaterial.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (throwArcMaterial.HasProperty("_Surface")) throwArcMaterial.SetFloat("_Surface", 1f);
            throwArcMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            throwArcMaterial.SetOverrideTag("RenderType", "Transparent");
            throwArcMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (throwArcMaterial.HasProperty("_SrcBlend")) throwArcMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (throwArcMaterial.HasProperty("_DstBlend")) throwArcMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (throwArcMaterial.HasProperty("_ZWrite")) throwArcMaterial.SetFloat("_ZWrite", 0f);
            Color hdr = PaletteSettings.Sapphire * 1.35f;
            hdr.a = 0.85f;
            throwArcMaterial.SetColor("_BaseColor", hdr);
            throwArcMaterial.SetColor("_Color", hdr);

            GameObject arcGo = new GameObject("Throw Trajectory Arc");
            arcGo.transform.SetParent(transform, false);
            throwArcLine = arcGo.AddComponent<LineRenderer>();
            throwArcLine.useWorldSpace = true;
            throwArcLine.sharedMaterial = throwArcMaterial;
            throwArcLine.startWidth = GridSettings.ToWorld(0.055f);
            throwArcLine.endWidth = GridSettings.ToWorld(0.025f);
            throwArcLine.numCornerVertices = 2;
            throwArcLine.numCapVertices = 4;
            throwArcLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            throwArcLine.receiveShadows = false;
            Gradient g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(PaletteSettings.Sky, 0f), new GradientColorKey(PaletteSettings.Sapphire, 1f) },
                new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.55f, 1f) });
            throwArcLine.colorGradient = g;
            throwArcLine.enabled = false;

            GameObject ringGo = new GameObject("Throw Landing Ring");
            ringGo.transform.SetParent(transform, false);
            throwLandingRing = ringGo.AddComponent<LineRenderer>();
            throwLandingRing.useWorldSpace = true;
            throwLandingRing.loop = true;
            throwLandingRing.sharedMaterial = throwArcMaterial;
            throwLandingRing.startWidth = GridSettings.ToWorld(0.045f);
            throwLandingRing.endWidth = GridSettings.ToWorld(0.045f);
            throwLandingRing.positionCount = throwRingBuffer.Length;
            throwLandingRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            throwLandingRing.receiveShadows = false;
            throwLandingRing.enabled = false;
        }

        private void UpdateThrowPreview()
        {
            if (throwArcLine == null || carried == null) return;

            ComputeThrowKinematics(carried, out Vector3 launchPos, out Vector3 velocity);
            Vector3 gravity = Physics.gravity;
            int hitCount = 1;
            throwArcBuffer[0] = launchPos;

            Vector3 prev = launchPos;
            Vector3 landingPoint = launchPos;
            Vector3 landingNormal = Vector3.up;
            bool hitSurface = false;
            int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);

            for (int i = 1; i < ThrowArcSteps; i++)
            {
                float t = i * ThrowArcTimeStep;
                Vector3 next = launchPos + velocity * t + 0.5f * gravity * (t * t);
                Vector3 segment = next - prev;
                float dist = segment.magnitude;
                if (dist > 0.001f && Physics.SphereCast(prev, GridSettings.ToWorld(0.14f), segment / dist,
                    out RaycastHit hit, dist, mask, QueryTriggerInteraction.Ignore))
                {
                    throwArcBuffer[i] = hit.point;
                    hitCount = i + 1;
                    landingPoint = hit.point + hit.normal * 0.025f;
                    landingNormal = hit.normal;
                    hitSurface = true;
                    break;
                }

                throwArcBuffer[i] = next;
                hitCount = i + 1;
                prev = next;
                landingPoint = next;
            }

            throwArcLine.positionCount = hitCount;
            for (int i = 0; i < hitCount; i++)
                throwArcLine.SetPosition(i, throwArcBuffer[i]);
            throwArcLine.enabled = hitCount >= 2;

            hasPredictedLanding = hitSurface;
            predictedLandingPoint = landingPoint;
            Vector3 flatDelta = landingPoint - transform.position;
            flatDelta.y = 0f;
            predictedRangeGrid = GridSettings.ToGrid(flatDelta.magnitude);

            if (throwLandingRing != null)
            {
                throwLandingRing.enabled = hitSurface;
                if (hitSurface)
                {
                    Quaternion rot = Quaternion.FromToRotation(Vector3.up, landingNormal);
                    float radius = GridSettings.ToWorld(0.32f);
                    for (int i = 0; i < throwRingBuffer.Length; i++)
                    {
                        float angle = i / (float)throwRingBuffer.Length * Mathf.PI * 2f;
                        Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                        throwLandingRing.SetPosition(i, landingPoint + rot * local);
                    }
                }
            }
        }

        private void HideThrowPreview()
        {
            hasPredictedLanding = false;
            if (throwArcLine != null && throwArcLine.enabled) throwArcLine.enabled = false;
            if (throwLandingRing != null && throwLandingRing.enabled) throwLandingRing.enabled = false;
        }

        private void OnDisable()
        {
            if (carried != null) Release();
            HideThrowPreview();
            if (placementGhost != null) placementGhost.Hide();
        }

        private void OnDestroy()
        {
            if (throwArcMaterial != null) Destroy(throwArcMaterial);
        }
    }
}
