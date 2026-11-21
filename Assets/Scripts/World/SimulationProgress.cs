using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Persistent campaign progression backed by PlayerPrefs tracking cleared levels,
    /// efficiency star ratings (1-3), collected Memory Fragments, Iteration #127 counter,
    /// and a presenter Demo-Unlock toggle.
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

        public static readonly string[] ChapterNames =
        {
            "",
            "TRANSLASI",
            "ELEVASI",
            "ROTASI",
            "REKONSTRUKSI"
        };

        public static readonly CampaignEntry[] Catalog =
        {
            new CampaignEntry(1, 1, "Warming Up"),
            new CampaignEntry(1, 2, "Pasar"),
            new CampaignEntry(1, 3, "Tag AD-I"),
            new CampaignEntry(1, 4, "Ujian Pasar"),
            new CampaignEntry(2, 1, "Angkat"),
            new CampaignEntry(2, 2, "Susun"),
            new CampaignEntry(2, 3, "Kotak Musik"),
            new CampaignEntry(3, 1, "Kunci Orientasi"),
            new CampaignEntry(3, 2, "Bias"),
            new CampaignEntry(3, 3, "Ruang Piano"),
            new CampaignEntry(4, 1, "Dua Sisi"),
            new CampaignEntry(4, 2, "Ambang Badai")
        };

        private const string KeyClearedPrefix = "POLY_CLR_";
        private const string KeyStarsPrefix = "POLY_STAR_";
        private const string KeyFragPrefix = "POLY_FRAG_";
        private const string KeyDemoUnlock = "POLY_DEMO_ALL";
        private const string KeyIteration = "POLY_ITERATION";

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
            int count = 0;
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (HasFragment(Catalog[i].sceneName)) count++;
            }
            return count;
        }

        public static bool IsUnlocked(int chapter, int level)
        {
            if (IsDemoUnlocked) return true;
            if (chapter == 1 && level == 1) return true;

            int idx = IndexOf(chapter, level);
            if (idx <= 0) return idx == 0;
            return IsCleared(Catalog[idx - 1].sceneName);
        }

        public static CampaignEntry GetHighestUnlocked()
        {
            CampaignEntry highest = Catalog[0];
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (IsUnlocked(Catalog[i].chapter, Catalog[i].level))
                    highest = Catalog[i];
            }
            return highest;
        }

        public static string GetNextSceneName(string currentScene)
        {
            int idx = IndexOf(currentScene);
            if (idx >= 0 && idx + 1 < Catalog.Length)
                return Catalog[idx + 1].sceneName;
            return string.Empty;
        }

        public static int ComputeStars(int totalActions, int parActions)
        {
            int par = Mathf.Max(1, parActions);
            if (totalActions <= par) return 3;
            if (totalActions <= par + 4) return 2;
            return 1;
        }

        public static int RecordClear(string sceneName, int totalActions, int parActions)
        {
            int stars = ComputeStars(totalActions, parActions);
            PlayerPrefs.SetInt(KeyClearedPrefix + sceneName, 1);
            int prevStars = GetStars(sceneName);
            if (stars > prevStars)
                PlayerPrefs.SetInt(KeyStarsPrefix + sceneName, stars);
            PlayerPrefs.Save();
            return stars;
        }

        public static void RecordFragment(string sceneName)
        {
            PlayerPrefs.SetInt(KeyFragPrefix + sceneName, 1);
            PlayerPrefs.Save();
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
            PlayerPrefs.DeleteKey(KeyDemoUnlock);
            PlayerPrefs.SetInt(KeyIteration, 127);
            PlayerPrefs.Save();
        }

        private static int IndexOf(int chapter, int level)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].chapter == chapter && Catalog[i].level == level) return i;
            }
            return -1;
        }

        private static int IndexOf(string sceneName)
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                if (Catalog[i].sceneName == sceneName) return i;
            }
            return -1;
        }
    }
}
