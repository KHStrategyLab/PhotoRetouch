using System.IO;
using System.Text;
using System.Text.Json;

namespace PhotoRetouch;

public sealed record RetouchPreset(
    string PresetName,
    string PresetId,
    int PresetVersion,
    int Stage,
    SkinSmoothToolset SkinSmoothToolset,
    BlemishToolset BlemishToolset,
    WrinkleToolset WrinkleToolset,
    ToneEvenToolset ToneEvenToolset,
    TextureRestoreToolset TextureRestoreToolset,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    string? Notes = null)
{
    public const int CurrentVersion = 1;

    public RetouchToolset ToToolset()
    {
        return new RetouchToolset(
            Math.Clamp(Stage, 1, 10),
            SkinSmoothToolset,
            BlemishToolset,
            WrinkleToolset,
            ToneEvenToolset,
            TextureRestoreToolset,
            MaskDebugOptions.Default,
            new RetouchUserOverrideFlags(true, true, true, true, true, false));
    }

    public static RetouchPreset FromToolset(string presetName, RetouchToolset toolset, string? notes = null)
    {
        DateTime now = DateTime.UtcNow;
        return new RetouchPreset(
            presetName,
            Guid.NewGuid().ToString("N"),
            CurrentVersion,
            Math.Clamp(toolset.CurrentStage, 1, 10),
            toolset.SkinSmooth,
            toolset.Blemish,
            toolset.Wrinkle,
            toolset.ToneEven,
            toolset.TextureRestore,
            now,
            now,
            notes);
    }
}

public sealed class RetouchPresetService
{
    public static RetouchPresetService Default { get; } = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoRetouch",
            "presets"));

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _rootDirectory;

    public RetouchPresetService(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public string UserPresetDirectory => Path.Combine(_rootDirectory, "user");

    public string DefaultPresetDirectory => Path.Combine(_rootDirectory, "default");

    public RetouchPreset SaveUserPreset(string presetName, RetouchToolset toolset, string? notes = null)
    {
        RetouchPreset preset = RetouchPreset.FromToolset(presetName, toolset, notes);
        Directory.CreateDirectory(UserPresetDirectory);
        string path = Path.Combine(UserPresetDirectory, SanitizeFileName(presetName) + ".retouchpreset.json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions), Encoding.UTF8);
        return preset;
    }

    public RetouchPreset? TryLoad(string path)
    {
        try
        {
            RetouchPreset? preset = JsonSerializer.Deserialize<RetouchPreset>(File.ReadAllText(path, Encoding.UTF8));
            return preset is null
                ? null
                : NormalizePreset(preset);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public IReadOnlyList<RetouchPreset> LoadAll()
    {
        EnsureDefaultPresets();
        List<RetouchPreset> presets = new();
        foreach (string path in EnumeratePresetFiles(DefaultPresetDirectory).Concat(EnumeratePresetFiles(UserPresetDirectory)))
        {
            RetouchPreset? preset = TryLoad(path);
            if (preset is not null)
            {
                presets.Add(preset);
            }
        }

        return presets.OrderBy(preset => preset.PresetName, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    public void EnsureDefaultPresets()
    {
        Directory.CreateDirectory(DefaultPresetDirectory);
        EnsureDefaultPreset("Natural", 2);
        EnsureDefaultPreset("Studio", 5);
        EnsureDefaultPreset("Beauty", 7);
        EnsureDefaultPreset("Strong", 9);
    }

    private void EnsureDefaultPreset(string name, int stage)
    {
        string path = Path.Combine(DefaultPresetDirectory, SanitizeFileName(name) + ".retouchpreset.json");
        if (File.Exists(path))
        {
            return;
        }

        StagePreset stagePreset = StagePresetMapper.Map(stage);
        RetouchPreset preset = RetouchPreset.FromToolset(name, RetouchToolset.FromStagePreset(stagePreset));
        File.WriteAllText(path, JsonSerializer.Serialize(preset, JsonOptions), Encoding.UTF8);
    }

    private static RetouchPreset NormalizePreset(RetouchPreset preset)
    {
        int stage = Math.Clamp(preset.Stage, 1, 10);
        StagePreset fallback = StagePresetMapper.Map(stage);
        RetouchToolset fallbackToolset = RetouchToolset.FromStagePreset(fallback);
        return preset with
        {
            PresetVersion = RetouchPreset.CurrentVersion,
            Stage = stage,
            PresetName = string.IsNullOrWhiteSpace(preset.PresetName) ? "Untitled" : preset.PresetName,
            PresetId = string.IsNullOrWhiteSpace(preset.PresetId) ? Guid.NewGuid().ToString("N") : preset.PresetId,
            SkinSmoothToolset = preset.SkinSmoothToolset ?? fallbackToolset.SkinSmooth,
            BlemishToolset = preset.BlemishToolset ?? fallbackToolset.Blemish,
            WrinkleToolset = preset.WrinkleToolset ?? fallbackToolset.Wrinkle,
            ToneEvenToolset = preset.ToneEvenToolset ?? fallbackToolset.ToneEven,
            TextureRestoreToolset = preset.TextureRestoreToolset ?? fallbackToolset.TextureRestore
        };
    }

    private static IEnumerable<string> EnumeratePresetFiles(string directory)
    {
        return Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.retouchpreset.json")
            : Array.Empty<string>();
    }

    private static string SanitizeFileName(string name)
    {
        string sanitized = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "preset" : sanitized;
    }
}
