using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.SceneManagement;
using Polymulator;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Builds the Catppuccin Mocha prototype chamber, stylized RECT rig, Anchor socket,
    /// sculptural props, SRP-Batched URP materials, lighting, Volume post-processing,
    /// and UI Toolkit HUD from logical grid units.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string RootName = "POLYMULATOR";
        private const string ScenePath = "Assets/Scenes/Level1.1.unity";
        private const string MaterialFolder = "Assets/Materials";
        private const string ResourcesFolder = "Assets/Resources";
        private const string UIFolder = "Assets/UI";
        private const string SettingsFolder = "Assets/Settings";
        private const string GridSettingsPath = ResourcesFolder + "/GridSettings.asset";
        private const string PaletteSettingsPath = ResourcesFolder + "/PaletteSettings.asset";
        private const string OutlineMaterialPath = ResourcesFolder + "/Sim_ObjectOutline.mat";
        private const string GhostLineMaterialPath = ResourcesFolder + "/Sim_PlacementGhostLine.mat";
        private const string GhostFillMaterialPath = ResourcesFolder + "/Sim_PlacementGhostFill.mat";
        private const string PanelSettingsPath = UIFolder + "/PanelSettings.asset";
        private const string HudUxmlPath = UIFolder + "/SimulationHUD.uxml";
        private const string VolumeProfilePath = SettingsFolder + "/SampleSceneProfile.asset";

        private static float gridUnit = GridSettings.DefaultGridUnitSize;

        private sealed class Mats
        {
            public Material sky, floor, plinth, dark, trimMetal;
            public Material platformLow, platformMid, platformHigh;
            public Material accentLimited, accentFlexible, accentTeal, accentGreen, accentLavender, accentPeach, accentYellow;
            public Material coreGold, objectDark, objectLight, player, playerDark, anchor;
            public Material[] interactivePalette;
        }

        [MenuItem("Tools/POLYMULATOR/Build Prototype Scene")]
        public static void Build()
        {
            var scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded || scene.path != ScenePath)
                scene = EditorSceneManager.OpenScene(ScenePath);

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
            BuildChamber(environment, mats, out WeightedPressurePlate plate);
            Transform anchorTransform = BuildProps(environment, mats);
            ConfigureLighting(environment, mats);
            ConfigureVolume(environment);

            GameObject player = BuildPlayer(root.transform, mats, out ObjectInteractor interactor,
                out ObjectCarryController carry, out TelekinesisController telekinesis,
                out SimulationModeController modeCtrl);

            GameObject cameraObject = BuildCamera(player.transform);
            player.GetComponent<RectController>().cameraTransform = cameraObject.transform;

            AnchorSocketObjective anchorObjective = anchorTransform.GetComponent<AnchorSocketObjective>();
            BuildServicesAndUI(root.transform, player.transform, anchorTransform,
                interactor, carry, telekinesis, modeCtrl, plate, anchorObjective);

            GridSettings.ClearCache();
            PaletteSettings.ClearCache();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, scene.path);
            AssetDatabase.SaveAssets();
            Selection.activeGameObject = player;
            Debug.Log("[POLYMULATOR] Catppuccin Mocha chamber built and saved to " + scene.path +
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
            return settings;
        }

        private static PaletteSettings LoadOrCreatePaletteSettings()
        {
            PaletteSettings settings = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PaletteSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder))
                    AssetDatabase.CreateFolder("Assets", "Resources");

                settings = ScriptableObject.CreateInstance<PaletteSettings>();
                AssetDatabase.CreateAsset(settings, PaletteSettingsPath);
            }

            settings.ApplyCatppuccinMochaDefaults();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            return settings;
        }

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

            material.SetFloat("_Cull", 1f);
            material.SetColor("_BaseColor", PaletteSettings.Text);
            material.SetColor("_Color", PaletteSettings.Text);
            EditorUtility.SetDirty(material);
        }

        private static void EnsurePlacementGhostMaterials()
        {
            Material line = CreateResourceMaterial(GhostLineMaterialPath, "POLYMULATOR/Placement Ghost Line");
            if (line != null)
            {
                line.SetColor("_BaseColor", PaletteSettings.Green);
                line.SetFloat("_DashCount", 6f);
                EditorUtility.SetDirty(line);
            }

            Material fill = CreateResourceMaterial(GhostFillMaterialPath, "POLYMULATOR/Placement Ghost Fill");
            if (fill != null)
            {
                Color tint = PaletteSettings.Green;
                tint.a = 0.08f;
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

            m.sky = SkyMat("Sim_Sky");
            m.floor = ChamberGridMat("Sim_Floor");
            m.plinth = SimSurfaceMat("Sim_Plinth", PaletteSettings.Crust, PaletteSettings.Crust,
                PaletteSettings.Surface0, PaletteSettings.Crust, 0.25f, 0.12f, 0.14f, 0.03f, 0.35f, false);
            m.dark = SimSurfaceMat("Sim_Dark", PaletteSettings.Mantle, PaletteSettings.Crust,
                PaletteSettings.Overlay0, PaletteSettings.Crust, 0.35f, 0.10f, 0.18f, 0.03f, 0.25f, false);
            m.trimMetal = SimSurfaceMat("Sim_TrimMetal", PaletteSettings.Surface1, PaletteSettings.Crust,
                PaletteSettings.Subtext0, PaletteSettings.Crust, 0.56f, 0.32f, 0.26f, 0.04f, 0.28f, false);

            m.platformLow = SimSurfaceMat("Sim_PlatformLow", PaletteSettings.Surface0, PaletteSettings.Mantle,
                PaletteSettings.Sky, PaletteSettings.Crust, 0.36f, 0.06f, 0.18f, 0.03f, 0.24f, true);
            m.platformMid = SimSurfaceMat("Sim_PlatformMid", PaletteSettings.Surface1, PaletteSettings.Mantle,
                PaletteSettings.Sapphire, PaletteSettings.Crust, 0.42f, 0.08f, 0.22f, 0.03f, 0.24f, true);
            m.platformHigh = SimSurfaceMat("Sim_PlatformHigh", PaletteSettings.Surface2, PaletteSettings.Surface0,
                PaletteSettings.Lavender, PaletteSettings.Mantle, 0.46f, 0.10f, 0.26f, 0.03f, 0.24f, true);

            m.accentLimited = AccentMat("Sim_Accent", PaletteSettings.Sapphire, 1.24f);
            m.accentFlexible = AccentMat("Sim_Accent_Flexible", PaletteSettings.Mauve, 1.24f);
            m.accentTeal = AccentMat("Sim_Accent_Teal", PaletteSettings.Teal, 1.22f);
            m.accentGreen = AccentMat("Sim_Accent_Green", PaletteSettings.Green, 1.20f);
            m.accentLavender = AccentMat("Sim_Accent_Lavender", PaletteSettings.Lavender, 1.18f);
            m.accentPeach = AccentMat("Sim_Accent_Peach", PaletteSettings.Peach, 1.18f);
            m.accentYellow = AccentMat("Sim_Accent_Yellow", PaletteSettings.Yellow, 1.26f);

            m.coreGold = SimSurfaceMat("Sim_Core", PaletteSettings.Yellow, PaletteSettings.Peach,
                PaletteSettings.Rosewater, PaletteSettings.Crust, 0.62f, 0.22f, 0.34f, 0.045f, 0.26f, false);
            m.objectDark = SimSurfaceMat("Sim_ObjectDark", PaletteSettings.Overlay1, PaletteSettings.Mantle,
                PaletteSettings.Text, PaletteSettings.Crust, 0.44f, 0.06f, 0.22f, 0.04f, 0.26f, false);
            m.objectLight = SimSurfaceMat("Sim_ObjectLight", PaletteSettings.Overlay2, PaletteSettings.Mantle,
                PaletteSettings.Text, PaletteSettings.Crust, 0.44f, 0.06f, 0.22f, 0.04f, 0.26f, false);

            m.player = SimSurfaceMat("Sim_Player", PaletteSettings.Lavender, PaletteSettings.Surface1,
                PaletteSettings.Text, PaletteSettings.Surface0, 0.54f, 0.12f, 0.30f, 0.04f, 0.26f, false);
            m.playerDark = SimSurfaceMat("Sim_PlayerDark", PaletteSettings.Surface0, PaletteSettings.Crust,
                PaletteSettings.Lavender, PaletteSettings.Crust, 0.44f, 0.22f, 0.20f, 0.04f, 0.28f, false);
            m.anchor = SimSurfaceMat("Sim_Anchor", PaletteSettings.Surface1, PaletteSettings.Crust,
                PaletteSettings.Teal, PaletteSettings.Crust, 0.50f, 0.20f, 0.26f, 0.04f, 0.30f, false);

            Color[] interactive = PaletteSettings.InteractivePalette;
            m.interactivePalette = new Material[interactive.Length];
            for (int i = 0; i < interactive.Length; i++)
            {
                Color c = interactive[i];
                Color rim = Color.Lerp(c, PaletteSettings.Text, 0.55f);
                m.interactivePalette[i] = SimSurfaceMat(
                    "Sim_Object_" + PaletteSettings.InteractiveColorNames[i],
                    c, PaletteSettings.Mantle, rim, PaletteSettings.Crust,
                    0.46f, 0.05f, 0.24f, 0.045f, 0.26f, false);
            }
            return m;
        }

        private static void BuildChamber(Transform env, Mats m, out WeightedPressurePlate pressurePlate)
        {
            // Main playable Chamber Grid floor (top surface exactly at y = 0)
            Primitive("Chamber Floor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f),
                new Vector3(30f, 0.5f, 30f), m.floor, env, true, true);

            // Recessed architectural under-plinth slab framing the diorama in depth
            Primitive("Chamber Under-Plinth", PrimitiveType.Cube, new Vector3(0f, -0.82f, 0f),
                new Vector3(31.8f, 0.9f, 31.8f), m.plinth, env, false, true);

            // Outer backdrop silhouette monoliths strictly on North/East horizons (never occluding camera!)
            BuildHorizonMonolith("Horizon NorthWest", env, m, new Vector3(-9f, -1.5f, 21f), new Vector3(3.2f, 7.5f, 3.2f));
            BuildHorizonMonolith("Horizon NorthEast", env, m, new Vector3(13f, -1.2f, 21.5f), new Vector3(4.0f, 9.2f, 4.0f));
            BuildHorizonMonolith("Horizon EastNorth", env, m, new Vector3(21.5f, -1.4f, 10f), new Vector3(3.5f, 8.0f, 3.5f));
            BuildHorizonMonolith("Horizon EastSouth", env, m, new Vector3(21f, -1.8f, -7.5f), new Vector3(2.8f, 6.4f, 2.8f));

            AddInvisibleBoundary(env);

            // Perimeter dark curbs + HDR glowing Sapphire boundary rails
            Primitive("Curb North", PrimitiveType.Cube, new Vector3(0f, -0.02f, 14.95f),
                new Vector3(30.4f, 0.28f, 0.35f), m.plinth, env, false, true);
            Primitive("Curb South", PrimitiveType.Cube, new Vector3(0f, -0.02f, -14.95f),
                new Vector3(30.4f, 0.28f, 0.35f), m.plinth, env, false, true);
            Primitive("Curb East", PrimitiveType.Cube, new Vector3(14.95f, -0.02f, 0f),
                new Vector3(0.35f, 0.28f, 29.9f), m.plinth, env, false, true);
            Primitive("Curb West", PrimitiveType.Cube, new Vector3(-14.95f, -0.02f, 0f),
                new Vector3(0.35f, 0.28f, 29.9f), m.plinth, env, false, true);

            Primitive("Accent North", PrimitiveType.Cube, new Vector3(0f, 0.03f, 14.3f),
                new Vector3(28.6f, 0.045f, 0.06f), m.accentLimited, env, false, true);
            Primitive("Accent South", PrimitiveType.Cube, new Vector3(0f, 0.03f, -14.3f),
                new Vector3(28.6f, 0.045f, 0.06f), m.accentLimited, env, false, true);
            Primitive("Accent East", PrimitiveType.Cube, new Vector3(14.3f, 0.03f, 0f),
                new Vector3(0.06f, 0.045f, 28.6f), m.accentLimited, env, false, true);
            Primitive("Accent West", PrimitiveType.Cube, new Vector3(-14.3f, 0.03f, 0f),
                new Vector3(0.06f, 0.045f, 28.6f), m.accentLimited, env, false, true);

            // Multi-part corner pylons with stepped bases and glowing HDR energy cores
            float[] corners = { -14.35f, 14.35f };
            int idx = 1;
            foreach (float x in corners)
            {
                foreach (float z in corners)
                {
                    Transform pylon = Group("Corner Pylon " + idx++, env);
                    pylon.localPosition = new Vector3(x, 0f, z) * gridUnit;
                    Primitive("Pylon Base", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f),
                        new Vector3(1.25f, 0.44f, 1.25f), m.plinth, pylon, true, true);
                    Primitive("Pylon Shaft", PrimitiveType.Cube, new Vector3(0f, 1.55f, 0f),
                        new Vector3(0.84f, 2.22f, 0.84f), m.platformLow, pylon, true, true);
                    Primitive("Pylon Collar", PrimitiveType.Cube, new Vector3(0f, 2.72f, 0f),
                        new Vector3(1.02f, 0.14f, 1.02f), m.trimMetal, pylon, false, true);
                    Primitive("Pylon Core", PrimitiveType.Cube, new Vector3(0f, 2.98f, 0f),
                        new Vector3(0.56f, 0.36f, 0.56f), m.accentLimited, pylon, false, false);
                    Primitive("Pylon Crown", PrimitiveType.Cube, new Vector3(0f, 3.23f, 0f),
                        new Vector3(1.06f, 0.14f, 1.06f), m.platformMid, pylon, false, true);
                }
            }

            // Height-graded architectural platforms (Surface0 -> Surface1 -> Surface2)
            BuildTieredPlatform("Platform Low", env, new Vector3(-7f, 0.2f, 2f),
                new Vector3(6f, 0.4f, 5f), m.platformLow, m.plinth, m.accentLimited);
            BuildTieredPlatform("Platform Mid", env, new Vector3(7f, 0.5f, 2.5f),
                new Vector3(5f, 1f, 5f), m.platformMid, m.plinth, m.accentLimited);
            BuildTieredPlatform("Platform High", env, new Vector3(0f, 1f, 11f),
                new Vector3(5f, 2f, 4f), m.platformHigh, m.plinth, m.accentLavender);

            float stairFront = 5.4f;
            for (int i = 0; i < 2; i++)
            {
                float top = 0.4f * (i + 1);
                float depth = 0.8f;
                float z = stairFront + depth * 0.5f;
                Primitive("Stair " + (i + 1), PrimitiveType.Cube, new Vector3(0f, top * 0.5f, z),
                    new Vector3(2.4f, top, depth), m.platformLow, env, true, true);
                Primitive("Stair Lip " + (i + 1), PrimitiveType.Cube,
                    new Vector3(0f, top - 0.03f, z - depth * 0.5f + 0.025f),
                    new Vector3(2.26f, 0.035f, 0.05f), m.accentLimited, env, false, true);
                stairFront += depth;
            }

            // Weighted Conduit Pressure Plate + orthogonal floor circuit to Energy Bridge
            pressurePlate = BuildPressurePlate(env, m, new Vector3(-3.2f, 0f, -1.6f));
            BuildEnergyBridge(env, m, pressurePlate, stairFront);
            BuildConduitCircuit(env, m, pressurePlate);

            // Spawn Pad & Anchor Socket Pad
            Primitive("Spawn Pad Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, -6.5f),
                new Vector3(2.9f, 0.024f, 2.9f), m.plinth, env, false, true);
            Primitive("Spawn Pad Deck", PrimitiveType.Cylinder, new Vector3(0f, 0.028f, -6.5f),
                new Vector3(2.35f, 0.018f, 2.35f), m.platformLow, env, false, true);
            AddSquareOutline("Spawn Outline", env, new Vector3(0f, 0.042f, -6.5f), 1.45f, 0.055f, m.accentLimited);

            Primitive("Anchor Pad Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.012f, 3.5f),
                new Vector3(2.7f, 0.024f, 2.7f), m.plinth, env, false, true);
            Primitive("Anchor Pad Deck", PrimitiveType.Cylinder, new Vector3(0f, 0.028f, 3.5f),
                new Vector3(2.15f, 0.018f, 2.15f), m.platformLow, env, false, true);
            AddSquareOutline("Anchor Outline", env, new Vector3(0f, 0.042f, 3.5f), 1.05f, 0.06f, m.accentTeal);
        }

        private static WeightedPressurePlate BuildPressurePlate(Transform env, Mats m, Vector3 posGrid)
        {
            GameObject plateGo = new GameObject("CONDUIT-PAD-01");
            plateGo.transform.SetParent(env, false);
            plateGo.transform.localPosition = posGrid * gridUnit;

            Primitive("Plate Plinth", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f),
                new Vector3(2.45f, 0.06f, 2.45f), m.plinth, plateGo.transform, true, true);

            GameObject deck = Primitive("Plate Deck", PrimitiveType.Cube, new Vector3(0f, 0.085f, 0f),
                new Vector3(2.05f, 0.06f, 2.05f), m.platformMid, plateGo.transform, true, false);

            GameObject rimN = Primitive("Plate Rim N", PrimitiveType.Cube, new Vector3(0f, 0.095f, 1.08f),
                new Vector3(2.26f, 0.05f, 0.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimS = Primitive("Plate Rim S", PrimitiveType.Cube, new Vector3(0f, 0.095f, -1.08f),
                new Vector3(2.26f, 0.05f, 0.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimE = Primitive("Plate Rim E", PrimitiveType.Cube, new Vector3(1.08f, 0.095f, 0f),
                new Vector3(0.08f, 0.05f, 2.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimW = Primitive("Plate Rim W", PrimitiveType.Cube, new Vector3(-1.08f, 0.095f, 0f),
                new Vector3(0.08f, 0.05f, 2.08f), m.accentPeach, plateGo.transform, false, false);

            GameObject pip1 = Primitive("Weight Pip 1", PrimitiveType.Cube, new Vector3(1.24f, 0.06f, -0.38f),
                new Vector3(0.14f, 0.08f, 0.56f), m.accentPeach, plateGo.transform, false, false);
            GameObject pip2 = Primitive("Weight Pip 2", PrimitiveType.Cube, new Vector3(1.24f, 0.06f, 0.38f),
                new Vector3(0.14f, 0.08f, 0.56f), m.accentPeach, plateGo.transform, false, false);

            GameObject lightGo = new GameObject("Plate Glow Light");
            lightGo.transform.SetParent(plateGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, W(0.45f), 0f);
            Light plateLight = lightGo.AddComponent<Light>();
            plateLight.type = LightType.Point;
            plateLight.color = PaletteSettings.Peach;
            plateLight.range = W(4.2f);
            plateLight.intensity = 1.1f;
            plateLight.shadows = LightShadows.None;

            BoxCollider trigger = plateGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, W(0.35f), 0f);
            trigger.size = new Vector3(W(2.15f), W(0.65f), W(2.15f));

            WeightedPressurePlate plate = plateGo.AddComponent<WeightedPressurePlate>();
            plate.requiredMass = 1.8f;
            plate.movingDeck = deck.transform;
            plate.idleAccentMaterial = m.accentPeach;
            plate.partialAccentMaterial = m.accentYellow;
            plate.activeAccentMaterial = m.accentGreen;
            plate.plateLight = plateLight;
            plate.weightPipRenderers = new[]
            {
                pip1.GetComponent<Renderer>(),
                pip2.GetComponent<Renderer>()
            };
            plate.stateRenderers = new[]
            {
                rimN.GetComponent<Renderer>(),
                rimS.GetComponent<Renderer>(),
                rimE.GetComponent<Renderer>(),
                rimW.GetComponent<Renderer>()
            };
            return plate;
        }

        private static void BuildConduitCircuit(Transform env, Mats m, WeightedPressurePlate triggerPlate)
        {
            GameObject circuitGo = new GameObject("Conduit Floor Circuit");
            circuitGo.transform.SetParent(env, false);

            // Orthogonal floor trace along grid lanes from (-3.2, -0.45) to Energy Bridge west side (-1.1, 7.0)
            Vector4[] spans =
            {
                new Vector4(-3.20f, -0.45f, -3.20f, 1.80f),
                new Vector4(-3.20f,  1.80f, -3.20f, 4.45f),
                new Vector4(-3.20f,  4.45f, -1.95f, 4.45f),
                new Vector4(-1.95f,  4.45f, -1.10f, 4.45f),
                new Vector4(-1.10f,  4.45f, -1.10f, 7.05f)
            };

            Renderer[] segments = new Renderer[spans.Length];
            for (int i = 0; i < spans.Length; i++)
            {
                float x0 = spans[i].x, z0 = spans[i].y;
                float x1 = spans[i].z, z1 = spans[i].w;
                float cx = (x0 + x1) * 0.5f;
                float cz = (z0 + z1) * 0.5f;
                float lenX = Mathf.Abs(x1 - x0);
                float lenZ = Mathf.Abs(z1 - z0);
                bool horiz = lenX > lenZ;
                float sx = horiz ? lenX + 0.14f : 0.14f;
                float sz = horiz ? 0.14f : lenZ + 0.14f;

                Primitive("Circuit Channel " + (i + 1), PrimitiveType.Cube, new Vector3(cx, 0.008f, cz),
                    new Vector3(sx + 0.10f, 0.016f, sz + 0.10f), m.plinth, circuitGo.transform, false, true);

                GameObject trace = Primitive("Circuit Trace " + (i + 1), PrimitiveType.Cube, new Vector3(cx, 0.019f, cz),
                    new Vector3(sx, 0.012f, sz), m.accentPeach, circuitGo.transform, false, false);
                segments[i] = trace.GetComponent<Renderer>();
            }

            ConduitCircuit circuit = circuitGo.AddComponent<ConduitCircuit>();
            circuit.triggerPlate = triggerPlate;
            circuit.traceSegments = segments;
            circuit.dormantMaterial = m.accentPeach;
            circuit.activeMaterial = m.accentTeal;
        }

        private static void BuildEnergyBridge(Transform env, Mats m, WeightedPressurePlate triggerPlate, float stairFront)
        {
            GameObject bridgeGo = new GameObject("Energy Bridge");
            bridgeGo.transform.SetParent(env, false);

            Collider[] colliders = new Collider[2];
            Transform[] segments = new Transform[2];
            Renderer[] holos = new Renderer[2];

            for (int i = 0; i < 2; i++)
            {
                int stepNumber = i + 3;
                float top = 0.4f * stepNumber;
                float depth = i == 1 ? 1.2f : 0.8f;
                float z = stairFront + depth * 0.5f;

                Primitive("Bridge Foundation " + stepNumber, PrimitiveType.Cube, new Vector3(0f, 0.025f, z),
                    new Vector3(2.44f, 0.05f, depth), m.plinth, bridgeGo.transform, false, true);

                GameObject stepGo = Primitive("Bridge Step " + stepNumber, PrimitiveType.Cube,
                    new Vector3(0f, top * 0.5f, z), new Vector3(2.4f, top, depth),
                    m.platformHigh, bridgeGo.transform, true, false);

                // Attach thin glowing lip slightly proud of front top edge to avoid coplanar z-fighting
                GameObject lipGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lipGo.name = "Bridge Lip " + stepNumber;
                lipGo.transform.SetParent(stepGo.transform, false);
                lipGo.transform.localPosition = new Vector3(0f, 0.5f - 0.028f / top, -0.506f - 0.02f / depth);
                lipGo.transform.localScale = new Vector3(0.94f, 0.038f / top, 0.055f / depth);
                Collider lipCol = lipGo.GetComponent<Collider>();
                if (lipCol != null) Object.DestroyImmediate(lipCol);
                lipGo.GetComponent<Renderer>().sharedMaterial = m.accentPeach;

                colliders[i] = stepGo.GetComponent<Collider>();
                segments[i] = stepGo.transform;
                holos[i] = lipGo.GetComponent<Renderer>();
                stairFront += depth;
            }

            EnergyBridge bridge = bridgeGo.AddComponent<EnergyBridge>();
            bridge.triggerPlate = triggerPlate;
            bridge.stepColliders = colliders;
            bridge.animatedSegments = segments;
            bridge.hologramRenderers = holos;
            bridge.dormantMaterial = m.accentPeach;
            bridge.activeMaterial = m.accentTeal;
        }

        private static void BuildTieredPlatform(string name, Transform env, Vector3 centerGrid,
            Vector3 sizeGrid, Material topMat, Material plinthMat, Material trimAccent)
        {
            Primitive(name, PrimitiveType.Cube, centerGrid, sizeGrid, topMat, env, true, true);

            float topY = centerGrid.y + sizeGrid.y * 0.5f;
            float baseH = Mathf.Min(0.14f, sizeGrid.y * 0.35f);
            Primitive(name + " Plinth", PrimitiveType.Cube,
                new Vector3(centerGrid.x, baseH * 0.5f, centerGrid.z),
                new Vector3(sizeGrid.x + 0.14f, baseH, sizeGrid.z + 0.14f), plinthMat, env, false, true);

            float trimY = topY - 0.05f;
            Primitive(name + " Trim S", PrimitiveType.Cube,
                new Vector3(centerGrid.x, trimY, centerGrid.z - sizeGrid.z * 0.5f - 0.015f),
                new Vector3(sizeGrid.x - 0.18f, 0.04f, 0.04f), trimAccent, env, false, true);
            Primitive(name + " Trim W", PrimitiveType.Cube,
                new Vector3(centerGrid.x - sizeGrid.x * 0.5f - 0.015f, trimY, centerGrid.z),
                new Vector3(0.04f, 0.04f, sizeGrid.z - 0.18f), trimAccent, env, false, true);
        }

        private static void BuildHorizonMonolith(string name, Transform env, Mats m, Vector3 posGrid, Vector3 sizeGrid)
        {
            Primitive(name, PrimitiveType.Cube, posGrid, sizeGrid, m.plinth, env, false, true);
            Primitive(name + " Cap", PrimitiveType.Cube,
                new Vector3(posGrid.x, posGrid.y + sizeGrid.y * 0.5f + 0.04f, posGrid.z),
                new Vector3(sizeGrid.x * 0.85f, 0.08f, sizeGrid.z * 0.85f), m.dark, env, false, true);
        }

        private static void AddInvisibleBoundary(Transform parent)
        {
            AddBoundaryCollider("World Boundary North", parent, new Vector3(0f, 1.5f, 14.7f), new Vector3(30f, 8f, 0.6f));
            AddBoundaryCollider("World Boundary South", parent, new Vector3(0f, 1.5f, -14.7f), new Vector3(30f, 8f, 0.6f));
            AddBoundaryCollider("World Boundary East", parent, new Vector3(14.7f, 1.5f, 0f), new Vector3(0.6f, 8f, 30f));
            AddBoundaryCollider("World Boundary West", parent, new Vector3(-14.7f, 1.5f, 0f), new Vector3(0.6f, 8f, 30f));
        }

        private static void AddBoundaryCollider(string name, Transform parent, Vector3 gridPosition, Vector3 gridSize)
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
                new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, true);
            Primitive(prefix + " S", PrimitiveType.Cube, center + new Vector3(0f, 0f, -half),
                new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, true);
            Primitive(prefix + " E", PrimitiveType.Cube, center + new Vector3(half, 0f, 0f),
                new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, true);
            Primitive(prefix + " W", PrimitiveType.Cube, center + new Vector3(-half, 0f, 0f),
                new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, true);
        }

        private static Transform BuildProps(Transform env, Mats m)
        {
            Transform props = Group("Props", env);

            Prop("CUBE-01", PrimitiveType.Cube, new Vector3(-2.5f, 0.6f, -4.5f), new Vector3(1.2f, 1.2f, 1.2f), m.objectDark, m, props, 2f);
            Prop("CUBE-02", PrimitiveType.Cube, new Vector3(2.5f, 0.5f, -3.5f), new Vector3(1f, 1f, 1f), m.objectLight, m, props, 1.5f);
            Prop("CUBE-03", PrimitiveType.Cube, new Vector3(-8f, 1f, 2f), new Vector3(1.2f, 1.2f, 1.2f), m.objectDark, m, props, 2f);
            Prop("CUBE-04", PrimitiveType.Cube, new Vector3(6.5f, 1.5f, 2.5f), new Vector3(1f, 1f, 1f), m.objectLight, m, props, 1.5f);
            Prop("CUBE-05", PrimitiveType.Cube, new Vector3(-1.6f, 2.5f, 11.4f), new Vector3(1f, 1f, 1f), m.objectDark, m, props, 1.5f);
            Prop("CUBE-06", PrimitiveType.Cube, new Vector3(4f, 0.6f, 7f), new Vector3(1.2f, 1.2f, 1.2f), m.objectLight, m, props, 2f);

            Prop("ORB-01", PrimitiveType.Sphere, new Vector3(0f, 0.5f, -2.5f), new Vector3(1f, 1f, 1f), m.objectDark, m, props, 1f);
            Prop("ORB-02", PrimitiveType.Sphere, new Vector3(-5.5f, 0.9f, 3.5f), new Vector3(1f, 1f, 1f), m.objectLight, m, props, 1f);
            Prop("ORB-03", PrimitiveType.Sphere, new Vector3(1.6f, 2.5f, 11.4f), new Vector3(1f, 1f, 1f), m.objectDark, m, props, 1f);
            Prop("ORB-04", PrimitiveType.Sphere, new Vector3(4f, 0.5f, 8f), new Vector3(1f, 1f, 1f), m.objectLight, m, props, 1f);

            Prop("CYL-01", PrimitiveType.Cylinder, new Vector3(9f, 0.5f, -6f), new Vector3(1f, 1f, 1f), m.objectDark, m, props, 1.5f);
            Prop("CYL-02", PrimitiveType.Cylinder, new Vector3(8f, 1.5f, 1f), new Vector3(1f, 1f, 1f), m.objectLight, m, props, 1.5f);

            GameObject anchor = BuildAnchorStation(props, m);

            InteractivePaletteAssigner.AssignAll(PaletteSettings.InteractivePalette, m.interactivePalette);

            // Spawn CORE-01 after InteractivePaletteAssigner so it keeps its signature golden highlight
            GameObject coreGo = Prop("CORE-01", PrimitiveType.Cube, new Vector3(0f, 2.48f, 10.6f),
                new Vector3(0.92f, 0.92f, 0.92f), m.coreGold, m, props, 1.2f);
            CreateUnscaledChildTrim("Core Halo", PrimitiveType.Cylinder, Vector3.zero,
                new Vector3(1.18f, 0.08f, 1.18f), m.accentYellow, coreGo.transform);

            GameObject beaconGo = CreateUnscaledChildTrim("Core Beacon", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, 0f), new Vector3(0.048f, 2.2f, 0.048f), null, coreGo.transform);
            Material beaconMat = CreateResourceMaterial(MaterialFolder + "/Sim_CoreBeacon.mat", "Universal Render Pipeline/Unlit");
            if (beaconMat != null)
            {
                if (beaconMat.HasProperty("_Surface")) beaconMat.SetFloat("_Surface", 1f);
                beaconMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                beaconMat.SetOverrideTag("RenderType", "Transparent");
                beaconMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                if (beaconMat.HasProperty("_SrcBlend")) beaconMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (beaconMat.HasProperty("_DstBlend")) beaconMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (beaconMat.HasProperty("_ZWrite")) beaconMat.SetFloat("_ZWrite", 0f);
                Color bc = PaletteSettings.Yellow * 1.15f;
                bc.a = 0.28f;
                beaconMat.SetColor("_BaseColor", bc);
                EditorUtility.SetDirty(beaconMat);
                beaconGo.GetComponent<Renderer>().sharedMaterial = beaconMat;
            }

            AnchorSocketObjective objective = anchor.GetComponent<AnchorSocketObjective>();
            if (objective != null)
            {
                objective.targetCore = coreGo.GetComponent<InteractableObject>();
                objective.coreRespawnGridPos = new Vector3(0f, 2.48f, 10.6f);
            }

            return anchor.transform;
        }

        private static GameObject BuildAnchorStation(Transform props, Mats m)
        {
            GameObject anchor = new GameObject("ANCHOR-01");
            anchor.transform.SetParent(props, false);
            anchor.transform.localPosition = new Vector3(0f, W(0.72f), W(3.5f));

            BoxCollider col = anchor.AddComponent<BoxCollider>();
            col.size = new Vector3(W(1.4f), W(1.4f), W(1.4f));

            // Pedestal body (all ObjectTrim so Anchor keeps its architectural colors)
            AddTrimPrimitive("Base Pedestal", PrimitiveType.Cylinder, new Vector3(0f, -0.58f, 0f),
                new Vector3(1.55f, 0.24f, 1.55f), m.plinth, anchor.transform);
            AddTrimPrimitive("Receptacle Collar", PrimitiveType.Cylinder, new Vector3(0f, -0.43f, 0f),
                new Vector3(1.24f, 0.12f, 1.24f), m.anchor, anchor.transform);
            AddTrimPrimitive("Receptacle Dish", PrimitiveType.Cylinder, new Vector3(0f, -0.36f, 0f),
                new Vector3(0.86f, 0.04f, 0.86f), m.plinth, anchor.transform);

            float prongDist = 0.56f;
            float[] signs = { -1f, 1f };
            int pIdx = 1;
            foreach (float sx in signs)
            {
                foreach (float sz in signs)
                {
                    AddTrimPrimitive("Prong " + pIdx, PrimitiveType.Cube, new Vector3(sx * prongDist, -0.06f, sz * prongDist),
                        new Vector3(0.20f, 0.72f, 0.20f), m.trimMetal, anchor.transform);
                    AddTrimPrimitive("Prong Tip " + pIdx, PrimitiveType.Cube, new Vector3(sx * prongDist, 0.35f, sz * prongDist),
                        new Vector3(0.15f, 0.10f, 0.15f), m.accentGreen, anchor.transform);
                    pIdx++;
                }
            }

            Transform haloRing = Group("Anchor Outer Halo", anchor.transform);
            haloRing.localPosition = new Vector3(0f, W(-0.24f), 0f);
            AddTrimPrimitive("Halo N", PrimitiveType.Cube, new Vector3(0f, 0f, 0.54f),
                new Vector3(1.12f, 0.03f, 0.045f), m.accentTeal, haloRing);
            AddTrimPrimitive("Halo S", PrimitiveType.Cube, new Vector3(0f, 0f, -0.54f),
                new Vector3(1.12f, 0.03f, 0.045f), m.accentTeal, haloRing);
            AddTrimPrimitive("Halo E", PrimitiveType.Cube, new Vector3(0.54f, 0f, 0f),
                new Vector3(0.045f, 0.03f, 1.08f), m.accentTeal, haloRing);
            AddTrimPrimitive("Halo W", PrimitiveType.Cube, new Vector3(-0.54f, 0f, 0f),
                new Vector3(0.045f, 0.03f, 1.08f), m.accentTeal, haloRing);

            GameObject outerRing = haloRing.gameObject;

            GameObject core = Primitive("Anchor Crystal Core", PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f),
                new Vector3(0.36f, 0.36f, 0.36f), m.accentTeal, anchor.transform, false, false);
            core.transform.localRotation = Quaternion.Euler(45f, 45f, 0f);
            core.AddComponent<ObjectTrim>();

            GameObject lightGo = new GameObject("Anchor Point Light");
            lightGo.transform.SetParent(anchor.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, W(0.45f), 0f);
            Light anchorLight = lightGo.AddComponent<Light>();
            anchorLight.type = LightType.Point;
            anchorLight.color = PaletteSettings.Teal;
            anchorLight.range = W(6.0f);
            anchorLight.intensity = 1.45f;
            anchorLight.shadows = LightShadows.None;

            InteractableObject info = anchor.AddComponent<InteractableObject>();
            info.displayName = "ANCHOR-01";
            info.canBePickedUp = false;
            info.canRotate = false;
            info.canThrow = false;
            info.canTelekinesis = false;
            info.canPartition = false;
            info.canFuse = false;

            AnchorVisualRig rig = anchor.AddComponent<AnchorVisualRig>();
            rig.targetCore = core.transform;
            rig.outerRing = outerRing.transform;
            rig.anchorLight = anchorLight;

            SphereCollider socketTrigger = anchor.AddComponent<SphereCollider>();
            socketTrigger.isTrigger = true;
            socketTrigger.center = new Vector3(0f, W(0.28f), 0f);
            socketTrigger.radius = W(1.35f);

            AnchorSocketObjective objective = anchor.AddComponent<AnchorSocketObjective>();
            objective.visualRig = rig;
            objective.cradleSeatPoint = core.transform;
            return anchor;
        }

        private static GameObject BuildPlayer(Transform parent, Mats m,
            out ObjectInteractor interactor, out ObjectCarryController carry,
            out TelekinesisController telekinesis, out SimulationModeController modeCtrl)
        {
            GameObject player = new GameObject("Rect");
            player.transform.SetParent(parent, false);
            player.transform.localPosition = new Vector3(0f, W(0.05f), W(-6.5f));
            player.tag = "Player";
            player.layer = 2;

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.center = new Vector3(0f, W(0.5f), 0f);
            controller.height = W(1f);
            controller.radius = W(0.4f);
            controller.slopeLimit = 50f;
            controller.stepOffset = W(0.42f);
            controller.skinWidth = W(0.04f);
            controller.minMoveDistance = W(0.001f);

            // Stylized compound Spatial-Bot ("RECT") silhouette
            Transform visualRoot = Group("VisualRoot", player.transform);
            visualRoot.gameObject.layer = 2;

            GameObject hoverChassis = Primitive("Hover Base", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f),
                new Vector3(0.76f, 0.11f, 0.76f), m.playerDark, visualRoot, false, false);
            hoverChassis.layer = 2;

            GameObject hoverGlow = Primitive("Hover Glow Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.025f, 0f),
                new Vector3(0.64f, 0.03f, 0.64f), m.accentLimited, visualRoot, false, false);
            hoverGlow.layer = 2;

            GameObject torso = Primitive("Torso Chassis", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f),
                new Vector3(0.64f, 0.44f, 0.52f), m.player, visualRoot, false, false);
            torso.layer = 2;

            GameObject bolsterL = Primitive("Shoulder Bolster L", PrimitiveType.Cube, new Vector3(-0.34f, 0.40f, 0f),
                new Vector3(0.10f, 0.32f, 0.40f), m.playerDark, visualRoot, false, false);
            bolsterL.layer = 2;

            GameObject bolsterR = Primitive("Shoulder Bolster R", PrimitiveType.Cube, new Vector3(0.34f, 0.40f, 0f),
                new Vector3(0.10f, 0.32f, 0.40f), m.playerDark, visualRoot, false, false);
            bolsterR.layer = 2;

            GameObject chestCore = Primitive("Chest Core", PrimitiveType.Cube, new Vector3(0f, 0.40f, 0.27f),
                new Vector3(0.16f, 0.16f, 0.04f), m.accentLimited, visualRoot, false, false);
            chestCore.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            chestCore.layer = 2;

            GameObject backpack = Primitive("Back Pack", PrimitiveType.Cube, new Vector3(0f, 0.42f, -0.27f),
                new Vector3(0.32f, 0.30f, 0.08f), m.playerDark, visualRoot, false, false);
            backpack.layer = 2;

            GameObject backFin = Primitive("Mode Fin", PrimitiveType.Cube, new Vector3(0f, 0.42f, -0.315f),
                new Vector3(0.08f, 0.22f, 0.03f), m.accentLimited, visualRoot, false, false);
            backFin.layer = 2;

            Transform headPivot = Group("Head Pivot", visualRoot);
            headPivot.gameObject.layer = 2;
            headPivot.localPosition = new Vector3(0f, W(0.78f), 0f);

            GameObject headCowl = Primitive("Head Cowl", PrimitiveType.Cube, new Vector3(0f, 0f, 0f),
                new Vector3(0.56f, 0.34f, 0.48f), m.player, headPivot, false, false);
            headCowl.layer = 2;

            GameObject visorMask = Primitive("Visor Recess", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.225f),
                new Vector3(0.48f, 0.16f, 0.05f), m.playerDark, headPivot, false, false);
            visorMask.layer = 2;

            GameObject visor = Primitive("Optic Visor", PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.245f),
                new Vector3(0.40f, 0.085f, 0.04f), m.accentLimited, headPivot, false, false);
            visor.layer = 2;

            GameObject antennaStem = Primitive("Antenna Stem", PrimitiveType.Cylinder, new Vector3(0.18f, 0.24f, -0.06f),
                new Vector3(0.04f, 0.18f, 0.04f), m.trimMetal, headPivot, false, false);
            antennaStem.layer = 2;

            GameObject antennaTip = Primitive("Antenna Beacon", PrimitiveType.Sphere, new Vector3(0.18f, 0.36f, -0.06f),
                new Vector3(0.11f, 0.11f, 0.11f), m.accentLimited, headPivot, false, false);
            antennaTip.layer = 2;

            GameObject carryAnchor = new GameObject("Carry Anchor");
            carryAnchor.transform.SetParent(player.transform, false);
            carryAnchor.transform.localPosition = new Vector3(0f, W(1.78f), 0f);

            GameObject emitter = new GameObject("Telekinesis Emitter");
            emitter.transform.SetParent(headPivot, false);
            emitter.transform.localPosition = new Vector3(0f, W(0.22f), W(0.12f));

            GameObject origin = new GameObject("Interaction Origin");
            origin.transform.SetParent(player.transform, false);
            origin.transform.localPosition = new Vector3(0f, W(0.55f), W(0.15f));

            player.AddComponent<RectController>();
            modeCtrl = player.AddComponent<SimulationModeController>();
            carry = player.AddComponent<ObjectCarryController>();
            carry.carryAnchor = carryAnchor.transform;

            telekinesis = player.AddComponent<TelekinesisController>();
            telekinesis.beamEmitter = emitter.transform;

            interactor = player.AddComponent<ObjectInteractor>();
            interactor.carryController = carry;
            interactor.telekinesisController = telekinesis;
            interactor.modeController = modeCtrl;
            interactor.interactionOrigin = origin.transform;
            interactor.detectionMask = Physics.DefaultRaycastLayers;

            modeCtrl.carryController = carry;
            modeCtrl.telekinesisController = telekinesis;
            modeCtrl.interactor = interactor;

            RectVisualRig rig = player.AddComponent<RectVisualRig>();
            rig.visualRoot = visualRoot;
            rig.headPivot = headPivot;
            rig.limitedAccentMaterial = m.accentLimited;
            rig.flexibleAccentMaterial = m.accentFlexible;
            rig.modeAccentRenderers = new[]
            {
                hoverGlow.GetComponent<Renderer>(),
                chestCore.GetComponent<Renderer>(),
                backFin.GetComponent<Renderer>(),
                visor.GetComponent<Renderer>(),
                antennaTip.GetComponent<Renderer>()
            };

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
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.backgroundColor = PaletteSettings.Crust;
            camera.orthographic = true;
            camera.orthographicSize = W(3.85f);
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
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                data.antialiasingQuality = AntialiasingQuality.High;
            }

            RectCameraController controller = cameraObject.GetComponent<RectCameraController>();
            if (controller == null) controller = cameraObject.AddComponent<RectCameraController>();
            controller.target = target;
            controller.orthographicSize = 3.85f;
            controller.yaw = 40f;
            controller.pitch = 35f;
            controller.cameraDistance = 25f;
            controller.cameraHeight = 18f;
            controller.focusHeight = 0f;
            controller.followTarget = true;
            controller.followSmoothTime = 0.14f;

            Quaternion rotation = Quaternion.Euler(controller.pitch, controller.yaw, 0f);
            Vector3 horizontal = Quaternion.Euler(0f, controller.yaw, 0f) * Vector3.back * W(controller.cameraDistance);
            Vector3 focus = new Vector3(target.position.x, W(controller.focusHeight), target.position.z);
            cameraObject.transform.SetPositionAndRotation(
                focus + horizontal + Vector3.up * W(controller.cameraHeight), rotation);
            return cameraObject;
        }

        private static void ConfigureLighting(Transform env, Mats m)
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
            sun.color = Color.Lerp(PaletteSettings.Text, PaletteSettings.Rosewater, 0.25f);
            sun.intensity = 1.55f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            lightObject.transform.rotation = Quaternion.Euler(50f, -38f, 0f);
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
            fill.intensity = 0.42f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(26f, 142f, 0f);

            GameObject spawnLightGo = new GameObject("Spawn Pad Pool Light");
            spawnLightGo.transform.SetParent(env, false);
            spawnLightGo.transform.localPosition = new Vector3(0f, W(1.6f), W(-6.5f));
            Light spawnLight = spawnLightGo.AddComponent<Light>();
            spawnLight.type = LightType.Point;
            spawnLight.color = PaletteSettings.Sapphire;
            spawnLight.range = W(5.5f);
            spawnLight.intensity = 1.9f;
            spawnLight.shadows = LightShadows.None;

            RenderSettings.skybox = m.sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = PaletteSettings.Surface1 * 1.15f;
            RenderSettings.ambientEquatorColor = PaletteSettings.Base * 1.05f;
            RenderSettings.ambientGroundColor = PaletteSettings.Mantle * 0.9f;
            RenderSettings.reflectionIntensity = 0.75f;
            RenderSettings.fog = false;
            DynamicGI.UpdateEnvironment();
        }

        private static void ConfigureVolume(Transform env)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if (profile == null)
            {
                if (!AssetDatabase.IsValidFolder(SettingsFolder))
                    AssetDatabase.CreateFolder("Assets", "Settings");
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet(out Tonemapping tonemapping))
                tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.Neutral);

            if (!profile.TryGet(out Bloom bloom))
                bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.98f);
            bloom.intensity.Override(0.28f);
            bloom.scatter.Override(0.50f);
            bloom.highQualityFiltering.Override(true);

            if (!profile.TryGet(out Vignette vignette))
                vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(PaletteSettings.Crust);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.36f);

            if (!profile.TryGet(out ColorAdjustments adjust))
                adjust = profile.Add<ColorAdjustments>(true);
            adjust.active = true;
            adjust.contrast.Override(7f);
            adjust.saturation.Override(6f);

            EditorUtility.SetDirty(profile);

            GameObject volGo = new GameObject("Global PostProcess Volume");
            volGo.transform.SetParent(env, false);
            Volume vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1f;
            vol.sharedProfile = profile;
        }

        private static void BuildServicesAndUI(Transform root, Transform playerTransform, Transform anchorTransform,
            ObjectInteractor interactor, ObjectCarryController carry,
            TelekinesisController telekinesis, SimulationModeController modeCtrl,
            WeightedPressurePlate plate, AnchorSocketObjective anchorObjective)
        {
            GameObject services = new GameObject("Simulation Services");
            services.transform.SetParent(root, false);
            services.AddComponent<SimulationVFX>();
            services.AddComponent<SimulationAudio>();

            SimulationVisualBridge bridge = services.AddComponent<SimulationVisualBridge>();
            bridge.playerTransform = playerTransform;
            bridge.anchorTransform = anchorTransform;
            bridge.modeController = modeCtrl;

            SpatialBlueprintVisualizer blueprint = services.AddComponent<SpatialBlueprintVisualizer>();
            blueprint.modeController = modeCtrl;
            blueprint.interactor = interactor;
            blueprint.carryController = carry;
            blueprint.telekinesisController = telekinesis;
            blueprint.anchorTransform = anchorTransform;

            PanelSettings panelSettings = EnsurePanelSettings();
            VisualTreeAsset uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(HudUxmlPath);

            GameObject hudObject = new GameObject("Simulation HUD");
            hudObject.transform.SetParent(root, false);
            UIDocument doc = hudObject.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            if (uxml != null) doc.visualTreeAsset = uxml;

            SimulationHUDController hudCtrl = hudObject.AddComponent<SimulationHUDController>();
            hudCtrl.modeController = modeCtrl;
            hudCtrl.interactor = interactor;
            hudCtrl.carryController = carry;
            hudCtrl.telekinesisController = telekinesis;
            hudCtrl.pressurePlate = plate;
            hudCtrl.anchorObjective = anchorObjective;
            hudCtrl.blueprintVisualizer = blueprint;
        }

        private static PanelSettings EnsurePanelSettings()
        {
            if (!AssetDatabase.IsValidFolder(UIFolder))
                AssetDatabase.CreateFolder("Assets", "UI");
            string fontsFolder = UIFolder + "/Fonts";
            if (!AssetDatabase.IsValidFolder(fontsFolder))
                AssetDatabase.CreateFolder(UIFolder, "Fonts");

            UnityEngine.TextCore.Text.FontAsset interSemi = EnsureSdfFont(
                "Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-SemiBold.ttf",
                fontsFolder + "/Font_InterSemiBold.asset");
            EnsureSdfFont(
                "Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-Regular.ttf",
                fontsFolder + "/Font_InterRegular.asset");
            EnsureSdfFont(
                "Packages/com.unity.dt.app-ui/PackageResources/Fonts/RobotoMono-Regular.ttf",
                fontsFolder + "/Font_RobotoMono.asset");

            PanelSettings panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                AssetDatabase.CreateAsset(panel, PanelSettingsPath);
            }

            ThemeStyleSheet defaultTheme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(
                "Assets/UI Toolkit/UnityThemes/UnityDefaultRuntimeTheme.tss");
            if (defaultTheme != null) panel.themeStyleSheet = defaultTheme;

            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1920, 1080);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;
            panel.sortingOrder = 10f;

            if (interSemi != null)
            {
                string tsPath = UIFolder + "/PanelTextSettings.asset";
                PanelTextSettings ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(tsPath);
                if (ts == null)
                {
                    ts = ScriptableObject.CreateInstance<PanelTextSettings>();
                    AssetDatabase.CreateAsset(ts, tsPath);
                }
                ts.defaultFontAsset = interSemi;
                EditorUtility.SetDirty(ts);
                panel.textSettings = ts;
            }

            EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(UIFolder + "/SimulationHUD.uss");
            AssetDatabase.ImportAsset(HudUxmlPath);
            return panel;
        }

        private static UnityEngine.TextCore.Text.FontAsset EnsureSdfFont(string ttfPath, string assetPath)
        {
            UnityEngine.TextCore.Text.FontAsset existing =
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(assetPath);
            if (existing != null) return existing;

            Font ttf = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
            if (ttf == null) return null;

            UnityEngine.TextCore.Text.FontAsset fa =
                UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(ttf);
            if (fa == null) return null;

            fa.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(fa, assetPath);
            if (fa.material != null)
            {
                fa.material.name = fa.name + " Material";
                AssetDatabase.AddObjectToAsset(fa.material, assetPath);
            }
            if (fa.atlasTextures != null)
            {
                for (int i = 0; i < fa.atlasTextures.Length; i++)
                {
                    if (fa.atlasTextures[i] != null)
                    {
                        fa.atlasTextures[i].name = fa.name + " Atlas " + i;
                        AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], assetPath);
                    }
                }
            }
            EditorUtility.SetDirty(fa);
            return fa;
        }

        private static void RemoveLegacyFloor()
        {
            var scene = EditorSceneManager.GetActiveScene();
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name == "Floor")
                    Object.DestroyImmediate(go);
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

        private static GameObject AddTrimPrimitive(string name, PrimitiveType type, Vector3 localOffsetUnscaled,
            Vector3 localScale, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localOffsetUnscaled * gridUnit;
            float y = (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule)
                ? localScale.y * 0.5f
                : localScale.y;
            go.transform.localScale = new Vector3(localScale.x, y, localScale.z) * gridUnit;

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            go.AddComponent<ObjectTrim>();
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
            Material material, Mats m, Transform parent, float mass)
        {
            GameObject go = Primitive(name, type, gridPosition, gridSize, material, parent, true, false);
            Rigidbody body = go.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.14f;
            body.angularDamping = 0.08f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Architectural structural trims in local unscaled space (proportional to core shape)
            if (type == PrimitiveType.Cube)
            {
                CreateUnscaledChildTrim("Waist Band", PrimitiveType.Cube, Vector3.zero,
                    new Vector3(1.03f, 0.14f, 1.03f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Orientation Pip", PrimitiveType.Cube, new Vector3(0f, 0f, 0.515f),
                    new Vector3(0.28f, 0.08f, 0.04f), m.accentTeal, go.transform);
            }
            else if (type == PrimitiveType.Sphere)
            {
                CreateUnscaledChildTrim("Gyro Belt", PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(1.03f, 0.09f, 1.03f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Axis Band E", PrimitiveType.Cylinder, Vector3.zero,
                    new Vector3(1.015f, 0.03f, 1.015f), m.accentTeal, go.transform);
            }
            else if (type == PrimitiveType.Cylinder)
            {
                CreateUnscaledChildTrim("Top Collar", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f),
                    new Vector3(1.05f, 0.16f, 1.05f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Base Collar", PrimitiveType.Cylinder, new Vector3(0f, -0.85f, 0f),
                    new Vector3(1.05f, 0.16f, 1.05f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Front Marker", PrimitiveType.Cube, new Vector3(0f, 0f, 0.51f),
                    new Vector3(0.20f, 0.45f, 0.05f), m.accentTeal, go.transform);
            }

            InteractableObject info = go.AddComponent<InteractableObject>();
            info.displayName = name;
            info.canBePickedUp = true;
            go.AddComponent<ImpactAudioEmitter>();
            return go;
        }

        private static GameObject CreateUnscaledChildTrim(string name, PrimitiveType type, Vector3 localPos,
            Vector3 localScale, Material mat, Transform parent)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            float y = (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule)
                ? localScale.y * 0.5f
                : localScale.y;
            go.transform.localScale = new Vector3(localScale.x, y, localScale.z);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            go.AddComponent<ObjectTrim>();
            return go;
        }

        private static Material LoadOrCreateMat(string name, Shader shader)
        {
            string path = MaterialFolder + "/" + name + ".mat";
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
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SkyMat(string name)
        {
            Shader shader = Shader.Find("POLYMULATOR/Mocha Gradient Sky");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat = LoadOrCreateMat(name, shader);
            if (mat.HasProperty("_ZenithColor")) mat.SetColor("_ZenithColor", PaletteSettings.Base);
            if (mat.HasProperty("_HorizonColor")) mat.SetColor("_HorizonColor", PaletteSettings.Surface0);
            if (mat.HasProperty("_NadirColor")) mat.SetColor("_NadirColor", PaletteSettings.Crust);
            if (mat.HasProperty("_GlowColor")) mat.SetColor("_GlowColor", PaletteSettings.Sapphire);
            return mat;
        }

        private static Material ChamberGridMat(string name)
        {
            Shader shader = Shader.Find("POLYMULATOR/Chamber Grid");
            if (shader == null) shader = Shader.Find("POLYMULATOR/Sim Surface");
            Material mat = LoadOrCreateMat(name, shader);
            mat.SetColor("_BaseColor", PaletteSettings.Base);
            if (mat.HasProperty("_TileAltColor"))
                mat.SetColor("_TileAltColor", Color.Lerp(PaletteSettings.Base, PaletteSettings.Mantle, 0.55f));
            if (mat.HasProperty("_MinorLineColor")) mat.SetColor("_MinorLineColor", PaletteSettings.Surface0);
            if (mat.HasProperty("_MajorLineColor"))
                mat.SetColor("_MajorLineColor", Color.Lerp(PaletteSettings.Surface1, PaletteSettings.Sapphire, 0.16f));
            if (mat.HasProperty("_BorderColor")) mat.SetColor("_BorderColor", PaletteSettings.Sapphire);
            if (mat.HasProperty("_PulseColor")) mat.SetColor("_PulseColor", PaletteSettings.Sapphire);
            if (mat.HasProperty("_GridUnitSize")) mat.SetFloat("_GridUnitSize", gridUnit);
            if (mat.HasProperty("_MajorStep")) mat.SetFloat("_MajorStep", 5f);
            if (mat.HasProperty("_BoundaryHalfExtent")) mat.SetFloat("_BoundaryHalfExtent", WorldBounds.InnerHalfExtentGrid);
            if (mat.HasProperty("_EmissionStrength")) mat.SetFloat("_EmissionStrength", 0.65f);
            return mat;
        }

        private static Material SimSurfaceMat(string name, Color baseColor, Color shadeColor, Color rimColor,
            Color seamColor, float smoothness, float metallic, float rimStrength, float bevelWidth,
            float seamStrength, bool worldGridSeam)
        {
            Shader shader = Shader.Find("POLYMULATOR/Sim Surface");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = LoadOrCreateMat(name, shader);
            mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_ShadeColor")) mat.SetColor("_ShadeColor", shadeColor);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", rimColor);
            if (mat.HasProperty("_SeamColor")) mat.SetColor("_SeamColor", seamColor);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", rimStrength);
            if (mat.HasProperty("_BevelWidth")) mat.SetFloat("_BevelWidth", bevelWidth);
            if (mat.HasProperty("_SeamStrength")) mat.SetFloat("_SeamStrength", seamStrength);
            if (mat.HasProperty("_WorldGridSeam")) mat.SetFloat("_WorldGridSeam", worldGridSeam ? 1f : 0f);
            if (mat.HasProperty("_GridUnitSize")) mat.SetFloat("_GridUnitSize", gridUnit);
            return mat;
        }

        private static Material AccentMat(string name, Color color, float hdrBoost)
        {
            Shader shader = Shader.Find("POLYMULATOR/Sim Surface");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = LoadOrCreateMat(name, shader);
            // Keep albedo muted so directional sun doesn't clip HDR emissions to white
            mat.SetColor("_BaseColor", color * 0.24f);
            if (mat.HasProperty("_ShadeColor")) mat.SetColor("_ShadeColor", PaletteSettings.Mantle);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", color);
            if (mat.HasProperty("_SeamColor")) mat.SetColor("_SeamColor", PaletteSettings.Crust);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * hdrBoost);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.55f);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0.05f);
            if (mat.HasProperty("_SeamStrength")) mat.SetFloat("_SeamStrength", 0f);
            if (mat.HasProperty("_RimStrength")) mat.SetFloat("_RimStrength", 0.18f);
            return mat;
        }

        private static float W(float gridUnits) => gridUnits * gridUnit;
    }
}
