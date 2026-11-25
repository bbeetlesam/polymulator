using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Persistent player settings for master volume, camera orbit sensitivity,
    /// and reduced screen motion accessibility across Main Menu and chambers.
    /// </summary>
    public static class SimulationSettings
    {
        private const string KeyVolume = "POLY_SET_VOL";
        private const string KeySensitivity = "POLY_SET_SENS";
        private const string KeyReduceMotion = "POLY_SET_REDUCE_MOTION";
        private const string KeyLanguage = "POLY_SET_LANG";

        public static float MasterVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(KeyVolume, 0.85f));
            set
            {
                PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                ApplyAudioVolume();
            }
        }

        public static float CameraSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(KeySensitivity, 1.0f), 0.35f, 2.2f);
            set
            {
                PlayerPrefs.SetFloat(KeySensitivity, Mathf.Clamp(value, 0.35f, 2.2f));
                PlayerPrefs.Save();
            }
        }

        public static bool ReduceMotion
        {
            get => PlayerPrefs.GetInt(KeyReduceMotion, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyReduceMotion, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static GameLanguage Language
        {
            get => PlayerPrefs.GetInt(KeyLanguage, (int)GameLanguage.English) == (int)GameLanguage.Indonesian
                ? GameLanguage.Indonesian
                : GameLanguage.English;
            set
            {
                PlayerPrefs.SetInt(KeyLanguage, (int)value);
                PlayerPrefs.Save();
            }
        }

        public static void ApplyAudioVolume()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
