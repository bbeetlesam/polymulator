using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
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
        private VisualElement hudRoot;
        private static int interactiveHoverCount;

        public static bool IsPointerOverInteractiveUI => interactiveHoverCount > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticHoverCount()
        {
            interactiveHoverCount = 0;
        }

        private VisualElement modeAccentBar;
        private Label objectiveLabel;
        private Label objectiveDetailLabel;

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
        private Label storyProgressLabel;
        private Label storyTextLabel;

        private VisualElement fusionStageBar;
        private VisualElement fusionStageAccent;
        private Label fusionStageTitle;
        private Label fusionStageHint;

        private VisualElement targetCard;
        private VisualElement targetAccentBar;
        private Label targetNameLabel;
        private Label targetMetaLabel;
        private Label targetRotationLabel;

        private VisualElement statusToast;
        private Label statusToastLabel;

        private readonly List<Button> visibleModalButtons = new List<Button>(5);
        private int focusedModalIndex;

        private VisualElement controlsCard;
        private VisualElement slotPartition;
        private VisualElement slotFusion;
        private PartitionFusionController partitionFusionController;
        private EndingChoiceController endingController;
        private VisualElement endingEpilogueBox;
        private Label endingSummaryLabel;
        private Label endingPara1;
        private Label endingPara2;
        private Label endingPara3;
        private Label endingMemoriesLabel;

        private VisualElement slotRotasi;
        private VisualElement slotSpecial;
        private HudVectorIcon iconSpecial;
        private HudVectorIcon iconRotasi;
        private HudVectorIcon iconTranslasi;
        private HudVectorIcon iconElevasi;
        private HudVectorIcon iconPartition;
        private HudVectorIcon iconFusion;
        private Label titleSpecial;

        private bool controlsExpanded = false;
        private float toastTimer;
        private bool hasShownFlexNarrative;
        private bool isPaused;
        private bool pauseSettingsOpen;
        private bool chamberCompleted;
        private float clearOverlayDelay = -1f;
        private string levelDisplayName = string.Empty;
        private string levelNumberLabel = "1.1";
        private string nextSceneName = string.Empty;
        private LevelInfo activeLevelInfo;
        private WeightedPressurePlate[] allPlates;
        private int lastPlateMassSignature = -1;
        private bool lastTargetVisible;
        private string lastTargetName = string.Empty;
        private string lastTargetWeight = string.Empty;
        private string lastTargetRotation = string.Empty;
        private Color lastTargetBarColor;
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
        private int translationCount;
        private int rotationCount;
        private int throwOrTetherCount;
        private bool hasShownBlueprintConcept;
        private int hintStep;

        private void Awake()
        {
            Time.timeScale = 1f;
            EnsureEventSystem();
            uiDocument = GetComponent<UIDocument>();
            ResolveReferences();
            ResolveLevelMetadata();
        }

        private static void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
            }
            StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private void Start()
        {
            BindVisualTree();
            RefreshModeVisuals();
            RefreshObjectiveText();
            RefreshFusionStageBar();
            RefreshTargetAndPrompt(true);

            string prefix = SimulationProgress.IsSpecialShowcase(SceneManager.GetActiveScene().name)
                ? L.T("ui.hud.start.showcase")
                : L.T("ui.hud.start.room", levelNumberLabel);
            HandleStatusBanner(prefix + levelDisplayName, PaletteSettings.Sapphire);
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
                levelDisplayName = activeLevelInfo.Title;
            else
                levelDisplayName = L.T("ui.hud.default_level");
        }

        private void OnEnable()
        {
            ResolveReferences();
            BindVisualTree();
            SubscribeEvents();
            RefreshModeVisuals();
            RefreshObjectiveText();
            RefreshFusionStageBar();
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
            if (allPlates != null && allPlates.Length > 1)
            {
                System.Array.Sort(allPlates, (a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    int xCmp = a.transform.position.x.CompareTo(b.transform.position.x);
                    return xCmp != 0 ? xCmp : string.CompareOrdinal(a.gameObject.name, b.gameObject.name);
                });
            }
            if (anchorObjective == null) anchorObjective = Object.FindFirstObjectByType<AnchorSocketObjective>();
            if (blueprintVisualizer == null) blueprintVisualizer = Object.FindFirstObjectByType<SpatialBlueprintVisualizer>();
            if (partitionFusionController == null) partitionFusionController = Object.FindFirstObjectByType<PartitionFusionController>();
            if (endingController == null) endingController = Object.FindFirstObjectByType<EndingChoiceController>();
            SimulationSettings.ApplyAudioVolume();
        }

        private void BindVisualTree()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            if (boundVisualRoot == root) return;
            boundVisualRoot = root;
            hudRoot = root.Q<VisualElement>("hud-root");
            ApplyReduceMotionClass();

            modeAccentBar = root.Q<VisualElement>("mode-accent-bar");
            objectiveLabel = root.Q<Label>("objective-label");
            objectiveDetailLabel = root.Q<Label>("objective-detail-label");

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
            storyProgressLabel = root.Q<Label>("story-progress-label");
            storyTextLabel = root.Q<Label>("story-text-label");

            fusionStageBar = root.Q<VisualElement>("fusion-stage-bar");
            fusionStageAccent = root.Q<VisualElement>("fusion-stage-accent");
            fusionStageTitle = root.Q<Label>("fusion-stage-title");
            fusionStageHint = root.Q<Label>("fusion-stage-hint");

            targetCard = root.Q<VisualElement>("target-card");
            targetAccentBar = root.Q<VisualElement>("target-accent-bar");
            targetNameLabel = root.Q<Label>("target-name-label");
            targetMetaLabel = root.Q<Label>("target-meta-label");
            targetRotationLabel = root.Q<Label>("target-rotation-label");

            statusToast = root.Q<VisualElement>("status-toast");
            statusToastLabel = root.Q<Label>("status-toast-label");

            controlsCard = root.Q<VisualElement>("controls-card");
            if (controlsCard != null)
            {
                controlsCard.EnableInClassList("controls-card--collapsed", !controlsExpanded);
                controlsCard.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0) return;
                    controlsExpanded = !controlsExpanded;
                    controlsCard.EnableInClassList("controls-card--collapsed", !controlsExpanded);
                    SimulationAudio.PlayMenuHover();
                    evt.StopPropagation();
                });
            }

            if (storyFeed != null)
            {
                storyFeed.RegisterCallback<PointerDownEvent>(OnStoryCardClicked);
                RegisterInteractiveHover(storyFeed);
            }
            RegisterInteractiveHover(modalCard);
            RegisterInteractiveHover(controlsCard);

            BindModalButton(btnNext, LoadNextChamber);
            BindModalButton(btnResume, TogglePauseMenu);
            BindModalButton(btnPauseSettings, TogglePauseSettingsPanel);
            BindModalButton(btnReplay, ReplayCurrentChamber);
            BindModalButton(btnMainMenu, ReturnToMainMenu);

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
                ApplyReduceMotionClass();
                SimulationAudio.PlayMenuHover();
            });

            if (btnNext != null && !string.IsNullOrEmpty(nextSceneName))
                btnNext.text = L.T("ui.hud.overlay.next_room", nextSceneName.Replace("Level", ""));

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
            titleSpecial = root.Q<Label>("title-special");

            if (iconPartition != null) iconPartition.SetTint(PaletteSettings.Peach);
            if (iconFusion != null) iconFusion.SetTint(PaletteSettings.Subtext1);
        }

        private void BindModalButton(Button button, System.Action onClick)
        {
            if (button == null) return;
            button.clicked += onClick;
            button.RegisterCallback<PointerEnterEvent>(_ =>
            {
                int idx = visibleModalButtons.IndexOf(button);
                if (idx >= 0) focusedModalIndex = idx;
                SimulationAudio.PlayMenuHover();
            });
            button.RegisterCallback<FocusInEvent>(_ =>
            {
                int idx = visibleModalButtons.IndexOf(button);
                if (idx >= 0) focusedModalIndex = idx;
            });
        }

        private void ApplyReduceMotionClass()
        {
            if (hudRoot != null)
                hudRoot.EnableInClassList("hud-root--reduce-motion", SimulationSettings.ReduceMotion);
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
                partitionFusionController.OnFusionStagingChanged += HandleFusionStagingChanged;
                partitionFusionController.OnFusionSideChanged += HandleFusionSideChanged;
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
                partitionFusionController.OnFusionStagingChanged -= HandleFusionStagingChanged;
                partitionFusionController.OnFusionSideChanged -= HandleFusionSideChanged;
            }
            if (endingController != null)
            {
                endingController.OnEndingChosen -= HandleEndingChosen;
                endingController.OnChoiceFeedback -= HandleChoiceFeedback;
            }
            MemoryFragment.OnCollected -= HandleFragmentCollected;
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
                HandleStatusBanner(L.T("ui.hud.blueprint.intro"), PaletteSettings.Teal);
            }
            else
            {
                HandleStatusBanner(active ? L.T("ui.hud.blueprint.on") : L.T("ui.hud.blueprint.off"), PaletteSettings.Teal);
            }
        }

        private void HandleModeChanged(SimulationMode mode)
        {
            RefreshModeVisuals();
            RefreshTargetAndPrompt(true);
            if (mode == SimulationMode.Flexible && !hasShownFlexNarrative)
            {
                hasShownFlexNarrative = true;
                HandleStatusBanner(L.T("ui.hud.mode.flexible_active"), PaletteSettings.Mauve);
            }
        }

        private void HandlePlateChanged(bool active)
        {
            RefreshObjectiveText();
            if (chamberCompleted) return;
            HandleStatusBanner(
                active ? L.T("ui.hud.plate.active") : L.T("ui.hud.plate.released"),
                active ? PaletteSettings.Green : PaletteSettings.Peach);
        }

        private void HandleFragmentCollected(string fragTitle, string speaker, string line, string replySpeaker, string replyLine)
        {
            fragmentCollectedThisRun = true;
            HandleStatusBanner(L.T("ui.hud.fragment.saved", fragTitle), PaletteSettings.Rosewater);
            storyQueue.Clear();
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
                AdvanceStoryQueue();
            }
            else
            {
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
            if (storyFeed == null || storyTextLabel == null) return;
            if (storyQueue.Count == 0)
            {
                storyShowing = false;
                storyTyping = false;
                storyHoldTimer = 0f;
                storyFeed.AddToClassList("story-feed--hidden");
                return;
            }

            LevelInfo.StoryLine next = storyQueue.Dequeue();
            currentSpeakerRaw = string.IsNullOrEmpty(next.speaker) ? "SIM" : next.speaker.ToUpperInvariant();
            currentFullLine = next.text ?? string.Empty;
            bool instantReveal = SimulationSettings.ReduceMotion;
            currentTypedChars = instantReveal ? currentFullLine.Length : 0;
            storyShowing = true;
            storyTyping = !instantReveal;
            nextTypeCharAt = Time.unscaledTime;
            storyHoldTimer = instantReveal
                ? Mathf.Clamp(3.2f + currentFullLine.Length * 0.045f, 3.6f, 6.8f)
                : 0f;

            storyFeed.style.borderTopColor = SpeakerColor(currentSpeakerRaw);
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
                    else if (ch == ',' || ch == ':' || ch == '\u2014' || ch == ';') step = 0.085f;
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
            storyProgressLabel.text = L.T("ui.hud.story.next");
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
        private void HandleFusionStagingChanged(bool staging) => RefreshFusionStageBar();
        private void HandleFusionSideChanged(FusionAttachmentSide side, bool valid) => RefreshFusionStageBar();

        private void RefreshFusionStageBar()
        {
            if (fusionStageBar == null) return;
            bool active = partitionFusionController != null && partitionFusionController.IsFusionStaging && !isPaused && !chamberCompleted;
            fusionStageBar.EnableInClassList("fusion-stage-bar--hidden", !active);
            if (!active) return;

            string sideLabel = partitionFusionController.SelectedFusionSideLabel;
            string presetName = partitionFusionController.SelectedPresetDisplayName;
            string pairLabel = partitionFusionController.SelectedPairLabel;
            bool valid = partitionFusionController.IsSelectedSideValid;
            Color accent = valid ? partitionFusionController.SelectedPresetAccent : PaletteSettings.Red;

            if (fusionStageAccent != null) fusionStageAccent.style.backgroundColor = accent;

            if (fusionStageTitle != null)
            {
                string presetTag = !string.IsNullOrEmpty(pairLabel)
                    ? presetName + " (" + pairLabel + ")"
                    : presetName;
                fusionStageTitle.text = valid
                    ? L.T("ui.hud.fusion.stage.valid", presetTag, sideLabel)
                    : L.T("ui.hud.fusion.stage.blocked", presetName, sideLabel);
                fusionStageTitle.style.color = accent;
            }
            if (fusionStageHint != null)
            {
                fusionStageHint.text = L.T("ui.hud.fusion.stage.hint");
            }
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

            HandleStatusBanner(L.T("ui.hud.objective.completed", levelNumberLabel), PaletteSettings.Green);

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

        private void RefreshPlateMassIfChanged()
        {
            if (chamberCompleted) return;
            int sig = 17;
            if (allPlates != null && allPlates.Length > 0)
            {
                for (int i = 0; i < allPlates.Length; i++)
                {
                    WeightedPressurePlate p = allPlates[i];
                    if (p == null) continue;
                    int decMass = Mathf.RoundToInt(p.CurrentMass * 10f);
                    sig = sig * 31 + decMass * 2 + (p.IsActivated ? 1 : 0);
                }
            }
            else if (pressurePlate != null)
            {
                sig = sig * 31 + Mathf.RoundToInt(pressurePlate.CurrentMass * 10f) * 2 + (pressurePlate.IsActivated ? 1 : 0);
            }
            else
            {
                return;
            }

            if (sig != lastPlateMassSignature)
            {
                lastPlateMassSignature = sig;
                RefreshObjectiveText();
            }
        }

        private void RefreshObjectiveText()
        {
            if (objectiveLabel == null) return;

            void SetDetail(string text, Color color)
            {
                if (objectiveDetailLabel == null) return;
                bool hasText = !string.IsNullOrEmpty(text);
                objectiveDetailLabel.style.display = hasText ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasText)
                {
                    objectiveDetailLabel.text = text;
                    objectiveDetailLabel.style.color = color;
                }
            }

            if (chamberCompleted)
            {
                objectiveLabel.text = endingController != null
                    ? L.T("ui.hud.objective.decision_set")
                    : L.T("ui.hud.objective.chapter_complete");
                objectiveLabel.style.color = PaletteSettings.Green;
                SetDetail(endingController != null ? string.Empty : L.T("ui.hud.objective.core_locked"), PaletteSettings.Green);
                if (modeAccentBar != null) modeAccentBar.style.backgroundColor = PaletteSettings.Green;
                return;
            }

            if (endingController != null)
            {
                objectiveLabel.text = L.T("ui.hud.objective.final_choice");
                objectiveLabel.style.color = PaletteSettings.Text;
                SetDetail(L.T("ui.hud.objective.final_paths"), PaletteSettings.Teal);
                if (modeAccentBar != null) modeAccentBar.style.backgroundColor = PaletteSettings.Teal;
                return;
            }

            WeightedPressurePlate[] plates = (allPlates != null && allPlates.Length > 0)
                ? allPlates
                : (pressurePlate != null ? new[] { pressurePlate } : null);

            if (plates != null && plates.Length > 0)
            {
                int totalPlates = 0;
                int activeCount = 0;
                float sumCurrent = 0f;
                float sumRequired = 0f;

                for (int i = 0; i < plates.Length; i++)
                {
                    if (plates[i] == null) continue;
                    totalPlates++;
                    if (plates[i].IsActivated) activeCount++;
                    sumCurrent += plates[i].CurrentMass;
                    sumRequired += plates[i].requiredMass;
                }

                int totalSteps = totalPlates + 1; // plates + Anchor lock
                bool allActive = totalPlates > 0 && activeCount >= totalPlates;

                if (allActive)
                {
                    objectiveLabel.text = L.T("ui.hud.objective.plates",
                        activeCount, totalPlates, activeCount, totalSteps);
                    objectiveLabel.style.color = PaletteSettings.Green;
                    SetDetail(L.T("ui.hud.objective.bring_core"), PaletteSettings.Green);
                    if (modeAccentBar != null) modeAccentBar.style.backgroundColor = PaletteSettings.Green;
                }
                else
                {
                    objectiveLabel.text = L.T("ui.hud.objective.plates",
                        activeCount, totalPlates, activeCount, totalSteps);
                    objectiveLabel.style.color = PaletteSettings.Text;

                    if (totalPlates == 1)
                    {
                        WeightedPressurePlate single = plates[0];
                        SetDetail(L.T("ui.hud.objective.plate_weight", single.CurrentMass, single.requiredMass),
                            PaletteSettings.Teal);
                    }
                    else if (totalPlates <= 3)
                    {
                        System.Text.StringBuilder sb = new System.Text.StringBuilder(L.T("ui.hud.objective.weights"));
                        bool first = true;
                        for (int i = 0; i < plates.Length; i++)
                        {
                            if (plates[i] == null) continue;
                            if (!first) sb.Append("  •  ");
                            first = false;
                            sb.Append(L.T("ui.hud.objective.weight_item",
                                plates[i].CurrentMass, plates[i].requiredMass));
                        }
                        SetDetail(sb.ToString(), PaletteSettings.Teal);
                    }
                    else
                    {
                        SetDetail(L.T("ui.hud.objective.total_weight", sumCurrent, sumRequired),
                            PaletteSettings.Teal);
                    }

                    if (modeAccentBar != null) modeAccentBar.style.backgroundColor = PaletteSettings.Teal;
                }
                return;
            }

            objectiveLabel.text = L.T("ui.hud.objective.lock_core");
            objectiveLabel.style.color = PaletteSettings.Text;
            SetDetail(string.Empty, PaletteSettings.Teal);
            if (modeAccentBar != null) modeAccentBar.style.backgroundColor = PaletteSettings.Teal;
        }

        private void HandleStatusBanner(string message, Color color)
        {
            if (statusToast == null || statusToastLabel == null) return;

            statusToastLabel.text = message;
            statusToast.style.borderTopColor = color;

            statusToast.RemoveFromClassList("status-toast--hidden");
            toastTimer = 2.2f;
        }

        private void HandleTargetAction(InteractableObject obj) => RefreshTargetAndPrompt(true);
        private void HandleRotated(InteractableObject obj, float yaw) { rotationCount++; RefreshTargetAndPrompt(true); }

        private void RefreshModeVisuals()
        {
            bool isFlexible = modeController != null && modeController.IsFlexible;
            Color accent = isFlexible ? PaletteSettings.ModeFlexible : PaletteSettings.ModeLimited;

            // Keep always-on standard transforms in calm unified Text so focus stays on mode and context slots.
            if (iconTranslasi != null) iconTranslasi.SetTint(PaletteSettings.Text);
            if (iconElevasi != null) iconElevasi.SetTint(PaletteSettings.Text);
            if (iconRotasi != null) iconRotasi.SetTint(PaletteSettings.Text);

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
            if (titleSpecial != null) titleSpecial.text = isFlexible
                ? L.T("ui.hud.ability.telekinesis")
                : L.T("ui.hud.ability.throw");

            RefreshPartitionFusionDock();
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

            if (slotFusion != null)
            {
                slotFusion.EnableInClassList("ability-slot--queued", !canFuse);
                slotFusion.EnableInClassList("ability-slot--active", canFuse && !pairReady);
                slotFusion.EnableInClassList("ability-slot--fusion-ready", pairReady);
            }
            if (iconFusion != null)
                iconFusion.SetTint(canFuse ? (pairReady ? PaletteSettings.Green : PaletteSettings.Subtext1) : PaletteSettings.Overlay0);
        }

        private void Update()
        {
            if (boundVisualRoot == null)
            {
                BindVisualTree();
                if (boundVisualRoot != null)
                {
                    RefreshModeVisuals();
                    RefreshObjectiveText();
                    RefreshFusionStageBar();
                    RefreshTargetAndPrompt(true);
                }
            }

            bool inFusionStaging = partitionFusionController != null &&
                                   (partitionFusionController.IsFusionStaging ||
                                    partitionFusionController.ConsumedCancelOrConfirmThisFrame);
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.escapeKey.wasPressedThisFrame && !chamberCompleted && !inFusionStaging)
                {
                    if (isPaused && pauseSettingsOpen) TogglePauseSettingsPanel();
                    else TogglePauseMenu();
                }

                if (keyboard.hKey.wasPressedThisFrame && controlsCard != null && !isPaused && !chamberCompleted)
                {
                    controlsExpanded = !controlsExpanded;
                    controlsCard.EnableInClassList("controls-card--collapsed", !controlsExpanded);
                }

                if (keyboard.f1Key.wasPressedThisFrame && !isPaused && !chamberCompleted && !inFusionStaging)
                    ShowContextualSimHint();

                if (keyboard.spaceKey.wasPressedThisFrame && storyShowing && !isPaused && !chamberCompleted && !inFusionStaging)
                    SkipOrAdvanceStory();

                if (isPaused || chamberCompleted)
                {
                    if (!pauseSettingsOpen)
                    {
                        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
                        {
                            FocusModalButton(focusedModalIndex + 1);
                            SimulationAudio.PlayMenuHover();
                        }
                        else if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
                        {
                            FocusModalButton(focusedModalIndex - 1);
                            SimulationAudio.PlayMenuHover();
                        }
                        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                        {
                            ActivateFocusedModalButton();
                        }
                        else if (keyboard.rKey.wasPressedThisFrame)
                        {
                            ReplayCurrentChamber();
                        }
                        else if (keyboard.mKey.wasPressedThisFrame)
                        {
                            ReturnToMainMenu();
                        }
                    }
                }
            }

            UpdateStoryTypewriter();
            RefreshPlateMassIfChanged();
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
            if (btnPauseSettings != null) btnPauseSettings.text = pauseSettingsOpen
                ? L.T("ui.menu.settings.close")
                : L.T("ui.menu.settings.title");
            SimulationAudio.PlayMenuClick();
            if (pauseSettingsOpen && pauseSliderVolume != null) pauseSliderVolume.Focus();
            else if (!pauseSettingsOpen && btnPauseSettings != null) btnPauseSettings.Focus();
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
            if (btnPauseSettings != null) btnPauseSettings.text = pauseSettingsOpen
                ? L.T("ui.menu.settings.close")
                : L.T("ui.menu.settings.title");
        }

        private void ShowContextualSimHint()
        {
            SimulationAudio.PlayMenuHover();
            if (chamberCompleted)
            {
                HandleStatusBanner(L.T("ui.hud.hint.completed"), PaletteSettings.Green);
                return;
            }

            if (activeLevelInfo != null && activeLevelInfo.Hints != null && activeLevelInfo.Hints.Length > 0)
            {
                string customHint = activeLevelInfo.Hints[hintStep % activeLevelInfo.Hints.Length];
                hintStep++;
                HandleStatusBanner(L.T("ui.hud.hint.custom", customHint), PaletteSettings.Sky);
                return;
            }

            if (pressurePlate != null && !pressurePlate.IsActivated)
            {
                hintStep = (hintStep + 1) % 2;
                string msg = hintStep == 0
                    ? L.T("ui.hud.hint.plate_place")
                    : L.T("ui.hud.hint.modes");
                HandleStatusBanner(msg, PaletteSettings.Peach);
                return;
            }

            hintStep = (hintStep + 1) % 2;
            string bridgeMsg = hintStep == 0
                ? L.T("ui.hud.hint.core_to_anchor")
                : L.T("ui.hud.hint.rotate_core");
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
                modalTitle.text = completed
                    ? L.T("ui.hud.overlay.cleared_title", levelNumberLabel)
                    : L.T("ui.hud.overlay.paused_title");
            if (modalSubtitle != null)
                modalSubtitle.text = completed
                    ? L.T("ui.hud.overlay.cleared_subtitle", levelDisplayName)
                    : L.T("ui.hud.overlay.paused_subtitle", levelNumberLabel, levelDisplayName);
            if (modalRankLabel != null)
            {
                modalRankLabel.EnableInClassList("modal-rank--hidden", !completed);
                if (completed)
                {
                    string stars = new string('★', lastClearStars) + new string('☆', 3 - lastClearStars);
                    string fragTag = fragmentCollectedThisRun || SimulationProgress.HasFragment(SceneManager.GetActiveScene().name)
                        ? L.T("ui.hud.overlay.memory_saved")
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
            bool hasNext = completed && !string.IsNullOrEmpty(nextSceneName);
            if (btnNext != null)
            {
                btnNext.text = !string.IsNullOrEmpty(nextSceneName)
                    ? L.T("ui.hud.overlay.next_room", nextSceneName.Replace("Level", ""))
                    : L.T("ui.hud.overlay.next_generic");
                btnNext.EnableInClassList("modal-btn--hidden", !hasNext);
                btnNext.EnableInClassList("modal-btn--primary", hasNext);
            }
            if (btnResume != null)
            {
                btnResume.text = L.T("ui.hud.overlay.resume");
                btnResume.EnableInClassList("modal-btn--hidden", completed);
                btnResume.EnableInClassList("modal-btn--primary", !completed);
            }
            if (btnPauseSettings != null)
            {
                btnPauseSettings.text = L.T("ui.menu.settings.title");
                btnPauseSettings.EnableInClassList("modal-btn--hidden", completed);
            }
            if (btnReplay != null)
            {
                btnReplay.text = L.T("ui.hud.overlay.replay");
                btnReplay.EnableInClassList("modal-btn--primary", completed && !hasNext);
            }
            if (btnMainMenu != null)
            {
                btnMainMenu.text = L.T("ui.hud.overlay.main_menu");
                btnMainMenu.RemoveFromClassList("modal-btn--primary");
                btnMainMenu.AddToClassList("modal-btn--subtle");
            }
            if (pauseSettingsPanel != null)
                pauseSettingsPanel.AddToClassList("pause-settings-panel--hidden");

            RevealModalOverlay();
            RebuildModalFocusList();
            FocusModalButton(0);
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
                endingMemoriesLabel.text = L.T("ui.hud.overlay.total_memories", outcome.fragmentCount);

            if (btnNext != null)
            {
                btnNext.AddToClassList("modal-btn--hidden");
                btnNext.RemoveFromClassList("modal-btn--primary");
            }
            if (btnResume != null)
            {
                btnResume.AddToClassList("modal-btn--hidden");
                btnResume.RemoveFromClassList("modal-btn--primary");
            }
            if (btnPauseSettings != null) SetPauseSettingsButtonVisible(false);
            if (btnReplay != null)
            {
                btnReplay.text = L.T("ui.hud.overlay.replay_ending");
                btnReplay.RemoveFromClassList("modal-btn--primary");
            }
            if (btnMainMenu != null)
            {
                btnMainMenu.text = L.T("ui.hud.overlay.back_to_menu");
                btnMainMenu.RemoveFromClassList("modal-btn--subtle");
                btnMainMenu.AddToClassList("modal-btn--primary");
            }

            RevealModalOverlay();
            RebuildModalFocusList();
            int mainIdx = visibleModalButtons.IndexOf(btnMainMenu);
            FocusModalButton(mainIdx >= 0 ? mainIdx : 0);
        }

        private void RevealModalOverlay()
        {
            if (modalCard != null && !SimulationSettings.ReduceMotion)
            {
                modalCard.AddToClassList("modal-card--entering");
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
                modalCard.schedule.Execute(() => modalCard.RemoveFromClassList("modal-card--entering")).ExecuteLater(16);
            }
            else
            {
                if (modalCard != null) modalCard.RemoveFromClassList("modal-card--entering");
                chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
            }
        }

        private void RebuildModalFocusList()
        {
            visibleModalButtons.Clear();
            Button[] ordered = { btnNext, btnResume, btnPauseSettings, btnReplay, btnMainMenu };
            for (int i = 0; i < ordered.Length; i++)
            {
                if (ordered[i] != null && !ordered[i].ClassListContains("modal-btn--hidden"))
                    visibleModalButtons.Add(ordered[i]);
            }
            focusedModalIndex = 0;
        }

        private void FocusModalButton(int index)
        {
            if (visibleModalButtons.Count == 0) return;
            focusedModalIndex = (index + visibleModalButtons.Count) % visibleModalButtons.Count;
            visibleModalButtons[focusedModalIndex]?.Focus();
        }

        private void ActivateFocusedModalButton()
        {
            if (visibleModalButtons.Count == 0) return;
            Button active = visibleModalButtons[Mathf.Clamp(focusedModalIndex, 0, visibleModalButtons.Count - 1)];
            if (active == btnNext) LoadNextChamber();
            else if (active == btnResume) TogglePauseMenu();
            else if (active == btnPauseSettings) TogglePauseSettingsPanel();
            else if (active == btnReplay) ReplayCurrentChamber();
            else if (active == btnMainMenu) ReturnToMainMenu();
        }

        private void SetPauseSettingsButtonVisible(bool visible)
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
            if (targetCard == null || targetNameLabel == null) return;

            if (isPaused || chamberCompleted)
            {
                if (force || lastTargetVisible)
                {
                    lastTargetVisible = false;
                    targetCard.AddToClassList("target-card--hidden");
                }
                return;
            }

            InteractableObject activeObj = null;
            if (carryController != null && carryController.IsCarrying)
                activeObj = carryController.CarriedObject;
            else if (telekinesisController != null && telekinesisController.HasTether)
                activeObj = telekinesisController.TetheredObject;
            else if (interactor != null && interactor.HasTarget)
                activeObj = interactor.CurrentTarget;

            bool showTargetCard = activeObj != null;
            string objName = showTargetCard ? activeObj.displayName : string.Empty;
            string weightText = string.Empty;
            if (showTargetCard && activeObj.CanBePickedUp && activeObj.Body != null)
            {
                weightText = L.T("ui.hud.target.weight", activeObj.Body.mass);
            }
            string rotationText = showTargetCard
                ? L.T("ui.hud.target.rotation", Mathf.Repeat(Mathf.Round(activeObj.transform.eulerAngles.y), 360f))
                : string.Empty;

            bool isFlexible = modeController != null && modeController.IsFlexible;
            Color modeColor = isFlexible ? PaletteSettings.ModeFlexible : PaletteSettings.ModeLimited;
            Color barColor = showTargetCard
                ? (activeObj.HasAssignedColor ? activeObj.AssignedColor : modeColor)
                : PaletteSettings.Overlay0;

            if (!force &&
                showTargetCard == lastTargetVisible &&
                objName == lastTargetName &&
                weightText == lastTargetWeight &&
                rotationText == lastTargetRotation &&
                barColor == lastTargetBarColor)
            {
                return;
            }

            lastTargetVisible = showTargetCard;
            lastTargetName = objName;
            lastTargetWeight = weightText;
            lastTargetRotation = rotationText;
            lastTargetBarColor = barColor;

            targetCard.EnableInClassList("target-card--hidden", !showTargetCard);
            if (!showTargetCard) return;

            targetNameLabel.text = objName;
            if (targetMetaLabel != null)
            {
                bool hasWeight = !string.IsNullOrEmpty(weightText);
                targetMetaLabel.style.display = hasWeight ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasWeight) targetMetaLabel.text = weightText;
            }
            if (targetRotationLabel != null)
            {
                bool hasRotation = !string.IsNullOrEmpty(rotationText);
                targetRotationLabel.style.display = hasRotation ? DisplayStyle.Flex : DisplayStyle.None;
                if (hasRotation) targetRotationLabel.text = rotationText;
            }
            if (targetAccentBar != null) targetAccentBar.style.backgroundColor = barColor;
        }

        private static void RegisterInteractiveHover(VisualElement el)
        {
            if (el == null) return;
            el.RegisterCallback<PointerEnterEvent>(_ => interactiveHoverCount++);
            el.RegisterCallback<PointerLeaveEvent>(_ => interactiveHoverCount = Mathf.Max(0, interactiveHoverCount - 1));
        }
    }
}
