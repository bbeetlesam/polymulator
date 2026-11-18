using UnityEditor;
using UnityEngine;
using Polymulator;

namespace Polymulator.EditorTools
{
    [CustomEditor(typeof(GridSettings))]
    public sealed class GridSettingsInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "GRID_UNIT_SIZE is the world-space size of one logical grid unit. " +
                "The prototype defines all entity dimensions in grid units and converts them " +
                "through this value. After changing it, rebuild the prototype scene so meshes, " +
                "colliders, player, camera, and interaction ranges all use the new scale.",
                MessageType.Info);

            if (GUILayout.Button("Rebuild Prototype Scene"))
                PrototypeSceneBuilder.Build();
        }
    }
}
