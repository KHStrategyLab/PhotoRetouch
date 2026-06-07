using System.Windows;

namespace PhotoRetouch;

public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
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
        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = "\uD45C\uC2DC \uD504\uB85C\uD30C\uC77C \uBD88\uB7EC\uC624\uAE30",
            Filter = "ICC profile files|*.icc;*.icm|All files|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog(this) == true)
        {
            ColorManagementSettings.ManualDisplayProfilePath = dialog.FileName;
            ManualProfilePathTextBox.Text = dialog.FileName;
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
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
