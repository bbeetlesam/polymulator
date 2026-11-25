using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Per-level metadata, objective configuration, and diegetic narrative beats stored in
    /// Assets/Resources/Levels/Level{chapter}.{level}.asset.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelInfo", menuName = "POLYMULATOR/Level Info")]
    public sealed class LevelInfo : ScriptableObject
    {
        [System.Serializable]
        public struct StoryLine
        {
            public string speaker;
            public string text;
        }

        [Header("Identity")]
        [SerializeField] private string levelTitle = string.Empty;
        [SerializeField] private string chapterSubtitle = "TRANSLASI";
        [SerializeField] private string districtLabel = "RUANG UJI";
        [SerializeField, Min(1)] private int parActions = 8;

        [Header("Objective Rules & Unlocks")]
        [SerializeField] private bool requireFacingAngle;
        [SerializeField] private float requiredFacingYaw;
        [SerializeField] private string fragmentTitle = string.Empty;
        [SerializeField] private bool triggerGlitchOnClear;
        [SerializeField] private bool playPianoOnClear;
        [SerializeField] private bool allowPartition;
        [SerializeField] private bool allowFusion;

        [Header("Diegetic Narrative & Hints")]
        [SerializeField] private StoryLine[] openingDialogue = System.Array.Empty<StoryLine>();
        [SerializeField] private StoryLine[] clearDialogue = System.Array.Empty<StoryLine>();
        [SerializeField] private string[] hints = System.Array.Empty<string>();

        private string Key(string suffix) => LocalizationKeys.Level(name, suffix);

        public string Title => L.TOr(Key("title"), levelTitle);
        public string ChapterSubtitle => L.TOr(Key("chapter_subtitle"), chapterSubtitle);
        public string DistrictLabel => L.TOr(Key("district_label"), districtLabel);
        public int ParActions => Mathf.Max(1, parActions);
        public bool RequireFacingAngle => requireFacingAngle;
        public float RequiredFacingYaw => requiredFacingYaw;
        public string FragmentTitle => L.TOr(Key("fragment_title"), fragmentTitle);
        public bool TriggerGlitchOnClear => triggerGlitchOnClear;
        public bool PlayPianoOnClear => playPianoOnClear;
        public bool AllowPartition => allowPartition;
        public bool AllowFusion => allowFusion;
        public StoryLine[] OpeningDialogue => LocalizeLines(openingDialogue, "openingDialogue");
        public StoryLine[] ClearDialogue => LocalizeLines(clearDialogue, "clearDialogue");
        public string[] Hints => LocalizeHints(hints);

        private StoryLine[] LocalizeLines(StoryLine[] source, string group)
        {
            if (source == null || source.Length == 0) return System.Array.Empty<StoryLine>();
            var result = new StoryLine[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                result[i] = source[i];
                result[i].text = L.TOr(Key(group + "." + i + ".text"), source[i].text);
            }
            return result;
        }

        private string[] LocalizeHints(string[] source)
        {
            if (source == null || source.Length == 0) return System.Array.Empty<string>();
            var result = new string[source.Length];
            for (int i = 0; i < source.Length; i++)
                result[i] = L.TOr(Key("hints." + i), source[i]);
            return result;
        }

        public void Configure(string title, string chapterSub, string district, int par,
            bool requireAngle, float targetYaw, string fragment, bool glitchClear, bool pianoClear,
            StoryLine[] opening, StoryLine[] clear, string[] simHints,
            bool canUsePartition = false, bool canUseFusion = false)
        {
            levelTitle = title;
            chapterSubtitle = chapterSub;
            districtLabel = district;
            parActions = Mathf.Max(1, par);
            requireFacingAngle = requireAngle;
            requiredFacingYaw = targetYaw;
            fragmentTitle = fragment;
            triggerGlitchOnClear = glitchClear;
            playPianoOnClear = pianoClear;
            allowPartition = canUsePartition;
            allowFusion = canUseFusion;
            openingDialogue = opening ?? System.Array.Empty<StoryLine>();
            clearDialogue = clear ?? System.Array.Empty<StoryLine>();
            hints = simHints ?? System.Array.Empty<string>();
        }
    }
}
