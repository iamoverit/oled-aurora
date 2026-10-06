using System;
using System.Windows;
using System.Windows.Media;
using OledAurora.Core;

namespace OledAurora;

public sealed class AuroraVisual : FrameworkElement
{
    private static readonly Brush[] AuroraBrushes = [Cloud(119, 177, 179), Cloud(167, 149, 194), Cloud(124, 160, 185), Cloud(162, 186, 182)];
    private static readonly Brush[] SmokeBrushes = [Cloud(153, 160, 170), Cloud(117, 126, 140), Cloud(169, 176, 186), Cloud(129, 139, 149)];
    private double _time = 2.4;
    private WavePalette _palette;

    public AuroraVisual()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        var feather = new LinearGradientBrush();
        feather.StartPoint = new Point(0, 0);
        feather.EndPoint = new Point(0, 1);
        feather.GradientStops.Add(new GradientStop(Colors.White, 0));
        feather.GradientStops.Add(new GradientStop(Colors.White, .45));
        feather.GradientStops.Add(new GradientStop(Color.FromArgb(140, 255, 255, 255), .72));
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
        var brushes = _palette == WavePalette.Aurora ? AuroraBrushes : SmokeBrushes;
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
