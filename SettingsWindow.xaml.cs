using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PhotoRetouch;

public partial class SettingsWindow : Window
{
    private System.Windows.Controls.Button? _capturingShortcutButton;
    private ShortcutKeyTarget? _capturingShortcutTarget;
    private System.Windows.Controls.Button? _capturingModifierButton;
    private ModifierKeyTarget? _capturingModifierTarget;
    private MouseModifierGesture? _pendingModifierCapture;

    public SettingsWindow()
    {
        InitializeComponent();
        InitializeColorManagementControls();
        InitializePreviewControls();
        InitializePerformanceControls();
        InitializeWorkingFolderControls();
        InitializeShortcutControls();
    }

    private void InitializeColorManagementControls()
    {
        ManualProfilePathTextBox.Text = ColorManagementSettings.ManualDisplayProfilePath ?? "sRGB IEC61966-2.1";

        switch (ColorManagementSettings.Mode)
        {
            case ColorManagementMode.Manual:
                ManualColorManagementRadioButton.IsChecked = true;
                break;
            case ColorManagementMode.Disabled:
                DisabledColorManagementRadioButton.IsChecked = true;
                break;
            default:
                AutomaticColorManagementRadioButton.IsChecked = true;
                break;
        }

        UpdateManualProfileControls();
    }

    private void InitializeShortcutControls()
    {
        UpdateShortcutButtonText();
    }

    private void InitializePreviewControls()
    {
        PreviewMaxLongSideTextBox.Text = PreviewSettings.MaxLongSidePixels.ToString();
        if (PreviewSettings.UseOriginalSize)
        {
            PreviewOriginalSizeRadioButton.IsChecked = true;
        }
        else
        {
            PreviewCustomSizeRadioButton.IsChecked = true;
        }

        UpdatePreviewSizeControls();
    }

    private void InitializeWorkingFolderControls()
    {
        WorkingFolderPathTextBox.Text = WorkingFolderSettings.WorkingFolderPath;
    }

    private void InitializePerformanceControls()
    {
        if (PerformanceSettings.PreviewEngine == PreviewEngineMode.Gpu)
        {
            GpuPreviewEngineRadioButton.IsChecked = true;
        }
        else
        {
            CpuPreviewEngineRadioButton.IsChecked = true;
        }
    }

    private void ColorManagementMode_Changed(object sender, RoutedEventArgs e)
    {
        if (AutomaticColorManagementRadioButton is null ||
            ManualColorManagementRadioButton is null ||
            DisabledColorManagementRadioButton is null)
        {
            return;
        }

        if (ManualColorManagementRadioButton.IsChecked == true)
        {
            ColorManagementSettings.Mode = ColorManagementMode.Manual;
        }
        else if (DisabledColorManagementRadioButton.IsChecked == true)
        {
            ColorManagementSettings.Mode = ColorManagementMode.Disabled;
        }
        else
        {
            ColorManagementSettings.Mode = ColorManagementMode.Automatic;
        }

        UpdateManualProfileControls();
    }

