using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Polymulator
{
    /// <summary>
    /// Persistent uGUI Screen-Space Overlay transition controller.
    /// Fades cleanly using Catppuccin Mantle without legacy OnGUI allocations,
    /// blocking pointer clicks while loading between MainMenu and simulation chambers.
    /// </summary>
    public sealed class SceneTransition : MonoBehaviour
    {
        private enum Phase { Waiting, FadeOut, FadeIn, Done }
        private enum Mode { LoadScene, Quit }

        private Image fadeImage;
        private string sceneName;
        private float delay;
        private float fadeDuration;
        private float startTime;
        private float alpha;
        private Phase phase;
        private Mode mode;
        private Color fadeColor;

        private static SceneTransition activeTransition;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeTransition = null;
        }

        public static void Begin(string targetScene, float waitSeconds, float duration)
            => Begin(targetScene, waitSeconds, duration, PaletteSettings.Mantle);

        public static void Begin(string targetScene, float waitSeconds, float duration, Color color)
        {
            if (activeTransition != null) return;
            SceneTransition transition = Create(waitSeconds, duration, color);
            transition.mode = Mode.LoadScene;
            transition.sceneName = targetScene;
        }

        public static void Quit(float waitSeconds, float duration)
        {
            if (activeTransition != null) return;
            SceneTransition transition = Create(waitSeconds, duration, PaletteSettings.Crust);
            transition.mode = Mode.Quit;
        }

        /// <summary>Cancels and destroys any in-flight scene transition overlay.</summary>
        public static void CancelIfActive()
        {
            if (activeTransition == null) return;
            SceneTransition current = activeTransition;
            activeTransition = null;
            current.enabled = false;
            current.gameObject.SetActive(false);
            Destroy(current.gameObject);
        }

        private static SceneTransition Create(float waitSeconds, float duration, Color color)
        {
            GameObject overlay = new GameObject("Scene Transition");
            DontDestroyOnLoad(overlay);

            Canvas canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;

            GameObject panelGo = new GameObject("TransitionFadePanel");
            panelGo.transform.SetParent(overlay.transform, false);
            RectTransform rt = panelGo.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            Image img = panelGo.AddComponent<Image>();
            img.raycastTarget = true;
            color.a = 0f;
            img.color = color;

            SceneTransition transition = overlay.AddComponent<SceneTransition>();
            transition.fadeImage = img;
            transition.fadeColor = color;
            activeTransition = transition;
            bool reduce = SimulationSettings.ReduceMotion;
            transition.delay = reduce ? 0f : Mathf.Max(0f, waitSeconds);
            transition.fadeDuration = reduce ? Mathf.Min(0.10f, Mathf.Max(0.05f, duration)) : Mathf.Max(0.05f, duration);
            transition.startTime = Time.unscaledTime;
            transition.phase = Phase.Waiting;
            return transition;
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
                    ApplyAlpha();
                    if (elapsed >= fadeDuration)
                    {
                        alpha = 1f;
                        ApplyAlpha();
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
                    ApplyAlpha();
                    if (elapsed >= fadeDuration)
                    {
                        alpha = 0f;
                        ApplyAlpha();
                        phase = Phase.Done;
                        Destroy(gameObject);
                    }
                    break;
            }
        }

        private void ApplyAlpha()
        {
            if (fadeImage == null) return;
            Color c = fadeColor;
            c.a = Mathf.Clamp01(alpha);
            fadeImage.color = c;
            fadeImage.raycastTarget = alpha > 0.02f;
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (activeTransition == this) activeTransition = null;
        }
    }
}
