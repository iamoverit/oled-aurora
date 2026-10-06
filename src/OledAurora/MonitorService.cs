using System;
using System.Linq;
using System.Text;
using Forms = System.Windows.Forms;

namespace OledAurora;

internal sealed record MonitorChoice(string DeviceName, string Label);

internal static class MonitorService
{
    internal static MonitorChoice[] Choices() => new[] { new MonitorChoice("", "Основной монитор (автоматически)") }
        .Concat(Forms.Screen.AllScreens.Select((s, i) => new MonitorChoice(s.DeviceName,
            $"Экран {i + 1} · {s.Bounds.Width} × {s.Bounds.Height}{(s.Primary ? " · основной" : "")} · {s.DeviceName}"))).ToArray();

    internal static Forms.Screen Resolve(string deviceName) => Forms.Screen.AllScreens.FirstOrDefault(s => s.DeviceName == deviceName)
        ?? Forms.Screen.PrimaryScreen ?? Forms.Screen.AllScreens[0];

    internal static bool IsFullscreen(Forms.Screen screen)
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;
        NativeMethods.GetWindowThreadProcessId(hwnd, out var process);
        if (process == Environment.ProcessId) return false;
        var name = new StringBuilder(256);
        NativeMethods.GetClassName(hwnd, name, name.Capacity);
        if (name.ToString() is "Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd") return false;
        // A maximized IDE is still work: require a borderless foreground window.
        if ((NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlStyle).ToInt64() & 0x00C00000) != 0) return false;
        if (!NativeMethods.GetWindowRect(hwnd, out var rect)) return false;
        var bounds = screen.Bounds;
        return rect.Left <= bounds.Left && rect.Top <= bounds.Top && rect.Right >= bounds.Right && rect.Bottom >= bounds.Bottom;
    }
}
