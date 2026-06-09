namespace PhotoRetouch;

public enum PreviewRenderTier
{
    LowPreview,
    FastPreview,
    QualityPreview,
    ExportRender
}

public sealed record PreviewRenderTierPolicy(
    PreviewRenderTier Tier,
    int? MaxLongSide,
    bool IsQualityJudgement,
    bool UsesOriginalResolution)
{
    public static PreviewRenderTierPolicy For(PreviewRenderTier tier, int? visibleMaxLongSide = null)
    {
        return tier switch
        {
            PreviewRenderTier.LowPreview => new PreviewRenderTierPolicy(tier, ClampVisibleLongSide(visibleMaxLongSide, 1200, PreviewSettings.MaximumMaxLongSidePixels), true, false),
            PreviewRenderTier.FastPreview => new PreviewRenderTierPolicy(tier, ClampVisibleLongSide(visibleMaxLongSide, 1200, PreviewSettings.MaximumMaxLongSidePixels), true, false),
            PreviewRenderTier.QualityPreview => new PreviewRenderTierPolicy(tier, ClampVisibleLongSide(visibleMaxLongSide, 1200, PreviewSettings.MaximumMaxLongSidePixels), true, false),
            PreviewRenderTier.ExportRender => new PreviewRenderTierPolicy(tier, null, true, true),
            _ => new PreviewRenderTierPolicy(tier, visibleMaxLongSide, false, false)
        };
    }

    private static int ClampVisibleLongSide(int? visibleMaxLongSide, int minimum, int maximum)
    {
        int value = visibleMaxLongSide ?? maximum;
        return Math.Clamp(value, minimum, maximum);
    }
}
