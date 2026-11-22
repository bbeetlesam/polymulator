using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Volumetric placement preview. It draws shape-aware 3D wireframes (box edges,
    /// sphere latitudes/meridians, cylinder rings/ribs) plus a faint translucent
    /// shell, so the indicator reads as a solid in depth instead of a flat
    /// silhouette.
    /// </summary>
    public sealed class PlacementGhost : MonoBehaviour
    {
        [Header("Placement (grid units)")]
        [Tooltip("Distance in front of Rect where the object will be put down.")]
        [Min(0.1f)] public float forwardDistance = 1.3f;
        [Min(0f)] public float groundClearance = 0.02f;

        [Header("Wireframe")]
        [Min(0.005f)] public float lineWidth = 0.035f;
        [Tooltip("Dashes per grid unit of edge length.")]
        [Min(1f)] public float dashCount = 3.5f;
        [Min(0f)] public float backEdgeAlpha = 0.35f;
        [Min(0f)] public float fillAlpha = 0.07f;
        [Range(1, 90)] public float featureAngle = 30f;

        private const int RingSegments = 24;
        private const int MeridianCount = 6;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int DashCountId = Shader.PropertyToID("_DashCount");
        private static readonly int CenterVSId = Shader.PropertyToID("_GhostCenterVS");
        private static readonly int RadiusId = Shader.PropertyToID("_GhostRadius");
        private static readonly int BackAlphaId = Shader.PropertyToID("_GhostBackAlpha");

        private readonly List<GameObject> ghostObjects = new List<GameObject>();
        private Transform rectTransform;
        private Material lineMaterial;
        private Material fillMaterial;
        private InteractableObject source;
        private float radius;
        private bool visible;
        private Quaternion targetRotation = Quaternion.identity;
        private Vector3? overrideWorldXZ;
        private Color? accentOverride;

        public bool Visible => visible;
        public Quaternion TargetRotation => targetRotation;

        private enum GhostShape { Box, Sphere, Cylinder, Capsule, Generic }

        public void Initialize(Transform rect)
        {
            rectTransform = rect;
        }

        public void SetTargetRotation(Quaternion rotation)
        {
            targetRotation = rotation;
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
            targetRotation = Quaternion.identity;
            overrideWorldXZ = null;
            accentOverride = null;
        }

        public void Show(InteractableObject target)
        {
            Hide();
            if (target == null) return;

            source = target;
            BuildPreview(target);
            visible = true;
            UpdatePosition();
            UpdateDepthGlobals();
        }

        public void Hide()
        {
            for (int i = 0; i < ghostObjects.Count; i++)
            {
                if (ghostObjects[i] != null) Destroy(ghostObjects[i]);
            }
            ghostObjects.Clear();
            source = null;
            radius = 0f;
            visible = false;
            Shader.SetGlobalFloat(RadiusId, 0f);
        }

        private void LateUpdate()
        {
            if (!visible || source == null) return;
            UpdatePosition();
            UpdateDepthGlobals();
        }

        private void UpdatePosition()
        {
            transform.position = GetPlacementPosition(source);
            transform.rotation = targetRotation;
        }

        private void UpdateDepthGlobals()
        {
            Camera camera = Camera.main;
            Vector3 centerVS = camera != null
                ? camera.worldToCameraMatrix.MultiplyPoint(transform.position)
                : Vector3.zero;
            Shader.SetGlobalVector(CenterVSId, new Vector4(centerVS.x, centerVS.y, centerVS.z, 0f));
            Shader.SetGlobalFloat(RadiusId, Mathf.Max(0.01f, radius));
            Shader.SetGlobalFloat(BackAlphaId, backEdgeAlpha);
        }

        /// <summary>
        /// World position where the object would be dropped: in front of Rect (or at
        /// override XZ for Flexible telekinesis), resting on the surface below, inside bounds.
        /// </summary>
        public Vector3 GetPlacementPosition(InteractableObject target)
        {
            Vector3 probe;
            if (overrideWorldXZ.HasValue)
            {
                probe = new Vector3(overrideWorldXZ.Value.x, GridSettings.ToWorld(12f), overrideWorldXZ.Value.z);
            }
            else
            {
                Vector3 forward = rectTransform != null
                    ? Vector3.ProjectOnPlane(rectTransform.forward, Vector3.up)
                    : Vector3.forward;
                if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
                forward.Normalize();

                Vector3 origin = rectTransform != null ? rectTransform.position : Vector3.zero;
                probe = origin + forward * GridSettings.ToWorld(forwardDistance);
                probe.y = GridSettings.ToWorld(12f);
            }

            float surfaceY = 0f;
            RaycastHit[] hits = Physics.RaycastAll(probe, Vector3.down, GridSettings.ToWorld(25f), ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null) continue;
                InteractableObject hitObj = hits[i].collider.GetComponentInParent<InteractableObject>();
                if (hitObj != null && (hitObj == target || hitObj.canBePickedUp)) continue;
                surfaceY = hits[i].point.y;
                break;
            }

            Vector3 halfExtents = Vector3.one * 0.5f;
            if (target != null && target.TryGetHalfExtents(out Vector3 extents)) halfExtents = extents;
            radius = Mathf.Max(halfExtents.x, Mathf.Max(halfExtents.y, halfExtents.z));

            Vector3 position = new Vector3(probe.x,
                surfaceY + halfExtents.y + GridSettings.ToWorld(groundClearance), probe.z);
            return WorldBounds.ClampInside(position, halfExtents.x, halfExtents.z);
        }

        private void BuildPreview(InteractableObject target)
        {
            Renderer[] baseRenderers = target.GetBaseRenderers();
            if (baseRenderers == null) return;

            for (int i = 0; i < baseRenderers.Length; i++)
            {
                Renderer sourceRenderer = baseRenderers[i];
                if (sourceRenderer == null) continue;

                MeshFilter filter = sourceRenderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null) continue;

                Mesh mesh = filter.sharedMesh;
                Matrix4x4 relative = target.transform.worldToLocalMatrix *
                    sourceRenderer.transform.localToWorldMatrix;
                GhostShape shape = DetectShape(mesh, target.displayName);

                switch (shape)
                {
                    case GhostShape.Box:
                        AddBoxWire(mesh.bounds, relative);
                        break;
                    case GhostShape.Sphere:
                    case GhostShape.Capsule:
                        AddSphereWire(mesh.bounds, relative, shape == GhostShape.Capsule);
                        break;
                    case GhostShape.Cylinder:
                        AddCylinderWire(mesh.bounds, relative);
                        break;
                    default:
                        List<Vector3> featureEdges = CollectFeatureEdges(mesh, relative, featureAngle);
                        if (featureEdges.Count > 0)
                        {
                            for (int e = 0; e + 1 < featureEdges.Count; e += 2)
                                CreateLine(new[] { featureEdges[e], featureEdges[e + 1] }, false);
                        }
                        else
                        {
                            AddBoxWire(mesh.bounds, relative);
                        }
                        break;
                }

                AddFillShell(mesh, relative);
            }
        }

        private static GhostShape DetectShape(Mesh mesh, string displayName)
        {
            string meshName = mesh.name != null ? mesh.name.ToLowerInvariant() : string.Empty;
            string objectName = displayName != null ? displayName.ToUpperInvariant() : string.Empty;

            if (meshName.Contains("cube") || objectName.StartsWith("CUBE")) return GhostShape.Box;
            if (meshName.Contains("sphere") || objectName.StartsWith("ORB")) return GhostShape.Sphere;
            if (meshName.Contains("cylinder") || objectName.StartsWith("CYL")) return GhostShape.Cylinder;
            if (meshName.Contains("capsule")) return GhostShape.Capsule;
            return GhostShape.Generic;
        }

        private void AddBoxWire(Bounds bounds, Matrix4x4 relative)
        {
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z)
            };

            int[,] pairs =
            {
                { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
                { 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
                { 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 }
            };

            for (int i = 0; i < pairs.GetLength(0); i++)
            {
                Vector3 a = relative.MultiplyPoint3x4(corners[pairs[i, 0]]);
                Vector3 b = relative.MultiplyPoint3x4(corners[pairs[i, 1]]);
                CreateLine(new[] { a, b }, false);
            }
        }

        private void AddSphereWire(Bounds bounds, Matrix4x4 relative, bool capsule)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float horizontalRadius = Mathf.Max(extents.x, extents.z);
            float[] latitudes = capsule
                ? new[] { -0.7f, -0.35f, 0f, 0.35f, 0.7f }
                : new[] { -0.55f, 0f, 0.55f };

            foreach (float lat in latitudes)
            {
                float y = lat * extents.y;
                float ringRadius = horizontalRadius * Mathf.Sqrt(Mathf.Max(0f, 1f - lat * lat));
                List<Vector3> ring = new List<Vector3>(RingSegments);
                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / RingSegments;
                    Vector3 local = center + new Vector3(
                        Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius);
                    ring.Add(relative.MultiplyPoint3x4(local));
                }
                CreateLine(ring, true);
            }

            for (int m = 0; m < MeridianCount; m++)
            {
                float azimuth = m * Mathf.PI / MeridianCount;
                Vector3 direction = new Vector3(Mathf.Cos(azimuth), 0f, Mathf.Sin(azimuth));
                List<Vector3> meridian = new List<Vector3>(RingSegments);
                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / RingSegments;
                    Vector3 local = center + direction * (Mathf.Cos(angle) * horizontalRadius) +
                        Vector3.up * (Mathf.Sin(angle) * extents.y);
                    meridian.Add(relative.MultiplyPoint3x4(local));
                }
                CreateLine(meridian, true);
            }
        }

        private void AddCylinderWire(Bounds bounds, Matrix4x4 relative)
        {
            Vector3 center = bounds.center;
            Vector3 extents = bounds.extents;
            float radiusXZ = Mathf.Max(extents.x, extents.z);

            foreach (float y in new[] { bounds.min.y, bounds.max.y })
            {
                List<Vector3> ring = new List<Vector3>(RingSegments);
                for (int i = 0; i < RingSegments; i++)
                {
                    float angle = i * Mathf.PI * 2f / RingSegments;
                    Vector3 local = new Vector3(
                        center.x + Mathf.Cos(angle) * radiusXZ, y,
                        center.z + Mathf.Sin(angle) * radiusXZ);
                    ring.Add(relative.MultiplyPoint3x4(local));
                }
                CreateLine(ring, true);
            }

            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                Vector3 bottom = relative.MultiplyPoint3x4(new Vector3(
                    center.x + Mathf.Cos(angle) * radiusXZ, bounds.min.y,
                    center.z + Mathf.Sin(angle) * radiusXZ));
                Vector3 top = relative.MultiplyPoint3x4(new Vector3(
                    center.x + Mathf.Cos(angle) * radiusXZ, bounds.max.y,
                    center.z + Mathf.Sin(angle) * radiusXZ));
                CreateLine(new[] { bottom, top }, false);
            }
        }

        private void CreateLine(IList<Vector3> points, bool closed)
        {
            if (points == null || points.Count < 2) return;

            GameObject lineObject = new GameObject("Placement Wire");
            lineObject.transform.SetParent(transform, false);

            LineRenderer line = lineObject.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = points.Count + (closed ? 1 : 0);
            for (int i = 0; i < points.Count; i++) line.SetPosition(i, points[i]);
            if (closed) line.SetPosition(points.Count, points[0]);

            float worldWidth = GridSettings.ToWorld(lineWidth);
            line.startWidth = worldWidth;
            line.endWidth = worldWidth;
            line.numCornerVertices = 0;
            line.numCapVertices = 0;
            line.sharedMaterial = LineMaterial;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            line.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            float length = 0f;
            for (int i = 1; i < line.positionCount; i++)
                length += Vector3.Distance(line.GetPosition(i - 1), line.GetPosition(i));

            Color wireTint = accentOverride ?? PaletteSettings.Green;
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, wireTint);
            block.SetFloat(DashCountId, Mathf.Max(1f, dashCount * length / GridSettings.ToWorld(1f)));
            line.SetPropertyBlock(block);

            ghostObjects.Add(lineObject);
        }

        private void AddFillShell(Mesh mesh, Matrix4x4 relative)
        {
            DecomposeTRS(relative, out Vector3 position, out Quaternion rotation, out Vector3 scale);

            GameObject fill = new GameObject("Placement Volume");
            fill.transform.SetParent(transform, false);
            fill.transform.localPosition = position;
            fill.transform.localRotation = rotation;
            fill.transform.localScale = scale;

            MeshFilter filter = fill.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = fill.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = FillMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

            MaterialPropertyBlock block = new MaterialPropertyBlock();
            Color color = accentOverride ?? PaletteSettings.Green;
            color.a = fillAlpha;
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);

            ghostObjects.Add(fill);
        }

        private static void DecomposeTRS(Matrix4x4 matrix, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            position = matrix.GetColumn(3);
            Vector3 column0 = matrix.GetColumn(0);
            Vector3 column1 = matrix.GetColumn(1);
            Vector3 column2 = matrix.GetColumn(2);
            scale = new Vector3(column0.magnitude, column1.magnitude, column2.magnitude);

            Matrix4x4 rotationOnly = new Matrix4x4(
                column0.normalized, column1.normalized, column2.normalized,
                new Vector4(0f, 0f, 0f, 1f));
            rotation = rotationOnly.rotation;
        }

        private static List<Vector3> CollectFeatureEdges(Mesh mesh, Matrix4x4 relative, float angleThreshold)
        {
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            Dictionary<string, Edge> edges = new Dictionary<string, Edge>();

            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 a = vertices[triangles[i]];
                Vector3 b = vertices[triangles[i + 1]];
                Vector3 c = vertices[triangles[i + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a);
                if (normal.sqrMagnitude < 1e-10f) continue;
                normal.Normalize();

                AddEdge(edges, a, b, normal);
                AddEdge(edges, b, c, normal);
                AddEdge(edges, c, a, normal);
            }

            List<Vector3> result = new List<Vector3>();
            foreach (Edge edge in edges.Values)
            {
                bool keep = edge.normals.Count <= 1;
                for (int i = 1; !keep && i < edge.normals.Count; i++)
                {
                    if (Vector3.Angle(edge.normals[0], edge.normals[i]) >= angleThreshold) keep = true;
                }
                if (!keep) continue;

                result.Add(relative.MultiplyPoint3x4(edge.a));
                result.Add(relative.MultiplyPoint3x4(edge.b));
            }
            return result;
        }

        private static void AddEdge(Dictionary<string, Edge> edges, Vector3 a, Vector3 b, Vector3 normal)
        {
            string key = EdgeKey(a, b);
            if (!edges.TryGetValue(key, out Edge edge))
            {
                edge = new Edge { a = a, b = b };
                edges.Add(key, edge);
            }
            edge.normals.Add(normal);
        }

        private static string EdgeKey(Vector3 a, Vector3 b)
        {
            string first = PointKey(a);
            string second = PointKey(b);
            return string.CompareOrdinal(first, second) < 0
                ? first + "|" + second
                : second + "|" + first;
        }

        private static string PointKey(Vector3 point)
        {
            return Mathf.RoundToInt(point.x * 10000f) + "," +
                   Mathf.RoundToInt(point.y * 10000f) + "," +
                   Mathf.RoundToInt(point.z * 10000f);
        }

        private sealed class Edge
        {
            public Vector3 a;
            public Vector3 b;
            public readonly List<Vector3> normals = new List<Vector3>();
        }

        private Material LineMaterial
        {
            get
            {
                if (lineMaterial == null)
                    lineMaterial = Resources.Load<Material>("Sim_PlacementGhostLine");
                return lineMaterial;
            }
        }

        private Material FillMaterial
        {
            get
            {
                if (fillMaterial == null)
                    fillMaterial = Resources.Load<Material>("Sim_PlacementGhostFill");
                return fillMaterial;
            }
        }
    }
}
