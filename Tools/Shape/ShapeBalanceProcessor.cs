using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class ShapeBalanceProcessor
{
    private readonly FaceSymmetryAnalyzer _analyzer = new();
    private readonly ShapeBalanceMapBuilder _mapBuilder = new();
    private readonly BalancedMaskQualityValidator _qualityValidator = new();

    public BalancedImageBundle Process(BitmapSource originalImage, FaceSnapshotMaskSet sourceSnapshot, ShapeBalanceOptions options)
    {
        ArgumentNullException.ThrowIfNull(originalImage);
        ArgumentNullException.ThrowIfNull(sourceSnapshot);
        ArgumentNullException.ThrowIfNull(options);

        BitmapSource source = originalImage.Format == PixelFormats.Bgra32
            ? originalImage
            : new FormatConvertedBitmap(originalImage, PixelFormats.Bgra32, null, 0);
        source.Freeze();

        ShapeBalanceAnalysisReport analysisReport = _analyzer.Analyze(sourceSnapshot);
        ShapeBalanceMap map = _mapBuilder.Build(sourceSnapshot, analysisReport, options, source.PixelWidth, source.PixelHeight);
        if (!options.EnableShapeBalance || map.IsIdentity)
        {
            FaceSnapshotMaskSet unchangedSnapshot = CreateBalancedSnapshot(sourceSnapshot, sourceSnapshot.Masks, sourceSnapshot.Analysis, analysisReport, "identity");
            BalancedMaskQualityReport unchangedQuality = _qualityValidator.Validate(unchangedSnapshot, map);
            ShapeBalanceReport unchangedReport = new(
                analysisReport,
                false,
                0,
                unchangedQuality.BalancedMaskQualityScore,
                unchangedQuality.MaxAllowedShapeStage,
                unchangedQuality.DebugWarnings);
            return new BalancedImageBundle(
                source,
                source,
                sourceSnapshot,
                unchangedSnapshot,
                sourceSnapshot.Analysis.FaceLandmarks,
                sourceSnapshot.Analysis.FaceBox,
                map,
                unchangedReport,
                unchangedQuality);
        }

        MaskPlane faceOnlyWarpMask = SkinToneMaskBuilder.Build(sourceSnapshot.Masks).FaceOnlyWarpMask;
        BitmapSource balancedImage = WarpImage(source, map, faceOnlyWarpMask);
        FaceMaskSet balancedMasks = WarpMasks(sourceSnapshot.Masks, map, faceOnlyWarpMask);
        FaceMaskSet? balancedWarpedStandardMasks = sourceSnapshot.WarpedStandardMasks is null
            ? null
            : WarpMasks(sourceSnapshot.WarpedStandardMasks, map, SkinToneMaskBuilder.Build(sourceSnapshot.WarpedStandardMasks).FaceOnlyWarpMask);
        IReadOnlyDictionary<string, WpfPoint> balancedLandmarks = WarpLandmarks(sourceSnapshot.Analysis.FaceLandmarks, map);
        Int32Rect balancedFaceBox = WarpFaceBox(sourceSnapshot.Analysis.FaceBox, map);
        FaceAnalysisResult balancedAnalysis = sourceSnapshot.Analysis with
        {
            FaceBox = balancedFaceBox,
            FaceLandmarks = balancedLandmarks,
            FaceAngle = sourceSnapshot.Analysis.FaceAngle + map.RotationRadians * 180d / Math.PI,
            DebugWarnings = sourceSnapshot.Analysis.DebugWarnings.Concat(new[] { "shape_balance_applied" }).ToArray()
        };
        FaceSnapshotMaskSet balancedSnapshot = CreateBalancedSnapshot(sourceSnapshot, balancedMasks, balancedAnalysis, analysisReport, CreateMapStableId(map));
        balancedSnapshot = balancedSnapshot with { WarpedStandardMasks = balancedWarpedStandardMasks };
        BalancedMaskQualityReport qualityReport = _qualityValidator.Validate(balancedSnapshot, map);
        ShapeBalanceAnalysisReport finalAnalysisReport = analysisReport with
        {
            NostrilBalanceObservation = UpdateNostrilObservation(
                analysisReport.NostrilBalanceObservation,
                sourceSnapshot.Masks.NostrilMask,
                balancedMasks.NostrilMask)
        };
        ShapeBalanceReport shapeReport = new(
            finalAnalysisReport,
            true,
            options.GlobalShapeBalanceAmount,
            qualityReport.BalancedMaskQualityScore,
            qualityReport.MaxAllowedShapeStage,
            finalAnalysisReport.DebugWarnings.Concat(qualityReport.DebugWarnings).ToArray());

        return new BalancedImageBundle(
            source,
            balancedImage,
            sourceSnapshot,
            balancedSnapshot,
            balancedLandmarks,
            balancedFaceBox,
            map,
            shapeReport,
            qualityReport);
    }

    private static FaceSnapshotMaskSet CreateBalancedSnapshot(
        FaceSnapshotMaskSet sourceSnapshot,
        FaceMaskSet masks,
        FaceAnalysisResult analysis,
        ShapeBalanceAnalysisReport analysisReport,
        string mapStableId)
    {
        MaskQualityReport qualityReport = MaskQualityReport.FromMasks(analysis, masks);
        SnapshotMaskCacheKey cacheKey = sourceSnapshot.CacheKey with
        {
            FaceBox = analysis.FaceBox,
            FaceAngle = analysis.FaceAngle,
            CropVersion = sourceSnapshot.CacheKey.CropVersion + "|shape:" + mapStableId,
            MaskVersion = sourceSnapshot.CacheKey.MaskVersion + "+shape_balance_v1"
        };
        return sourceSnapshot with
        {
            CacheKey = cacheKey,
            Analysis = analysis,
            Masks = masks,
            QualityReport = qualityReport,
            CreatedAtUtc = DateTime.UtcNow,
            WarpedStandardMasks = null,
            NostrilDetection = sourceSnapshot.NostrilDetection
        };
    }

    private static string CreateMapStableId(ShapeBalanceMap map)
    {
        return string.Join(
            ",",
            map.RotationRadians.ToString("0.00000", System.Globalization.CultureInfo.InvariantCulture),
            map.EyeLevelDelta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            map.NoseCenterDelta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            map.ChinCenterDelta.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            map.MaxDisplacementPixels.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            map.SymmetryAnalysisReport.SuggestedSymmetryAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture),
            map.SymmetryBalanceMap.SymmetryWarpRegions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static BitmapSource WarpImage(BitmapSource source, ShapeBalanceMap map, MaskPlane faceOnlyWarpMask)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        source.CopyPixels(sourcePixels, stride, 0);
        byte[] outputPixels = new byte[sourcePixels.Length];
        MaskPlane balancedFaceOnlyWarpMask = WarpMask(faceOnlyWarpMask, map);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int outputIndex = y * stride + x * 4;
                double faceWeight = Math.Clamp(balancedFaceOnlyWarpMask[x, y], 0, 1);
                if (faceWeight <= 0.001)
                {
                    Array.Copy(sourcePixels, outputIndex, outputPixels, outputIndex, 4);
                    continue;
                }

                WpfPoint sourcePoint = map.MapBalancedToSource(x + 0.5, y + 0.5);
                if (faceWeight >= 0.999)
                {
                    SampleBgra(sourcePixels, width, height, stride, sourcePoint.X, sourcePoint.Y, outputPixels, outputIndex);
                }
                else
                {
                    SampleBgraBlended(sourcePixels, width, height, stride, sourcePoint.X, sourcePoint.Y, outputPixels, outputIndex, faceWeight);
                }
            }
        }

        BitmapSource output = BitmapSource.Create(width, height, source.DpiX, source.DpiY, PixelFormats.Bgra32, null, outputPixels, stride);
        output.Freeze();
        return output;
    }

    private static FaceMaskSet WarpMasks(FaceMaskSet masks, ShapeBalanceMap map, MaskPlane faceOnlyWarpMask)
    {
        MaskPlane balancedFaceOnlyWarpMask = WarpMask(faceOnlyWarpMask, map);
        MaskPlane skin = MaskPlane.Intersect(WarpMask(masks.SkinMask, map), balancedFaceOnlyWarpMask);
        MaskPlane eye = NormalizeHardMask(WarpMask(masks.EyeMask, map), 0.38);
        MaskPlane eyebrow = NormalizeHardMask(WarpMask(masks.EyebrowMask, map), 0.35);
        MaskPlane lip = NormalizeHardMask(WarpMask(masks.LipMask, map), 0.36);
        MaskPlane innerMouth = NormalizeHardMask(WarpMask(masks.InnerMouthMask, map), 0.35);
        MaskPlane teeth = NormalizeHardMask(WarpMask(masks.TeethMask, map), 0.35);
        MaskPlane nose = MaskPlane.Intersect(WarpMask(masks.NoseMask, map), balancedFaceOnlyWarpMask);
        MaskPlane nostril = NormalizeHardMask(WarpMask(masks.NostrilMask, map), 0.30);
        MaskPlane noseSkin = MaskPlane.Intersect(MaskPlane.Subtract(WarpMask(masks.NoseSkinMask, map), nostril), balancedFaceOnlyWarpMask);
        MaskPlane noseShadow = NormalizeHardMask(WarpMask(masks.NoseShadowMask, map), 0.36);
        MaskPlane hair = NormalizeHardMask(masks.HairMask.Clone(), 0.34);
        MaskPlane beard = NormalizeHardMask(WarpMask(masks.BeardMask, map), 0.34);
        MaskPlane mustache = NormalizeHardMask(WarpMask(masks.MustacheMask, map), 0.34);
        MaskPlane glasses = NormalizeHardMask(WarpMask(masks.GlassesMask, map), 0.32);
        MaskPlane facialHardProtect = NormalizeHardMask(WarpMask(MaskPlane.Subtract(masks.HardProtectMask, masks.HairMask), map), 0.28);
        MaskPlane hardProtect = NormalizeHardMask(
            MaskPlane.Union(
                facialHardProtect,
                eye,
                eyebrow,
                lip,
                innerMouth,
                teeth,
                nostril,
                noseShadow,
                hair,
                beard,
                mustache,
                glasses),
            0.28);
        MaskPlane softProtect = MaskPlane.Intersect(MaskPlane.Subtract(WarpMask(masks.SoftProtectMask, map), hardProtect), balancedFaceOnlyWarpMask);
        MaskPlane retouchAllow = MaskPlane.Intersect(MaskPlane.Subtract(
            MaskPlane.Union(WarpMask(masks.RetouchAllowMask, map), skin, noseSkin),
            hardProtect), balancedFaceOnlyWarpMask);
        MaskPlane finalOverlay = MaskPlane.Subtract(
            MaskPlane.Union(retouchAllow, MaskPlane.Multiply(softProtect, 0.45)),
            hardProtect);

        FaceMaskSet warpedMasks = new(
            skin,
            eye,
            eyebrow,
            lip,
            innerMouth,
            teeth,
            nose,
            noseSkin,
            nostril,
            noseShadow,
            hair,
            beard,
            mustache,
            glasses,
            hardProtect,
            softProtect,
            retouchAllow,
            finalOverlay);

        return SkinToneMaskBuilder.ApplyToFaceMaskSet(warpedMasks);
    }

    private static MaskPlane WarpMask(MaskPlane source, ShapeBalanceMap map)
    {
        MaskPlane output = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                WpfPoint sourcePoint = map.MapBalancedToSource(x + 0.5, y + 0.5);
                output[x, y] = SampleMask(source, sourcePoint.X, sourcePoint.Y);
            }
        }

        return output;
    }

    private static MaskPlane NormalizeHardMask(MaskPlane source, double threshold)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int index = 0; index < source.Values.Length; index++)
        {
            double value = source.Values[index];
            result.Values[index] = value >= threshold
                ? Math.Clamp(0.76 + value * 0.24, 0, 1)
                : Math.Clamp(value * 0.38, 0, 1);
        }

        return result;
    }

    private static ShapeBalanceAnalysisReport UpdateNostrilObservation(
        ShapeBalanceAnalysisReport report,
        MaskPlane originalNostril,
        MaskPlane balancedNostril)
    {
        return report with
        {
            NostrilBalanceObservation = UpdateNostrilObservation(report.NostrilBalanceObservation, originalNostril, balancedNostril)
        };
    }

    private static NostrilBalanceObservation UpdateNostrilObservation(
        NostrilBalanceObservation observation,
        MaskPlane originalNostril,
        MaskPlane balancedNostril)
    {
        WpfPoint originalCenter = CalculateMaskCenter(originalNostril);
        WpfPoint balancedCenter = CalculateMaskCenter(balancedNostril);
        double shift = double.IsNaN(originalCenter.X) || double.IsNaN(balancedCenter.X)
            ? 0
            : Math.Sqrt(Math.Pow(balancedCenter.X - originalCenter.X, 2) + Math.Pow(balancedCenter.Y - originalCenter.Y, 2));
        double safeLimit = Math.Max(2, Math.Min(balancedNostril.Width, balancedNostril.Height) * 0.018);
        return observation with
        {
            BeforeAfterNostrilShift = shift,
            IsNostrilWarpSafe = shift <= safeLimit || !observation.IsNostrilBalanceReliable
        };
    }

    private static WpfPoint CalculateMaskCenter(MaskPlane mask)
    {
        double sum = 0;
        double xSum = 0;
        double ySum = 0;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                double value = mask[x, y];
                sum += value;
                xSum += x * value;
                ySum += y * value;
            }
        }

        return sum <= 0.00001
            ? new WpfPoint(double.NaN, double.NaN)
            : new WpfPoint(xSum / sum, ySum / sum);
    }

    private static IReadOnlyDictionary<string, WpfPoint> WarpLandmarks(IReadOnlyDictionary<string, WpfPoint> landmarks, ShapeBalanceMap map)
    {
        Dictionary<string, WpfPoint> result = new();
        foreach (KeyValuePair<string, WpfPoint> landmark in landmarks)
        {
            result[landmark.Key] = map.MapSourceToBalanced(landmark.Value);
        }

        return result;
    }

    private static Int32Rect WarpFaceBox(Int32Rect faceBox, ShapeBalanceMap map)
    {
        WpfPoint[] corners =
        {
            map.MapSourceToBalanced(new WpfPoint(faceBox.X, faceBox.Y)),
            map.MapSourceToBalanced(new WpfPoint(faceBox.X + faceBox.Width, faceBox.Y)),
            map.MapSourceToBalanced(new WpfPoint(faceBox.X, faceBox.Y + faceBox.Height)),
            map.MapSourceToBalanced(new WpfPoint(faceBox.X + faceBox.Width, faceBox.Y + faceBox.Height))
        };
        int left = Math.Clamp((int)Math.Floor(corners.Min(point => point.X)), 0, Math.Max(0, map.Width - 1));
        int top = Math.Clamp((int)Math.Floor(corners.Min(point => point.Y)), 0, Math.Max(0, map.Height - 1));
        int right = Math.Clamp((int)Math.Ceiling(corners.Max(point => point.X)), left + 1, map.Width);
        int bottom = Math.Clamp((int)Math.Ceiling(corners.Max(point => point.Y)), top + 1, map.Height);
        return new Int32Rect(left, top, right - left, bottom - top);
    }

    private static void SampleBgra(byte[] pixels, int width, int height, int stride, double x, double y, byte[] output, int outputIndex)
    {
        int x0 = Math.Clamp((int)Math.Floor(x), 0, width - 1);
        int y0 = Math.Clamp((int)Math.Floor(y), 0, height - 1);
        int x1 = Math.Clamp(x0 + 1, 0, width - 1);
        int y1 = Math.Clamp(y0 + 1, 0, height - 1);
        double tx = Math.Clamp(x - x0, 0, 1);
        double ty = Math.Clamp(y - y0, 0, 1);
        int i00 = y0 * stride + x0 * 4;
        int i10 = y0 * stride + x1 * 4;
        int i01 = y1 * stride + x0 * 4;
        int i11 = y1 * stride + x1 * 4;

        for (int channel = 0; channel < 4; channel++)
        {
            double top = pixels[i00 + channel] * (1 - tx) + pixels[i10 + channel] * tx;
            double bottom = pixels[i01 + channel] * (1 - tx) + pixels[i11 + channel] * tx;
            output[outputIndex + channel] = (byte)Math.Clamp((int)Math.Round(top * (1 - ty) + bottom * ty), 0, 255);
        }
    }

    private static void SampleBgraBlended(byte[] pixels, int width, int height, int stride, double x, double y, byte[] output, int outputIndex, double amount)
    {
        Span<byte> warped = stackalloc byte[4];
        byte[] warpedArray = new byte[4];
        SampleBgra(pixels, width, height, stride, x, y, warpedArray, 0);
        for (int channel = 0; channel < 4; channel++)
        {
            warped[channel] = warpedArray[channel];
            double original = pixels[outputIndex + channel];
            double blended = original * (1 - amount) + warped[channel] * amount;
            output[outputIndex + channel] = (byte)Math.Clamp((int)Math.Round(blended), 0, 255);
        }
    }

    private static double SampleMask(MaskPlane mask, double x, double y)
    {
        int x0 = Math.Clamp((int)Math.Floor(x), 0, mask.Width - 1);
        int y0 = Math.Clamp((int)Math.Floor(y), 0, mask.Height - 1);
        int x1 = Math.Clamp(x0 + 1, 0, mask.Width - 1);
        int y1 = Math.Clamp(y0 + 1, 0, mask.Height - 1);
        double tx = Math.Clamp(x - x0, 0, 1);
        double ty = Math.Clamp(y - y0, 0, 1);
        double top = mask[x0, y0] * (1 - tx) + mask[x1, y0] * tx;
        double bottom = mask[x0, y1] * (1 - tx) + mask[x1, y1] * tx;
        return top * (1 - ty) + bottom * ty;
    }
}
