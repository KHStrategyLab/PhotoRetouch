using System.IO;
using System.Text;
using System.Text.Json;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record NormalizedFacePoint(double X, double Y)
{
    public NormalizedFacePoint Clamp()
    {
        return new NormalizedFacePoint(Math.Clamp(X, 0, 1), Math.Clamp(Y, 0, 1));
    }

    public WpfPoint ToImagePoint(int width, int height)
    {
        return new WpfPoint(Math.Clamp(X, 0, 1) * width, Math.Clamp(Y, 0, 1) * height);
    }

    public static NormalizedFacePoint FromImagePoint(WpfPoint point, int width, int height)
    {
        return new NormalizedFacePoint(
            width <= 0 ? 0.5 : Math.Clamp(point.X / width, 0, 1),
            height <= 0 ? 0.5 : Math.Clamp(point.Y / height, 0, 1));
    }
}

public sealed record FaceManualAdjustOverride(
    string ImageId,
    string SnapshotMaskCacheKey,
    int ManualAdjustVersion,
    FaceWorkArea? FaceBoxOverride,
    NormalizedFacePoint? LeftEyeCenterOverride,
    NormalizedFacePoint? RightEyeCenterOverride,
    NormalizedFacePoint? NoseTipOverride,
    NormalizedFacePoint? MouthCenterOverride,
    NormalizedFacePoint? ChinPointOverride,
    double? FaceAngleOverride,
    double? FaceScaleOverride,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public const int CurrentVersion = 1;

    public bool HasOverrides =>
        FaceBoxOverride is not null ||
        LeftEyeCenterOverride is not null ||
        RightEyeCenterOverride is not null ||
        NoseTipOverride is not null ||
        MouthCenterOverride is not null ||
        ChinPointOverride is not null ||
        FaceAngleOverride is not null ||
        FaceScaleOverride is not null;

    public string StableVersion
    {
        get
        {
            FaceWorkArea area = (FaceBoxOverride ?? FaceWorkArea.Default).Clamp();
            return string.Join(
                ",",
                ManualAdjustVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                area.CenterX.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                area.CenterY.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                area.Width.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                area.Height.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                PointVersion(LeftEyeCenterOverride),
                PointVersion(RightEyeCenterOverride),
                PointVersion(NoseTipOverride),
                PointVersion(MouthCenterOverride),
                PointVersion(ChinPointOverride),
                (FaceAngleOverride ?? 0).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture),
                (FaceScaleOverride ?? 0).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture));
        }
    }

    public FaceManualAdjustOverride WithFaceBox(FaceWorkArea faceWorkArea)
    {
        DateTime now = DateTime.UtcNow;
        return this with
        {
            FaceBoxOverride = faceWorkArea.Clamp(),
            UpdatedAtUtc = now
        };
    }

    public static FaceManualAdjustOverride Create(PhotoItem photo, string snapshotMaskCacheKey, FaceWorkArea faceWorkArea)
    {
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        string imageId = SnapshotMaskBuilder.CreateImageId(photo.Path, lastWriteTimeUtc, sourceLength);
        DateTime now = DateTime.UtcNow;
        return new FaceManualAdjustOverride(
            imageId,
            snapshotMaskCacheKey,
            CurrentVersion,
            faceWorkArea.Clamp(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            now,
            now);
    }

    private static string PointVersion(NormalizedFacePoint? point)
    {
        return point is null
            ? "-"
            : point.Clamp().X.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) +
              ":" +
              point.Clamp().Y.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class FaceManualAdjustStore
{
    public static FaceManualAdjustStore Default { get; } = new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PhotoRetouch",
            "cache",
            "manual_adjustments"));

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _rootDirectory;

    public FaceManualAdjustStore(string rootDirectory)
    {
        _rootDirectory = rootDirectory;
    }

    public FaceManualAdjustOverride? TryLoad(PhotoItem photo)
    {
        try
        {
            string path = GetPath(photo);
            if (!File.Exists(path))
            {
                return null;
            }

            FaceManualAdjustOverride? faceAdjust = JsonSerializer.Deserialize<FaceManualAdjustOverride>(
                File.ReadAllText(path, Encoding.UTF8));
            return faceAdjust is { ManualAdjustVersion: FaceManualAdjustOverride.CurrentVersion, HasOverrides: true }
                ? faceAdjust
                : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return null;
        }
    }

    public void Save(PhotoItem photo, FaceManualAdjustOverride faceAdjust)
    {
        Directory.CreateDirectory(_rootDirectory);
        File.WriteAllText(GetPath(photo), JsonSerializer.Serialize(faceAdjust, JsonOptions), Encoding.UTF8);
    }

    public void Delete(PhotoItem photo)
    {
        try
        {
            string path = GetPath(photo);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
        }
    }

    private string GetPath(PhotoItem photo)
    {
        (DateTime lastWriteTimeUtc, long sourceLength) = photo.GetSourceVersion();
        string imageId = SnapshotMaskBuilder.CreateImageId(photo.Path, lastWriteTimeUtc, sourceLength);
        return Path.Combine(_rootDirectory, imageId + "_face_adjust.json");
    }
}
