using System;
using System.Globalization;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Runtime localization service. English is the canonical source language and the default.
    /// </summary>
    public static class L
    {
        public static event Action OnLanguageChanged;

        private const string EnglishCatalogPath = "Localization/English";
        private const string IndonesianCatalogPath = "Localization/Indonesian";

        private static LocalizationCatalog englishCatalog;
        private static LocalizationCatalog indonesianCatalog;
        private static GameLanguage currentLanguage = GameLanguage.English;

        public static GameLanguage Language => currentLanguage;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            englishCatalog = Resources.Load<LocalizationCatalog>(EnglishCatalogPath);
            indonesianCatalog = Resources.Load<LocalizationCatalog>(IndonesianCatalogPath);
            currentLanguage = SimulationSettings.Language;
        }

        public static string T(string key, params object[] args)
        {
            string template = Resolve(key);
            if (string.IsNullOrEmpty(template)) template = key;
            return Format(template, args);
        }

        public static string TOr(string key, string fallback, params object[] args)
        {
            string template = Resolve(key);
            if (string.IsNullOrEmpty(template)) template = fallback ?? key;
            return Format(template, args);
        }

        public static void SetLanguage(GameLanguage language)
        {
            if (language == currentLanguage) return;

            currentLanguage = language;
            SimulationSettings.Language = language;
            OnLanguageChanged?.Invoke();
        }

        public static void Refresh()
        {
            OnLanguageChanged?.Invoke();
        }

        private static string Resolve(string key)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            LocalizationCatalog active = currentLanguage == GameLanguage.Indonesian
                ? indonesianCatalog
                : englishCatalog;
            if (active != null && active.TryGet(key, out string text) && !string.IsNullOrEmpty(text))
                return text;

            if (currentLanguage != GameLanguage.English && englishCatalog != null &&
                englishCatalog.TryGet(key, out string englishText))
                return englishText;

            return string.Empty;
        }

        private static string Format(string template, object[] args)
        {
            if (args == null || args.Length == 0) return template;
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
    }
}
