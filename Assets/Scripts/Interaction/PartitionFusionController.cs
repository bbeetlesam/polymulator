using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    public enum PartitionAxisMode
    {
        Auto = 0,
        LeftRight = 1,
        FrontBack = 2,
        Horizontal = 3
    }

    /// <summary>
    /// Executes core POLYMULATOR Partition [1] and Fusion [2] spatial operations.
    /// Shows a live 3D cut-plane blade preview ([3]), a curved magnetic Fusion arc,
    /// a diegetic ground target reticle, and choreographed physical cleave/pull animations.
    /// </summary>
    public sealed class PartitionFusionController : MonoBehaviour
    {
        private const int LinkArcSegments = 18;
        private const int FloorRingSegments = 16;
        private const float PartitionChargeSeconds = 0.09f;
        private const float PartitionSlideSeconds = 0.12f;
        private const float FusionPullSeconds = 0.20f;
        private const float PartitionHitStopSeconds = 0.045f;
        private const float FusionHitStopSeconds = 0.055f;

        [Header("Partition & Fusion (grid units)")]
        [Min(0.8f)] public float fusionPairRange = 3.2f;
        [Min(0.5f)] public float minimumSplitMass = 1.15f;
        [Min(0.3f)] public float minimumHalfExtent = 0.40f;
        [Range(1, 3)] public int maxPartitionDepth = 2;
        [SerializeField] private PartitionAxisMode cutAxisMode = PartitionAxisMode.Auto;

        [Header("References")]
        public SimulationModeController modeController;
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;

        private readonly Collider[] pairBuffer = new Collider[48];
        private readonly HashSet<InteractableObject> transitioningObjects = new HashSet<InteractableObject>();
        private int activeTransitionCount;

        private LineRenderer linkLine;
        private Material linkMaterial;
        private GameObject linkMidBead;
        private Material linkMidBeadMaterial;

        private GameObject cutPlaneRoot;
        private MeshRenderer cutPlaneFillRenderer;
        private Material cutPlaneFillMaterial;
        private LineRenderer cutPlaneFrame;
        private Material cutPlaneFrameMaterial;

        private LineRenderer targetFloorRing;
        private Material targetFloorRingMaterial;

        private InteractableObject cachedSource;
        private InteractableObject cachedPartner;
        private InteractableObject cachedCutTarget;
        private Vector3 cachedCutAxis = Vector3.right;
        private string cachedCutRuleLabel = "Auto (Ikut Hadap)";
        private bool cachedHasValidCut;
        private float cachedHalfMass;

        private Coroutine hitStopCoroutine;
        private bool hitStopActive;
        private float preHitStopTimeScale = 1f;

        public InteractableObject PreviewSource => cachedSource;
        public InteractableObject PreviewPartner => cachedPartner;
        public bool HasFusionPair => cachedSource != null && cachedPartner != null;
        public bool IsTransitioning => activeTransitionCount > 0;
        public PartitionAxisMode CutAxisMode => cutAxisMode;
        public InteractableObject PreviewCutTarget => cachedHasValidCut ? cachedCutTarget : null;
        public Vector3 PreviewCutAxis => cachedCutAxis;
        public bool HasCutPreview => cachedHasValidCut && cachedCutTarget != null;
        public string CutAxisShortLabel => GetAxisShortLabel(cutAxisMode);
        public string CutAxisRuleLabel => cachedHasValidCut ? cachedCutRuleLabel : GetAxisRuleFallback(cutAxisMode);
        public float PreviewSplitHalfMass => cachedHalfMass;

        public event Action<InteractableObject, InteractableObject> OnPartitionPerformed;
        public event Action<InteractableObject> OnFusionPerformed;
        public event Action<PartitionAxisMode> OnCutAxisModeChanged;

        private void Awake()
        {
            if (modeController == null) modeController = GetComponent<SimulationModeController>();
            if (interactor == null) interactor = GetComponent<ObjectInteractor>();
            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = GetComponent<TelekinesisController>();
            EnsureLinkVisual();
            EnsureCutPlaneVisual();
            EnsureTargetFloorRing();
        }

        private void EnsureLinkVisual()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            GameObject linkGo = new GameObject("Fusion Link Preview");
            linkGo.transform.SetParent(transform, false);
            linkLine = linkGo.AddComponent<LineRenderer>();
            linkLine.useWorldSpace = true;
            linkLine.positionCount = LinkArcSegments;
            linkLine.startWidth = GridSettings.ToWorld(0.045f);
            linkLine.endWidth = GridSettings.ToWorld(0.045f);
            linkLine.numCapVertices = 4;
            linkLine.numCornerVertices = 3;
            linkLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            linkLine.receiveShadows = false;

            Gradient arcGrad = new Gradient();
            arcGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(PaletteSettings.Green, 0f),
                    new GradientColorKey(PaletteSettings.Teal, 0.5f),
                    new GradientColorKey(PaletteSettings.Green, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.85f, 0f),
                    new GradientAlphaKey(1f, 0.5f),
                    new GradientAlphaKey(0.85f, 1f)
                });
            linkLine.colorGradient = arcGrad;

            linkMaterial = CreateTransparentUnlitMaterial(unlitShader, Color.white, 0.92f);
            linkLine.sharedMaterial = linkMaterial;
            linkLine.enabled = false;

            linkMidBead = GameObject.CreatePrimitive(PrimitiveType.Cube);
            linkMidBead.name = "Fusion Link Apex Bead";
            Collider col = linkMidBead.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            linkMidBead.transform.SetParent(transform, false);
            linkMidBead.transform.localScale = Vector3.one * GridSettings.ToWorld(0.14f);
            MeshRenderer beadMr = linkMidBead.GetComponent<MeshRenderer>();
            beadMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beadMr.receiveShadows = false;
            linkMidBeadMaterial = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Teal * 1.25f, 0.92f);
            beadMr.sharedMaterial = linkMidBeadMaterial;
            linkMidBead.SetActive(false);
        }

        private void EnsureCutPlaneVisual()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            cutPlaneRoot = new GameObject("PartitionCutPreview");
            cutPlaneRoot.transform.SetParent(transform, false);

            GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillGo.name = "CutPlaneFill";
            Collider col = fillGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            fillGo.transform.SetParent(cutPlaneRoot.transform, false);

            cutPlaneFillRenderer = fillGo.GetComponent<MeshRenderer>();
            cutPlaneFillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cutPlaneFillRenderer.receiveShadows = false;
            cutPlaneFillMaterial = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Peach * 1.15f, 0.24f);
            cutPlaneFillRenderer.sharedMaterial = cutPlaneFillMaterial;

            GameObject frameGo = new GameObject("CutPlaneFrame");
            frameGo.transform.SetParent(cutPlaneRoot.transform, false);
            cutPlaneFrame = frameGo.AddComponent<LineRenderer>();
            cutPlaneFrame.useWorldSpace = false;
            cutPlaneFrame.loop = true;
            cutPlaneFrame.positionCount = 4;
            cutPlaneFrame.SetPosition(0, new Vector3(-0.5f, -0.5f, 0f));
            cutPlaneFrame.SetPosition(1, new Vector3(0.5f, -0.5f, 0f));
            cutPlaneFrame.SetPosition(2, new Vector3(0.5f, 0.5f, 0f));
            cutPlaneFrame.SetPosition(3, new Vector3(-0.5f, 0.5f, 0f));
            cutPlaneFrame.startWidth = GridSettings.ToWorld(0.032f);
            cutPlaneFrame.endWidth = GridSettings.ToWorld(0.032f);
            cutPlaneFrame.numCapVertices = 3;
            cutPlaneFrame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            cutPlaneFrame.receiveShadows = false;

            Gradient frameGrad = new Gradient();
            frameGrad.SetKeys(
                new[]
                {
                    new GradientColorKey(PaletteSettings.Peach, 0f),
                    new GradientColorKey(PaletteSettings.Yellow, 0.5f),
                    new GradientColorKey(PaletteSettings.Peach, 1f)
                },
                new[] { new GradientAlphaKey(0.95f, 0f), new GradientAlphaKey(0.95f, 1f) });
            cutPlaneFrame.colorGradient = frameGrad;
            cutPlaneFrameMaterial = CreateTransparentUnlitMaterial(unlitShader, Color.white, 0.95f);
            cutPlaneFrame.sharedMaterial = cutPlaneFrameMaterial;

            cutPlaneRoot.SetActive(false);
        }

        private void EnsureTargetFloorRing()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            GameObject ringGo = new GameObject("TargetFloorRing");
            ringGo.transform.SetParent(transform, false);
            targetFloorRing = ringGo.AddComponent<LineRenderer>();
            targetFloorRing.useWorldSpace = true;
            targetFloorRing.loop = true;
            targetFloorRing.positionCount = FloorRingSegments;
            targetFloorRing.startWidth = GridSettings.ToWorld(0.028f);
            targetFloorRing.endWidth = GridSettings.ToWorld(0.028f);
            targetFloorRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            targetFloorRing.receiveShadows = false;

            targetFloorRingMaterial = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Sapphire, 0.65f);
            targetFloorRing.sharedMaterial = targetFloorRingMaterial;
            targetFloorRing.enabled = false;
        }

        private static Material CreateTransparentUnlitMaterial(Shader shader, Color baseColor, float alpha)
        {
            Material mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            baseColor.a = alpha;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            return mat;
        }

        public PartitionAxisMode CycleCutAxis()
        {
            cutAxisMode = (PartitionAxisMode)(((int)cutAxisMode + 1) % 4);
            SimulationAudio.PlayRotate(transform.position);
            RectVisualRig.NotifyRotateSnap(transform, 0.5f);
            OnCutAxisModeChanged?.Invoke(cutAxisMode);

            string desc = cutAxisMode switch
            {
                PartitionAxisMode.LeftRight => "KIRI | KANAN (Belah sumbu X)",
                PartitionAxisMode.FrontBack => "DEPAN | BELAKANG (Belah sumbu Z)",
                PartitionAxisMode.Horizontal => "ATAS | BAWAH (Belah horizontal Y)",
                _ => "OTOMATIS (Mengikuti hadap Rect & panjang objek)"
            };
            modeController?.NotifyBanner("Arah Partition [3]: " + desc, PaletteSettings.Peach);
            return cutAxisMode;
        }

        public void SetCutAxisMode(PartitionAxisMode mode)
        {
            if (cutAxisMode == mode) return;
            cutAxisMode = mode;
            OnCutAxisModeChanged?.Invoke(cutAxisMode);
        }

        private void LateUpdate()
        {
            if (IsTransitioning)
                return;

            cachedSource = null;
            cachedPartner = null;
            cachedCutTarget = null;
            cachedHasValidCut = false;
            cachedHalfMass = 0f;

            if (Time.timeScale <= 0f && !hitStopActive)
            {
                if (cutPlaneRoot != null && cutPlaneRoot.activeSelf)
                    cutPlaneRoot.SetActive(false);
                HideFusionPreviewVisuals();
                if (targetFloorRing != null && targetFloorRing.enabled) targetFloorRing.enabled = false;
                return;
            }

            InteractableObject active = ResolveActiveTarget();
            UpdateCutPlanePreview(active);
            UpdateFusionLinkPreview(active);
            UpdateTargetFloorRing(active);
        }

        private void UpdateCutPlanePreview(InteractableObject active)
        {
            bool partitionAllowed = modeController == null || modeController.CanUsePartition;
            if (!partitionAllowed || !CanPartitionTarget(active, out float sourceMass))
            {
                if (cutPlaneRoot != null && cutPlaneRoot.activeSelf)
                    cutPlaneRoot.SetActive(false);
                return;
            }

            Vector3 splitAxis = ComputeWorldSplitAxis(active, out string ruleLabel);
            cachedCutTarget = active;
            cachedCutAxis = splitAxis;
            cachedCutRuleLabel = ruleLabel;
            cachedHalfMass = Mathf.Max(0.4f, Mathf.Round((sourceMass * 0.5f) * 100f) * 0.01f);
            cachedHasValidCut = true;

            PositionCutPlaneAt(active, splitAxis, 1f, 0.20f + 0.08f * Mathf.Sin(Time.unscaledTime * 5.2f));
        }

        private void PositionCutPlaneAt(InteractableObject active, Vector3 splitAxis, float widthMul, float alpha)
        {
            if (active == null) return;
            if (cutPlaneRoot == null) EnsureCutPlaneVisual();
            if (cutPlaneRoot == null) return;

            Vector3 upHint = Mathf.Abs(splitAxis.y) > 0.9f ? transform.forward : Vector3.up;
            if (upHint.sqrMagnitude < 0.001f) upHint = Vector3.forward;
            Quaternion planeRot = Quaternion.LookRotation(splitAxis.normalized, upHint.normalized);

            Vector3 worldSize = GetTrueWorldSize(active);
            Vector3 planeRight = planeRot * Vector3.right;
            Vector3 planeUp = planeRot * Vector3.up;
            float pad = GridSettings.ToWorld(0.30f);
            float quadW = ProjectWorldSizeAlong(worldSize, active.transform, planeRight) + pad;
            float quadH = ProjectWorldSizeAlong(worldSize, active.transform, planeUp) + pad;

            cutPlaneRoot.transform.position = active.transform.position;
            cutPlaneRoot.transform.rotation = planeRot;
            cutPlaneRoot.transform.localScale = new Vector3(
                Mathf.Max(GridSettings.ToWorld(0.55f), quadW) * widthMul,
                Mathf.Max(GridSettings.ToWorld(0.55f), quadH) * widthMul,
                1f);

            if (cutPlaneFillMaterial != null)
            {
                Color c = PaletteSettings.Peach * 1.15f;
                c.a = alpha;
                if (cutPlaneFillMaterial.HasProperty("_BaseColor")) cutPlaneFillMaterial.SetColor("_BaseColor", c);
                if (cutPlaneFillMaterial.HasProperty("_Color")) cutPlaneFillMaterial.SetColor("_Color", c);
            }

            if (!cutPlaneRoot.activeSelf) cutPlaneRoot.SetActive(true);
        }

        private void UpdateFusionLinkPreview(InteractableObject src)
        {
            bool fusionAllowed = modeController == null || modeController.CanUseFusion;
            if (!fusionAllowed || src == null || !src.CanBePickedUp || !src.canFuse || transitioningObjects.Contains(src))
            {
                HideFusionPreviewVisuals();
                return;
            }

            InteractableObject partner = FindFusionPartner(src);
            if (partner == null)
            {
                HideFusionPreviewVisuals();
                return;
            }

            cachedSource = src;
            cachedPartner = partner;
            DrawMagneticArc(src.transform.position, partner.transform.position, 0.28f);
        }

        private void DrawMagneticArc(Vector3 p0, Vector3 p2, float liftRatio)
        {
            if (linkLine == null) return;
            linkLine.enabled = true;
            float dist = Vector3.Distance(p0, p2);
            float lift = Mathf.Clamp(dist * liftRatio, GridSettings.ToWorld(0.24f), GridSettings.ToWorld(0.95f))
                         + 0.05f * Mathf.Sin(Time.unscaledTime * 5.0f);
            Vector3 p1 = (p0 + p2) * 0.5f + Vector3.up * lift;

            for (int i = 0; i < LinkArcSegments; i++)
            {
                float t = i / (float)(LinkArcSegments - 1);
                float u = 1f - t;
                Vector3 pt = (u * u) * p0 + (2f * u * t) * p1 + (t * t) * p2;
                linkLine.SetPosition(i, pt);
            }

            if (linkMidBead != null)
            {
                Vector3 apex = 0.25f * p0 + 0.5f * p1 + 0.25f * p2;
                linkMidBead.transform.position = apex;
                linkMidBead.transform.rotation = Quaternion.Euler(45f, Time.unscaledTime * 110f, 35f);
                float pulse = 0.9f + 0.22f * Mathf.Sin(Time.unscaledTime * 6.5f);
                linkMidBead.transform.localScale = Vector3.one * (GridSettings.ToWorld(0.14f) * pulse);
                if (!linkMidBead.activeSelf) linkMidBead.SetActive(true);
            }
        }

        private void UpdateTargetFloorRing(InteractableObject active)
        {
            if (targetFloorRing == null) return;
            if (active == null || !active.CanBePickedUp)
            {
                if (targetFloorRing.enabled) targetFloorRing.enabled = false;
                return;
            }

            Vector3 extents = active.TryGetHalfExtents(out Vector3 ext) ? ext : Vector3.one * GridSettings.ToWorld(0.45f);
            float radius = Mathf.Max(extents.x, extents.z) + GridSettings.ToWorld(0.20f)
                           + 0.02f * Mathf.Sin(Time.unscaledTime * 4.5f);

            float groundY = Mathf.Max(0f, active.transform.position.y - extents.y);
            Vector3 origin = active.transform.position;
            RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, GridSettings.ToWorld(8f), ~(1 << 2), QueryTriggerInteraction.Ignore);
            float bestY = -999f;
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c.transform.IsChildOf(active.transform)) continue;
                if (hits[i].point.y > bestY) bestY = hits[i].point.y;
            }
            if (bestY > -900f) groundY = bestY;
            float ringY = groundY + 0.024f;

            float rotOffset = Time.unscaledTime * 0.45f;
            for (int i = 0; i < FloorRingSegments; i++)
            {
                float ang = rotOffset + (Mathf.PI * 2f * i) / FloorRingSegments;
                targetFloorRing.SetPosition(i, new Vector3(
                    active.transform.position.x + Mathf.Cos(ang) * radius,
                    ringY,
                    active.transform.position.z + Mathf.Sin(ang) * radius));
            }

            if (targetFloorRingMaterial != null)
            {
                Color tint = HasFusionPair
                    ? PaletteSettings.Green
                    : (HasCutPreview ? PaletteSettings.Peach : (modeController != null ? modeController.ModeColor : PaletteSettings.Sapphire));
                tint.a = 0.58f;
                if (targetFloorRingMaterial.HasProperty("_BaseColor")) targetFloorRingMaterial.SetColor("_BaseColor", tint);
                if (targetFloorRingMaterial.HasProperty("_Color")) targetFloorRingMaterial.SetColor("_Color", tint);
            }

            if (!targetFloorRing.enabled) targetFloorRing.enabled = true;
        }

        private void HideFusionPreviewVisuals()
        {
            if (linkLine != null && linkLine.enabled) linkLine.enabled = false;
            if (linkMidBead != null && linkMidBead.activeSelf) linkMidBead.SetActive(false);
        }

        private bool CanPartitionTarget(InteractableObject target, out float sourceMass)
        {
            sourceMass = 0f;
            if (target == null || !target.CanBePickedUp || !target.canPartition || transitioningObjects.Contains(target))
                return false;

            Rigidbody rb = target.Body;
            sourceMass = rb != null ? rb.mass : 1f;
            Vector3 worldSize = GetTrueWorldSize(target);
            float maxSpan = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
            return target.partitionDepth < maxPartitionDepth
                   && sourceMass >= minimumSplitMass
                   && GridSettings.ToGrid(maxSpan) >= minimumHalfExtent * 1.5f;
        }

        public bool TryPartition()
        {
            if (IsTransitioning) return false;

            InteractableObject target = ResolveActiveTarget();
            if (target == null)
            {
                modeController?.NotifyBanner("Dekati atau pegang objek terlebih dulu untuk membelah [1]", PaletteSettings.Peach);
                return false;
            }

            if (!target.CanBePickedUp || !target.canPartition)
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                modeController?.NotifyBanner(target.displayName + " tidak dapat dibelah", PaletteSettings.Peach);
                return false;
            }

            if (!CanPartitionTarget(target, out float sourceMass))
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Yellow, 1.3f, 0.32f);
                modeController?.NotifyBanner(target.displayName + " sudah terlalu kecil untuk dibelah lagi", PaletteSettings.Yellow);
                return false;
            }

            StartCoroutine(PartitionRoutine(target, sourceMass));
            return true;
        }

        private IEnumerator PartitionRoutine(InteractableObject target, float sourceMass)
        {
            activeTransitionCount++;
            transitioningObjects.Add(target);

            Vector3 trueWorldSize = GetTrueWorldSize(target);
            Vector3 originPos = ReleaseFromHoldersAndPlates(target, out bool wasHeldByRect);
            Vector3 splitAxis = ComputeWorldSplitAxis(target, out string axisDesc);
            if (wasHeldByRect)
            {
                Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
                originPos = transform.position + fwd.normalized * GridSettings.ToWorld(1.25f) + Vector3.up * GridSettings.ToWorld(0.65f);
                target.transform.position = originPos;
            }

            // Lock physics cleanly during laser charge anticipation
            target.canBePickedUp = false;
            target.SetCollidersEnabled(false);
            if (target.Body != null) target.Body.isKinematic = true;
            if (targetFloorRing != null) targetFloorRing.enabled = false;

            RectVisualRig.NotifyCarryAction(transform, true);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 10, 1.8f);

            // Phase 1: Laser cleave anticipation & micro buzz
            Vector3 restPos = target.transform.position;
            Vector3 buzzAxis = Mathf.Abs(splitAxis.y) > 0.7f ? Vector3.right : splitAxis;
            float elapsed = 0f;
            while (elapsed < PartitionChargeSeconds && target != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / PartitionChargeSeconds);
                float buzz = Mathf.Sin(u * Mathf.PI * 6f) * GridSettings.ToWorld(0.025f) * u;
                target.transform.position = restPos + buzzAxis * buzz;
                PositionCutPlaneAt(target, splitAxis, 1f + 0.24f * u, 0.28f + 0.36f * u);
                yield return null;
            }

            if (target == null)
            {
                activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);
                yield break;
            }
            target.transform.position = restPos;

            // Compute final target placements for split halves
            float halfMass = Mathf.Max(0.4f, Mathf.Round((sourceMass * 0.5f) * 100f) * 0.01f);
            Color baseColor = target.AssignedColor;
            Color colorA = PaletteSettings.NearestInteractiveAccent(Color.Lerp(baseColor, PaletteSettings.Peach, 0.25f));
            Color colorB = PaletteSettings.NearestInteractiveAccent(Color.Lerp(baseColor, PaletteSettings.Sky, 0.25f));
            Vector3 newScale = ComputeSplitScale(target, trueWorldSize, splitAxis);
            bool horizontalCut = Mathf.Abs(splitAxis.y) > 0.7f;

            Vector3 posA;
            Vector3 posB;
            Vector3 startA;
            Vector3 startB;
            Vector3 finalVelA;
            Vector3 finalVelB;

            if (horizontalCut)
            {
                float bottomY = originPos.y - trueWorldSize.y * 0.5f;
                float halfH = newScale.y * 0.5f;
                float groundSafeA = Mathf.Max(bottomY + halfH, halfH * 1.02f);
                posA = WorldBounds.ClampInside(new Vector3(originPos.x, groundSafeA, originPos.z), newScale.x * 0.5f, newScale.z * 0.5f);
                posA.y = groundSafeA;
                posB = new Vector3(posA.x, posA.y + newScale.y + GridSettings.ToWorld(0.06f), posA.z);
                startA = new Vector3(originPos.x, posA.y, originPos.z);
                startB = new Vector3(originPos.x, posA.y + newScale.y * 0.55f, originPos.z);
                finalVelA = Vector3.zero;
                finalVelB = Vector3.up * 0.55f + transform.right * 0.15f;
            }
            else
            {
                float separation = Mathf.Max(GridSettings.ToWorld(0.42f), EstimateExtentAlongAxis(target, newScale, splitAxis) * 1.25f);
                posA = WorldBounds.ClampInside(originPos - splitAxis * (separation * 0.55f), newScale.x * 0.5f, newScale.z * 0.5f);
                posB = WorldBounds.ClampInside(originPos + splitAxis * (separation * 0.55f), newScale.x * 0.5f, newScale.z * 0.5f);
                posA.y = Mathf.Max(posA.y, newScale.y * 0.52f);
                posB.y = Mathf.Max(posB.y, newScale.y * 0.52f);
                startA = originPos - splitAxis * GridSettings.ToWorld(0.14f);
                startB = originPos + splitAxis * GridSettings.ToWorld(0.14f);
                finalVelA = -splitAxis * 0.45f;
                finalVelB = splitAxis * 0.45f;
            }

            string baseLabel = TrimPartSuffix(target.displayName);
            InteractableObject partA = SpawnSplitHalf(target, baseLabel + "-A", startA, newScale, halfMass, colorA, kinematicDuringSlide: true);
            InteractableObject partB = SpawnSplitHalf(target, baseLabel + "-B", startB, newScale, halfMass, colorB, kinematicDuringSlide: true);

            transitioningObjects.Remove(target);
            transitioningObjects.Add(partA);
            transitioningObjects.Add(partB);
            Destroy(target.gameObject);
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);

            // Phase 2 Impact peak + hit-stop
            SimulationAudio.PlayPartition(originPos);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 24, 3.4f);
            SimulationVFX.Ring(originPos, PaletteSettings.Peach, 2.2f, 0.45f);
            SimulationVisualBridge.TriggerFloorRipple(originPos, PaletteSettings.Peach, 5.2f, 1.0f, 0.62f);
            RectVisualRig.NotifyPartitionChop(transform, splitAxis);
            RectCameraController.AddImpulse(0.085f);
            TriggerHitStop(PartitionHitStopSeconds);

            // Let hit-stop finish before sliding halves apart
            while (hitStopActive) yield return null;

            // Phase 3: Smooth ease-out glide separation along split vector
            float slideElapsed = 0f;
            float liftPeak = horizontalCut ? GridSettings.ToWorld(0.08f) : GridSettings.ToWorld(0.14f);
            while (slideElapsed < PartitionSlideSeconds)
            {
                slideElapsed += Time.deltaTime;
                float u = Mathf.Clamp01(slideElapsed / PartitionSlideSeconds);
                float ease = 1f - Mathf.Pow(1f - u, 3f);
                float arc = Mathf.Sin(u * Mathf.PI) * liftPeak;

                if (partA != null)
                    partA.transform.position = Vector3.Lerp(startA, posA, ease) + (horizontalCut ? Vector3.zero : Vector3.up * (arc * 0.5f));
                if (partB != null)
                    partB.transform.position = Vector3.Lerp(startB, posB, ease) + Vector3.up * arc;
                yield return null;
            }

            FinalizeSplitHalfPhysics(partA, posA, finalVelA);
            FinalizeSplitHalfPhysics(partB, posB, finalVelB);

            transitioningObjects.Remove(partA);
            transitioningObjects.Remove(partB);
            activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);

            modeController?.NotifyBanner(
                $"Partition ({axisDesc}): {baseLabel} -> {partA.displayName} & {partB.displayName} ({halfMass:0.0}t x 2)",
                PaletteSettings.Peach);
            OnPartitionPerformed?.Invoke(partA, partB);
        }

        private void FinalizeSplitHalfPhysics(InteractableObject part, Vector3 finalPos, Vector3 vel)
        {
            if (part == null) return;
            part.transform.position = finalPos;
            part.canBePickedUp = true;
            part.SetCollidersEnabled(true);
            Rigidbody rb = part.Body;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.position = finalPos;
                rb.linearVelocity = vel;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }
            part.SetSafeOrigin(finalPos, part.transform.rotation);
        }

        public bool TryFusion()
        {
            if (IsTransitioning) return false;

            InteractableObject src = ResolveActiveTarget();
            if (src == null)
            {
                modeController?.NotifyBanner("Dekati atau pegang satu objek di dekat pasangannya untuk digabung [2]", PaletteSettings.Green);
                return false;
            }

            if (!src.CanBePickedUp || !src.canFuse)
            {
                SimulationVFX.Ring(src.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                modeController?.NotifyBanner(src.displayName + " tidak dapat digabungkan", PaletteSettings.Peach);
                return false;
            }

            InteractableObject partner = FindFusionPartner(src);
            if (partner == null)
            {
                string hint = IsCoreObject(src)
                    ? src.displayName + " hanya bisa disatukan lagi dengan pecahan CORE-01 lain"
                    : "Tidak ada objek lain di dekat " + src.displayName + " untuk digabung [2]";
                SimulationVFX.Ring(src.transform.position, PaletteSettings.Yellow, 1.8f, 0.40f);
                modeController?.NotifyBanner(hint, PaletteSettings.Yellow);
                return false;
            }

            StartCoroutine(FusionRoutine(src, partner));
            return true;
        }

        private IEnumerator FusionRoutine(InteractableObject src, InteractableObject partner)
        {
            activeTransitionCount++;
            transitioningObjects.Add(src);
            transitioningObjects.Add(partner);

            bool restoringCore = IsCoreObject(src) && IsCoreObject(partner);
            bool heterogeneousVisual = HasNonBoxGeometry(src) || HasNonBoxGeometry(partner) ||
                                       HasColorContrast(src.AssignedColor, partner.AssignedColor);

            Vector3 partnerStart = ReleaseFromHoldersAndPlates(partner, out _);
            Vector3 srcStart = ReleaseFromHoldersAndPlates(src, out bool srcWasHeld);
            if (srcWasHeld)
            {
                srcStart = transform.position + transform.forward * GridSettings.ToWorld(1.1f) + Vector3.up * GridSettings.ToWorld(0.65f);
                src.transform.position = srcStart;
            }

            Vector3 mergePos = srcWasHeld
                ? transform.position + transform.forward * GridSettings.ToWorld(1.3f) + Vector3.up * GridSettings.ToWorld(0.75f)
                : (srcStart + partnerStart) * 0.5f;

            float massA = src.Body != null ? src.Body.mass : 1f;
            float massB = partner.Body != null ? partner.Body.mass : 1f;
            float totalMass = Mathf.Round((massA + massB) * 100f) * 0.01f;

            Vector3 sizeA = GetTrueWorldSize(src);
            Vector3 sizeB = GetTrueWorldSize(partner);
            Vector3 mergedScale = restoringCore
                ? Vector3.one * GridSettings.ToWorld(0.92f)
                : new Vector3(
                    Mathf.Clamp(Mathf.Max((sizeA.x + sizeB.x) * 0.5f * 1.18f, Mathf.Max(sizeA.x, sizeB.x) * 1.04f), GridSettings.ToWorld(0.84f), GridSettings.ToWorld(2.1f)),
                    Mathf.Clamp(Mathf.Max((sizeA.y + sizeB.y) * 0.5f * 1.14f, Mathf.Max(sizeA.y, sizeB.y) * 1.03f), GridSettings.ToWorld(0.76f), GridSettings.ToWorld(1.9f)),
                    Mathf.Clamp(Mathf.Max((sizeA.z + sizeB.z) * 0.5f * 1.18f, Mathf.Max(sizeA.z, sizeB.z) * 1.04f), GridSettings.ToWorld(0.84f), GridSettings.ToWorld(2.1f)));

            mergePos = WorldBounds.ClampInside(mergePos, mergedScale.x * 0.55f, mergedScale.z * 0.55f);
            mergePos.y = Mathf.Max(mergePos.y, mergedScale.y * 0.54f);

            // Lock both objects cleanly during magnetic attraction pull
            src.canBePickedUp = false;
            partner.canBePickedUp = false;
            src.SetCollidersEnabled(false);
            partner.SetCollidersEnabled(false);
            if (src.Body != null) src.Body.isKinematic = true;
            if (partner.Body != null) partner.Body.isKinematic = true;
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);
            if (targetFloorRing != null) targetFloorRing.enabled = false;

            Quaternion srcRotStart = src.transform.rotation;
            Quaternion partnerRotStart = partner.transform.rotation;
            Quaternion targetRot = Quaternion.Euler(0f, src.transform.eulerAngles.y, 0f);
            Vector3 srcScaleStart = src.transform.localScale;
            Vector3 partnerScaleStart = partner.transform.localScale;

            RectVisualRig.NotifyCarryAction(transform, true);
            SimulationVFX.Ring(mergePos, PaletteSettings.Teal, 1.8f, 0.25f);

            // Phase 1: Magnetic levitation & accelerating convergence pull
            float elapsed = 0f;
            float liftAmp = GridSettings.ToWorld(0.32f);
            while (elapsed < FusionPullSeconds && src != null && partner != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / FusionPullSeconds);
                float pull = u * u * (1.2f - 0.2f * u);
                float arc = Mathf.Sin(u * Mathf.PI) * liftAmp;

                Vector3 pA = Vector3.Lerp(srcStart, mergePos, pull) + Vector3.up * arc;
                Vector3 pB = Vector3.Lerp(partnerStart, mergePos, pull) + Vector3.up * arc;
                src.transform.SetPositionAndRotation(pA, Quaternion.Slerp(srcRotStart, targetRot, u));
                partner.transform.SetPositionAndRotation(pB, Quaternion.Slerp(partnerRotStart, targetRot, u));
                src.transform.localScale = Vector3.Lerp(srcScaleStart, srcScaleStart * 0.88f, pull);
                partner.transform.localScale = Vector3.Lerp(partnerScaleStart, partnerScaleStart * 0.88f, pull);
                DrawMagneticArc(pA, pB, 0.22f * (1f - u));
                yield return null;
            }

            if (src == null || partner == null)
            {
                HideFusionPreviewVisuals();
                if (src != null) transitioningObjects.Remove(src);
                if (partner != null) transitioningObjects.Remove(partner);
                activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);
                yield break;
            }

            Color blendedColor = restoringCore
                ? PaletteSettings.Yellow
                : PaletteSettings.NearestInteractiveAccent(
                    Color.Lerp(Color.Lerp(src.AssignedColor, partner.AssignedColor, 0.5f), PaletteSettings.Green, 0.25f));

            bool partnerWasNonBox = HasNonBoxGeometry(src) || HasNonBoxGeometry(partner);
            Color partnerCol = partner.AssignedColor;
            string srcLabel = src.displayName;
            string partnerLabel = partner.displayName;

            GameObject mergedGo = Instantiate(src.gameObject, mergePos, targetRot);
            if (restoringCore)
            {
                RestoreCoreVisuals(mergedGo);
                mergedGo.transform.localScale = Vector3.one * GridSettings.ToWorld(1f);
            }
            else
            {
                ConvertToChamferCubeSafely(mergedGo, stripHeightCoupledTrim: true);
                mergedGo.transform.localScale = mergedScale;
            }

            InteractableObject merged = mergedGo.GetComponent<InteractableObject>();
            merged.canBePickedUp = true;
            if (restoringCore)
            {
                mergedGo.name = "CORE-01";
                merged.displayName = "CORE-01";
                merged.partitionDepth = 0;
                merged.isFusedComposite = false;
                merged.canPartition = true;
                merged.canFuse = false;
            }
            else
            {
                string fusionLabel = "FUSED-" + totalMass.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "T";
                mergedGo.name = fusionLabel;
                merged.displayName = fusionLabel;
                merged.partitionDepth = 0;
                merged.isFusedComposite = true;
                merged.canPartition = true;
                merged.canFuse = true;

                if (heterogeneousVisual)
                    AttachHybridFusionInlay(mergedGo, partnerCol, partnerWasNonBox);
            }

            Rigidbody mergedRb = merged.Body;
            if (mergedRb != null)
            {
                mergedRb.isKinematic = false;
                mergedRb.detectCollisions = true;
                mergedRb.mass = totalMass;
                mergedRb.linearVelocity = Vector3.up * 0.85f;
                mergedRb.angularVelocity = Vector3.zero;
                mergedRb.WakeUp();
            }

            merged.SetCollidersEnabled(true);
            merged.SetSafeOrigin(mergePos, merged.transform.rotation);
            merged.ReinitializeClonedVisuals(blendedColor);
            merged.TriggerPulse(0.24f, 0.34f);

            transitioningObjects.Remove(src);
            transitioningObjects.Remove(partner);
            Destroy(src.gameObject);
            Destroy(partner.gameObject);

            HideFusionPreviewVisuals();
            SimulationAudio.PlayFusion(mergePos);
            SimulationVFX.Burst(mergePos, PaletteSettings.Green, 30, 4.0f);
            SimulationVFX.Ring(mergePos, PaletteSettings.Green, 2.5f, 0.52f);
            SimulationVisualBridge.TriggerFloorRipple(mergePos, PaletteSettings.Green, 6.4f, 1.18f, 0.68f);
            RectVisualRig.NotifyFusionPulse(transform);
            RectCameraController.AddImpulse(0.095f);
            TriggerHitStop(FusionHitStopSeconds);

            activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);

            string banner = restoringCore
                ? "CORE-01 dipulihkan utuh melalui Fusion [2]!"
                : $"Fusion Hibrid: {srcLabel} + {partnerLabel} -> {merged.displayName}";
            modeController?.NotifyBanner(banner, restoringCore ? PaletteSettings.Yellow : PaletteSettings.Green);
            OnFusionPerformed?.Invoke(merged);
        }

        private InteractableObject ResolveActiveTarget()
        {
            if (carryController != null && carryController.IsCarrying)
                return carryController.CarriedObject;
            if (telekinesisController != null && telekinesisController.HasTether)
                return telekinesisController.TetheredObject;
            return interactor != null ? interactor.CurrentTarget : null;
        }

        private InteractableObject FindFusionPartner(InteractableObject src)
        {
            float searchRadius = GridSettings.ToWorld(fusionPairRange);
            if (modeController != null && modeController.IsFlexible) searchRadius *= 1.25f;

            bool srcIsCore = IsCoreObject(src);
            int count = Physics.OverlapSphereNonAlloc(src.transform.position, searchRadius, pairBuffer, ~0, QueryTriggerInteraction.Ignore);
            InteractableObject best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = pairBuffer[i];
                if (col == null) continue;
                InteractableObject cand = col.GetComponentInParent<InteractableObject>();
                if (cand == null || cand == src || !cand.isActiveAndEnabled) continue;
                if (!cand.CanBePickedUp || !cand.canFuse || transitioningObjects.Contains(cand)) continue;
                if (IsCoreObject(cand) != srcIsCore) continue;
                if (carryController != null && cand == carryController.CarriedObject) continue;
                if (telekinesisController != null && cand == telekinesisController.TetheredObject) continue;

                float d = Vector3.Distance(src.transform.position, cand.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cand;
                }
            }
            return best;
        }

        private Vector3 ReleaseFromHoldersAndPlates(InteractableObject target, out bool wasCarriedByRect)
        {
            wasCarriedByRect = false;
            Vector3 pos = target.transform.position;

            if (carryController != null && carryController.CarriedObject == target)
            {
                wasCarriedByRect = true;
                carryController.ReleaseAndReturn();
            }
            if (telekinesisController != null && telekinesisController.TetheredObject == target)
                telekinesisController.Release(false);

            Rigidbody rb = target.Body;
            if (rb != null)
            {
                WeightedPressurePlate[] plates = FindObjectsByType<WeightedPressurePlate>(FindObjectsSortMode.None);
                for (int i = 0; i < plates.Length; i++)
                    plates[i].UnregisterBody(rb);
            }
            return pos;
        }

        private Vector3 ComputeWorldSplitAxis(InteractableObject target, out string reasonLabel)
        {
            Vector3 localX = Vector3.ProjectOnPlane(target.transform.right, Vector3.up).normalized;
            Vector3 localZ = Vector3.ProjectOnPlane(target.transform.forward, Vector3.up).normalized;
            if (localX.sqrMagnitude < 0.001f) localX = Vector3.right;
            if (localZ.sqrMagnitude < 0.001f) localZ = Vector3.forward;

            if (cutAxisMode == PartitionAxisMode.Horizontal)
            {
                reasonLabel = "Atas | Bawah (Y)";
                return Vector3.up;
            }
            if (cutAxisMode == PartitionAxisMode.LeftRight)
            {
                reasonLabel = "Kiri | Kanan (X)";
                return Mathf.Abs(Vector3.Dot(Vector3.right, localX)) >= Mathf.Abs(Vector3.Dot(Vector3.right, localZ)) ? localX : localZ;
            }
            if (cutAxisMode == PartitionAxisMode.FrontBack)
            {
                reasonLabel = "Depan | Belakang (Z)";
                return Mathf.Abs(Vector3.Dot(Vector3.forward, localX)) >= Mathf.Abs(Vector3.Dot(Vector3.forward, localZ)) ? localX : localZ;
            }

            Vector3 size = GetTrueWorldSize(target);
            if (size.z > size.x * 1.15f)
            {
                reasonLabel = "Auto (Panjang Objek)";
                return localZ;
            }
            if (size.x > size.z * 1.15f)
            {
                reasonLabel = "Auto (Lebar Objek)";
                return localX;
            }

            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up);
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            right.Normalize();

            bool pickX = Mathf.Abs(Vector3.Dot(right, localX)) >= Mathf.Abs(Vector3.Dot(right, localZ));
            reasonLabel = "Auto (Hadap Rect)";
            return pickX ? localX : localZ;
        }

        private static Vector3 ComputeSplitScale(InteractableObject target, Vector3 trueWorldSize, Vector3 splitAxisWorld)
        {
            if (Mathf.Abs(splitAxisWorld.y) > 0.7f)
            {
                return new Vector3(
                    trueWorldSize.x * 0.96f,
                    Mathf.Max(GridSettings.ToWorld(0.42f), trueWorldSize.y * 0.52f),
                    trueWorldSize.z * 0.96f);
            }

            Vector3 localAxis = target.transform.InverseTransformDirection(splitAxisWorld);
            bool cutX = Mathf.Abs(localAxis.x) >= Mathf.Abs(localAxis.z);

            if (cutX)
                return new Vector3(Mathf.Max(GridSettings.ToWorld(0.45f), trueWorldSize.x * 0.54f), trueWorldSize.y * 0.96f, trueWorldSize.z * 0.96f);
            return new Vector3(trueWorldSize.x * 0.96f, trueWorldSize.y * 0.96f, Mathf.Max(GridSettings.ToWorld(0.45f), trueWorldSize.z * 0.54f));
        }

        private static float EstimateExtentAlongAxis(InteractableObject target, Vector3 scale, Vector3 axisWorld)
        {
            Vector3 localAxis = target.transform.InverseTransformDirection(axisWorld);
            return Mathf.Abs(localAxis.x) * scale.x + Mathf.Abs(localAxis.y) * scale.y + Mathf.Abs(localAxis.z) * scale.z;
        }

        private static float ProjectWorldSizeAlong(Vector3 worldSize, Transform targetTransform, Vector3 worldDir)
        {
            Vector3 localDir = targetTransform.InverseTransformDirection(worldDir.normalized);
            return Mathf.Abs(localDir.x) * worldSize.x + Mathf.Abs(localDir.y) * worldSize.y + Mathf.Abs(localDir.z) * worldSize.z;
        }

        private static Vector3 GetTrueWorldSize(InteractableObject target)
        {
            if (target == null) return Vector3.one;
            if (target.TryGetHalfExtents(out Vector3 halfExtents))
                return halfExtents * 2f;
            return target.transform.localScale;
        }

        private InteractableObject SpawnSplitHalf(InteractableObject template, string label, Vector3 pos,
            Vector3 scale, float mass, Color accentColor, bool kinematicDuringSlide)
        {
            GameObject go = Instantiate(template.gameObject, pos, template.transform.rotation);
            go.name = label;
            HideCoreCageIfPresent(go, hidden: true);
            ConvertToChamferCubeSafely(go, stripHeightCoupledTrim: true);
            go.transform.localScale = scale;

            InteractableObject info = go.GetComponent<InteractableObject>();
            info.displayName = label;
            info.partitionDepth = template.partitionDepth + 1;
            info.isFusedComposite = false;
            info.canPartition = info.partitionDepth < maxPartitionDepth && mass >= minimumSplitMass;
            info.canFuse = true;
            info.canBePickedUp = !kinematicDuringSlide;

            Rigidbody rb = info.Body;
            if (rb != null)
            {
                rb.isKinematic = kinematicDuringSlide;
                rb.detectCollisions = !kinematicDuringSlide;
                rb.mass = mass;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                if (!kinematicDuringSlide) rb.WakeUp();
            }

            info.SetCollidersEnabled(!kinematicDuringSlide);
            info.SetSafeOrigin(pos, go.transform.rotation);
            info.ReinitializeClonedVisuals(accentColor);
            info.TriggerPulse(0.16f, 0.22f);
            return info;
        }

        private static void HideCoreCageIfPresent(GameObject go, bool hidden)
        {
            if (go == null) return;
            Transform cage = go.transform.Find("Core Cage Frame");
            if (cage != null) cage.gameObject.SetActive(!hidden);
            Transform frontVisor = go.transform.Find("Core Front Visor");
            if (frontVisor != null && hidden) frontVisor.gameObject.SetActive(false);
            else if (frontVisor != null) frontVisor.gameObject.SetActive(true);
        }

        private static void RestoreCoreVisuals(GameObject go)
        {
            if (go == null) return;
            HideCoreCageIfPresent(go, hidden: false);
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc != null)
            {
                bc.center = Vector3.zero;
                bc.size = Vector3.one * GridSettings.ToWorld(0.92f);
            }
            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                Mesh coreInner = FindLoadedMesh("Core_InnerChamber", PrimitiveType.Cube);
                if (coreInner != null) mf.sharedMesh = coreInner;
            }
        }

        private static void ConvertToChamferCubeSafely(GameObject go, bool stripHeightCoupledTrim)
        {
            if (stripHeightCoupledTrim)
                StripDynamicTrimChildren(go.transform);

            SphereCollider sc = go.GetComponent<SphereCollider>();
            CapsuleCollider cc = go.GetComponent<CapsuleCollider>();
            MeshFilter mf = go.GetComponent<MeshFilter>();
            bool nonBoxMesh = mf != null && mf.sharedMesh != null &&
                              mf.sharedMesh.name != "ChamferCube" && mf.sharedMesh.name != "Cube";
            if (sc == null && cc == null && !nonBoxMesh) return;

            if (sc != null) DestroyImmediate(sc);
            if (cc != null) DestroyImmediate(cc);
            BoxCollider bc = go.GetComponent<BoxCollider>();
            if (bc == null) bc = go.AddComponent<BoxCollider>();
            bc.center = Vector3.zero;
            bc.size = Vector3.one;

            if (mf != null)
            {
                Mesh chamferCube = FindLoadedMesh("ChamferCube", PrimitiveType.Cube);
                if (chamferCube != null) mf.sharedMesh = chamferCube;
            }
        }

        private static void StripDynamicTrimChildren(Transform root)
        {
            if (root == null) return;
            List<GameObject> toDestroy = null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child == null) continue;
                string n = child.name;
                if (n == "Top Collar" || n == "Base Collar" || n == "Gyro Belt" || n == "Axis Band E" ||
                    n == "Fusion Core Inlay" || n == "Fusion Octo Collar")
                {
                    if (toDestroy == null) toDestroy = new List<GameObject>(4);
                    toDestroy.Add(child.gameObject);
                }
            }
            if (toDestroy != null)
            {
                for (int i = 0; i < toDestroy.Count; i++)
                    DestroyImmediate(toDestroy[i]);
            }
        }

        private static void AttachHybridFusionInlay(GameObject mergedGo, Color partnerColor, bool includeOctoCollar)
        {
            Material partnerMat = InteractableObject.GetSharedSurfaceMaterial(PaletteSettings.NearestInteractiveAccent(partnerColor));

            GameObject belt = GameObject.CreatePrimitive(PrimitiveType.Cube);
            belt.name = "Fusion Core Inlay";
            Collider beltCol = belt.GetComponent<Collider>();
            if (beltCol != null) DestroyImmediate(beltCol);
            belt.transform.SetParent(mergedGo.transform, false);
            belt.transform.localPosition = Vector3.zero;
            belt.transform.localRotation = Quaternion.identity;
            belt.transform.localScale = new Vector3(1.025f, 0.24f, 1.025f);
            belt.AddComponent<ObjectTrim>();
            Mesh chamferCube = FindLoadedMesh("ChamferCube", PrimitiveType.Cube);
            if (chamferCube != null) belt.GetComponent<MeshFilter>().sharedMesh = chamferCube;
            belt.GetComponent<MeshRenderer>().sharedMaterial = partnerMat;

            if (includeOctoCollar)
            {
                GameObject octo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                octo.name = "Fusion Octo Collar";
                Collider octoCol = octo.GetComponent<Collider>();
                if (octoCol != null) DestroyImmediate(octoCol);
                octo.transform.SetParent(mergedGo.transform, false);
                octo.transform.localPosition = Vector3.zero;
                octo.transform.localRotation = Quaternion.identity;
                octo.transform.localScale = new Vector3(0.68f, 0.535f, 0.68f);
                octo.AddComponent<ObjectTrim>();
                Mesh octoMesh = FindLoadedMesh("OctagonPrism", PrimitiveType.Cylinder);
                if (octoMesh != null) octo.GetComponent<MeshFilter>().sharedMesh = octoMesh;
                octo.GetComponent<MeshRenderer>().sharedMaterial = partnerMat;
            }
        }

        private static bool HasNonBoxGeometry(InteractableObject obj)
        {
            if (obj == null) return false;
            if (obj.GetComponent<CapsuleCollider>() != null || obj.GetComponent<SphereCollider>() != null)
                return true;
            MeshFilter mf = obj.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string n = mf.sharedMesh.name;
                return n != "ChamferCube" && n != "Cube" && n != "Core_InnerChamber";
            }
            return false;
        }

        private static bool HasColorContrast(Color a, Color b)
        {
            return (Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b)) > 0.18f;
        }

        private static bool IsCoreObject(InteractableObject obj)
        {
            if (obj == null) return false;
            return obj.displayName.IndexOf("CORE-01", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   obj.name.IndexOf("CORE-01", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Mesh FindLoadedMesh(string preferredName, PrimitiveType fallbackType)
        {
            Mesh[] meshes = Resources.FindObjectsOfTypeAll<Mesh>();
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null && meshes[i].name == preferredName)
                    return meshes[i];
            }

            GameObject tmp = GameObject.CreatePrimitive(fallbackType);
            Mesh fallback = tmp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tmp);
            return fallback;
        }

        private static string TrimPartSuffix(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "PART";
            if (raw.EndsWith("-A") || raw.EndsWith("-B"))
                return raw.Substring(0, raw.Length - 2);
            return raw;
        }

        private static string GetAxisShortLabel(PartitionAxisMode mode)
        {
            return mode switch
            {
                PartitionAxisMode.LeftRight => "KIRI | KANAN",
                PartitionAxisMode.FrontBack => "DEPAN | BELAKANG",
                PartitionAxisMode.Horizontal => "ATAS | BAWAH",
                _ => "OTOMATIS"
            };
        }

        private static string GetAxisRuleFallback(PartitionAxisMode mode)
        {
            return mode switch
            {
                PartitionAxisMode.LeftRight => "Sumbu Kiri | Kanan (X)",
                PartitionAxisMode.FrontBack => "Sumbu Depan | Belakang (Z)",
                PartitionAxisMode.Horizontal => "Sumbu Atas | Bawah (Y)",
                _ => "Auto (Hadap Rect)"
            };
        }

        private void TriggerHitStop(float seconds)
        {
            if (!gameObject.activeInHierarchy || !enabled || seconds <= 0f) return;
            if (hitStopActive)
            {
                if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
                Time.timeScale = preHitStopTimeScale;
                hitStopActive = false;
            }
            if (Time.timeScale <= 0f) return;
            hitStopCoroutine = StartCoroutine(HitStopRoutine(seconds));
        }

        private IEnumerator HitStopRoutine(float seconds)
        {
            preHitStopTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            hitStopActive = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = preHitStopTimeScale;
            hitStopActive = false;
            hitStopCoroutine = null;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            activeTransitionCount = 0;
            transitioningObjects.Clear();
            hitStopCoroutine = null;
            if (hitStopActive)
            {
                Time.timeScale = preHitStopTimeScale;
                hitStopActive = false;
            }
            HideFusionPreviewVisuals();
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);
            if (targetFloorRing != null) targetFloorRing.enabled = false;
        }

        private void OnDestroy()
        {
            if (hitStopActive)
            {
                Time.timeScale = preHitStopTimeScale;
                hitStopActive = false;
            }
            if (linkMaterial != null) Destroy(linkMaterial);
            if (linkMidBeadMaterial != null) Destroy(linkMidBeadMaterial);
            if (cutPlaneFillMaterial != null) Destroy(cutPlaneFillMaterial);
            if (cutPlaneFrameMaterial != null) Destroy(cutPlaneFrameMaterial);
            if (targetFloorRingMaterial != null) Destroy(targetFloorRingMaterial);
        }
    }
}
