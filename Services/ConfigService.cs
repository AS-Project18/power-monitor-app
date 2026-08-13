using System.IO;
using System.Text.Json;
using PowerMonitorApp.Models;

namespace PowerMonitorApp.Services;

/// <summary>
/// Baca/tulis konfigurasi aplikasi (tarif, interval polling) dari/ke config.json.
/// </summary>
public static class ConfigService
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static AppConfig LoadConfig(string path)
    {
        if (!File.Exists(path))
        {
            var defaultConfig = new AppConfig { TariffPerKwh = 1444.70, PollingIntervalSeconds = 2 };
            SaveConfig(path, defaultConfig);
            return defaultConfig;
        }

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppConfig>(json)
            ?? new AppConfig { TariffPerKwh = 1444.70, PollingIntervalSeconds = 2 };
    }

    public static void SaveConfig(string path, AppConfig config)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(config, WriteOptions));
    }
}
