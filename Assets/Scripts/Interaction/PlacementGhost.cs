using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    public enum PlacementFollowMode
    {
        DropInFrontOfRect = 0,
        DirectlyUnderTarget = 1
    }

    /// <summary>
    /// Single unified 2D floor footprint indicator.
    /// Renders a flat 2D outline flush on the landing surface (square/rectangle for box/cube objects,
    /// circle for spherical/cylindrical objects) with zero floating 3D wireframe or volume shell,
    /// synchronized directly with the object's live position and yaw from the player's isometric POV.
    /// </summary>
    public sealed class PlacementGhost : MonoBehaviour
    {
        [Header("Placement (grid units)")]
        [Tooltip("Distance in front of Rect where the object will be put down in Limited carry mode.")]
        [Min(0.1f)] public float forwardDistance = 1.3f;
        [Min(0f)] public float groundClearance = 0.02f;

        [Header("2D Floor Outline (grid units)")]
        [Min(0.01f)] public float outlineThickness = 0.065f;
        [Min(0.005f)] public float floorSurfaceOffset = 0.024f;
        [Range(0.2f, 1f)] public float outlineAlpha = 0.94f;
        [Range(0f, 0.35f)] public float floorFillAlpha = 0.10f;

        private const int CircleSegments = 48;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly RaycastHit[] RaycastBuffer = new RaycastHit[32];

        private readonly List<GameObject> visualObjects = new List<GameObject>(2);
        private readonly List<Mesh> ownedMeshes = new List<Mesh>(2);
        private readonly List<Material> ownedMaterials = new List<Material>(2);

        private Transform rectTransform;
        private Material fillTemplate;
        private InteractableObject source;
        private PlacementFollowMode followMode = PlacementFollowMode.DropInFrontOfRect;
        private Quaternion targetRotation = Quaternion.identity;
        private Vector3? overrideWorldXZ;
        private Color? accentOverride;
        private Vector3 cachedLocalSize = Vector3.one;
        private bool visible;
        private int lastSyncedFrame = -1;

        public bool Visible => visible;
        public Quaternion TargetRotation => targetRotation;
        public PlacementFollowMode FollowMode => followMode;

        public void Initialize(Transform rect)
        {
            rectTransform = rect;
        }

        public void SetFollowMode(PlacementFollowMode mode)
        {
            followMode = mode;
        }

        public void SetTargetRotation(Quaternion rotation)
        {
            targetRotation = ExtractFlatYaw(rotation);
            if (visible) transform.rotation = targetRotation;
        }

        public void SetOverrideXZ(Vector3? xzWorld)
        {
            overrideWorldXZ = xzWorld;
        }

        public void SetAccentOverride(Color? color)
        {
            accentOverride = color;
        }

        public void ClearOverrides()
        {
            followMode = PlacementFollowMode.DropInFrontOfRect;
            targetRotation = Quaternion.identity;
            overrideWorldXZ = null;
            accentOverride = null;
        }

        public void Show(InteractableObject target)
        {
            Hide();
            if (target == null) return;

            source = target;
            cachedLocalSize = ResolveLocalSize(target);
            BuildFlatFloorIndicator(target, cachedLocalSize);
            visible = true;
            lastSyncedFrame = -1;
            SyncNow();
        }

        public void Hide()
        {
            for (int i = 0; i < visualObjects.Count; i++)
            {
                if (visualObjects[i] != null) Destroy(visualObjects[i]);
            }
            visualObjects.Clear();

            for (int i = 0; i < ownedMeshes.Count; i++)
            {
                if (ownedMeshes[i] != null) Destroy(ownedMeshes[i]);
            }
            ownedMeshes.Clear();

            for (int i = 0; i < ownedMaterials.Count; i++)
            {
                if (ownedMaterials[i] != null) Destroy(ownedMaterials[i]);
            }
            ownedMaterials.Clear();

            source = null;
            visible = false;
            lastSyncedFrame = -1;
        }

        /// <summary>
        /// Immediately synchronizes the 2D floor outline with the current movement and rotation.
        /// Call from ObjectCarryController.LateUpdate / TelekinesisController.LateUpdate for zero-frame lag.
        /// </summary>
        public void SyncNow()
        {
            if (!visible || source == null) return;
            lastSyncedFrame = Time.frameCount;

            Quaternion flatYaw = followMode == PlacementFollowMode.DirectlyUnderTarget
                ? ExtractFlatYaw(source.transform.rotation)
                : ExtractFlatYaw(targetRotation);

            ComputeFloorAnchor(source, flatYaw, out Vector3 floorCenter, out _);
            transform.SetPositionAndRotation(
                new Vector3(floorCenter.x, floorCenter.y + GridSettings.ToWorld(floorSurfaceOffset), floorCenter.z),
                flatYaw);
        }

        private void LateUpdate()
        {
            if (!visible || source == null) return;
            if (lastSyncedFrame == Time.frameCount) return;
            SyncNow();
        }

        /// <summary>
        /// World 3D center where the object's body will rest on the surface below, matching the
        /// exact XZ center of the 2D floor outline.
        /// </summary>
        public Vector3 GetPlacementPosition(InteractableObject target)
        {
            if (target == null) return Vector3.zero;
            Quaternion flatYaw = followMode == PlacementFollowMode.DirectlyUnderTarget
                ? ExtractFlatYaw(target.transform.rotation)
                : ExtractFlatYaw(targetRotation);
            ComputeFloorAnchor(target, flatYaw, out Vector3 floorCenter, out Vector3 localSize);
            float halfY = localSize.y * 0.5f;
            return new Vector3(
                floorCenter.x,
                floorCenter.y + halfY + GridSettings.ToWorld(groundClearance),
                floorCenter.z);
        }

        private void ComputeFloorAnchor(InteractableObject target, Quaternion flatYaw,
            out Vector3 floorPoint, out Vector3 localSize)
        {
            localSize = (target == source && cachedLocalSize.sqrMagnitude > 0.001f)
                ? cachedLocalSize
                : ResolveLocalSize(target);

            float hx = localSize.x * 0.5f;
            float hz = localSize.z * 0.5f;
            float yawRad = flatYaw.eulerAngles.y * Mathf.Deg2Rad;
            float cos = Mathf.Abs(Mathf.Cos(yawRad));
            float sin = Mathf.Abs(Mathf.Sin(yawRad));
            float boundHalfX = cos * hx + sin * hz;
            float boundHalfZ = sin * hx + cos * hz;
            float footprintRadius = Mathf.Max(hx, hz);

            Vector3 probeXZ;
            float probeTopY;

            if (followMode == PlacementFollowMode.DirectlyUnderTarget)
            {
                Vector3 livePos = target.transform.position;
                probeXZ = new Vector3(livePos.x, 0f, livePos.z);
                probeTopY = livePos.y + GridSettings.ToWorld(0.15f);
            }
            else if (overrideWorldXZ.HasValue)
            {
                probeXZ = new Vector3(overrideWorldXZ.Value.x, 0f, overrideWorldXZ.Value.z);
                float refY = target != null ? target.transform.position.y : 0f;
                probeTopY = Mathf.Max(refY, rectTransform != null ? rectTransform.position.y : 0f) + GridSettings.ToWorld(1.65f);
            }
            else
            {
                Vector3 forward = rectTransform != null
                    ? Vector3.ProjectOnPlane(rectTransform.forward, Vector3.up)
                    : Vector3.forward;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 rectPos = rectTransform != null ? rectTransform.position : Vector3.zero;
                float targetDist = GridSettings.ToWorld(forwardDistance);

                // Prevent the drop point from clipping inside or jumping on top of tall walls/pillars.
                Vector3 chestOrigin = rectPos + Vector3.up * GridSettings.ToWorld(0.65f);
                float maxWallCheck = targetDist + footprintRadius;
                int wallHits = Physics.RaycastNonAlloc(chestOrigin, forward, RaycastBuffer, maxWallCheck, ~0, QueryTriggerInteraction.Ignore);
                float nearestWallDist = float.MaxValue;
                for (int i = 0; i < wallHits; i++)
                {
                    RaycastHit hit = RaycastBuffer[i];
                    if (hit.collider == null || ShouldIgnoreSurfaceCollider(hit.collider, target)) continue;
                    // Allow waist-high ledges/platforms that Rect can drop onto; block tall walls/pillars.
                    if (hit.collider.bounds.max.y <= rectPos.y + GridSettings.ToWorld(1.15f)) continue;
                    if (hit.distance < nearestWallDist) nearestWallDist = hit.distance;
                }

                if (nearestWallDist < float.MaxValue)
                {
                    float clampedDist = Mathf.Max(GridSettings.ToWorld(0.45f), nearestWallDist - footprintRadius - GridSettings.ToWorld(0.06f));
                    targetDist = Mathf.Min(targetDist, clampedDist);
                }

                Vector3 dropPos = rectPos + forward * targetDist;
                probeXZ = new Vector3(dropPos.x, 0f, dropPos.z);
                probeTopY = rectPos.y + GridSettings.ToWorld(1.65f);
            }

            Vector3 clampedXZ = WorldBounds.ClampInside(probeXZ, boundHalfX, boundHalfZ);
            float surfaceY = SampleLandingSurfaceY(clampedXZ.x, clampedXZ.z, probeTopY, target);
            floorPoint = new Vector3(clampedXZ.x, surfaceY, clampedXZ.z);
        }

        private float SampleLandingSurfaceY(float worldX, float worldZ, float startY, InteractableObject target)
        {
            Vector3 probeOrigin = new Vector3(worldX, startY, worldZ);
            float maxRayDist = GridSettings.ToWorld(25f);
            int hitCount = Physics.RaycastNonAlloc(probeOrigin, Vector3.down, RaycastBuffer, maxRayDist, ~0, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            float surfaceY = 0f;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = RaycastBuffer[i];
                if (hit.collider == null || hit.distance >= bestDistance) continue;
                if (ShouldIgnoreSurfaceCollider(hit.collider, target)) continue;

                bestDistance = hit.distance;
                surfaceY = hit.point.y;
            }
            return surfaceY;
        }

        private bool ShouldIgnoreSurfaceCollider(Collider col, InteractableObject target)
        {
            if (col == null) return true;
            if (rectTransform != null && (col.transform == rectTransform || col.transform.IsChildOf(rectTransform)))
                return true;
            if (target != null && (col.transform == target.transform || col.transform.IsChildOf(target.transform)))
                return true;

            InteractableObject hitObj = col.GetComponentInParent<InteractableObject>();
            if (hitObj != null && (hitObj == target || hitObj.canBePickedUp))
                return true;

            return false;
        }

        private static Vector3 ResolveLocalSize(InteractableObject target)
        {
            if (target != null && target.TryGetLocalFootprint(out Vector3 localSize))
                return localSize;
            if (target != null && target.TryGetHalfExtents(out Vector3 extents))
                return extents * 2f;
            return Vector3.one * GridSettings.ToWorld(1f);
        }

        private static bool IsCircularFootprint(InteractableObject target)
        {
            if (target == null) return false;
            if (target.isFusedComposite) return false;

            string objName = target.displayName != null ? target.displayName.ToUpperInvariant() : string.Empty;
            if (objName.StartsWith("CORE") || objName.StartsWith("CUBE")) return false;
            if (objName.StartsWith("ORB") || objName.StartsWith("CYL") || objName.StartsWith("BOLA") || objName.StartsWith("SPHERE"))
                return true;

            if (target.GetComponent<SphereCollider>() != null || target.GetComponent<CapsuleCollider>() != null)
                return true;

            Renderer[] baseRenderers = target.GetBaseRenderers();
            if (baseRenderers != null)
            {
                for (int i = 0; i < baseRenderers.Length; i++)
                {
                    Renderer r = baseRenderers[i];
                    if (r == null) continue;
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    string meshName = mf.sharedMesh.name != null ? mf.sharedMesh.name.ToLowerInvariant() : string.Empty;
                    if (meshName.Contains("sphere") || meshName.Contains("cylinder") ||
                        meshName.Contains("octagon") || meshName.Contains("capsule") || meshName.Contains("orb"))
                        return true;
                }
            }

            return false;
        }

        private void BuildFlatFloorIndicator(InteractableObject target, Vector3 localSize)
        {
            Color accent = accentOverride ?? PaletteSettings.Green;

            // Render each welded sub-part's own 2D footprint on the floor.
            FusedSubPart[] subParts = target != null && target.isFusedComposite
                ? target.GetComponentsInChildren<FusedSubPart>(true)
                : null;
            if (subParts != null && subParts.Length >= 2)
            {
                Quaternion invRootRot = Quaternion.Inverse(target.transform.rotation);
                for (int i = 0; i < subParts.Length; i++)
                {
                    FusedSubPart part = subParts[i];
                    if (part == null) continue;

                    Vector3 worldDelta = part.transform.position - target.transform.position;
                    Vector3 localPos = invRootRot * worldDelta;
                    Quaternion relRot = invRootRot * part.transform.rotation;

                    Vector3 partSize = part.partLocalSize.sqrMagnitude > 0.001f
                        ? part.partLocalSize
                        : Vector3.one * GridSettings.ToWorld(0.85f);

                    // A horizontally tilted cylinder projects a rectangle onto the floor plane.
                    bool tiltedHorizontalCylinder = part.shapeKind == FusionShapeKind.Cylinder &&
                                                    Mathf.Abs(Vector3.Dot(relRot * Vector3.up, Vector3.up)) < 0.5f;
                    bool drawCircle = part.isCircularShape && !tiltedHorizontalCylinder;

                    Quaternion localYaw;
                    float pHalfX;
                    float pHalfZ;
                    if (tiltedHorizontalCylinder)
                    {
                        Vector3 barrelAxisLocal = Vector3.ProjectOnPlane(relRot * Vector3.up, Vector3.up);
                        localYaw = barrelAxisLocal.sqrMagnitude > 0.0001f
                            ? Quaternion.LookRotation(barrelAxisLocal.normalized, Vector3.up)
                            : Quaternion.identity;
                        pHalfX = Mathf.Max(0.12f, Mathf.Max(partSize.x, partSize.z) * 0.5f);
                        pHalfZ = Mathf.Max(0.12f, partSize.y * 0.5f);
                    }
                    else
                    {
                        localYaw = ExtractFlatYaw(relRot);
                        pHalfX = Mathf.Max(0.12f, partSize.x * 0.5f);
                        pHalfZ = Mathf.Max(0.12f, partSize.z * 0.5f);
                    }

                    float pStroke = Mathf.Clamp(
                        GridSettings.ToWorld(outlineThickness),
                        GridSettings.ToWorld(0.025f),
                        Mathf.Min(pHalfX, pHalfZ) * 0.45f);

                    Mesh partBorder = drawCircle
                        ? BuildCircleRingMesh(Mathf.Max(pHalfX, pHalfZ), pStroke)
                        : BuildRectFrameMesh(pHalfX, pHalfZ, pStroke);
                    CreateFlatLayer("Floor Outline 2D_" + i, partBorder, accent, outlineAlpha,
                        new Vector3(localPos.x, i * 0.0006f, localPos.z), localYaw);

                    if (floorFillAlpha > 0.005f)
                    {
                        Mesh partFill = drawCircle
                            ? BuildCircleDiscMesh(Mathf.Max(0.04f, Mathf.Max(pHalfX, pHalfZ) - pStroke))
                            : BuildRectDiscMesh(Mathf.Max(0.04f, pHalfX - pStroke), Mathf.Max(0.04f, pHalfZ - pStroke));
                        CreateFlatLayer("Floor Shadow 2D_" + i, partFill, accent, floorFillAlpha,
                            new Vector3(localPos.x, -0.002f + i * 0.0003f, localPos.z), localYaw);
                    }
                }
                return;
            }

            bool circular = IsCircularFootprint(target);
            float halfX = Mathf.Max(0.12f, localSize.x * 0.5f);
            float halfZ = Mathf.Max(0.12f, localSize.z * 0.5f);
            float stroke = Mathf.Clamp(
                GridSettings.ToWorld(outlineThickness),
                GridSettings.ToWorld(0.025f),
                Mathf.Min(halfX, halfZ) * 0.45f);

            Mesh borderMesh = circular
                ? BuildCircleRingMesh(Mathf.Max(halfX, halfZ), stroke)
                : BuildRectFrameMesh(halfX, halfZ, stroke);
            CreateFlatLayer("Floor Outline 2D", borderMesh, accent, outlineAlpha, Vector3.zero, Quaternion.identity);

            if (floorFillAlpha > 0.005f)
            {
                Mesh fillMesh = circular
                    ? BuildCircleDiscMesh(Mathf.Max(0.04f, Mathf.Max(halfX, halfZ) - stroke))
                    : BuildRectDiscMesh(Mathf.Max(0.04f, halfX - stroke), Mathf.Max(0.04f, halfZ - stroke));
                CreateFlatLayer("Floor Shadow 2D", fillMesh, accent, floorFillAlpha, new Vector3(0f, -0.002f, 0f), Quaternion.identity);
            }
        }

        private void CreateFlatLayer(string childName, Mesh mesh, Color tint, float alpha, Vector3 localPos, Quaternion localRot)
        {
            ownedMeshes.Add(mesh);

            GameObject go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;

            Material mat = CreateFlatMaterial(tint, alpha);
            ownedMaterials.Add(mat);

            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            visualObjects.Add(go);
        }

        private Material CreateFlatMaterial(Color tint, float alpha)
        {
            Material mat;
            if (FillTemplate != null)
            {
                mat = new Material(FillTemplate);
            }
            else
            {
                Shader shader = Shader.Find("POLYMULATOR/Placement Ghost Fill");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");
                mat = new Material(shader);
                if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }

            mat.hideFlags = HideFlags.HideAndDontSave;
            Color c = tint;
            c.a = alpha;
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, c);
            return mat;
        }

        private static Mesh BuildRectFrameMesh(float halfX, float halfZ, float stroke)
        {
            float inX = Mathf.Max(0.01f, halfX - stroke);
            float inZ = Mathf.Max(0.01f, halfZ - stroke);

            Vector3[] verts =
            {
                // Outer 0..3 (CW from +Y)
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f,  halfZ),
                new Vector3( halfX, 0f,  halfZ),
                new Vector3( halfX, 0f, -halfZ),
                // Inner 4..7
                new Vector3(-inX, 0f, -inZ),
                new Vector3(-inX, 0f,  inZ),
                new Vector3( inX, 0f,  inZ),
                new Vector3( inX, 0f, -inZ)
            };

            Vector3[] normals = new Vector3[8];
            for (int i = 0; i < 8; i++) normals[i] = Vector3.up;

            int[] tris =
            {
                0, 1, 5,  0, 5, 4,
                1, 2, 6,  1, 6, 5,
                2, 3, 7,  2, 7, 6,
                3, 0, 4,  3, 4, 7
            };

            Mesh mesh = new Mesh { name = "FloorOutline_Rect2D", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildRectDiscMesh(float halfX, float halfZ)
        {
            Vector3[] verts =
            {
                new Vector3(-halfX, 0f, -halfZ),
                new Vector3(-halfX, 0f,  halfZ),
                new Vector3( halfX, 0f,  halfZ),
                new Vector3( halfX, 0f, -halfZ)
            };
            Vector3[] normals = { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            int[] tris = { 0, 1, 2, 0, 2, 3 };

            Mesh mesh = new Mesh { name = "FloorShadow_Rect2D", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCircleRingMesh(float outerRadius, float stroke)
        {
            float innerRadius = Mathf.Max(0.01f, outerRadius - stroke);
            Vector3[] verts = new Vector3[CircleSegments * 2];
            Vector3[] normals = new Vector3[CircleSegments * 2];
            int[] tris = new int[CircleSegments * 6];

            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                float cx = Mathf.Sin(angle);
                float cz = Mathf.Cos(angle);

                verts[i * 2] = new Vector3(cx * outerRadius, 0f, cz * outerRadius);
                verts[i * 2 + 1] = new Vector3(cx * innerRadius, 0f, cz * innerRadius);
                normals[i * 2] = Vector3.up;
                normals[i * 2 + 1] = Vector3.up;

                int next = (i + 1) % CircleSegments;
                int o0 = i * 2;
                int i0 = i * 2 + 1;
                int o1 = next * 2;
                int i1 = next * 2 + 1;

                int t = i * 6;
                tris[t] = o0;
                tris[t + 1] = o1;
                tris[t + 2] = i1;
                tris[t + 3] = o0;
                tris[t + 4] = i1;
                tris[t + 5] = i0;
            }

            Mesh mesh = new Mesh { name = "FloorOutline_Circle2D", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildCircleDiscMesh(float radius)
        {
            Vector3[] verts = new Vector3[CircleSegments + 1];
            Vector3[] normals = new Vector3[CircleSegments + 1];
            int[] tris = new int[CircleSegments * 3];

            verts[0] = Vector3.zero;
            normals[0] = Vector3.up;
            for (int i = 0; i < CircleSegments; i++)
            {
                float angle = (i / (float)CircleSegments) * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
                normals[i + 1] = Vector3.up;

                int next = ((i + 1) % CircleSegments) + 1;
                int t = i * 3;
                tris[t] = 0;
                tris[t + 1] = i + 1;
                tris[t + 2] = next;
            }

            Mesh mesh = new Mesh { name = "FloorShadow_Circle2D", hideFlags = HideFlags.HideAndDontSave };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.triangles = tris;
            mesh.RecalculateBounds();
            return mesh;
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

        private Material FillTemplate
        {
            get
            {
                if (fillTemplate == null)
                    fillTemplate = Resources.Load<Material>("Sim_PlacementGhostFill");
                return fillTemplate;
            }
        }

        private void OnDestroy()
        {
            Hide();
        }
    }
}
