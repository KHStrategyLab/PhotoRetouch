using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using Forms = System.Windows.Forms;

namespace PhotoRetouch;

public partial class MainWindow : Window
{
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string SectionOrderPath = Path.Combine(SettingsDirectory, "section-order.json");

    private RetouchSection? _sectionDragSource;
    private RetouchSection? _sectionDropTarget;
    private bool _dropAfterTarget;
    private System.Windows.Point _sectionDragStart;

    public ObservableCollection<PersonOption> People { get; } = CreatePeople();

    public ObservableCollection<RetouchSection> RetouchSections { get; } = new()
    {
        new RetouchSection(
            "skin",
            "\uD53C\uBD80",
            false,
            new RetouchControl[]
            {
                new("blemish_remove", "\uC7A1\uD2F0 \uC81C\uAC70", 0, 100, 0),
                new("skin_smooth", "\uD53C\uBD80 \uB9E4\uB044\uB7EC\uC6C0", 0, 100, 20),
                new("pore_clean", "\uBAA8\uACF5 \uC815\uB9AC", 0, 100, 0),
                new("tone_even", "\uD53C\uBD80\uD1A4 \uADE0\uC77C\uD654", 0, 100, 0)
            }),
        new RetouchSection(
            "face_shape",
            "\uC5BC\uAD74\uD615",
            false,
            new RetouchControl[]
            {
                new("oval_face", "\uACC4\uB780\uD615 \uBCF4\uC815", 0, 100, 0),
                new("face_balance", "\uC88C\uC6B0 \uADE0\uD615", 0, 100, 0),
                new("cheekbone_soften", "\uAD11\uB300 \uC644\uD654", 0, 100, 0)
            }),
        new RetouchSection(
            "background",
            "\uBC30\uACBD",
            false,
            new RetouchControl[]
            {
                RetouchControl.CreateAction("background_image", "\uBC30\uACBD \uBD88\uB7EC\uC624\uAE30", "\uD30C\uC77C \uBD88\uB7EC\uC624\uAE30"),
                RetouchControl.CreateBackgroundLibrary(
                    "background_library",
                    "\uC800\uC7A5\uB41C \uBC30\uACBD",
                    new BackgroundOption[]
                    {
                        new("\uC2A4\uD29C\uB514\uC624 \uD68C\uC0C9", "#5A6268"),
                        new("\uBD80\uB4DC\uB7EC\uC6B4 \uD68C\uC0C9", "#727A80"),
                        new("\uC9C4\uD55C \uCC28\uCF5C", "#343A40")
                    }),
                new("background_image_opacity", "\uBC30\uACBD \uD22C\uBA85\uB3C4", 0, 100, 100),
                RetouchControl.CreateColor("background_color", "\uB2E8\uC77C \uC0C9\uC0C1 \uC120\uD0DD", "#4A5157"),
                new("background_color_amount", "\uB2E8\uC77C \uC0C9\uC0C1 \uC801\uC6A9 \uB18D\uB3C4", 0, 100, 100),
                new("background_blend", "\uC778\uBB3C \uACBD\uACC4 \uC790\uC5F0\uC2A4\uB7EC\uC6C0", 0, 100, 20)
            }),
        new RetouchSection(
            "eyes",
            "\uB208",
            false,
            new RetouchControl[]
            {
                new("eye_balance", "\uC88C\uC6B0 \uADE0\uD615", -100, 100, 0),
                new("pupil_size", "\uB208\uB3D9\uC790 \uD06C\uAE30", -100, 100, 0),
                new("eye_height", "\uB208\uB192\uC774", -100, 100, 0),
                new("eye_width", "\uB208\uB113\uC774", -100, 100, 0),
                new("eye_brightness", "\uB208 \uBC1D\uAE30", 0, 100, 0),
                new("red_eye_reduce", "\uCDA9\uD608\uB41C \uB208 \uC81C\uAC70", 0, 100, 0)
            }),
        new RetouchSection(
            "nose",
            "\uCF54",
            false,
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
            "\uC785",
            false,
            new RetouchControl[]
            {
                new("mouth_width", "\uC785 \uB113\uC774", -100, 100, 0),
                new("upper_lip", "\uC717\uC785\uC220", -100, 100, 0),
                new("lower_lip", "\uC544\uB7AB\uC785\uC220", -100, 100, 0)
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
                new("double_chin", "\uC774\uC911\uD131 \uC644\uD654", 0, 100, 0),
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
                RetouchControl.CreateColor("hair_color", "\uC5FC\uC0C9 \uC0C9\uC0C1 \uC120\uD0DD", "#4D555B"),
                new("hair_color_amount", "\uC5FC\uC0C9 \uAC15\uB3C4", 0, 100, 0),
                new("gray_hair_cover", "\uC0C8\uCE58 \uCEE4\uBC84", 0, 100, 0)
            }),
        new RetouchSection(
            "clothing",
            "\uC758\uC0C1",
            false,
            new RetouchControl[]
            {
                new("clothing_fine_wrinkle", "\uC0C1\uC758 \uC794\uC8FC\uB984 \uC81C\uAC70", 0, 100, 0),
                new("clothing_deep_wrinkle", "\uC0C1\uC758 \uD070 \uC8FC\uB984 \uC81C\uAC70", 0, 100, 0)
            })
    };

