using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public static class PreviewSourceFactory
{
    public static BitmapSource CreateEffectPreviewSource(BitmapSource source, int? visibleMaxLongSide = null)
    {
        if (PreviewSettings.UseOriginalSize && visibleMaxLongSide is null)
        {
            return source;
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

        double scale = (double)maxLongSide / longestSide;
        TransformedBitmap preview = new(source, new ScaleTransform(scale, scale));
        preview.Freeze();
        return preview;
    }
}
