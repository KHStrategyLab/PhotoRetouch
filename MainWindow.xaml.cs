using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

namespace PhotoRetouch;

public partial class MainWindow : Window
{
    public ObservableCollection<PersonOption> People { get; } = CreatePeople();

    public ObservableCollection<RetouchSection> RetouchSections { get; } = new()
    {
        new RetouchSection(
            "skin",
            "\uD53C\uBD80",
            true,
            new RetouchControl[]
            {
                new("skin_smooth", "\uD53C\uBD80 \uB9E4\uB044\uB7EC\uC6C0", 0, 100, 20),
                new("blemish_reduce", "\uC7A1\uD2F0 \uC644\uD654", 0, 100, 0),
                new("pore_clean", "\uBAA8\uACF5 \uC815\uB9AC", 0, 100, 0),
                new("tone_even", "\uD53C\uBD80\uD1A4 \uADE0\uC77C\uD654", 0, 100, 0),
                new("dark_circle", "\uB2E4\uD06C\uC11C\uD074 \uC644\uD654", 0, 100, 0)
            }),
        new RetouchSection(
            "face_shape",
            "\uC5BC\uAD74\uD615",
            false,
            new RetouchControl[]
            {
                new("face_width", "\uC5BC\uAD74 \uD3ED", -100, 100, 0),
                new("face_length", "\uC5BC\uAD74 \uAE38\uC774", -100, 100, 0),
                new("cheekbone", "\uAD11\uB300", -100, 100, 0),
                new("jaw_width", "\uD131 \uD3ED", -100, 100, 0),
                new("double_chin", "\uC774\uC911\uD131 \uC644\uD654", 0, 100, 0)
            }),
        new RetouchSection(
            "eyes",
            "\uB208",
            true,
            new RetouchControl[]
            {
                new("eye_balance", "\uC88C\uC6B0 \uADE0\uD615", -100, 100, 0),
                new("pupil_size", "\uB208\uB3D9\uC790 \uD06C\uAE30", -100, 100, 0),
                new("eye_height", "\uB208\uB192\uC774", -100, 100, 0),
                new("eye_width", "\uB208\uB113\uC774", -100, 100, 0),
                new("eye_brightness", "\uB208 \uBC1D\uAE30", 0, 100, 0),
                new("white_eye", "\uD770\uC790 \uBC1D\uAE30", 0, 100, 0)
            }),
        new RetouchSection(
            "nose",
            "\uCF54",
            true,
            new RetouchControl[]
            {
                new("nostril_balance", "\uCF67\uAD6C\uBA4D \uD06C\uAE30 \uB9DE\uCD94\uAE30", -100, 100, 0),
                new("nose_wing_size", "\uCF67\uBCFC \uD06C\uAE30 \uC870\uC808", -100, 100, 0),
                new("nose_width", "\uCF54 \uD3ED", -100, 100, 0),
                new("nose_height", "\uCF54 \uB192\uC774", -100, 100, 0),
                new("nose_tip_size", "\uCF54\uB05D \uD06C\uAE30", -100, 100, 0)
            }),
        new RetouchSection(
            "mouth",
            "\uC785 / \uC785\uC220",
            false,
            new RetouchControl[]
            {
                new("mouth_size", "\uC785 \uD06C\uAE30", -100, 100, 0),
                new("mouth_corner", "\uC785\uAF2C\uB9AC", -100, 100, 0),
                new("upper_lip", "\uC717\uC785\uC220", -100, 100, 0),
                new("lower_lip", "\uC544\uB7AB\uC785\uC220", -100, 100, 0),
                new("lip_color", "\uC785\uC220 \uC0C9", 0, 100, 0)
            }),
        new RetouchSection(
            "jawline",
            "\uD131 / \uD131\uC120",
            false,
            new RetouchControl[]
            {
                new("jawline_define", "\uD131\uC120 \uC120\uBA85\uB3C4", 0, 100, 0),
                new("chin_length", "\uD131\uB05D \uAE38\uC774", -100, 100, 0),
                new("chin_width", "\uD131\uB05D \uD3ED", -100, 100, 0),
                new("jaw_balance", "\uD131 \uC88C\uC6B0 \uADE0\uD615", -100, 100, 0),
                new("neck_jaw_edge", "\uBAA9\uACFC \uD131 \uACBD\uACC4", 0, 100, 0)
            }),
        new RetouchSection(
            "hair",
            "\uD5E4\uC5B4",
            false,
            new RetouchControl[]
            {
                new("hair_volume_top", "\uBCFC\uB968\uC5C5 - \uC704", 0, 100, 0),
                new("hair_volume_side_top", "\uBCFC\uB968\uC5C5 - \uC704\uC606", 0, 100, 0),
                new("flyaway_face", "\uC794\uBA38\uB9AC \uC81C\uAC70 - \uC5BC\uAD74\uCABD", 0, 100, 0),
                new("flyaway_background", "\uC794\uBA38\uB9AC \uC81C\uAC70 - \uBC30\uACBD\uCABD", 0, 100, 0),
                new("hair_gloss", "\uC724\uAE30\uB098\uB294 \uBA38\uB9AC", 0, 100, 0),
                RetouchControl.CreateColor("\uC5FC\uC0C9 \uC0C9\uC0C1 \uC120\uD0DD", "#6B4A3A"),
                new("hair_color_amount", "\uC5FC\uC0C9 \uAC15\uB3C4", 0, 100, 0),
                new("gray_hair_cover", "\uC0C8\uCE58 \uCEE4\uBC84", 0, 100, 0)
            })
    };

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        Focus();
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.IsRepeat)
        {
            return;
        }

        ShowOriginalPreview();
        e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Space)
        {
            return;
        }

        ShowEditedPreview();
        e.Handled = true;
    }

    private void CompareButton_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        ShowOriginalPreview();
    }

    private void CompareButton_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        ShowEditedPreview();
    }

    private void CompareButton_MouseLeave(object sender, MouseEventArgs e)
    {
        ShowEditedPreview();
    }

    private void ShowOriginalPreview()
    {
        OriginalMockPreview.Visibility = Visibility.Visible;
        EditedMockPreview.Visibility = Visibility.Collapsed;
        PreviewStateText.Text = "\uC6D0\uBCF8";
    }

    private void ShowEditedPreview()
    {
        OriginalMockPreview.Visibility = Visibility.Collapsed;
        EditedMockPreview.Visibility = Visibility.Visible;
        PreviewStateText.Text = "\uBCF4\uC815\uBCF8";
    }

    private static ObservableCollection<PersonOption> CreatePeople()
    {
        ObservableCollection<PersonOption> people = new()
        {
            new PersonOption("all", "\uBAA8\uB4E0 \uC778\uBB3C", "ALL")
        };

        for (int i = 1; i <= 10; i++)
        {
            people.Add(new PersonOption($"person_{i:00}", $"\uC778\uBB3C {i:00}", i.ToString("00")));
        }

        return people;
    }
}

