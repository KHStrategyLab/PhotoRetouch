using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;
public static class PhotoAdjustmentEngine
{
    private const double AdjustmentEpsilon = 0.001;

    public static byte[] CreateIdentityLookupTable()
    {
        byte[] lookup = new byte[256];
        for (int index = 0; index < lookup.Length; index++)
        {
            lookup[index] = (byte)index;
        }

        return lookup;
    }

    public static bool HasEffectiveAdjustment(
        double brightness,
        double contrast,
        double saturation,
        double whiteBalance,
        double blurSharpen,
        double curveAmount,
        byte[] curveLookup)
    {
        return Math.Abs(brightness) >= AdjustmentEpsilon ||
               Math.Abs(contrast) >= AdjustmentEpsilon ||
               Math.Abs(saturation) >= AdjustmentEpsilon ||
               Math.Abs(whiteBalance) >= AdjustmentEpsilon ||
               Math.Abs(blurSharpen) >= AdjustmentEpsilon ||
               (Math.Abs(curveAmount) >= AdjustmentEpsilon && !IsIdentityLookupTable(curveLookup));
    }

    public static bool IsIdentityLookupTable(byte[] lookup)
    {
        if (lookup.Length != 256)
        {
            return false;
        }

        for (int index = 0; index < lookup.Length; index++)
        {
            if (lookup[index] != index)
            {
                return false;
            }
        }

        return true;
    }

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

