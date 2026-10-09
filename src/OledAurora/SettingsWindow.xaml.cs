using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using OledAurora.Core;

namespace OledAurora;

public partial class SettingsWindow : Window
{
    private readonly App _app;
    private bool _loading = true;
    internal bool AllowClose { get; set; }

    public SettingsWindow(App app)
    {
        _app = app;
        InitializeComponent();
        var version = typeof(App).Assembly.GetName().Version!;
        VersionLabel.Text = $"FO48U · {version.Major}.{version.Minor}.{version.Build}";
        Height = Math.Min(860, SystemParameters.WorkArea.Height - 48);
        LoadSettings(app.Settings);
        app.StatusChanged += RefreshStatus;
        Closing += OnClosing;
        Closed += (_, _) => app.StatusChanged -= RefreshStatus;
        RefreshStatus();
    }

    internal void RefreshMonitors()
    {
        var selected = MonitorBox.SelectedValue as string ?? _app.Settings.MonitorDeviceName;
        var choices = MonitorService.Choices();
        MonitorBox.ItemsSource = choices;
        MonitorBox.SelectedValue = choices.Any(x => x.DeviceName == selected) ? selected : "";
    }

    private void LoadSettings(AppSettings settings)
    {
        _loading = true;
        MonitorBox.ItemsSource = MonitorService.Choices();
        MonitorBox.SelectedValue = settings.MonitorDeviceName;
        if (MonitorBox.SelectedIndex < 0) MonitorBox.SelectedIndex = 0;
        CoverageSlider.Value = settings.CoveragePercent;
        OpacitySlider.Value = settings.OpacityPercent;
        DurationSlider.Value = settings.DurationSeconds;
        IntervalSlider.Value = settings.IntervalSeconds;
        PaletteBox.SelectedIndex = (int)settings.Palette;
        FpsBox.SelectedIndex = settings.FramesPerSecond == 60 ? 1 : 0;
        EnabledBox.IsChecked = settings.Enabled;
        FullscreenBox.IsChecked = settings.PauseOnFullscreen;
        StartupBox.IsChecked = settings.StartWithWindows;
        _loading = false;
        UpdatePreview();
    }

    private AppSettings ReadSettings() => new AppSettings
    {
        Enabled = EnabledBox.IsChecked == true,
        MonitorDeviceName = MonitorBox.SelectedValue as string ?? "",
        CoveragePercent = CoverageSlider.Value,
        OpacityPercent = OpacitySlider.Value,
        DurationSeconds = DurationSlider.Value,
        IntervalSeconds = (int)IntervalSlider.Value,
        FramesPerSecond = FpsBox.SelectedIndex == 1 ? 60 : 30,
        Palette = SelectedPalette,
        PauseOnFullscreen = FullscreenBox.IsChecked == true,
        StartWithWindows = StartupBox.IsChecked == true
    }.Normalize();

    private void UpdatePreview()
    {
        if (_loading) return;
        CoverageLabel.Text = $"{CoverageSlider.Value:0}%";
        OpacityLabel.Text = $"{OpacitySlider.Value:0}%";
        DurationLabel.Text = $"{DurationSlider.Value:0} секунд";
        IntervalLabel.Text = $"{IntervalSlider.Value / 60:0.##} мин · {IntervalSlider.Value:0} с";
        MiniPreview.SetFrame(2.4, OpacitySlider.Value / 100, SelectedPalette);
    }

    private WavePalette SelectedPalette => PaletteBox.SelectedIndex >= 0
        && Enum.IsDefined((WavePalette)PaletteBox.SelectedIndex) ? (WavePalette)PaletteBox.SelectedIndex : WavePalette.Aurora;

    private void ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) => UpdatePreview();
    private void PaletteChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();
    private void PreviewClick(object sender, RoutedEventArgs e) => _app.PlayPreview(ReadSettings());

    private void SaveClick(object sender, RoutedEventArgs e)
    {
        if (_app.ApplySettings(ReadSettings())) Hide();
    }

    internal void SetEnabledIndicator(bool enabled) => EnabledBox.IsChecked = enabled;

    private void RefreshStatus()
    {
        StatusLabel.Text = _app.Status;
        WarningLabel.Text = _app.Warning;
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (!AllowClose) { e.Cancel = true; Hide(); }
    }
}
