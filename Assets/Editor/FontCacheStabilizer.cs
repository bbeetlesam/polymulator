using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.Text;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Keeps dynamic font assets stable by disabling clear-on-build and warming their
    /// atlases with every character used by the localization tables.
    /// </summary>
    public sealed class FontCacheStabilizer
    {
        private const string CsvPath = "Assets/Localization/strings.csv";
        private const string FontFolder = "Assets/UI/Fonts";

        [MenuItem("Tools/POLYMULATOR/Stabilize Dynamic Font Caches", priority = 61)]
        public static void Rebuild()
        {
            if (!File.Exists(CsvPath))
            {
                Debug.LogError("[POLYMULATOR] Missing localization source: " + CsvPath);
                return;
            }

            string characterSet = BuildCharacterSet();
            string[] guids = AssetDatabase.FindAssets("t:FontAsset", new[] { FontFolder });
            if (guids.Length == 0)
            {
                Debug.LogError("[POLYMULATOR] No font assets found under " + FontFolder);
                return;
            }

            int stabilized = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<FontAsset>(path);
                if (fontAsset == null)
                    continue;

                SerializedObject serializedFont = new SerializedObject(fontAsset);
                SerializedProperty clearOnBuild = serializedFont.FindProperty("m_ClearDynamicDataOnBuild");
                if (clearOnBuild != null && clearOnBuild.boolValue)
                {
                    clearOnBuild.boolValue = false;
                    serializedFont.ApplyModifiedPropertiesWithoutUndo();
                }

                if (fontAsset.sourceFontFile == null)
                {
                    Debug.LogWarning("[POLYMULATOR] Skipped atlas warm-up for " + path + ": source font is missing.");
                }
                else
                {
                    string missingCharacters;
                    if (!fontAsset.TryAddCharacters(characterSet, out missingCharacters, false))
                    {
                        Debug.LogWarning("[POLYMULATOR] Atlas warm-up incomplete for " + path + ": " + missingCharacters);
                    }
                }

                EditorUtility.SetDirty(fontAsset);
                stabilized++;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[POLYMULATOR] Stabilized " + stabilized + " dynamic font assets with " +
                      characterSet.Length + " characters.");
        }

        private static string BuildCharacterSet()
        {
            var characters = new SortedSet<char>();

            for (char c = ' '; c <= '~'; c++)
                characters.Add(c);

            foreach (string[] row in ParseCsv(CsvPath))
            {
                if (row.Length < 3)
                    continue;

                AddText(characters, row[1]);
                AddText(characters, row[2]);
            }

            var builder = new StringBuilder(characters.Count);
            foreach (char character in characters)
                builder.Append(character);

            return builder.ToString();
        }

        private static void AddText(ISet<char> characters, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            foreach (char character in text)
            {
                if (character != '\r' && character != '\n')
                    characters.Add(character);
            }
        }

        private static List<string[]> ParseCsv(string path)
        {
            string content = File.ReadAllText(path, Encoding.UTF8);
            if (content.Length > 0 && content[0] == '\uFEFF')
                content = content.Substring(1);

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
                    if (c == '\r' && i + 1 < content.Length && content[i + 1] == '\n')
                        i++;
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