public sealed class PersonOption
{
    public PersonOption(string id, string name, string shortName)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
    }

    public string Id { get; }
    public string Name { get; }
    public string ShortName { get; }
}

public sealed class RetouchSection
{
    public RetouchSection(string id, string title, bool isExpanded, IReadOnlyList<RetouchControl> controls)
    {
        Id = id;
        Title = title;
        IsExpanded = isExpanded;
        Controls = controls;
    }

    public string Id { get; }
    public string Title { get; }
    public bool IsExpanded { get; set; }
    public IReadOnlyList<RetouchControl> Controls { get; }
}

public sealed class RetouchControl : INotifyPropertyChanged
{
    private double _value;

    public RetouchControl(string id, string label, double minimum, double maximum, double value)
    {
        Id = id;
        Label = label;
        Minimum = minimum;
        Maximum = maximum;
        _value = value;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Label { get; }
    public double Minimum { get; }
    public double Maximum { get; }
    public bool IsColorPicker { get; }
    public string? ColorValue { get; }
    public double Value
    {
        get => _value;
        set
        {
            if (Math.Abs(_value - value) < 0.001)
            {
                return;
            }

            _value = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayValue));
        }
    }

    public string DisplayValue => Value.ToString("0");

    public static RetouchControl CreateColor(string label, string colorValue)
    {
        return new RetouchControl("hair_color", label, 0, 0, 0, true, colorValue);
    }

    private RetouchControl(string id, string label, double minimum, double maximum, double value, bool isColorPicker, string? colorValue)
        : this(id, label, minimum, maximum, value)
    {
        IsColorPicker = isColorPicker;
        ColorValue = colorValue;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
