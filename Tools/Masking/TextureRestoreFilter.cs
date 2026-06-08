using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class TextureRestoreFilter : ITextureRestoreFilter
{
    private readonly Dictionary<string, TextureRestoreAnalysisCache> _analysisCache = new();

    public TextureRestoreResult Apply(TextureRestoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        TextureRestoreToolset toolset = input.Toolset ?? TextureRestoreToolset.FromStagePreset(input.StagePreset);
        if (!toolset.EnableTextureRestore || toolset.GlobalTextureAmount <= 0)
        {
            TextureRestoreProcessReport disabledReport = new(
                input.AppliedStage,
                0,
                0,
                0,
                0,
                0,
                new[] { "texture_restore_disabled" });
            MaskPlane empty = MaskPlane.Empty(input.OriginalImage.PixelWidth, input.OriginalImage.PixelHeight);
            return new TextureRestoreResult(input.CurrentRetouchedImage, input.CurrentRetouchedImage, input.CurrentRetouchedImage, empty, empty, empty, disabledReport, disabledReport.DebugWarnings);
        }

        int width = input.OriginalImage.PixelWidth;
        int height = input.OriginalImage.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = CopyPixels(input.OriginalImage);
        byte[] currentPixels = CopyPixels(input.CurrentRetouchedImage);
        TextureRestoreAnalysisCache analysis = GetOrCreateAnalysis(input, originalPixels, currentPixels, width, height);
        byte[] outputPixels = (byte[])currentPixels.Clone();

        double qualityScale = GetQualityScale(input.MaskQualityReport);
        double globalAmount = Math.Clamp(input.StagePreset.TextureRestoreAmount * toolset.GlobalTextureAmount * qualityScale, 0, 1);
        double retouchAllowAmount = Math.Clamp(globalAmount * (0.48 + toolset.PoreTextureAmount * 0.30 + toolset.FineDetailAmount * 0.22), 0, 0.92);
        double softProtectAmount = Math.Clamp(retouchAllowAmount * toolset.SoftProtectTextureAmount * input.StagePreset.SoftProtectTextureRestoreAmount, 0, 0.36);
        double guardBoost = toolset.PlasticSkinGuardEnabled
            ? input.StagePreset.PlasticSkinGuardAmount * analysis.PlasticSkinRiskScore * 0.28
            : 0;
        retouchAllowAmount = Math.Clamp(retouchAllowAmount + guardBoost, 0, 0.94);

        MaskPlane strengthMap = MaskPlane.Empty(width, height);
        double detailLimit = 4 + toolset.DetailSharpnessLimit * input.StagePreset.DetailSharpnessLimit * 38;
        double grainScale = 0.35 + toolset.SkinGrainAmount * 0.35;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                if (hardProtect >= 0.98)
                {
                    int protectedIndex = y * stride + x * 4;
                    CopyPixel(originalPixels, outputPixels, protectedIndex);
                    continue;
                }

                double retouchAllow = input.RetouchAllowMask[x, y];
                double softProtect = input.SoftProtectMask[x, y];
                double restoreMask = analysis.TextureRestoreMask[x, y];
                double blockedByBlemish = input.BlemishMask?.Values[y * width + x] ?? 0;
                double blockedByWrinkle = input.WrinkleAppliedMask?.Values[y * width + x] ?? 0;
                double repairBlock = 1 - Math.Clamp(blockedByBlemish * 0.62 + blockedByWrinkle * 0.72, 0, 0.92);
                double amount = Math.Clamp((retouchAllow * retouchAllowAmount + softProtect * softProtectAmount) * restoreMask * repairBlock * (1 - hardProtect), 0, 0.94);
                if (amount <= 0)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                double blueDetail = ClampDetail(originalPixels[index] - analysis.BlurOriginalPixels[index], detailLimit);
                double greenDetail = ClampDetail(originalPixels[index + 1] - analysis.BlurOriginalPixels[index + 1], detailLimit);
                double redDetail = ClampDetail(originalPixels[index + 2] - analysis.BlurOriginalPixels[index + 2], detailLimit);
                double detailEnergy = (Math.Abs(blueDetail) + Math.Abs(greenDetail) + Math.Abs(redDetail)) / 3d;
                double detailGate = 1 - SmoothStep(detailLimit * 0.82, detailLimit * 1.30, detailEnergy);
                double finalAmount = Math.Clamp(amount * (0.72 + detailGate * 0.28), 0, 0.94);

                outputPixels[index] = AddDetail(outputPixels[index], blueDetail * grainScale, finalAmount);
                outputPixels[index + 1] = AddDetail(outputPixels[index + 1], greenDetail * grainScale, finalAmount);
                outputPixels[index + 2] = AddDetail(outputPixels[index + 2], redDetail * grainScale, finalAmount);
                outputPixels[index + 3] = currentPixels[index + 3];
                strengthMap[x, y] = finalAmount;
            }
        }

        RestoreHardProtect(originalPixels, outputPixels, input.HardProtectMask, width, height);
        TextureRestoreProcessReport report = new(
            input.AppliedStage,
            globalAmount,
            retouchAllowAmount,
            softProtectAmount,
            grainScale,
            analysis.PlasticSkinRiskScore,
            analysis.DebugWarnings);
        return new TextureRestoreResult(
            CreateBitmap(width, height, outputPixels),
            CreateBitmap(width, height, analysis.BlurOriginalPixels),
            CreateBitmap(width, height, analysis.DetailPreviewPixels),
            analysis.TextureRestoreMask,
            strengthMap,
            analysis.PlasticRiskMap,
            report,
            analysis.DebugWarnings);
    }

    private TextureRestoreAnalysisCache GetOrCreateAnalysis(TextureRestoreInput input, byte[] originalPixels, byte[] currentPixels, int width, int height)
    {
        string cacheKey = input.Snapshot.CacheKey.StableId + "|texture_restore_v1";
        if (_analysisCache.TryGetValue(cacheKey, out TextureRestoreAnalysisCache? cached))
        {
            return cached;
        }

        TextureRestoreAnalysisCache created = AnalyzeTexture(input, originalPixels, currentPixels, width, height);
        _analysisCache[cacheKey] = created;
        return created;
    }

    private static TextureRestoreAnalysisCache AnalyzeTexture(TextureRestoreInput input, byte[] originalPixels, byte[] currentPixels, int width, int height)
    {
        int blurRadius = Math.Clamp((int)Math.Round(input.StagePreset.DetailLayerBlurRadius), 1, 8);
        byte[] blurOriginal = FastBoxBlur(originalPixels, width, height, blurRadius);
        byte[] detailPreview = new byte[originalPixels.Length];
        MaskPlane textureMask = BuildTextureRestoreMask(input, width, height);
        MaskPlane plasticRiskMap = MaskPlane.Empty(width, height);
        double originalDetailSum = 0;
        double currentDetailSum = 0;
        double maskSum = 0;
        int stride = width * 4;

        byte[] blurCurrent = FastBoxBlur(currentPixels, width, height, blurRadius);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                detailPreview[index] = DetailPreviewChannel(originalPixels[index], blurOriginal[index]);
                detailPreview[index + 1] = DetailPreviewChannel(originalPixels[index + 1], blurOriginal[index + 1]);
                detailPreview[index + 2] = DetailPreviewChannel(originalPixels[index + 2], blurOriginal[index + 2]);
                detailPreview[index + 3] = originalPixels[index + 3];

                double mask = textureMask[x, y];
                if (mask <= 0)
                {
                    continue;
                }

                double originalDetail = GetDetailEnergy(originalPixels, blurOriginal, index);
                double currentDetail = GetDetailEnergy(currentPixels, blurCurrent, index);
                double risk = Math.Clamp((originalDetail - currentDetail) / Math.Max(6, originalDetail + 0.001), 0, 1) * mask;
                plasticRiskMap[x, y] = risk;
                originalDetailSum += originalDetail * mask;
                currentDetailSum += currentDetail * mask;
                maskSum += mask;
            }
        }

        double plasticRisk = maskSum <= 0
            ? 0
            : Math.Clamp((originalDetailSum - currentDetailSum) / Math.Max(1, originalDetailSum), 0, 1);
        List<string> warnings = new() { "texture_restore_filter_v1" };
        if (plasticRisk > 0.42)
        {
            warnings.Add("plastic_skin_guard_boost");
        }

        return new TextureRestoreAnalysisCache(
            input.Snapshot.CacheKey.StableId,
            blurOriginal,
            detailPreview,
            textureMask,
            plasticRiskMap,
            plasticRisk,
            warnings);
    }

    private static MaskPlane BuildTextureRestoreMask(TextureRestoreInput input, int width, int height)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                double value = Math.Clamp(input.RetouchAllowMask[x, y] + input.SoftProtectMask[x, y] * input.StagePreset.SoftProtectTextureRestoreAmount, 0, 1);
                double blemishBlock = input.BlemishMask?[x, y] ?? 0;
                double wrinkleBlock = input.WrinkleAppliedMask?[x, y] ?? 0;
                value *= 1 - Math.Clamp(blemishBlock * 0.48 + wrinkleBlock * 0.58, 0, 0.86);
                mask[x, y] = value * (1 - hardProtect);
            }
        }

        return mask;
    }

    private static double GetQualityScale(MaskQualityReport report)
    {
        double scale = 1;
        if (!report.IsUsable)
        {
            scale *= 0.70;
        }

        if (report.SkinMaskQualityScore < 0.55 || report.RetouchAllowQualityScore < 0.55)
        {
            scale *= 0.74;
        }

        if (report.HairMaskQualityScore < 0.50 || report.NostrilMaskQualityScore < 0.50)
        {
            scale *= 0.84;
        }

        return Math.Clamp(scale, 0.34, 1);
    }

    private static double GetDetailEnergy(byte[] source, byte[] blurred, int index)
    {
        return (Math.Abs(source[index] - blurred[index]) +
                Math.Abs(source[index + 1] - blurred[index + 1]) +
                Math.Abs(source[index + 2] - blurred[index + 2])) / 3d;
    }

    private static byte DetailPreviewChannel(byte original, byte blurredOriginal)
    {
        return (byte)Math.Clamp((int)Math.Round(128d + original - blurredOriginal), 0, 255);
    }

    private static double ClampDetail(double detail, double limit)
    {
        return Math.Clamp(detail, -limit, limit);
    }

    private static byte AddDetail(byte source, double detail, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + detail * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static byte[] FastBoxBlur(byte[] sourcePixels, int width, int height, int radius)
    {
        int stride = width * 4;
        byte[] horizontal = new byte[sourcePixels.Length];
        byte[] output = new byte[sourcePixels.Length];
        for (int y = 0; y < height; y++)
        {
            int blueSum = 0;
            int greenSum = 0;
            int redSum = 0;
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                int addX = Math.Min(width - 1, x + radius);
                int addIndex = y * stride + addX * 4;
                blueSum += sourcePixels[addIndex];
                greenSum += sourcePixels[addIndex + 1];
                redSum += sourcePixels[addIndex + 2];
                count++;
                if (x > radius)
                {
                    int removeX = x - radius - 1;
                    int removeIndex = y * stride + removeX * 4;
                    blueSum -= sourcePixels[removeIndex];
                    greenSum -= sourcePixels[removeIndex + 1];
                    redSum -= sourcePixels[removeIndex + 2];
                    count--;
                }

                int index = y * stride + x * 4;
                horizontal[index] = (byte)(blueSum / Math.Max(1, count));
                horizontal[index + 1] = (byte)(greenSum / Math.Max(1, count));
                horizontal[index + 2] = (byte)(redSum / Math.Max(1, count));
                horizontal[index + 3] = sourcePixels[index + 3];
            }
        }

        for (int x = 0; x < width; x++)
        {
            int blueSum = 0;
            int greenSum = 0;
            int redSum = 0;
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                int addY = Math.Min(height - 1, y + radius);
                int addIndex = addY * stride + x * 4;
                blueSum += horizontal[addIndex];
                greenSum += horizontal[addIndex + 1];
                redSum += horizontal[addIndex + 2];
                count++;
                if (y > radius)
                {
                    int removeY = y - radius - 1;
                    int removeIndex = removeY * stride + x * 4;
                    blueSum -= horizontal[removeIndex];
                    greenSum -= horizontal[removeIndex + 1];
                    redSum -= horizontal[removeIndex + 2];
                    count--;
                }

                int index = y * stride + x * 4;
                output[index] = (byte)(blueSum / Math.Max(1, count));
                output[index + 1] = (byte)(greenSum / Math.Max(1, count));
                output[index + 2] = (byte)(redSum / Math.Max(1, count));
                output[index + 3] = sourcePixels[index + 3];
            }
        }

        return output;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static void RestoreHardProtect(byte[] originalPixels, byte[] outputPixels, MaskPlane hardProtectMask, int width, int height)
    {
        int stride = width * 4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = hardProtectMask[x, y];
                if (hardProtect <= 0)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                outputPixels[index] = BlendChannel(outputPixels[index], originalPixels[index], hardProtect);
                outputPixels[index + 1] = BlendChannel(outputPixels[index + 1], originalPixels[index + 1], hardProtect);
                outputPixels[index + 2] = BlendChannel(outputPixels[index + 2], originalPixels[index + 2], hardProtect);
                outputPixels[index + 3] = originalPixels[index + 3];
            }
        }
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

    private static byte[] CopyPixels(BitmapSource source)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed record TextureRestoreAnalysisCache(
        string SnapshotStableId,
        byte[] BlurOriginalPixels,
        byte[] DetailPreviewPixels,
        MaskPlane TextureRestoreMask,
        MaskPlane PlasticRiskMap,
        double PlasticSkinRiskScore,
        IReadOnlyList<string> DebugWarnings);
}

