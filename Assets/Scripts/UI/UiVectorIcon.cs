using UnityEngine;
using UnityEngine.UIElements;

namespace Polymulator
{
    public enum UiIconKind
    {
        Limited,
        Flexible,
        Translasi,
        Elevasi,
        Rotasi,
        Throw,
        Telekinesis,
        Partition,
        Fusion,
        Back,
        StarFilled,
        StarOutline,
        Weight,
        Play,
        Lock,
        Settings,
        Volume,
        Language,
        ReduceMotion,
        Camera
    }

    /// <summary>
    /// Crisp resolution-independent geometric vector icon rendered via UI Toolkit Painter2D.
    /// Zero external SVG/texture dependency, tinted cleanly via USS color or code.
    /// </summary>
    [UxmlElement]
    public sealed partial class UiVectorIcon : VisualElement
    {
        private UiIconKind iconKind = UiIconKind.Limited;
        private Color? tintOverride;

        [UxmlAttribute("icon-kind")]
        public UiIconKind IconKind
        {
            get => iconKind;
            set
            {
                if (iconKind == value) return;
                iconKind = value;
                MarkDirtyRepaint();
            }
        }

        public UiVectorIcon()
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
                case UiIconKind.Limited:
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

                case UiIconKind.Flexible:
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

                case UiIconKind.Translasi:
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

