using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public static class PreviewSettings
{
    public const int MinimumMaxLongSidePixels = 800;
    public const int MaximumMaxLongSidePixels = 4000;
    public const int DefaultMaxLongSidePixels = 1200;

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string PreviewSettingsPath = Path.Combine(SettingsDirectory, "preview-settings.json");

    static PreviewSettings()
    {
        Load();
    }

    public static bool UseOriginalSize { get; set; } = true;
    public static int MaxLongSidePixels { get; set; } = DefaultMaxLongSidePixels;

    public static void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        PreviewSettingsData data = new()
        {
            UseOriginalSize = UseOriginalSize,
            MaxLongSidePixels = Math.Clamp(MaxLongSidePixels, MinimumMaxLongSidePixels, MaximumMaxLongSidePixels)
        };

        File.WriteAllText(PreviewSettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Load()
    {
        if (!File.Exists(PreviewSettingsPath))
        {
            return;
        }

        try
        {
            PreviewSettingsData? data = JsonSerializer.Deserialize<PreviewSettingsData>(File.ReadAllText(PreviewSettingsPath));
            if (data is null)
            {
                return;
            }

            UseOriginalSize = data.UseOriginalSize;
            MaxLongSidePixels = Math.Clamp(data.MaxLongSidePixels, MinimumMaxLongSidePixels, MaximumMaxLongSidePixels);
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private sealed class PreviewSettingsData
    {
        public bool UseOriginalSize { get; set; } = true;
        public int MaxLongSidePixels { get; set; } = DefaultMaxLongSidePixels;
    }
}
