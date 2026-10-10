using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using OledAurora.Core;
using Forms = System.Windows.Forms;

namespace OledAurora;

internal sealed class OverlayWindow : Window
{
    private readonly AuroraVisual _visual = new();
    private readonly Stopwatch _watch = new();
    private AppSettings _settings = new();
    private double _lastFrame = -1;
    private HwndSource? _source;
    internal bool IsPlaying { get; private set; }
    internal int RenderedFrames { get; private set; }
    internal IntPtr Handle { get; private set; }
    internal bool IsUsingShader => _visual.IsUsingShader;

    internal OverlayWindow()
    {
        Title = "OLED Aurora overlay";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        ResizeMode = ResizeMode.NoResize;
        Focusable = false;
        IsHitTestVisible = false;
        Width = Height = 1;
        Content = _visual;
        SourceInitialized += (_, _) => InitializeHandle();
        Closed += (_, _) => { Stop(); _source?.RemoveHook(WindowProc); };
    }

    private void InitializeHandle()
    {
        Handle = new WindowInteropHelper(this).Handle;
        var style = NativeMethods.GetWindowLongPtr(Handle, NativeMethods.GwlExStyle).ToInt64();
        NativeMethods.SetWindowLongPtr(Handle, NativeMethods.GwlExStyle,
            new IntPtr(style | NativeMethods.ExTransparent | NativeMethods.ExToolWindow | NativeMethods.ExNoActivate));
        _source = HwndSource.FromHwnd(Handle);
        _source?.AddHook(WindowProc);
    }

    internal void Play(AppSettings settings, Forms.Screen screen)
    {
        Stop();
        _settings = settings.Normalize();
        _visual.SetFrame(0, 0, _settings.Palette, _settings.NorthernLightsRenderer);
        Show();
        var bounds = screen.Bounds;
        // Native coordinates are physical pixels, including negative monitor origins.
        if (!NativeMethods.SetWindowPos(Handle, NativeMethods.Topmost, bounds.X, bounds.Y,
            bounds.Width, AnimationMath.CoverageHeight(bounds.Height, _settings.CoveragePercent),
            NativeMethods.SwpNoActivate | NativeMethods.SwpShowWindow))
        {
            Hide();
            throw new Win32Exception();
        }
        _lastFrame = -1;
        RenderedFrames = 0;
        IsPlaying = true;
        _watch.Restart();
        CompositionTarget.Rendering += RenderFrame;
    }

    private void RenderFrame(object? sender, EventArgs e)
    {
        double seconds = _watch.Elapsed.TotalSeconds;
        if (seconds >= _settings.DurationSeconds) { Stop(); return; }
        if (seconds - _lastFrame < 1.0 / _settings.FramesPerSecond) return;
        _lastFrame = seconds;
        _visual.SetFrame(seconds, _settings.OpacityPercent / 100 * AnimationMath.Envelope(seconds, _settings.DurationSeconds),
            _settings.Palette, _settings.NorthernLightsRenderer);
        RenderedFrames++;
    }

    internal void Stop()
    {
        CompositionTarget.Rendering -= RenderFrame;
        IsPlaying = false;
        _watch.Stop();
        _visual.SetFrame(0, 0, _settings.Palette, _settings.NorthernLightsRenderer);
        if (IsVisible) Hide();
    }

    private IntPtr WindowProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0084) { handled = true; return new IntPtr(-1); } // HTTRANSPARENT
        if (message == 0x0021) { handled = true; return new IntPtr(3); } // MA_NOACTIVATE
        return IntPtr.Zero;
    }
}
