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

        private UIDocument uiDocument;

        private VisualElement modeAccentBar;
        private HudVectorIcon modeIcon;
        private Label chamberSubtitleLabel;
        private Label objectiveLabel;
        private Label modeBadgeLabel;
        private Label modeSummaryLabel;

        private VisualElement chamberOverlay;
        private VisualElement modalAccentBar;
        private Label modalTitle;
        private Label modalSubtitle;
        private Button btnResume;
        private Button btnReplay;
        private Button btnMainMenu;

        private VisualElement targetCard;
        private VisualElement targetAccentBar;
        private VisualElement targetSwatch;
        private Label targetStateLabel;
        private Label targetNameLabel;
        private Label targetMetaLabel;

        private VisualElement statusToast;
        private VisualElement statusToastDot;
        private Label statusToastLabel;

        private VisualElement controlsCard;
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

        private bool controlsExpanded = true;
        private float toastTimer;
        private string lastPromptText;
        private Color lastPromptColor;
        private bool hasShownFlexNarrative;
        private bool isPaused;
        private bool chamberCompleted;
        private float clearOverlayDelay = -1f;
        private string levelDisplayName = "WARMING UP";
        private string levelNumberLabel = "1.1";

        private void Awake()
        {
            uiDocument = GetComponent<UIDocument>();
            ResolveReferences();
            ResolveLevelMetadata();
        }

        private void Start()
        {
            HandleStatusBanner("SIM // CHAMBER " + levelNumberLabel + " — PLACE WEIGHT ON CONDUIT PAD", PaletteSettings.Sapphire);
            RefreshObjectiveText();
        }

        private void ResolveLevelMetadata()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            if (sceneName.StartsWith("Level") && sceneName.Length > 5)
                levelNumberLabel = sceneName.Substring(5);

            LevelInfo info = Resources.Load<LevelInfo>("Levels/" + sceneName);
            if (info != null && !string.IsNullOrEmpty(info.Title))
                levelDisplayName = info.Title.ToUpperInvariant();
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
            if (anchorObjective == null) anchorObjective = Object.FindFirstObjectByType<AnchorSocketObjective>();
        }

        private void BindVisualTree()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            VisualElement root = uiDocument.rootVisualElement;

            modeAccentBar = root.Q<VisualElement>("mode-accent-bar");
            modeIcon = root.Q<HudVectorIcon>("mode-icon");
            chamberSubtitleLabel = root.Q<Label>("chamber-subtitle-label");
            objectiveLabel = root.Q<Label>("objective-label");
            modeBadgeLabel = root.Q<Label>("mode-badge-label");
            modeSummaryLabel = root.Q<Label>("mode-summary-label");

            chamberOverlay = root.Q<VisualElement>("chamber-overlay");
            modalAccentBar = root.Q<VisualElement>("modal-accent-bar");
            modalTitle = root.Q<Label>("modal-title");
            modalSubtitle = root.Q<Label>("modal-subtitle");
            btnResume = root.Q<Button>("btn-resume");
            btnReplay = root.Q<Button>("btn-replay");
            btnMainMenu = root.Q<Button>("btn-mainmenu");

            if (chamberSubtitleLabel != null)
                chamberSubtitleLabel.text = "CHAMBER " + levelNumberLabel + "  //  " + levelDisplayName;
            if (btnResume != null) btnResume.clicked += TogglePauseMenu;
            if (btnReplay != null) btnReplay.clicked += ReplayCurrentChamber;
            if (btnMainMenu != null) btnMainMenu.clicked += ReturnToMainMenu;

            targetCard = root.Q<VisualElement>("target-card");
            targetAccentBar = root.Q<VisualElement>("target-accent-bar");
            targetSwatch = root.Q<VisualElement>("target-swatch");
            targetStateLabel = root.Q<Label>("target-state-label");
            targetNameLabel = root.Q<Label>("target-name-label");
            targetMetaLabel = root.Q<Label>("target-meta-label");

            statusToast = root.Q<VisualElement>("status-toast");
            statusToastDot = root.Q<VisualElement>("status-toast-dot");
            statusToastLabel = root.Q<Label>("status-toast-label");

            controlsCard = root.Q<VisualElement>("controls-card");
            contextPrompt = root.Q<VisualElement>("context-prompt");
            contextPromptAccent = root.Q<VisualElement>("context-prompt-accent");
            contextPromptLabel = root.Q<Label>("context-prompt-label");

            slotRotasi = root.Q<VisualElement>("slot-rotasi");
            slotSpecial = root.Q<VisualElement>("slot-special");
            iconTranslasi = root.Q<HudVectorIcon>("icon-translasi");
            iconElevasi = root.Q<HudVectorIcon>("icon-elevasi");
            iconRotasi = root.Q<HudVectorIcon>("icon-rotasi");
            iconSpecial = root.Q<HudVectorIcon>("icon-special");
            iconPartition = root.Q<HudVectorIcon>("icon-partition");
            iconFusion = root.Q<HudVectorIcon>("icon-fusion");
            keyRotasi = root.Q<Label>("key-rotasi");
            titleSpecial = root.Q<Label>("title-special");
            keySpecial = root.Q<Label>("key-special");

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
                carryController.OnPickedUp += HandleTargetAction;
                carryController.OnDropped += HandleTargetAction;
                carryController.OnThrown += HandleTargetAction;
                carryController.OnRotated += HandleRotated;
            }
            if (telekinesisController != null)
            {
                telekinesisController.OnTetherStarted += HandleTargetAction;
                telekinesisController.OnTetherReleased += HandleTargetAction;
            }
            if (pressurePlate != null)
                pressurePlate.OnActivatedChanged += HandlePlateChanged;
            if (anchorObjective != null)
            {
                anchorObjective.OnObjectiveCompleted += HandleObjectiveCompleted;
                anchorObjective.OnObjectiveFeedback += HandleStatusBanner;
            }
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
                carryController.OnPickedUp -= HandleTargetAction;
                carryController.OnDropped -= HandleTargetAction;
                carryController.OnThrown -= HandleTargetAction;
                carryController.OnRotated -= HandleRotated;
            }
            if (telekinesisController != null)
            {
                telekinesisController.OnTetherStarted -= HandleTargetAction;
                telekinesisController.OnTetherReleased -= HandleTargetAction;
            }
            if (pressurePlate != null)
                pressurePlate.OnActivatedChanged -= HandlePlateChanged;
            if (anchorObjective != null)
            {
                anchorObjective.OnObjectiveCompleted -= HandleObjectiveCompleted;
                anchorObjective.OnObjectiveFeedback -= HandleStatusBanner;
            }
            if (btnResume != null) btnResume.clicked -= TogglePauseMenu;
            if (btnReplay != null) btnReplay.clicked -= ReplayCurrentChamber;
            if (btnMainMenu != null) btnMainMenu.clicked -= ReturnToMainMenu;
        }

        private void HandleModeChanged(SimulationMode mode)
        {
            RefreshModeVisuals();
            RefreshTargetAndPrompt(true);
            if (mode == SimulationMode.Flexible && !hasShownFlexNarrative)
            {
                hasShownFlexNarrative = true;
                HandleStatusBanner("SIM // RECT VECTOR OVERRIDE — FREE ELEVATE ENABLED", PaletteSettings.Mauve);
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

        private void HandleObjectiveCompleted()
        {
            chamberCompleted = true;
            RefreshObjectiveText();
            HandleStatusBanner("CHAMBER " + levelNumberLabel + " CALIBRATED — ANCHOR LOCKED", PaletteSettings.Green);
            clearOverlayDelay = 0.9f;
        }

        private void RefreshObjectiveText()
        {
            if (objectiveLabel == null) return;
            if (chamberCompleted)
            {
                objectiveLabel.text = "OBJ // CALIBRATED — CORE-01 LOCKED";
                objectiveLabel.style.color = PaletteSettings.Green;
            }
            else if (pressurePlate != null && pressurePlate.IsActivated)
            {
                objectiveLabel.text = "OBJ // BRIDGE ONLINE — DELIVER CORE-01 TO ANCHOR";
                objectiveLabel.style.color = PaletteSettings.Green;
            }
            else
            {
                objectiveLabel.text = "OBJ // WEIGHT CONDUIT PAD -> UNLOCK CORE-01";
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
        private void HandleRotated(InteractableObject obj, float yaw) => RefreshTargetAndPrompt(true);

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
                modeBadgeLabel.text = isFlexible ? "FLEXIBLE" : "LIMITED";
                modeBadgeLabel.style.color = accent;
            }
            if (modeSummaryLabel != null)
            {
                modeSummaryLabel.text = isFlexible
                    ? "Free-Aim Telekinesis"
                    : "Carry  /  90 deg Rotate  /  Throw";
            }

            if (iconTranslasi != null) iconTranslasi.SetTint(PaletteSettings.Sky);
            if (iconElevasi != null) iconElevasi.SetTint(PaletteSettings.Teal);
            if (iconRotasi != null) iconRotasi.SetTint(PaletteSettings.Yellow);
            if (keyRotasi != null) keyRotasi.text = isFlexible ? "[ R ] SPIN" : "[ R ] 90 deg";

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
            if (titleSpecial != null) titleSpecial.text = isFlexible ? "TELEKINESIS" : "THROW";
            if (keySpecial != null) keySpecial.text = isFlexible ? "[ E / LMB ]" : "[ F ]";
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

                if (isPaused || chamberCompleted)
                {
                    if (keyboard.rKey.wasPressedThisFrame) ReplayCurrentChamber();
                    else if (keyboard.mKey.wasPressedThisFrame) ReturnToMainMenu();
                }
            }

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
            Time.timeScale = isPaused ? 0f : 1f;
            SimulationAudio.PlayMenuClick();
            if (isPaused) ShowChamberOverlay(false);
            else if (chamberOverlay != null) chamberOverlay.AddToClassList("chamber-overlay--hidden");
        }

        private void ShowChamberOverlay(bool completed)
        {
            if (chamberOverlay == null) return;
            if (modalAccentBar != null)
                modalAccentBar.style.backgroundColor = completed ? PaletteSettings.Green : PaletteSettings.Sapphire;
            if (modalTitle != null)
                modalTitle.text = completed ? "CHAMBER " + levelNumberLabel + " CALIBRATED" : "CHAMBER PAUSED";
            if (modalSubtitle != null)
                modalSubtitle.text = completed
                    ? levelDisplayName + "  //  ANCHOR-01 LOCKED"
                    : "CHAMBER " + levelNumberLabel + "  //  " + levelDisplayName;
            if (btnResume != null)
                btnResume.EnableInClassList("modal-btn--hidden", completed);
            chamberOverlay.RemoveFromClassList("chamber-overlay--hidden");
        }

        public void ReplayCurrentChamber()
        {
            SimulationAudio.PlayMenuClick();
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
            Color swatchColor = PaletteSettings.Surface2;
            Color barColor = PaletteSettings.Overlay0;

            string prompt = string.Empty;
            Color promptColor = modeColor;

            if (carryController != null && carryController.IsCarrying)
            {
                showTargetCard = true;
                InteractableObject held = carryController.CarriedObject;
                stateText = "CARRYING";
                objName = carryController.CarriedObjectName;
                metaText = "Yaw " + Mathf.RoundToInt(carryController.CurrentHeldYawDegrees) +
                           " deg  |  Reach " + carryController.CurrentDropDistanceGrid.ToString("0.1") + " GU";
                swatchColor = held != null ? held.AssignedColor : PaletteSettings.Green;
                barColor = PaletteSettings.Green;

                prompt = "[ E ]  DROP    [ R ]  ROTATE 90 deg    [ F ]  THROW    " + objName;
                promptColor = PaletteSettings.Green;
            }
            else if (telekinesisController != null && telekinesisController.HasTether)
            {
                showTargetCard = true;
                InteractableObject tethered = telekinesisController.TetheredObject;
                stateText = "TELEKINESIS";
                objName = tethered != null ? tethered.displayName : "OBJECT";
                metaText = "Elevation " + telekinesisController.CurrentElevationGrid.ToString("0.1") + " GU";
                swatchColor = tethered != null ? tethered.AssignedColor : PaletteSettings.BeamTelekinesis;
                barColor = PaletteSettings.BeamTelekinesis;

                prompt = "[ E / LMB ]  RELEASE    [ Q / Z ]  ELEVATE    [ R ]  SPIN    [ F ]  PUSH";
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
                    stateText = isFlexible ? "IN TETHER RANGE" : "IN REACH";
                    metaText = isFlexible ? "E or Click to tether" : "E to pick up";
                    barColor = modeColor;
                    prompt = isFlexible
                        ? "[ E / LMB ]  TELEKINESIS   " + objName
                        : "[ E ]  PICK UP   " + objName;
                    promptColor = modeColor;
                }
                else
                {
                    stateText = "FIXED ANCHOR";
                    metaText = "Stationary socket geometry";
                    barColor = PaletteSettings.Peach;
                    prompt = objName + "  //  FIXED SOCKET";
                    promptColor = PaletteSettings.Peach;
                }
            }

            if (targetCard != null)
                targetCard.EnableInClassList("target-card--hidden", !showTargetCard);

            targetStateLabel.text = stateText;
            targetNameLabel.text = objName;
            targetMetaLabel.text = metaText;
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
    }
}
