using System;
using System.Collections.Generic;
using System.Windows.Interop;

namespace OledAurora;

internal sealed class HotkeyService : IDisposable
{
    private readonly HwndSource _source;
    private readonly Action _play;
    private readonly Action _pause;
    private readonly List<int> _registered = [];
    internal string Warning { get; private set; } = "";

    internal HotkeyService(Action play, Action pause)
    {
        _play = play; _pause = pause;
        _source = new HwndSource(new HwndSourceParameters("OLED Aurora hotkeys") { ParentWindow = new IntPtr(-3) });
        _source.AddHook(Hook);
        Register(1, 0x79, "Ctrl + Alt + F10");
        Register(2, 0x7A, "Ctrl + Alt + F11");
    }

    private void Register(int id, uint key, string label)
    {
        if (NativeMethods.RegisterHotKey(_source.Handle, id, 0x4003, key)) _registered.Add(id);
        else Warning += $"{label} занята другим приложением. ";
    }

    private IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == 0x0312)
        {
            handled = true;
            if (wParam.ToInt32() == 1) _play();
            else if (wParam.ToInt32() == 2) _pause();
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        foreach (var id in _registered) NativeMethods.UnregisterHotKey(_source.Handle, id);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }
}
