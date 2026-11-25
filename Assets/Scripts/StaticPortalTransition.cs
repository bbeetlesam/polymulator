using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Polymulator
{
    /// <summary>
    /// Seamless non-black scene transition driven by a static rear-camera snapshot.
    /// Holds the captured texture across SceneManager.LoadScene and dissolves it as
    /// the gameplay camera zooms out from behind Rect into default isometric framing.
    /// </summary>
    public sealed class StaticPortalTransition : MonoBehaviour
    {
        private static StaticPortalTransition activeInstance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            activeInstance = null;
        }

        private Texture2D snapshotTexture;
        private RawImage overlayImage;
        private float holdDuration;
        private float dissolveDuration;

        public static void Begin(string targetScene, Texture2D capturedFrame,
            float holdSeconds = 0.06f, float dissolveSeconds = 0.28f)
        {
            if (string.IsNullOrEmpty(targetScene))
            {
                if (capturedFrame != null) Destroy(capturedFrame);
                return;
            }

            SceneTransition.CancelIfActive();

            if (activeInstance != null)
            {
                Destroy(activeInstance.gameObject);
                activeInstance = null;
            }

            GameObject host = new GameObject("StaticPortalTransition");
            DontDestroyOnLoad(host);

            StaticPortalTransition controller = host.AddComponent<StaticPortalTransition>();
            controller.snapshotTexture = capturedFrame;
            bool reduce = SimulationSettings.ReduceMotion;
            controller.holdDuration = reduce ? 0f : Mathf.Max(0f, holdSeconds);
            controller.dissolveDuration = reduce ? 0.08f : Mathf.Max(0.08f, dissolveSeconds);
            activeInstance = controller;

            controller.BuildOverlayCanvas(host);
            controller.StartCoroutine(controller.ExecuteRoutine(targetScene));
        }

        public static bool IsActive => activeInstance != null;

        private void BuildOverlayCanvas(GameObject host)
        {
            Canvas canvas = host.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            GameObject rawGo = new GameObject("StaticSnapshot");
            rawGo.transform.SetParent(host.transform, false);

            overlayImage = rawGo.AddComponent<RawImage>();
            overlayImage.raycastTarget = true;
            if (snapshotTexture != null)
            {
                overlayImage.texture = snapshotTexture;
                overlayImage.color = Color.white;
            }
            else
            {
                overlayImage.color = PaletteSettings.Crust;
            }

            RectTransform rt = overlayImage.rectTransform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private IEnumerator ExecuteRoutine(string targetScene)
        {
            SceneManager.LoadScene(targetScene);

            // Allow one frame for newly loaded chamber lights, Rect, and camera intro pose to bind.
            yield return null;

            if (holdDuration > 0f)
            {
                float h = 0f;
                while (h < holdDuration)
                {
                    h += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            float elapsed = 0f;
            while (elapsed < dissolveDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / dissolveDuration);
                float alpha = 1f - (t * t * (3f - 2f * t));
                if (overlayImage != null)
                {
                    Color c = overlayImage.color;
                    c.a = alpha;
                    overlayImage.color = c;
                }
                yield return null;
            }

            if (activeInstance == this) activeInstance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (activeInstance == this) activeInstance = null;
            if (snapshotTexture != null)
            {
                Destroy(snapshotTexture);
                snapshotTexture = null;
            }
        }
    }
}
