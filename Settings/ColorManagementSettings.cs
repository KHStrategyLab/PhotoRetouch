namespace PhotoRetouch;

public enum ColorManagementMode
{
    Automatic,
    Manual,
    Disabled
}

public static class ColorManagementSettings
{
    public static ColorManagementMode Mode { get; set; } = ColorManagementMode.Automatic;
    public static string? ManualDisplayProfilePath { get; set; }
}
