using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Polymulator
{
    public enum RadialSelectionMode
    {
        ChapterOverview,
        ChapterExpanded
    }

    public enum RadialPreviewSource
    {
        ChapterArc,
        LevelSlice,
        Keyboard
    }

    public sealed class CampaignRadialLevel
    {
        public int Chapter;
        public int Level;
        public string Code;
        public bool Unlocked;
        public bool Cleared;
        public bool Current;
        public Label Label;
    }

    public sealed class CampaignRadialChapter
    {
        public int Index;
        public int ClearedCount;
        public bool Locked;
        public readonly List<CampaignRadialLevel> Levels = new List<CampaignRadialLevel>();
    }

    /// <summary>
    /// Campaign circle. The four UXML attributes are the only visual tuning knobs:
    /// circle-radius, arc-thickness, outline-thickness and level-pie-depth.
    /// The chapter gap is derived from outline-thickness; every other dimension is derived.
    /// </summary>
    [UxmlElement]
    public sealed partial class CampaignRadialSelector : VisualElement
    {
        private struct RadialGeometry
        {
            public Vector2 Center;
            public float Scale;
            public float CenterRadius;
            public float ArcInner;
            public float Outer;
            public float LevelBand;
            public float Outline;
            public float GapHalf;
        }

        private float circleRadius = 132f;
        private float arcThickness = 16f;
        private float outlineThickness = 2.5f;
        private float levelPieDepth = 480f;

        [UxmlAttribute("circle-radius")]
        public float CircleRadius
        {
            get => circleRadius;
            set
            {
                float next = Mathf.Max(60f, value);
                if (Mathf.Approximately(circleRadius, next)) return;
                circleRadius = next;
                MarkDirtyRepaint();
                UpdateOverlayLayout();
            }
        }

        [UxmlAttribute("arc-thickness")]
        public float ArcThickness
        {
            get => arcThickness;
            set
            {
                float next = Mathf.Max(4f, value);
                if (Mathf.Approximately(arcThickness, next)) return;
                arcThickness = next;
                MarkDirtyRepaint();
                UpdateOverlayLayout();
            }
        }

        [UxmlAttribute("outline-thickness")]
        public float OutlineThickness
        {
            get => outlineThickness;
            set
            {
                float next = Mathf.Max(0.5f, value);
                if (Mathf.Approximately(outlineThickness, next)) return;
                outlineThickness = next;
                MarkDirtyRepaint();
                UpdateOverlayLayout();
            }
        }

        [UxmlAttribute("level-pie-depth")]
        public float LevelPieDepth
        {
            get => levelPieDepth;
            set
            {
                float next = Mathf.Max(8f, value);
                if (Mathf.Approximately(levelPieDepth, next)) return;
                levelPieDepth = next;
                MarkDirtyRepaint();
                UpdateOverlayLayout();
            }
        }

        private readonly List<CampaignRadialChapter> chapters = new List<CampaignRadialChapter>();
        private readonly List<Label> levelLabels = new List<Label>();
        private float[] chapterGrowth = Array.Empty<float>();
        private float[] chapterProgress = Array.Empty<float>();

        private Label centerNumber;
        private string centerValue = string.Empty;
        private float centerReveal = 1f;

        private RadialSelectionMode mode = RadialSelectionMode.ChapterOverview;
        private int expandedChapter;
        private int focusedChapter = 1;
        private int focusedLevel = 1;
        private int selectedChapter = 1;
        private int selectedLevel = 1;
        private int hoverChapter;
        private int hoverLevel;
        private bool hoverIsLevel;
        private bool hasActive;

        public event Action<int, int, RadialPreviewSource, int, int> PreviewChanged;
        public event Action<int, int> LevelLaunched;
        public event Action LevelRejected;
        public event Action HighlightCleared;

        public bool HasKeyboardFocus { get; private set; }
        public bool HasActiveHighlight => hasActive;
        public RadialSelectionMode Mode => mode;
        public int FocusedChapter => focusedChapter;
        public int FocusedLevel => focusedLevel;
        public int ExpandedChapter => expandedChapter;
        public int ActiveChapterIndex => ActiveChapter();
        public int ActiveLevelIndex => ActiveLevel();

        public CampaignRadialSelector()
        {
            pickingMode = PickingMode.Position;
            focusable = true;
            tabIndex = 0;
            generateVisualContent += OnGenerateVisualContent;

            RegisterCallback<GeometryChangedEvent>(_ => UpdateOverlayLayout());
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerLeaveEvent>(_ => ClearActive());
            RegisterCallback<FocusInEvent>(_ =>
            {
                HasKeyboardFocus = true;
                MarkDirtyRepaint();
            });
            RegisterCallback<FocusOutEvent>(_ =>
            {
                HasKeyboardFocus = false;
                MarkDirtyRepaint();
                if (hoverChapter == 0) ClearActive();
            });

            centerNumber = new Label();
            centerNumber.AddToClassList("radial-center-number");
            centerNumber.pickingMode = PickingMode.Ignore;
            Add(centerNumber);
        }

        public void SetCampaign(List<CampaignRadialChapter> data, int currentChapter, int currentLevel)
        {
            chapters.Clear();
            chapters.AddRange(data);
            chapterGrowth = new float[chapters.Count];
            chapterProgress = new float[chapters.Count];

            for (int i = 0; i < chapters.Count; i++)
            {
                CampaignRadialChapter chapter = chapters[i];
                chapter.Locked = true;
                for (int l = 0; l < chapter.Levels.Count; l++)
                {
                    if (chapter.Levels[l].Unlocked) chapter.Locked = false;
                }
                chapterProgress[i] = 0f;
                chapterGrowth[i] = 0f;
            }

            selectedChapter = Mathf.Clamp(currentChapter, 1, Mathf.Max(1, chapters.Count));
            selectedLevel = Mathf.Clamp(currentLevel, 1, ChapterLevelCount(selectedChapter));
            focusedChapter = selectedChapter;
            focusedLevel = selectedLevel;
            hoverChapter = 0;
            hoverLevel = 0;
            hoverIsLevel = false;
            hasActive = false;
            mode = RadialSelectionMode.ChapterOverview;
            expandedChapter = 0;
            SetCenterNumber(selectedChapter.ToString());
            RebuildLevelLabels();
            MarkDirtyRepaint();
            UpdateOverlayLayout();
        }

        public void SetSelection(int chapter, int level)
        {
            selectedChapter = Mathf.Clamp(chapter, 1, Mathf.Max(1, chapters.Count));
            selectedLevel = Mathf.Clamp(level, 1, ChapterLevelCount(selectedChapter));
            focusedChapter = selectedChapter;
            focusedLevel = selectedLevel;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
        }

        public void SetFocus(int chapter, int level, bool expanded)
        {
            focusedChapter = Mathf.Clamp(chapter, 1, Mathf.Max(1, chapters.Count));
            focusedLevel = Mathf.Clamp(level, 1, ChapterLevelCount(focusedChapter));
            mode = expanded ? RadialSelectionMode.ChapterExpanded : RadialSelectionMode.ChapterOverview;
            expandedChapter = expanded ? focusedChapter : 0;
            hoverChapter = 0;
            hoverLevel = 0;
            hoverIsLevel = false;
            hasActive = true;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
            NotifyPreview(RadialPreviewSource.Keyboard);
        }

        public void ExpandChapter(int chapter)
        {
            int target = Mathf.Clamp(chapter, 1, Mathf.Max(1, chapters.Count));
            mode = RadialSelectionMode.ChapterExpanded;
            expandedChapter = target;
            focusedChapter = target;
            focusedLevel = PreferredLevel(target);
            hoverChapter = 0;
            hoverLevel = 0;
            hoverIsLevel = false;
            hasActive = true;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
            NotifyPreview(RadialPreviewSource.Keyboard);
        }

        public void Collapse()
        {
            mode = RadialSelectionMode.ChapterOverview;
            expandedChapter = 0;
            focusedChapter = selectedChapter;
            focusedLevel = selectedLevel;
            hoverChapter = 0;
            hoverLevel = 0;
            hoverIsLevel = false;
            hasActive = false;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
        }

        public void ClearActive()
        {
            string restingCenter = selectedChapter.ToString();
            bool wasActive = hasActive || centerValue != restingCenter;
            hasActive = false;
            hoverChapter = 0;
            hoverLevel = 0;
            hoverIsLevel = false;
            mode = RadialSelectionMode.ChapterOverview;
            expandedChapter = 0;
            SetCenterNumber(restingCenter);

            if (!wasActive) return;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
            HighlightCleared?.Invoke();
        }

        public void SetCenterNumber(string number)
        {
            string next = number ?? string.Empty;
            if (centerValue == next) return;
            centerValue = next;
            centerNumber.text = next;
            centerReveal = 0f;
            centerNumber.style.opacity = 0f;
            centerNumber.style.scale = new Scale(new Vector2(0.9f, 0.9f));
        }

        public void Tick(float deltaTime, bool reduceMotion)
        {
            if (chapters.Count == 0) return;

            int active = ActiveChapter();
            float growthSpeed = reduceMotion ? 1f : 1f - Mathf.Exp(-deltaTime / 0.07f);
            float fillSpeed = reduceMotion ? 1f : 1f - Mathf.Exp(-deltaTime / 0.12f);
            bool changed = false;

            for (int i = 0; i < chapters.Count; i++)
            {
                int chapterIndex = chapters[i].Index;
                float growthTarget = chapterIndex == active ? 1f : 0f;
                int total = Mathf.Max(1, chapters[i].Levels.Count);
                float progressTarget = chapters[i].Locked ? 0f : Mathf.Clamp01(chapters[i].ClearedCount / (float)total);

                if (!Mathf.Approximately(chapterGrowth[i], growthTarget))
                {
                    chapterGrowth[i] = reduceMotion ? growthTarget : Mathf.Lerp(chapterGrowth[i], growthTarget, growthSpeed);
                    changed = true;
                }
                if (!Mathf.Approximately(chapterProgress[i], progressTarget))
                {
                    chapterProgress[i] = reduceMotion ? progressTarget : Mathf.Lerp(chapterProgress[i], progressTarget, fillSpeed);
                    changed = true;
                }
            }

            if (centerReveal < 1f)
            {
                centerReveal = reduceMotion ? 1f : Mathf.Lerp(centerReveal, 1f, growthSpeed);
                if (centerReveal > 0.995f) centerReveal = 1f;
                float alpha = 0.28f + 0.72f * centerReveal;
                float size = 0.9f + 0.1f * centerReveal;
                centerNumber.style.opacity = alpha;
                centerNumber.style.scale = new Scale(new Vector2(size, size));
                changed = true;
            }

            if (!changed) return;
            MarkDirtyRepaint();
            UpdateOverlayLayout();
        }

        private int ActiveChapter()
        {
            if (!hasActive) return 0;
            int chapter = hoverChapter > 0 ? hoverChapter : focusedChapter;
            return FindChapter(chapter) != null ? chapter : 0;
        }

        private int ActiveLevel()
        {
            if (!hasActive) return 0;
            int level = hoverLevel > 0 ? hoverLevel : focusedLevel;
            return level > 0 ? level : 1;
        }

        private void NotifyPreview(RadialPreviewSource source)
        {
            int chapter = ActiveChapter();
            if (chapter <= 0) return;
            CampaignRadialChapter data = FindChapter(chapter);
            int cleared = data != null ? data.ClearedCount : 0;
            int total = data != null ? Mathf.Max(1, data.Levels.Count) : 1;
            PreviewChanged?.Invoke(chapter, ActiveLevel(), source, cleared, total);
        }

        private void RebuildLevelLabels()
        {
            for (int i = 0; i < levelLabels.Count; i++) levelLabels[i].RemoveFromHierarchy();
            levelLabels.Clear();

            for (int c = 0; c < chapters.Count; c++)
            {
                for (int l = 0; l < chapters[c].Levels.Count; l++)
                {
                    CampaignRadialLevel level = chapters[c].Levels[l];
                    Label label = new Label(level.Code);
                    label.AddToClassList("radial-level-label");
                    label.pickingMode = PickingMode.Ignore;
                    level.Label = label;
                    levelLabels.Add(label);
                    Add(label);
                }
            }
        }

        private RadialGeometry GetGeometry()
        {
            Rect rect = contentRect;
            float size = Mathf.Min(rect.width, rect.height);
            float levelBand = levelPieDepth;
            float extent = circleRadius + levelBand + outlineThickness * 2f + 8f;
            float scale = Mathf.Clamp(size * 0.5f / Mathf.Max(1f, extent), 0.2f, 1f);
            float arcInner = Mathf.Max(circleRadius * 0.5f, circleRadius - arcThickness) * scale;
            float gap = Mathf.Min(10f, Mathf.Rad2Deg * outlineThickness / Mathf.Max(1f, circleRadius));

            return new RadialGeometry
            {
                Center = new Vector2(rect.width * 0.5f, rect.height * 0.5f),
                Scale = scale,
                CenterRadius = arcInner,
                ArcInner = arcInner,
                Outer = circleRadius * scale,
                LevelBand = levelBand * scale,
                Outline = Mathf.Max(1f, outlineThickness * scale),
                GapHalf = gap * 0.5f
            };
        }

        private static Vector2 PolarPoint(Vector2 center, float radius, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return center + new Vector2(Mathf.Sin(radians), -Mathf.Cos(radians)) * radius;
        }

        private float TotalLevels()
        {
            int total = 0;
            for (int i = 0; i < chapters.Count; i++) total += chapters[i].Levels.Count;
            return Mathf.Max(1, total);
        }

        private float LevelSpan() => 360f / TotalLevels();

        private int ChapterLevelCount(int chapter)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].Index == chapter) return Mathf.Max(1, chapters[i].Levels.Count);
            }
            return 1;
        }

        private CampaignRadialChapter FindChapter(int chapter)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].Index == chapter) return chapters[i];
            }
            return null;
        }

        private CampaignRadialLevel FindLevel(int chapter, int level)
        {
            CampaignRadialChapter data = FindChapter(chapter);
            if (data == null) return null;
            for (int i = 0; i < data.Levels.Count; i++)
            {
                if (data.Levels[i].Level == level) return data.Levels[i];
            }
            return null;
        }

        private int ChapterArrayIndex(int chapter)
        {
            for (int i = 0; i < chapters.Count; i++)
            {
                if (chapters[i].Index == chapter) return i;
            }
            return -1;
        }

        private int PreferredLevel(int chapter)
        {
            CampaignRadialChapter data = FindChapter(chapter);
            if (data == null || data.Levels.Count == 0) return 1;

            for (int i = 0; i < data.Levels.Count; i++)
            {
                if (data.Levels[i].Current) return data.Levels[i].Level;
            }
            for (int i = 0; i < data.Levels.Count; i++)
            {
                CampaignRadialLevel level = data.Levels[i];
                if (level.Unlocked && !level.Cleared) return level.Level;
            }
            for (int i = 0; i < data.Levels.Count; i++)
            {
                if (data.Levels[i].Unlocked) return data.Levels[i].Level;
            }
            return data.Levels[data.Levels.Count - 1].Level;
        }

        private void AddWedge(Painter2D painter, RadialGeometry geo, float start, float end, float inner, float outer)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((end - start) / 2f));
            Vector2 point = PolarPoint(geo.Center, outer, start);
            painter.BeginPath();
            painter.MoveTo(point);
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                painter.LineTo(PolarPoint(geo.Center, outer, angle));
            }
            for (int i = steps; i >= 0; i--)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                painter.LineTo(PolarPoint(geo.Center, inner, angle));
            }
            painter.ClosePath();
        }

        private void AddArc(Painter2D painter, RadialGeometry geo, float radius, float start, float end)
        {
            int steps = Mathf.Max(2, Mathf.CeilToInt((end - start) / 2f));
            painter.BeginPath();
            painter.MoveTo(PolarPoint(geo.Center, radius, start));
            for (int i = 1; i <= steps; i++)
            {
                float angle = Mathf.Lerp(start, end, i / (float)steps);
                painter.LineTo(PolarPoint(geo.Center, radius, angle));
            }
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            Color result = color;
            result.a = alpha;
            return result;
        }

        private void DrawChapterBand(Painter2D painter, RadialGeometry geo,
            float start, float end, int arrayIndex)
        {
            float outer = geo.Outer;
            CampaignRadialChapter chapter = chapters[arrayIndex];

            AddWedge(painter, geo, start, end, geo.ArcInner, outer);
            painter.fillColor = chapter.Locked
                ? WithAlpha(PaletteSettings.Subtext0, 0.2f)
                : PaletteSettings.Surface0;
            painter.Fill();

            if (!chapter.Locked && chapterProgress[arrayIndex] > 0f)
            {
                float fillEnd = Mathf.Lerp(start, end, chapterProgress[arrayIndex]);
                AddWedge(painter, geo, start, fillEnd, geo.ArcInner, outer);
                painter.fillColor = PaletteSettings.Blue;
                painter.Fill();
            }
        }

        private void DrawLevelPie(Painter2D painter, RadialGeometry geo,
            float start, float end, int arrayIndex, int activeLevel)
        {
            CampaignRadialChapter chapter = chapters[arrayIndex];
            float span = (end - start) / Mathf.Max(1, chapter.Levels.Count);
            float inner = geo.Outer;
            float outer = inner + geo.LevelBand * chapterGrowth[arrayIndex];

            for (int l = 0; l < chapter.Levels.Count; l++)
            {
                CampaignRadialLevel level = chapter.Levels[l];
                float levelStart = start + l * span;
                float levelEnd = levelStart + span;
                bool locked = !level.Unlocked && !level.Cleared && !level.Current;

                AddWedge(painter, geo, levelStart, levelEnd, inner, outer);
                painter.fillColor = level.Cleared
                    ? WithAlpha(PaletteSettings.Blue, 0.85f)
                    : (locked
                        ? WithAlpha(PaletteSettings.Subtext0, 0.22f)
                        : WithAlpha(PaletteSettings.Sapphire, 0.45f));
                painter.Fill();

                bool highlight = level.Level == activeLevel && (hoverChapter == 0 || hoverIsLevel);
                if (highlight)
                {
                    painter.strokeColor = PaletteSettings.Crust;
                    painter.lineWidth = geo.Outline + Mathf.Max(1f, geo.Outline * 0.5f);
                    painter.Stroke();
                    painter.strokeColor = PaletteSettings.Yellow;
                    painter.lineWidth = geo.Outline;
                    painter.Stroke();
                }
            }
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            if (chapters.Count == 0) return;
            RadialGeometry geo = GetGeometry();
            if (geo.Scale <= 0f) return;

            Painter2D painter = ctx.painter2D;
            float span = LevelSpan();
            int activeLevel = ActiveLevel();
            float angle = 0f;

            for (int i = 0; i < chapters.Count; i++)
            {
                float chapterSpan = chapters[i].Levels.Count * span;
                DrawChapterBand(painter, geo, angle + geo.GapHalf, angle + chapterSpan - geo.GapHalf, i);
                angle += chapterSpan;
            }

            angle = 0f;
            for (int i = 0; i < chapters.Count; i++)
            {
                int previous = (i - 1 + chapters.Count) % chapters.Count;
                float previousExtent = geo.Outer + geo.LevelBand * chapterGrowth[previous];
                float currentExtent = geo.Outer + geo.LevelBand * chapterGrowth[i];
                float outerExtent = Mathf.Max(previousExtent, currentExtent) + geo.Outline;
                AddWedge(painter, geo, angle - geo.GapHalf, angle + geo.GapHalf, geo.ArcInner, outerExtent);
                painter.fillColor = PaletteSettings.Base;
                painter.Fill();
                angle += chapters[i].Levels.Count * span;
            }

            angle = 0f;
            for (int i = 0; i < chapters.Count; i++)
            {
                float chapterSpan = chapters[i].Levels.Count * span;
                if (chapterGrowth[i] > 0.02f)
                    DrawLevelPie(painter, geo, angle + geo.GapHalf, angle + chapterSpan - geo.GapHalf, i, activeLevel);
                angle += chapterSpan;
            }

            angle = 0f;
            painter.strokeColor = PaletteSettings.Base;
            painter.lineWidth = geo.Outline;
            for (int i = 0; i < chapters.Count; i++)
            {
                float chapterSpan = chapters[i].Levels.Count * span;
                if (chapterGrowth[i] <= 0.02f)
                {
                    AddArc(painter, geo, geo.Outer, angle + geo.GapHalf, angle + chapterSpan - geo.GapHalf);
                    painter.Stroke();
                }
                angle += chapterSpan;
            }

            AddWedge(painter, geo, 0f, 360f, 0f, geo.CenterRadius);
            painter.fillColor = PaletteSettings.Crust;
            painter.Fill();
            AddArc(painter, geo, geo.CenterRadius, 0f, 360f);
            painter.strokeColor = PaletteSettings.Base;
            painter.lineWidth = geo.Outline;
            painter.Stroke();
        }

        private bool HitTest(Vector2 localPoint, out int chapter, out int level, out bool overLevelPie)
        {
            chapter = 0;
            level = 0;
            overLevelPie = false;
            if (chapters.Count == 0) return false;

            RadialGeometry geo = GetGeometry();
            Vector2 delta = localPoint - geo.Center;
            float radius = delta.magnitude;
            if (radius < geo.ArcInner - 4f * geo.Scale) return false;

            float angle = Mathf.Atan2(delta.x, -delta.y) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            if (angle >= 359.99f) angle = 359.99f;

            float boundary = 0f;
            float span = LevelSpan();
            for (int c = 0; c < chapters.Count; c++)
            {
                float chapterSpan = chapters[c].Levels.Count * span;
                float start = boundary + geo.GapHalf;
                float end = boundary + chapterSpan - geo.GapHalf;
                if (angle >= start && angle < end)
                {
                    int arrayIndex = ChapterArrayIndex(chapters[c].Index);
                    float pieDepth = arrayIndex >= 0 ? geo.LevelBand * chapterGrowth[arrayIndex] : 0f;
                    if (radius > geo.Outer + pieDepth + 2f * geo.Scale) return false;

                    chapter = chapters[c].Index;
                    if (radius > geo.Outer - 2f * geo.Scale)
                    {
                        float insetSpan = (end - start) / Mathf.Max(1, chapters[c].Levels.Count);
                        int offset = Mathf.FloorToInt((angle - start) / insetSpan);
                        level = Mathf.Clamp(offset + 1, 1, chapters[c].Levels.Count);
                        overLevelPie = true;
                    }
                    else
                    {
                        level = PreferredLevel(chapter);
                    }
                    return true;
                }
                boundary += chapterSpan;
            }
            return false;
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!HitTest(evt.localPosition, out int chapter, out int level, out bool overLevelPie))
            {
                ClearActive();
                return;
            }

            hasActive = true;
            if (!HasKeyboardFocus) Focus();
            if (chapter == hoverChapter && level == hoverLevel && overLevelPie == hoverIsLevel) return;
            hoverChapter = chapter;
            hoverLevel = level;
            hoverIsLevel = overLevelPie;
            MarkDirtyRepaint();
            NotifyPreview(overLevelPie ? RadialPreviewSource.LevelSlice : RadialPreviewSource.ChapterArc);
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (!HitTest(evt.localPosition, out int chapter, out int level, out bool overLevelPie)) return;
            if (!overLevelPie) return;
            evt.StopPropagation();
            Focus();

            CampaignRadialLevel target = FindLevel(chapter, level);
            if (target == null || !target.Unlocked)
            {
                LevelRejected?.Invoke();
                return;
            }

            SetSelection(chapter, level);
            LevelLaunched?.Invoke(chapter, level);
        }

        private void UpdateOverlayLayout()
        {
            RadialGeometry geo = GetGeometry();
            if (geo.Scale <= 0f || geo.Center.x <= 0f) return;

            float centerSize = Mathf.Max(8f, geo.CenterRadius * 1.7f);
            centerNumber.style.left = geo.Center.x - centerSize * 0.5f;
            centerNumber.style.top = geo.Center.y - centerSize * 0.5f;
            centerNumber.style.width = centerSize;
            centerNumber.style.height = centerSize;
            centerNumber.style.fontSize = Mathf.Max(8f, geo.CenterRadius * 0.82f);
            if (centerReveal >= 1f)
            {
                centerNumber.style.opacity = 1f;
                centerNumber.style.scale = new Scale(Vector2.one);
            }

            float span = LevelSpan();
            float angle = 0f;
            int activeChapter = ActiveChapter();
            int activeLevel = ActiveLevel();

            for (int c = 0; c < chapters.Count; c++)
            {
                CampaignRadialChapter chapter = chapters[c];
                bool visible = chapter.Index == activeChapter && chapterGrowth[c] > 0.02f;
                float labelRadius = geo.Outer + geo.LevelBand * chapterGrowth[c] * 0.5f;

                float chapterSpan = chapter.Levels.Count * span;
                float insetSpan = (chapterSpan - 2f * geo.GapHalf) / Mathf.Max(1, chapter.Levels.Count);
                for (int l = 0; l < chapter.Levels.Count; l++)
                {
                    CampaignRadialLevel level = chapter.Levels[l];
                    if (level.Label == null) continue;
                    level.Label.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
                    if (!visible) continue;

                    float mid = angle + geo.GapHalf + (l + 0.5f) * insetSpan;
                    Vector2 point = PolarPoint(geo.Center, labelRadius, mid);
                    level.Label.style.left = Mathf.Clamp(point.x - 20f * geo.Scale, 0f, contentRect.width - 40f * geo.Scale);
                    level.Label.style.top = Mathf.Clamp(point.y - 14f * geo.Scale, 0f, contentRect.height - 28f * geo.Scale);
                    bool highlight = level.Level == activeLevel && (hoverChapter == 0 || hoverIsLevel);
                    level.Label.style.color = highlight
                        ? PaletteSettings.Yellow
                        : PaletteSettings.TextPrimary;
                }
                angle += chapterSpan;
            }
        }
    }
}
