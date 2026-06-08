using System.IO;
using System.Text.Json;
using System.Windows.Input;

namespace PhotoRetouch;

public enum ShortcutKeyTarget
{
    RenamePhoto,
    CompareOriginal,
    ToggleSectionOrderEdit,
    Undo,
    Redo
}

public enum ModifierKeyTarget
{
    AddSelection,
    RangeSelection,
    WholeSplitPreview
}

public sealed record ShortcutKey(ModifierKeys Modifiers, Key Key)
{
    public string DisplayText
    {
        get
        {
            string keyText = Key.ToString();
            if (Modifiers == ModifierKeys.None)
            {
                return keyText;
            }

            return $"{ShortcutSettings.FormatModifiers(Modifiers)}+{keyText}";
        }
    }
}

public sealed record MouseModifierGesture(ModifierKeys Modifiers, bool UsesSpace);

public static class ShortcutSettings
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string ShortcutSettingsPath = Path.Combine(SettingsDirectory, "shortcuts.json");

    static ShortcutSettings()
    {
        Load();
    }

    public static ShortcutKey RenamePhotoShortcut { get; set; } = new(ModifierKeys.None, Key.F2);
    public static ShortcutKey CompareOriginalShortcut { get; set; } = new(ModifierKeys.None, Key.Space);
    public static ShortcutKey ToggleSectionOrderEditShortcut { get; set; } = new(ModifierKeys.Control, Key.M);
    public static ShortcutKey UndoShortcut { get; set; } = new(ModifierKeys.Control, Key.Z);
    public static ShortcutKey RedoShortcut { get; set; } = new(ModifierKeys.Control | ModifierKeys.Shift, Key.Z);
    public static MouseModifierGesture AddSelectionGesture { get; set; } = new(ModifierKeys.Control, false);
    public static MouseModifierGesture RangeSelectionGesture { get; set; } = new(ModifierKeys.Shift, false);
    public static MouseModifierGesture WholeSplitPreviewGesture { get; set; } = new(ModifierKeys.Control | ModifierKeys.Shift, false);

    public static bool Matches(System.Windows.Input.KeyEventArgs e, ShortcutKey shortcut)
    {
        Key key = NormalizeKey(e.Key, e.SystemKey);
        return key == shortcut.Key && Keyboard.Modifiers == shortcut.Modifiers;
    }

    public static bool HasModifiers(ModifierKeys activeModifiers, ModifierKeys requiredModifiers)
    {
        return requiredModifiers != ModifierKeys.None &&
               (activeModifiers & requiredModifiers) == requiredModifiers;
    }

    public static bool MatchesMouseGesture(ModifierKeys activeModifiers, bool isSpacePressed, MouseModifierGesture gesture)
    {
        bool modifiersMatch = gesture.Modifiers == ModifierKeys.None ||
                              (activeModifiers & gesture.Modifiers) == gesture.Modifiers;
        bool spaceMatches = !gesture.UsesSpace || isSpacePressed;
        return (gesture.Modifiers != ModifierKeys.None || gesture.UsesSpace) &&
               modifiersMatch &&
               spaceMatches;
    }

    public static string FormatModifiers(ModifierKeys modifiers)
    {
        List<string> parts = new();
        if ((modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            parts.Add("Ctrl");
        }

        if ((modifiers & ModifierKeys.Alt) == ModifierKeys.Alt)
        {
            parts.Add("Alt");
        }

        if ((modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
        {
            parts.Add("Shift");
        }

        return string.Join("+", parts);
    }

    public static string FormatMouseGesture(MouseModifierGesture gesture)
    {
        List<string> parts = new();
        string modifiers = FormatModifiers(gesture.Modifiers);
        if (!string.IsNullOrWhiteSpace(modifiers))
        {
            parts.AddRange(modifiers.Split('+'));
        }

        if (gesture.UsesSpace)
        {
            parts.Add("Space");
        }

        return parts.Count == 0 ? "-" : string.Join("+", parts);
    }

    public static Key NormalizeKey(Key key, Key systemKey)
    {
        return key == Key.System ? systemKey : key;
    }

    public static void ResetToDefaults()
    {
        RenamePhotoShortcut = new ShortcutKey(ModifierKeys.None, Key.F2);
        CompareOriginalShortcut = new ShortcutKey(ModifierKeys.None, Key.Space);
        ToggleSectionOrderEditShortcut = new ShortcutKey(ModifierKeys.Control, Key.M);
        UndoShortcut = new ShortcutKey(ModifierKeys.Control, Key.Z);
        RedoShortcut = new ShortcutKey(ModifierKeys.Control | ModifierKeys.Shift, Key.Z);
        AddSelectionGesture = new MouseModifierGesture(ModifierKeys.Control, false);
        RangeSelectionGesture = new MouseModifierGesture(ModifierKeys.Shift, false);
        WholeSplitPreviewGesture = new MouseModifierGesture(ModifierKeys.Control | ModifierKeys.Shift, false);
        Save();
    }

    public static void Save()
    {
        Directory.CreateDirectory(SettingsDirectory);
        ShortcutSettingsData data = new()
        {
            RenamePhotoModifiers = RenamePhotoShortcut.Modifiers,
            RenamePhotoKey = RenamePhotoShortcut.Key,
            CompareOriginalModifiers = CompareOriginalShortcut.Modifiers,
            CompareOriginalKey = CompareOriginalShortcut.Key,
            ToggleSectionOrderEditModifiers = ToggleSectionOrderEditShortcut.Modifiers,
            ToggleSectionOrderEditKey = ToggleSectionOrderEditShortcut.Key,
            UndoModifiers = UndoShortcut.Modifiers,
            UndoKey = UndoShortcut.Key,
            RedoModifiers = RedoShortcut.Modifiers,
            RedoKey = RedoShortcut.Key,
            AddSelectionModifiers = AddSelectionGesture.Modifiers,
            AddSelectionUsesSpace = AddSelectionGesture.UsesSpace,
            RangeSelectionModifiers = RangeSelectionGesture.Modifiers,
            RangeSelectionUsesSpace = RangeSelectionGesture.UsesSpace,
            WholeSplitPreviewModifiers = WholeSplitPreviewGesture.Modifiers,
            WholeSplitPreviewUsesSpace = WholeSplitPreviewGesture.UsesSpace
        };

        File.WriteAllText(ShortcutSettingsPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Load()
    {
        if (!File.Exists(ShortcutSettingsPath))
        {
            return;
        }

        try
        {
            ShortcutSettingsData? data = JsonSerializer.Deserialize<ShortcutSettingsData>(File.ReadAllText(ShortcutSettingsPath));
            if (data is null)
            {
                return;
            }

            RenamePhotoShortcut = new ShortcutKey(data.RenamePhotoModifiers, data.RenamePhotoKey);
            CompareOriginalShortcut = new ShortcutKey(data.CompareOriginalModifiers, data.CompareOriginalKey);
            ToggleSectionOrderEditShortcut = new ShortcutKey(data.ToggleSectionOrderEditModifiers, data.ToggleSectionOrderEditKey);
            UndoShortcut = new ShortcutKey(data.UndoModifiers, data.UndoKey);
            RedoShortcut = new ShortcutKey(data.RedoModifiers, data.RedoKey);
            AddSelectionGesture = new MouseModifierGesture(data.AddSelectionModifiers, data.AddSelectionUsesSpace);
            RangeSelectionGesture = new MouseModifierGesture(data.RangeSelectionModifiers, data.RangeSelectionUsesSpace);
            WholeSplitPreviewGesture = new MouseModifierGesture(data.WholeSplitPreviewModifiers, data.WholeSplitPreviewUsesSpace);
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
    }

    private sealed class ShortcutSettingsData
    {
        public ModifierKeys RenamePhotoModifiers { get; set; }
        public Key RenamePhotoKey { get; set; } = Key.F2;
        public ModifierKeys CompareOriginalModifiers { get; set; }
        public Key CompareOriginalKey { get; set; } = Key.Space;
        public ModifierKeys ToggleSectionOrderEditModifiers { get; set; } = ModifierKeys.Control;
        public Key ToggleSectionOrderEditKey { get; set; } = Key.M;
        public ModifierKeys UndoModifiers { get; set; } = ModifierKeys.Control;
        public Key UndoKey { get; set; } = Key.Z;
        public ModifierKeys RedoModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
        public Key RedoKey { get; set; } = Key.Z;
        public ModifierKeys AddSelectionModifiers { get; set; } = ModifierKeys.Control;
        public bool AddSelectionUsesSpace { get; set; }
        public ModifierKeys RangeSelectionModifiers { get; set; } = ModifierKeys.Shift;
        public bool RangeSelectionUsesSpace { get; set; }
        public ModifierKeys WholeSplitPreviewModifiers { get; set; } = ModifierKeys.Control | ModifierKeys.Shift;
        public bool WholeSplitPreviewUsesSpace { get; set; }
    }
}
