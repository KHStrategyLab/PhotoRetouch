using System.IO;
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
