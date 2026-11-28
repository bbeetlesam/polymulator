using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UIElements;

namespace Polymulator
{
    /// <summary>
    /// Controls the POLYMULATOR Title Screen, Chapter Index, Settings modal, and direct
    /// chamber launch transition atop the live 3D Kota Tepi diorama.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuState { Title, LevelMenu, PortalLaunch, Done }

        private const int ChapterCount = 5;

        [SerializeField] private string levelSceneFormat = "Level{0}.{1}";
        [SerializeField] private float portalLaunchDuration = 0.44f;

        private UIDocument uiDocument;
        private VisualElement boundRoot;
        private VisualElement menuRoot;
        private MainMenuCameraOrbit cameraOrbit;

        private VisualElement titleView;
        private Label titleProgressLabel;
        private Button btnEnter;
        private Button btnChapters;
        private Button btnTitleShowcase;
        private Button btnTitleSettings;
        private Button btnShutdown;
        private Button[] titleNavButtons;
        private int focusedTitleIndex;

        private VisualElement ribbonView;
        private IVisualElementScheduledItem ribbonFadeSchedule;
        private Label ribbonMetaLabel;
        private CampaignRadialSelector campaignRadial;
        private Button btnRibbonSettings;
        private Button btnRibbonBack;

        private VisualElement chamberPreviewCard;
        private Label chapterFooterLabel;
        private Label previewTitleLabel;
        private readonly UiVectorIcon[] previewStars = new UiVectorIcon[3];
        private Label previewMemoryLabel;
        private Label previewEmptyLabel;
        private Button btnPreviewLaunch;
        private Label btnEnterLabel;
        private Label previewLaunchLabel;
        private UiVectorIcon previewLaunchIcon;
        private int previewChapter = 1;
        private int previewLevel = 1;

        private VisualElement settingsOverlay;
        private Slider sliderVolume;
        private Label valVolume;
        private Slider sliderSensitivity;
        private Label valSensitivity;
        private Toggle toggleReduceMotion;
        private Button btnLanguage;
        private Button btnResetSave;
        private Button btnSettingsClose;
        private Button settingsReturnFocus;

        private readonly string[,] levelTitles = new string[ChapterCount + 1, 5];
        private readonly List<Vector2Int> keyboardLevels = new List<Vector2Int>();

        private MenuState state = MenuState.Title;
        private int currentChapter = 1;
        private int currentLevel = 1;
        private int selectedChapter = 1;
        private int selectedLevel = 1;
        private bool settingsOpen;
        private float resetConfirmUntil;

        private float portalStartTime;
        private string portalSceneName;
        private bool portalSceneDispatched;

        private void Awake()
        {
            Time.timeScale = 1f;
            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;

            EnsureEventSystem();
            SimulationSettings.ApplyAudioVolume();
            SimulationAudio.EnsureInstance();
            uiDocument = GetComponent<UIDocument>();
            cameraOrbit = Object.FindFirstObjectByType<MainMenuCameraOrbit>();

            RefreshCurrentProgress();
            selectedChapter = currentChapter;
            selectedLevel = currentLevel;
            LoadLevelCatalogTitles();
        }

        private void OnEnable()
        {
            L.OnLanguageChanged += RefreshLocalizedMenuText;
            TryBindAndRefresh();
        }

        private void OnDisable()
        {
            L.OnLanguageChanged -= RefreshLocalizedMenuText;
        }

        private void Start()
        {
            TryBindAndRefresh();
        }

        private void EnsureEventSystem()
        {
            EventSystem es = Object.FindFirstObjectByType<EventSystem>();
            if (es == null)
            {
                GameObject esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<EventSystem>();
            }

            // Input System package requires InputSystemUIInputModule for runtime pointer and keyboard events.
            StandaloneInputModule legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) Destroy(legacy);
            if (es.GetComponent<InputSystemUIInputModule>() == null)
                es.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        private void TryBindAndRefresh()
        {
            if (uiDocument == null) uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            if (boundRoot == uiDocument.rootVisualElement) return;

            BindUI();
            RebuildRadialCampaign();
            ApplySettingsValuesToUI();
            UpdateTitleAndMetaLabels();
            ShowEmptyPreview();
            FocusTitleButton(0);
        }