public interface ITextureRestoreFilter
{
    TextureRestoreResult Apply(TextureRestoreInput input);
}

public sealed record TextureRestoreToolset(
    bool EnableTextureRestore,
    double GlobalTextureAmount,
    double PoreTextureAmount,
    double FineDetailAmount,
    double SkinGrainAmount,
    double SoftProtectTextureAmount,
    double DetailSharpnessLimit,
    bool PlasticSkinGuardEnabled)
{
    public static TextureRestoreToolset FromStagePreset(StagePreset preset)
    {
        return new TextureRestoreToolset(
            true,
            1,
            preset.PoreTextureAmount,
            preset.FineDetailAmount,
            preset.SkinGrainAmount,
            preset.SoftProtectTextureRestoreAmount,
            preset.DetailSharpnessLimit,
            true);
    }
}

public sealed record TextureRestoreInput(
    BitmapSource OriginalImage,
    BitmapSource CurrentRetouchedImage,
    FaceSnapshotMaskSet Snapshot,
    MaskPlane RetouchAllowMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    int AppliedStage,
    StagePreset StagePreset,
    TextureRestoreToolset? Toolset,
    MaskQualityReport MaskQualityReport,
    MaskPlane? BlemishMask = null,
    MaskPlane? WrinkleAppliedMask = null);

public sealed record TextureRestoreResult(
    BitmapSource TextureRestoredImage,
    BitmapSource BlurOriginalImage,
    BitmapSource DetailLayerImage,
    MaskPlane TextureRestoreMask,
    MaskPlane TextureRestoreStrengthMap,
    MaskPlane PlasticSkinRiskMap,
    TextureRestoreProcessReport Report,
    IReadOnlyList<string> DebugWarnings);

public sealed record TextureRestoreProcessReport(
    int AppliedStage,
    double GlobalTextureRestoreAmount,
    double RetouchAllowTextureAmount,
    double SoftProtectTextureAmount,
    double DetailLayerStrength,
    double PlasticSkinRiskScore,
    IReadOnlyList<string> DebugWarnings);
