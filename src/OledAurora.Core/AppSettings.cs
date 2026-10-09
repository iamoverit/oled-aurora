using System;

namespace OledAurora.Core;

// Keep existing values/names so saved 0.1 settings retain their selected effect.
public enum WavePalette { Aurora = 0, Smoke = 1, NorthernLights = 2 }

public sealed record AppSettings
{
    public bool Enabled { get; init; } = true;
    public double CoveragePercent { get; init; } = 20;
    public double OpacityPercent { get; init; } = 25;
    public double DurationSeconds { get; init; } = 6;
    public int IntervalSeconds { get; init; } = 180;
    public int FramesPerSecond { get; init; } = 30;
    public string MonitorDeviceName { get; init; } = "";
    public WavePalette Palette { get; init; } = WavePalette.Aurora;
    public bool PauseOnFullscreen { get; init; } = true;
    public bool StartWithWindows { get; init; }

    public AppSettings Normalize() => this with
    {
        CoveragePercent = ClampFinite(CoveragePercent, 5, 40, 20),
        OpacityPercent = ClampFinite(OpacityPercent, 5, 80, 25),
        DurationSeconds = ClampFinite(DurationSeconds, 2, 20, 6),
        IntervalSeconds = Math.Clamp(IntervalSeconds, 30, 600),
        FramesPerSecond = FramesPerSecond == 60 ? 60 : 30,
        MonitorDeviceName = MonitorDeviceName ?? "",
        Palette = Enum.IsDefined(Palette) ? Palette : WavePalette.Aurora
    };

    private static double ClampFinite(double value, double min, double max, double fallback)
        => double.IsFinite(value) ? Math.Clamp(value, min, max) : fallback;
}