        private void BindUI()
        {
            VisualElement root = uiDocument.rootVisualElement;
            boundRoot = root;
            menuRoot = root.Q<VisualElement>("menu-root");
            ApplyReduceMotionClass();

            titleView = root.Q<VisualElement>("title-view");
            titleProgressLabel = root.Q<Label>("title-progress-label");
            btnEnter = root.Q<Button>("btn-enter");
            btnEnterLabel = root.Q<Label>("btn-enter-label");
            btnChapters = root.Q<Button>("btn-chapters");
            btnTitleShowcase = root.Q<Button>("btn-title-showcase");
            btnTitleSettings = root.Q<Button>("btn-title-settings");
            btnShutdown = root.Q<Button>("btn-shutdown");
            titleNavButtons = new[] { btnEnter, btnChapters, btnTitleShowcase, btnTitleSettings, btnShutdown };

            ribbonView = root.Q<VisualElement>("ribbon-view");
            ribbonMetaLabel = root.Q<Label>("ribbon-meta-label");
            campaignRadial = root.Q<CampaignRadialSelector>("campaign-radial");
            btnRibbonSettings = root.Q<Button>("btn-ribbon-settings");
            btnRibbonBack = root.Q<Button>("btn-ribbon-back");
            if (btnRibbonSettings != null) btnRibbonSettings.focusable = false;
            if (btnRibbonBack != null) btnRibbonBack.focusable = false;

            chamberPreviewCard = root.Q<VisualElement>("chamber-preview-card");
            chapterFooterLabel = root.Q<Label>("chapter-footer-label");
            previewTitleLabel = root.Q<Label>("preview-title-label");
            for (int i = 0; i < previewStars.Length; i++) previewStars[i] = root.Q<UiVectorIcon>("preview-star-" + i);
            previewMemoryLabel = root.Q<Label>("preview-memory-label");
            previewEmptyLabel = root.Q<Label>("preview-empty-label");
            btnPreviewLaunch = root.Q<Button>("btn-preview-launch");
            previewLaunchLabel = root.Q<Label>("preview-launch-label");
            previewLaunchIcon = root.Q<UiVectorIcon>("preview-launch-icon");
            if (btnPreviewLaunch != null) btnPreviewLaunch.focusable = false;

            settingsOverlay = root.Q<VisualElement>("settings-overlay");
            sliderVolume = root.Q<Slider>("slider-volume");
            valVolume = root.Q<Label>("val-volume");
            sliderSensitivity = root.Q<Slider>("slider-sensitivity");
            valSensitivity = root.Q<Label>("val-sensitivity");
            toggleReduceMotion = root.Q<Toggle>("toggle-reduce-motion");
            btnLanguage = root.Q<Button>("btn-language");
            btnResetSave = root.Q<Button>("btn-reset-save");
            btnSettingsClose = root.Q<Button>("btn-settings-close");

            for (int i = 0; i < titleNavButtons.Length; i++)
            {
                int idx = i;
                Button b = titleNavButtons[i];
                if (b == null) continue;
                b.RegisterCallback<PointerEnterEvent>(_ =>
                {
                    focusedTitleIndex = idx;
                    SimulationAudio.PlayMenuHover();
                });
                b.RegisterCallback<FocusInEvent>(_ => focusedTitleIndex = idx);
            }

            if (btnEnter != null) btnEnter.clicked += LaunchActiveChamberFromTitle;
            if (btnChapters != null) btnChapters.clicked += OpenChapterIndex;
            if (btnTitleShowcase != null) btnTitleShowcase.clicked += LaunchShowcase;
            if (btnTitleSettings != null) btnTitleSettings.clicked += () => OpenSettings(btnTitleSettings);
            if (btnShutdown != null) btnShutdown.clicked += BeginShutdown;

            if (btnRibbonSettings != null) btnRibbonSettings.clicked += () => OpenSettings(btnRibbonSettings);
            if (btnRibbonBack != null) btnRibbonBack.clicked += ReturnToTitle;
            if (btnSettingsClose != null) btnSettingsClose.clicked += CloseSettings;
            if (btnLanguage != null) btnLanguage.clicked += ToggleLanguage;
            if (btnResetSave != null) btnResetSave.clicked += HandleResetSaveClicked;
            if (btnPreviewLaunch != null) btnPreviewLaunch.clicked += LaunchSelectedChamber;

            if (campaignRadial != null)
            {
                campaignRadial.PreviewChanged += OnRadialPreviewChanged;
                campaignRadial.LevelLaunched += LaunchLevel;
                campaignRadial.LevelRejected += RejectLevelSelection;
                campaignRadial.HighlightCleared += ShowEmptyPreview;
            }

            if (sliderVolume != null) sliderVolume.RegisterValueChangedCallback(evt =>
            {
                SimulationSettings.MasterVolume = evt.newValue / 100f;
                if (valVolume != null) valVolume.text = Mathf.RoundToInt(evt.newValue) + "%";
            });
            if (sliderSensitivity != null) sliderSensitivity.RegisterValueChangedCallback(evt =>
            {
                SimulationSettings.CameraSensitivity = evt.newValue / 100f;
                if (valSensitivity != null) valSensitivity.text = (evt.newValue / 100f).ToString("0.0") + "x";
            });
            if (toggleReduceMotion != null) toggleReduceMotion.RegisterValueChangedCallback(evt =>
            {
                SimulationSettings.ReduceMotion = evt.newValue;
                ApplyReduceMotionClass();
                SimulationAudio.PlayMenuHover();
            });
        }

