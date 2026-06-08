using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class StageCompareReportRunner
{
    private static readonly int[] CompareStages = { 1, 5, 10 };

    private readonly SnapshotMaskBuilder _snapshotMaskBuilder;
    private readonly RetouchStageProcessor _retouchStageProcessor;

    public StageCompareReportRunner()
        : this(new SnapshotMaskBuilder(new StandardMaskWarpEngine()), new RetouchStageProcessor())
    {
    }

    public StageCompareReportRunner(SnapshotMaskBuilder snapshotMaskBuilder, RetouchStageProcessor retouchStageProcessor)
    {
        _snapshotMaskBuilder = snapshotMaskBuilder;
        _retouchStageProcessor = retouchStageProcessor;
    }

    public StageCompareRunSummary RunManifest(string projectRootDirectory)
    {
        string manifestPath = Path.Combine(projectRootDirectory, PortraitTestCaseCatalog.DefaultManifestPath);
        IReadOnlyList<PortraitTestCase> cases = PortraitTestCaseCatalog.Load(manifestPath);
        return RunCases(projectRootDirectory, cases);
    }

    public StageCompareRunSummary RunCases(string projectRootDirectory, IReadOnlyList<PortraitTestCase> cases)
    {
        string originalDirectory = Path.Combine(projectRootDirectory, PortraitTestCaseCatalog.OriginalDirectory);
        string stageOutputDirectory = Path.Combine(projectRootDirectory, PortraitTestCaseCatalog.StageOutputDirectory);
        string reportDirectory = Path.Combine(projectRootDirectory, PortraitTestCaseCatalog.ReportDirectory);
        Directory.CreateDirectory(stageOutputDirectory);
        Directory.CreateDirectory(reportDirectory);

        List<StageCompareReport> reports = new();
        foreach (PortraitTestCase testCase in cases)
        {
            reports.Add(RunCase(testCase, originalDirectory, stageOutputDirectory, reportDirectory));
        }

        StageCompareRunSummary summary = new(
            DateTime.UtcNow,
            reports.Count,
            reports.Count(report => report.RunStatus == "Completed"),
            reports.Count(report => report.RunStatus == "Skipped"),
            reports);
        SaveJson(summary, Path.Combine(reportDirectory, "stage_compare_summary.json"));
        return summary;
    }

    private StageCompareReport RunCase(
        PortraitTestCase testCase,
        string originalDirectory,
        string stageOutputDirectory,
        string reportDirectory)
    {
        if (string.IsNullOrWhiteSpace(testCase.FileName))
        {
            return CreateSkippedReport(testCase, "MissingFileName");
        }

        string originalPath = Path.Combine(originalDirectory, testCase.FileName);
        if (!File.Exists(originalPath))
        {
            return CreateSkippedReport(testCase, "OriginalFileNotFound");
        }

        PhotoItem photo = PhotoItem.Load(originalPath);
        FaceSnapshotMaskSet snapshot = _snapshotMaskBuilder.GetOrCreate(photo);
        Dictionary<int, RetouchStageProcessorOutput> outputs = new();
        foreach (int stage in CompareStages)
        {
            outputs[stage] = _retouchStageProcessor.Process(
                photo.BaseImage,
                snapshot,
                new RetouchOptions(stage));
        }

        string testOutputDirectory = Path.Combine(stageOutputDirectory, testCase.TestId);
        Directory.CreateDirectory(testOutputDirectory);
        SaveBitmap(photo.BaseImage, Path.Combine(testOutputDirectory, $"{testCase.TestId}_original.png"));
        foreach (int stage in CompareStages)
        {
            RetouchStageProcessorOutput output = outputs[stage];
            SaveBitmap(output.FinalImage, Path.Combine(testOutputDirectory, $"{testCase.TestId}_stage_{stage}.png"));
            SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.HardProtectAfterRestoreDiffMask), Path.Combine(testOutputDirectory, $"{testCase.TestId}_hardprotect_diff_stage_{stage}.png"));
        }

        SaveBitmap(
            CreateCompareSheet(photo.BaseImage, outputs[1].FinalImage, outputs[5].FinalImage, outputs[10].FinalImage),
            Path.Combine(testOutputDirectory, $"{testCase.TestId}_compare_stage_1_5_10.png"));

        StageCompareReport report = CreateCompletedReport(testCase, photo, snapshot, outputs);
        SaveJson(report, Path.Combine(reportDirectory, $"{testCase.TestId}_stage_report.json"));
        SaveMarkdown(report, Path.Combine(reportDirectory, $"{testCase.TestId}_stage_report.md"));
        return report;
    }

    private static StageCompareReport CreateCompletedReport(
        PortraitTestCase testCase,
        PhotoItem photo,
        FaceSnapshotMaskSet snapshot,
        IReadOnlyDictionary<int, RetouchStageProcessorOutput> outputs)
    {
        StageCompareResult stage1 = CreateResult(outputs[1]);
        StageCompareResult stage5 = CreateResult(outputs[5]);
        StageCompareResult stage10 = CreateResult(outputs[10]);
        bool hardProtectPassed = stage1.HardProtectPassed && stage5.HardProtectPassed && stage10.HardProtectPassed;
        string failReason = hardProtectPassed
            ? GetQualityFailReason(stage5, stage10)
            : "HardProtectBroken";

        return new StageCompareReport(
            testCase.TestId,
            photo.FileName,
            GetTags(testCase),
            "Completed",
            snapshot.QualityReport.Score,
            CompareStages,
            new[] { stage1.AppliedStage, stage5.AppliedStage, stage10.AppliedStage },
            stage1,
            stage5,
            stage10,
            hardProtectPassed,
            !stage10.NostrilChanged,
            !stage10.EyeChanged,
            !stage10.EyebrowChanged,
            !stage10.LipChanged,
            !stage10.HairChanged,
            !stage10.BeardChanged,
            !stage10.GlassesChanged,
            DescribeSkinSmooth(stage5, stage10),
            DescribeBlemish(stage5, stage10),
            DescribeWrinkle(stage5, stage10),
            DescribeToneEven(stage5, stage10),
            DescribeTextureRestore(stage5, stage10),
            Math.Max(stage1.PlasticSkinRiskScore, Math.Max(stage5.PlasticSkinRiskScore, stage10.PlasticSkinRiskScore)),
            failReason,
            testCase.Notes);
    }

    private static StageCompareReport CreateSkippedReport(PortraitTestCase testCase, string reason)
    {
        StageCompareResult empty = StageCompareResult.Empty;
        return new StageCompareReport(
            testCase.TestId,
            testCase.FileName,
            GetTags(testCase),
            "Skipped",
            0,
            CompareStages,
            Array.Empty<int>(),
            empty,
            empty,
            empty,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            false,
            "Skipped",
            "Skipped",
            "Skipped",
            "Skipped",
            "Skipped",
            0,
            reason,
            testCase.Notes);
    }

    private static StageCompareResult CreateResult(RetouchStageProcessorOutput output)
    {
        HardProtectRestoreReport hardProtect = output.HardProtectRestoreReport;
        return new StageCompareResult(
            output.Report.RequestedStage,
            output.Report.AppliedStage,
            output.Report.SkinSmoothAmount,
            output.Report.BlemishReduceAmount,
            output.Report.WrinkleReduceAmount,
            output.Report.ToneEvenAmount,
            output.Report.TextureRestoreAmount,
            output.Report.PlasticSkinRiskScore,
            hardProtect.ChangedPixelAfterRestoreCount,
            hardProtect.IsHardProtectClean,
            hardProtect.EyeChanged,
            hardProtect.EyebrowChanged,
            hardProtect.LipChanged,
            hardProtect.InnerMouthChanged,
            hardProtect.NostrilChanged,
            hardProtect.HairChanged,
            hardProtect.BeardChanged,
            hardProtect.GlassesChanged,
            output.Report.BlemishCandidateCount,
            output.Report.BlemishAppliedCount,
            output.Report.WrinkleAppliedCount);
    }

    private static IReadOnlyList<string> GetTags(PortraitTestCase testCase)
    {
        return testCase.SkinConditionTags
            .Concat(testCase.FaceFeatureTags)
            .Concat(testCase.LightingTags)
            .Concat(testCase.AccessoryTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string GetQualityFailReason(StageCompareResult stage5, StageCompareResult stage10)
    {
        if (stage10.PlasticSkinRiskScore > 0.72)
        {
            return "PlasticSkin";
        }

        if (stage5.AppliedStage < 5 || stage10.AppliedStage < 10)
        {
            return "StageLimitedByMaskQuality";
        }

        return string.Empty;
    }

    private static string DescribeSkinSmooth(StageCompareResult stage5, StageCompareResult stage10)
    {
        return stage10.SkinSmoothAmount > stage5.SkinSmoothAmount ? "Stage strength increases" : "NeedsReview";
    }

    private static string DescribeBlemish(StageCompareResult stage5, StageCompareResult stage10)
    {
        return stage10.BlemishAppliedCount >= stage5.BlemishAppliedCount ? "Candidate/apply counts recorded" : "NeedsReview";
    }

    private static string DescribeWrinkle(StageCompareResult stage5, StageCompareResult stage10)
    {
        return stage10.WrinkleAppliedCount >= stage5.WrinkleAppliedCount ? "Wrinkle apply counts recorded" : "NeedsReview";
    }

    private static string DescribeToneEven(StageCompareResult stage5, StageCompareResult stage10)
    {
        return stage10.ToneEvenAmount >= stage5.ToneEvenAmount ? "Stage tone amount recorded" : "NeedsReview";
    }

    private static string DescribeTextureRestore(StageCompareResult stage5, StageCompareResult stage10)
    {
        return stage10.TextureRestoreAmount > 0 && stage5.TextureRestoreAmount > 0 ? "Texture restore active" : "NeedsReview";
    }

    private static BitmapSource CreateCompareSheet(BitmapSource original, BitmapSource stage1, BitmapSource stage5, BitmapSource stage10)
    {
        BitmapSource[] sources = { ToBgra32(original), ToBgra32(stage1), ToBgra32(stage5), ToBgra32(stage10) };
        int width = sources[0].PixelWidth;
        int height = sources[0].PixelHeight;
        int outputWidth = width * sources.Length;
        int sourceStride = width * 4;
        int outputStride = outputWidth * 4;
        byte[] outputPixels = new byte[outputStride * height];

        for (int sourceIndex = 0; sourceIndex < sources.Length; sourceIndex++)
        {
            byte[] sourcePixels = new byte[sourceStride * height];
            sources[sourceIndex].CopyPixels(sourcePixels, sourceStride, 0);
            for (int y = 0; y < height; y++)
            {
                Buffer.BlockCopy(sourcePixels, y * sourceStride, outputPixels, y * outputStride + sourceIndex * sourceStride, sourceStride);
            }

            if (sourceIndex > 0)
            {
                DrawDivider(outputPixels, outputWidth, height, sourceIndex * width);
            }
        }

        BitmapSource bitmap = BitmapSource.Create(outputWidth, height, 96, 96, PixelFormats.Bgra32, null, outputPixels, outputStride);
        bitmap.Freeze();
        return bitmap;
    }

    private static BitmapSource ToBgra32(BitmapSource source)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawDivider(byte[] pixels, int width, int height, int x)
    {
        int stride = width * 4;
        for (int y = 0; y < height; y++)
        {
            for (int offset = -1; offset <= 1; offset++)
            {
                int dividerX = x + offset;
                if (dividerX < 0 || dividerX >= width)
                {
                    continue;
                }

                int index = y * stride + dividerX * 4;
                pixels[index] = 255;
                pixels[index + 1] = 255;
                pixels[index + 2] = 255;
                pixels[index + 3] = 255;
            }
        }
    }

    private static void SaveMarkdown(StageCompareReport report, string path)
    {
        string[] lines =
        {
            "# Stage Compare Report",
            string.Empty,
            "- TestId: " + report.TestId,
            "- FileName: " + report.FileName,
            "- RunStatus: " + report.RunStatus,
            "- MaskQualityScore: " + report.MaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "- HardProtectPassed: " + report.HardProtectPassed,
            "- MainFailReason: " + report.MainFailReason,
            "- PlasticSkinRiskScore: " + report.PlasticSkinRiskScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "- Tags: " + string.Join(", ", report.Tags),
            string.Empty,
            "## Stage Results",
            FormatStage(report.Stage1Result),
            FormatStage(report.Stage5Result),
            FormatStage(report.Stage10Result)
        };
        File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    }

    private static string FormatStage(StageCompareResult result)
    {
        return $"- Stage {result.RequestedStage}: applied={result.AppliedStage}, hardProtect={result.HardProtectPassed}, diff={result.HardProtectChangedPixelCount}, plastic={result.PlasticSkinRiskScore:0.###}";
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

public sealed record StageCompareRunSummary(
    DateTime CreatedAtUtc,
    int ReportCount,
    int CompletedCount,
    int SkippedCount,
    IReadOnlyList<StageCompareReport> Reports);

public sealed record StageCompareReport(
    string TestId,
    string FileName,
    IReadOnlyList<string> Tags,
    string RunStatus,
    double MaskQualityScore,
    IReadOnlyList<int> RequestedStages,
    IReadOnlyList<int> AppliedStages,
    StageCompareResult Stage1Result,
    StageCompareResult Stage5Result,
    StageCompareResult Stage10Result,
    bool HardProtectPassed,
    bool NostrilPassed,
    bool EyePassed,
    bool EyebrowPassed,
    bool LipPassed,
    bool HairPassed,
    bool BeardPassed,
    bool GlassesPassed,
    string SkinSmoothResult,
    string BlemishResult,
    string WrinkleResult,
    string ToneEvenResult,
    string TextureRestoreResult,
    double PlasticSkinRiskScore,
    string MainFailReason,
    string Notes);

public sealed record StageCompareResult(
    int RequestedStage,
    int AppliedStage,
    double SkinSmoothAmount,
    double BlemishReduceAmount,
    double WrinkleReduceAmount,
    double ToneEvenAmount,
    double TextureRestoreAmount,
    double PlasticSkinRiskScore,
    int HardProtectChangedPixelCount,
    bool HardProtectPassed,
    bool EyeChanged,
    bool EyebrowChanged,
    bool LipChanged,
    bool InnerMouthChanged,
    bool NostrilChanged,
    bool HairChanged,
    bool BeardChanged,
    bool GlassesChanged,
    int BlemishCandidateCount,
    int BlemishAppliedCount,
    int WrinkleAppliedCount)
{
    public static StageCompareResult Empty { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        0,
        0,
        0);
}

