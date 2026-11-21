using UnityEngine;
using UnityEngine.UIElements;

namespace Polymulator
{
    public enum HudIconKind
    {
        Limited,
        Flexible,
        Translasi,
        Elevasi,
        Rotasi,
        Throw,
        Telekinesis,
        Partition,
        Fusion
    }

    /// <summary>
    /// Crisp resolution-independent geometric vector icon rendered via UI Toolkit Painter2D.
    /// Zero external SVG/texture dependency, tinted cleanly via USS color or code.
    /// </summary>
    [UxmlElement]
    public sealed partial class HudVectorIcon : VisualElement
    {
        private HudIconKind iconKind = HudIconKind.Limited;
        private Color? tintOverride;

        [UxmlAttribute("icon-kind")]
        public HudIconKind IconKind
        {
            get => iconKind;
            set
            {
                if (iconKind == value) return;
                iconKind = value;
                MarkDirtyRepaint();
            }
        }

        public HudVectorIcon()
        {
            pickingMode = PickingMode.Ignore;
            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetTint(Color color)
        {
            tintOverride = color;
            MarkDirtyRepaint();
        }

        public void ClearTint()
        {
            tintOverride = null;
            MarkDirtyRepaint();
        }

        private void OnGenerateVisualContent(MeshGenerationContext ctx)
        {
            Rect r = contentRect;
            float size = Mathf.Min(r.width, r.height);
            if (size <= 2f) return;

            float s = size / 24f;
            float ox = (r.width - size) * 0.5f;
            float oy = (r.height - size) * 0.5f;

            Vector2 P(float x, float y) => new Vector2(ox + x * s, oy + y * s);

            Painter2D painter = ctx.painter2D;
            Color stroke = tintOverride ?? resolvedStyle.color;
            painter.strokeColor = stroke;
            painter.fillColor = stroke;
            painter.lineWidth = Mathf.Max(1.4f, 1.85f * s);
            painter.lineCap = LineCap.Round;
            painter.lineJoin = LineJoin.Round;

            switch (iconKind)
            {
                case HudIconKind.Limited:
                    // Crisp bounded square frame + centered cube dot
                    painter.BeginPath();
                    painter.MoveTo(P(5f, 5f));
                    painter.LineTo(P(19f, 5f));
                    painter.LineTo(P(19f, 19f));
                    painter.LineTo(P(5f, 19f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(9.5f, 9.5f));
                    painter.LineTo(P(14.5f, 9.5f));
                    painter.LineTo(P(14.5f, 14.5f));
                    painter.LineTo(P(9.5f, 14.5f));
                    painter.ClosePath();
                    painter.Fill();
                    break;

                case HudIconKind.Flexible:
                    // Open diamond + centered orbital core
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 3.5f));
                    painter.LineTo(P(20.5f, 12f));
                    painter.LineTo(P(12f, 20.5f));
                    painter.LineTo(P(3.5f, 12f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 3.2f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Fill();
                    break;

                case HudIconKind.Translasi:
                    // 4-way planar movement cross arrows
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 3.5f)); painter.LineTo(P(12f, 20.5f));
                    painter.MoveTo(P(3.5f, 12f)); painter.LineTo(P(20.5f, 12f));
                    painter.MoveTo(P(9.5f, 6f)); painter.LineTo(P(12f, 3.5f)); painter.LineTo(P(14.5f, 6f));
                    painter.MoveTo(P(9.5f, 18f)); painter.LineTo(P(12f, 20.5f)); painter.LineTo(P(14.5f, 18f));
                    painter.MoveTo(P(6f, 9.5f)); painter.LineTo(P(3.5f, 12f)); painter.LineTo(P(6f, 14.5f));
                    painter.MoveTo(P(18f, 9.5f)); painter.LineTo(P(20.5f, 12f)); painter.LineTo(P(18f, 14.5f));
                    painter.Stroke();
                    break;

                case HudIconKind.Elevasi:
                    // Vertical shaft with opposing lift arrowheads + base plane
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 4f)); painter.LineTo(P(12f, 17.5f));
                    painter.MoveTo(P(8.5f, 7.5f)); painter.LineTo(P(12f, 4f)); painter.LineTo(P(15.5f, 7.5f));
                    painter.MoveTo(P(8.5f, 14f)); painter.LineTo(P(12f, 17.5f)); painter.LineTo(P(15.5f, 14f));
                    painter.MoveTo(P(5f, 20.5f)); painter.LineTo(P(19f, 20.5f));
                    painter.Stroke();
                    break;

                case HudIconKind.Rotasi:
                    // Circular rotation arc + arrow tip
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 7f * s, Angle.Degrees(30f), Angle.Degrees(315f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(16f, 4.5f));
                    painter.LineTo(P(19f, 8f));
                    painter.LineTo(P(14.5f, 9f));
                    painter.Stroke();
                    break;

                case HudIconKind.Throw:
                    // Parabolic throw trajectory arc + arrowhead
                    painter.BeginPath();
                    painter.MoveTo(P(4f, 18f));
                    painter.QuadraticCurveTo(P(11f, 3f), P(19.5f, 11.5f));
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(15.5f, 9f));
                    painter.LineTo(P(19.8f, 11.8f));
                    painter.LineTo(P(17f, 15.5f));
                    painter.Stroke();
                    break;

                case HudIconKind.Telekinesis:
                    // Concentric beam emitters + floating core
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 3f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Fill();
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 6.2f * s, Angle.Degrees(210f), Angle.Degrees(330f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 8.8f * s, Angle.Degrees(30f), Angle.Degrees(150f), ArcDirection.Clockwise);
                    painter.Stroke();
                    break;

                case HudIconKind.Partition:
                    // Split cube halves with center cut line
                    painter.BeginPath();
                    painter.MoveTo(P(4f, 6f)); painter.LineTo(P(9.5f, 6f)); painter.LineTo(P(9.5f, 18f)); painter.LineTo(P(4f, 18f)); painter.ClosePath();
                    painter.MoveTo(P(14.5f, 6f)); painter.LineTo(P(20f, 6f)); painter.LineTo(P(20f, 18f)); painter.LineTo(P(14.5f, 18f)); painter.ClosePath();
                    painter.MoveTo(P(12f, 3.5f)); painter.LineTo(P(12f, 20.5f));
                    painter.Stroke();
                    break;

                case HudIconKind.Fusion:
                    // Two converging blocks merging toward center
                    painter.BeginPath();
                    painter.MoveTo(P(4.5f, 6.5f)); painter.LineTo(P(11.5f, 6.5f)); painter.LineTo(P(11.5f, 17.5f)); painter.LineTo(P(4.5f, 17.5f)); painter.ClosePath();
                    painter.MoveTo(P(12.5f, 6.5f)); painter.LineTo(P(19.5f, 6.5f)); painter.LineTo(P(19.5f, 17.5f)); painter.LineTo(P(12.5f, 17.5f)); painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 2.5f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Fill();
                    break;
            }
        }
    }
}
