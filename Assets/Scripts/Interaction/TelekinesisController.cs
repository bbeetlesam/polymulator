using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    /// <summary>
    /// Flexible Mode special ability: Free-Aim Telekinesis.
    /// Projects cursor aim onto an elevation plane using Plane.Raycast, allows smooth
    /// horizontal translation within tether radius, continuous vertical elevation (Scroll / Q / Z),
    /// free rotation (R / Right-Click), and telekinetic launch (F), while showing a
    /// glowing Bezier energy beam and a surface plumb-ghost underneath.
    /// </summary>
    public sealed class TelekinesisController : MonoBehaviour
    {
        [Header("Telekinesis Reach & Feel (grid units)")]
        [Min(1f)] public float maxTetherRadius = 8.2f;
        [Min(0.1f)] public float minElevation = 0.25f;
        [Min(1f)] public float maxElevation = 5.5f;
        [Min(0.5f)] public float elevationKeySpeed = 3.2f;
        [Min(0.1f)] public float scrollElevationStep = 0.45f;
        [Min(10f)] public float freeSpinSpeed = 135f;
        [Min(1f)] public float followGain = 14f;
        [Min(2f)] public float maxFollowSpeed = 18f;
        [Min(1f)] public float pushImpulseSpeed = 12f;

        [Header("Beam Visuals")]
        public Transform beamEmitter;
        public PlacementGhost placementGhost;
        public float beamWidth = 0.045f;

        private const int BeamSegments = 20;

        private InteractableObject tethered;
        private Rigidbody tetheredBody;
        private bool prevUseGravity;
        private float prevLinearDamping;
        private float prevAngularDamping;

        private Vector3 targetWorldXZ;
        private float targetHeightAboveSurface;
        private Quaternion desiredRotation = Quaternion.identity;
        private Vector2 lockMousePosition;
        private bool cursorAimEngaged;

        private LineRenderer beamLine;
        private LineRenderer plumbLine;
        private Material beamMaterial;
        private Material plumbMaterial;

        public bool HasTether => tethered != null;
        public InteractableObject TetheredObject => tethered;
        public float CurrentElevationGrid => GridSettings.ToGrid(targetHeightAboveSurface);

        public event Action<InteractableObject> OnTetherStarted;
        public event Action<InteractableObject> OnTetherReleased;

        private void Awake()
        {
            ResolveGhost();
            EnsureVisualLines();
        }

        private void Start()
        {
            ResolveGhost();
        }

        private void ResolveGhost()
        {
            if (placementGhost != null) return;
            ObjectCarryController carry = GetComponent<ObjectCarryController>();
            if (carry != null) placementGhost = carry.placementGhost;
            if (placementGhost == null) placementGhost = UnityEngine.Object.FindFirstObjectByType<PlacementGhost>();
        }

        private void EnsureVisualLines()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            if (beamLine == null)
            {
                GameObject beamGo = new GameObject("Telekinesis Beam");
                beamGo.transform.SetParent(transform, false);
                beamLine = beamGo.AddComponent<LineRenderer>();
                beamLine.useWorldSpace = true;
                beamLine.positionCount = BeamSegments;
                beamLine.numCornerVertices = 4;
                beamLine.numCapVertices = 4;
                beamLine.textureMode = LineTextureMode.Stretch;
                beamMaterial = CreateLineMat(unlitShader, PaletteSettings.BeamTelekinesis, 1.55f);
                beamLine.sharedMaterial = beamMaterial;

                Gradient grad = new Gradient();
                grad.SetKeys(
                    new[]
                    {
                        new GradientColorKey(PaletteSettings.Sapphire, 0f),
                        new GradientColorKey(PaletteSettings.BeamTelekinesis, 0.5f),
                        new GradientColorKey(PaletteSettings.Mauve, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(0.9f, 0f),
                        new GradientAlphaKey(1f, 0.5f),
                        new GradientAlphaKey(0.95f, 1f)
                    });
                beamLine.colorGradient = grad;
                beamLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamLine.receiveShadows = false;
                beamLine.enabled = false;
            }

            if (plumbLine == null)
            {
                GameObject plumbGo = new GameObject("Telekinesis Plumb Line");
                plumbGo.transform.SetParent(transform, false);
                plumbLine = plumbGo.AddComponent<LineRenderer>();
                plumbLine.useWorldSpace = true;
                plumbLine.positionCount = 2;
                Color plumbTint = PaletteSettings.Mauve;
                plumbTint.a = 0.72f;
                plumbMaterial = CreateLineMat(unlitShader, plumbTint, 1.15f);
                plumbLine.sharedMaterial = plumbMaterial;
                plumbLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                plumbLine.receiveShadows = false;
                plumbLine.enabled = false;
            }
        }

        private static Material CreateLineMat(Shader shader, Color baseColor, float hdrMult)
        {
            Material mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            Color hdr = baseColor * hdrMult;
            hdr.a = baseColor.a;
            mat.SetColor("_BaseColor", hdr);
            mat.SetColor("_Color", hdr);
            return mat;
        }

        public bool TryLockTarget(InteractableObject target)
        {
            if (tethered != null || target == null || !target.CanBePickedUp || !target.canTelekinesis)
                return false;

            ResolveGhost();
            Rigidbody body = target.Body;
            if (body == null) return false;

            tethered = target;
            tetheredBody = body;
            prevUseGravity = body.useGravity;
            prevLinearDamping = body.linearDamping;
            prevAngularDamping = body.angularDamping;

            body.isKinematic = false;
            body.detectCollisions = true;
            body.useGravity = false;
            body.linearDamping = 3.5f;
            body.angularDamping = 8f;
            body.WakeUp();

            targetWorldXZ = new Vector3(target.transform.position.x, 0f, target.transform.position.z);
            desiredRotation = target.transform.rotation;
            cursorAimEngaged = false;
            lockMousePosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

            float groundY = SampleSurfaceY(target.transform.position, target);
            float halfY = target.TryGetHalfExtents(out Vector3 ext) ? ext.y : GridSettings.ToWorld(0.5f);
            float currentClearance = target.transform.position.y - (groundY + halfY);
            targetHeightAboveSurface = Mathf.Clamp(
                Mathf.Max(GridSettings.ToWorld(0.95f), currentClearance),
                GridSettings.ToWorld(minElevation),
                GridSettings.ToWorld(maxElevation));

            target.SetHighlight(false, false);
            target.TriggerPulse(0.16f);
            RectVisualRig.NotifyActionStretch(transform, 0.14f);
            SimulationVFX.Burst(target.transform.position, PaletteSettings.BeamTelekinesis, 18, 2.4f);
            SimulationVFX.Ring(target.transform.position, PaletteSettings.Mauve, 1.55f, 0.36f);

            if (placementGhost != null)
            {
                placementGhost.SetTargetRotation(desiredRotation);
                placementGhost.SetAccentOverride(PaletteSettings.BeamTelekinesis);
                placementGhost.Show(target);
                placementGhost.SetOverrideXZ(targetWorldXZ);
            }

            UpdateVisualLines(groundY + halfY, target.transform.position);
            beamLine.enabled = true;
            plumbLine.enabled = true;
            SimulationAudio.PlayTelekinesisLock(target.transform.position);
            OnTetherStarted?.Invoke(target);
            return true;
        }

        public void Release(bool pushForward = false)
        {
            if (tethered == null) return;

            InteractableObject released = tethered;
            Rigidbody body = tetheredBody;

            tethered = null;
            tetheredBody = null;

            if (beamLine != null) beamLine.enabled = false;
            if (plumbLine != null) plumbLine.enabled = false;
            if (placementGhost != null)
            {
                placementGhost.ClearOverrides();
                placementGhost.Hide();
            }

            if (body != null)
            {
                body.useGravity = prevUseGravity;
                body.linearDamping = prevLinearDamping;
                body.angularDamping = prevAngularDamping;
                if (pushForward)
                {
                    Vector3 pushDir = (released.transform.position - transform.position);
                    pushDir.y = 0.2f;
                    if (pushDir.sqrMagnitude < 0.01f) pushDir = transform.forward + Vector3.up * 0.2f;
                    body.linearVelocity = pushDir.normalized * GridSettings.ToWorld(pushImpulseSpeed);
                    SimulationVFX.Burst(released.transform.position, PaletteSettings.Mauve, 20, 3.0f);
                    SimulationVFX.Ring(released.transform.position, PaletteSettings.Sapphire, 1.8f, 0.35f);
                    RectCameraController.AddImpulse(0.06f);
                }
                else
                {
                    body.linearVelocity = Vector3.ClampMagnitude(body.linearVelocity, GridSettings.ToWorld(3f));
                    SimulationVFX.Burst(released.transform.position, PaletteSettings.Teal, 10, 1.4f);
                }
                body.WakeUp();
            }

            released.TriggerPulse(0.10f);
            SimulationAudio.PlayTelekinesisRelease(released.transform.position);
            OnTetherReleased?.Invoke(released);
        }

        /// <summary>Moves telekinetic XZ aim directly in world space (within tether radius & bounds).</summary>
        public void SetAimWorldXZ(Vector3 xzWorld)
        {
            targetWorldXZ = new Vector3(xzWorld.x, 0f, xzWorld.z);
        }

        /// <summary>Sets target elevation clearance above the surface below in grid units.</summary>
        public void SetTargetElevationGrid(float elevationGrid)
        {
            targetHeightAboveSurface = Mathf.Clamp(
                GridSettings.ToWorld(elevationGrid),
                GridSettings.ToWorld(minElevation),
                GridSettings.ToWorld(maxElevation));
        }

        private void Update()
        {
            if (tethered == null || tetheredBody == null) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            // 1. Free XZ Aim via Camera ray once pointer actually moves inside viewport after lock
            if (mouse != null && Camera.main != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                if (!cursorAimEngaged &&
                    mousePos.x >= 0f && mousePos.y >= 0f &&
                    mousePos.x <= Screen.width && mousePos.y <= Screen.height &&
                    (mousePos - lockMousePosition).sqrMagnitude > 9f)
                {
                    cursorAimEngaged = true;
                }

                if (cursorAimEngaged)
                {
                    Ray ray = Camera.main.ScreenPointToRay(mousePos);
                    Plane plane = new Plane(Vector3.up, tetheredBody.position);
                    if (plane.Raycast(ray, out float enter) && enter > 0f && enter < 250f)
                    {
                        Vector3 hit = ray.GetPoint(enter);
                        targetWorldXZ = new Vector3(hit.x, 0f, hit.z);
                    }
                }
            }

            // Clamp within tether radius from Rect and world bounds
            Vector3 playerXZ = new Vector3(transform.position.x, 0f, transform.position.z);
            Vector3 offset = targetWorldXZ - playerXZ;
            float maxRadiusWorld = GridSettings.ToWorld(maxTetherRadius);
            if (offset.magnitude > maxRadiusWorld)
                offset = offset.normalized * maxRadiusWorld;
            targetWorldXZ = playerXZ + offset;

            if (tethered.TryGetPlanarHalfExtents(out float hx, out float hz))
                targetWorldXZ = WorldBounds.ClampInside(targetWorldXZ, hx, hz);

            // 2. Free Y Elevation (Scroll wheel + Q/Z keys)
            float elevDelta = 0f;
            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                    elevDelta += Mathf.Sign(scrollY) * GridSettings.ToWorld(scrollElevationStep);
            }
            if (keyboard != null)
            {
                if (keyboard.qKey.isPressed) elevDelta += GridSettings.ToWorld(elevationKeySpeed) * Time.deltaTime;
                if (keyboard.zKey.isPressed) elevDelta -= GridSettings.ToWorld(elevationKeySpeed) * Time.deltaTime;
            }
            targetHeightAboveSurface = Mathf.Clamp(
                targetHeightAboveSurface + elevDelta,
                GridSettings.ToWorld(minElevation),
                GridSettings.ToWorld(maxElevation));

            // 3. Free Rotation (R or Right Mouse Button)
            bool spinPressed = (keyboard != null && keyboard.rKey.isPressed) ||
                               (mouse != null && mouse.rightButton.isPressed);
            if (spinPressed)
            {
                desiredRotation = Quaternion.Euler(0f, freeSpinSpeed * Time.deltaTime, 0f) * desiredRotation;
            }

            // 4. Telekinetic Push / Throw (F)
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                Release(true);
                return;
            }

            float surfaceY = SampleSurfaceY(targetWorldXZ, tethered);
            float halfY = tethered.TryGetHalfExtents(out Vector3 ext) ? ext.y : GridSettings.ToWorld(0.5f);

            if (placementGhost != null)
            {
                placementGhost.SetOverrideXZ(targetWorldXZ);
                placementGhost.SetTargetRotation(desiredRotation);
            }

            UpdateVisualLines(surfaceY, tethered.transform.position);
        }

        private void FixedUpdate()
        {
            if (tethered == null || tetheredBody == null) return;

            float surfaceY = SampleSurfaceY(targetWorldXZ, tethered);
            float halfY = tethered.TryGetHalfExtents(out Vector3 ext) ? ext.y : GridSettings.ToWorld(0.5f);
            Vector3 desiredPos = new Vector3(targetWorldXZ.x, surfaceY + halfY + targetHeightAboveSurface, targetWorldXZ.z);
            if (tethered.TryGetPlanarHalfExtents(out float hx, out float hz))
                desiredPos = WorldBounds.ClampInside(desiredPos, hx, hz);

            Vector3 velocity = (desiredPos - tetheredBody.position) * followGain;
            float maxVel = GridSettings.ToWorld(maxFollowSpeed);
            tetheredBody.linearVelocity = Vector3.ClampMagnitude(velocity, maxVel);
            tetheredBody.MoveRotation(Quaternion.Slerp(tetheredBody.rotation, desiredRotation, Time.fixedDeltaTime * 14f));
        }

        private void UpdateVisualLines(float surfaceY, Vector3 objPos)
        {
            if (beamLine == null || plumbLine == null) return;

            Vector3 start = beamEmitter != null
                ? beamEmitter.position
                : transform.position + Vector3.up * GridSettings.ToWorld(0.9f);
            Vector3 mid = Vector3.Lerp(start, objPos, 0.5f) +
                          Vector3.up * GridSettings.ToWorld(0.35f + 0.08f * Mathf.Sin(Time.time * 4.5f));

            float pulseWidth = GridSettings.ToWorld(Mathf.Max(0.06f, beamWidth)) * (0.95f + 0.16f * Mathf.Sin(Time.time * 7f));
            beamLine.startWidth = pulseWidth * 0.72f;
            beamLine.endWidth = pulseWidth * 1.15f;

            for (int i = 0; i < BeamSegments; i++)
            {
                float t = i / (float)(BeamSegments - 1);
                float u = 1f - t;
                Vector3 pt = (u * u) * start + (2f * u * t) * mid + (t * t) * objPos;
                beamLine.SetPosition(i, pt);
            }

            float plumbWidth = GridSettings.ToWorld(0.032f);
            plumbLine.startWidth = plumbWidth;
            plumbLine.endWidth = plumbWidth;
            plumbLine.SetPosition(0, objPos);
            plumbLine.SetPosition(1, new Vector3(objPos.x, surfaceY + GridSettings.ToWorld(0.02f), objPos.z));
        }

        private static float SampleSurfaceY(Vector3 xzPos, InteractableObject ignoreTarget)
        {
            Vector3 probe = new Vector3(xzPos.x, GridSettings.ToWorld(12f), xzPos.z);
            RaycastHit[] hits = Physics.RaycastAll(probe, Vector3.down, GridSettings.ToWorld(25f), ~0, QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                Collider col = hits[i].collider;
                if (col == null) continue;
                InteractableObject hitObj = col.GetComponentInParent<InteractableObject>();
                if (hitObj != null && (hitObj == ignoreTarget || hitObj.canBePickedUp)) continue;
                return hits[i].point.y;
            }
            return 0f;
        }

        private void OnDisable()
        {
            if (tethered != null) Release(false);
        }
    }
}