                case UiIconKind.Elevasi:
                    // Vertical shaft with opposing lift arrowheads + base plane
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 4f)); painter.LineTo(P(12f, 17.5f));
                    painter.MoveTo(P(8.5f, 7.5f)); painter.LineTo(P(12f, 4f)); painter.LineTo(P(15.5f, 7.5f));
                    painter.MoveTo(P(8.5f, 14f)); painter.LineTo(P(12f, 17.5f)); painter.LineTo(P(15.5f, 14f));
                    painter.MoveTo(P(5f, 20.5f)); painter.LineTo(P(19f, 20.5f));
                    painter.Stroke();
                    break;

                case UiIconKind.Rotasi:
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

                case UiIconKind.Throw:
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

                case UiIconKind.Telekinesis:
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

                case UiIconKind.Partition:
                    // Split cube halves with center cut line
                    painter.BeginPath();
                    painter.MoveTo(P(4f, 6f)); painter.LineTo(P(9.5f, 6f)); painter.LineTo(P(9.5f, 18f)); painter.LineTo(P(4f, 18f)); painter.ClosePath();
                    painter.MoveTo(P(14.5f, 6f)); painter.LineTo(P(20f, 6f)); painter.LineTo(P(20f, 18f)); painter.LineTo(P(14.5f, 18f)); painter.ClosePath();
                    painter.MoveTo(P(12f, 3.5f)); painter.LineTo(P(12f, 20.5f));
                    painter.Stroke();
                    break;

                case UiIconKind.Fusion:
                    // Two converging blocks merging toward center
                    painter.BeginPath();
                    painter.MoveTo(P(4.5f, 6.5f)); painter.LineTo(P(11.5f, 6.5f)); painter.LineTo(P(11.5f, 17.5f)); painter.LineTo(P(4.5f, 17.5f)); painter.ClosePath();
                    painter.MoveTo(P(12.5f, 6.5f)); painter.LineTo(P(19.5f, 6.5f)); painter.LineTo(P(19.5f, 17.5f)); painter.LineTo(P(12.5f, 17.5f)); painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 2.5f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Fill();
                    break;

                case UiIconKind.Back:
                    // Left chevron
                    painter.BeginPath();
                    painter.MoveTo(P(15f, 5f)); painter.LineTo(P(8f, 12f)); painter.LineTo(P(15f, 19f));
                    painter.Stroke();
                    break;

                case UiIconKind.StarFilled:
                case UiIconKind.StarOutline:
                {
                    float inset = iconKind == UiIconKind.StarOutline ? painter.lineWidth * 0.5f / s : 0f;
                    float k = Mathf.Clamp01(1f - inset / 12f);
                    Vector2 Q(float x, float y) => P(12f + (x - 12f) * k, 12f + (y - 12f) * k);
                    Vector2[] pts =
                    {
                        Q(12f, 3.5f), Q(14.2f, 9.3f), Q(20.1f, 9.9f), Q(15.5f, 13.6f), Q(17.2f, 19.4f),
                        Q(12f, 15.9f), Q(6.8f, 19.4f), Q(8.5f, 13.6f), Q(3.9f, 9.9f), Q(9.8f, 9.3f)
                    };
                    painter.BeginPath();
                    painter.MoveTo(pts[0]);
                    for (int i = 1; i < pts.Length; i++) painter.LineTo(pts[i]);
                    painter.ClosePath();
                    if (iconKind == UiIconKind.StarFilled) painter.Fill(); else painter.Stroke();
                    break;
                }

                case UiIconKind.Weight:
                    // Mass block with a downward pressure arrow
                    painter.BeginPath();
                    painter.MoveTo(P(7f, 11f)); painter.LineTo(P(17f, 11f)); painter.LineTo(P(17f, 18f)); painter.LineTo(P(7f, 18f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 3.5f)); painter.LineTo(P(12f, 9f));
                    painter.MoveTo(P(9.5f, 6.7f)); painter.LineTo(P(12f, 9f)); painter.LineTo(P(14.5f, 6.7f));
                    painter.Stroke();
                    break;

                case UiIconKind.Play:
                    // Right-facing triangle
                    painter.BeginPath();
                    painter.MoveTo(P(8f, 4.5f)); painter.LineTo(P(20f, 12f)); painter.LineTo(P(8f, 19.5f));
                    painter.ClosePath();
                    painter.Fill();
                    break;

                case UiIconKind.Lock:
                    // Padlock with an open shackle and a keyhole
                    painter.BeginPath();
                    painter.Arc(P(12f, 10.5f), 4.2f * s, Angle.Degrees(180f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(6.5f, 10.5f)); painter.LineTo(P(17.5f, 10.5f));
                    painter.LineTo(P(17.5f, 19f)); painter.LineTo(P(6.5f, 19f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 14.5f), 1.4f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Fill();
                    break;

                case UiIconKind.Settings:
                    // Gear ring with eight radial teeth
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 4.6f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Stroke();
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 0.25f;
                        float dx = Mathf.Cos(a), dy = Mathf.Sin(a);
                        painter.BeginPath();
                        painter.MoveTo(P(12f + dx * 6f, 12f + dy * 6f));
                        painter.LineTo(P(12f + dx * 8.6f, 12f + dy * 8.6f));
                        painter.Stroke();
                    }
                    break;

                case UiIconKind.Volume:
                    // Speaker cone with two sound waves
                    painter.BeginPath();
                    painter.MoveTo(P(4f, 9f)); painter.LineTo(P(8f, 9f)); painter.LineTo(P(13f, 5f));
                    painter.LineTo(P(13f, 19f)); painter.LineTo(P(8f, 15f)); painter.LineTo(P(4f, 15f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(13f, 12f), 3f * s, Angle.Degrees(-45f), Angle.Degrees(45f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(13f, 12f), 5.4f * s, Angle.Degrees(-45f), Angle.Degrees(45f), ArcDirection.Clockwise);
                    painter.Stroke();
                    break;

                case UiIconKind.Language:
                    // Globe with equator and two curved meridians
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 8f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(4f, 12f)); painter.LineTo(P(20f, 12f));
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(12f, 4f));
                    painter.QuadraticCurveTo(P(7f, 12f), P(12f, 20f));
                    painter.QuadraticCurveTo(P(17f, 12f), P(12f, 4f));
                    painter.Stroke();
                    break;

                case UiIconKind.ReduceMotion:
                    // Motion arc crossed out
                    painter.BeginPath();
                    painter.Arc(P(12f, 12f), 7f * s, Angle.Degrees(20f), Angle.Degrees(340f), ArcDirection.Clockwise);
                    painter.Stroke();
                    painter.BeginPath();
                    painter.MoveTo(P(5.5f, 18.5f)); painter.LineTo(P(18.5f, 5.5f));
                    painter.Stroke();
                    break;

                case UiIconKind.Camera:
                    // Camera body with protruding viewfinder and lens
                    painter.BeginPath();
                    painter.MoveTo(P(3.5f, 8f)); painter.LineTo(P(7f, 8f)); painter.LineTo(P(9f, 5.5f));
                    painter.LineTo(P(15f, 5.5f)); painter.LineTo(P(17f, 8f)); painter.LineTo(P(20.5f, 8f));
                    painter.LineTo(P(20.5f, 19f)); painter.LineTo(P(3.5f, 19f));
                    painter.ClosePath();
                    painter.Stroke();
                    painter.BeginPath();
                    painter.Arc(P(12f, 13.5f), 4.2f * s, Angle.Degrees(0f), Angle.Degrees(360f), ArcDirection.Clockwise);
                    painter.Stroke();
                    break;
            }
        }
    }
}