        private void FocusTitleButton(int index)
        {
            if (titleNavButtons == null || titleNavButtons.Length == 0) return;
            focusedTitleIndex = (index + titleNavButtons.Length) % titleNavButtons.Length;
            titleNavButtons[focusedTitleIndex]?.Focus();
        }

        private void ActivateTitleButton(int index)
        {
            if (titleNavButtons == null || index < 0 || index >= titleNavButtons.Length) return;
            Button target = titleNavButtons[index];
            if (target == btnEnter) LaunchActiveChamberFromTitle();
            else if (target == btnChapters) OpenChapterIndex();
            else if (target == btnTitleShowcase) LaunchShowcase();
            else if (target == btnTitleSettings) OpenSettings(btnTitleSettings);
            else if (target == btnShutdown) BeginShutdown();
        }

        private void LoadLevelCatalogTitles()
        {
            for (int c = 1; c <= ChapterCount; c++)
            {
                int count = SimulationProgress.GetLevelsInChapter(c);
                for (int l = 1; l <= count; l++)
                {
                    string scene = LevelSceneName(c, l);
                    LevelInfo info = Resources.Load<LevelInfo>("Levels/" + scene);
                    if (info != null && !string.IsNullOrEmpty(info.Title))
                    {
                        levelTitles[c, l] = info.Title;
                        continue;
                    }
                    for (int k = 0; k < SimulationProgress.Catalog.Length; k++)
                    {
                        if (SimulationProgress.Catalog[k].chapter == c && SimulationProgress.Catalog[k].level == l)
                        {
                            levelTitles[c, l] = SimulationProgress.CampaignTitle(SimulationProgress.Catalog[k]);
                            break;
                        }
                    }
                }
            }
        }

        private void RefreshCurrentProgress()
        {
            var highest = SimulationProgress.GetHighestUnlocked();
            currentChapter = highest.chapter;
            currentLevel = highest.level;
            RebuildKeyboardLevels();
        }

        private void RebuildKeyboardLevels()
        {
            keyboardLevels.Clear();
            SimulationProgress.CampaignEntry[] catalog = SimulationProgress.Catalog;
            for (int i = 0; i < catalog.Length; i++)
                keyboardLevels.Add(new Vector2Int(catalog[i].chapter, catalog[i].level));
        }

