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
    private bool _isSkinToneSamplingMode;
    private bool _isMaskDebugPreviewEnabled;
    private bool _isDummyMaskRetouchPreviewEnabled;
    private bool _isEnsuringSnapshotMask;
    private PhotoItem? _maskDebugPhoto;
    private BitmapSource? _maskDebugPreviousImage;
    private double _dummyMaskStageValue = 1;
    private RetouchProcessReport? _lastRetouchProcessReport;
    private PhotoItem? _lastRetouchOutputPhoto;
    private RetouchStageProcessorOutput? _lastRetouchStageOutput;
    private RetouchBindingReport _lastRetouchBindingReport = RetouchBindingReport.Empty;
    private string _pendingRetouchBindingEventName = "Retouch";
    private string? _pendingRetouchBindingControlId;
    private double? _pendingRetouchBindingControlValue;
    private System.Windows.Media.Color? _manualSkinReferenceColor;
    private bool _isDraggingFaceWorkArea;
    private FaceWorkAreaDragMode _faceWorkAreaDragMode;
    private System.Windows.Point _faceWorkAreaDragStart;
    private FaceWorkArea _faceWorkAreaDragStartArea = FaceWorkArea.Default;
    private RetouchAdjustmentState? _faceWorkAreaDragUndoBeforeState;
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
    private IPreviewEngine _previewEngine = PreviewEngineFactory.Create();
    private readonly SnapshotMaskBuilder _snapshotMaskBuilder = new(new StandardMaskWarpEngine());
    private readonly RetouchStageProcessor _retouchStageProcessor = new();

    public ObservableCollection<PersonOption> People { get; } = CreatePeople();
    public ObservableCollection<PhotoItem> Photos { get; } = new();
    public ObservableCollection<PhotoItem> SelectedPhotos { get; } = new();
    public ObservableCollection<DebugMaskOption> DebugMaskOptions { get; } = CreateDebugMaskOptions();

    private PhotoItem? _selectedPhoto;
    private DebugMaskOption? _selectedDebugMaskOption;
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
    public string MaskDebugButtonText => _isMaskDebugPreviewEnabled ? "마스크 끄기" : "마스크 보기";
    public Visibility DebugMaskPanelVisibility => _isMaskDebugPreviewEnabled ? Visibility.Visible : Visibility.Collapsed;
    public string DebugMaskStatusText => _isMaskDebugPreviewEnabled && SelectedDebugMaskOption is not null
        ? IsRetouchOutputDebugMask(SelectedDebugMaskOption.Id) && !ReferenceEquals(_lastRetouchOutputPhoto, SelectedPhoto)
            ? $"Mask {SelectedDebugMaskOption.Name} / pipeline first"
            : $"Mask {SelectedDebugMaskOption.Name}"
        : "Mask off";
    public string DummyMaskRetouchButtonText => _isDummyMaskRetouchPreviewEnabled ? "피부 보정 끄기" : "피부 보정";
    public string SnapshotMaskStatusText => $"Snapshot {_snapshotMaskBuilder.CreatedCount} / reuse {_snapshotMaskBuilder.CacheHitCount} / disk {_snapshotMaskBuilder.DiskCacheHitCount} {RetouchStageStatusText}";
    public string RetouchBindingStatusText => _lastRetouchBindingReport.ToStatusText();
    public Visibility DeveloperStatusVisibility => _isMaskDebugPreviewEnabled || _isDummyMaskRetouchPreviewEnabled
        ? Visibility.Visible
        : Visibility.Collapsed;

    public double DummyMaskStageValue
    {
        get => _dummyMaskStageValue;
        set
        {
            double nextValue = Math.Clamp(Math.Round(value), 1, 10);
            if (Math.Abs(_dummyMaskStageValue - nextValue) < 0.001)
            {
                return;
            }

            _dummyMaskStageValue = nextValue;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DummyMaskStageText));
            if (_isDummyMaskRetouchPreviewEnabled)
            {
                SetPendingRetouchBindingEvent("StageChanged", "stage", nextValue);
                _ = ApplyDummyMaskRetouchAsync();
            }
        }
    }

    public string DummyMaskStageText => $"Stage {_dummyMaskStageValue:0}";

    public DebugMaskOption? SelectedDebugMaskOption
    {
        get => _selectedDebugMaskOption;
        set
        {
            if (ReferenceEquals(_selectedDebugMaskOption, value))
            {
                return;
            }

            _selectedDebugMaskOption = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DebugMaskStatusText));
            if (_isMaskDebugPreviewEnabled)
            {
                _ = RefreshMaskDebugPreviewAsync(saveDebugImages: false);
            }
        }
    }

    private string RetouchStageStatusText => _lastRetouchProcessReport is null
        ? string.Empty
        : $"req {_lastRetouchProcessReport.RequestedStage} / app {_lastRetouchProcessReport.AppliedStage} / max {_lastRetouchProcessReport.MaxAllowedStage}";

    private void OnDeveloperStatusPropertiesChanged()
    {
        OnPropertyChanged(nameof(SnapshotMaskStatusText));
        OnPropertyChanged(nameof(DebugMaskStatusText));
        OnPropertyChanged(nameof(RetouchBindingStatusText));
        OnPropertyChanged(nameof(DeveloperStatusVisibility));
    }

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

            RestoreMaskDebugPreviousPreview();
            _isMaskDebugPreviewEnabled = false;
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DebugMaskPanelVisibility));
            OnPropertyChanged(nameof(DebugMaskStatusText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
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
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
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
    public Visibility FaceWorkAreaOverlayVisibility =>
        _activeRetouchSection?.Id == "face_shape" &&
        SelectedPhotos.Count == 1 &&
        !_isShowingOriginalPreview
            ? Visibility.Visible
            : Visibility.Collapsed;
    public double FaceWorkAreaOverlayLeft => GetFaceWorkAreaOverlayBounds().Left;
    public double FaceWorkAreaOverlayTop => GetFaceWorkAreaOverlayBounds().Top;
    public double FaceWorkAreaOverlayWidth => GetFaceWorkAreaOverlayBounds().Width;
    public double FaceWorkAreaOverlayHeight => GetFaceWorkAreaOverlayBounds().Height;
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
                new("acne_remove", "\uC5EC\uB4DC\uB984 \uC81C\uAC70", 0, 100, 0),
                new("mole_age_spot_remove", "\uC810/\uAC80\uBC84\uC12F \uC81C\uAC70", 0, 100, 0),
                new("skin_smooth", "\uD53C\uBD80\uACB0 \uC815\uB9AC", 0, 100, 0),
                new("pore_clean", "\uBAA8\uACF5 \uC815\uB9AC", 0, 100, 0),
                new("tone_even", "\uD53C\uBD80\uD1A4 \uBCF4\uC815", 0, 100, 0),
                RetouchControl.CreateAction("skin_sample_tone", "\uD53C\uBD80\uD1A4 \uAE30\uC900", "\uB113\uC740 \uC2A4\uD3EC\uC774\uB4DC"),
                new("skin_mask_range", "\uD53C\uBD80 \uBCF4\uC815 \uBC94\uC704", 0, 100, 45),
                new("skin_texture_protect", "\uD53C\uBD80\uACB0 \uBCF4\uC874", 0, 100, 70)
            }),
        new RetouchSection(
            "wrinkle",
            "\uC8FC\uB984",
            false,
            new RetouchControl[]
            {
                new("wrinkle_global", "\uC804\uCCB4", 0, 100, 0),
                new("wrinkle_under_eye", "\uB208\uBC11", 0, 100, 0),
                new("wrinkle_glabella", "\uBBF8\uAC04", 0, 100, 0),
                new("wrinkle_forehead", "\uC774\uB9C8", 0, 100, 0),
                new("wrinkle_nasolabial", "\uD314\uC790", 0, 100, 0),
                new("wrinkle_mouth_corner", "\uC785\uAC00", 0, 100, 0),
                new("wrinkle_neck", "\uBAA9", 0, 100, 0),
                new("wrinkle_nose_shadow", "\uCF54\uADF8\uB9BC\uC790", 0, 100, 0)
            }),
        new RetouchSection(
            "face_shape",
            "\uC5BC\uAD74\uD615",
            false,
            new RetouchControl[]
            {
                new("oval_face", "\uACC4\uB780\uD615 \uBCF4\uC815", 0, 100, 0),
                new("face_balance", "\uC88C\uC6B0 \uBC38\uB7F0\uC2A4", -100, 100, 0),
                new("cheekbone_soften", "\uAD11\uB300 \uC644\uD654", 0, 100, 0),
                new("jawline_define", "\uD131\uC120 \uC120\uBA85\uB3C4", 0, 100, 0),
                new("chin_length", "\uD131\uB05D \uAE38\uC774", -100, 100, 0),
                new("chin_width", "\uD131\uB05D \uD3ED", -100, 100, 0),
                new("jaw_balance", "\uC5BC\uAD74 \uC88C\uC6B0 \uADE0\uD615", -100, 100, 0),
                new("eye_height_balance", "\uC591\uCABD \uB208 \uB192\uC774", -100, 100, 0),
                new("brow_height_balance", "\uB208\uC379 \uB192\uC774", -100, 100, 0),
                new("nose_center_balance", "\uCF54 \uD718\uC784 \uC911\uC2EC \uC7A1\uAE30", -100, 100, 0),
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
                new("background_color_amount", "\uB2E8\uC77C \uC0C9\uC0C1 \uC801\uC6A9 \uB18D\uB3C4", 0, 100, 0),
                new("background_blend", "\uACBD\uACC4 \uBE14\uB80C\uB529", 0, 100, 20)
            }),
        new RetouchSection(
            "photo_adjust",
            "\uD1A4 \uBCF4\uC815",
            false,
            new RetouchControl[]
            {
                RetouchControl.CreateCurve("photo_curves", "\uCEE4\uBE0C \uC870\uC815"),
                RetouchControl.CreateExposure("photo_brightness", "\uB178\uCD9C"),
                RetouchControl.CreateContrast("photo_contrast", "\uB300\uBE44"),
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
        SelectedDebugMaskOption = DebugMaskOptions.FirstOrDefault();
        Photos.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(PhotoCountText));
            OnPropertyChanged(nameof(PhotoSelectionText));
            OnPropertyChanged(nameof(EmptyPhotoListVisibility));
            OnPropertyChanged(nameof(PhotoListVisibility));
        };
        SelectedPhotos.CollectionChanged += (_, _) =>
        {
            if (_isMaskDebugPreviewEnabled && SelectedPhotos.Count != 1)
            {
                RestoreMaskDebugPreviousPreview();
                _isMaskDebugPreviewEnabled = false;
                OnPropertyChanged(nameof(MaskDebugButtonText));
                OnPropertyChanged(nameof(DebugMaskPanelVisibility));
                OnPropertyChanged(nameof(DebugMaskStatusText));
                OnPropertyChanged(nameof(DeveloperStatusVisibility));
            }

            OnPropertyChanged(nameof(PhotoSelectionText));
            OnPropertyChanged(nameof(PhotoPreviewVisibility));
            OnPropertyChanged(nameof(MockPreviewVisibility));
            OnPropertyChanged(nameof(PreviewTitleVisibility));
            OnPropertyChanged(nameof(FaceWorkAreaOverlayVisibility));
            OnFaceWorkAreaOverlayPropertiesChanged();
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
        OnFaceWorkAreaOverlayPropertiesChanged();
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
        if (_isSkinToneSamplingMode && e.Key == Key.Escape)
        {
            SetSkinToneSamplingMode(false);
            e.Handled = true;
            return;
        }

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
        PreviewEngineMode previousPreviewEngine = PerformanceSettings.PreviewEngine;

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

        if (previousPreviewEngine != PerformanceSettings.PreviewEngine)
        {
            _previewEngine = PreviewEngineFactory.Create();
        }

        _ = ApplyPhotoAdjustmentsAsync();

        if (!string.Equals(previousWorkingFolderPath, WorkingFolderSettings.WorkingFolderPath, StringComparison.OrdinalIgnoreCase))
        {
            LoadWorkingFolderPhotos();
        }
    }

    private async void MaskDebugButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (_isMaskDebugPreviewEnabled)
        {
            _isMaskDebugPreviewEnabled = false;
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DebugMaskPanelVisibility));
            OnPropertyChanged(nameof(DebugMaskStatusText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            RestoreMaskDebugPreviousPreview();
            return;
        }

        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            System.Windows.MessageBox.Show(
                this,
                "마스크 디버그는 사진 한 장을 선택했을 때 볼 수 있어.",
                "마스크 보기",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        _isMaskDebugPreviewEnabled = true;
        OnPropertyChanged(nameof(MaskDebugButtonText));
        OnPropertyChanged(nameof(DebugMaskPanelVisibility));
        OnPropertyChanged(nameof(DebugMaskStatusText));
        OnPropertyChanged(nameof(DeveloperStatusVisibility));
        await RefreshMaskDebugPreviewAsync(saveDebugImages: true);
    }

    private async Task RefreshMaskDebugPreviewAsync(bool saveDebugImages)
    {
        if (!_isMaskDebugPreviewEnabled || IsPreviewProcessing)
        {
            return;
        }

        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            RestoreMaskDebugPreviousPreview();
            _isMaskDebugPreviewEnabled = false;
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DebugMaskPanelVisibility));
            OnPropertyChanged(nameof(DebugMaskStatusText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            return;
        }

        PhotoItem photo = SelectedPhoto;
        _maskDebugPhoto ??= photo;
        _maskDebugPreviousImage ??= photo.Image;
        try
        {
            _showPreviewProcessingOverlay = false;
            IsPreviewProcessing = true;
            FaceSnapshotMaskSet snapshot = await Task.Run(() => _snapshotMaskBuilder.GetOrCreate(photo));
            snapshot = ManualMaskOverrideApplier.Apply(snapshot, photo.ManualMaskOverride);
            OnPropertyChanged(nameof(SnapshotMaskStatusText));
            BitmapSource overlay = await Dispatcher.InvokeAsync(() => CreateSelectedDebugMaskPreview(photo.BaseImage, snapshot));
            photo.SetAdjustedImage(overlay);
            if (saveDebugImages)
            {
                SaveSnapshotDebugMasks(photo, snapshot);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            RestoreMaskDebugPreviousPreview();
            _isMaskDebugPreviewEnabled = false;
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DebugMaskPanelVisibility));
            OnPropertyChanged(nameof(DebugMaskStatusText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "마스크 보기",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewProcessing = false;
            _showPreviewProcessingOverlay = false;
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }
    }

    private BitmapSource CreateSelectedDebugMaskPreview(BitmapSource source, FaceSnapshotMaskSet snapshot)
    {
        string id = SelectedDebugMaskOption?.Id ?? "final";
        FaceMaskSet masks = snapshot.Masks;
        RetouchStageProcessorOutput? output = ReferenceEquals(_lastRetouchOutputPhoto, SelectedPhoto)
            ? _lastRetouchStageOutput
            : null;

        if (output is not null)
        {
            BitmapSource? filterOverlay = id switch
            {
                "blemish_candidate" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.BlemishCandidateMask, 255, 120, 70, 0.72),
                "blemish_applied" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.BlemishMask, 255, 80, 80, 0.72),
                "wrinkle_candidate" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.WrinkleCandidateMask, 150, 120, 255, 0.70),
                "wrinkle_applied" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.WrinkleAppliedMask, 125, 95, 235, 0.74),
                "wrinkle_combined" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.WrinkleMaskSet.CombinedWrinkleMask, 145, 115, 245, 0.70),
                "glabella_wrinkle" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.WrinkleMaskSet.GlabellaWrinkleMask, 190, 110, 255, 0.76),
                "under_eye_wrinkle" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.WrinkleMaskSet.UnderEyeWrinkleMask, 105, 150, 255, 0.74),
                "texture_restore" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.TextureRestoreMask, 90, 220, 210, 0.68),
                "texture_strength" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.TextureRestoreStrengthMap, 70, 210, 230, 0.72),
                "plastic_risk" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.PlasticSkinRiskMap, 255, 180, 40, 0.74),
                "hardprotect_diff" => DebugMaskExporter.CreateMaskOverlayPreview(source, output.HardProtectAfterRestoreDiffMask, 255, 40, 40, 0.88),
                _ => null
            };

            if (filterOverlay is not null)
            {
                return filterOverlay;
            }
        }

        return id switch
        {
            "skin" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.SkinMask, 70, 220, 120, 0.60),
            "hard_protect" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.HardProtectMask, 235, 60, 70, 0.72),
            "soft_protect" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.SoftProtectMask, 255, 210, 50, 0.68),
            "retouch_allow" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.RetouchAllowMask, 50, 210, 90, 0.60),
            "eye" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.EyeMask, 90, 170, 255, 0.76),
            "eyebrow" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.EyebrowMask, 180, 150, 80, 0.76),
            "lip" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.LipMask, 230, 80, 120, 0.76),
            "inner_mouth" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.InnerMouthMask, 130, 40, 70, 0.78),
            "nostril" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.NostrilMask, 255, 120, 40, 0.82),
            "hair" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.HairMask, 90, 105, 120, 0.76),
            "beard" => DebugMaskExporter.CreateMaskOverlayPreview(source, MaskPlane.Union(masks.BeardMask, masks.MustacheMask), 80, 85, 95, 0.78),
            "glasses" => DebugMaskExporter.CreateMaskOverlayPreview(source, masks.GlassesMask, 220, 220, 240, 0.78),
            _ => DebugMaskExporter.CreateFinalOverlayPreview(source, masks)
        };
    }

    private static bool IsRetouchOutputDebugMask(string id)
    {
        return id is "blemish_candidate" or "blemish_applied" or
            "wrinkle_candidate" or "wrinkle_applied" or "wrinkle_combined" or
            "glabella_wrinkle" or "under_eye_wrinkle" or
            "texture_restore" or "texture_strength" or "plastic_risk" or "hardprotect_diff";
    }

    private void RestoreMaskDebugPreviousPreview()
    {
        if (_maskDebugPhoto is not null && _maskDebugPreviousImage is not null)
        {
            _maskDebugPhoto.SetAdjustedImage(_maskDebugPreviousImage);
        }

        _maskDebugPhoto = null;
        _maskDebugPreviousImage = null;
    }

    private async void DummyMaskRetouchButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (_isDummyMaskRetouchPreviewEnabled)
        {
            _isDummyMaskRetouchPreviewEnabled = false;
            OnPropertyChanged(nameof(DummyMaskRetouchButtonText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
            return;
        }

        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            System.Windows.MessageBox.Show(
                this,
                "피부 보정은 사진 한 장을 선택했을 때 확인할 수 있어.",
                "피부 보정",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (_isMaskDebugPreviewEnabled)
        {
            _isMaskDebugPreviewEnabled = false;
            RestoreMaskDebugPreviousPreview();
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DebugMaskPanelVisibility));
            OnPropertyChanged(nameof(DebugMaskStatusText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
        }

        _isDummyMaskRetouchPreviewEnabled = true;
        OnPropertyChanged(nameof(DummyMaskRetouchButtonText));
        OnPropertyChanged(nameof(DeveloperStatusVisibility));
        SetPendingRetouchBindingEvent("PipelineEnabled", null, null);
        await ApplyDummyMaskRetouchAsync();
    }

    private async Task ApplyDummyMaskRetouchAsync()
    {
        if (!_isDummyMaskRetouchPreviewEnabled || SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            return;
        }

        if (IsPreviewProcessing)
        {
            return;
        }

        PhotoItem photo = SelectedPhoto;
        double stage = DummyMaskStageValue;
        int createdBefore = _snapshotMaskBuilder.CreatedCount;
        int cacheBefore = _snapshotMaskBuilder.CacheHitCount;
        string eventName = _pendingRetouchBindingEventName;
        string? changedControlId = _pendingRetouchBindingControlId;
        double? changedControlValue = _pendingRetouchBindingControlValue;
        ClearPendingRetouchBindingEvent();
        try
        {
            _showPreviewProcessingOverlay = false;
            IsPreviewProcessing = true;
            (FaceSnapshotMaskSet snapshot, RetouchStageProcessorOutput output) = await Task.Run(() =>
            {
                FaceSnapshotMaskSet maskSnapshot = _snapshotMaskBuilder.GetOrCreate(photo);
                FaceSnapshotMaskSet effectiveSnapshot = ManualMaskOverrideApplier.Apply(maskSnapshot, photo.ManualMaskOverride);
                RetouchStageProcessorOutput result = _retouchStageProcessor.Process(
                    photo.BaseImage,
                    effectiveSnapshot,
                    CreateRetouchOptions((int)Math.Round(stage)));
                return (effectiveSnapshot, result);
            });

            if (ReferenceEquals(SelectedPhoto, photo) && _isDummyMaskRetouchPreviewEnabled)
            {
                photo.SetAdjustedImage(output.FinalImage);
                _lastRetouchProcessReport = output.Report;
                _lastRetouchOutputPhoto = photo;
                _lastRetouchStageOutput = output;
                UpdateRetouchBindingReport(eventName, changedControlId, changedControlValue, output, createdBefore, cacheBefore);
                SaveRetouchDebugImages(photo, snapshot, output);
            }

            OnPropertyChanged(nameof(SnapshotMaskStatusText));
            OnPropertyChanged(nameof(RetouchBindingStatusText));
            OnPropertyChanged(nameof(DebugMaskStatusText));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            _isDummyMaskRetouchPreviewEnabled = false;
            OnPropertyChanged(nameof(DummyMaskRetouchButtonText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "피부 보정",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewProcessing = false;
            _showPreviewProcessingOverlay = false;
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }
    }

    private void SetPendingRetouchBindingEvent(string eventName, string? controlId, double? value)
    {
        _pendingRetouchBindingEventName = eventName;
        _pendingRetouchBindingControlId = controlId;
        _pendingRetouchBindingControlValue = value;
    }

    private void ClearPendingRetouchBindingEvent()
    {
        _pendingRetouchBindingEventName = "Retouch";
        _pendingRetouchBindingControlId = null;
        _pendingRetouchBindingControlValue = null;
    }

    private void UpdateRetouchBindingReport(
        string eventName,
        string? changedControlId,
        double? changedValue,
        RetouchStageProcessorOutput output,
        int createdBefore,
        int cacheBefore)
    {
        bool analysisReexecuted = _snapshotMaskBuilder.CreatedCount > createdBefore;
        bool cacheReused = _snapshotMaskBuilder.CacheHitCount > cacheBefore || !analysisReexecuted;
        _lastRetouchBindingReport = new RetouchBindingReport(
            eventName,
            changedControlId,
            changedValue,
            output.Report.RequestedStage,
            output.Report.AppliedStage,
            cacheReused,
            analysisReexecuted,
            true,
            output.Report.IsStageLimited);
        System.Diagnostics.Debug.WriteLine(_lastRetouchBindingReport.ToStatusText());
    }

    private async void ReAnalyzeButton_Click(object sender, RoutedEventArgs e)
    {
        if (IsPreviewProcessing)
        {
            return;
        }

        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            System.Windows.MessageBox.Show(
                this,
                "재분석할 사진 1장을 선택해줘.",
                "재분석",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        PhotoItem photo = SelectedPhoto;
        int createdBefore = _snapshotMaskBuilder.CreatedCount;
        int cacheBefore = _snapshotMaskBuilder.CacheHitCount;
        try
        {
            _showPreviewProcessingOverlay = false;
            IsPreviewProcessing = true;
            (FaceSnapshotMaskSet snapshot, RetouchStageProcessorOutput output) = await Task.Run(() =>
            {
                FaceSnapshotMaskSet rebuiltSnapshot = _snapshotMaskBuilder.Rebuild(photo);
                FaceSnapshotMaskSet effectiveSnapshot = ManualMaskOverrideApplier.Apply(rebuiltSnapshot, photo.ManualMaskOverride);
                RetouchStageProcessorOutput result = _retouchStageProcessor.Process(
                    photo.BaseImage,
                    effectiveSnapshot,
                    CreateRetouchOptions((int)Math.Round(DummyMaskStageValue)));
                return (effectiveSnapshot, result);
            });

            if (ReferenceEquals(SelectedPhoto, photo))
            {
                _lastRetouchProcessReport = output.Report;
                _lastRetouchOutputPhoto = photo;
                _lastRetouchStageOutput = output;
                UpdateRetouchBindingReport("ReAnalyze", null, null, output, createdBefore, cacheBefore);
                if (_isMaskDebugPreviewEnabled)
                {
                    BitmapSource overlay = CreateSelectedDebugMaskPreview(photo.BaseImage, snapshot);
                    photo.SetAdjustedImage(overlay);
                    SaveSnapshotDebugMasks(photo, snapshot);
                }
                else
                {
                    _isDummyMaskRetouchPreviewEnabled = true;
                    OnPropertyChanged(nameof(DummyMaskRetouchButtonText));
                    OnPropertyChanged(nameof(DeveloperStatusVisibility));
                    photo.SetAdjustedImage(output.FinalImage);
                    SaveRetouchDebugImages(photo, snapshot, output);
                }

                OnPropertyChanged(nameof(SnapshotMaskStatusText));
                OnPropertyChanged(nameof(RetouchBindingStatusText));
                OnPropertyChanged(nameof(DebugMaskStatusText));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            System.Windows.MessageBox.Show(
                this,
                ex.Message,
                "재분석",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            IsPreviewProcessing = false;
            _showPreviewProcessingOverlay = false;
            OnPropertyChanged(nameof(PreviewProcessingVisibility));
        }
    }

    private async Task EnsureSnapshotMaskForSelectedPhotoAsync(PhotoItem photo)
    {
        if (_isEnsuringSnapshotMask)
        {
            return;
        }

        try
        {
            _isEnsuringSnapshotMask = true;
            await Task.Run(() => _snapshotMaskBuilder.GetOrCreate(photo));
            OnPropertyChanged(nameof(SnapshotMaskStatusText));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            System.Diagnostics.Debug.WriteLine(ex);
        }
        finally
        {
            _isEnsuringSnapshotMask = false;
        }
    }

    private static void SaveSnapshotDebugMasks(PhotoItem photo, FaceSnapshotMaskSet snapshot)
    {
        string? outputDirectory = GetSnapshotDebugDirectory(photo);
        if (outputDirectory is null)
        {
            return;
        }

        DebugMaskExporter.SaveAll(
            photo.BaseImage,
            new PortraitMaskResult(
                snapshot.Analysis,
                snapshot.Masks,
                snapshot.QualityReport,
                snapshot.ParsingMasks,
                snapshot.WarpedStandardMasks,
                snapshot.NostrilDetection),
            outputDirectory);
    }

    private static void SaveRetouchDebugImages(PhotoItem photo, FaceSnapshotMaskSet snapshot, RetouchStageProcessorOutput output)
    {
        string? outputDirectory = GetSnapshotDebugDirectory(photo);
        if (outputDirectory is null)
        {
            return;
        }

        RetouchDebugExporter.SaveAll(photo.BaseImage, snapshot, output, outputDirectory);
    }

    private static string? GetSnapshotDebugDirectory(PhotoItem photo)
    {
        string? directory = Path.GetDirectoryName(photo.Path);
        return string.IsNullOrWhiteSpace(directory)
            ? null
            : Path.Combine(directory, "_mask_debug", Path.GetFileNameWithoutExtension(photo.FileName));
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
        if (_isMaskDebugPreviewEnabled)
        {
            return;
        }

        if (_isResettingRetouchControlsForPhotoChange)
        {
            return;
        }

        if (e.PropertyName is not (nameof(RetouchControl.Value) or nameof(RetouchControl.CurveChannel)) ||
            sender is not RetouchControl control ||
            !IsPhotoAdjustmentControl(control))
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

        if (_isDummyMaskRetouchPreviewEnabled &&
            e.PropertyName == nameof(RetouchControl.Value))
        {
            SetPendingRetouchBindingEvent("SliderChanged", control.Id, control.Value);
        }

        await ApplyPhotoAdjustmentsAsync(showOverlay: control.Id != "photo_curves");
    }

    private async Task ApplyPhotoAdjustmentsAsync(bool showOverlay = true)
    {
        if (_isMaskDebugPreviewEnabled)
        {
            return;
        }

        if (_isDummyMaskRetouchPreviewEnabled)
        {
            await ApplyDummyMaskRetouchAsync();
            return;
        }

        if (IsPreviewProcessing)
        {
            _pendingPreviewAdjustment = true;
            _pendingPreviewAdjustmentShowsOverlay |= showOverlay;
            return;
        }

        PreviewAdjustment adjustment = CapturePreviewAdjustment();
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
                    results[photo] = _previewEngine.Render(previewSource, adjustment);
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

    private System.Windows.Media.Color GetRetouchControlColor(string id, System.Windows.Media.Color fallbackColor)
    {
        RetouchControl? control = FindRetouchControl(id);
        return control?.ColorValue is not null &&
            TryParsePreviewBackgroundColor(control.ColorValue, out System.Windows.Media.Color color)
                ? color
                : fallbackColor;
    }

    private void RetouchActionButton_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not RetouchControl control)
        {
            return;
        }

        if (control.Id == "skin_sample_tone")
        {
            if (SelectedPhoto is null || SelectedPhotos.Count != 1)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "\uD53C\uBD80\uD1A4 \uAE30\uC900\uC744 \uC7A1\uC744 \uC0AC\uC9C4 1\uC7A5\uC744 \uC120\uD0DD\uD574\uC918.",
                    "\uD53C\uBD80\uD1A4 \uAE30\uC900",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            SetSkinToneSamplingMode(true);
            e.Handled = true;
        }
    }

    private void SetSkinToneSamplingMode(bool isEnabled)
    {
        _isSkinToneSamplingMode = isEnabled;
        PreviewSurface.Cursor = isEnabled ? System.Windows.Input.Cursors.Cross : null;
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
        PreviewAdjustment adjustment = CapturePreviewAdjustment();
        if (!_previewEngine.HasEffectiveAdjustment(adjustment))
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
                _previewEngine.Render(photo.BaseImage, adjustment));

            ExportService exportService = new();
            ExportResult exportResult = await Task.Run(() => exportService.Save(new ExportRequest(
                photo.BaseImage,
                adjustedImage,
                photo.Path,
                RequestedStage: 1,
                AppliedStage: 1,
                photo.SnapshotMaskSet?.QualityReport,
                null,
                new ExportOptions(OutputDirectory: Path.GetDirectoryName(savePath), SaveSidecarReport: true),
                Array.Empty<string>())));
            System.Windows.MessageBox.Show(
                this,
                $"\uC800\uC7A5\uD588\uC5B4.\n{exportResult.SavedFilePath}",
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

    private PreviewAdjustment CapturePreviewAdjustment()
    {
        RetouchControl? curveControl = FindRetouchControl("photo_curves");
        CurveChannel curveChannel = curveControl?.CurveChannel ?? CurveChannel.All;
        return new PreviewAdjustment(
            FindRetouchControl("photo_brightness")?.Value ?? 0,
            FindRetouchControl("photo_contrast")?.Value ?? 0,
            FindRetouchControl("photo_saturation")?.Value ?? 0,
            FindRetouchControl("photo_white_balance")?.Value ?? 0,
            FindRetouchControl("photo_blur_sharpen")?.Value ?? 0,
            FindRetouchControl("blemish_remove")?.Value ?? 0,
            FindRetouchControl("acne_remove")?.Value ?? 0,
            FindRetouchControl("mole_age_spot_remove")?.Value ?? 0,
            FindRetouchControl("skin_smooth")?.Value ?? 0,
            FindRetouchControl("pore_clean")?.Value ?? 0,
            FindRetouchControl("tone_even")?.Value ?? 0,
            FindRetouchControl("skin_mask_range")?.Value ?? 45,
            FindRetouchControl("skin_texture_protect")?.Value ?? 70,
            _manualSkinReferenceColor.HasValue,
            _manualSkinReferenceColor ?? System.Windows.Media.Colors.Transparent,
            FindRetouchControl("oval_face")?.Value ?? 0,
            FindRetouchControl("face_balance")?.Value ?? 0,
            FindRetouchControl("cheekbone_soften")?.Value ?? 0,
            FindRetouchControl("jawline_define")?.Value ?? 0,
            FindRetouchControl("chin_length")?.Value ?? 0,
            FindRetouchControl("chin_width")?.Value ?? 0,
            FindRetouchControl("jaw_balance")?.Value ?? 0,
            FindRetouchControl("eye_height_balance")?.Value ?? 0,
            FindRetouchControl("brow_height_balance")?.Value ?? 0,
            FindRetouchControl("nose_center_balance")?.Value ?? 0,
            FindRetouchControl("double_chin")?.Value ?? 0,
            FindRetouchControl("neck_jaw_edge")?.Value ?? 0,
            GetRetouchControlColor("background_color", System.Windows.Media.Color.FromRgb(0x4A, 0x51, 0x57)),
            FindRetouchControl("background_color_amount")?.Value ?? 0,
            SelectedPhoto?.FaceWorkArea ?? FaceWorkArea.Default,
            curveControl?.Value ?? 0,
            curveChannel,
            curveControl?.BuildCurveLookupTable(curveChannel) ?? CurveLookupTables.CreateIdentity());
    }

    private RetouchOptions CreateRetouchOptions(int requestedStage)
    {
        return new RetouchOptions(
            requestedStage,
            Toolset: CaptureRetouchToolset(requestedStage));
    }

    private RetouchToolset CaptureRetouchToolset(int requestedStage)
    {
        StagePreset stagePreset = StagePresetMapper.Map(requestedStage);
        RetouchToolset defaults = RetouchToolset.FromStagePreset(stagePreset);
        RetouchControl? skinSmooth = FindRetouchControl("skin_smooth");
        RetouchControl? skinTextureProtect = FindRetouchControl("skin_texture_protect");
        RetouchControl? poreClean = FindRetouchControl("pore_clean");
        RetouchControl? blemishRemove = FindRetouchControl("blemish_remove");
        RetouchControl? acneRemove = FindRetouchControl("acne_remove");
        RetouchControl? moleAgeSpotRemove = FindRetouchControl("mole_age_spot_remove");
        RetouchControl? toneEven = FindRetouchControl("tone_even");
        RetouchControl? wrinkleGlobal = FindRetouchControl("wrinkle_global");
        RetouchControl? wrinkleUnderEye = FindRetouchControl("wrinkle_under_eye");
        RetouchControl? wrinkleGlabella = FindRetouchControl("wrinkle_glabella");
        RetouchControl? wrinkleForehead = FindRetouchControl("wrinkle_forehead");
        RetouchControl? wrinkleNasolabial = FindRetouchControl("wrinkle_nasolabial");
        RetouchControl? wrinkleMouthCorner = FindRetouchControl("wrinkle_mouth_corner");
        RetouchControl? wrinkleNeck = FindRetouchControl("wrinkle_neck");
        RetouchControl? wrinkleNoseShadow = FindRetouchControl("wrinkle_nose_shadow");

        bool skinOverride = HasUserOverride(skinSmooth) || HasUserOverride(skinTextureProtect);
        bool blemishOverride = HasUserOverride(blemishRemove) || HasUserOverride(acneRemove) || HasUserOverride(moleAgeSpotRemove);
        bool wrinkleOverride = HasUserOverride(wrinkleGlobal) ||
            HasUserOverride(wrinkleUnderEye) ||
            HasUserOverride(wrinkleGlabella) ||
            HasUserOverride(wrinkleForehead) ||
            HasUserOverride(wrinkleNasolabial) ||
            HasUserOverride(wrinkleMouthCorner) ||
            HasUserOverride(wrinkleNeck) ||
            HasUserOverride(wrinkleNoseShadow);
        bool toneOverride = HasUserOverride(toneEven);
        bool textureOverride = HasUserOverride(poreClean) || HasUserOverride(skinTextureProtect);

        SkinSmoothToolset skinSmoothToolset = defaults.SkinSmooth;
        if (skinOverride)
        {
            skinSmoothToolset = skinSmoothToolset with
            {
                EnableSkinSmooth = NormalizeSlider(skinSmooth) > 0,
                GlobalSmoothAmount = NormalizeSlider(skinSmooth),
                DetailPreserveAmount = NormalizeSlider(skinTextureProtect, skinSmoothToolset.DetailPreserveAmount),
                SoftProtectSmoothAmount = Math.Clamp(NormalizeSlider(skinSmooth) * 0.65, 0, 1)
            };
        }

        BlemishToolset blemishToolset = defaults.Blemish;
        if (blemishOverride)
        {
            double globalBlemish = Math.Max(
                NormalizeSlider(blemishRemove),
                Math.Max(NormalizeSlider(acneRemove), NormalizeSlider(moleAgeSpotRemove)));
            blemishToolset = blemishToolset with
            {
                EnableBlemishReduce = globalBlemish > 0,
                GlobalBlemishAmount = globalBlemish,
                SmallSpotAmount = NormalizeSlider(blemishRemove, globalBlemish),
                RedSpotAmount = NormalizeSlider(acneRemove, globalBlemish),
                BrownSpotAmount = NormalizeSlider(moleAgeSpotRemove, globalBlemish),
                PatchySpotAmount = NormalizeSlider(blemishRemove, globalBlemish)
            };
        }

        WrinkleToolset wrinkleToolset = defaults.Wrinkle;
        if (wrinkleOverride)
        {
            double globalWrinkle = NormalizeSlider(wrinkleGlobal);
            wrinkleToolset = wrinkleToolset with
            {
                EnableWrinkleReduce = globalWrinkle > 0 || defaults.Wrinkle.EnableWrinkleReduce,
                GlobalWrinkleAmount = globalWrinkle > 0 ? globalWrinkle : defaults.Wrinkle.GlobalWrinkleAmount,
                UnderEyeWrinkleAmount = NormalizeSlider(wrinkleUnderEye, defaults.Wrinkle.UnderEyeWrinkleAmount),
                GlabellaWrinkleAmount = NormalizeSlider(wrinkleGlabella, defaults.Wrinkle.GlabellaWrinkleAmount),
                ForeheadWrinkleAmount = NormalizeSlider(wrinkleForehead, defaults.Wrinkle.ForeheadWrinkleAmount),
                NasolabialFoldAmount = NormalizeSlider(wrinkleNasolabial, defaults.Wrinkle.NasolabialFoldAmount),
                MouthCornerWrinkleAmount = NormalizeSlider(wrinkleMouthCorner, defaults.Wrinkle.MouthCornerWrinkleAmount),
                NeckWrinkleAmount = NormalizeSlider(wrinkleNeck, defaults.Wrinkle.NeckWrinkleAmount),
                NoseShadowWrinkleAmount = NormalizeSlider(wrinkleNoseShadow, defaults.Wrinkle.NoseShadowWrinkleAmount)
            };
        }

        ToneEvenToolset toneToolset = defaults.ToneEven;
        if (toneOverride)
        {
            double toneAmount = NormalizeSlider(toneEven);
            toneToolset = toneToolset with
            {
                EnableToneEven = toneAmount > 0,
                GlobalToneEvenAmount = toneAmount,
                RednessReduceAmount = toneAmount,
                YellowReduceAmount = toneAmount,
                DullnessReduceAmount = toneAmount,
                PatchyToneReduceAmount = toneAmount
            };
        }

        TextureRestoreToolset textureToolset = defaults.TextureRestore;
        if (textureOverride)
        {
            textureToolset = textureToolset with
            {
                EnableTextureRestore = true,
                GlobalTextureAmount = NormalizeSlider(skinTextureProtect, defaults.TextureRestore.GlobalTextureAmount),
                PoreTextureAmount = NormalizeSlider(poreClean, defaults.TextureRestore.PoreTextureAmount),
                FineDetailAmount = NormalizeSlider(skinTextureProtect, defaults.TextureRestore.FineDetailAmount),
                SkinGrainAmount = NormalizeSlider(skinTextureProtect, defaults.TextureRestore.SkinGrainAmount),
                PlasticSkinGuardEnabled = true
            };
        }

        return defaults with
        {
            SkinSmooth = skinSmoothToolset,
            Blemish = blemishToolset,
            Wrinkle = wrinkleToolset,
            ToneEven = toneToolset,
            TextureRestore = textureToolset,
            UserOverrideFlags = new RetouchUserOverrideFlags(
                skinOverride,
                blemishOverride,
                wrinkleOverride,
                toneOverride,
                textureOverride,
                false)
        };
    }

    private static bool HasUserOverride(RetouchControl? control)
    {
        return control is not null && Math.Abs(control.Value - control.DefaultValue) >= 0.001;
    }

    private static double NormalizeSlider(RetouchControl? control, double fallback = 0)
    {
        if (control is null)
        {
            return Math.Clamp(fallback, 0, 1);
        }

        if (Math.Abs(control.Maximum - control.Minimum) < 0.001)
        {
            return Math.Clamp(fallback, 0, 1);
        }

        return Math.Clamp((control.Value - control.Minimum) / (control.Maximum - control.Minimum), 0, 1);
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
        _faceWorkAreaDragUndoBeforeState = null;
        try
        {
            foreach (RetouchControl control in section.Controls)
            {
                control.ResetToDefault();
            }

            if (section.Id == "face_shape" && SelectedPhoto is not null)
            {
                SelectedPhoto.ResetManualFaceAdjustOverride();
                OnFaceWorkAreaOverlayPropertiesChanged();
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
            "photo_curves" or
            "blemish_remove" or
            "acne_remove" or
            "mole_age_spot_remove" or
            "skin_smooth" or
            "pore_clean" or
            "tone_even" or
            "skin_mask_range" or
            "skin_texture_protect" or
            "wrinkle_global" or
            "wrinkle_under_eye" or
            "wrinkle_glabella" or
            "wrinkle_forehead" or
            "wrinkle_nasolabial" or
            "wrinkle_mouth_corner" or
            "wrinkle_neck" or
            "wrinkle_nose_shadow" or
            "oval_face" or
            "face_balance" or
            "cheekbone_soften" or
            "jawline_define" or
            "chin_length" or
            "chin_width" or
            "jaw_balance" or
            "eye_height_balance" or
            "brow_height_balance" or
            "nose_center_balance" or
            "double_chin" or
            "neck_jaw_edge" or
            "background_color_amount";
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
        if (IsOutsideCurveCanvas(canvas, point))
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
            IsPreviewProcessing)
        {
            return false;
        }

        _ = DeleteSelectedCurvePointAsync();
        return true;
    }

    private static bool IsOutsideCurveCanvas(System.Windows.Controls.Canvas canvas, System.Windows.Point point)
    {
        const double deleteMargin = 10;
        return point.X < -deleteMargin ||
               point.Y < -deleteMargin ||
               point.X > canvas.ActualWidth + deleteMargin ||
               point.Y > canvas.ActualHeight + deleteMargin;
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
        if (_isMaskDebugPreviewEnabled)
        {
            _isMaskDebugPreviewEnabled = false;
            OnPropertyChanged(nameof(MaskDebugButtonText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            previousSelectedPhoto?.ResetAdjustedImage();
        }

        if (_isDummyMaskRetouchPreviewEnabled)
        {
            _isDummyMaskRetouchPreviewEnabled = false;
            OnPropertyChanged(nameof(DummyMaskRetouchButtonText));
            OnPropertyChanged(nameof(DeveloperStatusVisibility));
            previousSelectedPhoto?.ResetAdjustedImage();
        }

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
        ReleaseInactivePhotoMemory(selected);
        bool selectedPhotoChanged = !ReferenceEquals(previousSelectedPhoto, SelectedPhoto);
        bool selectionChanged = HasSelectionChanged(previousSelection, selected);
        if (selectionChanged)
        {
            ClearRetouchHistory();
        }

        if (selectedPhotoChanged && SelectedPhoto is not null && selected.Length == 1)
        {
            RestoreRetouchControlsForPhotoSelection(SelectedPhoto);
            _ = EnsureSnapshotMaskForSelectedPhotoAsync(SelectedPhoto);
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

    private void ReleaseInactivePhotoMemory(IReadOnlyCollection<PhotoItem> selected)
    {
        foreach (PhotoItem photo in Photos)
        {
            if (selected.Contains(photo))
            {
                continue;
            }

            photo.ReleaseInactiveRetouchMemory();
        }

        if (_lastRetouchOutputPhoto is not null && !selected.Contains(_lastRetouchOutputPhoto))
        {
            _lastRetouchOutputPhoto = null;
            _lastRetouchStageOutput = null;
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
            curveControl?.ExportCurvePointsByChannel() ?? RetouchControl.CreateDefaultCurvePointsByChannel(),
            SelectedPhoto?.FaceWorkArea ?? FaceWorkArea.Default,
            _manualSkinReferenceColor.HasValue,
            _manualSkinReferenceColor ?? System.Windows.Media.Colors.Transparent);
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

            if (SelectedPhoto is not null)
            {
                SelectedPhoto.FaceWorkArea = state.FaceWorkArea;
                _manualSkinReferenceColor = state.HasManualSkinReference
                    ? state.ManualSkinReferenceColor
                    : null;
                OnFaceWorkAreaOverlayPropertiesChanged();
            }
            else
            {
                _manualSkinReferenceColor = null;
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
        _faceWorkAreaDragUndoBeforeState = null;
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

        if (!AreFaceWorkAreasEquivalent(left.FaceWorkArea, right.FaceWorkArea))
        {
            return false;
        }

        if (left.HasManualSkinReference != right.HasManualSkinReference)
        {
            return false;
        }

        if (left.HasManualSkinReference && left.ManualSkinReferenceColor != right.ManualSkinReferenceColor)
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

    private static bool AreFaceWorkAreasEquivalent(FaceWorkArea left, FaceWorkArea right)
    {
        return Math.Abs(left.CenterX - right.CenterX) < 0.001 &&
               Math.Abs(left.CenterY - right.CenterY) < 0.001 &&
               Math.Abs(left.Width - right.Width) < 0.001 &&
               Math.Abs(left.Height - right.Height) < 0.001;
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

            _manualSkinReferenceColor = null;
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

    private void PreviewSurface_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        OnFaceWorkAreaOverlayPropertiesChanged();
    }

    private async void PreviewFrame_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsFaceWorkAreaOverlayMouseSource(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (_isDraggingFaceWorkArea)
        {
            e.Handled = true;
            return;
        }

        if (IsPreviewProcessing)
        {
            e.Handled = true;
            return;
        }

        if (SelectedPhotos.Count == 0)
        {
            return;
        }

        if (_isSkinToneSamplingMode)
        {
            await TrySampleManualSkinToneAsync(e);
            e.Handled = true;
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

    private async Task<bool> TrySampleManualSkinToneAsync(MouseButtonEventArgs e)
    {
        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            SetSkinToneSamplingMode(false);
            return false;
        }

        PhotoItem photo = SelectedPhoto;
        FrameworkElement previewElement = GetPreviewPhotoFromEvent(e.OriginalSource as DependencyObject, out FrameworkElement? targetElement) == photo && targetElement is not null
            ? targetElement
            : PreviewSurface;
        System.Windows.Point pointer = e.GetPosition(previewElement);
        if (!TryMapPreviewPointToImagePixel(photo, previewElement, pointer, out int pixelX, out int pixelY))
        {
            return false;
        }

        RetouchAdjustmentState before = CaptureRetouchState();
        _manualSkinReferenceColor = SampleWideAverageSkinColor(photo.BaseImage, pixelX, pixelY);
        SetSkinToneSamplingMode(false);
        RetouchAdjustmentState after = CaptureRetouchState();
        PushRetouchHistory(before, after);
        photo.RetouchState = after;
        await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        return true;
    }

    private static bool TryMapPreviewPointToImagePixel(
        PhotoItem photo,
        FrameworkElement previewElement,
        System.Windows.Point pointer,
        out int pixelX,
        out int pixelY)
    {
        pixelX = 0;
        pixelY = 0;

        double viewportWidth = previewElement.ActualWidth;
        double viewportHeight = previewElement.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0 || photo.BaseImage.PixelWidth <= 0 || photo.BaseImage.PixelHeight <= 0)
        {
            return false;
        }

        double zoomScale = Math.Max(0.001, photo.PreviewZoomScale);
        System.Windows.Point zoomOrigin = photo.PreviewZoomOrigin;
        double originX = zoomOrigin.X * viewportWidth;
        double originY = zoomOrigin.Y * viewportHeight;
        double untransformedX = originX + (pointer.X - photo.PreviewPanX - originX) / zoomScale;
        double untransformedY = originY + (pointer.Y - photo.PreviewPanY - originY) / zoomScale;

        double dpiX = photo.BaseImage.DpiX > 0 ? photo.BaseImage.DpiX : 96;
        double dpiY = photo.BaseImage.DpiY > 0 ? photo.BaseImage.DpiY : 96;
        double imageWidth = photo.BaseImage.PixelWidth * 96 / dpiX;
        double imageHeight = photo.BaseImage.PixelHeight * 96 / dpiY;
        double fitScale = Math.Min(viewportWidth / imageWidth, viewportHeight / imageHeight);
        if (fitScale <= 0)
        {
            return false;
        }

        double drawnWidth = imageWidth * fitScale;
        double drawnHeight = imageHeight * fitScale;
        double offsetX = (viewportWidth - drawnWidth) / 2;
        double offsetY = (viewportHeight - drawnHeight) / 2;
        if (untransformedX < offsetX ||
            untransformedX > offsetX + drawnWidth ||
            untransformedY < offsetY ||
            untransformedY > offsetY + drawnHeight)
        {
            return false;
        }

        double normalizedX = (untransformedX - offsetX) / drawnWidth;
        double normalizedY = (untransformedY - offsetY) / drawnHeight;
        pixelX = Math.Clamp((int)Math.Round(normalizedX * (photo.BaseImage.PixelWidth - 1)), 0, photo.BaseImage.PixelWidth - 1);
        pixelY = Math.Clamp((int)Math.Round(normalizedY * (photo.BaseImage.PixelHeight - 1)), 0, photo.BaseImage.PixelHeight - 1);
        return true;
    }

    private static System.Windows.Media.Color SampleWideAverageSkinColor(BitmapSource source, int centerX, int centerY)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        int radius = Math.Clamp(Math.Min(width, height) / 42, 24, 90);
        int minX = Math.Max(0, centerX - radius);
        int maxX = Math.Min(width - 1, centerX + radius);
        int minY = Math.Max(0, centerY - radius);
        int maxY = Math.Min(height - 1, centerY + radius);
        double redSum = 0;
        double greenSum = 0;
        double blueSum = 0;
        double weightSum = 0;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double distance = Math.Sqrt(dx * dx + dy * dy) / radius;
                if (distance > 1)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                byte alpha = pixels[index + 3];
                if (alpha < 16)
                {
                    continue;
                }

                byte blue = pixels[index];
                byte green = pixels[index + 1];
                byte red = pixels[index + 2];
                double luminance = GetSampleLuminance(red, green, blue);
                double distanceWeight = 1 - SmoothStep(0.74, 1, distance);
                double luminanceWeight = SmoothStep(22, 70, luminance) * (1 - SmoothStep(235, 254, luminance));
                double skinWeight = GetWideSkinSampleWeight(red, green, blue, luminance);
                double weight = distanceWeight * luminanceWeight * (0.35 + skinWeight * 0.65);
                if (weight <= 0)
                {
                    continue;
                }

                redSum += red * weight;
                greenSum += green * weight;
                blueSum += blue * weight;
                weightSum += weight;
            }
        }

        if (weightSum <= 0.001)
        {
            int index = Math.Clamp(centerY, 0, height - 1) * stride + Math.Clamp(centerX, 0, width - 1) * 4;
            return System.Windows.Media.Color.FromRgb(pixels[index + 2], pixels[index + 1], pixels[index]);
        }

        return System.Windows.Media.Color.FromRgb(
            (byte)Math.Clamp((int)Math.Round(redSum / weightSum), 0, 255),
            (byte)Math.Clamp((int)Math.Round(greenSum / weightSum), 0, 255),
            (byte)Math.Clamp((int)Math.Round(blueSum / weightSum), 0, 255));
    }

    private static double GetWideSkinSampleWeight(byte red, byte green, byte blue, double luminance)
    {
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double chroma = max - min;
        double redDominance = red - Math.Max(green, blue);
        double warmBalance = red - blue;
        double greenBalance = green - blue;
        double skinBase = SmoothStep(8, 46, warmBalance) *
            SmoothStep(-28, 18, redDominance) *
            (1 - SmoothStep(86, 156, chroma)) *
            SmoothStep(-18, 38, greenBalance);
        double lumaWeight = SmoothStep(42, 96, luminance) * (1 - SmoothStep(220, 248, luminance));
        return Math.Clamp(Math.Max(0.12, skinBase) * lumaWeight, 0, 1);
    }

    private static double GetSampleLuminance(byte red, byte green, byte blue)
    {
        return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (Math.Abs(edge1 - edge0) < 0.001)
        {
            return value < edge0 ? 0 : 1;
        }

        double t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static bool IsFaceWorkAreaOverlayMouseSource(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is FrameworkElement { Tag: "FaceWorkAreaOverlay" })
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void FaceWorkAreaOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsPreviewProcessing ||
            SelectedPhoto is null ||
            SelectedPhotos.Count != 1 ||
            sender is not FrameworkElement overlay)
        {
            return;
        }

        _isDraggingFaceWorkArea = true;
        _faceWorkAreaDragStart = e.GetPosition(PreviewSurface);
        _faceWorkAreaDragStartArea = SelectedPhoto.FaceWorkArea;
        _faceWorkAreaDragUndoBeforeState = CaptureRetouchState();
        _faceWorkAreaDragMode = GetFaceWorkAreaDragMode(e.GetPosition(overlay), overlay.ActualWidth, overlay.ActualHeight);
        Mouse.Capture(overlay);
        e.Handled = true;
    }

    private void FaceWorkAreaOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_isDraggingFaceWorkArea ||
            e.LeftButton != MouseButtonState.Pressed ||
            SelectedPhoto is null)
        {
            return;
        }

        Rect imageRect = GetSelectedPhotoDisplayRect();
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return;
        }

        System.Windows.Point currentPoint = e.GetPosition(PreviewSurface);
        SelectedPhoto.FaceWorkArea = _faceWorkAreaDragMode == FaceWorkAreaDragMode.Move
            ? MoveFaceWorkArea(_faceWorkAreaDragStartArea, currentPoint, imageRect)
            : ResizeFaceWorkArea(_faceWorkAreaDragStartArea, currentPoint, imageRect, _faceWorkAreaDragMode);
        OnFaceWorkAreaOverlayPropertiesChanged();
        e.Handled = true;
    }

    private async void FaceWorkAreaOverlay_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDraggingFaceWorkArea)
        {
            return;
        }

        _isDraggingFaceWorkArea = false;
        _faceWorkAreaDragMode = FaceWorkAreaDragMode.None;
        PushRetouchHistory(_faceWorkAreaDragUndoBeforeState, CaptureRetouchState());
        if (SelectedPhoto is not null)
        {
            string snapshotKey = SelectedPhoto.SnapshotMaskSet?.CacheKey.StableId ?? string.Empty;
            SelectedPhoto.ApplyManualFaceAdjustOverride(SelectedPhoto.FaceWorkArea, snapshotKey);
        }

        _faceWorkAreaDragUndoBeforeState = null;
        if (Mouse.Captured == sender)
        {
            Mouse.Capture(null);
        }

        e.Handled = true;
        if (HasActiveFaceShapePreviewWarp())
        {
            await ApplyPhotoAdjustmentsAsync(showOverlay: false);
        }
    }

    private bool HasActiveFaceShapePreviewWarp()
    {
        return Math.Abs(FindRetouchControl("oval_face")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("face_balance")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("cheekbone_soften")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("jawline_define")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("chin_length")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("chin_width")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("jaw_balance")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("eye_height_balance")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("brow_height_balance")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("nose_center_balance")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("double_chin")?.Value ?? 0) >= 0.001 ||
               Math.Abs(FindRetouchControl("neck_jaw_edge")?.Value ?? 0) >= 0.001;
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

    private Rect GetFaceWorkAreaOverlayBounds()
    {
        if (SelectedPhoto is null || SelectedPhotos.Count != 1)
        {
            return Rect.Empty;
        }

        Rect imageRect = GetSelectedPhotoDisplayRect();
        if (imageRect.Width <= 0 || imageRect.Height <= 0)
        {
            return Rect.Empty;
        }

        FaceWorkArea faceArea = SelectedPhoto.FaceWorkArea.Clamp();
        double width = imageRect.Width * faceArea.Width;
        double height = imageRect.Height * faceArea.Height;
        double left = imageRect.Left + imageRect.Width * faceArea.CenterX - width / 2;
        double top = imageRect.Top + imageRect.Height * faceArea.CenterY - height / 2;
        return new Rect(left, top, width, height);
    }

    private Rect GetSelectedPhotoDisplayRect()
    {
        if (SelectedPhoto is null || PreviewSurface.ActualWidth <= 0 || PreviewSurface.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        (double fitWidth, double fitHeight) = GetFitImageSize(SelectedPhoto, PreviewSurface.ActualWidth, PreviewSurface.ActualHeight);
        double left = (PreviewSurface.ActualWidth - fitWidth) / 2;
        double top = (PreviewSurface.ActualHeight - fitHeight) / 2;
        double scale = SelectedPhoto.PreviewZoomScale;
        double originX = PreviewSurface.ActualWidth * Math.Clamp(SelectedPhoto.PreviewZoomOrigin.X, 0, 1);
        double originY = PreviewSurface.ActualHeight * Math.Clamp(SelectedPhoto.PreviewZoomOrigin.Y, 0, 1);

        double scaledLeft = originX + (left - originX) * scale + SelectedPhoto.PreviewPanX;
        double scaledTop = originY + (top - originY) * scale + SelectedPhoto.PreviewPanY;
        return new Rect(scaledLeft, scaledTop, fitWidth * scale, fitHeight * scale);
    }

    private void OnFaceWorkAreaOverlayPropertiesChanged()
    {
        OnPropertyChanged(nameof(FaceWorkAreaOverlayLeft));
        OnPropertyChanged(nameof(FaceWorkAreaOverlayTop));
        OnPropertyChanged(nameof(FaceWorkAreaOverlayWidth));
        OnPropertyChanged(nameof(FaceWorkAreaOverlayHeight));
    }

    private static FaceWorkAreaDragMode GetFaceWorkAreaDragMode(System.Windows.Point point, double width, double height)
    {
        const double handleRange = 22;
        bool nearLeft = point.X <= handleRange;
        bool nearRight = point.X >= width - handleRange;
        bool nearTop = point.Y <= handleRange;
        bool nearBottom = point.Y >= height - handleRange;

        if (nearLeft && nearTop)
        {
            return FaceWorkAreaDragMode.ResizeTopLeft;
        }

        if (nearRight && nearTop)
        {
            return FaceWorkAreaDragMode.ResizeTopRight;
        }

        if (nearLeft && nearBottom)
        {
            return FaceWorkAreaDragMode.ResizeBottomLeft;
        }

        if (nearRight && nearBottom)
        {
            return FaceWorkAreaDragMode.ResizeBottomRight;
        }

        return FaceWorkAreaDragMode.Move;
    }

    private FaceWorkArea MoveFaceWorkArea(FaceWorkArea startArea, System.Windows.Point currentPoint, Rect imageRect)
    {
        double deltaX = (currentPoint.X - _faceWorkAreaDragStart.X) / imageRect.Width;
        double deltaY = (currentPoint.Y - _faceWorkAreaDragStart.Y) / imageRect.Height;
        return new FaceWorkArea(
            startArea.CenterX + deltaX,
            startArea.CenterY + deltaY,
            startArea.Width,
            startArea.Height).Clamp();
    }

    private static FaceWorkArea ResizeFaceWorkArea(
        FaceWorkArea startArea,
        System.Windows.Point currentPoint,
        Rect imageRect,
        FaceWorkAreaDragMode mode)
    {
        const double minimumSize = 0.08;
        double left = startArea.CenterX - startArea.Width / 2;
        double right = startArea.CenterX + startArea.Width / 2;
        double top = startArea.CenterY - startArea.Height / 2;
        double bottom = startArea.CenterY + startArea.Height / 2;
        double normalizedX = Math.Clamp((currentPoint.X - imageRect.Left) / imageRect.Width, 0, 1);
        double normalizedY = Math.Clamp((currentPoint.Y - imageRect.Top) / imageRect.Height, 0, 1);

        switch (mode)
        {
            case FaceWorkAreaDragMode.ResizeTopLeft:
                left = Math.Min(normalizedX, right - minimumSize);
                top = Math.Min(normalizedY, bottom - minimumSize);
                break;
            case FaceWorkAreaDragMode.ResizeTopRight:
                right = Math.Max(normalizedX, left + minimumSize);
                top = Math.Min(normalizedY, bottom - minimumSize);
                break;
            case FaceWorkAreaDragMode.ResizeBottomLeft:
                left = Math.Min(normalizedX, right - minimumSize);
                bottom = Math.Max(normalizedY, top + minimumSize);
                break;
            case FaceWorkAreaDragMode.ResizeBottomRight:
                right = Math.Max(normalizedX, left + minimumSize);
                bottom = Math.Max(normalizedY, top + minimumSize);
                break;
        }

        left = Math.Clamp(left, 0, 1 - minimumSize);
        right = Math.Clamp(right, left + minimumSize, 1);
        top = Math.Clamp(top, 0, 1 - minimumSize);
        bottom = Math.Clamp(bottom, top + minimumSize, 1);

        return new FaceWorkArea(
            (left + right) / 2,
            (top + bottom) / 2,
            right - left,
            bottom - top).Clamp();
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

    private void ClampPhotoPreviewPan(PhotoItem photo, double width, double height, double panX, double panY)
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
        if (ReferenceEquals(photo, SelectedPhoto))
        {
            OnFaceWorkAreaOverlayPropertiesChanged();
        }
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
            OnPropertyChanged(nameof(FaceWorkAreaOverlayVisibility));
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
            OnPropertyChanged(nameof(FaceWorkAreaOverlayVisibility));
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
            OnPropertyChanged(nameof(FaceWorkAreaOverlayVisibility));
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

    private static ObservableCollection<DebugMaskOption> CreateDebugMaskOptions()
    {
        return new ObservableCollection<DebugMaskOption>
        {
            new("final", "Final"),
            new("skin", "Skin"),
            new("hard_protect", "HardProtect"),
            new("soft_protect", "SoftProtect"),
            new("retouch_allow", "RetouchAllow"),
            new("eye", "Eye"),
            new("eyebrow", "Eyebrow"),
            new("lip", "Lip"),
            new("inner_mouth", "InnerMouth"),
            new("nostril", "Nostril"),
            new("hair", "Hair"),
            new("beard", "Beard"),
            new("glasses", "Glasses"),
            new("blemish_candidate", "BlemishCandidate"),
            new("blemish_applied", "BlemishApplied"),
            new("wrinkle_candidate", "WrinkleCandidate"),
            new("wrinkle_applied", "WrinkleApplied"),
            new("wrinkle_combined", "WrinkleCombined"),
            new("under_eye_wrinkle", "UnderEyeWrinkle"),
            new("glabella_wrinkle", "GlabellaWrinkle"),
            new("texture_restore", "TextureRestore"),
            new("texture_strength", "TextureStrength"),
            new("plastic_risk", "PlasticRisk"),
            new("hardprotect_diff", "HardProtectDiff")
        };
    }
}

public enum FaceWorkAreaDragMode
{
    None,
    Move,
    ResizeTopLeft,
    ResizeTopRight,
    ResizeBottomLeft,
    ResizeBottomRight
}
