using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polymulator
{
    /// <summary>
    /// Persistent fade overlay. Fades the current scene to the flat Mantle colour,
    /// which is the darkest palette entry, then either loads the next scene and
    /// fades back in, or quits the game. SmoothStep gives the fade an ease in/out
    /// curve rather than a linear ramp.
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        private enum Phase { Waiting, FadeOut, FadeIn, Done }
        private enum Mode { LoadScene, Quit }

        private Texture2D fadeTexture;
        private string sceneName;
        private float delay;
        private float fadeDuration;
        private float startTime;
        private float alpha;
        private Phase phase;
        private Mode mode;

        private static SceneTransition activeTransition;

        public static void Begin(string targetScene, float waitSeconds, float duration)
        {
            if (activeTransition != null) return;
            SceneTransition transition = Create(waitSeconds, duration);
            transition.mode = Mode.LoadScene;
            transition.sceneName = targetScene;
        }

        public static void Quit(float waitSeconds, float duration)
        {
            if (activeTransition != null) return;
            SceneTransition transition = Create(waitSeconds, duration);
            transition.mode = Mode.Quit;
        }

        private static SceneTransition Create(float waitSeconds, float duration)
        {
            GameObject overlay = new GameObject("Scene Transition");
            DontDestroyOnLoad(overlay);

            SceneTransition transition = overlay.AddComponent<SceneTransition>();
            activeTransition = transition;
            transition.delay = Mathf.Max(0f, waitSeconds);
            transition.fadeDuration = Mathf.Max(0.05f, duration);
            transition.startTime = Time.unscaledTime;
            transition.phase = Phase.Waiting;
            return transition;
        }

        private void Awake()
        {
            fadeTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            fadeTexture.SetPixel(0, 0, Color.white);
            fadeTexture.Apply();
            fadeTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - startTime;

            switch (phase)
            {
                case Phase.Waiting:
                    if (elapsed >= delay)
                    {
                        phase = Phase.FadeOut;
                        startTime = Time.unscaledTime;
                    }
                    break;

                case Phase.FadeOut:
                    alpha = Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);
                    if (elapsed >= fadeDuration)
                    {
                        alpha = 1f;
                        if (mode == Mode.Quit)
                        {
                            phase = Phase.Done;
                            QuitGame();
                        }
                        else
                        {
                            phase = Phase.FadeIn;
                            Time.timeScale = 1f;
                            SceneManager.LoadScene(sceneName);
                            startTime = Time.unscaledTime;
                        }
                    }
                    break;

                case Phase.FadeIn:
                    alpha = 1f - Mathf.SmoothStep(0f, 1f, elapsed / fadeDuration);
                    if (elapsed >= fadeDuration)
                    {
                        alpha = 0f;
                        phase = Phase.Done;
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnGUI()
        {
            if (alpha <= 0f || fadeTexture == null) return;

            Color previous = GUI.color;
            Color tint = PaletteSettings.Mantle;
            tint.a = alpha;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), fadeTexture,
                ScaleMode.StretchToFill);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (activeTransition == this) activeTransition = null;
            if (fadeTexture != null) Destroy(fadeTexture);
        }
    }
}
