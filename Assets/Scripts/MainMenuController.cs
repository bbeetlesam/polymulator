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
        private const float ClusterStride = 602f;

        [SerializeField] private string levelSceneFormat = "Level{0}.{1}";
        [SerializeField] private float portalLaunchDuration = 0.44f;

        private UIDocument uiDocument;
        private VisualElement boundRoot;
        private VisualElement menuRoot;
        private MainMenuCameraOrbit cameraOrbit;

        private VisualElement titleView;
        private Label titleIterLabel;
        private Label titleProgressLabel;
        private Button btnEnter;
        private Button btnChapters;
        private Button btnTitleShowcase;
        private Button btnTitleSettings;
        private Button btnShutdown;
        private Button[] titleNavButtons;
        private int focusedTitleIndex;

        private VisualElement ribbonView;
        private VisualElement ribbonViewport;
        private VisualElement ribbonTrack;
        private Label ribbonMetaLabel;
        private Button btnRibbonShowcase;
        private Button btnDemoToggle;
        private Button btnRibbonSettings;
        private Button btnRibbonBack;

        private Label previewChapterLabel;
        private Label previewTitleLabel;
        private Label previewStatusLabel;
        private Button btnPreviewLaunch;

        private VisualElement settingsOverlay;
        private Slider sliderVolume;
        private Label valVolume;
        private Slider sliderSensitivity;
        private Label valSensitivity;
        private Toggle toggleReduceMotion;
        private Button btnResetSave;
        private Button btnSettingsClose;
        private Button settingsReturnFocus;

        private readonly Dictionary<string, Button> dotButtonsByScene = new Dictionary<string, Button>();
        private readonly string[,] levelTitles = new string[ChapterCount + 1, 5];

        private MenuState state = MenuState.Title;
        private int currentChapter = 1;
        private int currentLevel = 1;
        private int selectedChapter = 1;
        private int selectedLevel = 1;
        private bool settingsOpen;
        private float resetConfirmUntil;

        private float horizontalScroll;
        private float targetScroll;
        private bool draggingRibbon;
        private float dragStartX;
        private float dragStartScroll;

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
            TryBindAndRefresh();
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
            RebuildRibbonNodes();
            ApplySettingsValuesToUI();
            UpdateTitleAndMetaLabels();
            UpdatePreviewCard(selectedChapter, selectedLevel);
            FocusTitleButton(0);
        }

        private void BindUI()
        {
            VisualElement root = uiDocument.rootVisualElement;
            boundRoot = root;
            menuRoot = root.Q<VisualElement>("menu-root");
            ApplyReduceMotionClass();

            titleView = root.Q<VisualElement>("title-view");
            titleIterLabel = root.Q<Label>("title-iter-label");
            titleProgressLabel = root.Q<Label>("title-progress-label");
            btnEnter = root.Q<Button>("btn-enter");
            btnChapters = root.Q<Button>("btn-chapters");
            btnTitleShowcase = root.Q<Button>("btn-title-showcase");
            btnTitleSettings = root.Q<Button>("btn-title-settings");
            btnShutdown = root.Q<Button>("btn-shutdown");
            titleNavButtons = new[] { btnEnter, btnChapters, btnTitleShowcase, btnTitleSettings, btnShutdown };

            ribbonView = root.Q<VisualElement>("ribbon-view");
            ribbonViewport = root.Q<VisualElement>("ribbon-viewport");
            ribbonTrack = root.Q<VisualElement>("ribbon-track");
            ribbonMetaLabel = root.Q<Label>("ribbon-meta-label");
            btnRibbonShowcase = root.Q<Button>("btn-ribbon-showcase");
            btnDemoToggle = root.Q<Button>("btn-demo-toggle");
            btnRibbonSettings = root.Q<Button>("btn-ribbon-settings");
            btnRibbonBack = root.Q<Button>("btn-ribbon-back");

            previewChapterLabel = root.Q<Label>("preview-chapter-label");
            previewTitleLabel = root.Q<Label>("preview-title-label");
            previewStatusLabel = root.Q<Label>("preview-status-label");
            btnPreviewLaunch = root.Q<Button>("btn-preview-launch");

            settingsOverlay = root.Q<VisualElement>("settings-overlay");
            sliderVolume = root.Q<Slider>("slider-volume");
            valVolume = root.Q<Label>("val-volume");
            sliderSensitivity = root.Q<Slider>("slider-sensitivity");
            valSensitivity = root.Q<Label>("val-sensitivity");
            toggleReduceMotion = root.Q<Toggle>("toggle-reduce-motion");
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

            if (btnRibbonShowcase != null) btnRibbonShowcase.clicked += LaunchShowcase;
            if (btnDemoToggle != null) btnDemoToggle.clicked += ToggleDemoAccess;
            if (btnRibbonSettings != null) btnRibbonSettings.clicked += () => OpenSettings(btnRibbonSettings);
            if (btnRibbonBack != null) btnRibbonBack.clicked += ReturnToTitle;
            if (btnSettingsClose != null) btnSettingsClose.clicked += CloseSettings;
            if (btnResetSave != null) btnResetSave.clicked += HandleResetSaveClicked;
            if (btnPreviewLaunch != null) btnPreviewLaunch.clicked += LaunchSelectedChamber;

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

            if (ribbonViewport != null)
            {
                ribbonViewport.RegisterCallback<PointerDownEvent>(OnRibbonPointerDown);
                ribbonViewport.RegisterCallback<PointerMoveEvent>(OnRibbonPointerMove);
                ribbonViewport.RegisterCallback<PointerUpEvent>(OnRibbonPointerUp);
                ribbonViewport.RegisterCallback<WheelEvent>(OnRibbonWheel);
            }
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
                            levelTitles[c, l] = SimulationProgress.Catalog[k].fallbackTitle;
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
        }

        private void UpdateTitleAndMetaLabels()
        {
            if (titleIterLabel != null)
                titleIterLabel.text = "KOTA TEPI  /  ARSIP SPASIAL";

            if (btnEnter != null)
            {
                btnEnter.text = currentChapter > 1 || currentLevel > 1
                    ? string.Format("Lanjutkan Ruangan {0}.{1}", currentChapter, currentLevel)
                    : "Mulai Simulasi  1.1";
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
                titleProgressLabel.text = string.Format("{0}/{1} Ruangan Selesai  •  {2} Memori",
                    totalCleared, totalLevels, fragments);
            if (ribbonMetaLabel != null)
                ribbonMetaLabel.text = string.Format("{0}/{1} Ruangan  •  {2} Memori",
                    totalCleared, totalLevels, fragments);

            if (btnDemoToggle != null)
            {
                bool on = SimulationProgress.IsDemoUnlocked;
                btnDemoToggle.text = on ? "[F2] Buka Semua: Nyala" : "[F2] Buka Semua: Mati";
                btnDemoToggle.EnableInClassList("top-action-btn--on", on);
            }
        }

        private void RebuildRibbonNodes()
        {
            if (ribbonTrack == null) return;
            ribbonTrack.Clear();
            dotButtonsByScene.Clear();

            for (int c = 1; c <= ChapterCount; c++)
            {
                Color chapColor = ChapterAccentColor(c);

                VisualElement cluster = new VisualElement();
                cluster.AddToClassList("chapter-cluster");

                bool reached = c <= currentChapter || SimulationProgress.IsDemoUnlocked;
                VisualElement card = new VisualElement();
                card.AddToClassList("chapter-node-card");
                if (reached) card.AddToClassList("chapter-node-card--reached");
                card.style.borderTopColor = chapColor;

                Label sectorLbl = new Label("CHAPTER 0" + c);
                sectorLbl.AddToClassList("chapter-sector-pill");
                if (reached) sectorLbl.style.color = chapColor;
                card.Add(sectorLbl);

                Label numLbl = new Label(c.ToString());
                numLbl.AddToClassList("chapter-node-num");
                if (reached) numLbl.style.color = chapColor;
                card.Add(numLbl);

                string themeName = c < SimulationProgress.ChapterNames.Length
                    ? SimulationProgress.ChapterNames[c] : string.Empty;
                Label themeLbl = new Label(themeName);
                themeLbl.AddToClassList("chapter-node-theme");
                card.Add(themeLbl);

                int count = SimulationProgress.GetLevelsInChapter(c);
                int clearedInChap = 0;
                for (int k = 1; k <= count; k++)
                {
                    if (SimulationProgress.IsCleared(LevelSceneName(c, k))) clearedInChap++;
                }

                VisualElement barBg = new VisualElement();
                barBg.AddToClassList("chapter-node-bar-bg");
                VisualElement barFill = new VisualElement();
                barFill.AddToClassList("chapter-node-bar-fill");
                barFill.style.backgroundColor = chapColor;
                barFill.style.width = Length.Percent(count > 0 ? (clearedInChap / (float)count) * 100f : 0f);
                barBg.Add(barFill);
                card.Add(barBg);

                cluster.Add(card);

                VisualElement rail = new VisualElement();
                rail.AddToClassList("segment-rail");

                VisualElement lineBg = new VisualElement();
                lineBg.AddToClassList("segment-line-bg");
                rail.Add(lineBg);

                VisualElement lineFill = new VisualElement();
                lineFill.AddToClassList("segment-line-fill");
                lineFill.style.backgroundColor = chapColor;
                float fillPct = 0f;
                if (c < currentChapter || SimulationProgress.IsDemoUnlocked) fillPct = 100f;
                else if (c == currentChapter && count > 0) fillPct = (currentLevel / (float)(count + 1)) * 100f;
                lineFill.style.width = Length.Percent(fillPct);
                rail.Add(lineFill);

                for (int l = 1; l <= count; l++)
                {
                    int chap = c;
                    int lvl = l;
                    string sceneName = LevelSceneName(chap, lvl);
                    bool unlocked = SimulationProgress.IsUnlocked(chap, lvl);
                    bool cleared = SimulationProgress.IsCleared(sceneName);
                    bool isTarget = chap == currentChapter && lvl == currentLevel && !cleared;

                    VisualElement dotWrap = new VisualElement();
                    dotWrap.AddToClassList("level-dot-wrap");

                    if (isTarget)
                    {
                        Label nowBadge = new Label("AKTIF");
                        nowBadge.AddToClassList("level-dot-badge");
                        nowBadge.style.backgroundColor = chapColor;
                        dotWrap.Add(nowBadge);
                    }

                    Label codeLbl = new Label(chap + "." + lvl);
                    codeLbl.AddToClassList("level-dot-code");
                    if (isTarget) codeLbl.style.color = chapColor;
                    dotWrap.Add(codeLbl);

                    Button dotBtn = new Button();
                    dotBtn.AddToClassList("level-dot-btn");
                    if (cleared) dotBtn.AddToClassList("level-dot-btn--cleared");
                    else if (isTarget) dotBtn.AddToClassList("level-dot-btn--current");
                    else if (unlocked) dotBtn.AddToClassList("level-dot-btn--unlocked");
                    if (isTarget) dotBtn.style.backgroundColor = chapColor;

                    dotBtn.RegisterCallback<PointerEnterEvent>(_ =>
                    {
                        SimulationAudio.PlayMenuHover();
                        SelectChamberPreview(chap, lvl);
                    });
                    dotBtn.RegisterCallback<FocusInEvent>(_ => SelectChamberPreview(chap, lvl));
                    dotBtn.clicked += () =>
                    {
                        if (!unlocked)
                        {
                            SimulationAudio.PlayAnchorReject(Vector3.zero);
                            return;
                        }
                        SelectChamberPreview(chap, lvl);
                        LaunchChamber(sceneName);
                    };

                    dotWrap.Add(dotBtn);
                    dotButtonsByScene[sceneName] = dotBtn;

                    if (cleared)
                    {
                        int stars = SimulationProgress.GetStars(sceneName);
                        Label starLbl = new Label(new string('★', stars));
                        starLbl.AddToClassList("level-dot-stars");
                        dotWrap.Add(starLbl);
                    }

                    rail.Add(dotWrap);
                }

                cluster.Add(rail);
                ribbonTrack.Add(cluster);
            }

            VisualElement scCard = new VisualElement();
            scCard.AddToClassList("chapter-node-card");
            scCard.AddToClassList("chapter-node-card--showcase");
            Label scSec = new Label("SANDBOX");
            scSec.AddToClassList("chapter-sector-pill");
            scSec.style.color = PaletteSettings.Teal;
            scCard.Add(scSec);
            Label scNum = new Label("S");
            scNum.AddToClassList("chapter-node-num");
            scNum.style.color = PaletteSettings.Teal;
            scCard.Add(scNum);
            Label scTheme = new Label("SHOWCASE");
            scTheme.AddToClassList("chapter-node-theme");
            scCard.Add(scTheme);
            Button scBtn = new Button(LaunchShowcase) { text = "Buka Demo" };
            scBtn.AddToClassList("showcase-launch-btn");
            scBtn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                SimulationAudio.PlayMenuHover();
                UpdateShowcasePreview();
            });
            scCard.Add(scBtn);
            ribbonTrack.Add(scCard);
        }

        private void ApplyReduceMotionClass()
        {
            if (menuRoot != null)
                menuRoot.EnableInClassList("menu-root--reduce-motion", SimulationSettings.ReduceMotion);
        }

        private void UpdateShowcasePreview()
        {
            Color teal = PaletteSettings.Teal;
            if (previewChapterLabel != null)
            {
                previewChapterLabel.text = "Ruang Showcase  •  Semua Mekanik Terbuka";
                previewChapterLabel.style.color = teal;
            }
            if (previewTitleLabel != null)
                previewTitleLabel.text = "Demo Lengkap POLYMULATOR";
            if (previewStatusLabel != null)
                previewStatusLabel.text = "Partition [1], Fusion [2], Telekinesis [F], Rotasi [R], dan Mode Fleksibel langsung aktif";
            if (btnPreviewLaunch != null)
            {
                btnPreviewLaunch.SetEnabled(true);
                btnPreviewLaunch.style.backgroundColor = teal;
                btnPreviewLaunch.style.color = PaletteSettings.Crust;
                btnPreviewLaunch.text = "Buka Showcase";
            }
        }

        private void SelectChamberPreview(int chapter, int level)
        {
            selectedChapter = chapter;
            selectedLevel = level;
            UpdatePreviewCard(chapter, level);
        }

        private void UpdatePreviewCard(int chapter, int level)
        {
            string sceneName = LevelSceneName(chapter, level);
            bool unlocked = SimulationProgress.IsUnlocked(chapter, level);
            bool cleared = SimulationProgress.IsCleared(sceneName);
            string title = levelTitles[chapter, level];
            if (string.IsNullOrEmpty(title)) title = "Ruangan " + chapter + "." + level;

            string chapterTheme = chapter < SimulationProgress.ChapterNames.Length
                ? SimulationProgress.ChapterNames[chapter] : "ARSIP";
            Color chapColor = ChapterAccentColor(chapter);

            if (previewChapterLabel != null)
            {
                previewChapterLabel.text = string.Format("Chapter {0}  •  {1}", chapter, chapterTheme);
                previewChapterLabel.style.color = chapColor;
            }
            if (previewTitleLabel != null)
                previewTitleLabel.text = string.Format("{0}.{1}  •  {2}", chapter, level, title);

            if (btnPreviewLaunch != null)
            {
                btnPreviewLaunch.SetEnabled(unlocked);
                btnPreviewLaunch.style.backgroundColor = unlocked ? chapColor : PaletteSettings.Surface1;
                btnPreviewLaunch.style.color = unlocked ? PaletteSettings.Crust : PaletteSettings.Subtext0;
                btnPreviewLaunch.text = cleared ? "Main Ulang" : (unlocked ? "Mulai" : "Terkunci");
            }

            if (previewStatusLabel != null)
            {
                if (!unlocked)
                {
                    previewStatusLabel.text = "Ruangan terkunci: selesaikan ruangan sebelumnya lebih dulu";
                }
                else if (cleared)
                {
                    int stars = SimulationProgress.GetStars(sceneName);
                    string starStr = new string('★', stars) + new string('☆', 3 - stars);
                    string mem = SimulationProgress.HasFragment(sceneName) ? "  •  Memori tersimpan" : "";
                    previewStatusLabel.text = string.Format("Selesai  [{0}]{1}", starStr, mem);
                }
                else
                {
                    previewStatusLabel.text = "Siap dimulai  •  Klik titik ruangan atau tekan Enter";
                }
            }
        }

        private void Update()
        {
            if (boundRoot == null) TryBindAndRefresh();

            HandleKeyboard();
            float dt = Time.unscaledDeltaTime;

            if (state == MenuState.LevelMenu && !draggingRibbon)
                UpdateRibbonScrollSmoothing(dt);
            else if (state == MenuState.PortalLaunch)
                UpdateStaticPortalLaunch();

            if (resetConfirmUntil > 0f && Time.unscaledTime > resetConfirmUntil)
            {
                resetConfirmUntil = 0f;
                if (btnResetSave != null) btnResetSave.text = "Reset Progres";
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

            if (kb.leftArrowKey.wasPressedThisFrame || kb.aKey.wasPressedThisFrame)
                NudgeRibbonScroll(-260f);
            else if (kb.rightArrowKey.wasPressedThisFrame || kb.dKey.wasPressedThisFrame)
                NudgeRibbonScroll(260f);
            else if (kb.f2Key.wasPressedThisFrame)
                ToggleDemoAccess();
            else if (kb.f9Key.wasPressedThisFrame)
                ExecuteResetProgress();
            else if (kb.escapeKey.wasPressedThisFrame)
                ReturnToTitle();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                LaunchSelectedChamber();
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
                ribbonView.RemoveFromClassList("ribbon-view--hidden");
                ribbonView.RemoveFromClassList("ribbon-view--fading");
            }

            Vector2 panelSize = GetPanelSize();
            float viewW = Mathf.Max(400f, panelSize.x - 88f);
            int levelCount = Mathf.Max(1, SimulationProgress.GetLevelsInChapter(currentChapter));
            float dotOffset = 420f * (currentLevel / (float)(levelCount + 1));
            float targetX = (currentChapter - 1) * ClusterStride + 182f + dotOffset;
            horizontalScroll = targetScroll = ClampScroll(targetX - viewW * 0.45f, viewW);
            if (ribbonTrack != null) ribbonTrack.style.left = -horizontalScroll;
            UpdateCameraScrollParallax(viewW);

            string curScene = LevelSceneName(currentChapter, currentLevel);
            if (dotButtonsByScene.TryGetValue(curScene, out Button activeDot))
                activeDot.Focus();
            else if (btnPreviewLaunch != null)
                btnPreviewLaunch.Focus();
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
            if (ribbonView != null) ribbonView.AddToClassList("ribbon-view--fading");
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

        private Vector2 GetPanelSize()
        {
            Vector2 panelSize = uiDocument != null && uiDocument.rootVisualElement != null
                ? uiDocument.rootVisualElement.worldBound.size : new Vector2(1920f, 1080f);
            return panelSize.x < 100f ? new Vector2(1920f, 1080f) : panelSize;
        }

        private void OnRibbonPointerDown(PointerDownEvent evt)
        {
            if (state != MenuState.LevelMenu || evt.button != 0) return;
            if (evt.target is Button) return;
            draggingRibbon = true;
            dragStartX = evt.position.x;
            dragStartScroll = horizontalScroll;
            targetScroll = horizontalScroll;
            ribbonViewport.CapturePointer(evt.pointerId);
        }

        private void OnRibbonPointerMove(PointerMoveEvent evt)
        {
            if (!draggingRibbon) return;
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1832f;
            float next = dragStartScroll - (evt.position.x - dragStartX);
            horizontalScroll = targetScroll = ClampScroll(next, viewW);
            if (ribbonTrack != null) ribbonTrack.style.left = -horizontalScroll;
            UpdateCameraScrollParallax(viewW);
        }

        private void OnRibbonPointerUp(PointerUpEvent evt)
        {
            if (!draggingRibbon) return;
            draggingRibbon = false;
            if (ribbonViewport.HasPointerCapture(evt.pointerId))
                ribbonViewport.ReleasePointer(evt.pointerId);
        }

        private void OnRibbonWheel(WheelEvent evt)
        {
            if (state != MenuState.LevelMenu) return;
            float delta = Mathf.Abs(evt.delta.y) >= Mathf.Abs(evt.delta.x) ? evt.delta.y : evt.delta.x;
            NudgeRibbonScroll(delta * 65f);
            evt.StopPropagation();
        }

        private void NudgeRibbonScroll(float delta)
        {
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1832f;
            targetScroll = ClampScroll(targetScroll + delta, viewW);
        }

        private void UpdateRibbonScrollSmoothing(float dt)
        {
            if (ribbonTrack == null || Mathf.Approximately(horizontalScroll, targetScroll)) return;
            float blend = 1f - Mathf.Exp(-14f * dt);
            horizontalScroll = Mathf.Lerp(horizontalScroll, targetScroll, blend);
            if (Mathf.Abs(targetScroll - horizontalScroll) < 0.5f) horizontalScroll = targetScroll;
            ribbonTrack.style.left = -horizontalScroll;
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1832f;
            UpdateCameraScrollParallax(viewW);
        }

        private void UpdateCameraScrollParallax(float viewportWidth)
        {
            if (cameraOrbit == null) return;
            float max = MaxScroll(viewportWidth);
            cameraOrbit.SetScrollFraction(Mathf.InverseLerp(-80f, max, horizontalScroll));
        }

        private static float MaxScroll(float viewportWidth)
        {
            float contentW = ChapterCount * ClusterStride + 260f;
            return Mathf.Max(120f, contentW - Mathf.Max(400f, viewportWidth) + 80f);
        }

        private static float ClampScroll(float value, float viewportWidth)
            => Mathf.Clamp(value, -80f, MaxScroll(viewportWidth));

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

        private void ToggleDemoAccess()
        {
            SimulationProgress.IsDemoUnlocked = !SimulationProgress.IsDemoUnlocked;
            RefreshCurrentProgress();
            UpdateTitleAndMetaLabels();
            RebuildRibbonNodes();
            UpdatePreviewCard(selectedChapter, selectedLevel);
            SimulationAudio.PlayMenuClick();
        }

        private void ExecuteResetProgress()
        {
            SimulationProgress.ResetAll();
            RefreshCurrentProgress();
            selectedChapter = currentChapter;
            selectedLevel = currentLevel;
            UpdateTitleAndMetaLabels();
            RebuildRibbonNodes();
            UpdatePreviewCard(selectedChapter, selectedLevel);
            SimulationAudio.PlayMenuSmash();
        }

        private void HandleResetSaveClicked()
        {
            if (resetConfirmUntil <= Time.unscaledTime)
            {
                resetConfirmUntil = Time.unscaledTime + 3.0f;
                if (btnResetSave != null) btnResetSave.text = "Klik Lagi untuk Hapus";
                SimulationAudio.PlayMenuHover();
                return;
            }
            resetConfirmUntil = 0f;
            if (btnResetSave != null) btnResetSave.text = "Reset Progres";
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
            if (btnResetSave != null) btnResetSave.text = "Reset Progres";
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
        }

        private void ReturnToTitle()
        {
            if (state != MenuState.LevelMenu) return;
            state = MenuState.Title;
            if (cameraOrbit != null) cameraOrbit.SetRibbonOpen(false);
            if (ribbonView != null) ribbonView.AddToClassList("ribbon-view--hidden");
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
