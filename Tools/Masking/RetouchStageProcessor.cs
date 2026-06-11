using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class RetouchStageProcessor
{
    public RetouchAnalysisCacheStatus AnalysisCacheStatus => new(0, 0, 0);

    public void ClearAnalysisCaches()
    {
    }

    public RetouchStageProcessorOutput Process(BitmapSource originalImage, FaceSnapshotMaskSet snapshot, RetouchOptions options)
    {
        ArgumentNullException.ThrowIfNull(originalImage);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        DateTime pipelineStartedAtUtc = DateTime.UtcNow;
        BitmapSource bitmap = originalImage.Format == PixelFormats.Bgra32
            ? originalImage
            : new FormatConvertedBitmap(originalImage, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        AppliedRetouchOptions appliedOptions = AppliedRetouchOptions.Create(snapshot.QualityReport, options);
        int requestedStage = appliedOptions.RequestedStage;
        int appliedStage = appliedOptions.AppliedStage;
        StagePreset preset = appliedOptions.StagePreset;
        FaceMaskSet masks = snapshot.Masks;

        MaskPlane empty = MaskPlane.Empty(bitmap.PixelWidth, bitmap.PixelHeight);
        WrinkleMaskSet wrinkleMasks = CreateEmptyWrinkleMaskSet(empty);
        List<string> warnings = new(snapshot.QualityReport.Warnings)
        {
            "guide_only_pipeline",
            "retouch_filters_disabled",
            "feature_protection_pipeline_removed"
        };

        RetouchProcessReport report = new(
            requestedStage,
            appliedStage,
            Math.Clamp(snapshot.QualityReport.MaxAllowedStage, 1, 10),
            preset.SkinSmoothAmount,
            0,
            0,
            0,
            0,
            preset.DetailRestoreAmount,
            false,
            0,
            0,
            snapshot.QualityReport.Score,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            true,
            warnings);

        DateTime pipelineFinishedAtUtc = DateTime.UtcNow;
        PipelineDebugReport pipelineReport = new(
            snapshot.ImageId,
            snapshot.CacheKey.StableId,
            requestedStage,
            appliedStage,
            pipelineStartedAtUtc,
            pipelineFinishedAtUtc,
            false,
            true,
            true,
            new[]
            {
                "GuideDetection",
                "GuideOverlay",
                "PreviewPassthrough"
            },
            warnings,
            Array.Empty<string>());

        return new RetouchStageProcessorOutput(
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            empty,
            empty,
            new BlemishProcessReport(0, 0, 0, 0, 0, warnings),
            bitmap,
            wrinkleMasks,
            empty,
            empty,
            WrinkleProcessReport.Empty(warnings),
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            empty,
            empty,
            empty,
            new TextureRestoreProcessReport(appliedStage, 0, 0, 0, 0, 0, warnings),
            bitmap,
            empty,
            empty,
            new HardProtectRestoreReport(appliedStage, 0, 0, 0, 0, false, false, false, false, false, false, false, false, true, warnings),
            appliedStage,
            appliedOptions,
            report,
            pipelineReport,
            warnings);
    }

    private static WrinkleMaskSet CreateEmptyWrinkleMaskSet(MaskPlane empty)
    {
        return new WrinkleMaskSet(
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty);
    }
}

public sealed record RetouchAnalysisCacheStatus(
    int BlemishAnalysisCacheCount,
    int WrinkleAnalysisCacheCount,
    int TextureRestoreAnalysisCacheCount)
{
    public int TotalCount => BlemishAnalysisCacheCount + WrinkleAnalysisCacheCount + TextureRestoreAnalysisCacheCount;
}
