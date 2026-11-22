using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polymulator.EditorTools
{
    /// <summary>
    /// Ensures opening the project interactively never strands the Editor on an empty
    /// Untitled scene or a maximized Game view hiding Hierarchy/Project panels.
    /// </summary>
    [InitializeOnLoad]
    public static class PolymulatorEditorStartup
    {
        private const string MainMenuPath = "Assets/Scenes/MainMenu.unity";
        private const string SessionKey = "POLYMULATOR_EDITOR_STARTUP_DONE";

        static PolymulatorEditorStartup()
        {
            EditorApplication.delayCall += EnsureValidStartupSceneAndView;
        }

        [MenuItem("Tools/POLYMULATOR/Open MainMenu", priority = 50)]
        public static void OpenMainMenu()
        {
            if (File.Exists(MainMenuPath))
                EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        }

        private static void EnsureValidStartupSceneAndView()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
                return;

            if (SessionState.GetBool(SessionKey, false))
                return;
            SessionState.SetBool(SessionKey, true);

            foreach (EditorWindow win in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (win != null && win.GetType().Name == "GameView" && win.maximized)
                    win.maximized = false;
            }

            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty && File.Exists(MainMenuPath))
                EditorSceneManager.OpenScene(MainMenuPath, OpenSceneMode.Single);
        }
    }
}
