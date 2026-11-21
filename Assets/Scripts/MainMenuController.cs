using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// First scene of the game. Draws one flat palette colour with no lighting or
    /// shadows. Selecting Enter drops a Blue chapter node that eases into a slow
    /// stop: it crushes the Enter button on the way down and lands exactly on the
    /// current chapter's node, so the falling node IS that chapter node with no
    /// morph or fade, while the rest of the ribbon is visible throughout the fall.
    /// Selecting Shutdown keeps the original drop-and-fall quit sequence.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private enum MenuState { Title, Dropping, ShutdownFalling, Falling, LevelMenu, Done }

        private const int EnterButton = 0;
        private const int ShutdownButton = 1;
        private const int ChapterCount = 4;
        private const int LevelsPerChapter = 4;
        private const int CurrentChapter = 1;
        private const int CurrentLevel = 1;
        private const float ChapterNodeSize = 96f;
        private const float ScrollResponsiveness = 14f;

        [SerializeField] private string levelSceneFormat = "Level{0}.{1}";

        [Header("Timing (seconds)")]
        [SerializeField] private float squareDropDuration = 0.45f;
        [SerializeField] private float buttonFallDuration = 0.65f;
        [SerializeField] private float fallDuration = 0.9f;
        [SerializeField] private float transitionDelay = 0.3f;
        [SerializeField] private float fadeDuration = 0.5f;

        private struct BreakPiece
        {
            public Rect rect;
            public Vector2 velocity;
            public float life;
            public Texture2D texture;
        }

        private struct Layout
        {
            public float nodeSize;
            public float dotSize;
            public float lineHeight;
            public float segmentLength;
            public float pitch;
            public float padding;
            public float contentWidth;
            public float originX;
            public float bandY;
        }

        private Texture2D backgroundTexture;
        private Texture2D buttonNormalTexture;
        private Texture2D buttonHoverTexture;
        private Texture2D squareSharpTexture;
        private Texture2D chapterDarkTexture;
        private Texture2D chapterBlueTexture;
        private Texture2D dotDarkTexture;
        private Texture2D dotBlueTexture;
        private Texture2D lineDarkTexture;
        private Texture2D lineBlueTexture;

        private GUIStyle titleStyle;
        private GUIStyle buttonStyle;
        private GUIStyle chapterLabelStyle;
        private GUIStyle tooltipStyle;

        private readonly string[,] levelTitles = new string[ChapterCount + 1, LevelsPerChapter + 1];
        private int hoveredChapter;
        private int hoveredLevel;
        private Rect hoveredDotRect;

        private readonly List<BreakPiece> pieces = new List<BreakPiece>();
        private MenuState state = MenuState.Title;
        private int selectedButton = -1;
        private float stateStartTime;
        private float squareSize;
        private float squareX;
        private float squareY;
        private float fallStartY;
        private Rect fallTargetRect;
        private bool buttonBroken;
        private float horizontalScroll;
        private float targetScroll;
        private float minScroll;
        private float maxScroll;
        private bool dragging;
        private float dragStartX;
        private float dragStartScroll;

        private void Awake()
        {
            backgroundTexture = SolidTexture(PaletteSettings.Mantle);
            buttonNormalTexture = SolidTexture(PaletteSettings.BaseSurface);
            buttonHoverTexture = SolidTexture(PaletteSettings.Crust);
            squareSharpTexture = SolidTexture(PaletteSettings.Blue);
            chapterDarkTexture = RoundedTexture(PaletteSettings.BaseSurface, 96, 20f);
            chapterBlueTexture = RoundedTexture(PaletteSettings.Blue, 96, 20f);
            dotDarkTexture = RoundedTexture(PaletteSettings.BaseSurface, 24, 12f);
            dotBlueTexture = RoundedTexture(PaletteSettings.Blue, 24, 12f);
            lineDarkTexture = SolidTexture(PaletteSettings.BaseSurface);
            lineBlueTexture = SolidTexture(PaletteSettings.Blue);

            for (int chapter = 1; chapter <= ChapterCount; chapter++)
            {
                for (int level = 1; level <= LevelsPerChapter; level++)
                {
                    LevelInfo info = Resources.Load<LevelInfo>("Levels/" + LevelSceneName(chapter, level));
                    if (info != null)
                        levelTitles[chapter, level] = info.Title;
                }
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            for (int i = pieces.Count - 1; i >= 0; i--)
            {
                BreakPiece piece = pieces[i];
                piece.life -= dt;
                if (piece.life <= 0f)
                {
                    pieces.RemoveAt(i);
                    continue;
                }

                piece.velocity += new Vector2(0f, 1500f) * dt;
                piece.rect.x += piece.velocity.x * dt;
                piece.rect.y += piece.velocity.y * dt;
                pieces[i] = piece;
            }

            if (dragging || Mathf.Approximately(horizontalScroll, targetScroll)) return;

            float blend = 1f - Mathf.Exp(-ScrollResponsiveness * dt);
            horizontalScroll = Mathf.Lerp(horizontalScroll, targetScroll, blend);
            if (Mathf.Abs(targetScroll - horizontalScroll) < 0.5f)
                horizontalScroll = targetScroll;
        }

        private void OnGUI()
        {
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), backgroundTexture,
                ScaleMode.StretchToFill);

            switch (state)
            {
                case MenuState.Title:
                    DrawTitleState();
                    break;
                case MenuState.Dropping:
                    DrawDropping();
                    break;
                case MenuState.ShutdownFalling:
                    DrawShutdownFalling();
                    break;
                case MenuState.Falling:
                    DrawFalling();
                    break;
                case MenuState.LevelMenu:
                    DrawLevelMenu();
                    break;
            }

            DrawPieces();
        }

        private void DrawTitleState()
        {
            Rect enterRect, shutdownRect;
            GetButtonRects(out enterRect, out shutdownRect);

            GUI.Label(new Rect(0f, enterRect.y - 104f, Screen.width, 64f), "POLYMULATOR", TitleStyle);

            if (GUI.Button(enterRect, "ENTER", ButtonStyle)) Select(EnterButton);
            if (GUI.Button(shutdownRect, "SHUTDOWN", ButtonStyle)) Select(ShutdownButton);
        }

        // Shutdown path: square drops onto the button and stops, then falls away.
        private void DrawDropping()
        {
            Rect enterRect, shutdownRect;
            GetButtonRects(out enterRect, out shutdownRect);

            GUI.Label(new Rect(0f, enterRect.y - 104f, Screen.width, 64f), "POLYMULATOR", TitleStyle);
            GUI.Label(enterRect, "ENTER", ButtonStyle);
            GUI.Label(shutdownRect, "SHUTDOWN", ButtonStyle);

            Rect target = selectedButton == EnterButton ? enterRect : shutdownRect;
            float t = Mathf.Clamp01((Time.unscaledTime - stateStartTime) /
                Mathf.Max(0.05f, squareDropDuration));
            float eased = t * t;

            squareX = target.center.x - squareSize * 0.5f;
            squareY = Mathf.Lerp(-squareSize, target.y - squareSize, eased);
            GUI.DrawTexture(new Rect(squareX, squareY, squareSize, squareSize), squareSharpTexture,
                ScaleMode.StretchToFill);

            if (t < 1f) return;

            stateStartTime = Time.unscaledTime;
            state = MenuState.ShutdownFalling;
        }

        private void DrawShutdownFalling()
        {
            Rect enterRect, shutdownRect;
            GetButtonRects(out enterRect, out shutdownRect);
            Rect target = selectedButton == EnterButton ? enterRect : shutdownRect;

            float t = Mathf.Clamp01((Time.unscaledTime - stateStartTime) /
                Mathf.Max(0.05f, buttonFallDuration));
            float offset = t * t * t * (Screen.height - target.y + target.height + squareSize);

            GUI.Label(new Rect(0f, enterRect.y - 104f, Screen.width, 64f), "POLYMULATOR", TitleStyle);
            GUI.Label(enterRect, "ENTER", ButtonStyle);

            Rect moved = shutdownRect;
            moved.y += offset;
            GUI.Label(moved, "SHUTDOWN", ButtonStyle);

            GUI.DrawTexture(new Rect(target.center.x - squareSize * 0.5f,
                target.y - squareSize + offset, squareSize, squareSize), squareSharpTexture,
                ScaleMode.StretchToFill);

            if (target.y + offset <= Screen.height) return;

            state = MenuState.Done;
            SceneTransition.Quit(transitionDelay, fadeDuration);
        }

        // Enter path: one continuous fall that eases into a slow stop. The square is
        // the current chapter node, so its final rect is exactly the node's rect;
        // no morph or fade. The rest of the ribbon is already in place during the fall.
        private void DrawFalling()
        {
            Rect enterRect, shutdownRect;
            GetButtonRects(out enterRect, out shutdownRect);

            float t = Mathf.Clamp01((Time.unscaledTime - stateStartTime) /
                Mathf.Max(0.05f, fallDuration));
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            DrawRibbon(GetLayout(), true);

            float panDistance = enterRect.y + enterRect.height + 60f;
            float panOffset = eased * panDistance;

            GUI.Label(new Rect(0f, enterRect.y - 104f - panOffset, Screen.width, 64f),
                "POLYMULATOR", TitleStyle);

            Rect shutdownMoved = shutdownRect;
            shutdownMoved.y -= panOffset;
            GUI.Label(shutdownMoved, "SHUTDOWN", ButtonStyle);

            Rect enterMoved = enterRect;
            enterMoved.y -= panOffset;

            squareY = Mathf.Lerp(fallStartY, fallTargetRect.y, eased);

            if (!buttonBroken && squareY + squareSize >= enterMoved.y)
            {
                SpawnButtonBreak(enterMoved);
                buttonBroken = true;
            }

            if (!buttonBroken)
                GUI.Label(enterMoved, "ENTER", ButtonStyle);

            Rect fallingRect = new Rect(squareX, squareY, squareSize, squareSize);
            GUI.DrawTexture(fallingRect, chapterBlueTexture, ScaleMode.StretchToFill);
            ChapterLabelStyle.normal.textColor = PaletteSettings.Mantle;
            GUI.Label(fallingRect, CurrentChapter.ToString(), ChapterLabelStyle);

            if (t < 1f) return;

            state = MenuState.LevelMenu;
        }

        private void DrawLevelMenu()
        {
            HandleLevelMenuInput();
            DrawRibbon(GetLayout());
        }

        private void DrawRibbon(Layout layout, bool skipCurrentNode = false)
        {
            hoveredChapter = 0;
            hoveredLevel = 0;

            for (int chapter = 1; chapter <= ChapterCount; chapter++)
            {
                Rect node = GetChapterNodeRect(chapter, layout);
                float segmentX = node.xMax;
                float segmentY = node.center.y - layout.lineHeight * 0.5f;

                if (chapter < CurrentChapter)
                {
                    GUI.DrawTexture(new Rect(segmentX, segmentY, layout.segmentLength,
                        layout.lineHeight), lineBlueTexture, ScaleMode.StretchToFill);
                }
                else if (chapter == CurrentChapter)
                {
                    Rect currentDot = GetLevelDotRect(chapter, CurrentLevel, layout);
                    float blueWidth = Mathf.Max(0f, currentDot.center.x - segmentX);
                    GUI.DrawTexture(new Rect(segmentX, segmentY, blueWidth,
                        layout.lineHeight), lineBlueTexture, ScaleMode.StretchToFill);
                    GUI.DrawTexture(new Rect(segmentX + blueWidth, segmentY,
                        layout.segmentLength - blueWidth, layout.lineHeight), lineDarkTexture,
                        ScaleMode.StretchToFill);
                }
                else
                {
                    GUI.DrawTexture(new Rect(segmentX, segmentY, layout.segmentLength,
                        layout.lineHeight), lineDarkTexture, ScaleMode.StretchToFill);
                }

                for (int level = 1; level <= LevelsPerChapter; level++)
                {
                    Rect dot = GetLevelDotRect(chapter, level, layout);
                    bool unlocked = IsLevelUnlocked(chapter, level);
                    Texture2D dotTexture = unlocked ? dotBlueTexture : dotDarkTexture;
                    GUI.DrawTexture(dot, dotTexture, ScaleMode.StretchToFill);

                    Event current = Event.current;
                    if (state == MenuState.LevelMenu && dot.Contains(current.mousePosition))
                    {
                        hoveredChapter = chapter;
                        hoveredLevel = level;
                        hoveredDotRect = dot;
                    }

                    if (state != MenuState.LevelMenu || !unlocked) continue;
                    if (current.type != EventType.MouseDown || current.button != 0 ||
                        !dot.Contains(current.mousePosition)) continue;

                    current.Use();
                    dragging = false;
                    state = MenuState.Done;
                    SceneTransition.Begin(LevelSceneName(chapter, level), transitionDelay, fadeDuration);
                }
            }

            for (int chapter = 1; chapter <= ChapterCount; chapter++)
            {
                if (skipCurrentNode && chapter == CurrentChapter) continue;

                Rect node = GetChapterNodeRect(chapter, layout);
                bool reached = chapter <= CurrentChapter;
                Texture2D nodeTexture = reached ? chapterBlueTexture : chapterDarkTexture;
                GUI.DrawTexture(node, nodeTexture, ScaleMode.StretchToFill);

                ChapterLabelStyle.normal.textColor = reached
                    ? PaletteSettings.Mantle
                    : PaletteSettings.TextPrimary;
                GUI.Label(node, chapter.ToString(), ChapterLabelStyle);
            }

            Rect endNode = GetChapterNodeRect(ChapterCount + 1, layout);
            GUI.DrawTexture(endNode, chapterDarkTexture, ScaleMode.StretchToFill);
            ChapterLabelStyle.normal.textColor = PaletteSettings.TextPrimary;
            GUI.Label(endNode, "N", ChapterLabelStyle);

            if (hoveredChapter > 0)
                DrawTooltip(hoveredDotRect, levelTitles[hoveredChapter, hoveredLevel]);
        }

        private void DrawTooltip(Rect anchor, string title)
        {
            if (string.IsNullOrEmpty(title)) return;

            Vector2 size = TooltipStyle.CalcSize(new GUIContent(title));
            size.x += 8f;
            size.y += 4f;
            Rect rect = new Rect(anchor.center.x - size.x * 0.5f, anchor.yMax + 10f,
                size.x, size.y);

            GUI.DrawTexture(rect, buttonNormalTexture, ScaleMode.StretchToFill);
            GUI.Label(rect, title, TooltipStyle);
        }

        private void SpawnButtonBreak(Rect buttonRect)
        {
            pieces.Clear();

            const int columns = 4;
            const int rows = 2;
            float pieceWidth = buttonRect.width / columns;
            float pieceHeight = buttonRect.height / rows;
            Vector2 centre = buttonRect.center;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Rect rect = new Rect(buttonRect.x + x * pieceWidth,
                        buttonRect.y + y * pieceHeight, pieceWidth, pieceHeight);
                    Vector2 outward = (rect.center - centre).normalized;

                    pieces.Add(new BreakPiece
                    {
                        rect = rect,
                        velocity = outward * Random.Range(140f, 300f) +
                            new Vector2(Random.Range(-60f, 60f), -Random.Range(80f, 220f)),
                        life = Random.Range(0.45f, 0.7f),
                        texture = (x + y) % 2 == 0 ? buttonNormalTexture : buttonHoverTexture
                    });
                }
            }
        }

        private void DrawPieces()
        {
            Color previous = GUI.color;
            for (int i = 0; i < pieces.Count; i++)
            {
                BreakPiece piece = pieces[i];
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(piece.life / 0.7f));
                GUI.DrawTexture(piece.rect, piece.texture, ScaleMode.StretchToFill);
            }
            GUI.color = previous;
        }

        private void Select(int index)
        {
            selectedButton = index;
            stateStartTime = Time.unscaledTime;
            horizontalScroll = 0f;
            targetScroll = 0f;
            dragging = false;
            buttonBroken = false;

            if (index == EnterButton)
            {
                Rect enterRect, shutdownRect;
                GetButtonRects(out enterRect, out shutdownRect);

                squareSize = ChapterNodeSize;
                squareX = enterRect.center.x - squareSize * 0.5f;
                fallStartY = -squareSize;

                // Align the ribbon so the current chapter node sits exactly where
                // the falling node will land (the Enter button's x-centre).
                Layout raw = BuildLayout();
                float baseX = Mathf.Max(16f, (Screen.width - raw.contentWidth) * 0.5f);
                float alignScroll = baseX + raw.padding +
                    (CurrentChapter - 1) * raw.pitch + raw.nodeSize * 0.5f - enterRect.center.x;
                horizontalScroll = targetScroll = alignScroll;

                fallTargetRect = GetChapterNodeRect(CurrentChapter, GetLayout());
                state = MenuState.Falling;
            }
            else
            {
                squareSize = Mathf.Min(72f, Screen.width * 0.14f);
                state = MenuState.Dropping;
            }
        }

        private void HandleLevelMenuInput()
        {
            Event current = Event.current;
            Layout layout = GetLayout();

            switch (current.type)
            {
                case EventType.MouseDown:
                    if (current.button != 0 || IsPointerOverLevelDot(current.mousePosition, layout))
                        break;
                    dragging = true;
                    dragStartX = current.mousePosition.x;
                    dragStartScroll = horizontalScroll;
                    targetScroll = horizontalScroll;
                    break;

                case EventType.MouseDrag:
                    if (!dragging) break;
                    horizontalScroll = dragStartScroll - (current.mousePosition.x - dragStartX);
                    targetScroll = horizontalScroll;
                    current.Use();
                    break;

                case EventType.MouseUp:
                    if (current.button == 0) dragging = false;
                    break;

                case EventType.ScrollWheel:
                    float delta = Mathf.Abs(current.delta.y) >= Mathf.Abs(current.delta.x)
                        ? current.delta.y : current.delta.x;
                    if (!Mathf.Approximately(delta, 0f))
                    {
                        float step = Mathf.Max(120f, Mathf.Abs(delta) * 60f);
                        targetScroll = Mathf.Clamp(targetScroll + (delta < 0f ? step : -step),
                            minScroll, maxScroll);
                    }
                    current.Use();
                    break;

                case EventType.KeyDown:
                    if (current.keyCode == KeyCode.LeftArrow || current.keyCode == KeyCode.A)
                        targetScroll = Mathf.Clamp(targetScroll - 160f, minScroll, maxScroll);
                    else if (current.keyCode == KeyCode.RightArrow || current.keyCode == KeyCode.D)
                        targetScroll = Mathf.Clamp(targetScroll + 160f, minScroll, maxScroll);
                    else break;
                    current.Use();
                    break;
            }
        }

        private static bool IsPointerOverLevelDot(Vector2 point, Layout layout)
        {
            for (int chapter = 1; chapter <= ChapterCount; chapter++)
            {
                for (int level = 1; level <= LevelsPerChapter; level++)
                {
                    if (GetLevelDotRect(chapter, level, layout).Contains(point)) return true;
                }
            }
            return false;
        }

        private static Layout BuildLayout()
        {
            Layout layout;
            layout.nodeSize = ChapterNodeSize;
            layout.dotSize = 24f;
            layout.lineHeight = 6f;
            layout.segmentLength = 400f;
            layout.pitch = layout.nodeSize + layout.segmentLength;
            layout.padding = 80f;
            layout.contentWidth = layout.padding * 2f +
                (ChapterCount + 1) * layout.nodeSize + ChapterCount * layout.segmentLength;
            layout.bandY = (Screen.height - layout.nodeSize) * 0.5f;
            layout.originX = 0f;
            return layout;
        }

        private Layout GetLayout()
        {
            Layout layout = BuildLayout();
            float baseX = Mathf.Max(16f, (Screen.width - layout.contentWidth) * 0.5f);

            // The scroll range has to include the landing scroll so the LevelMenu
            // doesn't jump the moment the user starts scrolling after the fall.
            float alignCurrent = baseX + layout.padding +
                (CurrentChapter - 1) * layout.pitch + layout.nodeSize * 0.5f - EnterCenterX;
            float normalMax = Mathf.Max(0f, layout.contentWidth - Screen.width + layout.padding);
            minScroll = Mathf.Min(0f, alignCurrent);
            maxScroll = Mathf.Max(normalMax, alignCurrent);

            horizontalScroll = Mathf.Clamp(horizontalScroll, minScroll, maxScroll);
            targetScroll = Mathf.Clamp(targetScroll, minScroll, maxScroll);

            layout.originX = baseX - horizontalScroll;
            return layout;
        }

        private static Rect GetChapterNodeRect(int chapter, Layout layout)
        {
            float x = layout.originX + layout.padding + (chapter - 1) * layout.pitch;
            return new Rect(x, layout.bandY, layout.nodeSize, layout.nodeSize);
        }

        private static Rect GetLevelDotRect(int chapter, int level, Layout layout)
        {
            Rect node = GetChapterNodeRect(chapter, layout);
            float spacing = layout.segmentLength / (LevelsPerChapter + 1);
            float centreX = node.xMax + spacing * level;
            return new Rect(centreX - layout.dotSize * 0.5f,
                node.center.y - layout.dotSize * 0.5f, layout.dotSize, layout.dotSize);
        }

        private static bool IsLevelUnlocked(int chapter, int level)
        {
            return chapter < CurrentChapter ||
                (chapter == CurrentChapter && level <= CurrentLevel);
        }

        private string LevelSceneName(int chapter, int level)
        {
            return string.Format(levelSceneFormat, chapter, level);
        }

        private static float EnterCenterX
        {
            get
            {
                Rect enter, shutdown;
                GetButtonRects(out enter, out shutdown);
                return enter.center.x;
            }
        }

        private static void GetButtonRects(out Rect enterRect, out Rect shutdownRect)
        {
            float gap = 20f;
            float buttonWidth = Mathf.Min(260f, (Screen.width - 64f - gap) * 0.5f);
            float buttonHeight = 58f;
            float left = (Screen.width - (buttonWidth * 2f + gap)) * 0.5f;
            float buttonY = Screen.height * 0.5f + 20f;

            enterRect = new Rect(left, buttonY, buttonWidth, buttonHeight);
            shutdownRect = new Rect(left + buttonWidth + gap, buttonY, buttonWidth, buttonHeight);
        }

        private GUIStyle TitleStyle
        {
            get
            {
                if (titleStyle != null) return titleStyle;

                titleStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = Mathf.Max(24, Mathf.RoundToInt(Screen.height * 0.06f)),
                    fontStyle = FontStyle.Bold
                };
                titleStyle.font = GUI.skin.font;
                titleStyle.normal.textColor = PaletteSettings.TextPrimary;
                return titleStyle;
            }
        }

        private GUIStyle ButtonStyle
        {
            get
            {
                if (buttonStyle != null) return buttonStyle;

                buttonStyle = new GUIStyle
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 20,
                    fontStyle = FontStyle.Bold,
                    border = new RectOffset(0, 0, 0, 0),
                    padding = new RectOffset(12, 12, 8, 8)
                };
                buttonStyle.font = GUI.skin.font;

                buttonStyle.normal.background = buttonNormalTexture;
                buttonStyle.normal.textColor = PaletteSettings.TextPrimary;
                buttonStyle.hover.background = buttonHoverTexture;
                buttonStyle.hover.textColor = PaletteSettings.TextPrimary;
                buttonStyle.active.background = buttonNormalTexture;
                buttonStyle.active.textColor = PaletteSettings.TextPrimary;
                buttonStyle.focused.background = buttonNormalTexture;
                buttonStyle.focused.textColor = PaletteSettings.TextPrimary;
                buttonStyle.onNormal.background = buttonNormalTexture;
                buttonStyle.onNormal.textColor = PaletteSettings.TextPrimary;
                buttonStyle.onHover.background = buttonHoverTexture;
                buttonStyle.onHover.textColor = PaletteSettings.TextPrimary;
                buttonStyle.onActive.background = buttonNormalTexture;
                buttonStyle.onActive.textColor = PaletteSettings.TextPrimary;
                buttonStyle.onFocused.background = buttonNormalTexture;
                buttonStyle.onFocused.textColor = PaletteSettings.TextPrimary;
                return buttonStyle;
            }
        }

        private GUIStyle ChapterLabelStyle
        {
            get
            {
                if (chapterLabelStyle != null) return chapterLabelStyle;

                chapterLabelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 34,
                    fontStyle = FontStyle.Bold
                };
                chapterLabelStyle.font = GUI.skin.font;
                return chapterLabelStyle;
            }
        }

        private GUIStyle TooltipStyle
        {
            get
            {
                if (tooltipStyle != null) return tooltipStyle;

                tooltipStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(12, 12, 6, 6)
                };
                tooltipStyle.font = GUI.skin.font;
                tooltipStyle.normal.textColor = PaletteSettings.TextPrimary;
                return tooltipStyle;
            }
        }

        private static Texture2D SolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private static Texture2D RoundedTexture(Color color, int size, float radius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            Color transparent = new Color(color.r, color.g, color.b, 0f);
            float corner = Mathf.Clamp(radius, 0f, size * 0.5f);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nearestX = Mathf.Clamp(x + 0.5f, corner, size - corner);
                    float nearestY = Mathf.Clamp(y + 0.5f, corner, size - corner);
                    float dx = x + 0.5f - nearestX;
                    float dy = y + 0.5f - nearestY;
                    texture.SetPixel(x, y, dx * dx + dy * dy <= corner * corner ? color : transparent);
                }
            }

            texture.Apply();
            texture.hideFlags = HideFlags.HideAndDontSave;
            return texture;
        }

        private void OnDestroy()
        {
            Destroy(backgroundTexture);
            Destroy(buttonNormalTexture);
            Destroy(buttonHoverTexture);
            Destroy(squareSharpTexture);
            Destroy(chapterDarkTexture);
            Destroy(chapterBlueTexture);
            Destroy(dotDarkTexture);
            Destroy(dotBlueTexture);
            Destroy(lineDarkTexture);
            Destroy(lineBlueTexture);
        }
    }
}
