using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;

namespace PhotoRetouch;

public sealed record AverageFaceColorMaskResult(
    MaskPlane ColorDifferenceMask,
    MediaColor ReferenceColor,
    double AverageSignal);

public static class AverageFaceColorMaskBuilder
{
    public static AverageFaceColorMaskResult Build(BitmapSource source, FaceAnalysisResult analysis, FaceMaskSet? masks = null, CancellationToken cancellationToken = default)
    {
        return Build(source, analysis, masks, 0.45, cancellationToken, null);
    }

    public static AverageFaceColorMaskResult Build(BitmapSource source, FaceAnalysisResult analysis, FaceMaskSet? masks, double rangeAmount, CancellationToken cancellationToken)
    {
        return Build(source, analysis, masks, rangeAmount, cancellationToken, null);
    }

    public static AverageFaceColorMaskResult Build(
        BitmapSource source,
        FaceAnalysisResult analysis,
        FaceMaskSet? masks,
        double rangeAmount,
        CancellationToken cancellationToken,
        IReadOnlyList<MediaColor>? manualReferenceColors)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(analysis);
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateEmptyResult(source.PixelWidth, source.PixelHeight);
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        if (cancellationToken.IsCancellationRequested)
        {
            return CreateEmptyResult(width, height);
        }

        FaceColorReference[] references = CreateSkinColorReferences(manualReferenceColors);
        if (references.Length == 0)
        {
            return CreateEmptyResult(width, height);
        }

        MaskPlane skinRangeMask = BuildSkinRangeMask(pixels, width, height, stride, analysis, references, rangeAmount, cancellationToken);
        skinRangeMask = FeatherColorMask(skinRangeMask, pixels, stride, analysis, references, rangeAmount, cancellationToken);
        if (masks is not null)
        {
            MaskPlane colorProtectMask = BuildColorMaskProtectionMask(masks, width, height);
            skinRangeMask = MaskPlane.Subtract(skinRangeMask, colorProtectMask);
        }

        double average = skinRangeMask.Average();
        FaceColorReference displayReference = BlendReferences(references);
        MediaColor color = MediaColor.FromRgb(
            (byte)Math.Clamp((int)Math.Round(displayReference.Red), 0, 255),
            (byte)Math.Clamp((int)Math.Round(displayReference.Green), 0, 255),
            (byte)Math.Clamp((int)Math.Round(displayReference.Blue), 0, 255));