    public static BitmapSource ApplyBasicTone(
        BitmapSource source,
        double brightness,
        double contrast,
        double saturation,
        double whiteBalance,
        double blurSharpen,
        double curveAmount,
        CurveChannel curveChannel,
        byte[] curveLookup)
    {
        if (!HasEffectiveAdjustment(brightness, contrast, saturation, whiteBalance, blurSharpen, curveAmount, curveLookup))
        {
            return source;
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        double brightnessOffset = brightness * 2.55;
        double contrastFactor = GetContrastFactor(contrast);
        double saturationFactor = GetSaturationFactor(saturation);
        (double redGain, double greenGain, double blueGain) = GetWhiteBalanceGains(whiteBalance);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            double blue = ApplyToneValue(pixels[index], brightnessOffset, contrastFactor);
            double green = ApplyToneValue(pixels[index + 1], brightnessOffset, contrastFactor);
            double red = ApplyToneValue(pixels[index + 2], brightnessOffset, contrastFactor);
            (red, green, blue) = ApplySaturation(red, green, blue, saturationFactor);
            red *= redGain;
            green *= greenGain;
            blue *= blueGain;
            (red, green, blue) = ApplyCurve(red, green, blue, curveAmount, curveChannel, curveLookup);

            pixels[index] = ClampToByte(blue);
            pixels[index + 1] = ClampToByte(green);
            pixels[index + 2] = ClampToByte(red);
        }

        if (Math.Abs(blurSharpen) >= 0.001)
        {
            pixels = ApplyBlurSharpen(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, blurSharpen);
        }

        BitmapSource adjusted = BitmapSource.Create(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bitmap.DpiX,
            bitmap.DpiY,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
        adjusted.Freeze();
        return adjusted;
    }

    private static double GetContrastFactor(double contrast)
    {
        double normalized = Math.Clamp(contrast, -100, 100);
        return normalized >= 0
            ? 1 + normalized / 50
            : 1 + normalized / 100;
    }

    private static double GetSaturationFactor(double saturation)
    {
        double normalized = Math.Clamp(saturation, -100, 100);
        return normalized >= 0
            ? 1 + normalized / 100
            : 1 + normalized / 100;
    }

    private static (double Red, double Green, double Blue) GetWhiteBalanceGains(double whiteBalance)
    {
        double normalized = Math.Clamp(whiteBalance, -100, 100) / 100;
        double redGain = 1 + normalized * 0.18;
        double blueGain = 1 - normalized * 0.18;
        double greenGain = 1 - Math.Abs(normalized) * 0.025;
        return (redGain, greenGain, blueGain);
    }

    private static double ApplyToneValue(byte value, double brightnessOffset, double contrastFactor)
    {
        return ((value + brightnessOffset) - 128) * contrastFactor + 128;
    }

    private static (double Red, double Green, double Blue) ApplySaturation(double red, double green, double blue, double saturationFactor)
    {
        double luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722;
        return (
            luminance + (red - luminance) * saturationFactor,
            luminance + (green - luminance) * saturationFactor,
            luminance + (blue - luminance) * saturationFactor);
    }

    private static (double Red, double Green, double Blue) ApplyCurve(
        double red,
        double green,
        double blue,
        double curveAmount,
        CurveChannel curveChannel,
        byte[] curveLookup)
    {
        if (Math.Abs(curveAmount) < 0.001)
        {
            return (red, green, blue);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Red)
        {
            red = ApplyCurveChannel(red, curveAmount, curveLookup);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Green)
        {
            green = ApplyCurveChannel(green, curveAmount, curveLookup);
        }

        if (curveChannel is CurveChannel.All or CurveChannel.Blue)
        {
            blue = ApplyCurveChannel(blue, curveAmount, curveLookup);
        }

        return (red, green, blue);
    }

    private static double ApplyCurveChannel(double value, double curveAmount, byte[] curveLookup)
    {
        int index = Math.Clamp((int)Math.Round(value), 0, 255);
        double amount = Math.Clamp(Math.Abs(curveAmount), 0, 100) / 100;
        double curvedValue = curveLookup[Math.Clamp(index, 0, curveLookup.Length - 1)];
        return value + (curvedValue - value) * amount;
    }

    private static byte[] ApplyBlurSharpen(byte[] source, int width, int height, int stride, double blurSharpen)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(Math.Abs(blurSharpen), 0, 100) / 100;

        if (blurSharpen < 0)
        {
            for (int index = 0; index < source.Length; index += 4)
            {
                result[index] = BlendChannel(source[index], blurred[index], amount);
                result[index + 1] = BlendChannel(source[index + 1], blurred[index + 1], amount);
                result[index + 2] = BlendChannel(source[index + 2], blurred[index + 2], amount);
                result[index + 3] = source[index + 3];
            }

            return result;
        }

        double sharpenAmount = amount * 1.4;
        for (int index = 0; index < source.Length; index += 4)
        {
            result[index] = ClampToByte(source[index] + (source[index] - blurred[index]) * sharpenAmount);
            result[index + 1] = ClampToByte(source[index + 1] + (source[index + 1] - blurred[index + 1]) * sharpenAmount);
            result[index + 2] = ClampToByte(source[index + 2] + (source[index + 2] - blurred[index + 2]) * sharpenAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] CreateSoftBlur(byte[] source, int width, int height, int stride)
    {
        byte[] result = new byte[source.Length];
        int[] kernel =
        {
            1, 2, 1,
            2, 4, 2,
            1, 2, 1
        };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int blue = 0;
                int green = 0;
                int red = 0;
                int weightSum = 0;

                for (int ky = -1; ky <= 1; ky++)
                {
                    int sampleY = Math.Clamp(y + ky, 0, height - 1);
                    for (int kx = -1; kx <= 1; kx++)
                    {
                        int sampleX = Math.Clamp(x + kx, 0, width - 1);
                        int weight = kernel[(ky + 1) * 3 + (kx + 1)];
                        int sampleIndex = sampleY * stride + sampleX * 4;
                        blue += source[sampleIndex] * weight;
                        green += source[sampleIndex + 1] * weight;
                        red += source[sampleIndex + 2] * weight;
                        weightSum += weight;
                    }
                }

                int targetIndex = y * stride + x * 4;
                result[targetIndex] = (byte)(blue / weightSum);
                result[targetIndex + 1] = (byte)(green / weightSum);
                result[targetIndex + 2] = (byte)(red / weightSum);
                result[targetIndex + 3] = source[targetIndex + 3];
            }
        }

        return result;
    }

    private static byte BlendChannel(byte source, byte target, double amount)
    {
        return ClampToByte(source + (target - source) * amount);
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}

