using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Generates dynamic SDF TextCore FontAssets from the SIL OFL Akt typeface family
    /// under Assets/UI/Fonts/Akt/ and binds Akt Regular as the default UI Toolkit text font.
    /// </summary>
    public static class AktFontBuilder
    {
        private static readonly (string ttfPath, string assetPath)[] Pairs =
        {
            ("Assets/UI/Fonts/Akt/Akt-Regular.ttf", "Assets/UI/Fonts/Font_AktRegular.asset"),
            ("Assets/UI/Fonts/Akt/Akt-Medium.ttf", "Assets/UI/Fonts/Font_AktMedium.asset"),
            ("Assets/UI/Fonts/Akt/Akt-SemiBold.ttf", "Assets/UI/Fonts/Font_AktSemiBold.asset"),
            ("Assets/UI/Fonts/Akt/Akt-Bold.ttf", "Assets/UI/Fonts/Font_AktBold.asset")
        };

        [MenuItem("Tools/POLYMULATOR/Build Akt FontAssets")]
        public static void BuildAktFontAssets()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            FontAsset defaultAkt = null;
            for (int i = 0; i < Pairs.Length; i++)
            {
                var (ttfPath, assetPath) = Pairs[i];
                Font font = AssetDatabase.LoadAssetAtPath<Font>(ttfPath);
                if (font == null)
                {
                    Debug.LogError("[AktFontBuilder] Missing source font at: " + ttfPath);
                    continue;
                }

                FontAsset existing = AssetDatabase.LoadAssetAtPath<FontAsset>(assetPath);
                if (existing != null)
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }

                FontAsset created = FontAsset.CreateFontAsset(
                    font,
                    90,
                    9,
                    GlyphRenderMode.SDFAA,
                    1024,
                    1024,
                    AtlasPopulationMode.Dynamic,
                    true);

                if (created == null)
                {
                    Debug.LogError("[AktFontBuilder] Failed creating FontAsset from: " + ttfPath);
                    continue;
                }

                created.name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
                created.isMultiAtlasTexturesEnabled = true;
                AssetDatabase.CreateAsset(created, assetPath);

                if (created.atlasTextures != null)
                {
                    for (int t = 0; t < created.atlasTextures.Length; t++)
                    {
                        if (created.atlasTextures[t] != null)
                        {
                            created.atlasTextures[t].name = created.name + " Atlas " + t;
                            AssetDatabase.AddObjectToAsset(created.atlasTextures[t], created);
                        }
                    }
                }

                if (created.material != null)
                {
                    created.material.name = created.name + " Material";
                    AssetDatabase.AddObjectToAsset(created.material, created);
                }

                EditorUtility.SetDirty(created);
                if (i == 0) defaultAkt = created;
                Debug.Log("[AktFontBuilder] Created " + assetPath);
            }

            if (defaultAkt != null)
            {
                var textSettings = AssetDatabase.LoadAssetAtPath<PanelTextSettings>("Assets/UI/PanelTextSettings.asset");
                if (textSettings != null)
                {
                    var so = new SerializedObject(textSettings);
                    var defProp = so.FindProperty("m_DefaultFontAsset");
                    if (defProp != null)
                    {
                        defProp.objectReferenceValue = defaultAkt;
                        so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    EditorUtility.SetDirty(textSettings);
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[AktFontBuilder] Done.");
        }
    }
}
