using System;
using Microsoft.Win32;

namespace OledAurora;

internal static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    internal static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Не найден путь приложения.");
            key.SetValue("OledAurora", $"\"{path}\" --background");
        }
        else key.DeleteValue("OledAurora", false);
    }
}