    private void LoadProfileButton_Click(object sender, RoutedEventArgs e)
    {
        const string defaultProfileDirectory = @"C:\Windows\System32\spool\drivers\color";
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = "\uD45C\uC2DC \uD504\uB85C\uD30C\uC77C \uBD88\uB7EC\uC624\uAE30",
            Filter = "ICC profile files|*.icc;*.icm|All files|*.*",
            InitialDirectory = Directory.Exists(defaultProfileDirectory) ? defaultProfileDirectory : string.Empty,
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ColorManagementSettings.ManualDisplayProfilePath = dialog.FileName;
            ManualProfilePathTextBox.Text = dialog.FileName;
        }
    }

    private void ShortcutButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender == RenameShortcutButton)
        {
            BeginShortcutCapture(RenameShortcutButton, ShortcutKeyTarget.RenamePhoto);
        }
        else if (sender == CompareShortcutButton)
        {
            BeginShortcutCapture(CompareShortcutButton, ShortcutKeyTarget.CompareOriginal);
        }
        else if (sender == ToggleSectionOrderEditShortcutButton)
        {
            BeginShortcutCapture(ToggleSectionOrderEditShortcutButton, ShortcutKeyTarget.ToggleSectionOrderEdit);
        }
        else if (sender == UndoShortcutButton)
        {
            BeginShortcutCapture(UndoShortcutButton, ShortcutKeyTarget.Undo);
        }
        else if (sender == RedoShortcutButton)
        {
            BeginShortcutCapture(RedoShortcutButton, ShortcutKeyTarget.Redo);
        }
    }

    private void ModifierButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender == AddSelectionModifierButton)
        {
            BeginModifierCapture(AddSelectionModifierButton, ModifierKeyTarget.AddSelection);
        }
        else if (sender == RangeSelectionModifierButton)
        {
            BeginModifierCapture(RangeSelectionModifierButton, ModifierKeyTarget.RangeSelection);
        }
        else if (sender == WholeSplitModifierButton)
        {
            BeginModifierCapture(WholeSplitModifierButton, ModifierKeyTarget.WholeSplitPreview);
        }
    }

    private void BeginShortcutCapture(System.Windows.Controls.Button button, ShortcutKeyTarget target)
    {
        CancelModifierCapture();
        _capturingShortcutButton = button;
        _capturingShortcutTarget = target;
        button.Content = "키 입력...";
        button.Focus();
    }

    private void BeginModifierCapture(System.Windows.Controls.Button button, ModifierKeyTarget target)
    {
        CancelShortcutCapture();
        _capturingModifierButton = button;
        _capturingModifierTarget = target;
        _pendingModifierCapture = null;
        button.Content = "보조키 입력...";
        button.Focus();
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_capturingShortcutButton is not null || _capturingModifierButton is not null)
            {
                CancelShortcutCapture();
                CancelModifierCapture();
                UpdateShortcutButtonText();
                e.Handled = true;
                return;
            }

            Close();
            e.Handled = true;
            return;
        }

        if (_capturingModifierButton is not null && _capturingModifierTarget is not null)
        {
            Key modifierKey = ShortcutSettings.NormalizeKey(e.Key, e.SystemKey);
            MouseModifierGesture? gesture = GetModifierCaptureValue(modifierKey);
            if (gesture is not null)
            {
                _pendingModifierCapture = gesture;
                _capturingModifierButton.Content = ShortcutSettings.FormatMouseGesture(gesture);
            }

            e.Handled = true;
            return;
        }

        if (_capturingShortcutButton is null || _capturingShortcutTarget is null)
        {
            return;
        }

        Key key = ShortcutSettings.NormalizeKey(e.Key, e.SystemKey);
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
        {
            return;
        }

        if (_capturingShortcutTarget == ShortcutKeyTarget.RenamePhoto)
        {
            ShortcutSettings.RenamePhotoShortcut = new ShortcutKey(Keyboard.Modifiers, key);
        }
        else if (_capturingShortcutTarget == ShortcutKeyTarget.CompareOriginal)
        {
            ShortcutSettings.CompareOriginalShortcut = new ShortcutKey(Keyboard.Modifiers, key);
        }
        else if (_capturingShortcutTarget == ShortcutKeyTarget.ToggleSectionOrderEdit)
        {
            ShortcutSettings.ToggleSectionOrderEditShortcut = new ShortcutKey(Keyboard.Modifiers, key);
        }
        else if (_capturingShortcutTarget == ShortcutKeyTarget.Undo)
        {
            ShortcutSettings.UndoShortcut = new ShortcutKey(Keyboard.Modifiers, key);
        }
        else
        {
            ShortcutSettings.RedoShortcut = new ShortcutKey(Keyboard.Modifiers, key);
        }

        ShortcutSettings.Save();
        CancelShortcutCapture();
        UpdateShortcutButtonText();
        e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_capturingModifierButton is null ||
            _capturingModifierTarget is null ||
            _pendingModifierCapture is null)
        {
            return;
        }

        if (_capturingModifierTarget == ModifierKeyTarget.AddSelection)
        {
            ShortcutSettings.AddSelectionGesture = _pendingModifierCapture;
        }
        else if (_capturingModifierTarget == ModifierKeyTarget.RangeSelection)
        {
            ShortcutSettings.RangeSelectionGesture = _pendingModifierCapture;
        }
        else
        {
            ShortcutSettings.WholeSplitPreviewGesture = _pendingModifierCapture;
        }

        ShortcutSettings.Save();
        CancelModifierCapture();
        UpdateShortcutButtonText();
        e.Handled = true;
    }

    private void ResetShortcutsButton_Click(object sender, RoutedEventArgs e)
    {
        ShortcutSettings.ResetToDefaults();
        InitializeShortcutControls();
    }

    private void CancelShortcutCapture()
    {
        _capturingShortcutButton = null;
        _capturingShortcutTarget = null;
    }

    private void CancelModifierCapture()
    {
        _capturingModifierButton = null;
        _capturingModifierTarget = null;
        _pendingModifierCapture = null;
    }

    private static MouseModifierGesture? GetModifierCaptureValue(Key key)
    {
        ModifierKeys modifiers = Keyboard.Modifiers;
        modifiers |= key switch
        {
            Key.LeftCtrl or Key.RightCtrl => ModifierKeys.Control,
            Key.LeftShift or Key.RightShift => ModifierKeys.Shift,
            Key.LeftAlt or Key.RightAlt => ModifierKeys.Alt,
            _ => ModifierKeys.None
        };

        bool usesSpace = key == Key.Space || Keyboard.IsKeyDown(Key.Space);
        ModifierKeys cleanModifiers = modifiers & (ModifierKeys.Control | ModifierKeys.Shift | ModifierKeys.Alt);
        return cleanModifiers == ModifierKeys.None && !usesSpace
            ? null
            : new MouseModifierGesture(cleanModifiers, usesSpace);
    }

    private void PreviewSizeMode_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreviewSizeControls();
    }

    private void PreviewMaxLongSideTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        e.Handled = e.Text.Any(character => !char.IsDigit(character));
    }

    private void ApplyPreviewSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        int maxLongSide = PreviewSettings.DefaultMaxLongSidePixels;
        if (int.TryParse(PreviewMaxLongSideTextBox.Text, out int parsedValue))
        {
            maxLongSide = Math.Clamp(parsedValue, PreviewSettings.MinimumMaxLongSidePixels, PreviewSettings.MaximumMaxLongSidePixels);
        }

        PreviewSettings.UseOriginalSize = PreviewOriginalSizeRadioButton.IsChecked == true;
        PreviewSettings.MaxLongSidePixels = maxLongSide;
        PreviewMaxLongSideTextBox.Text = maxLongSide.ToString();
        PreviewSettings.Save();
        UpdatePreviewSizeControls();
    }

    private void BrowseWorkingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        using System.Windows.Forms.FolderBrowserDialog dialog = new()
        {
            Description = "작업 폴더 선택",
            SelectedPath = Directory.Exists(WorkingFolderPathTextBox.Text)
                ? WorkingFolderPathTextBox.Text
                : WorkingFolderSettings.DefaultWorkingFolderPath,
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            WorkingFolderPathTextBox.Text = dialog.SelectedPath;
        }
    }

    private void ApplyWorkingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        string folderPath = WorkingFolderPathTextBox.Text.Trim();
        if (!Directory.Exists(folderPath))
        {
            System.Windows.MessageBox.Show(this, "선택한 폴더를 찾을 수 없습니다.", "작업 폴더", MessageBoxButton.OK, MessageBoxImage.Warning);
            WorkingFolderPathTextBox.Text = WorkingFolderSettings.WorkingFolderPath;
            return;
        }

        WorkingFolderSettings.WorkingFolderPath = folderPath;
        WorkingFolderSettings.Save();
    }

    private void PreviewEngineMode_Changed(object sender, RoutedEventArgs e)
    {
        if (CpuPreviewEngineRadioButton is null || GpuPreviewEngineRadioButton is null)
        {
            return;
        }

        PerformanceSettings.PreviewEngine = GpuPreviewEngineRadioButton.IsChecked == true
            ? PreviewEngineMode.Gpu
            : PreviewEngineMode.Cpu;
        PerformanceSettings.Save();
    }

    private void UpdateManualProfileControls()
    {
        if (ManualProfilePathTextBox is null || LoadProfileButton is null)
        {
            return;
        }

        bool isManual = ManualColorManagementRadioButton.IsChecked == true;
        ManualProfilePathTextBox.IsEnabled = isManual;
        LoadProfileButton.IsEnabled = isManual;
    }

    private void UpdateShortcutButtonText()
    {
        RenameShortcutButton.Content = ShortcutSettings.RenamePhotoShortcut.DisplayText;
        CompareShortcutButton.Content = ShortcutSettings.CompareOriginalShortcut.DisplayText;
        ToggleSectionOrderEditShortcutButton.Content = ShortcutSettings.ToggleSectionOrderEditShortcut.DisplayText;
        UndoShortcutButton.Content = ShortcutSettings.UndoShortcut.DisplayText;
        RedoShortcutButton.Content = ShortcutSettings.RedoShortcut.DisplayText;
        AddSelectionModifierButton.Content = ShortcutSettings.FormatMouseGesture(ShortcutSettings.AddSelectionGesture);
        RangeSelectionModifierButton.Content = ShortcutSettings.FormatMouseGesture(ShortcutSettings.RangeSelectionGesture);
        WholeSplitModifierButton.Content = ShortcutSettings.FormatMouseGesture(ShortcutSettings.WholeSplitPreviewGesture);
    }

    private void UpdatePreviewSizeControls()
    {
        if (PreviewMaxLongSideTextBox is null)
        {
            return;
        }

        PreviewMaxLongSideTextBox.IsEnabled = PreviewCustomSizeRadioButton.IsChecked == true;
    }
}

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
