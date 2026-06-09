using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class CSharpPreviewEngine : IPreviewEngine
{
    private const double AdjustmentEpsilon = 0.001;
    private const double ExposureOffsetScale = 2.55;
    private const double ExposureHighlightProtectionStart = 0.72;
    private const double ExposureHighlightProtectionEnd = 1.0;
    private static readonly (int X, int Y)[] SkinPatchSampleOffsets =
    {
        (-6, -2), (-5, 3), (-4, -5), (-3, 6),
        (-2, -7), (2, 7), (3, -6), (4, 5),
        (5, -3), (6, 2), (-8, 0), (8, 0),
        (0, -8), (0, 8), (-7, 7), (7, -7)
    };
    private static readonly (int X, int Y)[] CleanSkinPatchSampleOffsets =
    {
        (-18, -6), (-17, 7), (-15, -13), (-14, 14),
        (-11, -18), (-10, 18), (-6, -22), (-5, 22),
        (5, -22), (6, 22), (10, -18), (11, 18),
        (14, -14), (15, 13), (17, -7), (18, 6),
        (-25, 0), (25, 0), (0, -25), (0, 25),
        (-21, -21), (-21, 21), (21, -21), (21, 21)
    };
    private readonly record struct SkinReference(double Red, double Green, double Blue, double Luminance);

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

        SkinReference skinReference = adjustment.HasManualSkinReference
            ? CreateSkinReference(adjustment.ManualSkinReferenceColor)
            : CreateSkinReference(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.FaceWorkArea);

        if (Math.Abs(adjustment.ToneEven) >= AdjustmentEpsilon)
        {
            pixels = ApplySkinToneEvening(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.ToneEven);
        }

        if (Math.Abs(adjustment.BlemishRemove) >= AdjustmentEpsilon)
        {
            pixels = ApplyBlemishRemoval(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.BlemishRemove, adjustment.FaceWorkArea, skinReference, adjustment.SkinMaskRange, adjustment.SkinTextureProtect);
        }

        if (Math.Abs(adjustment.AcneRemove) >= AdjustmentEpsilon)
        {
            pixels = ApplyAcneRemoval(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.AcneRemove, adjustment.FaceWorkArea, skinReference, adjustment.SkinMaskRange, adjustment.SkinTextureProtect);
        }

        if (Math.Abs(adjustment.MoleAgeSpotRemove) >= AdjustmentEpsilon)
        {
            pixels = ApplyMoleAgeSpotRemoval(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment.MoleAgeSpotRemove, adjustment.FaceWorkArea, skinReference, adjustment.SkinMaskRange, adjustment.SkinTextureProtect);
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

        if (HasEffectiveHairRetouch(adjustment))
        {
            pixels = ApplyHairRetouch(pixels, bitmap.PixelWidth, bitmap.PixelHeight, stride, adjustment);
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
               Math.Abs(adjustment.MoleAgeSpotRemove) >= AdjustmentEpsilon ||
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
               HasEffectiveHairRetouch(adjustment) ||
               (Math.Abs(adjustment.CurveAmount) >= AdjustmentEpsilon && !IsIdentityLookupTable(adjustment.CurveLookup));
    }

    private static bool HasEffectiveHairRetouch(PreviewAdjustment adjustment)
    {
        return Math.Abs(adjustment.HairColorAmount) >= AdjustmentEpsilon ||
            Math.Abs(adjustment.HairGloss) >= AdjustmentEpsilon ||
            Math.Abs(adjustment.GrayHairCover) >= AdjustmentEpsilon;
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

    private static byte[] ApplyBlemishRemoval(byte[] source, int width, int height, int stride, double blemishRemove, FaceWorkArea faceWorkArea, SkinReference skinReference, double skinMaskRange, double skinTextureProtect)
    {
        byte[] localAverage = CreateRepeatedSoftBlur(source, width, height, stride, 2);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(blemishRemove, 0, 100) / 100 * 1.16;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                byte blue = source[index];
                byte green = source[index + 1];
                byte red = source[index + 2];
                double sourceLuminance = GetLuminance(red, green, blue);
                double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
                double darkDetail = averageLuminance - sourceLuminance;
                double redExcess = red - Math.Max(green, blue);
                double averageRedExcess = localAverage[index + 2] - Math.Max(localAverage[index + 1], localAverage[index]);
                double redSpotDetail = Math.Max(0, redExcess - averageRedExcess * 0.35);
                double skinMaskWeight = GetSkinProcessingWeight(
                    source,
                    localAverage,
                    width,
                    height,
                    stride,
                    x,
                    y,
                    index,
                    red,
                    green,
                    blue,
                    sourceLuminance,
                    faceWorkArea,
                    skinReference,
                    skinMaskRange,
                    skinTextureProtect,
                    12,
                    42);
                double lightSpotWeight = SmoothStep(7, 22, darkDetail) * (1 - SmoothStep(42, 82, darkDetail));
                double redSpotWeight = SmoothStep(8, 26, redSpotDetail) * (1 - SmoothStep(62, 108, redSpotDetail));
                double rawBlemishWeight = Math.Max(lightSpotWeight, redSpotWeight * 0.78) * skinMaskWeight;
                double blemishWeight = SmoothStep(0.24, 0.62, rawBlemishWeight);
                double localAmount = Math.Clamp(amount * Math.Clamp(blemishWeight, 0, 0.34), 0, 1);

                if (localAmount < 0.01)
                {
                    CopyPixel(source, result, index, index);
                    continue;
                }

                (byte targetBlue, byte targetGreen, byte targetRed) = GetCleanSkinPatchAverage(
                    source,
                    localAverage,
                    width,
                    height,
                    stride,
                    x,
                    y,
                    skinReference,
                    skinMaskRange);
                result[index] = BlendChannel(blue, targetBlue, localAmount);
                result[index + 1] = BlendChannel(green, targetGreen, localAmount);
                result[index + 2] = BlendChannel(red, targetRed, localAmount);
                result[index + 3] = source[index + 3];
            }
        }

        return result;
    }

    private static byte[] ApplyMoleAgeSpotRemoval(byte[] source, int width, int height, int stride, double moleAgeSpotRemove, FaceWorkArea faceWorkArea, SkinReference skinReference, double skinMaskRange, double skinTextureProtect)
    {
        byte[] result = source;
        double strength = Math.Clamp(moleAgeSpotRemove, 0, 100) / 100;
        int passes = strength > 0.66 ? 3 : strength > 0.28 ? 2 : 1;
        double passAmount = strength * 0.56;
        for (int pass = 0; pass < passes; pass++)
        {
            result = ApplyMoleAgeSpotRemovalPass(result, width, height, stride, passAmount, faceWorkArea, skinReference, skinMaskRange, skinTextureProtect);
        }

        return result;
    }

    private static byte[] ApplyMoleAgeSpotRemovalPass(byte[] source, int width, int height, int stride, double amount, FaceWorkArea faceWorkArea, SkinReference skinReference, double skinMaskRange, double skinTextureProtect)
    {
        byte[] localAverage = CreateRepeatedSoftBlur(source, width, height, stride, 7);
        byte[] result = new byte[source.Length];
        double scaledAmount = Math.Clamp(amount, 0, 1) * 2;
        FaceWorkArea faceArea = faceWorkArea.Clamp();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                byte blue = source[index];
                byte green = source[index + 1];
                byte red = source[index + 2];
                double sourceLuminance = GetLuminance(red, green, blue);
                double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
                double darkDetail = averageLuminance - sourceLuminance;
                double redExcess = red - Math.Max(green, blue);
                double averageRedExcess = localAverage[index + 2] - Math.Max(localAverage[index + 1], localAverage[index]);
                double redSpotDetail = Math.Max(0, redExcess - averageRedExcess * 0.35);
                double warmth = (red + green) / 2d - blue;
                double averageWarmth = (localAverage[index + 2] + localAverage[index + 1]) / 2d - localAverage[index];
                double brownSpotDetail = Math.Max(0, warmth - averageWarmth * 0.45);
                double skinMaskWeight = GetSkinProcessingWeight(
                    source,
                    localAverage,
                    width,
                    height,
                    stride,
                    x,
                    y,
                    index,
                    red,
                    green,
                    blue,
                    sourceLuminance,
                    faceWorkArea,
                    skinReference,
                    skinMaskRange,
                    skinTextureProtect,
                    28,
                    76);
                double surroundingToneWeight = GetSkinToneSimilarity(
                    localAverage[index + 2],
                    localAverage[index + 1],
                    localAverage[index],
                    averageLuminance,
                    skinReference,
                    skinMaskRange);
                double moleSurroundingMaskWeight = GetSkinCandidateAreaWeight(x, y, width, height, faceArea) *
                    surroundingToneWeight *
                    GetFacialFeatureFeatherWeight(x, y, width, height, faceArea);
                double effectiveSkinMaskWeight = Math.Max(skinMaskWeight, moleSurroundingMaskWeight * 0.92);
                (byte targetBlue, byte targetGreen, byte targetRed) = GetCleanSkinPatchAverage(
                    source,
                    localAverage,
                    width,
                    height,
                    stride,
                    x,
                    y,
                    skinReference,
                    skinMaskRange);
                double targetLuminance = GetLuminance(targetRed, targetGreen, targetBlue);
                double targetToneWeight = GetSkinToneSimilarity(targetRed, targetGreen, targetBlue, targetLuminance, skinReference, skinMaskRange);
                double normalizedY = (y - faceArea.CenterY * (height - 1)) / Math.Max(1, faceArea.Height * height / 2);
                double skinBandWeight = SmoothStep(-0.66, -0.46, normalizedY) * (1 - SmoothStep(0.70, 0.90, normalizedY));
                double localStructureWeight = 1 - SmoothStep(24, 58, GetLocalLuminanceGradient(source, width, height, stride, x, y));
                double localDarkOutlierWeight = SmoothStep(16, 42, targetLuminance - sourceLuminance) *
                    (1 - SmoothStep(96, 156, targetLuminance - sourceLuminance));
                double faceAverageDarkOutlierWeight = SmoothStep(34, 86, skinReference.Luminance - sourceLuminance) *
                    (1 - SmoothStep(138, 198, skinReference.Luminance - sourceLuminance));
                double surroundingOutlierMaskWeight = GetSkinCandidateAreaWeight(x, y, width, height, faceArea) *
                    targetToneWeight *
                    surroundingToneWeight *
                    GetFacialFeatureFeatherWeight(x, y, width, height, faceArea) *
                    skinBandWeight *
                    localStructureWeight;
                double absoluteDarkWeight = 1 - SmoothStep(118, 178, sourceLuminance);
                double moleDarkWeight = SmoothStep(10, 30, darkDetail) * (1 - SmoothStep(118, 174, darkDetail)) * absoluteDarkWeight;
                double compactDarkWeight = SmoothStep(18, 42, darkDetail) * (1 - SmoothStep(92, 150, darkDetail)) * absoluteDarkWeight;
                double ageSpotDarkWeight = SmoothStep(8, 24, darkDetail) * (1 - SmoothStep(82, 132, darkDetail));
                double brownWeight = SmoothStep(14, 36, brownSpotDetail) * (1 - SmoothStep(96, 154, brownSpotDetail));
                double redWeight = SmoothStep(14, 36, redSpotDetail) * (1 - SmoothStep(104, 164, redSpotDetail));
                double ageSpotWeight = Math.Min(ageSpotDarkWeight, Math.Max(brownWeight, redWeight * 0.55));
                double faceAverageOutlierWeight = Math.Min(localDarkOutlierWeight, faceAverageDarkOutlierWeight) *
                    SmoothStep(9, 28, darkDetail) *
                    surroundingOutlierMaskWeight;
                double rawSpotWeight = Math.Max(
                    Math.Max(Math.Max(moleDarkWeight, compactDarkWeight * 1.12), ageSpotWeight) * effectiveSkinMaskWeight,
                    faceAverageOutlierWeight);
                double spotWeight = SmoothStep(0.05, 0.34, rawSpotWeight);
                double localAmount = Math.Clamp(scaledAmount * Math.Clamp(spotWeight, 0, 0.42), 0, 1);

                if (localAmount < 0.03)
                {
                    CopyPixel(source, result, index, index);
                    continue;
                }

                double highConfidenceMoleWeight = SmoothStep(0.12, 0.48, Math.Max(Math.Max(moleDarkWeight, compactDarkWeight) * effectiveSkinMaskWeight, faceAverageOutlierWeight));
                double luminanceLift = Math.Max(0, darkDetail) * (0.42 + highConfidenceMoleWeight * 0.18) * scaledAmount * spotWeight * effectiveSkinMaskWeight;
                double warmthReduction = Math.Max(brownSpotDetail, redSpotDetail) * 0.22 * scaledAmount * spotWeight * effectiveSkinMaskWeight;
                double cleanAmount = Math.Clamp(localAmount * (0.88 + highConfidenceMoleWeight * 0.18), 0, 1);

                result[index] = ClampToByte(blue + (targetBlue - blue) * cleanAmount + luminanceLift);
                result[index + 1] = ClampToByte(green + (targetGreen - green) * cleanAmount + luminanceLift);
                result[index + 2] = ClampToByte(red + (targetRed - red) * cleanAmount + luminanceLift * 0.82 - warmthReduction);
                result[index + 3] = source[index + 3];
            }
        }

        return result;
    }

    private static byte[] ApplyAcneRemoval(byte[] source, int width, int height, int stride, double acneRemove, FaceWorkArea faceWorkArea, SkinReference skinReference, double skinMaskRange, double skinTextureProtect)
    {
        byte[] localAverage = CreateRepeatedSoftBlur(source, width, height, stride, 4);
        byte[] result = new byte[source.Length];
        double amount = Math.Clamp(acneRemove, 0, 100) / 100 * 2;

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
            double rednessWeight = SmoothStep(28, 56, localRedness) * (1 - SmoothStep(104, 164, localRedness));
            double spotWeight = SmoothStep(5, 18, darkDetail) * (1 - SmoothStep(72, 118, darkDetail));
            int x = index / 4 % width;
            int y = index / stride;
            double skinMaskWeight = GetSkinProcessingWeight(
                source,
                localAverage,
                width,
                height,
                stride,
                x,
                y,
                index,
                red,
                green,
                blue,
                sourceLuminance,
                faceWorkArea,
                skinReference,
                skinMaskRange,
                skinTextureProtect,
                18,
                56);
            double acneWeight = rednessWeight * Math.Max(spotWeight, 0.28) * skinMaskWeight;
            double localAmount = Math.Clamp(amount * Math.Clamp(acneWeight, 0, 1), 0, 1);
            double rednessReduction = localRedness * amount * rednessWeight * skinMaskWeight * 0.42;
            double luminanceLift = darkDetail * amount * acneWeight * 0.18;

            result[index] = ClampToByte(blue + (localAverage[index] - blue) * localAmount * 0.72 + luminanceLift);
            result[index + 1] = ClampToByte(green + (localAverage[index + 1] - green) * localAmount * 0.82 + luminanceLift);
            result[index + 2] = ClampToByte(red + (localAverage[index + 2] - red) * localAmount - rednessReduction + luminanceLift * 0.7);
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

    private static byte[] ApplyHairRetouch(byte[] source, int width, int height, int stride, PreviewAdjustment adjustment)
    {
        if (adjustment.HairMask is not { } hairMask ||
            hairMask.Width != width ||
            hairMask.Height != height)
        {
            return source;
        }

        byte[] result = new byte[source.Length];
        Array.Copy(source, result, source.Length);

        (double targetRed, double targetGreen, double targetBlue) = EstimateHairCoverColor(
            source,
            width,
            height,
            stride,
            hairMask,
            adjustment.HairColor);
        double grayCoverAmount = Math.Clamp(adjustment.GrayHairCover, 0, 100) / 100 * 0.72;
        double colorAmount = Math.Clamp(adjustment.HairColorAmount, 0, 100) / 100 * 0.34;
        double glossAmount = Math.Clamp(adjustment.HairGloss, 0, 100) / 100 * 0.20;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double maskWeight = Math.Clamp(hairMask[x, y], 0, 1);
                if (maskWeight <= 0.01)
                {
                    continue;
                }

                double blue = source[index];
                double green = source[index + 1];
                double red = source[index + 2];
                double luminance = GetLuminance((byte)red, (byte)green, (byte)blue);
                double maxChannel = Math.Max(red, Math.Max(green, blue));
                double minChannel = Math.Min(red, Math.Min(green, blue));
                double chroma = maxChannel - minChannel;
                double saturation = maxChannel <= 1 ? 0 : chroma / maxChannel;
                double textureProtect = SmoothStep(74, 126, GetLocalLuminanceGradient(source, width, height, stride, x, y));

                double grayCandidateWeight =
                    SmoothStep(112, 210, luminance) *
                    (1 - SmoothStep(0.12, 0.30, saturation)) *
                    (1 - textureProtect * 0.35);
                double grayCoverWeight = maskWeight * grayCoverAmount * grayCandidateWeight;

                double colorWeight = maskWeight * colorAmount * (1 - grayCandidateWeight * 0.45);
                if (colorWeight > 0.001)
                {
                    double targetLuminance = Math.Max(1, GetLuminance(adjustment.HairColor.R, adjustment.HairColor.G, adjustment.HairColor.B));
                    double luminanceScale = Math.Clamp(luminance / targetLuminance, 0.35, 1.75);
                    red = red + (adjustment.HairColor.R * luminanceScale - red) * colorWeight;
                    green = green + (adjustment.HairColor.G * luminanceScale - green) * colorWeight;
                    blue = blue + (adjustment.HairColor.B * luminanceScale - blue) * colorWeight;
                }

                if (grayCoverWeight > 0.001)
                {
                    red = red + (targetRed - red) * grayCoverWeight;
                    green = green + (targetGreen - green) * grayCoverWeight;
                    blue = blue + (targetBlue - blue) * grayCoverWeight;
                }

                double glossWeight = maskWeight * glossAmount * SmoothStep(46, 174, luminance) * (1 - SmoothStep(230, 252, luminance));
                if (glossWeight > 0.001)
                {
                    double highlightLift = 18 * glossWeight;
                    red += highlightLift;
                    green += highlightLift;
                    blue += highlightLift;
                }

                result[index] = ClampToByte(blue);
                result[index + 1] = ClampToByte(green);
                result[index + 2] = ClampToByte(red);
            }
        }

        return result;
    }

    private static (double Red, double Green, double Blue) EstimateHairCoverColor(
        byte[] source,
        int width,
        int height,
        int stride,
        MaskPlane hairMask,
        System.Windows.Media.Color fallbackColor)
    {
        double redSum = 0;
        double greenSum = 0;
        double blueSum = 0;
        double weightSum = 0;

        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                double maskWeight = Math.Clamp(hairMask[x, y], 0, 1);
                if (maskWeight <= 0.05)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                byte blue = source[index];
                byte green = source[index + 1];
                byte red = source[index + 2];
                double luminance = GetLuminance(red, green, blue);
                double maxChannel = Math.Max(red, Math.Max(green, blue));
                double minChannel = Math.Min(red, Math.Min(green, blue));
                double saturation = maxChannel <= 1 ? 0 : (maxChannel - minChannel) / maxChannel;
                double nonGrayWeight = maskWeight * (1 - SmoothStep(122, 214, luminance)) * SmoothStep(0.08, 0.26, saturation);
                if (nonGrayWeight <= 0.001)
                {
                    continue;
                }

                redSum += red * nonGrayWeight;
                greenSum += green * nonGrayWeight;
                blueSum += blue * nonGrayWeight;
                weightSum += nonGrayWeight;
            }
        }

        if (weightSum < 8)
        {
            return (fallbackColor.R, fallbackColor.G, fallbackColor.B);
        }

        return (redSum / weightSum, greenSum / weightSum, blueSum / weightSum);
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

    private static SkinReference CreateSkinReference(byte[] source, int width, int height, int stride, FaceWorkArea faceWorkArea)
    {
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double redSum = 0;
        double greenSum = 0;
        double blueSum = 0;
        double weightSum = 0;

        int step = Math.Max(1, Math.Min(width, height) / 180);
        for (int y = 0; y < height; y += step)
        {
            for (int x = 0; x < width; x += step)
            {
                double faceWeight = GetSkinCandidateAreaWeight(x, y, width, height, faceArea);
                if (faceWeight <= 0)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                byte blue = source[index];
                byte green = source[index + 1];
                byte red = source[index + 2];
                double luminance = GetLuminance(red, green, blue);
                double skinWeight = GetSkinColorWeight(red, green, blue) *
                    SmoothStep(72, 118, luminance) *
                    (1 - SmoothStep(214, 244, luminance)) *
                    faceWeight;
                if (skinWeight <= 0.02)
                {
                    continue;
                }

                redSum += red * skinWeight;
                greenSum += green * skinWeight;
                blueSum += blue * skinWeight;
                weightSum += skinWeight;
            }
        }

        if (weightSum <= 0.001)
        {
            return new SkinReference(186, 139, 115, 148);
        }

        double averageRed = redSum / weightSum;
        double averageGreen = greenSum / weightSum;
        double averageBlue = blueSum / weightSum;
        return new SkinReference(
            averageRed,
            averageGreen,
            averageBlue,
            GetLuminance((byte)Math.Clamp((int)Math.Round(averageRed), 0, 255), (byte)Math.Clamp((int)Math.Round(averageGreen), 0, 255), (byte)Math.Clamp((int)Math.Round(averageBlue), 0, 255)));
    }

    private static SkinReference CreateSkinReference(System.Windows.Media.Color color)
    {
        return new SkinReference(color.R, color.G, color.B, GetLuminance(color.R, color.G, color.B));
    }

    private static double GetSkinProcessingWeight(
        byte[] source,
        byte[] localAverage,
        int width,
        int height,
        int stride,
        int x,
        int y,
        int index,
        byte red,
        byte green,
        byte blue,
        double luminance,
        FaceWorkArea faceWorkArea,
        SkinReference skinReference,
        double skinMaskRange,
        double skinTextureProtect,
        double edgeStart,
        double edgeEnd)
    {
        FaceWorkArea faceArea = faceWorkArea.Clamp();
        double faceWeight = GetSkinCandidateAreaWeight(x, y, width, height, faceArea);
        if (faceWeight <= 0)
        {
            return 0;
        }

        double currentToneWeight = GetSkinToneSimilarity(red, green, blue, luminance, skinReference, skinMaskRange);
        double averageLuminance = GetLuminance(localAverage[index + 2], localAverage[index + 1], localAverage[index]);
        double averageToneWeight = GetSkinToneSimilarity(
            localAverage[index + 2],
            localAverage[index + 1],
            localAverage[index],
            averageLuminance,
            skinReference,
            skinMaskRange);
        double toneWeight = Math.Max(currentToneWeight, averageToneWeight);
        double protect = Math.Clamp(skinTextureProtect, 0, 100) / 100;
        double edgeScale = 1.22 - protect * 0.58;
        double edgeWeight = 1 - SmoothStep(edgeStart * edgeScale, edgeEnd * edgeScale, GetLocalLuminanceGradient(source, width, height, stride, x, y));
        double featureFeatherWeight = GetFacialFeatureFeatherWeight(x, y, width, height, faceArea);
        return Math.Clamp(faceWeight * toneWeight * edgeWeight * featureFeatherWeight, 0, 1);
    }

    private static double GetFaceAreaWeight(int x, int y, int width, int height, FaceWorkArea faceArea)
    {
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double normalizedX = (x - centerX) / radiusX;
        double normalizedY = (y - centerY) / radiusY;
        double distance = normalizedX * normalizedX + normalizedY * normalizedY;
        return 1 - SmoothStep(0.82, 1.1, distance);
    }

    private static double GetSkinCandidateAreaWeight(int x, int y, int width, int height, FaceWorkArea faceArea)
    {
        double centerX = faceArea.CenterX * (width - 1);
        double centerY = faceArea.CenterY * (height - 1);
        double radiusX = Math.Max(1, faceArea.Width * width / 2);
        double radiusY = Math.Max(1, faceArea.Height * height / 2);
        double normalizedX = (x - centerX) / radiusX;
        double normalizedY = (y - centerY) / radiusY;

        double faceCoreDistance = normalizedX * normalizedX / 2.35 + normalizedY * normalizedY / 1.48;
        double faceWeight = 1 - SmoothStep(0.72, 1.08, faceCoreDistance);

        double leftEarWeight = GetEllipseSoftWeight(normalizedX, normalizedY, -1.17, -0.08, 0.38, 0.62);
        double rightEarWeight = GetEllipseSoftWeight(normalizedX, normalizedY, 1.17, -0.08, 0.38, 0.62);
        double earWeight = Math.Max(leftEarWeight, rightEarWeight) * (1 - SmoothStep(-0.58, -0.20, normalizedY)) * (1 - SmoothStep(0.58, 0.92, normalizedY));

        double neckCenterWeight = 1 - SmoothStep(0.45, 0.92, Math.Abs(normalizedX));
        double neckVerticalWeight = SmoothStep(0.55, 0.88, normalizedY) * (1 - SmoothStep(1.42, 1.85, normalizedY));
        double neckWeight = neckCenterWeight * neckVerticalWeight;

        return Math.Clamp(Math.Max(Math.Max(faceWeight, earWeight), neckWeight), 0, 1);
    }

    private static double GetFacialFeatureFeatherWeight(int x, int y, int width, int height, FaceWorkArea faceArea)
    {
        double normalizedX = (x - faceArea.CenterX * (width - 1)) / Math.Max(1, faceArea.Width * width / 2);
        double normalizedY = (y - faceArea.CenterY * (height - 1)) / Math.Max(1, faceArea.Height * height / 2);
        double protection = 0;

        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, -0.42, -0.38, 0.31, 0.20, 0.98));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, 0.42, -0.38, 0.31, 0.20, 0.98));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, -0.42, -0.58, 0.34, 0.15, 0.78));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, 0.42, -0.58, 0.34, 0.15, 0.78));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, 0, -0.12, 0.24, 0.44, 0.42));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, 0, 0.18, 0.38, 0.24, 0.96));
        protection = Math.Max(protection, GetEllipseProtection(normalizedX, normalizedY, 0, 0.46, 0.50, 0.20, 0.90));

        return Math.Clamp(1 - protection, 0, 1);
    }

    private static double GetEllipseProtection(double x, double y, double centerX, double centerY, double radiusX, double radiusY, double strength)
    {
        double dx = (x - centerX) / radiusX;
        double dy = (y - centerY) / radiusY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        return strength * (1 - SmoothStep(0.72, 1.35, distance));
    }

    private static double GetEllipseSoftWeight(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        double dx = (x - centerX) / radiusX;
        double dy = (y - centerY) / radiusY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        return 1 - SmoothStep(0.72, 1.18, distance);
    }

    private static double GetSkinToneSimilarity(byte red, byte green, byte blue, double luminance, SkinReference skinReference, double skinMaskRange)
    {
        double range = Math.Clamp(skinMaskRange, 0, 100) / 100;
        double rangeScale = 0.68 + range * 0.92;
        double redDelta = red - skinReference.Red;
        double greenDelta = green - skinReference.Green;
        double blueDelta = blue - skinReference.Blue;
        double colorDistance = Math.Sqrt(redDelta * redDelta * 0.65 + greenDelta * greenDelta * 0.85 + blueDelta * blueDelta * 0.55);
        double colorWeight = 1 - SmoothStep(34 * rangeScale, 96 * rangeScale, colorDistance);
        double luminanceWeight = 1 - SmoothStep(54 * rangeScale, 132 * rangeScale, Math.Abs(luminance - skinReference.Luminance));
        return Math.Clamp(Math.Max(GetSkinColorWeight(red, green, blue) * 0.65, colorWeight) * luminanceWeight, 0, 1);
    }

    private static (byte Blue, byte Green, byte Red) GetSkinPatchAverage(byte[] source, int width, int height, int stride, int x, int y)
    {
        int blueSum = 0;
        int greenSum = 0;
        int redSum = 0;
        int weightSum = 0;

        foreach ((int offsetX, int offsetY) in SkinPatchSampleOffsets)
        {
            int sampleX = Math.Clamp(x + offsetX, 0, width - 1);
            int sampleY = Math.Clamp(y + offsetY, 0, height - 1);
            int sampleIndex = sampleY * stride + sampleX * 4;
            byte sampleBlue = source[sampleIndex];
            byte sampleGreen = source[sampleIndex + 1];
            byte sampleRed = source[sampleIndex + 2];
            double sampleLuminance = GetLuminance(sampleRed, sampleGreen, sampleBlue);
            double skinWeight = GetSkinColorWeight(sampleRed, sampleGreen, sampleBlue) *
                SmoothStep(28, 70, sampleLuminance) *
                (1 - SmoothStep(238, 254, sampleLuminance));
            int weight = Math.Max(1, (int)Math.Round(skinWeight * 8));
            blueSum += sampleBlue * weight;
            greenSum += sampleGreen * weight;
            redSum += sampleRed * weight;
            weightSum += weight;
        }

        return (
            (byte)(blueSum / weightSum),
            (byte)(greenSum / weightSum),
            (byte)(redSum / weightSum));
    }

    private static (byte Blue, byte Green, byte Red) GetCleanSkinPatchAverage(
        byte[] source,
        byte[] localAverage,
        int width,
        int height,
        int stride,
        int x,
        int y,
        SkinReference skinReference,
        double skinMaskRange)
    {
        double blueSum = 0;
        double greenSum = 0;
        double redSum = 0;
        double weightSum = 0;

        foreach ((int offsetX, int offsetY) in CleanSkinPatchSampleOffsets)
        {
            int sampleX = Math.Clamp(x + offsetX, 0, width - 1);
            int sampleY = Math.Clamp(y + offsetY, 0, height - 1);
            int sampleIndex = sampleY * stride + sampleX * 4;
            byte sampleBlue = source[sampleIndex];
            byte sampleGreen = source[sampleIndex + 1];
            byte sampleRed = source[sampleIndex + 2];
            double sampleLuminance = GetLuminance(sampleRed, sampleGreen, sampleBlue);
            double averageLuminance = GetLuminance(localAverage[sampleIndex + 2], localAverage[sampleIndex + 1], localAverage[sampleIndex]);
            double darkDefect = Math.Max(0, averageLuminance - sampleLuminance);
            double redExcess = sampleRed - Math.Max(sampleGreen, sampleBlue);
            double averageRedExcess = localAverage[sampleIndex + 2] - Math.Max(localAverage[sampleIndex + 1], localAverage[sampleIndex]);
            double redDefect = Math.Max(0, redExcess - averageRedExcess * 0.4);
            double toneWeight = GetSkinToneSimilarity(sampleRed, sampleGreen, sampleBlue, sampleLuminance, skinReference, skinMaskRange);
            double cleanWeight =
                (1 - SmoothStep(8, 34, darkDefect)) *
                (1 - SmoothStep(18, 54, redDefect)) *
                SmoothStep(32, 78, sampleLuminance) *
                (1 - SmoothStep(226, 250, sampleLuminance));
            double weight = Math.Clamp(toneWeight * cleanWeight, 0, 1);
            if (weight <= 0.015)
            {
                continue;
            }

            blueSum += sampleBlue * weight;
            greenSum += sampleGreen * weight;
            redSum += sampleRed * weight;
            weightSum += weight;
        }

        if (weightSum <= 0.001)
        {
            return GetSkinPatchAverage(source, width, height, stride, x, y);
        }

        return (
            (byte)Math.Clamp((int)Math.Round(blueSum / weightSum), 0, 255),
            (byte)Math.Clamp((int)Math.Round(greenSum / weightSum), 0, 255),
            (byte)Math.Clamp((int)Math.Round(redSum / weightSum), 0, 255));
    }

    private static double GetLocalLuminanceGradient(byte[] source, int width, int height, int stride, int x, int y)
    {
        int left = y * stride + Math.Max(0, x - 1) * 4;
        int right = y * stride + Math.Min(width - 1, x + 1) * 4;
        int top = Math.Max(0, y - 1) * stride + x * 4;
        int bottom = Math.Min(height - 1, y + 1) * stride + x * 4;
        double horizontal = Math.Abs(GetLuminance(source[right + 2], source[right + 1], source[right]) - GetLuminance(source[left + 2], source[left + 1], source[left]));
        double vertical = Math.Abs(GetLuminance(source[bottom + 2], source[bottom + 1], source[bottom]) - GetLuminance(source[top + 2], source[top + 1], source[top]));
        return Math.Max(horizontal, vertical);
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

    private static double GetSkinColorWeight(byte red, byte green, byte blue)
    {
        double redMinusBlue = red - blue;
        double greenMinusBlue = green - blue;
        double redMinusGreen = red - green;
        double channelRange = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        double warmWeight = SmoothStep(-6, 22, redMinusBlue) * SmoothStep(-14, 16, greenMinusBlue);
        double balanceWeight = 1 - SmoothStep(64, 118, Math.Abs(redMinusGreen));
        double chromaWeight = Math.Max(0.58, SmoothStep(5, 22, channelRange));
        return Math.Clamp(warmWeight * balanceWeight * chromaWeight, 0, 1);
    }

    private static byte ClampToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(value), 0, 255);
    }
}
