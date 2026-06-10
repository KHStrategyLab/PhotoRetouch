using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class RetouchStageProcessor
{
    private readonly IBlemishReduceFilter _blemishReduceFilter = new BlemishReduceFilter();
    private readonly IWrinkleSoftReduceFilter _wrinkleSoftReduceFilter = new WrinkleSoftReduceFilter();
    private readonly ITextureRestoreFilter _textureRestoreFilter = new TextureRestoreFilter();
    private readonly IHardProtectFinalRestoreFilter _hardProtectFinalRestoreFilter = new HardProtectFinalRestoreFilter();

    public RetouchAnalysisCacheStatus AnalysisCacheStatus => new(
        _blemishReduceFilter.AnalysisCacheCount,
        _wrinkleSoftReduceFilter.AnalysisCacheCount,
        _textureRestoreFilter.AnalysisCacheCount);

    public void ClearAnalysisCaches()
    {
        _blemishReduceFilter.ClearAnalysisCache();
        _wrinkleSoftReduceFilter.ClearAnalysisCache();
        _textureRestoreFilter.ClearAnalysisCache();
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

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        FaceMaskSet masks = snapshot.Masks;
        ValidateMaskSize(masks, width, height);

        AppliedRetouchOptions appliedOptions = AppliedRetouchOptions.Create(snapshot.QualityReport, options);
        int requestedStage = appliedOptions.RequestedStage;
        int maxAllowedStage = Math.Clamp(snapshot.QualityReport.MaxAllowedStage, 1, 10);
        int appliedStage = appliedOptions.AppliedStage;
        StagePreset preset = ApplyFailSafeOpacity(appliedOptions.StagePreset, snapshot.QualityReport);
        RetouchToolset toolset = appliedOptions.RetouchToolset;
        bool runSkinSmooth = options.EnableSkinSmooth && toolset.SkinSmooth.EnableSkinSmooth;
        bool runBlemishReduce = options.EnableBlemishReduce && toolset.Blemish.EnableBlemishReduce;
        bool runWrinkleReduce = options.EnableWrinkleReduce && toolset.Wrinkle.EnableWrinkleReduce;
        bool runToneEven = options.EnableToneEven && toolset.ToneEven.EnableToneEven;
        bool runTextureRestore = options.EnableTextureRestore && toolset.TextureRestore.EnableTextureRestore;

        if (!runSkinSmooth &&
            !runBlemishReduce &&
            !runWrinkleReduce &&
            !runToneEven &&
            !runTextureRestore)
        {
            return CreatePassthroughOutput(bitmap, snapshot, appliedOptions, preset, requestedStage, appliedStage, maxAllowedStage, pipelineStartedAtUtc);
        }

        int stride = width * 4;
        byte[] originalPixels = new byte[stride * height];
        bitmap.CopyPixels(originalPixels, stride, 0);

        byte[] smoothBasePixels = runSkinSmooth
            ? CreateSmoothBase(originalPixels, masks, width, height, preset.SkinSmoothAmount)
            : originalPixels;

        byte[] detailLayerPixels = runSkinSmooth && runTextureRestore
            ? CreateDetailLayer(originalPixels, width, height)
            : originalPixels;
        byte[] textureRestoredPixels = runSkinSmooth && runTextureRestore
            ? RestoreTexture(originalPixels, smoothBasePixels, detailLayerPixels, width, height, preset.TextureRestoreAmount)
            : smoothBasePixels;

        BitmapSource smoothBaseImage = runSkinSmooth ? CreateBitmap(width, height, smoothBasePixels) : bitmap;
        BitmapSource detailLayerImage = runSkinSmooth && runTextureRestore ? CreateBitmap(width, height, detailLayerPixels) : bitmap;
        BitmapSource textureRestoredImage = runSkinSmooth && runTextureRestore ? CreateBitmap(width, height, textureRestoredPixels) : smoothBaseImage;
        BitmapSource retouchAllowAppliedImage;
        BitmapSource softProtectAppliedImage;
        BitmapSource hardProtectRestoredImage;
        BitmapSource composedFinalImage;
        if (runSkinSmooth)
        {
            RetouchComposeResult composeResult = ComposeFinal(originalPixels, textureRestoredPixels, masks, width, height, preset);
            retouchAllowAppliedImage = CreateBitmap(width, height, composeResult.RetouchAllowAppliedPixels);
            softProtectAppliedImage = CreateBitmap(width, height, composeResult.SoftProtectAppliedPixels);
            hardProtectRestoredImage = CreateBitmap(width, height, composeResult.HardProtectRestoredPixels);
            composedFinalImage = CreateBitmap(width, height, composeResult.FinalPixels);
        }
        else
        {
            retouchAllowAppliedImage = bitmap;
            softProtectAppliedImage = bitmap;
            hardProtectRestoredImage = bitmap;
            composedFinalImage = bitmap;
        }
        BlemishReduceResult blemishResult = runBlemishReduce
            ? _blemishReduceFilter.Apply(new BlemishReduceInput(
                bitmap,
                composedFinalImage,
                snapshot,
                masks.RetouchAllowMask,
                masks.SoftProtectMask,
                masks.HardProtectMask,
                appliedStage,
                preset,
                snapshot.QualityReport))
            : CreateDisabledBlemishResult(composedFinalImage, width, height, preset.BlemishReduceAmount);
        WrinkleSoftReduceResult wrinkleResult = runWrinkleReduce
            ? _wrinkleSoftReduceFilter.Apply(new WrinkleSoftReduceInput(
                bitmap,
                blemishResult.BlemishReducedImage,
                snapshot,
                masks.RetouchAllowMask,
                masks.SoftProtectMask,
                masks.HardProtectMask,
                appliedStage,
                preset,
                options.WrinkleToolset ?? toolset.Wrinkle,
                snapshot.QualityReport,
                blemishResult.BlemishMask))
            : CreateDisabledWrinkleResult(blemishResult.BlemishReducedImage, width, height);
        BitmapSource toneEvenImage = runToneEven
            ? ApplyToneEven(wrinkleResult.WrinkleReducedImage, originalPixels, masks, width, height, preset.ToneEvenAmount)
            : wrinkleResult.WrinkleReducedImage;
        TextureRestoreResult textureResult = runTextureRestore
            ? _textureRestoreFilter.Apply(new TextureRestoreInput(
                bitmap,
                toneEvenImage,
                snapshot,
                masks.RetouchAllowMask,
                masks.SoftProtectMask,
                masks.HardProtectMask,
                appliedStage,
                preset,
                options.TextureRestoreToolset ?? toolset.TextureRestore,
                snapshot.QualityReport,
                blemishResult.BlemishMask,
                wrinkleResult.WrinkleAppliedMask))
            : CreateDisabledTextureRestoreResult(wrinkleResult.WrinkleReducedImage, width, height, appliedStage);
        HardProtectFinalRestoreResult hardProtectResult = _hardProtectFinalRestoreFilter.Apply(new HardProtectFinalRestoreInput(
            bitmap,
            textureResult.TextureRestoredImage,
            snapshot,
            masks.HardProtectMask,
            masks.SoftProtectMask,
            masks.RetouchAllowMask,
            appliedStage,
            snapshot.QualityReport));
        BitmapSource finalImage = hardProtectResult.FinalProtectedImage;
        List<string> warnings = new(snapshot.QualityReport.Warnings);
        warnings.AddRange(snapshot.QualityReport.FatalErrors.Select(error => "fatal_" + error));
        warnings.AddRange(blemishResult.DebugWarnings.Select(warning => "blemish_" + warning));
        warnings.AddRange(wrinkleResult.DebugWarnings.Select(warning => "wrinkle_" + warning));
        warnings.AddRange(textureResult.DebugWarnings.Select(warning => "texture_" + warning));
        warnings.AddRange(hardProtectResult.DebugWarnings.Select(warning => "hardprotect_" + warning));
        if (appliedStage < requestedStage)
        {
            warnings.Add("strong_retouch_limited_by_mask_quality");
        }
        List<string> filtersExecuted = new()
        {
            runSkinSmooth ? "SkinSmoothFilter" : "SkinSmoothFilter(skipped)",
            runBlemishReduce ? "BlemishReduceFilter" : "BlemishReduceFilter(skipped)",
            runWrinkleReduce ? "WrinkleSoftReduceFilter" : "WrinkleSoftReduceFilter(skipped)",
            runToneEven ? "ToneEvenFilter" : "ToneEvenFilter(skipped)",
            runTextureRestore ? "TextureRestoreFilter" : "TextureRestoreFilter(skipped)",
            "HardProtectFinalRestoreFilter"
        };

        RetouchProcessReport report = new(
            requestedStage,
            appliedStage,
            maxAllowedStage,
            preset.SkinSmoothAmount,
            preset.BlemishReduceAmount,
            preset.WrinkleReduceAmount,
            preset.ToneEvenAmount,
            preset.TextureRestoreAmount,
            preset.DetailRestoreAmount,
            true,
            preset.SoftProtectOpacity,
            preset.RetouchAllowOpacity,
            snapshot.QualityReport.Score,
            blemishResult.Report.CandidateCount,
            blemishResult.Report.AppliedCount,
            blemishResult.Report.AverageCorrectionStrength,
            wrinkleResult.Report.AppliedCount,
            wrinkleResult.Report.AverageCorrectionStrength,
            textureResult.Report.RetouchAllowTextureAmount,
            textureResult.Report.SoftProtectTextureAmount,
            textureResult.Report.PlasticSkinRiskScore,
            hardProtectResult.Report.ChangedPixelBeforeRestoreCount,
            hardProtectResult.Report.ChangedPixelAfterRestoreCount,
            hardProtectResult.Report.IsHardProtectClean,
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
            filtersExecuted,
            warnings,
            Array.Empty<string>());
        return new RetouchStageProcessorOutput(
            finalImage,
            smoothBaseImage,
            detailLayerImage,
            textureRestoredImage,
            retouchAllowAppliedImage,
            softProtectAppliedImage,
            hardProtectRestoredImage,
            blemishResult.BlemishReducedImage,
            blemishResult.BlemishCandidateMask,
            blemishResult.BlemishMask,
            blemishResult.Report,
            wrinkleResult.WrinkleReducedImage,
            wrinkleResult.WrinkleMaskSet,
            wrinkleResult.WrinkleCandidateMask,
            wrinkleResult.WrinkleAppliedMask,
            wrinkleResult.Report,
            toneEvenImage,
            textureResult.TextureRestoredImage,
            textureResult.BlurOriginalImage,
            textureResult.DetailLayerImage,
            textureResult.TextureRestoreMask,
            textureResult.TextureRestoreStrengthMap,
            textureResult.PlasticSkinRiskMap,
            textureResult.Report,
            hardProtectResult.FinalProtectedImage,
            hardProtectResult.BeforeRestoreDiffMask,
            hardProtectResult.AfterRestoreDiffMask,
            hardProtectResult.Report,
            appliedStage,
            appliedOptions,
            report,
            pipelineReport,
            warnings);
    }

    private static int GetBlurRadius(double skinSmoothAmount)
    {
        return Math.Clamp((int)Math.Round(1 + skinSmoothAmount * 7), 1, 8);
    }

    private static StagePreset ApplyFailSafeOpacity(StagePreset preset, MaskQualityReport qualityReport)
    {
        double retouchAllowOpacity = preset.RetouchAllowOpacity;
        double softProtectOpacity = preset.SoftProtectOpacity;
        if (!qualityReport.IsUsable)
        {
            retouchAllowOpacity *= 0.45;
            softProtectOpacity *= 0.35;
        }

        if (qualityReport.NostrilMaskQualityScore < 0.45 ||
            HasWarning(qualityReport, "nostril") ||
            HasWarning(qualityReport, "nose_lower"))
        {
            softProtectOpacity = Math.Min(softProtectOpacity, 0.30);
        }

        if (qualityReport.HairMaskQualityScore < 0.45 ||
            HasWarning(qualityReport, "hair"))
        {
            retouchAllowOpacity *= 0.86;
            softProtectOpacity *= 0.82;
        }

        if (qualityReport.EyeMaskQualityScore < 0.55 ||
            qualityReport.LipMaskQualityScore < 0.55 ||
            qualityReport.HardProtectQualityScore < 0.70)
        {
            retouchAllowOpacity *= 0.72;
            softProtectOpacity *= 0.60;
        }

        if (qualityReport.SkinMaskQualityScore < 0.45 ||
            qualityReport.RetouchAllowQualityScore < 0.45)
        {
            retouchAllowOpacity *= 0.55;
            softProtectOpacity *= 0.55;
        }

        return preset with
        {
            RetouchAllowOpacity = Math.Clamp(retouchAllowOpacity, 0, preset.RetouchAllowOpacity),
            SoftProtectOpacity = Math.Clamp(softProtectOpacity, 0, preset.SoftProtectOpacity),
            BlemishReduceAmount = Math.Clamp(preset.BlemishReduceAmount * GetBlemishQualityScale(qualityReport), 0, preset.BlemishReduceAmount),
            WrinkleReduceAmount = Math.Clamp(preset.WrinkleReduceAmount * GetWrinkleQualityScale(qualityReport), 0, preset.WrinkleReduceAmount),
            TextureRestoreAmount = Math.Clamp(preset.TextureRestoreAmount * GetTextureQualityScale(qualityReport), 0.12, preset.TextureRestoreAmount)
        };
    }

    private static TextureRestoreResult CreateDisabledTextureRestoreResult(BitmapSource currentImage, int width, int height, int appliedStage)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        TextureRestoreProcessReport report = new(appliedStage, 0, 0, 0, 0, 0, new[] { "texture_restore_disabled" });
        return new TextureRestoreResult(currentImage, currentImage, currentImage, empty, empty, empty, report, report.DebugWarnings);
    }

    private static BlemishReduceResult CreateDisabledBlemishResult(BitmapSource currentImage, int width, int height, double amount)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        BlemishProcessReport report = new(0, 0, 0, amount, 0, new[] { "blemish_reduce_disabled" }, Array.Empty<BlemishCandidatePoint>());
        return new BlemishReduceResult(currentImage, empty, empty, report, report.DebugWarnings);
    }

    private static WrinkleSoftReduceResult CreateDisabledWrinkleResult(BitmapSource currentImage, int width, int height)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        WrinkleProcessReport report = WrinkleProcessReport.Empty(new[] { "wrinkle_reduce_disabled" });
        return new WrinkleSoftReduceResult(currentImage, WrinkleMaskSet.Empty(width, height), empty, empty, report, report.DebugWarnings);
    }

    private static RetouchStageProcessorOutput CreatePassthroughOutput(
        BitmapSource bitmap,
        FaceSnapshotMaskSet snapshot,
        AppliedRetouchOptions appliedOptions,
        StagePreset preset,
        int requestedStage,
        int appliedStage,
        int maxAllowedStage,
        DateTime pipelineStartedAtUtc)
    {
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        MaskPlane empty = MaskPlane.Empty(width, height);
        BlemishProcessReport blemishReport = new(0, 0, 0, 0, 0, new[] { "blemish_reduce_not_requested" }, Array.Empty<BlemishCandidatePoint>());
        WrinkleProcessReport wrinkleReport = WrinkleProcessReport.Empty(new[] { "wrinkle_reduce_not_requested" });
        TextureRestoreProcessReport textureReport = new(appliedStage, 0, 0, 0, 0, 0, new[] { "texture_restore_not_requested" });
        HardProtectRestoreReport hardProtectReport = new(
            appliedStage,
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
            true,
            new[] { "hardprotect_restore_not_requested" });
        List<string> warnings = new(snapshot.QualityReport.Warnings);
        warnings.AddRange(snapshot.QualityReport.FatalErrors.Select(error => "fatal_" + error));
        warnings.Add("retouch_pipeline_passthrough_no_filter_requested");
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
                "SkinSmoothFilter(skipped)",
                "BlemishReduceFilter(skipped)",
                "WrinkleSoftReduceFilter(skipped)",
                "ToneEvenFilter(skipped)",
                "TextureRestoreFilter(skipped)",
                "HardProtectFinalRestoreFilter(skipped)"
            },
            warnings,
            Array.Empty<string>());
        RetouchProcessReport report = new(
            requestedStage,
            appliedStage,
            maxAllowedStage,
            0,
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
            blemishReport,
            bitmap,
            WrinkleMaskSet.Empty(width, height),
            empty,
            empty,
            wrinkleReport,
            bitmap,
            bitmap,
            bitmap,
            bitmap,
            empty,
            empty,
            empty,
            textureReport,
            bitmap,
            empty,
            empty,
            hardProtectReport,
            appliedStage,
            appliedOptions,
            report,
            pipelineReport,
            warnings);
    }

    private static double GetTextureQualityScale(MaskQualityReport qualityReport)
    {
        double scale = 1;
        if (!qualityReport.IsUsable)
        {
            scale *= 0.72;
        }

        if (qualityReport.SkinMaskQualityScore < 0.55 ||
            qualityReport.RetouchAllowQualityScore < 0.55)
        {
            scale *= 0.78;
        }

        if (qualityReport.NostrilMaskQualityScore < 0.55 ||
            qualityReport.HairMaskQualityScore < 0.50)
        {
            scale *= 0.86;
        }

        return Math.Clamp(scale, 0.35, 1);
    }

    private static double GetWrinkleQualityScale(MaskQualityReport qualityReport)
    {
        double scale = 1;
        if (!qualityReport.IsUsable)
        {
            scale *= 0.50;
        }

        if (qualityReport.EyeMaskQualityScore < 0.55 ||
            qualityReport.EyebrowMaskQualityScore < 0.55)
        {
            scale *= 0.70;
        }

        if (qualityReport.LipMaskQualityScore < 0.55 ||
            qualityReport.NostrilMaskQualityScore < 0.55)
        {
            scale *= 0.78;
        }

        if (qualityReport.SkinMaskQualityScore < 0.55 ||
            qualityReport.RetouchAllowQualityScore < 0.55)
        {
            scale *= 0.70;
        }

        return Math.Clamp(scale, 0.22, 1);
    }

    private static double GetBlemishQualityScale(MaskQualityReport qualityReport)
    {
        double scale = 1;
        if (!qualityReport.IsUsable)
        {
            scale *= 0.55;
        }

        if (qualityReport.SkinMaskQualityScore < 0.55 ||
            qualityReport.RetouchAllowQualityScore < 0.55)
        {
            scale *= 0.70;
        }

        if (qualityReport.EyeMaskQualityScore < 0.55 ||
            qualityReport.LipMaskQualityScore < 0.55 ||
            qualityReport.NostrilMaskQualityScore < 0.55 ||
            qualityReport.HardProtectQualityScore < 0.70)
        {
            scale *= 0.75;
        }

        return Math.Clamp(scale, 0.25, 1);
    }

    private static bool HasWarning(MaskQualityReport qualityReport, string token)
    {
        return qualityReport.Warnings.Any(warning => warning.Contains(token, StringComparison.OrdinalIgnoreCase)) ||
               qualityReport.FatalErrors.Any(error => error.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateMaskSize(FaceMaskSet masks, int width, int height)
    {
        if (masks.HardProtectMask.Width != width ||
            masks.HardProtectMask.Height != height ||
            masks.SoftProtectMask.Width != width ||
            masks.SoftProtectMask.Height != height ||
            masks.RetouchAllowMask.Width != width ||
            masks.RetouchAllowMask.Height != height)
        {
            throw new InvalidOperationException("Snapshot mask size must match the original image size.");
        }
    }

    private static byte[] CreateSmoothBase(byte[] originalPixels, FaceMaskSet masks, int width, int height, double skinSmoothAmount)
    {
        byte[] wideBlur = BoxBlur(originalPixels, width, height, GetBlurRadius(skinSmoothAmount));
        byte[] edgeAware = new byte[originalPixels.Length];
        int stride = width * 4;
        double colorTolerance = 18 + skinSmoothAmount * 52;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double hardProtect = masks.HardProtectMask[x, y];
                if (hardProtect >= 0.98)
                {
                    CopyPixel(originalPixels, edgeAware, index);
                    continue;
                }

                double maskWeight = Math.Clamp(
                    masks.RetouchAllowMask[x, y] +
                    masks.SoftProtectMask[x, y] * 0.45,
                    0,
                    1);
                double edgeWeight = CalculateEdgeWeight(originalPixels, width, height, x, y, colorTolerance);
                double blend = Math.Clamp(skinSmoothAmount * maskWeight * edgeWeight * (1 - hardProtect), 0, 1);
                edgeAware[index] = BlendChannel(originalPixels[index], wideBlur[index], blend);
                edgeAware[index + 1] = BlendChannel(originalPixels[index + 1], wideBlur[index + 1], blend);
                edgeAware[index + 2] = BlendChannel(originalPixels[index + 2], wideBlur[index + 2], blend);
                edgeAware[index + 3] = originalPixels[index + 3];
            }
        }

        return edgeAware;
    }

    private static byte[] BoxBlur(byte[] sourcePixels, int width, int height, int radius)
    {
        int stride = width * 4;
        byte[] output = new byte[sourcePixels.Length];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int redSum = 0;
                int greenSum = 0;
                int blueSum = 0;
                int count = 0;
                for (int sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(height - 1, y + radius); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(width - 1, x + radius); sampleX++)
                    {
                        int sampleIndex = sampleY * stride + sampleX * 4;
                        blueSum += sourcePixels[sampleIndex];
                        greenSum += sourcePixels[sampleIndex + 1];
                        redSum += sourcePixels[sampleIndex + 2];
                        count++;
                    }
                }

                int index = y * stride + x * 4;
                output[index] = (byte)(blueSum / count);
                output[index + 1] = (byte)(greenSum / count);
                output[index + 2] = (byte)(redSum / count);
                output[index + 3] = sourcePixels[index + 3];
            }
        }

        return output;
    }

    private static BitmapSource ApplyToneEven(BitmapSource currentImage, byte[] originalPixels, FaceMaskSet masks, int width, int height, double amount)
    {
        if (amount <= 0)
        {
            return currentImage;
        }

        BitmapSource currentBgra = currentImage.Format == PixelFormats.Bgra32
            ? currentImage
            : new FormatConvertedBitmap(currentImage, PixelFormats.Bgra32, null, 0);
        currentBgra.Freeze();
        int stride = width * 4;
        byte[] currentPixels = new byte[stride * height];
        currentBgra.CopyPixels(currentPixels, stride, 0);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double hardProtect = Math.Clamp(masks.HardProtectMask[x, y], 0, 1);
                if (hardProtect >= 0.98)
                {
                    CopyPixel(originalPixels, currentPixels, index);
                    continue;
                }

                double retouchAllow = Math.Clamp(masks.RetouchAllowMask[x, y], 0, 1);
                double softProtect = Math.Clamp(masks.SoftProtectMask[x, y], 0, 1);
                double maskWeight = Math.Clamp(retouchAllow * 0.48 + softProtect * 0.16, 0, 1);
                double blend = Math.Clamp(amount * maskWeight * (1 - hardProtect), 0, 1);
                if (blend <= 0)
                {
                    continue;
                }

                double average = (originalPixels[index] + originalPixels[index + 1] + originalPixels[index + 2]) / 3d;
                currentPixels[index] = BlendChannel(currentPixels[index], average, blend);
                currentPixels[index + 1] = BlendChannel(currentPixels[index + 1], average, blend);
                currentPixels[index + 2] = BlendChannel(currentPixels[index + 2], average, blend);
                currentPixels[index + 3] = originalPixels[index + 3];
            }
        }

        return CreateBitmap(width, height, currentPixels);
    }

    private static byte[] CreateDetailLayer(byte[] originalPixels, int width, int height)
    {
        int radius = 2;
        byte[] blurredOriginal = BoxBlur(originalPixels, width, height, radius);
        byte[] detailPixels = new byte[originalPixels.Length];
        for (int index = 0; index < detailPixels.Length; index += 4)
        {
            detailPixels[index] = DetailPreviewChannel(originalPixels[index], blurredOriginal[index]);
            detailPixels[index + 1] = DetailPreviewChannel(originalPixels[index + 1], blurredOriginal[index + 1]);
            detailPixels[index + 2] = DetailPreviewChannel(originalPixels[index + 2], blurredOriginal[index + 2]);
            detailPixels[index + 3] = originalPixels[index + 3];
        }

        return detailPixels;
    }

    private static byte[] RestoreTexture(byte[] originalPixels, byte[] smoothPixels, byte[] detailLayerPixels, int width, int height, double amount)
    {
        byte[] output = new byte[originalPixels.Length];
        for (int index = 0; index < output.Length; index += 4)
        {
            output[index] = RestoreChannel(smoothPixels[index], detailLayerPixels[index], amount);
            output[index + 1] = RestoreChannel(smoothPixels[index + 1], detailLayerPixels[index + 1], amount);
            output[index + 2] = RestoreChannel(smoothPixels[index + 2], detailLayerPixels[index + 2], amount);
            output[index + 3] = originalPixels[index + 3];
        }

        return output;
    }

    private static RetouchComposeResult ComposeFinal(byte[] originalPixels, byte[] retouchedPixels, FaceMaskSet masks, int width, int height, StagePreset preset)
    {
        int stride = width * 4;
        byte[] retouchAllowApplied = new byte[originalPixels.Length];
        byte[] softProtectApplied = new byte[originalPixels.Length];
        byte[] hardProtectRestored = new byte[originalPixels.Length];
        byte[] finalPixels = new byte[originalPixels.Length];
        Buffer.BlockCopy(originalPixels, 0, retouchAllowApplied, 0, originalPixels.Length);
        Buffer.BlockCopy(originalPixels, 0, softProtectApplied, 0, originalPixels.Length);
        Buffer.BlockCopy(originalPixels, 0, hardProtectRestored, 0, originalPixels.Length);
        Buffer.BlockCopy(originalPixels, 0, finalPixels, 0, originalPixels.Length);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double hardProtect = Math.Clamp(masks.HardProtectMask[x, y], 0, 1);
                double retouchAllow = Math.Clamp(masks.RetouchAllowMask[x, y] * (1 - hardProtect), 0, 1);
                double softProtect = Math.Clamp(masks.SoftProtectMask[x, y] * (1 - hardProtect), 0, 1);
                double retouchAllowOpacity = Math.Clamp(retouchAllow * preset.RetouchAllowOpacity, 0, 1);
                double softProtectOpacity = Math.Clamp(softProtect * preset.SoftProtectOpacity * (1 - retouchAllow), 0, 1);
                double detailPreserveOpacity = Math.Clamp(1 - preset.DetailRestoreAmount * 0.32, 0.65, 1);
                double finalOpacity = Math.Clamp((retouchAllowOpacity + softProtectOpacity) * detailPreserveOpacity, 0, 1);

                if (retouchAllowOpacity > 0)
                {
                    retouchAllowApplied[index] = BlendChannel(originalPixels[index], retouchedPixels[index], retouchAllowOpacity);
                    retouchAllowApplied[index + 1] = BlendChannel(originalPixels[index + 1], retouchedPixels[index + 1], retouchAllowOpacity);
                    retouchAllowApplied[index + 2] = BlendChannel(originalPixels[index + 2], retouchedPixels[index + 2], retouchAllowOpacity);
                }

                if (softProtectOpacity > 0)
                {
                    softProtectApplied[index] = BlendChannel(retouchAllowApplied[index], retouchedPixels[index], softProtectOpacity);
                    softProtectApplied[index + 1] = BlendChannel(retouchAllowApplied[index + 1], retouchedPixels[index + 1], softProtectOpacity);
                    softProtectApplied[index + 2] = BlendChannel(retouchAllowApplied[index + 2], retouchedPixels[index + 2], softProtectOpacity);
                }
                else
                {
                    CopyPixel(retouchAllowApplied, softProtectApplied, index);
                }

                if (finalOpacity > 0)
                {
                    finalPixels[index] = BlendChannel(originalPixels[index], retouchedPixels[index], finalOpacity);
                    finalPixels[index + 1] = BlendChannel(originalPixels[index + 1], retouchedPixels[index + 1], finalOpacity);
                    finalPixels[index + 2] = BlendChannel(originalPixels[index + 2], retouchedPixels[index + 2], finalOpacity);
                }

                if (hardProtect > 0)
                {
                    hardProtectRestored[index] = BlendChannel(softProtectApplied[index], originalPixels[index], hardProtect);
                    hardProtectRestored[index + 1] = BlendChannel(softProtectApplied[index + 1], originalPixels[index + 1], hardProtect);
                    hardProtectRestored[index + 2] = BlendChannel(softProtectApplied[index + 2], originalPixels[index + 2], hardProtect);
                    CopyPixel(originalPixels, finalPixels, index);
                }
                else
                {
                    CopyPixel(softProtectApplied, hardProtectRestored, index);
                }

                finalPixels[index + 3] = originalPixels[index + 3];
                retouchAllowApplied[index + 3] = originalPixels[index + 3];
                softProtectApplied[index + 3] = originalPixels[index + 3];
                hardProtectRestored[index + 3] = originalPixels[index + 3];
            }
        }

        return new RetouchComposeResult(finalPixels, retouchAllowApplied, softProtectApplied, hardProtectRestored);
    }

    private static double CalculateEdgeWeight(byte[] pixels, int width, int height, int x, int y, double tolerance)
    {
        int stride = width * 4;
        int index = y * stride + x * 4;
        double centerLuminance = GetLuminance(pixels, index);
        double maxDifference = 0;
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            int sampleY = Math.Clamp(y + offsetY, 0, height - 1);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int sampleX = Math.Clamp(x + offsetX, 0, width - 1);
                int sampleIndex = sampleY * stride + sampleX * 4;
                maxDifference = Math.Max(maxDifference, Math.Abs(centerLuminance - GetLuminance(pixels, sampleIndex)));
            }
        }

        return 1 - Math.Clamp(maxDifference / tolerance, 0, 1);
    }

    private static double GetLuminance(byte[] pixels, int index)
    {
        return pixels[index + 2] * 0.299 + pixels[index + 1] * 0.587 + pixels[index] * 0.114;
    }

    private static byte DetailPreviewChannel(byte original, byte blurredOriginal)
    {
        double value = 128d + original - blurredOriginal;
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }

    private static byte RestoreChannel(byte smooth, byte detailPreview, double amount)
    {
        double detail = detailPreview - 128;
        return (byte)Math.Clamp((int)Math.Round(smooth + detail * amount), 0, 255);
    }

    private static void CopyPixel(byte[] source, byte[] target, int index)
    {
        target[index] = source[index];
        target[index + 1] = source[index + 1];
        target[index + 2] = source[index + 2];
        target[index + 3] = source[index + 3];
    }

    private static byte BlendChannel(byte source, byte target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static byte BlendChannel(byte source, double target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed record RetouchComposeResult(
        byte[] FinalPixels,
        byte[] RetouchAllowAppliedPixels,
        byte[] SoftProtectAppliedPixels,
        byte[] HardProtectRestoredPixels);
}

public sealed record RetouchAnalysisCacheStatus(
    int BlemishAnalysisCacheCount,
    int WrinkleAnalysisCacheCount,
    int TextureRestoreAnalysisCacheCount)
{
    public int TotalCount => BlemishAnalysisCacheCount + WrinkleAnalysisCacheCount + TextureRestoreAnalysisCacheCount;
}
