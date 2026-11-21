using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Procedural Web/Desktop-safe spatial and UI audio synthesizer using PCM AudioClip generation
    /// and pooled AudioSources with pitch randomization to avoid repetitive SFX playback.
    /// </summary>
    public sealed class SimulationAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;
        private const int SpatialPoolSize = 8;

        public static SimulationAudio Instance { get; private set; }

        [Header("Master Volumes")]
        [Range(0f, 1f)] public float uiVolume = 0.55f;
        [Range(0f, 1f)] public float sfxVolume = 0.65f;

        private AudioSource uiSource;
        private readonly AudioSource[] spatialPool = new AudioSource[SpatialPoolSize];
        private int nextSpatialIndex;
        private readonly Dictionary<string, float> cooldowns = new Dictionary<string, float>();

        private AudioClip clipMenuHover;
        private AudioClip clipMenuClick;
        private AudioClip clipMenuSmash;
        private AudioClip clipMenuLand;
        private AudioClip clipModeFlexible;
        private AudioClip clipModeLimited;
        private AudioClip clipPickup;
        private AudioClip clipRotate;
        private AudioClip clipDrop;
        private AudioClip clipThrow;
        private AudioClip clipTetherLock;
        private AudioClip clipTetherRelease;
        private AudioClip clipPlateOn;
        private AudioClip clipPlateOff;
        private AudioClip clipAnchorReject;
        private AudioClip clipAnchorLock;
        private AudioClip clipImpact;

        private readonly List<AudioClip> ownedClips = new List<AudioClip>(20);

        public static SimulationAudio EnsureInstance()
        {
            if (Instance != null) return Instance;
            SimulationAudio found = Object.FindFirstObjectByType<SimulationAudio>();
            if (found != null)
            {
                Instance = found;
                return found;
            }

            GameObject go = new GameObject("Simulation Audio");
            return go.AddComponent<SimulationAudio>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildAudioSources();
            SynthesizeLibrary();
        }

        private void BuildAudioSources()
        {
            uiSource = gameObject.AddComponent<AudioSource>();
            uiSource.playOnAwake = false;
            uiSource.spatialBlend = 0f;
            uiSource.ignoreListenerPause = true;

            for (int i = 0; i < SpatialPoolSize; i++)
            {
                GameObject emitter = new GameObject("Audio Pool " + i);
                emitter.transform.SetParent(transform, false);
                AudioSource src = emitter.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0.85f;
                src.minDistance = GridSettings.ToWorld(2.5f);
                src.maxDistance = GridSettings.ToWorld(24f);
                src.rolloffMode = AudioRolloffMode.Logarithmic;
                spatialPool[i] = src;
            }
        }

        private void SynthesizeLibrary()
        {
            clipMenuHover = RegisterClip("SFX_MenuHover", 0.055f,
                t => Env(t, 0.004f, 0.055f) * 0.16f * Sine(t, 880f));

            clipMenuClick = RegisterClip("SFX_MenuClick", 0.09f,
                t => Env(t, 0.003f, 0.09f) * 0.28f * (0.7f * Sine(t, 660f) + 0.3f * Sine(t, 990f)));

            clipMenuSmash = RegisterClip("SFX_MenuSmash", 0.22f,
                t => Env(t, 0.002f, 0.22f) * 0.40f *
                     (0.55f * Sine(t, Mathf.Lerp(170f, 52f, t / 0.22f)) + 0.45f * SoftNoise(t, 17f) * (1f - t / 0.22f)));

            clipMenuLand = RegisterClip("SFX_MenuLand", 0.26f,
                t => Env(t, 0.008f, 0.26f) * 0.32f * (0.6f * Sine(t, 392f) + 0.4f * Sine(t, 587.33f)));

            clipModeFlexible = RegisterClip("SFX_ModeFlex", 0.28f,
                t => Env(t, 0.01f, 0.28f) * 0.32f *
                     (0.5f * Sine(t, 523.25f) + 0.35f * Sine(t, 659.25f) + 0.30f * Sine(t, 783.99f) * StepRatio(t, 0.08f)));

            clipModeLimited = RegisterClip("SFX_ModeLimited", 0.24f,
                t => Env(t, 0.008f, 0.24f) * 0.32f *
                     (0.55f * Sine(t, 440f) + 0.35f * Sine(t, 329.63f) * StepRatio(t, 0.05f)));

            clipPickup = RegisterClip("SFX_Pickup", 0.14f,
                t => Env(t, 0.005f, 0.14f) * 0.30f * Sine(t, Mathf.Lerp(380f, 640f, t / 0.14f)));

            clipRotate = RegisterClip("SFX_Rotate", 0.08f,
                t => Env(t, 0.003f, 0.08f) * 0.26f * (0.75f * Sine(t, 740f) + 0.25f * Triangle(t, 1110f)));

            clipDrop = RegisterClip("SFX_Drop", 0.16f,
                t => Env(t, 0.003f, 0.16f) * 0.38f *
                     (0.75f * Sine(t, Mathf.Lerp(210f, 88f, t / 0.16f)) + 0.25f * SoftNoise(t, 9f) * (1f - t / 0.16f)));

            clipThrow = RegisterClip("SFX_Throw", 0.20f,
                t => Env(t, 0.008f, 0.20f) * 0.34f *
                     (0.60f * Sine(t, Mathf.Lerp(240f, 520f, t / 0.20f)) + 0.40f * SoftNoise(t, 13f)));

            clipTetherLock = RegisterClip("SFX_TetherLock", 0.24f,
                t => Env(t, 0.01f, 0.24f) * 0.30f *
                     (0.55f * Sine(t, 587.33f) + 0.45f * Sine(t, 880f) + 0.18f * Triangle(t, 1174.66f)));

            clipTetherRelease = RegisterClip("SFX_TetherRelease", 0.18f,
                t => Env(t, 0.005f, 0.18f) * 0.26f * Sine(t, Mathf.Lerp(620f, 290f, t / 0.18f)));

            clipPlateOn = RegisterClip("SFX_PlateOn", 0.30f,
                t => Env(t, 0.006f, 0.30f) * 0.35f *
                     (0.50f * Sine(t, 349.23f) + 0.50f * Sine(t, 523.25f) * StepRatio(t, 0.06f)));

            clipPlateOff = RegisterClip("SFX_PlateOff", 0.20f,
                t => Env(t, 0.005f, 0.20f) * 0.28f *
                     (0.60f * Sine(t, 329.63f) + 0.40f * Sine(t, 246.94f) * StepRatio(t, 0.05f)));

            clipAnchorReject = RegisterClip("SFX_AnchorReject", 0.22f,
                t => Env(t, 0.005f, 0.22f) * 0.30f *
                     (0.55f * Triangle(t, 220f) + 0.45f * Triangle(t, 233.08f)));

            clipAnchorLock = RegisterClip("SFX_AnchorLock", 0.72f,
                t => Env(t, 0.012f, 0.72f) * 0.36f * (
                     0.35f * Sine(t, 392.00f) +
                     0.30f * Sine(t, 493.88f) * StepRatio(t, 0.08f) +
                     0.30f * Sine(t, 587.33f) * StepRatio(t, 0.16f) +
                     0.35f * Sine(t, 783.99f) * StepRatio(t, 0.26f)));

            clipImpact = RegisterClip("SFX_Impact", 0.11f,
                t => Env(t, 0.002f, 0.11f) * 0.34f *
                     (0.70f * Sine(t, Mathf.Lerp(180f, 70f, t / 0.11f)) + 0.30f * SoftNoise(t, 21f) * (1f - t / 0.11f)));
        }

        private AudioClip RegisterClip(string clipName, float durationSeconds, System.Func<float, float> generator)
        {
            int sampleCount = Mathf.Max(64, Mathf.RoundToInt(durationSeconds * SampleRate));
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
                samples[i] = Mathf.Clamp(generator(i / (float)SampleRate), -1f, 1f);

            AudioClip clip = AudioClip.Create(clipName, sampleCount, 1, SampleRate, false);
            clip.SetData(samples, 0);
            ownedClips.Add(clip);
            return clip;
        }

        private static float Sine(float t, float hz) => Mathf.Sin(2f * Mathf.PI * hz * t);
        private static float Triangle(float t, float hz) => 2f * Mathf.Abs(2f * ((t * hz) - Mathf.Floor(t * hz + 0.5f))) - 1f;
        private static float Env(float t, float attack, float total)
        {
            float up = attack > 0f ? Mathf.Clamp01(t / attack) : 1f;
            float down = Mathf.Pow(Mathf.Clamp01(1f - t / Mathf.Max(0.01f, total)), 1.6f);
            return up * down;
        }
        private static float StepRatio(float t, float delay) => t >= delay ? 1f : 0f;
        private static float SoftNoise(float t, float seed)
        {
            float v = Mathf.Sin((t * 4375.85f + seed) * 12.9898f) * 43758.5453f;
            return (v - Mathf.Floor(v)) * 2f - 1f;
        }

        public static void PlayMenuHover() => EnsureInstance().PlayUI("hover", Instance.clipMenuHover, 0.45f, 0.05f, 0.05f);
        public static void PlayMenuClick() => EnsureInstance().PlayUI("click", Instance.clipMenuClick, 0.85f, 0.03f, 0.04f);
        public static void PlayMenuSmash() => EnsureInstance().PlayUI("smash", Instance.clipMenuSmash, 0.90f, 0.05f, 0.05f);
        public static void PlayMenuNodeLand() => EnsureInstance().PlayUI("land", Instance.clipMenuLand, 0.80f, 0.02f, 0.02f);

        public static void PlayModeSwitch(bool toFlexible)
        {
            SimulationAudio a = EnsureInstance();
            a.PlayUI("mode", toFlexible ? a.clipModeFlexible : a.clipModeLimited, 0.90f, 0.04f, 0.02f);
        }

        public static void PlayPickup(Vector3 pos) => EnsureInstance().PlaySpatial("pickup", Instance.clipPickup, pos, 0.85f, 0.05f, 0.06f);
        public static void PlayRotate(Vector3 pos) => EnsureInstance().PlaySpatial("rotate", Instance.clipRotate, pos, 0.72f, 0.04f, 0.06f);
        public static void PlayDrop(Vector3 pos, float mass)
        {
            float pitchBias = Mathf.Clamp(1.12f - mass * 0.08f, 0.78f, 1.15f);
            EnsureInstance().PlaySpatial("drop", Instance.clipDrop, pos, 0.92f, 0.06f, pitchBias);
        }
        public static void PlayThrow(Vector3 pos) => EnsureInstance().PlaySpatial("throw", Instance.clipThrow, pos, 0.95f, 0.05f, 0.05f);
        public static void PlayTelekinesisLock(Vector3 pos) => EnsureInstance().PlaySpatial("tk_lock", Instance.clipTetherLock, pos, 0.85f, 0.05f, 0.03f);
        public static void PlayTelekinesisRelease(Vector3 pos) => EnsureInstance().PlaySpatial("tk_rel", Instance.clipTetherRelease, pos, 0.78f, 0.05f, 0.04f);
        public static void PlayPlateToggle(Vector3 pos, bool active)
        {
            SimulationAudio a = EnsureInstance();
            a.PlaySpatial("plate", active ? a.clipPlateOn : a.clipPlateOff, pos, 0.95f, 0.05f, 0.03f);
        }
        public static void PlayAnchorReject(Vector3 pos) => EnsureInstance().PlaySpatial("reject", Instance.clipAnchorReject, pos, 0.85f, 0.35f, 0.03f);
        public static void PlayAnchorLock(Vector3 pos) => EnsureInstance().PlaySpatial("anchor_lock", Instance.clipAnchorLock, pos, 1.0f, 0.5f, 0f);

        public static void PlayImpact(Vector3 pos, float relativeSpeed, float mass)
        {
            if (relativeSpeed < 1.35f) return;
            SimulationAudio a = EnsureInstance();
            float gain = Mathf.Clamp01((relativeSpeed - 1.2f) / 7f) * Mathf.Clamp(0.45f + mass * 0.25f, 0.35f, 0.95f);
            float basePitch = Mathf.Clamp(1.15f - mass * 0.1f, 0.75f, 1.15f);
            a.PlaySpatial("impact", a.clipImpact, pos, gain, 0.08f, basePitch, 0.08f);
        }

        private void PlayUI(string key, AudioClip clip, float volumeScale, float cooldown, float pitchJitter)
        {
            if (clip == null || uiSource == null || !CanPlay(key, cooldown)) return;
            uiSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            uiSource.PlayOneShot(clip, Mathf.Clamp01(uiVolume * volumeScale));
        }

        private void PlaySpatial(string key, AudioClip clip, Vector3 pos, float volumeScale, float cooldown,
            float basePitch, float pitchJitter = 0.05f)
        {
            if (clip == null || !CanPlay(key, cooldown)) return;
            AudioSource src = spatialPool[nextSpatialIndex];
            nextSpatialIndex = (nextSpatialIndex + 1) % SpatialPoolSize;
            if (src == null) return;

            src.transform.position = pos;
            src.pitch = Mathf.Clamp(basePitch + Random.Range(-pitchJitter, pitchJitter), 0.6f, 1.5f);
            src.PlayOneShot(clip, Mathf.Clamp01(sfxVolume * volumeScale));
        }

        private bool CanPlay(string key, float cooldown)
        {
            float now = Time.unscaledTime;
            if (cooldowns.TryGetValue(key, out float last) && now - last < cooldown)
                return false;
            cooldowns[key] = now;
            return true;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            for (int i = 0; i < ownedClips.Count; i++)
            {
                if (ownedClips[i] != null) Destroy(ownedClips[i]);
            }
            ownedClips.Clear();
        }
    }
}
