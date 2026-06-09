using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public static class PreviewSourceFactory
{
    public static BitmapSource CreateEffectPreviewSource(BitmapSource source, int? visibleMaxLongSide = null)
    {
        if (PreviewSettings.UseOriginalSize && visibleMaxLongSide is null)
        {
            ImageProcessingDecision decision = HighResolutionProcessingPolicy.Decide(source, null);
            return decision.MemoryWarning
                ? HighResolutionProcessingPolicy.CreatePreviewSource(source, HighResolutionProcessingPolicy.CurrentProfile.PreviewMaxLongSide)
                : source;
        }

        int longestSide = Math.Max(source.PixelWidth, source.PixelHeight);
        int settingMaxLongSide = PreviewSettings.UseOriginalSize
            ? PreviewSettings.MaximumMaxLongSidePixels
            : Math.Clamp(
                PreviewSettings.MaxLongSidePixels,
                PreviewSettings.MinimumMaxLongSidePixels,
                PreviewSettings.MaximumMaxLongSidePixels);
        int maxLongSide = visibleMaxLongSide is null
            ? settingMaxLongSide
            : Math.Min(
                settingMaxLongSide,
                Math.Clamp(visibleMaxLongSide.Value, 320, PreviewSettings.MaximumMaxLongSidePixels));
        if (longestSide <= maxLongSide)
        {
            return source;
        }

        return HighResolutionProcessingPolicy.CreatePreviewSource(source, maxLongSide);
    }
}
