using System;
using System.Windows;
using System.Windows.Media;
using OledAurora.Core;

namespace OledAurora;

public sealed class AuroraVisual : FrameworkElement
{
    private static readonly Brush[] AuroraBrushes = [Cloud(119, 177, 179), Cloud(167, 149, 194), Cloud(124, 160, 185), Cloud(162, 186, 182)];
    private static readonly Brush[] SmokeBrushes = [Cloud(153, 160, 170), Cloud(117, 126, 140), Cloud(169, 176, 186), Cloud(129, 139, 149)];
    private static readonly Brush[] CurtainBrushes = [Cloud(94, 226, 154), Cloud(72, 201, 197), Cloud(163, 130, 213)];
    private static readonly Brush[] RibbonBrushes = [Solid(110, 232, 166), Solid(91, 211, 201), Solid(172, 144, 218)];
    private double _time = 2.4;
    private WavePalette _palette;

    public AuroraVisual()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (ActualHeight <= 0) return;
        // RelativeToBoundingBox would include the overflowing cloud geometry, so its
        // transparent stop could land below the window and be cut off. Anchor the mask
        // to the actual viewport in DIPs, independent of the animation's drawing bounds.
        var feather = new LinearGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            StartPoint = new Point(0, 0),
            EndPoint = new Point(0, ActualHeight),
            SpreadMethod = GradientSpreadMethod.Pad
        };
        feather.GradientStops.Add(new GradientStop(Colors.White, 0));
        for (int i = 0; i <= 32; i++)
        {
            double progress = i / 32.0;
            byte alpha = (byte)Math.Round(255 * (1 - AnimationMath.SmoothStep(progress)));
            feather.GradientStops.Add(new GradientStop(Color.FromArgb(alpha, 255, 255, 255), .25 + .69 * progress));
        }
        // The final 6% is fully transparent, including rasterisation/filter margins.
        feather.GradientStops.Add(new GradientStop(Colors.Transparent, 1));
        feather.Freeze();
        OpacityMask = feather;
    }

    public void SetFrame(double seconds, double opacity, WavePalette palette)
    {
        _time = seconds;
        _palette = palette;
        Opacity = Math.Clamp(opacity, 0, 1);
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        double width = ActualWidth, height = ActualHeight;
        if (width <= 0 || height <= 0) return;
        if (_palette == WavePalette.NorthernLights)
        {
            DrawNorthernLights(dc, width, height);
            return;
        }
        var brushes = _palette == WavePalette.Smoke ? SmokeBrushes : AuroraBrushes;
        // Large overlapping, softly shaded fields change broad areas without sharp edges.
        for (int i = -1; i <= 4; i++)
        {
            var phase = _time * .52 + i * 1.48;
            var x = width * (i * .29 + .1 + Math.Sin(phase) * .17);
            var y = height * (.13 + .17 * Math.Sin(phase * .8 + i));
            var rx = width * (.38 + .04 * Math.Cos(phase));
            var ry = height * (1.05 + .15 * Math.Sin(phase * .7));
            dc.PushOpacity(.64 + .24 * Math.Sin(phase + .7));
            dc.DrawEllipse(brushes[(i + 4) % 4], null, new Point(x, y), rx, ry);
            dc.Pop();
        }
    }

    private void DrawNorthernLights(DrawingContext dc, double width, double height)
    {
        // Broad drifting curtains, with diffuse vertical light and a gently folded
        // lower ribbon. All geometry scales with the selected viewport height.
        for (int layer = 2; layer >= 0; layer--)
        {
            double phase = _time * .34 + layer * 1.9;
            double strength = .65 + .15 * Math.Sin(phase * .8);
            dc.PushOpacity(strength);
            for (int ray = -4; ray <= 74; ray++)
            {
                double u = (ray + .16 * Math.Sin(ray * 1.73) + .12 * Math.Sin(ray * 2.81)) / 70.0;
                double drift = .014 * Math.Sin(phase + u * 13);
                double x = width * (u + drift);
                double bottom = height * CurtainY(u, phase, layer);
                double length = height * (.36 + .11 * Math.Sin(u * 19 - phase * .6));
                double rayWidth = width * (.011 + .004 * Math.Sin(u * 31 + phase));
                double intensity = .22 + .38 * AnimationMath.SmoothStep(.5 + .5 * Math.Sin(u * 37 + phase));
                dc.PushOpacity(intensity);
                dc.DrawEllipse(CurtainBrushes[layer], null,
                    new Point(x, bottom - length * .48), rayWidth, length * .65);
                dc.Pop();
            }
            // Nested bands create a soft halo instead of applying a blur that would
            // spill beyond the region. The common viewport mask is applied last.
            for (int glow = 5; glow >= 0; glow--)
            {
                double halfThickness = height * (.010 + glow * .017);
                var ribbon = new StreamGeometry();
                using (var geometry = ribbon.Open())
                {
                    const int segments = 100;
                    geometry.BeginFigure(new Point(-width * .06, height * CurtainY(-.06, phase, layer) - halfThickness), true, true);
                    for (int point = 1; point <= segments; point++)
                    {
                        double u = -.06 + 1.12 * point / segments;
                        geometry.LineTo(new Point(width * u, height * CurtainY(u, phase, layer) - halfThickness), true, false);
                    }
                    for (int point = segments; point >= 0; point--)
                    {
                        double u = -.06 + 1.12 * point / segments;
                        geometry.LineTo(new Point(width * u, height * CurtainY(u, phase, layer) + halfThickness), true, false);
                    }
                }
                ribbon.Freeze();
                dc.PushOpacity(glow == 0 ? .36 : .055);
                dc.DrawGeometry(RibbonBrushes[layer], null, ribbon);
                dc.Pop();
            }
            dc.Pop();
        }
    }

    private static double CurtainY(double u, double phase, int layer)
        => .33 + layer * .07 + .075 * Math.Sin(u * 7.5 + phase)
           + .032 * Math.Sin(u * 17 - phase * .7 + layer);

    private static Brush Solid(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Brush Cloud(byte r, byte g, byte b)
    {
        var brush = new RadialGradientBrush();
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(240, r, g, b), 0));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(140, r, g, b), .42));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(35, r, g, b), .76));
        brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, r, g, b), 1));
        brush.Freeze();
        return brush;
    }
}
