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
    private readonly Dictionary<PhotoItem, (double X, double Y)> _previewPanStartByPhoto = new();
    private PhotoItem? _selectionAnchor;
    private System.Windows.Point _previewZoomOrigin = new(0.5, 0.5);
    private bool _isPreviewProcessing;
    private bool _showPreviewProcessingOverlay;
    private bool _pendingPreviewAdjustment;
    private bool _pendingPreviewAdjustmentShowsOverlay;
    private RetouchControl? _draggingCurveControl;
    private CurvePoint? _draggingCurvePoint;
    private bool _curveDragChanged;
    private bool _pendingCurveKeyboardPreview;
    private bool _isUpdatingCurveAmountFromSlider;
    private bool _pendingCurveAmountLivePreview;
    private bool _isResettingRetouchControlsForPhotoChange;
    private RetouchControl? _selectedCurveControl;
    private CurvePoint? _selectedCurvePoint;
    private readonly System.Windows.Threading.DispatcherTimer _curveAmountPreviewTimer;

    public ObservableCollection<PersonOption> People { get; } = CreatePeople();
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    public ObservableCollection<PhotoItem> SelectedPhotos { get; } = new();

    private PhotoItem? _selectedPhoto;
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
            OnPropertyChanged(nameof(PhotoPreviewVisibility));
            OnPropertyChanged(nameof(MockPreviewVisibility));
            OnPropertyChanged(nameof(PreviewRows));
            OnPropertyChanged(nameof(PreviewColumns));
            OnPreviewTransformPropertiesChanged();
        }
    }

    public string PhotoCountText => Photos.Count == 0 ? "0 photos selected" : $"{Photos.Count} photos selected";
    public Visibility EmptyPhotoListVisibility => Photos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    public Visibility PhotoListVisibility => Photos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility PhotoPreviewVisibility => SelectedPhotos.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
    public Visibility MockPreviewVisibility => SelectedPhotos.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
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

            _zoomPercent = value;
            if (_zoomPercent <= 100)
            {
                CenterZoomOrigin();
                _isPreviewPanning = false;
                if (Mouse.Captured is not null)
                {
                    Mouse.Capture(null);
                }

                ResetPreviewPan();
            }
            else
            {
                ClampPreviewPan();
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
        RestoreSectionOrder();
        SubscribeRetouchControlChanges();
        InitializeComponent();
        DataContext = this;
        Photos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PhotoCountText));
            OnPropertyChanged(nameof(EmptyPhotoListVisibility));
            OnPropertyChanged(nameof(PhotoListVisibility));
        };
        SelectedPhotos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PhotoPreviewVisibility));
            OnPropertyChanged(nameof(MockPreviewVisibility));
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

        if (ShortcutSettings.Matches(e, ShortcutSettings.RenamePhotoShortcut))
        {
            RenameSelectedPhoto();
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

    private static bool IsTextEditingElementFocused()
    {
        return Keyboard.FocusedElement is System.Windows.Controls.TextBox;
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

        if (_selectedCurveControl.NudgeSelectedCurvePoint(inputDelta, outputDelta))
        {
            _pendingCurveKeyboardPreview = true;
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
            ".jpg" or ".jpeg" => new JpegBitmapEncoder { QualityLevel = 95 },
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
            bool isAddSelectionPressed = ShortcutSettings.HasModifiers(modifiers, ShortcutSettings.AddSelectionModifiers);
            bool isRangeSelectionPressed = ShortcutSettings.HasModifiers(modifiers, ShortcutSettings.RangeSelectionModifiers);

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
            e.Handled = true;
        }
    }

    private void ValueSlider_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        e.Handled = true;
    }

    private void ValueSlider_CommitValue(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (sender is not System.Windows.Controls.Slider slider)
        {
            return;
        }

        slider.GetBindingExpression(System.Windows.Controls.Primitives.RangeBase.ValueProperty)?.UpdateSource();
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
        comboBox.GetBindingExpression(System.Windows.Controls.Primitives.Selector.SelectedValueProperty)?.UpdateSource();
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

        bool changed = textBox.Tag switch
        {
            "Input" => control.SetSelectedCurvePointInput(value),
            "Output" => control.SetSelectedCurvePointOutput(value),
            _ => false
        };

        RefreshCurvePointValueTextBox(textBox);
        if (changed)
        {
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

        if (control.ResetCurrentCurveChannel())
        {
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

        ClearSelectedCurvePoint();
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
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
        CurvePoint? curvePoint = control.AddCurvePointFromCanvas(point.X, point.Y);
        if (curvePoint is null)
        {
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
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
        else if (shouldRenderPreview)
        {
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }

        e.Handled = true;
    }

    private async Task DeleteSelectedCurvePointAsync()
    {
        if (_selectedCurveControl is null || _selectedCurvePoint is null)
        {
            return;
        }

        if (_selectedCurveControl.DeleteCurvePoint(_selectedCurvePoint))
        {
            ClearSelectedCurvePoint();
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
        if (selectedPhotoChanged && SelectedPhoto is not null && selected.Length == 1)
        {
            RestoreRetouchControlsForPhotoSelection(SelectedPhoto);
        }

        UpdateCurveHistogram();

        if (HasSelectionChanged(previousSelection, selected))
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

        ClearSelectedCurvePoint();
        _pendingCurveAmountLivePreview = false;
        _curveAmountPreviewTimer.Stop();
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
        return ShortcutSettings.HasModifiers(Keyboard.Modifiers, ShortcutSettings.WholeSplitPreviewModifiers);
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

    private void RetouchSection_Expanded(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RetouchSection expandedSection)
        {
            return;
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
    }

    private void ShowEditedPreview()
    {
        OriginalMockPreview.Visibility = Visibility.Collapsed;
        EditedMockPreview.Visibility = Visibility.Visible;
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

public sealed class PhotoItem : INotifyPropertyChanged
{
    private string _path;
    private string _fileName;
    private BitmapSource _baseImage;
    private BitmapSource _image;
    private BitmapSource _thumbnail;
    private BitmapSource? _neutralPreviewImage;
    private readonly Dictionary<int, BitmapSource> _effectPreviewCache = new();
    private bool _isSelected;
    private DateTime _sourceLastWriteTimeUtc;
    private long _sourceLength;
    private double _previewZoomPercent = 100;
    private double _previewPanX;
    private double _previewPanY;
    private System.Windows.Point _previewZoomOrigin = new(0.5, 0.5);

    private PhotoItem(string path, BitmapSource image, BitmapSource thumbnail, DateTime sourceLastWriteTimeUtc, long sourceLength)
    {
        _path = path;
        _baseImage = image;
        _image = image;
        _thumbnail = thumbnail;
        _fileName = System.IO.Path.GetFileName(path);
        _sourceLastWriteTimeUtc = sourceLastWriteTimeUtc;
        _sourceLength = sourceLength;
        DisplayInfo = $"{image.PixelWidth} x {image.PixelHeight}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Path
    {
        get => _path;
        private set
        {
            if (_path == value)
            {
                return;
            }

            _path = value;
            OnPropertyChanged();
        }
    }

    public string FileName
    {
        get => _fileName;
        private set
        {
            if (_fileName == value)
            {
                return;
            }

            _fileName = value;
            OnPropertyChanged();
        }
    }

    public string DisplayInfo { get; }
    public BitmapSource BaseImage => _baseImage;
    public RetouchAdjustmentState? RetouchState { get; set; }
    public double PreviewZoomPercent
    {
        get => _previewZoomPercent;
        set
        {
            if (Math.Abs(_previewZoomPercent - value) < 0.001)
            {
                return;
            }

            _previewZoomPercent = value;
            if (_previewZoomPercent <= 100)
            {
                PreviewZoomOrigin = new System.Windows.Point(0.5, 0.5);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(PreviewZoomScale));
        }
    }

    public double PreviewZoomScale => PreviewZoomPercent / 100;
    public double PreviewPanX
    {
        get => _previewPanX;
        set
        {
            if (Math.Abs(_previewPanX - value) < 0.001)
            {
                return;
            }

            _previewPanX = value;
            OnPropertyChanged();
        }
    }

    public double PreviewPanY
    {
        get => _previewPanY;
        set
        {
            if (Math.Abs(_previewPanY - value) < 0.001)
            {
                return;
            }

            _previewPanY = value;
            OnPropertyChanged();
        }
    }

    public System.Windows.Point PreviewZoomOrigin
    {
        get => _previewZoomOrigin;
        set
        {
            if (_previewZoomOrigin == value)
            {
                return;
            }

            _previewZoomOrigin = value;
            OnPropertyChanged();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public BitmapSource Image
    {
        get => _image;
        private set
        {
            _image = value;
            OnPropertyChanged();
        }
    }

    public BitmapSource Thumbnail
    {
        get => _thumbnail;
        private set
        {
            _thumbnail = value;
            OnPropertyChanged();
        }
    }

    public static PhotoItem Load(string path)
    {
        BitmapSource image = LoadBitmap(path, null);
        BitmapSource thumbnail = LoadBitmap(path, 96);
        (DateTime lastWriteTimeUtc, long length) = GetFileVersion(path);
        return new PhotoItem(path, image, thumbnail, lastWriteTimeUtc, length);
    }

    public void ReloadImage()
    {
        _baseImage = LoadBitmap(Path, null);
        _effectPreviewCache.Clear();
        _neutralPreviewImage = null;
        Image = _baseImage;
        Thumbnail = LoadBitmap(Path, 96);
        UpdateFileVersion(Path);
    }

    public BitmapSource GetEffectPreviewSource(int? visibleMaxLongSide)
    {
        int cacheKey = CreateEffectPreviewCacheKey(visibleMaxLongSide);
        if (!_effectPreviewCache.TryGetValue(cacheKey, out BitmapSource? previewSource))
        {
            previewSource = PhotoAdjustmentEngine.CreateEffectPreviewSource(BaseImage, visibleMaxLongSide);
            _effectPreviewCache[cacheKey] = previewSource;
        }

        _neutralPreviewImage = previewSource;
        return previewSource;
    }

    public bool MatchesFileVersion(string path)
    {
        try
        {
            (DateTime lastWriteTimeUtc, long length) = GetFileVersion(path);
            return lastWriteTimeUtc == _sourceLastWriteTimeUtc && length == _sourceLength;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void SetAdjustedImage(BitmapSource image)
    {
        Image = image;
    }

    public void ResetAdjustedImage()
    {
        Image = BaseImage;
    }

    public void ResetRetouchWorkState()
    {
        RetouchState = null;
        Image = _neutralPreviewImage ?? GetEffectPreviewSource(null);
    }

    public void Rename(string newName)
    {
        string? directory = System.IO.Path.GetDirectoryName(Path);
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new IOException("\uD30C\uC77C \uACBD\uB85C\uB97C \uD655\uC778\uD560 \uC218 \uC5C6\uC2B5\uB2C8\uB2E4.");
        }

        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        if (newName.IndexOfAny(invalidChars) >= 0)
        {
            throw new ArgumentException("\uD30C\uC77C \uC774\uB984\uC5D0 \uC0AC\uC6A9\uD560 \uC218 \uC5C6\uB294 \uBB38\uC790\uAC00 \uC788\uC2B5\uB2C8\uB2E4.");
        }

        string extension = System.IO.Path.GetExtension(newName);
        string finalName = string.IsNullOrWhiteSpace(extension)
            ? newName + System.IO.Path.GetExtension(Path)
            : newName;
        string newPath = System.IO.Path.Combine(directory, finalName);

        if (string.Equals(Path, newPath, StringComparison.OrdinalIgnoreCase))
        {
            FileName = System.IO.Path.GetFileName(newPath);
            return;
        }

        if (File.Exists(newPath))
        {
            throw new IOException("\uAC19\uC740 \uC774\uB984\uC758 \uD30C\uC77C\uC774 \uC774\uBBF8 \uC788\uC2B5\uB2C8\uB2E4.");
        }

        File.Move(Path, newPath);
        Path = newPath;
        FileName = System.IO.Path.GetFileName(newPath);
        UpdateFileVersion(newPath);
    }

    public void ResetPreviewPan()
    {
        PreviewPanX = 0;
        PreviewPanY = 0;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private void UpdateFileVersion(string path)
    {
        (_sourceLastWriteTimeUtc, _sourceLength) = GetFileVersion(path);
    }

    private static (DateTime LastWriteTimeUtc, long Length) GetFileVersion(string path)
    {
        FileInfo fileInfo = new(path);
        if (!fileInfo.Exists)
        {
            throw new IOException("\uD30C\uC77C\uC744 \uCC3E\uC744 \uC218 \uC5C6\uC5B4.");
        }

        return (fileInfo.LastWriteTimeUtc, fileInfo.Length);
    }

    private static int CreateEffectPreviewCacheKey(int? visibleMaxLongSide)
    {
        return HashCode.Combine(
            visibleMaxLongSide ?? -1,
            PreviewSettings.UseOriginalSize,
            PreviewSettings.MaxLongSidePixels,
            PreviewSettings.MaximumMaxLongSidePixels);
    }

    private static BitmapSource LoadBitmap(string path, int? decodePixelWidth)
    {
        if (ColorManagementSettings.Mode == ColorManagementMode.Disabled)
        {
            return LoadFallbackBitmap(path, decodePixelWidth);
        }

        try
        {
            return LoadColorManagedBitmap(path, decodePixelWidth);
        }
        catch (NotSupportedException)
        {
            return LoadFallbackBitmap(path, decodePixelWidth);
        }
        catch (FileFormatException)
        {
            return LoadFallbackBitmap(path, decodePixelWidth);
        }
    }

    private static BitmapSource LoadColorManagedBitmap(string path, int? decodePixelWidth)
    {
        BitmapDecoder decoder = BitmapDecoder.Create(
            new Uri(path, UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreImageCache,
            BitmapCacheOption.OnLoad);

        BitmapFrame frame = decoder.Frames[0];
        BitmapSource source = frame;
        ColorContext destinationContext = CreateDestinationColorContext();
        ColorContext? sourceContext = frame.ColorContexts?.FirstOrDefault();

        if (sourceContext is not null)
        {
            source = new ColorConvertedBitmap(source, sourceContext, destinationContext, PixelFormats.Pbgra32);
        }
        else if (source.Format != PixelFormats.Pbgra32)
        {
            source = new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        }

        if (decodePixelWidth is not null && source.PixelWidth > decodePixelWidth.Value)
        {
            double scale = (double)decodePixelWidth.Value / source.PixelWidth;
            source = new TransformedBitmap(source, new ScaleTransform(scale, scale));
        }

        source.Freeze();
        return source;
    }

    private static ColorContext CreateDestinationColorContext()
    {
        if (ColorManagementSettings.Mode == ColorManagementMode.Manual &&
            !string.IsNullOrWhiteSpace(ColorManagementSettings.ManualDisplayProfilePath) &&
            File.Exists(ColorManagementSettings.ManualDisplayProfilePath))
        {
            try
            {
                return new ColorContext(new Uri(ColorManagementSettings.ManualDisplayProfilePath, UriKind.Absolute));
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
            {
            }
        }

        return new ColorContext(PixelFormats.Bgra32);
    }

    private static BitmapImage LoadFallbackBitmap(string path, int? decodePixelWidth)
    {
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        if (decodePixelWidth is not null)
        {
            bitmap.DecodePixelWidth = decodePixelWidth.Value;
        }

        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}

public enum CurveChannel
{
    All,
    Red,
    Green,
    Blue
}

public sealed record RetouchAdjustmentState(
    Dictionary<string, double> ControlValues,
    CurveChannel CurveChannel,
    Dictionary<CurveChannel, CurvePointState[]> CurvePointsByChannel);

public sealed record CurvePointState(double Input, double Output, bool IsEndpoint);

public sealed class RetouchControl : INotifyPropertyChanged
{
    private const double CurveCanvasSize = 180;
    private const int MaxCurvePoints = 7;
    private const int CurveHistogramSampleLongSide = 512;
    private double _value;
    private readonly double _defaultValue;
    private CurveChannel _curveChannel = CurveChannel.All;
    private CurvePoint? _selectedCurvePoint;
    private PointCollection _curveHistogramPoints = new();
    private Dictionary<CurveChannel, PointCollection> _curveHistogramPointsByChannel = CreateEmptyCurveHistogramPointsByChannel();
    private readonly Dictionary<CurveChannel, ObservableCollection<CurvePoint>> _curvePointsByChannel = new();

    public RetouchControl(string id, string label, double minimum, double maximum, double value)
    {
        Id = id;
        Label = label;
        Minimum = minimum;
        Maximum = maximum;
        _defaultValue = value;
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
    public bool IsCurveEditor { get; }
    public string? ColorValue { get; }
    public string? ActionText { get; }
    public IReadOnlyList<BackgroundOption> BackgroundOptions { get; } = Array.Empty<BackgroundOption>();
    public ObservableCollection<CurvePoint> CurvePoints => GetCurvePoints(CurveChannel);
    public CurvePoint? SelectedCurvePoint
    {
        get => _selectedCurvePoint;
        set
        {
            if (ReferenceEquals(_selectedCurvePoint, value))
            {
                return;
            }

            if (_selectedCurvePoint is not null)
            {
                _selectedCurvePoint.PropertyChanged -= SelectedCurvePoint_PropertyChanged;
            }

            _selectedCurvePoint = value;
            if (_selectedCurvePoint is not null)
            {
                _selectedCurvePoint.PropertyChanged += SelectedCurvePoint_PropertyChanged;
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedCurveInputText));
            OnPropertyChanged(nameof(SelectedCurveOutputText));
            OnPropertyChanged(nameof(HasSelectedCurvePoint));
            OnPropertyChanged(nameof(IsSelectedCurveInputEditable));
        }
    }

    public bool HasSelectedCurvePoint => SelectedCurvePoint is not null;
    public bool IsSelectedCurveInputEditable => SelectedCurvePoint is { IsEndpoint: false };
    public string SelectedCurveInputText => SelectedCurvePoint is null ? "-" : SelectedCurvePoint.Input.ToString("0");
    public string SelectedCurveOutputText => SelectedCurvePoint is null ? "-" : SelectedCurvePoint.Output.ToString("0");
    public PointCollection CurveHistogramPoints
    {
        get => _curveHistogramPoints;
        private set
        {
            _curveHistogramPoints = value;
            OnPropertyChanged();
        }
    }

    public PointCollection CurvePolylinePoints
    {
        get
        {
            PointCollection points = new();
            byte[] lut = BuildCurveLookupTable(CurveChannel);
            for (int input = 0; input < lut.Length; input++)
            {
                points.Add(new System.Windows.Point(input / 255d * CurveCanvasSize, (255 - lut[input]) / 255d * CurveCanvasSize));
            }

            return points;
        }
    }

    public CurveChannel CurveChannel
    {
        get => _curveChannel;
        set
        {
            if (_curveChannel == value)
            {
                return;
            }

            _curveChannel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CurvePoints));
            OnPropertyChanged(nameof(CurvePolylinePoints));
            OnPropertyChanged(nameof(CurveStrengthLabel));
            RefreshCurveHistogramPoints();
        }
    }

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
    public string CurveStrengthLabel => $"\uCEE4\uBE0C \uC801\uC6A9\uB7C9 ({CurveChannelDisplayName})";
    private string CurveChannelDisplayName => CurveChannel switch
    {
        CurveChannel.Red => "R",
        CurveChannel.Green => "G",
        CurveChannel.Blue => "B",
        _ => "\uC804\uCCB4"
    };

    public void ResetToDefault()
    {
        Value = _defaultValue;
        if (IsCurveEditor)
        {
            ResetAllCurveChannels();
        }
    }

    private void SelectedCurvePoint_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CurvePoint.Input) or nameof(CurvePoint.Output))
        {
            OnPropertyChanged(nameof(SelectedCurveInputText));
            OnPropertyChanged(nameof(SelectedCurveOutputText));
        }
    }

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

    public static RetouchControl CreateCurve(string id, string label)
    {
        RetouchControl control = new(id, label, 0, 100, 100, isCurveEditor: true);
        control.InitializeCurvePoints();
        return control;
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

    private RetouchControl(string id, string label, double minimum, double maximum, double value, bool isCurveEditor)
        : this(id, label, minimum, maximum, value)
    {
        IsCurveEditor = isCurveEditor;
    }

    public void SetCurveHistogramSource(BitmapSource? source)
    {
        _curveHistogramPointsByChannel = source is null
            ? CreateEmptyCurveHistogramPointsByChannel()
            : CreateCurveHistogramPointsByChannel(source);
        RefreshCurveHistogramPoints();
    }

    private void RefreshCurveHistogramPoints()
    {
        CurveHistogramPoints = _curveHistogramPointsByChannel.TryGetValue(CurveChannel, out PointCollection? points)
            ? points
            : new PointCollection();
    }

    private static Dictionary<CurveChannel, PointCollection> CreateEmptyCurveHistogramPointsByChannel()
    {
        return Enum.GetValues<CurveChannel>()
            .ToDictionary(channel => channel, _ => new PointCollection());
    }

    private static Dictionary<CurveChannel, PointCollection> CreateCurveHistogramPointsByChannel(BitmapSource source)
    {
        BitmapSource sample = CreateCurveHistogramSample(source);
        BitmapSource bitmap = sample.Format == PixelFormats.Bgra32
            ? sample
            : new FormatConvertedBitmap(sample, PixelFormats.Bgra32, null, 0);

        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        int[] allBins = new int[256];
        int[] redBins = new int[256];
        int[] greenBins = new int[256];
        int[] blueBins = new int[256];
        for (int index = 0; index < pixels.Length; index += 4)
        {
            if (pixels[index + 3] == 0)
            {
                continue;
            }

            int blue = pixels[index];
            int green = pixels[index + 1];
            int red = pixels[index + 2];
            int luminance = Math.Clamp((int)Math.Round(red * 0.2126 + green * 0.7152 + blue * 0.0722), 0, 255);
            allBins[luminance]++;
            redBins[red]++;
            greenBins[green]++;
            blueBins[blue]++;
        }

        return new Dictionary<CurveChannel, PointCollection>
        {
            [CurveChannel.All] = CreateCurveHistogramPoints(allBins),
            [CurveChannel.Red] = CreateCurveHistogramPoints(redBins),
            [CurveChannel.Green] = CreateCurveHistogramPoints(greenBins),
            [CurveChannel.Blue] = CreateCurveHistogramPoints(blueBins)
        };
    }

    private static PointCollection CreateCurveHistogramPoints(int[] bins)
    {
        int maximum = bins.Max();
        if (maximum == 0)
        {
            return new PointCollection();
        }

        double logMaximum = Math.Log(maximum + 1);
        PointCollection points = new()
        {
            new System.Windows.Point(0, CurveCanvasSize)
        };

        for (int index = 0; index < bins.Length; index++)
        {
            double x = index / 255d * CurveCanvasSize;
            double normalized = Math.Log(bins[index] + 1) / logMaximum;
            double y = CurveCanvasSize - normalized * (CurveCanvasSize * 0.88);
            points.Add(new System.Windows.Point(x, y));
        }

        points.Add(new System.Windows.Point(CurveCanvasSize, CurveCanvasSize));
        return points;
    }

    private static BitmapSource CreateCurveHistogramSample(BitmapSource source)
    {
        int longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longestSide <= CurveHistogramSampleLongSide)
        {
            return source;
        }

        double scale = (double)CurveHistogramSampleLongSide / longestSide;
        TransformedBitmap sample = new(source, new ScaleTransform(scale, scale));
        sample.Freeze();
        return sample;
    }

    public CurvePoint? AddCurvePointFromCanvas(double canvasX, double canvasY)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (points.Count >= MaxCurvePoints || !IsNearCurveLine(canvasX, canvasY))
        {
            return null;
        }

        double input = Math.Clamp(canvasX / CurveCanvasSize * 255, 1, 254);
        if (points.Any(point => Math.Abs(point.Input - input) <= 2))
        {
            return null;
        }

        double output = InterpolateCurveOutput(CurveChannel, input);
        CurvePoint point = new(input, output, isEndpoint: false);
        points.Add(point);
        SortCurvePoints(points);
        NotifyCurveChanged();
        return point;
    }

    private bool IsNearCurveLine(double canvasX, double canvasY)
    {
        const double curveHitTolerance = 14;
        if (canvasX < 0 || canvasX > CurveCanvasSize || canvasY < 0 || canvasY > CurveCanvasSize)
        {
            return false;
        }

        double input = CanvasXToInput(canvasX);
        double output = InterpolateCurveOutput(CurveChannel, input);
        double curveY = (255 - output) / 255d * CurveCanvasSize;
        return Math.Abs(canvasY - curveY) <= curveHitTolerance;
    }

    public void MoveCurvePoint(CurvePoint point, double canvasX, double canvasY)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(point))
        {
            return;
        }

        if (point.IsEndpoint)
        {
            point.Output = CanvasYToOutput(canvasY);
        }
        else
        {
            (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, point);
            point.Input = Math.Clamp(CanvasXToInput(canvasX), minimumInput, maximumInput);
            point.Output = CanvasYToOutput(canvasY);
        }

        SortCurvePoints(points);
        NotifyCurveChanged();
    }

    public bool SetSelectedCurvePointInput(double input)
    {
        if (SelectedCurvePoint is null || SelectedCurvePoint.IsEndpoint)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, SelectedCurvePoint);
        double clampedInput = Math.Clamp(input, minimumInput, maximumInput);
        if (Math.Abs(SelectedCurvePoint.Input - clampedInput) < 0.001)
        {
            return false;
        }

        SelectedCurvePoint.Input = clampedInput;
        SortCurvePoints(points);
        NotifyCurveChanged();
        return true;
    }

    public bool SetSelectedCurvePointOutput(double output)
    {
        if (SelectedCurvePoint is null)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        double clampedOutput = Math.Clamp(output, 0, 255);
        if (Math.Abs(SelectedCurvePoint.Output - clampedOutput) < 0.001)
        {
            return false;
        }

        SelectedCurvePoint.Output = clampedOutput;
        NotifyCurveChanged();
        return true;
    }

    public bool NudgeSelectedCurvePoint(double inputDelta, double outputDelta)
    {
        if (SelectedCurvePoint is null)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        if (!points.Contains(SelectedCurvePoint))
        {
            return false;
        }

        double nextInput = SelectedCurvePoint.Input;
        bool inputChanged = false;
        if (!SelectedCurvePoint.IsEndpoint && Math.Abs(inputDelta) > 0.001)
        {
            (double minimumInput, double maximumInput) = GetInputBoundsForPoint(points, SelectedCurvePoint);
            nextInput = Math.Clamp(SelectedCurvePoint.Input + inputDelta, minimumInput, maximumInput);
            inputChanged = Math.Abs(SelectedCurvePoint.Input - nextInput) >= 0.001;
        }

        double nextOutput = Math.Clamp(SelectedCurvePoint.Output + outputDelta, 0, 255);
        bool outputChanged = Math.Abs(SelectedCurvePoint.Output - nextOutput) >= 0.001;
        if (!inputChanged && !outputChanged)
        {
            return false;
        }

        SelectedCurvePoint.Input = nextInput;
        SelectedCurvePoint.Output = nextOutput;
        if (inputChanged)
        {
            SortCurvePoints(points);
        }

        NotifyCurveChanged();
        return true;
    }

    public bool ResetCurrentCurveChannel()
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        bool isAlreadyReset =
            points.Count == 2 &&
            points.Any(point => point.IsEndpoint && Math.Abs(point.Input) < 0.001 && Math.Abs(point.Output) < 0.001) &&
            points.Any(point => point.IsEndpoint && Math.Abs(point.Input - 255) < 0.001 && Math.Abs(point.Output - 255) < 0.001);

        if (isAlreadyReset)
        {
            return false;
        }

        SelectedCurvePoint = null;
        points.Clear();
        points.Add(new CurvePoint(0, 0, isEndpoint: true));
        points.Add(new CurvePoint(255, 255, isEndpoint: true));
        NotifyCurveChanged();
        return true;
    }

    public static Dictionary<CurveChannel, CurvePointState[]> CreateDefaultCurvePointsByChannel()
    {
        return Enum.GetValues<CurveChannel>()
            .ToDictionary(
                channel => channel,
                _ => new[]
                {
                    new CurvePointState(0, 0, true),
                    new CurvePointState(255, 255, true)
                });
    }

    public Dictionary<CurveChannel, CurvePointState[]> ExportCurvePointsByChannel()
    {
        Dictionary<CurveChannel, CurvePointState[]> result = new();
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            result[channel] = GetCurvePoints(channel)
                .OrderBy(point => point.Input)
                .Select(point => new CurvePointState(point.Input, point.Output, point.IsEndpoint))
                .ToArray();
        }

        return result;
    }

    public void RestoreCurveState(
        CurveChannel channel,
        IReadOnlyDictionary<CurveChannel, CurvePointState[]> curvePointsByChannel)
    {
        SelectedCurvePoint = null;
        foreach (CurveChannel curveChannel in Enum.GetValues<CurveChannel>())
        {
            ObservableCollection<CurvePoint> points = GetCurvePoints(curveChannel);
            points.Clear();

            CurvePointState[] states = curvePointsByChannel.TryGetValue(curveChannel, out CurvePointState[]? savedStates)
                ? savedStates
                : CreateDefaultCurvePointsByChannel()[curveChannel];

            foreach (CurvePointState state in NormalizeCurvePointStates(states))
            {
                points.Add(new CurvePoint(state.Input, state.Output, state.IsEndpoint));
            }

            SortCurvePoints(points);
        }

        CurveChannel = Enum.IsDefined(channel) ? channel : CurveChannel.All;
        NotifyCurveChanged();
    }

    private void ResetAllCurveChannels()
    {
        SelectedCurvePoint = null;
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            ObservableCollection<CurvePoint> points = GetCurvePoints(channel);
            points.Clear();
            points.Add(new CurvePoint(0, 0, isEndpoint: true));
            points.Add(new CurvePoint(255, 255, isEndpoint: true));
        }

        CurveChannel = CurveChannel.All;
        NotifyCurveChanged();
    }

    private static CurvePointState[] NormalizeCurvePointStates(IEnumerable<CurvePointState>? states)
    {
        CurvePointState[] saved = states?.ToArray() ?? Array.Empty<CurvePointState>();
        CurvePointState blackPoint = saved
            .Where(point => point.IsEndpoint)
            .OrderBy(point => Math.Abs(point.Input))
            .FirstOrDefault() ?? new CurvePointState(0, 0, true);
        CurvePointState whitePoint = saved
            .Where(point => point.IsEndpoint)
            .OrderBy(point => Math.Abs(point.Input - 255))
            .FirstOrDefault() ?? new CurvePointState(255, 255, true);

        IEnumerable<CurvePointState> middlePoints = saved
            .Where(point => !point.IsEndpoint)
            .Select(point => new CurvePointState(
                Math.Clamp(point.Input, 1, 254),
                Math.Clamp(point.Output, 0, 255),
                false))
            .GroupBy(point => Math.Round(point.Input))
            .Select(group => group.Last())
            .OrderBy(point => point.Input)
            .Take(MaxCurvePoints - 2);

        return new[]
            {
                new CurvePointState(0, Math.Clamp(blackPoint.Output, 0, 255), true)
            }
            .Concat(middlePoints)
            .Concat(new[]
            {
                new CurvePointState(255, Math.Clamp(whitePoint.Output, 0, 255), true)
            })
            .ToArray();
    }

    private static (double Minimum, double Maximum) GetInputBoundsForPoint(ObservableCollection<CurvePoint> points, CurvePoint point)
    {
        CurvePoint[] orderedPoints = points.OrderBy(item => item.Input).ToArray();
        int pointIndex = Array.IndexOf(orderedPoints, point);
        double previousInput = pointIndex > 0 ? orderedPoints[pointIndex - 1].Input : 0;
        double nextInput = pointIndex >= 0 && pointIndex < orderedPoints.Length - 1 ? orderedPoints[pointIndex + 1].Input : 255;
        double minimumInput = Math.Min(254, previousInput + 1);
        double maximumInput = Math.Max(1, nextInput - 1);
        return (minimumInput, maximumInput);
    }

    public void MarkCurvePointForDeletion(CurvePoint point)
    {
        if (!point.IsEndpoint)
        {
            point.IsPendingDelete = true;
        }
    }

    public bool DeleteCurvePointIfMarked(CurvePoint point)
    {
        return point.IsPendingDelete && DeleteCurvePoint(point);
    }

    public bool DeleteCurvePoint(CurvePoint point)
    {
        if (point.IsEndpoint)
        {
            return false;
        }

        ObservableCollection<CurvePoint> points = GetCurvePoints(CurveChannel);
        bool removed = points.Remove(point);
        if (removed)
        {
            NotifyCurveChanged();
        }

        return removed;
    }

    public byte[] BuildCurveLookupTable(CurveChannel channel)
    {
        ObservableCollection<CurvePoint> points = GetCurvePoints(channel);
        CurvePoint[] orderedPoints = points.OrderBy(point => point.Input).ToArray();
        byte[] lut = new byte[256];
        if (orderedPoints.Length < 2)
        {
            for (int input = 0; input < lut.Length; input++)
            {
                lut[input] = (byte)input;
            }

            return lut;
        }

        double[] tangents = CalculateCurveTangents(orderedPoints);
        int segmentIndex = 0;

        for (int input = 0; input < lut.Length; input++)
        {
            while (segmentIndex < orderedPoints.Length - 2 && input > orderedPoints[segmentIndex + 1].Input)
            {
                segmentIndex++;
            }

            CurvePoint left = orderedPoints[segmentIndex];
            CurvePoint right = orderedPoints[segmentIndex + 1];
            double output = EvaluateCubicHermite(
                left.Input,
                right.Input,
                left.Output,
                right.Output,
                tangents[segmentIndex],
                tangents[segmentIndex + 1],
                input);
            lut[input] = (byte)Math.Clamp((int)Math.Round(output), 0, 255);
        }

        return lut;
    }

    private static double[] CalculateCurveTangents(CurvePoint[] points)
    {
        double[] tangents = new double[points.Length];
        if (points.Length < 2)
        {
            return tangents;
        }

        if (points.Length == 2)
        {
            double slope = SegmentSlope(points[0], points[1]);
            tangents[0] = slope;
            tangents[1] = slope;
            return tangents;
        }

        double[] widths = new double[points.Length - 1];
        double[] slopes = new double[points.Length - 1];
        for (int index = 0; index < widths.Length; index++)
        {
            widths[index] = Math.Max(0.001, points[index + 1].Input - points[index].Input);
            slopes[index] = (points[index + 1].Output - points[index].Output) / widths[index];
        }

        tangents[0] = CalculateEndpointTangent(widths[0], widths[1], slopes[0], slopes[1]);
        tangents[^1] = CalculateEndpointTangent(widths[^1], widths[^2], slopes[^1], slopes[^2]);

        for (int index = 1; index < points.Length - 1; index++)
        {
            double previousSlope = slopes[index - 1];
            double nextSlope = slopes[index];
            if (Math.Abs(previousSlope) < 0.0001 ||
                Math.Abs(nextSlope) < 0.0001 ||
                Math.Sign(previousSlope) != Math.Sign(nextSlope))
            {
                tangents[index] = 0;
                continue;
            }

            double previousWidth = widths[index - 1];
            double nextWidth = widths[index];
            double weightA = 2 * nextWidth + previousWidth;
            double weightB = nextWidth + 2 * previousWidth;
            tangents[index] = (weightA + weightB) / (weightA / previousSlope + weightB / nextSlope);
        }

        return tangents;
    }

    private static double SegmentSlope(CurvePoint left, CurvePoint right)
    {
        return (right.Output - left.Output) / Math.Max(0.001, right.Input - left.Input);
    }

    private static double CalculateEndpointTangent(double width, double nextWidth, double slope, double nextSlope)
    {
        double tangent = ((2 * width + nextWidth) * slope - width * nextSlope) / (width + nextWidth);
        if (Math.Sign(tangent) != Math.Sign(slope))
        {
            return 0;
        }

        if (Math.Sign(slope) != Math.Sign(nextSlope) && Math.Abs(tangent) > Math.Abs(3 * slope))
        {
            return 3 * slope;
        }

        return tangent;
    }

    private static double EvaluateCubicHermite(
        double leftInput,
        double rightInput,
        double leftOutput,
        double rightOutput,
        double leftTangent,
        double rightTangent,
        double input)
    {
        double width = Math.Max(0.001, rightInput - leftInput);
        double t = Math.Clamp((input - leftInput) / width, 0, 1);
        double t2 = t * t;
        double t3 = t2 * t;

        double leftBlend = 2 * t3 - 3 * t2 + 1;
        double leftTangentBlend = t3 - 2 * t2 + t;
        double rightBlend = -2 * t3 + 3 * t2;
        double rightTangentBlend = t3 - t2;

        return leftBlend * leftOutput +
               leftTangentBlend * width * leftTangent +
               rightBlend * rightOutput +
               rightTangentBlend * width * rightTangent;
    }

    private void InitializeCurvePoints()
    {
        foreach (CurveChannel channel in Enum.GetValues<CurveChannel>())
        {
            _curvePointsByChannel[channel] = new ObservableCollection<CurvePoint>
            {
                new(0, 0, isEndpoint: true),
                new(255, 255, isEndpoint: true)
            };
        }
    }

    private ObservableCollection<CurvePoint> GetCurvePoints(CurveChannel channel)
    {
        if (!_curvePointsByChannel.TryGetValue(channel, out ObservableCollection<CurvePoint>? points))
        {
            points = new ObservableCollection<CurvePoint>
            {
                new(0, 0, isEndpoint: true),
                new(255, 255, isEndpoint: true)
            };
            _curvePointsByChannel[channel] = points;
        }

        return points;
    }

    private double InterpolateCurveOutput(CurveChannel channel, double input)
    {
        byte[] lut = BuildCurveLookupTable(channel);
        return lut[(int)Math.Clamp(Math.Round(input), 0, 255)];
    }

    private static double CanvasXToInput(double canvasX)
    {
        return Math.Clamp(canvasX / CurveCanvasSize * 255, 0, 255);
    }

    private static double CanvasYToOutput(double canvasY)
    {
        return Math.Clamp(255 - canvasY / CurveCanvasSize * 255, 0, 255);
    }

    private void SortCurvePoints(ObservableCollection<CurvePoint> points)
    {
        CurvePoint[] orderedPoints = points.OrderBy(point => point.Input).ToArray();
        for (int index = 0; index < orderedPoints.Length; index++)
        {
            int currentIndex = points.IndexOf(orderedPoints[index]);
            if (currentIndex != index)
            {
                points.Move(currentIndex, index);
            }
        }
    }

    private void NotifyCurveChanged()
    {
        OnPropertyChanged(nameof(CurvePoints));
        OnPropertyChanged(nameof(CurvePolylinePoints));
        OnPropertyChanged(nameof(CurveChannel));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class CurvePoint : INotifyPropertyChanged
{
    private double _input;
    private double _output;
    private bool _isSelected;
    private bool _isPendingDelete;

    public CurvePoint(double input, double output, bool isEndpoint)
    {
        _input = input;
        _output = output;
        IsEndpoint = isEndpoint;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public bool IsEndpoint { get; }
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
            {
                return;
            }

            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsPendingDelete
    {
        get => _isPendingDelete;
        set
        {
            if (_isPendingDelete == value)
            {
                return;
            }

            _isPendingDelete = value;
            OnPropertyChanged();
        }
    }

    public double Input
    {
        get => _input;
        set
        {
            if (Math.Abs(_input - value) < 0.001)
            {
                return;
            }

            _input = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasLeft));
        }
    }

    public double Output
    {
        get => _output;
        set
        {
            if (Math.Abs(_output - value) < 0.001)
            {
                return;
            }

            _output = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CanvasTop));
        }
    }

    public double CanvasLeft => Input / 255d * 180 - 4;
    public double CanvasTop => (255 - Output) / 255d * 180 - 4;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public static class PhotoAdjustmentEngine
{
    private const double AdjustmentEpsilon = 0.001;

    public static byte[] CreateIdentityLookupTable()
    {
        byte[] lookup = new byte[256];
        for (int index = 0; index < lookup.Length; index++)
        {
            lookup[index] = (byte)index;
        }

        return lookup;
    }

    public static bool HasEffectiveAdjustment(
        double brightness,
        double contrast,
        double saturation,
        double whiteBalance,
        double blurSharpen,
        double curveAmount,
        byte[] curveLookup)
    {
        return Math.Abs(brightness) >= AdjustmentEpsilon ||
               Math.Abs(contrast) >= AdjustmentEpsilon ||
               Math.Abs(saturation) >= AdjustmentEpsilon ||
               Math.Abs(whiteBalance) >= AdjustmentEpsilon ||
               Math.Abs(blurSharpen) >= AdjustmentEpsilon ||
               (Math.Abs(curveAmount) >= AdjustmentEpsilon && !IsIdentityLookupTable(curveLookup));
    }

    public static bool IsIdentityLookupTable(byte[] lookup)
    {
        if (lookup.Length != 256)
        {
            return false;
        }

        for (int index = 0; index < lookup.Length; index++)
        {
            if (lookup[index] != index)
            {
                return false;
            }
        }

        return true;
    }

    public static BitmapSource CreateEffectPreviewSource(BitmapSource source, int? visibleMaxLongSide = null)
    {
        if (PreviewSettings.UseOriginalSize && visibleMaxLongSide is null)
        {
            return source;
        }

        int longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        int settingMaxLongSide = PreviewSettings.UseOriginalSize
            ? PreviewSettings.MaximumMaxLongSidePixels
            : Math.Clamp(
                PreviewSettings.MaxLongSidePixels,
                PreviewSettings.MinimumMaxLongSidePixels,
                PreviewSettings.MaximumMaxLongSidePixels);
        int maxLongSide = visibleMaxLongSide is null
            ? settingMaxLongSide
            : Math.Min(
                settingMaxLongSide,
                Math.Clamp(visibleMaxLongSide.Value, 320, PreviewSettings.MaximumMaxLongSidePixels));
        if (longestSide <= maxLongSide)
        {
            return source;
        }

        double scale = (double)maxLongSide / longestSide;
        TransformedBitmap preview = new(source, new ScaleTransform(scale, scale));
        preview.Freeze();
        return preview;
    }

    public static BitmapSource ApplyBasicTone(
        BitmapSource source,
        double brightness,
        double contrast,
        double saturation,
        double whiteBalance,
        double blurSharpen,
        double curveAmount,
        CurveChannel curveChannel,
        byte[] curveLookup)
    {
        if (!HasEffectiveAdjustment(brightness, contrast, saturation, whiteBalance, blurSharpen, curveAmount, curveLookup))
        {
            return source;
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        double brightnessOffset = brightness * 2.55;
        double contrastFactor = GetContrastFactor(contrast);
        double saturationFactor = GetSaturationFactor(saturation);
        (double redGain, double greenGain, double blueGain) = GetWhiteBalanceGains(whiteBalance);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            double blue = ApplyToneValue(pixels[index], brightnessOffset, contrastFactor);
            double green = ApplyToneValue(pixels[index + 1], brightnessOffset, contrastFactor);
            double red = ApplyToneValue(pixels[index + 2], brightnessOffset, contrastFactor);
            (red, green, blue) = ApplySaturation(red, green, blue, saturationFactor);
            red *= redGain;
            green *= greenGain;
            blue *= blueGain;
            (red, green, blue) = ApplyCurve(red, green, blue, curveAmount, curveChannel, curveLookup);

            pixels[index] = ClampToByte(blue);
            pixels[index + 1] = ClampToByte(green);
            pixels[index + 2] = ClampToByte(red);
        }

        if (Math.Abs(blurSharpen) >= 0.001)
        {
            pixels = ApplyBlurSharpen(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, blurSharpen);
        }

        BitmapSource adjusted = BitmapSource.Create(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bitmap.DpiX,
            bitmap.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        adjusted.Freeze();
        return adjusted;
    }

    private static double GetContrastFactor(double contrast)
    {
        double normalized = Math.Clamp(contrast, -100, 100);
        return normalized >= 0
            ? 1 + normalized / 50
            : 1 + normalized / 100;
    }

    private static double GetSaturationFactor(double saturation)
    {
        double normalized = Math.Clamp(saturation, -100, 100);
        return normalized >= 0
            ? 1 + normalized / 100
            : 1 + normalized / 100;
    }

    private static (double Red, double Green, double Blue) GetWhiteBalanceGains(double whiteBalance)
    {
        double normalized = Math.Clamp(whiteBalance, -100, 100) / 100;
        double redGain = 1 + normalized * 0.18;
        double blueGain = 1 - normalized * 0.18;
        double greenGain = 1 - Math.Abs(normalized) * 0.025;
        return (redGain, greenGain, blueGain);
    }

    private static double ApplyToneValue(byte value, double brightnessOffset, double contrastFactor)
    {
        return ((value + brightnessOffset) - 128) * contrastFactor + 128;
    }

    private static (double Red, double Green, double Blue) ApplySaturation(double red, double green, double blue, double saturationFactor)
    {
        double luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722;
        return (
            luminance + (red - luminance) * saturationFactor,
            luminance + (green - luminance) * saturationFactor,
            luminance + (blue - luminance) * saturationFactor);
    }

    private static (double Red, double Green, double Blue) ApplyCurve(
        double red,
        double green,
        double blue,
        double curveAmount,
        CurveChannel curveChannel,
        byte[] curveLookup)
    {
        if (Math.Abs(curveAmount) < 0.001)
        {
            return (red, green, blue);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Red)
        {
            red = ApplyCurveChannel(red, curveAmount, curveLookup);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Green)
        {
            green = ApplyCurveChannel(green, curveAmount, curveLookup);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Blue)
        {
            blue = ApplyCurveChannel(blue, curveAmount, curveLookup);
        }

        return (red, green, blue);
    }

    private static double ApplyCurveChannel(double value, double curveAmount, byte[] curveLookup)
    {
        int index = Math.Clamp((int)Math.Round(value), 0, 255);
        double amount = Math.Clamp(Math.Abs(curveAmount), 0, 100) / 100;
        double curvedValue = curveLookup[Math.Clamp(index, 0, curveLookup.Length - 1)];
        return value + (curvedValue - value) * amount;
    }

    private static byte[] ApplyBlurSharpen(byte[] source, int width, int height, int stride, double blurSharpen)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(Math.Abs(blurSharpen), 0, 100) / 100;

        if (blurSharpen < 0)
        {
            for (int index = 0; index < source.Length; index += 4)
            {
                result[index] = BlendChannel(source[index], blurred[index], amount);
                result[index + 1] = BlendChannel(source[index + 1], blurred[index + 1], amount);
                result[index + 2] = BlendChannel(source[index + 2], blurred[index + 2], amount);
                result[index + 3] = source[index + 3];
            }

            return result;
        }

        double sharpenAmount = amount * 1.4;
        for (int index = 0; index < source.Length; index += 4)
        {
            result[index] = ClampToByte(source[index] + (source[index] - blurred[index]) * sharpenAmount);
            result[index + 1] = ClampToByte(source[index + 1] + (source[index + 1] - blurred[index + 1]) * sharpenAmount);
            result[index + 2] = ClampToByte(source[index + 2] + (source[index + 2] - blurred[index + 2]) * sharpenAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] CreateSoftBlur(byte[] source, int width, int height, int stride)
    {
        byte[] result = new byte[source.Length];
        int[] kernel =
        {
            1, 2, 1,
            2, 4, 2,
            1, 2, 1
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int blue = 0;
                int green = 0;
                int red = 0;
                int weightSum = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    int sampleY = Math.Clamp(y + ky, 0, height - 1);
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sampleX = Math.Clamp(x + kx, 0, width - 1);
                        int weight = kernel[(ky + 1) * 3 + (kx + 1)];
                        int sampleIndex = sampleY * stride + sampleX * 4;
                        blue += source[sampleIndex] * weight;
                        green += source[sampleIndex + 1] * weight;
                        red += source[sampleIndex + 2] * weight;
                        weightSum += weight;
                    }
                }

                int targetIndex = y * stride + x * 4;
                result[targetIndex] = (byte)(blue / weightSum);
                result[targetIndex + 1] = (byte)(green / weightSum);
                result[targetIndex + 2] = (byte)(red / weightSum);
                result[targetIndex + 3] = source[targetIndex + 3];
            }
        }

        return result;
    }

    private static byte BlendChannel(byte source, byte target, double amount)
    {
        return ClampToByte(source + (target - source) * amount);
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
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