        private Vector2Int KeyboardLevelForChapter(int chapter)
        {
            for (int i = 0; i < keyboardLevels.Count; i++)
            {
                if (keyboardLevels[i].x == chapter) return keyboardLevels[i];
            }
            return new Vector2Int(chapter, 1);
        }

        private void UpdateTitleAndMetaLabels()
        {
            if (btnEnterLabel != null)
            {
                btnEnterLabel.text = currentChapter > 1 || currentLevel > 1
                    ? L.T("ui.menu.continue", currentChapter, currentLevel)
                    : L.T("ui.menu.start");
            }

            int totalLevels = SimulationProgress.Catalog.Length;
            int totalCleared = 0;
            for (int c = 1; c <= ChapterCount; c++)
            {
                int count = SimulationProgress.GetLevelsInChapter(c);
                for (int l = 1; l <= count; l++)
                {
                    if (SimulationProgress.IsCleared(LevelSceneName(c, l))) totalCleared++;
                }
            }

            int fragments = SimulationProgress.GetCollectedFragmentCount();
            if (titleProgressLabel != null)
                titleProgressLabel.text = L.T("ui.menu.progress.title", totalCleared, totalLevels, fragments);
            if (ribbonMetaLabel != null)
                ribbonMetaLabel.text = L.T("ui.menu.progress.ribbon", totalCleared, totalLevels, fragments);
        }

        private void RebuildRadialCampaign()
        {
            if (campaignRadial == null) return;

            var chapters = new List<CampaignRadialChapter>();
            for (int c = 1; c <= ChapterCount; c++)
            {
                var chapter = new CampaignRadialChapter { Index = c };

                int count = SimulationProgress.GetLevelsInChapter(c);
                for (int l = 1; l <= count; l++)
                {
                    string sceneName = LevelSceneName(c, l);
                    bool unlocked = SimulationProgress.IsUnlocked(c, l);
                    bool cleared = SimulationProgress.IsCleared(sceneName);
                    if (cleared) chapter.ClearedCount++;

                    chapter.Levels.Add(new CampaignRadialLevel
                    {
                        Chapter = c,
                        Level = l,
                        Code = c + "." + l,
                        Unlocked = unlocked,
                        Cleared = cleared,
                        Current = c == currentChapter && l == currentLevel && !cleared,
                        Stars = cleared ? SimulationProgress.GetStars(sceneName) : 0
                    });
                }

                chapters.Add(chapter);
            }

            campaignRadial.SetCampaign(chapters, currentChapter, currentLevel);
            campaignRadial.SetSelection(selectedChapter, selectedLevel);
        }

        private void ApplyReduceMotionClass()
        {
            if (menuRoot != null)
                menuRoot.EnableInClassList("menu-root--reduce-motion", SimulationSettings.ReduceMotion);
        }

        private void OnRadialPreviewChanged(int chapter, int level)
        {
            previewChapter = chapter;
            previewLevel = level;
            UpdatePreviewCard(chapter, level);
            UpdateChapterFooter(chapter);
            UpdateRadialCenter(chapter);
            SimulationAudio.PlayMenuHover();
        }

        private void UpdateChapterFooter(int chapter)
        {
            if (chapterFooterLabel == null) return;
            string theme = chapter > 0 && chapter < SimulationProgress.ChapterNames.Length
                ? SimulationProgress.ChapterName(chapter) : L.T("ui.menu.archive_short");
            chapterFooterLabel.text = L.T("ui.menu.chapter_footer", chapter, theme);
        }

        private void ShowEmptyPreview()
        {
            UpdateChapterFooter(currentChapter);
            if (chamberPreviewCard != null) chamberPreviewCard.style.display = DisplayStyle.None;
            if (previewEmptyLabel != null) previewEmptyLabel.style.display = DisplayStyle.Flex;
            campaignRadial?.SetCenterNumber(currentChapter.ToString());
        }

        private void RejectLevelSelection()
        {
            SimulationAudio.PlayAnchorReject(Vector3.zero);
        }

