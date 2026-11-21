using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using Polymulator;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Builds the prototype from logical grid units. Every position and size passed
    /// to Primitive/Prop is expressed in grid units and converted through GridSettings.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string RootName = "POLYMULATOR";
        private const string MaterialFolder = "Assets/Materials";
        private const string ResourcesFolder = "Assets/Resources";
        private const string GridSettingsPath = ResourcesFolder + "/GridSettings.asset";
        private const string PaletteSettingsPath = ResourcesFolder + "/PaletteSettings.asset";
        private const string OutlineMaterialPath = ResourcesFolder + "/Sim_ObjectOutline.mat";
        private const string GhostLineMaterialPath = ResourcesFolder + "/Sim_PlacementGhostLine.mat";
        private const string GhostFillMaterialPath = ResourcesFolder + "/Sim_PlacementGhostFill.mat";

        private static float gridUnit = GridSettings.DefaultGridUnitSize;

        private sealed class Mats
        {
            public Material floor, platform, dark, accent, objectDark, objectLight, player, anchor;
            public Material[] interactivePalette;
        }

        [MenuItem("Tools/POLYMULATOR/Build Prototype Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                scene = EditorSceneManager.OpenScene("Assets/Scenes/Level1.1.unity");

            GridSettings settings = LoadOrCreateGridSettings();
            gridUnit = settings.GridUnitSize;
            LoadOrCreatePaletteSettings();
            EnsureOutlineMaterial();
            EnsurePlacementGhostMaterials();

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");

            Mats mats = CreateMaterials();
            RemoveRoot(RootName);
            RemoveLegacyFloor();

            GameObject root = new GameObject(RootName);
            Transform environment = Group("Environment", root.transform);
            BuildChamber(environment, mats);
            BuildProps(environment, mats);
            ConfigureLighting(environment);

            ObjectInteractor interactor;
            ObjectCarryController carry;
            GameObject player = BuildPlayer(root.transform, mats, out interactor, out carry);
            GameObject cameraObject = BuildCamera(player.transform);
            player.GetComponent<RectController>().cameraTransform = cameraObject.transform;
            BuildUI(root.transform, interactor, carry);

            GridSettings.ClearCache();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scene.path);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[POLYMULATOR] Prototype scene built and saved to " + scene.path +
                      " (GRID_UNIT_SIZE = " + gridUnit.ToString("0.###") + ")");
        }

        private static GridSettings LoadOrCreateGridSettings()
        {
            GridSettings settings = AssetDatabase.LoadAssetAtPath<GridSettings>(GridSettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            settings = ScriptableObject.CreateInstance<GridSettings>();
            AssetDatabase.CreateAsset(settings, GridSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[POLYMULATOR] Created " + GridSettingsPath);
            return settings;
        }

        private static PaletteSettings LoadOrCreatePaletteSettings()
        {
            PaletteSettings settings = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PaletteSettingsPath);
            if (settings != null) return settings;

            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                AssetDatabase.CreateFolder("Assets", "Resources");

            settings = ScriptableObject.CreateInstance<PaletteSettings>();
            AssetDatabase.CreateAsset(settings, PaletteSettingsPath);
            AssetDatabase.SaveAssets();
            Debug.Log("[POLYMULATOR] Created " + PaletteSettingsPath);
            return settings;
        }

        /// <summary>
        /// Ensures the shared inverted-hull material exists as an asset, so outline
        /// renderers created at runtime are also included in builds.
        /// </summary>
        private static void EnsureOutlineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(OutlineMaterialPath);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, OutlineMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetFloat("_Cull", 1f); // front-face culling: draw the hull's back faces
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// Keeps the ghost's wireframe and translucent volume materials as assets,
        /// so the runtime-created placement preview also works in builds.
        /// </summary>
        private static void EnsurePlacementGhostMaterials()
        {
            Material line = CreateResourceMaterial(GhostLineMaterialPath,
                "POLYMULATOR/Placement Ghost Line");
            if (line != null)
            {
                line.SetColor("_BaseColor", PaletteSettings.Green);
                line.SetFloat("_DashCount", 6f);
                EditorUtility.SetDirty(line);
            }

            Material fill = CreateResourceMaterial(GhostFillMaterialPath,
                "POLYMULATOR/Placement Ghost Fill");
            if (fill != null)
            {
                Color tint = PaletteSettings.Green;
                tint.a = 0.07f;
                fill.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(fill);
            }
        }

        private static Material CreateResourceMaterial(string path, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null) return null;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                    AssetDatabase.CreateFolder("Assets", "Resources");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Mats CreateMaterials()
        {
            Mats m = new Mats();
            Color emission = PaletteSettings.Sapphire * 0.45f;

            m.floor = Mat("Sim_Floor", PaletteSettings.BaseSurface, 0.22f, 0f);
            m.platform = Mat("Sim_Platform",
                Color.Lerp(PaletteSettings.Crust, PaletteSettings.Overlay2, 0.22f), 0.28f, 0f);
            m.dark = Mat("Sim_Dark", PaletteSettings.Mantle, 0.35f, 0.1f);
            m.accent = Mat("Sim_Accent", PaletteSettings.Sapphire, 0.45f, 0.1f, emission);
            m.objectDark = Mat("Sim_ObjectDark", PaletteSettings.Overlay2, 0.3f, 0.05f);
            m.objectLight = Mat("Sim_ObjectLight", PaletteSettings.Overlay0, 0.28f, 0.05f);
            m.player = Mat("Sim_Player", PaletteSettings.Lavender, 0.45f, 0.1f);
            m.anchor = Mat("Sim_Anchor", PaletteSettings.Overlay1, 0.4f, 0.15f);

            // One shared material per palette colour keeps edit-mode previews correct;
            // runtime highlighting still uses property blocks, so no instances are created.
            Color[] interactive = PaletteSettings.InteractivePalette;
            m.interactivePalette = new Material[interactive.Length];
            for (int i = 0; i < interactive.Length; i++)
            {
                m.interactivePalette[i] = Mat(
                    "Sim_Object_" + PaletteSettings.InteractiveColorNames[i],
                    interactive[i], 0.3f, 0.05f);
            }
            return m;
        }

        private static void BuildChamber(Transform env, Mats m)
        {
            // All dimensions below are logical grid units.
            Primitive("Chamber Floor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f),
                new Vector3(30f, 0.5f, 30f), m.floor, env, true, true);

            // Invisible colliders keep Rect and physics props on the floor without
            // placing tall geometry between the camera and the play area.
            AddInvisibleBoundary(env);

            Primitive("Accent North", PrimitiveType.Cube, new Vector3(0f, 0.04f, 14.3f),
                new Vector3(28.6f, 0.05f, 0.05f), m.accent, env, false, false);
            Primitive("Accent South", PrimitiveType.Cube, new Vector3(0f, 0.04f, -14.3f),
                new Vector3(28.6f, 0.05f, 0.05f), m.accent, env, false, false);
            Primitive("Accent East", PrimitiveType.Cube, new Vector3(14.3f, 0.04f, 0f),
                new Vector3(0.05f, 0.05f, 28.6f), m.accent, env, false, false);
            Primitive("Accent West", PrimitiveType.Cube, new Vector3(-14.3f, 0.04f, 0f),
                new Vector3(0.05f, 0.05f, 28.6f), m.accent, env, false, false);

            float[] corners = { -14.35f, 14.35f };
            foreach (float x in corners)
            {
                foreach (float z in corners)
                {
                    Primitive("Pylon", PrimitiveType.Cube, new Vector3(x, 1.5f, z),
                        new Vector3(0.9f, 3f, 0.9f), m.dark, env, true, true);
                    Primitive("Pylon Cap", PrimitiveType.Cube, new Vector3(x, 3.03f, z),
                        new Vector3(0.95f, 0.06f, 0.95f), m.accent, env, false, false);
                }
            }

            Primitive("Platform Low", PrimitiveType.Cube, new Vector3(-7f, 0.2f, 2f),
                new Vector3(6f, 0.4f, 5f), m.platform, env, true, true);
            Primitive("Platform Mid", PrimitiveType.Cube, new Vector3(7f, 0.5f, 2.5f),
                new Vector3(5f, 1f, 5f), m.platform, env, true, true);
            Primitive("Platform High", PrimitiveType.Cube, new Vector3(0f, 1f, 11f),
                new Vector3(5f, 2f, 4f), m.platform, env, true, true);

            float stairFront = 5.4f;
            for (int i = 0; i < 4; i++)
            {
                float top = 0.4f * (i + 1);
                float depth = i == 3 ? 1.2f : 0.8f;
                float z = stairFront + depth * 0.5f;
                Primitive("Stair " + (i + 1), PrimitiveType.Cube, new Vector3(0f, top * 0.5f, z),
                    new Vector3(2.4f, top, depth), m.platform, env, true, true);
                stairFront += depth;
            }

            Primitive("Spawn Pad", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, -6.5f),
                new Vector3(2.6f, 0.03f, 2.6f), m.dark, env, false, false);
            AddSquareOutline("Spawn Outline", env, new Vector3(0f, 0.04f, -6.5f), 1.35f, 0.06f, m.accent);
            Primitive("Anchor Base", PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 3.5f),
                new Vector3(2.2f, 0.03f, 2.2f), m.dark, env, false, false);
            AddSquareOutline("Anchor Outline", env, new Vector3(0f, 0.04f, 3.5f), 0.95f, 0.06f, m.accent);
        }

        private static void AddInvisibleBoundary(Transform parent)
        {
            AddBoundaryCollider("World Boundary North", parent, new Vector3(0f, 1.5f, 14.7f),
                new Vector3(30f, 8f, 0.6f));
            AddBoundaryCollider("World Boundary South", parent, new Vector3(0f, 1.5f, -14.7f),
                new Vector3(30f, 8f, 0.6f));
            AddBoundaryCollider("World Boundary East", parent, new Vector3(14.7f, 1.5f, 0f),
                new Vector3(0.6f, 8f, 30f));
            AddBoundaryCollider("World Boundary West", parent, new Vector3(-14.7f, 1.5f, 0f),
                new Vector3(0.6f, 8f, 30f));
        }

        private static void AddBoundaryCollider(string name, Transform parent, Vector3 gridPosition,
            Vector3 gridSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = gridPosition * gridUnit;

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.size = gridSize * gridUnit;
            collider.isTrigger = false;
            go.isStatic = true;
        }

        private static void AddSquareOutline(string prefix, Transform parent, Vector3 center, float half,
            float thickness, Material mat)
        {
            Primitive(prefix + " N", PrimitiveType.Cube, center + new Vector3(0f, 0f, half),
                new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, false);
            Primitive(prefix + " S", PrimitiveType.Cube, center + new Vector3(0f, 0f, -half),
                new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, false);
            Primitive(prefix + " E", PrimitiveType.Cube, center + new Vector3(half, 0f, 0f),
                new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, false);
            Primitive(prefix + " W", PrimitiveType.Cube, center + new Vector3(-half, 0f, 0f),
                new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, false);
        }

        private static void BuildProps(Transform env, Mats m)
        {
            Transform props = Group("Props", env);

            // Standard cubes are 1 x 1 x 1 grid units; the 1.2 variants are larger blocks.
            Prop("CUBE-01", PrimitiveType.Cube, new Vector3(-2.5f, 0.6f, -4.5f), new Vector3(1.2f, 1.2f, 1.2f), m.objectDark, props, 2f);
            Prop("CUBE-02", PrimitiveType.Cube, new Vector3(2.5f, 0.5f, -3.5f), new Vector3(1f, 1f, 1f), m.objectLight, props, 1.5f);
            Prop("CUBE-03", PrimitiveType.Cube, new Vector3(-8f, 1f, 2f), new Vector3(1.2f, 1.2f, 1.2f), m.objectDark, props, 2f);
            Prop("CUBE-04", PrimitiveType.Cube, new Vector3(6.5f, 1.5f, 2.5f), new Vector3(1f, 1f, 1f), m.objectLight, props, 1.5f);
            Prop("CUBE-05", PrimitiveType.Cube, new Vector3(-1f, 2.5f, 10.5f), new Vector3(1f, 1f, 1f), m.objectDark, props, 1.5f);
            Prop("CUBE-06", PrimitiveType.Cube, new Vector3(4f, 0.6f, 7f), new Vector3(1.2f, 1.2f, 1.2f), m.objectLight, props, 2f);

            // Spheres use 1 grid unit diameter.
            Prop("ORB-01", PrimitiveType.Sphere, new Vector3(0f, 0.5f, -2.5f), new Vector3(1f, 1f, 1f), m.objectDark, props, 1f);
            Prop("ORB-02", PrimitiveType.Sphere, new Vector3(-5.5f, 0.9f, 3.5f), new Vector3(1f, 1f, 1f), m.objectLight, props, 1f);
            Prop("ORB-03", PrimitiveType.Sphere, new Vector3(1.2f, 2.5f, 11.3f), new Vector3(1f, 1f, 1f), m.objectDark, props, 1f);
            Prop("ORB-04", PrimitiveType.Sphere, new Vector3(4f, 0.5f, 8f), new Vector3(1f, 1f, 1f), m.objectLight, props, 1f);

            // Cylinders use 1 grid unit diameter and height.
            Prop("CYL-01", PrimitiveType.Cylinder, new Vector3(9f, 0.5f, -6f), new Vector3(1f, 1f, 1f), m.objectDark, props, 1.5f);
            Prop("CYL-02", PrimitiveType.Cylinder, new Vector3(8f, 1.5f, 1f), new Vector3(1f, 1f, 1f), m.objectLight, props, 1.5f);

            GameObject anchor = Primitive("ANCHOR-01", PrimitiveType.Cube, new Vector3(0f, 0.72f, 3.5f),
                new Vector3(1.4f, 1.4f, 1.4f), m.anchor, props, true, false);
            InteractableObject info = anchor.AddComponent<InteractableObject>();
            info.displayName = "ANCHOR-01";
            info.canBePickedUp = false;

            // Every object carrying InteractableObject gets a stable palette colour.
            InteractivePaletteAssigner.AssignAll(PaletteSettings.InteractivePalette, m.interactivePalette);
        }

        private static void BuildUI(Transform parent, ObjectInteractor interactor, ObjectCarryController carry)
        {
            GameObject uiObject = new GameObject("Prototype UI");
            uiObject.transform.SetParent(parent, false);
            InteractionUI ui = uiObject.AddComponent<InteractionUI>();
            ui.interactor = interactor;
            ui.carryController = carry;
        }

        private static GameObject BuildPlayer(Transform parent, Mats m, out ObjectInteractor interactor,
            out ObjectCarryController carry)
        {
            GameObject player = new GameObject("Rect");
            player.transform.SetParent(parent, false);
            player.transform.localPosition = new Vector3(0f, W(0.05f), W(-6.5f));
            player.tag = "Player";
            player.layer = 2;

            // One grid unit tall with an approximately 0.8 x 1 x 0.8 footprint.
            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, W(0.5f), 0f);
            controller.height = W(1f);
            controller.radius = W(0.4f);
            controller.slopeLimit = 50f;
            controller.stepOffset = W(0.42f);
            controller.skinWidth = W(0.04f);
            controller.minMoveDistance = W(0.001f);

            GameObject body = Primitive("Body", PrimitiveType.Capsule, new Vector3(0f, 0.5f, 0f),
                new Vector3(0.8f, 1f, 0.8f), m.player, player.transform, false, false);
            body.layer = 2;
            GameObject visor = Primitive("Visor", PrimitiveType.Cube, new Vector3(0f, 0.78f, 0.38f),
                new Vector3(0.42f, 0.1f, 0.12f), m.accent, player.transform, false, false);
            visor.layer = 2;

            GameObject anchor = new GameObject("Carry Anchor");
            anchor.transform.SetParent(player.transform, false);
            // Directly above Rect's head (Rect is 1 grid unit tall); no forward offset.
            anchor.transform.localPosition = new Vector3(0f, W(1.7f), 0f);

            GameObject origin = new GameObject("Interaction Origin");
            origin.transform.SetParent(player.transform, false);
            origin.transform.localPosition = new Vector3(0f, W(0.55f), W(0.15f));

            player.AddComponent<RectController>();
            carry = player.AddComponent<ObjectCarryController>();
            carry.carryAnchor = anchor.transform;
            interactor = player.AddComponent<ObjectInteractor>();
            interactor.carryController = carry;
            interactor.interactionOrigin = origin.transform;
            interactor.detectionMask = Physics.DefaultRaycastLayers;
            return player;
        }

        private static GameObject BuildCamera(Transform target)
        {
            GameObject cameraObject = GameObject.Find("Main Camera");
            if (cameraObject == null)
            {
                cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            Camera camera = cameraObject.GetComponent<Camera>();
            if (camera == null) camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = PaletteSettings.Mantle;
            camera.orthographic = true;
            camera.orthographicSize = W(3f);
            camera.nearClipPlane = W(0.1f);
            camera.farClipPlane = W(250f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.cullingMask = -1;

            UniversalAdditionalCameraData data = camera.GetUniversalAdditionalCameraData();
            if (data != null)
            {
                data.renderPostProcessing = true;
                data.renderType = CameraRenderType.Base;
            }

            RectCameraController controller = cameraObject.GetComponent<RectCameraController>();
            if (controller == null) controller = cameraObject.AddComponent<RectCameraController>();
            controller.target = target;
            controller.orthographicSize = 3f;
            controller.yaw = 40f;
            controller.pitch = 35f;
            controller.cameraDistance = 25f;
            controller.cameraHeight = 18f;
            controller.focusHeight = 0f;
            controller.followTarget = true;
            controller.followSmoothTime = 0.15f;

            // Frame the camera correctly in the editor as well as at runtime.
            Quaternion rotation = Quaternion.Euler(controller.pitch, controller.yaw, 0f);
            Vector3 horizontal = Quaternion.Euler(0f, controller.yaw, 0f) * Vector3.back * W(controller.cameraDistance);
            Vector3 focus = new Vector3(target.position.x, W(controller.focusHeight), target.position.z);
            cameraObject.transform.SetPositionAndRotation(
                focus + horizontal + Vector3.up * W(controller.cameraHeight), rotation);
            return cameraObject;
        }

        private static void ConfigureLighting(Transform env)
        {
            GameObject lightObject = GameObject.Find("Directional Light");
            if (lightObject == null)
            {
                lightObject = new GameObject("Directional Light");
                lightObject.AddComponent<Light>();
            }
            Light sun = lightObject.GetComponent<Light>();
            if (sun == null) sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = PaletteSettings.TextPrimary;
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.72f;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            lightObject.transform.rotation = Quaternion.Euler(52f, -35f, 0f);
            lightObject.transform.position = new Vector3(0f, W(8f), 0f);

            GameObject fillObject = GameObject.Find("Fill Light");
            if (fillObject == null)
            {
                fillObject = new GameObject("Fill Light");
                fillObject.transform.SetParent(env, false);
                fillObject.AddComponent<Light>();
            }
            Light fill = fillObject.GetComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = PaletteSettings.Sapphire;
            fill.intensity = 0.18f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(24f, 140f, 0f);

            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(PaletteSettings.Mantle, PaletteSettings.Overlay2, 0.3f);
            RenderSettings.reflectionIntensity = 0.6f;
            RenderSettings.fog = false;
        }

        private static void RemoveLegacyFloor()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Floor")
                {
                    Object.DestroyImmediate(go);
                    Debug.Log("[POLYMULATOR] Removed legacy default Floor object.");
                }
            }
        }

        private static void RemoveRoot(string name)
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == name) Object.DestroyImmediate(go);
            }
        }

        private static Transform Group(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        /// <summary>
        /// Creates a primitive from logical grid-unit position and size.
        /// Cylinder and capsule sizes describe their final bounds; Unity's source meshes
        /// are 2 units tall, so their Y scale is halved here.
        /// </summary>
        private static GameObject Primitive(string name, PrimitiveType type, Vector3 gridPosition,
            Vector3 gridSize, Material material, Transform parent, bool keepCollider, bool isStatic)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = gridPosition * gridUnit;
            go.transform.localScale = WorldScale(type, gridSize);

            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
            if (!keepCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }
            go.isStatic = isStatic;
            return go;
        }

        private static Vector3 WorldScale(PrimitiveType type, Vector3 gridSize)
        {
            float y = gridSize.y;
            if (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule)
                y *= 0.5f;
            return new Vector3(gridSize.x, y, gridSize.z) * gridUnit;
        }

        private static GameObject Prop(string name, PrimitiveType type, Vector3 gridPosition, Vector3 gridSize,
            Material material, Transform parent, float mass)
        {
            GameObject go = Primitive(name, type, gridPosition, gridSize, material, parent, true, false);
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.12f;
            body.angularDamping = 0.05f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            InteractableObject info = go.AddComponent<InteractableObject>();
            info.displayName = name;
            info.canBePickedUp = true;
            return go;
        }

        private static Material Mat(string name, Color color, float smoothness, float metallic, Color? emission = null)
        {
            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null)
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", color);
            material.SetColor("_Color", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }
            EditorUtility.SetDirty(material);
            return material;
        }

        private static float W(float gridUnits) => gridUnits * gridUnit;
    }
}
