using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public sealed record LastSessionState(
    IReadOnlyList<string> OpenPhotoPaths,
    string? SelectedPhotoPath,
    double ZoomPercent,
    DateTime SavedAtUtc);

public static class SessionSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string SessionSettingsPath = Path.Combine(SettingsDirectory, "last-session.json");

    public static LastSessionState Load()
    {
        if (!File.Exists(SessionSettingsPath))
        {
            return Empty;
        }

        try
        {
            LastSessionState? state = JsonSerializer.Deserialize<LastSessionState>(File.ReadAllText(SessionSettingsPath));
            return state is null
                ? Empty
                : state with
                {
                    OpenPhotoPaths = state.OpenPhotoPaths
                        .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray()
                };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return Empty;
        }
    }

    public static void Save(LastSessionState state)
    {
        Directory.CreateDirectory(SettingsDirectory);
        LastSessionState sanitized = state with
        {
            OpenPhotoPaths = state.OpenPhotoPaths
                .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ZoomPercent = Math.Clamp(state.ZoomPercent, 25, 400)
        };
        File.WriteAllText(SessionSettingsPath, JsonSerializer.Serialize(sanitized, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static LastSessionState Empty { get; } = new(Array.Empty<string>(), null, 100, DateTime.MinValue);
}
