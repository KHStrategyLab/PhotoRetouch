using System.Windows.Media.Imaging;
using System.Windows;

namespace PhotoRetouch;

public sealed class SnapshotMaskBuilder
{
    private readonly IPortraitMaskEngine _maskEngine;
    private readonly SnapshotMaskDiskCache _diskCache;
    private int _createdCount;
    private int _cacheHitCount;
    private int _diskCacheHitCount;

    public SnapshotMaskBuilder(IPortraitMaskEngine maskEngine, SnapshotMaskDiskCache? diskCache = null)
    {
        _maskEngine = maskEngine;
        _diskCache = diskCache ?? SnapshotMaskDiskCache.Default;
    }

    public int CreatedCount => _createdCount;

    public int CacheHitCount => _cacheHitCount;

    public int DiskCacheHitCount => _diskCacheHitCount;

    public FaceSnapshotMaskSet GetOrCreate(PhotoItem photo)
    {
        return GetOrCreate(photo, photo.BaseImage);
    }

    public FaceSnapshotMaskSet GetOrCreate(PhotoItem photo, BitmapSource analysisSource)
    {
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        if (photo.SnapshotMaskSet is not null &&
            CanReuseSnapshot(photo, photo.SnapshotMaskSet, lastWriteTimeUtc, sourceLength, analysisSource))
        {
            ApplyAutoFaceWorkAreaFromSnapshot(photo, photo.SnapshotMaskSet, analysisSource);
            Interlocked.Increment(ref _cacheHitCount);
            return photo.SnapshotMaskSet;
        }

        SnapshotMaskCacheLoadResult diskResult = _diskCache.TryLoad(photo, _maskEngine.MaskVersion, analysisSource.PixelWidth, analysisSource.PixelHeight);
        if (diskResult.CacheHit && diskResult.Snapshot is not null)
        {
            photo.SnapshotMaskSet = diskResult.Snapshot;
            ApplyAutoFaceWorkAreaFromSnapshot(photo, diskResult.Snapshot, analysisSource);
            Interlocked.Increment(ref _cacheHitCount);
            Interlocked.Increment(ref _diskCacheHitCount);
            return diskResult.Snapshot;
        }

        return Rebuild(photo, analysisSource);
    }

    public FaceSnapshotMaskSet Rebuild(PhotoItem photo)
    {
        return Rebuild(photo, photo.BaseImage);
    }

    public FaceSnapshotMaskSet Rebuild(PhotoItem photo, BitmapSource analysisSource)
    {
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        FaceSnapshotMaskSet snapshot = Create(
            photo.Path,
            lastWriteTimeUtc,
            sourceLength,
            analysisSource,
            photo.FaceWorkArea,
            photo.FaceManualAdjustOverride);
        photo.SnapshotMaskSet = snapshot;
        if (photo.FaceManualAdjustOverride is not { HasOverrides: true })
        {
            photo.ApplyAutoFaceWorkArea(CreateFaceWorkAreaFromFaceBox(
                snapshot.Analysis.FaceBox,
                analysisSource.PixelWidth,
                analysisSource.PixelHeight));
        }

        _diskCache.Save(snapshot);
        Interlocked.Increment(ref _createdCount);
        return snapshot;
    }

    public FaceSnapshotMaskSet Create(
        string sourcePath,
        DateTime sourceLastWriteTimeUtc,
        long sourceLength,
        BitmapSource source,
        FaceWorkArea faceWorkArea,
        FaceManualAdjustOverride? faceManualAdjustOverride)
    {
        PortraitMaskResult result = _maskEngine.Analyze(source, faceWorkArea);
        FaceWorkArea effectiveFaceWorkArea = faceManualAdjustOverride is { HasOverrides: true }
            ? faceWorkArea.Clamp()
            : CreateFaceWorkAreaFromFaceBox(result.Analysis.FaceBox, source.PixelWidth, source.PixelHeight);
        SnapshotMaskCacheKey cacheKey = CreateCacheKey(
            sourcePath,
            sourceLastWriteTimeUtc,
            sourceLength,
            source,
            effectiveFaceWorkArea,
            faceManualAdjustOverride,
            result.Analysis.FaceBox,
            result.Analysis.FaceAngle,
            _maskEngine.MaskVersion);
        return new FaceSnapshotMaskSet(
            cacheKey.ImageId,
            cacheKey,
            sourcePath,
            sourceLastWriteTimeUtc,
            sourceLength,
            result.Analysis,
            result.Masks,
            result.QualityReport,
            DateTime.UtcNow,
            result.ParsingMasks,
            result.WarpedStandardMasks,
            result.NostrilDetection,
            result.FeatureMeshes);
    }

