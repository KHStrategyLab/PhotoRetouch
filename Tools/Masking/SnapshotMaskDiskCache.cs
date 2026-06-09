using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class SnapshotMaskDiskCache
{
    public const int CacheVersion = 1;

    public static SnapshotMaskDiskCache Default { get; } = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoRetouch",
            "cache",
            "snapshot_masks"));

    private readonly string _rootDirectory;

    public SnapshotMaskDiskCache(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public SnapshotMaskCacheLoadResult TryLoad(PhotoItem photo, string maskVersion)
    {
        return TryLoad(photo, maskVersion, photo.BaseImage.PixelWidth, photo.BaseImage.PixelHeight);
    }

    public SnapshotMaskCacheLoadResult TryLoad(PhotoItem photo, string maskVersion, int imageWidth, int imageHeight)
    {
        try
        {
            (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
            string imageId = SnapshotMaskBuilder.CreateImageId(photo.Path, lastWriteTimeUtc, sourceLength);
            string cropVersion = SnapshotMaskBuilder.CreateCropVersion(photo.FaceWorkArea.Clamp(), photo.FaceManualAdjustOverride);
            foreach (string directory in FindCandidateDirectories(imageId))
            {
                string metaPath = Path.Combine(directory, "snapshot_meta.json");
                if (!File.Exists(metaPath))
                {
                    continue;
                }

                SnapshotMaskCacheDocument? document = JsonSerializer.Deserialize<SnapshotMaskCacheDocument>(
                    File.ReadAllText(metaPath, Encoding.UTF8));
                if (document is null)
                {
                    continue;
                }

                string invalidReason = GetInvalidReason(document, imageId, lastWriteTimeUtc, sourceLength, imageWidth, imageHeight, cropVersion, maskVersion);
                if (!string.IsNullOrEmpty(invalidReason))
                {
                    return SnapshotMaskCacheLoadResult.Miss(invalidReason);
                }

                FaceSnapshotMaskSet snapshot = CreateSnapshot(photo.Path, lastWriteTimeUtc, sourceLength, document, directory);
                return SnapshotMaskCacheLoadResult.Hit(snapshot, document.CacheKey.StableId);
            }

            return SnapshotMaskCacheLoadResult.Miss("cache_file_not_found");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException)
        {
            return SnapshotMaskCacheLoadResult.Miss("cache_load_failed_" + ex.GetType().Name);
        }
    }

    public SnapshotMaskCacheSaveResult Save(FaceSnapshotMaskSet snapshot)
    {
        try
        {
            string directory = GetCacheDirectory(snapshot.CacheKey);
            Directory.CreateDirectory(directory);
            SaveMasks(snapshot.Masks, directory);
            SnapshotMaskCacheDocument document = SnapshotMaskCacheDocument.FromSnapshot(snapshot);
            string metaPath = Path.Combine(directory, "snapshot_meta.json");
            File.WriteAllText(metaPath, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
            return new SnapshotMaskCacheSaveResult(true, directory, string.Empty);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return new SnapshotMaskCacheSaveResult(false, string.Empty, "cache_save_failed_" + ex.GetType().Name);
        }
    }

    private IEnumerable<string> FindCandidateDirectories(string imageId)
    {
        if (!Directory.Exists(_rootDirectory))
        {
            yield break;
        }

        foreach (string directory in Directory.EnumerateDirectories(_rootDirectory, imageId + "_*"))
        {
            yield return directory;
        }
    }

    private string GetCacheDirectory(SnapshotMaskCacheKey cacheKey)
    {
        string stableHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey.StableId)))[..16];
        return Path.Combine(_rootDirectory, cacheKey.ImageId + "_" + stableHash);
    }

    private static string GetInvalidReason(
        SnapshotMaskCacheDocument document,
        string imageId,
        DateTime lastWriteTimeUtc,
        long sourceLength,
        int imageWidth,
        int imageHeight,
        string cropVersion,
        string maskVersion)
    {
        if (document.CacheVersion != CacheVersion)
        {
            return "cache_version_mismatch";
        }

        if (document.CacheKey.ImageId != imageId)
        {
            return "image_hash_mismatch";
        }

        if (document.SourceLastWriteTimeUtc != lastWriteTimeUtc || document.SourceLength != sourceLength)
        {
            return "source_file_changed";
        }

        if (document.CacheKey.ImageWidth != imageWidth || document.CacheKey.ImageHeight != imageHeight)
        {
            return "image_size_mismatch";
        }

        if (document.CacheKey.CropVersion != cropVersion)
        {
            return "crop_version_mismatch";
        }

        if (document.CacheKey.MaskVersion != maskVersion)
        {
            return "mask_version_mismatch";
        }

        return string.Empty;
    }

    private static FaceSnapshotMaskSet CreateSnapshot(
        string sourcePath,
        DateTime sourceLastWriteTimeUtc,
        long sourceLength,
        SnapshotMaskCacheDocument document,
        string directory)
    {
        FaceMaskSet masks = LoadMasks(directory);
        FaceAnalysisResult analysis = new(
            document.Analysis.FaceBox.ToRect(),
            document.Analysis.FaceLandmarks.ToDictionary(pair => pair.Key, pair => pair.Value.ToPoint()),
            null,
            document.Analysis.FaceAngle,
            document.Analysis.FaceQualityScore,
            document.Analysis.LandmarkConfidence,
            document.Analysis.ParsingConfidence,
            document.Analysis.DebugWarnings);
        MaskQualityReport quality = document.QualityReport.ToReport();
        SnapshotMaskCacheKey cacheKey = document.CacheKey.ToCacheKey();
        FaceFeatureMeshSet featureMeshes = FeatureMeshGenerator.Generate(masks.SkinMask.Width, masks.SkinMask.Height, analysis);
        return new FaceSnapshotMaskSet(
            cacheKey.ImageId,
            cacheKey,
            sourcePath,
            sourceLastWriteTimeUtc,
            sourceLength,
            analysis,
            masks,
            quality,
            document.CreatedAtUtc,
            FeatureMeshes: featureMeshes);
    }

    private static void SaveMasks(FaceMaskSet masks, string directory)
    {
        SaveMask(masks.SkinMask, Path.Combine(directory, "skin_mask.png"));
        SaveMask(masks.EyeMask, Path.Combine(directory, "eye_mask.png"));
        SaveMask(masks.EyebrowMask, Path.Combine(directory, "eyebrow_mask.png"));
        SaveMask(masks.LipMask, Path.Combine(directory, "lip_mask.png"));
        SaveMask(masks.InnerMouthMask, Path.Combine(directory, "inner_mouth_mask.png"));
        SaveMask(masks.TeethMask, Path.Combine(directory, "teeth_mask.png"));
        SaveMask(masks.NoseMask, Path.Combine(directory, "nose_mask.png"));
        SaveMask(masks.NoseSkinMask, Path.Combine(directory, "nose_skin_mask.png"));
        SaveMask(masks.NostrilMask, Path.Combine(directory, "nostril_mask.png"));
        SaveMask(masks.NoseShadowMask, Path.Combine(directory, "nose_shadow_mask.png"));
        SaveMask(masks.HairMask, Path.Combine(directory, "hair_mask.png"));
        SaveMask(masks.BeardMask, Path.Combine(directory, "beard_mask.png"));
        SaveMask(masks.MustacheMask, Path.Combine(directory, "mustache_mask.png"));
        SaveMask(masks.GlassesMask, Path.Combine(directory, "glasses_mask.png"));
        SaveMask(masks.HardProtectMask, Path.Combine(directory, "hard_protect_mask.png"));
        SaveMask(masks.SoftProtectMask, Path.Combine(directory, "soft_protect_mask.png"));
        SaveMask(masks.RetouchAllowMask, Path.Combine(directory, "retouch_allow_mask.png"));
        SaveMask(masks.FinalOverlayMask, Path.Combine(directory, "final_overlay_mask.png"));
    }

    private static FaceMaskSet LoadMasks(string directory)
    {
        return new FaceMaskSet(
            LoadMask(Path.Combine(directory, "skin_mask.png")),
            LoadMask(Path.Combine(directory, "eye_mask.png")),
            LoadMask(Path.Combine(directory, "eyebrow_mask.png")),
            LoadMask(Path.Combine(directory, "lip_mask.png")),
            LoadMask(Path.Combine(directory, "inner_mouth_mask.png")),
            LoadMask(Path.Combine(directory, "teeth_mask.png")),
            LoadMask(Path.Combine(directory, "nose_mask.png")),
            LoadMask(Path.Combine(directory, "nose_skin_mask.png")),
            LoadMask(Path.Combine(directory, "nostril_mask.png")),
            LoadMask(Path.Combine(directory, "nose_shadow_mask.png")),
            LoadMask(Path.Combine(directory, "hair_mask.png")),
            LoadMask(Path.Combine(directory, "beard_mask.png")),
            LoadMask(Path.Combine(directory, "mustache_mask.png")),
            LoadMask(Path.Combine(directory, "glasses_mask.png")),
            LoadMask(Path.Combine(directory, "hard_protect_mask.png")),
            LoadMask(Path.Combine(directory, "soft_protect_mask.png")),
            LoadMask(Path.Combine(directory, "retouch_allow_mask.png")),
            LoadMask(Path.Combine(directory, "final_overlay_mask.png")));
    }

    private static void SaveMask(MaskPlane mask, string path)
    {
        int stride = mask.Width;
        byte[] pixels = new byte[stride * mask.Height];
        for (int index = 0; index < pixels.Length; index++)
        {
            pixels[index] = (byte)Math.Clamp((int)Math.Round(mask.Values[index] * 255), 0, 255);
        }

        BitmapSource bitmap = BitmapSource.Create(mask.Width, mask.Height, 96, 96, PixelFormats.Gray8, null, pixels, stride);
        bitmap.Freeze();
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static MaskPlane LoadMask(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        BitmapSource gray = image.Format == PixelFormats.Gray8
            ? image
            : new FormatConvertedBitmap(image, PixelFormats.Gray8, null, 0);
        gray.Freeze();
        int stride = gray.PixelWidth;
        byte[] pixels = new byte[stride * gray.PixelHeight];
        gray.CopyPixels(pixels, stride, 0);
        double[] values = new double[pixels.Length];
        for (int index = 0; index < pixels.Length; index++)
        {
            values[index] = pixels[index] / 255d;
        }

        return new MaskPlane(gray.PixelWidth, gray.PixelHeight, values);
    }
}

