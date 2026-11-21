using UnityEditor;
using UnityEngine;
using Polymulator;

namespace Polymulator.EditorTools
{
    [CustomEditor(typeof(PaletteSettings))]
    public sealed class PaletteSettingsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "PaletteSettings is the single source of colour for materials, lighting and UI. " +
                "Materials and the UI read these values at runtime. Rebuild the prototype scene " +
                "after changing environment colours so the generated materials pick up the new values.",
                MessageType.Info);

            if (GUILayout.Button("Reset to Official Catppuccin Mocha (26 Colours)"))
            {
                ((PaletteSettings)target).ApplyCatppuccinMochaDefaults();
                EditorUtility.SetDirty(target);
                AssetDatabase.SaveAssets();
            }

            if (GUILayout.Button("Rebuild Prototype Scene"))
                PrototypeSceneBuilder.Build();
        }
    }
}
