using System.IO;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class HardProtectTestSetRunner
{
    private static readonly int[] TestStages = { 1, 5, 10 };
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff"
    };

    private readonly SnapshotMaskBuilder _snapshotMaskBuilder;
    private readonly RetouchStageProcessor _retouchStageProcessor;

    public HardProtectTestSetRunner()
        : this(new SnapshotMaskBuilder(new StandardMaskWarpEngine()), new RetouchStageProcessor())
    {
    }

    public HardProtectTestSetRunner(SnapshotMaskBuilder snapshotMaskBuilder, RetouchStageProcessor retouchStageProcessor)
    {
        _snapshotMaskBuilder = snapshotMaskBuilder;
        _retouchStageProcessor = retouchStageProcessor;
    }

    public HardProtectTestSetSummary RunDirectory(string inputDirectory, string outputDirectory)
    {
        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException(inputDirectory);
        }

        PhotoItem[] photos = Directory.EnumerateFiles(inputDirectory)
            .Where(path => SupportedExtensions.Contains(Path.GetExtension(path)))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(PhotoItem.Load)
            .ToArray();

        return RunPhotos(photos, outputDirectory);
    }

    public HardProtectTestSetSummary RunPhotos(IEnumerable<PhotoItem> photos, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        List<HardProtectTestReport> reports = new();

        foreach (PhotoItem photo in photos)
        {
            FaceSnapshotMaskSet snapshot = _snapshotMaskBuilder.GetOrCreate(photo);
            string photoDirectory = Path.Combine(outputDirectory, SanitizeFileName(Path.GetFileNameWithoutExtension(photo.FileName)));
            Directory.CreateDirectory(photoDirectory);
            SaveCommonMasks(photo.BaseImage, snapshot, photoDirectory);

            foreach (int stage in TestStages)
            {
                RetouchStageProcessorOutput output = _retouchStageProcessor.Process(
                    photo.BaseImage,
                    snapshot,
                    new RetouchOptions(stage));
                string stageDirectory = Path.Combine(photoDirectory, $"stage_{stage}");
                Directory.CreateDirectory(stageDirectory);
                SaveStageOutputs(output, stageDirectory);
                reports.Add(CreateReport(photo, output, snapshot));
            }
        }

        HardProtectTestSetSummary summary = new(
            DateTime.UtcNow,
            reports.Count,
            reports.Count(report => report.IsPassed),
            reports.Count(report => !report.IsPassed),
            reports);
        SaveJson(summary, Path.Combine(outputDirectory, "hardprotect_test_summary.json"));
        return summary;
    }

    private static void SaveCommonMasks(BitmapSource original, FaceSnapshotMaskSet snapshot, string outputDirectory)
    {
        SaveBitmap(original, Path.Combine(outputDirectory, "original.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.HardProtectMask), Path.Combine(outputDirectory, "hardprotect_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.EyeMask), Path.Combine(outputDirectory, "eye_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.EyebrowMask), Path.Combine(outputDirectory, "eyebrow_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.LipMask), Path.Combine(outputDirectory, "lip_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.InnerMouthMask), Path.Combine(outputDirectory, "inner_mouth_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.NostrilMask), Path.Combine(outputDirectory, "nostril_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.HairMask), Path.Combine(outputDirectory, "hair_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(MaskPlane.Union(snapshot.Masks.BeardMask, snapshot.Masks.MustacheMask)), Path.Combine(outputDirectory, "beard_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.GlassesMask), Path.Combine(outputDirectory, "glasses_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateFinalOverlayPreview(original, snapshot.Masks), Path.Combine(outputDirectory, "hardprotect_overlay.png"));
    }

    private static void SaveStageOutputs(RetouchStageProcessorOutput output, string outputDirectory)
    {
        int stage = output.Report.RequestedStage;
        SaveBitmap(output.FinalImage, Path.Combine(outputDirectory, $"final_stage_{stage}.png"));
        SaveBitmap(output.HardProtectFinalImage, Path.Combine(outputDirectory, $"hardprotect_restored_stage_{stage}.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.HardProtectBeforeRestoreDiffMask), Path.Combine(outputDirectory, $"hardprotect_diff_before_stage_{stage}.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.HardProtectAfterRestoreDiffMask), Path.Combine(outputDirectory, $"hardprotect_diff_after_stage_{stage}.png"));
        SaveJson(output.HardProtectRestoreReport, Path.Combine(outputDirectory, $"hardprotect_report_stage_{stage}.json"));
        SaveJson(output.Report, Path.Combine(outputDirectory, $"retouch_report_stage_{stage}.json"));
    }

    private static HardProtectTestReport CreateReport(PhotoItem photo, RetouchStageProcessorOutput output, FaceSnapshotMaskSet snapshot)
    {
        HardProtectRestoreReport restoreReport = output.HardProtectRestoreReport;
        string failReason = restoreReport.IsHardProtectClean
            ? string.Empty
            : ClassifyFailure(restoreReport, snapshot);

        return new HardProtectTestReport(
            photo.FileName,
            output.Report.RequestedStage,
            output.Report.AppliedStage,
            output.Report.MaskQualityScore,
            restoreReport.HardProtectPixelCount,
            restoreReport.ChangedPixelAfterRestoreCount,
            restoreReport.EyeChanged,
            restoreReport.EyebrowChanged,
            restoreReport.LipChanged,
            restoreReport.InnerMouthChanged,
            restoreReport.NostrilChanged,
            restoreReport.HairChanged,
            restoreReport.BeardChanged,
            restoreReport.GlassesChanged,
            restoreReport.IsHardProtectClean,
            failReason,
            output.DebugWarnings);
    }

    private static string ClassifyFailure(HardProtectRestoreReport report, FaceSnapshotMaskSet snapshot)
    {
        if (report.HardProtectPixelCount == 0)
        {
            return "MaskMissing";
        }

        if (snapshot.QualityReport.HardProtectQualityScore < 0.50)
        {
            return "MaskTooSmallOrMisaligned";
        }

        if (report.ChangedPixelAfterRestoreCount > 0)
        {
            return "FinalRestoreFailed";
        }

        return "NeedsReview";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (char invalidChar in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalidChar, '_');
        }

        return string.IsNullOrWhiteSpace(name) ? "photo" : name;
    }

    private static void SaveJson<T>(T value, string path)
    {
        JsonSerializerOptions options = new() { WriteIndented = true };
        File.WriteAllText(path, JsonSerializer.Serialize(value, options), System.Text.Encoding.UTF8);
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}

public sealed record HardProtectTestSetSummary(
    DateTime CreatedAtUtc,
    int ReportCount,
    int PassedCount,
    int FailedCount,
    IReadOnlyList<HardProtectTestReport> Reports);

public sealed record HardProtectTestReport(
    string TestImageName,
    int RequestedStage,
    int AppliedStage,
    double MaskQualityScore,
    int HardProtectPixelCount,
    int ChangedPixelCount,
    bool EyeChanged,
    bool EyebrowChanged,
    bool LipChanged,
    bool InnerMouthChanged,
    bool NostrilChanged,
    bool HairChanged,
    bool BeardChanged,
    bool GlassesChanged,
    bool IsPassed,
    string FailReason,
    IReadOnlyList<string> DebugWarnings);
