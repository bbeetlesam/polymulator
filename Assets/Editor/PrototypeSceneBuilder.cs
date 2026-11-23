using System.Collections.Generic;
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
    /// Builds individual or full campaign Catppuccin Mocha POLYMULATOR chambers, stylized RECT rig,
    /// Anchor directional sockets, dynamic Energy Bridges & Barriers, Memory Fragments,
    /// SRP-Batched URP materials, lighting, Volume post-processing, and LevelInfo assets.
    /// </summary>
    public static class PrototypeSceneBuilder
    {
        private const string RootName = "POLYMULATOR";
        private const string MainMenuScenePath = "Assets/Scenes/MainMenu.unity";
        private const string DefaultScenePath = "Assets/Scenes/Level1.1.unity";
        private const string MaterialFolder = "Assets/Materials";
        private const string ResourcesFolder = "Assets/Resources";
        private const string LevelsResourcesFolder = ResourcesFolder + "/Levels";
        private const string UIFolder = "Assets/UI";
        private const string SettingsFolder = "Assets/Settings";
        private const string GridSettingsPath = ResourcesFolder + "/GridSettings.asset";
        private const string PaletteSettingsPath = ResourcesFolder + "/PaletteSettings.asset";
        private const string OutlineMaterialPath = ResourcesFolder + "/Sim_ObjectOutline.mat";
        private const string RimOverlayMaterialPath = ResourcesFolder + "/Sim_ObjectRimOverlay.mat";
        private const string ParticleGlowMaterialPath = ResourcesFolder + "/Sim_ParticleGlow.mat";
        private const string GhostLineMaterialPath = ResourcesFolder + "/Sim_PlacementGhostLine.mat";
        private const string GhostFillMaterialPath = ResourcesFolder + "/Sim_PlacementGhostFill.mat";
        private const string PanelSettingsPath = UIFolder + "/PanelSettings.asset";
        private const string HudUxmlPath = UIFolder + "/SimulationHUD.uxml";
        private const string MainMenuUxmlPath = UIFolder + "/MainMenuHUD.uxml";
        private const string VolumeProfilePath = SettingsFolder + "/SampleSceneProfile.asset";

        private static float gridUnit = GridSettings.DefaultGridUnitSize;

        private sealed class Mats
        {
            public Material sky, floor, plinth, dark, trimMetal;
            public Material platformLow, platformMid, platformHigh;
            public Material accentLimited, accentFlexible, accentTeal, accentGreen;
            public Material accentLavender, accentPeach, accentYellow, accentMaroon, accentRosewater;
            public Material coreGold, coreBeacon, barrierMat, fragmentMat;
            public Material objectDark, objectLight, player, playerDark, anchor;
            public Material[] interactivePalette;
        }

        private static bool EnsureEditMode()
        {
            if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode) return true;
            EditorApplication.isPlaying = false;
            Debug.LogWarning("[POLYMULATOR] Play Mode stopped — run menu builder once back in Edit Mode.");
            return false;
        }

        [MenuItem("Tools/POLYMULATOR/Build Prototype Scene")]
        public static void Build()
        {
            if (!EnsureEditMode()) return;
            List<CampaignLevelCatalog.LevelSpec> catalog = CampaignLevelCatalog.BuildCatalog();
            var active = EditorSceneManager.GetActiveScene();
            CampaignLevelCatalog.LevelSpec target = null;
            if (active.IsValid())
            {
                for (int i = 0; i < catalog.Count; i++)
                {
                    if (catalog[i].ScenePath == active.path)
                    {
                        target = catalog[i];
                        break;
                    }
                }
            }
            if (target == null) target = catalog[0];

            Mats mats = PrepareSharedAssets();
            BuildSingleCampaignScene(target, mats);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/POLYMULATOR/Build MainMenu Diorama")]
        public static void BuildMainMenu()
        {
            if (!EnsureEditMode()) return;
            Mats mats = PrepareSharedAssets();
            BuildMainMenuScene(mats);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Tools/POLYMULATOR/Build All Campaign Scenes")]
        public static void BuildAllCampaignScenes()
        {
            if (!EnsureEditMode()) return;
            FbxModelLibrary.ClearCache();
            List<CampaignLevelCatalog.LevelSpec> catalog = CampaignLevelCatalog.BuildCatalog();
            Mats mats = PrepareSharedAssets();

            for (int i = 0; i < catalog.Count; i++)
                BuildSingleCampaignScene(catalog[i], mats);

            BuildMainMenuScene(mats);
            UpdateEditorBuildSettings(catalog);

            if (System.IO.File.Exists(MainMenuScenePath))
                EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);

            AssetDatabase.SaveAssets();
            Debug.Log("[POLYMULATOR] Built all " + catalog.Count + " campaign chambers + MainMenu diorama & updated EditorBuildSettings.");
        }

        private static void BuildMainMenuScene(Mats m)
        {
            UnityEngine.SceneManagement.Scene scene;
            if (System.IO.File.Exists(MainMenuScenePath))
                scene = EditorSceneManager.OpenScene(MainMenuScenePath, OpenSceneMode.Single);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            foreach (GameObject oldRoot in scene.GetRootGameObjects())
            {
                if (oldRoot.name != "Main Camera" && oldRoot.name != "Directional Light")
                    Object.DestroyImmediate(oldRoot);
            }

            GameObject root = new GameObject(RootName);
            Transform env = Group("Diorama Environment", root.transform);
            ChamberArchitectureBuilder.BuildMainMenuShowcase(
                env, m.floor, m.plinth, m.platformLow, m.dark, m.trimMetal, m.accentTeal, m.accentLimited, gridUnit);

            // Hero CORE-01 unit inside MainMenu diorama (CoreUnit.fbx scaled 1.15x rests flush at floor)
            Transform menuCorePivot = new GameObject("Diorama CoreUnit").transform;
            menuCorePivot.SetParent(env, false);
            menuCorePivot.localPosition = new Vector3(-2.6f, W(0.51f), 1.5f);
            menuCorePivot.localRotation = Quaternion.Euler(0f, 18f, 0f);
            FbxModelLibrary.CreateSubMeshObject("Core Inner", "CoreUnit.fbx", "Core_InnerChamber", m.interactivePalette[2], menuCorePivot, W(1.15f), true);
            FbxModelLibrary.CreateSubMeshObject("Core Cage Frame", "CoreUnit.fbx", "Core_CageFrame", m.trimMetal, menuCorePivot, W(1.15f), true);
            FbxModelLibrary.CreateSubMeshObject("Core Front Visor", "CoreUnit.fbx", "Core_FrontVisor", m.accentLimited, menuCorePivot, W(1.15f), true);

            // Decorative pulsing ANCHOR-01 pedestal + faceted crystal gem
            var anchorSpec = new CampaignLevelCatalog.LevelSpec
            {
                anchorGrid = new Vector3(2.4f, 0.0f, 2.2f),
                requireFacingAngle = false
            };
            GameObject showcaseAnchor = BuildAnchorStation(env, m, anchorSpec);
            Object.DestroyImmediate(showcaseAnchor.GetComponent<AnchorSocketObjective>());
            Object.DestroyImmediate(showcaseAnchor.GetComponent<SphereCollider>());
            Object.DestroyImmediate(showcaseAnchor.GetComponent<BoxCollider>());
            Object.DestroyImmediate(showcaseAnchor.GetComponent<InteractableObject>());

            // Decorative idling RECT protagonist (no player input script on MainMenu)
            GameObject showcaseBot = BuildPlayer(root.transform, m, new Vector3(-0.7f, 0f, -0.5f), out _, out _, out _, out var modeCtrl);
            showcaseBot.name = "Showcase Rect";
            showcaseBot.transform.localRotation = Quaternion.Euler(0f, 28f, 0f);
            Object.DestroyImmediate(showcaseBot.GetComponent<ObjectInteractor>());
            Object.DestroyImmediate(showcaseBot.GetComponent<TelekinesisController>());
            Object.DestroyImmediate(showcaseBot.GetComponent<ObjectCarryController>());
            Object.DestroyImmediate(modeCtrl);
            Object.DestroyImmediate(showcaseBot.GetComponent<RectController>());
            Object.DestroyImmediate(showcaseBot.GetComponent<CharacterController>());

            ConfigureLighting(env, m, CampaignLevelCatalog.ChapterTheme.Translasi);
            ConfigureVolume(env);

            GameObject camGo = GameObject.Find("Main Camera");
            if (camGo == null)
            {
                camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.backgroundColor = PaletteSettings.Crust;
            cam.cullingMask = -1;
            cam.orthographic = false;
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.2f;
            cam.farClipPlane = 180f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var camData = cam.GetUniversalAdditionalCameraData();
            if (camData != null)
            {
                camData.renderPostProcessing = true;
                camData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                camData.antialiasingQuality = AntialiasingQuality.High;
            }
            var oldRectCam = camGo.GetComponent<RectCameraController>();
            if (oldRectCam != null) Object.DestroyImmediate(oldRectCam);
            MainMenuCameraOrbit orbit = camGo.GetComponent<MainMenuCameraOrbit>();
            if (orbit == null) orbit = camGo.AddComponent<MainMenuCameraOrbit>();
            orbit.focusPoint = new Vector3(-1.45f, 0.85f, 1.95f) * gridUnit;
            orbit.rectFocusPoint = showcaseBot.transform.position + Vector3.up * W(0.70f);
            orbit.baseYaw = 32f;
            orbit.basePitch = 18f;
            orbit.orbitDistance = 9.2f;
            Quaternion initRot = Quaternion.Euler(orbit.basePitch, orbit.baseYaw, 0f);
            camGo.transform.SetPositionAndRotation(orbit.focusPoint + initRot * (Vector3.back * orbit.orbitDistance), initRot);

            GameObject services = new GameObject("Showcase VFX Services");
            services.transform.SetParent(root.transform, false);
            SimulationVisualBridge vBridge = services.AddComponent<SimulationVisualBridge>();
            vBridge.playerTransform = showcaseBot.transform;
            vBridge.anchorTransform = showcaseAnchor.transform;
            services.AddComponent<SimulationVFX>();

            PanelSettings panelSettings = EnsurePanelSettings();
            AssetDatabase.ImportAsset(UIFolder + "/MainMenuHUD.uss");
            AssetDatabase.ImportAsset(MainMenuUxmlPath);
            VisualTreeAsset menuUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MainMenuUxmlPath);

            GameObject menuUiGo = new GameObject("MainMenu UI");
            menuUiGo.transform.SetParent(root.transform, false);
            UIDocument doc = menuUiGo.AddComponent<UIDocument>();
            doc.panelSettings = panelSettings;
            if (menuUxml != null) doc.visualTreeAsset = menuUxml;
            menuUiGo.AddComponent<MainMenuController>();

            GridSettings.ClearCache();
            PaletteSettings.ClearCache();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MainMenuScenePath);
            Debug.Log("[POLYMULATOR] Built MainMenu 3D showcase + UI Toolkit scene.");
        }

        private static Mats PrepareSharedAssets()
        {
            GridSettings settings = LoadOrCreateGridSettings();
            gridUnit = settings.GridUnitSize;
            LoadOrCreatePaletteSettings();
            EnsureOutlineMaterial();
            EnsurePlacementGhostMaterials();

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
                AssetDatabase.CreateFolder("Assets", "Materials");
            if (!AssetDatabase.IsValidFolder(LevelsResourcesFolder))
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
                AssetDatabase.CreateFolder(ResourcesFolder, "Levels");
            }
            return CreateMaterials();
        }

        private static void BuildSingleCampaignScene(CampaignLevelCatalog.LevelSpec spec, Mats mats)
        {
            UnityEngine.SceneManagement.Scene scene;
            if (System.IO.File.Exists(spec.ScenePath))
                scene = EditorSceneManager.OpenScene(spec.ScenePath, OpenSceneMode.Single);
            else
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RemoveRoot(RootName);
            RemoveLegacyFloor();

            GameObject root = new GameObject(RootName);
            Transform environment = Group("Environment", root.transform);
            BuildChamber(environment, mats, spec, out WeightedPressurePlate primaryPlate);
            Transform anchorTransform = BuildPropsAndObjectives(environment, mats, spec);
            ConfigureLighting(environment, mats, spec.theme);
            ConfigureVolume(environment);

            GameObject player = BuildPlayer(root.transform, mats, spec.playerSpawnGrid,
                out ObjectInteractor interactor, out ObjectCarryController carry,
                out TelekinesisController telekinesis, out SimulationModeController modeCtrl);

            GameObject cameraObject = BuildCamera(player.transform);
            player.GetComponent<RectController>().cameraTransform = cameraObject.transform;

            AnchorSocketObjective anchorObjective = anchorTransform.GetComponent<AnchorSocketObjective>();
            if (anchorObjective != null && anchorObjective.targetCore != null &&
                anchorObjective.targetCore.TryGetComponent<Rigidbody>(out Rigidbody coreRb))
            {
                coreRb.mass = Mathf.Max(0.8f, spec.coreMass);
            }

            if (spec.isEndingChamber)
            {
                if (anchorObjective != null)
                {
                    Object.DestroyImmediate(anchorObjective);
                    anchorObjective = null;
                }
                BuildEndingChoiceRig(environment, mats, anchorTransform, primaryPlate);
            }

            BuildServicesAndUI(root.transform, player.transform, anchorTransform,
                interactor, carry, telekinesis, modeCtrl, primaryPlate, anchorObjective);

            SaveLevelInfoAsset(spec);

            GridSettings.ClearCache();
            PaletteSettings.ClearCache();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, spec.ScenePath);
            Debug.Log("[POLYMULATOR] Built " + spec.ScenePath + " (" + spec.title + ")");
        }

        private static void BuildEndingChoiceRig(Transform env, Mats m, Transform northAnchor, WeightedPressurePlate relayPlate)
        {
            GameObject rigGo = new GameObject("Ending Choice Controller");
            rigGo.transform.SetParent(env, false);

            EndingChoiceController choiceCtrl = rigGo.AddComponent<EndingChoiceController>();
            choiceCtrl.archivistPartitionPlate = relayPlate;
            choiceCtrl.archivistPos = northAnchor.position;

            // North station: PEMANCAR WARGA (Archivist — Partition [1] pathway)
            Primitive("Pemancar Spire", PrimitiveType.Cylinder, new Vector3(0f, 1.35f, 8.2f),
                new Vector3(0.18f, 2.7f, 0.18f), m.trimMetal, rigGo.transform, false, true);
            Primitive("Pemancar Beacon", PrimitiveType.Sphere, new Vector3(0f, 2.78f, 8.2f),
                new Vector3(0.32f, 0.32f, 0.32f), m.accentTeal, rigGo.transform, false, false);

            SphereCollider northZone = northAnchor.gameObject.AddComponent<SphereCollider>();
            northZone.isTrigger = true;
            northZone.center = new Vector3(0f, W(0.55f), 0f);
            northZone.radius = W(1.35f);
            EndingSocketTrigger northTrig = northAnchor.gameObject.AddComponent<EndingSocketTrigger>();
            northTrig.controller = choiceCtrl;
            northTrig.path = EndingPath.Archivist;

            // East station: TANGGUL UTARA (Restoration pathway)
            Vector3 eastGrid = new Vector3(6.6f, 0f, 3.2f);
            BuildChoicePedestal(rigGo.transform, m, choiceCtrl, EndingPath.Restoration,
                "TANGGUL UTARA", eastGrid, m.accentLimited, PaletteSettings.Sapphire);
            choiceCtrl.restorationPos = eastGrid * gridUnit;

            // West station: BRANKAS ARSIP (Preservation pathway)
            Vector3 westGrid = new Vector3(-6.6f, 0f, 3.2f);
            BuildChoicePedestal(rigGo.transform, m, choiceCtrl, EndingPath.Preservation,
                "BRANKAS ARSIP", westGrid, m.accentLavender, PaletteSettings.Lavender);
            choiceCtrl.preservationPos = westGrid * gridUnit;

            // Clean guiding floor paths radiating from Plaza Center (0, 2.2) toward the 3 pedestals
            BuildChoiceFloorGuide(rigGo.transform, m.plinth, m.accentLimited, new Vector2(0.8f, 2.2f), new Vector2(5.4f, 3.2f), "Path East");
            BuildChoiceFloorGuide(rigGo.transform, m.plinth, m.accentLavender, new Vector2(-0.8f, 2.2f), new Vector2(-5.4f, 3.2f), "Path West");
            BuildChoiceFloorGuide(rigGo.transform, m.plinth, m.accentTeal, new Vector2(0f, 3.4f), new Vector2(0f, 6.0f), "Path North");
        }

        private static void BuildChoicePedestal(Transform parent, Mats m, EndingChoiceController ctrl,
            EndingPath path, string label, Vector3 gridPos, Material accentMat, Color lightColor)
        {
            GameObject station = new GameObject("CHOICE-" + label);
            station.transform.SetParent(parent, false);
            station.transform.localPosition = gridPos * gridUnit;

            Primitive("Base Plinth", PrimitiveType.Cylinder, new Vector3(0f, 0.08f, 0f), new Vector3(2.3f, 0.16f, 2.3f), m.plinth, station.transform, true, true);
            Primitive("Mid Ring", PrimitiveType.Cylinder, new Vector3(0f, 0.19f, 0f), new Vector3(1.65f, 0.08f, 1.65f), m.anchor, station.transform, true, true);
            Primitive("Accent Rim", PrimitiveType.Cylinder, new Vector3(0f, 0.245f, 0f), new Vector3(1.26f, 0.04f, 1.26f), accentMat, station.transform, false, false);
            Primitive("Lock Pylon L", PrimitiveType.Cube, new Vector3(-0.92f, 0.72f, 0.55f), new Vector3(0.22f, 1.25f, 0.22f), m.trimMetal, station.transform, false, true);
            Primitive("Lock Pylon R", PrimitiveType.Cube, new Vector3(0.92f, 0.72f, 0.55f), new Vector3(0.22f, 1.25f, 0.22f), m.trimMetal, station.transform, false, true);
            Primitive("Choice Beacon", PrimitiveType.Cube, new Vector3(0f, 1.38f, 0.55f), new Vector3(1.88f, 0.12f, 0.18f), accentMat, station.transform, false, false);

            GameObject lightGo = new GameObject("Choice Light");
            lightGo.transform.SetParent(station.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, W(1.1f), 0f);
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = lightColor;
            l.range = W(4.6f);
            l.intensity = 1.65f;
            l.shadows = LightShadows.None;

            SphereCollider trigger = station.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, W(0.55f), 0f);
            trigger.radius = W(1.35f);

            EndingSocketTrigger trig = station.AddComponent<EndingSocketTrigger>();
            trig.controller = ctrl;
            trig.path = path;
        }

        private static void BuildChoiceFloorGuide(Transform parent, Material borderMat, Material traceMat,
            Vector2 fromGrid, Vector2 toGrid, string name)
        {
            Vector2 mid = (fromGrid + toGrid) * 0.5f;
            Vector2 delta = toGrid - fromGrid;
            float len = delta.magnitude;
            if (len < 0.1f) return;
            float yaw = Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg;

            GameObject guide = Primitive(name, PrimitiveType.Cube, new Vector3(mid.x, 0.012f, mid.y),
                new Vector3(0.18f, 0.016f, len), borderMat, parent, false, true);
            guide.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            GameObject trace = Primitive(name + " Trace", PrimitiveType.Cube, new Vector3(mid.x, 0.022f, mid.y),
                new Vector3(0.08f, 0.010f, Mathf.Max(0.1f, len - 0.08f)), traceMat, parent, false, false);
            trace.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void UpdateEditorBuildSettings(List<CampaignLevelCatalog.LevelSpec> catalog)
        {
            var scenes = new List<EditorBuildSettingsScene>(catalog.Count + 1);
            if (System.IO.File.Exists(MainMenuScenePath))
                scenes.Add(new EditorBuildSettingsScene(MainMenuScenePath, true));
            for (int i = 0; i < catalog.Count; i++)
                scenes.Add(new EditorBuildSettingsScene(catalog[i].ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void SaveLevelInfoAsset(CampaignLevelCatalog.LevelSpec spec)
        {
            string assetPath = LevelsResourcesFolder + "/" + spec.SceneName + ".asset";
            LevelInfo info = AssetDatabase.LoadAssetAtPath<LevelInfo>(assetPath);
            if (info == null)
            {
                info = ScriptableObject.CreateInstance<LevelInfo>();
                AssetDatabase.CreateAsset(info, assetPath);
            }
            string fragTitle = spec.fragment.enabled ? spec.fragment.title : string.Empty;
            info.Configure(spec.title, spec.chapterSubtitle, spec.districtLabel, spec.parActions,
                spec.requireFacingAngle, spec.requiredFacingYaw, fragTitle,
                spec.triggerGlitchOnClear, spec.playPianoOnClear,
                spec.openingDialogue, spec.clearDialogue, spec.hints,
                spec.allowPartition, spec.allowFusion);
            EditorUtility.SetDirty(info);
        }

        private static GridSettings LoadOrCreateGridSettings()
        {
            GridSettings settings = AssetDatabase.LoadAssetAtPath<GridSettings>(GridSettingsPath);
            if (settings != null) return settings;
            if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
            settings = ScriptableObject.CreateInstance<GridSettings>();
            AssetDatabase.CreateAsset(settings, GridSettingsPath);
            return settings;
        }

        private static PaletteSettings LoadOrCreatePaletteSettings()
        {
            PaletteSettings settings = AssetDatabase.LoadAssetAtPath<PaletteSettings>(PaletteSettingsPath);
            if (settings == null)
            {
                if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
                settings = ScriptableObject.CreateInstance<PaletteSettings>();
                AssetDatabase.CreateAsset(settings, PaletteSettingsPath);
            }
            settings.ApplyCatppuccinMochaDefaults();
            EditorUtility.SetDirty(settings);
            return settings;
        }

        private static void EnsureOutlineMaterial()
        {
            Shader shader = Shader.Find("POLYMULATOR/Pro Outline");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null)
            {
                Material material = CreateResourceMaterial(OutlineMaterialPath, shader.name);
                if (material != null)
                {
                    if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", PaletteSettings.Sky);
                    if (material.HasProperty("_RimTint")) material.SetColor("_RimTint", PaletteSettings.Text);
                    if (material.HasProperty("_OutlineWidth")) material.SetFloat("_OutlineWidth", W(0.056f));
                    EditorUtility.SetDirty(material);
                }
            }

            Material rim = CreateResourceMaterial(RimOverlayMaterialPath, "POLYMULATOR/Pro Rim Overlay");
            if (rim != null)
            {
                if (rim.HasProperty("_BaseColor")) rim.SetColor("_BaseColor", PaletteSettings.Sky);
                if (rim.HasProperty("_RimTint")) rim.SetColor("_RimTint", PaletteSettings.Text);
                EditorUtility.SetDirty(rim);
            }

            Material particleGlow = CreateResourceMaterial(ParticleGlowMaterialPath, "Universal Render Pipeline/Particles/Unlit");
            if (particleGlow != null)
            {
                if (particleGlow.HasProperty("_Surface")) particleGlow.SetFloat("_Surface", 1f);
                if (particleGlow.HasProperty("_Blend")) particleGlow.SetFloat("_Blend", 2f);
                if (particleGlow.HasProperty("_ZWrite")) particleGlow.SetFloat("_ZWrite", 0f);
                particleGlow.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(particleGlow);
            }
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
                if (!AssetDatabase.IsValidFolder(ResourcesFolder)) AssetDatabase.CreateFolder("Assets", "Resources");
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else material.shader = shader;
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
            m.accentMaroon = AccentMat("Sim_Accent_Maroon", PaletteSettings.Maroon, 1.18f);
            m.accentRosewater = AccentMat("Sim_Accent_Rosewater", PaletteSettings.Rosewater, 1.22f);

            m.coreGold = SimSurfaceMat("Sim_Core", PaletteSettings.Yellow, PaletteSettings.Peach,
                PaletteSettings.Rosewater, PaletteSettings.Crust, 0.62f, 0.22f, 0.34f, 0.045f, 0.26f, false);
            m.coreBeacon = TransparentUnlitMat("Sim_CoreBeacon", PaletteSettings.Yellow, 1.15f, 0.28f);
            m.barrierMat = TransparentUnlitMat("Sim_Barrier", PaletteSettings.Red, 1.25f, 0.34f);
            m.fragmentMat = AccentMat("Sim_Fragment", PaletteSettings.Rosewater, 1.35f);

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

        private static Material ThemeBorderAccent(Mats m, CampaignLevelCatalog.ChapterTheme theme)
        {
            switch (theme)
            {
                case CampaignLevelCatalog.ChapterTheme.Elevasi: return m.accentLavender;
                case CampaignLevelCatalog.ChapterTheme.Rotasi: return m.accentMaroon;
                case CampaignLevelCatalog.ChapterTheme.Rekonstruksi: return m.accentTeal;
                case CampaignLevelCatalog.ChapterTheme.Badai: return m.accentGreen;
                case CampaignLevelCatalog.ChapterTheme.Showcase: return m.accentLavender;
                default: return m.accentLimited;
            }
        }

        private static Color ThemeLightColor(CampaignLevelCatalog.ChapterTheme theme)
        {
            switch (theme)
            {
                case CampaignLevelCatalog.ChapterTheme.Elevasi: return PaletteSettings.Lavender;
                case CampaignLevelCatalog.ChapterTheme.Rotasi: return PaletteSettings.Maroon;
                case CampaignLevelCatalog.ChapterTheme.Rekonstruksi: return PaletteSettings.Teal;
                case CampaignLevelCatalog.ChapterTheme.Badai: return PaletteSettings.Flamingo;
                case CampaignLevelCatalog.ChapterTheme.Showcase: return PaletteSettings.Teal;
                default: return PaletteSettings.Sapphire;
            }
        }

        private static void BuildChamber(Transform env, Mats m, CampaignLevelCatalog.LevelSpec spec,
            out WeightedPressurePlate primaryPlate)
        {
            Material chapterAccent = ThemeBorderAccent(m, spec.theme);

            Primitive("Chamber Floor", PrimitiveType.Cube, new Vector3(0f, -0.25f, 0f),
                new Vector3(28.8f, 0.5f, 28.8f), m.floor, env, true, true);
            Primitive("Chamber Under-Plinth", PrimitiveType.Cube, new Vector3(0f, -0.78f, 0f),
                new Vector3(29.8f, 0.72f, 29.8f), m.plinth, env, false, true);

            ChamberArchitectureBuilder.BuildSurroundingFacility(
                env, m.floor, m.plinth, m.platformLow, m.platformMid, m.dark, m.trimMetal, chapterAccent, spec.theme, gridUnit);

            AddInvisibleBoundary(env);

            Primitive("Curb North", PrimitiveType.Cube, new Vector3(0f, -0.02f, 14.95f), new Vector3(30.4f, 0.28f, 0.35f), m.plinth, env, false, true);
            Primitive("Curb South", PrimitiveType.Cube, new Vector3(0f, -0.02f, -14.95f), new Vector3(30.4f, 0.28f, 0.35f), m.plinth, env, false, true);
            Primitive("Curb East", PrimitiveType.Cube, new Vector3(14.95f, -0.02f, 0f), new Vector3(0.35f, 0.28f, 29.9f), m.plinth, env, false, true);
            Primitive("Curb West", PrimitiveType.Cube, new Vector3(-14.95f, -0.02f, 0f), new Vector3(0.35f, 0.28f, 29.9f), m.plinth, env, false, true);

            Primitive("Accent Rail N", PrimitiveType.Cube, new Vector3(0f, 0.025f, 14.32f), new Vector3(28.6f, 0.032f, 0.07f), m.trimMetal, env, false, true);
            Primitive("Accent Rail S", PrimitiveType.Cube, new Vector3(0f, 0.025f, -14.32f), new Vector3(28.6f, 0.032f, 0.07f), m.trimMetal, env, false, true);
            Primitive("Accent Rail E", PrimitiveType.Cube, new Vector3(14.32f, 0.025f, 0f), new Vector3(0.07f, 0.032f, 28.6f), m.trimMetal, env, false, true);
            Primitive("Accent Rail W", PrimitiveType.Cube, new Vector3(-14.32f, 0.025f, 0f), new Vector3(0.07f, 0.032f, 28.6f), m.trimMetal, env, false, true);

            Primitive("Accent North", PrimitiveType.Cube, new Vector3(0f, 0.036f, 14.32f), new Vector3(4.8f, 0.024f, 0.055f), chapterAccent, env, false, false);
            Primitive("Accent South", PrimitiveType.Cube, new Vector3(0f, 0.036f, -14.32f), new Vector3(4.8f, 0.024f, 0.055f), chapterAccent, env, false, false);
            Primitive("Accent East", PrimitiveType.Cube, new Vector3(14.32f, 0.036f, 0f), new Vector3(0.055f, 0.024f, 4.8f), chapterAccent, env, false, false);
            Primitive("Accent West", PrimitiveType.Cube, new Vector3(-14.32f, 0.036f, 0f), new Vector3(0.055f, 0.024f, 4.8f), chapterAccent, env, false, false);

            float[] corners = { -14.35f, 14.35f };
            int idx = 1;
            foreach (float x in corners)
            {
                foreach (float z in corners)
                {
                    Transform pylon = Group("Corner Pylon " + idx++, env);
                    pylon.localPosition = new Vector3(x, 0f, z) * gridUnit;
                    Primitive("Pylon Base", PrimitiveType.Cube, new Vector3(0f, 0.22f, 0f), new Vector3(1.25f, 0.44f, 1.25f), m.plinth, pylon, true, true);
                    Primitive("Pylon Shaft", PrimitiveType.Cube, new Vector3(0f, 1.55f, 0f), new Vector3(0.84f, 2.22f, 0.84f), m.platformLow, pylon, true, true);
                    Primitive("Pylon Collar", PrimitiveType.Cube, new Vector3(0f, 2.72f, 0f), new Vector3(1.02f, 0.14f, 1.02f), m.trimMetal, pylon, false, true);
                    Primitive("Pylon Core", PrimitiveType.Cube, new Vector3(0f, 2.98f, 0f), new Vector3(0.56f, 0.36f, 0.56f), chapterAccent, pylon, false, false);
                    Primitive("Pylon Crown", PrimitiveType.Cube, new Vector3(0f, 3.23f, 0f), new Vector3(1.06f, 0.14f, 1.06f), m.platformMid, pylon, false, true);
                }
            }

            if (spec.platforms != null)
            {
                for (int i = 0; i < spec.platforms.Length; i++)
                {
                    var p = spec.platforms[i];
                    Material topMat = p.tier == 2 ? m.platformHigh : (p.tier == 1 ? m.platformMid : m.platformLow);
                    Material trim = p.tier == 2 ? m.accentLavender : chapterAccent;
                    BuildTieredPlatform(p.name, env, p.centerGrid, p.sizeGrid, topMat, m.plinth, trim);
                    BuildWalkableSideSteps(env, m, chapterAccent, p);
                }
            }

            if (spec.extraWalls != null)
            {
                for (int i = 0; i < spec.extraWalls.Length; i++)
                {
                    var w = spec.extraWalls[i];
                    Primitive(w.name, PrimitiveType.Cube, w.centerGrid, w.sizeGrid, m.platformMid, env, true, true);
                }
            }

            List<WeightedPressurePlate> builtPlates = new List<WeightedPressurePlate>();
            if (spec.plates != null)
            {
                for (int i = 0; i < spec.plates.Length; i++)
                    builtPlates.Add(BuildPressurePlate(env, m, spec.plates[i]));
            }
            primaryPlate = builtPlates.Count > 0 ? builtPlates[0] : null;

            if (spec.barriers != null)
            {
                for (int i = 0; i < spec.barriers.Length; i++)
                    BuildEnergyBarrier(env, m, spec.barriers[i], builtPlates);
            }

            if (spec.bridge.dynamicStepCount > 0)
            {
                WeightedPressurePlate[] bridgePlates = ResolvePlates(spec.bridge.plateIndices, builtPlates);
                BuildSpecBridge(env, m, chapterAccent, spec.bridge, bridgePlates, out float firstBridgeZ);
                if (bridgePlates.Length > 0)
                    BuildAutoConduitCircuit(env, m, bridgePlates[0], spec.bridge.centerX, firstBridgeZ);
            }

            if (spec.addPianoRoomProp)
                BuildPianoAlcoveDecor(env, m);

            if (spec.fragment.enabled)
                BuildMemoryFragment(env, m, spec.fragment);

            Vector3 spawnCenter = new Vector3(spec.playerSpawnGrid.x, 0f, spec.playerSpawnGrid.z);
            Primitive("Spawn Pad Plinth", PrimitiveType.Cylinder, spawnCenter + new Vector3(0f, 0.012f, 0f), new Vector3(2.9f, 0.024f, 2.9f), m.plinth, env, false, true);
            Primitive("Spawn Pad Deck", PrimitiveType.Cylinder, spawnCenter + new Vector3(0f, 0.028f, 0f), new Vector3(2.35f, 0.018f, 2.35f), m.platformLow, env, false, true);
            AddSquareOutline("Spawn Outline", env, spawnCenter + new Vector3(0f, 0.040f, 0f), 1.45f, 0.035f, m.trimMetal);

            Vector3 anchorCenter = new Vector3(spec.anchorGrid.x, 0f, spec.anchorGrid.z);
            Primitive("Anchor Pad Plinth", PrimitiveType.Cylinder, anchorCenter + new Vector3(0f, 0.012f, 0f), new Vector3(2.7f, 0.024f, 2.7f), m.plinth, env, false, true);
            Primitive("Anchor Pad Deck", PrimitiveType.Cylinder, anchorCenter + new Vector3(0f, 0.028f, 0f), new Vector3(2.15f, 0.018f, 2.15f), m.platformLow, env, false, true);
            AddSquareOutline("Anchor Outline", env, anchorCenter + new Vector3(0f, 0.042f, 0f), 1.05f, 0.06f, m.accentTeal);
        }

        private static WeightedPressurePlate[] ResolvePlates(int[] indices, List<WeightedPressurePlate> all)
        {
            if (indices == null || indices.Length == 0 || all.Count == 0)
                return all.Count > 0 ? new[] { all[0] } : System.Array.Empty<WeightedPressurePlate>();

            var res = new List<WeightedPressurePlate>(indices.Length);
            for (int i = 0; i < indices.Length; i++)
            {
                int k = indices[i];
                if (k >= 0 && k < all.Count) res.Add(all[k]);
            }
            return res.ToArray();
        }

        private static void BuildWalkableSideSteps(Transform env, Mats m, Material accent, CampaignLevelCatalog.PlatformSpec p)
        {
            float topY = p.centerGrid.y + p.sizeGrid.y * 0.5f;
            int stepCount = Mathf.Max(1, Mathf.CeilToInt(topY / 0.36f) - 1);
            float stepHeight = topY / (stepCount + 1);
            const float treadDepth = 0.72f;

            if (p.addSouthSteps)
            {
                float edgeZ = p.centerGrid.z - p.sizeGrid.z * 0.5f;
                for (int s = 0; s < stepCount; s++)
                {
                    float h = stepHeight * (s + 1);
                    float z = edgeZ - (stepCount - s - 0.5f) * treadDepth;
                    Primitive(p.name + " StepS " + (s + 1), PrimitiveType.Cube,
                        new Vector3(p.centerGrid.x, h * 0.5f, z), new Vector3(2.0f, h, treadDepth),
                        m.platformLow, env, true, true);
                    Primitive(p.name + " StepLipS " + (s + 1), PrimitiveType.Cube,
                        new Vector3(p.centerGrid.x, h - 0.03f, z - treadDepth * 0.5f + 0.025f),
                        new Vector3(1.86f, 0.035f, 0.05f), accent, env, false, true);
                }
            }
            if (p.addWestSteps)
            {
                float edgeX = p.centerGrid.x - p.sizeGrid.x * 0.5f;
                for (int s = 0; s < stepCount; s++)
                {
                    float h = stepHeight * (s + 1);
                    float x = edgeX - (stepCount - s - 0.5f) * treadDepth;
                    Primitive(p.name + " StepW " + (s + 1), PrimitiveType.Cube,
                        new Vector3(x, h * 0.5f, p.centerGrid.z), new Vector3(treadDepth, h, 2.0f),
                        m.platformLow, env, true, true);
                }
            }
            if (p.addEastSteps)
            {
                float edgeX = p.centerGrid.x + p.sizeGrid.x * 0.5f;
                for (int s = 0; s < stepCount; s++)
                {
                    float h = stepHeight * (s + 1);
                    float x = edgeX + (stepCount - s - 0.5f) * treadDepth;
                    Primitive(p.name + " StepE " + (s + 1), PrimitiveType.Cube,
                        new Vector3(x, h * 0.5f, p.centerGrid.z), new Vector3(treadDepth, h, 2.0f),
                        m.platformLow, env, true, true);
                }
            }
        }

        private static WeightedPressurePlate BuildPressurePlate(Transform env, Mats m, CampaignLevelCatalog.PlateSpec spec)
        {
            GameObject plateGo = new GameObject(spec.name);
            plateGo.transform.SetParent(env, false);
            plateGo.transform.localPosition = spec.posGrid * gridUnit;

            Primitive("Plate Plinth", PrimitiveType.Cube, new Vector3(0f, 0.03f, 0f), new Vector3(2.45f, 0.06f, 2.45f), m.plinth, plateGo.transform, true, true);
            GameObject deck = Primitive("Plate Deck", PrimitiveType.Cube, new Vector3(0f, 0.085f, 0f), new Vector3(2.05f, 0.06f, 2.05f), m.platformMid, plateGo.transform, true, false);

            GameObject rimN = Primitive("Plate Rim N", PrimitiveType.Cube, new Vector3(0f, 0.095f, 1.08f), new Vector3(2.26f, 0.05f, 0.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimS = Primitive("Plate Rim S", PrimitiveType.Cube, new Vector3(0f, 0.095f, -1.08f), new Vector3(2.26f, 0.05f, 0.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimE = Primitive("Plate Rim E", PrimitiveType.Cube, new Vector3(1.08f, 0.095f, 0f), new Vector3(0.08f, 0.05f, 2.08f), m.accentPeach, plateGo.transform, false, false);
            GameObject rimW = Primitive("Plate Rim W", PrimitiveType.Cube, new Vector3(-1.08f, 0.095f, 0f), new Vector3(0.08f, 0.05f, 2.08f), m.accentPeach, plateGo.transform, false, false);

            GameObject pip1 = Primitive("Weight Pip 1", PrimitiveType.Cube, new Vector3(1.24f, 0.06f, -0.38f), new Vector3(0.14f, 0.08f, 0.56f), m.accentPeach, plateGo.transform, false, false);
            GameObject pip2 = Primitive("Weight Pip 2", PrimitiveType.Cube, new Vector3(1.24f, 0.06f, 0.38f), new Vector3(0.14f, 0.08f, 0.56f), m.accentPeach, plateGo.transform, false, false);

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
            plate.requiredMass = spec.requiredMass;
            plate.movingDeck = deck.transform;
            plate.idleAccentMaterial = m.accentPeach;
            plate.partialAccentMaterial = m.accentYellow;
            plate.activeAccentMaterial = m.accentGreen;
            plate.plateLight = plateLight;
            plate.weightPipRenderers = new[] { pip1.GetComponent<Renderer>(), pip2.GetComponent<Renderer>() };
            plate.stateRenderers = new[] { rimN.GetComponent<Renderer>(), rimS.GetComponent<Renderer>(), rimE.GetComponent<Renderer>(), rimW.GetComponent<Renderer>() };
            return plate;
        }

        private static void BuildEnergyBarrier(Transform env, Mats m, CampaignLevelCatalog.BarrierSpec spec, List<WeightedPressurePlate> allPlates)
        {
            GameObject go = new GameObject(spec.name);
            go.transform.SetParent(env, false);

            Primitive(spec.name + " Post L", PrimitiveType.Cube,
                spec.posGrid - new Vector3(spec.sizeGrid.x > spec.sizeGrid.z ? spec.sizeGrid.x * 0.5f : 0f, 0f, spec.sizeGrid.z > spec.sizeGrid.x ? spec.sizeGrid.z * 0.5f : 0f),
                new Vector3(0.24f, spec.sizeGrid.y + 0.15f, 0.24f), m.trimMetal, go.transform, true, true);
            Primitive(spec.name + " Post R", PrimitiveType.Cube,
                spec.posGrid + new Vector3(spec.sizeGrid.x > spec.sizeGrid.z ? spec.sizeGrid.x * 0.5f : 0f, 0f, spec.sizeGrid.z > spec.sizeGrid.x ? spec.sizeGrid.z * 0.5f : 0f),
                new Vector3(0.24f, spec.sizeGrid.y + 0.15f, 0.24f), m.trimMetal, go.transform, true, true);

            GameObject wallGo = Primitive(spec.name + " Field", PrimitiveType.Cube, spec.posGrid, spec.sizeGrid, m.barrierMat, go.transform, true, false);
            EnergyBarrier barrier = go.AddComponent<EnergyBarrier>();
            barrier.requiredPlates = ResolvePlates(spec.plateIndices, allPlates);
            barrier.barrierCollider = wallGo.GetComponent<Collider>();
            barrier.barrierMeshTransform = wallGo.transform;
            barrier.barrierRenderer = wallGo.GetComponent<Renderer>();
        }

        private static void BuildSpecBridge(Transform env, Mats m, Material staticAccent,
            CampaignLevelCatalog.BridgeSpec spec, WeightedPressurePlate[] linkedPlates, out float dynamicStartZ)
        {
            float bx = spec.centerX;
            float stairFront = spec.stairStartZ;
            int totalSteps = spec.staticStepCount + spec.dynamicStepCount;
            float stepRise = spec.targetHeightGrid / (totalSteps + 1);

            for (int i = 0; i < spec.staticStepCount; i++)
            {
                float top = stepRise * (i + 1);
                float depth = 0.8f;
                float z = stairFront + depth * 0.5f;
                Primitive("Stair " + (i + 1), PrimitiveType.Cube, new Vector3(bx, top * 0.5f, z),
                    new Vector3(2.4f, top, depth), m.platformLow, env, true, true);
                Primitive("Stair Lip " + (i + 1), PrimitiveType.Cube,
                    new Vector3(bx, top - 0.03f, z - depth * 0.5f + 0.025f),
                    new Vector3(2.26f, 0.035f, 0.05f), staticAccent, env, false, true);
                stairFront += depth;
            }

            dynamicStartZ = stairFront;
            GameObject bridgeGo = new GameObject("Energy Bridge");
            bridgeGo.transform.SetParent(env, false);

            Collider[] colliders = new Collider[spec.dynamicStepCount];
            Transform[] segments = new Transform[spec.dynamicStepCount];
            Renderer[] holos = new Renderer[spec.dynamicStepCount];

            for (int i = 0; i < spec.dynamicStepCount; i++)
            {
                int stepNumber = spec.staticStepCount + i + 1;
                float top = stepRise * stepNumber;
                float depth = i == spec.dynamicStepCount - 1 ? 1.2f : 0.8f;
                float z = stairFront + depth * 0.5f;

                Primitive("Bridge Foundation " + stepNumber, PrimitiveType.Cube, new Vector3(bx, 0.025f, z),
                    new Vector3(2.44f, 0.05f, depth), m.plinth, bridgeGo.transform, false, true);

                GameObject stepGo = Primitive("Bridge Step " + stepNumber, PrimitiveType.Cube,
                    new Vector3(bx, top * 0.5f, z), new Vector3(2.4f, top, depth),
                    m.platformHigh, bridgeGo.transform, true, false);

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
            bridge.requiredPlates = linkedPlates;
            bridge.stepColliders = colliders;
            bridge.animatedSegments = segments;
            bridge.hologramRenderers = holos;
            bridge.dormantMaterial = m.accentPeach;
            bridge.activeMaterial = m.accentTeal;
        }

        private static void BuildAutoConduitCircuit(Transform env, Mats m, WeightedPressurePlate plate, float bridgeX, float targetZ)
        {
            if (plate == null) return;
            GameObject circuitGo = new GameObject("Conduit Floor Circuit");
            circuitGo.transform.SetParent(env, false);

            Vector3 pPos = plate.transform.localPosition / gridUnit;
            if (pPos.y > 0.25f) return; // Elevated pad routes cleanly without clipping floating deck

            float startX = pPos.x;
            float startZ = pPos.z + 1.15f;
            float endX = bridgeX - 1.1f;
            float midZ = Mathf.Min(targetZ - 0.4f, Mathf.Max(startZ + 1.4f, (startZ + targetZ) * 0.5f));

            Vector4[] spans =
            {
                new Vector4(startX, startZ, startX, midZ),
                new Vector4(startX, midZ, endX, midZ),
                new Vector4(endX, midZ, endX, targetZ + 0.05f)
            };

            var renderers = new List<Renderer>(spans.Length);
            for (int i = 0; i < spans.Length; i++)
            {
                float x0 = spans[i].x, z0 = spans[i].y;
                float x1 = spans[i].z, z1 = spans[i].w;
                float lenX = Mathf.Abs(x1 - x0);
                float lenZ = Mathf.Abs(z1 - z0);
                if (lenX < 0.05f && lenZ < 0.05f) continue;

                float cx = (x0 + x1) * 0.5f;
                float cz = (z0 + z1) * 0.5f;
                bool horiz = lenX > lenZ;
                float sx = horiz ? lenX + 0.14f : 0.14f;
                float sz = horiz ? 0.14f : lenZ + 0.14f;

                Primitive("Circuit Channel " + (i + 1), PrimitiveType.Cube, new Vector3(cx, 0.008f, cz),
                    new Vector3(sx + 0.10f, 0.016f, sz + 0.10f), m.plinth, circuitGo.transform, false, true);
                GameObject trace = Primitive("Circuit Trace " + (i + 1), PrimitiveType.Cube, new Vector3(cx, 0.019f, cz),
                    new Vector3(sx, 0.012f, sz), m.accentPeach, circuitGo.transform, false, false);
                renderers.Add(trace.GetComponent<Renderer>());
            }

            ConduitCircuit circuit = circuitGo.AddComponent<ConduitCircuit>();
            circuit.triggerPlate = plate;
            circuit.traceSegments = renderers.ToArray();
            circuit.dormantMaterial = m.accentPeach;
            circuit.activeMaterial = m.accentTeal;
        }

        private static void BuildPianoAlcoveDecor(Transform env, Mats m)
        {
            Transform alcove = Group("Piano Alcove Decor", env);
            alcove.localPosition = new Vector3(-8.4f, 0.0f, 4.9f) * gridUnit;

            // Acoustic dark Mocha rug under grand piano & bench
            Primitive("Piano Rug Trim", PrimitiveType.Cube, new Vector3(0f, 0.008f, -0.35f), new Vector3(2.75f, 0.016f, 2.85f), m.accentRosewater, alcove, false, true);
            Primitive("Piano Rug", PrimitiveType.Cube, new Vector3(0f, 0.024f, -0.35f), new Vector3(2.60f, 0.018f, 2.70f), m.dark, alcove, false, true);

            // Sculpted S-curved Grand Piano from GrandPiano.fbx resting atop rug (with collision footprint)
            Transform pianoRoot = Group("Grand Piano", alcove);
            pianoRoot.localPosition = new Vector3(0f, W(0.032f), 0f);
            pianoRoot.localRotation = Quaternion.Euler(0f, -12f, 0f);
            BoxCollider pianoCol = pianoRoot.gameObject.AddComponent<BoxCollider>();
            pianoCol.center = new Vector3(0f, W(0.68f), W(-0.22f));
            pianoCol.size = new Vector3(W(1.68f), W(1.36f), W(2.45f));
            pianoRoot.gameObject.isStatic = true;

            GameObject pianoCase = FbxModelLibrary.CreateSubMeshObject("Piano Case", "GrandPiano.fbx", "Piano_Case", m.plinth, pianoRoot, gridUnit, true);
            FbxModelLibrary.CreateSubMeshObject("Piano Brass", "GrandPiano.fbx", "Piano_Brass", m.accentPeach, pianoRoot, gridUnit, true);
            FbxModelLibrary.CreateSubMeshObject("Piano Keys", "GrandPiano.fbx", "Piano_Keys", m.objectLight, pianoRoot, gridUnit, true);
            if (pianoCase == null)
            {
                // Fallback only if FBX absent
                Primitive("Piano Soundboard", PrimitiveType.Cube, new Vector3(0f, 0.62f, 0.14f), new Vector3(1.62f, 1.24f, 0.44f), m.plinth, pianoRoot, false, true);
            }

            // Warm standing brass alcove lamp beside the piano curve
            Primitive("Lamp Base", PrimitiveType.Cylinder, new Vector3(-1.05f, 0.05f, 0.55f), new Vector3(0.32f, 0.06f, 0.32f), m.trimMetal, alcove, false, true);
            Primitive("Lamp Stem", PrimitiveType.Cylinder, new Vector3(-1.05f, 0.85f, 0.55f), new Vector3(0.05f, 1.56f, 0.05f), m.trimMetal, alcove, false, true);
            Primitive("Lamp Shade", PrimitiveType.Cube, new Vector3(-1.05f, 1.66f, 0.55f), new Vector3(0.34f, 0.18f, 0.34f), m.dark, alcove, false, true);
            Primitive("Lamp Bulb", PrimitiveType.Sphere, new Vector3(-1.05f, 1.56f, 0.55f), new Vector3(0.11f, 0.09f, 0.11f), m.accentPeach, alcove, false, false);

            GameObject lightGo = new GameObject("Warm Piano Light");
            lightGo.transform.SetParent(alcove, false);
            lightGo.transform.localPosition = new Vector3(-0.65f, 1.85f, -0.20f) * gridUnit;
            Light l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = PaletteSettings.Peach;
            l.range = W(5.6f);
            l.intensity = 1.55f;
            l.shadows = LightShadows.None;
        }

        private static void BuildMemoryFragment(Transform env, Mats m, CampaignLevelCatalog.FragmentSpec spec)
        {
            GameObject fragGo = new GameObject("MEMORY-FRAGMENT");
            fragGo.transform.SetParent(env, false);
            fragGo.transform.localPosition = spec.posGrid * gridUnit;

            Primitive("Frag Plinth", PrimitiveType.Cylinder, new Vector3(0f, -0.45f, 0f), new Vector3(1.15f, 0.14f, 1.15f), m.plinth, fragGo.transform, false, true);
            Primitive("Frag Ring", PrimitiveType.Cylinder, new Vector3(0f, -0.36f, 0f), new Vector3(0.82f, 0.05f, 0.82f), m.accentRosewater, fragGo.transform, false, true);

            GameObject crystal = Primitive("Frag Crystal", PrimitiveType.Cube, new Vector3(0f, 0.25f, 0f),
                new Vector3(0.46f, 0.58f, 0.46f), m.fragmentMat, fragGo.transform, false, false);
            crystal.GetComponent<MeshFilter>().sharedMesh = ChamberMeshFactory.GetFacetedCrystalGem();
            crystal.transform.localRotation = Quaternion.Euler(10f, 0f, 6f);

            GameObject lightGo = new GameObject("Frag Light");
            lightGo.transform.SetParent(fragGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, W(0.40f), 0f);
            Light fl = lightGo.AddComponent<Light>();
            fl.type = LightType.Point;
            fl.color = PaletteSettings.Rosewater;
            fl.range = W(4.5f);
            fl.intensity = 1.9f;
            fl.shadows = LightShadows.None;

            SphereCollider col = fragGo.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = W(1.1f);

            MemoryFragment frag = fragGo.AddComponent<MemoryFragment>();
            frag.fragmentTitle = spec.title;
            frag.speaker = spec.speaker;
            frag.storyLine = spec.line;
            frag.replySpeaker = spec.replySpeaker;
            frag.replyLine = spec.replyLine;
            frag.playPianoMotif = spec.playPianoMotif;
            frag.visualCrystal = crystal.transform;
            frag.fragmentLight = fl;
        }

        private static Transform BuildPropsAndObjectives(Transform env, Mats m, CampaignLevelCatalog.LevelSpec spec)
        {
            Transform props = Group("Props", env);
            if (spec.props != null)
            {
                for (int i = 0; i < spec.props.Length; i++)
                {
                    var pr = spec.props[i];
                    Material mat = (i % 2 == 0) ? m.objectDark : m.objectLight;
                    Prop(pr.name, pr.shape, pr.posGrid, pr.sizeGrid, mat, m, props, pr.mass);
                }
            }

            GameObject anchor = BuildAnchorStation(props, m, spec);
            InteractivePaletteAssigner.AssignAll(PaletteSettings.InteractivePalette, m.interactivePalette);

            float resolvedCoreMass = spec.coreMass > 0f ? spec.coreMass : 1.2f;
            GameObject coreGo = BuildCoreUnitProp(props, m, spec.coreSpawnGrid, resolvedCoreMass, spec.isEndingChamber);
            coreGo.transform.rotation = Quaternion.Euler(0f, spec.coreStartYaw, 0f);

            GameObject beaconGo = CreateUnscaledChildTrim("Core Beacon", PrimitiveType.Cylinder,
                new Vector3(0f, 1.15f, 0f), new Vector3(0.048f, 2.2f, 0.048f), null, coreGo.transform);
            beaconGo.GetComponent<Renderer>().sharedMaterial = m.coreBeacon;

            AnchorSocketObjective objective = anchor.GetComponent<AnchorSocketObjective>();
            if (objective != null)
            {
                objective.targetCore = coreGo.GetComponent<InteractableObject>();
                objective.coreRespawnGridPos = spec.coreSpawnGrid;
            }
            return anchor.transform;
        }

        private static GameObject BuildCoreUnitProp(Transform parent, Mats m, Vector3 spawnGrid, float mass, bool allowPartition)
        {
            Mesh innerMesh = FbxModelLibrary.LoadSubMesh("CoreUnit.fbx", "Core_InnerChamber");
            if (innerMesh == null)
            {
                GameObject fb = Prop("CORE-01", PrimitiveType.Cube, spawnGrid, new Vector3(0.92f, 0.92f, 0.92f), m.coreGold, m, parent, mass);
                InteractableObject fbIo = fb.GetComponent<InteractableObject>();
                if (fbIo != null) { fbIo.canFuse = false; fbIo.canPartition = allowPartition; }
                return fb;
            }

            GameObject coreGo = new GameObject("CORE-01");
            coreGo.transform.SetParent(parent, false);
            coreGo.transform.localPosition = spawnGrid * gridUnit;
            coreGo.transform.localScale = Vector3.one * gridUnit;

            MeshFilter mf = coreGo.AddComponent<MeshFilter>();
            mf.sharedMesh = innerMesh;
            MeshRenderer mr = coreGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.coreGold;

            BoxCollider col = coreGo.AddComponent<BoxCollider>();
            col.center = Vector3.zero;
            col.size = new Vector3(0.92f, 0.88f, 0.92f);

            Rigidbody body = coreGo.AddComponent<Rigidbody>();
            body.mass = mass;
            body.linearDamping = 0.14f;
            body.angularDamping = 0.08f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            GameObject cage = FbxModelLibrary.CreateSubMeshObject("Core Cage Frame", "CoreUnit.fbx", "Core_CageFrame", m.trimMetal, coreGo.transform, 1f, false);
            if (cage != null) cage.AddComponent<ObjectTrim>();

            GameObject visor = FbxModelLibrary.CreateSubMeshObject("Core Front Visor", "CoreUnit.fbx", "Core_FrontVisor", m.accentLimited, coreGo.transform, 1f, false);
            if (visor != null) visor.AddComponent<ObjectTrim>();

            InteractableObject coreInteract = coreGo.AddComponent<InteractableObject>();
            coreInteract.displayName = "CORE-01";
            coreInteract.canBePickedUp = true;
            coreInteract.canFuse = false;
            coreInteract.canPartition = allowPartition;
            coreInteract.SetAssignedColor(PaletteSettings.Yellow, m.coreGold);
            coreGo.AddComponent<ImpactAudioEmitter>();
            return coreGo;
        }

        private static GameObject BuildAnchorStation(Transform props, Mats m, CampaignLevelCatalog.LevelSpec spec)
        {
            GameObject anchor = new GameObject("ANCHOR-01");
            anchor.transform.SetParent(props, false);
            anchor.transform.localPosition = spec.anchorGrid * gridUnit;

            BoxCollider col = anchor.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, W(0.55f), 0f);
            col.size = new Vector3(W(1.45f), W(1.10f), W(1.45f));

            // AnchorPedestal.fbx plinth base is at local Y = -0.70; shift +0.70 so base rests flush on floor/deck surface
            Transform modelPivot = Group("Anchor Pedestal Mesh", anchor.transform);
            modelPivot.localPosition = new Vector3(0f, W(0.70f), 0f);

            GameObject plinthObj = FbxModelLibrary.CreateSubMeshObject("Base Pedestal", "AnchorPedestal.fbx", "Anchor_Plinth", m.plinth, modelPivot, gridUnit, false);
            if (plinthObj != null) plinthObj.AddComponent<ObjectTrim>();
            GameObject clawsObj = FbxModelLibrary.CreateSubMeshObject("Containment Claws", "AnchorPedestal.fbx", "Anchor_Claws", m.trimMetal, modelPivot, gridUnit, false);
            if (clawsObj != null) clawsObj.AddComponent<ObjectTrim>();
            GameObject haloObj = FbxModelLibrary.CreateSubMeshObject("Anchor Outer Halo", "AnchorPedestal.fbx", "Anchor_Halo", m.accentTeal, modelPivot, gridUnit, false);
            if (haloObj != null) haloObj.AddComponent<ObjectTrim>();

            if (plinthObj == null)
                AddTrimPrimitive("Base Pedestal", PrimitiveType.Cylinder, new Vector3(0f, 0.12f, 0f), new Vector3(1.55f, 0.24f, 1.55f), m.plinth, anchor.transform);

            if (spec.requireFacingAngle)
            {
                Transform chevronPivot = Group("Anchor Direction Chevron", anchor.transform);
                chevronPivot.localPosition = new Vector3(0f, W(0.06f), 0f);
                chevronPivot.localRotation = Quaternion.Euler(0f, spec.requiredFacingYaw, 0f);
                AddTrimPrimitive("Chevron Stem", PrimitiveType.Cube, new Vector3(0f, 0f, 1.08f), new Vector3(0.14f, 0.06f, 0.55f), m.accentLimited, chevronPivot);
                AddTrimPrimitive("Chevron Wing L", PrimitiveType.Cube, new Vector3(-0.14f, 0f, 1.24f), new Vector3(0.28f, 0.07f, 0.14f), m.accentLimited, chevronPivot)
                    .transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
                AddTrimPrimitive("Chevron Wing R", PrimitiveType.Cube, new Vector3(0.14f, 0f, 1.24f), new Vector3(0.28f, 0.07f, 0.14f), m.accentLimited, chevronPivot)
                    .transform.localRotation = Quaternion.Euler(0f, -45f, 0f);
            }

            GameObject core = Primitive("Anchor Crystal Core", PrimitiveType.Cube, new Vector3(0f, 0.92f, 0f),
                new Vector3(0.38f, 0.50f, 0.38f), m.accentTeal, anchor.transform, false, false);
            core.GetComponent<MeshFilter>().sharedMesh = ChamberMeshFactory.GetFacetedCrystalGem();
            core.transform.localRotation = Quaternion.Euler(12f, 45f, 0f);
            core.AddComponent<ObjectTrim>();

            GameObject lightGo = new GameObject("Anchor Point Light");
            lightGo.transform.SetParent(anchor.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, W(1.05f), 0f);
            Light anchorLight = lightGo.AddComponent<Light>();
            anchorLight.type = LightType.Point;
            anchorLight.color = PaletteSettings.Teal;
            anchorLight.range = W(6.2f);
            anchorLight.intensity = 1.55f;
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
            rig.outerRing = haloObj != null ? haloObj.transform : modelPivot;
            rig.anchorLight = anchorLight;

            SphereCollider socketTrigger = anchor.AddComponent<SphereCollider>();
            socketTrigger.isTrigger = true;
            socketTrigger.center = new Vector3(0f, W(0.75f), 0f);
            socketTrigger.radius = W(1.38f);

            AnchorSocketObjective objective = anchor.AddComponent<AnchorSocketObjective>();
            objective.visualRig = rig;
            objective.cradleSeatPoint = core.transform;
            objective.requireFacingAngle = spec.requireFacingAngle;
            objective.requiredFacingYaw = spec.requiredFacingYaw;
            return anchor;
        }

        private static void BuildTieredPlatform(string name, Transform env, Vector3 centerGrid,
            Vector3 sizeGrid, Material topMat, Material plinthMat, Material trimAccent)
        {
            Primitive(name, PrimitiveType.Cube, centerGrid, sizeGrid, topMat, env, true, true);

            float topY = centerGrid.y + sizeGrid.y * 0.5f;
            float baseH = Mathf.Min(0.16f, sizeGrid.y * 0.35f);
            Primitive(name + " Plinth", PrimitiveType.Cube,
                new Vector3(centerGrid.x, baseH * 0.5f, centerGrid.z),
                new Vector3(sizeGrid.x + 0.16f, baseH, sizeGrid.z + 0.16f), plinthMat, env, false, true);

            // Architectural corner structural brackets anchoring platform cleanly
            float hx = sizeGrid.x * 0.5f;
            float hz = sizeGrid.z * 0.5f;
            float lugH = Mathf.Max(0.18f, sizeGrid.y * 0.85f);
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Primitive(name + " Bracket " + sx + "_" + sz, PrimitiveType.Cube,
                        new Vector3(centerGrid.x + sx * (hx - 0.04f), lugH * 0.5f, centerGrid.z + sz * (hz - 0.04f)),
                        new Vector3(0.18f, lugH, 0.18f), plinthMat, env, false, true);
                }
            }

            float trimY = topY - 0.05f;
            Primitive(name + " Trim S", PrimitiveType.Cube,
                new Vector3(centerGrid.x, trimY, centerGrid.z - sizeGrid.z * 0.5f - 0.015f),
                new Vector3(sizeGrid.x - 0.18f, 0.04f, 0.04f), trimAccent, env, false, true);
            Primitive(name + " Trim W", PrimitiveType.Cube,
                new Vector3(centerGrid.x - sizeGrid.x * 0.5f - 0.015f, trimY, centerGrid.z),
                new Vector3(0.04f, 0.04f, sizeGrid.z - 0.18f), trimAccent, env, false, true);
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

        private static void AddSquareOutline(string prefix, Transform parent, Vector3 center, float half, float thickness, Material mat)
        {
            Primitive(prefix + " N", PrimitiveType.Cube, center + new Vector3(0f, 0f, half), new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, true);
            Primitive(prefix + " S", PrimitiveType.Cube, center + new Vector3(0f, 0f, -half), new Vector3(half * 2f + thickness, 0.02f, thickness), mat, parent, false, true);
            Primitive(prefix + " E", PrimitiveType.Cube, center + new Vector3(half, 0f, 0f), new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, true);
            Primitive(prefix + " W", PrimitiveType.Cube, center + new Vector3(-half, 0f, 0f), new Vector3(thickness, 0.02f, half * 2f), mat, parent, false, true);
        }

        private static GameObject BuildPlayer(Transform parent, Mats m, Vector3 spawnGrid,
            out ObjectInteractor interactor, out ObjectCarryController carry,
            out TelekinesisController telekinesis, out SimulationModeController modeCtrl)
        {
            GameObject player = new GameObject("Rect");
            player.transform.SetParent(parent, false);
            player.transform.localPosition = spawnGrid * gridUnit;
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

            Transform visualRoot = Group("VisualRoot", player.transform);
            visualRoot.gameObject.layer = 2;

            // Load sculpted multi-part FBX character rig cleanly onto VisualRoot & HeadPivot
            FbxModelLibrary.CreateSubMeshObject("Hover Base", "RectCharacter.fbx", "Rect_HoverBase", m.playerDark, visualRoot, gridUnit, false, 2);
            GameObject hoverGlow = FbxModelLibrary.CreateSubMeshObject("Hover Glow Ring", "RectCharacter.fbx", "Rect_HoverGlowRing", m.accentLimited, visualRoot, gridUnit, false, 2);
            GameObject torso = FbxModelLibrary.CreateSubMeshObject("Torso Chassis", "RectCharacter.fbx", "Rect_Torso", m.player, visualRoot, gridUnit, false, 2);
            FbxModelLibrary.CreateSubMeshObject("Torso Dark Trim", "RectCharacter.fbx", "Rect_TorsoDarkTrim", m.playerDark, visualRoot, gridUnit, false, 2);
            GameObject chestCore = FbxModelLibrary.CreateSubMeshObject("Chest Core", "RectCharacter.fbx", "Rect_ChestCore", m.accentLimited, visualRoot, gridUnit, false, 2);
            GameObject backFin = FbxModelLibrary.CreateSubMeshObject("Mode Fin", "RectCharacter.fbx", "Rect_ModeFin", m.accentLimited, visualRoot, gridUnit, false, 2);

            if (torso == null)
            {
                torso = Primitive("Torso Chassis", PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(0.66f, 0.44f, 0.54f), m.player, visualRoot, false, false);
                torso.layer = 2;
            }

            Transform headPivot = Group("Head Pivot", visualRoot);
            headPivot.gameObject.layer = 2;
            headPivot.localPosition = new Vector3(0f, W(0.78f), 0f);

            GameObject headCowl = FbxModelLibrary.CreateSubMeshObject("Head Cowl", "RectCharacter.fbx", "Rect_HeadCowl", m.player, headPivot, gridUnit, false, 2);
            FbxModelLibrary.CreateSubMeshObject("Visor Recess", "RectCharacter.fbx", "Rect_VisorRecess", m.playerDark, headPivot, gridUnit, false, 2);
            GameObject visor = FbxModelLibrary.CreateSubMeshObject("Optic Visor", "RectCharacter.fbx", "Rect_OpticVisor", m.accentLimited, headPivot, gridUnit, false, 2);
            GameObject antennaTip = FbxModelLibrary.CreateSubMeshObject("Antenna Beacon", "RectCharacter.fbx", "Rect_AntennaBeacon", m.accentLimited, headPivot, gridUnit, false, 2);

            if (headCowl == null)
            {
                headCowl = Primitive("Head Cowl", PrimitiveType.Cube, Vector3.zero, new Vector3(0.58f, 0.35f, 0.50f), m.player, headPivot, false, false);
                headCowl.layer = 2;
            }

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

            PartitionFusionController partitionFusion = player.AddComponent<PartitionFusionController>();
            partitionFusion.modeController = modeCtrl;
            partitionFusion.interactor = interactor;
            partitionFusion.carryController = carry;
            partitionFusion.telekinesisController = telekinesis;
            modeCtrl.partitionFusionController = partitionFusion;

            RectVisualRig rig = player.AddComponent<RectVisualRig>();
            rig.visualRoot = visualRoot;
            rig.headPivot = headPivot;
            rig.limitedAccentMaterial = m.accentLimited;
            rig.flexibleAccentMaterial = m.accentFlexible;
            rig.modeAccentRenderers = new[]
            {
                hoverGlow != null ? hoverGlow.GetComponent<Renderer>() : null,
                chestCore != null ? chestCore.GetComponent<Renderer>() : null,
                backFin != null ? backFin.GetComponent<Renderer>() : null,
                visor != null ? visor.GetComponent<Renderer>() : null,
                antennaTip != null ? antennaTip.GetComponent<Renderer>() : null
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
            controller.introZoomOnStart = true;
            controller.introStartBehindRect = true;
            controller.introStartPitch = 22f;
            controller.introStartDistance = 5.6f;
            controller.introStartHeight = 3.0f;
            controller.introStartOrthoSize = 2.65f;
            controller.introHoldSeconds = 0.08f;
            controller.introZoomOutSeconds = 1.05f;

            Quaternion rotation = Quaternion.Euler(controller.pitch, controller.yaw, 0f);
            Vector3 horizontal = Quaternion.Euler(0f, controller.yaw, 0f) * Vector3.back * W(controller.cameraDistance);
            Vector3 focus = new Vector3(target.position.x, W(controller.focusHeight), target.position.z);
            cameraObject.transform.SetPositionAndRotation(focus + horizontal + Vector3.up * W(controller.cameraHeight), rotation);
            return cameraObject;
        }

        private static void ConfigureLighting(Transform env, Mats m, CampaignLevelCatalog.ChapterTheme theme)
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
            sun.color = Color.Lerp(PaletteSettings.Text, PaletteSettings.Peach, 0.30f);
            sun.intensity = 1.85f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.lightmapBakeType = LightmapBakeType.Realtime;
            lightObject.transform.rotation = Quaternion.Euler(50f, -42f, 0f);
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
            fill.color = Color.Lerp(ThemeLightColor(theme), PaletteSettings.Sapphire, 0.35f);
            fill.intensity = 0.48f;
            fill.shadows = LightShadows.None;
            fillObject.transform.rotation = Quaternion.Euler(28f, 140f, 0f);

            GameObject spawnLightGo = new GameObject("Spawn Pad Pool Light");
            spawnLightGo.transform.SetParent(env, false);
            spawnLightGo.transform.localPosition = new Vector3(0f, W(1.6f), W(-6.5f));
            Light spawnLight = spawnLightGo.AddComponent<Light>();
            spawnLight.type = LightType.Point;
            spawnLight.color = ThemeLightColor(theme);
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
                if (!AssetDatabase.IsValidFolder(SettingsFolder)) AssetDatabase.CreateFolder("Assets", "Settings");
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VolumeProfilePath);
            }

            if (!profile.TryGet(out Tonemapping tonemapping)) tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.active = true;
            tonemapping.mode.Override(TonemappingMode.ACES);

            if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
            bloom.active = true;
            bloom.threshold.Override(0.90f);
            bloom.intensity.Override(0.36f);
            bloom.scatter.Override(0.62f);
            bloom.highQualityFiltering.Override(true);

            if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
            vignette.active = true;
            vignette.color.Override(PaletteSettings.Crust);
            vignette.intensity.Override(0.24f);
            vignette.smoothness.Override(0.36f);

            if (!profile.TryGet(out ColorAdjustments adjust)) adjust = profile.Add<ColorAdjustments>(true);
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
            if (!AssetDatabase.IsValidFolder(UIFolder)) AssetDatabase.CreateFolder("Assets", "UI");
            string fontsFolder = UIFolder + "/Fonts";
            if (!AssetDatabase.IsValidFolder(fontsFolder)) AssetDatabase.CreateFolder(UIFolder, "Fonts");

            UnityEngine.TextCore.Text.FontAsset aktReg =
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextCore.Text.FontAsset>(fontsFolder + "/Font_AktRegular.asset");
            UnityEngine.TextCore.Text.FontAsset interSemi = EnsureSdfFont(
                "Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-SemiBold.ttf",
                fontsFolder + "/Font_InterSemiBold.asset");
            EnsureSdfFont("Packages/com.unity.dt.app-ui/PackageResources/Fonts/Inter-Regular.ttf", fontsFolder + "/Font_InterRegular.asset");
            EnsureSdfFont("Packages/com.unity.dt.app-ui/PackageResources/Fonts/RobotoMono-Regular.ttf", fontsFolder + "/Font_RobotoMono.asset");

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

            UnityEngine.TextCore.Text.FontAsset preferredDefaultFont = aktReg != null ? aktReg : interSemi;
            if (preferredDefaultFont != null)
            {
                string tsPath = UIFolder + "/PanelTextSettings.asset";
                PanelTextSettings ts = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(tsPath);
                if (ts == null)
                {
                    ts = ScriptableObject.CreateInstance<PanelTextSettings>();
                    AssetDatabase.CreateAsset(ts, tsPath);
                }
                ts.defaultFontAsset = preferredDefaultFont;
                EditorUtility.SetDirty(ts);
                panel.textSettings = ts;
            }

            EditorUtility.SetDirty(panel);
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

            UnityEngine.TextCore.Text.FontAsset fa = UnityEngine.TextCore.Text.FontAsset.CreateFontAsset(ttf);
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
                if (go.name == "Floor") Object.DestroyImmediate(go);
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

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (type == PrimitiveType.Cube)
                {
                    bool largeSlab = gridSize.x >= 2.2f || gridSize.z >= 2.2f || gridSize.y >= 2.2f;
                    mf.sharedMesh = largeSlab ? ChamberMeshFactory.GetChamferSlab() : ChamberMeshFactory.GetChamferCube();
                }
                else if (type == PrimitiveType.Cylinder && gridSize.x >= 0.35f)
                {
                    mf.sharedMesh = ChamberMeshFactory.GetOctagonPrism();
                }
            }

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
                ? localScale.y * 0.5f : localScale.y;
            go.transform.localScale = new Vector3(localScale.x, y, localScale.z) * gridUnit;

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf != null)
            {
                if (type == PrimitiveType.Cube && localScale.x >= 0.14f && localScale.z >= 0.14f)
                    mf.sharedMesh = ChamberMeshFactory.GetChamferCube();
                else if (type == PrimitiveType.Cylinder && localScale.x >= 0.35f)
                    mf.sharedMesh = ChamberMeshFactory.GetOctagonPrism();
            }

            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && mat != null) r.sharedMaterial = mat;
            go.AddComponent<ObjectTrim>();
            return go;
        }

        private static Vector3 WorldScale(PrimitiveType type, Vector3 gridSize)
        {
            float y = (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule) ? gridSize.y * 0.5f : gridSize.y;
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

            if (type == PrimitiveType.Cube)
            {
                CreateUnscaledChildTrim("Waist Band", PrimitiveType.Cube, Vector3.zero, new Vector3(1.03f, 0.14f, 1.03f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Orientation Pip", PrimitiveType.Cube, new Vector3(0f, 0f, 0.515f), new Vector3(0.28f, 0.08f, 0.04f), m.accentTeal, go.transform);
            }
            else if (type == PrimitiveType.Sphere)
            {
                CreateUnscaledChildTrim("Gyro Belt", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.03f, 0.09f, 1.03f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Axis Band E", PrimitiveType.Cylinder, Vector3.zero, new Vector3(1.015f, 0.03f, 1.015f), m.accentTeal, go.transform);
            }
            else if (type == PrimitiveType.Cylinder)
            {
                CreateUnscaledChildTrim("Top Collar", PrimitiveType.Cylinder, new Vector3(0f, 0.85f, 0f), new Vector3(1.05f, 0.16f, 1.05f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Base Collar", PrimitiveType.Cylinder, new Vector3(0f, -0.85f, 0f), new Vector3(1.05f, 0.16f, 1.05f), m.trimMetal, go.transform);
                CreateUnscaledChildTrim("Front Marker", PrimitiveType.Cube, new Vector3(0f, 0f, 0.51f), new Vector3(0.20f, 0.45f, 0.05f), m.accentTeal, go.transform);
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
            float y = (type == PrimitiveType.Cylinder || type == PrimitiveType.Capsule) ? localScale.y * 0.5f : localScale.y;
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
            else if (shader != null) material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TransparentUnlitMat(string name, Color color, float hdrMult, float alpha)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            Material mat = LoadOrCreateMat(name, shader);
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            Color c = color * hdrMult;
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
            return mat;
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
            if (mat.HasProperty("_TileAltColor")) mat.SetColor("_TileAltColor", Color.Lerp(PaletteSettings.Base, PaletteSettings.Mantle, 0.55f));
            if (mat.HasProperty("_MinorLineColor")) mat.SetColor("_MinorLineColor", PaletteSettings.Surface0);
            if (mat.HasProperty("_MajorLineColor")) mat.SetColor("_MajorLineColor", Color.Lerp(PaletteSettings.Surface1, PaletteSettings.Sapphire, 0.16f));
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
