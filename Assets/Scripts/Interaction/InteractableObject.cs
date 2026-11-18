using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour
    {
        [Header("Identity")]
        public string displayName = "OBJECT";
        public bool canBePickedUp = true;

        [Header("Colour")]
        [SerializeField] private Color assignedColor = new Color(0f, 0f, 0f, 0f);

        [Header("Outline")]
        [Tooltip("How much larger the target outline is than the object itself.")]
        [Range(1.01f, 1.5f)]
        [SerializeField] private float outlineScale = 1.08f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private static Material sharedOutlineMaterial;

        private readonly List<Renderer> outlineRenderers = new List<Renderer>();
        private Rigidbody body;
        private Renderer[] renderers;
        private Collider[] colliders;
        private bool isHighlighted;
        private bool highlightedAsPickup;
        private bool outlinesBuilt;

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

        public void CacheComponents()
        {
            body = GetComponent<Rigidbody>();
            colliders = GetComponentsInChildren<Collider>(true);

            Renderer[] all = GetComponentsInChildren<Renderer>(true);
            List<Renderer> baseRenderers = new List<Renderer>(all.Length);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].GetComponent<OutlineShell>() == null)
                    baseRenderers.Add(all[i]);
            }
            renderers = baseRenderers.ToArray();
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
            if (renderers == null) CacheComponents();
            if (renderers == null || renderers.Length == 0) return false;

            Vector3 center = transform.position;
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
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
        /// Keeps the assigned colour on the object and shows the inverted-hull outline
        /// only while a pickable object is targeted. Locked objects never receive a
        /// visible outline, and the base colour is never replaced by feedback.
        /// </summary>
        private void ApplyVisualState()
        {
            if (renderers == null) CacheComponents();
            if (!HasAssignedColor) return;

            Color display = assignedColor;

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer targetRenderer = renderers[i];
                if (targetRenderer == null) continue;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                targetRenderer.GetPropertyBlock(block);
                block.SetColor(BaseColorId, display);
                block.SetColor(ColorId, display);
                targetRenderer.SetPropertyBlock(block);
            }

            bool showOutline = isHighlighted && highlightedAsPickup && CanBePickedUp;
            if (showOutline && !outlinesBuilt) BuildOutlines();

            for (int i = 0; i < outlineRenderers.Count; i++)
            {
                Renderer outline = outlineRenderers[i];
                if (outline == null) continue;

                MaterialPropertyBlock block = new MaterialPropertyBlock();
                outline.GetPropertyBlock(block);
                block.SetColor(BaseColorId, display);
                outline.SetPropertyBlock(block);
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
                shellRenderer.sharedMaterial = OutlineMaterial;
                shellRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                shellRenderer.receiveShadows = false;
                shellRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                shellRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                shellRenderer.enabled = false;

                shell.AddComponent<OutlineShell>();
                outlineRenderers.Add(shellRenderer);
            }
        }

        private static Material OutlineMaterial
        {
            get
            {
                if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

                sharedOutlineMaterial = Resources.Load<Material>("Sim_ObjectOutline");
                if (sharedOutlineMaterial != null) return sharedOutlineMaterial;

                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Unlit/Color");
                sharedOutlineMaterial = new Material(shader);
                sharedOutlineMaterial.name = "Sim_ObjectOutline (Runtime)";
                if (sharedOutlineMaterial.HasProperty(CullId))
                    sharedOutlineMaterial.SetFloat(CullId, 1f);
                return sharedOutlineMaterial;
            }
        }
    }
}
