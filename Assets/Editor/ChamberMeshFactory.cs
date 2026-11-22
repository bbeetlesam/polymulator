using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Generates and caches sculpted hard-surface 3D meshes (.asset) so blocks, architectural slabs,
    /// pedestals, resonance crystals, robot armor, trusses, and volumetric light shafts read like
    /// tactile studio assets rather than raw Unity primitives.
    /// </summary>
    public static class ChamberMeshFactory
    {
        private const string MeshFolder = "Assets/Models/Generated";

        public static Mesh GetChamferCube() => LoadOrCreateMesh("ChamferCube.asset", () => BuildChamferBoxMesh(0.055f));
        public static Mesh GetChamferSlab() => LoadOrCreateMesh("ChamferSlab.asset", () => BuildChamferBoxMesh(0.028f));
        public static Mesh GetOctagonPrism() => LoadOrCreateMesh("OctagonPrism.asset", BuildOctagonBevelPrism);
        public static Mesh GetFacetedCrystalGem() => LoadOrCreateMesh("FacetedCrystalGem.asset", BuildFacetedCrystalMesh);
        public static Mesh GetTaperedChassis() => LoadOrCreateMesh("TaperedChassis.asset", BuildTaperedChassisMesh);
        public static Mesh GetSteelTruss() => LoadOrCreateMesh("SteelTruss.asset", BuildTrussMesh);
        public static Mesh GetLightShaftCone() => LoadOrCreateMesh("LightShaftCone.asset", BuildLightShaftMesh);

        private static Mesh LoadOrCreateMesh(string fileName, System.Func<Mesh> builder)
        {
            EnsureFolderHierarchy();
            string path = MeshFolder + "/" + fileName;
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Mesh mesh = builder();
            mesh.name = System.IO.Path.GetFileNameWithoutExtension(fileName);
            AssetDatabase.CreateAsset(mesh, path);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void EnsureFolderHierarchy()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(MeshFolder))
                AssetDatabase.CreateFolder("Assets/Models", "Generated");
        }

        private static Mesh BuildChamferBoxMesh(float b)
        {
            float e = 0.5f;
            float i = e - b;

            var verts = new List<Vector3>(144);
            var uvs = new List<Vector2>(144);
            var tris = new List<int>(260);

            void AddQuad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3, Vector2 uv0, Vector2 uv1, Vector2 uv2, Vector2 uv3)
            {
                int start = verts.Count;
                verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
                uvs.Add(uv0); uvs.Add(uv1); uvs.Add(uv2); uvs.Add(uv3);
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
            }

            void AddTri(Vector3 v0, Vector3 v1, Vector3 v2)
            {
                int start = verts.Count;
                verts.Add(v0); verts.Add(v1); verts.Add(v2);
                uvs.Add(new Vector2(0.02f, 0.02f));
                uvs.Add(new Vector2(0.05f, 0.02f));
                uvs.Add(new Vector2(0.02f, 0.05f));
                tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
            }

            Vector2 uvA = new Vector2(0f, 0f);
            Vector2 uvB = new Vector2(0f, 1f);
            Vector2 uvC = new Vector2(1f, 1f);
            Vector2 uvD = new Vector2(1f, 0f);

            AddQuad(new Vector3(-i, e, -i), new Vector3(-i, e, i), new Vector3(i, e, i), new Vector3(i, e, -i), uvA, uvB, uvC, uvD);
            AddQuad(new Vector3(-i, -e, i), new Vector3(-i, -e, -i), new Vector3(i, -e, -i), new Vector3(i, -e, i), uvA, uvB, uvC, uvD);
            AddQuad(new Vector3(i, -i, e), new Vector3(i, i, e), new Vector3(-i, i, e), new Vector3(-i, -i, e), uvA, uvB, uvC, uvD);
            AddQuad(new Vector3(-i, -i, -e), new Vector3(-i, i, -e), new Vector3(i, i, -e), new Vector3(i, -i, -e), uvA, uvB, uvC, uvD);
            AddQuad(new Vector3(e, -i, -i), new Vector3(e, i, -i), new Vector3(e, i, i), new Vector3(e, -i, i), uvA, uvB, uvC, uvD);
            AddQuad(new Vector3(-e, -i, i), new Vector3(-e, i, i), new Vector3(-e, i, -i), new Vector3(-e, -i, -i), uvA, uvB, uvC, uvD);

            Vector2 buA = new Vector2(0.01f, 0.01f);
            Vector2 buB = new Vector2(0.01f, 0.99f);
            Vector2 buC = new Vector2(0.04f, 0.99f);
            Vector2 buD = new Vector2(0.04f, 0.01f);

            AddQuad(new Vector3(-i, e, i), new Vector3(-i, i, e), new Vector3(i, i, e), new Vector3(i, e, i), buA, buB, buC, buD);
            AddQuad(new Vector3(i, e, -i), new Vector3(i, i, -e), new Vector3(-i, i, -e), new Vector3(-i, e, -i), buA, buB, buC, buD);
            AddQuad(new Vector3(i, e, i), new Vector3(e, i, i), new Vector3(e, i, -i), new Vector3(i, e, -i), buA, buB, buC, buD);
            AddQuad(new Vector3(-i, e, -i), new Vector3(-e, i, -i), new Vector3(-e, i, i), new Vector3(-i, e, i), buA, buB, buC, buD);

            AddQuad(new Vector3(i, -e, i), new Vector3(i, -i, e), new Vector3(-i, -i, e), new Vector3(-i, -e, i), buA, buB, buC, buD);
            AddQuad(new Vector3(-i, -e, -i), new Vector3(-i, -i, -e), new Vector3(i, -i, -e), new Vector3(i, -e, -i), buA, buB, buC, buD);
            AddQuad(new Vector3(i, -e, -i), new Vector3(e, -i, -i), new Vector3(e, -i, i), new Vector3(i, -e, i), buA, buB, buC, buD);
            AddQuad(new Vector3(-i, -e, i), new Vector3(-e, -i, i), new Vector3(-e, -i, -i), new Vector3(-i, -e, -i), buA, buB, buC, buD);

            AddQuad(new Vector3(e, -i, i), new Vector3(e, i, i), new Vector3(i, i, e), new Vector3(i, -i, e), buA, buB, buC, buD);
            AddQuad(new Vector3(-i, -i, e), new Vector3(-i, i, e), new Vector3(-e, i, i), new Vector3(-e, -i, i), buA, buB, buC, buD);
            AddQuad(new Vector3(-e, -i, -i), new Vector3(-e, i, -i), new Vector3(-i, i, -e), new Vector3(-i, -i, -e), buA, buB, buC, buD);
            AddQuad(new Vector3(i, -i, -e), new Vector3(i, i, -e), new Vector3(e, i, -i), new Vector3(e, -i, -i), buA, buB, buC, buD);

            AddTri(new Vector3(i, e, i), new Vector3(i, i, e), new Vector3(e, i, i));
            AddTri(new Vector3(-i, e, i), new Vector3(-e, i, i), new Vector3(-i, i, e));
            AddTri(new Vector3(i, e, -i), new Vector3(e, i, -i), new Vector3(i, i, -e));
            AddTri(new Vector3(-i, e, -i), new Vector3(-i, i, -e), new Vector3(-e, i, -i));

            AddTri(new Vector3(i, -e, i), new Vector3(e, -i, i), new Vector3(i, -i, e));
            AddTri(new Vector3(-i, -e, i), new Vector3(-i, -i, e), new Vector3(-e, -i, i));
            AddTri(new Vector3(i, -e, -i), new Vector3(i, -i, -e), new Vector3(e, -i, -i));
            AddTri(new Vector3(-i, -e, -i), new Vector3(-e, -i, -i), new Vector3(-i, -i, -e));

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Machined 12-sided beveled pedestal / canister drum (-1..+1 Y radius 0.5 to match Unity Cylinder scale conventions).
        /// </summary>
        private static Mesh BuildOctagonBevelPrism()
        {
            const int sides = 12;
            const float rOuter = 0.5f;
            const float rInner = 0.44f;
            const float yCap = 1.0f;
            const float yShoulder = 0.88f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uva, Vector2 uvb, Vector2 uvc, Vector2 uvd)
            {
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c); verts.Add(d);
                uvs.Add(uva); uvs.Add(uvb); uvs.Add(uvc); uvs.Add(uvd);
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }

            Vector2 uv0 = new Vector2(0.05f, 0.05f), uv1 = new Vector2(0.05f, 0.95f),
                    uv2 = new Vector2(0.95f, 0.95f), uv3 = new Vector2(0.95f, 0.05f);
            Vector2 bu0 = new Vector2(0.01f, 0.01f), bu1 = new Vector2(0.01f, 0.99f),
                    bu2 = new Vector2(0.04f, 0.99f), bu3 = new Vector2(0.04f, 0.01f);

            for (int k = 0; k < sides; k++)
            {
                float a0 = (k / (float)sides) * Mathf.PI * 2f + Mathf.PI / sides;
                float a1 = ((k + 1) / (float)sides) * Mathf.PI * 2f + Mathf.PI / sides;
                float c0 = Mathf.Cos(a0), s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1), s1 = Mathf.Sin(a1);

                Vector3 outB0 = new Vector3(c0 * rOuter, -yShoulder, s0 * rOuter);
                Vector3 outB1 = new Vector3(c1 * rOuter, -yShoulder, s1 * rOuter);
                Vector3 outT0 = new Vector3(c0 * rOuter, yShoulder, s0 * rOuter);
                Vector3 outT1 = new Vector3(c1 * rOuter, yShoulder, s1 * rOuter);

                Vector3 inT0 = new Vector3(c0 * rInner, yCap, s0 * rInner);
                Vector3 inT1 = new Vector3(c1 * rInner, yCap, s1 * rInner);
                Vector3 inB0 = new Vector3(c0 * rInner, -yCap, s0 * rInner);
                Vector3 inB1 = new Vector3(c1 * rInner, -yCap, s1 * rInner);

                Quad(outB0, outT0, outT1, outB1, uv0, uv1, uv2, uv3);
                Quad(outT0, inT0, inT1, outT1, bu0, bu1, bu2, bu3);
                Quad(inB0, outB0, outB1, inB1, bu0, bu1, bu2, bu3);

                // Top cap (+Y outward)
                int tc = verts.Count;
                verts.Add(new Vector3(0f, yCap, 0f)); verts.Add(inT1); verts.Add(inT0);
                uvs.Add(new Vector2(0.5f, 0.5f));
                uvs.Add(new Vector2(0.5f + c1 * 0.45f, 0.5f + s1 * 0.45f));
                uvs.Add(new Vector2(0.5f + c0 * 0.45f, 0.5f + s0 * 0.45f));
                tris.Add(tc); tris.Add(tc + 1); tris.Add(tc + 2);

                // Bottom cap (-Y outward)
                int bc = verts.Count;
                verts.Add(new Vector3(0f, -yCap, 0f)); verts.Add(inB0); verts.Add(inB1);
                uvs.Add(new Vector2(0.5f, 0.5f));
                uvs.Add(new Vector2(0.5f + c0 * 0.45f, 0.5f + s0 * 0.45f));
                uvs.Add(new Vector2(0.5f + c1 * 0.45f, 0.5f + s1 * 0.45f));
                tris.Add(bc); tris.Add(bc + 1); tris.Add(bc + 2);
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Elongated 8-faceted crystal bi-pyramid with waist belt for Memory Fragments & Anchor Core jewel.
        /// </summary>
        private static Mesh BuildFacetedCrystalMesh()
        {
            const int sides = 8;
            const float radius = 0.44f;
            const float waistY = 0.14f;
            const float apexY = 0.62f;

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            Vector3 topApex = new Vector3(0f, apexY, 0f);
            Vector3 botApex = new Vector3(0f, -apexY, 0f);

            for (int k = 0; k < sides; k++)
            {
                float a0 = (k / (float)sides) * Mathf.PI * 2f;
                float a1 = ((k + 1) / (float)sides) * Mathf.PI * 2f;
                Vector3 t0 = new Vector3(Mathf.Cos(a0) * radius, waistY, Mathf.Sin(a0) * radius);
                Vector3 t1 = new Vector3(Mathf.Cos(a1) * radius, waistY, Mathf.Sin(a1) * radius);
                Vector3 b0 = new Vector3(Mathf.Cos(a0) * radius, -waistY, Mathf.Sin(a0) * radius);
                Vector3 b1 = new Vector3(Mathf.Cos(a1) * radius, -waistY, Mathf.Sin(a1) * radius);

                // Top pyramid facet
                int i0 = verts.Count;
                verts.Add(topApex); verts.Add(t1); verts.Add(t0);
                uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(1f, 0.2f)); uvs.Add(new Vector2(0f, 0.2f));
                tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2);

                // Waist girdle quad
                int i1 = verts.Count;
                verts.Add(b0); verts.Add(t0); verts.Add(t1); verts.Add(b1);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                tris.Add(i1); tris.Add(i1 + 1); tris.Add(i1 + 2);
                tris.Add(i1); tris.Add(i1 + 2); tris.Add(i1 + 3);

                // Bottom pyramid facet
                int i2 = verts.Count;
                verts.Add(botApex); verts.Add(b0); verts.Add(b1);
                uvs.Add(new Vector2(0.5f, 0f)); uvs.Add(new Vector2(0f, 0.8f)); uvs.Add(new Vector2(1f, 0.8f));
                tris.Add(i2); tris.Add(i2 + 1); tris.Add(i2 + 2);
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Angled hard-surface hull (-0.5..+0.5) slightly tapered toward top/front for RECT armor & pylons.
        /// </summary>
        private static Mesh BuildTaperedChassisMesh()
        {
            const float bx = 0.50f, bz = 0.50f;
            const float tx = 0.42f, tz = 0.43f;

            var verts = new List<Vector3>(24);
            var uvs = new List<Vector2>(24);
            var tris = new List<int>(36);

            void Quad(Vector3 v0, Vector3 v1, Vector3 v2, Vector3 v3)
            {
                int s = verts.Count;
                verts.Add(v0); verts.Add(v1); verts.Add(v2); verts.Add(v3);
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0f, 1f));
                uvs.Add(new Vector2(1f, 1f)); uvs.Add(new Vector2(1f, 0f));
                tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);
                tris.Add(s); tris.Add(s + 2); tris.Add(s + 3);
            }

            // +Y, -Y, +Z, -Z, +X, -X
            Quad(new Vector3(-tx, 0.5f, -tz), new Vector3(-tx, 0.5f, tz), new Vector3(tx, 0.5f, tz), new Vector3(tx, 0.5f, -tz));
            Quad(new Vector3(-bx, -0.5f, bz), new Vector3(-bx, -0.5f, -bz), new Vector3(bx, -0.5f, -bz), new Vector3(bx, -0.5f, bz));
            Quad(new Vector3(bx, -0.5f, bz), new Vector3(tx, 0.5f, tz), new Vector3(-tx, 0.5f, tz), new Vector3(-bx, -0.5f, bz));
            Quad(new Vector3(-bx, -0.5f, -bz), new Vector3(-tx, 0.5f, -tz), new Vector3(tx, 0.5f, -tz), new Vector3(bx, -0.5f, -bz));
            Quad(new Vector3(bx, -0.5f, -bz), new Vector3(tx, 0.5f, -tz), new Vector3(tx, 0.5f, tz), new Vector3(bx, -0.5f, bz));
            Quad(new Vector3(-bx, -0.5f, bz), new Vector3(-tx, 0.5f, tz), new Vector3(-tx, 0.5f, -tz), new Vector3(-bx, -0.5f, -bz));

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildTrussMesh()
        {
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            void AddBoxPiece(Vector3 center, Vector3 size)
            {
                Vector3 hs = size * 0.5f;
                Vector3[] p =
                {
                    center + new Vector3(-hs.x, -hs.y, -hs.z), center + new Vector3(hs.x, -hs.y, -hs.z),
                    center + new Vector3(hs.x, hs.y, -hs.z),  center + new Vector3(-hs.x, hs.y, -hs.z),
                    center + new Vector3(-hs.x, -hs.y, hs.z),  center + new Vector3(hs.x, -hs.y, hs.z),
                    center + new Vector3(hs.x, hs.y, hs.z),   center + new Vector3(-hs.x, hs.y, hs.z)
                };
                int[] faceIdx = { 0,3,2,1, 5,6,7,4, 4,7,3,0, 1,2,6,5, 3,7,6,2, 4,0,1,5 };
                for (int f = 0; f < 6; f++)
                {
                    int start = verts.Count;
                    for (int k = 0; k < 4; k++) verts.Add(p[faceIdx[f * 4 + k]]);
                    uvs.Add(Vector2.zero); uvs.Add(Vector2.up); uvs.Add(Vector2.one); uvs.Add(Vector2.right);
                    tris.Add(start); tris.Add(start + 1); tris.Add(start + 2);
                    tris.Add(start); tris.Add(start + 2); tris.Add(start + 3);
                }
            }

            AddBoxPiece(new Vector3(0f, 0.44f, 0f), new Vector3(1f, 0.12f, 1f));
            AddBoxPiece(new Vector3(0f, -0.44f, 0f), new Vector3(1f, 0.12f, 1f));
            AddBoxPiece(new Vector3(-0.42f, 0f, 0f), new Vector3(0.14f, 0.78f, 0.86f));
            AddBoxPiece(new Vector3(0.42f, 0f, 0f), new Vector3(0.14f, 0.78f, 0.86f));
            AddBoxPiece(new Vector3(0f, 0f, 0f), new Vector3(0.76f, 0.14f, 0.34f));

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh BuildLightShaftMesh()
        {
            const int segments = 16;
            const float topRadius = 0.28f;
            const float bottomRadius = 1.0f;
            var verts = new List<Vector3>((segments + 1) * 2);
            var uvs = new List<Vector2>((segments + 1) * 2);
            var tris = new List<int>(segments * 6);

            for (int s = 0; s <= segments; s++)
            {
                float u = (float)s / segments;
                float ang = u * Mathf.PI * 2f;
                verts.Add(new Vector3(Mathf.Cos(ang) * bottomRadius, 0f, Mathf.Sin(ang) * bottomRadius));
                uvs.Add(new Vector2(u, 0f));
                verts.Add(new Vector3(Mathf.Cos(ang) * topRadius, 1f, Mathf.Sin(ang) * topRadius));
                uvs.Add(new Vector2(u, 1f));
            }

            for (int s = 0; s < segments; s++)
            {
                int b0 = s * 2, t0 = b0 + 1, b1 = b0 + 2, t1 = b0 + 3;
                tris.Add(b0); tris.Add(t0); tris.Add(t1);
                tris.Add(b0); tris.Add(t1); tris.Add(b1);
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