    public static FaceWorkArea CreateFaceWorkAreaFromFaceBox(Int32Rect faceBox, int imageWidth, int imageHeight)
    {
        double safeWidth = Math.Max(1, imageWidth);
        double safeHeight = Math.Max(1, imageHeight);
        double centerX = (faceBox.X + faceBox.Width / 2d) / safeWidth;
        double centerY = (faceBox.Y + faceBox.Height * 0.52) / safeHeight;
        double width = faceBox.Width * 1.08 / safeWidth;
        double height = faceBox.Height * 1.12 / safeHeight;
        return new FaceWorkArea(centerX, centerY, width, height).Clamp();
    }

    private static void ApplyAutoFaceWorkAreaFromSnapshot(PhotoItem photo, FaceSnapshotMaskSet snapshot, BitmapSource analysisSource)
    {
        if (photo.FaceManualAdjustOverride is { HasOverrides: true })
        {
            return;
        }

        photo.ApplyAutoFaceWorkArea(CreateFaceWorkAreaFromFaceBox(
            snapshot.Analysis.FaceBox,
            analysisSource.PixelWidth,
            analysisSource.PixelHeight));
    }

    private bool CanReuseSnapshot(
        PhotoItem photo,
        FaceSnapshotMaskSet snapshot,
        DateTime lastWriteTimeUtc,
        long sourceLength,
        BitmapSource analysisSource)
    {
        return string.Equals(snapshot.SourcePath, photo.Path, StringComparison.OrdinalIgnoreCase) &&
               snapshot.SourceLastWriteTimeUtc == lastWriteTimeUtc &&
               snapshot.SourceLength == sourceLength &&
               snapshot.CacheKey.ImageWidth == analysisSource.PixelWidth &&
               snapshot.CacheKey.ImageHeight == analysisSource.PixelHeight &&
               snapshot.CacheKey.CropVersion == CreateCropVersion(photo.FaceWorkArea.Clamp(), photo.FaceManualAdjustOverride) &&
               snapshot.CacheKey.MaskVersion == _maskEngine.MaskVersion;
    }

    public static string CreateImageId(string sourcePath, DateTime sourceLastWriteTimeUtc, long sourceLength)
    {
        return HashCode.Combine(
            sourcePath.ToUpperInvariant(),
            sourceLastWriteTimeUtc,
            sourceLength).ToString("X8", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static SnapshotMaskCacheKey CreateCacheKey(
        string sourcePath,
        DateTime sourceLastWriteTimeUtc,
        long sourceLength,
        BitmapSource source,
        FaceWorkArea faceWorkArea,
        FaceManualAdjustOverride? faceManualAdjustOverride,
        System.Windows.Int32Rect faceBox,
        double faceAngle,
        string maskVersion)
    {
        string imageId = CreateImageId(sourcePath, sourceLastWriteTimeUtc, sourceLength);
        return new SnapshotMaskCacheKey(
            imageId,
            source.PixelWidth,
            source.PixelHeight,
            faceBox,
            faceAngle,
            CreateCropVersion(faceWorkArea.Clamp(), faceManualAdjustOverride),
            maskVersion);
    }

    public static string CreateCropVersion(FaceWorkArea area)
    {
        return CreateCropVersion(area, null);
    }

    public static string CreateCropVersion(FaceWorkArea area, FaceManualAdjustOverride? faceManualAdjustOverride)
    {
        string faceWorkAreaVersion = string.Join(
            ",",
            area.CenterX.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.CenterY.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.Width.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.Height.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
        return faceManualAdjustOverride is { HasOverrides: true }
            ? faceWorkAreaVersion + "|manual:" + faceManualAdjustOverride.StableVersion
            : faceWorkAreaVersion;
    }
}
