using System.Windows.Media.Imaging;
using System.Windows;
using WpfPoint = System.Windows.Point;

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
            photo.ApplyAutoFaceWorkArea(CreateFaceWorkAreaFromAnalysis(
                snapshot.Analysis,
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
            : CreateFaceWorkAreaFromAnalysis(result.Analysis, source.PixelWidth, source.PixelHeight);
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
            result.NostrilDetection);
    }

    public static FaceWorkArea CreateFaceWorkAreaFromFaceBox(Int32Rect faceBox, int imageWidth, int imageHeight)
    {
        return CreateFallbackFaceWorkAreaFromFaceBox(faceBox, imageWidth, imageHeight);
    }

    public static FaceWorkArea CreateFaceWorkAreaFromAnalysis(FaceAnalysisResult analysis, int imageWidth, int imageHeight)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        return TryCreateFaceWorkAreaFromLandmarks(analysis.FaceLandmarks, imageWidth, imageHeight, out FaceWorkArea anchorArea)
            ? anchorArea
            : CreateFallbackFaceWorkAreaFromFaceBox(analysis.FaceBox, imageWidth, imageHeight);
    }

    private static bool TryCreateFaceWorkAreaFromLandmarks(IReadOnlyDictionary<string, WpfPoint> landmarks, int imageWidth, int imageHeight, out FaceWorkArea area)
    {
        area = FaceWorkArea.Default;
        if (!TryGetLandmark(landmarks, "left_eye", out WpfPoint leftEye) ||
            !TryGetLandmark(landmarks, "right_eye", out WpfPoint rightEye) ||
            !TryGetLandmark(landmarks, "nose_tip", out WpfPoint noseTip) ||
            !TryGetLandmark(landmarks, "mouth_center", out WpfPoint mouthCenter))
        {
            return false;
        }

        double eyeDistance = Distance(leftEye, rightEye);
        if (eyeDistance < 4)
        {
            return false;
        }

        WpfPoint eyeCenter = new((leftEye.X + rightEye.X) * 0.5, (leftEye.Y + rightEye.Y) * 0.5);
        WpfPoint chin = TryGetLandmark(landmarks, "chin", out WpfPoint detectedChin)
            ? detectedChin
            : EstimateChinPoint(eyeCenter, noseTip, mouthCenter, eyeDistance, imageWidth, imageHeight);
        double chinY = Math.Clamp(chin.Y, mouthCenter.Y + eyeDistance * 0.55, mouthCenter.Y + eyeDistance * 1.45);
        double centerX = eyeCenter.X * 0.30 + noseTip.X * 0.30 + mouthCenter.X * 0.28 + chin.X * 0.12;
        double top = eyeCenter.Y - eyeDistance * 1.22;
        double bottom = Math.Max(chinY, mouthCenter.Y + eyeDistance * 1.05);
        double anchorWidth = eyeDistance * 2.72;
        double anchorHeight = Math.Max(eyeDistance * 3.05, bottom - top);
        double centerY = (top + bottom) * 0.5;

        area = new FaceWorkArea(
            centerX / Math.Max(1, imageWidth),
            centerY / Math.Max(1, imageHeight),
            anchorWidth / Math.Max(1, imageWidth),
            anchorHeight / Math.Max(1, imageHeight)).Clamp();
        return true;
    }

    private static FaceWorkArea CreateFallbackFaceWorkAreaFromFaceBox(Int32Rect faceBox, int imageWidth, int imageHeight)
    {
        double safeWidth = Math.Max(1, imageWidth);
        double safeHeight = Math.Max(1, imageHeight);
        double centerX = (faceBox.X + faceBox.Width / 2d) / safeWidth;
        double centerY = (faceBox.Y + faceBox.Height * 0.52) / safeHeight;
        double width = faceBox.Width * 1.08 / safeWidth;
        double height = faceBox.Height * 1.12 / safeHeight;
        return new FaceWorkArea(centerX, centerY, width, height).Clamp();
    }

    private static bool TryGetLandmark(IReadOnlyDictionary<string, WpfPoint> landmarks, string key, out WpfPoint point)
    {
        return landmarks.TryGetValue(key, out point) &&
               !double.IsNaN(point.X) &&
               !double.IsNaN(point.Y);
    }

    private static WpfPoint EstimateChinPoint(WpfPoint eyeCenter, WpfPoint noseTip, WpfPoint mouthCenter, double eyeDistance, int imageWidth, int imageHeight)
    {
        double noseToMouth = Math.Max(eyeDistance * 0.30, mouthCenter.Y - noseTip.Y);
        double chinX = eyeCenter.X * 0.18 + noseTip.X * 0.24 + mouthCenter.X * 0.58;
        double chinY = mouthCenter.Y + Math.Clamp(Math.Max(eyeDistance * 0.72, noseToMouth * 1.08), eyeDistance * 0.55, eyeDistance * 1.35);
        return new WpfPoint(
            Math.Clamp(chinX, 0, Math.Max(0, imageWidth - 1)),
            Math.Clamp(chinY, 0, Math.Max(0, imageHeight - 1)));
    }

    private static double Distance(WpfPoint a, WpfPoint b)
    {
        double dx = b.X - a.X;
        double dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static void ApplyAutoFaceWorkAreaFromSnapshot(PhotoItem photo, FaceSnapshotMaskSet snapshot, BitmapSource analysisSource)
    {
        if (photo.FaceManualAdjustOverride is { HasOverrides: true })
        {
            return;
        }

        photo.ApplyAutoFaceWorkArea(CreateFaceWorkAreaFromAnalysis(
            snapshot.Analysis,
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