        private void LaunchLevel(int chapter, int level)
        {
            if (!SimulationProgress.IsUnlocked(chapter, level))
            {
                RejectLevelSelection();
                return;
            }

            selectedChapter = chapter;
            selectedLevel = level;
            previewChapter = chapter;
            previewLevel = level;
            campaignRadial?.SetSelection(chapter, level);
            SimulationAudio.PlayMenuClick();
            LaunchChamber(LevelSceneName(chapter, level));
        }

        private void UpdateRadialCenter(int chapter)
        {
            campaignRadial?.SetCenterNumber(chapter.ToString());
        }

        private void UpdatePreviewCard(int chapter, int level)
        {
            if (chamberPreviewCard != null) chamberPreviewCard.style.display = DisplayStyle.Flex;
            if (previewEmptyLabel != null) previewEmptyLabel.style.display = DisplayStyle.None;

            string sceneName = LevelSceneName(chapter, level);
            bool unlocked = SimulationProgress.IsUnlocked(chapter, level);
            bool cleared = SimulationProgress.IsCleared(sceneName);
            string title = levelTitles[chapter, level];
            if (string.IsNullOrEmpty(title)) title = L.T("ui.menu.room_fallback", chapter, level);
            Color chapColor = ChapterAccentColor(chapter);

            if (previewTitleLabel != null)
                previewTitleLabel.text = L.T("ui.menu.preview.title", chapter, level, title);

            int stars = cleared ? Mathf.Clamp(SimulationProgress.GetStars(sceneName), 0, 3) : 0;
            for (int i = 0; i < previewStars.Length; i++)
            {
                UiVectorIcon star = previewStars[i];
                if (star == null) continue;
                star.IconKind = i < stars ? UiIconKind.StarFilled : UiIconKind.StarOutline;
                star.SetTint(i < stars ? PaletteSettings.Green : PaletteSettings.Yellow);
            }

            bool hasMemory = unlocked && SimulationProgress.HasFragment(sceneName);
            if (previewMemoryLabel != null)
                previewMemoryLabel.style.display = hasMemory ? DisplayStyle.Flex : DisplayStyle.None;

            if (btnPreviewLaunch != null)
            {
                btnPreviewLaunch.style.display = DisplayStyle.Flex;
                btnPreviewLaunch.SetEnabled(unlocked);
                btnPreviewLaunch.style.backgroundColor = unlocked ? chapColor : PaletteSettings.Surface1;
                btnPreviewLaunch.style.color = unlocked ? PaletteSettings.Crust : PaletteSettings.Subtext0;
                if (previewLaunchIcon != null)
                {
                    previewLaunchIcon.IconKind = unlocked ? UiIconKind.Play : UiIconKind.Lock;
                    previewLaunchIcon.SetTint(unlocked ? PaletteSettings.Crust : PaletteSettings.TextPrimary);
                }
                if (previewLaunchLabel != null)
                    previewLaunchLabel.text = cleared
                        ? L.T("ui.menu.preview.launch.replay")
                        : (unlocked ? L.T("ui.menu.preview.launch.start") : L.T("ui.menu.preview.launch.locked"));
            }
        }

        private void Update()
        {
            if (boundRoot == null) TryBindAndRefresh();

            campaignRadial?.Tick(Time.unscaledDeltaTime, SimulationSettings.ReduceMotion);
            HandleKeyboard();

            if (state == MenuState.PortalLaunch)
                UpdateStaticPortalLaunch();

            if (resetConfirmUntil > 0f && Time.unscaledTime > resetConfirmUntil)
            {
                resetConfirmUntil = 0f;
                if (btnResetSave != null) btnResetSave.text = L.T("ui.menu.settings.reset");
            }
        }

        private void HandleKeyboard()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;

            if (settingsOpen)
            {
                if (kb.escapeKey.wasPressedThisFrame) CloseSettings();
                return;
            }