    public MainWindow()
    {
        RestoreSectionOrder();
        InitializeComponent();
        DataContext = this;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MoveToRightmostScreen();
        Focus();
    }

    private void MoveToRightmostScreen()
    {
        Forms.Screen? targetScreen = Forms.Screen.AllScreens
            .OrderByDescending(screen => screen.WorkingArea.Right)
            .ThenByDescending(screen => screen.WorkingArea.Width)
            .FirstOrDefault();

        if (targetScreen is null)
        {
            return;
        }

        Rect workingArea = ToWpfRect(targetScreen.WorkingArea);
        double windowWidth = ActualWidth > 0 ? ActualWidth : Width;
        double windowHeight = ActualHeight > 0 ? ActualHeight : Height;

        Left = workingArea.Left + Math.Max(0, (workingArea.Width - windowWidth) / 2);
        Top = workingArea.Top + Math.Max(0, (workingArea.Height - windowHeight) / 2);
        WindowState = WindowState.Maximized;
    }

    private Rect ToWpfRect(System.Drawing.Rectangle rectangle)
    {
        PresentationSource? source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget is null)
        {
            return new Rect(rectangle.Left, rectangle.Top, rectangle.Width, rectangle.Height);
        }

        System.Windows.Point topLeft = source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(rectangle.Left, rectangle.Top));
        System.Windows.Point bottomRight = source.CompositionTarget.TransformFromDevice.Transform(new System.Windows.Point(rectangle.Right, rectangle.Bottom));
        return new Rect(topLeft, bottomRight);
    }

    private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Space || e.IsRepeat)
        {
            return;
        }

        ShowOriginalPreview();
        e.Handled = true;
    }

    private void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
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

    private void CompareButton_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        ShowEditedPreview();
    }

    private void CollapseAllSectionsButton_Click(object sender, RoutedEventArgs e)
    {
        foreach (RetouchSection section in RetouchSections)
        {
            section.IsExpanded = false;
        }
    }

    private void SectionDragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _sectionDragSource = (sender as FrameworkElement)?.DataContext as RetouchSection;
        _sectionDragStart = e.GetPosition(this);

        if (sender is IInputElement dragHandle)
        {
            Mouse.Capture(dragHandle);
        }

        e.Handled = true;
    }

    private void SectionDragHandle_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (Mouse.Captured == sender)
        {
            Mouse.Capture(null);
        }

        _sectionDragSource = null;
        e.Handled = true;
    }

    private void SectionDragHandle_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_sectionDragSource is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        System.Windows.Point currentPosition = e.GetPosition(this);
        bool movedFarEnough =
            Math.Abs(currentPosition.X - _sectionDragStart.X) > SystemParameters.MinimumHorizontalDragDistance ||
            Math.Abs(currentPosition.Y - _sectionDragStart.Y) > SystemParameters.MinimumVerticalDragDistance;

        if (!movedFarEnough || sender is not DependencyObject dragSource)
        {
            return;
        }

        System.Windows.DataObject dragData = new();
        dragData.SetData(typeof(RetouchSection), _sectionDragSource);
        try
        {
            DragDrop.DoDragDrop(dragSource, dragData, System.Windows.DragDropEffects.Move);
        }
        finally
        {
            if (Mouse.Captured == sender)
            {
                Mouse.Capture(null);
            }

            _sectionDragSource = null;
            ClearDropPreview();
        }
    }

    private void RetouchSection_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(RetouchSection)) is RetouchSection sourceSection &&
            sender is FrameworkElement targetElement &&
            targetElement.DataContext is RetouchSection targetSection)
        {
            UpdateDropPreview(sourceSection, targetSection, e.GetPosition(targetElement).Y > targetElement.ActualHeight / 2);
            e.Effects = System.Windows.DragDropEffects.Move;
        }
        else
        {
            e.Effects = System.Windows.DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void RetouchSection_PreviewDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (e.Data.GetData(typeof(RetouchSection)) is RetouchSection sourceSection &&
            _sectionDropTarget is not null)
        {
            MoveDraggedSection(sourceSection, _sectionDropTarget, _dropAfterTarget);
            ClearDropPreview();
            SaveSectionOrder();
            e.Handled = true;
        }
    }

    private void UpdateDropPreview(RetouchSection sourceSection, RetouchSection targetSection, bool dropAfterTarget)
    {
        if (ReferenceEquals(sourceSection, targetSection))
        {
            return;
        }

        if (ReferenceEquals(_sectionDropTarget, targetSection) && _dropAfterTarget == dropAfterTarget)
        {
            return;
        }

        ClearDropPreview();
        _sectionDropTarget = targetSection;
        _dropAfterTarget = dropAfterTarget;

        if (dropAfterTarget)
        {
            targetSection.DragGapAfter = 18;
        }
        else
        {
            targetSection.DragGapBefore = 18;
        }
    }

    private void MoveDraggedSection(RetouchSection sourceSection, RetouchSection targetSection, bool dropAfterTarget)
    {
        if (ReferenceEquals(sourceSection, targetSection))
        {
            return;
        }

        int oldIndex = RetouchSections.IndexOf(sourceSection);
        int targetIndex = RetouchSections.IndexOf(targetSection);

        if (oldIndex < 0 || targetIndex < 0)
        {
            return;
        }

        int newIndex = dropAfterTarget ? targetIndex + 1 : targetIndex;
        if (oldIndex < newIndex)
        {
            newIndex--;
        }

        if (oldIndex == newIndex)
        {
            return;
        }

        RetouchSections.Move(oldIndex, newIndex);
    }

    private void ClearDropPreview()
    {
        if (_sectionDropTarget is not null)
        {
            _sectionDropTarget.DragGapBefore = 0;
            _sectionDropTarget.DragGapAfter = 0;
        }

        _sectionDropTarget = null;
        _dropAfterTarget = false;
    }

    private void RestoreSectionOrder()
    {
        if (!File.Exists(SectionOrderPath))
        {
            return;
        }

        try
        {
            string[]? sectionIds = JsonSerializer.Deserialize<string[]>(File.ReadAllText(SectionOrderPath));
            if (sectionIds is null || sectionIds.Length == 0)
            {
                return;
            }

            Dictionary<string, RetouchSection> sectionsById = RetouchSections.ToDictionary(section => section.Id);
            List<RetouchSection> orderedSections = new();

            foreach (string sectionId in sectionIds)
            {
                if (sectionsById.Remove(sectionId, out RetouchSection? section))
                {
                    orderedSections.Add(section);
                }
            }

            orderedSections.AddRange(RetouchSections.Where(section => sectionsById.ContainsKey(section.Id)));

            RetouchSections.Clear();
            foreach (RetouchSection section in orderedSections)
            {
                RetouchSections.Add(section);
            }
        }
        catch (IOException)
        {
        }
        catch (JsonException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void SaveSectionOrder()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(RetouchSections.Select(section => section.Id).ToArray());
            File.WriteAllText(SectionOrderPath, json);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
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

public sealed class RetouchSection : INotifyPropertyChanged
{
    private bool _isExpanded;
    private double _dragGapBefore;
    private double _dragGapAfter;

    public RetouchSection(string id, string title, bool isExpanded, IReadOnlyList<RetouchControl> controls)
    {
        Id = id;
        Title = title;
        _isExpanded = isExpanded;
        Controls = controls;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; }
    public string Title { get; }
    public Thickness SectionPadding => new(0, DragGapBefore, 0, DragGapAfter);
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value)
            {
                return;
            }

            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    public double DragGapBefore
    {
        get => _dragGapBefore;
        set
        {
            if (Math.Abs(_dragGapBefore - value) < 0.001)
            {
                return;
            }

            _dragGapBefore = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SectionPadding));
        }
    }

    public double DragGapAfter
    {
        get => _dragGapAfter;
        set
        {
            if (Math.Abs(_dragGapAfter - value) < 0.001)
            {
                return;
            }

            _dragGapAfter = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SectionPadding));
        }
    }

    public IReadOnlyList<RetouchControl> Controls { get; }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
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
    public bool IsActionButton { get; }
    public bool IsBackgroundLibrary { get; }
    public string? ColorValue { get; }
    public string? ActionText { get; }
    public IReadOnlyList<BackgroundOption> BackgroundOptions { get; } = Array.Empty<BackgroundOption>();
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

    public static RetouchControl CreateColor(string id, string label, string colorValue)
    {
        return new RetouchControl(id, label, 0, 0, 0, true, colorValue);
    }

    public static RetouchControl CreateAction(string id, string label, string actionText)
    {
        return new RetouchControl(id, label, 0, 0, 0, false, null, true, actionText);
    }

    public static RetouchControl CreateBackgroundLibrary(string id, string label, IReadOnlyList<BackgroundOption> backgroundOptions)
    {
        return new RetouchControl(id, label, backgroundOptions);
    }

    private RetouchControl(string id, string label, double minimum, double maximum, double value, bool isColorPicker, string? colorValue)
        : this(id, label, minimum, maximum, value)
    {
        IsColorPicker = isColorPicker;
        ColorValue = colorValue;
    }

    private RetouchControl(
        string id,
        string label,
        double minimum,
        double maximum,
        double value,
        bool isColorPicker,
        string? colorValue,
        bool isActionButton,
        string? actionText)
        : this(id, label, minimum, maximum, value, isColorPicker, colorValue)
    {
        IsActionButton = isActionButton;
        ActionText = actionText;
    }

    private RetouchControl(string id, string label, IReadOnlyList<BackgroundOption> backgroundOptions)
        : this(id, label, 0, 0, 0)
    {
        IsBackgroundLibrary = true;
        BackgroundOptions = backgroundOptions;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class BackgroundOption
{
    public BackgroundOption(string name, string previewColor)
    {
        Name = name;
        PreviewColor = previewColor;
    }

    public string Name { get; }
    public string PreviewColor { get; }
}
