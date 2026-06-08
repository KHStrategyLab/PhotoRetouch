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
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        if (photo.SnapshotMaskSet is not null &&
            CanReuseSnapshot(photo, photo.SnapshotMaskSet, lastWriteTimeUtc, sourceLength))
        {
            Interlocked.Increment(ref _cacheHitCount);
            return photo.SnapshotMaskSet;
        }

        SnapshotMaskCacheLoadResult diskResult = _diskCache.TryLoad(photo, _maskEngine.MaskVersion);
        if (diskResult.CacheHit && diskResult.Snapshot is not null)
        {
            photo.SnapshotMaskSet = diskResult.Snapshot;
            Interlocked.Increment(ref _cacheHitCount);
            Interlocked.Increment(ref _diskCacheHitCount);
            return diskResult.Snapshot;
        }

        return Rebuild(photo);
    }

    public FaceSnapshotMaskSet Rebuild(PhotoItem photo)
    {
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        FaceSnapshotMaskSet snapshot = Create(
            photo.Path,
            lastWriteTimeUtc,
            sourceLength,
            photo.BaseImage,
            photo.FaceWorkArea);
        photo.SnapshotMaskSet = snapshot;
        _diskCache.Save(snapshot);
        Interlocked.Increment(ref _createdCount);
        return snapshot;
    }

    public FaceSnapshotMaskSet Create(
        string sourcePath,
        DateTime sourceLastWriteTimeUtc,
        long sourceLength,
        BitmapSource source,
        FaceWorkArea faceWorkArea)
    {
        PortraitMaskResult result = _maskEngine.Analyze(source, faceWorkArea);
        SnapshotMaskCacheKey cacheKey = CreateCacheKey(
            sourcePath,
            sourceLastWriteTimeUtc,
            sourceLength,
            source,
            faceWorkArea,
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
            result.NostrilDetection);
    }

    private bool CanReuseSnapshot(PhotoItem photo, FaceSnapshotMaskSet snapshot, DateTime lastWriteTimeUtc, long sourceLength)
    {
        return string.Equals(snapshot.SourcePath, photo.Path, StringComparison.OrdinalIgnoreCase) &&
               snapshot.SourceLastWriteTimeUtc == lastWriteTimeUtc &&
               snapshot.SourceLength == sourceLength &&
               snapshot.CacheKey.ImageWidth == photo.BaseImage.PixelWidth &&
               snapshot.CacheKey.ImageHeight == photo.BaseImage.PixelHeight &&
               snapshot.CacheKey.CropVersion == CreateCropVersion(photo.FaceWorkArea.Clamp()) &&
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
            CreateCropVersion(faceWorkArea.Clamp()),
            maskVersion);
    }

    public static string CreateCropVersion(FaceWorkArea area)
    {
        return string.Join(
            ",",
            area.CenterX.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.CenterY.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.Width.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
            area.Height.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
    }
}
