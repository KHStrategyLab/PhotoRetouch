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

        if (Math.Abs(adjustment.AcneRemove) >= AdjustmentEpsilon)
        {
            pixels = ApplyAcneRemoval(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.AcneRemove);
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

        if (Math.Abs(adjustment.FaceSymmetry) >= AdjustmentEpsilon)
        {
            pixels = ApplyFaceSymmetryWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.FaceSymmetry, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.EyeHeightBalance) >= AdjustmentEpsilon)
        {
            pixels = ApplyPairedFeatureHeightBalanceWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.EyeHeightBalance, adjustment.FaceWorkArea, -0.42, -0.08, 0.045);
        }

        if (Math.Abs(adjustment.BrowHeightBalance) >= AdjustmentEpsilon)
        {
            pixels = ApplyPairedFeatureHeightBalanceWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.BrowHeightBalance, adjustment.FaceWorkArea, -0.68, -0.34, 0.038);
        }

        if (Math.Abs(adjustment.NoseCenterBalance) >= AdjustmentEpsilon)
        {
            pixels = ApplyNoseCenterBalanceWarp(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.NoseCenterBalance, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.DoubleChin) >= AdjustmentEpsilon)
        {
            pixels = ApplyDoubleChinSoften(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.DoubleChin, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.NeckJawEdge) >= AdjustmentEpsilon)
        {
            pixels = ApplyNeckJawEdgeRefine(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.NeckJawEdge, adjustment.FaceWorkArea);
        }

        if (Math.Abs(adjustment.BackgroundColorAmount) >= AdjustmentEpsilon)
        {
            pixels = ApplySolidBackgroundColorPreview(pixels, adjustment.BackgroundColor, adjustment.BackgroundColorAmount);
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
               Math.Abs(adjustment.AcneRemove) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.SkinSmooth) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.PoreClean) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ToneEven) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.OvalFace) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.FaceBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.CheekboneSoften) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.JawlineDefine) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ChinLength) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.ChinWidth) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.FaceSymmetry) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.EyeHeightBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.BrowHeightBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.NoseCenterBalance) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.DoubleChin) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.NeckJawEdge) >= AdjustmentEpsilon ||
               Math.Abs(adjustment.BackgroundColorAmount) >= AdjustmentEpsilon ||
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
        double amount = Math.Clamp(skinSmooth, 0, 100) / 100 * 0.78;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double detailProtection = SmoothStep(12, 44, detail);
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
        double amount = Math.Clamp(toneEven, 0, 100) / 100 * 0.52;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double edgeProtection = SmoothStep(16, 50, detail);
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
        double amount = Math.Clamp(blemishRemove, 0, 100) / 100 * 0.9;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
            double darkDetail = averageLuminance - sourceLuminance;
            double redExcess = source[index + 2] - Math.Max(source[index + 1], source[index]);
            double averageRedExcess = localAverage[index + 2] - Math.Max(localAverage[index + 1], localAverage[index]);
            double redSpotDetail = Math.Max(0, redExcess - averageRedExcess * 0.35);
            double darkWeight = SmoothStep(2, 14, darkDetail) * (1 - SmoothStep(58, 96, darkDetail));
            double redWeight = SmoothStep(5, 24, redSpotDetail) * (1 - SmoothStep(70, 116, redSpotDetail));
            double skinRangeWeight = SmoothStep(36, 78, sourceLuminance) * (1 - SmoothStep(226, 250, sourceLuminance));
            double blemishWeight = Math.Max(darkWeight, redWeight * 0.8) * skinRangeWeight;
            double baselineCleanup = 0.08 * skinRangeWeight;
            blemishWeight = Math.Max(blemishWeight, baselineCleanup);
            double localAmount = amount * blemishWeight;

            result[index] = BlendChannel(source[index], localAverage[index], localAmount);
            result[index + 1] = BlendChannel(source[index + 1], localAverage[index + 1], localAmount);
            result[index + 2] = BlendChannel(source[index + 2], localAverage[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplyAcneRemoval(byte[] source, int width, int height, int stride, double acneRemove)
    {
        byte[] localAverage = CreateRepeatedSoftBlur(source, width, height, stride, 2);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(acneRemove, 0, 100) / 100 * 0.86;

        for (int index = 0; index < source.Length; index += 4)
        {
            byte blue = source[index];
            byte green = source[index + 1];
            byte red = source[index + 2];
            double sourceLuminance = GetLuminance(red, green, blue);
            double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
            double redExcess = red - Math.Max(green, blue);
            double averageRedExcess = localAverage[index + 2] - Math.Max(localAverage[index + 1], localAverage[index]);
            double localRedness = Math.Max(0, redExcess - averageRedExcess * 0.45);
            double darkDetail = Math.Max(0, averageLuminance - sourceLuminance);
            double rednessWeight = SmoothStep(4, 22, localRedness) * (1 - SmoothStep(72, 118, localRedness));
            double spotWeight = SmoothStep(2, 14, darkDetail) * (1 - SmoothStep(54, 92, darkDetail));
            double skinRangeWeight = SmoothStep(34, 76, sourceLuminance) * (1 - SmoothStep(226, 250, sourceLuminance));
            double acneWeight = Math.Max(rednessWeight, spotWeight * 0.8) * skinRangeWeight;
            acneWeight = Math.Max(acneWeight, 0.05 * skinRangeWeight);
            double localAmount = amount * acneWeight;

            result[index] = BlendChannel(blue, localAverage[index], localAmount * 0.68);
            result[index + 1] = BlendChannel(green, localAverage[index + 1], localAmount * 0.76);
            result[index + 2] = BlendChannel(red, localAverage[index + 2], localAmount);
            result[index + 3] = source[index + 3];
        }

        return result;
    }

    private static byte[] ApplyPoreCleanup(byte[] source, int width, int height, int stride, double poreClean)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(poreClean, 0, 100) / 100 * 0.62;

        for (int index = 0; index < source.Length; index += 4)
        {
            double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
            double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
            double detail = Math.Abs(sourceLuminance - blurLuminance);
            double smallTextureWeight = SmoothStep(2, 9, detail) * (1 - SmoothStep(18, 36, detail));
            smallTextureWeight = Math.Max(smallTextureWeight, 0.04 * (1 - SmoothStep(30, 70, detail)));
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

    private static byte[] ApplyFaceSymmetryWarp(byte[] source, int width, int height, int stride, double faceSymmetry, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(faceSymmetry, -100, 100) / 100 * 0.075;

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

                double feather = 1 - SmoothStep(0.6, 1, distance);
                double verticalWeight = SmoothStep(-0.35, 0.88, normalizedY);
                double sideWeight = SmoothStep(0.18, 0.92, Math.Abs(normalizedX));
                double sideDirection = normalizedX >= 0 ? 1 : -1;
                double localAmount = amount * sideDirection * feather * verticalWeight * sideWeight;
                double sourceX = centerX + (x - centerX) * (1 + localAmount);
                SamplePixel(source, width, height, stride, sourceX, y, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyPairedFeatureHeightBalanceWarp(
        byte[] source,
        int width,
        int height,
        int stride,
        double balance,
        FaceWorkArea faceWorkArea,
        double bandTop,
        double bandBottom,
        double strength)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double shiftPixels = Math.Clamp(balance, -100, 100) / 100 * radiusY * strength;

        for (int y = 0; y < height; y++)
        {
            double normalizedY = (y - centerY) / radiusY;
            double verticalWeight = SmoothStep(bandTop, (bandTop + bandBottom) / 2, normalizedY) *
                (1 - SmoothStep((bandTop + bandBottom) / 2, bandBottom, normalizedY));
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
                double pairWeight = SmoothStep(0.18, 0.42, Math.Abs(normalizedX)) *
                    (1 - SmoothStep(0.68, 0.9, Math.Abs(normalizedX)));
                double sideDirection = normalizedX >= 0 ? 1 : -1;
                double localShift = shiftPixels * sideDirection * feather * verticalWeight * pairWeight;
                SamplePixel(source, width, height, stride, x, y - localShift, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyNoseCenterBalanceWarp(byte[] source, int width, int height, int stride, double noseCenterBalance, FaceWorkArea faceWorkArea)
    {
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double shiftPixels = Math.Clamp(noseCenterBalance, -100, 100) / 100 * radiusX * 0.045;

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
                double verticalWeight = SmoothStep(-0.28, 0.06, normalizedY) *
                    (1 - SmoothStep(0.42, 0.72, normalizedY));
                double centerWeight = 1 - SmoothStep(0.04, 0.32, Math.Abs(normalizedX));
                double localShift = shiftPixels * feather * verticalWeight * centerWeight;
                SamplePixel(source, width, height, stride, x - localShift, y, result, targetIndex);
            }
        }

        return result;
    }

    private static byte[] ApplyDoubleChinSoften(byte[] source, int width, int height, int stride, double doubleChin, FaceWorkArea faceWorkArea)
    {
        byte[] blurred = CreateRepeatedSoftBlur(source, width, height, stride, 2);
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(doubleChin, 0, 100) / 100;

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

                double feather = 1 - SmoothStep(0.66, 1, distance);
                double lowerWeight = SmoothStep(0.58, 0.96, normalizedY);
                double centerWeight = 1 - SmoothStep(0.18, 0.78, Math.Abs(normalizedX));
                double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
                double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
                double foldDetail = Math.Abs(sourceLuminance - blurLuminance);
                double detailWeight = SmoothStep(4, 24, foldDetail) * (1 - SmoothStep(68, 106, foldDetail));
                double shadowWeight = 1 - SmoothStep(110, 196, sourceLuminance);
                double localAmount = amount * 0.42 * feather * lowerWeight * centerWeight * (0.45 + detailWeight * 0.55);
                double shadowLift = amount * 12 * feather * lowerWeight * centerWeight * shadowWeight;

                result[index] = ClampToByte(source[index] + (blurred[index] - source[index]) * localAmount + shadowLift);
                result[index + 1] = ClampToByte(source[index + 1] + (blurred[index + 1] - source[index + 1]) * localAmount + shadowLift);
                result[index + 2] = ClampToByte(source[index + 2] + (blurred[index + 2] - source[index + 2]) * localAmount + shadowLift);
                result[index + 3] = source[index + 3];
            }
        }

        return result;
    }

    private static byte[] ApplyNeckJawEdgeRefine(byte[] source, int width, int height, int stride, double neckJawEdge, FaceWorkArea faceWorkArea)
    {
        byte[] blurred = CreateSoftBlur(source, width, height, stride);
        byte[] result = new byte[source.Length];
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double amount = Math.Clamp(neckJawEdge, 0, 100) / 100;

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

                double feather = 1 - SmoothStep(0.64, 1, distance);
                double bandWeight = SmoothStep(0.46, 0.68, normalizedY) *
                    (1 - SmoothStep(0.78, 0.96, normalizedY));
                double widthWeight = 1 - SmoothStep(0.38, 0.88, Math.Abs(normalizedX));
                double sourceLuminance = GetLuminance(source[index + 2], source[index + 1], source[index]);
                double blurLuminance = GetLuminance(blurred[index + 2], blurred[index + 1], blurred[index]);
                double detail = Math.Abs(sourceLuminance - blurLuminance);
                double edgeWeight = SmoothStep(3, 18, detail) * (1 - SmoothStep(86, 130, detail));
                double localWeight = feather * bandWeight * widthWeight;
                double sharpenAmount = amount * 0.9 * localWeight * edgeWeight;
                double shadowLift = amount * 5 * localWeight * (1 - SmoothStep(96, 178, sourceLuminance));

                result[index] = ClampToByte(source[index] + (source[index] - blurred[index]) * sharpenAmount + shadowLift);
                result[index + 1] = ClampToByte(source[index + 1] + (source[index + 1] - blurred[index + 1]) * sharpenAmount + shadowLift);
                result[index + 2] = ClampToByte(source[index + 2] + (source[index + 2] - blurred[index + 2]) * sharpenAmount + shadowLift);
                result[index + 3] = source[index + 3];
            }
        }

        return result;
    }

    private static byte[] ApplySolidBackgroundColorPreview(byte[] source, System.Windows.Media.Color color, double backgroundColorAmount)
    {
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(backgroundColorAmount, 0, 100) / 100 * 0.35;

        for (int index = 0; index < source.Length; index += 4)
        {
            result[index] = BlendChannel(source[index], color.B, amount);
            result[index + 1] = BlendChannel(source[index + 1], color.G, amount);
            result[index + 2] = BlendChannel(source[index + 2], color.R, amount);
            result[index + 3] = source[index + 3];
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
