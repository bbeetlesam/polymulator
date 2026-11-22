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

        [Header("Colour")]
        [SerializeField] private Color assignedColor = new Color(0f, 0f, 0f, 0f);

        [Header("Outline")]
        [Tooltip("How much larger the target outline is than the object itself.")]
        [Range(1.01f, 1.5f)]
        [SerializeField] private float outlineScale = 1.07f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CullId = Shader.PropertyToID("_Cull");
        private static readonly Dictionary<uint, Material> SharedOutlineMaterials = new Dictionary<uint, Material>();
        private static readonly Dictionary<uint, Material> SharedRuntimeSurfaceMaterials = new Dictionary<uint, Material>();

        private static Material sharedOutlineTemplate;

        private readonly List<Renderer> outlineRenderers = new List<Renderer>();
        private Rigidbody body;
        private Renderer[] renderers;
        private Renderer[] boundsRenderers;
        private Collider[] colliders;
        private bool isHighlighted;
        private bool highlightedAsPickup;
        private bool outlinesBuilt;
        private Vector3 initialLocalScale = Vector3.one;
        private float pulseTimer;
        private float pulseDuration = 0.22f;
        private float pulseStrength;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticMaterialCaches()
        {
            SharedOutlineMaterials.Clear();
            SharedRuntimeSurfaceMaterials.Clear();
            sharedOutlineTemplate = null;
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
            CacheComponents();
            if (!HasAssignedColor)
            {
                // Objects created outside the scene builder still get a stable palette colour.
                assignedColor = InteractivePaletteAssigner.NextColor();
                assignedColor.a = 1f;
            }
            if (CanBePickedUp) BuildOutlines();
            ApplyVisualState();
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

        /// <summary>
        /// Stores the object's stable base colour and optionally its shared palette material.
        /// Called during scene setup and never during interaction.
        /// </summary>
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

        /// <summary>
        /// Largest rendered half-extent on each axis, used for the placement ghost
        /// and to keep a carried object inside the platform while its colliders are off.
        /// </summary>
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
        /// Keeps the assigned shared SRP-Batched material on the object and enables the shared
        /// colour-matched inverted-hull outline only while a pickable object is targeted.
        /// Never creates per-renderer MaterialPropertyBlocks.
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
                    {
                        r.sharedMaterial = GetSharedSurfaceMaterial(assignedColor);
                    }
                }
            }

            bool showOutline = isHighlighted && highlightedAsPickup && CanBePickedUp;
            if (showOutline && !outlinesBuilt) BuildOutlines();

            Material coloredOutline = showOutline ? GetSharedOutlineMaterial(assignedColor) : null;
            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                Renderer outline = outlineRenderers[i];
                if (outline == null) continue;

                if (coloredOutline != null)
                    outline.sharedMaterial = coloredOutline;
                outline.enabled = showOutline;
            }
        }

        /// <summary>
        /// Creates one slightly enlarged copy of each base mesh with front faces culled,
        /// so only the rim beyond the object's silhouette is drawn.
        /// </summary>
        private void BuildOutlines()
        {
            if (outlinesBuilt || renderers == null || !CanBePickedUp) return;
            outlinesBuilt = true;

            float scale = Mathf.Max(1.01f, outlineScale);
            Material outlineMat = HasAssignedColor
                ? GetSharedOutlineMaterial(assignedColor)
                : OutlineMaterial;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer source = renderers[i];
                if (source == null) continue;

                MeshFilter filter = source.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                GameObject shell = new GameObject(source.gameObject.name + " Outline");
                shell.layer = source.gameObject.layer;
                shell.transform.SetParent(source.transform, false);
                shell.transform.localPosition = Vector3.zero;
                shell.transform.localRotation = Quaternion.identity;
                shell.transform.localScale = Vector3.one * scale;

                MeshFilter shellFilter = shell.AddComponent<MeshFilter>();
                shellFilter.sharedMesh = filter.sharedMesh;

                MeshRenderer shellRenderer = shell.AddComponent<MeshRenderer>();
                shellRenderer.sharedMaterial = outlineMat;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                shellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                shellRenderer.enabled = false;

                shell.AddComponent<OutlineShell>();
                outlineRenderers.Add(shellRenderer);
            }
        }

        private static uint PackColorKey(Color c)
        {
            Color32 c32 = c;
            return ((uint)c32.r << 16) | ((uint)c32.g << 8) | c32.b;
        }

        private static Material GetSharedOutlineMaterial(Color color)
        {
            uint key = PackColorKey(color);
            if (SharedOutlineMaterials.TryGetValue(key, out Material cached) && cached != null)
                return cached;

            Material mat = new Material(OutlineMaterial);
            mat.hideFlags = HideFlags.HideAndDontSave;
            mat.name = "Sim_ObjectOutline_" + key.ToString("X6");
            Color hdr = color * 1.15f;
            hdr.a = 1f;
            mat.SetColor(BaseColorId, hdr);
            mat.SetColor(ColorId, hdr);
            SharedOutlineMaterials[key] = mat;
            return mat;
        }

        private static Material GetSharedSurfaceMaterial(Color color)
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

                sharedOutlineTemplate = Resources.Load<Material>("Sim_ObjectOutline");
                if (sharedOutlineTemplate != null) return sharedOutlineTemplate;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                sharedOutlineTemplate = new Material(shader);
                sharedOutlineTemplate.name = "Sim_ObjectOutline (Runtime)";
                if (sharedOutlineTemplate.HasProperty(CullId))
                    sharedOutlineTemplate.SetFloat(CullId, 1f);
                return sharedOutlineTemplate;
            }
        }
    }
}
