using UnityEngine;
using UnityEngine.SceneManagement;

namespace Polymulator
{
    /// <summary>
    /// Collectible narrative artifact (Memory Fragment) anchored softly in world space.
    /// Triggers story dialogue, particle celebration, persistent save, and optional Adi Piano motif.
    /// </summary>
    public sealed class MemoryFragment : MonoBehaviour
    {
        public string fragmentTitle = "Fragmen Ingatan";
        public string speaker = "RECT";
        [TextArea] public string storyLine = string.Empty;
        public string replySpeaker = "SIM";
        [TextArea] public string replyLine = string.Empty;
        public bool playPianoMotif;

        public Transform visualCrystal;
        public Light fragmentLight;

        private Vector3 startLocalPos;
        private bool collected;

        public static event System.Action<string, string, string, string, string> OnCollected;

        private static string Key(string suffix)
        {
            return LocalizationKeys.Fragment(SceneManager.GetActiveScene().name, suffix);
        }

        private void Start()
        {
            if (visualCrystal == null) visualCrystal = transform;
            startLocalPos = visualCrystal.localPosition;

            if (SimulationProgress.HasFragment(SceneManager.GetActiveScene().name))
            {
                collected = true;
                gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (collected || visualCrystal == null) return;
            float bob = Mathf.Sin(Time.time * 2.4f) * GridSettings.ToWorld(0.09f);
            visualCrystal.localPosition = startLocalPos + Vector3.up * bob;
            visualCrystal.Rotate(Vector3.up, 64f * Time.deltaTime, Space.World);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (collected) return;
            if (!other.CompareTag("Player") && other.GetComponentInParent<RectController>() == null) return;
            Collect();
        }

        public void Collect()
        {
            if (collected) return;
            collected = true;

            string sceneName = SceneManager.GetActiveScene().name;
            SimulationProgress.RecordFragment(sceneName);
            SimulationAudio.PlayFragmentPickup(transform.position, playPianoMotif);
            SimulationVFX.Burst(transform.position, PaletteSettings.Rosewater, 24, 3.0f);
            SimulationVFX.Ring(transform.position, PaletteSettings.Yellow, 2.2f, 0.45f);
            SimulationVisualBridge.TriggerFloorRipple(transform.position, PaletteSettings.Rosewater, 6.2f, 1.15f, 0.72f);

            OnCollected?.Invoke(
                L.TOr(Key("title"), fragmentTitle),
                speaker,
                L.TOr(Key("line"), storyLine),
                replySpeaker,
                L.TOr(Key("reply_line"), replyLine));
            gameObject.SetActive(false);
        }
    }
}
