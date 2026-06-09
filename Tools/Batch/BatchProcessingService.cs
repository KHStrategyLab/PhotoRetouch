using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public sealed record BatchOptions(
    bool UseCurrentToolset = true,
    string? PresetName = null,
    string? OutputDirectory = null,
    bool PreserveOriginalFiles = true,
    bool AutoRename = true,
    bool StopOnFirstError = false,
    bool SaveSidecarReport = true,
    bool SaveDebugImages = false,
    int MaxParallelCount = 1,
    bool SkipIfNoFaceDetected = true,
    bool ContinueOnWarning = true);

public sealed record BatchRequest(
    IReadOnlyList<string> ImageFileList,
    RetouchPreset? Preset,
    RetouchToolset? CurrentToolset,
    ExportOptions ExportOptions,
    BatchOptions BatchOptions);

public sealed record BatchReport(
    string BatchId,
    DateTime StartedAtUtc,
    DateTime FinishedAtUtc,
    int TotalCount,
    int SuccessCount,
    int FailedCount,
    int WarningCount,
    string? UsedPresetName,
    string OutputDirectory,
    IReadOnlyList<BatchItemReport> ItemReports)
{
    public bool IsCompleted => FailedCount == 0;
}

public sealed record BatchItemReport(
    string SourceFileName,
    string? OutputFileName,
    string Status,
    int RequestedStage,
    int AppliedStage,
    double MaskQualityScore,
    bool StrongRetouchLimited,
    bool FaceDetected,
    double NostrilConfidence,
    bool HardProtectPassed,
    string FailReason,
    IReadOnlyList<string> DebugWarnings);

public sealed class BatchProcessingService
{
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".tif",
        ".tiff",
        ".bmp"
    };

    private readonly SnapshotMaskBuilder _snapshotMaskBuilder;
    private readonly RetouchStageProcessor _retouchStageProcessor;
    private readonly ExportService _exportService;

    public BatchProcessingService()
        : this(new SnapshotMaskBuilder(new StandardMaskWarpEngine()), new RetouchStageProcessor(), new ExportService())
    {
    }

    public BatchProcessingService(
        SnapshotMaskBuilder snapshotMaskBuilder,
        RetouchStageProcessor retouchStageProcessor,
        ExportService exportService)
    {
        _snapshotMaskBuilder = snapshotMaskBuilder;
        _retouchStageProcessor = retouchStageProcessor;
        _exportService = exportService;
    }

    public BatchReport Run(BatchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        DateTime startedAtUtc = DateTime.UtcNow;
        string batchId = startedAtUtc.ToString("yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture);
        string outputDirectory = GetOutputDirectory(request.BatchOptions.OutputDirectory, request.ImageFileList);
        Directory.CreateDirectory(outputDirectory);

        RetouchToolset? toolset = request.Preset?.ToToolset() ?? request.CurrentToolset;
        int requestedStage = Math.Clamp(toolset?.CurrentStage ?? request.Preset?.Stage ?? 5, 1, 10);
        ExportOptions exportOptions = request.ExportOptions with
        {
            OutputDirectory = outputDirectory,
            SaveSidecarReport = request.BatchOptions.SaveSidecarReport,
            OverwritePolicy = request.BatchOptions.AutoRename
                ? ExportOverwritePolicy.AutoRename
                : request.ExportOptions.OverwritePolicy
        };

        List<BatchItemReport> itemReports = new();
        foreach (string path in request.ImageFileList.Where(IsSupportedImagePath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            BatchItemReport itemReport = ProcessItem(path, requestedStage, toolset, exportOptions);
            itemReports.Add(itemReport);
            if (itemReport.Status == "Failed" && request.BatchOptions.StopOnFirstError)
            {
                break;
            }
        }

        DateTime finishedAtUtc = DateTime.UtcNow;
        BatchReport report = new(
            batchId,
            startedAtUtc,
            finishedAtUtc,
            request.ImageFileList.Count,
            itemReports.Count(report => report.Status == "Completed"),
            itemReports.Count(report => report.Status == "Failed"),
            itemReports.Count(report => report.Status == "Warning"),
            request.Preset?.PresetName ?? request.BatchOptions.PresetName,
            outputDirectory,
            itemReports);
        SaveJson(report, Path.Combine(outputDirectory, $"batch_report_{batchId}.json"));
        return report;
    }

    private BatchItemReport ProcessItem(string path, int requestedStage, RetouchToolset? toolset, ExportOptions exportOptions)
    {
        PhotoItem? photo = null;
        try
        {
            photo = PhotoItem.Load(path);
            FaceSnapshotMaskSet snapshot = _snapshotMaskBuilder.GetOrCreate(photo);
            RetouchStageProcessorOutput output = _retouchStageProcessor.Process(
                photo.BaseImage,
                snapshot,
                new RetouchOptions(requestedStage, Toolset: toolset));
            ExportResult exportResult = _exportService.Save(new ExportRequest(
                photo.BaseImage,
                output.FinalImage,
                photo.Path,
                output.Report.RequestedStage,
                output.Report.AppliedStage,
                snapshot.QualityReport,
                toolset,
                exportOptions,
                output.DebugWarnings));
            photo.ClearTransientPreviewCache();
            return new BatchItemReport(
                photo.FileName,
                Path.GetFileName(exportResult.SavedFilePath),
                output.Report.IsStageLimited ? "Warning" : "Completed",
                output.Report.RequestedStage,
                output.Report.AppliedStage,
                output.Report.MaskQualityScore,
                output.Report.IsStageLimited,
                snapshot.QualityReport.FaceQualityScore > 0,
                snapshot.NostrilDetection?.NostrilConfidence ?? snapshot.QualityReport.NostrilMaskQualityScore,
                output.HardProtectRestoreReport.IsHardProtectClean,
                string.Empty,
                output.DebugWarnings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or InvalidOperationException)
        {
            photo?.ClearTransientPreviewCache();
            return new BatchItemReport(
                Path.GetFileName(path),
                null,
                "Failed",
                requestedStage,
                0,
                0,
                false,
                false,
                0,
                false,
                ex.GetType().Name,
                new[] { ex.Message });
        }
    }

    private static string GetOutputDirectory(string? requestedOutputDirectory, IReadOnlyList<string> imageFileList)
    {
        if (!string.IsNullOrWhiteSpace(requestedOutputDirectory))
        {
            return requestedOutputDirectory;
        }

        string? firstDirectory = imageFileList
            .Select(Path.GetDirectoryName)
            .FirstOrDefault(directory => !string.IsNullOrWhiteSpace(directory));
        return Path.Combine(firstDirectory ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "batch_outputs");
    }

    private static bool IsSupportedImagePath(string path)
    {
        return File.Exists(path) && SupportedExtensions.Contains(Path.GetExtension(path));
    }

    private static void SaveJson<T>(T value, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true }), System.Text.Encoding.UTF8);
    }
}
