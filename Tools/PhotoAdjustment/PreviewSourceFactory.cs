using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public static class PreviewSourceFactory
{
    public static BitmapSource CreateEffectPreviewSource(BitmapSource source, int? visibleMaxLongSide = null)
    {
        return CreateEffectPreviewSource(source, PreviewRenderTier.QualityPreview, visibleMaxLongSide);
    }

    public static BitmapSource CreateEffectPreviewSource(BitmapSource source, PreviewRenderTier tier, int? visibleMaxLongSide = null)
    {
        if (tier == PreviewRenderTier.ExportRender)
        {
            return source;
        }

        int longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        PreviewRenderTierPolicy tierPolicy = PreviewRenderTierPolicy.For(tier, visibleMaxLongSide);
        int settingMaxLongSide = PreviewSettings.UseOriginalSize
            ? PreviewSettings.MaximumMaxLongSidePixels
            : Math.Clamp(
                PreviewSettings.MaxLongSidePixels,
                PreviewSettings.MinimumMaxLongSidePixels,
                PreviewSettings.MaximumMaxLongSidePixels);
        int maxLongSide = tierPolicy.MaxLongSide is null
            ? settingMaxLongSide
            : Math.Min(
                settingMaxLongSide,
                Math.Clamp(tierPolicy.MaxLongSide.Value, 320, PreviewSettings.MaximumMaxLongSidePixels));
        if (longestSide <= maxLongSide)
        {
            return source;
        }

        return HighResolutionProcessingPolicy.CreatePreviewSource(source, maxLongSide);
    }
}
