using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public static class RetouchDebugExporter
{
    public static void SaveAll(
        BitmapSource original,
        FaceSnapshotMaskSet snapshot,
        RetouchStageProcessorOutput output,
        string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(output);

        Directory.CreateDirectory(outputDirectory);

        SaveBitmap(original, Path.Combine(outputDirectory, "debug_pipeline_original.png"));
        SaveBitmap(DebugMaskExporter.CreateFinalOverlayPreview(original, snapshot.Masks), Path.Combine(outputDirectory, "debug_pipeline_snapshot_mask_overlay.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.HardProtectMask), Path.Combine(outputDirectory, "debug_pipeline_hard_protect.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.SoftProtectMask), Path.Combine(outputDirectory, "debug_pipeline_soft_protect.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.RetouchAllowMask), Path.Combine(outputDirectory, "debug_pipeline_retouch_allow.png"));
        SaveBitmap(output.SmoothBaseImage, Path.Combine(outputDirectory, "debug_pipeline_after_skin_smooth.png"));
        SaveBitmap(output.BlemishReducedImage, Path.Combine(outputDirectory, "debug_pipeline_after_blemish.png"));
        SaveBitmap(output.WrinkleReducedImage, Path.Combine(outputDirectory, "debug_pipeline_after_wrinkle.png"));
        SaveBitmap(output.ToneEvenImage, Path.Combine(outputDirectory, "debug_pipeline_after_tone_even.png"));
        SaveBitmap(output.FinalTextureRestoredImage, Path.Combine(outputDirectory, "debug_pipeline_after_texture_restore.png"));
        SaveBitmap(output.HardProtectFinalImage, Path.Combine(outputDirectory, "debug_pipeline_final_after_hardprotect_restore.png"));

        SaveBitmap(CreateStageInfoImage(output.Report.RequestedStage, 10), Path.Combine(outputDirectory, "debug_stage_requested.png"));
        SaveBitmap(CreateStageInfoImage(output.Report.AppliedStage, output.Report.MaxAllowedStage), Path.Combine(outputDirectory, "debug_stage_applied.png"));
        SaveBitmap(output.SmoothBaseImage, Path.Combine(outputDirectory, "debug_smooth_image.png"));
        SaveBitmap(output.SmoothBaseImage, Path.Combine(outputDirectory, "debug_smooth_base.png"));
        SaveBitmap(output.DetailLayerImage, Path.Combine(outputDirectory, "debug_detail_layer.png"));
        SaveBitmap(output.TextureRestoredImage, Path.Combine(outputDirectory, "debug_texture_restored_initial.png"));
        SaveBitmap(output.TextureRestoredImage, Path.Combine(outputDirectory, "debug_texture_restored.png"));
        SaveBitmap(output.RetouchAllowAppliedImage, Path.Combine(outputDirectory, "debug_retouch_allow_applied.png"));
        SaveBitmap(output.SoftProtectAppliedImage, Path.Combine(outputDirectory, "debug_soft_protect_applied.png"));
        SaveBitmap(output.HardProtectRestoredImage, Path.Combine(outputDirectory, "debug_hard_protect_restored.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.BlemishCandidateMask), Path.Combine(outputDirectory, "debug_blemish_candidates.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.BlemishCandidateMask), Path.Combine(outputDirectory, "debug_blemish_components.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.BlemishMask), Path.Combine(outputDirectory, "debug_blemish_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(CreateBlemishSearchMask(snapshot.Masks)), Path.Combine(outputDirectory, "debug_blemish_search_mask.png"));
        SaveBitmap(output.BlemishReducedImage, Path.Combine(outputDirectory, "debug_blemish_corrected.png"));
        SaveBitmap(CreateBeforeAfterSplit(output.HardProtectRestoredImage, output.BlemishReducedImage), Path.Combine(outputDirectory, "debug_blemish_before_after.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(CreateWrinkleSearchMask(snapshot.Masks)), Path.Combine(outputDirectory, "debug_wrinkle_search_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleCandidateMask), Path.Combine(outputDirectory, "debug_wrinkle_candidates.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleCandidateMask), Path.Combine(outputDirectory, "debug_wrinkle_components.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.UnderEyeWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_under_eye_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.GlabellaWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_glabella_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.ForeheadWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_forehead_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.NasolabialFoldMask), Path.Combine(outputDirectory, "debug_wrinkle_nasolabial_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.MouthCornerWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_mouth_corner_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.NeckWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_neck_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.NoseShadowWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_nose_shadow_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleMaskSet.CombinedWrinkleMask), Path.Combine(outputDirectory, "debug_wrinkle_combined_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.WrinkleAppliedMask), Path.Combine(outputDirectory, "debug_wrinkle_applied_mask.png"));
        SaveBitmap(output.WrinkleReducedImage, Path.Combine(outputDirectory, "debug_wrinkle_corrected.png"));
        SaveBitmap(CreateBeforeAfterSplit(output.BlemishReducedImage, output.WrinkleReducedImage), Path.Combine(outputDirectory, "debug_wrinkle_before_after.png"));
        SaveBitmap(output.FinalTextureBlurOriginalImage, Path.Combine(outputDirectory, "debug_texture_blur_original.png"));
        SaveBitmap(output.FinalTextureDetailLayerImage, Path.Combine(outputDirectory, "debug_texture_detail_layer.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.TextureRestoreMask), Path.Combine(outputDirectory, "debug_texture_restore_mask.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.TextureRestoreStrengthMap), Path.Combine(outputDirectory, "debug_texture_restore_strength_map.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.PlasticSkinRiskMap), Path.Combine(outputDirectory, "debug_plastic_skin_risk_map.png"));
        SaveBitmap(output.FinalTextureRestoredImage, Path.Combine(outputDirectory, "debug_texture_restored_image.png"));
        SaveBitmap(CreateBeforeAfterSplit(output.ToneEvenImage, output.FinalTextureRestoredImage), Path.Combine(outputDirectory, "debug_texture_before_after.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.HardProtectMask), Path.Combine(outputDirectory, "debug_hardprotect_mask.png"));
        SaveBitmap(output.FinalTextureRestoredImage, Path.Combine(outputDirectory, "debug_before_hardprotect_restore.png"));
        SaveBitmap(output.HardProtectFinalImage, Path.Combine(outputDirectory, "debug_after_hardprotect_restore.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.HardProtectBeforeRestoreDiffMask), Path.Combine(outputDirectory, "debug_hardprotect_diff_before.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(output.HardProtectAfterRestoreDiffMask), Path.Combine(outputDirectory, "debug_hardprotect_diff_after.png"));
        SaveBitmap(CreatePartRestoreCheck(original, output.HardProtectFinalImage, snapshot.Masks.EyeMask), Path.Combine(outputDirectory, "debug_eye_restore_check.png"));
        SaveBitmap(CreatePartRestoreCheck(original, output.HardProtectFinalImage, snapshot.Masks.LipMask), Path.Combine(outputDirectory, "debug_lip_restore_check.png"));
        SaveBitmap(CreatePartRestoreCheck(original, output.HardProtectFinalImage, snapshot.Masks.NostrilMask), Path.Combine(outputDirectory, "debug_nostril_restore_check.png"));
        SaveBitmap(CreatePartRestoreCheck(original, output.HardProtectFinalImage, snapshot.Masks.HairMask), Path.Combine(outputDirectory, "debug_hair_restore_check.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskPreview(snapshot.Masks.FinalOverlayMask), Path.Combine(outputDirectory, "debug_final_retouch_mask.png"));
        SaveBitmap(output.FinalImage, Path.Combine(outputDirectory, $"debug_final_output_stage_{output.Report.AppliedStage}.png"));

        foreach (int stage in new[] { 1, 3, 5, 7, 10 })
        {
            RetouchStageProcessor stageProcessor = new();
            RetouchStageProcessorOutput stageOutput = stageProcessor.Process(
                original,
                snapshot,
                new RetouchOptions(stage));
            SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_final_output_stage_{stage}.png"));
            SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_final_stage_{stage}.png"));
            SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_final_after_blemish_stage_{stage}.png"));
            SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_final_after_wrinkle_stage_{stage}.png"));
            SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_final_after_texture_stage_{stage}.png"));
            if (stage is 1 or 5 or 10)
            {
                SaveBitmap(stageOutput.HardProtectFinalImage, Path.Combine(outputDirectory, $"debug_final_stage_{stage}_hardprotect_check.png"));
                SaveBitmap(stageOutput.FinalImage, Path.Combine(outputDirectory, $"debug_pipeline_stage_{stage}_final.png"));
            }
        }

        SaveBitmap(CreateCompareOverlay(original, output.FinalImage, snapshot.Masks.HardProtectMask), Path.Combine(outputDirectory, "debug_hard_protect_compare.png"));
        SaveBitmap(CreateCompareOverlay(original, output.FinalImage, snapshot.Masks.SoftProtectMask), Path.Combine(outputDirectory, "debug_soft_protect_compare.png"));
        SaveBitmap(CreateCompareOverlay(original, output.FinalImage, snapshot.Masks.RetouchAllowMask), Path.Combine(outputDirectory, "debug_retouch_allow_compare.png"));
        SaveReport(output.Report, Path.Combine(outputDirectory, "debug_retouch_report.txt"));
        SavePipelineReport(output.PipelineReport, Path.Combine(outputDirectory, "debug_pipeline_report.txt"));
    }

    private static BitmapSource CreateStageInfoImage(int stage, int limit)
    {
        int width = 320;
        int height = 80;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        double stageAmount = Math.Clamp(stage / 10d, 0, 1);
        double limitAmount = Math.Clamp(limit / 10d, 0, 1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                bool stageBar = y >= 18 && y <= 34 && x < width * stageAmount;
                bool limitBar = y >= 48 && y <= 64 && x < width * limitAmount;
                byte value = (byte)(stageBar ? 220 : limitBar ? 130 : 35);
                pixels[index] = value;
                pixels[index + 1] = value;
                pixels[index + 2] = value;
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static BitmapSource CreateCompareOverlay(BitmapSource original, BitmapSource finalImage, MaskPlane mask)
    {
        BitmapSource originalBgra = original.Format == PixelFormats.Bgra32
            ? original
            : new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        BitmapSource finalBgra = finalImage.Format == PixelFormats.Bgra32
            ? finalImage
            : new FormatConvertedBitmap(finalImage, PixelFormats.Bgra32, null, 0);
        originalBgra.Freeze();
        finalBgra.Freeze();

        int width = originalBgra.PixelWidth;
        int height = originalBgra.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = new byte[stride * height];
        byte[] finalPixels = new byte[stride * height];
        byte[] outputPixels = new byte[stride * height];
        originalBgra.CopyPixels(originalPixels, stride, 0);
        finalBgra.CopyPixels(finalPixels, stride, 0);
        Buffer.BlockCopy(finalPixels, 0, outputPixels, 0, finalPixels.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double difference =
                    Math.Abs(originalPixels[index] - finalPixels[index]) +
                    Math.Abs(originalPixels[index + 1] - finalPixels[index + 1]) +
                    Math.Abs(originalPixels[index + 2] - finalPixels[index + 2]);
                double amount = Math.Clamp(mask[x, y] * 0.55 + difference / 765d * 0.75, 0, 1);
                outputPixels[index] = Blend(outputPixels[index], 40, amount);
                outputPixels[index + 1] = Blend(outputPixels[index + 1], 210, amount);
                outputPixels[index + 2] = Blend(outputPixels[index + 2], 255, amount);
                outputPixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, outputPixels);
    }

    private static void SaveReport(RetouchProcessReport report, string path)
    {
        string[] lines =
        {
            "RetouchProcessReport",
            "RequestedStage: " + report.RequestedStage,
            "AppliedStage: " + report.AppliedStage,
            "MaxAllowedStage: " + report.MaxAllowedStage,
            "SkinSmoothAmount: " + report.SkinSmoothAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "BlemishReduceAmount: " + report.BlemishReduceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "WrinkleReduceAmount: " + report.WrinkleReduceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "ToneEvenAmount: " + report.ToneEvenAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "TextureRestoreAmount: " + report.TextureRestoreAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "DetailPreserveAmount: " + report.DetailPreserveAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "SoftProtectOpacity: " + report.SoftProtectOpacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "RetouchAllowOpacity: " + report.RetouchAllowOpacity.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "MaskQualityScore: " + report.MaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "BlemishCandidateCount: " + report.BlemishCandidateCount,
            "BlemishAppliedCount: " + report.BlemishAppliedCount,
            "BlemishAverageCorrectionStrength: " + report.BlemishAverageCorrectionStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "WrinkleAppliedCount: " + report.WrinkleAppliedCount,
            "WrinkleAverageCorrectionStrength: " + report.WrinkleAverageCorrectionStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "TextureRetouchAllowAmount: " + report.TextureRetouchAllowAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "TextureSoftProtectAmount: " + report.TextureSoftProtectAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "PlasticSkinRiskScore: " + report.PlasticSkinRiskScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "HardProtectChangedBeforeRestoreCount: " + report.HardProtectChangedBeforeRestoreCount,
            "HardProtectChangedAfterRestoreCount: " + report.HardProtectChangedAfterRestoreCount,
            "IsHardProtectClean: " + report.IsHardProtectClean,
            "StageLimited: " + report.IsStageLimited,
            "Warnings:",
            string.Join(Environment.NewLine, report.DebugWarnings.Select(warning => "- " + warning))
        };
        File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    }

    private static void SavePipelineReport(PipelineDebugReport report, string path)
    {
        string[] lines =
        {
            "PipelineDebugReport",
            "ImageId: " + report.ImageId,
            "SnapshotMaskCacheKey: " + report.SnapshotMaskCacheKey,
            "RequestedStage: " + report.RequestedStage,
            "AppliedStage: " + report.AppliedStage,
            "PipelineStartedAtUtc: " + report.PipelineStartedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "PipelineFinishedAtUtc: " + report.PipelineFinishedAtUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            "DurationMilliseconds: " + report.DurationMilliseconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "AnalysisExecuted: " + report.AnalysisExecuted,
            "SnapshotMaskReused: " + report.SnapshotMaskReused,
            "QualityReportReused: " + report.QualityReportReused,
            "FiltersExecuted:",
            string.Join(Environment.NewLine, report.FiltersExecuted.Select(filter => "- " + filter)),
            "Warnings:",
            string.Join(Environment.NewLine, report.Warnings.Select(warning => "- " + warning)),
            "Errors:",
            string.Join(Environment.NewLine, report.Errors.Select(error => "- " + error))
        };
        File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    }

    private static MaskPlane CreateBlemishSearchMask(FaceMaskSet masks)
    {
        MaskPlane searchMask = MaskPlane.Subtract(
            MaskPlane.Union(masks.RetouchAllowMask, MaskPlane.Multiply(masks.SoftProtectMask, 0.12)),
            masks.HardProtectMask);
        return searchMask;
    }

    private static MaskPlane CreateWrinkleSearchMask(FaceMaskSet masks)
    {
        return MaskPlane.Subtract(
            MaskPlane.Union(MaskPlane.Multiply(masks.SoftProtectMask, 0.92), MaskPlane.Multiply(masks.RetouchAllowMask, 0.32)),
            masks.HardProtectMask);
    }

    private static BitmapSource CreateBeforeAfterSplit(BitmapSource before, BitmapSource after)
    {
        BitmapSource beforeBgra = before.Format == PixelFormats.Bgra32
            ? before
            : new FormatConvertedBitmap(before, PixelFormats.Bgra32, null, 0);
        BitmapSource afterBgra = after.Format == PixelFormats.Bgra32
            ? after
            : new FormatConvertedBitmap(after, PixelFormats.Bgra32, null, 0);
        beforeBgra.Freeze();
        afterBgra.Freeze();

        int width = beforeBgra.PixelWidth;
        int height = beforeBgra.PixelHeight;
        int stride = width * 4;
        byte[] beforePixels = new byte[stride * height];
        byte[] afterPixels = new byte[stride * height];
        byte[] outputPixels = new byte[stride * height];
        beforeBgra.CopyPixels(beforePixels, stride, 0);
        afterBgra.CopyPixels(afterPixels, stride, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                if (x < width / 2)
                {
                    outputPixels[index] = beforePixels[index];
                    outputPixels[index + 1] = beforePixels[index + 1];
                    outputPixels[index + 2] = beforePixels[index + 2];
                    outputPixels[index + 3] = beforePixels[index + 3];
                }
                else
                {
                    outputPixels[index] = afterPixels[index];
                    outputPixels[index + 1] = afterPixels[index + 1];
                    outputPixels[index + 2] = afterPixels[index + 2];
                    outputPixels[index + 3] = afterPixels[index + 3];
                }

                if (Math.Abs(x - width / 2) <= 1)
                {
                    outputPixels[index] = 255;
                    outputPixels[index + 1] = 255;
                    outputPixels[index + 2] = 255;
                    outputPixels[index + 3] = 255;
                }
            }
        }

        return CreateBitmap(width, height, outputPixels);
    }

    private static BitmapSource CreatePartRestoreCheck(BitmapSource original, BitmapSource finalImage, MaskPlane partMask)
    {
        BitmapSource originalBgra = original.Format == PixelFormats.Bgra32
            ? original
            : new FormatConvertedBitmap(original, PixelFormats.Bgra32, null, 0);
        BitmapSource finalBgra = finalImage.Format == PixelFormats.Bgra32
            ? finalImage
            : new FormatConvertedBitmap(finalImage, PixelFormats.Bgra32, null, 0);
        originalBgra.Freeze();
        finalBgra.Freeze();

        int width = originalBgra.PixelWidth;
        int height = originalBgra.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = new byte[stride * height];
        byte[] finalPixels = new byte[stride * height];
        byte[] outputPixels = new byte[stride * height];
        originalBgra.CopyPixels(originalPixels, stride, 0);
        finalBgra.CopyPixels(finalPixels, stride, 0);
        Buffer.BlockCopy(finalPixels, 0, outputPixels, 0, finalPixels.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double mask = partMask[x, y];
                if (mask <= 0)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                double difference =
                    Math.Abs(originalPixels[index] - finalPixels[index]) +
                    Math.Abs(originalPixels[index + 1] - finalPixels[index + 1]) +
                    Math.Abs(originalPixels[index + 2] - finalPixels[index + 2]);
                double amount = Math.Clamp(mask * 0.35 + difference / 255d, 0, 1);
                outputPixels[index] = Blend(outputPixels[index], (byte)(difference > 7.5 ? 20 : 50), amount);
                outputPixels[index + 1] = Blend(outputPixels[index + 1], (byte)(difference > 7.5 ? 40 : 210), amount);
                outputPixels[index + 2] = Blend(outputPixels[index + 2], (byte)(difference > 7.5 ? 255 : 70), amount);
                outputPixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, outputPixels);
    }

    private static byte Blend(byte source, byte target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
