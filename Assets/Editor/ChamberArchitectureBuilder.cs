using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Builds multi-layered 3D architectural enclosures around chambers so gameplay sits inside
    /// an authentic spatial testing facility: sunken trenches, North/East ribbed walls,
    /// observation control booths, mezzanine catwalks, subterranean trusses, skylight beams,
    /// and chapter-specific architectural storytelling props.
    /// </summary>
    public static class ChamberArchitectureBuilder
    {
        public struct ArchMats
        {
            public Material wallPanel, trenchFloor, darkSteel, trimMetal;
            public Material chapterAccent, warmWindow, coolWindow, glassPane;
            public Material beamCool, beamWarm;
            public Mesh chamferCube, chamferSlab, steelTruss, lightShaftCone;
        }

        public static void BuildSurroundingFacility(
            Transform env,
            Material floorMat,
            Material plinthMat,
            Material platformLowMat,
            Material platformMidMat,
            Material darkMat,
            Material trimMetalMat,
            Material chapterAccentMat,
            CampaignLevelCatalog.ChapterTheme theme,
            float gridUnit)
        {
            ArchMats am = PrepareArchAssets(platformLowMat, darkMat, trimMetalMat, chapterAccentMat, plinthMat);
            Transform archRoot = CreateGroup("Facility Architecture", env);

            BuildPerimeterTrench(archRoot, am, gridUnit);
            BuildLowForegroundRails(archRoot, am, gridUnit);
            BuildFarWallEnclosure(archRoot, am, gridUnit);
            BuildObservationControlBay(archRoot, am, theme, gridUnit);
            BuildSubterraneanUnderstructure(archRoot, am, gridUnit);
            BuildSkylightGantriesAndShafts(archRoot, am, theme, gridUnit);
            BuildChapterSpecificSetDressing(archRoot, am, platformMidMat, theme, gridUnit);
        }

        public static void BuildMainMenuShowcase(
            Transform env,
            Material floorMat,
            Material plinthMat,
            Material platformLowMat,
            Material darkMat,
            Material trimMetalMat,
            Material accentTeal,
            Material accentSapphire,
            float gridUnit)
        {
            ArchMats am = PrepareArchAssets(platformLowMat, darkMat, trimMetalMat, accentSapphire, plinthMat);
            Transform root = CreateGroup("Diorama Architecture", env);

            Box(root, "Showcase Floor", am.chamferSlab, floorMat, new Vector3(0f, -0.25f, 0f), new Vector3(14f, 0.5f, 14f), gridUnit);
            Box(root, "Showcase Sub-Base", am.chamferSlab, plinthMat, new Vector3(0f, -0.82f, 0f), new Vector3(14.8f, 0.85f, 14.8f), gridUnit);

            for (int i = -2; i <= 2; i++)
            {
                float pos = i * 2.8f;
                Box(root, "Wall Panel N " + i, am.chamferSlab, platformLowMat, new Vector3(pos, 3.2f, 7.2f), new Vector3(2.65f, 6.4f, 0.5f), gridUnit);
                Box(root, "Wall Panel E " + i, am.chamferSlab, platformLowMat, new Vector3(7.2f, 3.2f, pos), new Vector3(0.5f, 6.4f, 2.65f), gridUnit);
            }

            float[] pilasterOffsets = { -4.2f, 0f, 4.2f };
            foreach (float p in pilasterOffsets)
            {
                Box(root, "Pilaster N " + p, am.chamferCube, darkMat, new Vector3(p, 3.4f, 6.95f), new Vector3(0.55f, 6.9f, 0.72f), gridUnit);
                Box(root, "Pilaster E " + p, am.chamferCube, darkMat, new Vector3(6.95f, 3.4f, p), new Vector3(0.72f, 6.9f, 0.55f), gridUnit);
            }

            Box(root, "Corner Slit Light", am.chamferCube, accentTeal, new Vector3(6.75f, 2.8f, 6.75f), new Vector3(0.14f, 4.8f, 0.14f), gridUnit);
            Box(root, "Booth Frame", am.chamferCube, trimMetalMat, new Vector3(-1.4f, 4.3f, 6.85f), new Vector3(4.2f, 1.85f, 0.35f), gridUnit);
            Box(root, "Booth Back Glow", am.chamferSlab, am.warmWindow, new Vector3(-1.4f, 4.3f, 7.05f), new Vector3(3.8f, 1.45f, 0.10f), gridUnit);

            Box(root, "Overhead Beam N", am.steelTruss, trimMetalMat, new Vector3(0f, 6.3f, 5.2f), new Vector3(13.5f, 0.48f, 0.65f), gridUnit);
            GameObject shaft = MeshGo(root, "Menu Skylight Shaft", am.lightShaftCone, am.beamCool,
                new Vector3(0.4f, 0.02f, 0.8f) * gridUnit, Quaternion.Euler(14f, 28f, -12f), new Vector3(2.8f, 7.6f, 2.8f) * gridUnit);
            shaft.isStatic = false;
        }

        private static ArchMats PrepareArchAssets(
            Material wallPanel, Material darkSteel, Material trimMetal, Material chapterAccent, Material trenchFloor)
        {
            return new ArchMats
            {
                wallPanel = wallPanel,
                trenchFloor = trenchFloor,
                darkSteel = darkSteel,
                trimMetal = trimMetal,
                chapterAccent = chapterAccent,
                chamferCube = ChamberMeshFactory.GetChamferCube(),
                chamferSlab = ChamberMeshFactory.GetChamferSlab(),
                steelTruss = ChamberMeshFactory.GetSteelTruss(),
                lightShaftCone = ChamberMeshFactory.GetLightShaftCone(),
                warmWindow = CreateAccentPanelMat("Sim_Window_WarmGlow", PaletteSettings.Peach, 0.90f),
                coolWindow = CreateAccentPanelMat("Sim_Window_CoolGlow", PaletteSettings.Sapphire, 0.72f),
                glassPane = CreateTransparentMat("Sim_ObsGlass", PaletteSettings.Surface0, 1.0f, 0.28f),
                beamCool = CreateVolumetricMat("Sim_Volumetric_Cool", PaletteSettings.Sapphire, 0.16f),
                beamWarm = CreateVolumetricMat("Sim_Volumetric_Warm", PaletteSettings.Peach, 0.145f)
            };
        }

        private static void BuildPerimeterTrench(Transform root, ArchMats am, float gu)
        {
            Transform g = CreateGroup("Perimeter Trench", root);
            Box(g, "Trench Slab N", am.chamferSlab, am.trenchFloor, new Vector3(0f, -0.62f, 15.65f), new Vector3(34.6f, 0.36f, 2.4f), gu);
            Box(g, "Trench Slab S", am.chamferSlab, am.trenchFloor, new Vector3(0f, -0.62f, -15.65f), new Vector3(34.6f, 0.36f, 2.4f), gu);
            Box(g, "Trench Slab E", am.chamferSlab, am.trenchFloor, new Vector3(15.65f, -0.62f, 0f), new Vector3(2.4f, 0.36f, 28.9f), gu);
            Box(g, "Trench Slab W", am.chamferSlab, am.trenchFloor, new Vector3(-15.65f, -0.62f, 0f), new Vector3(2.4f, 0.36f, 28.9f), gu);

            Box(g, "Outer Rim N", am.chamferSlab, am.darkSteel, new Vector3(0f, -0.18f, 16.8f), new Vector3(34.8f, 0.85f, 0.65f), gu);
            Box(g, "Outer Rim S", am.chamferSlab, am.darkSteel, new Vector3(0f, -0.25f, -16.8f), new Vector3(34.8f, 0.72f, 0.65f), gu);
            Box(g, "Outer Rim E", am.chamferSlab, am.darkSteel, new Vector3(16.8f, -0.18f, 0f), new Vector3(0.65f, 0.85f, 33.0f), gu);
            Box(g, "Outer Rim W", am.chamferSlab, am.darkSteel, new Vector3(-16.8f, -0.25f, 0f), new Vector3(0.65f, 0.72f, 33.0f), gu);

            for (int i = -3; i <= 3; i++)
            {
                float d = i * 3.6f;
                Box(g, "Trench Strut N " + i, am.chamferCube, am.trimMetal, new Vector3(d, -0.32f, 15.65f), new Vector3(0.36f, 0.16f, 1.9f), gu);
                Box(g, "Trench Strut S " + i, am.chamferCube, am.trimMetal, new Vector3(d, -0.32f, -15.65f), new Vector3(0.36f, 0.16f, 1.9f), gu);
                Box(g, "Trench Strut E " + i, am.chamferCube, am.trimMetal, new Vector3(15.65f, -0.32f, d), new Vector3(1.9f, 0.16f, 0.36f), gu);
                Box(g, "Trench Strut W " + i, am.chamferCube, am.trimMetal, new Vector3(-15.65f, -0.32f, d), new Vector3(1.9f, 0.16f, 0.36f), gu);
            }
        }

        private static void BuildLowForegroundRails(Transform root, ArchMats am, float gu)
        {
            Transform g = CreateGroup("Foreground Safety Curbs", root);
            Box(g, "South Rail Bar", am.chamferCube, am.trimMetal, new Vector3(0f, 0.34f, -14.88f), new Vector3(28.4f, 0.055f, 0.07f), gu);
            Box(g, "West Rail Bar", am.chamferCube, am.trimMetal, new Vector3(-14.88f, 0.34f, 0f), new Vector3(0.07f, 0.055f, 28.4f), gu);

            for (int i = -2; i <= 2; i++)
            {
                float p = i * 5.0f;
                Box(g, "S Stanchion " + i, am.chamferCube, am.darkSteel, new Vector3(p, 0.17f, -14.88f), new Vector3(0.14f, 0.36f, 0.16f), gu);
                Box(g, "W Stanchion " + i, am.chamferCube, am.darkSteel, new Vector3(-14.88f, 0.17f, p), new Vector3(0.16f, 0.36f, 0.14f), gu);
            }
        }

        private static void BuildFarWallEnclosure(Transform root, ArchMats am, float gu)
        {
            Transform g = CreateGroup("North-East Enclosure Walls", root);
            const float wallPos = 17.2f;
            const float wallHeight = 10.6f;
            const float wallCenterY = 3.85f;

            for (int i = -3; i <= 3; i++)
            {
                float span = i * 4.8f;
                Box(g, "North Acoustic Bay " + i, am.chamferSlab, am.wallPanel, new Vector3(span, wallCenterY, wallPos), new Vector3(4.55f, wallHeight, 0.55f), gu);
                Box(g, "North Dado Trim " + i, am.chamferCube, am.darkSteel, new Vector3(span, 1.35f, wallPos - 0.30f), new Vector3(4.55f, 0.18f, 0.22f), gu);
                Box(g, "East Acoustic Bay " + i, am.chamferSlab, am.wallPanel, new Vector3(wallPos, wallCenterY, span), new Vector3(0.55f, wallHeight, 4.55f), gu);
                Box(g, "East Dado Trim " + i, am.chamferCube, am.darkSteel, new Vector3(wallPos - 0.30f, 1.35f, span), new Vector3(0.22f, 0.18f, 4.55f), gu);
            }

            for (int p = -15; p <= 15; p += 5)
            {
                Box(g, "N Pilaster " + p, am.chamferCube, am.darkSteel, new Vector3(p, wallCenterY, wallPos - 0.38f), new Vector3(0.76f, wallHeight + 0.4f, 0.88f), gu);
                Box(g, "N Pilaster Pipe " + p, am.chamferCube, am.trimMetal, new Vector3(p - 0.46f, 2.6f, wallPos - 0.55f), new Vector3(0.11f, 6.2f, 0.11f), gu);
                if (p != 0)
                    Box(g, "N Pilaster Beacon " + p, am.chamferCube, am.chapterAccent, new Vector3(p, 2.2f, wallPos - 0.84f), new Vector3(0.09f, 1.6f, 0.06f), gu);

                Box(g, "E Pilaster " + p, am.chamferCube, am.darkSteel, new Vector3(wallPos - 0.38f, wallCenterY, p), new Vector3(0.88f, wallHeight + 0.4f, 0.76f), gu);
                Box(g, "E Pilaster Pipe " + p, am.chamferCube, am.trimMetal, new Vector3(wallPos - 0.55f, 2.6f, p - 0.46f), new Vector3(0.11f, 6.2f, 0.11f), gu);
                if (p != 0)
                    Box(g, "E Pilaster Beacon " + p, am.chamferCube, am.chapterAccent, new Vector3(wallPos - 0.84f, 2.2f, p), new Vector3(0.06f, 1.6f, 0.09f), gu);
            }

            Box(g, "Mezzanine Deck N", am.chamferSlab, am.darkSteel, new Vector3(0f, 4.15f, 16.05f), new Vector3(34.2f, 0.24f, 1.75f), gu);
            Box(g, "Mezzanine Rail N", am.chamferCube, am.trimMetal, new Vector3(0f, 4.82f, 15.26f), new Vector3(33.8f, 0.06f, 0.07f), gu);
            Box(g, "Mezzanine Deck E", am.chamferSlab, am.darkSteel, new Vector3(16.05f, 4.15f, 0f), new Vector3(1.75f, 0.24f, 34.2f), gu);
            Box(g, "Mezzanine Rail E", am.chamferCube, am.trimMetal, new Vector3(15.26f, 4.82f, 0f), new Vector3(0.07f, 0.06f, 33.8f), gu);

            Box(g, "NE Structural Tower", am.chamferCube, am.darkSteel, new Vector3(wallPos - 0.3f, 4.4f, wallPos - 0.3f), new Vector3(2.2f, 12.2f, 2.2f), gu);
            Box(g, "NE Tower Accent Spine", am.chamferCube, am.chapterAccent, new Vector3(wallPos - 1.42f, 4.6f, wallPos - 1.42f), new Vector3(0.14f, 8.8f, 0.14f), gu);

            GameObject wallGrazeN = new GameObject("Wall Graze Fill N");
            wallGrazeN.transform.SetParent(g, false);
            wallGrazeN.transform.localPosition = new Vector3(2.5f, 6.0f, 13.8f) * gu;
            Light wgN = wallGrazeN.AddComponent<Light>();
            wgN.type = LightType.Point;
            wgN.color = PaletteSettings.Sapphire;
            wgN.range = 16.0f * gu;
            wgN.intensity = 2.4f;
            wgN.shadows = LightShadows.None;

            GameObject wallGrazeE = new GameObject("Wall Graze Fill E");
            wallGrazeE.transform.SetParent(g, false);
            wallGrazeE.transform.localPosition = new Vector3(13.8f, 6.0f, 2.5f) * gu;
            Light wgE = wallGrazeE.AddComponent<Light>();
            wgE.type = LightType.Point;
            wgE.color = PaletteSettings.Lavender;
            wgE.range = 16.0f * gu;
            wgE.intensity = 2.2f;
            wgE.shadows = LightShadows.None;
        }

        private static void BuildObservationControlBay(Transform root, ArchMats am, CampaignLevelCatalog.ChapterTheme theme, float gu)
        {
            Transform g = CreateGroup("Observation Control Booth", root);
            const float centerZ = 16.65f;
            const float centerY = 4.90f;

            Box(g, "Booth Header", am.chamferCube, am.darkSteel, new Vector3(0f, 6.32f, centerZ), new Vector3(9.4f, 0.45f, 1.2f), gu);
            Box(g, "Booth Sill", am.chamferCube, am.darkSteel, new Vector3(0f, 3.55f, centerZ), new Vector3(9.4f, 0.42f, 1.2f), gu);
            Box(g, "Booth Frame W", am.chamferCube, am.darkSteel, new Vector3(-4.5f, centerY, centerZ), new Vector3(0.45f, 2.8f, 1.2f), gu);
            Box(g, "Booth Frame E", am.chamferCube, am.darkSteel, new Vector3(4.5f, centerY, centerZ), new Vector3(0.45f, 2.8f, 1.2f), gu);

            for (int m = -2; m <= 2; m++)
                Box(g, "Booth Mullion " + m, am.chamferCube, am.trimMetal, new Vector3(m * 1.9f, centerY, centerZ - 0.38f), new Vector3(0.11f, 2.5f, 0.18f), gu);

            Material boothGlowMat = theme == CampaignLevelCatalog.ChapterTheme.Badai ? am.coolWindow : am.warmWindow;
            Box(g, "Booth Interior Backdrop", am.chamferSlab, boothGlowMat, new Vector3(0f, centerY + 0.1f, centerZ + 0.42f), new Vector3(8.5f, 2.3f, 0.12f), gu);
            Box(g, "Booth Glazing", am.chamferSlab, am.glassPane, new Vector3(0f, centerY, centerZ - 0.32f), new Vector3(8.6f, 2.35f, 0.06f), gu);
            Box(g, "Booth Console", am.chamferCube, am.darkSteel, new Vector3(0f, 4.02f, centerZ + 0.15f), new Vector3(6.8f, 0.55f, 0.52f), gu);

            GameObject lightGo = new GameObject("Observation Spill Light");
            lightGo.transform.SetParent(g, false);
            lightGo.transform.localPosition = new Vector3(0f, 4.75f, 14.3f) * gu;
            Light boothLight = lightGo.AddComponent<Light>();
            boothLight.type = LightType.Point;
            boothLight.color = theme == CampaignLevelCatalog.ChapterTheme.Badai ? PaletteSettings.Teal : PaletteSettings.Peach;
            boothLight.range = 13.5f * gu;
            boothLight.intensity = 3.2f;
            boothLight.shadows = LightShadows.None;
        }

        private static void BuildSubterraneanUnderstructure(Transform root, ArchMats am, float gu)
        {
            Transform g = CreateGroup("Abyss Understructure", root);
            Vector2[] pierCoords =
            {
                new Vector2(-16.2f, -16.2f), new Vector2(-16.2f, -4.5f), new Vector2(-16.2f, 6.5f),
                new Vector2(-4.5f, -16.2f),  new Vector2(6.5f, -16.2f),  new Vector2(16.2f, -16.2f),
                new Vector2(-16.2f, 16.2f),  new Vector2(16.2f, 16.2f)
            };

            for (int i = 0; i < pierCoords.Length; i++)
            {
                Vector2 c = pierCoords[i];
                Box(g, "Sub-Pier " + i, am.chamferCube, am.trenchFloor, new Vector3(c.x, -7.2f, c.y), new Vector3(1.45f, 13.4f, 1.45f), gu);
                Box(g, "Sub-Pier Collar " + i, am.chamferCube, am.trimMetal, new Vector3(c.x, -2.6f, c.y), new Vector3(1.75f, 0.36f, 1.75f), gu);
            }

            Box(g, "Abyss Truss S", am.steelTruss, am.darkSteel, new Vector3(0f, -3.2f, -16.2f), new Vector3(31.5f, 0.9f, 0.85f), gu);
            Box(g, "Abyss Truss W", am.steelTruss, am.darkSteel, new Vector3(-16.2f, -3.2f, 0f), new Vector3(0.85f, 0.9f, 31.5f), gu);
            Box(g, "Abyss Truss N", am.steelTruss, am.darkSteel, new Vector3(0f, -3.2f, 16.2f), new Vector3(31.5f, 0.9f, 0.85f), gu);
            Box(g, "Abyss Truss E", am.steelTruss, am.darkSteel, new Vector3(16.2f, -3.2f, 0f), new Vector3(0.85f, 0.9f, 31.5f), gu);

            Box(g, "Deep Truss S", am.steelTruss, am.trenchFloor, new Vector3(0f, -7.6f, -16.2f), new Vector3(31.5f, 1.1f, 0.9f), gu);
            Box(g, "Deep Truss W", am.steelTruss, am.trenchFloor, new Vector3(-16.2f, -7.6f, 0f), new Vector3(0.9f, 1.1f, 31.5f), gu);
        }

        private static void BuildSkylightGantriesAndShafts(Transform root, ArchMats am, CampaignLevelCatalog.ChapterTheme theme, float gu)
        {
            Transform g = CreateGroup("Overhead Skylight Gantries", root);
            for (int i = -2; i <= 2; i++)
                Box(g, "Roof Rib N " + i, am.steelTruss, am.darkSteel, new Vector3(i * 5.0f, 9.35f, 13.6f), new Vector3(0.55f, 0.72f, 6.8f), gu);

            Box(g, "Main Roof Tie N", am.chamferCube, am.trimMetal, new Vector3(0f, 9.75f, 11.2f), new Vector3(32.0f, 0.42f, 0.72f), gu);
            Box(g, "Main Roof Tie E", am.chamferCube, am.trimMetal, new Vector3(11.2f, 9.75f, 0f), new Vector3(0.72f, 0.42f, 32.0f), gu);

            Material shaftMat = theme == CampaignLevelCatalog.ChapterTheme.Rotasi ? am.beamWarm : am.beamCool;
            MeshGo(g, "Skylight Shaft Center", am.lightShaftCone, shaftMat,
                new Vector3(0f, 0.05f, 7.6f) * gu, Quaternion.Euler(14f, 16f, -14f), new Vector3(3.2f, 10.2f, 3.2f) * gu);
            MeshGo(g, "Skylight Shaft East", am.lightShaftCone, am.beamWarm,
                new Vector3(7.4f, 0.05f, 2.2f) * gu, Quaternion.Euler(14f, 16f, -14f), new Vector3(2.6f, 9.8f, 2.6f) * gu);
            MeshGo(g, "Skylight Shaft NW", am.lightShaftCone, shaftMat,
                new Vector3(-7.6f, 0.05f, 8.2f) * gu, Quaternion.Euler(14f, 16f, -14f), new Vector3(2.4f, 9.8f, 2.4f) * gu);
        }

        private static void BuildChapterSpecificSetDressing(
            Transform root, ArchMats am, Material platformMidMat, CampaignLevelCatalog.ChapterTheme theme, float gu)
        {
            Transform g = CreateGroup("Thematic Environment Dressing", root);
            Material accentTeal = LoadMatByName("Sim_Accent_Teal", am.chapterAccent);
            Material accentGreen = LoadMatByName("Sim_Accent_Green", am.chapterAccent);
            Material objLight = LoadMatByName("Sim_ObjectLight", am.trimMetal);

            switch (theme)
            {
                case CampaignLevelCatalog.ChapterTheme.Translasi:
                    Box(g, "Crane Bridge Rail", am.chamferCube, am.trimMetal, new Vector3(4.5f, 7.4f, 12.8f), new Vector3(16.0f, 0.36f, 0.65f), gu);
                    Box(g, "Crane Trolley", am.chamferCube, am.darkSteel, new Vector3(7.5f, 7.1f, 12.8f), new Vector3(1.6f, 0.72f, 1.0f), gu);
                    Box(g, "Crane Indicator", am.chamferCube, am.chapterAccent, new Vector3(7.5f, 6.72f, 12.8f), new Vector3(0.95f, 0.08f, 0.35f), gu);

                    PlaceFbxProp(g, "Pasar Stalls N", "PasarStall.fbx",
                        new[] { ("Pasar_Frame", am.darkSteel), ("Pasar_Awning", am.chapterAccent), ("Pasar_Lantern", am.warmWindow) },
                        new Vector3(-11.8f, 0f, 11.6f) * gu, 0f, gu * 1.10f);
                    PlaceFbxProp(g, "Pasar Stalls NE", "PasarStall.fbx",
                        new[] { ("Pasar_Frame", am.darkSteel), ("Pasar_Awning", accentTeal), ("Pasar_Lantern", am.warmWindow) },
                        new Vector3(11.8f, 0f, 11.6f) * gu, -35f, gu * 1.10f);
                    PlaceFbxProp(g, "Pasar Stalls E", "PasarStall.fbx",
                        new[] { ("Pasar_Frame", am.darkSteel), ("Pasar_Awning", am.chapterAccent), ("Pasar_Lantern", am.warmWindow) },
                        new Vector3(12.2f, 0f, -6.8f) * gu, -90f, gu * 1.10f);
                    break;

                case CampaignLevelCatalog.ChapterTheme.Elevasi:
                    for (int lvl = 0; lvl < 2; lvl++)
                    {
                        float by = 2.6f + lvl * 3.1f;
                        Box(g, "Rusun Balcony " + lvl, am.chamferSlab, platformMidMat, new Vector3(-9.6f, by, 16.0f), new Vector3(3.8f, 0.24f, 1.4f), gu);
                        Box(g, "Rusun DoorGlow " + lvl, am.chamferSlab, am.warmWindow, new Vector3(-9.6f, by + 1.0f, 16.82f), new Vector3(1.1f, 1.85f, 0.10f), gu);
                        PlaceFbxProp(g, "Rusun Unit " + lvl, "RusunBalconyUnit.fbx",
                            new[] { ("Rusun_Structure", platformMidMat), ("Rusun_ACUnit", am.trimMetal), ("Rusun_PlanterGreens", accentGreen) },
                            new Vector3(-9.6f, by + 0.12f, 15.85f) * gu, 0f, gu * 1.10f);
                    }
                    PlaceFbxProp(g, "Rusun North Corner", "RusunBalconyUnit.fbx",
                        new[] { ("Rusun_Structure", platformMidMat), ("Rusun_ACUnit", am.trimMetal), ("Rusun_PlanterGreens", accentGreen) },
                        new Vector3(-11.8f, 0f, 11.8f) * gu, 0f, gu * 1.15f);
                    PlaceFbxProp(g, "Rusun East Bay", "RusunBalconyUnit.fbx",
                        new[] { ("Rusun_Structure", platformMidMat), ("Rusun_ACUnit", am.trimMetal), ("Rusun_PlanterGreens", accentGreen) },
                        new Vector3(12.2f, 0f, -6.2f) * gu, -90f, gu * 1.15f);
                    break;

                case CampaignLevelCatalog.ChapterTheme.Rotasi:
                    for (int k = 0; k < 2; k++)
                    {
                        float kz = (k == 0) ? -6.0f : 11.0f;
                        PlaceFbxProp(g, "Archive Rack E " + k, "ArchiveMonolithRack.fbx",
                            new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", am.chapterAccent) },
                            new Vector3(12.2f, 0f, kz) * gu, 90f, gu * 1.12f);
                    }
                    PlaceFbxProp(g, "Archive Rack NW", "ArchiveMonolithRack.fbx",
                        new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", accentTeal) },
                        new Vector3(-11.8f, 0f, 11.8f) * gu, 0f, gu * 1.12f);
                    Box(g, "Archive Gimbal Pillar", am.chamferCube, am.trimMetal, new Vector3(13.2f, 6.2f, 13.2f), new Vector3(0.32f, 5.2f, 0.32f), gu);
                    Box(g, "Archive Gimbal Frame", am.chamferCube, am.chapterAccent, new Vector3(13.2f, 7.6f, 13.2f), new Vector3(2.8f, 0.14f, 2.8f), gu);
                    break;

                case CampaignLevelCatalog.ChapterTheme.Rekonstruksi:
                    PlaceFbxProp(g, "Archive Bank NW", "ArchiveMonolithRack.fbx",
                        new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", accentTeal) },
                        new Vector3(-11.8f, 0f, 11.8f) * gu, 0f, gu * 1.12f);
                    PlaceFbxProp(g, "Archive Bank NE", "ArchiveMonolithRack.fbx",
                        new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", accentTeal) },
                        new Vector3(11.8f, 0f, 11.8f) * gu, -35f, gu * 1.12f);
                    PlaceFbxProp(g, "Archive Bank E", "ArchiveMonolithRack.fbx",
                        new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", accentTeal) },
                        new Vector3(12.2f, 0f, -6.2f) * gu, 90f, gu * 1.12f);
                    break;

                case CampaignLevelCatalog.ChapterTheme.Badai:
                    Box(g, "Broken Span Girder 1", am.steelTruss, am.darkSteel, new Vector3(18.8f, 0.4f, -3.5f), new Vector3(4.8f, 0.85f, 1.4f), gu);
                    Box(g, "Broken Span Girder 2", am.chamferCube, am.trimMetal, new Vector3(19.6f, 1.2f, 6.2f), new Vector3(3.6f, 0.42f, 1.1f), gu);
                    PlaceFbxProp(g, "Flood Turbine E1", "FloodTurbinePylon.fbx",
                        new[] { ("Storm_Pylon", am.darkSteel), ("Storm_TurbineShroud", am.trimMetal), ("Storm_WarningCap", am.chapterAccent) },
                        new Vector3(12.2f, 0f, -6.2f) * gu, 90f, gu * 1.20f);
                    PlaceFbxProp(g, "Flood Turbine E2", "FloodTurbinePylon.fbx",
                        new[] { ("Storm_Pylon", am.darkSteel), ("Storm_TurbineShroud", am.trimMetal), ("Storm_WarningCap", am.chapterAccent) },
                        new Vector3(12.2f, 0f, 10.8f) * gu, 90f, gu * 1.20f);
                    PlaceFbxProp(g, "Flood Turbine NW", "FloodTurbinePylon.fbx",
                        new[] { ("Storm_Pylon", am.darkSteel), ("Storm_TurbineShroud", am.trimMetal), ("Storm_WarningCap", accentTeal) },
                        new Vector3(-11.8f, 0f, 11.8f) * gu, 0f, gu * 1.15f);
                    break;

                case CampaignLevelCatalog.ChapterTheme.Showcase:
                    PlaceFbxProp(g, "Showcase Pasar NW", "PasarStall.fbx",
                        new[] { ("Pasar_Frame", am.darkSteel), ("Pasar_Awning", am.warmWindow), ("Pasar_Lantern", objLight) },
                        new Vector3(-11.6f, 0f, 11.6f) * gu, 0f, gu * 1.05f);
                    PlaceFbxProp(g, "Showcase Rusun NE", "RusunBalconyUnit.fbx",
                        new[] { ("Rusun_Structure", platformMidMat), ("Rusun_ACUnit", am.trimMetal), ("Rusun_PlanterGreens", accentGreen) },
                        new Vector3(11.6f, 0f, 11.6f) * gu, -35f, gu * 1.10f);
                    PlaceFbxProp(g, "Showcase Archive E", "ArchiveMonolithRack.fbx",
                        new[] { ("Archive_Cabinet", am.darkSteel), ("Archive_TapeModules", am.trimMetal), ("Archive_IndicatorNodes", accentTeal) },
                        new Vector3(12.2f, 0f, 2.0f) * gu, 90f, gu * 1.12f);
                    PlaceFbxProp(g, "Showcase Turbine SE", "FloodTurbinePylon.fbx",
                        new[] { ("Storm_Pylon", am.darkSteel), ("Storm_TurbineShroud", am.trimMetal), ("Storm_WarningCap", am.chapterAccent) },
                        new Vector3(12.0f, 0f, -7.2f) * gu, 90f, gu * 1.15f);
                    break;
            }
        }

        private static Material LoadMatByName(string name, Material fallback)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/" + name + ".mat");
            return mat != null ? mat : fallback;
        }

        private static void PlaceFbxProp(
            Transform parent,
            string groupName,
            string fbxFileName,
            (string subMesh, Material mat)[] parts,
            Vector3 localPos,
            float yawDeg,
            float scale)
        {
            Transform group = CreateGroup(groupName, parent);
            group.localPosition = localPos;
            group.localRotation = Quaternion.Euler(0f, yawDeg, 0f);
            for (int i = 0; i < parts.Length; i++)
                FbxModelLibrary.CreateSubMeshObject(parts[i].subMesh, fbxFileName, parts[i].subMesh, parts[i].mat, group, scale, true);
        }

        private static Transform CreateGroup(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static GameObject Box(Transform parent, string name, Mesh mesh, Material mat,
            Vector3 gridPos, Vector3 gridScale, float gridUnit)
        {
            return MeshGo(parent, name, mesh, mat, gridPos * gridUnit, Quaternion.identity, gridScale * gridUnit);
        }

        private static GameObject MeshGo(Transform parent, string name, Mesh mesh, Material mat,
            Vector3 localPos, Quaternion localRot, Vector3 localScale)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static Material LoadOrCreateMat(string name, Shader shader)
        {
            string path = "Assets/Materials/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateAccentPanelMat(string name, Color color, float hdrBoost)
        {
            Shader shader = Shader.Find("POLYMULATOR/Sim Surface");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            Material mat = LoadOrCreateMat(name, shader);
            mat.SetColor("_BaseColor", color * 0.32f);
            if (mat.HasProperty("_ShadeColor")) mat.SetColor("_ShadeColor", PaletteSettings.Mantle);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", color);
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", color * hdrBoost);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.35f);
            if (mat.HasProperty("_SeamStrength")) mat.SetFloat("_SeamStrength", 0.25f);
            return mat;
        }

        private static Material CreateTransparentMat(string name, Color color, float hdrBoost, float alpha)
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
            Color c = color * hdrBoost;
            c.a = alpha;
            mat.SetColor("_BaseColor", c);
            return mat;
        }

        private static Material CreateVolumetricMat(string name, Color color, float alpha)
        {
            Shader shader = Shader.Find("POLYMULATOR/Volumetric Beam");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            Material mat = LoadOrCreateMat(name, shader);
            Color c = color;
            c.a = alpha;
            if (mat.HasProperty("_BeamColor")) mat.SetColor("_BeamColor", c);
            else mat.SetColor("_BaseColor", c);
            if (mat.HasProperty("_EdgeFade")) mat.SetFloat("_EdgeFade", 1.65f);
            if (mat.HasProperty("_NoiseSpeed")) mat.SetFloat("_NoiseSpeed", 0.18f);
            return mat;
        }
    }
}
