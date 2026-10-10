using System;
using System.IO;
using System.Linq;
using OledAurora.Core;

int count = 0;
Check("First cycle waits the full interval", () =>
{
    var schedule = new CycleSchedule(TimeSpan.Zero, TimeSpan.FromSeconds(180));
    Require(!schedule.Tick(TimeSpan.FromSeconds(179), false));
    Require(schedule.Tick(TimeSpan.FromSeconds(180), false));
    Require(!schedule.Tick(TimeSpan.FromSeconds(181), false));
});
Check("No burst after a delayed tick", () =>
{
    var schedule = new CycleSchedule(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    Require(schedule.Tick(TimeSpan.FromHours(1), false));
    Require(!schedule.Tick(TimeSpan.FromHours(1), false));
});
Check("Blocked session postpones instead of accumulating cycles", () =>
{
    var schedule = new CycleSchedule(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    Require(!schedule.Tick(TimeSpan.FromSeconds(90), true));
    Require(!schedule.Tick(TimeSpan.FromSeconds(91), false));
    Require(schedule.Tick(TimeSpan.FromSeconds(120), false));
});
Check("New interval takes effect immediately", () =>
{
    var schedule = new CycleSchedule(TimeSpan.Zero, TimeSpan.FromSeconds(180));
    schedule.Restart(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(60));
    Require(!schedule.Tick(TimeSpan.FromSeconds(69), false));
    Require(schedule.Tick(TimeSpan.FromSeconds(70), false));
});
Check("Invalid intervals are rejected", () =>
{
    try { _ = new CycleSchedule(TimeSpan.Zero, TimeSpan.Zero); }
    catch (ArgumentOutOfRangeException) { return; }
    throw new Exception("Zero interval accepted");
});
Check("Configuration clamps hostile and non-finite values", () =>
{
    var settings = new AppSettings { CoveragePercent = double.NaN, OpacityPercent = double.PositiveInfinity,
        DurationSeconds = -10, IntervalSeconds = int.MinValue, FramesPerSecond = 999, Palette = (WavePalette)100,
        NorthernLightsRenderer = (NorthernLightsRenderer)100 }.Normalize();
    Require(settings.CoveragePercent == 20 && settings.OpacityPercent == 25 && settings.DurationSeconds == 2);
    Require(settings.IntervalSeconds == 30 && settings.FramesPerSecond == 30 && settings.Palette == WavePalette.Aurora);
    Require(settings.NorthernLightsRenderer == NorthernLightsRenderer.Classic);
});
Check("Envelope has no flash at either boundary", () =>
{
    Require(AnimationMath.Envelope(0, 6) == 0 && AnimationMath.Envelope(6, 6) == 0);
    Require(AnimationMath.Envelope(3, 6) == 1);
    Require(AnimationMath.Envelope(.001, 6) < .00001 && AnimationMath.Envelope(5.999, 6) < .00001);
    Require(Enumerable.Range(0, 601).All(i => AnimationMath.Envelope(i / 100.0, 6) is >= 0 and <= 1));
});
Check("Coverage uses physical screen pixels", () =>
{
    Require(AnimationMath.CoverageHeight(2160, 20) == 432);
    Require(AnimationMath.CoverageHeight(1440, 35) == 504);
});
Check("Northern lights survives configuration normalization", () =>
{
    var settings = new AppSettings { Palette = WavePalette.NorthernLights }.Normalize();
    Require(settings.Palette == WavePalette.NorthernLights);
});
Check("Settings roundtrip and corrupt-file recovery preserve the original", () =>
{
    var directory = Path.Combine(Path.GetTempPath(), "OledAuroraChecks", Guid.NewGuid().ToString("N"));
    try
    {
        var store = new SettingsStore(directory);
        Require(store.Load() == new AppSettings());
        var settings = new AppSettings { Enabled = false, MonitorDeviceName = @"\\.\DISPLAY2", Palette = WavePalette.Smoke, OpacityPercent = 47 };
        store.Save(settings);
        Require(store.Load() == settings);
        var northernLights = settings with { Palette = WavePalette.NorthernLights, NorthernLightsRenderer = NorthernLightsRenderer.Shader };
        store.Save(northernLights);
        Require(store.Load() == northernLights);
        File.WriteAllText(store.FilePath, "{\"Palette\":\"NorthernLights\",\"CoveragePercent\":35,\"OpacityPercent\":47}");
        Require(store.Load().Palette == WavePalette.NorthernLights && store.Load().NorthernLightsRenderer == NorthernLightsRenderer.Classic);
        File.WriteAllText(store.FilePath, "{\"Palette\":\"Aurora\",\"CoveragePercent\":35,\"OpacityPercent\":47}");
        Require(store.Load().Palette == WavePalette.Aurora && store.Load().CoveragePercent == 35 && store.Load().OpacityPercent == 47);
        File.WriteAllText(store.FilePath, "{\"Palette\":\"Smoke\",\"IntervalSeconds\":300}");
        Require(store.Load().Palette == WavePalette.Smoke && store.Load().IntervalSeconds == 300);
        Require(!File.Exists(store.FilePath + ".tmp"));
        File.WriteAllText(store.FilePath, "{ corrupt");
        Require(store.Load() == new AppSettings() && store.LoadWarning != null);
        Require(Directory.GetFiles(directory, "*.backup-*").Length == 1);
        Require(File.ReadAllText(store.FilePath) == "{ corrupt");
    }
    finally
    {
        var allowedRoot = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "OledAuroraChecks")) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(directory);
        if (resolved.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved)) Directory.Delete(resolved, true);
    }
});
Console.WriteLine($"PASS: {count} checks");
return 0;

void Check(string label, Action action)
{
    action();
    Console.WriteLine("PASS: " + label);
    count++;
}
void Require(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
