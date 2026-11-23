using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Polymulator
{
    /// <summary>
    /// Modern Catppuccin Mocha UI Toolkit HUD controller driven by mode/interaction events,
    /// chamber objective state, pause/clear modal flow, and light reactive inspection.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class SimulationHUDController : MonoBehaviour
    {
        [Header("References")]
        public SimulationModeController modeController;
        public ObjectInteractor interactor;
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;
        public WeightedPressurePlate pressurePlate;
        public AnchorSocketObjective anchorObjective;
        public SpatialBlueprintVisualizer blueprintVisualizer;

        private UIDocument uiDocument;
        private VisualElement boundVisualRoot;
        private static int interactiveHoverCount;

        public static bool IsPointerOverInteractiveUI => interactiveHoverCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticHoverCount()
        {
            interactiveHoverCount = 0;
        }

        private VisualElement modeAccentBar;
        private HudVectorIcon modeIcon;
        private Label chamberSubtitleLabel;
        private Label objectiveLabel;
        private Label modeBadgeLabel;
        private Label modeSummaryLabel;

        private VisualElement chamberOverlay;
        private VisualElement modalCard;
        private VisualElement modalAccentBar;
        private VisualElement modalMetrics;
        private Label modalTitle;
        private Label modalSubtitle;
        private Label modalRankLabel;
        private Label metricTranslations;
        private Label metricRotations;
        private Label metricTethers;
        private VisualElement pauseSettingsPanel;
        private Slider pauseSliderVolume;
        private Label pauseValVolume;
        private Slider pauseSliderSensitivity;
        private Label pauseValSensitivity;
        private Toggle pauseToggleReduce;
        private Button btnNext;
        private Button btnResume;
        private Button btnPauseSettings;
        private Button btnReplay;
        private Button btnMainMenu;

        private VisualElement storyFeed;
        private Label storySpeakerLabel;
        private Label storyProgressLabel;
        private Label storyTextLabel;

        private VisualElement targetCard;
        private VisualElement targetAccentBar;
        private VisualElement targetSwatch;
        private Label targetStateLabel;
        private Label targetNameLabel;
        private Label targetMetaLabel;
        private Label targetVectorLabel;

        private VisualElement statusToast;
        private VisualElement statusToastDot;
        private Label statusToastLabel;

        private VisualElement controlsCard;
        private VisualElement slotPartition;
        private VisualElement slotFusion;
        private Label keyPartitionLabel;
        private Label keyFusionLabel;
        private PartitionFusionController partitionFusionController;
        private EndingChoiceController endingController;
        private VisualElement endingEpilogueBox;
        private Label endingSummaryLabel;
        private Label endingPara1;
        private Label endingPara2;
        private Label endingPara3;
        private Label endingMemoriesLabel;
        private VisualElement contextPrompt;
        private VisualElement contextPromptAccent;
        private Label contextPromptLabel;

        private VisualElement slotRotasi;
        private VisualElement slotSpecial;
        private HudVectorIcon iconSpecial;
        private HudVectorIcon iconRotasi;
        private HudVectorIcon iconTranslasi;
        private HudVectorIcon iconElevasi;
        private HudVectorIcon iconPartition;
        private HudVectorIcon iconFusion;
        private Label keyRotasi;
        private Label titleSpecial;
        private Label keySpecial;

        private bool controlsExpanded = false;
        private float toastTimer;
        private string lastPromptText;
        private Color lastPromptColor;
        private bool hasShownFlexNarrative;
        private bool isPaused;
        private bool pauseSettingsOpen;
        private bool chamberCompleted;
        private float clearOverlayDelay = -1f;
        private string levelDisplayName = "Ruangan Uji 01";
        private string levelNumberLabel = "1.1";
        private string nextSceneName = string.Empty;
        private LevelInfo activeLevelInfo;
        private WeightedPressurePlate[] allPlates;
        private int lastClearStars = 1;
        private bool fragmentCollectedThisRun;
        private readonly Queue<LevelInfo.StoryLine> storyQueue = new Queue<LevelInfo.StoryLine>();
        private float storyHoldTimer;
        private bool storyShowing;
        private bool storyTyping;
        private string currentSpeakerRaw = "SIM";
        private string currentFullLine = string.Empty;
        private int currentTypedChars;
        private float nextTypeCharAt;
        private float cursorBlinkTimer;
        private int storyBatchTotal;
        private int storyBatchIndex;
        private int translationCount;
        private int rotationCount;
        private int throwOrTetherCount;
        private bool hasShownBlueprintConcept;
        private int hintStep;

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            ResolveReferences();
            ResolveLevelMetadata();
        }

        private void Start()
        {
            string prefix = SimulationProgress.IsSpecialShowcase(SceneManager.GetActiveScene().name)
                ? "Ruang Showcase — "
                : "Ruangan " + levelNumberLabel + " — ";
            HandleStatusBanner(prefix + levelDisplayName, PaletteSettings.Sapphire);
            RefreshObjectiveText();
            if (activeLevelInfo != null && activeLevelInfo.OpeningDialogue != null && activeLevelInfo.OpeningDialogue.Length > 0)
                EnqueueStoryLines(activeLevelInfo.OpeningDialogue);
        }

        private void ResolveLevelMetadata()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.StartsWith("Level") && sceneName.Length > 5)
                levelNumberLabel = sceneName.Substring(5);

            nextSceneName = SimulationProgress.GetNextSceneName(sceneName);
            activeLevelInfo = Resources.Load<LevelInfo>("Levels/" + sceneName);
            if (activeLevelInfo != null && !string.IsNullOrEmpty(activeLevelInfo.Title))
                levelDisplayName = activeLevelInfo.Title.ToUpperInvariant();
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindVisualTree();
            SubscribeEvents();
            RefreshModeVisuals();
            RefreshObjectiveText();
            RefreshTargetAndPrompt(true);
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
            interactiveHoverCount = 0;
            if (isPaused)
            {
                isPaused = false;
                Time.timeScale = 1f;
            }
        }

        private void ResolveReferences()
        {
            if (modeController == null) modeController = Object.FindFirstObjectByType<SimulationModeController>();
            if (interactor == null) interactor = Object.FindFirstObjectByType<ObjectInteractor>();
            if (carryController == null) carryController = Object.FindFirstObjectByType<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = Object.FindFirstObjectByType<TelekinesisController>();
            if (pressurePlate == null) pressurePlate = Object.FindFirstObjectByType<WeightedPressurePlate>();
            allPlates = Object.FindObjectsByType<WeightedPressurePlate>(FindObjectsSortMode.None);
            if (anchorObjective == null) anchorObjective = Object.FindFirstObjectByType<AnchorSocketObjective>();
            if (blueprintVisualizer == null) blueprintVisualizer = Object.FindFirstObjectByType<SpatialBlueprintVisualizer>();
            if (partitionFusionController == null) partitionFusionController = Object.FindFirstObjectByType<PartitionFusionController>();
            if (endingController == null) endingController = Object.FindFirstObjectByType<EndingChoiceController>();
            SimulationSettings.ApplyAudioVolume();
        }

        private void BindVisualTree()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            bool firstBind = boundVisualRoot != root;
            boundVisualRoot = root;

            modeAccentBar = root.Q<VisualElement>("mode-accent-bar");
            modeIcon = root.Q<HudVectorIcon>("mode-icon");
            chamberSubtitleLabel = root.Q<Label>("chamber-subtitle-label");
            objectiveLabel = root.Q<Label>("objective-label");
            modeBadgeLabel = root.Q<Label>("mode-badge-label");
            modeSummaryLabel = root.Q<Label>("mode-summary-label");

            chamberOverlay = root.Q<VisualElement>("chamber-overlay");
            modalCard = root.Q<VisualElement>("modal-card");
            modalAccentBar = root.Q<VisualElement>("modal-accent-bar");
            modalMetrics = root.Q<VisualElement>("modal-metrics");
            modalTitle = root.Q<Label>("modal-title");
            modalSubtitle = root.Q<Label>("modal-subtitle");
            modalRankLabel = root.Q<Label>("modal-rank-label");
            endingEpilogueBox = root.Q<VisualElement>("ending-epilogue-box");
            endingSummaryLabel = root.Q<Label>("ending-summary-label");
            endingPara1 = root.Q<Label>("ending-epilogue-p1");
            endingPara2 = root.Q<Label>("ending-epilogue-p2");
            endingPara3 = root.Q<Label>("ending-epilogue-p3");
            endingMemoriesLabel = root.Q<Label>("ending-memories-label");
            metricTranslations = root.Q<Label>("metric-translations");
            metricRotations = root.Q<Label>("metric-rotations");
            metricTethers = root.Q<Label>("metric-tethers");
            pauseSettingsPanel = root.Q<VisualElement>("pause-settings-panel");
            pauseSliderVolume = root.Q<Slider>("pause-slider-volume");
            pauseValVolume = root.Q<Label>("pause-val-volume");
            pauseSliderSensitivity = root.Q<Slider>("pause-slider-sensitivity");
            pauseValSensitivity = root.Q<Label>("pause-val-sensitivity");
            pauseToggleReduce = root.Q<Toggle>("pause-toggle-reduce");
            btnNext = root.Q<Button>("btn-next");
            btnResume = root.Q<Button>("btn-resume");
            btnPauseSettings = root.Q<Button>("btn-pause-settings");
            btnReplay = root.Q<Button>("btn-replay");
            btnMainMenu = root.Q<Button>("btn-mainmenu");

            storyFeed = root.Q<VisualElement>("story-feed");
            storySpeakerLabel = root.Q<Label>("story-speaker-label");
            storyProgressLabel = root.Q<Label>("story-progress-label");
            storyTextLabel = root.Q<Label>("story-text-label");

            if (firstBind)
            {
                if (storyFeed != null)
                {
                    storyFeed.RegisterCallback<PointerDownEvent>(OnStoryCardClicked);
                    RegisterInteractiveHover(storyFeed);
                }
                RegisterInteractiveHover(modalCard);
                RegisterInteractiveHover(controlsCard);
                if (btnNext != null)
                {
                    btnNext.clicked += LoadNextChamber;
                    btnNext.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
                }
                if (btnResume != null)
                {
                    btnResume.clicked += TogglePauseMenu;
                    btnResume.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
                }
                if (btnPauseSettings != null)
                {
                    btnPauseSettings.clicked += TogglePauseSettingsPanel;
                    btnPauseSettings.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
                }
                if (btnReplay != null)
                {
                    btnReplay.clicked += ReplayCurrentChamber;
                    btnReplay.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
                }
                if (btnMainMenu != null)
                {
                    btnMainMenu.clicked += ReturnToMainMenu;
                    btnMainMenu.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
                }

                if (pauseSliderVolume != null) pauseSliderVolume.RegisterValueChangedCallback(evt =>
                {
                    SimulationSettings.MasterVolume = evt.newValue / 100f;
                    if (pauseValVolume != null) pauseValVolume.text = Mathf.RoundToInt(evt.newValue) + "%";
                });
                if (pauseSliderSensitivity != null) pauseSliderSensitivity.RegisterValueChangedCallback(evt =>
                {
                    SimulationSettings.CameraSensitivity = evt.newValue / 100f;
                    if (pauseValSensitivity != null) pauseValSensitivity.text = (evt.newValue / 100f).ToString("0.0") + "x";
                });
                if (pauseToggleReduce != null) pauseToggleReduce.RegisterValueChangedCallback(evt =>
                {
                    SimulationSettings.ReduceMotion = evt.newValue;
                    SimulationAudio.PlayMenuHover();
                });
            }

            if (chamberSubtitleLabel != null)
            {
                string tag = SimulationProgress.IsSpecialShowcase(SceneManager.GetActiveScene().name)
                    ? "Ruang Showcase"
                    : "Ruangan " + levelNumberLabel;
                chamberSubtitleLabel.text = tag + "  •  " + levelDisplayName;
            }
            if (btnNext != null && !string.IsNullOrEmpty(nextSceneName))
                btnNext.text = "Ruangan " + nextSceneName.Replace("Level", "") + "   [ ENTER ]";

            targetCard = root.Q<VisualElement>("target-card");
            targetAccentBar = root.Q<VisualElement>("target-accent-bar");
            targetSwatch = root.Q<VisualElement>("target-swatch");
            targetStateLabel = root.Q<Label>("target-state-label");
            targetNameLabel = root.Q<Label>("target-name-label");
            targetMetaLabel = root.Q<Label>("target-meta-label");
            targetVectorLabel = root.Q<Label>("target-vector-label");

            statusToast = root.Q<VisualElement>("status-toast");
            statusToastDot = root.Q<VisualElement>("status-toast-dot");
            statusToastLabel = root.Q<Label>("status-toast-label");

            controlsCard = root.Q<VisualElement>("controls-card");
            if (controlsCard != null)
            {
                controlsCard.EnableInClassList("controls-card--collapsed", !controlsExpanded);
                EnsurePartitionFusionControlHints();
            }
            contextPrompt = root.Q<VisualElement>("context-prompt");
            contextPromptAccent = root.Q<VisualElement>("context-prompt-accent");
            contextPromptLabel = root.Q<Label>("context-prompt-label");

            slotRotasi = root.Q<VisualElement>("slot-rotasi");
            slotSpecial = root.Q<VisualElement>("slot-special");
            slotPartition = root.Q<VisualElement>("slot-partition");
            slotFusion = root.Q<VisualElement>("slot-fusion");
            iconTranslasi = root.Q<HudVectorIcon>("icon-translasi");
            iconElevasi = root.Q<HudVectorIcon>("icon-elevasi");
            iconRotasi = root.Q<HudVectorIcon>("icon-rotasi");
            iconSpecial = root.Q<HudVectorIcon>("icon-special");
            iconPartition = root.Q<HudVectorIcon>("icon-partition");
            iconFusion = root.Q<HudVectorIcon>("icon-fusion");
            keyRotasi = root.Q<Label>("key-rotasi");
            titleSpecial = root.Q<Label>("title-special");
            keySpecial = root.Q<Label>("key-special");
            keyPartitionLabel = root.Q<Label>("key-partition");
            keyFusionLabel = root.Q<Label>("key-fusion");

            if (iconPartition != null) iconPartition.SetTint(PaletteSettings.Peach);
            if (iconFusion != null) iconFusion.SetTint(PaletteSettings.Green);
        }

        private void SubscribeEvents()
        {
            if (modeController != null)
            {
                modeController.OnModeChanged += HandleModeChanged;
                modeController.OnStatusBanner += HandleStatusBanner;
            }
            if (carryController != null)
            {
                carryController.OnPickedUp += HandlePickedUp;
                carryController.OnDropped += HandleDropped;
                carryController.OnThrown += HandleThrown;
                carryController.OnRotated += HandleRotated;
            }
            if (telekinesisController != null)
            {
                telekinesisController.OnTetherStarted += HandleTetherStarted;
                telekinesisController.OnTetherReleased += HandleTargetAction;
            }
            if (allPlates != null && allPlates.Length > 0)
            {
                for (int i = 0; i < allPlates.Length; i++)
                {
                    if (allPlates[i] != null) allPlates[i].OnActivatedChanged += HandlePlateChanged;
                }
            }
            else if (pressurePlate != null)
            {
                pressurePlate.OnActivatedChanged += HandlePlateChanged;
            }
            if (anchorObjective != null)
            {
                anchorObjective.OnObjectiveCompleted += HandleObjectiveCompleted;
                anchorObjective.OnObjectiveFeedback += HandleStatusBanner;
            }
            if (blueprintVisualizer != null)
                blueprintVisualizer.OnBlueprintToggled += HandleBlueprintToggled;
            if (partitionFusionController != null)
            {
                partitionFusionController.OnPartitionPerformed += HandlePartitionPerformed;
                partitionFusionController.OnFusionPerformed += HandleFusionPerformed;
                partitionFusionController.OnCutAxisModeChanged += HandleCutAxisModeChanged;
            }
            if (endingController != null)
            {
                endingController.OnEndingChosen += HandleEndingChosen;
                endingController.OnChoiceFeedback += HandleChoiceFeedback;
            }
            MemoryFragment.OnCollected += HandleFragmentCollected;
        }

        private void UnsubscribeEvents()
        {
            if (modeController != null)
            {
                modeController.OnModeChanged -= HandleModeChanged;
                modeController.OnStatusBanner -= HandleStatusBanner;
            }
            if (carryController != null)
            {
                carryController.OnPickedUp -= HandlePickedUp;
                carryController.OnDropped -= HandleDropped;
                carryController.OnThrown -= HandleThrown;
                carryController.OnRotated -= HandleRotated;
            }
            if (telekinesisController != null)
            {
                telekinesisController.OnTetherStarted -= HandleTetherStarted;
                telekinesisController.OnTetherReleased -= HandleTargetAction;
            }
            if (allPlates != null && allPlates.Length > 0)
            {
                for (int i = 0; i < allPlates.Length; i++)
                {
                    if (allPlates[i] != null) allPlates[i].OnActivatedChanged -= HandlePlateChanged;
                }
            }
            else if (pressurePlate != null)
            {
                pressurePlate.OnActivatedChanged -= HandlePlateChanged;
            }
            if (anchorObjective != null)
            {
                anchorObjective.OnObjectiveCompleted -= HandleObjectiveCompleted;
                anchorObjective.OnObjectiveFeedback -= HandleStatusBanner;
            }
            if (blueprintVisualizer != null)
                blueprintVisualizer.OnBlueprintToggled -= HandleBlueprintToggled;
            if (partitionFusionController != null)
            {
                partitionFusionController.OnPartitionPerformed -= HandlePartitionPerformed;
                partitionFusionController.OnFusionPerformed -= HandleFusionPerformed;
                partitionFusionController.OnCutAxisModeChanged -= HandleCutAxisModeChanged;
            }
            if (endingController != null)
            {
                endingController.OnEndingChosen -= HandleEndingChosen;
                endingController.OnChoiceFeedback -= HandleChoiceFeedback;
            }
            MemoryFragment.OnCollected -= HandleFragmentCollected;
            if (storyFeed != null) storyFeed.UnregisterCallback<PointerDownEvent>(OnStoryCardClicked);
            if (btnNext != null) btnNext.clicked -= LoadNextChamber;
            if (btnResume != null) btnResume.clicked -= TogglePauseMenu;
            if (btnPauseSettings != null) btnPauseSettings.clicked -= TogglePauseSettingsPanel;
            if (btnReplay != null) btnReplay.clicked -= ReplayCurrentChamber;
            if (btnMainMenu != null) btnMainMenu.clicked -= ReturnToMainMenu;
        }

        private void HandlePickedUp(InteractableObject obj) => RefreshTargetAndPrompt(true);
        private void HandleDropped(InteractableObject obj) { translationCount++; RefreshTargetAndPrompt(true); }
        private void HandleThrown(InteractableObject obj) { translationCount++; throwOrTetherCount++; RefreshTargetAndPrompt(true); }
        private void HandleTetherStarted(InteractableObject obj) { throwOrTetherCount++; RefreshTargetAndPrompt(true); }
        private void HandleBlueprintToggled(bool active)
        {
            if (!hasShownBlueprintConcept)
            {
                hasShownBlueprintConcept = true;
                HandleStatusBanner("Mode Cetak Biru [V]: Arah Sumbu & Garis Target Aktif", PaletteSettings.Teal);
            }
            else
            {
                HandleStatusBanner(active ? "BLUEPRINT LENS: ON" : "BLUEPRINT LENS: OFF", PaletteSettings.Teal);
            }
        }

        private void HandleModeChanged(SimulationMode mode)
        {
            RefreshModeVisuals();
            RefreshTargetAndPrompt(true);
            if (mode == SimulationMode.Flexible && !hasShownFlexNarrative)
            {
                hasShownFlexNarrative = true;
                HandleStatusBanner("Mode Fleksibel aktif — Gerak Bebas & Telekinesis [F]", PaletteSettings.Mauve);
            }
        }

        private void HandlePlateChanged(bool active)
        {
            RefreshObjectiveText();
            if (chamberCompleted) return;
            HandleStatusBanner(
                active ? "CONDUIT ENGAGED — ENERGY BRIDGE ONLINE" : "CONDUIT WEIGHT DROPPED — BRIDGE OFFLINE",
                active ? PaletteSettings.Green : PaletteSettings.Peach);
        }

        private void HandleFragmentCollected(string fragTitle, string speaker, string line, string replySpeaker, string replyLine)
        {
            fragmentCollectedThisRun = true;
            HandleStatusBanner("Memori Tersimpan: " + fragTitle, PaletteSettings.Rosewater);
            storyQueue.Clear();
            storyBatchTotal = 0;
            storyBatchIndex = 0;
            var lines = new List<LevelInfo.StoryLine>(2);
            if (!string.IsNullOrEmpty(line)) lines.Add(new LevelInfo.StoryLine { speaker = speaker, text = line });
            if (!string.IsNullOrEmpty(replyLine)) lines.Add(new LevelInfo.StoryLine { speaker = replySpeaker, text = replyLine });
            EnqueueStoryLines(lines.ToArray());
        }

        private void EnqueueStoryLines(LevelInfo.StoryLine[] lines)
        {
            int added = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i].text))
                {
                    storyQueue.Enqueue(lines[i]);
                    added++;
                }
            }
            if (added <= 0) return;

            if (!storyShowing)
            {
                storyBatchTotal = storyQueue.Count;
                storyBatchIndex = 0;
                AdvanceStoryQueue();
            }
            else
            {
                storyBatchTotal += added;
                UpdateStoryProgressLabel();
            }
        }

        private void OnStoryCardClicked(PointerDownEvent evt)
        {
            if (storyShowing && evt.button == 0)
            {
                SkipOrAdvanceStory();
                evt.StopPropagation();
            }
        }

        private void SkipOrAdvanceStory()
        {
            if (!storyShowing) return;
            if (storyTyping)
            {
                currentTypedChars = currentFullLine.Length;
                storyTyping = false;
                storyHoldTimer = Mathf.Clamp(2.8f + currentFullLine.Length * 0.042f, 3.2f, 6.2f);
                UpdateStoryProgressLabel();
                RenderStoryTextWithCaret();
                return;
            }
            AdvanceStoryQueue();
        }

        private void AdvanceStoryQueue()
        {
            if (storyFeed == null || storySpeakerLabel == null || storyTextLabel == null) return;
            if (storyQueue.Count == 0)
            {
                storyShowing = false;
                storyTyping = false;
                storyHoldTimer = 0f;
                storyFeed.AddToClassList("story-feed--hidden");
                return;
            }

            LevelInfo.StoryLine next = storyQueue.Dequeue();
            storyBatchIndex++;
            currentSpeakerRaw = string.IsNullOrEmpty(next.speaker) ? "SIM" : next.speaker.ToUpperInvariant();
            currentFullLine = next.text ?? string.Empty;
            currentTypedChars = 0;
            storyShowing = true;
            storyTyping = true;
            nextTypeCharAt = Time.unscaledTime;
            storyHoldTimer = 0f;

            storySpeakerLabel.text = FormatSpeakerTag(currentSpeakerRaw);
            Color spColor = SpeakerColor(currentSpeakerRaw);
            storySpeakerLabel.style.color = spColor;
            storyFeed.style.borderLeftColor = spColor;
            storyFeed.RemoveFromClassList("story-feed--hidden");
            UpdateStoryProgressLabel();
            RenderStoryTextWithCaret();
            SimulationAudio.PlayDialogBlip(currentSpeakerRaw != "SIM");
        }

        private void UpdateStoryTypewriter()
        {
            if (!storyShowing || storyTextLabel == null) return;

            cursorBlinkTimer += Time.unscaledDeltaTime * 3.4f;

            if (storyTyping)
            {
                while (storyTyping && Time.unscaledTime >= nextTypeCharAt)
                {
                    if (currentTypedChars >= currentFullLine.Length)
                    {
                        storyTyping = false;
                        storyHoldTimer = Mathf.Clamp(2.8f + currentFullLine.Length * 0.042f, 3.2f, 6.2f);
                        UpdateStoryProgressLabel();
                        break;
                    }

                    char ch = currentFullLine[currentTypedChars++];
                    if (ch != ' ' && currentTypedChars % 3 == 1)
                        SimulationAudio.PlayTypewriterTick(currentSpeakerRaw);

                    float step = 0.022f;
                    if (ch == '.' || ch == '!' || ch == '?') step = 0.17f;
                    else if (ch == ',' || ch == ':' || ch == '—' || ch == ';') step = 0.085f;
                    nextTypeCharAt = Time.unscaledTime + step;
                }
                RenderStoryTextWithCaret();
            }
            else
            {
                RenderStoryTextWithCaret();
                if (storyHoldTimer > 0f)
                {
                    storyHoldTimer -= Time.unscaledDeltaTime;
                    if (storyHoldTimer <= 0f) AdvanceStoryQueue();
                }
            }
        }

        private void RenderStoryTextWithCaret()
        {
            if (storyTextLabel == null) return;
            string visible = currentTypedChars >= currentFullLine.Length
                ? currentFullLine
                : currentFullLine.Substring(0, currentTypedChars);
            storyTextLabel.text = visible;
        }

        private void UpdateStoryProgressLabel()
        {
            if (storyProgressLabel == null) return;
            string action = storyTyping ? "[SPACE] Lewati" : (storyQueue.Count > 0 ? "[SPACE] Lanjut" : "[SPACE] Tutup");
            storyProgressLabel.text = string.Format("{0}  •  {1}/{2}", action, Mathf.Max(1, storyBatchIndex), Mathf.Max(1, storyBatchTotal));
        }

        private static string FormatSpeakerTag(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return "SIM";
            if (speaker.Contains("VELA")) return "VELA  •  Suara Warga";
            if (speaker.Contains("MIRA")) return "MIRA  •  Arsiparis";
            if (speaker.Contains("DANU")) return "DANU  •  Catatan Lama";
            if (speaker.Contains("NARA")) return "NARA  •  Menara Rusun";
            if (speaker.Contains("HALIM")) return "HALIM  •  Rekaman Akhir";
            if (speaker.Contains("TEPI")) return "KOTA TEPI";
            if (speaker.Contains("TIM")) return "TIM 8  •  Demo Showcase";
            if (speaker.Contains("RECT")) return "RECT";
            if (speaker.Contains("SIGNAL")) return "SINYAL ANOMALI";
            return "SIM  •  Sistem Kota Tepi";
        }

        private static Color SpeakerColor(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return PaletteSettings.Sapphire;
            if (speaker.Contains("VELA")) return PaletteSettings.Teal;
            if (speaker.Contains("MIRA")) return PaletteSettings.Peach;
            if (speaker.Contains("NARA")) return PaletteSettings.Sky;
            if (speaker.Contains("HALIM")) return PaletteSettings.Rosewater;
            if (speaker.Contains("TEPI")) return PaletteSettings.Green;
            if (speaker.Contains("TIM")) return PaletteSettings.Teal;
            if (speaker.Contains("RECT")) return PaletteSettings.Lavender;
            if (speaker.Contains("DANU")) return PaletteSettings.Yellow;
            if (speaker.Contains("SIGNAL")) return PaletteSettings.Red;
            return PaletteSettings.Sapphire;
        }

        private void HandlePartitionPerformed(InteractableObject a, InteractableObject b) => RefreshTargetAndPrompt(true);
        private void HandleFusionPerformed(InteractableObject merged) => RefreshTargetAndPrompt(true);
        private void HandleCutAxisModeChanged(PartitionAxisMode mode)
        {
            RefreshPartitionFusionDock();
            RefreshTargetAndPrompt(true);
        }
        private void HandleChoiceFeedback(string msg) => HandleStatusBanner(msg, PaletteSettings.Peach);

        private void HandleEndingChosen(EndingOutcome outcome)
        {
            if (chamberCompleted) return;
            chamberCompleted = true;
            RefreshObjectiveText();
            ShowEndingOverlay(outcome);
        }

        private void HandleObjectiveCompleted()
        {
            if (endingController != null) return;
            chamberCompleted = true;
            RefreshObjectiveText();

            int totalActions = translationCount + rotationCount + throwOrTetherCount;
            int par = activeLevelInfo != null ? activeLevelInfo.ParActions : 8;
            lastClearStars = SimulationProgress.RecordClear(SceneManager.GetActiveScene().name, totalActions, par);

            HandleStatusBanner("Ruangan " + levelNumberLabel + " selesai — Anchor terkunci", PaletteSettings.Green);

            if (activeLevelInfo != null)
            {
                if (activeLevelInfo.TriggerGlitchOnClear)
                {
                    SimulationAudio.PlaySignalGlitch();
                    SimulationVisualBridge.TriggerFloorRipple(Vector3.zero, PaletteSettings.Red, 14f, 1.4f, 0.9f);
                }
                if (activeLevelInfo.PlayPianoOnClear)
                    SimulationAudio.TriggerAdiPianoMotif();
                if (activeLevelInfo.ClearDialogue != null && activeLevelInfo.ClearDialogue.Length > 0)
                    EnqueueStoryLines(activeLevelInfo.ClearDialogue);
            }

            clearOverlayDelay = activeLevelInfo != null && activeLevelInfo.ClearDialogue != null && activeLevelInfo.ClearDialogue.Length > 0
                ? 2.2f
                : 0.9f;
        }

        private void RefreshObjectiveText()
        {
            if (objectiveLabel == null) return;
            if (chamberCompleted)
            {
                objectiveLabel.text = endingController != null
                    ? "Keputusan Kota Tepi telah ditetapkan"
                    : "Selesai — CORE-01 terkunci di Anchor";
                objectiveLabel.style.color = PaletteSettings.Green;
                return;
            }

            if (endingController != null)
            {
                objectiveLabel.text = "Tujuan: Bawa CORE-01 ke Timur (Tanggul), Utara (Pemancar + Partition [1]), atau Barat (Brankas)";
                objectiveLabel.style.color = PaletteSettings.Teal;
                return;
            }

            if (allPlates != null && allPlates.Length > 1)
            {
                int activeCount = 0;
                for (int i = 0; i < allPlates.Length; i++)
                {
                    if (allPlates[i] != null && allPlates[i].IsActivated) activeCount++;
                }

                if (activeCount >= allPlates.Length)
                {
                    objectiveLabel.text = "Jalur aktif — Bawa CORE-01 ke Anchor";
                    objectiveLabel.style.color = PaletteSettings.Green;
                }
                else
                {
                    objectiveLabel.text = string.Format("Pelat beban {0}/{1} aktif — Buka akses Anchor", activeCount, allPlates.Length);
                    objectiveLabel.style.color = PaletteSettings.Teal;
                }
                return;
            }

            if (pressurePlate != null && pressurePlate.IsActivated)
            {
                objectiveLabel.text = "Jalur aktif — Bawa CORE-01 ke Anchor";
                objectiveLabel.style.color = PaletteSettings.Green;
            }
            else if (pressurePlate != null)
            {
                objectiveLabel.text = "Tujuan: Letakkan beban di pelat lantai -> Buka jalan ke Anchor";
                objectiveLabel.style.color = PaletteSettings.Teal;
            }
            else
            {
                objectiveLabel.text = "Tujuan: Bawa CORE-01 menuju soket Anchor";
                objectiveLabel.style.color = PaletteSettings.Teal;
            }
        }

        private void HandleStatusBanner(string message, Color color)
        {
            if (statusToast == null || statusToastLabel == null) return;

            statusToastLabel.text = message;
            statusToast.style.borderTopColor = color;
            statusToast.style.borderBottomColor = color;
            statusToast.style.borderLeftColor = color;
            statusToast.style.borderRightColor = color;
            if (statusToastDot != null) statusToastDot.style.backgroundColor = color;

            statusToast.RemoveFromClassList("status-toast--hidden");
            toastTimer = 2.2f;
        }

        private void HandleTargetAction(InteractableObject obj) => RefreshTargetAndPrompt(true);
        private void HandleRotated(InteractableObject obj, float yaw) { rotationCount++; RefreshTargetAndPrompt(true); }

        private void RefreshModeVisuals()
        {
            if (modeAccentBar == null) return;

            bool isFlexible = modeController != null && modeController.IsFlexible;
            Color accent = isFlexible ? PaletteSettings.ModeFlexible : PaletteSettings.ModeLimited;

            modeAccentBar.style.backgroundColor = accent;
            if (modeIcon != null)
            {
                modeIcon.IconKind = isFlexible ? HudIconKind.Flexible : HudIconKind.Limited;
                modeIcon.SetTint(accent);
            }
            if (modeBadgeLabel != null)
            {
                modeBadgeLabel.text = isFlexible ? "FLEKSIBEL" : "TERBATAS";
                modeBadgeLabel.style.color = accent;
            }
            if (modeSummaryLabel != null)
            {
                modeSummaryLabel.text = isFlexible
                    ? "Gerak Bebas  •  Telekinesis"
                    : "Langkah Grid  •  Putar 90°  •  Lempar";
            }

            if (iconTranslasi != null) iconTranslasi.SetTint(PaletteSettings.Sky);
            if (iconElevasi != null) iconElevasi.SetTint(PaletteSettings.Teal);
            if (iconRotasi != null) iconRotasi.SetTint(PaletteSettings.Yellow);
            if (keyRotasi != null) keyRotasi.text = isFlexible ? "[ R ] Bebas" : "[ R ] 90°";

            if (slotSpecial != null)
            {
                slotSpecial.EnableInClassList("ability-slot--highlight", !isFlexible);
                slotSpecial.EnableInClassList("ability-slot--flexible", isFlexible);
            }
            if (iconSpecial != null)
            {
                iconSpecial.IconKind = isFlexible ? HudIconKind.Telekinesis : HudIconKind.Throw;
                iconSpecial.SetTint(accent);
            }
            if (titleSpecial != null) titleSpecial.text = isFlexible ? "TELEKINESIS" : "LEMPAR";
            if (keySpecial != null) keySpecial.text = isFlexible ? "[ E / F ]" : "[ Klik ]";

            RefreshPartitionFusionDock();
        }

        private void EnsurePartitionFusionControlHints()
        {
            if (controlsCard == null || controlsCard.Q<VisualElement>("ctrl-row-partition") != null) return;
            VisualElement content = controlsCard.Q<VisualElement>(className: "controls-content");
            if (content == null) return;

            Label header = content.Q<Label>(className: "controls-header");
            if (header != null) header.text = "[ H ]  Kontrol";

            void AddRow(string rowName, string key, string desc, bool isLast)
            {
                VisualElement row = new VisualElement { name = rowName, pickingMode = PickingMode.Ignore };
                row.AddToClassList("control-line");
                if (isLast) row.AddToClassList("control-line--last");

                Label k = new Label(key) { pickingMode = PickingMode.Ignore };
                k.AddToClassList("keycap");
                Label d = new Label(desc) { pickingMode = PickingMode.Ignore };
                d.AddToClassList("control-desc");
                row.Add(k);
                row.Add(d);
                content.Add(row);
            }

            AddRow("ctrl-row-partition", "[ 1 ]", "Partition (Belah Beban)", false);
            AddRow("ctrl-row-cut-axis", "[ 3 ]", "Arah Potong Partition", false);
            AddRow("ctrl-row-fusion", "[ 2 ]", "Fusion (Gabung Beban)", false);
            AddRow("ctrl-row-hint", "[ F1 ]", "Petunjuk Ruangan", true);
        }

        private void RefreshPartitionFusionDock()
        {
            bool canPart = modeController != null && modeController.CanUsePartition;
            bool canFuse = modeController != null && modeController.CanUseFusion;
            bool pairReady = canFuse && partitionFusionController != null && partitionFusionController.HasFusionPair;

            if (slotPartition != null)
            {
                slotPartition.EnableInClassList("ability-slot--queued", !canPart);
                slotPartition.EnableInClassList("ability-slot--active", canPart);
                slotPartition.EnableInClassList("ability-slot--partition-ready", canPart);
            }
            if (iconPartition != null)
                iconPartition.SetTint(canPart ? PaletteSettings.Peach : PaletteSettings.Overlay0);
            if (keyPartitionLabel != null)
            {
                string axisBadge = partitionFusionController != null
                    ? partitionFusionController.CutAxisMode switch
                    {
                        PartitionAxisMode.LeftRight => "Sumbu X",
                        PartitionAxisMode.FrontBack => "Sumbu Z",
                        PartitionAxisMode.Horizontal => "Sumbu Y",
                        _ => "Auto [3]"
                    }
                    : "Auto [3]";
                keyPartitionLabel.text = canPart ? "[ 1 ] " + axisBadge : "[ 1 ] Terkunci";
            }

            if (slotFusion != null)
            {
                slotFusion.EnableInClassList("ability-slot--queued", !canFuse);
                slotFusion.EnableInClassList("ability-slot--active", canFuse && !pairReady);
                slotFusion.EnableInClassList("ability-slot--fusion-ready", pairReady);
            }
            if (iconFusion != null)
                iconFusion.SetTint(canFuse ? (pairReady ? PaletteSettings.Green : PaletteSettings.Teal) : PaletteSettings.Overlay0);
            if (keyFusionLabel != null)
                keyFusionLabel.text = canFuse ? (pairReady ? "[ 2 ] Gabung!" : "[ 2 ] Gabung") : "[ 2 ] Terkunci";
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame && !chamberCompleted)
                    TogglePauseMenu();

                if (keyboard.hKey.wasPressedThisFrame && controlsCard != null)
                {
                    controlsExpanded = !controlsExpanded;
                    controlsCard.EnableInClassList("controls-card--collapsed", !controlsExpanded);
                }

                if (keyboard.f1Key.wasPressedThisFrame && !isPaused && !chamberCompleted)
                    ShowContextualSimHint();

                if (keyboard.spaceKey.wasPressedThisFrame && storyShowing && !isPaused && !chamberCompleted)
                    SkipOrAdvanceStory();

                if (isPaused || chamberCompleted)
                {
                    if (chamberCompleted && !string.IsNullOrEmpty(nextSceneName) &&
                        (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                        LoadNextChamber();
                    else if (keyboard.rKey.wasPressedThisFrame) ReplayCurrentChamber();
                    else if (keyboard.mKey.wasPressedThisFrame) ReturnToMainMenu();
                }
            }

            UpdateStoryTypewriter();
            RefreshPartitionFusionDock();

            if (clearOverlayDelay > 0f)
            {
                clearOverlayDelay -= Time.unscaledDeltaTime;
                if (clearOverlayDelay <= 0f)
                {
                    clearOverlayDelay = -1f;
                    ShowChamberOverlay(true);
                }
            }

            if (toastTimer > 0f && statusToast != null)
            {
                toastTimer -= Time.unscaledDeltaTime;
                if (toastTimer <= 0f)
                {
                    toastTimer = 0f;
                    statusToast.AddToClassList("status-toast--hidden");
                }
            }

            RefreshTargetAndPrompt(false);
        }

        private void TogglePauseMenu()
        {
            isPaused = !isPaused;
            pauseSettingsOpen = false;
            if (pauseSettingsPanel != null) pauseSettingsPanel.AddToClassList("pause-settings-panel--hidden");
            Time.timeScale = isPaused ? 0f : 1f;
            SimulationAudio.PlayMenuClick();
            if (isPaused)
            {
                SyncPauseSettingsToUI();
                ShowChamberOverlay(false);
            }
            else if (chamberOverlay != null)
            {
                chamberOverlay.AddToClassList("chamber-overlay--hidden");
            }
        }

        private void TogglePauseSettingsPanel()
        {
            if (pauseSettingsPanel == null || chamberCompleted) return;
            pauseSettingsOpen = !pauseSettingsOpen;
            if (pauseSettingsOpen) SyncPauseSettingsToUI();
            pauseSettingsPanel.EnableInClassList("pause-settings-panel--hidden", !pauseSettingsOpen);
            if (btnPauseSettings != null) btnPauseSettings.text = pauseSettingsOpen ? "HIDE SETTINGS" : "SETTINGS";
            SimulationAudio.PlayMenuClick();
        }

        private void SyncPauseSettingsToUI()
        {
            float vol = SimulationSettings.MasterVolume * 100f;
            if (pauseSliderVolume != null) pauseSliderVolume.SetValueWithoutNotify(vol);
            if (pauseValVolume != null) pauseValVolume.text = Mathf.RoundToInt(vol) + "%";

            float sens = SimulationSettings.CameraSensitivity * 100f;
            if (pauseSliderSensitivity != null) pauseSliderSensitivity.SetValueWithoutNotify(sens);
            if (pauseValSensitivity != null) pauseValSensitivity.text = SimulationSettings.CameraSensitivity.ToString("0.0") + "x";

            if (pauseToggleReduce != null) pauseToggleReduce.SetValueWithoutNotify(SimulationSettings.ReduceMotion);
            if (btnPauseSettings != null) btnPauseSettings.text = pauseSettingsOpen ? "HIDE SETTINGS" : "SETTINGS";
        }

        private void ShowContextualSimHint()
        {
            SimulationAudio.PlayMenuHover();
            if (chamberCompleted)
            {
                HandleStatusBanner("Ruangan selesai — lanjut ke ruangan berikutnya", PaletteSettings.Green);
                return;
            }

            if (activeLevelInfo != null && activeLevelInfo.Hints != null && activeLevelInfo.Hints.Length > 0)
            {
                string customHint = activeLevelInfo.Hints[hintStep % activeLevelInfo.Hints.Length];
                hintStep++;
                HandleStatusBanner("Petunjuk: " + customHint, PaletteSettings.Sky);
                return;
            }

            if (pressurePlate != null && !pressurePlate.IsActivated)
            {
                hintStep = (hintStep + 1) % 2;
                string msg = hintStep == 0
                    ? "Petunjuk: Letakkan balok pemberat di atas pelat lantai hingga berubah hijau"
                    : "Petunjuk: Gunakan [E] untuk mengangkat, [TAB] untuk Mode Fleksibel, atau [1]/[3]/[2] bila aktif";
                HandleStatusBanner(msg, PaletteSettings.Peach);
                return;
            }

            hintStep = (hintStep + 1) % 2;
            string bridgeMsg = hintStep == 0
                ? "Petunjuk: Bawa kubus kuning CORE-01 ke arah soket Anchor"
                : "Petunjuk: Jika soket memiliki tanda panah, tekan [R] untuk memutar CORE-01";
            HandleStatusBanner(bridgeMsg, PaletteSettings.Teal);
        }

        private void ShowChamberOverlay(bool completed)
        {
            if (chamberOverlay == null) return;
            if (modalCard != null) modalCard.RemoveFromClassList("modal-card--ending");
            if (endingEpilogueBox != null) endingEpilogueBox.AddToClassList("ending-epilogue-box--hidden");

            if (modalAccentBar != null)
                modalAccentBar.style.backgroundColor = completed ? PaletteSettings.Green : PaletteSettings.Sapphire;
            if (modalTitle != null)
                modalTitle.text = completed ? "Ruangan " + levelNumberLabel + " Selesai" : "Simulasi Dijeda";
            if (modalSubtitle != null)
                modalSubtitle.text = completed
                    ? levelDisplayName + "  •  Anchor Terkunci"
                    : "Ruangan " + levelNumberLabel + "  •  " + levelDisplayName;
            if (modalRankLabel != null)
            {
                modalRankLabel.EnableInClassList("modal-rank--hidden", !completed);
                if (completed)
                {
                    string stars = new string('★', lastClearStars) + new string('☆', 3 - lastClearStars);
                    string fragTag = fragmentCollectedThisRun || SimulationProgress.HasFragment(SceneManager.GetActiveScene().name)
                        ? "   •   Memori Tersimpan"
                        : string.Empty;
                    modalRankLabel.text = stars + fragTag;
                }
            }
            if (modalMetrics != null)
                modalMetrics.EnableInClassList("modal-metrics--hidden", !completed);
            if (completed)
            {
                if (metricTranslations != null) metricTranslations.text = translationCount.ToString();
                if (metricRotations != null) metricRotations.text = rotationCount.ToString();
                if (metricTethers != null) metricTethers.text = throwOrTetherCount.ToString();
            }
            if (btnNext != null)
            {
                btnNext.text = "Ruangan Berikutnya   [ ENTER ]";
                btnNext.EnableInClassList("modal-btn--hidden", !completed || string.IsNullOrEmpty(nextSceneName));
            }
            if (btnResume != null)
            {
                btnResume.text = "Lanjutkan   [ ESC ]";
                btnResume.EnableInClassList("modal-btn--hidden", completed);
            }
            if (btnPauseSettings != null)
            {
                btnPauseSettings.text = "Pengaturan";
                btnPauseSettings.EnableInClassList("modal-btn--hidden", completed);
            }
            if (btnReplay != null) btnReplay.text = "Ulangi Ruangan   [ R ]";
            if (btnMainMenu != null) btnMainMenu.text = "Menu Utama   [ M ]";
            if (pauseSettingsPanel != null)
                pauseSettingsPanel.AddToClassList("pause-settings-panel--hidden");

            if (modalCard != null)
            {
                modalCard.AddToClassList("modal-card--entering");
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
                modalCard.schedule.Execute(() => modalCard.RemoveFromClassList("modal-card--entering")).ExecuteLater(16);
            }
            else
            {
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
            }

            if (completed && btnNext != null && !string.IsNullOrEmpty(nextSceneName)) btnNext.Focus();
            else if (!completed && btnResume != null) btnResume.Focus();
            else if (btnReplay != null) btnReplay.Focus();
        }

        private void ShowEndingOverlay(EndingOutcome outcome)
        {
            if (chamberOverlay == null) return;
            storyQueue.Clear();
            storyShowing = false;
            storyTyping = false;
            if (storyFeed != null) storyFeed.AddToClassList("story-feed--hidden");
            Color accent = outcome.path switch
            {
                EndingPath.Archivist => PaletteSettings.Teal,
                EndingPath.Restoration => PaletteSettings.Sapphire,
                _ => PaletteSettings.Lavender
            };

            if (modalCard != null) modalCard.AddToClassList("modal-card--ending");
            if (modalAccentBar != null) modalAccentBar.style.backgroundColor = accent;
            if (modalTitle != null) modalTitle.text = outcome.title;
            if (modalSubtitle != null) modalSubtitle.text = outcome.badge;
            if (modalRankLabel != null) modalRankLabel.AddToClassList("modal-rank--hidden");
            if (modalMetrics != null) modalMetrics.AddToClassList("modal-metrics--hidden");
            if (pauseSettingsPanel != null) pauseSettingsPanel.AddToClassList("pause-settings-panel--hidden");

            if (endingEpilogueBox != null) endingEpilogueBox.RemoveFromClassList("ending-epilogue-box--hidden");
            if (endingSummaryLabel != null) endingSummaryLabel.text = outcome.summary;
            if (endingPara1 != null)
                endingPara1.text = outcome.epilogueLines != null && outcome.epilogueLines.Length > 0 ? outcome.epilogueLines[0] : "";
            if (endingPara2 != null)
                endingPara2.text = outcome.epilogueLines != null && outcome.epilogueLines.Length > 1 ? outcome.epilogueLines[1] : "";
            if (endingPara3 != null)
                endingPara3.text = outcome.epilogueLines != null && outcome.epilogueLines.Length > 2 ? outcome.epilogueLines[2] : "";
            if (endingMemoriesLabel != null)
                endingMemoriesLabel.text = string.Format("Total Memori Terkumpul: {0} Fragmen Kota Tepi", outcome.fragmentCount);

            if (btnNext != null) btnNext.AddToClassList("modal-btn--hidden");
            if (btnResume != null) btnResume.AddToClassList("modal-btn--hidden");
            if (btnPauseSettings != null) btnPauseSettingsPanelButtonState(false);
            if (btnReplay != null) btnReplay.text = "Ulangi Pilihan Akhir   [ R ]";
            if (btnMainMenu != null) btnMainMenu.text = "Kembali ke Menu Utama   [ M ]";

            if (modalCard != null)
            {
                modalCard.AddToClassList("modal-card--entering");
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
                modalCard.schedule.Execute(() => modalCard.RemoveFromClassList("modal-card--entering")).ExecuteLater(16);
            }
            else
            {
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
            }

            if (btnMainMenu != null) btnMainMenu.Focus();
        }

        private void btnPauseSettingsPanelButtonState(bool visible)
        {
            if (btnPauseSettings != null) btnPauseSettings.EnableInClassList("modal-btn--hidden", !visible);
        }

        public void LoadNextChamber()
        {
            if (string.IsNullOrEmpty(nextSceneName)) return;
            SimulationAudio.PlayMenuClick();
            Time.timeScale = 1f;
            SceneTransition.Begin(nextSceneName, 0.05f, 0.40f);
        }

        public void ReplayCurrentChamber()
        {
            SimulationAudio.PlayMenuClick();
            SimulationProgress.IncrementIterationOnRetry();
            Time.timeScale = 1f;
            SceneTransition.Begin(SceneManager.GetActiveScene().name, 0.05f, 0.35f);
        }

        public void ReturnToMainMenu()
        {
            SimulationAudio.PlayMenuClick();
            Time.timeScale = 1f;
            SceneTransition.Begin("MainMenu", 0.05f, 0.40f);
        }

        private void RefreshTargetAndPrompt(bool force)
        {
            if (targetNameLabel == null || contextPromptLabel == null) return;

            if (isPaused || chamberCompleted)
            {
                if (targetCard != null) targetCard.AddToClassList("target-card--hidden");
                contextPrompt.AddToClassList("context-prompt--hidden");
                return;
            }

            bool isFlexible = modeController != null && modeController.IsFlexible;
            Color modeColor = isFlexible ? PaletteSettings.ModeFlexible : PaletteSettings.ModeLimited;

            bool showTargetCard = false;
            string stateText = "TARGET";
            string objName = string.Empty;
            string metaText = string.Empty;
            string vectorText = string.Empty;
            Color swatchColor = PaletteSettings.Surface2;
            Color barColor = PaletteSettings.Overlay0;

            string prompt = string.Empty;
            Color promptColor = modeColor;

            bool canSplit = modeController != null && modeController.CanUsePartition;
            bool canFusePair = modeController != null && modeController.CanUseFusion && partitionFusionController != null && partitionFusionController.HasFusionPair;
            bool hasCutPreview = canSplit && partitionFusionController != null && partitionFusionController.HasCutPreview;

            string splitAxisTag = partitionFusionController != null ? partitionFusionController.CutAxisShortLabel : "OTOMATIS";
            string cutRuleInfo = hasCutPreview
                ? "Potong: " + partitionFusionController.CutAxisRuleLabel + " -> 2x " +
                  partitionFusionController.PreviewSplitHalfMass.ToString("0.0", CultureInfo.InvariantCulture) + " t"
                : string.Empty;

            string fusePartnerInfo = string.Empty;
            string fusePromptInfo = string.Empty;
            if (canFusePair)
            {
                InteractableObject srcObj = partitionFusionController.PreviewSource;
                InteractableObject partnerObj = partitionFusionController.PreviewPartner;
                float mA = srcObj != null && srcObj.Body != null ? srcObj.Body.mass : 1f;
                float mB = partnerObj != null && partnerObj.Body != null ? partnerObj.Body.mass : 1f;
                float sumMass = mA + mB;
                string partnerName = partnerObj != null ? partnerObj.displayName : "Partner";
                fusePartnerInfo = "\nSiap Fusion: + " + partnerName + " (" + sumMass.ToString("0.0", CultureInfo.InvariantCulture) + " t)";
                fusePromptInfo = "   [ 2 ] Gabung (" + partnerName + ")";
            }

            string cutPromptTag = hasCutPreview ? "   [ 1 ] Belah (" + splitAxisTag + ")   [ 3 ] Arah" : string.Empty;

            if (carryController != null && carryController.IsCarrying)
            {
                showTargetCard = true;
                InteractableObject held = carryController.CarriedObject;
                stateText = "DIBAWA";
                objName = carryController.CarriedObjectName;
                metaText = "Hadap " + FormatCompassHeading(carryController.CurrentHeldYawDegrees) +
                           "  •  Jarak Taruh " + carryController.CurrentDropDistanceGrid.ToString("0.0", CultureInfo.InvariantCulture) + " m" +
                           fusePartnerInfo;
                string throwCue = carryController.TryGetPredictedThrowLanding(out _, out float rangeGu)
                    ? "Estimasi Lempar ~" + rangeGu.ToString("0.0", CultureInfo.InvariantCulture) + " m"
                    : "Siap ditaruh di pelat / soket";
                vectorText = string.IsNullOrEmpty(cutRuleInfo) ? throwCue : throwCue + "\n" + cutRuleInfo;
                swatchColor = held != null ? held.AssignedColor : PaletteSettings.Green;
                barColor = canFusePair ? PaletteSettings.Green : PaletteSettings.Sapphire;

                prompt = "[ E ] Letakkan   [ R ] Putar 90°   [ Klik ] Lempar" + cutPromptTag + fusePromptInfo;
                promptColor = canFusePair ? PaletteSettings.Green : (hasCutPreview ? PaletteSettings.Peach : PaletteSettings.Sapphire);
            }
            else if (telekinesisController != null && telekinesisController.HasTether)
            {
                showTargetCard = true;
                InteractableObject tethered = telekinesisController.TetheredObject;
                stateText = "TELEKINESIS";
                objName = tethered != null ? tethered.displayName : "OBJEK";
                metaText = "Elevasi " + telekinesisController.CurrentElevationGrid.ToString("0.0", CultureInfo.InvariantCulture) + " m" +
                           fusePartnerInfo;
                string massLine = tethered != null
                    ? "Bobot " + (tethered.Body != null ? tethered.Body.mass.ToString("0.0", CultureInfo.InvariantCulture) : "1.0") + " t  •  [Klik] Dorong"
                    : "[Q/Z] Atur Elevasi";
                vectorText = string.IsNullOrEmpty(cutRuleInfo) ? massLine : massLine + "\n" + cutRuleInfo;
                swatchColor = tethered != null ? tethered.AssignedColor : PaletteSettings.BeamTelekinesis;
                barColor = PaletteSettings.BeamTelekinesis;

                prompt = "[ E / F ] Lepas   [ Q / Z ] Naik-Turun   [ R ] Putar" + cutPromptTag + fusePromptInfo;
                promptColor = PaletteSettings.BeamTelekinesis;
            }
            else if (interactor != null && interactor.HasTarget)
            {
                showTargetCard = true;
                InteractableObject target = interactor.CurrentTarget;
                objName = interactor.TargetName;
                swatchColor = target != null ? target.AssignedColor : modeColor;

                if (interactor.TargetCanBePickedUp)
                {
                    float mass = target != null && target.Body != null ? target.Body.mass : 1f;
                    stateText = isFlexible ? "JANGKAUAN SINAR" : "SIAP ANGKAT";
                    metaText = (isFlexible ? "[E] Angkat atau [F] Telekinesis" : "[E] Angkat objek") +
                               "  •  Bobot " + mass.ToString("0.0", CultureInfo.InvariantCulture) + " t" + fusePartnerInfo;
                    vectorText = !string.IsNullOrEmpty(cutRuleInfo)
                        ? cutRuleInfo
                        : (canFusePair ? "Tekan [ 2 ] untuk menggabungkan objek" : "Arahkan ke pelat beban atau dek Anchor");
                    barColor = canFusePair ? PaletteSettings.Green : (hasCutPreview ? PaletteSettings.Peach : modeColor);
                    prompt = (isFlexible ? "[ E ] Angkat  •  [ F ] Telekinesis  " : "[ E ] Angkat  ") + objName + cutPromptTag + fusePromptInfo;
                    promptColor = canFusePair ? PaletteSettings.Green : (hasCutPreview ? PaletteSettings.Peach : modeColor);
                }
                else
                {
                    stateText = "SOKET ANCHOR";
                    metaText = "Dudukan Kunci Energi";
                    vectorText = anchorObjective != null && anchorObjective.requireFacingAngle
                        ? "Wajib hadap " + FormatCompassHeading(anchorObjective.requiredFacingYaw)
                        : "Letakkan CORE-01 ke dudukan";
                    barColor = PaletteSettings.Peach;
                    prompt = objName + "  •  Letakkan CORE-01 di sini";
                    promptColor = PaletteSettings.Peach;
                }
            }

            if (targetCard != null)
                targetCard.EnableInClassList("target-card--hidden", !showTargetCard);

            targetStateLabel.text = stateText;
            targetNameLabel.text = objName;
            targetMetaLabel.text = metaText;
            if (targetVectorLabel != null) targetVectorLabel.text = vectorText;
            if (targetSwatch != null) targetSwatch.style.backgroundColor = swatchColor;
            if (targetAccentBar != null) targetAccentBar.style.backgroundColor = barColor;

            if (force || prompt != lastPromptText || promptColor != lastPromptColor)
            {
                lastPromptText = prompt;
                lastPromptColor = promptColor;
                if (string.IsNullOrEmpty(prompt))
                {
                    contextPrompt.AddToClassList("context-prompt--hidden");
                }
                else
                {
                    contextPromptLabel.text = prompt;
                    if (contextPromptAccent != null) contextPromptAccent.style.backgroundColor = promptColor;
                    contextPrompt.RemoveFromClassList("context-prompt--hidden");
                }
            }
        }

        private static void RegisterInteractiveHover(VisualElement el)
        {
            if (el == null) return;
            el.RegisterCallback<PointerEnterEvent>(_ => interactiveHoverCount++);
            el.RegisterCallback<PointerLeaveEvent>(_ => interactiveHoverCount = Mathf.Max(0, interactiveHoverCount - 1));
        }

        private static string FormatCompassHeading(float yawDegrees)
        {
            float yaw = Mathf.Repeat(yawDegrees, 360f);
            if (yaw < 45f || yaw >= 315f) return "Utara (0°)";
            if (yaw < 135f) return "Timur (90°)";
            if (yaw < 225f) return "Selatan (180°)";
            return "Barat (270°)";
        }
    }
}
