using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public enum PreviewEngineMode
{
    Cpu,
    Gpu
}

public static class PerformanceSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string PerformanceSettingsPath = Path.Combine(SettingsDirectory, "performance-settings.json");

    static PerformanceSettings()
    {
        Load();
    }

    public static PreviewEngineMode PreviewEngine { get; set; } = PreviewEngineMode.Cpu;

    public static void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        PerformanceSettingsData data = new()
        {
            PreviewEngine = PreviewEngine
        };

        File.WriteAllText(PerformanceSettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Load()
    {
        if (!File.Exists(PerformanceSettingsPath))
        {
            return;
        }

        try
        {
            PerformanceSettingsData? data = JsonSerializer.Deserialize<PerformanceSettingsData>(File.ReadAllText(PerformanceSettingsPath));
            if (data is not null)
            {
                PreviewEngine = data.PreviewEngine;
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private sealed class PerformanceSettingsData
    {
        public PreviewEngineMode PreviewEngine { get; set; } = PreviewEngineMode.Cpu;
    }
}
