using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public enum ExportFormat
{
    Jpg,
    Png
}

public enum ExportOverwritePolicy
{
    NeverOverwrite,
    AskBeforeOverwrite,
    AutoRename
}

public sealed record ExportOptions(
    ExportFormat ExportFormat = ExportFormat.Jpg,
    int JpegQuality = 100,
    int PngCompressionLevel = 6,
    bool SaveSidecarReport = true,
    bool PreserveOriginalFile = true,
    bool AutoFileName = true,
    string? OutputDirectory = null,
    string FileNameSuffix = "_-_1",
    ExportOverwritePolicy OverwritePolicy = ExportOverwritePolicy.AutoRename);

public sealed record ExportRequest(
    BitmapSource OriginalImage,
    BitmapSource FinalImage,
    string SourceFilePath,
    int RequestedStage,
    int AppliedStage,
    MaskQualityReport? MaskQualityReport,
    RetouchToolset? Toolset,
    ExportOptions Options,
    IReadOnlyList<string>? DebugWarnings = null);

public sealed record ExportReport(
    string SourceFileName,
    string OutputFileName,
    ExportFormat ExportFormat,
    int JpegQuality,
    int ImageWidth,
    int ImageHeight,
    int RequestedStage,
    int AppliedStage,
    double? MaskQualityScore,
    bool StrongRetouchLimited,
    string PipelineVersion,
    DateTime CreatedAtUtc,
    IReadOnlyList<string> DebugWarnings);

public sealed record ExportResult(string SavedFilePath, string? SidecarReportPath, ExportReport Report);

public sealed class ExportService
{
    public const string PipelineVersion = "v1";

    public ExportResult Save(ExportRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.FinalImage);
        ExportOptions options = request.Options with
        {
            JpegQuality = Math.Clamp(request.Options.JpegQuality, 1, 100)
        };
        string savePath = CreateOutputPath(request.SourceFilePath, options);
        SaveBitmap(request.FinalImage, savePath, options);

        ExportReport report = new(
            Path.GetFileName(request.SourceFilePath),
            Path.GetFileName(savePath),
            options.ExportFormat,
            options.JpegQuality,
            request.FinalImage.PixelWidth,
            request.FinalImage.PixelHeight,
            request.RequestedStage,
            request.AppliedStage,
            request.MaskQualityReport?.Score,
            request.AppliedStage < request.RequestedStage,
            PipelineVersion,
            DateTime.UtcNow,
            request.DebugWarnings ?? Array.Empty<string>());
        string? reportPath = null;
        if (options.SaveSidecarReport)
        {
            reportPath = Path.ChangeExtension(savePath, Path.GetExtension(savePath) + ".report.json");
            File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), Encoding.UTF8);
        }

        return new ExportResult(savePath, reportPath, report);
    }

    public static string CreateOutputPath(string sourceFilePath, ExportOptions options)
    {
        string directory = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? Path.GetDirectoryName(sourceFilePath) ?? Environment.GetFolderPath(Environment.SpecialFolder.MyPictures)
            : options.OutputDirectory;
        Directory.CreateDirectory(directory);

        string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
        string extension = options.ExportFormat == ExportFormat.Png ? ".png" : ".jpg";
        string candidate = Path.Combine(directory, baseName + options.FileNameSuffix + extension);
        if (options.PreserveOriginalFile && string.Equals(candidate, sourceFilePath, StringComparison.OrdinalIgnoreCase))
        {
            candidate = Path.Combine(directory, baseName + "_retouched" + extension);
        }

        return options.OverwritePolicy == ExportOverwritePolicy.AutoRename
            ? CreateAutoRenamePath(candidate)
            : candidate;
    }

    private static string CreateAutoRenamePath(string path)
    {
        if (!File.Exists(path))
        {
            return path;
        }

        string directory = Path.GetDirectoryName(path) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(path);
        string extension = Path.GetExtension(path);
        for (int index = 1; index < 10000; index++)
        {
            string candidate = Path.Combine(directory, $"{baseName}_{index:000}{extension}");
            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new IOException("저장 파일 이름을 만들 수 없어.");
    }

    private static void SaveBitmap(BitmapSource image, string path, ExportOptions options)
    {
        BitmapEncoder encoder = options.ExportFormat == ExportFormat.Png
            ? new PngBitmapEncoder()
            : new JpegBitmapEncoder { QualityLevel = options.JpegQuality };
        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
