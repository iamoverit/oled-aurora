using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using OledAurora.Core;
using Forms = System.Windows.Forms;

namespace OledAurora;

public partial class App : Application
{
    internal static string DataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OledAurora");
    private readonly SettingsStore _store = new(DataDirectory);
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private CycleSchedule _schedule = new(TimeSpan.Zero, TimeSpan.FromSeconds(180));
    private DispatcherTimer? _timer;
    private OverlayWindow? _overlay;
    private SettingsWindow? _window;
    private HotkeyService? _hotkeys;
    private Forms.NotifyIcon? _tray;
    private Icon? _icon;
    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private bool _ownsMutex, _locked, _suspended, _manualPreview, _shuttingDown, _eventsRegistered;
    internal AppSettings Settings { get; private set; } = new();
    internal string Status { get; private set; } = "Запуск…";
    internal string Warning { get; private set; } = "";
    internal event Action? StatusChanged;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Contains("--smoke-test"))
        {
            int index = Array.IndexOf(e.Args, "--smoke-test");
            var report = index + 1 < e.Args.Length ? e.Args[index + 1] : Path.Combine(Path.GetTempPath(), "oled-aurora-smoke.json");
            Dispatcher.InvokeAsync(async () => Shutdown(await SmokeTest.RunAsync(report)));
            return;
        }
        DispatcherUnhandledException += (_, args) =>
        {
            Log(args.Exception);
            args.Handled = true;
            MessageBox.Show("OLED Aurora остановлена из-за ошибки. Подробности: " + Path.Combine(DataDirectory, "app.log"), "OLED Aurora", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        };
        _mutex = new Mutex(true, @"Local\OledAurora.Application", out _ownsMutex);
        if (!_ownsMutex)
        {
            try { using var signal = EventWaitHandle.OpenExisting(@"Local\OledAurora.ShowSettings"); signal.Set(); }
            catch (WaitHandleCannotBeOpenedException) { }
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\OledAurora.ShowSettings");
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) =>
        {
            if (!_shuttingDown) Dispatcher.BeginInvoke(ShowSettings);
        }, null, Timeout.Infinite, false);

        bool firstRun = !File.Exists(_store.FilePath);
        Settings = _store.Load();
        Warning = _store.LoadWarning ?? "";
        _overlay = new OverlayWindow();
        _hotkeys = new HotkeyService(() => PlayPreview(Settings), ToggleEnabled);
        if (_hotkeys.Warning.Length > 0) Warning += " " + _hotkeys.Warning;
        CreateTray();
        _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
        SystemEvents.SessionSwitch += SessionChanged;
        SystemEvents.PowerModeChanged += PowerChanged;
        SystemEvents.DisplaySettingsChanged += DisplaysChanged;
        _eventsRegistered = true;
        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();
        Tick();
        if (!e.Args.Contains("--background") || firstRun) ShowSettings();
    }

    private void CreateTray()
    {
        using var resource = GetResourceStream(new Uri("pack://application:,,,/Assets/aurora.ico"))!.Stream;
        using var embedded = new Icon(resource);
        _icon = (Icon)embedded.Clone();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Показать анимацию  ·  Ctrl+Alt+F10", null, (_, _) => Dispatcher.Invoke(() => PlayPreview(Settings)));
        var pause = menu.Items.Add("Пауза", null, (_, _) => Dispatcher.Invoke(ToggleEnabled));
        menu.Items.Add("Настройки…", null, (_, _) => Dispatcher.Invoke(ShowSettings));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Выход", null, (_, _) => Dispatcher.Invoke(() => Shutdown()));
        menu.Opening += (_, _) => pause.Text = Settings.Enabled ? "Пауза  ·  Ctrl+Alt+F11" : "Продолжить  ·  Ctrl+Alt+F11";
        _tray = new Forms.NotifyIcon { Icon = _icon, Text = "OLED Aurora", ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => Dispatcher.Invoke(ShowSettings);
    }

    internal void ShowSettings()
    {
        if (_shuttingDown) return;
        _window ??= new SettingsWindow(this);
        _window.RefreshMonitors();
        _window.Show();
        if (_window.WindowState == WindowState.Minimized) _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    internal bool ApplySettings(AppSettings settings)
    {
        settings = settings.Normalize();
        try
        {
            if (settings.StartWithWindows != Settings.StartWithWindows || settings.StartWithWindows)
                StartupService.SetEnabled(settings.StartWithWindows);
            _store.Save(settings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            Log(ex);
            try { StartupService.SetEnabled(Settings.StartWithWindows); } catch (Exception rollback) { Log(rollback); }
            MessageBox.Show("Не удалось сохранить настройки: " + ex.Message, "OLED Aurora", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        _overlay?.Stop();
        _manualPreview = false;
        Settings = settings;
        _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
        Tick();
        return true;
    }

    private void ToggleEnabled()
    {
        if (ApplySettings(Settings with { Enabled = !Settings.Enabled })) _window?.SetEnabledIndicator(Settings.Enabled);
    }

    internal void PlayPreview(AppSettings settings)
    {
        if (_locked || _suspended) return;
        _manualPreview = true;
        Play(settings);
        _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
        Tick();
    }

    private void Play(AppSettings settings)
    {
        try { _overlay?.Play(settings, MonitorService.Resolve(settings.MonitorDeviceName)); }
        catch (Exception ex)
        {
            Log(ex);
            _overlay?.Stop();
            Warning = "Не удалось показать анимацию. Попробуйте выбрать другой монитор. " + ex.Message;
        }
    }

    private void Tick()
    {
        if (_shuttingDown) return;
        bool fullscreen = Settings.PauseOnFullscreen && MonitorService.IsFullscreen(MonitorService.Resolve(Settings.MonitorDeviceName));
        bool blocked = !Settings.Enabled || _locked || _suspended || fullscreen;
        if (_locked || _suspended || ((!Settings.Enabled || fullscreen) && !_manualPreview)) _overlay?.Stop();
        if (_overlay?.IsPlaying != true) _manualPreview = false;
        if (_schedule.Tick(_clock.Elapsed, blocked)) Play(Settings);
        Status = _overlay?.IsPlaying == true ? "Анимация воспроизводится · ввод остаётся в вашем приложении"
            : !Settings.Enabled ? "Расписание на паузе"
            : _locked || _suspended ? "Пауза · сеанс неактивен"
            : fullscreen ? "Пауза · полноэкранное приложение"
            : $@"Следующая анимация через {_schedule.Remaining(_clock.Elapsed):m\:ss}";
        if (_tray is not null) _tray.Text = Settings.Enabled ? "OLED Aurora · работает" : "OLED Aurora · пауза";
        StatusChanged?.Invoke();
    }

    private void SessionChanged(object sender, SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (e.Reason is SessionSwitchReason.SessionLock or SessionSwitchReason.RemoteDisconnect or SessionSwitchReason.ConsoleDisconnect) _locked = true;
        if (e.Reason is SessionSwitchReason.SessionUnlock or SessionSwitchReason.RemoteConnect or SessionSwitchReason.ConsoleConnect) _locked = false;
        _overlay?.Stop();
        _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
        Tick();
    });

    private void PowerChanged(object sender, PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (e.Mode == PowerModes.Suspend) _suspended = true;
        if (e.Mode == PowerModes.Resume) _suspended = false;
        if (e.Mode is PowerModes.Suspend or PowerModes.Resume)
        {
            _overlay?.Stop();
            _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
            Tick();
        }
    });

    private void DisplaysChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        _overlay?.Stop();
        _window?.RefreshMonitors();
        _schedule.Restart(_clock.Elapsed, TimeSpan.FromSeconds(Settings.IntervalSeconds));
        Tick();
    });

    internal static void Log(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(DataDirectory);
            var path = Path.Combine(DataDirectory, "app.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1_000_000) File.Move(path, path + ".old", true);
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {ex}\n");
        }
        catch (Exception logError) when (logError is IOException or UnauthorizedAccessException) { }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _shuttingDown = true;
        _timer?.Stop();
        if (_eventsRegistered)
        {
            SystemEvents.SessionSwitch -= SessionChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
            SystemEvents.DisplaySettingsChanged -= DisplaysChanged;
        }
        _overlay?.Stop();
        _hotkeys?.Dispose();
        if (_tray is not null) { _tray.Visible = false; _tray.ContextMenuStrip?.Dispose(); _tray.Dispose(); }
        _icon?.Dispose();
        if (_window is not null) _window.AllowClose = true;
        _showWait?.Unregister(null);
        _showEvent?.Dispose();
        if (_ownsMutex) _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}
