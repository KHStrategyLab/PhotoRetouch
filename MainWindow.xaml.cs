using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Forms = System.Windows.Forms;

namespace PhotoRetouch;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private const int CurveAmountLivePreviewIntervalMilliseconds = 140;
    private const int RetouchSliderLivePreviewIntervalMilliseconds = 140;
    private const int RetouchHistoryLimit = 80;
    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PhotoRetouch");
    private static readonly string SectionOrderPath = Path.Combine(SettingsDirectory, "section-order.json");

    private RetouchSection? _sectionDragSource;
    private RetouchSection? _sectionDropTarget;
    private bool _dropAfterTarget;
    private System.Windows.Point _sectionDragStart;
    private bool _isPreviewPanning;
    private System.Windows.Point _previewPanStart;
    private double _previewPanStartX;
    private double _previewPanStartY;
    private PhotoItem? _previewPanPhoto;
    private FrameworkElement? _previewPanElement;
    private bool _isWholeSplitPanning;
    private bool _isApplyingZoomSliderValue;
    private bool _isZoomSliderRenderApplyQueued;
    private bool _isPhotoListNavigationActive;
    private readonly Dictionary<PhotoItem, (double X, double Y)> _previewPanStartByPhoto = new();
    private PhotoItem? _selectionAnchor;
    private System.Windows.Point _previewZoomOrigin = new(0.5, 0.5);
    private bool _isPreviewProcessing;
    private bool _showPreviewProcessingOverlay;
    private bool _isShowingOriginalPreview;
    private bool _pendingPreviewAdjustment;
    private bool _pendingPreviewAdjustmentShowsOverlay;
    private RetouchControl? _draggingCurveControl;
    private CurvePoint? _draggingCurvePoint;
    private bool _curveDragChanged;
    private bool _pendingCurveKeyboardPreview;
    private bool _isUpdatingCurveAmountFromSlider;
    private bool _pendingCurveAmountLivePreview;
    private bool _isUpdatingRetouchSliderFromSlider;
    private bool _pendingRetouchSliderLivePreview;
    private bool _isResettingRetouchControlsForPhotoChange;
    private bool _isSectionOrderEditMode;
    private RetouchAdjustmentState? _retouchSliderUndoBeforeState;
    private RetouchAdjustmentState? _curveAmountUndoBeforeState;
    private RetouchAdjustmentState? _curveDragUndoBeforeState;
    private RetouchAdjustmentState? _curveKeyboardUndoBeforeState;
    private RetouchSection? _activeRetouchSection;
    private RetouchControl? _selectedCurveControl;
    private CurvePoint? _selectedCurvePoint;
    private readonly System.Windows.Threading.DispatcherTimer _curveAmountPreviewTimer;
    private readonly System.Windows.Threading.DispatcherTimer _retouchSliderPreviewTimer;
    private readonly List<RetouchHistoryEntry> _undoHistory = new();
    private readonly List<RetouchHistoryEntry> _redoHistory = new();

    public ObservableCollection<PersonOption> People { get; } = CreatePeople();
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    public ObservableCollection<PhotoItem> SelectedPhotos { get; } = new();

    private PhotoItem? _selectedPhoto;
    private SolidColorBrush _previewBackgroundBrush = CreatePreviewBackgroundBrush(PreviewBackgroundSettings.BackgroundColor);
    private double _zoomPercent = 100;
    private double _panX;
    private double _panY;
    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsPreviewProcessing
    {
        get => _isPreviewProcessing;
        private set
        {
            if (_isPreviewProcessing == value)
            {
                return;
            }

            _isPreviewProcessing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }
    }

    public Visibility PreviewProcessingVisibility => IsPreviewProcessing && _showPreviewProcessingOverlay ? Visibility.Visible : Visibility.Collapsed;

    public bool IsSectionOrderEditMode
    {
        get => _isSectionOrderEditMode;
        private set
        {
            if (_isSectionOrderEditMode == value)
            {
                return;
            }

            _isSectionOrderEditMode = value;
            OnPropertyChanged();
        }
    }

    public PhotoItem? SelectedPhoto
    {
        get => _selectedPhoto;
        set
        {
            if (ReferenceEquals(_selectedPhoto, value))
            {
                return;
            }

            _selectedPhoto = value;
            ResetPreviewPan();
            ZoomPercent = 100;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PhotoCountText));
            OnPropertyChanged(nameof(PhotoSelectionText));
            OnPropertyChanged(nameof(PhotoPreviewVisibility));
            OnPropertyChanged(nameof(MockPreviewVisibility));
            OnPropertyChanged(nameof(PreviewRows));
            OnPropertyChanged(nameof(PreviewColumns));
            OnPreviewTransformPropertiesChanged();
        }
    }

    public string PhotoCountText => $"{Photos.Count} open files";
    public string PhotoSelectionText => $"{SelectedPhotos.Count} / {Photos.Count} selected";
    public Visibility EmptyPhotoListVisibility => Photos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PhotoListVisibility => Photos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PhotoPreviewVisibility => SelectedPhotos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MockPreviewVisibility => SelectedPhotos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PreviewTitleVisibility => SelectedPhotos.Count == 1 ? Visibility.Visible : Visibility.Collapsed;
    public string PreviewTitleText => _isShowingOriginalPreview ? "Original" : "Preview";
    public System.Windows.Media.Brush PreviewBackgroundBrush => _previewBackgroundBrush;
    public bool IsSplitPreview => SelectedPhotos.Count > 1;
    public int PreviewRows => SelectedPhotos.Count switch
    {
        0 or 1 or 2 or 3 => 1,
        _ => 2
    };

    public int PreviewColumns => SelectedPhotos.Count switch
    {
        0 or 1 => 1,
        2 => 2,
        3 => 3,
        4 => 2,
        5 or 6 => 3,
        _ => 4
    };

    public System.Windows.Point PreviewZoomOrigin
    {
        get => _previewZoomOrigin;
        private set
        {
            if (_previewZoomOrigin == value)
            {
                return;
            }

            _previewZoomOrigin = value;
            OnPropertyChanged();
        }
    }

    public double ZoomPercent
    {
        get => _zoomPercent;
        set
        {
            if (Math.Abs(_zoomPercent - value) < 0.001)
            {
                return;
            }

            _zoomPercent = Math.Clamp(value, 25, 200);
            if (!_isApplyingZoomSliderValue)
            {
                QueueZoomSliderRenderApply();
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(ZoomScale));
            OnPreviewTransformPropertiesChanged();
        }
    }

    public double ZoomScale => ZoomPercent / 100;
    public double SplitZoomScale => 1;
    public double SplitPanX => 0;
    public double SplitPanY => 0;
    public double PanX
    {
        get => _panX;
        set
        {
            if (Math.Abs(_panX - value) < 0.001)
            {
                return;
            }

            _panX = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SplitPanX));
        }
    }

    public double PanY
    {
        get => _panY;
        set
        {
            if (Math.Abs(_panY - value) < 0.001)
            {
                return;
            }

            _panY = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SplitPanY));
        }
    }

    public ObservableCollection<RetouchSection> RetouchSections { get; } = new()
    {
        new RetouchSection(
            "skin",
            "\uD53C\uBD80",
            false,
            new RetouchControl[]
            {
                new("blemish_remove", "\uC7A1\uD2F0 \uC81C\uAC70", 0, 100, 0),
                new("skin_smooth", "\uD53C\uBD80\uACB0 \uC815\uB9AC", 0, 100, 20),
                new("pore_clean", "\uBAA8\uACF5 \uC815\uB9AC", 0, 100, 0),
                new("tone_even", "\uD53C\uBD80\uD1A4 \uBCF4\uC815", 0, 100, 0)
            }),
        new RetouchSection(
            "face_shape",
            "\uC5BC\uAD74\uD615",
            false,
            new RetouchControl[]
            {
                new("oval_face", "\uACC4\uB780\uD615 \uBCF4\uC815", 0, 100, 0),
                new("face_balance", "\uC88C\uC6B0 \uBC38\uB7F0\uC2A4", 0, 100, 0),
                new("cheekbone_soften", "\uAD11\uB300 \uC644\uD654", 0, 100, 0),
                new("jawline_define", "\uD131\uC120 \uC120\uBA85\uB3C4", 0, 100, 0),
                new("chin_length", "\uD131\uB05D \uAE38\uC774", -100, 100, 0),
                new("chin_width", "\uD131\uB05D \uD3ED", -100, 100, 0),
                new("jaw_balance", "\uD131 \uC88C\uC6B0 \uADE0\uD615", -100, 100, 0),
                new("double_chin", "\uC774\uC911\uD131 \uC644\uD654", 0, 100, 0),
                new("neck_jaw_edge", "\uBAA9\uACFC \uD131 \uACBD\uACC4", 0, 100, 0)
            }),
        new RetouchSection(
            "background",
            "\uBC30\uACBD",
            false,
            new RetouchControl[]
            {
                RetouchControl.CreateAction("background_image", "\uBC30\uACBD \uC120\uD0DD", "\uD30C\uC77C \uBD88\uB7EC\uC624\uAE30"),
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
                new("background_blend", "\uACBD\uACC4 \uBE14\uB80C\uB529", 0, 100, 20)
            }),
        new RetouchSection(
            "photo_adjust",
            "\uD1A4 \uBCF4\uC815",
            false,
            new RetouchControl[]
            {
                RetouchControl.CreateCurve("photo_curves", "\uCEE4\uBE0C \uC870\uC815"),
                new("photo_brightness", "\uB178\uCD9C", -100, 100, 0),
                new("photo_contrast", "\uB300\uBE44", -100, 100, 0),
                new("photo_saturation", "\uCC44\uB3C4", -100, 100, 0),
                new("photo_white_balance", "\uD654\uC774\uD2B8\uBC38\uB7F0\uC2A4", -100, 100, 0),
                new("photo_blur_sharpen", "\uC120\uBA85\uB3C4", -100, 100, 0)
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
        _curveAmountPreviewTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(CurveAmountLivePreviewIntervalMilliseconds)
        };
        _curveAmountPreviewTimer.Tick += CurveAmountPreviewTimer_Tick;
        _retouchSliderPreviewTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RetouchSliderLivePreviewIntervalMilliseconds)
        };
        _retouchSliderPreviewTimer.Tick += RetouchSliderPreviewTimer_Tick;
        RestoreSectionOrder();
        SubscribeRetouchControlChanges();
        InitializeComponent();
        DataContext = this;
        Photos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PhotoCountText));
            OnPropertyChanged(nameof(PhotoSelectionText));
            OnPropertyChanged(nameof(EmptyPhotoListVisibility));
            OnPropertyChanged(nameof(PhotoListVisibility));
        };
        SelectedPhotos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PhotoSelectionText));
            OnPropertyChanged(nameof(PhotoPreviewVisibility));
            OnPropertyChanged(nameof(MockPreviewVisibility));
            OnPropertyChanged(nameof(PreviewTitleVisibility));
            OnPropertyChanged(nameof(PreviewRows));
            OnPropertyChanged(nameof(PreviewColumns));
            OnPropertyChanged(nameof(IsSplitPreview));
            OnPreviewTransformPropertiesChanged();
        };
    }

    private void OnPreviewTransformPropertiesChanged()
    {
        OnPropertyChanged(nameof(SplitZoomScale));
        OnPropertyChanged(nameof(SplitPanX));
        OnPropertyChanged(nameof(SplitPanY));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MoveToRightmostScreen();
        LoadWorkingFolderPhotos();
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
        if (!IsTextEditingElementFocused() &&
            (e.Key == Key.Delete || e.Key == Key.Back) &&
            TryDeleteSelectedCurvePoint())
        {
            e.Handled = true;
            return;
        }

        if (!IsTextEditingElementFocused() && TryNudgeSelectedCurvePoint(e))
        {
            e.Handled = true;
            return;
        }

        if (!IsTextEditingElementFocused() && TryNavigatePhotoListByKey(e))
        {
            e.Handled = true;
            return;
        }

        if (ShortcutSettings.Matches(e, ShortcutSettings.RenamePhotoShortcut))
        {
            RenameSelectedPhoto();
            e.Handled = true;
            return;
        }

        if (ShortcutSettings.Matches(e, ShortcutSettings.ToggleSectionOrderEditShortcut))
        {
            ToggleSectionOrderEditMode();
            e.Handled = true;
            return;
        }

        if (!IsTextEditingElementFocused() &&
            ShortcutSettings.Matches(e, ShortcutSettings.UndoShortcut))
        {
            _ = UndoRetouchAsync();
            e.Handled = true;
            return;
        }

        if (!IsTextEditingElementFocused() &&
            ShortcutSettings.Matches(e, ShortcutSettings.RedoShortcut))
        {
            _ = RedoRetouchAsync();
            e.Handled = true;
            return;
        }

        if (!ShortcutSettings.Matches(e, ShortcutSettings.CompareOriginalShortcut) || e.IsRepeat)
        {
            return;
        }

        ShowOriginalPreview();
        e.Handled = true;
    }

    private void Window_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsPhotoListItemMouseSource(e.OriginalSource as DependencyObject))
        {
            _isPhotoListNavigationActive = false;
        }
    }

    private static bool IsTextEditingElementFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
    }

    private bool TryNavigatePhotoListByKey(System.Windows.Input.KeyEventArgs e)
    {
        if (IsPreviewProcessing || Photos.Count == 0 || !_isPhotoListNavigationActive)
        {
            return false;
        }

        Key key = ShortcutSettings.NormalizeKey(e.Key, e.SystemKey);
        int direction = key switch
        {
            Key.Up or Key.Left => -1,
            Key.Down or Key.Right => 1,
            _ => 0
        };

        if (direction == 0)
        {
            return false;
        }

        NavigatePhotoList(direction);
        return true;
    }

    private bool IsPhotoListItemMouseSource(DependencyObject? source)
    {
        bool hasPhotoItem = false;
        bool isInsidePhotoListPanel = false;

        while (source is not null)
        {
            if (source is FrameworkElement { DataContext: PhotoItem })
            {
                hasPhotoItem = true;
            }

            if (ReferenceEquals(source, PhotoListPanel))
            {
                isInsidePhotoListPanel = true;
                break;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return hasPhotoItem && isInsidePhotoListPanel;
    }

    private bool TryNudgeSelectedCurvePoint(System.Windows.Input.KeyEventArgs e)
    {
        if (IsPreviewProcessing || _selectedCurveControl is null || _selectedCurveControl.SelectedCurvePoint is null)
        {
            return false;
        }

        Key key = ShortcutSettings.NormalizeKey(e.Key, e.SystemKey);
        if (!IsCurveNudgeKey(key))
        {
            return false;
        }

        double step = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift ? 10 : 1;
        double inputDelta = key switch
        {
            Key.Left => -step,
            Key.Right => step,
            _ => 0
        };
        double outputDelta = key switch
        {
            Key.Up => step,
            Key.Down => -step,
            _ => 0
        };

        _curveKeyboardUndoBeforeState ??= CaptureRetouchState();
        if (_selectedCurveControl.NudgeSelectedCurvePoint(inputDelta, outputDelta))
        {
            _pendingCurveKeyboardPreview = true;
        }
        else if (!_pendingCurveKeyboardPreview)
        {
            _curveKeyboardUndoBeforeState = null;
        }

        return true;
    }

    private static bool IsCurveNudgeKey(Key key)
    {
        return key is Key.Left or Key.Right or Key.Up or Key.Down;
    }

    private async void Window_PreviewKeyUp(object sender, System.Windows.Input.KeyEventArgs e)
    {
        Key key = ShortcutSettings.NormalizeKey(e.Key, e.SystemKey);
        if (_pendingCurveKeyboardPreview && IsCurveNudgeKey(key))
        {
            _pendingCurveKeyboardPreview = false;
            PushRetouchHistory(_curveKeyboardUndoBeforeState, CaptureRetouchState());
            _curveKeyboardUndoBeforeState = null;
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
            e.Handled = true;
            return;
        }

        if (key != ShortcutSettings.CompareOriginalShortcut.Key)
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

    private void PreviewBackgroundBlack_Click(object sender, RoutedEventArgs e)
    {
        SetPreviewBackgroundColor("#000000");
    }

    private void PreviewBackgroundGray_Click(object sender, RoutedEventArgs e)
    {
        SetPreviewBackgroundColor("#404040");
    }

    private void PreviewBackgroundCustom_Click(object sender, RoutedEventArgs e)
    {
        ColorInputWindow colorInputWindow = new(PreviewBackgroundSettings.BackgroundColor)
        {
            Owner = this
        };

        if (colorInputWindow.ShowDialog() == true)
        {
            SetPreviewBackgroundColor(colorInputWindow.ColorText);
        }
    }

    private void PreviewBackgroundChooseColor_Click(object sender, RoutedEventArgs e)
    {
        using Forms.ColorDialog dialog = new()
        {
            AllowFullOpen = true,
            FullOpen = true
        };

        if (TryParsePreviewBackgroundColor(PreviewBackgroundSettings.BackgroundColor, out System.Windows.Media.Color currentColor))
        {
            dialog.Color = System.Drawing.Color.FromArgb(currentColor.R, currentColor.G, currentColor.B);
        }

        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            SetPreviewBackgroundColor($"#{dialog.Color.R:X2}{dialog.Color.G:X2}{dialog.Color.B:X2}");
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        ColorManagementMode previousMode = ColorManagementSettings.Mode;
        string? previousManualProfilePath = ColorManagementSettings.ManualDisplayProfilePath;
        string previousWorkingFolderPath = WorkingFolderSettings.WorkingFolderPath;

        SettingsWindow settingsWindow = new()
        {
            Owner = this
        };
        settingsWindow.ShowDialog();

        if (previousMode != ColorManagementSettings.Mode ||
            !string.Equals(previousManualProfilePath, ColorManagementSettings.ManualDisplayProfilePath, StringComparison.OrdinalIgnoreCase))
        {
            ReloadPhotosForColorManagement();
        }

        _ = ApplyPhotoAdjustmentsAsync();

        if (!string.Equals(previousWorkingFolderPath, WorkingFolderSettings.WorkingFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            LoadWorkingFolderPhotos();
        }
    }

    private void SubscribeRetouchControlChanges()
    {
        foreach (RetouchControl control in RetouchSections.SelectMany(section => section.Controls))
        {
            control.PropertyChanged += RetouchControl_PropertyChanged;
        }
    }

    private async void RetouchControl_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isResettingRetouchControlsForPhotoChange)
        {
            return;
        }

        if (e.PropertyName is not (nameof(RetouchControl.Value) or nameof(RetouchControl.CurveChannel)) ||
            sender is not RetouchControl control ||
            control.Id is not ("photo_brightness" or "photo_contrast" or "photo_saturation" or "photo_white_balance" or "photo_blur_sharpen" or "photo_curves"))
        {
            return;
        }

        if (_isUpdatingCurveAmountFromSlider &&
            control.Id == "photo_curves" &&
            e.PropertyName == nameof(RetouchControl.Value))
        {
            return;
        }

        if (_isUpdatingRetouchSliderFromSlider &&
            e.PropertyName == nameof(RetouchControl.Value))
        {
            return;
        }

        await ApplyPhotoAdjustmentsAsync(showOverlay: control.Id != "photo_curves");
    }

    private async Task ApplyPhotoAdjustmentsAsync(bool showOverlay = true)
    {
        if (IsPreviewProcessing)
        {
            _pendingPreviewAdjustment = true;
            _pendingPreviewAdjustmentShowsOverlay |= showOverlay;
            return;
        }

        double brightness = FindRetouchControl("photo_brightness")?.Value ?? 0;
        double contrast = FindRetouchControl("photo_contrast")?.Value ?? 0;
        double saturation = FindRetouchControl("photo_saturation")?.Value ?? 0;
        double whiteBalance = FindRetouchControl("photo_white_balance")?.Value ?? 0;
        double blurSharpen = FindRetouchControl("photo_blur_sharpen")?.Value ?? 0;
        RetouchControl? curveControl = FindRetouchControl("photo_curves");
        double curveAmount = curveControl?.Value ?? 0;
        CurveChannel curveChannel = curveControl?.CurveChannel ?? CurveChannel.All;
        byte[] curveLookup = curveControl?.BuildCurveLookupTable(curveChannel) ?? PhotoAdjustmentEngine.CreateIdentityLookupTable();
        PhotoItem[] adjustmentTargets = GetPreviewAdjustmentTargets();
        if (adjustmentTargets.Length == 0)
        {
            return;
        }

        int? visiblePreviewMaxLongSide = GetVisibleEffectPreviewMaxLongSide();
        Dictionary<PhotoItem, BitmapSource> previewSources = adjustmentTargets.ToDictionary(
            photo => photo,
            photo => photo.GetEffectPreviewSource(visiblePreviewMaxLongSide));

        try
        {
            _showPreviewProcessingOverlay = showOverlay;
            IsPreviewProcessing = true;
            Dictionary<PhotoItem, BitmapSource> adjustedImages = await Task.Run(() =>
            {
                Dictionary<PhotoItem, BitmapSource> results = new();
                foreach ((PhotoItem photo, BitmapSource previewSource) in previewSources)
                {
                    results[photo] = PhotoAdjustmentEngine.ApplyBasicTone(previewSource, brightness, contrast, saturation, whiteBalance, blurSharpen, curveAmount, curveChannel, curveLookup);
                }

                return results;
            });

            foreach ((PhotoItem photo, BitmapSource image) in adjustedImages)
            {
                photo.SetAdjustedImage(image);
            }
        }
        finally
        {
            IsPreviewProcessing = false;
            _showPreviewProcessingOverlay = false;
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }

        if (_pendingPreviewAdjustment)
        {
            bool pendingShowOverlay = _pendingPreviewAdjustmentShowsOverlay;
            _pendingPreviewAdjustment = false;
            _pendingPreviewAdjustmentShowsOverlay = false;
            await ApplyPhotoAdjustmentsAsync(pendingShowOverlay);
        }
    }

    private PhotoItem[] GetPreviewAdjustmentTargets()
    {
        return SelectedPhotos.Count == 1 && SelectedPhoto is not null
            ? new[] { SelectedPhoto }
            : Array.Empty<PhotoItem>();
    }

    private int? GetVisibleEffectPreviewMaxLongSide()
    {
        if (SelectedPhoto is null ||
            PreviewSurface.ActualWidth <= 0 ||
            PreviewSurface.ActualHeight <= 0)
        {
            return null;
        }

        double viewportLongSide = Math.Max(GetPreviewCellWidth(), GetPreviewCellHeight());
        if (viewportLongSide <= 0)
        {
            return null;
        }

        double zoomScale = Math.Max(1, SelectedPhoto.PreviewZoomScale);
        int maxLongSide = (int)Math.Ceiling(viewportLongSide * zoomScale);
        return Math.Clamp(maxLongSide, 320, PreviewSettings.MaximumMaxLongSidePixels);
    }

    private RetouchControl? FindRetouchControl(string id)
    {
        return RetouchSections
            .SelectMany(section => section.Controls)
            .FirstOrDefault(control => control.Id == id);
    }

    private void LoadPhotosButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        Microsoft.Win32.OpenFileDialog dialog = new()
        {
            Title = "\uC0AC\uC9C4 \uBD88\uB7EC\uC624\uAE30",
            Filter = "Image files|*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.bmp|JPEG|*.jpg;*.jpeg|PNG|*.png|TIFF|*.tif;*.tiff|All files|*.*",
            InitialDirectory = Directory.Exists(WorkingFolderSettings.WorkingFolderPath)
                ? WorkingFolderSettings.WorkingFolderPath
                : WorkingFolderSettings.DefaultWorkingFolderPath,
            Multiselect = true
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        AddPhotos(dialog.FileNames);
    }

    private async void SavePhotoButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            System.Windows.MessageBox.Show(
                this,
                "\uC800\uC7A5\uD560 \uC0AC\uC9C4 1\uC7A5\uC744 \uC120\uD0DD\uD574\uC918.",
                "\uC800\uC7A5",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        PhotoItem photo = SelectedPhoto;
        (double brightness, double contrast, double saturation, double whiteBalance, double blurSharpen, double curveAmount, CurveChannel curveChannel, byte[] curveLookup) = CaptureCurrentAdjustmentValues();
        if (!PhotoAdjustmentEngine.HasEffectiveAdjustment(brightness, contrast, saturation, whiteBalance, blurSharpen, curveAmount, curveLookup))
        {
            System.Windows.MessageBox.Show(
                this,
                "\uBCC0\uACBD\uB41C \uB0B4\uC6A9\uC774 \uC5C6\uC5B4 \uC800\uC7A5\uD558\uC9C0 \uC54A\uC558\uC5B4.",
                "\uC800\uC7A5",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        string savePath = CreateNumberedSavePath(photo.Path);

        try
        {
            IsPreviewProcessing = true;
            BitmapSource adjustedImage = await Task.Run(() =>
                PhotoAdjustmentEngine.ApplyBasicTone(
                    photo.BaseImage,
                    brightness,
                    contrast,
                    saturation,
                    whiteBalance,
                    blurSharpen,
                    curveAmount,
                    curveChannel,
                    curveLookup));

            await Task.Run(() => SaveBitmapToFile(adjustedImage, savePath));
            System.Windows.MessageBox.Show(
                this,
                $"\uC800\uC7A5\uD588\uC5B4.\n{savePath}",
                "\uC800\uC7A5",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "\uC800\uC7A5",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewProcessing = false;
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }
    }

    private (double Brightness, double Contrast, double Saturation, double WhiteBalance, double BlurSharpen, double CurveAmount, CurveChannel CurveChannel, byte[] CurveLookup) CaptureCurrentAdjustmentValues()
    {
        RetouchControl? curveControl = FindRetouchControl("photo_curves");
        CurveChannel curveChannel = curveControl?.CurveChannel ?? CurveChannel.All;
        return (
            FindRetouchControl("photo_brightness")?.Value ?? 0,
            FindRetouchControl("photo_contrast")?.Value ?? 0,
            FindRetouchControl("photo_saturation")?.Value ?? 0,
            FindRetouchControl("photo_white_balance")?.Value ?? 0,
            FindRetouchControl("photo_blur_sharpen")?.Value ?? 0,
            curveControl?.Value ?? 0,
            curveChannel,
            curveControl?.BuildCurveLookupTable(curveChannel) ?? PhotoAdjustmentEngine.CreateIdentityLookupTable());
    }

    private static string CreateNumberedSavePath(string sourcePath)
    {
        string? directory = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        }

        string baseName = Path.GetFileNameWithoutExtension(sourcePath);
        string extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".png";
        }

        for (int index = 1; index < 10_000; index++)
        {
            string candidate = Path.Combine(directory, $"{baseName}_{index}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("\uC800\uC7A5\uD560 \uD30C\uC77C \uC774\uB984\uC744 \uB9CC\uB4E4 \uC218 \uC5C6\uC5B4.");
    }

    private static void SaveBitmapToFile(BitmapSource image, string path)
    {
        BitmapSource saveImage = PrepareBitmapForSave(image, Path.GetExtension(path));
        BitmapEncoder encoder = CreateBitmapEncoder(path);
        encoder.Frames.Add(BitmapFrame.Create(saveImage));

        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static BitmapSource PrepareBitmapForSave(BitmapSource image, string extension)
    {
        string normalizedExtension = extension.ToLowerInvariant();
        if (normalizedExtension is ".jpg" or ".jpeg")
        {
            if (image.Format == PixelFormats.Bgr24)
            {
                return image;
            }

            FormatConvertedBitmap converted = new(image, PixelFormats.Bgr24, null, 0);
            converted.Freeze();
            return converted;
        }

        if (image.Format == PixelFormats.Bgra32 ||
            image.Format == PixelFormats.Pbgra32 ||
            image.Format == PixelFormats.Bgr24)
        {
            return image;
        }

        FormatConvertedBitmap fallback = new(image, PixelFormats.Bgra32, null, 0);
        fallback.Freeze();
        return fallback;
    }

    private static BitmapEncoder CreateBitmapEncoder(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            // WPF uses 1-100; keep JPEG fixed at the maximum quality, equivalent to Photoshop quality 12.
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 100 },
            ".png" => new PngBitmapEncoder(),
            ".tif" or ".tiff" => new TiffBitmapEncoder(),
            ".bmp" => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };
    }

    private async void RefreshWorkingFolderButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        Dictionary<string, PhotoItem> reusablePhotos = CaptureReusablePhotoCache();
        ClearPhotoListForWorkingFolderReload();
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        LoadWorkingFolderPhotos(clearExisting: false, reusablePhotos);
    }

    private void LoadWorkingFolderPhotos()
    {
        LoadWorkingFolderPhotos(clearExisting: true, reusablePhotos: null);
    }

    private void LoadWorkingFolderPhotos(bool clearExisting, IReadOnlyDictionary<string, PhotoItem>? reusablePhotos)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (clearExisting)
        {
            ClearPhotoListForWorkingFolderReload();
        }

        if (!Directory.Exists(WorkingFolderSettings.WorkingFolderPath))
        {
            return;
        }

        string[] imagePaths = Directory
            .EnumerateFiles(WorkingFolderSettings.WorkingFolderPath)
            .Where(IsSupportedImageFile)
            .OrderByDescending(File.GetLastWriteTime)
            .ToArray();

        if (reusablePhotos is null)
        {
            AddPhotos(imagePaths);
        }
        else
        {
            AddPhotosWithReusableCache(imagePaths, reusablePhotos);
        }

        ActivatePhotoListNavigationForCurrentSelection();
    }

    private void ClearPhotoListForWorkingFolderReload()
    {
        if (Photos.Count == 0 && SelectedPhotos.Count == 0 && SelectedPhoto is null)
        {
            return;
        }

        SetSelectedPhotos(Array.Empty<PhotoItem>(), null);
        Photos.Clear();
        _selectionAnchor = null;
        _isPhotoListNavigationActive = false;
    }

    private void ActivatePhotoListNavigationForCurrentSelection()
    {
        _selectionAnchor = SelectedPhoto;
        _isPhotoListNavigationActive = SelectedPhoto is not null;

        if (SelectedPhoto is null)
        {
            return;
        }

        _ = Dispatcher.BeginInvoke(() =>
        {
            PhotoListPanel.Focus();
            Keyboard.Focus(PhotoListPanel);
        }, System.Windows.Threading.DispatcherPriority.Input);
    }

    private Dictionary<string, PhotoItem> CaptureReusablePhotoCache()
    {
        return Photos
            .GroupBy(photo => NormalizePhotoPath(photo.Path), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last(), StringComparer.OrdinalIgnoreCase);
    }

    private void AddPhotosWithReusableCache(IEnumerable<string> fileNames, IReadOnlyDictionary<string, PhotoItem> reusablePhotos)
    {
        HashSet<string> existingPaths = new(StringComparer.OrdinalIgnoreCase);
        bool addedAnyPhoto = false;

        foreach (string fileName in fileNames.Reverse())
        {
            string normalizedPath = NormalizePhotoPath(fileName);
            if (!existingPaths.Add(normalizedPath))
            {
                continue;
            }

            try
            {
                PhotoItem photo = reusablePhotos.TryGetValue(normalizedPath, out PhotoItem? cachedPhoto) &&
                                  cachedPhoto.MatchesFileVersion(fileName)
                    ? cachedPhoto
                    : PhotoItem.Load(fileName);
                photo.ResetRetouchWorkState();
                photo.IsSelected = false;
                Photos.Insert(0, photo);
                addedAnyPhoto = true;
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (addedAnyPhoto)
        {
            SelectOnly(Photos.FirstOrDefault());
        }
    }

    private void PhotoDropTarget_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            e.Effects = System.Windows.DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = GetDroppedImagePaths(e).Any() ? System.Windows.DragDropEffects.Copy : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void PhotoDropTarget_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            e.Handled = true;
            return;
        }

        string[] imagePaths = GetDroppedImagePaths(e).ToArray();
        if (imagePaths.Length > 0)
        {
            AddPhotos(imagePaths);
        }

        e.Handled = true;
    }

    private void AddPhotos(IEnumerable<string> fileNames, bool preserveSelection = false)
    {
        PhotoItem? previousSelectedPhoto = SelectedPhoto;
        PhotoItem[] previousSelectedPhotos = SelectedPhotos.ToArray();
        HashSet<string> existingPaths = Photos
            .Select(photo => NormalizePhotoPath(photo.Path))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        bool addedAnyPhoto = false;

        foreach (string fileName in fileNames.Reverse())
        {
            string normalizedPath = NormalizePhotoPath(fileName);
            if (!existingPaths.Add(normalizedPath))
            {
                continue;
            }

            try
            {
                Photos.Insert(0, PhotoItem.Load(fileName));
                addedAnyPhoto = true;
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        if (!addedAnyPhoto)
        {
            return;
        }

        if (preserveSelection && previousSelectedPhotos.Length > 0)
        {
            SetSelectedPhotos(previousSelectedPhotos.Where(Photos.Contains), previousSelectedPhoto);
        }
        else
        {
            SelectOnly(Photos.FirstOrDefault());
        }
    }

    private static string NormalizePhotoPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private void ReloadPhotosForColorManagement()
    {
        PhotoItem? selectedPhoto = SelectedPhoto;
        PhotoItem[] selectedPhotos = SelectedPhotos.ToArray();

        foreach (PhotoItem photo in Photos)
        {
            try
            {
                photo.ReloadImage();
            }
            catch (IOException)
            {
            }
            catch (NotSupportedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        _ = ApplyPhotoAdjustmentsAsync();
        SetSelectedPhotos(selectedPhotos.Where(Photos.Contains), selectedPhoto);
    }

    private static IEnumerable<string> GetDroppedImagePaths(System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop) ||
            e.Data.GetData(System.Windows.DataFormats.FileDrop) is not string[] paths)
        {
            return Array.Empty<string>();
        }

        return paths.Where(IsSupportedImageFile);
    }

    private static bool IsSupportedImageFile(string path)
    {
        string extension = System.IO.Path.GetExtension(path).ToLowerInvariant();
        return extension is ".jpg" or ".jpeg" or ".png" or ".tif" or ".tiff" or ".bmp";
    }

    private void PhotoItem_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            e.Handled = true;
            return;
        }

        if ((sender as FrameworkElement)?.DataContext is PhotoItem photo)
        {
            ModifierKeys modifiers = Keyboard.Modifiers;
            bool isSpacePressed = IsSpacePressed();
            bool isAddSelectionPressed = ShortcutSettings.MatchesMouseGesture(modifiers, isSpacePressed, ShortcutSettings.AddSelectionGesture);
            bool isRangeSelectionPressed = ShortcutSettings.MatchesMouseGesture(modifiers, isSpacePressed, ShortcutSettings.RangeSelectionGesture);

            if (isRangeSelectionPressed && _selectionAnchor is not null)
            {
                SelectRange(_selectionAnchor, photo, isAddSelectionPressed);
            }
            else if (isAddSelectionPressed)
            {
                TogglePhotoSelection(photo);
            }
            else
            {
                SelectOnly(photo);
            }

            _selectionAnchor = photo;
            _isPhotoListNavigationActive = true;
            e.Handled = true;
        }
    }

    private void ValueSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void ValueSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not System.Windows.Controls.Slider slider ||
            slider.DataContext is not RetouchControl control ||
            !ShouldLivePreviewSlider(control) ||
            !slider.IsLoaded ||
            (!slider.IsMouseCaptureWithin && !slider.IsKeyboardFocusWithin))
        {
            return;
        }

        _retouchSliderUndoBeforeState ??= CaptureRetouchState();
        if (CommitValueSliderValue(slider))
        {
            _pendingRetouchSliderLivePreview = true;
            if (!_retouchSliderPreviewTimer.IsEnabled)
            {
                _retouchSliderPreviewTimer.Start();
            }
        }
    }

    private async void ValueSlider_CommitValue(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider slider ||
            slider.DataContext is not RetouchControl control)
        {
            return;
        }

        RetouchAdjustmentState before = _retouchSliderUndoBeforeState ?? CaptureRetouchState();
        bool changed = CommitValueSliderValue(slider);
        bool hadPendingPreview = _pendingRetouchSliderLivePreview;
        if (ShouldLivePreviewSlider(control))
        {
            _retouchSliderUndoBeforeState = null;
            _pendingRetouchSliderLivePreview = false;
            _retouchSliderPreviewTimer.Stop();
        }

        PushRetouchHistory(before, CaptureRetouchState());
        if (ShouldLivePreviewSlider(control) && (changed || hadPendingPreview))
        {
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private bool CommitValueSliderValue(System.Windows.Controls.Slider slider)
    {
        if (slider.DataContext is not RetouchControl control)
        {
            return false;
        }

        double previousValue = control.Value;
        _isUpdatingRetouchSliderFromSlider = true;
        try
        {
            slider.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)?.UpdateSource();
        }
        finally
        {
            _isUpdatingRetouchSliderFromSlider = false;
        }

        return Math.Abs(previousValue - control.Value) >= 0.001;
    }

    private async void RetouchSliderPreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (!_pendingRetouchSliderLivePreview)
        {
            _retouchSliderPreviewTimer.Stop();
            return;
        }

        if (IsPreviewProcessing)
        {
            return;
        }

        _pendingRetouchSliderLivePreview = false;
        await ApplyPhotoAdjustmentsAsync(showOverlay: false);
    }

    private void CurveAmountSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (sender is not System.Windows.Controls.Slider slider ||
            slider.DataContext is not RetouchControl control ||
            control.Id != "photo_curves" ||
            !slider.IsLoaded ||
            (!slider.IsMouseCaptureWithin && !slider.IsKeyboardFocusWithin))
        {
            return;
        }

        _curveAmountUndoBeforeState ??= CaptureRetouchState();
        if (CommitCurveAmountSliderValue(slider))
        {
            _pendingCurveAmountLivePreview = true;
            if (!_curveAmountPreviewTimer.IsEnabled)
            {
                _curveAmountPreviewTimer.Start();
            }
        }
    }

    private async void CurveAmountSlider_CommitValue(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Slider slider ||
            slider.DataContext is not RetouchControl control ||
            control.Id != "photo_curves")
        {
            return;
        }

        bool changed = CommitCurveAmountSliderValue(slider);
        bool hadPendingPreview = _pendingCurveAmountLivePreview;
        PushRetouchHistory(_curveAmountUndoBeforeState, CaptureRetouchState());
        _curveAmountUndoBeforeState = null;
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
        if (changed || hadPendingPreview)
        {
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private bool CommitCurveAmountSliderValue(System.Windows.Controls.Slider slider)
    {
        if (slider.DataContext is not RetouchControl control)
        {
            return false;
        }

        double previousValue = control.Value;
        _isUpdatingCurveAmountFromSlider = true;
        try
        {
            slider.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)?.UpdateSource();
        }
        finally
        {
            _isUpdatingCurveAmountFromSlider = false;
        }

        return Math.Abs(previousValue - control.Value) >= 0.001;
    }

    private async void CurveAmountPreviewTimer_Tick(object? sender, EventArgs e)
    {
        if (!_pendingCurveAmountLivePreview)
        {
            _curveAmountPreviewTimer.Stop();
            return;
        }

        if (IsPreviewProcessing)
        {
            return;
        }

        _pendingCurveAmountLivePreview = false;
        await ApplyPhotoAdjustmentsAsync(showOverlay: false);
    }

    private void CurveChannelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (IsPreviewProcessing || sender is not System.Windows.Controls.ComboBox comboBox)
        {
            return;
        }

        RetouchControl? previousSelectedControl = _selectedCurveControl;
        RetouchAdjustmentState before = CaptureRetouchState();
        comboBox.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
        PushRetouchHistory(before, CaptureRetouchState());
        if (previousSelectedControl is not null && ReferenceEquals(previousSelectedControl, comboBox.DataContext))
        {
            ClearSelectedCurvePoint();
        }
    }

    private async void CurvePointValueTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Return)
        {
            return;
        }

        await CommitCurvePointValueTextBoxAsync(sender as System.Windows.Controls.TextBox);
        e.Handled = true;
    }

    private async void CurvePointValueTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        await CommitCurvePointValueTextBoxAsync(sender as System.Windows.Controls.TextBox);
    }

    private async Task CommitCurvePointValueTextBoxAsync(System.Windows.Controls.TextBox? textBox)
    {
        if (textBox is null ||
            IsPreviewProcessing ||
            textBox.DataContext is not RetouchControl control ||
            control.SelectedCurvePoint is null)
        {
            RefreshCurvePointValueTextBox(textBox);
            return;
        }

        if (!int.TryParse(textBox.Text.Trim(), out int value))
        {
            RefreshCurvePointValueTextBox(textBox);
            return;
        }

        RetouchAdjustmentState before = CaptureRetouchState();
        bool changed = textBox.Tag switch
        {
            "Input" => control.SetSelectedCurvePointInput(value),
            "Output" => control.SetSelectedCurvePointOutput(value),
            _ => false
        };

        RefreshCurvePointValueTextBox(textBox);
        if (changed)
        {
            PushRetouchHistory(before, CaptureRetouchState());
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private static void RefreshCurvePointValueTextBox(System.Windows.Controls.TextBox? textBox)
    {
        textBox?.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty)?.UpdateTarget();
    }

    private async void ResetCurveChannelButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing ||
            sender is not FrameworkElement element ||
            element.DataContext is not RetouchControl control ||
            !control.IsCurveEditor)
        {
            return;
        }

        if (ReferenceEquals(_selectedCurveControl, control))
        {
            ClearSelectedCurvePoint();
        }

        RetouchAdjustmentState before = CaptureRetouchState();
        if (control.ResetCurrentCurveChannel())
        {
            PushRetouchHistory(before, CaptureRetouchState());
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private async void ResetRetouchSectionButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing ||
            sender is not FrameworkElement element ||
            element.DataContext is not RetouchSection section)
        {
            return;
        }

        RetouchAdjustmentState before = CaptureRetouchState();
        ClearSelectedCurvePoint();
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
        _pendingRetouchSliderLivePreview = false;
        _retouchSliderUndoBeforeState = null;
        _retouchSliderPreviewTimer.Stop();
        _isResettingRetouchControlsForPhotoChange = true;
        try
        {
            foreach (RetouchControl control in section.Controls)
            {
                control.ResetToDefault();
            }
        }
        finally
        {
            _isResettingRetouchControlsForPhotoChange = false;
        }

        PushRetouchHistory(before, CaptureRetouchState());
        if (section.Controls.Any(IsPhotoAdjustmentControl))
        {
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }

        e.Handled = true;
    }

    private static bool IsPhotoAdjustmentControl(RetouchControl control)
    {
        return control.Id is "photo_brightness" or
            "photo_contrast" or
            "photo_saturation" or
            "photo_white_balance" or
            "photo_blur_sharpen" or
            "photo_curves";
    }

    private static bool ShouldLivePreviewSlider(RetouchControl control)
    {
        return IsPhotoAdjustmentControl(control) && control.Id != "photo_curves";
    }

    private void CurveCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPreviewProcessing ||
            sender is not System.Windows.Controls.Canvas canvas ||
            canvas.DataContext is not RetouchControl control ||
            !control.IsCurveEditor)
        {
            return;
        }

        System.Windows.Point point = e.GetPosition(canvas);
        _curveDragUndoBeforeState = CaptureRetouchState();
        CurvePoint? curvePoint = control.AddCurvePointFromCanvas(point.X, point.Y);
        if (curvePoint is null)
        {
            _curveDragUndoBeforeState = null;
            return;
        }

        _draggingCurveControl = control;
        _draggingCurvePoint = curvePoint;
        _curveDragChanged = true;
        SelectCurvePoint(control, curvePoint);
        Mouse.Capture(canvas);
        e.Handled = true;
    }

    private void CurveCanvas_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (sender is System.Windows.Controls.Canvas canvas)
        {
            MoveDraggingCurvePoint(canvas, e);
        }
    }

    private async void CurveCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await FinishDraggingCurvePointAsync(e);
    }

    private void CurvePoint_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPreviewProcessing ||
            sender is not FrameworkElement element ||
            element.DataContext is not CurvePoint point ||
            FindVisualParent<System.Windows.Controls.Canvas>(element) is not System.Windows.Controls.Canvas canvas ||
            canvas.DataContext is not RetouchControl control)
        {
            return;
        }

        _curveDragUndoBeforeState = CaptureRetouchState();
        _draggingCurveControl = control;
        _draggingCurvePoint = point;
        _curveDragChanged = false;
        SelectCurvePoint(control, point);
        Mouse.Capture(canvas);
        e.Handled = true;
    }

    private void CurvePoint_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (Mouse.Captured is System.Windows.Controls.Canvas canvas)
        {
            MoveDraggingCurvePoint(canvas, e);
        }
    }

    private void MoveDraggingCurvePoint(System.Windows.Controls.Canvas canvas, System.Windows.Input.MouseEventArgs e)
    {
        if (_draggingCurveControl is null ||
            _draggingCurvePoint is null ||
            e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        System.Windows.Point point = e.GetPosition(canvas);
        if (IsOutsideCurveCanvas(point))
        {
            _draggingCurveControl.MarkCurvePointForDeletion(_draggingCurvePoint);
        }
        else
        {
            _draggingCurvePoint.IsPendingDelete = false;
            _draggingCurveControl.MoveCurvePoint(_draggingCurvePoint, point.X, point.Y);
            _curveDragChanged = true;
        }

        e.Handled = true;
    }

    private async void CurvePoint_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        await FinishDraggingCurvePointAsync(e);
    }

    private async Task FinishDraggingCurvePointAsync(MouseButtonEventArgs e)
    {
        RetouchControl? curveControl = _draggingCurveControl;
        CurvePoint? curvePoint = _draggingCurvePoint;
        if (curveControl is null || curvePoint is null)
        {
            return;
        }

        if (Mouse.Captured is not null)
        {
            Mouse.Capture(null);
        }

        _draggingCurveControl = null;
        _draggingCurvePoint = null;
        bool shouldRenderPreview = _curveDragChanged || curvePoint.IsPendingDelete;
        _curveDragChanged = false;
        if (curveControl.DeleteCurvePointIfMarked(curvePoint))
        {
            ClearSelectedCurvePoint();
            PushRetouchHistory(_curveDragUndoBeforeState, CaptureRetouchState());
            _curveDragUndoBeforeState = null;
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
        else if (shouldRenderPreview)
        {
            PushRetouchHistory(_curveDragUndoBeforeState, CaptureRetouchState());
            _curveDragUndoBeforeState = null;
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
        else
        {
            _curveDragUndoBeforeState = null;
        }

        e.Handled = true;
    }

    private async Task DeleteSelectedCurvePointAsync()
    {
        if (_selectedCurveControl is null || _selectedCurvePoint is null)
        {
            return;
        }

        RetouchAdjustmentState before = CaptureRetouchState();
        if (_selectedCurveControl.DeleteCurvePoint(_selectedCurvePoint))
        {
            ClearSelectedCurvePoint();
            PushRetouchHistory(before, CaptureRetouchState());
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private bool TryDeleteSelectedCurvePoint()
    {
        if (_selectedCurveControl is null ||
            _selectedCurvePoint is null ||
            _selectedCurvePoint.IsEndpoint ||
            IsPreviewProcessing)
        {
            return false;
        }

        _ = DeleteSelectedCurvePointAsync();
        return true;
    }

    private static bool IsOutsideCurveCanvas(System.Windows.Point point)
    {
        const double deleteMargin = 10;
        return point.X < -deleteMargin ||
               point.Y < -deleteMargin ||
               point.X > 180 + deleteMargin ||
               point.Y > 180 + deleteMargin;
    }

    private void SelectCurvePoint(RetouchControl control, CurvePoint point)
    {
        if (_selectedCurvePoint is not null)
        {
            _selectedCurvePoint.IsSelected = false;
        }

        if (_selectedCurveControl is not null && !ReferenceEquals(_selectedCurveControl, control))
        {
            _selectedCurveControl.SelectedCurvePoint = null;
        }

        _selectedCurveControl = control;
        _selectedCurvePoint = point;
        control.SelectedCurvePoint = point;
        point.IsSelected = true;
    }

    private void ClearSelectedCurvePoint()
    {
        RetouchControl? selectedCurveControl = _selectedCurveControl;
        if (_selectedCurvePoint is not null)
        {
            _selectedCurvePoint.IsSelected = false;
        }

        _selectedCurveControl = null;
        _selectedCurvePoint = null;
        if (selectedCurveControl is not null)
        {
            selectedCurveControl.SelectedCurvePoint = null;
        }
    }

    private static T? FindVisualParent<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void SelectOnly(PhotoItem? photo)
    {
        SetSelectedPhotos(photo is null ? Array.Empty<PhotoItem>() : new[] { photo }, photo);
    }

    private void PreviousPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        NavigatePhotoList(-1);
    }

    private void NextPhotoButton_Click(object sender, RoutedEventArgs e)
    {
        NavigatePhotoList(1);
    }

    private void NavigatePhotoList(int direction)
    {
        if (IsPreviewProcessing || Photos.Count == 0 || direction == 0)
        {
            return;
        }

        int currentIndex = SelectedPhoto is not null ? Photos.IndexOf(SelectedPhoto) : -1;
        if (currentIndex < 0)
        {
            SelectOnly(Photos.FirstOrDefault());
            _selectionAnchor = SelectedPhoto;
            return;
        }

        int nextIndex = currentIndex + Math.Sign(direction);
        if (nextIndex < 0 || nextIndex >= Photos.Count)
        {
            return;
        }

        PhotoItem nextPhoto = Photos[nextIndex];
        SelectOnly(nextPhoto);
        _selectionAnchor = nextPhoto;
    }

    private void TogglePhotoSelection(PhotoItem photo)
    {
        List<PhotoItem> selected = SelectedPhotos.ToList();
        if (photo.IsSelected)
        {
            selected.Remove(photo);
        }
        else if (selected.Count < 8)
        {
            selected.Add(photo);
        }

        SetSelectedPhotos(selected.OrderBy(Photos.IndexOf), photo);
    }

    private void SelectRange(PhotoItem anchor, PhotoItem photo, bool addToSelection)
    {
        int anchorIndex = Photos.IndexOf(anchor);
        int photoIndex = Photos.IndexOf(photo);
        if (anchorIndex < 0 || photoIndex < 0)
        {
            SelectOnly(photo);
            return;
        }

        int start = Math.Min(anchorIndex, photoIndex);
        int count = Math.Abs(anchorIndex - photoIndex) + 1;
        IEnumerable<PhotoItem> range = Photos.Skip(start).Take(count);
        IEnumerable<PhotoItem> selected = addToSelection
            ? SelectedPhotos.Concat(range).Distinct().OrderBy(Photos.IndexOf)
            : range;

        SetSelectedPhotos(selected, photo);
    }

    private void SetSelectedPhotos(IEnumerable<PhotoItem> photos, PhotoItem? currentPhoto)
    {
        PhotoItem[] previousSelection = SelectedPhotos.ToArray();
        PhotoItem? previousSelectedPhoto = SelectedPhoto;
        PhotoItem[] selected = photos
            .Where(photo => Photos.Contains(photo))
            .Distinct()
            .Take(8)
            .ToArray();
        if (previousSelectedPhoto is not null && previousSelection.Length == 1)
        {
            StoreRetouchStateForPhoto(previousSelectedPhoto);
        }

        foreach (PhotoItem photo in Photos)
        {
            photo.IsSelected = selected.Contains(photo);
        }

        SelectedPhotos.Clear();
        foreach (PhotoItem photo in selected)
        {
            SelectedPhotos.Add(photo);
        }

        SelectedPhoto = currentPhoto is not null && selected.Contains(currentPhoto)
            ? currentPhoto
            : selected.FirstOrDefault();
        bool selectedPhotoChanged = !ReferenceEquals(previousSelectedPhoto, SelectedPhoto);
        bool selectionChanged = HasSelectionChanged(previousSelection, selected);
        if (selectionChanged)
        {
            ClearRetouchHistory();
        }

        if (selectedPhotoChanged && SelectedPhoto is not null && selected.Length == 1)
        {
            RestoreRetouchControlsForPhotoSelection(SelectedPhoto);
        }

        UpdateCurveHistogram();

        if (selectionChanged)
        {
            ResetSelectedPhotoPreviewTransforms(selected);
            if (selected.Length == 1)
            {
                _ = ApplyPhotoAdjustmentsAsync(showOverlay: false);
            }
            else
            {
                _pendingPreviewAdjustment = false;
                _pendingPreviewAdjustmentShowsOverlay = false;
                ResetPhotosToOriginalPreview(selected);
            }
        }
    }

    private void UpdateCurveHistogram()
    {
        FindRetouchControl("photo_curves")?.SetCurveHistogramSource(SelectedPhoto?.BaseImage);
    }

    private void StoreRetouchStateForPhoto(PhotoItem photo)
    {
        photo.RetouchState = CaptureRetouchState();
    }

    private RetouchAdjustmentState CaptureRetouchState()
    {
        RetouchControl? curveControl = FindRetouchControl("photo_curves");
        CurveChannel curveChannel = curveControl?.CurveChannel ?? CurveChannel.All;
        Dictionary<string, double> controlValues = RetouchSections
            .SelectMany(section => section.Controls)
            .GroupBy(control => control.Id)
            .ToDictionary(group => group.Key, group => group.Last().Value);

        return new RetouchAdjustmentState(
            controlValues,
            curveChannel,
            curveControl?.ExportCurvePointsByChannel() ?? RetouchControl.CreateDefaultCurvePointsByChannel());
    }

    private void RestoreRetouchControlsForPhotoSelection(PhotoItem photo)
    {
        RetouchAdjustmentState? state = photo.RetouchState;
        if (state is null)
        {
            ResetRetouchControlsForPhotoChange();
            return;
        }

        ApplyRetouchStateToControls(state);
    }

    private async Task UndoRetouchAsync()
    {
        if (IsPreviewProcessing || SelectedPhoto is null || SelectedPhotos.Count != 1 || _undoHistory.Count == 0)
        {
            return;
        }

        RetouchHistoryEntry entry = _undoHistory[^1];
        _undoHistory.RemoveAt(_undoHistory.Count - 1);
        _redoHistory.Add(entry);
        await RestoreRetouchHistoryStateAsync(entry.Before);
    }

    private async Task RedoRetouchAsync()
    {
        if (IsPreviewProcessing || SelectedPhoto is null || SelectedPhotos.Count != 1 || _redoHistory.Count == 0)
        {
            return;
        }

        RetouchHistoryEntry entry = _redoHistory[^1];
        _redoHistory.RemoveAt(_redoHistory.Count - 1);
        _undoHistory.Add(entry);
        await RestoreRetouchHistoryStateAsync(entry.After);
    }

    private async Task RestoreRetouchHistoryStateAsync(RetouchAdjustmentState state)
    {
        ApplyRetouchStateToControls(state);
        if (SelectedPhoto is not null)
        {
            SelectedPhoto.RetouchState = CaptureRetouchState();
        }

        await ApplyPhotoAdjustmentsAsync(showOverlay: false);
    }

    private void ApplyRetouchStateToControls(RetouchAdjustmentState state)
    {
        ClearSelectedCurvePoint();
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
        _pendingRetouchSliderLivePreview = false;
        _retouchSliderUndoBeforeState = null;
        _retouchSliderPreviewTimer.Stop();
        _isResettingRetouchControlsForPhotoChange = true;
        try
        {
            foreach (RetouchControl control in RetouchSections.SelectMany(section => section.Controls))
            {
                if (state.ControlValues.TryGetValue(control.Id, out double value))
                {
                    control.Value = value;
                }
                else
                {
                    control.ResetToDefault();
                }
            }

            RetouchControl? curveControl = FindRetouchControl("photo_curves");
            if (curveControl is not null)
            {
                curveControl.RestoreCurveState(state.CurveChannel, state.CurvePointsByChannel);
            }
        }
        finally
        {
            _isResettingRetouchControlsForPhotoChange = false;
        }
    }

    private void PushRetouchHistory(RetouchAdjustmentState? before, RetouchAdjustmentState after)
    {
        if (before is null || AreRetouchStatesEquivalent(before, after))
        {
            return;
        }

        _undoHistory.Add(new RetouchHistoryEntry(before, after));
        if (_undoHistory.Count > RetouchHistoryLimit)
        {
            _undoHistory.RemoveAt(0);
        }

        _redoHistory.Clear();
    }

    private void ClearRetouchHistory()
    {
        _undoHistory.Clear();
        _redoHistory.Clear();
        _curveAmountUndoBeforeState = null;
        _curveDragUndoBeforeState = null;
        _curveKeyboardUndoBeforeState = null;
    }

    private static bool AreRetouchStatesEquivalent(RetouchAdjustmentState left, RetouchAdjustmentState right)
    {
        HashSet<string> controlIds = left.ControlValues.Keys
            .Concat(right.ControlValues.Keys)
            .ToHashSet(StringComparer.Ordinal);
        foreach (string controlId in controlIds)
        {
            left.ControlValues.TryGetValue(controlId, out double leftValue);
            right.ControlValues.TryGetValue(controlId, out double rightValue);
            if (Math.Abs(leftValue - rightValue) >= 0.001)
            {
                return false;
            }
        }

        if (left.CurveChannel != right.CurveChannel)
        {
            return false;
        }

        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            left.CurvePointsByChannel.TryGetValue(channel, out CurvePointState[]? leftPoints);
            right.CurvePointsByChannel.TryGetValue(channel, out CurvePointState[]? rightPoints);
            leftPoints ??= Array.Empty<CurvePointState>();
            rightPoints ??= Array.Empty<CurvePointState>();
            if (leftPoints.Length != rightPoints.Length)
            {
                return false;
            }

            for (int index = 0; index < leftPoints.Length; index++)
            {
                if (Math.Abs(leftPoints[index].Input - rightPoints[index].Input) >= 0.001 ||
                    Math.Abs(leftPoints[index].Output - rightPoints[index].Output) >= 0.001 ||
                    leftPoints[index].IsEndpoint != rightPoints[index].IsEndpoint)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void SetRetouchControlValue(string id, double value)
    {
        RetouchControl? control = FindRetouchControl(id);
        if (control is not null)
        {
            control.Value = value;
        }
    }

    private void ResetRetouchControlsForPhotoChange()
    {
        ClearSelectedCurvePoint();
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
        _pendingRetouchSliderLivePreview = false;
        _retouchSliderUndoBeforeState = null;
        _retouchSliderPreviewTimer.Stop();
        _isResettingRetouchControlsForPhotoChange = true;
        try
        {
            foreach (RetouchControl control in RetouchSections.SelectMany(section => section.Controls))
            {
                control.ResetToDefault();
            }
        }
        finally
        {
            _isResettingRetouchControlsForPhotoChange = false;
        }
    }

    private static bool HasSelectionChanged(PhotoItem[] previousSelection, PhotoItem[] selected)
    {
        return previousSelection.Length != selected.Length ||
               !previousSelection.SequenceEqual(selected);
    }

    private static void ResetSelectedPhotoPreviewTransforms(IEnumerable<PhotoItem> photos)
    {
        foreach (PhotoItem photo in photos)
        {
            photo.PreviewZoomPercent = 100;
            photo.PreviewZoomOrigin = new System.Windows.Point(0.5, 0.5);
            photo.ResetPreviewPan();
        }
    }

    private static void ResetPhotosToOriginalPreview(IEnumerable<PhotoItem> photos)
    {
        foreach (PhotoItem photo in photos)
        {
            photo.ResetAdjustedImage();
        }
    }

    private void RenameSelectedPhoto()
    {
        if (SelectedPhoto is null)
        {
            return;
        }

        string currentNameWithoutExtension = System.IO.Path.GetFileNameWithoutExtension(SelectedPhoto.FileName);
        RenamePhotoWindow renameWindow = new(currentNameWithoutExtension)
        {
            Owner = this
        };

        if (renameWindow.ShowDialog() != true)
        {
            return;
        }

        string newName = renameWindow.PhotoName.Trim();
        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        try
        {
            SelectedPhoto.Rename(newName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "\uD30C\uC77C \uC774\uB984 \uBCC0\uACBD", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void PreviewFrame_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            e.Handled = true;
            return;
        }

        if (SelectedPhotos.Count == 0)
        {
            return;
        }

        bool useWholeSplit = IsSplitPreview && IsControlShiftPressed();
        if (useWholeSplit)
        {
            double delta = e.Delta > 0 ? 10 : -10;
            foreach (PhotoItem photo in SelectedPhotos)
            {
                double maxZoomPercent = GetOneToOneZoomPercent(photo, GetPreviewCellWidth(), GetPreviewCellHeight());
                double nextPhotoZoomPercent = Math.Clamp(photo.PreviewZoomPercent + delta, 25, maxZoomPercent);
                photo.PreviewZoomOrigin = new System.Windows.Point(0.5, 0.5);
                photo.PreviewZoomPercent = nextPhotoZoomPercent;
                if (photo.PreviewZoomPercent <= 100)
                {
                    photo.ResetPreviewPan();
                }
                else
                {
                    ClampPhotoPreviewPanToPreviewCell(photo, photo.PreviewPanX, photo.PreviewPanY);
                }
            }

            e.Handled = true;
            return;
        }

        if (!useWholeSplit)
        {
            PhotoItem? targetPhoto = GetPreviewPhotoFromEvent(e.OriginalSource as DependencyObject, out FrameworkElement? targetElement)
                ?? (SelectedPhotos.Count == 1 ? SelectedPhotos[0] : null);
            if (targetPhoto is null)
            {
                return;
            }

            double maxZoomPercent = GetOneToOneZoomPercent(
                targetPhoto,
                targetElement?.ActualWidth > 0 ? targetElement.ActualWidth : GetPreviewCellWidth(),
                targetElement?.ActualHeight > 0 ? targetElement.ActualHeight : GetPreviewCellHeight());
            double nextPhotoZoomPercent = Math.Clamp(targetPhoto.PreviewZoomPercent + (e.Delta > 0 ? 10 : -10), 25, maxZoomPercent);
            if (nextPhotoZoomPercent > 100 && targetElement is not null && targetElement.ActualWidth > 0 && targetElement.ActualHeight > 0)
            {
                System.Windows.Point pointer = e.GetPosition(targetElement);
                targetPhoto.PreviewZoomOrigin = new System.Windows.Point(
                    Math.Clamp(pointer.X / targetElement.ActualWidth, 0, 1),
                    Math.Clamp(pointer.Y / targetElement.ActualHeight, 0, 1));
            }

            targetPhoto.PreviewZoomPercent = nextPhotoZoomPercent;
            if (targetPhoto.PreviewZoomPercent <= 100)
            {
                targetPhoto.ResetPreviewPan();
            }
            else
            {
                ClampPhotoPreviewPan(targetPhoto, targetElement, targetPhoto.PreviewPanX, targetPhoto.PreviewPanY);
            }

            if (!IsSplitPreview)
            {
                _zoomPercent = targetPhoto.PreviewZoomPercent;
                OnPropertyChanged(nameof(ZoomPercent));
                OnPropertyChanged(nameof(ZoomScale));
            }

            e.Handled = true;
            return;
        }
    }

    private void PreviewFrame_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            e.Handled = true;
            return;
        }

        if (SelectedPhotos.Count == 0)
        {
            return;
        }

        if (IsSplitPreview && e.ClickCount >= 2)
        {
            PhotoItem? clickedPhoto = GetPreviewPhotoFromEvent(e.OriginalSource as DependencyObject, out _);
            if (clickedPhoto is not null)
            {
                SelectOnly(clickedPhoto);
                _selectionAnchor = clickedPhoto;
                e.Handled = true;
                return;
            }
        }

        bool useWholeSplit = IsSplitPreview && IsControlShiftPressed();
        PhotoItem? targetPhoto = useWholeSplit
            ? null
            : GetPreviewPhotoFromEvent(e.OriginalSource as DependencyObject, out _previewPanElement)
                ?? (SelectedPhotos.Count == 1 ? SelectedPhotos[0] : null);

        if (useWholeSplit)
        {
            if (SelectedPhotos.All(photo => photo.PreviewZoomPercent <= 100))
            {
                return;
            }
        }
        else if (targetPhoto is null || targetPhoto.PreviewZoomPercent <= 100)
        {
            return;
        }

        _isPreviewPanning = true;
        _isWholeSplitPanning = useWholeSplit;
        _previewPanPhoto = targetPhoto;
        _previewPanStart = e.GetPosition(this);
        _previewPanStartX = useWholeSplit ? PanX : targetPhoto!.PreviewPanX;
        _previewPanStartY = useWholeSplit ? PanY : targetPhoto!.PreviewPanY;
        _previewPanStartByPhoto.Clear();
        if (useWholeSplit)
        {
            foreach (PhotoItem photo in SelectedPhotos)
            {
                _previewPanStartByPhoto[photo] = (photo.PreviewPanX, photo.PreviewPanY);
            }
        }

        if (sender is IInputElement previewFrame)
        {
            Mouse.Capture(previewFrame);
        }

        e.Handled = true;
    }

    private void PreviewFrame_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isPreviewPanning || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        System.Windows.Point currentPosition = e.GetPosition(this);
        double nextPanX = _previewPanStartX + currentPosition.X - _previewPanStart.X;
        double nextPanY = _previewPanStartY + currentPosition.Y - _previewPanStart.Y;
        if (_isWholeSplitPanning)
        {
            double deltaX = currentPosition.X - _previewPanStart.X;
            double deltaY = currentPosition.Y - _previewPanStart.Y;
            foreach (PhotoItem photo in SelectedPhotos)
            {
                if (!_previewPanStartByPhoto.TryGetValue(photo, out (double X, double Y) startPan))
                {
                    continue;
                }

                ClampPhotoPreviewPanToPreviewCell(photo, startPan.X + deltaX, startPan.Y + deltaY);
            }
        }
        else if (_previewPanPhoto is not null)
        {
            ClampPhotoPreviewPan(_previewPanPhoto, _previewPanElement, nextPanX, nextPanY);
        }

        e.Handled = true;
    }

    private void PreviewFrame_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isPreviewPanning)
        {
            return;
        }

        _isPreviewPanning = false;
        _isWholeSplitPanning = false;
        _previewPanPhoto = null;
        _previewPanElement = null;
        _previewPanStartByPhoto.Clear();
        if (Mouse.Captured == sender)
        {
            Mouse.Capture(null);
        }

        e.Handled = true;
    }

    private void CenterZoomOrigin()
    {
        if (!IsLoaded)
        {
            return;
        }

        System.Windows.Point center = new(0.5, 0.5);
        PreviewZoomOrigin = center;
    }

    private void ResetPreviewPan()
    {
        PanX = 0;
        PanY = 0;
    }

    private void ApplyZoomSliderValueToSelectedPhotos(double zoomPercent)
    {
        if (SelectedPhotos.Count == 0)
        {
            return;
        }

        double cellWidth = IsSplitPreview ? GetPreviewCellWidth() : PreviewSurface.ActualWidth;
        double cellHeight = IsSplitPreview ? GetPreviewCellHeight() : PreviewSurface.ActualHeight;
        foreach (PhotoItem photo in SelectedPhotos)
        {
            double maxZoomPercent = GetOneToOneZoomPercent(photo, cellWidth, cellHeight);
            double nextZoomPercent = Math.Clamp(zoomPercent, 25, maxZoomPercent);
            photo.PreviewZoomOrigin = new System.Windows.Point(0.5, 0.5);
            photo.PreviewZoomPercent = nextZoomPercent;
            if (photo.PreviewZoomPercent <= 100)
            {
                photo.ResetPreviewPan();
            }
            else
            {
                ClampPhotoPreviewPanToPreviewCell(photo, photo.PreviewPanX, photo.PreviewPanY);
            }
        }
    }

    private void QueueZoomSliderRenderApply()
    {
        if (_isZoomSliderRenderApplyQueued)
        {
            return;
        }

        _isZoomSliderRenderApplyQueued = true;
        Dispatcher.BeginInvoke(
            () =>
            {
                _isZoomSliderRenderApplyQueued = false;
                ApplyZoomSliderValueToSelectedPhotos(_zoomPercent);
            },
            System.Windows.Threading.DispatcherPriority.Render);
    }

    private void SyncZoomPercentFromSelectedPhoto()
    {
        PhotoItem? zoomSource = SelectedPhotos.Count == 1
            ? SelectedPhotos[0]
            : SelectedPhoto;
        double nextZoomPercent = zoomSource?.PreviewZoomPercent ?? 100;
        if (Math.Abs(_zoomPercent - nextZoomPercent) < 0.001)
        {
            return;
        }

        _isApplyingZoomSliderValue = true;
        try
        {
            _zoomPercent = nextZoomPercent;
        }
        finally
        {
            _isApplyingZoomSliderValue = false;
        }

        OnPropertyChanged(nameof(ZoomPercent));
        OnPropertyChanged(nameof(ZoomScale));
        OnPreviewTransformPropertiesChanged();
    }

    private void ClampPreviewPan()
    {
        SetClampedPreviewPan(PanX, PanY);
    }

    private void SetClampedPreviewPan(double panX, double panY)
    {
        if (PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0 || ZoomScale <= 1)
        {
            ResetPreviewPan();
            return;
        }

        double maxPanX = PreviewSurface.ActualWidth * (ZoomScale - 1) / 2;
        double maxPanY = PreviewSurface.ActualHeight * (ZoomScale - 1) / 2;
        PanX = Math.Clamp(panX, -maxPanX, maxPanX);
        PanY = Math.Clamp(panY, -maxPanY, maxPanY);
    }

    private static bool IsControlShiftPressed()
    {
        return ShortcutSettings.MatchesMouseGesture(
            Keyboard.Modifiers,
            IsSpacePressed(),
            ShortcutSettings.WholeSplitPreviewGesture);
    }

    private static bool IsSpacePressed()
    {
        return Keyboard.IsKeyDown(Key.Space);
    }

    private static PhotoItem? GetPreviewPhotoFromEvent(DependencyObject? source, out FrameworkElement? previewElement)
    {
        previewElement = null;

        while (source is not null)
        {
            if (source is FrameworkElement frameworkElement && frameworkElement.DataContext is PhotoItem photo)
            {
                previewElement = frameworkElement;
                return photo;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return null;
    }

    private void ClampPhotoPreviewPan(PhotoItem photo, FrameworkElement? previewElement, double panX, double panY)
    {
        double width = previewElement?.ActualWidth > 0 ? previewElement.ActualWidth : PreviewSurface.ActualWidth;
        double height = previewElement?.ActualHeight > 0 ? previewElement.ActualHeight : PreviewSurface.ActualHeight;

        ClampPhotoPreviewPan(photo, width, height, panX, panY);
    }

    private void ClampPhotoPreviewPanToPreviewCell(PhotoItem photo, double panX, double panY)
    {
        ClampPhotoPreviewPan(photo, GetPreviewCellWidth(), GetPreviewCellHeight(), panX, panY);
    }

    private double GetPreviewCellWidth()
    {
        return PreviewColumns > 0 ? PreviewSurface.ActualWidth / PreviewColumns : PreviewSurface.ActualWidth;
    }

    private double GetPreviewCellHeight()
    {
        return PreviewRows > 0 ? PreviewSurface.ActualHeight / PreviewRows : PreviewSurface.ActualHeight;
    }

    private static double GetOneToOneZoomPercent(PhotoItem photo, double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0 ||
            photo.BaseImage.PixelWidth <= 0 || photo.BaseImage.PixelHeight <= 0)
        {
            return 200;
        }

        double dpiX = photo.BaseImage.DpiX > 0 ? photo.BaseImage.DpiX : 96;
        double dpiY = photo.BaseImage.DpiY > 0 ? photo.BaseImage.DpiY : 96;
        double imageWidth = photo.BaseImage.PixelWidth * 96 / dpiX;
        double imageHeight = photo.BaseImage.PixelHeight * 96 / dpiY;
        double fitScale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
        if (fitScale <= 0)
        {
            return 200;
        }

        return Math.Max(100, 100 / fitScale);
    }

    private static void ClampPhotoPreviewPan(PhotoItem photo, double width, double height, double panX, double panY)
    {
        if (width <= 0 || height <= 0 || photo.PreviewZoomScale <= 1)
        {
            photo.ResetPreviewPan();
            return;
        }

        (double displayWidth, double displayHeight) = GetFitImageSize(photo, width, height);
        photo.PreviewPanX = ClampContentPan(
            panX,
            width,
            displayWidth,
            Math.Clamp(photo.PreviewZoomOrigin.X, 0, 1),
            photo.PreviewZoomScale);
        photo.PreviewPanY = ClampContentPan(
            panY,
            height,
            displayHeight,
            Math.Clamp(photo.PreviewZoomOrigin.Y, 0, 1),
            photo.PreviewZoomScale);
    }

    private static double ClampContentPan(double pan, double viewportSize, double contentSize, double origin, double zoomScale)
    {
        const double safetyPixels = 2;
        double contentOffset = (viewportSize - contentSize) / 2;
        double originPosition = viewportSize * origin;
        double scaledStart = originPosition + (contentOffset - originPosition) * zoomScale;
        double scaledEnd = originPosition + (contentOffset + contentSize - originPosition) * zoomScale;
        double scaledSize = scaledEnd - scaledStart;

        if (scaledSize <= viewportSize)
        {
            return 0;
        }

        double usableSafetyPixels = Math.Min(safetyPixels, Math.Max(0, (scaledSize - viewportSize) / 2));
        double minPan = viewportSize + usableSafetyPixels - scaledEnd;
        double maxPan = -scaledStart - usableSafetyPixels;
        if (minPan > maxPan)
        {
            return 0;
        }

        return Math.Clamp(pan, minPan, maxPan);
    }

    private static (double Width, double Height) GetFitImageSize(PhotoItem photo, double viewportWidth, double viewportHeight)
    {
        double dpiX = photo.BaseImage.DpiX > 0 ? photo.BaseImage.DpiX : 96;
        double dpiY = photo.BaseImage.DpiY > 0 ? photo.BaseImage.DpiY : 96;
        double imageWidth = photo.BaseImage.PixelWidth * 96 / dpiX;
        double imageHeight = photo.BaseImage.PixelHeight * 96 / dpiY;
        double fitScale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);

        return (imageWidth * fitScale, imageHeight * fitScale);
    }

    private void ToggleSectionOrderEditMode()
    {
        IsSectionOrderEditMode = !IsSectionOrderEditMode;
        if (!IsSectionOrderEditMode)
        {
            _sectionDragSource = null;
            ClearDropPreview();
            if (Mouse.Captured is not null)
            {
                Mouse.Capture(null);
            }
        }
    }

    private void RetouchSection_Expanded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RetouchSection expandedSection)
        {
            return;
        }

        if (!ReferenceEquals(_activeRetouchSection, expandedSection))
        {
            ClearRetouchHistory();
            _activeRetouchSection = expandedSection;
        }

        foreach (RetouchSection section in RetouchSections)
        {
            if (!ReferenceEquals(section, expandedSection))
            {
                section.IsExpanded = false;
            }
        }
    }

    private void SectionDragHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!IsSectionOrderEditMode)
        {
            return;
        }

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
        if (!IsSectionOrderEditMode)
        {
            return;
        }

        if (Mouse.Captured == sender)
        {
            Mouse.Capture(null);
        }

        _sectionDragSource = null;
        e.Handled = true;
    }

    private void SectionDragHandle_PreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!IsSectionOrderEditMode ||
            _sectionDragSource is null ||
            e.LeftButton != MouseButtonState.Pressed)
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
        if (IsSectionOrderEditMode &&
            e.Data.GetData(typeof(RetouchSection)) is RetouchSection sourceSection &&
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
        if (IsSectionOrderEditMode &&
            e.Data.GetData(typeof(RetouchSection)) is RetouchSection sourceSection &&
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
        if (!_isShowingOriginalPreview)
        {
            _isShowingOriginalPreview = true;
            OnPropertyChanged(nameof(PreviewTitleText));
        }

        OriginalMockPreview.Visibility = Visibility.Visible;
        EditedMockPreview.Visibility = Visibility.Collapsed;
    }

    private void ShowEditedPreview()
    {
        if (_isShowingOriginalPreview)
        {
            _isShowingOriginalPreview = false;
            OnPropertyChanged(nameof(PreviewTitleText));
        }

        OriginalMockPreview.Visibility = Visibility.Collapsed;
        EditedMockPreview.Visibility = Visibility.Visible;
    }

    private void SetPreviewBackgroundColor(string colorText)
    {
        if (!TryParsePreviewBackgroundColor(colorText, out System.Windows.Media.Color color))
        {
            System.Windows.MessageBox.Show(
                this,
                "Use HEX (#202224) or RGB (32,34,36).",
                "Preview background",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        string normalizedColor = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        PreviewBackgroundSettings.BackgroundColor = normalizedColor;
        PreviewBackgroundSettings.Save();
        _previewBackgroundBrush = CreatePreviewBackgroundBrush(normalizedColor);
        OnPropertyChanged(nameof(PreviewBackgroundBrush));
    }

    private static SolidColorBrush CreatePreviewBackgroundBrush(string colorText)
    {
        if (!TryParsePreviewBackgroundColor(colorText, out System.Windows.Media.Color color))
        {
            TryParsePreviewBackgroundColor(PreviewBackgroundSettings.DefaultBackgroundColor, out color);
        }

        SolidColorBrush brush = new(color);
        brush.Freeze();
        return brush;
    }

    private static bool TryParsePreviewBackgroundColor(string colorText, out System.Windows.Media.Color color)
    {
        color = System.Windows.Media.Colors.Transparent;
        string normalizedText = colorText.Trim();
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        if (normalizedText.Contains(','))
        {
            string[] parts = normalizedText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length == 3 &&
                byte.TryParse(parts[0], out byte red) &&
                byte.TryParse(parts[1], out byte green) &&
                byte.TryParse(parts[2], out byte blue))
            {
                color = System.Windows.Media.Color.FromRgb(red, green, blue);
                return true;
            }

            return false;
        }

        if (!normalizedText.StartsWith('#') && normalizedText.Length is 6 or 8)
        {
            normalizedText = $"#{normalizedText}";
        }

        try
        {
            object? converted = System.Windows.Media.ColorConverter.ConvertFromString(normalizedText);
            if (converted is System.Windows.Media.Color convertedColor)
            {
                color = System.Windows.Media.Color.FromRgb(convertedColor.R, convertedColor.G, convertedColor.B);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
