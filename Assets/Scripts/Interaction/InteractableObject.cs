using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour
    {
        [Header("Identity & Capabilities")]
        public string displayName = "OBJECT";
        public bool canBePickedUp = true;
        public bool canRotate = true;
        public bool canThrow = true;
        public bool canTelekinesis = true;
        public bool canPartition = true;
        public bool canFuse = true;

        [Header("Partition & Fusion")]
        public int partitionDepth;
        public bool isFusedComposite;
        public bool allowVoidRespawn = true;

        [Header("Colour")]
        [SerializeField] private Color assignedColor = new Color(0f, 0f, 0f, 0f);

        [Header("Outline")]
        [Tooltip("World-space thickness (GU) of the 360-degree smoothed-normal silhouette outline.")]
        [Range(0.02f, 0.14f)]
        [SerializeField] private float outlineWidth = 0.056f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int RimTintId = Shader.PropertyToID("_RimTint");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int RimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int RimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private static readonly Dictionary<uint, Material> SharedOutlineMaterials = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Material> SharedRimMaterials = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Material> SharedRuntimeSurfaceMaterials = new Dictionary<uint, Material>();
        private static readonly Dictionary<Mesh, Mesh> SmoothedOutlineMeshes = new Dictionary<Mesh, Mesh>();

        private static Material sharedOutlineTemplate;
        private static Material sharedRimTemplate;

        private readonly List<Renderer> outlineRenderers = new List<Renderer>();
        private readonly List<Renderer> rimRenderers = new List<Renderer>();
        private Rigidbody body;
        private Renderer[] renderers;
        private Renderer[] boundsRenderers;
        private Collider[] colliders;
        private bool isHighlighted;
        private bool highlightedAsPickup;
        private bool outlinesBuilt;
        private Vector3 initialLocalScale = Vector3.one;
        private Vector3 safeOriginPos;
        private Quaternion safeOriginRot = Quaternion.identity;
        private float pulseTimer;
        private float pulseDuration = 0.22f;
        private float pulseStrength;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticMaterialCaches()
        {
            SharedOutlineMaterials.Clear();
            SharedRimMaterials.Clear();
            SharedRuntimeSurfaceMaterials.Clear();
            foreach (var kv in SmoothedOutlineMeshes)
            {
                if (kv.Value != null) Object.Destroy(kv.Value);
            }
            SmoothedOutlineMeshes.Clear();
            sharedOutlineTemplate = null;
            sharedRimTemplate = null;
        }

        public bool CanBePickedUp => canBePickedUp && Body != null;
        public bool HasAssignedColor => assignedColor.a > 0.5f;
        public Color AssignedColor => assignedColor;

        public Rigidbody Body
        {
            get
            {
                if (body == null) body = GetComponent<Rigidbody>();
                return body;
            }
        }

        private void Awake()
        {
            initialLocalScale = transform.localScale;
            safeOriginPos = transform.position;
            safeOriginRot = transform.rotation;
            StripDuplicatedOutlineChildren();
            CacheComponents();
            if (!HasAssignedColor)
            {
                assignedColor = InteractivePaletteAssigner.NextColor();
                assignedColor.a = 1f;
            }
            if (CanBePickedUp) BuildOutlines();
            ApplyVisualState();
        }

        public void SetSafeOrigin(Vector3 pos, Quaternion rot)
        {
            safeOriginPos = pos;
            safeOriginRot = rot;
        }

        public void ReinitializeClonedVisuals(Color newColor, Material sharedSurfaceMat = null)
        {
            StripDuplicatedOutlineChildren();
            outlineRenderers.Clear();
            rimRenderers.Clear();
            outlinesBuilt = false;
            isHighlighted = false;
            highlightedAsPickup = false;
            initialLocalScale = transform.localScale;
            safeOriginPos = transform.position;
            safeOriginRot = transform.rotation;
            CacheComponents();
            SetAssignedColor(newColor, sharedSurfaceMat);
            if (CanBePickedUp) BuildOutlines();
            ApplyVisualState();
        }

        private void StripDuplicatedOutlineChildren()
        {
            OutlineShell[] shells = GetComponentsInChildren<OutlineShell>(true);
            for (int i = 0; i < shells.Length; i++)
            {
                if (shells[i] != null) DestroyImmediate(shells[i].gameObject);
            }
        }

        public void RefreshBaseScale()
        {
            initialLocalScale = transform.localScale;
            pulseTimer = 0f;
            pulseStrength = 0f;
        }

        public void TriggerPulse(float strength = 0.14f, float duration = 0.22f)
        {
            pulseStrength = Mathf.Max(pulseStrength, strength);
            pulseDuration = Mathf.Max(0.08f, duration);
            pulseTimer = pulseDuration;
        }

        private void Update()
        {
            if (allowVoidRespawn && CanBePickedUp && Body != null && !Body.isKinematic &&
                transform.position.y < GridSettings.ToWorld(-2.2f))
            {
                RespawnIntoBoundsSafely();
            }

            if (pulseTimer <= 0f) return;

            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0f)
            {
                pulseTimer = 0f;
                pulseStrength = 0f;
                transform.localScale = initialLocalScale;
                return;
            }

            float k = 1f - (pulseTimer / pulseDuration);
            float bell = Mathf.Sin(k * Mathf.PI) * (1f - k * 0.35f);
            transform.localScale = initialLocalScale * (1f + pulseStrength * bell);
        }

        private void RespawnIntoBoundsSafely()
        {
            Vector3 restore = WorldBounds.ClampInside(
                new Vector3(safeOriginPos.x, Mathf.Max(safeOriginPos.y, GridSettings.ToWorld(0.65f)), safeOriginPos.z),
                0.7f, 0.7f);
            transform.SetPositionAndRotation(restore, safeOriginRot);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.WakeUp();
            SimulationVFX.Ring(restore, PaletteSettings.Yellow, 1.5f, 0.35f);
            SimulationModeController.Instance?.NotifyBanner(displayName + " dikembalikan ke ruang uji", PaletteSettings.Yellow);
        }

        public void CacheComponents()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);

            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            List<Renderer> primaryRenderers = new List<Renderer>(all.Length);
            List<Renderer> extentRenderers = new List<Renderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                Renderer r = all[i];
                if (r == null || r.GetComponent<OutlineShell>() != null) continue;
                extentRenderers.Add(r);
                if (r.GetComponent<ObjectTrim>() == null)
                    primaryRenderers.Add(r);
            }
            renderers = primaryRenderers.ToArray();
            boundsRenderers = extentRenderers.ToArray();
        }

        public void SetAssignedColor(Color color, Material sharedMaterial = null)
        {
            color.a = 1f;
            assignedColor = color;
            if (renderers == null) CacheComponents();

            if (sharedMaterial != null && renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] != null) renderers[i].sharedMaterial = sharedMaterial;
                }
            }

            ApplyVisualState();
        }

        public void SetCollidersEnabled(bool enabled)
        {
            if (colliders == null) CacheComponents();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null) colliders[i].enabled = enabled;
            }
        }

        public Renderer[] GetBaseRenderers()
        {
            if (renderers == null) CacheComponents();
            return renderers;
        }

        public bool TryGetHalfExtents(out Vector3 halfExtents)
        {
            halfExtents = Vector3.zero;
            if (boundsRenderers == null) CacheComponents();
            if (boundsRenderers == null || boundsRenderers.Length == 0) return false;

            Vector3 center = transform.position;
            for (int i = 0; i < boundsRenderers.Length; i++)
            {
                Renderer targetRenderer = boundsRenderers[i];
                if (targetRenderer == null) continue;

                Bounds bounds = targetRenderer.bounds;
                halfExtents.x = Mathf.Max(halfExtents.x, Mathf.Abs(bounds.center.x - center.x) + bounds.extents.x);
                halfExtents.y = Mathf.Max(halfExtents.y, Mathf.Abs(bounds.center.y - center.y) + bounds.extents.y);
                halfExtents.z = Mathf.Max(halfExtents.z, Mathf.Abs(bounds.center.z - center.z) + bounds.extents.z);
            }
            return true;
        }

        public bool TryGetPlanarHalfExtents(out float halfExtentX, out float halfExtentZ)
        {
            bool found = TryGetHalfExtents(out Vector3 extents);
            halfExtentX = extents.x;
            halfExtentZ = extents.z;
            return found;
        }

        public void SetHighlight(bool active, bool canPickup)
        {
            if (!active)
            {
                if (!isHighlighted) return;
                isHighlighted = false;
                highlightedAsPickup = false;
                ApplyVisualState();
                return;
            }

            if (isHighlighted && highlightedAsPickup == canPickup) return;
            isHighlighted = true;
            highlightedAsPickup = canPickup;
            ApplyVisualState();
        }

        /// <summary>
        /// Keeps the assigned shared SRP-Batched material on the object and enables both
        /// the watertight world-space smoothed outline shell and edge-contour Fresnel rim overlay.
        /// </summary>
        private void ApplyVisualState()
        {
            if (renderers == null) CacheComponents();
            if (!HasAssignedColor) return;

            if (Application.isPlaying && renderers != null)
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer r = renderers[i];
                    if (r == null) continue;
                    if (r.sharedMaterial == null || !r.sharedMaterial.name.StartsWith("Sim_Object_"))
                        r.sharedMaterial = GetSharedSurfaceMaterial(assignedColor);
                }
            }

            bool showOutline = isHighlighted && highlightedAsPickup && CanBePickedUp;
            if (showOutline && !outlinesBuilt) BuildOutlines();

            Material coloredOutline = showOutline ? GetSharedOutlineMaterial(assignedColor, outlineWidth) : null;
            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                Renderer outline = outlineRenderers[i];
                if (outline == null) continue;
                if (coloredOutline != null) outline.sharedMaterial = coloredOutline;
                outline.enabled = showOutline;
            }

            Material coloredRim = showOutline ? GetSharedRimMaterial(assignedColor) : null;
            for (int i = 0; i < rimRenderers.Count; i++)
            {
                Renderer rim = rimRenderers[i];
                if (rim == null) continue;
                if (coloredRim != null) rim.sharedMaterial = coloredRim;
                rim.enabled = showOutline;
            }
        }

        private void BuildOutlines()
        {
            if (outlinesBuilt || renderers == null || !CanBePickedUp) return;
            outlinesBuilt = true;

            Material outlineMat = HasAssignedColor
                ? GetSharedOutlineMaterial(assignedColor, outlineWidth)
                : OutlineMaterial;
            Material rimMat = HasAssignedColor
                ? GetSharedRimMaterial(assignedColor)
                : RimOverlayMaterial;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer source = renderers[i];
                if (source == null) continue;

                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh smoothMesh = GetOrBuildSmoothOutlineMesh(filter.sharedMesh);

                GameObject shell = new GameObject(source.gameObject.name + " Outline");
                shell.layer = source.gameObject.layer;
                shell.transform.SetParent(source.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one;

                MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = smoothMesh;

                MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = outlineMat;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                shellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                shellRenderer.enabled = false;
                shell.AddComponent<OutlineShell>();
                outlineRenderers.Add(shellRenderer);

                GameObject rimShell = new GameObject(source.gameObject.name + " RimGlow");
                rimShell.layer = source.gameObject.layer;
                rimShell.transform.SetParent(source.transform, false);
                rimShell.transform.localPosition = Vector3.zero;
                rimShell.transform.localRotation = Quaternion.identity;
                rimShell.transform.localScale = Vector3.one;

                MeshFilter rimFilter = rimShell.AddComponent<MeshFilter>();
                rimFilter.sharedMesh = filter.sharedMesh;

                MeshRenderer rimRenderer = rimShell.AddComponent<MeshRenderer>();
                rimRenderer.sharedMaterial = rimMat;
                rimRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                rimRenderer.receiveShadows = false;
                rimRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                rimRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                rimRenderer.enabled = false;
                rimShell.AddComponent<OutlineShell>();
                rimRenderers.Add(rimRenderer);
            }
        }

        private static Mesh GetOrBuildSmoothOutlineMesh(Mesh src)
        {
            if (src == null) return null;
            if (SmoothedOutlineMeshes.TryGetValue(src, out Mesh cached) && cached != null)
                return cached;

            Vector3[] verts = src.vertices;
            Vector3[] norms = src.normals;
            if (verts == null || verts.Length == 0) return src;
            if (norms == null || norms.Length != verts.Length)
            {
                Mesh tmp = Object.Instantiate(src);
                tmp.RecalculateNormals();
                norms = tmp.normals;
                Object.Destroy(tmp);
            }

            // Average coincident vertex normals so extruded back-faces stay watertight around sharp bevel corners
            var accum = new Dictionary<Vector3Int, Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(v.x * 2000f),
                    Mathf.RoundToInt(v.y * 2000f),
                    Mathf.RoundToInt(v.z * 2000f));
                if (accum.TryGetValue(key, out Vector3 sum))
                    accum[key] = sum + norms[i];
                else
                    accum[key] = norms[i];
            }

            Vector3[] smoothNormals = new Vector3[verts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                Vector3Int key = new Vector3Int(
                    Mathf.RoundToInt(v.x * 2000f),
                    Mathf.RoundToInt(v.y * 2000f),
                    Mathf.RoundToInt(v.z * 2000f));
                Vector3 avg = accum[key];
                smoothNormals[i] = avg.sqrMagnitude > 1e-6f ? avg.normalized : norms[i];
            }

            Mesh outlineMesh = new Mesh();
            outlineMesh.name = src.name + "_SmoothOutline";
            outlineMesh.vertices = verts;
            outlineMesh.normals = smoothNormals;
            outlineMesh.triangles = src.triangles;
            outlineMesh.bounds = src.bounds;
            outlineMesh.hideFlags = HideFlags.HideAndDontSave;

            SmoothedOutlineMeshes[src] = outlineMesh;
            return outlineMesh;
        }

        private static uint PackColorKey(Color c)
        {
            Color32 c32 = c;
            return ((uint)c32.r << 16) | ((uint)c32.g << 8) | c32.b;
        }

        private static Material GetSharedOutlineMaterial(Color color, float width)
        {
            uint key = PackColorKey(color);
            if (SharedOutlineMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material mat = new Material(OutlineMaterial);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.name = "Sim_ObjectOutline_" + key.ToString("X6");
            Color hdr = Color.Lerp(color, PaletteSettings.Sky, 0.25f) * 1.25f;
            hdr.a = 1f;
            Color rimTint = Color.Lerp(color, PaletteSettings.Text, 0.65f);
            rimTint.a = 1f;
            mat.SetColor(BaseColorId, hdr);
            mat.SetColor(ColorId, hdr);
            if (mat.HasProperty(RimTintId)) mat.SetColor(RimTintId, rimTint);
            if (mat.HasProperty(OutlineWidthId)) mat.SetFloat(OutlineWidthId, GridSettings.ToWorld(width));
            SharedOutlineMaterials[key] = mat;
            return mat;
        }

        private static Material GetSharedRimMaterial(Color color)
        {
            uint key = PackColorKey(color);
            if (SharedRimMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material mat = new Material(RimOverlayMaterial);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.name = "Sim_ObjectRim_" + key.ToString("X6");
            Color baseCol = Color.Lerp(color, PaletteSettings.Text, 0.35f);
            baseCol.a = 1f;
            mat.SetColor(BaseColorId, baseCol);
            if (mat.HasProperty(RimTintId)) mat.SetColor(RimTintId, PaletteSettings.Text);
            if (mat.HasProperty(RimPowerId)) mat.SetFloat(RimPowerId, 2.0f);
            if (mat.HasProperty(RimIntensityId)) mat.SetFloat(RimIntensityId, 1.18f);
            SharedRimMaterials[key] = mat;
            return mat;
        }

        public static Material GetSharedSurfaceMaterial(Color color)
        {
            uint key = PackColorKey(color);
            if (SharedRuntimeSurfaceMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Shader shader = Shader.Find("POLYMULATOR/Sim Surface");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = new Material(shader);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.name = "Sim_Object_Runtime_" + key.ToString("X6");
            mat.SetColor(BaseColorId, color);
            mat.SetColor(ColorId, color);
            if (mat.HasProperty("_ShadeColor")) mat.SetColor("_ShadeColor", PaletteSettings.Mantle);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Color.Lerp(color, PaletteSettings.Text, 0.55f));
            if (mat.HasProperty("_SeamColor")) mat.SetColor("_SeamColor", PaletteSettings.Crust);
            SharedRuntimeSurfaceMaterials[key] = mat;
            return mat;
        }

        private static Material OutlineMaterial
        {
            get
            {
                if (sharedOutlineTemplate != null) return sharedOutlineTemplate;

                Material res = Resources.Load<Material>("Sim_ObjectOutline");
                if (res != null && res.shader != null && res.shader.name == "POLYMULATOR/Pro Outline")
                {
                    sharedOutlineTemplate = res;
                    return sharedOutlineTemplate;
                }

                Shader shader = Shader.Find("POLYMULATOR/Pro Outline");
                if (shader != null)
                {
                    sharedOutlineTemplate = new Material(shader)
                    {
                        name = "Sim_ProOutline (Template)",
                        hideFlags = HideFlags.HideAndDontSave
                    };
                    return sharedOutlineTemplate;
                }

                if (res != null)
                {
                    sharedOutlineTemplate = res;
                    return sharedOutlineTemplate;
                }

                Shader fallback = Shader.Find("Universal Render Pipeline/Unlit");
                if (fallback == null) fallback = Shader.Find("Unlit/Color");
                sharedOutlineTemplate = new Material(fallback) { name = "Sim_ObjectOutline (Fallback)" };
                if (sharedOutlineTemplate.HasProperty(CullId))
                    sharedOutlineTemplate.SetFloat(CullId, 1f);
                return sharedOutlineTemplate;
            }
        }

        private static Material RimOverlayMaterial
        {
            get
            {
                if (sharedRimTemplate != null) return sharedRimTemplate;
                Material res = Resources.Load<Material>("Sim_ObjectRimOverlay");
                if (res != null && res.shader != null && res.shader.name == "POLYMULATOR/Pro Rim Overlay")
                {
                    sharedRimTemplate = res;
                    return sharedRimTemplate;
                }

                Shader shader = Shader.Find("POLYMULATOR/Pro Rim Overlay");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                sharedRimTemplate = new Material(shader)
                {
                    name = "Sim_ProRimOverlay (Template)",
                    hideFlags = HideFlags.HideAndDontSave
                };
                return sharedRimTemplate;
            }
        }
    }
}
