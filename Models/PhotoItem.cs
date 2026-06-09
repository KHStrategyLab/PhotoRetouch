using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public sealed class PhotoItem : INotifyPropertyChanged
{
    private string _path;
    private string _fileName;
    private BitmapSource _baseImage;
    private BitmapSource _image;
    private BitmapSource _thumbnail;
    private FaceSnapshotMaskSet? _snapshotMaskSet;
    private AverageFaceColorMaskPreviewCache? _averageFaceColorMaskPreviewCache;
    private BitmapSource? _neutralPreviewImage;
    private readonly Dictionary<int, BitmapSource> _effectPreviewCache = new();
    private bool _isSelected;
    private DateTime _sourceLastWriteTimeUtc;
    private long _sourceLength;
    private double _previewZoomPercent = 100;
    private double _previewPanX;
    private double _previewPanY;
    private System.Windows.Point _previewZoomOrigin = new(0.5, 0.5);
    private FaceWorkArea _faceWorkArea = FaceWorkArea.Default;

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
    public AverageFaceColorMaskPreviewCache? AverageFaceColorMaskPreviewCache => _averageFaceColorMaskPreviewCache;
    public FaceSnapshotMaskSet? SnapshotMaskSet
    {
        get => _snapshotMaskSet;
        set
        {
            if (ReferenceEquals(_snapshotMaskSet, value))
            {
                return;
            }

            _snapshotMaskSet = value;
            InvalidateShapeBalanceCache();
        }
    }

    public BalancedImageBundle? CachedShapeBalanceBundle { get; private set; }

    public string? CachedShapeBalanceKey { get; private set; }

    public PreviewRenderDirtyState PreviewDirtyState { get; private set; } = PreviewRenderDirtyState.Clean;

    public ManualMaskOverride? ManualMaskOverride { get; set; }
    public FaceManualAdjustOverride? FaceManualAdjustOverride { get; private set; }
    public FaceWorkArea FaceWorkArea
    {
        get => _faceWorkArea;
        set
        {
            FaceWorkArea clampedValue = value.Clamp();
            if (_faceWorkArea == clampedValue)
            {
                return;
            }

            _faceWorkArea = clampedValue;
            SnapshotMaskSet = null;
            MarkMaskDirty("face_work_area_changed");
            OnPropertyChanged();
        }
    }

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
        PhotoItem photo = new(path, image, thumbnail, lastWriteTimeUtc, length);
        photo.LoadManualFaceAdjustOverride();
        return photo;
    }

    public void ReloadImage()
    {
        _baseImage = LoadBitmap(Path, null);
        _effectPreviewCache.Clear();
        _averageFaceColorMaskPreviewCache = null;
        _neutralPreviewImage = null;
        SnapshotMaskSet = null;
        ManualMaskOverride = null;
        FaceManualAdjustOverride = null;
        MarkMaskDirty("image_reloaded");
        LoadManualFaceAdjustOverride();
        Image = _baseImage;
        Thumbnail = LoadBitmap(Path, 96);
        UpdateFileVersion(Path);
    }

    public BitmapSource GetEffectPreviewSource(int? visibleMaxLongSide)
    {
        return GetEffectPreviewSource(PreviewRenderTier.QualityPreview, visibleMaxLongSide);
    }

    public BitmapSource GetEffectPreviewSource(PreviewRenderTier tier, int? visibleMaxLongSide)
    {
        int cacheKey = CreateEffectPreviewCacheKey(tier, visibleMaxLongSide);
        if (!_effectPreviewCache.TryGetValue(cacheKey, out BitmapSource? previewSource))
        {
            previewSource = PreviewSourceFactory.CreateEffectPreviewSource(BaseImage, tier, visibleMaxLongSide);
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

    public void SetLowPreviewImage(int? visibleMaxLongSide)
    {
        Image = GetEffectPreviewSource(PreviewRenderTier.QualityPreview, visibleMaxLongSide);
    }

    public void ResetRetouchWorkState()
    {
        RetouchState = null;
        _averageFaceColorMaskPreviewCache = null;
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
        SnapshotMaskSet = null;
        ManualMaskOverride = null;
        FaceManualAdjustOverride = null;
        MarkMaskDirty("photo_renamed");
    }

    public void ResetManualMaskOverride()
    {
        ManualMaskOverride = null;
        InvalidateShapeBalanceCache();
        MarkShapeDirty("manual_mask_reset");
    }

    public void ApplyAutoFaceWorkArea(FaceWorkArea faceWorkArea)
    {
        FaceWorkArea clampedValue = faceWorkArea.Clamp();
        if (_faceWorkArea == clampedValue)
        {
            return;
        }

        _faceWorkArea = clampedValue;
        OnPropertyChanged(nameof(FaceWorkArea));
    }

    public void ClearTransientPreviewCache()
    {
        _effectPreviewCache.Clear();
        _neutralPreviewImage = null;
    }

    public void ReleaseInactiveRetouchMemory()
    {
        ClearTransientPreviewCache();
        SnapshotMaskSet = null;
        InvalidateShapeBalanceCache();
        if (!ReferenceEquals(_image, _baseImage))
        {
            Image = _baseImage;
        }
    }

    public bool TryGetAverageFaceColorMaskPreview(double skinMaskRange, out AverageFaceColorMaskPreviewCache cache)
    {
        AverageFaceColorMaskPreviewCache? cached = _averageFaceColorMaskPreviewCache;
        (DateTime lastWriteTimeUtc, long length) = GetSourceVersion();
        if (cached is not null &&
            cached.SourceLastWriteTimeUtc == lastWriteTimeUtc &&
            cached.SourceLength == length &&
            cached.Width == BaseImage.PixelWidth &&
            cached.Height == BaseImage.PixelHeight &&
            Math.Abs(cached.SkinMaskRange - skinMaskRange) < 0.0005)
        {
            cache = cached;
            return true;
        }

        cache = null!;
        return false;
    }

    public void CacheAverageFaceColorMaskPreview(double skinMaskRange, AverageFaceColorMaskResult result, FaceSnapshotMaskSet snapshot, BitmapSource previewImage)
    {
        (DateTime lastWriteTimeUtc, long length) = GetSourceVersion();
        _averageFaceColorMaskPreviewCache = new AverageFaceColorMaskPreviewCache(
            skinMaskRange,
            result,
            snapshot,
            previewImage,
            BaseImage.PixelWidth,
            BaseImage.PixelHeight,
            lastWriteTimeUtc,
            length);
    }

    public void CacheShapeBalanceBundle(string cacheKey, BalancedImageBundle bundle)
    {
        CachedShapeBalanceKey = cacheKey;
        CachedShapeBalanceBundle = bundle;
    }

    public void InvalidateShapeBalanceCache()
    {
        CachedShapeBalanceKey = null;
        CachedShapeBalanceBundle = null;
    }

    public void MarkMaskDirty(string reason)
    {
        InvalidateShapeBalanceCache();
        PreviewDirtyState = PreviewDirtyState.MarkMaskDirty(reason);
    }

    public void MarkShapeDirty(string reason)
    {
        InvalidateShapeBalanceCache();
        PreviewDirtyState = PreviewDirtyState.MarkShapeDirty(reason);
    }

    public void MarkSkinDirty(string reason)
    {
        PreviewDirtyState = PreviewDirtyState.MarkSkinDirty(reason);
    }

    public void MarkPreviewRendered(string reason)
    {
        PreviewDirtyState = PreviewDirtyState.MarkPreviewRendered(reason);
    }

    public void MarkExportClean(string reason)
    {
        PreviewDirtyState = PreviewDirtyState.MarkExportClean(reason);
    }

    public void ApplyManualFaceAdjustOverride(FaceWorkArea faceWorkArea, string snapshotMaskCacheKey)
    {
        FaceManualAdjustOverride = (FaceManualAdjustOverride ?? FaceManualAdjustOverride.Create(this, snapshotMaskCacheKey, faceWorkArea))
            .WithFaceBox(faceWorkArea);
        FaceManualAdjustStore.Default.Save(this, FaceManualAdjustOverride);
        FaceWorkArea clampedValue = faceWorkArea.Clamp();
        if (_faceWorkArea != clampedValue)
        {
            _faceWorkArea = clampedValue;
            OnPropertyChanged(nameof(FaceWorkArea));
        }

        SnapshotMaskSet = null;
        MarkMaskDirty("face_manual_adjust");
    }

    public void ResetManualFaceAdjustOverride()
    {
        FaceManualAdjustOverride = null;
        FaceManualAdjustStore.Default.Delete(this);
        FaceWorkArea = FaceWorkArea.Default;
        MarkMaskDirty("face_manual_adjust_reset");
    }

    private void LoadManualFaceAdjustOverride()
    {
        FaceManualAdjustOverride = FaceManualAdjustStore.Default.TryLoad(this);
        if (FaceManualAdjustOverride?.FaceBoxOverride is { } faceBoxOverride)
        {
            _faceWorkArea = faceBoxOverride.Clamp();
        }
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

    public (DateTime LastWriteTimeUtc, long Length) GetSourceVersion()
    {
        return (_sourceLastWriteTimeUtc, _sourceLength);
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

    private static int CreateEffectPreviewCacheKey(PreviewRenderTier tier, int? visibleMaxLongSide)
    {
        return HashCode.Combine(
            tier,
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

public sealed record AverageFaceColorMaskPreviewCache(
    double SkinMaskRange,
    AverageFaceColorMaskResult Result,
    FaceSnapshotMaskSet Snapshot,
    BitmapSource PreviewImage,
    int Width,
    int Height,
    DateTime SourceLastWriteTimeUtc,
    long SourceLength);

