using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Loads sculpted FBX sub-meshes cleanly from Assets/Models/FBX and wraps them into
    /// Catppuccin Mocha SRP-Batched GameObjects without per-object material overhead.
    /// </summary>
    public static class FbxModelLibrary
    {
        public const string FbxFolder = "Assets/Models/FBX/";

        private static readonly Dictionary<string, Dictionary<string, Mesh>> MeshCache =
            new Dictionary<string, Dictionary<string, Mesh>>();

        public static void ClearCache() => MeshCache.Clear();

        public static Mesh LoadSubMesh(string fbxFileName, string meshName)
        {
            string path = FbxFolder + fbxFileName;
            if (!MeshCache.TryGetValue(path, out Dictionary<string, Mesh> map))
            {
                map = new Dictionary<string, Mesh>();
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                if (assets != null)
                {
                    for (int i = 0; i < assets.Length; i++)
                    {
                        if (assets[i] is Mesh mesh && !string.IsNullOrEmpty(mesh.name))
                            map[mesh.name] = mesh;
                    }
                }
                MeshCache[path] = map;
            }

            if (map.TryGetValue(meshName, out Mesh exact)) return exact;
            foreach (var kv in map)
            {
                if (kv.Key.Contains(meshName)) return kv.Value;
            }
            return null;
        }

        public static GameObject CreateSubMeshObject(
            string goName,
            string fbxFileName,
            string subMeshName,
            Material material,
            Transform parent,
            float gridScale = 1f,
            bool isStatic = true,
            int layer = 0)
        {
            Mesh mesh = LoadSubMesh(fbxFileName, subMeshName);
            if (mesh == null) return null;

            GameObject go = new GameObject(goName);
            go.layer = layer;
            go.isStatic = isStatic;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * gridScale;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (material != null) mr.sharedMaterial = material;
            return go;
        }

        public static void InspectAllFbx()
        {
            ClearCache();
            string[] files =
            {
                "RectCharacter.fbx",
                "CoreUnit.fbx",
                "AnchorPedestal.fbx",
                "GrandPiano.fbx",
                "PasarStall.fbx",
                "RusunBalconyUnit.fbx",
                "ArchiveMonolithRack.fbx",
                "FloodTurbinePylon.fbx"
            };
            foreach (string file in files)
            {
                string path = FbxFolder + file;
                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                int meshCount = 0;
                Vector3 totalMin = Vector3.one * 999f;
                Vector3 totalMax = Vector3.one * -999f;
                foreach (Object a in assets)
                {
                    if (a == null) continue;
                    Debug.Log($"[FBX-INSPECT] {file} subasset: {a.GetType().Name} '{a.name}'");
                    if (a is Mesh m)
                    {
                        meshCount++;
                        totalMin = Vector3.Min(totalMin, m.bounds.min);
                        totalMax = Vector3.Max(totalMax, m.bounds.max);
                        Debug.Log($"[FBX-INSPECT] {file} -> Mesh '{m.name}': verts={m.vertexCount}, size={m.bounds.size:F3}, min={m.bounds.min:F3}, max={m.bounds.max:F3}");
                    }
                }
                Vector3 rootScale = root != null ? root.transform.localScale : Vector3.zero;
                int childCount = root != null ? root.transform.childCount : -1;
                Debug.Log($"[FBX-SUMMARY] {file}: totalAssets={assets.Length}, children={childCount}, meshes={meshCount}, rootScale={rootScale:F3}, overallMin={totalMin:F3}, overallMax={totalMax:F3}");
            }
        }
    }
}
