using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class CSharpPreviewEngine : IPreviewEngine
{
    private const double AdjustmentEpsilon = 0.001;
    private const double ExposureOffsetScale = 2.55;
    private const double ExposureHighlightProtectionStart = 0.72;
    private const double ExposureHighlightProtectionEnd = 1.0;

    public BitmapSource Render(BitmapSource source, PreviewAdjustment adjustment)
    {
        if (!HasEffectiveAdjustment(adjustment))
        {
            return source;
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);

        double exposureOffset = GetExposureOffset(adjustment.Exposure);
        double contrastFactor = GetContrastFactor(adjustment.Contrast);
        double saturationFactor = GetSaturationFactor(adjustment.Saturation);
        (double redGain, double greenGain, double blueGain) = GetWhiteBalanceGains(adjustment.WhiteBalance);
        for (int index = 0; index < pixels.Length; index += 4)
        {
            double blue = ApplyToneValue(pixels[index], adjustment.Exposure, exposureOffset, contrastFactor);
            double green = ApplyToneValue(pixels[index + 1], adjustment.Exposure, exposureOffset, contrastFactor);
            double red = ApplyToneValue(pixels[index + 2], adjustment.Exposure, exposureOffset, contrastFactor);
            (red, green, blue) = ApplySaturation(red, green, blue, saturationFactor);
            red *= redGain;
            green *= greenGain;
            blue *= blueGain;
            (red, green, blue) = ApplyCurve(red, green, blue, adjustment);

            pixels[index] = ClampToByte(blue);
            pixels[index + 1] = ClampToByte(green);
            pixels[index + 2] = ClampToByte(red);
        }

        if (Math.Abs(adjustment.ToneEven) >= AdjustmentEpsilon)
        {
            pixels = ApplySkinToneEvening(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.ToneEven);
        }

        if (Math.Abs(adjustment.BlemishRemove) >= AdjustmentEpsilon)
        {
            pixels = ApplyBlemishRemoval(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.BlemishRemove);
        }

        if (Math.Abs(adjustment.SkinSmooth) >= AdjustmentEpsilon)
        {
            pixels = ApplySkinTextureSmoothing(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.SkinSmooth);
        }

        if (Math.Abs(adjustment.PoreClean) >= AdjustmentEpsilon)
        {
            pixels = ApplyPoreCleanup(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.PoreClean);
        }

        if (Math.Abs(adjustment.OvalFace) >= AdjustmentEpsilon)
        {
            pixels = ApplyOvalFaceWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.OvalFace, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.FaceBalance) >= AdjustmentEpsilon)
        {
            pixels = ApplyFaceBalanceWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.FaceBalance, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.CheekboneSoften) >= AdjustmentEpsilon)
        {
            pixels = ApplyCheekboneSoftenWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.CheekboneSoften, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.JawlineDefine) >= AdjustmentEpsilon)
        {
            pixels = ApplyJawlineDefine(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.JawlineDefine, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.ChinLength) >= AdjustmentEpsilon)
        {
            pixels = ApplyChinLengthWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.ChinLength, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.ChinWidth) >= AdjustmentEpsilon)
        {
            pixels = ApplyChinWidthWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.ChinWidth, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.BlurSharpen) >= AdjustmentEpsilon)
        {
            pixels = ApplyBlurSharpen(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.BlurSharpen);
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

    public bool HasEffectiveAdjustment(PreviewAdjustment adjustment)
    {
        return Math.Abs(adjustment.Exposure) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.Contrast) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.Saturation) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.WhiteBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.BlurSharpen) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.BlemishRemove) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.SkinSmooth) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.PoreClean) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ToneEven) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.OvalFace) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.FaceBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.CheekboneSoften) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.JawlineDefine) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ChinLength) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ChinWidth) >= AdjustmentEpsilon ||
               (Math.Abs(adjustment.CurveAmount) >= AdjustmentEpsilon && !IsIdentityLookupTable(adjustment.CurveLookup));
    }

    private static bool IsIdentityLookupTable(byte[] lookup)
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

    private static double GetExposureOffset(double exposure)
    {
        double normalized = Math.Clamp(exposure, -15, 15);
        return normalized * ExposureOffsetScale;
    }

    private static double GetContrastFactor(double contrast)
    {
        double normalized = Math.Clamp(contrast, -25, 25);
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

    private static double ApplyToneValue(byte value, double exposure, double exposureOffset, double contrastFactor)
    {
        double exposedValue = ApplyExposureValue(value, exposure, exposureOffset);
        return (exposedValue - 128) * contrastFactor + 128;
    }

    private static double ApplyExposureValue(byte value, double exposure, double exposureOffset)
    {
        if (exposure >= 0)
        {
            return value + exposureOffset;
        }

        double normalizedValue = value / 255d;
        double highlightProtection = SmoothStep(
            ExposureHighlightProtectionStart,
            ExposureHighlightProtectionEnd,
            normalizedValue);
        double exposureWeight = 1 - highlightProtection;
        return value + exposureOffset * exposureWeight;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double normalized = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return normalized * normalized * (3 - 2 * normalized);
    }

    private static (double Red, double Green, double Blue) ApplySaturation(double red, double green, double blue, double saturationFactor)
    {
        double luminance = red * 0.2126 + green * 0.7152 + blue * 0.0722;
        return (
            luminance + (red - luminance) * saturationFactor,
            luminance + (green - luminance) * saturationFactor,
            luminance + (blue - luminance) * saturationFactor);
    }

    private static (double Red, double Green, double Blue) ApplyCurve(double red, double green, double blue, PreviewAdjustment adjustment)
    {
        if (Math.Abs(adjustment.CurveAmount) < AdjustmentEpsilon)
        {
            return (red, green, blue);
        }

        if (adjustment.CurveChannel is CurveChannel.All or CurveChannel.Red)
        {
            red = ApplyCurveChannel(red, adjustment.CurveAmount, adjustment.CurveLookup);
        }

        if (adjustment.CurveChannel is CurveChannel.All or CurveChannel.Green)
        {
            green = ApplyCurveChannel(green, adjustment.CurveAmount, adjustment.CurveLookup);
        }

        if (adjustment.CurveChannel is CurveChannel.All or CurveChannel.Blue)
        {
            blue = ApplyCurveChannel(blue, adjustment.CurveAmount, adjustment.CurveLookup);
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

    private static byte[] ApplySkinTextureSmoothing(byte[] source, int width, int height, int stride, double skinSmooth)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(skinSmooth, 0, 100) / 100 * 0.55;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double detailProtection = SmoothStep(6, 28, detail);
            double localAmount = amount * (1 - detailProtection);

            result[index] = BlendChannel(source[index], blurred[index], localAmount);
            result[index + 1] = BlendChannel(source[index + 1], blurred[index + 1], localAmount);
            result[index + 2] = BlendChannel(source[index + 2], blurred[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplySkinToneEvening(byte[] source, int width, int height, int stride, double toneEven)
    {
        byte[] blurred = CreateRepeatedSoftBlur(source, width, height, stride, 3);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(toneEven, 0, 100) / 100 * 0.36;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double edgeProtection = SmoothStep(10, 34, detail);
            double localAmount = amount * (1 - edgeProtection);

            result[index] = BlendChannel(source[index], blurred[index], localAmount);
            result[index + 1] = BlendChannel(source[index + 1], blurred[index + 1], localAmount);
            result[index + 2] = BlendChannel(source[index + 2], blurred[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplyBlemishRemoval(byte[] source, int width, int height, int stride, double blemishRemove)
    {
        byte[] localAverage = CreateRepeatedSoftBlur(source, width, height, stride, 2);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(blemishRemove, 0, 100) / 100 * 0.68;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
            double darkDetail = averageLuminance - sourceLuminance;
            double blemishWeight = SmoothStep(5, 18, darkDetail) * (1 - SmoothStep(45, 85, darkDetail));
            double localAmount = amount * blemishWeight;

            result[index] = BlendChannel(source[index], localAverage[index], localAmount);
            result[index + 1] = BlendChannel(source[index + 1], localAverage[index + 1], localAmount);
            result[index + 2] = BlendChannel(source[index + 2], localAverage[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplyPoreCleanup(byte[] source, int width, int height, int stride, double poreClean)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(poreClean, 0, 100) / 100 * 0.42;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double smallTextureWeight = SmoothStep(2, 9, detail) * (1 - SmoothStep(18, 36, detail));
            double localAmount = amount * smallTextureWeight;

            result[index] = BlendChannel(source[index], blurred[index], localAmount);
            result[index + 1] = BlendChannel(source[index + 1], blurred[index + 1], localAmount);
            result[index + 2] = BlendChannel(source[index + 2], blurred[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplyOvalFaceWarp(byte[] source, int width, int height, int stride, double ovalFace, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(ovalFace, 0, 100) / 100 * 0.13;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int targetIndex = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, targetIndex, targetIndex);
                    continue;
                }

                double feather = 1 - SmoothStep(0.58, 1, distance);
                double lowerFaceWeight = SmoothStep(-0.15, 0.85, normalizedY);
                double sideWeight = SmoothStep(0.18, 0.95, Math.Abs(normalizedX));
                double localAmount = amount * feather * lowerFaceWeight * sideWeight;
                double sourceX = centerX + (x - centerX) * (1 + localAmount);
                SamplePixel(source, width, height, stride, sourceX, y, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyFaceBalanceWarp(byte[] source, int width, int height, int stride, double faceBalance, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double shiftPixels = Math.Clamp(faceBalance, -100, 100) / 100 * radiusX * 0.08;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int targetIndex = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, targetIndex, targetIndex);
                    continue;
                }

                double feather = 1 - SmoothStep(0.62, 1, distance);
                double centerWeight = 1 - SmoothStep(0.1, 0.95, Math.Abs(normalizedX));
                double sourceX = x - shiftPixels * feather * centerWeight;
                SamplePixel(source, width, height, stride, sourceX, y, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyCheekboneSoftenWarp(byte[] source, int width, int height, int stride, double cheekboneSoften, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(cheekboneSoften, 0, 100) / 100 * 0.09;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int targetIndex = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, targetIndex, targetIndex);
                    continue;
                }

                double feather = 1 - SmoothStep(0.56, 1, distance);
                double cheekYWeight = SmoothStep(-0.42, -0.02, normalizedY) * (1 - SmoothStep(0.32, 0.62, normalizedY));
                double sideWeight = SmoothStep(0.32, 0.92, Math.Abs(normalizedX));
                double localAmount = amount * feather * cheekYWeight * sideWeight;
                double sourceX = centerX + (x - centerX) * (1 + localAmount);
                SamplePixel(source, width, height, stride, sourceX, y, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyJawlineDefine(byte[] source, int width, int height, int stride, double jawlineDefine, FaceWorkArea faceWorkArea)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(jawlineDefine, 0, 100) / 100 * 0.78;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, index, index);
                    continue;
                }

                double feather = 1 - SmoothStep(0.62, 1, distance);
                double lowerWeight = SmoothStep(0.18, 0.92, normalizedY);
                double sideWeight = SmoothStep(0.26, 0.9, Math.Abs(normalizedX));
                double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
                double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
                double detail = Math.Abs(sourceLuminance - blurLuminance);
                double edgeWeight = SmoothStep(2, 16, detail) * (1 - SmoothStep(72, 112, detail));
                double localAmount = amount * feather * lowerWeight * sideWeight * edgeWeight;

                result[index] = ClampToByte(source[index] + (source[index] - blurred[index]) * localAmount);
                result[index + 1] = ClampToByte(source[index + 1] + (source[index + 1] - blurred[index + 1]) * localAmount);
                result[index + 2] = ClampToByte(source[index + 2] + (source[index + 2] - blurred[index + 2]) * localAmount);
                result[index + 3] = source[index + 3];
            }
        }

        return result;
    }

    private static byte[] ApplyChinLengthWarp(byte[] source, int width, int height, int stride, double chinLength, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double shiftPixels = Math.Clamp(chinLength, -100, 100) / 100 * radiusY * 0.07;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int targetIndex = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, targetIndex, targetIndex);
                    continue;
                }

                double feather = 1 - SmoothStep(0.66, 1, distance);
                double chinYWeight = SmoothStep(0.38, 0.96, normalizedY);
                double centerWeight = 1 - SmoothStep(0.08, 0.58, Math.Abs(normalizedX));
                double lowerLimit = 1 - SmoothStep(0.88, 1, normalizedY);
                double localShift = shiftPixels * feather * chinYWeight * centerWeight * lowerLimit;
                SamplePixel(source, width, height, stride, x, y - localShift, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyChinWidthWarp(byte[] source, int width, int height, int stride, double chinWidth, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(chinWidth, -100, 100) / 100 * 0.13;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            for (int x = 0; x < width; x++)
            {
                int targetIndex = y * stride + x * 4;
                double normalizedX = (x - centerX) / radiusX;
                double distance = normalizedX * normalizedX + normalizedY * normalizedY;
                if (distance >= 1)
                {
                    CopyPixel(source, result, targetIndex, targetIndex);
                    continue;
                }

                double feather = 1 - SmoothStep(0.66, 1, distance);
                double chinYWeight = SmoothStep(0.34, 0.96, normalizedY);
                double centerWeight = 1 - SmoothStep(0.12, 0.72, Math.Abs(normalizedX));
                double sideFalloff = 1 - SmoothStep(0.62, 0.94, Math.Abs(normalizedX));
                double localAmount = amount * feather * chinYWeight * centerWeight * sideFalloff;
                double sourceX = centerX + (x - centerX) * (1 - localAmount);
                SamplePixel(source, width, height, stride, sourceX, y, result, targetIndex);
            }
        }

        return result;
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

    private static byte[] CreateRepeatedSoftBlur(byte[] source, int width, int height, int stride, int passes)
    {
        byte[] result = source;
        for (int pass = 0; pass < passes; pass++)
        {
            result = CreateSoftBlur(result, width, height, stride);
        }

        return result;
    }

    private static byte BlendChannel(byte source, byte target, double amount)
    {
        return ClampToByte(source + (target - source) * amount);
    }

    private static void CopyPixel(byte[] source, byte[] target, int sourceIndex, int targetIndex)
    {
        target[targetIndex] = source[sourceIndex];
        target[targetIndex + 1] = source[sourceIndex + 1];
        target[targetIndex + 2] = source[sourceIndex + 2];
        target[targetIndex + 3] = source[sourceIndex + 3];
    }

    private static void SamplePixel(byte[] source, int width, int height, int stride, double x, double y, byte[] target, int targetIndex)
    {
        double clampedX = Math.Clamp(x, 0, width - 1);
        double clampedY = Math.Clamp(y, 0, height - 1);
        int x0 = (int)Math.Floor(clampedX);
        int y0 = (int)Math.Floor(clampedY);
        int x1 = Math.Min(width - 1, x0 + 1);
        int y1 = Math.Min(height - 1, y0 + 1);
        double tx = clampedX - x0;
        double ty = clampedY - y0;

        int topLeft = y0 * stride + x0 * 4;
        int topRight = y0 * stride + x1 * 4;
        int bottomLeft = y1 * stride + x0 * 4;
        int bottomRight = y1 * stride + x1 * 4;
        for (int channel = 0; channel < 4; channel++)
        {
            double top = source[topLeft + channel] + (source[topRight + channel] - source[topLeft + channel]) * tx;
            double bottom = source[bottomLeft + channel] + (source[bottomRight + channel] - source[bottomLeft + channel]) * tx;
            target[targetIndex + channel] = ClampToByte(top + (bottom - top) * ty);
        }
    }

    private static double GetLuminance(byte red, byte green, byte blue)
    {
        return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
