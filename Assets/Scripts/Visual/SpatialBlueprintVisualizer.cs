using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    /// <summary>
    /// Diegetic 3D spatial vector visualizer for Computer Graphics / SDG-4 education.
    /// Toggles clean Cartesian world axes, local object axes, anchor displacement vector ([V]),
    /// and subtle gimbal rotation rings around actively rotated/tethered props.
    /// </summary>
    public sealed class SpatialBlueprintVisualizer : MonoBehaviour
    {
        public SimulationModeController modeController;
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;
        public Transform anchorTransform;

        public bool IsBlueprintActive => blueprintActive;

        public event System.Action<bool> OnBlueprintToggled;

        private const int CircleSegments = 32;
        private bool blueprintActive;
        private float yawRingFlashUntil;

        private readonly System.Collections.Generic.List<Material> ownedMaterials = new System.Collections.Generic.List<Material>(8);
        private LineRenderer[] worldAxes = new LineRenderer[3];
        private LineRenderer[] localAxes = new LineRenderer[3];
        private LineRenderer anchorVectorLine;
        private LineRenderer yawGimbalRing;
        private readonly Vector3[] ringBuffer = new Vector3[CircleSegments];

        private void Awake()
        {
            ResolveReferences();
            BuildLineVisuals();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (carryController != null) carryController.OnRotated += HandleRotated;
        }

        private void OnDisable()
        {
            if (carryController != null) carryController.OnRotated -= HandleRotated;
        }

        private void ResolveReferences()
        {
            if (modeController == null) modeController = Object.FindFirstObjectByType<SimulationModeController>();
            if (interactor == null) interactor = Object.FindFirstObjectByType<ObjectInteractor>();
            if (carryController == null) carryController = Object.FindFirstObjectByType<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = Object.FindFirstObjectByType<TelekinesisController>();
            if (anchorTransform == null)
            {
                AnchorVisualRig rig = Object.FindFirstObjectByType<AnchorVisualRig>();
                if (rig != null) anchorTransform = rig.transform;
            }
        }

        private void HandleRotated(InteractableObject obj, float yawDeg)
        {
            yawRingFlashUntil = Time.time + 0.75f;
        }

        public void ToggleBlueprint()
        {
            blueprintActive = !blueprintActive;
            SimulationAudio.PlayMenuClick();
            OnBlueprintToggled?.Invoke(blueprintActive);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame && Time.timeScale > 0f)
                ToggleBlueprint();
        }

        private void LateUpdate()
        {
            SetLinesActive(worldAxes, blueprintActive);
            if (blueprintActive)
            {
                float axisLen = GridSettings.ToWorld(1.35f);
                Vector3 origin = Vector3.up * GridSettings.ToWorld(0.04f);
                UpdateRay(worldAxes[0], origin, Vector3.right * axisLen);
                UpdateRay(worldAxes[1], origin, Vector3.up * axisLen);
                UpdateRay(worldAxes[2], origin, Vector3.forward * axisLen);
            }

            InteractableObject activeObj = GetActiveObject();
            bool showLocal = blueprintActive && activeObj != null;
            SetLinesActive(localAxes, showLocal);
            if (showLocal)
            {
                float len = GridSettings.ToWorld(0.95f);
                Transform t = activeObj.transform;
                UpdateRay(localAxes[0], t.position, t.right * len);
                UpdateRay(localAxes[1], t.position, t.up * len);
                UpdateRay(localAxes[2], t.position, t.forward * len);
            }

            bool showAnchorVector = blueprintActive && activeObj != null && anchorTransform != null;
            anchorVectorLine.enabled = showAnchorVector;
            if (showAnchorVector)
            {
                anchorVectorLine.SetPosition(0, activeObj.transform.position);
                anchorVectorLine.SetPosition(1, anchorTransform.position + Vector3.up * GridSettings.ToWorld(0.28f));
            }

            bool showYawRing = activeObj != null &&
                               (blueprintActive || Time.time <= yawRingFlashUntil ||
                                (telekinesisController != null && telekinesisController.HasTether));
            yawGimbalRing.enabled = showYawRing;
            if (showYawRing)
            {
                float ringRadius = activeObj.TryGetPlanarHalfExtents(out float hx, out float hz)
                    ? Mathf.Max(hx, hz) + GridSettings.ToWorld(0.26f)
                    : GridSettings.ToWorld(0.82f);
                UpdateCircle(yawGimbalRing, activeObj.transform.position, activeObj.transform.rotation, ringRadius);
            }
        }

        private InteractableObject GetActiveObject()
        {
            if (carryController != null && carryController.IsCarrying)
                return carryController.CarriedObject;
            if (telekinesisController != null && telekinesisController.HasTether)
                return telekinesisController.TetheredObject;
            return interactor != null && interactor.HasTarget && interactor.CurrentTarget.CanBePickedUp
                ? interactor.CurrentTarget
                : null;
        }

        private void BuildLineVisuals()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Color[] axisColors = { PaletteSettings.Red, PaletteSettings.Green, PaletteSettings.Sapphire };
            for (int i = 0; i < 3; i++)
            {
                Material axisMat = CreateColorMaterial(shader, axisColors[i], 0.92f);
                worldAxes[i] = CreateLine("WorldAxis_" + i, axisMat, axisColors[i], 0.048f, 2, false);
                localAxes[i] = CreateLine("LocalAxis_" + i, axisMat, axisColors[i], 0.042f, 2, false);
            }

            Material tealMat = CreateColorMaterial(shader, PaletteSettings.Teal, 0.80f);
            anchorVectorLine = CreateLine("AnchorVector", tealMat, PaletteSettings.Teal, 0.032f, 2, false);

            Material yellowMat = CreateColorMaterial(shader, PaletteSettings.Yellow, 0.88f);
            yawGimbalRing = CreateLine("YawGimbalRing", yellowMat, PaletteSettings.Yellow, 0.032f, CircleSegments, true);
        }

        private Material CreateColorMaterial(Shader shader, Color color, float alpha)
        {
            Material mat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            color.a = alpha;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            ownedMaterials.Add(mat);
            return mat;
        }

        private LineRenderer CreateLine(string objName, Material mat, Color color, float widthGrid, int vertexCount, bool loop)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.loop = loop;
            lr.positionCount = vertexCount;
            lr.sharedMaterial = mat;
            lr.startColor = color;
            lr.endColor = color;
            float w = GridSettings.ToWorld(widthGrid);
            lr.startWidth = w;
            lr.endWidth = w;
            lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.enabled = false;
            return lr;
        }

        private static void UpdateRay(LineRenderer lr, Vector3 origin, Vector3 delta)
        {
            lr.SetPosition(0, origin);
            lr.SetPosition(1, origin + delta);
        }

        private void UpdateCircle(LineRenderer lr, Vector3 center, Quaternion rot, float radius)
        {
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = i / (float)CircleSegments * Mathf.PI * 2f;
                Vector3 local = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                ringBuffer[i] = center + rot * local;
            }
            lr.SetPositions(ringBuffer);
        }

        private static void SetLinesActive(LineRenderer[] lines, bool active)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i] != null && lines[i].enabled != active)
                    lines[i].enabled = active;
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
            }
            ownedMaterials.Clear();
        }
    }
}