        return new AverageFaceColorMaskResult(skinRangeMask, color, average);
    }

    private static FaceColorReference[] CreateSkinColorReferences(IReadOnlyList<MediaColor>? manualReferenceColors)
    {
        if (manualReferenceColors is { Count: > 0 })
        {
            return manualReferenceColors
                .Take(5)
                .Select(color => CreateReference(color.R, color.G, color.B, 1.0))
                .ToArray();
        }

        return CreateDefaultSkinColorReferences();
    }

    private static MaskPlane BuildColorMaskProtectionMask(FaceMaskSet masks, int width, int height)
    {
        MaskPlane protect = MaskPlane.Empty(width, height);
        AddIfSameSize(protect, masks.EyeMask);
        AddIfSameSize(protect, masks.LipMask);
        AddIfSameSize(protect, masks.InnerMouthMask);
        AddIfSameSize(protect, masks.TeethMask);
        AddIfSameSize(protect, masks.NostrilMask);
        AddIfSameSize(protect, masks.HairMask);
        AddIfSameSize(protect, masks.BeardMask);
        AddIfSameSize(protect, masks.MustacheMask);
        AddIfSameSize(protect, masks.GlassesMask);
        return protect;
    }

    private static void AddIfSameSize(MaskPlane target, MaskPlane candidate)
    {
        if (candidate.Width != target.Width || candidate.Height != target.Height)
        {
            return;
        }

        for (int index = 0; index < target.Values.Length; index++)
        {
            double value = candidate.Values[index];
            if (value > target.Values[index])
            {
                target.Values[index] = value;
            }
        }
    }

    private static FaceColorReference[] CreateDefaultSkinColorReferences()
    {
        return new[]
        {
            CreateReference(241, 206, 204, 1.0),
            CreateReference(168, 121, 99, 1.0),
            CreateReference(168, 121, 99, 1.0),
            CreateReference(213, 170, 151, 1.0),
            CreateReference(213, 170, 151, 1.0)
        };
    }

    private static FaceColorReference CreateReference(double red, double green, double blue, double weight)
    {
        return new FaceColorReference(red, green, blue, GetLuma(red, green, blue), weight);
    }

    private static MaskPlane BuildSkinRangeMask(
        byte[] pixels,
        int width,
        int height,
        int stride,
        FaceAnalysisResult analysis,
        FaceColorReference[] references,
        double rangeAmount,
        CancellationToken cancellationToken)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        double rangeScale = 0.75 + Math.Clamp(rangeAmount, 0, 1) * 1.55;

        for (int y = 0; y < height; y++)
        {
            if ((y & 7) == 0)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return mask;
                }
            }

            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                if (pixels[index + 3] < 16)
                {
                    continue;
                }

                double blue = pixels[index];
                double green = pixels[index + 1];
                double red = pixels[index + 2];
                double luma = GetLuma(red, green, blue);
                if (!IsCandidateSelectedSkinColor(red, green, blue, luma) ||
                    IsWhiteBackground(red, green, blue, luma, references))
                {
                    continue;
                }

                double bestRangeRatio = double.MaxValue;
                foreach (FaceColorReference reference in references)
                {
                    double rangeRatio = GetRangeRatio(red, green, blue, luma, reference, rangeScale);
                    if (rangeRatio < bestRangeRatio)
                    {
                        bestRangeRatio = rangeRatio;
                    }
                }

                double rangeWeight = 1 - SmoothStep(0.48, 2.65, bestRangeRatio);
                if (rangeWeight <= 0.0001)
                {
                    continue;
                }

                mask[x, y] = Math.Clamp(rangeWeight, 0, 1);
            }
        }

        return mask;
    }

    private static MaskPlane FeatherColorMask(
        MaskPlane source,
        byte[] pixels,
        int stride,
        FaceAnalysisResult analysis,
        FaceColorReference[] references,
        double rangeAmount,
        CancellationToken cancellationToken)
    {
        int radius = Math.Clamp((int)Math.Round(2 + Math.Clamp(rangeAmount, 0, 1) * 4), 2, 6);
        MaskPlane horizontal = MaskPlane.Empty(source.Width, source.Height);
        MaskPlane feathered = MaskPlane.Empty(source.Width, source.Height);

        for (int y = 0; y < source.Height; y++)
        {
            if ((y & 7) == 0 && cancellationToken.IsCancellationRequested)
            {
                return source;
            }

            for (int x = 0; x < source.Width; x++)
            {
                double sum = 0;
                double weight = 0;
                int left = Math.Max(0, x - radius);
                int right = Math.Min(source.Width - 1, x + radius);
                for (int sampleX = left; sampleX <= right; sampleX++)
                {
                    double distance = Math.Abs(sampleX - x);
                    double sampleWeight = 1 - distance / (radius + 1d);
                    sum += source[sampleX, y] * sampleWeight;
                    weight += sampleWeight;
                }

                horizontal[x, y] = weight > 0 ? sum / weight : source[x, y];
            }
        }

        for (int y = 0; y < source.Height; y++)
        {
            if ((y & 7) == 0 && cancellationToken.IsCancellationRequested)
            {
                return source;
            }

            for (int x = 0; x < source.Width; x++)
            {
                int pixelIndex = y * stride + x * 4;
                double blue = pixels[pixelIndex];
                double green = pixels[pixelIndex + 1];
                double red = pixels[pixelIndex + 2];
                double luma = GetLuma(red, green, blue);
                if (pixels[pixelIndex + 3] < 16 ||
                    !IsCandidateSelectedSkinColor(red, green, blue, luma) ||
                    IsWhiteBackground(red, green, blue, luma, references))
                {
                    continue;
                }

                double sum = 0;
                double weight = 0;
                int top = Math.Max(0, y - radius);
                int bottom = Math.Min(source.Height - 1, y + radius);
                for (int sampleY = top; sampleY <= bottom; sampleY++)
                {
                    double distance = Math.Abs(sampleY - y);
                    double sampleWeight = 1 - distance / (radius + 1d);
                    sum += horizontal[x, sampleY] * sampleWeight;
                    weight += sampleWeight;
                }

                double original = source[x, y];
                if (original <= 0.0001)
                {
                    continue;
                }

                double blurred = weight > 0 ? sum / weight : original;
                feathered[x, y] = Math.Clamp(Math.Min(original, blurred * 1.08), 0, 1);
            }
        }

        return feathered;
    }

    private static double GetRangeRatio(double red, double green, double blue, double luma, FaceColorReference reference, double rangeScale)
    {
        double darkerRedTolerance = Math.Max(14, reference.Red * 0.08) * rangeScale;
        double darkerGreenTolerance = Math.Max(14, reference.Green * 0.08) * rangeScale;
        double darkerBlueTolerance = Math.Max(14, reference.Blue * 0.09) * rangeScale;
        double brighterRedTolerance = Math.Max(18, reference.Red * 0.11) * rangeScale;
        double brighterGreenTolerance = Math.Max(20, reference.Green * 0.12) * rangeScale;
        double brighterBlueTolerance = Math.Max(20, reference.Blue * 0.14) * rangeScale;
        double darkerLumaTolerance = Math.Max(18, reference.Luma * 0.10) * rangeScale;
        double brighterLumaTolerance = Math.Max(22, reference.Luma * 0.13) * rangeScale;
        double lumaTolerance = luma >= reference.Luma
            ? brighterLumaTolerance
            : darkerLumaTolerance;
        double redTolerance = red >= reference.Red
            ? brighterRedTolerance
            : darkerRedTolerance;
        double greenTolerance = green >= reference.Green
            ? brighterGreenTolerance
            : darkerGreenTolerance;
        double blueTolerance = blue >= reference.Blue
            ? brighterBlueTolerance
            : darkerBlueTolerance;

        return Math.Max(
            Math.Max(
                Math.Abs(red - reference.Red) / redTolerance,
                Math.Abs(green - reference.Green) / greenTolerance),
            Math.Max(
                Math.Abs(blue - reference.Blue) / blueTolerance,
                Math.Abs(luma - reference.Luma) / lumaTolerance));
    }

    private static bool IsCandidateSelectedSkinColor(double red, double green, double blue, double luma)
    {
        if (luma < 42 || luma > 244)
        {
            return false;
        }

        double chroma = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        double redBlue = red - blue;
        double redGreen = red - green;
        double greenBlue = green - blue;
        if (chroma < 12 || redBlue < 22)
        {
            return false;
        }

        return red >= green + 2 &&
            green >= blue - 3 &&
            redGreen >= 2 &&
            redGreen <= 76 &&
            greenBlue >= -4 &&
            greenBlue <= 72;
    }

    private static bool IsWhiteBackground(double red, double green, double blue, double luma, FaceColorReference[] references)
    {
        if (!IsNearWhite(red, green, blue, luma))
        {
            return false;
        }

        double nearestColorDistance = references.Min(reference =>
        {
            double redDistance = Math.Abs(red - reference.Red);
            double greenDistance = Math.Abs(green - reference.Green);
            double blueDistance = Math.Abs(blue - reference.Blue);
            return Math.Sqrt(redDistance * redDistance + greenDistance * greenDistance + blueDistance * blueDistance);
        });
        return nearestColorDistance >= 34;
    }

    private static bool IsNearWhite(double red, double green, double blue, double luma)
    {
        double chroma = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        return luma >= 238 && chroma <= 18;
    }

    private static FaceColorReference BlendReferences(FaceColorReference[] references)
    {
        double weightSum = references.Sum(reference => reference.Weight);
        if (weightSum <= 0.001)
        {
            return references[0];
        }

        double red = references.Sum(reference => reference.Red * reference.Weight) / weightSum;
        double green = references.Sum(reference => reference.Green * reference.Weight) / weightSum;
        double blue = references.Sum(reference => reference.Blue * reference.Weight) / weightSum;
        return new FaceColorReference(red, green, blue, GetLuma(red, green, blue), 1);
    }

    private static double GetLuma(double red, double green, double blue)
    {
        return red * 0.299 + green * 0.587 + blue * 0.114;
    }

    private static AverageFaceColorMaskResult CreateEmptyResult(int width, int height)
    {
        MaskPlane empty = MaskPlane.Empty(Math.Max(1, width), Math.Max(1, height));
        return new AverageFaceColorMaskResult(empty, Colors.Transparent, 0);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private sealed record FaceColorReference(double Red, double Green, double Blue, double Luma, double Weight);

}
