using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Polymulator
{
    /// <summary>
    /// Modern UI Toolkit Main Menu & Chapter Ribbon controller layered atop a live 3D facility diorama.
    /// Orchestrates the Player-POV camera & sphere smash-and-descend into an interactive chapter ribbon,
    /// and a 1/3-to-1/2 screen portal orb zoom transition directly into RECT's arrival camera.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuState { Title, FallingSmash, LevelMenu, PortalZoom, Done }

        private sealed class RibbonShard
        {
            public VisualElement element;
            public float x, y, vx, vy, angle, angularVel, life, maxLife;
        }

        private const int ChapterCount = 5;
        private const float ClusterStride = 574f; // 146 card + 12 margin + 416 rail

        [SerializeField] private string levelSceneFormat = "Level{0}.{1}";

        [Header("Transitions (seconds)")]
        [SerializeField] private float fallDuration = 0.92f;
        [SerializeField] private float portalZoomDuration = 0.58f;
        [SerializeField] private float sceneFadeDuration = 0.36f;

        private UIDocument uiDocument;
        private VisualElement boundRoot;
        private MainMenuCameraOrbit cameraOrbit;

        private VisualElement titleView;
        private Label titleIterLabel;
        private Button btnEnter;
        private Button btnTitleShowcase;
        private Button btnTitleSettings;
        private Button btnShutdown;

        private VisualElement ribbonView;
        private VisualElement ribbonViewport;
        private VisualElement ribbonTrack;
        private Label ribbonMetaLabel;
        private Button btnRibbonShowcase;
        private Button btnDemoToggle;
        private Button btnRibbonSettings;
        private Button btnRibbonBack;

        private VisualElement previewAccent;
        private Label previewChapterLabel;
        private Label previewTitleLabel;
        private Label previewStatusLabel;
        private Button btnPreviewLaunch;

        private VisualElement fxLayer;
        private VisualElement impactShockwave;
        private VisualElement fallingNode;
        private Label fallingNodeLabel;
        private VisualElement portalZoomOrb;
        private VisualElement portalZoomRing;
        private Label portalZoomLabel;

        private VisualElement settingsOverlay;
        private Slider sliderVolume;
        private Label valVolume;
        private Slider sliderSensitivity;
        private Label valSensitivity;
        private Toggle toggleReduceMotion;
        private Button btnResetSave;
        private Button btnSettingsClose;

        private readonly List<VisualElement> chapterCards = new List<VisualElement>(ChapterCount + 1);
        private readonly Dictionary<string, Button> dotButtonsByScene = new Dictionary<string, Button>();
        private VisualElement currentHalo;
        private readonly string[,] levelTitles = new string[ChapterCount + 1, 5];
        private readonly List<RibbonShard> activeShards = new List<RibbonShard>();

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

        // Smash state
        private float smashStartTime;
        private float smashStartX;
        private float smashStartY;
        private float smashTargetX;
        private float smashTargetY;
        private float enterSmashThresholdY;
        private Rect enterWorldRect;
        private Button hiddenLandingDot;
        private bool buttonShattered;
        private bool landingPlayed;
        private float nextTrailMoteAt;
        private float shockwaveElapsed = -1f;
        private Vector2 shockwaveOrigin;

        // Portal zoom state
        private float portalStartTime;
        private Vector2 portalStartCenter;
        private float portalStartSize;
        private float portalTargetSize;
        private string portalSceneName;
        private bool portalSceneDispatched;

        private void Awake()
        {
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
            BindUI();
            RebuildRibbonNodes();
            ApplySettingsValuesToUI();
            UpdateTitleAndMetaLabels();
            UpdatePreviewCard(selectedChapter, selectedLevel);
            if (btnEnter != null) btnEnter.Focus();
        }

        private void BindUI()
        {
            if (uiDocument == null || uiDocument.rootVisualElement == null) return;
            VisualElement root = uiDocument.rootVisualElement;
            bool firstBind = boundRoot != root;
            boundRoot = root;

            titleView = root.Q<VisualElement>("title-view");
            titleIterLabel = root.Q<Label>("title-iter-label");
            btnEnter = root.Q<Button>("btn-enter");
            btnTitleShowcase = root.Q<Button>("btn-title-showcase");
            btnTitleSettings = root.Q<Button>("btn-title-settings");
            btnShutdown = root.Q<Button>("btn-shutdown");

            ribbonView = root.Q<VisualElement>("ribbon-view");
            ribbonViewport = root.Q<VisualElement>("ribbon-viewport");
            ribbonTrack = root.Q<VisualElement>("ribbon-track");
            ribbonMetaLabel = root.Q<Label>("ribbon-meta-label");
            btnRibbonShowcase = root.Q<Button>("btn-ribbon-showcase");
            btnDemoToggle = root.Q<Button>("btn-demo-toggle");
            btnRibbonSettings = root.Q<Button>("btn-ribbon-settings");
            btnRibbonBack = root.Q<Button>("btn-ribbon-back");

            previewAccent = root.Q<VisualElement>("preview-accent");
            previewChapterLabel = root.Q<Label>("preview-chapter-label");
            previewTitleLabel = root.Q<Label>("preview-title-label");
            previewStatusLabel = root.Q<Label>("preview-status-label");
            btnPreviewLaunch = root.Q<Button>("btn-preview-launch");

            fxLayer = root.Q<VisualElement>("fx-layer");
            impactShockwave = root.Q<VisualElement>("impact-shockwave");
            fallingNode = root.Q<VisualElement>("falling-node");
            fallingNodeLabel = root.Q<Label>("falling-node-label");
            portalZoomOrb = root.Q<VisualElement>("portal-zoom-orb");
            portalZoomRing = root.Q<VisualElement>("portal-zoom-ring");
            portalZoomLabel = root.Q<Label>("portal-zoom-label");

            settingsOverlay = root.Q<VisualElement>("settings-overlay");
            sliderVolume = root.Q<Slider>("slider-volume");
            valVolume = root.Q<Label>("val-volume");
            sliderSensitivity = root.Q<Slider>("slider-sensitivity");
            valSensitivity = root.Q<Label>("val-sensitivity");
            toggleReduceMotion = root.Q<Toggle>("toggle-reduce-motion");
            btnResetSave = root.Q<Button>("btn-reset-save");
            btnSettingsClose = root.Q<Button>("btn-settings-close");

            if (!firstBind) return;

            if (btnEnter != null)
            {
                btnEnter.clicked += BeginEnterSmash;
                btnEnter.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
            }
            if (btnTitleShowcase != null)
            {
                btnTitleShowcase.clicked += () => LaunchShowcase(btnTitleShowcase);
                btnTitleShowcase.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
            }
            if (btnTitleSettings != null)
            {
                btnTitleSettings.clicked += OpenSettings;
                btnTitleSettings.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
            }
            if (btnShutdown != null)
            {
                btnShutdown.clicked += BeginShutdown;
                btnShutdown.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
            }
            if (btnRibbonShowcase != null)
            {
                btnRibbonShowcase.clicked += () => LaunchShowcase(btnRibbonShowcase);
                btnRibbonShowcase.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
            }
            if (btnDemoToggle != null) btnDemoToggle.clicked += ToggleDemoAccess;
            if (btnRibbonSettings != null) btnRibbonSettings.clicked += OpenSettings;
            if (btnRibbonBack != null) btnRibbonBack.clicked += ReturnToTitle;
            if (btnSettingsClose != null) btnSettingsClose.clicked += CloseSettings;
            if (btnResetSave != null) btnResetSave.clicked += HandleResetSaveClicked;
            if (btnPreviewLaunch != null)
            {
                btnPreviewLaunch.clicked += LaunchSelectedChamber;
                btnPreviewLaunch.RegisterCallback<PointerEnterEvent>(_ => SimulationAudio.PlayMenuHover());
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
                titleIterLabel.text = "Kota Tepi  •  Iterasi #" + SimulationProgress.IterationNumber;

            if (btnEnter != null)
            {
                btnEnter.text = currentChapter > 1 || currentLevel > 1
                    ? string.Format("Lanjutkan  •  Ruangan {0}.{1}", currentChapter, currentLevel)
                    : "Mulai Simulasi";
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

            if (ribbonMetaLabel != null)
                ribbonMetaLabel.text = string.Format("Iterasi #{0}  •  {1}/{2} Ruangan  •  {3} Memori",
                    SimulationProgress.IterationNumber, totalCleared, totalLevels, SimulationProgress.GetCollectedFragmentCount());

            if (btnDemoToggle != null)
            {
                bool on = SimulationProgress.IsDemoUnlocked;
                btnDemoToggle.text = on ? "[F2] Buka Semua: Nyala" : "[F2] Buka Semua: Mati";
                btnDemoToggle.EnableInClassList("top-chip-btn--on", on);
            }
        }

        private void RebuildRibbonNodes()
        {
            if (ribbonTrack == null) return;
            ribbonTrack.Clear();
            chapterCards.Clear();
            dotButtonsByScene.Clear();
            currentHalo = null;

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
                chapterCards.Add(card);

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
                        Label nowBadge = new Label("NOW");
                        nowBadge.AddToClassList("level-dot-badge");
                        nowBadge.style.backgroundColor = chapColor;
                        dotWrap.Add(nowBadge);

                        VisualElement halo = new VisualElement();
                        halo.AddToClassList("level-dot-halo");
                        halo.pickingMode = PickingMode.Ignore;
                        SetBorderColor(halo, chapColor);
                        dotWrap.Add(halo);
                        currentHalo = halo;
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
                        LaunchChamber(sceneName, dotBtn);
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
            Label scSec = new Label("DEMO TERBUKA");
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
            Button scBtn = new Button();
            scBtn.text = "Buka Demo";
            scBtn.AddToClassList("showcase-launch-btn");
            scBtn.clicked += () => LaunchShowcase(scBtn);
            scBtn.RegisterCallback<PointerEnterEvent>(_ =>
            {
                SimulationAudio.PlayMenuHover();
                UpdateShowcasePreview();
            });
            scCard.Add(scBtn);
            ribbonTrack.Add(scCard);
            chapterCards.Add(scCard);

            VisualElement epCard = new VisualElement();
            epCard.AddToClassList("chapter-node-card");
            epCard.style.borderTopColor = PaletteSettings.Rosewater;
            Label epSec = new Label("AKHIR");
            epSec.AddToClassList("chapter-sector-pill");
            epSec.style.color = PaletteSettings.Rosewater;
            epCard.Add(epSec);
            Label epNum = new Label("N");
            epNum.AddToClassList("chapter-node-num");
            epCard.Add(epNum);
            Label epTheme = new Label("ARSIP KOTA");
            epTheme.AddToClassList("chapter-node-theme");
            epCard.Add(epTheme);
            ribbonTrack.Add(epCard);
            chapterCards.Add(epCard);
        }

        private void UpdateShowcasePreview()
        {
            Color teal = PaletteSettings.Teal;
            if (previewChapterLabel != null)
            {
                previewChapterLabel.text = "RUANG SHOWCASE  •  SEMUA MEKANIK TERBUKA";
                previewChapterLabel.style.color = teal;
            }
            if (previewTitleLabel != null)
                previewTitleLabel.text = "Demo Lengkap POLYMULATOR";
            if (previewStatusLabel != null)
                previewStatusLabel.text = "Partition [1], Fusion [2], Telekinesis [F], Rotasi [R] & Mode Fleksibel langsung aktif";
            if (previewAccent != null) previewAccent.style.backgroundColor = teal;
            if (btnPreviewLaunch != null)
            {
                btnPreviewLaunch.SetEnabled(true);
                btnPreviewLaunch.style.backgroundColor = teal;
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
            if (string.IsNullOrEmpty(title)) title = "SECTOR " + chapter + "." + level;

            string chapterTheme = chapter < SimulationProgress.ChapterNames.Length
                ? SimulationProgress.ChapterNames[chapter] : "ARCHIVE";
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
                btnPreviewLaunch.text = cleared ? "Main Ulang" : (unlocked ? "Mulai" : "Terkunci");
            }

            if (previewStatusLabel != null)
            {
                if (!unlocked)
                {
                    previewStatusLabel.text = "Sektor terkunci  •  Selesaikan ruangan sebelumnya terlebih dahulu";
                    if (previewAccent != null) previewAccent.style.backgroundColor = PaletteSettings.Overlay1;
                }
                else if (cleared)
                {
                    int stars = SimulationProgress.GetStars(sceneName);
                    string starStr = new string('★', stars) + new string('☆', 3 - stars);
                    string mem = SimulationProgress.HasFragment(sceneName) ? "  •  Memori tersimpan" : "";
                    previewStatusLabel.text = string.Format("Selesai  [{0}]{1}", starStr, mem);
                    if (previewAccent != null) previewAccent.style.backgroundColor = PaletteSettings.Green;
                }
                else
                {
                    previewStatusLabel.text = "Siap dimulai  •  Klik titik level atau tekan Enter";
                    if (previewAccent != null) previewAccent.style.backgroundColor = chapColor;
                }
            }
        }

        private void Update()
        {
            HandleKeyboard();
            float dt = Time.unscaledDeltaTime;
            UpdateShards(dt);
            UpdateShockwave();

            if (currentHalo != null && state == MenuState.LevelMenu && !SimulationSettings.ReduceMotion)
            {
                float s = 1f + 0.14f * Mathf.Sin(Time.unscaledTime * 3.8f);
                currentHalo.style.scale = new Scale(new Vector3(s, s, 1f));
                currentHalo.style.opacity = 0.55f + 0.35f * Mathf.Sin(Time.unscaledTime * 3.8f);
            }

            if (state == MenuState.FallingSmash)
                UpdateFallingSmash();
            else if (state == MenuState.LevelMenu && !draggingRibbon)
                UpdateRibbonScrollSmoothing(dt);
            else if (state == MenuState.PortalZoom)
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
                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    BeginEnterSmash();
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

        private void LaunchShowcase(Button sourceBtn)
        {
            selectedChapter = 5;
            LaunchChamber(SimulationProgress.ShowcaseSceneName, sourceBtn);
        }

        private void LaunchSelectedChamber()
        {
            if (!SimulationProgress.IsUnlocked(selectedChapter, selectedLevel))
            {
                SimulationAudio.PlayAnchorReject(Vector3.zero);
                return;
            }
            string sceneName = LevelSceneName(selectedChapter, selectedLevel);
            dotButtonsByScene.TryGetValue(sceneName, out Button sourceDot);
            LaunchChamber(sceneName, sourceDot);
        }

        public void BeginEnterSmash()
        {
            if (state != MenuState.Title || settingsOpen) return;
            SimulationAudio.PlayMenuClick();
            state = MenuState.FallingSmash;
            smashStartTime = Time.unscaledTime;
            buttonShattered = false;
            landingPlayed = false;
            nextTrailMoteAt = 0f;

            if (cameraOrbit != null)
            {
                cameraOrbit.SetRibbonOpen(true);
                cameraOrbit.SetSmashDescent(0f);
            }

            if (titleView != null) titleView.AddToClassList("title-view--up");
            if (ribbonView != null)
            {
                ribbonView.RemoveFromClassList("ribbon-view--hidden");
                ribbonView.RemoveFromClassList("ribbon-view--fading");
            }

            Vector2 panelSize = GetPanelSize();
            enterWorldRect = btnEnter != null
                ? btnEnter.worldBound
                : new Rect(panelSize.x * 0.5f - 180f, panelSize.y * 0.5f, 360f, 54f);
            float viewW = Mathf.Max(400f, panelSize.x - 72f);

            int levelCount = Mathf.Max(1, SimulationProgress.GetLevelsInChapter(currentChapter));
            float approxDotInRail = 416f * (currentLevel / (float)(levelCount + 1));
            float approxTargetLocalX = (currentChapter - 1) * ClusterStride + 158f + approxDotInRail;
            float desiredLeftInViewport = enterWorldRect.center.x - 36f;
            horizontalScroll = targetScroll = ClampScroll(approxTargetLocalX - desiredLeftInViewport, viewW);

            if (ribbonTrack != null) ribbonTrack.style.left = -horizontalScroll;
            UpdateCameraScrollParallax(viewW);

            smashStartX = enterWorldRect.center.x - 48f;
            smashStartY = -128f;
            smashTargetX = (approxTargetLocalX - horizontalScroll) + 36f - 48f;
            smashTargetY = panelSize.y * 0.5f - 48f;
            enterSmashThresholdY = enterWorldRect.yMin;

            string curScene = LevelSceneName(currentChapter, currentLevel);
            dotButtonsByScene.TryGetValue(curScene, out hiddenLandingDot);
            if (hiddenLandingDot != null)
                hiddenLandingDot.style.visibility = Visibility.Hidden;

            Color chapColor = ChapterAccentColor(currentChapter);
            if (fallingNodeLabel != null)
                fallingNodeLabel.text = currentChapter + "." + currentLevel;
            if (fallingNode != null)
            {
                fallingNode.style.backgroundColor = chapColor;
                fallingNode.style.left = smashStartX;
                fallingNode.style.top = smashStartY;
                fallingNode.RemoveFromClassList("falling-node--hidden");
            }
        }

        private void UpdateFallingSmash()
        {
            if (hiddenLandingDot != null && hiddenLandingDot.worldBound.width > 1f)
            {
                Vector2 dotCenter = hiddenLandingDot.worldBound.center;
                smashTargetX = dotCenter.x - 48f;
                smashTargetY = dotCenter.y - 48f;
            }
            else if (ribbonViewport != null && ribbonViewport.worldBound.height > 10f)
            {
                smashTargetY = ribbonViewport.worldBound.center.y - 48f;
            }

            float t = Mathf.Clamp01((Time.unscaledTime - smashStartTime) / Mathf.Max(0.15f, fallDuration));
            if (SimulationSettings.ReduceMotion) t = 1f;

            // Phase 1 (0..0.28): fast plunge into ENTER block; Phase 2 (0.28..1): tracking descent to Ribbon orb slot
            float y;
            float x;
            if (t < 0.28f)
            {
                float k = t / 0.28f;
                float easeIn = k * k;
                y = Mathf.Lerp(smashStartY, enterSmashThresholdY - 48f, easeIn);
                x = Mathf.Lerp(smashStartX, enterWorldRect.center.x - 48f, easeIn);
            }
            else
            {
                float k = (t - 0.28f) / 0.72f;
                float easeOut = 1f - Mathf.Pow(1f - k, 3f);
                y = Mathf.Lerp(enterSmashThresholdY - 48f, smashTargetY, easeOut);
                x = Mathf.Lerp(enterWorldRect.center.x - 48f, smashTargetX, easeOut);
                if (cameraOrbit != null) cameraOrbit.SetSmashDescent(k);
            }

            if (!buttonShattered && t >= 0.27f)
            {
                SpawnEnterShards(enterWorldRect, ChapterAccentColor(currentChapter));
                TriggerImpactShockwave(enterWorldRect.center, ChapterAccentColor(currentChapter));
                SimulationAudio.PlayMenuSmash();
                SimulationVFX.Burst(new Vector3(-0.4f, 0.9f, 0.3f), ChapterAccentColor(currentChapter), 26, 3.4f);
                SimulationVisualBridge.TriggerFloorRipple(new Vector3(-0.4f, 0f, 0.3f), ChapterAccentColor(currentChapter), 7.5f, 1.35f, 0.78f);
                if (cameraOrbit != null) cameraOrbit.TriggerImpactPunch(0.28f);
                buttonShattered = true;
            }

            if (fallingNode != null)
            {
                fallingNode.style.left = x;
                fallingNode.style.top = y;
                float spinScale = t < 0.28f ? 1.08f : Mathf.Lerp(1.12f, 0.65f, (t - 0.28f) / 0.72f);
                fallingNode.style.scale = new Scale(new Vector3(spinScale, spinScale, 1f));
            }

            if (Time.unscaledTime >= nextTrailMoteAt && fxLayer != null && t < 0.96f)
            {
                nextTrailMoteAt = Time.unscaledTime + 0.028f;
                SpawnTrailMote(new Vector2(x + 48f, y + 48f), ChapterAccentColor(currentChapter));
            }

            if (t < 1f) return;

            if (!landingPlayed)
            {
                SimulationAudio.PlayMenuNodeLand();
                SimulationVFX.Ring(new Vector3(-0.7f, 0.08f, -0.5f), ChapterAccentColor(currentChapter), 2.8f, 0.48f);
                TriggerImpactShockwave(new Vector2(smashTargetX + 48f, smashTargetY + 48f), ChapterAccentColor(currentChapter));
                landingPlayed = true;
            }

            if (cameraOrbit != null) cameraOrbit.ClearSmashDescent();
            if (fallingNode != null)
            {
                fallingNode.AddToClassList("falling-node--hidden");
                fallingNode.style.scale = new Scale(Vector3.one);
            }
            if (hiddenLandingDot != null)
            {
                hiddenLandingDot.style.visibility = Visibility.Visible;
                hiddenLandingDot = null;
            }

            if (titleView != null) titleView.style.display = DisplayStyle.None;
            state = MenuState.LevelMenu;
        }

        private void TriggerImpactShockwave(Vector2 center, Color color)
        {
            if (impactShockwave == null) return;
            shockwaveOrigin = center;
            shockwaveElapsed = 0f;
            impactShockwave.style.left = center.x - 60f;
            impactShockwave.style.top = center.y - 60f;
            SetBorderColor(impactShockwave, color);
            impactShockwave.style.scale = new Scale(new Vector3(0.45f, 0.45f, 1f));
            impactShockwave.style.opacity = 1f;
            impactShockwave.RemoveFromClassList("impact-shockwave--hidden");
        }

        private void UpdateShockwave()
        {
            if (shockwaveElapsed < 0f || impactShockwave == null) return;
            shockwaveElapsed += Time.unscaledDeltaTime;
            float u = Mathf.Clamp01(shockwaveElapsed / 0.28f);
            float s = Mathf.Lerp(0.45f, 2.75f, 1f - Mathf.Pow(1f - u, 3f));
            impactShockwave.style.left = shockwaveOrigin.x - 60f;
            impactShockwave.style.top = shockwaveOrigin.y - 60f;
            impactShockwave.style.scale = new Scale(new Vector3(s, s, 1f));
            impactShockwave.style.opacity = (1f - u) * (1f - u);
            if (u >= 1f)
            {
                shockwaveElapsed = -1f;
                impactShockwave.AddToClassList("impact-shockwave--hidden");
            }
        }

        private void SpawnTrailMote(Vector2 center, Color color)
        {
            if (fxLayer == null) return;
            VisualElement mote = new VisualElement();
            mote.AddToClassList("orb-trail-mote");
            mote.pickingMode = PickingMode.Ignore;
            float sz = Random.Range(10f, 18f);
            float sx = center.x - sz * 0.5f + Random.Range(-10f, 10f);
            float sy = center.y - sz * 0.5f + Random.Range(-10f, 10f);
            mote.style.width = sz;
            mote.style.height = sz;
            mote.style.left = sx;
            mote.style.top = sy;
            mote.style.backgroundColor = Color.Lerp(color, PaletteSettings.Text, 0.35f);
            fxLayer.Add(mote);

            activeShards.Add(new RibbonShard
            {
                element = mote,
                x = sx,
                y = sy,
                vx = Random.Range(-38f, 38f),
                vy = Random.Range(-90f, -20f),
                angle = 0f,
                angularVel = 0f,
                life = 0.34f,
                maxLife = 0.34f
            });
        }

        private void SpawnEnterShards(Rect rect, Color accentColor)
        {
            if (fxLayer == null) return;
            const int cols = 6;
            const int rows = 2;
            float w = Mathf.Max(22f, rect.width / cols);
            float h = Mathf.Max(14f, rect.height / rows);

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    VisualElement shard = new VisualElement();
                    shard.AddToClassList("break-shard");
                    shard.pickingMode = PickingMode.Ignore;
                    SetBorderColor(shard, accentColor);
                    float sx = rect.x + c * w;
                    float sy = rect.y + r * h;
                    shard.style.left = sx;
                    shard.style.top = sy;
                    shard.style.width = w - 2f;
                    shard.style.height = h - 2f;
                    fxLayer.Add(shard);

                    Vector2 outDir = new Vector2((c - 2.5f) * 0.85f, r - 0.5f).normalized;
                    float maxLife = Random.Range(0.50f, 0.80f);
                    activeShards.Add(new RibbonShard
                    {
                        element = shard,
                        x = sx,
                        y = sy,
                        vx = outDir.x * Random.Range(220f, 440f) + Random.Range(-65f, 65f),
                        vy = outDir.y * Random.Range(90f, 220f) - Random.Range(90f, 240f),
                        angle = Random.Range(-15f, 15f),
                        angularVel = Random.Range(-420f, 420f),
                        life = maxLife,
                        maxLife = maxLife
                    });
                }
            }
        }

        private void UpdateShards(float dt)
        {
            for (int i = activeShards.Count - 1; i >= 0; i--)
            {
                RibbonShard s = activeShards[i];
                s.life -= dt;
                if (s.life <= 0f)
                {
                    s.element.RemoveFromHierarchy();
                    activeShards.RemoveAt(i);
                    continue;
                }
                bool hasGravity = Mathf.Abs(s.angularVel) > 0.01f;
                if (hasGravity) s.vy += 1480f * dt;
                s.x += s.vx * dt;
                s.y += s.vy * dt;
                s.angle += s.angularVel * dt;
                s.element.style.left = s.x;
                s.element.style.top = s.y;
                if (hasGravity) s.element.style.rotate = new Rotate(s.angle);
                s.element.style.opacity = Mathf.Clamp01(s.life / Mathf.Max(0.1f, s.maxLife));
            }
        }

        private void LaunchChamber(string sceneName, Button sourceDot)
        {
            if (state == MenuState.PortalZoom || state == MenuState.Done) return;
            state = MenuState.PortalZoom;
            portalSceneName = sceneName;
            portalStartTime = Time.unscaledTime;
            portalSceneDispatched = false;

            SimulationAudio.PlayModeSwitch(true);
            SimulationVFX.PlaySpawnAperture(new Vector3(-0.7f, 0.3f, -0.5f), ChapterAccentColor(selectedChapter));
            SimulationVisualBridge.TriggerFloorRipple(new Vector3(-0.7f, 0f, -0.5f), ChapterAccentColor(selectedChapter), 9.0f, 1.4f, 0.72f);

            if (titleView != null) titleView.AddToClassList("title-view--up");
            if (ribbonView != null) ribbonView.AddToClassList("ribbon-view--fading");

            Vector2 panelSize = GetPanelSize();
            portalStartCenter = sourceDot != null && sourceDot.worldBound.width > 1f
                ? sourceDot.worldBound.center
                : panelSize * 0.5f;
            portalStartSize = sourceDot != null && sourceDot.worldBound.width > 1f
                ? sourceDot.worldBound.width
                : 40f;
            Color chapColor = ChapterAccentColor(selectedChapter);
            if (portalZoomLabel != null)
                portalZoomLabel.text = string.IsNullOrEmpty(portalSceneName) || SimulationProgress.IsSpecialShowcase(portalSceneName)
                    ? "DEMO"
                    : selectedChapter + "." + selectedLevel;
            if (portalZoomRing != null)
                SetBorderColor(portalZoomRing, chapColor);

            if (portalZoomOrb != null)
            {
                portalZoomOrb.RemoveFromClassList("portal-zoom-orb--hidden");
                portalZoomOrb.style.backgroundColor = chapColor;
                portalZoomOrb.style.left = portalStartCenter.x - portalStartSize * 0.5f;
                portalZoomOrb.style.top = portalStartCenter.y - portalStartSize * 0.5f;
                portalZoomOrb.style.width = portalStartSize;
                portalZoomOrb.style.height = portalStartSize;
                portalZoomOrb.style.borderTopLeftRadius = portalStartSize * 0.5f;
                portalZoomOrb.style.borderTopRightRadius = portalStartSize * 0.5f;
                portalZoomOrb.style.borderBottomLeftRadius = portalStartSize * 0.5f;
                portalZoomOrb.style.borderBottomRightRadius = portalStartSize * 0.5f;
                portalZoomOrb.style.scale = new Scale(Vector3.one);
                portalZoomOrb.style.opacity = 1f;
            }
        }

        private void UpdatePortalLaunch()
        {
            float duration = SimulationSettings.ReduceMotion ? 0.16f : Mathf.Max(0.25f, portalZoomDuration);
            float t = Mathf.Clamp01((Time.unscaledTime - portalStartTime) / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            Vector2 panelSize = GetPanelSize();
            Vector2 centerNow = Vector2.Lerp(portalStartCenter, panelSize * 0.5f, eased);
            float sizeNow = Mathf.Lerp(portalStartSize, portalTargetSize, eased);

            if (cameraOrbit != null) cameraOrbit.SetPortalDive(eased);

            if (portalZoomOrb != null)
            {
                portalZoomOrb.style.width = sizeNow;
                portalZoomOrb.style.height = sizeNow;
                portalZoomOrb.style.left = centerNow.x - sizeNow * 0.5f;
                portalZoomOrb.style.top = centerNow.y - sizeNow * 0.5f;
                if (portalZoomRing != null) portalZoomRing.style.rotate = new Rotate(eased * 140f);
                if (portalZoomLabel != null) portalZoomLabel.style.fontSize = Mathf.Lerp(14f, 64f, eased);
            }

            if (!portalSceneDispatched && t >= 0.72f)
            {
                portalSceneDispatched = true;
                state = MenuState.Done;
                SceneTransition.Begin(portalSceneName, 0f, sceneFadeDuration, PaletteSettings.Crust);
            }
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
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1848f;
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
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1848f;
            targetScroll = ClampScroll(targetScroll + delta, viewW);
        }

        private void UpdateRibbonScrollSmoothing(float dt)
        {
            if (ribbonTrack == null || Mathf.Approximately(horizontalScroll, targetScroll)) return;
            float blend = 1f - Mathf.Exp(-14f * dt);
            horizontalScroll = Mathf.Lerp(horizontalScroll, targetScroll, blend);
            if (Mathf.Abs(targetScroll - horizontalScroll) < 0.5f) horizontalScroll = targetScroll;
            ribbonTrack.style.left = -horizontalScroll;
            float viewW = ribbonViewport != null ? ribbonViewport.resolvedStyle.width : 1848f;
            UpdateCameraScrollParallax(viewW);
        }

        private void UpdateCameraScrollParallax(float viewportWidth)
        {
            if (cameraOrbit == null) return;
            float max = MaxScroll(viewportWidth);
            cameraOrbit.SetScrollFraction(Mathf.InverseLerp(-120f, max, horizontalScroll));
        }

        private static float MaxScroll(float viewportWidth)
        {
            float contentW = (ChapterCount + 1) * ClusterStride + 220f;
            return Mathf.Max(120f, contentW - Mathf.Max(400f, viewportWidth) + 120f);
        }

        private static float ClampScroll(float value, float viewportWidth)
            => Mathf.Clamp(value, -120f, MaxScroll(viewportWidth));

        private void UpdateStaticPortalLaunch()
        {
            const float cameraSettleDuration = 0.50f;
            const float shrinkDuration = 0.38f;
            float elapsed = Time.unscaledTime - portalStartTime;

            float camProgress = SimulationSettings.ReduceMotion ? 1f : Mathf.Clamp01(elapsed / cameraSettleDuration);
            float easedCam = 1f - Mathf.Pow(1f - camProgress, 3f);
            if (cameraOrbit != null)
                cameraOrbit.SetPortalDive(easedCam);

            // Smoothly shrink the clicked level circle instead of expanding/popping abruptly
            if (portalZoomOrb != null)
            {
                float shrinkT = SimulationSettings.ReduceMotion ? 1f : Mathf.Clamp01(elapsed / shrinkDuration);
                float shrinkEase = shrinkT * shrinkT * (3f - 2f * shrinkT);
                float curSize = Mathf.Lerp(portalStartSize, 2f, shrinkEase);
                portalZoomOrb.style.left = portalStartCenter.x - curSize * 0.5f;
                portalZoomOrb.style.top = portalStartCenter.y - curSize * 0.5f;
                portalZoomOrb.style.width = curSize;
                portalZoomOrb.style.height = curSize;
                portalZoomOrb.style.borderTopLeftRadius = curSize * 0.5f;
                portalZoomOrb.style.borderTopRightRadius = curSize * 0.5f;
                portalZoomOrb.style.borderBottomLeftRadius = curSize * 0.5f;
                portalZoomOrb.style.borderBottomRightRadius = curSize * 0.5f;
                portalZoomOrb.style.opacity = Mathf.Clamp01(1f - shrinkT * 0.85f);
                if (portalZoomLabel != null)
                    portalZoomLabel.style.opacity = Mathf.Clamp01(1f - shrinkT * 2.2f);
                if (shrinkT >= 1f)
                    portalZoomOrb.AddToClassList("portal-zoom-orb--hidden");
            }

            if (!portalSceneDispatched && elapsed >= cameraSettleDuration + 0.04f)
            {
                portalSceneDispatched = true;
                state = MenuState.Done;
                Texture2D staticSnapshot = CaptureRearRectCameraSnapshot();
                StaticPortalTransition.Begin(portalSceneName, staticSnapshot, 0.06f, 0.28f);
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

        private static void SetBorderColor(VisualElement el, Color c)
        {
            if (el == null) return;
            el.style.borderTopColor = c;
            el.style.borderRightColor = c;
            el.style.borderBottomColor = c;
            el.style.borderLeftColor = c;
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
                if (btnResetSave != null) btnResetSave.text = "Konfirmasi Hapus?";
                SimulationAudio.PlayMenuHover();
                return;
            }
            resetConfirmUntil = 0f;
            if (btnResetSave != null) btnResetSave.text = "RESET PROGRESS DATA";
            ExecuteResetProgress();
        }

        private void OpenSettings()
        {
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
            if (btnResetSave != null) btnResetSave.text = "RESET PROGRESS DATA";
            if (settingsOverlay != null) settingsOverlay.AddToClassList("settings-overlay--hidden");
            SimulationAudio.PlayMenuHover();
            if (state == MenuState.Title && btnEnter != null) btnEnter.Focus();
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
            if (titleView != null)
            {
                titleView.style.display = DisplayStyle.Flex;
                titleView.RemoveFromClassList("title-view--up");
            }
            SimulationAudio.PlayMenuHover();
            if (btnEnter != null) btnEnter.Focus();
        }

        private void BeginShutdown()
        {
            if (state != MenuState.Title || settingsOpen) return;
            state = MenuState.Done;
            SimulationAudio.PlayMenuSmash();
            SceneTransition.Quit(0.25f, 0.45f);
        }

        private string LevelSceneName(int chapter, int level) => string.Format(levelSceneFormat, chapter, level);
    }
}