public sealed record SnapshotMaskCacheLoadResult(
    bool CacheHit,
    FaceSnapshotMaskSet? Snapshot,
    string CacheKey,
    string MissReason)
{
    public static SnapshotMaskCacheLoadResult Hit(FaceSnapshotMaskSet snapshot, string cacheKey)
    {
        return new SnapshotMaskCacheLoadResult(true, snapshot, cacheKey, string.Empty);
    }

    public static SnapshotMaskCacheLoadResult Miss(string reason)
    {
        return new SnapshotMaskCacheLoadResult(false, null, string.Empty, reason);
    }
}

public sealed record SnapshotMaskCacheSaveResult(bool Saved, string CacheDirectory, string Warning);

internal sealed record SnapshotMaskCacheDocument(
    int CacheVersion,
    SnapshotMaskCacheKeyDto CacheKey,
    DateTime SourceLastWriteTimeUtc,
    long SourceLength,
    FaceAnalysisDto Analysis,
    MaskQualityReportDto QualityReport,
    DateTime CreatedAtUtc)
{
    public static SnapshotMaskCacheDocument FromSnapshot(FaceSnapshotMaskSet snapshot)
    {
        return new SnapshotMaskCacheDocument(
            SnapshotMaskDiskCache.CacheVersion,
            SnapshotMaskCacheKeyDto.From(snapshot.CacheKey),
            snapshot.SourceLastWriteTimeUtc,
            snapshot.SourceLength,
            FaceAnalysisDto.From(snapshot.Analysis),
            MaskQualityReportDto.From(snapshot.QualityReport),
            snapshot.CreatedAtUtc);
    }
}

