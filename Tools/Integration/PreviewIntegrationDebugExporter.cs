using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record IntegrationEventFlowReport(
    string EventName,
    string? ChangedControlId,
    bool MaskDirty,
    bool ShapeDirty,
    bool SkinDirty,
    bool PreviewDirty,
    bool ExportDirty,
    bool SnapshotMaskRebuilt,
    bool ShapeBalanceMapRebuilt,
    bool SkinRetouchExecuted,
    PreviewRenderTier PreviewTier,
    IReadOnlyList<string> DebugWarnings);

public sealed record IntegrationSourceOfTruthReport(
    string SourceFile,
    int OriginalImageWidth,
    int OriginalImageHeight,
    string? SnapshotMaskCacheKey,
    string? ShapeBalanceMapVersion,
    bool PreviewImageUsedAsSource,
    bool SnapshotMaskUsesOriginalCoordinates,
    bool ShapeBalanceUsesOriginalCoordinates,
    bool SkinRetouchUsesBalancedCoordinates,
    bool HardProtectFinalRestoreLast,
    bool ExportRenderUsesOriginalImage);

public sealed record IntegrationPreviewTierReport(
    PreviewRenderTier Tier,
    bool AllowsAnalysis,
    bool AllowsQualityJudgement,
    bool IsExportTier,
    int RenderedWidth,
    int RenderedHeight);

public static class PreviewIntegrationDebugExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static void SavePreviewReport(
        string outputDirectory,
        PhotoItem photo,
        PreviewRenderTier tier,
        IntegrationEventFlowReport eventReport,
        BalancedImageBundle? bundle,
        RetouchStageProcessorOutput? output,
        BitmapSource renderedImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(renderedImage);

        Directory.CreateDirectory(outputDirectory);
        SaveJson(eventReport, Path.Combine(outputDirectory, "debug_integration_event_flow_report.json"));
        SaveJson(photo.PreviewDirtyState, Path.Combine(outputDirectory, "debug_integration_dirty_flags.json"));
        SaveJson(CreateSourceOfTruthReport(photo, bundle, exportRenderUsesOriginalImage: false), Path.Combine(outputDirectory, "debug_integration_source_of_truth.json"));
        SaveJson(CreatePreviewTierReport(tier, renderedImage), Path.Combine(outputDirectory, "debug_integration_preview_tier_report.json"));

        if (bundle is not null)
        {
            SaveBitmap(
                DebugMaskExporter.CreateFinalOverlayPreview(bundle.BalancedImage, bundle.BalancedSnapshot.Masks),
                Path.Combine(outputDirectory, "debug_integration_balanced_mask_alignment.png"));
        }

        if (output is not null)
        {
            SaveBitmap(output.FinalImage, Path.Combine(outputDirectory, CreateSequenceFileName(eventReport)));
            SaveBitmap(
                DebugMaskExporter.CreateMaskOverlayPreview(output.HardProtectFinalImage, output.HardProtectAfterRestoreDiffMask, 255, 40, 40, 0.88),
                Path.Combine(outputDirectory, "debug_integration_hardprotect_final_restore.png"));
        }
    }

    public static void SaveExportReport(
        string outputDirectory,
        PhotoItem photo,
        IntegrationEventFlowReport eventReport,
        BalancedImageBundle? bundle,
        RetouchStageProcessorOutput? output,
        BitmapSource finalImage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(photo);
        ArgumentNullException.ThrowIfNull(finalImage);

        Directory.CreateDirectory(outputDirectory);
        SaveJson(eventReport, Path.Combine(outputDirectory, "debug_integration_export_render_report.json"));
        SaveJson(CreateSourceOfTruthReport(photo, bundle, exportRenderUsesOriginalImage: true), Path.Combine(outputDirectory, "debug_integration_source_of_truth.json"));
        SaveJson(CreatePreviewTierReport(PreviewRenderTier.ExportRender, finalImage), Path.Combine(outputDirectory, "debug_integration_preview_tier_report.json"));

        if (bundle is not null)
        {
            SaveBitmap(
                DebugMaskExporter.CreateFinalOverlayPreview(bundle.BalancedImage, bundle.BalancedSnapshot.Masks),
                Path.Combine(outputDirectory, "debug_integration_balanced_mask_alignment.png"));
        }

        if (output is not null)
        {
            SaveBitmap(
                DebugMaskExporter.CreateMaskOverlayPreview(output.HardProtectFinalImage, output.HardProtectAfterRestoreDiffMask, 255, 40, 40, 0.88),
                Path.Combine(outputDirectory, "debug_integration_hardprotect_final_restore.png"));
        }
    }

    private static IntegrationSourceOfTruthReport CreateSourceOfTruthReport(
        PhotoItem photo,
        BalancedImageBundle? bundle,
        bool exportRenderUsesOriginalImage)
    {
        return new IntegrationSourceOfTruthReport(
            photo.Path,
            photo.BaseImage.PixelWidth,
            photo.BaseImage.PixelHeight,
            bundle?.SourceSnapshot.CacheKey.StableId ?? photo.SnapshotMaskSet?.CacheKey.StableId,
            bundle?.ShapeBalanceMap.ShapeBalanceVersion,
            PreviewImageUsedAsSource: false,
            SnapshotMaskUsesOriginalCoordinates: true,
            ShapeBalanceUsesOriginalCoordinates: true,
            SkinRetouchUsesBalancedCoordinates: bundle is not null,
            HardProtectFinalRestoreLast: true,
            ExportRenderUsesOriginalImage: exportRenderUsesOriginalImage);
    }

    private static IntegrationPreviewTierReport CreatePreviewTierReport(PreviewRenderTier tier, BitmapSource renderedImage)
    {
        PreviewRenderTierPolicy policy = PreviewRenderTierPolicy.For(tier);
        return new IntegrationPreviewTierReport(
            tier,
            policy.IsQualityJudgement && tier != PreviewRenderTier.FastPreview,
            policy.IsQualityJudgement,
            policy.UsesOriginalResolution,
            renderedImage.PixelWidth,
            renderedImage.PixelHeight);
    }

    private static string CreateSequenceFileName(IntegrationEventFlowReport eventReport)
    {
        if (eventReport.ShapeBalanceMapRebuilt && eventReport.SkinRetouchExecuted)
        {
            return "debug_integration_shape_then_skin.png";
        }

        return "debug_integration_skin_then_shape.png";
    }

    private static void SaveJson<T>(T value, string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8);
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        BitmapEncoder encoder = Path.GetExtension(path).Equals(".jpg", StringComparison.OrdinalIgnoreCase)
            ? new JpegBitmapEncoder { QualityLevel = 100 }
            : new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
