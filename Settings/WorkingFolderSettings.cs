using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public static class WorkingFolderSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string WorkingFolderSettingsPath = Path.Combine(SettingsDirectory, "working-folder.json");

    static WorkingFolderSettings()
    {
        WorkingFolderPath = DefaultWorkingFolderPath;
        Load();
    }

    public static string DefaultWorkingFolderPath
    {
        get
        {
            string picturesPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return Directory.Exists(picturesPath)
                ? picturesPath
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }

    public static string WorkingFolderPath { get; set; }

    public static void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        WorkingFolderSettingsData data = new()
        {
            WorkingFolderPath = WorkingFolderPath
        };

        File.WriteAllText(WorkingFolderSettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Load()
    {
        if (!File.Exists(WorkingFolderSettingsPath))
        {
            return;
        }

        try
        {
            WorkingFolderSettingsData? data = JsonSerializer.Deserialize<WorkingFolderSettingsData>(File.ReadAllText(WorkingFolderSettingsPath));
            if (data is not null && Directory.Exists(data.WorkingFolderPath))
            {
                WorkingFolderPath = data.WorkingFolderPath;
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private sealed class WorkingFolderSettingsData
    {
        public string WorkingFolderPath { get; set; } = DefaultWorkingFolderPath;
    }
}
