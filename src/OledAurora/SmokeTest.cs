using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OledAurora.Core;

namespace OledAurora;

internal static class SmokeTest
{
    // Runs the actual WPF/Win32 overlay without loading or writing user preferences.
    internal static async Task<int> RunAsync(string reportPath)
    {
        var results = new List<object>();
        OverlayWindow? overlay = null;
        try
        {
            var screen = MonitorService.Resolve("");
            var settings = new AppSettings { DurationSeconds = 2, OpacityPercent = 5 };
            overlay = new OverlayWindow();
            overlay.Play(settings, screen);
            // The user may change applications while the test is running.
            // Assert that the overlay never takes focus, rather than freezing their foreground window.
            bool focusStayedOutsideOverlay = NativeMethods.GetForegroundWindow() != overlay.Handle;
            await Task.Delay(450);
            var styles = NativeMethods.GetWindowLongPtr(overlay.Handle, NativeMethods.GwlExStyle).ToInt64();
            Check((styles & (NativeMethods.ExLayered | NativeMethods.ExTransparent | NativeMethods.ExNoActivate | NativeMethods.ExToolWindow))
                == (NativeMethods.ExLayered | NativeMethods.ExTransparent | NativeMethods.ExNoActivate | NativeMethods.ExToolWindow), "Layered + click-through + no-activate + tool-window styles");
            Check(focusStayedOutsideOverlay && NativeMethods.GetForegroundWindow() != overlay.Handle, "Overlay does not receive foreground focus");
            Check(NativeMethods.GetWindowRect(overlay.Handle, out var rect), "Native window bounds available");
            Check(rect.Left == screen.Bounds.Left && rect.Top == screen.Bounds.Top && rect.Right - rect.Left == screen.Bounds.Width
                && rect.Bottom - rect.Top == AnimationMath.CoverageHeight(screen.Bounds.Height, 20), "Physical monitor bounds and 20% height");
            Check(overlay.RenderedFrames > 0, "WPF animation rendered frames");
            Check(NativeMethods.IsWindowVisible(overlay.Handle), "Overlay visible during playback");
            await Task.Delay(1900);
            Check(!overlay.IsPlaying && !NativeMethods.IsWindowVisible(overlay.Handle), "Overlay hidden after duration");
            int stoppedFrames = overlay.RenderedFrames;
            await Task.Delay(200);
            Check(overlay.RenderedFrames == stoppedFrames, "Rendering stopped between cycles");
            overlay.Play(settings with { CoveragePercent = 35, Palette = WavePalette.Smoke }, screen);
            await Task.Delay(150);
            NativeMethods.GetWindowRect(overlay.Handle, out rect);
            Check(rect.Bottom - rect.Top == AnimationMath.CoverageHeight(screen.Bounds.Height, 35), "Replay and changed coverage");
            overlay.Stop();
            overlay.Play(settings with { Palette = WavePalette.NorthernLights, NorthernLightsRenderer = NorthernLightsRenderer.Shader }, screen);
            await Task.Delay(250);
            Check(overlay.RenderedFrames > 0 && overlay.IsUsingShader == NorthernLightsEffect.IsSupported,
                "Shader selection uses GPU when supported and classic fallback otherwise");
            overlay.Stop();
            overlay.Play(settings with { Palette = WavePalette.NorthernLights }, screen);
            await Task.Delay(150);
            Check(!overlay.IsUsingShader && overlay.RenderedFrames > 0, "Switching back to classic northern lights works");
            overlay.Stop();
            Check(!overlay.IsPlaying, "Explicit stop");
            overlay.Play(settings with { CoveragePercent = 5, Palette = WavePalette.NorthernLights }, screen);
            await Task.Delay(150);
            NativeMethods.GetWindowRect(overlay.Handle, out rect);
            Check(overlay.RenderedFrames > 0 && rect.Bottom - rect.Top == AnimationMath.CoverageHeight(screen.Bounds.Height, 5),
                "Northern lights plays at minimum coverage");
            overlay.Stop();
            // Exercise the same registration path with F13/F14 while a user's older
            // copy may legitimately own the application's F10/F11 bindings.
            using (var hotkeys = new HotkeyService(() => { }, () => { }, 0x7C, 0x7D))
            {
                Check(hotkeys.Warning.Length == 0, "Global hotkeys registered and released");
            }
            using (var stream = Application.GetResourceStream(new Uri("pack://application:,,,/Assets/aurora.ico"))!.Stream)
            using (var icon = new System.Drawing.Icon(stream))
            {
                Check(icon.Width > 0, "Embedded tray icon loads");
            }
            var window = new SettingsWindow((App)Application.Current) { AllowClose = true };
            var slider = (Slider)window.FindName("CoverageSlider");
            slider.Value = 35;
            Check(((TextBlock)window.FindName("CoverageLabel")).Text == "35%", "Settings controls update their displayed values");
            slider.Value = 20;
            var paletteBox = (ComboBox)window.FindName("PaletteBox");
            paletteBox.SelectedIndex = 2;
            Check(((ComboBoxItem)paletteBox.SelectedItem).Content as string == "Северное сияние", "Northern lights can be selected in settings");
            var rendererBox = (ComboBox)window.FindName("RendererBox");
            rendererBox.SelectedIndex = 1;
            Check(((FrameworkElement)window.FindName("RendererPanel")).Visibility == Visibility.Visible,
                "Northern lights renderer selector is visible for northern lights");
            paletteBox.SelectedIndex = 0;
            Check(((FrameworkElement)window.FindName("RendererPanel")).Visibility == Visibility.Collapsed && rendererBox.SelectedIndex == 1,
                "Other effects hide the renderer selector and preserve its selection");
            paletteBox.SelectedIndex = 2;
            rendererBox.SelectedIndex = 0; // software screenshot cannot execute PS 3.0
            Check(((TextBlock)window.FindName("VersionLabel")).Text.EndsWith(typeof(App).Assembly.GetName().Version!.ToString(3), StringComparison.Ordinal),
                "Settings show the released version");
            var content = (FrameworkElement)window.Content;
            content.Measure(new Size(640, 820));
            content.Arrange(new Rect(0, 0, 640, 820));
            content.UpdateLayout();
            var bitmap = new RenderTargetBitmap(640, 820, 96, 96, PixelFormats.Pbgra32);
            var background = new DrawingVisual();
            using (var drawing = background.RenderOpen()) drawing.DrawRectangle(window.Background, null, new Rect(0, 0, 640, 820));
            bitmap.Render(background);
            bitmap.Render(content);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            var imagePath = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(reportPath))!, "settings-preview.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);
            using (var imageStream = File.Create(imagePath)) encoder.Save(imageStream);
            Check(content.ActualWidth > 0 && content.ActualHeight > 0, "Settings window lays out and renders");
            window.Close();
            WriteReport(true, null);
            return 0;
        }
        catch (Exception ex) { WriteReport(false, ex.ToString()); return 1; }
        finally { overlay?.Close(); }

        void Check(bool value, string label)
        {
            results.Add(new { check = label, passed = value });
            if (!value) throw new InvalidOperationException(label);
        }
        void WriteReport(bool passed, string? error)
        {
            var path = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { passed, checks = results, error }, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
