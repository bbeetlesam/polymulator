using System;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Persistent campaign progression backed by PlayerPrefs tracking cleared levels,
    /// best star ratings, collected memory fragments, showcase access, and chosen ending.
    /// </summary>
    public static class SimulationProgress
    {
        public struct CampaignEntry
        {
            public int chapter;
            public int level;
            public string sceneName;
            public string fallbackTitle;

            public CampaignEntry(int c, int l, string fallback)
            {
                chapter = c;
                level = l;
                sceneName = "Level" + c + "." + l;
                fallbackTitle = fallback;
            }
        }

        public const string ShowcaseSceneName = "LevelShowcase";
        public const string EndingSceneName = "Level5.2";

        public static readonly string[] ChapterNames =
        {
            "",
            "TRANSLASI",
            "ELEVASI",
            "ROTASI",
            "FUSION & PARTITION",
            "BADAI & PILIHAN AKHIR"
        };

        public static string ChapterName(int chapter)
        {
            if (chapter <= 0 || chapter >= ChapterNames.Length) return string.Empty;
            return L.TOr("chapter." + chapter, ChapterNames[chapter]);
        }

        public static string CampaignTitle(CampaignEntry entry)
        {
            return L.TOr(LocalizationKeys.Level(entry.sceneName, "title"), entry.fallbackTitle);
        }

        public static readonly CampaignEntry[] Catalog =
        {
            new CampaignEntry(1, 1, "Bangun di Ruangan Kosong"),
            new CampaignEntry(1, 2, "Jalan Pasar"),
            new CampaignEntry(1, 3, "Gerbang Arsip Timur"),
            new CampaignEntry(2, 1, "Tangga Rusun Matahari"),
            new CampaignEntry(2, 2, "Teras Atas Rusun"),
            new CampaignEntry(2, 3, "Puncak Menara Rusun"),
            new CampaignEntry(3, 1, "Kunci Arah Arsip"),
            new CampaignEntry(3, 2, "Ruang Cermin Arsip"),
            new CampaignEntry(3, 3, "Apartemen Tak Terindeks"),
            new CampaignEntry(4, 1, "Dua Beban Jadi Satu"),
            new CampaignEntry(4, 2, "Satu Dibelah Dua"),
            new CampaignEntry(4, 3, "Harmoni Dua Arah"),
            new CampaignEntry(5, 1, "Ambang Badai"),
            new CampaignEntry(5, 2, "Pilihan Akhir Kota Tepi")
        };

        private const string KeyClearedPrefix = "SIM_CLEARED_";
        private const string KeyStarsPrefix = "SIM_STARS_";
        private const string KeyFragPrefix = "SIM_FRAG_";
        private const string KeyDemoUnlock = "SIM_DEMO_UNLOCKED";
        private const string KeyIteration = "SIM_ITERATION";
        private const string KeyEnding = "SIM_ENDING_CHOICE";

        public static bool IsDemoUnlocked
        {
            get => PlayerPrefs.GetInt(KeyDemoUnlock, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyDemoUnlock, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static int IterationNumber => PlayerPrefs.GetInt(KeyIteration, 127);

        public static void IncrementIterationOnRetry()
        {
            int next = IterationNumber + 1;
            PlayerPrefs.SetInt(KeyIteration, next);
            PlayerPrefs.Save();
        }

        public static bool IsSpecialShowcase(string sceneName)
        {
            return string.Equals(sceneName, ShowcaseSceneName, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsEndingScene(string sceneName)
        {
            return string.Equals(sceneName, EndingSceneName, StringComparison.OrdinalIgnoreCase);
        }

        public static int GetLevelsInChapter(int chapter)
        {
            int count = 0;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].chapter == chapter) count++;
            }
            return count;
        }

        public static bool IsCleared(string sceneName) => PlayerPrefs.GetInt(KeyClearedPrefix + sceneName, 0) == 1;

        public static int GetStars(string sceneName) => Mathf.Clamp(PlayerPrefs.GetInt(KeyStarsPrefix + sceneName, 0), 0, 3);

        public static bool HasFragment(string sceneName) => PlayerPrefs.GetInt(KeyFragPrefix + sceneName, 0) == 1;

        public static int GetCollectedFragmentCount()
        {
            int total = 0;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (HasFragment(Catalog[i].sceneName)) total++;
            }
            if (HasFragment(ShowcaseSceneName)) total++;
            return total;
        }

        public static bool IsUnlocked(string sceneName)
        {
            if (IsSpecialShowcase(sceneName)) return true;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].sceneName == sceneName) return IsUnlocked(Catalog[i].chapter, Catalog[i].level);
            }
            return IsDemoUnlocked;
        }

        public static bool IsUnlocked(int chapter, int level)
        {
            if (IsDemoUnlocked) return true;
            if (chapter == 1 && level == 1) return true;
            for (int i = 1; i < Catalog.Length; i++)
            {
                if (Catalog[i].chapter == chapter && Catalog[i].level == level)
                    return IsCleared(Catalog[i - 1].sceneName);
            }
            return false;
        }

        public static CampaignEntry GetHighestUnlocked()
        {
            for (int i = Catalog.Length - 1; i >= 0; i--)
            {
                if (IsUnlocked(Catalog[i].chapter, Catalog[i].level)) return Catalog[i];
            }
            return Catalog[0];
        }

        public static string GetNextSceneName(string currentScene)
        {
            if (IsSpecialShowcase(currentScene)) return string.Empty;
            for (int i = 0; i < Catalog.Length - 1; i++)
            {
                if (Catalog[i].sceneName == currentScene) return Catalog[i + 1].sceneName;
            }
            return string.Empty;
        }

        public static int ComputeStars(int totalActions, int parActions)
        {
            if (parActions <= 0) return 3;
            if (totalActions <= parActions) return 3;
            if (totalActions <= parActions + 3) return 2;
            return 1;
        }

        public static int RecordClear(string sceneName, int totalActions, int parActions)
        {
            int stars = ComputeStars(totalActions, parActions);
            PlayerPrefs.SetInt(KeyClearedPrefix + sceneName, 1);
            int prev = GetStars(sceneName);
            if (stars > prev)
                PlayerPrefs.SetInt(KeyStarsPrefix + sceneName, stars);
            PlayerPrefs.Save();
            return stars;
        }

        public static void RecordFragment(string sceneName)
        {
            PlayerPrefs.SetInt(KeyFragPrefix + sceneName, 1);
            PlayerPrefs.Save();
        }

        public static void RecordEnding(string endingId)
        {
            if (!string.IsNullOrEmpty(endingId))
                PlayerPrefs.SetString(KeyEnding, endingId);
            PlayerPrefs.SetInt(KeyClearedPrefix + EndingSceneName, 1);
            if (GetStars(EndingSceneName) < 3)
                PlayerPrefs.SetInt(KeyStarsPrefix + EndingSceneName, 3);
            PlayerPrefs.Save();
        }

        public static string GetLastEnding()
        {
            return PlayerPrefs.GetString(KeyEnding, string.Empty);
        }

        public static void ResetAll()
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                string id = Catalog[i].sceneName;
                PlayerPrefs.DeleteKey(KeyClearedPrefix + id);
                PlayerPrefs.DeleteKey(KeyStarsPrefix + id);
                PlayerPrefs.DeleteKey(KeyFragPrefix + id);
            }
            PlayerPrefs.DeleteKey(KeyClearedPrefix + ShowcaseSceneName);
            PlayerPrefs.DeleteKey(KeyStarsPrefix + ShowcaseSceneName);
            PlayerPrefs.DeleteKey(KeyFragPrefix + ShowcaseSceneName);
            PlayerPrefs.DeleteKey(KeyDemoUnlock);
            PlayerPrefs.DeleteKey(KeyEnding);
            PlayerPrefs.SetInt(KeyIteration, 127);
            PlayerPrefs.Save();
        }
    }
}
