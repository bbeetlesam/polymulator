using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    public enum PartitionAxisMode
    {
        Auto = 0,
        LeftRight = 1,
        FrontBack = 2,
        Horizontal = 3
    }

    public enum FusionAttachmentSide
    {
        Right = 0,
        Front = 1,
        Left = 2,
        Back = 3,
        Top = 4
    }

    /// <summary>
    /// Executes POLYMULATOR Partition [1] and interactive Compound Snap Fusion [2].
    /// Fusion requires lifting/holding an object and bringing it close to a partner, then
    /// pressing [2] to enter the interactive Fusing Stage where the player selects which
    /// face (Kanan / Depan / Kiri / Belakang / Atas) to weld the object onto, preserving
    /// both distinct 3D shapes stuck together as a unique compound rigidbody.
    /// </summary>
    public sealed class PartitionFusionController : MonoBehaviour
    {
        private const int LinkArcSegments = 18;
        private const int MaxCompoundSubParts = 4;
        private const float PartitionChargeSeconds = 0.09f;
        private const float PartitionSlideSeconds = 0.12f;
        private const float FusionAnticipationSeconds = 0.085f;
        private const float FusionSlamSeconds = 0.095f;
        private const float PartitionHitStopSeconds = 0.045f;
        private const float FusionHitStopSeconds = 0.055f;

        private static readonly FusionAttachmentSide[] AllFusionSides =
        {
            FusionAttachmentSide.Right,
            FusionAttachmentSide.Front,
            FusionAttachmentSide.Left,
            FusionAttachmentSide.Back,
            FusionAttachmentSide.Top
        };

        [Header("Partition & Fusion (grid units)")]
        [Min(0.8f)] public float fusionPairRange = 2.8f;
        [Min(0.5f)] public float minimumSplitMass = 1.15f;
        [Min(0.3f)] public float minimumHalfExtent = 0.40f;
        [Range(1, 3)] public int maxPartitionDepth = 2;
        [SerializeField] private PartitionAxisMode cutAxisMode = PartitionAxisMode.Auto;

        [Header("References")]
        public SimulationModeController modeController;
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;
        public RectController rectController;
        [Tooltip("Optional override catalog; defaults automatically to Resources/FusionPresetCatalog or built-in presets.")]
        public FusionPresetCatalog presetCatalogOverride;

        private readonly Collider[] pairBuffer = new Collider[48];
        private readonly Collider[] overlapCheckBuffer = new Collider[32];
        private readonly HashSet<InteractableObject> transitioningObjects = new HashSet<InteractableObject>();
        private int activeTransitionCount;

        // Proximity / staging magnetic arc visual
        private LineRenderer linkLine;
        private Material linkMaterial;
        private GameObject linkMidBead;
        private Material linkMidBeadMaterial;

        // Partition cut-plane preview
        private GameObject cutPlaneRoot;
        private MeshRenderer cutPlaneFillRenderer;
        private Material cutPlaneFillMaterial;
        private LineRenderer cutPlaneFrame;
        private Material cutPlaneFrameMaterial;

        // Interactive Fusing Stage visuals (weld pad + 5 selectable face markers)
        private GameObject fusionWeldPadRoot;
        private MeshRenderer fusionWeldPadFill;
        private Material fusionWeldPadFillMat;
        private LineRenderer fusionWeldPadFrame;
        private Material fusionWeldPadFrameMat;
        private readonly GameObject[] faceMarkers = new GameObject[5];
        private readonly MeshRenderer[] faceMarkerRenderers = new MeshRenderer[5];
        private Material faceMarkerIdleMat;
        private Material faceMarkerActiveMat;
        private Material faceMarkerBlockedMat;
        private readonly Vector3[] cachedFaceMarkerPositions = new Vector3[5];
        private readonly bool[] cachedFaceValid = new bool[5];

        // Live preview state
        private InteractableObject cachedSource;
        private InteractableObject cachedPartner;
        private InteractableObject cachedCutTarget;
        private Vector3 cachedCutAxis = Vector3.right;
        private string cachedCutRuleLabel = "Auto (Ikut Hadap)";
        private bool cachedHasValidCut;
        private float cachedHalfMass;

        // Interactive Fusing Stage state
        private bool fusionStaging;
        private InteractableObject stagingSource;
        private InteractableObject stagingPartner;
        private bool stagingWasCarried;
        private bool stagingWasTethered;
        private Vector3 stagingPartnerOriginalPos;
        private Quaternion stagingPartnerOriginalRot;
        private bool stagingPartnerWasKinematic;
        private Quaternion stagingPartnerYaw = Quaternion.identity;
        private Quaternion stagingSourceSnapRot = Quaternion.identity;
        private FusionAttachmentSide selectedFusionSide = FusionAttachmentSide.Right;
        private bool selectedSideValid;
        private FusionShapeKind stagingSourceShape = FusionShapeKind.Box;
        private FusionShapeKind stagingPartnerShape = FusionShapeKind.Box;
        private FusionPresetDefinition activeStagingPreset;
        private Vector3 currentPreviewPos;
        private Quaternion currentPreviewRot = Quaternion.identity;
        private int stagingEnteredFrame = -1;
        private int lastStagingActionFrame = -1;

        // Hit-stop state (safe against overlapping stops)
        private int hitStopId;
        private Coroutine hitStopCoroutine;
        private bool hitStopActive;
        private float preHitStopTimeScale = 1f;

        private FusionPresetCatalog Catalog => presetCatalogOverride != null ? presetCatalogOverride : FusionPresetCatalog.Active;

        public InteractableObject PreviewSource => fusionStaging ? stagingSource : cachedSource;
        public InteractableObject PreviewPartner => fusionStaging ? stagingPartner : cachedPartner;
        public bool HasFusionPair => fusionStaging || (cachedSource != null && cachedPartner != null);
        public bool IsTransitioning => activeTransitionCount > 0;
        public bool IsFusionStaging => fusionStaging;
        public bool ConsumedCancelOrConfirmThisFrame => lastStagingActionFrame == Time.frameCount;
        public FusionAttachmentSide SelectedFusionSide => selectedFusionSide;
        public bool IsSelectedSideValid => selectedSideValid;
        public string SelectedFusionSideLabel => GetFusionSideLabel(selectedFusionSide);
        public string SelectedPresetDisplayName
        {
            get
            {
                if (!fusionStaging) return string.Empty;
                if (stagingSource != null && stagingPartner != null &&
                    IsCoreObject(stagingSource) && IsCoreObject(stagingPartner))
                    return "CORE-01";
                if (activeStagingPreset == null) return "FUSED";
                return activeStagingPreset.GetDisplayName(selectedFusionSide == FusionAttachmentSide.Top);
            }
        }
        public string SelectedPairLabel => fusionStaging
            ? FusionPresetCatalog.GetShapeLabelIndonesian(stagingPartnerShape) + " + " +
              FusionPresetCatalog.GetShapeLabelIndonesian(stagingSourceShape)
            : string.Empty;
        public Color SelectedPresetAccent => activeStagingPreset != null ? activeStagingPreset.accentTint : PaletteSettings.Green;
        public InteractableObject StagingSource => stagingSource;
        public InteractableObject StagingPartner => stagingPartner;

        public PartitionAxisMode CutAxisMode => cutAxisMode;
        public InteractableObject PreviewCutTarget => (!fusionStaging && cachedHasValidCut) ? cachedCutTarget : null;
        public Vector3 PreviewCutAxis => cachedCutAxis;
        public bool HasCutPreview => !fusionStaging && cachedHasValidCut && cachedCutTarget != null;
        public string CutAxisShortLabel => GetAxisShortLabel(cutAxisMode);
        public string CutAxisRuleLabel => cachedHasValidCut ? cachedCutRuleLabel : GetAxisRuleFallback(cutAxisMode);
        public float PreviewSplitHalfMass => cachedHalfMass;

        public event Action<InteractableObject, InteractableObject> OnPartitionPerformed;
        public event Action<InteractableObject> OnFusionPerformed;
        public event Action<PartitionAxisMode> OnCutAxisModeChanged;
        public event Action<bool> OnFusionStagingChanged;
        public event Action<FusionAttachmentSide, bool> OnFusionSideChanged;

        private void Awake()
        {
            if (modeController == null) modeController = GetComponent<SimulationModeController>();
            if (interactor == null) interactor = GetComponent<ObjectInteractor>();
            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = GetComponent<TelekinesisController>();
            if (rectController == null) rectController = GetComponent<RectController>();
            EnsureLinkVisual();
            EnsureCutPlaneVisual();
            EnsureFusionStageVisuals();
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

        private void EnsureFusionStageVisuals()
        {
            Shader unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
            if (unlitShader == null) unlitShader = Shader.Find("Sprites/Default");

            fusionWeldPadRoot = new GameObject("FusionWeldPadPreview");
            fusionWeldPadRoot.transform.SetParent(transform, false);

            GameObject fillGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            fillGo.name = "WeldPadFill";
            Collider col = fillGo.GetComponent<Collider>();
            if (col != null) DestroyImmediate(col);
            fillGo.transform.SetParent(fusionWeldPadRoot.transform, false);

            fusionWeldPadFill = fillGo.GetComponent<MeshRenderer>();
            fusionWeldPadFill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fusionWeldPadFill.receiveShadows = false;
            fusionWeldPadFillMat = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Green * 1.15f, 0.30f);
            fusionWeldPadFill.sharedMaterial = fusionWeldPadFillMat;

            GameObject frameGo = new GameObject("WeldPadFrame");
            frameGo.transform.SetParent(fusionWeldPadRoot.transform, false);
            fusionWeldPadFrame = frameGo.AddComponent<LineRenderer>();
            fusionWeldPadFrame.useWorldSpace = false;
            fusionWeldPadFrame.loop = true;
            fusionWeldPadFrame.positionCount = 4;
            fusionWeldPadFrame.SetPosition(0, new Vector3(-0.5f, -0.5f, 0f));
            fusionWeldPadFrame.SetPosition(1, new Vector3(0.5f, -0.5f, 0f));
            fusionWeldPadFrame.SetPosition(2, new Vector3(0.5f, 0.5f, 0f));
            fusionWeldPadFrame.SetPosition(3, new Vector3(-0.5f, 0.5f, 0f));
            fusionWeldPadFrame.startWidth = GridSettings.ToWorld(0.036f);
            fusionWeldPadFrame.endWidth = GridSettings.ToWorld(0.036f);
            fusionWeldPadFrame.numCapVertices = 3;
            fusionWeldPadFrame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fusionWeldPadFrame.receiveShadows = false;
            fusionWeldPadFrameMat = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Green * 1.25f, 0.96f);
            fusionWeldPadFrame.sharedMaterial = fusionWeldPadFrameMat;
            fusionWeldPadRoot.SetActive(false);

            faceMarkerIdleMat = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Sky, 0.62f);
            faceMarkerActiveMat = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Green * 1.25f, 0.95f);
            faceMarkerBlockedMat = CreateTransparentUnlitMaterial(unlitShader, PaletteSettings.Red, 0.55f);

            Mesh crystalMesh = FindLoadedMesh("FacetedCrystalGem", PrimitiveType.Cube);
            for (int i = 0; i < 5; i++)
            {
                GameObject mGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mGo.name = "FusionFaceMarker_" + AllFusionSides[i];
                Collider mCol = mGo.GetComponent<Collider>();
                if (mCol != null) DestroyImmediate(mCol);
                mGo.transform.SetParent(transform, false);
                if (crystalMesh != null) mGo.GetComponent<MeshFilter>().sharedMesh = crystalMesh;

                MeshRenderer mr = mGo.GetComponent<MeshRenderer>();
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.sharedMaterial = faceMarkerIdleMat;
                mGo.SetActive(false);

                faceMarkers[i] = mGo;
                faceMarkerRenderers[i] = mr;
            }
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
            if (fusionStaging)
            {
                CycleFusionSide(1);
                return cutAxisMode;
            }

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

        private void Update()
        {
            if (!fusionStaging || Time.timeScale <= 0f) return;

            if (stagingSource == null || stagingPartner == null)
            {
                CancelFusionStage(silent: true);
                return;
            }

            // Skip input processing on the exact frame the player pressed [2] to enter staging
            if (Time.frameCount == stagingEnteredFrame) return;

            HandleFusionStagingInput();
        }

        private void LateUpdate()
        {
            if (IsTransitioning)
                return;

            if (fusionStaging)
            {
                UpdateFusionStageVisuals();
                return;
            }

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
                return;
            }

            InteractableObject active = ResolveActiveTarget();
            UpdateCutPlanePreview(active);

            // Fusion proximity preview activates only while holding or tethering an object.
            InteractableObject held = ResolveHeldObject();
            UpdateFusionLinkPreview(held);
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

        private void UpdateFusionLinkPreview(InteractableObject heldSrc)
        {
            bool fusionAllowed = modeController == null || modeController.CanUseFusion;
            if (!fusionAllowed || heldSrc == null || !heldSrc.CanBePickedUp || !heldSrc.canFuse || transitioningObjects.Contains(heldSrc))
            {
                HideFusionPreviewVisuals();
                return;
            }

            if (!HasCompoundCapacity(heldSrc, null))
            {
                HideFusionPreviewVisuals();
                return;
            }

            InteractableObject partner = FindFusionPartner(heldSrc);
            if (partner == null)
            {
                HideFusionPreviewVisuals();
                return;
            }

            cachedSource = heldSrc;
            cachedPartner = partner;
            DrawMagneticArc(heldSrc.transform.position, partner.transform.position, 0.26f);
        }

        private void DrawMagneticArc(Vector3 p0, Vector3 p2, float liftRatio)
        {
            if (linkLine == null) return;
            linkLine.enabled = true;
            float dist = Vector3.Distance(p0, p2);
            float lift = Mathf.Clamp(dist * liftRatio, GridSettings.ToWorld(0.18f), GridSettings.ToWorld(0.85f))
                         + 0.04f * Mathf.Sin(Time.unscaledTime * 5.0f);
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

        private void HideFusionPreviewVisuals()
        {
            if (linkLine != null && linkLine.enabled) linkLine.enabled = false;
            if (linkMidBead != null && linkMidBead.activeSelf) linkMidBead.SetActive(false);
        }

        private void HideFusionStageVisuals()
        {
            if (fusionWeldPadRoot != null && fusionWeldPadRoot.activeSelf)
                fusionWeldPadRoot.SetActive(false);
            for (int i = 0; i < faceMarkers.Length; i++)
            {
                if (faceMarkers[i] != null && faceMarkers[i].activeSelf)
                    faceMarkers[i].SetActive(false);
            }
        }

        private bool CanPartitionTarget(InteractableObject target, out float sourceMass)
        {
            sourceMass = 0f;
            if (target == null || !target.CanBePickedUp || !target.canPartition || transitioningObjects.Contains(target))
                return false;

            Rigidbody rb = target.Body;
            sourceMass = rb != null ? rb.mass : 1f;

            // Compound fused shapes can always be un-welded cleanly along their seam
            if (target.isFusedComposite && target.GetComponentsInChildren<FusedSubPart>(true).Length >= 2)
                return true;

            Vector3 worldSize = GetTrueWorldSize(target);
            float maxSpan = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));
            return target.partitionDepth < maxPartitionDepth
                   && sourceMass >= minimumSplitMass
                   && GridSettings.ToGrid(maxSpan) >= minimumHalfExtent * 1.5f;
        }

        public bool TryPartition()
        {
            if (IsTransitioning || fusionStaging) return false;

            InteractableObject target = ResolveActiveTarget();
            if (target == null)
            {
                modeController?.NotifyBanner("Dekati atau pegang sebuah objek untuk dibelah [1]", PaletteSettings.Peach);
                return false;
            }

            if (!CanPartitionTarget(target, out float sourceMass))
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.3f, 0.35f);
                modeController?.NotifyBanner(target.displayName + " tidak dapat dibelah lagi", PaletteSettings.Peach);
                return false;
            }

            FusedSubPart[] subParts = target.isFusedComposite ? target.GetComponentsInChildren<FusedSubPart>(true) : null;
            if (subParts != null && subParts.Length >= 2)
            {
                StartCoroutine(UnfuseCompoundRoutine(target, subParts));
                return true;
            }

            StartCoroutine(PartitionRoutine(target, sourceMass));
            return true;
        }

        private IEnumerator UnfuseCompoundRoutine(InteractableObject compound, FusedSubPart[] subParts)
        {
            activeTransitionCount++;
            transitioningObjects.Add(compound);

            Vector3 originPos = ReleaseFromHoldersAndPlates(compound, out bool wasHeld);
            if (wasHeld)
            {
                originPos = transform.position + transform.forward * GridSettings.ToWorld(1.2f) + Vector3.up * GridSettings.ToWorld(0.55f);
                compound.transform.position = originPos;
            }

            compound.canBePickedUp = false;
            compound.SetCollidersEnabled(false);
            if (compound.Body != null) compound.Body.isKinematic = true;
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);

            // Split the latest welded sub-part cleanly from the rest of the compound
            Array.Sort(subParts, (a, b) => a.weldOrder.CompareTo(b.weldOrder));
            FusedSubPart detachedPart = subParts[subParts.Length - 1];
            Vector3 splitDir = (detachedPart.transform.position - compound.transform.position);
            if (splitDir.sqrMagnitude < 0.0001f) splitDir = transform.right;
            splitDir.Normalize();

            RectVisualRig.NotifyPartitionChop(transform, splitDir);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 12, 1.9f);

            float elapsed = 0f;
            while (elapsed < PartitionChargeSeconds && compound != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / PartitionChargeSeconds);
                PositionCutPlaneAt(compound, splitDir, 1f + 0.2f * u, 0.28f + 0.34f * u);
                yield return null;
            }

            if (compound == null)
            {
                activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);
                yield break;
            }

            Vector3 detachedWorldPos = detachedPart.transform.position;
            Quaternion detachedWorldRot = detachedPart.transform.rotation;
            InteractableObject spawnedDetached = ReconstructStandaloneFromSubPart(detachedPart, detachedWorldPos, detachedWorldRot);

            InteractableObject spawnedRemainder;
            if (subParts.Length == 2)
            {
                FusedSubPart otherPart = subParts[0];
                spawnedRemainder = ReconstructStandaloneFromSubPart(otherPart, otherPart.transform.position, otherPart.transform.rotation);
            }
            else
            {
                var remainingList = new List<FusedSubPart>(subParts.Length - 1);
                for (int i = 0; i < subParts.Length - 1; i++) remainingList.Add(subParts[i]);
                spawnedRemainder = ReconstructCompoundFromSubParts(remainingList, compound.transform.rotation);
            }

            transitioningObjects.Remove(compound);
            Destroy(compound.gameObject);
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);

            // Gentle separation pop along seam direction
            Vector3 push = splitDir.y > 0.65f ? Vector3.up * 0.7f : new Vector3(splitDir.x, 0.18f, splitDir.z).normalized * 0.9f;
            if (spawnedDetached != null && spawnedDetached.Body != null)
            {
                spawnedDetached.transform.position = WorldBounds.ClampInside(
                    spawnedDetached.transform.position + splitDir * GridSettings.ToWorld(0.18f), 0.45f, 0.45f);
                spawnedDetached.Body.position = spawnedDetached.transform.position;
                spawnedDetached.Body.linearVelocity = push;
                spawnedDetached.SetSafeOrigin(spawnedDetached.transform.position, spawnedDetached.transform.rotation);
                spawnedDetached.TriggerPulse(0.15f, 0.22f);
            }
            if (spawnedRemainder != null && spawnedRemainder.Body != null)
            {
                spawnedRemainder.transform.position = WorldBounds.ClampInside(
                    spawnedRemainder.transform.position - splitDir * GridSettings.ToWorld(0.12f), 0.45f, 0.45f);
                spawnedRemainder.Body.position = spawnedRemainder.transform.position;
                spawnedRemainder.Body.linearVelocity = -push * 0.55f;
                spawnedRemainder.SetSafeOrigin(spawnedRemainder.transform.position, spawnedRemainder.transform.rotation);
                spawnedRemainder.TriggerPulse(0.12f, 0.20f);
            }

            SimulationAudio.PlayPartition(originPos);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 24, 3.4f);
            SimulationVFX.Ring(originPos, PaletteSettings.Peach, 2.2f, 0.45f);
            SimulationVisualBridge.TriggerFloorRipple(originPos, PaletteSettings.Peach, 5.2f, 1.0f, 0.62f);
            RectCameraController.AddImpulse(0.08f);
            TriggerHitStop(PartitionHitStopSeconds);

            activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);
            modeController?.NotifyBanner(
                $"Partition Sambungan: {spawnedRemainder?.displayName} & {spawnedDetached?.displayName} dipisah",
                PaletteSettings.Peach);
            OnPartitionPerformed?.Invoke(spawnedRemainder, spawnedDetached);
        }

        private IEnumerator PartitionRoutine(InteractableObject target, float sourceMass)
        {
            activeTransitionCount++;
            transitioningObjects.Add(target);

            Vector3 trueWorldSize = GetTrueWorldSize(target);
            Vector3 splitAxis = ComputeWorldSplitAxis(target, out string axisDesc);
            Vector3 originPos = ReleaseFromHoldersAndPlates(target, out bool wasHeld);
            if (wasHeld)
            {
                originPos = transform.position + transform.forward * GridSettings.ToWorld(1.2f) + Vector3.up * GridSettings.ToWorld(0.55f);
                target.transform.position = originPos;
            }

            target.canBePickedUp = false;
            target.SetCollidersEnabled(false);
            if (target.Body != null) target.Body.isKinematic = true;

            RectVisualRig.NotifyCarryAction(transform, true);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 10, 1.8f);

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

            SimulationAudio.PlayPartition(originPos);
            SimulationVFX.Burst(originPos, PaletteSettings.Peach, 24, 3.4f);
            SimulationVFX.Ring(originPos, PaletteSettings.Peach, 2.2f, 0.45f);
            SimulationVisualBridge.TriggerFloorRipple(originPos, PaletteSettings.Peach, 5.2f, 1.0f, 0.62f);
            RectVisualRig.NotifyPartitionChop(transform, splitAxis);
            RectCameraController.AddImpulse(0.085f);
            TriggerHitStop(PartitionHitStopSeconds);

            while (hitStopActive) yield return null;

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

        /// <summary>
        /// Entry point for [2]. Requires lifting/holding an object near a fusable partner;
        /// first press enters the interactive Fusing Stage (choose side), second press confirms weld.
        /// </summary>
        public bool TryFusion()
        {
            if (IsTransitioning) return false;

            if (fusionStaging)
                return ConfirmFusionStage();

            InteractableObject held = ResolveHeldObject();
            if (held == null)
            {
                modeController?.NotifyBanner("Angkat objek dulu [E] lalu dekatkan ke objek lain untuk Fusion [2]", PaletteSettings.Yellow);
                return false;
            }

            if (!held.CanBePickedUp || !held.canFuse)
            {
                SimulationVFX.Ring(held.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                modeController?.NotifyBanner(held.displayName + " tidak dapat digabungkan", PaletteSettings.Peach);
                return false;
            }

            InteractableObject partner = FindFusionPartner(held);
            if (partner == null)
            {
                string hint = IsCoreObject(held)
                    ? held.displayName + " hanya bisa disatukan dengan pecahan CORE-01 lain di dekatnya"
                    : "Dekatkan " + held.displayName + " yang sedang diangkat ke objek lain untuk mulai Fusing [2]";
                SimulationVFX.Ring(held.transform.position, PaletteSettings.Yellow, 1.7f, 0.38f);
                modeController?.NotifyBanner(hint, PaletteSettings.Yellow);
                return false;
            }

            if (!HasCompoundCapacity(held, partner))
            {
                SimulationVFX.Ring(held.transform.position, PaletteSettings.Peach, 1.6f, 0.38f);
                modeController?.NotifyBanner("Batas maksimum gabungan (" + MaxCompoundSubParts + " bagian) tercapai", PaletteSettings.Peach);
                return false;
            }

            EnterFusionStage(held, partner);
            return true;
        }

        private void EnterFusionStage(InteractableObject src, InteractableObject partner)
        {
            stagingSource = src;
            stagingPartner = partner;
            stagingWasCarried = carryController != null && carryController.CarriedObject == src;
            stagingWasTethered = telekinesisController != null && telekinesisController.TetheredObject == src;

            Vector3 srcStartPos = src.transform.position;
            Quaternion srcStartRot = src.transform.rotation;

            // Release cleanly from carry/telekinesis while holding physics suspended for staging
            if (stagingWasCarried && carryController != null)
            {
                carryController.ReleaseAndReturn();
            }
            if (stagingWasTethered && telekinesisController != null)
            {
                telekinesisController.Release(false);
            }

            if (rectController != null) rectController.SetMovementLocked(true);
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);

            // Suspend physics on both objects during interactive side-selection stage
            stagingPartnerOriginalPos = partner.transform.position;
            stagingPartnerOriginalRot = partner.transform.rotation;
            stagingPartnerWasKinematic = partner.Body != null && partner.Body.isKinematic;
            stagingPartnerYaw = ExtractFlatYaw(partner.transform.rotation);

            stagingSourceShape = FusionPresetCatalog.ClassifyShape(src);
            stagingPartnerShape = FusionPresetCatalog.ClassifyShape(partner);
            activeStagingPreset = Catalog.ResolvePreset(stagingSourceShape, stagingPartnerShape);

            float relYaw = Mathf.DeltaAngle(stagingPartnerYaw.eulerAngles.y, srcStartRot.eulerAngles.y);
            float snappedRelYaw = Mathf.Round(relYaw / 90f) * 90f;
            stagingSourceSnapRot = stagingPartnerYaw * Quaternion.Euler(0f, snappedRelYaw, 0f);

            src.transform.SetPositionAndRotation(srcStartPos, srcStartRot);
            src.SetCollidersEnabled(false);
            src.SetHighlight(false, false);
            if (src.Body != null)
            {
                src.Body.isKinematic = true;
                src.Body.detectCollisions = false;
                src.Body.linearVelocity = Vector3.zero;
                src.Body.angularVelocity = Vector3.zero;
            }

            partner.SetHighlight(false, false);
            if (partner.Body != null)
            {
                partner.Body.isKinematic = true;
                partner.Body.linearVelocity = Vector3.zero;
                partner.Body.angularVelocity = Vector3.zero;
            }

            currentPreviewPos = srcStartPos;
            currentPreviewRot = srcStartRot;

            fusionStaging = true;
            stagingEnteredFrame = Time.frameCount;

            EvaluateAllFusionSides();
            selectedFusionSide = PickBestInitialSide(srcStartPos);
            selectedSideValid = cachedFaceValid[(int)selectedFusionSide];

            SimulationAudio.PlayTelekinesisLock(partner.transform.position);
            SimulationVFX.Ring(partner.transform.position, PaletteSettings.Teal, 1.8f, 0.34f);

            OnFusionStagingChanged?.Invoke(true);
            OnFusionSideChanged?.Invoke(selectedFusionSide, selectedSideValid);
        }

        public void CycleFusionSide(int delta = 1)
        {
            if (!fusionStaging) return;
            int next = ((int)selectedFusionSide + delta + AllFusionSides.Length) % AllFusionSides.Length;
            SelectFusionSide(AllFusionSides[next]);
        }

        public void SelectFusionSide(FusionAttachmentSide side)
        {
            if (!fusionStaging) return;
            EvaluateAllFusionSides();
            bool changed = selectedFusionSide != side;
            selectedFusionSide = side;
            selectedSideValid = cachedFaceValid[(int)side];
            if (changed)
            {
                SimulationAudio.PlayMenuHover();
                RectVisualRig.NotifyRotateSnap(transform, 0.4f);
            }
            OnFusionSideChanged?.Invoke(selectedFusionSide, selectedSideValid);
        }

        public bool ConfirmFusionStage()
        {
            if (!fusionStaging || stagingSource == null || stagingPartner == null) return false;
            EvaluateAllFusionSides();
            selectedSideValid = cachedFaceValid[(int)selectedFusionSide];
            if (!selectedSideValid)
            {
                SimulationAudio.PlayAnchorReject(stagingPartner.transform.position);
                modeController?.NotifyBanner("Sisi " + GetFusionSideLabel(selectedFusionSide) + " terhalang. Pilih sisi lain [WASD / Q / 3]", PaletteSettings.Red);
                return false;
            }

            InteractableObject src = stagingSource;
            InteractableObject partner = stagingPartner;
            FusionAttachmentSide chosenSide = selectedFusionSide;
            Quaternion partnerYaw = stagingPartnerYaw;
            Quaternion srcSnapRot = stagingSourceSnapRot;
            FusionShapeKind srcShape = stagingSourceShape;
            FusionShapeKind partnerShape = stagingPartnerShape;
            FusionPresetDefinition chosenPreset = activeStagingPreset;

            fusionStaging = false;
            lastStagingActionFrame = Time.frameCount;
            stagingSource = null;
            stagingPartner = null;
            activeStagingPreset = null;
            HideFusionStageVisuals();
            OnFusionStagingChanged?.Invoke(false);

            StartCoroutine(CompoundFusionRoutine(src, partner, chosenSide, partnerYaw, srcSnapRot, srcShape, partnerShape, chosenPreset));
            return true;
        }

        public void CancelFusionStage(bool silent = false)
        {
            if (!fusionStaging) return;

            InteractableObject src = stagingSource;
            InteractableObject partner = stagingPartner;
            bool returnToCarry = stagingWasCarried;
            bool returnToTether = stagingWasTethered;

            fusionStaging = false;
            lastStagingActionFrame = Time.frameCount;
            stagingSource = null;
            stagingPartner = null;
            activeStagingPreset = null;
            HideFusionStageVisuals();
            HideFusionPreviewVisuals();

            if (rectController != null) rectController.SetMovementLocked(false);

            if (partner != null)
            {
                partner.transform.SetPositionAndRotation(stagingPartnerOriginalPos, stagingPartnerOriginalRot);
                if (partner.Body != null)
                {
                    partner.Body.isKinematic = stagingPartnerWasKinematic;
                    partner.Body.WakeUp();
                }
            }

            if (src != null)
            {
                src.SetCollidersEnabled(true);
                if (src.Body != null)
                {
                    src.Body.isKinematic = false;
                    src.Body.detectCollisions = true;
                    src.Body.linearVelocity = Vector3.zero;
                    src.Body.angularVelocity = Vector3.zero;
                    src.Body.WakeUp();
                }

                if (returnToCarry && carryController != null && modeController != null && modeController.IsLimited)
                {
                    carryController.TryPickup(src);
                }
                else if (returnToTether && telekinesisController != null && modeController != null && modeController.IsFlexible)
                {
                    telekinesisController.TryLockTarget(src);
                }
                else
                {
                    Vector3 safeDrop = WorldBounds.ClampInside(
                        transform.position + transform.forward * GridSettings.ToWorld(1.25f) + Vector3.up * GridSettings.ToWorld(0.6f),
                        0.5f, 0.5f);
                    src.transform.position = safeDrop;
                    if (src.Body != null) src.Body.position = safeDrop;
                }
            }

            if (!silent)
            {
                SimulationAudio.PlayMenuClick();
                modeController?.NotifyBanner("Tahap Fusing dibatalkan", PaletteSettings.Subtext0);
            }

            OnFusionStagingChanged?.Invoke(false);
        }

        private void HandleFusionStagingInput()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ||
                (mouse != null && mouse.rightButton.wasPressedThisFrame))
            {
                CancelFusionStage();
                return;
            }

            // [2] is routed via SimulationModeController.
            if (keyboard != null && (keyboard.enterKey.wasPressedThisFrame ||
                                     keyboard.numpadEnterKey.wasPressedThisFrame ||
                                     keyboard.spaceKey.wasPressedThisFrame ||
                                     keyboard.eKey.wasPressedThisFrame))
            {
                ConfirmFusionStage();
                return;
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                CycleFusionSide(1);
                return;
            }
            if (mouse != null)
            {
                float scrollY = mouse.scroll.ReadValue().y;
                if (Mathf.Abs(scrollY) > 0.01f)
                {
                    CycleFusionSide(scrollY > 0f ? 1 : -1);
                    return;
                }
            }

            if (keyboard != null && keyboard.qKey.wasPressedThisFrame)
            {
                SelectFusionSide(FusionAttachmentSide.Top);
                return;
            }

            if (keyboard != null)
            {
                Vector2 dirInput = Vector2.zero;
                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) dirInput.y += 1f;
                if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) dirInput.y -= 1f;
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame) dirInput.x += 1f;
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame) dirInput.x -= 1f;

                if (dirInput.sqrMagnitude > 0.1f)
                {
                    FusionAttachmentSide mapped = MapCameraDirectionToHorizontalSide(dirInput);
                    SelectFusionSide(mapped);
                    return;
                }
            }

            // Clicking the already-selected valid marker confirms the weld.
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                !SimulationHUDController.IsPointerOverInteractiveUI && Camera.main != null)
            {
                Vector2 clickPos = mouse.position.ReadValue();
                int bestIdx = -1;
                float bestPixelDist = 72f;
                for (int i = 0; i < 5; i++)
                {
                    Vector3 sp = Camera.main.WorldToScreenPoint(cachedFaceMarkerPositions[i]);
                    if (sp.z < 0f) continue;
                    float pxDist = Vector2.Distance(clickPos, new Vector2(sp.x, sp.y));
                    if (pxDist < bestPixelDist)
                    {
                        bestPixelDist = pxDist;
                        bestIdx = i;
                    }
                }

                if (bestIdx >= 0)
                {
                    FusionAttachmentSide clickedSide = AllFusionSides[bestIdx];
                    if (clickedSide == selectedFusionSide && selectedSideValid)
                        ConfirmFusionStage();
                    else
                        SelectFusionSide(clickedSide);
                }
            }
        }

        private FusionAttachmentSide MapCameraDirectionToHorizontalSide(Vector2 input)
        {
            Camera cam = Camera.main;
            Vector3 camFwd = cam != null ? Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up) : Vector3.forward;
            if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward;
            camFwd.Normalize();
            Vector3 camRight = Vector3.Cross(Vector3.up, camFwd);

            Vector3 worldDir = (camFwd * input.y + camRight * input.x).normalized;
            Vector3 localDir = Quaternion.Inverse(stagingPartnerYaw) * worldDir;

            if (Mathf.Abs(localDir.x) >= Mathf.Abs(localDir.z))
                return localDir.x >= 0f ? FusionAttachmentSide.Right : FusionAttachmentSide.Left;
            return localDir.z >= 0f ? FusionAttachmentSide.Front : FusionAttachmentSide.Back;
        }

        private void EvaluateAllFusionSides()
        {
            if (stagingSource == null || stagingPartner == null) return;
            Vector3 sizeB = GetTrueWorldSize(stagingPartner);
            for (int i = 0; i < 5; i++)
            {
                FusionAttachmentSide side = AllFusionSides[i];
                ComputeSnapPose(stagingSource, stagingPartner, side, stagingPartnerYaw, stagingSourceSnapRot,
                    stagingSourceShape, stagingPartnerShape, activeStagingPreset,
                    out Vector3 flushPos, out Quaternion effectiveSrcRot, out _, out Vector3 worldNormal);

                float markerOffset = ProjectHalfExtent(sizeB, stagingPartnerYaw, worldNormal) + GridSettings.ToWorld(0.24f);
                cachedFaceMarkerPositions[i] = stagingPartner.transform.position + worldNormal * markerOffset;
                cachedFaceValid[i] = IsAttachmentPoseClear(stagingSource, stagingPartner, flushPos, effectiveSrcRot);
            }
        }

        private FusionAttachmentSide PickBestInitialSide(Vector3 approachWorldPos)
        {
            Vector3 delta = approachWorldPos - stagingPartner.transform.position;
            Vector3 localDelta = Quaternion.Inverse(stagingPartnerYaw) * delta;

            FusionAttachmentSide preferred;
            if (localDelta.y > GridSettings.ToWorld(0.85f) &&
                new Vector2(localDelta.x, localDelta.z).magnitude < GridSettings.ToWorld(0.65f))
            {
                preferred = FusionAttachmentSide.Top;
            }
            else if (Mathf.Abs(localDelta.x) >= Mathf.Abs(localDelta.z))
            {
                preferred = localDelta.x >= 0f ? FusionAttachmentSide.Right : FusionAttachmentSide.Left;
            }
            else
            {
                preferred = localDelta.z >= 0f ? FusionAttachmentSide.Front : FusionAttachmentSide.Back;
            }

            if (cachedFaceValid[(int)preferred]) return preferred;
            for (int i = 0; i < AllFusionSides.Length; i++)
            {
                if (cachedFaceValid[i]) return AllFusionSides[i];
            }
            return preferred;
        }

        private void UpdateFusionStageVisuals()
        {
            if (stagingSource == null || stagingPartner == null) return;

            EvaluateAllFusionSides();
            selectedSideValid = cachedFaceValid[(int)selectedFusionSide];

            ComputeSnapPose(stagingSource, stagingPartner, selectedFusionSide, stagingPartnerYaw, stagingSourceSnapRot,
                stagingSourceShape, stagingPartnerShape, activeStagingPreset,
                out Vector3 flushPos, out Quaternion effectiveSrcRot, out Vector3 seamCenter, out Vector3 worldNormal);

            // Hover slightly off the face with magnetic breathing pull toward the weld pad
            float hoverGap = GridSettings.ToWorld(0.15f + 0.045f * Mathf.Sin(Time.unscaledTime * 5.8f));
            Vector3 desiredPreviewPos = flushPos + worldNormal * hoverGap;
            float lerpAlpha = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            currentPreviewPos = Vector3.Lerp(currentPreviewPos, desiredPreviewPos, lerpAlpha);
            currentPreviewRot = Quaternion.Slerp(currentPreviewRot, effectiveSrcRot, lerpAlpha);

            stagingSource.transform.SetPositionAndRotation(currentPreviewPos, currentPreviewRot);

            // Position glowing weld-face pad flush on Partner B's chosen face
            if (fusionWeldPadRoot != null)
            {
                Vector3 upHint = Mathf.Abs(worldNormal.y) > 0.9f ? (stagingPartnerYaw * Vector3.forward) : Vector3.up;
                Quaternion padRot = Quaternion.LookRotation(worldNormal, upHint);
                Vector3 sizeA = GetTrueWorldSize(stagingSource);
                Vector3 sizeB = GetTrueWorldSize(stagingPartner);
                Vector3 padRight = padRot * Vector3.right;
                Vector3 padUp = padRot * Vector3.up;

                float wA = ProjectWorldSizeAlong(sizeA, stagingSource.transform, padRight);
                float hA = ProjectWorldSizeAlong(sizeA, stagingSource.transform, padUp);
                float wB = ProjectWorldSizeAlong(sizeB, stagingPartner.transform, padRight);
                float hB = ProjectWorldSizeAlong(sizeB, stagingPartner.transform, padUp);

                float padW = Mathf.Max(GridSettings.ToWorld(0.45f), Mathf.Min(wA, wB) * 0.94f);
                float padH = Mathf.Max(GridSettings.ToWorld(0.45f), Mathf.Min(hA, hB) * 0.94f);

                fusionWeldPadRoot.transform.SetPositionAndRotation(seamCenter + worldNormal * GridSettings.ToWorld(0.015f), padRot);
                fusionWeldPadRoot.transform.localScale = new Vector3(padW, padH, 1f);

                Color presetGlow = activeStagingPreset != null ? activeStagingPreset.accentTint : PaletteSettings.Green;
                Color padTint = selectedSideValid ? presetGlow : PaletteSettings.Red;
                float pulseAlpha = 0.28f + 0.14f * Mathf.Sin(Time.unscaledTime * 6.5f);
                if (fusionWeldPadFillMat != null)
                {
                    Color fc = padTint * 1.45f;
                    fc.a = pulseAlpha;
                    if (fusionWeldPadFillMat.HasProperty("_BaseColor")) fusionWeldPadFillMat.SetColor("_BaseColor", fc);
                    if (fusionWeldPadFillMat.HasProperty("_Color")) fusionWeldPadFillMat.SetColor("_Color", fc);
                }
                if (fusionWeldPadFrameMat != null)
                {
                    Color ec = padTint * 1.85f;
                    ec.a = 0.98f;
                    if (fusionWeldPadFrameMat.HasProperty("_BaseColor")) fusionWeldPadFrameMat.SetColor("_BaseColor", ec);
                    if (fusionWeldPadFrameMat.HasProperty("_Color")) fusionWeldPadFrameMat.SetColor("_Color", ec);
                }
                if (linkMaterial != null)
                {
                    Color lc = padTint * 1.65f;
                    lc.a = 0.95f;
                    if (linkMaterial.HasProperty("_BaseColor")) linkMaterial.SetColor("_BaseColor", lc);
                    if (linkMaterial.HasProperty("_Color")) linkMaterial.SetColor("_Color", lc);
                }
                if (linkMidBeadMaterial != null)
                {
                    Color bc = Color.Lerp(padTint, PaletteSettings.Text, 0.35f) * 1.8f;
                    bc.a = 1f;
                    if (linkMidBeadMaterial.HasProperty("_BaseColor")) linkMidBeadMaterial.SetColor("_BaseColor", bc);
                    if (linkMidBeadMaterial.HasProperty("_Color")) linkMidBeadMaterial.SetColor("_Color", bc);
                }
                if (!fusionWeldPadRoot.activeSelf) fusionWeldPadRoot.SetActive(true);
            }

            // Update the 5 selectable directional face markers around Partner B
            for (int i = 0; i < 5; i++)
            {
                GameObject marker = faceMarkers[i];
                MeshRenderer mr = faceMarkerRenderers[i];
                if (marker == null || mr == null) continue;

                bool isSelected = i == (int)selectedFusionSide;
                bool isValid = cachedFaceValid[i];
                float baseScale = isSelected
                    ? GridSettings.ToWorld(0.20f + 0.03f * Mathf.Sin(Time.unscaledTime * 7f))
                    : GridSettings.ToWorld(0.115f);

                marker.transform.position = cachedFaceMarkerPositions[i];
                marker.transform.rotation = Quaternion.Euler(45f, Time.unscaledTime * (isSelected ? 130f : 55f) + i * 36f, 45f);
                marker.transform.localScale = Vector3.one * baseScale;
                mr.sharedMaterial = !isValid ? faceMarkerBlockedMat : (isSelected ? faceMarkerActiveMat : faceMarkerIdleMat);
                if (!marker.activeSelf) marker.SetActive(true);
            }

            // Magnetic arc connecting hovering Object A straight into Partner B's weld pad
            DrawMagneticArc(currentPreviewPos, seamCenter, 0.16f);
        }

        private void ComputeSnapPose(InteractableObject src, InteractableObject partner,
            FusionAttachmentSide side, Quaternion partnerYaw, Quaternion srcBaseSnapRot,
            FusionShapeKind srcShape, FusionShapeKind partnerShape, FusionPresetDefinition preset,
            out Vector3 attachedSrcPos, out Quaternion effectiveSrcRot, out Vector3 seamCenter, out Vector3 worldNormal)
        {
            Vector3 localNormal = side switch
            {
                FusionAttachmentSide.Right => Vector3.right,
                FusionAttachmentSide.Front => Vector3.forward,
                FusionAttachmentSide.Left => Vector3.left,
                FusionAttachmentSide.Back => Vector3.back,
                _ => Vector3.up
            };
            worldNormal = (partnerYaw * localNormal).normalized;
            bool isTop = side == FusionAttachmentSide.Top;

            // Align a held cylinder horizontally along the chosen side face when the preset requests it.
            effectiveSrcRot = srcBaseSnapRot;
            if (!isTop && preset != null && preset.orientHeldCylinderAlongSide && srcShape == FusionShapeKind.Cylinder)
            {
                effectiveSrcRot = Quaternion.FromToRotation(Vector3.up, worldNormal) * srcBaseSnapRot;
            }

            Vector3 sizeB = GetTrueWorldSize(partner);
            Vector3 sizeA = GetTrueWorldSize(src);
            float halfB = ProjectHalfExtent(sizeB, partnerYaw, worldNormal);
            float halfA = ProjectHalfExtent(sizeA, effectiveSrcRot, worldNormal);
            float presetGap = preset != null
                ? GridSettings.ToWorld(preset.GetGapGrid(isTop))
                : GridSettings.ToWorld(-0.02f);

            Vector3 partnerPos = partner.transform.position;
            seamCenter = partnerPos + worldNormal * (halfB + Mathf.Max(0f, presetGap) * 0.5f);
            attachedSrcPos = partnerPos + worldNormal * (halfB + halfA + presetGap);

            if (!isTop)
            {
                float partnerBottomY = partnerPos.y - sizeB.y * 0.5f;
                float srcHalfVertical = ProjectHalfExtent(sizeA, effectiveSrcRot, Vector3.up);
                float floorAlignedY = partnerBottomY + srcHalfVertical;

                // Center nozzles & orb sockets vertically on the partner's side face while never sinking below floor
                bool centerVertically = srcShape == FusionShapeKind.Sphere ||
                                        partnerShape == FusionShapeKind.Sphere ||
                                        (preset != null && preset.orientHeldCylinderAlongSide && srcShape == FusionShapeKind.Cylinder);
                attachedSrcPos.y = centerVertically ? Mathf.Max(partnerPos.y, floorAlignedY) : floorAlignedY;
                seamCenter.y = (partnerPos.y + attachedSrcPos.y) * 0.5f;
            }
        }

        private bool IsAttachmentPoseClear(InteractableObject src, InteractableObject partner,
            Vector3 targetPos, Quaternion targetRot)
        {
            Vector3 sizeA = GetTrueWorldSize(src);
            float boundX = ProjectHalfExtent(sizeA, targetRot, Vector3.right);
            float boundY = ProjectHalfExtent(sizeA, targetRot, Vector3.up);
            float boundZ = ProjectHalfExtent(sizeA, targetRot, Vector3.forward);

            Vector3 clamped = WorldBounds.ClampInside(targetPos, boundX, boundZ);
            if ((clamped - targetPos).sqrMagnitude > 0.02f) return false;
            if (targetPos.y - boundY < GridSettings.ToWorld(-0.15f)) return false;

            Vector3 halfExtents = sizeA * 0.42f;
            int hits = Physics.OverlapBoxNonAlloc(targetPos, halfExtents, overlapCheckBuffer, targetRot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits; i++)
            {
                Collider col = overlapCheckBuffer[i];
                if (col == null) continue;
                if (col.transform == transform || col.transform.IsChildOf(transform)) continue;
                if (col.transform == src.transform || col.transform.IsChildOf(src.transform)) continue;
                if (col.transform == partner.transform || col.transform.IsChildOf(partner.transform)) continue;

                if (col.bounds.max.y <= targetPos.y - boundY * 0.85f + 0.05f) continue;
                return false;
            }
            return true;
        }

        private IEnumerator CompoundFusionRoutine(InteractableObject src, InteractableObject partner,
            FusionAttachmentSide chosenSide, Quaternion partnerYaw, Quaternion srcBaseSnapRot,
            FusionShapeKind srcShape, FusionShapeKind partnerShape, FusionPresetDefinition preset)
        {
            activeTransitionCount++;
            transitioningObjects.Add(src);
            transitioningObjects.Add(partner);

            bool restoringCore = IsCoreObject(src) && IsCoreObject(partner);
            bool isTop = chosenSide == FusionAttachmentSide.Top;
            if (preset == null) preset = Catalog.ResolvePreset(srcShape, partnerShape);
            Color vfxAccent = restoringCore ? PaletteSettings.Yellow : preset.accentTint;

            UnregisterFromAllPlates(src.Body);
            UnregisterFromAllPlates(partner.Body);

            ComputeSnapPose(src, partner, chosenSide, partnerYaw, srcBaseSnapRot, srcShape, partnerShape, preset,
                out Vector3 flushPos, out Quaternion effectiveSrcRot, out Vector3 seamCenter, out Vector3 worldNormal);

            Vector3 startPos = src.transform.position;
            Quaternion startRot = src.transform.rotation;
            Vector3 pullBackPos = flushPos + worldNormal * GridSettings.ToWorld(0.28f);

            RectVisualRig.NotifyCarryAction(transform, true);
            SimulationVFX.Ring(seamCenter, vfxAccent, 1.5f, 0.24f);

            float elapsed = 0f;
            while (elapsed < FusionAnticipationSeconds && src != null && partner != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / FusionAnticipationSeconds);
                float ease = 1f - Mathf.Pow(1f - u, 2f);
                src.transform.SetPositionAndRotation(
                    Vector3.Lerp(startPos, pullBackPos, ease),
                    Quaternion.Slerp(startRot, effectiveSrcRot, ease));
                DrawMagneticArc(src.transform.position, seamCenter, 0.18f * (1f - u * 0.5f));
                yield return null;
            }

            elapsed = 0f;
            Vector3 slamStart = src != null ? src.transform.position : pullBackPos;
            while (elapsed < FusionSlamSeconds && src != null && partner != null)
            {
                elapsed += Time.deltaTime;
                float u = Mathf.Clamp01(elapsed / FusionSlamSeconds);
                float accel = u * u * u;
                src.transform.SetPositionAndRotation(
                    Vector3.Lerp(slamStart, flushPos, accel),
                    effectiveSrcRot);
                DrawMagneticArc(src.transform.position, seamCenter, 0.08f * (1f - u));
                yield return null;
            }

            if (src == null || partner == null)
            {
                HideFusionPreviewVisuals();
                if (src != null) transitioningObjects.Remove(src);
                if (partner != null) transitioningObjects.Remove(partner);
                if (rectController != null) rectController.SetMovementLocked(false);
                activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);
                yield break;
            }

            src.transform.SetPositionAndRotation(flushPos, effectiveSrcRot);
            partner.transform.rotation = partnerYaw;

            float massA = src.Body != null ? src.Body.mass : 1f;
            float massB = partner.Body != null ? partner.Body.mass : 1f;
            float totalMass = Mathf.Round((massA + massB) * 100f) * 0.01f;
            string srcLabel = src.displayName;
            string partnerLabel = partner.displayName;

            InteractableObject merged;
            if (restoringCore)
            {
                Vector3 corePos = (partner.transform.position + flushPos) * 0.5f;
                corePos = WorldBounds.ClampInside(corePos, GridSettings.ToWorld(0.5f), GridSettings.ToWorld(0.5f));
                corePos.y = Mathf.Max(corePos.y, GridSettings.ToWorld(0.48f));
                merged = BuildRestoredCore(src, corePos, partnerYaw, totalMass);
            }
            else
            {
                merged = BuildCompoundFusedObject(partner, src, partnerYaw, seamCenter, worldNormal,
                    isTop, srcShape, partnerShape, preset, totalMass);
            }

            transitioningObjects.Remove(src);
            transitioningObjects.Remove(partner);
            Destroy(src.gameObject);
            Destroy(partner.gameObject);

            HideFusionPreviewVisuals();
            if (rectController != null) rectController.SetMovementLocked(false);

            SimulationAudio.PlayFusion(seamCenter);
            SimulationVFX.Burst(seamCenter, vfxAccent, 42, 4.4f);
            SimulationVFX.Ring(seamCenter, vfxAccent, 2.8f, 0.56f);
            SimulationVFX.Ring(seamCenter + Vector3.up * GridSettings.ToWorld(0.08f), PaletteSettings.Text, 1.85f, 0.36f);
            SimulationVisualBridge.TriggerFloorRipple(seamCenter, vfxAccent, 6.8f, 1.25f, 0.68f);
            RectVisualRig.NotifyFusionPulse(transform);
            RectCameraController.AddImpulse(0.11f);
            TriggerHitStop(FusionHitStopSeconds);

            activeTransitionCount = Mathf.Max(0, activeTransitionCount - 1);

            string banner = restoringCore
                ? "CORE-01 dipulihkan utuh melalui Fusion!"
                : $"{merged.displayName} ({GetFusionSideLabel(chosenSide)}): {partnerLabel} + {srcLabel}";
            modeController?.NotifyBanner(banner, vfxAccent);
            OnFusionPerformed?.Invoke(merged);
        }

        private InteractableObject BuildRestoredCore(InteractableObject template, Vector3 pos, Quaternion rot, float totalMass)
        {
            GameObject mergedGo = Instantiate(template.gameObject, pos, rot);
            RestoreCoreVisuals(mergedGo);
            mergedGo.transform.localScale = Vector3.one * GridSettings.ToWorld(1f);
            mergedGo.name = "CORE-01";

            InteractableObject merged = mergedGo.GetComponent<InteractableObject>();
            merged.displayName = "CORE-01";
            merged.partitionDepth = 0;
            merged.isFusedComposite = false;
            merged.canBePickedUp = true;
            merged.canPartition = true;
            merged.canFuse = false;

            Rigidbody rb = merged.Body;
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.detectCollisions = true;
                rb.mass = totalMass;
                rb.linearVelocity = Vector3.up * 0.65f;
                rb.angularVelocity = Vector3.zero;
                rb.WakeUp();
            }

            merged.SetCollidersEnabled(true);
            merged.SetSafeOrigin(pos, rot);
            merged.ReinitializeClonedVisuals(PaletteSettings.Yellow);
            merged.TriggerPulse(0.22f, 0.30f);
            return merged;
        }

        /// <summary>
        /// Builds a true compound welded InteractableObject driven by the matched FusionPresetDefinition,
        /// preserving BOTH real geometries, colors, trims, and local colliders with bespoke architectural
        /// Catppuccin Mocha joinery and a balanced geometric center pivot.
        /// </summary>
        private InteractableObject BuildCompoundFusedObject(InteractableObject partner, InteractableObject attached,
            Quaternion rootYaw, Vector3 seamCenter, Vector3 seamNormal, bool isTop,
            FusionShapeKind attachedShape, FusionShapeKind partnerShape,
            FusionPresetDefinition preset, float totalMass)
        {
            string fusionLabel = preset != null
                ? preset.GetDisplayName(isTop)
                : ("FUSED-" + totalMass.ToString("0.#", CultureInfo.InvariantCulture) + "T");

            GameObject rootGo = new GameObject(fusionLabel);
            rootGo.transform.SetPositionAndRotation(partner.transform.position, rootYaw);
            rootGo.transform.localScale = Vector3.one;

            int nextOrder = 0;
            nextOrder = AppendObjectPartsToCompound(rootGo.transform, partner, nextOrder);
            nextOrder = AppendObjectPartsToCompound(rootGo.transform, attached, nextOrder);

            // Attach preset-specific sculpted HDR joinery, kinetic rigs, plasma arcs, and core glow
            Mesh chamferCube = FindLoadedMesh("ChamferCube", PrimitiveType.Cube);
            Mesh octagonPrism = FindLoadedMesh("OctagonPrism", PrimitiveType.Cylinder);
            FusionPresetVisualBuilder.BuildPresetAssembly(
                rootGo.transform,
                partner,
                attached,
                GetTrueWorldSize(partner),
                GetTrueWorldSize(attached),
                seamCenter,
                seamNormal,
                isTop,
                partnerShape,
                attachedShape,
                preset,
                chamferCube,
                octagonPrism);

            // Re-center root pivot cleanly at the combined bounds center so carry, rotation & drop stay balanced
            Bounds combined = ComputeWorldRendererBounds(rootGo.transform);
            Vector3 worldCenterDelta = combined.center - rootGo.transform.position;
            if (worldCenterDelta.sqrMagnitude > 0.00001f)
            {
                rootGo.transform.position += worldCenterDelta;
                for (int i = 0; i < rootGo.transform.childCount; i++)
                {
                    rootGo.transform.GetChild(i).position -= worldCenterDelta;
                }
            }

            // Clamp safely inside chamber bounds and above floor
            Bounds centeredBounds = ComputeWorldRendererBounds(rootGo.transform);
            Vector3 safePos = WorldBounds.ClampInside(rootGo.transform.position, centeredBounds.extents.x, centeredBounds.extents.z);
            float bottomOffset = rootGo.transform.position.y - centeredBounds.min.y;
            if (centeredBounds.min.y < 0.01f)
                safePos.y = bottomOffset + GridSettings.ToWorld(0.02f);
            rootGo.transform.position = safePos;

            Rigidbody rb = rootGo.AddComponent<Rigidbody>();
            rb.mass = totalMass;
            rb.linearDamping = 0.14f;
            rb.angularDamping = 0.08f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.isKinematic = false;
            rb.detectCollisions = true;
            rb.linearVelocity = Vector3.up * 0.45f;
            rb.angularVelocity = Vector3.zero;
            rb.WakeUp();

            Color representativeColor = preset != null
                ? preset.accentTint
                : PaletteSettings.NearestInteractiveAccent(Color.Lerp(partner.AssignedColor, attached.AssignedColor, 0.5f));

            InteractableObject merged = rootGo.AddComponent<InteractableObject>();
            merged.displayName = fusionLabel;
            merged.canBePickedUp = true;
            merged.canRotate = true;
            merged.canThrow = true;
            merged.canTelekinesis = true;
            merged.canPartition = true;
            merged.canFuse = CountSubParts(rootGo) < MaxCompoundSubParts;
            merged.partitionDepth = 0;
            merged.isFusedComposite = true;

            merged.SetCollidersEnabled(true);
            merged.SetSafeOrigin(safePos, rootGo.transform.rotation);
            merged.ReinitializeClonedVisuals(representativeColor, null);
            merged.TriggerPulse(0.18f, 0.28f);
            rootGo.AddComponent<ImpactAudioEmitter>();
            return merged;
        }

        private int AppendObjectPartsToCompound(Transform compoundRoot, InteractableObject sourceObj, int startOrder)
        {
            FusedSubPart[] existingParts = sourceObj.GetComponentsInChildren<FusedSubPart>(true);
            if (existingParts != null && existingParts.Length > 0)
            {
                Array.Sort(existingParts, (a, b) => a.weldOrder.CompareTo(b.weldOrder));
                for (int i = 0; i < existingParts.Length; i++)
                {
                    FusedSubPart srcPart = existingParts[i];
                    GameObject cloned = Instantiate(srcPart.gameObject, srcPart.transform.position, srcPart.transform.rotation);
                    cloned.name = "FusedPart_" + startOrder;
                    StripOutlineShells(cloned);
                    cloned.transform.SetParent(compoundRoot, true);
                    FusedSubPart meta = cloned.GetComponent<FusedSubPart>();
                    if (meta != null) meta.weldOrder = startOrder;
                    startOrder++;
                }

                for (int i = 0; i < sourceObj.transform.childCount; i++)
                {
                    Transform ch = sourceObj.transform.GetChild(i);
                    if (ch != null && (ch.name.StartsWith("Fusion Weld Seam") || ch.name.StartsWith("Fusion Joinery")))
                    {
                        GameObject seamCopy = Instantiate(ch.gameObject, ch.position, ch.rotation);
                        seamCopy.name = ch.name;
                        // Avoid stacking redundant lights or orphaned arc renderers when merging into a higher-order compound
                        Light[] copiedLights = seamCopy.GetComponentsInChildren<Light>(true);
                        for (int li = 0; li < copiedLights.Length; li++)
                            if (copiedLights[li] != null) DestroyImmediate(copiedLights[li].gameObject);
                        LineRenderer[] copiedArcs = seamCopy.GetComponentsInChildren<LineRenderer>(true);
                        for (int ai = 0; ai < copiedArcs.Length; ai++)
                            if (copiedArcs[ai] != null) DestroyImmediate(copiedArcs[ai].gameObject);
                        seamCopy.transform.SetParent(compoundRoot, true);
                    }
                }
                return startOrder;
            }

            GameObject partGo = Instantiate(sourceObj.gameObject, sourceObj.transform.position, sourceObj.transform.rotation);
            partGo.name = "FusedPart_" + startOrder;
            StripOutlineShells(partGo);

            ImpactAudioEmitter impact = partGo.GetComponent<ImpactAudioEmitter>();
            if (impact != null) DestroyImmediate(impact);
            FusedKineticRig oldRig = partGo.GetComponent<FusedKineticRig>();
            if (oldRig != null) DestroyImmediate(oldRig);
            InteractableObject childInteract = partGo.GetComponent<InteractableObject>();
            if (childInteract != null) DestroyImmediate(childInteract);
            Rigidbody childRb = partGo.GetComponent<Rigidbody>();
            if (childRb != null) DestroyImmediate(childRb);

            Collider[] cols = partGo.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
            {
                if (cols[i] != null) cols[i].enabled = true;
            }

            Material ownMat = InteractableObject.GetSharedSurfaceMaterial(sourceObj.AssignedColor);
            Renderer rootRenderer = partGo.GetComponent<Renderer>();
            if (rootRenderer != null && (rootRenderer.sharedMaterial == null || !rootRenderer.sharedMaterial.name.StartsWith("Sim_Object_")))
                rootRenderer.sharedMaterial = ownMat;

            partGo.transform.SetParent(compoundRoot, true);

            FusionShapeKind kind = FusionPresetCatalog.ClassifyShape(sourceObj);
            FusedSubPart partMeta = partGo.AddComponent<FusedSubPart>();
            partMeta.partName = sourceObj.displayName;
            partMeta.partMass = sourceObj.Body != null ? sourceObj.Body.mass : 1f;
            partMeta.partColor = sourceObj.AssignedColor;
            partMeta.shapeKind = kind;
            partMeta.isCircularShape = kind == FusionShapeKind.Cylinder || kind == FusionShapeKind.Sphere;
            partMeta.partLocalSize = GetTrueWorldSize(sourceObj);
            partMeta.weldOrder = startOrder;
            return startOrder + 1;
        }

        private InteractableObject ReconstructStandaloneFromSubPart(FusedSubPart part, Vector3 worldPos, Quaternion worldRot)
        {
            // Restore tilted horizontal cylinders back upright when un-fusing cleanly
            Quaternion uprightRot = Quaternion.Euler(0f, ExtractFlatYaw(worldRot).eulerAngles.y, 0f);
            GameObject go = Instantiate(part.gameObject, worldPos, uprightRot);
            go.name = string.IsNullOrEmpty(part.partName) ? "PART" : part.partName;
            go.transform.SetParent(null, true);
            StripOutlineShells(go);

            FusedSubPart meta = go.GetComponent<FusedSubPart>();
            float mass = meta != null ? meta.partMass : 1f;
            Color col = meta != null ? meta.partColor : PaletteSettings.Sapphire;
            string label = meta != null && !string.IsNullOrEmpty(meta.partName) ? meta.partName : go.name;
            if (meta != null) DestroyImmediate(meta);

            Rigidbody rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.4f, mass);
            rb.linearDamping = 0.14f;
            rb.angularDamping = 0.08f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            InteractableObject info = go.AddComponent<InteractableObject>();
            info.displayName = label;
            info.canBePickedUp = true;
            info.canRotate = true;
            info.canThrow = true;
            info.canTelekinesis = true;
            info.canPartition = rb.mass >= minimumSplitMass;
            info.canFuse = true;
            info.isFusedComposite = false;
            info.SetCollidersEnabled(true);
            info.SetSafeOrigin(worldPos, worldRot);
            info.ReinitializeClonedVisuals(col, null);
            go.AddComponent<ImpactAudioEmitter>();
            return info;
        }

        private InteractableObject ReconstructCompoundFromSubParts(List<FusedSubPart> parts, Quaternion rootRot)
        {
            float totalMass = 0f;
            for (int i = 0; i < parts.Count; i++) totalMass += parts[i].partMass;
            totalMass = Mathf.Round(totalMass * 100f) * 0.01f;
            string label = "FUSED-" + totalMass.ToString("0.#", CultureInfo.InvariantCulture) + "T";

            GameObject rootGo = new GameObject(label);
            rootGo.transform.SetPositionAndRotation(parts[0].transform.position, rootRot);
            for (int i = 0; i < parts.Count; i++)
            {
                GameObject clone = Instantiate(parts[i].gameObject, parts[i].transform.position, parts[i].transform.rotation);
                clone.name = "FusedPart_" + i;
                StripOutlineShells(clone);
                clone.transform.SetParent(rootGo.transform, true);
                FusedSubPart m = clone.GetComponent<FusedSubPart>();
                if (m != null) m.weldOrder = i;
            }

            Bounds combined = ComputeWorldRendererBounds(rootGo.transform);
            Vector3 delta = combined.center - rootGo.transform.position;
            if (delta.sqrMagnitude > 0.00001f)
            {
                rootGo.transform.position += delta;
                for (int i = 0; i < rootGo.transform.childCount; i++)
                    rootGo.transform.GetChild(i).position -= delta;
            }

            Rigidbody rb = rootGo.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(0.4f, totalMass);
            rb.linearDamping = 0.14f;
            rb.angularDamping = 0.08f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            InteractableObject info = rootGo.AddComponent<InteractableObject>();
            info.displayName = label;
            info.canBePickedUp = true;
            info.canRotate = true;
            info.canThrow = true;
            info.canTelekinesis = true;
            info.canPartition = true;
            info.canFuse = parts.Count < MaxCompoundSubParts;
            info.isFusedComposite = true;
            info.SetCollidersEnabled(true);
            info.SetSafeOrigin(rootGo.transform.position, rootRot);
            info.ReinitializeClonedVisuals(parts[0].partColor, null);
            rootGo.AddComponent<ImpactAudioEmitter>();
            return info;
        }

        private InteractableObject ResolveHeldObject()
        {
            if (carryController != null && carryController.IsCarrying)
                return carryController.CarriedObject;
            if (telekinesisController != null && telekinesisController.HasTether)
                return telekinesisController.TetheredObject;
            return null;
        }

        private InteractableObject ResolveActiveTarget()
        {
            InteractableObject held = ResolveHeldObject();
            if (held != null) return held;
            return interactor != null ? interactor.CurrentTarget : null;
        }

        private InteractableObject FindFusionPartner(InteractableObject heldSrc)
        {
            if (heldSrc == null) return null;
            float searchRadius = GridSettings.ToWorld(fusionPairRange);
            if (modeController != null && modeController.IsFlexible) searchRadius *= 1.15f;

            // Measure horizontal distance from Rect (when carrying overhead) or from the lifted object itself
            Vector3 searchOrigin = (carryController != null && carryController.CarriedObject == heldSrc)
                ? new Vector3(transform.position.x, transform.position.y + GridSettings.ToWorld(0.55f), transform.position.z)
                : heldSrc.transform.position;

            bool srcIsCore = IsCoreObject(heldSrc);
            int count = Physics.OverlapSphereNonAlloc(searchOrigin, searchRadius, pairBuffer, ~0, QueryTriggerInteraction.Ignore);
            InteractableObject best = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = pairBuffer[i];
                if (col == null) continue;
                InteractableObject cand = col.GetComponentInParent<InteractableObject>();
                if (cand == null || cand == heldSrc || !cand.isActiveAndEnabled) continue;
                if (!cand.CanBePickedUp || !cand.canFuse || transitioningObjects.Contains(cand)) continue;
                if (IsCoreObject(cand) != srcIsCore) continue;
                if (carryController != null && cand == carryController.CarriedObject) continue;
                if (telekinesisController != null && cand == telekinesisController.TetheredObject) continue;
                if (!HasCompoundCapacity(heldSrc, cand)) continue;

                float d = Vector3.Distance(searchOrigin, cand.transform.position);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = cand;
                }
            }
            return best;
        }

        private static int CountSubParts(GameObject go)
        {
            if (go == null) return 0;
            int n = go.GetComponentsInChildren<FusedSubPart>(true).Length;
            return n > 0 ? n : 1;
        }

        private static bool HasCompoundCapacity(InteractableObject a, InteractableObject b)
        {
            if (a != null && b != null && IsCoreObject(a) && IsCoreObject(b)) return true;
            int countA = a != null ? CountSubParts(a.gameObject) : 0;
            int countB = b != null ? CountSubParts(b.gameObject) : 0;
            return (countA + countB) <= MaxCompoundSubParts;
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

            UnregisterFromAllPlates(target.Body);
            return pos;
        }

        private void UnregisterFromAllPlates(Rigidbody rb)
        {
            if (rb == null) return;
            WeightedPressurePlate[] plates = FindObjectsByType<WeightedPressurePlate>(FindObjectsSortMode.None);
            for (int i = 0; i < plates.Length; i++)
                plates[i].UnregisterBody(rb);
        }

        private static void StripOutlineShells(GameObject go)
        {
            if (go == null) return;
            OutlineShell[] shells = go.GetComponentsInChildren<OutlineShell>(true);
            for (int i = 0; i < shells.Length; i++)
            {
                if (shells[i] != null) DestroyImmediate(shells[i].gameObject);
            }
        }

        private static Bounds ComputeWorldRendererBounds(Transform root)
        {
            Renderer[] rends = root.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;
            Bounds total = new Bounds(root.position, Vector3.one * 0.5f);
            for (int i = 0; i < rends.Length; i++)
            {
                Renderer r = rends[i];
                if (r == null || r.GetComponent<OutlineShell>() != null || r.GetComponent<ObjectTrim>() != null) continue;
                if (!hasBounds)
                {
                    total = r.bounds;
                    hasBounds = true;
                }
                else
                {
                    total.Encapsulate(r.bounds);
                }
            }
            return total;
        }

        private static Quaternion ExtractFlatYaw(Quaternion rotation)
        {
            Vector3 fwd = Vector3.ProjectOnPlane(rotation * Vector3.forward, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f)
                fwd = Vector3.ProjectOnPlane(rotation * Vector3.up, Vector3.up);
            if (fwd.sqrMagnitude < 0.0001f)
                return Quaternion.identity;
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        private static float ProjectHalfExtent(Vector3 localSize, Quaternion objectRot, Vector3 worldDir)
        {
            Vector3 localDir = Quaternion.Inverse(objectRot) * worldDir.normalized;
            return (Mathf.Abs(localDir.x) * localSize.x +
                    Mathf.Abs(localDir.y) * localSize.y +
                    Mathf.Abs(localDir.z) * localSize.z) * 0.5f;
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
            if (target.TryGetLocalFootprint(out Vector3 localSize))
                return localSize;
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
            if (frontVisor != null) frontVisor.gameObject.SetActive(!hidden);
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
                    n == "Fusion Core Inlay" || n == "Fusion Octo Collar" || n.StartsWith("Fusion Weld Seam"))
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

        public static string GetFusionSideLabel(FusionAttachmentSide side)
        {
            return side switch
            {
                FusionAttachmentSide.Right => "KANAN",
                FusionAttachmentSide.Front => "DEPAN",
                FusionAttachmentSide.Left => "KIRI",
                FusionAttachmentSide.Back => "BELAKANG",
                FusionAttachmentSide.Top => "ATAS",
                _ => "KANAN"
            };
        }

        private void TriggerHitStop(float seconds)
        {
            if (!gameObject.activeInHierarchy || !enabled || seconds <= 0f) return;
            if (Time.timeScale <= 0f && !hitStopActive) return;
            if (!hitStopActive) preHitStopTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            if (hitStopCoroutine != null) StopCoroutine(hitStopCoroutine);
            hitStopCoroutine = StartCoroutine(HitStopRoutine(++hitStopId, seconds));
        }

        private IEnumerator HitStopRoutine(int myId, float seconds)
        {
            hitStopActive = true;
            Time.timeScale = 0f;
            yield return new WaitForSecondsRealtime(seconds);
            if (myId == hitStopId)
            {
                Time.timeScale = preHitStopTimeScale;
                hitStopActive = false;
                hitStopCoroutine = null;
            }
        }

        private void OnDisable()
        {
            if (fusionStaging) CancelFusionStage(silent: true);
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
            HideFusionStageVisuals();
            if (cutPlaneRoot != null) cutPlaneRoot.SetActive(false);
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
            if (fusionWeldPadFillMat != null) Destroy(fusionWeldPadFillMat);
            if (fusionWeldPadFrameMat != null) Destroy(fusionWeldPadFrameMat);
            if (faceMarkerIdleMat != null) Destroy(faceMarkerIdleMat);
            if (faceMarkerActiveMat != null) Destroy(faceMarkerActiveMat);
            if (faceMarkerBlockedMat != null) Destroy(faceMarkerBlockedMat);
        }
    }
}
