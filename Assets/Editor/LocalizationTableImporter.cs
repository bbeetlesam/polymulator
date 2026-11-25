using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Builds runtime locale assets from Assets/Localization/strings.csv and checks that
    /// every key has both English and Indonesian text with matching format arguments.
    /// </summary>
    public sealed class LocalizationTableImporter : AssetPostprocessor
    {
        private const string CsvPath = "Assets/Localization/strings.csv";
        private const string ResourceFolder = "Assets/Resources/Localization";
        private const string EnglishAssetPath = ResourceFolder + "/English.asset";
        private const string IndonesianAssetPath = ResourceFolder + "/Indonesian.asset";

        private static readonly Regex PlaceholderPattern = new Regex(@"\{(\d+)", RegexOptions.Compiled);

        [MenuItem("Tools/POLYMULATOR/Rebuild Localization Tables", priority = 60)]
        public static void Rebuild()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError("[POLYMULATOR] Missing localization source: " + CsvPath);
                return;
            }

            List<string[]> rows = ParseCsv(CsvPath);
            if (rows.Count < 2)
            {
                Debug.LogError("[POLYMULATOR] Localization CSV has no entries.");
                return;
            }

            string[] header = rows[0];
            if (header.Length < 3 || header[0].Trim() != "key" || header[1].Trim() != "en" || header[2].Trim() != "id")
            {
                Debug.LogError("[POLYMULATOR] Localization CSV header must be: key,en,id");
                return;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            var keys = new List<string>();
            var english = new List<string>();
            var indonesian = new List<string>();
            bool valid = true;

            for (int i = 1; i < rows.Count; i++)
            {
                string[] row = rows[i];
                string line = (i + 1).ToString();
                if (row.Length < 3 || string.IsNullOrWhiteSpace(row[0]))
                {
                    Debug.LogError("[POLYMULATOR] Localization CSV line " + line + " is missing key, en, or id.");
                    valid = false;
                    continue;
                }

                string key = row[0].Trim();
                if (!seen.Add(key))
                {
                    Debug.LogError("[POLYMULATOR] Duplicate localization key on line " + line + ": " + key);
                    valid = false;
                    continue;
                }

                string en = row[1];
                string id = row[2];
                if (string.IsNullOrEmpty(en) || string.IsNullOrEmpty(id))
                {
                    Debug.LogError("[POLYMULATOR] Localization key " + key + " has an empty translation.");
                    valid = false;
                }

                if (!PlaceholdersMatch(en, id))
                {
                    Debug.LogError("[POLYMULATOR] Localization key " + key + " has mismatched format arguments.");
                    valid = false;
                }

                keys.Add(key);
                english.Add(en);
                indonesian.Add(id);
            }

            if (!valid)
            {
                Debug.LogError("[POLYMULATOR] Localization tables were not rebuilt. Fix the CSV errors above.");
                return;
            }

            EnsureResourceFolder();
            WriteCatalog(EnglishAssetPath, keys.ToArray(), english.ToArray());
            WriteCatalog(IndonesianAssetPath, keys.ToArray(), indonesian.ToArray());
            AssetDatabase.SaveAssets();
            Debug.Log("[POLYMULATOR] Rebuilt localization tables with " + keys.Count + " keys.");
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!WasTouched(importedAssets) && !WasTouched(deletedAssets) &&
                !WasTouched(movedAssets) && !WasTouched(movedFromAssetPaths))
                return;

            EditorApplication.delayCall += Rebuild;
        }

        private static bool WasTouched(string[] paths)
        {
            if (paths == null) return false;
            for (int i = 0; i < paths.Length; i++)
            {
                if (paths[i] == CsvPath) return true;
            }
            return false;
        }

        private static void EnsureResourceFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            if (!AssetDatabase.IsValidFolder(ResourceFolder)) AssetDatabase.CreateFolder("Assets/Resources", "Localization");
        }

        private static void WriteCatalog(string path, string[] keys, string[] texts)
        {
            LocalizationCatalog catalog = AssetDatabase.LoadAssetAtPath<LocalizationCatalog>(path);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<LocalizationCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            catalog.SetEntries(keys, texts);
            EditorUtility.SetDirty(catalog);
        }

        private static bool PlaceholdersMatch(string english, string indonesian)
        {
            string a = NormalizePlaceholders(english);
            string b = NormalizePlaceholders(indonesian);
            return a == b;
        }

        private static string NormalizePlaceholders(string text)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(text)) return string.Empty;

            MatchCollection matches = PlaceholderPattern.Matches(text);
            for (int i = 0; i < matches.Count; i++) tokens.Add(matches[i].Groups[1].Value);
            tokens.Sort(StringComparer.Ordinal);
            return string.Join(",", tokens.ToArray());
        }

        private static List<string[]> ParseCsv(string path)
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            if (content.Length > 0 && content[0] == '\uFEFF') content = content.Substring(1);

            var rows = new List<string[]>();
            var row = new List<string>();
            var field = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < content.Length && content[i + 1] == '"')
                        {
                            field.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        field.Append(c);
                    }
                    continue;
                }

                if (c == '"')
                {
                    inQuotes = true;
                }
                else if (c == ',')
                {
                    row.Add(field.ToString());
                    field.Length = 0;
                }
                else if (c == '\n' || c == '\r')
                {
                    row.Add(field.ToString());
                    rows.Add(row.ToArray());
                    row.Clear();
                    field.Length = 0;
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n') i++;
                }
                else
                {
                    field.Append(c);
                }
            }

            if (field.Length > 0 || row.Count > 0)
            {
                row.Add(field.ToString());
                rows.Add(row.ToArray());
            }

            return rows;
        }
    }
}
