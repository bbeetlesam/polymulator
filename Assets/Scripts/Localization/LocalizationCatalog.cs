using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    [CreateAssetMenu(fileName = "LocalizationCatalog", menuName = "POLYMULATOR/Localization Catalog")]
    public sealed class LocalizationCatalog : ScriptableObject
    {
        [SerializeField] private string[] keys = System.Array.Empty<string>();
        [SerializeField] private string[] texts = System.Array.Empty<string>();

        private readonly Dictionary<string, string> lookup = new Dictionary<string, string>();

        public int Count => keys != null ? keys.Length : 0;

        private void OnEnable()
        {
            RebuildLookup();
        }

        public bool TryGet(string key, out string text)
        {
            text = null;
            return !string.IsNullOrEmpty(key) && lookup.TryGetValue(key, out text);
        }

        public void SetEntries(string[] nextKeys, string[] nextTexts)
        {
            keys = nextKeys ?? System.Array.Empty<string>();
            texts = nextTexts ?? System.Array.Empty<string>();
            RebuildLookup();
        }

        private void RebuildLookup()
        {
            lookup.Clear();
            if (keys == null || texts == null) return;

            int count = Mathf.Min(keys.Length, texts.Length);
            for (int i = 0; i < count; i++)
            {
                if (string.IsNullOrEmpty(keys[i]) || lookup.ContainsKey(keys[i])) continue;
                lookup.Add(keys[i], texts[i] ?? string.Empty);
            }
        }
    }
}
