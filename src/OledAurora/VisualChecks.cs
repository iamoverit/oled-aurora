using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OledAurora.Core;

namespace OledAurora;

internal static class VisualChecks
{
    // Inspect actual WPF output, including alpha after compositing the opacity mask.
    // No desktop capture or visible window is needed.
    internal static int Run(string reportPath)
    {
        var results = new List<object>();
        var directory = Path.GetDirectoryName(Path.GetFullPath(reportPath))!;
        Directory.CreateDirectory(directory);
        try
        {
            var sizes = new[]
            {
                (Width: 960, Height: 27, Scale: 1.0),   // 5% at a small preview scale
                (Width: 960, Height: 108, Scale: 1.0),  // 20%
                (Width: 960, Height: 216, Scale: 1.0),  // 40%
                (Width: 1920, Height: 216, Scale: 1.5),
                (Width: 3840, Height: 864, Scale: 2.0)
            };
            foreach (var palette in Enum.GetValues<WavePalette>())
            {
                // Reuse the visual to verify mask updates after a range/DPI change.
                var visual = new AuroraVisual();
                foreach (var size in sizes)
                {
                    byte[]? firstFrame = null;
                    foreach (double time in new[] { 0.0, 2.4, 6.0, 15.0 })
                    {
                        var bitmap = Render(visual, size.Width, size.Height, size.Scale, time, palette);
                        var pixels = Pixels(bitmap);
                        string label = $"{palette} {size.Width}x{size.Height} @{size.Scale:0.0} t={time:0.0}";
                        Check(MaxAlpha(pixels, size.Width, 0, (int)(size.Height * .7)) > 10, label + " has visible content");
                        Check(MaxAlpha(pixels, size.Width, (int)Math.Ceiling(size.Height * .96), size.Height) == 0,
                            label + " has a transparent bottom margin");
                        Check(MaxAlpha(pixels, size.Width, (int)Math.Ceiling(size.Height * .8), size.Height) <= 24,
                            label + " fades before the bottom margin");
                        if (firstFrame is null) firstFrame = pixels;
                        else Check(Difference(firstFrame, pixels) > .1, label + " moves across time");
                        if (size.Width == 960 && size.Height == 108 && time == 2.4)
                            SavePng(bitmap, Path.Combine(directory, $"animation-{palette}.png"));
                    }
                }
            }
            // Keep a short sequence for reviewing curtain motion and edge behavior.
            if (Enum.TryParse<WavePalette>("NorthernLights", out var northernLights))
            {
                var visual = new AuroraVisual();
                for (int i = 0; i < 12; i++)
                    SavePng(Render(visual, 960, 192, 1, i * .5, northernLights), Path.Combine(directory, $"northern-lights-{i:00}.png"));
            }
            WriteReport(true, null);
            return 0;
        }
        catch (Exception ex) { WriteReport(false, ex.ToString()); return 1; }

        void Check(bool value, string label)
        {
            results.Add(new { check = label, passed = value });
            if (!value) throw new InvalidOperationException(label);
        }
        void WriteReport(bool passed, string? error) => File.WriteAllText(reportPath,
            JsonSerializer.Serialize(new { passed, checks = results, error }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static RenderTargetBitmap Render(AuroraVisual visual, int width, int height, double scale, double seconds, WavePalette palette)
    {
        // A clipped parent reproduces the layered window's hard rectangular limit.
        var parent = new Grid { Width = width / scale, Height = height / scale, ClipToBounds = true };
        if (visual.Parent is Panel previous) previous.Children.Remove(visual);
        parent.Children.Add(visual);
        visual.SetFrame(seconds, .8, palette);
        parent.Measure(new Size(parent.Width, parent.Height));
        parent.Arrange(new Rect(0, 0, parent.Width, parent.Height));
        parent.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        bitmap.Render(parent);
        return bitmap;
    }

    private static byte[] Pixels(BitmapSource bitmap)
    {
        var pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight * 4];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth * 4, 0);
        return pixels;
    }

    private static byte MaxAlpha(byte[] pixels, int width, int startY, int endY)
    {
        byte max = 0;
        for (int i = startY * width * 4 + 3; i < endY * width * 4; i += 4)
            if (pixels[i] > max) max = pixels[i];
        return max;
    }

    private static double Difference(byte[] first, byte[] second)
    {
        long difference = 0;
        for (int i = 0; i < first.Length; i++) difference += Math.Abs(first[i] - second[i]);
        return (double)difference / first.Length;
    }

    internal static void SavePng(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
