using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OledAurora.Core;

public sealed class SettingsStore(string directory)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    public string FilePath => Path.Combine(directory, "settings.json");
    public string? LoadWarning { get; private set; }

    public AppSettings Load()
    {
        LoadWarning = null;
        if (!File.Exists(FilePath)) return new AppSettings();
        try
        {
            return (JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath), Options)
                ?? throw new JsonException("Settings are null.")).Normalize();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = "Не удалось прочитать настройки. Загружены значения по умолчанию.";
            try
            {
                File.Copy(FilePath, FilePath + ".backup-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff"));
            }
            catch (Exception backupError) when (backupError is IOException or UnauthorizedAccessException) { }
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(directory);
        var temp = FilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(settings.Normalize(), Options));
        File.Move(temp, FilePath, true);
    }
}