            if (state == MenuState.Title)
            {
                if (kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame)
                {
                    FocusTitleButton(focusedTitleIndex + 1);
                    SimulationAudio.PlayMenuHover();
                }
                else if (kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame)
                {
                    FocusTitleButton(focusedTitleIndex - 1);
                    SimulationAudio.PlayMenuHover();
                }
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame)
                {
                    ActivateTitleButton(focusedTitleIndex);
                }
                return;
            }

            if (state != MenuState.LevelMenu) return;

            if (kb.f9Key.wasPressedThisFrame)
                ExecuteResetProgress();
            else if (kb.escapeKey.wasPressedThisFrame)
                ReturnToTitle();
            else
                HandleRadialKeyboard(kb);
        }

        private void HandleRadialKeyboard(Keyboard kb)
        {
            if (campaignRadial == null) return;

            bool right = kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame;
            bool horizontal = right || kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame;
            bool up = kb.upArrowKey.wasPressedThisFrame || kb.wKey.wasPressedThisFrame;
            bool down = kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
            bool confirm = kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame;

            if (!campaignRadial.HasActiveHighlight && !horizontal && !up && !down) return;

            if (!campaignRadial.HasActiveHighlight)
            {
                campaignRadial.SetFocus(currentChapter, currentLevel, true);
                return;
            }

            int chapter = campaignRadial.ActiveChapterIndex;
            int level = campaignRadial.ActiveLevelIndex;

            if (horizontal && keyboardLevels.Count > 0)
            {
                int index = -1;
                for (int i = 0; i < keyboardLevels.Count; i++)
                {
                    if (keyboardLevels[i].x == chapter && keyboardLevels[i].y == level)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0) return;

                int next = index + (right ? 1 : -1);
                if (next < 0 || next >= keyboardLevels.Count) return;
                Vector2Int target = keyboardLevels[next];
                if (!SimulationProgress.IsUnlocked(target.x, target.y)) return;
                campaignRadial.SetFocus(target.x, target.y, true);
                return;
            }

            if (up || down)
            {
                int nextChapter = chapter + (down ? 1 : -1);
                if (nextChapter < 1 || nextChapter > ChapterCount) return;
                Vector2Int target = KeyboardLevelForChapter(nextChapter);
                if (!SimulationProgress.IsUnlocked(target.x, target.y)) return;
                campaignRadial.SetFocus(target.x, target.y, true);
                return;
            }

            if (confirm && chapter > 0 && level > 0)
                LaunchLevel(chapter, level);
        }

        private void LaunchActiveChamberFromTitle()
        {
            if (state != MenuState.Title || settingsOpen) return;
            selectedChapter = currentChapter;
            selectedLevel = currentLevel;
            LaunchChamber(LevelSceneName(currentChapter, currentLevel));
        }

        private void OpenChapterIndex()
        {
            if (state != MenuState.Title || settingsOpen) return;
            SimulationAudio.PlayMenuClick();
            state = MenuState.LevelMenu;

            if (cameraOrbit != null) cameraOrbit.SetRibbonOpen(true);
            if (titleView != null) titleView.AddToClassList("title-view--hidden");
            if (ribbonView != null)
            {
                CancelRibbonFade();
                ribbonView.RemoveFromClassList("ribbon-view--hidden");
                ribbonView.AddToClassList("ribbon-view--fading");
                ribbonFadeSchedule = ribbonView.schedule.Execute(() =>
                {
                    ribbonFadeSchedule = null;
                    ribbonView.RemoveFromClassList("ribbon-view--fading");
                }).StartingIn(16);
            }

            previewChapter = currentChapter;
            previewLevel = currentLevel;
            if (campaignRadial != null)
            {
                campaignRadial.BeginEntryAnimation();
                campaignRadial.SetFocus(currentChapter, currentLevel, true);
                campaignRadial.Focus();
            }
        }

        private void LaunchShowcase()
        {
            if (state == MenuState.PortalLaunch || state == MenuState.Done || settingsOpen) return;
            selectedChapter = 5;
            LaunchChamber(SimulationProgress.ShowcaseSceneName);
        }

        private void LaunchSelectedChamber()
        {
            if (!SimulationProgress.IsUnlocked(selectedChapter, selectedLevel))
            {
                SimulationAudio.PlayAnchorReject(Vector3.zero);
                return;
            }
            LaunchChamber(LevelSceneName(selectedChapter, selectedLevel));
        }

        private void LaunchChamber(string sceneName)
        {
            if (state == MenuState.PortalLaunch || state == MenuState.Done) return;
            state = MenuState.PortalLaunch;
            portalSceneName = sceneName;
            portalStartTime = Time.unscaledTime;
            portalSceneDispatched = false;

            SimulationAudio.PlayModeSwitch(true);
            SimulationVFX.PlaySpawnAperture(new Vector3(-0.7f, 0.3f, -0.5f), ChapterAccentColor(selectedChapter));
            SimulationVisualBridge.TriggerFloorRipple(new Vector3(-0.7f, 0f, -0.5f), ChapterAccentColor(selectedChapter), 8.5f, 1.3f, 0.65f);

            if (titleView != null) titleView.AddToClassList("title-view--hidden");
            if (ribbonView != null)
            {
                CancelRibbonFade();
                ribbonView.RemoveFromClassList("ribbon-view--hidden");
                ribbonView.AddToClassList("ribbon-view--fading");
            }
        }

        private void CancelRibbonFade()
        {
            if (ribbonFadeSchedule == null) return;
            ribbonFadeSchedule.Pause();
            ribbonFadeSchedule = null;
        }

        private void UpdateStaticPortalLaunch()
        {
            float duration = SimulationSettings.ReduceMotion ? 0.08f : Mathf.Max(0.24f, portalLaunchDuration);
            float elapsed = Time.unscaledTime - portalStartTime;
            float camProgress = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - camProgress, 3f);

            if (cameraOrbit != null)
                cameraOrbit.SetPortalDive(eased);

            if (!portalSceneDispatched && elapsed >= duration)
            {
                portalSceneDispatched = true;
                state = MenuState.Done;
                Texture2D staticSnapshot = CaptureRearRectCameraSnapshot();
                StaticPortalTransition.Begin(portalSceneName, staticSnapshot, 0.05f, 0.26f);
            }
        }

        private static Texture2D CaptureRearRectCameraSnapshot()
        {
            Camera cam = Camera.main;
            if (cam == null) cam = Object.FindFirstObjectByType<Camera>();
            if (cam == null) return null;

            int width = Mathf.Clamp(Screen.width, 640, 1920);
            int height = Mathf.Clamp(Screen.height, 360, 1080);

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.Default);
            RenderTexture prevTarget = cam.targetTexture;
            RenderTexture prevActive = RenderTexture.active;

            Texture2D capture = null;
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;

                capture = new Texture2D(width, height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                capture.Apply(false, false);
            }
            finally
            {
                cam.targetTexture = prevTarget;
                RenderTexture.active = prevActive;
                RenderTexture.ReleaseTemporary(rt);
            }
            return capture;
        }

        private static Color ChapterAccentColor(int chapter)
        {
            switch (chapter)
            {
                case 1: return PaletteSettings.Sapphire;
                case 2: return PaletteSettings.Lavender;
                case 3: return PaletteSettings.Peach;
                case 4: return PaletteSettings.Teal;
                default: return PaletteSettings.Rosewater;
            }
        }

        private void ExecuteResetProgress()
        {
            SimulationProgress.ResetAll();
            RefreshCurrentProgress();
            selectedChapter = currentChapter;
            selectedLevel = currentLevel;
            UpdateTitleAndMetaLabels();
            RebuildRadialCampaign();
            campaignRadial?.ClearActive();
            ShowEmptyPreview();
            SimulationAudio.PlayMenuSmash();
        }

        private void HandleResetSaveClicked()
        {
            if (resetConfirmUntil <= Time.unscaledTime)
            {
                resetConfirmUntil = Time.unscaledTime + 3.0f;
                if (btnResetSave != null) btnResetSave.text = L.T("ui.menu.settings.reset_confirm");
                SimulationAudio.PlayMenuHover();
                return;
            }
            resetConfirmUntil = 0f;
            if (btnResetSave != null) btnResetSave.text = L.T("ui.menu.settings.reset");
            ExecuteResetProgress();
        }

        private void OpenSettings(Button triggerButton)
        {
            settingsReturnFocus = triggerButton;
            settingsOpen = true;
            ApplySettingsValuesToUI();
            if (settingsOverlay != null) settingsOverlay.RemoveFromClassList("settings-overlay--hidden");
            SimulationAudio.PlayMenuClick();
            if (btnSettingsClose != null) btnSettingsClose.Focus();
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            resetConfirmUntil = 0f;
            if (btnResetSave != null) btnResetSave.text = L.T("ui.menu.settings.reset");
            if (settingsOverlay != null) settingsOverlay.AddToClassList("settings-overlay--hidden");
            SimulationAudio.PlayMenuHover();
            if (settingsReturnFocus != null) settingsReturnFocus.Focus();
            else if (state == MenuState.Title) FocusTitleButton(focusedTitleIndex);
        }

        private void ApplySettingsValuesToUI()
        {
            float vol = SimulationSettings.MasterVolume * 100f;
            if (sliderVolume != null) sliderVolume.SetValueWithoutNotify(vol);
            if (valVolume != null) valVolume.text = Mathf.RoundToInt(vol) + "%";

            float sens = SimulationSettings.CameraSensitivity * 100f;
            if (sliderSensitivity != null) sliderSensitivity.SetValueWithoutNotify(sens);
            if (valSensitivity != null) valSensitivity.text = SimulationSettings.CameraSensitivity.ToString("0.0") + "x";

            if (toggleReduceMotion != null) toggleReduceMotion.SetValueWithoutNotify(SimulationSettings.ReduceMotion);
            if (btnLanguage != null)
                btnLanguage.text = L.T(L.Language == GameLanguage.English
                    ? "ui.language.english"
                    : "ui.language.indonesian");
            if (btnResetSave != null) btnResetSave.text = L.T("ui.menu.settings.reset");
        }

        private void ToggleLanguage()
        {
            L.SetLanguage(L.Language == GameLanguage.English ? GameLanguage.Indonesian : GameLanguage.English);
            SimulationAudio.PlayMenuClick();
        }

        private void RefreshLocalizedMenuText()
        {
            if (uiDocument == null || boundRoot == null)
            {
                TryBindAndRefresh();
                return;
            }

            LoadLevelCatalogTitles();
            UpdateTitleAndMetaLabels();
            bool wasActive = campaignRadial != null && campaignRadial.HasActiveHighlight;
            RebuildRadialCampaign();
            if (wasActive && campaignRadial != null)
            {
                campaignRadial.SetFocus(previewChapter, previewLevel, true);
                UpdatePreviewCard(previewChapter, previewLevel);
                UpdateChapterFooter(previewChapter);
                UpdateRadialCenter(previewChapter);
            }
            else
            {
                ShowEmptyPreview();
            }
            ApplySettingsValuesToUI();
        }

        private void ReturnToTitle()
        {
            if (state != MenuState.LevelMenu) return;
            state = MenuState.Title;
            if (cameraOrbit != null) cameraOrbit.SetRibbonOpen(false);
            if (ribbonView != null)
            {
                CancelRibbonFade();
                ribbonView.AddToClassList("ribbon-view--fading");
                ribbonFadeSchedule = ribbonView.schedule.Execute(() =>
                {
                    ribbonFadeSchedule = null;
                    ribbonView.AddToClassList("ribbon-view--hidden");
                }).StartingIn(200);
            }
            if (titleView != null) titleView.RemoveFromClassList("title-view--hidden");
            SimulationAudio.PlayMenuHover();
            FocusTitleButton(1);
        }

        private void BeginShutdown()
        {
            if (state != MenuState.Title || settingsOpen) return;
            state = MenuState.Done;
            SimulationAudio.PlayMenuClick();
            SceneTransition.Quit(0.18f, 0.35f);
        }

        private string LevelSceneName(int chapter, int level) => string.Format(levelSceneFormat, chapter, level);
    }
}
