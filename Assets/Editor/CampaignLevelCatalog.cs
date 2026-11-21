using System.Collections.Generic;
using UnityEngine;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Declarative level specifications for all 12 POLYMULATOR campaign chambers
    /// aligned cleanly to STORYLINE_POLYMULATOR_V2.md and Samudra's MainMenu chapter tree.
    /// </summary>
    public static class CampaignLevelCatalog
    {
        public enum ChapterTheme { Translasi, Elevasi, Rotasi, Badai }

        public struct PlatformSpec
        {
            public string name;
            public Vector3 centerGrid;
            public Vector3 sizeGrid;
            public int tier; // 0=Low, 1=Mid, 2=High
            public bool addSouthSteps;
            public bool addWestSteps;
            public bool addEastSteps;
        }

        public struct WallSpec
        {
            public string name;
            public Vector3 centerGrid;
            public Vector3 sizeGrid;
        }

        public struct PropSpec
        {
            public string name;
            public PrimitiveType shape;
            public Vector3 posGrid;
            public Vector3 sizeGrid;
            public float mass;
        }

        public struct PlateSpec
        {
            public string name;
            public Vector3 posGrid;
            public float requiredMass;
        }

        public struct BarrierSpec
        {
            public string name;
            public Vector3 posGrid;
            public Vector3 sizeGrid;
            public int[] plateIndices;
        }

        public struct BridgeSpec
        {
            public float centerX;
            public float stairStartZ;
            public float baseHeightGrid;
            public int staticStepCount;
            public int dynamicStepCount;
            public float targetHeightGrid;
            public int[] plateIndices;
        }

        public struct FragmentSpec
        {
            public bool enabled;
            public Vector3 posGrid;
            public string title;
            public string speaker;
            public string line;
            public string replySpeaker;
            public string replyLine;
            public bool playPianoMotif;
        }

        public sealed class LevelSpec
        {
            public int chapter;
            public int level;
            public string title;
            public string chapterSubtitle;
            public string districtLabel;
            public ChapterTheme theme;
            public int parActions;

            public Vector3 playerSpawnGrid;
            public Vector3 anchorGrid;
            public Vector3 coreSpawnGrid;
            public float coreStartYaw;
            public bool requireFacingAngle;
            public float requiredFacingYaw;
            public bool triggerGlitchOnClear;
            public bool playPianoOnClear;
            public bool addPianoRoomProp;

            public PlatformSpec[] platforms;
            public WallSpec[] extraWalls;
            public PropSpec[] props;
            public PlateSpec[] plates;
            public BarrierSpec[] barriers;
            public BridgeSpec bridge;
            public FragmentSpec fragment;

            public LevelInfo.StoryLine[] openingDialogue;
            public LevelInfo.StoryLine[] clearDialogue;
            public string[] hints;

            public string SceneName => "Level" + chapter + "." + level;
            public string ScenePath => "Assets/Scenes/" + SceneName + ".unity";
        }

        private static LevelInfo.StoryLine Line(string sp, string txt) =>
            new LevelInfo.StoryLine { speaker = sp, text = txt };

        public static List<LevelSpec> BuildCatalog()
        {
            var list = new List<LevelSpec>(12)
            {
                Build1_1(), Build1_2(), Build1_3(), Build1_4(),
                Build2_1(), Build2_2(), Build2_3(),
                Build3_1(), Build3_2(), Build3_3(),
                Build4_1(), Build4_2()
            };
            return list;
        }

        private static LevelSpec Build1_1() => new LevelSpec
        {
            chapter = 1, level = 1, title = "Warming Up", chapterSubtitle = "TRANSLASI",
            districtLabel = "RUANG UJI 01", theme = ChapterTheme.Translasi, parActions = 6,
            playerSpawnGrid = new Vector3(0f, 0.05f, -6.5f),
            anchorGrid = new Vector3(0f, 0.72f, 3.5f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.6f),
            coreStartYaw = 0f,
            triggerGlitchOnClear = true,
            platforms = new[]
            {
                new PlatformSpec { name = "Platform Low", centerGrid = new Vector3(-7f, 0.2f, 2f), sizeGrid = new Vector3(6f, 0.4f, 5f), tier = 0 },
                new PlatformSpec { name = "Platform Mid", centerGrid = new Vector3(7f, 0.5f, 2.5f), sizeGrid = new Vector3(5f, 1f, 5f), tier = 1 },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 11f), sizeGrid = new Vector3(5f, 2f, 4f), tier = 2 }
            },
            plates = new[] { new PlateSpec { name = "CONDUIT-PAD-01", posGrid = new Vector3(-3.2f, 0f, -1.6f), requiredMass = 1.8f } },
            bridge = new BridgeSpec { stairStartZ = 5.4f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-2.5f, 0.6f, -4.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(2.5f, 0.5f, -3.5f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CUBE-03", shape = PrimitiveType.Cube, posGrid = new Vector3(-8f, 1f, 2f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-04", shape = PrimitiveType.Cube, posGrid = new Vector3(6.5f, 1.5f, 2.5f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CUBE-05", shape = PrimitiveType.Cube, posGrid = new Vector3(-1.6f, 2.5f, 11.4f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CUBE-06", shape = PrimitiveType.Cube, posGrid = new Vector3(4f, 0.6f, 7f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(0f, 0.5f, -2.5f), sizeGrid = Vector3.one, mass = 1f },
                new PropSpec { name = "ORB-02", shape = PrimitiveType.Sphere, posGrid = new Vector3(-5.5f, 0.9f, 3.5f), sizeGrid = Vector3.one, mass = 1f },
                new PropSpec { name = "ORB-03", shape = PrimitiveType.Sphere, posGrid = new Vector3(1.6f, 2.5f, 11.4f), sizeGrid = Vector3.one, mass = 1f },
                new PropSpec { name = "ORB-04", shape = PrimitiveType.Sphere, posGrid = new Vector3(4f, 0.5f, 8f), sizeGrid = Vector3.one, mass = 1f },
                new PropSpec { name = "CYL-01", shape = PrimitiveType.Cylinder, posGrid = new Vector3(9f, 0.5f, -6f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CYL-02", shape = PrimitiveType.Cylinder, posGrid = new Vector3(8f, 1.5f, 1f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            openingDialogue = new[]
            {
                Line("SIM", "Selamat datang di Polymulator, Iterasi #127. Letakkan beban >= 1.8t di Conduit Pad."),
                Line("RECT", "Apa itu iterasi?"),
                Line("SIM", "Jumlah percobaan. Untuk menjadi cukup.")
            },
            clearDialogue = new[]
            {
                Line("SIGNAL", "...Apakah dia merespons?"),
                Line("SIM", "Gangguan arsip sesaat. Abaikan. Memuat Sektor 1.2.")
            },
            hints = new[]
            {
                "Conduit Pad butuh massa >= 1.8t. Bawa CUBE-01 (2.0t) lalu drop [E] atau lempar [F].",
                "Naiki Energy Bridge ke Platform High, ambil CORE-01 emas dan masukkan ke ANCHOR-01."
            }
        };

        private static LevelSpec Build1_2() => new LevelSpec
        {
            chapter = 1, level = 2, title = "Pasar", chapterSubtitle = "TRANSLASI",
            districtLabel = "JALAN PASAR", theme = ChapterTheme.Translasi, parActions = 8,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, -1.0f),
            coreSpawnGrid = new Vector3(-7.0f, 0.90f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Kios Barat", centerGrid = new Vector3(-7f, 0.2f, 5.5f), sizeGrid = new Vector3(5.5f, 0.4f, 6f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Kios Timur", centerGrid = new Vector3(7f, 0.4f, 4f), sizeGrid = new Vector3(5f, 0.8f, 5f), tier = 1, addSouthSteps = true }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Arch Back", centerGrid = new Vector3(-7f, 1.3f, 8.3f), sizeGrid = new Vector3(5.6f, 2.2f, 0.4f) },
                new WallSpec { name = "Arch West", centerGrid = new Vector3(-9.6f, 1.3f, 5.5f), sizeGrid = new Vector3(0.4f, 2.2f, 5.6f) },
                new WallSpec { name = "Arch East", centerGrid = new Vector3(-4.4f, 1.3f, 5.5f), sizeGrid = new Vector3(0.4f, 2.2f, 5.6f) }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-PAD-A", posGrid = new Vector3(-3.4f, 0f, -3.5f), requiredMass = 1.8f },
                new PlateSpec { name = "CONDUIT-PAD-B", posGrid = new Vector3(3.4f, 0f, -3.5f), requiredMass = 2.0f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Market Gate Barrier", posGrid = new Vector3(-7f, 1.4f, 2.65f), sizeGrid = new Vector3(4.8f, 2.0f, 0.25f), plateIndices = new[] { 0, 1 } }
            },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-5.8f, 0.6f, -6.2f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(2.8f, 0.5f, -6.2f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(5.5f, 0.5f, -5.2f), sizeGrid = Vector3.one, mass = 1f },
                new PropSpec { name = "CYL-01", shape = PrimitiveType.Cylinder, posGrid = new Vector3(6.8f, 1.3f, 3.8f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "ORB-02", shape = PrimitiveType.Sphere, posGrid = new Vector3(-1.2f, 0.5f, -5.8f), sizeGrid = Vector3.one, mass = 1f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-11.2f, 0.65f, 8.2f),
                title = "Gambar Nara (4 thn)",
                speaker = "RECT", line = "Ada gambar kotak biru dengan dua mata di balik lengkungan. Tertulis: 'Nara, 4 tahun.'",
                replySpeaker = "SIM", replyLine = "Data tidak lengkap. Lanjutkan rekonstruksi pasar."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Distrik Jalan Pasar dimuat. Dua Conduit Pad harus aktif bersamaan."),
                Line("RECT", "Hanya ada satu balok 2.0t."),
                Line("SIM", "Massa bersifat aditif. Gabungkan dua beban lebih ringan di satu pad.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Bagus... rig di Tepi mulai membersihkan puing Jalan Pasar."),
                Line("SIM", "Membuka jalur Sektor 1.3.")
            },
            hints = new[]
            {
                "Pad B butuh >= 2.0t tapi CUBE-01 (2.0t) terpakai di Pad A. Gabungkan CUBE-02 (1.5t) + ORB-01 (1.0t) = 2.5t!",
                "Tengok sudut kiri belakang Kios Barat untuk fragmen 'Gambar Nara'."
            }
        };

        private static LevelSpec Build1_3() => new LevelSpec
        {
            chapter = 1, level = 3, title = "Tag AD-I", chapterSubtitle = "TRANSLASI",
            districtLabel = "LENGKUNGAN PASAR", theme = ChapterTheme.Translasi, parActions = 7,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8f),
            anchorGrid = new Vector3(0f, 0.72f, 0.5f),
            coreSpawnGrid = new Vector3(7f, 2.48f, 6.0f),
            platforms = new[]
            {
                new PlatformSpec { name = "Balkon Barat", centerGrid = new Vector3(-7f, 0.5f, 0f), sizeGrid = new Vector3(5.2f, 1f, 5.2f), tier = 0, addWestSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(7f, 1f, 6f), sizeGrid = new Vector3(5f, 2f, 5f), tier = 2 }
            },
            plates = new[] { new PlateSpec { name = "CONDUIT-PAD-01", posGrid = new Vector3(-7f, 1.0f, 0f), requiredMass = 1.8f } },
            bridge = new BridgeSpec { centerX = 7f, stairStartZ = -0.1f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0 } },
            extraWalls = new[]
            {
                // Stepped offset X for bridge is shifted to East platform (x = +7) cleanly via stairShift! Let's build East ramp cleanly as stairs
                new WallSpec { name = "Parapet Barat", centerGrid = new Vector3(-4.35f, 0.7f, 0f), sizeGrid = new Vector3(0.25f, 1.4f, 5.0f) }
            },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-1.8f, 0.6f, -5.2f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(3.2f, 0.5f, -4.5f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(-2.8f, 0.5f, 4.2f), sizeGrid = Vector3.one, mass = 1f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-11.2f, 0.65f, 5.5f),
                title = "Tag Logam AD-I",
                speaker = "RECT", line = "Ada tag logam kecil terjatuh dari lengkungan batu: 'AD-I'.",
                replySpeaker = "MIRA", replyLine = "Jangan sentuh itu! ...Lanjutkan saja ke Anchor."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Pad berada di atas Balkon Barat. Lemparan parabola [F] atau tangga barat dapat menjangkaunya.")
            },
            clearDialogue = new[]
            {
                Line("RECT", "Kenapa Mira melarangku menyentuh nama di tag logam itu?"),
                Line("SIM", "Tag AD-I ditandai: tidak terindeks.")
            },
            hints = new[]
            {
                "Bawa CUBE-01 (2.0t) melewati tangga di sisi barat Balkon Barat, atau lempar dengan Throw Arc [F] melewati parapet.",
                "Periksa sudut barat laut untuk menemukan Tag Logam AD-I."
            }
        };

        private static LevelSpec Build1_4() => new LevelSpec
        {
            chapter = 1, level = 4, title = "Ujian Pasar", chapterSubtitle = "TRANSLASI",
            districtLabel = "GERBANG ARSIP TIMUR", theme = ChapterTheme.Translasi, parActions = 10,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, -4.2f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 9.8f),
            platforms = new[]
            {
                new PlatformSpec { name = "Platform Vault Timur", centerGrid = new Vector3(8.5f, 0.2f, -1f), sizeGrid = new Vector3(4.5f, 0.4f, 4.5f), tier = 0, addWestSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 10f), sizeGrid = new Vector3(5.2f, 2f, 4.2f), tier = 2 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Vault N", centerGrid = new Vector3(8.5f, 1.3f, 1.35f), sizeGrid = new Vector3(4.6f, 2.2f, 0.35f) },
                new WallSpec { name = "Vault S", centerGrid = new Vector3(8.5f, 1.3f, -3.35f), sizeGrid = new Vector3(4.6f, 2.2f, 0.35f) },
                new WallSpec { name = "Vault E", centerGrid = new Vector3(10.8f, 1.3f, -1f), sizeGrid = new Vector3(0.35f, 2.2f, 4.6f) }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-VAULT-A", posGrid = new Vector3(-5.2f, 0f, -3.5f), requiredMass = 1.4f },
                new PlateSpec { name = "CONDUIT-BRIDGE-B", posGrid = new Vector3(-3.5f, 0f, 2.0f), requiredMass = 1.8f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Vault Door Barrier", posGrid = new Vector3(6.2f, 1.4f, -1f), sizeGrid = new Vector3(0.25f, 2.0f, 4.4f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec { stairStartZ = 4.3f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 1 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(-2.5f, 0.5f, -6.2f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(8.5f, 0.8f, -1f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(2.8f, 0.5f, -6.2f), sizeGrid = Vector3.one, mass = 1f }
            },
            openingDialogue = new[]
            {
                Line("SIM", "Ujian Bab 1: Kaskade Ruang. Bebaskan beban berat dari kubah timur terlebih dahulu.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Pintu Menara Arsip terbuka! Persiapkan modul Elevasi, RECT."),
                Line("SIM", "Memasuki Babak II: Rusun Matahari.")
            },
            hints = new[]
            {
                "Taruh CUBE-02 (1.5t) di Pad Barat (CONDUIT-VAULT-A) agar Energy Barrier kubah timur terbuka.",
                "Ambil CUBE-01 (2.0t) dari kubah timur lalu pindahkan ke Pad Utara untuk menyalakan Energy Bridge."
            }
        };

        private static LevelSpec Build2_1() => new LevelSpec
        {
            chapter = 2, level = 1, title = "Angkat", chapterSubtitle = "ELEVASI",
            districtLabel = "RUSUN MATAHARI // L1", theme = ChapterTheme.Elevasi, parActions = 7,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8f),
            anchorGrid = new Vector3(-4.5f, 0.72f, 0f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 9.8f),
            platforms = new[]
            {
                new PlatformSpec { name = "Balkon Timur", centerGrid = new Vector3(6.8f, 0.5f, -0.5f), sizeGrid = new Vector3(5.2f, 1.0f, 5.5f), tier = 1, addSouthSteps = true },
                new PlatformSpec { name = "Atap Rusun", centerGrid = new Vector3(0f, 1.0f, 10f), sizeGrid = new Vector3(5.2f, 2.0f, 4.2f), tier = 2 }
            },
            plates = new[] { new PlateSpec { name = "CONDUIT-ELEV-01", posGrid = new Vector3(6.8f, 1.0f, -0.5f), requiredMass = 1.8f } },
            bridge = new BridgeSpec { stairStartZ = 4.3f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-2.5f, 0.6f, -5.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(3.2f, 0.5f, -5.5f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(6.8f, 1.5f, 1.2f), sizeGrid = Vector3.one, mass = 1f }
            },
            openingDialogue = new[]
            {
                Line("SIM", "ELEVASI. Ketinggian mengubah cara ruang terbaca."),
                Line("RECT", "Angka iterasi di memori terus tercatat."),
                Line("SIM", "Fokus pada atap Rusun Matahari.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Satu platform logistik berhasil diangkat! Masih ada warga di atap blok sebelah.")
            },
            hints = new[]
            {
                "Pad terletak di atas Balkon Timur (+1.0 GU). Bawa CUBE-01 menaiki tangga selatan Balkon Timur.",
                "Gunakan Telekinesis [Tab] jika ingin mengangkat beban dari lantai langsung ke atas."
            }
        };

        private static LevelSpec Build2_2() => new LevelSpec
        {
            chapter = 2, level = 2, title = "Susun", chapterSubtitle = "ELEVASI",
            districtLabel = "RUSUN MATAHARI // L2", theme = ChapterTheme.Elevasi, parActions = 9,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, 0f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Sayap Barat", centerGrid = new Vector3(-7.2f, 0.25f, 0f), sizeGrid = new Vector3(4.8f, 0.5f, 5.2f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Sayap Timur", centerGrid = new Vector3(7.2f, 0.5f, 0f), sizeGrid = new Vector3(4.8f, 1.0f, 5.2f), tier = 1, addSouthSteps = true },
                new PlatformSpec { name = "Atap Rusun", centerGrid = new Vector3(0f, 1.0f, 10.2f), sizeGrid = new Vector3(5.2f, 2.0f, 4.2f), tier = 2 }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-WEST", posGrid = new Vector3(-7.2f, 0.5f, 0f), requiredMass = 1.4f },
                new PlateSpec { name = "CONDUIT-EAST", posGrid = new Vector3(7.2f, 1.0f, 0f), requiredMass = 1.8f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Atap Shield", posGrid = new Vector3(0f, 2.5f, 8.2f), sizeGrid = new Vector3(4.8f, 1.8f, 0.25f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec { stairStartZ = 4.5f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 1 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.0f, 0.6f, -6.0f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(4.0f, 0.5f, -6.0f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CYL-01", shape = PrimitiveType.Cylinder, posGrid = new Vector3(0f, 0.5f, -5.0f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            openingDialogue = new[]
            {
                Line("DANU", "Tepi makin krisis daya, Mira. Jangan paksakan menara kalau belum seimbang."),
                Line("SIM", "Seimbangkan Sayap Barat (1.4t) dan Sayap Timur (1.8t) untuk akses atap.")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Keseimbangan tercapai. Bersiap menghadapi getaran menara pada Sektor 2.3.")
            },
            hints = new[]
            {
                "Pad Barat membuka perisai energi atap, sedangkan Pad Timur menyalakan Energy Bridge.",
                "Letakkan CUBE-02 / CYL-01 (1.5t) di Sayap Barat dan CUBE-01 (2.0t) di Sayap Timur."
            }
        };

        private static LevelSpec Build2_3() => new LevelSpec
        {
            chapter = 2, level = 3, title = "Kotak Musik", chapterSubtitle = "ELEVASI",
            districtLabel = "MENARA RUSUN", theme = ChapterTheme.Elevasi, parActions = 9,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, 1.5f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.5f),
            platforms = new[]
            {
                new PlatformSpec { name = "Pelindung Kotak Musik", centerGrid = new Vector3(8.0f, 0.25f, -2.5f), sizeGrid = new Vector3(4.5f, 0.5f, 4.5f), tier = 0, addWestSteps = true },
                new PlatformSpec { name = "Menara Atas", centerGrid = new Vector3(0f, 1.0f, 10.5f), sizeGrid = new Vector3(5.2f, 2.0f, 4.2f), tier = 2 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Ruang Musik N", centerGrid = new Vector3(8.0f, 1.35f, -0.35f), sizeGrid = new Vector3(4.5f, 2.2f, 0.3f) },
                new WallSpec { name = "Ruang Musik S", centerGrid = new Vector3(8.0f, 1.35f, -4.65f), sizeGrid = new Vector3(4.5f, 2.2f, 0.3f) },
                new WallSpec { name = "Ruang Musik E", centerGrid = new Vector3(10.2f, 1.35f, -2.5f), sizeGrid = new Vector3(0.3f, 2.2f, 4.5f) }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-TOWER", posGrid = new Vector3(-4.5f, 0f, -1.5f), requiredMass = 1.8f },
                new PlateSpec { name = "CONDUIT-ARCHIVE", posGrid = new Vector3(3.5f, 0f, -5.0f), requiredMass = 1.4f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Shelter Gate", posGrid = new Vector3(5.8f, 1.4f, -2.5f), sizeGrid = new Vector3(0.25f, 2.0f, 4.2f), plateIndices = new[] { 1 } }
            },
            bridge = new BridgeSpec { stairStartZ = 4.8f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-3.0f, 0.6f, -5.8f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(1.8f, 0.5f, -6.2f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(8.3f, 1.0f, -2.5f),
                title = "Kotak Musik Adi", playPianoMotif = true,
                speaker = "MIRA", line = "Simpan melodi itu... itu satu-satunya suara Adi yang masih tersisa.",
                replySpeaker = "RECT", replyLine = "Lima nada. Kenapa suaranya tidak ada di tabel data?"
            },
            openingDialogue = new[]
            {
                Line("SIM", "PERINGATAN: stabilitas menara menurun. Fokus pada CORE-01."),
                Line("RECT", "Ada benda berdenyut di dalam reruntuhan timur.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Terima kasih telah memeriksanya, RECT..."),
                Line("SIM", "Memasuki Babak III: Gedung Arsip Vela.")
            },
            hints = new[]
            {
                "Gunakan CUBE-02 (1.5t) pada Pad Timur untuk menyelamatkan Kotak Musik Adi (5 nada piano!) sebelum menyelesaikan menara.",
                "Gunakan CUBE-01 (2.0t) pada Pad Barat agar jembatan Menara Atas aktif."
            }
        };

        private static LevelSpec Build3_1() => new LevelSpec
        {
            chapter = 3, level = 1, title = "Kunci Orientasi", chapterSubtitle = "ROTASI",
            districtLabel = "GEDUNG ARSIP // PINTU", theme = ChapterTheme.Rotasi, parActions = 6,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.5f),
            anchorGrid = new Vector3(0f, 0.72f, 1.8f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 9.8f),
            coreStartYaw = 0f,
            requireFacingAngle = true,
            requiredFacingYaw = 90f,
            platforms = new[]
            {
                new PlatformSpec { name = "Rak Arsip Barat", centerGrid = new Vector3(-7.5f, 0.4f, 1.5f), sizeGrid = new Vector3(4.5f, 0.8f, 5.5f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 10f), sizeGrid = new Vector3(5.2f, 2f, 4.2f), tier = 2 }
            },
            plates = new[] { new PlateSpec { name = "CONDUIT-ARCHIVE-01", posGrid = new Vector3(-3.8f, 0f, -2.5f), requiredMass = 1.8f } },
            bridge = new BridgeSpec { stairStartZ = 4.3f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(2.8f, 0.6f, -5.0f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CYL-01", shape = PrimitiveType.Cylinder, posGrid = new Vector3(-6.8f, 1.3f, 1.5f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            openingDialogue = new[]
            {
                Line("SIM", "ROTASI Ry(θ). Bentuk tetap sama; arah hadap menentukan kecocokan."),
                Line("RECT", "Apakah benda yang sama bisa salah hanya karena menghadap ke sisi lain?")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Kalibrasi arah 90° valid. Pintu Gedung Arsip bergeser membuka koridor dalam.")
            },
            hints = new[]
            {
                "Perhatikan indikator anak panah pada ANCHOR-01 menghadap 90 deg (Timur).",
                "Saat memegang CORE-01, tekan [R] untuk memutar Ry(+90 deg) hingga garis visor sejajar panah Anchor."
            }
        };

        private static LevelSpec Build3_2() => new LevelSpec
        {
            chapter = 3, level = 2, title = "Bias", chapterSubtitle = "ROTASI",
            districtLabel = "RUANG CERMIN ARSIP", theme = ChapterTheme.Rotasi, parActions = 9,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, -1.2f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.2f),
            coreStartYaw = 180f,
            requireFacingAngle = true,
            requiredFacingYaw = 270f,
            platforms = new[]
            {
                new PlatformSpec { name = "Cermin Barat", centerGrid = new Vector3(-7.5f, 0.25f, 1.5f), sizeGrid = new Vector3(4.5f, 0.5f, 5f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Cermin Timur", centerGrid = new Vector3(7.5f, 0.25f, 1.5f), sizeGrid = new Vector3(4.5f, 0.5f, 5f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 10.2f), sizeGrid = new Vector3(5.2f, 2f, 4.2f), tier = 2 }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-MIRROR-W", posGrid = new Vector3(-3.8f, 0f, -3.8f), requiredMass = 1.4f },
                new PlateSpec { name = "CONDUIT-MIRROR-E", posGrid = new Vector3(3.8f, 0f, -3.8f), requiredMass = 1.8f }
            },
            bridge = new BridgeSpec { stairStartZ = 4.5f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0, 1 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(6.8f, 1.1f, 1.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(-6.8f, 1.0f, 1.5f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-9.5f, 1.0f, 8.2f),
                title = "Foto Adi Muda",
                speaker = "DANU", line = "Ini foto Adi di depan pianonya. Dia bukan sekadar baris kode di server, Mira.",
                replySpeaker = "RECT", replyLine = "Tersimpan: Adi. Nama manusia."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Dua pelat cermin menuntut kesetimbangan sebelum jembatan terbuka. Kunci menuntut sudut 270 deg (Barat).")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Rekonstruksi Cermin selesai. Terdeteksi ruang tersembunyi tanpa indeks pada Sektor 3.3.")
            },
            hints = new[]
            {
                "Kedua Pad (Barat >= 1.4t, Timur >= 1.8t) harus aktif bersamaan.",
                "CORE-01 awalnya menghadap 180 deg (Selatan). Tekan [R] satu kali (+90 deg -> 270 deg Barat) atau gunakan [V] Blueprint."
            }
        };

        private static LevelSpec Build3_3() => new LevelSpec
        {
            chapter = 3, level = 3, title = "Ruang Piano", chapterSubtitle = "ROTASI",
            districtLabel = "APARTEMEN TAK TERINDEKS", theme = ChapterTheme.Rotasi, parActions = 10,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, 0.5f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.2f),
            coreStartYaw = 90f,
            requireFacingAngle = true,
            requiredFacingYaw = 0f,
            playPianoOnClear = true,
            addPianoRoomProp = true,
            platforms = new[]
            {
                new PlatformSpec { name = "Ruang Piano Hangat", centerGrid = new Vector3(-8.2f, 0.25f, 3.5f), sizeGrid = new Vector3(5.2f, 0.5f, 5.2f), tier = 0, addEastSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 10.2f), sizeGrid = new Vector3(5.2f, 2f, 4.2f), tier = 2 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Piano Room N", centerGrid = new Vector3(-8.2f, 1.4f, 6.2f), sizeGrid = new Vector3(5.2f, 2.3f, 0.3f) },
                new WallSpec { name = "Piano Room S", centerGrid = new Vector3(-8.2f, 1.4f, 0.85f), sizeGrid = new Vector3(5.2f, 2.3f, 0.3f) },
                new WallSpec { name = "Piano Room W", centerGrid = new Vector3(-10.9f, 1.4f, 3.5f), sizeGrid = new Vector3(0.3f, 2.3f, 5.2f) }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-PIANO", posGrid = new Vector3(-4.2f, 0f, -2.5f), requiredMass = 1.4f },
                new PlateSpec { name = "CONDUIT-NORTH", posGrid = new Vector3(4.2f, 0f, -2.5f), requiredMass = 1.8f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Ruang Tersembunyi Barrier", posGrid = new Vector3(-5.6f, 1.4f, 3.5f), sizeGrid = new Vector3(0.25f, 2.0f, 5.0f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec { stairStartZ = 4.5f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 1 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(4.5f, 0.6f, -6.0f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(-3.8f, 0.5f, -6.0f), sizeGrid = Vector3.one, mass = 1.5f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-8.2f, 1.0f, 4.3f),
                title = "Pita Rekaman Ruang Piano", playPianoMotif = true,
                speaker = "MIRA", line = "Jangan hapus ruangan itu, SIM! Aku membangunnya agar dia tidak benar-benar pergi.",
                replySpeaker = "RECT", replyLine = "Kalau sesuatu tidak ada di arsip, apakah berarti tidak pernah ada?"
            },
            openingDialogue = new[]
            {
                Line("SIM", "PERINGATAN: Anomali ruangan tak terindeks terdeteksi di barat."),
                Line("MIRA", "Jangan sentuh penghapusannya, SIM. Biarkan saja di situ.")
            },
            clearDialogue = new[]
            {
                Line("DANU", "ITERATION 47 // JANGAN HAPUS. Mereka juga mencoba melindunginya."),
                Line("SIM", "Memasuki Babak IV: Jembatan Lama & Peringatan Badai.")
            },
            hints = new[]
            {
                "Aktifkan Pad Barat (>= 1.4t) untuk membuka Ruang Piano hangat dan dengarkan rekaman Adi.",
                "Aktifkan Pad Timur (2.0t), ambil CORE-01, dan putar [R] hingga menghadap 0 deg (Utara) ke Anchor."
            }
        };

        private static LevelSpec Build4_1() => new LevelSpec
        {
            chapter = 4, level = 1, title = "Dua Sisi", chapterSubtitle = "REKONSTRUKSI",
            districtLabel = "JEMBATAN LAMA VELA", theme = ChapterTheme.Badai, parActions = 11,
            playerSpawnGrid = new Vector3(0f, 0.05f, -8.5f),
            anchorGrid = new Vector3(0f, 0.72f, -3.5f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.2f),
            coreStartYaw = 0f,
            requireFacingAngle = true,
            requiredFacingYaw = 180f,
            platforms = new[]
            {
                new PlatformSpec { name = "Dermaga Timur", centerGrid = new Vector3(7.2f, 0.5f, 1.5f), sizeGrid = new Vector3(5.0f, 1.0f, 5.2f), tier = 1, addSouthSteps = true },
                new PlatformSpec { name = "Platform High", centerGrid = new Vector3(0f, 1f, 10.2f), sizeGrid = new Vector3(5.5f, 2f, 4.2f), tier = 2 }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-WEST-01", posGrid = new Vector3(-5.2f, 0f, -0.5f), requiredMass = 2.0f },
                new PlateSpec { name = "CONDUIT-EAST-02", posGrid = new Vector3(7.2f, 1.0f, 1.5f), requiredMass = 1.8f }
            },
            bridge = new BridgeSpec { stairStartZ = 4.5f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 0, 1 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(-5.2f, 0.6f, -5.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-06", shape = PrimitiveType.Cube, posGrid = new Vector3(2.8f, 0.6f, -5.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(7.2f, 1.5f, 3.2f), sizeGrid = Vector3.one, mass = 1f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(7.2f, 1.75f, 7.2f),
                title = "Data Echo Warga Vela",
                speaker = "DANU", line = "Dengar gema radio itu: warga saling berjanji bertemu di dermaga selatan.",
                replySpeaker = "RECT", replyLine = "Jembatan menghubungkan dua sisi, tapi tidak bisa menggantikan yang hilang."
            },
            openingDialogue = new[]
            {
                Line("MIRA", "Dua sisi Tepi berselisih antara tanggul dan taman. Kita butuh jembatan stabil sekarang."),
                Line("SIM", "Pad Barat (2.0t) dan Pad Dermaga Timur (1.8t) menyalakan Jembatan Lama. Anchor menuntut sudut 180 deg.")
            },
            clearDialogue = new[]
            {
                Line("SIM", "PERINGATAN CUACA: Badai Ibu mendekati batas kritis. Bersiap untuk kalibrasi akhir.")
            },
            hints = new[]
            {
                "Bawa CUBE-01 ke Pad Barat dan CUBE-06 menaiki tangga selatan ke Pad Dermaga Timur (+1.0 GU).",
                "Putar CORE-01 dengan [R] dua kali (0 -> 180 deg Selatan) sebelum masuk ANCHOR-01."
            }
        };

        private static LevelSpec Build4_2() => new LevelSpec
        {
            chapter = 4, level = 2, title = "Ambang Badai", chapterSubtitle = "REKONSTRUKSI",
            districtLabel = "AMBANG BADAI // TEPI", theme = ChapterTheme.Badai, parActions = 13,
            playerSpawnGrid = new Vector3(0f, 0.05f, -9.0f),
            anchorGrid = new Vector3(0f, 0.72f, -4.8f),
            coreSpawnGrid = new Vector3(0f, 2.48f, 10.6f),
            coreStartYaw = 270f,
            requireFacingAngle = true,
            requiredFacingYaw = 90f,
            triggerGlitchOnClear = true,
            playPianoOnClear = true,
            platforms = new[]
            {
                new PlatformSpec { name = "Vault Gudang Timur", centerGrid = new Vector3(8.5f, 0.2f, -3.5f), sizeGrid = new Vector3(4.5f, 0.4f, 5.0f), tier = 0 },
                new PlatformSpec { name = "Menara Arsip Tepi", centerGrid = new Vector3(0f, 1f, 10.6f), sizeGrid = new Vector3(5.6f, 2f, 4.2f), tier = 2 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Storm Vault N", centerGrid = new Vector3(8.5f, 1.3f, -0.85f), sizeGrid = new Vector3(4.6f, 2.2f, 0.35f) },
                new WallSpec { name = "Storm Vault S", centerGrid = new Vector3(8.5f, 1.3f, -6.15f), sizeGrid = new Vector3(4.6f, 2.2f, 0.35f) },
                new WallSpec { name = "Storm Vault E", centerGrid = new Vector3(10.8f, 1.3f, -3.5f), sizeGrid = new Vector3(0.35f, 2.2f, 5.2f) }
            },
            plates = new[]
            {
                new PlateSpec { name = "CONDUIT-VAULT-RELEASE", posGrid = new Vector3(-6.5f, 0f, -4.5f), requiredMass = 1.4f },
                new PlateSpec { name = "CONDUIT-STORM-WEST", posGrid = new Vector3(-4.2f, 0f, 2.5f), requiredMass = 2.0f },
                new PlateSpec { name = "CONDUIT-STORM-EAST", posGrid = new Vector3(4.2f, 0f, 2.5f), requiredMass = 2.0f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "Storm Vault Gate", posGrid = new Vector3(6.2f, 1.4f, -3.5f), sizeGrid = new Vector3(0.25f, 2.0f, 4.8f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec { stairStartZ = 4.9f, staticStepCount = 2, dynamicStepCount = 2, targetHeightGrid = 2f, plateIndices = new[] { 1, 2 } },
            props = new[]
            {
                new PropSpec { name = "CUBE-02", shape = PrimitiveType.Cube, posGrid = new Vector3(-3.0f, 0.5f, -7.2f), sizeGrid = Vector3.one, mass = 1.5f },
                new PropSpec { name = "CUBE-01", shape = PrimitiveType.Cube, posGrid = new Vector3(8.5f, 0.8f, -2.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "CUBE-06", shape = PrimitiveType.Cube, posGrid = new Vector3(8.5f, 0.8f, -4.5f), sizeGrid = Vector3.one * 1.2f, mass = 2f },
                new PropSpec { name = "ORB-01", shape = PrimitiveType.Sphere, posGrid = new Vector3(3.0f, 0.5f, -7.2f), sizeGrid = Vector3.one, mass = 1f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(4.8f, 0.65f, 10.6f),
                title = "Rekaman RECT-126",
                speaker = "MIRA", line = "...Maaf. Aku memutus kill switch Dewan agar kau bisa memilih sendiri, RECT.",
                replySpeaker = "RECT", replyLine = "Untuk pertama kalinya, ini bukan sekadar objektif. Ini keputusanku."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Ujian Akhir Badai. Bebaskan material tanggul, aktifkan jembatan menara, dan kunci CORE pada 90 deg."),
                Line("MIRA", "Semua bergantung pada kalibrasi terakhirmu!")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Objektif tercapai: selamatkan mereka."),
                Line("RECT", "Ruang untuk pulang terbuka.")
            },
            hints = new[]
            {
                "1) Taruh CUBE-02 (1.5t) di Pad Barat Daya untuk membebaskan CUBE-01 & CUBE-06 (2.0t).",
                "2) Bawa keduanya ke Pad Barat & Pad Timur (masing-masing 2.0t), lalu ambil CORE-01 & putar [R] dari 270 deg -> 90 deg!"
            }
        };
    }
}
