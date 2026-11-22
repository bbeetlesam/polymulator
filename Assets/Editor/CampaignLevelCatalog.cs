using System.Collections.Generic;
using UnityEngine;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Authoritative level catalog covering Chapters 1-5 of Kota Tepi (14 chambers)
    /// plus the always-unlocked Special Showcase chamber (LevelShowcase).
    /// </summary>
    public static class CampaignLevelCatalog
    {
        public enum ChapterTheme { Translasi, Elevasi, Rotasi, Badai, Rekonstruksi, Showcase }

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
            public bool disableSplit;
            public bool disableFuse;
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
            public string customSceneName;
            public string title;
            public string chapterSubtitle;
            public string districtLabel;
            public ChapterTheme theme;
            public int parActions;

            public bool allowPartition;
            public bool allowFusion;
            public bool isEndingChamber;
            public bool isShowcaseChamber;

            public Vector3 playerSpawnGrid;
            public Vector3 anchorGrid;
            public Vector3 coreSpawnGrid;
            public float coreStartYaw;
            public float coreMass = 1.2f;
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

            public string SceneName => !string.IsNullOrEmpty(customSceneName)
                ? customSceneName
                : "Level" + chapter + "." + level;

            public string ScenePath => "Assets/Scenes/" + SceneName + ".unity";
        }

        private static LevelInfo.StoryLine Line(string speaker, string text)
        {
            return new LevelInfo.StoryLine { speaker = speaker, text = text };
        }

        public static List<LevelSpec> BuildCatalog()
        {
            return new List<LevelSpec>(15)
            {
                Build1_1(), Build1_2(), Build1_3(),
                Build2_1(), Build2_2(), Build2_3(),
                Build3_1(), Build3_2(), Build3_3(),
                Build4_1(), Build4_2(), Build4_3(),
                Build5_1(), Build5_2(),
                BuildShowcase()
            };
        }

        // =====================================================================
        // CHAPTER 1 — TRANSLASI (Mengenal Ruang & Jejak Awal)
        // =====================================================================

        private static LevelSpec Build1_1() => new LevelSpec
        {
            chapter = 1, level = 1,
            title = "Bangun di Ruangan Kosong",
            chapterSubtitle = "CHAPTER 1  •  TRANSLASI",
            districtLabel = "RUANG UJI 01", theme = ChapterTheme.Translasi, parActions = 5,
            playerSpawnGrid = new Vector3(0f, 0.05f, -6.5f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -1.6f),
            anchorGrid = new Vector3(0f, 0f, 5.4f),
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-4.8f, 0.52f, 0.6f),
                title = "Suara di Balik Kaca",
                speaker = "VELA", line = "Kalau kau masih bisa mendengar rekaman ini, Rect... Kota Tepi belum benar-benar habis.",
                replySpeaker = "MIRA", replyLine = "Kita tinggal di ruangan sunyi ini bersama suara yang sempat tertinggal."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Sesi simulasi dimulai. Unit Rect aktif kembali di Ruang Uji 01."),
                Line("TEPI", "Sebuah inti kuning menyala di tengah lantai. Seseorang meninggalkannya tepat di jalurmu.")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Kunci utama terpasang. Pintu menuju Distrik Pasar terbuka."),
                Line("TEPI", "Di balik cahaya dinding ruangan ini, bayangan jalanan lama mulai terlihat.")
            },
            hints = new[]
            {
                "Gerakkan Rect dengan WASD atau tombol panah mendekati kubus kuning CORE-01.",
                "Tekan [E] untuk mengangkat CORE-01, bawa ke soket bulat ANCHOR-01 di utara, lalu tekan [E] atau [Q] untuk meletakkan.",
                "Kristal merah muda di sisi kiri bersifat opsional — sentuh untuk mendengar fragmen cerita."
            }
        };

        private static LevelSpec Build1_2() => new LevelSpec
        {
            chapter = 1, level = 2,
            title = "Jalan Pasar",
            chapterSubtitle = "CHAPTER 1  •  TRANSLASI",
            districtLabel = "JALAN PASAR LAMA", theme = ChapterTheme.Translasi, parActions = 8,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(4.0f, 0.48f, -2.4f),
            anchorGrid = new Vector3(0f, 0f, 6.8f),
            extraWalls = new[]
            {
                new WallSpec { name = "Gate Wall L", centerGrid = new Vector3(-6.8f, 1.5f, 3.2f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) },
                new WallSpec { name = "Gate Wall R", centerGrid = new Vector3(6.8f, 1.5f, 3.2f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) }
            },
            props = new[]
            {
                new PropSpec { name = "BEBAN-PASAR", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.0f, 0.50f, -3.4f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-PASAR", posGrid = new Vector3(-4.0f, 0f, 0.8f), requiredMass = 1.0f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "GERBANG-PASAR", posGrid = new Vector3(0f, 1.35f, 3.2f), sizeGrid = new Vector3(6.2f, 2.7f, 0.35f), plateIndices = new[] { 0 } }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(6.2f, 0.52f, 0.6f),
                title = "Gerai Roti Pagi",
                speaker = "MIRA", line = "Dulu bau roti selalu sampai ke tikungan sebelum matahari naik.",
                replySpeaker = "VELA", replyLine = "Sekarang yang tersisa hanya kotak penahan dan pagar cahaya."
            },
            openingDialogue = new[]
            {
                Line("MIRA", "Gerbang merah di depan dikunci pelat beban di lantai kiri."),
                Line("TEPI", "Cari balok pemberat di sekitar lorong agar pagarnya padam.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Bagus, jalannya terbuka lagi. Ayo terus ke arah gedung arsip.")
            },
            hints = new[]
            {
                "Angkat balok BEBAN-PASAR dengan [E] lalu letakkan di atas pelat lantai PAD-PASAR di kiri.",
                "Saat pelat berubah hijau dan pagar merah menghilang, bawa CORE-01 ke ANCHOR-01 di utara.",
                "Tips: tekan [Klik Kiri] saat memegang balok jika ingin mendorongnya ke depan."
            }
        };

        private static LevelSpec Build1_3() => new LevelSpec
        {
            chapter = 1, level = 3,
            title = "Gerbang Arsip Timur",
            chapterSubtitle = "CHAPTER 1  •  TRANSLASI",
            districtLabel = "GERBANG ARSIP", theme = ChapterTheme.Translasi, parActions = 10,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.5f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -4.4f),
            anchorGrid = new Vector3(0f, 1.40f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Dek Gerbang Arsip", centerGrid = new Vector3(0f, 0.70f, 5.8f), sizeGrid = new Vector3(7.6f, 1.40f, 4.6f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "BEBAN-BARAT", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.6f, 0.50f, -5.2f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f },
                new PropSpec { name = "BEBAN-TIMUR", shape = PrimitiveType.Cube, posGrid = new Vector3(4.6f, 0.50f, -5.2f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-BARAT", posGrid = new Vector3(-4.6f, 0f, -1.6f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-TIMUR", posGrid = new Vector3(4.6f, 0f, -1.6f), requiredMass = 1.0f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = -0.2f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 0, 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(6.2f, 0.52f, 5.4f),
                title = "Surat Penutup Shift",
                speaker = "DANU", line = "Kalau jembatan arsip naik lagi suatu hari nanti, artinya ada yang menolak melupakan kota ini.",
                replySpeaker = "VELA", replyLine = "Jembatannya butuh dua pemberat sekaligus. Jangan tinggalkan salah satunya."
            },
            openingDialogue = new[]
            {
                Line("VELA", "Tangga menuju dek arsip terputus di tengah."),
                Line("MIRA", "Aktifkan kedua pelat lantai agar jembatan energinya tersambung penuh.")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Sektor Translasi selesai. Akses ke tingkat elevasi rusun dibuka.")
            },
            hints = new[]
            {
                "Letakkan BEBAN-BARAT di PAD-BARAT dan BEBAN-TIMUR di PAD-TIMUR.",
                "Jika kedua pelat sudah hijau, tangga jembatan di tengah akan memanjang hingga menyentuh dek atas.",
                "Bawa CORE-01 naik menyusuri tangga jembatan ke ANCHOR-01."
            }
        };

        // =====================================================================
        // CHAPTER 2 — ELEVASI (Rusun Matahari & Telekinesis)
        // =====================================================================

        private static LevelSpec Build2_1() => new LevelSpec
        {
            chapter = 2, level = 1,
            title = "Tangga Rusun Matahari",
            chapterSubtitle = "CHAPTER 2  •  ELEVASI",
            districtLabel = "RUSUN MATAHARI // L1", theme = ChapterTheme.Elevasi, parActions = 8,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(2.8f, 0.48f, -4.5f),
            anchorGrid = new Vector3(4.2f, 1.40f, 6.0f),
            platforms = new[]
            {
                new PlatformSpec { name = "Balkon Pemberat", centerGrid = new Vector3(-5.2f, 0.50f, 0.5f), sizeGrid = new Vector3(4.4f, 1.00f, 4.4f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Dek Anchor", centerGrid = new Vector3(4.2f, 0.70f, 5.8f), sizeGrid = new Vector3(6.2f, 1.40f, 4.6f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "BEBAN-UNGU", shape = PrimitiveType.Cube, posGrid = new Vector3(-2.0f, 0.50f, -3.5f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-BALKON", posGrid = new Vector3(-5.2f, 1.00f, 0.5f), requiredMass = 1.0f }
            },
            bridge = new BridgeSpec
            {
                centerX = 4.2f, stairStartZ = -0.3f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 0 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-5.4f, 0.52f, -4.5f),
                title = "Jemuran Lantai Dua",
                speaker = "NARA", line = "Waktu listrik naik turun, cuma anak tangga ini yang jadi jalan keluar warga.",
                replySpeaker = "VELA", replyLine = "Di mode Fleksibel kamu bisa melompat dan menarik beban dari jauh."
            },
            openingDialogue = new[]
            {
                Line("VELA", "Pelat pemicu jembatan berada di atas balkon kiri!"),
                Line("TEPI", "Tekan [TAB] untuk beralih ke Mode Fleksibel agar Rect bisa melompat dan memakai telekinesis [F].")
            },
            clearDialogue = new[]
            {
                Line("NARA", "Pelan-pelan, bangunan lama ini mulai merespons langkah kita lagi.")
            },
            hints = new[]
            {
                "Tekan [TAB] untuk mengganti mode gerak ke Mode Fleksibel (warna ungu).",
                "Angkat BEBAN-UNGU lewat tangga balkon kiri, atau arahkan ke BEBAN-UNGU dan tekan [F] untuk mengangkatnya dengan telekinesis ke PAD-BALKON.",
                "Setelah jembatan kanan aktif, bawa CORE-01 naik ke Dek Anchor."
            }
        };

        private static LevelSpec Build2_2() => new LevelSpec
        {
            chapter = 2, level = 2,
            title = "Teras Atas Rusun",
            chapterSubtitle = "CHAPTER 2  •  ELEVASI",
            districtLabel = "RUSUN MATAHARI // L2", theme = ChapterTheme.Elevasi, parActions = 10,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(-5.5f, 1.82f, 2.5f),
            anchorGrid = new Vector3(0f, 0f, 7.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Menara Core Barat", centerGrid = new Vector3(-5.5f, 0.65f, 2.5f), sizeGrid = new Vector3(4.2f, 1.30f, 4.2f), tier = 1 },
                new PlatformSpec { name = "Rak Pemberat Timur", centerGrid = new Vector3(5.6f, 0.55f, -3.8f), sizeGrid = new Vector3(3.8f, 1.10f, 3.8f), tier = 0 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Teras Wall L", centerGrid = new Vector3(-6.8f, 1.5f, 4.8f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) },
                new WallSpec { name = "Teras Wall R", centerGrid = new Vector3(6.8f, 1.5f, 4.8f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) }
            },
            props = new[]
            {
                new PropSpec { name = "BEBAN-RAK", shape = PrimitiveType.Cube, posGrid = new Vector3(5.6f, 1.62f, -3.8f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-UTARA", posGrid = new Vector3(4.2f, 0f, 1.2f), requiredMass = 1.0f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "PAGAR-TERAS", posGrid = new Vector3(0f, 1.35f, 4.8f), sizeGrid = new Vector3(6.2f, 2.7f, 0.35f), plateIndices = new[] { 0 } }
            },
            openingDialogue = new[]
            {
                Line("MIRA", "CORE-01 terjebak di puncak tiang barat tanpa tangga!"),
                Line("VELA", "Di Mode Fleksibel [TAB], gunakan sinar [F] Telekinesis untuk menurunkan benda dari tempat tinggi.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Sinar telekinesismu makin stabil. Puncak menara tinggal selangkah lagi.")
            },
            hints = new[]
            {
                "Tekan [TAB] ke Mode Fleksibel agar Telekinesis aktif.",
                "Arahkan ke BEBAN-RAK di platform kanan atas, tekan [F] untuk menariknya turun ke PAD-UTARA.",
                "Arahkan ke CORE-01 di menara kiri atas, tekan [F] untuk menurunkannya, lalu bawa masuk melewati pagar yang sudah terbuka."
            }
        };

        private static LevelSpec Build2_3() => new LevelSpec
        {
            chapter = 2, level = 3,
            title = "Puncak Menara Rusun",
            chapterSubtitle = "CHAPTER 2  •  ELEVASI",
            districtLabel = "MENARA RUSUN", theme = ChapterTheme.Elevasi, parActions = 11,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.6f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -5.0f),
            anchorGrid = new Vector3(0f, 1.50f, 7.0f),
            platforms = new[]
            {
                new PlatformSpec { name = "Balkon Menengah", centerGrid = new Vector3(5.4f, 0.50f, 1.2f), sizeGrid = new Vector3(4.0f, 1.00f, 4.0f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Puncak Menara", centerGrid = new Vector3(0f, 0.75f, 6.8f), sizeGrid = new Vector3(6.8f, 1.50f, 4.4f), tier = 2 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Menara Wall L", centerGrid = new Vector3(-6.8f, 1.5f, 0.8f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) },
                new WallSpec { name = "Menara Wall R", centerGrid = new Vector3(6.8f, 1.5f, 0.8f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) }
            },
            props = new[]
            {
                new PropSpec { name = "PEMBERAT-A", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.5f, 0.50f, -4.5f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f },
                new PropSpec { name = "PEMBERAT-B", shape = PrimitiveType.Cube, posGrid = new Vector3(4.5f, 0.50f, -4.5f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-PINTU", posGrid = new Vector3(-4.6f, 0f, -1.8f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-PUNCAK", posGrid = new Vector3(5.4f, 1.00f, 1.2f), requiredMass = 1.0f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "SEKAT-MENARA", posGrid = new Vector3(0f, 1.35f, 0.8f), sizeGrid = new Vector3(6.2f, 2.7f, 0.35f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 1.8f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.50f,
                plateIndices = new[] { 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(2.4f, 2.02f, 7.0f),
                title = "Antena Pemancar Nara",
                speaker = "NARA", line = "Dari atap ini kita bisa melihat seluruh tanggul utara sebelum air naik.",
                replySpeaker = "DANU", replyLine = "Jaga jalur elevasinya tetap utuh, Nara. Arsip berada di seberang."
            },
            openingDialogue = new[]
            {
                Line("NARA", "Puncak menara dijaga pagar tengah sekaligus tangga putus."),
                Line("VELA", "Buka pagar dengan pelat kiri dulu, lalu bawa pemberat kedua ke balkon kanan.")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Sektor Elevasi terhubung. Pintu Gedung Arsip mulai terbaca.")
            },
            hints = new[]
            {
                "Taruh PEMBERAT-A ke PAD-PINTU di kiri bawah agar pagar tengah terbuka.",
                "Gunakan Mode Fleksibel [TAB] dan angkat PEMBERAT-B (dengan tangga atau [F]) ke PAD-PUNCAK di balkon kanan.",
                "Setelah jembatan ke puncak aktif, bawa CORE-01 naik ke ANCHOR-01."
            }
        };

        // =====================================================================
        // CHAPTER 3 — ROTASI (Gedung Arsip & Apartemen Piano Danu)
        // =====================================================================

        private static LevelSpec Build3_1() => new LevelSpec
        {
            chapter = 3, level = 1,
            title = "Kunci Arah Arsip",
            chapterSubtitle = "CHAPTER 3  •  ROTASI",
            districtLabel = "LOBI ARSIP", theme = ChapterTheme.Rotasi, parActions = 7,
            requireFacingAngle = true, requiredFacingYaw = 0f, coreStartYaw = 90f,
            playerSpawnGrid = new Vector3(0f, 0.05f, -6.5f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -2.0f),
            anchorGrid = new Vector3(0f, 0f, 5.4f),
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(4.8f, 0.52f, 0.4f),
                title = "Kunci Putar Ruang Arsip",
                speaker = "MIRA", line = "Semua pintu arsip lama hanya mau terkunci kalau simbol sisinya menghadap ke arah yang benar.",
                replySpeaker = "DANU", replyLine = "Sudut yang keliru cuma akan memantulkan inti keluar."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Soket Anchor ini memiliki pengunci arah. Perhatikan panah pada dudukan Anchor."),
                Line("MIRA", "Tekan [R] saat memegang atau menarik kubus untuk memutarnya 90 derajat.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Pas sekali. Pintu menuju ruang dalam arsip langsung berbunyi klik.")
            },
            hints = new[]
            {
                "Soket Anchor meminta inti menghadap UTARA (0°), sesuai arah panah di dudukan.",
                "Saat memegang CORE-01 [E] atau mengarahkannya dengan [F], tekan [R] untuk memutarnya hingga panah kubus cocok dengan soket.",
                "Letakkan CORE-01 yang sudah sejajar tepat ke tengah ANCHOR-01."
            }
        };

        private static LevelSpec Build3_2() => new LevelSpec
        {
            chapter = 3, level = 2,
            title = "Ruang Cermin Arsip",
            chapterSubtitle = "CHAPTER 3  •  ROTASI",
            districtLabel = "RUANG CERMIN ARSIP", theme = ChapterTheme.Rotasi, parActions = 10,
            requireFacingAngle = true, requiredFacingYaw = 180f, coreStartYaw = 0f,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(4.4f, 0.48f, -3.8f),
            anchorGrid = new Vector3(0f, 1.30f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Balkon Cermin", centerGrid = new Vector3(0f, 0.65f, 5.8f), sizeGrid = new Vector3(6.6f, 1.30f, 4.4f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "PRISMA-BERAT", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.5f, 0.50f, -3.8f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-CERMIN", posGrid = new Vector3(-4.5f, 0f, 0.4f), requiredMass = 1.0f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = -0.2f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.30f,
                plateIndices = new[] { 0 }
            },
            openingDialogue = new[]
            {
                Line("VELA", "Soket kali ini membalik ke arah SELATAN — berlawanan dari arah kedatanganmu."),
                Line("TEPI", "Putar CORE-01 dua kali sebelum memasangnya.")
            },
            clearDialogue = new[]
            {
                Line("DANU", "Rekaman arsip kota tersembunyi di unit yang tidak pernah dicatat peta resmi...")
            },
            hints = new[]
            {
                "Bawa PRISMA-BERAT ke PAD-CERMIN di kiri untuk menyalakan tangga naik.",
                "Ambil CORE-01 dan tekan [R] DUA KALI sampai arah hadapnya menjadi 180° (Selatan).",
                "Bawa CORE-01 naik jembatan ke ANCHOR-01."
            }
        };

        private static LevelSpec Build3_3() => new LevelSpec
        {
            chapter = 3, level = 3,
            title = "Apartemen Tak Terindeks",
            chapterSubtitle = "CHAPTER 3  •  ROTASI",
            districtLabel = "UNIT PIANO DANU", theme = ChapterTheme.Rotasi, parActions = 11,
            requireFacingAngle = true, requiredFacingYaw = 270f, coreStartYaw = 90f,
            addPianoRoomProp = true, playPianoOnClear = true,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -4.4f),
            anchorGrid = new Vector3(0f, 1.30f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Dek Ruang Piano", centerGrid = new Vector3(0f, 0.65f, 5.8f), sizeGrid = new Vector3(6.2f, 1.30f, 4.4f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "PEMBERAT-KIRI", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.5f, 0.50f, -4.4f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f },
                new PropSpec { name = "PEMBERAT-KANAN", shape = PrimitiveType.Cube, posGrid = new Vector3(4.5f, 0.50f, -4.4f), sizeGrid = new Vector3(0.95f, 0.95f, 0.95f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-KUNCI", posGrid = new Vector3(-4.5f, 0f, -1.2f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-NADA", posGrid = new Vector3(4.5f, 0f, -1.2f), requiredMass = 1.0f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 0.0f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.30f,
                plateIndices = new[] { 0, 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-5.8f, 0.52f, 3.2f),
                title = "Partitur Tanpa Judul",
                speaker = "HALIM", line = "Danu selalu bilang kalau musik tidak bisa diukur grid. Ia menaruhnya di sudut ruang yang tak terindeks.",
                replySpeaker = "DANU", replyLine = "Kalau sistem mulai membekukan memori warga, nada ini akan membukanya lagi.",
                playPianoMotif = true
            },
            openingDialogue = new[]
            {
                Line("HALIM", "Ruangan di sudut kiri itu apartemen lama Danu... bahkan di dalam simulasi, pianonya masih ada."),
                Line("VELA", "Aktifkan kedua pelat agar jembatan naik, lalu kunci inti menghadap BARAT (270°).")
            },
            clearDialogue = new[]
            {
                Line("DANU", "Nada terakhir berbunyi. Kunci sistem Rekonstruksi — Fusion dan Partition — kini terbuka untukmu.")
            },
            hints = new[]
            {
                "Taruh PEMBERAT-KIRI di PAD-KUNCI dan PEMBERAT-KANAN di PAD-NADA.",
                "Ambil CORE-01, tekan [R] dua kali (dari 90° ke 270° / menghadap BARAT, ke arah piano).",
                "Naiki tangga jembatan di tengah dan pasang CORE-01 ke ANCHOR-01."
            }
        };

        // =====================================================================
        // CHAPTER 4 — FUSION & PARTITION (Mekanik Utama Rekonstruksi)
        // =====================================================================

        private static LevelSpec Build4_1() => new LevelSpec
        {
            chapter = 4, level = 1,
            title = "Dua Beban Jadi Satu",
            chapterSubtitle = "CHAPTER 4  •  FUSION & PARTITION",
            districtLabel = "BENGKEL FUSION // 01", theme = ChapterTheme.Rekonstruksi, parActions = 7,
            allowFusion = true, allowPartition = false,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(4.8f, 0.48f, -4.6f),
            anchorGrid = new Vector3(0f, 0f, 6.8f),
            extraWalls = new[]
            {
                new WallSpec { name = "Fuse Wall L", centerGrid = new Vector3(-6.8f, 1.5f, 3.6f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) },
                new WallSpec { name = "Fuse Wall R", centerGrid = new Vector3(6.8f, 1.5f, 3.6f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) }
            },
            props = new[]
            {
                new PropSpec { name = "RINGAN-A", shape = PrimitiveType.Cube, posGrid = new Vector3(-1.5f, 0.48f, -3.2f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f },
                new PropSpec { name = "RINGAN-B", shape = PrimitiveType.Cylinder, posGrid = new Vector3(1.5f, 0.44f, -3.2f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-BERAT", posGrid = new Vector3(0f, 0f, 1.0f), requiredMass = 2.2f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "GERBANG-BERAT", posGrid = new Vector3(0f, 1.35f, 3.6f), sizeGrid = new Vector3(6.2f, 2.7f, 0.35f), plateIndices = new[] { 0 } }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-5.5f, 0.52f, 1.4f),
                title = "Hukum Kekekalan Ruang",
                speaker = "VELA", line = "Dua benda yang disatukan dengan Fusion tidak harus sejenis — kubus dan prisma tetap menyatu membentuk komposit baru.",
                replySpeaker = "SIM", replyLine = "FUSION [2] aktif: gabungkan dua benda berbeda bentuk maupun massa menjadi satu inti komposit."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Pelat PAD-BERAT butuh minimal 2.2 ton. Di depanmu ada Kubus RINGAN-A (1.2 t) dan Prisma RINGAN-B (1.2 t)."),
                Line("VELA", "Bawa salah satu mendekati pasangannya hingga garis lengkung hijau menyala, lalu tekan [2] FUSION untuk menyatukan bentuk yang berbeda.")
            },
            clearDialogue = new[]
            {
                Line("VELA", "Berhasil! Sekarang kamu menguasai FUSION [2] untuk menggabungkan beban berat.")
            },
            hints = new[]
            {
                "Dekati RINGAN-A atau RINGAN-B (atau pegang satu dan dekatkan ke pasangannya hingga muncul garis tautan hijau).",
                "Tekan tombol [2] untuk menjalankan FUSION — keduanya akan menyatu menjadi satu blok 2.4 ton.",
                "Bawa blok gabungan 2.4 t ke PAD-BERAT agar pagar merah padam, lalu taruh CORE-01 ke ANCHOR-01."
            }
        };

        private static LevelSpec Build4_2() => new LevelSpec
        {
            chapter = 4, level = 2,
            title = "Satu Dibelah Dua",
            chapterSubtitle = "CHAPTER 4  •  FUSION & PARTITION",
            districtLabel = "RUANG PARTITION // 02", theme = ChapterTheme.Rekonstruksi, parActions = 8,
            allowFusion = false, allowPartition = true,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(5.2f, 0.48f, -4.8f),
            anchorGrid = new Vector3(0f, 1.40f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Dek Partition", centerGrid = new Vector3(0f, 0.70f, 5.8f), sizeGrid = new Vector3(6.6f, 1.40f, 4.4f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "MONOLIT", shape = PrimitiveType.Cube, posGrid = new Vector3(0f, 0.54f, -3.5f), sizeGrid = new Vector3(1.35f, 1.05f, 1.05f), mass = 2.4f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-KIRI", posGrid = new Vector3(-4.4f, 0f, 0.5f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-KANAN", posGrid = new Vector3(4.4f, 0f, 0.5f), requiredMass = 1.0f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 0.0f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 0, 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-6.0f, 0.52f, 5.0f),
                title = "Pecahan yang Terkendali",
                speaker = "DANU", line = "Menyimpan seluruh data dalam satu gumpalan besar justru berbahaya. Belah ia agar bisa mengisi dua penjaga sekaligus.",
                replySpeaker = "SIM", replyLine = "PARTITION [1] aktif: belah satu benda berat menjadi dua bagian seimbang."
            },
            openingDialogue = new[]
            {
                Line("MIRA", "Hanya ada satu blok MONOLIT seberat 2.4 ton. Bidang laser oranye menunjukkan ke mana belahan akan jatuh."),
                Line("VELA", "Secara otomatis arah potong mengikuti hadap Rect; tekan [3] untuk memutar bidang potong (Kiri|Kanan, Depan|Belakang, Atas|Bawah), lalu [1] PARTITION.")
            },
            clearDialogue = new[]
            {
                Line("MIRA", "Satu benda jadi dua kunci. Pembagian yang adil menyelamatkan jalur ini.")
            },
            hints = new[]
            {
                "Dekati atau pegang blok besar MONOLIT (2.4 t): garis bidang laser menunjukkan arah potongan secara real-time.",
                "Gunakan [3] bila ingin mengubah sumbu potong (Auto / X / Z / Y), lalu tekan [1] untuk membelah MONOLIT menjadi MONOLIT-A & -B (1.2 t x 2).",
                "Taruh masing-masing pecahan di PAD-KIRI dan PAD-KANAN, lalu bawa CORE-01 naik jembatan."
            }
        };

        private static LevelSpec Build4_3() => new LevelSpec
        {
            chapter = 4, level = 3,
            title = "Harmoni Dua Arah",
            chapterSubtitle = "CHAPTER 4  •  FUSION & PARTITION",
            districtLabel = "SIMPUL REKONSTRUKSI", theme = ChapterTheme.Rekonstruksi, parActions = 12,
            allowFusion = true, allowPartition = true,
            requireFacingAngle = true, requiredFacingYaw = 180f, coreStartYaw = 0f,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.6f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -5.6f),
            anchorGrid = new Vector3(0f, 1.40f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Dek Rekonstruksi", centerGrid = new Vector3(0f, 0.70f, 5.8f), sizeGrid = new Vector3(6.6f, 1.40f, 4.4f), tier = 1 }
            },
            props = new[]
            {
                new PropSpec { name = "BLOK-UTAMA", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.2f, 0.54f, -4.0f), sizeGrid = new Vector3(1.35f, 1.05f, 1.05f), mass = 2.4f },
                new PropSpec { name = "BLOK-TAMBAH", shape = PrimitiveType.Cube, posGrid = new Vector3(4.2f, 0.48f, -4.0f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-RINGAN", posGrid = new Vector3(-4.4f, 0f, 0.5f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-GABUNG", posGrid = new Vector3(4.4f, 0f, 0.5f), requiredMass = 2.2f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 0.0f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 0, 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(6.0f, 0.52f, 5.0f),
                title = "Catatan Terakhir Laboratorium",
                speaker = "HALIM", line = "Saat Partition dan Fusion dipakai bergantian, tidak ada massa yang terbuang percuma.",
                replySpeaker = "VELA", replyLine = "Badai di tanggul luar mulai terdengar. Bersiaplah menuju kota utama."
            },
            openingDialogue = new[]
            {
                Line("SIM", "Uji gabungan: pelat kiri butuh >= 1.0 t, pelat kanan butuh >= 2.2 t."),
                Line("TEPI", "Gunakan Partition [1] lalu Fusion [2] secara berurutan, lalu putar CORE-01 [R] ke arah 180°.")
            },
            clearDialogue = new[]
            {
                Line("VELA", "Semua mekanik dasar telah dikuasai. Badai utama di Kota Tepi sudah di depan mata.")
            },
            hints = new[]
            {
                "Langkah 1: Belah BLOK-UTAMA (2.4 t) dengan [1] Partition -> menjadi dua potongan 1.2 t.",
                "Langkah 2: Letakkan satu potongan 1.2 t di PAD-RINGAN (kiri). Bawa potongan kedua mendekati BLOK-TAMBAH (1.2 t), tekan [2] Fusion menjadi 2.4 t, lalu taruh di PAD-GABUNG (kanan).",
                "Langkah 3: Putar CORE-01 dengan [R] dua kali hingga menghadap 180° (Selatan) dan pasang di ANCHOR-01."
            }
        };

        // =====================================================================
        // CHAPTER 5 — BADAI & PILIHAN AKHIR
        // =====================================================================

        private static LevelSpec Build5_1() => new LevelSpec
        {
            chapter = 5, level = 1,
            title = "Ambang Badai",
            chapterSubtitle = "CHAPTER 5  •  BADAI & PILIHAN AKHIR",
            districtLabel = "AMBANG TANGGUL UTARA", theme = ChapterTheme.Badai, parActions = 13,
            allowFusion = true, allowPartition = true,
            requireFacingAngle = true, requiredFacingYaw = 90f, coreStartYaw = 270f,
            triggerGlitchOnClear = true,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.5f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -5.0f),
            anchorGrid = new Vector3(0f, 1.40f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Gardu Elevasi", centerGrid = new Vector3(-5.2f, 0.50f, -0.4f), sizeGrid = new Vector3(3.8f, 1.00f, 3.8f), tier = 0, addSouthSteps = true },
                new PlatformSpec { name = "Menara Kendali", centerGrid = new Vector3(0f, 0.70f, 5.8f), sizeGrid = new Vector3(6.6f, 1.40f, 4.4f), tier = 1 }
            },
            extraWalls = new[]
            {
                new WallSpec { name = "Badai Wall L", centerGrid = new Vector3(-6.8f, 1.5f, 2.2f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) },
                new WallSpec { name = "Badai Wall R", centerGrid = new Vector3(6.8f, 1.5f, 2.2f), sizeGrid = new Vector3(7.4f, 3.0f, 0.5f) }
            },
            props = new[]
            {
                new PropSpec { name = "PENAHAN-BESAR", shape = PrimitiveType.Cube, posGrid = new Vector3(-3.0f, 0.54f, -4.5f), sizeGrid = new Vector3(1.35f, 1.05f, 1.05f), mass = 2.4f },
                new PropSpec { name = "PENAHAN-TAMBAH", shape = PrimitiveType.Cube, posGrid = new Vector3(3.0f, 0.48f, -4.5f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-GARDU", posGrid = new Vector3(-5.2f, 1.00f, -0.4f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-TANGGUL", posGrid = new Vector3(4.6f, 0f, 0.2f), requiredMass = 2.2f }
            },
            barriers = new[]
            {
                new BarrierSpec { name = "SEKAM-BADAI", posGrid = new Vector3(0f, 1.35f, 2.2f), sizeGrid = new Vector3(6.2f, 2.7f, 0.35f), plateIndices = new[] { 0 } }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 0.0f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 1 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(2.4f, 1.92f, 6.2f),
                title = "Gelombang Tanggul Utara",
                speaker = "VELA", line = "Air badai sudah menyentuh sisi luar ruang kendali. Sebentar lagi semua jalur mengarah ke ruang keputusan akhir.",
                replySpeaker = "HALIM", replyLine = "Ingat, kamu masih punya kendali penuh untuk membelah atau menyatukan apa yang tersisa."
            },
            openingDialogue = new[]
            {
                Line("HALIM", "Satu pagar energi dan jembatan rusak menghalangi pintu menuju inti pengendali tanggul."),
                Line("VELA", "Gunakan seluruh kemampuanmu: Partition [1], Fusion [2], Telekinesis [F], dan Rotasi [R].")
            },
            clearDialogue = new[]
            {
                Line("SIM", "Peringatan: Ruang Pilihan Akhir telah dicapai. Bersiaplah mengambil keputusan untuk Kota Tepi.")
            },
            hints = new[]
            {
                "Belah PENAHAN-BESAR (2.4 t) dengan [1] Partition (putar bidang potong dengan [3] jika perlu) -> naikkan satu bagian 1.2 t ke PAD-GARDU di balkon kiri ([TAB] + [F] atau tangga).",
                "Gabungkan bagian 1.2 t kedua dengan PENAHAN-TAMBAH (1.2 t) memakai [2] Fusion -> letakkan blok 2.4 t di PAD-TANGGUL (kanan).",
                "Putar CORE-01 dengan [R] ke 90° (Timur), lalu bawa melewati pagar dan jembatan ke ANCHOR-01."
            }
        };

        private static LevelSpec Build5_2() => new LevelSpec
        {
            chapter = 5, level = 2,
            title = "Pilihan Akhir Kota Tepi",
            chapterSubtitle = "CHAPTER 5  •  PILIHAN AKHIR",
            districtLabel = "SIMPUL PUSAT KOTA TEPI", theme = ChapterTheme.Badai, parActions = 6,
            allowFusion = true, allowPartition = true,
            isEndingChamber = true,
            playerSpawnGrid = new Vector3(0f, 0.05f, -6.8f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -1.8f),
            coreMass = 1.4f,
            anchorGrid = new Vector3(0f, 0f, 7.2f),
            props = new[]
            {
                new PropSpec { name = "BALOK-ARSIP", shape = PrimitiveType.Cube, posGrid = new Vector3(0f, 0.54f, -4.4f), sizeGrid = new Vector3(1.30f, 1.00f, 1.00f), mass = 2.0f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-PEMANCAR", posGrid = new Vector3(0f, 0f, 2.2f), requiredMass = 0.8f }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(-4.2f, 0.52f, -3.5f),
                title = "Suara Warga Kota Tepi",
                speaker = "VELA", line = "Kami tidak akan memaksamu memilih, Rect.",
                replySpeaker = "DANU", replyLine = "Selamatkan tanggulnya, kunci arsipnya, atau pecah energinya agar suara kami ikut bertahan.",
                playPianoMotif = true
            },
            openingDialogue = new[]
            {
                Line("VELA", "Tiga jalur terakhir menyala di hadapanmu. Tentukan ke mana CORE-01 akan disalurkan:"),
                Line("MIRA", "Timur: TANGGUL UTARA (selamatkan kota fisik)  •  Utara: PEMANCAR (pecah dengan Partition [1])  •  Barat: BRANKAS ARSIP.")
            },
            clearDialogue = new[]
            {
                Line("TEPI", "Keputusan telah tercatat di sejarah Kota Tepi.")
            },
            hints = new[]
            {
                "Pilihan A (Restoration — Timur): Bawa CORE-01 langsung ke Soket Timur untuk menutup penuh tanggul kota.",
                "Pilihan B (Preservation — Barat): Bawa CORE-01 ke Soket Barat untuk mengabadikan seluruh rekaman arsip.",
                "Pilihan C (Archivist — Utara): Tekan [1] Partition pada CORE-01 (atau belah BALOK-ARSIP di PAD-PEMANCAR) lalu pasang ke Soket Utara!"
            }
        };

        // =====================================================================
        // SPECIAL SHOWCASE — LEVEL SHOWCASE (Semua Mekanik Terbuka Untuk Demo)
        // =====================================================================

        private static LevelSpec BuildShowcase() => new LevelSpec
        {
            chapter = 0, level = 0,
            customSceneName = SimulationProgress.ShowcaseSceneName,
            title = "Ruang Showcase: Semua Mekanik",
            chapterSubtitle = "SPECIAL SHOWCASE  •  SEMUA FITUR",
            districtLabel = "SANDBOX & DEMO", theme = ChapterTheme.Showcase, parActions = 8,
            allowFusion = true, allowPartition = true,
            isShowcaseChamber = true,
            addPianoRoomProp = true,
            requireFacingAngle = true, requiredFacingYaw = 0f, coreStartYaw = 90f,
            playerSpawnGrid = new Vector3(0f, 0.05f, -7.2f),
            coreSpawnGrid = new Vector3(0f, 0.48f, -4.4f),
            coreMass = 1.4f,
            anchorGrid = new Vector3(0f, 1.40f, 6.2f),
            platforms = new[]
            {
                new PlatformSpec { name = "Dek Anchor Demo", centerGrid = new Vector3(0f, 0.70f, 5.8f), sizeGrid = new Vector3(6.6f, 1.40f, 4.4f), tier = 1 },
                new PlatformSpec { name = "Rak Elevasi Demo", centerGrid = new Vector3(5.6f, 0.45f, -1.0f), sizeGrid = new Vector3(3.6f, 0.90f, 3.6f), tier = 0, addSouthSteps = true }
            },
            props = new[]
            {
                // Partition [1] Demo — Left Side
                new PropSpec { name = "DEMO-PARTITION", shape = PrimitiveType.Cube, posGrid = new Vector3(-4.8f, 0.54f, -4.2f), sizeGrid = new Vector3(1.35f, 1.05f, 1.05f), mass = 2.4f },
                // Fusion [2] Demo — Right Side (Heterogeneous Cube + Octagon Prism)
                new PropSpec { name = "DEMO-FUSE-A", shape = PrimitiveType.Cube, posGrid = new Vector3(3.8f, 0.48f, -4.6f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f },
                new PropSpec { name = "DEMO-FUSE-B", shape = PrimitiveType.Cylinder, posGrid = new Vector3(6.0f, 0.44f, -4.6f), sizeGrid = new Vector3(0.88f, 0.88f, 0.88f), mass = 1.2f }
            },
            plates = new[]
            {
                new PlateSpec { name = "PAD-SPLIT-1", posGrid = new Vector3(-6.0f, 0f, 0.4f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-SPLIT-2", posGrid = new Vector3(-3.2f, 0f, 0.4f), requiredMass = 1.0f },
                new PlateSpec { name = "PAD-FUSE-ATAS", posGrid = new Vector3(5.6f, 0.90f, -1.0f), requiredMass = 2.2f }
            },
            bridge = new BridgeSpec
            {
                centerX = 0f, stairStartZ = 0.0f, baseHeightGrid = 0f,
                staticStepCount = 2, dynamicStepCount = 3, targetHeightGrid = 1.40f,
                plateIndices = new[] { 2 }
            },
            fragment = new FragmentSpec
            {
                enabled = true, posGrid = new Vector3(5.8f, 0.52f, 5.2f),
                title = "Arsip Demo POLYMULATOR",
                speaker = "TIM 8", line = "Seluruh mekanik utama aktif di ruang ini: Translasi, Elevasi, Rotasi, Partition [1]/[3], dan Fusion Hibrid [2].",
                replySpeaker = "RECT", replyLine = "Gunakan [TAB] untuk Mode Fleksibel, [F] Telekinesis, [R] Rotasi, [1] Partition, [3] Arah Potong, dan [2] Fusion.",
                playPianoMotif = true
            },
            openingDialogue = new[]
            {
                Line("TIM 8", "Selamat datang di Ruang Showcase! Semua mekanik siap diuji dengan pratinjau laser Partition & tautan Fusion."),
                Line("RECT", "Kiri: uji Partition [1] & sumbu potong [3]  •  Kanan: uji Fusion [2] objek Kubus+Prisma  •  Tengah: Rotasi [R] CORE-01.")
            },
            clearDialogue = new[]
            {
                Line("TIM 8", "Demo Showcase selesai! Semua sistem mekanik dan audio-visual terverifikasi berfungsi.")
            },
            hints = new[]
            {
                "KIRI (Partition [1] + [3]): Dekati DEMO-PARTITION (2.4 t) untuk melihat bidang potong laser oranye. Tekan [3] untuk mengganti sumbu potong, lalu [1] untuk membelah jadi 2 x 1.2 t.",
                "KANAN (Fusion Hibrid [2] + [TAB]/[F]): Dekatkan Kubus DEMO-FUSE-A & Prisma DEMO-FUSE-B, tekan [2] untuk menggabung dua bentuk berbeda jadi 2.4 t, lalu naikkan ke PAD-FUSE-ATAS.",
                "TENGAH (Rotasi [R]): Ambil CORE-01, tekan [R] tiga kali sampai 0° (Utara), lalu pasang ke ANCHOR-01 di puncak."
            }
        };
    }
}