internal sealed record SnapshotMaskCacheKeyDto(
    string ImageId,
    int ImageWidth,
    int ImageHeight,
    Int32RectDto FaceBox,
    double FaceAngle,
    string CropVersion,
    string MaskVersion)
{
    public string StableId => ToCacheKey().StableId;

    public static SnapshotMaskCacheKeyDto From(SnapshotMaskCacheKey cacheKey)
    {
        return new SnapshotMaskCacheKeyDto(
            cacheKey.ImageId,
            cacheKey.ImageWidth,
            cacheKey.ImageHeight,
            Int32RectDto.From(cacheKey.FaceBox),
            cacheKey.FaceAngle,
            cacheKey.CropVersion,
            cacheKey.MaskVersion);
    }

    public SnapshotMaskCacheKey ToCacheKey()
    {
        return new SnapshotMaskCacheKey(ImageId, ImageWidth, ImageHeight, FaceBox.ToRect(), FaceAngle, CropVersion, MaskVersion);
    }
}

internal sealed record FaceAnalysisDto(
    Int32RectDto FaceBox,
    Dictionary<string, PointDto> FaceLandmarks,
    double FaceAngle,
    double FaceQualityScore,
    double LandmarkConfidence,
    double ParsingConfidence,
    IReadOnlyList<string> DebugWarnings)
{
    public static FaceAnalysisDto From(FaceAnalysisResult analysis)
    {
        return new FaceAnalysisDto(
            Int32RectDto.From(analysis.FaceBox),
            analysis.FaceLandmarks.ToDictionary(pair => pair.Key, pair => PointDto.From(pair.Value)),
            analysis.FaceAngle,
            analysis.FaceQualityScore,
            analysis.LandmarkConfidence,
            analysis.ParsingConfidence,
            analysis.DebugWarnings);
    }
}

