using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public static class PreviewBackgroundSettings
{
    public const string DefaultBackgroundColor = "#101112";

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string PreviewBackgroundSettingsPath = Path.Combine(SettingsDirectory, "preview-background.json");

    static PreviewBackgroundSettings()
    {
        Load();
    }

    public static string BackgroundColor { get; set; } = DefaultBackgroundColor;

    public static void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        PreviewBackgroundSettingsData data = new()
        {
            BackgroundColor = BackgroundColor
        };

        File.WriteAllText(PreviewBackgroundSettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Load()
    {
        if (!File.Exists(PreviewBackgroundSettingsPath))
        {
            return;
        }

        try
        {
            PreviewBackgroundSettingsData? data = JsonSerializer.Deserialize<PreviewBackgroundSettingsData>(File.ReadAllText(PreviewBackgroundSettingsPath));
            if (!string.IsNullOrWhiteSpace(data?.BackgroundColor))
            {
                BackgroundColor = data.BackgroundColor;
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private sealed class PreviewBackgroundSettingsData
    {
        public string BackgroundColor { get; set; } = DefaultBackgroundColor;
    }
}