internal sealed record MaskQualityReportDto(
    double Score,
    double FaceQualityScore,
    double LandmarkQualityScore,
    double ParsingQualityScore,
    double SkinMaskQualityScore,
    double EyeMaskQualityScore,
    double EyebrowMaskQualityScore,
    double LipMaskQualityScore,
    double NostrilMaskQualityScore,
    double HairMaskQualityScore,
    double HardProtectQualityScore,
    double RetouchAllowQualityScore,
    bool IsUsable,
    int MaxAllowedStage,
    bool IsSafeForStrongRetouch,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> FatalErrors)
{
    public static MaskQualityReportDto From(MaskQualityReport report)
    {
        return new MaskQualityReportDto(
            report.Score,
            report.FaceQualityScore,
            report.LandmarkQualityScore,
            report.ParsingQualityScore,
            report.SkinMaskQualityScore,
            report.EyeMaskQualityScore,
            report.EyebrowMaskQualityScore,
            report.LipMaskQualityScore,
            report.NostrilMaskQualityScore,
            report.HairMaskQualityScore,
            report.HardProtectQualityScore,
            report.RetouchAllowQualityScore,
            report.IsUsable,
            report.MaxAllowedStage,
            report.IsSafeForStrongRetouch,
            report.Warnings,
            report.FatalErrors);
    }

    public MaskQualityReport ToReport()
    {
        return new MaskQualityReport(
            Score,
            FaceQualityScore,
            LandmarkQualityScore,
            ParsingQualityScore,
            SkinMaskQualityScore,
            EyeMaskQualityScore,
            EyebrowMaskQualityScore,
            LipMaskQualityScore,
            NostrilMaskQualityScore,
            HairMaskQualityScore,
            HardProtectQualityScore,
            RetouchAllowQualityScore,
            IsUsable,
            MaxAllowedStage,
            IsSafeForStrongRetouch,
            Warnings,
            FatalErrors);
    }
}

internal sealed record Int32RectDto(int X, int Y, int Width, int Height)
{
    public static Int32RectDto From(Int32Rect rect)
    {
        return new Int32RectDto(rect.X, rect.Y, rect.Width, rect.Height);
    }

    public Int32Rect ToRect()
    {
        return new Int32Rect(X, Y, Width, Height);
    }
}

internal sealed record PointDto(double X, double Y)
{
    public static PointDto From(WpfPoint point)
    {
        return new PointDto(point.X, point.Y);
    }

    public WpfPoint ToPoint()
    {
        return new WpfPoint(X, Y);
    }
}
