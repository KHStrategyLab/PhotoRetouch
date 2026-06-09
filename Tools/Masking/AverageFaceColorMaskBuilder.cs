using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record AverageFaceColorMaskResult(
    MaskPlane ColorDifferenceMask,
    MediaColor ReferenceColor,
    double AverageSignal);

public static class AverageFaceColorMaskBuilder
{
    public static AverageFaceColorMaskResult Build(BitmapSource source, FaceAnalysisResult analysis, FaceMaskSet? masks = null, CancellationToken cancellationToken = default)
    {
        return Build(source, analysis, masks, 0.45, cancellationToken);
    }

    public static AverageFaceColorMaskResult Build(BitmapSource source, FaceAnalysisResult analysis, FaceMaskSet? masks, double rangeAmount, CancellationToken cancellationToken)
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

        FaceColorReference[] references = CreateDefaultSkinColorReferences();
        if (references.Length == 0)
        {
            return CreateEmptyResult(width, height);
        }

        MaskPlane skinRangeMask = BuildSkinRangeMask(pixels, width, height, stride, references, rangeAmount, cancellationToken);
        skinRangeMask = FeatherColorMask(skinRangeMask, pixels, stride, references, rangeAmount, cancellationToken);
        MaskPlane featureBlockMask = BuildFeatureBlockMask(width, height, analysis, masks);
        skinRangeMask = FillEnclosedMaskHoles(skinRangeMask, featureBlockMask, rangeAmount, cancellationToken);
        skinRangeMask = ApplyFeatureBlockMask(skinRangeMask, featureBlockMask);
        double average = skinRangeMask.Average();
        FaceColorReference displayReference = BlendReferences(references);
        MediaColor color = MediaColor.FromRgb(
            (byte)Math.Clamp((int)Math.Round(displayReference.Red), 0, 255),
            (byte)Math.Clamp((int)Math.Round(displayReference.Green), 0, 255),
            (byte)Math.Clamp((int)Math.Round(displayReference.Blue), 0, 255));

        return new AverageFaceColorMaskResult(skinRangeMask, color, average);
    }

    private static FaceColorReference[] CreateDefaultSkinColorReferences()
    {
        return new[]
        {
            CreateReference(238, 200, 178, 1.0),
            CreateReference(224, 184, 160, 1.0),
            CreateReference(207, 166, 143, 1.0),
            CreateReference(190, 146, 124, 0.9),
            CreateReference(244, 214, 198, 0.8)
        };
    }

    private static FaceColorReference CreateReference(double red, double green, double blue, double weight)
    {
        return new FaceColorReference(red, green, blue, GetLuma(red, green, blue), weight);
    }

    private static MaskPlane BuildSkinRangeMask(byte[] pixels, int width, int height, int stride, FaceColorReference[] references, double rangeAmount, CancellationToken cancellationToken)
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

    private static MaskPlane FeatherColorMask(MaskPlane source, byte[] pixels, int stride, FaceColorReference[] references, double rangeAmount, CancellationToken cancellationToken)
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

    private static MaskPlane FillEnclosedMaskHoles(MaskPlane source, MaskPlane featureBlockMask, double rangeAmount, CancellationToken cancellationToken)
    {
        MaskPlane.EnsureSameSize(source, featureBlockMask);
        int width = source.Width;
        int height = source.Height;
        byte[] horizontalSupport = new byte[source.Values.Length];
        byte[] verticalSupport = new byte[source.Values.Length];
        int maxGap = Math.Clamp((int)Math.Round(5 + Math.Clamp(rangeAmount, 0, 1) * 16), 6, 21);
        const double sourceThreshold = 0.10;
        const double blockThreshold = 0.20;

        for (int y = 0; y < height; y++)
        {
            if ((y & 7) == 0 && cancellationToken.IsCancellationRequested)
            {
                return source;
            }

            int lastSolidX = -1;
            double lastSolidValue = 0;
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                if (featureBlockMask.Values[index] >= blockThreshold)
                {
                    lastSolidX = -1;
                    lastSolidValue = 0;
                    continue;
                }

                double value = source.Values[index];
                if (value > sourceThreshold)
                {
                    if (lastSolidX >= 0)
                    {
                        int gap = x - lastSolidX - 1;
                        if (gap > 0 && gap <= maxGap)
                        {
                            byte strength = ToSupportByte(Math.Min(lastSolidValue, value));
                            for (int fillX = lastSolidX + 1; fillX < x; fillX++)
                            {
                                int fillIndex = y * width + fillX;
                                if (source.Values[fillIndex] <= sourceThreshold &&
                                    featureBlockMask.Values[fillIndex] < blockThreshold)
                                {
                                    horizontalSupport[fillIndex] = strength;
                                }
                            }
                        }
                    }

                    lastSolidX = x;
                    lastSolidValue = value;
                }
            }
        }

        for (int x = 0; x < width; x++)
        {
            if ((x & 7) == 0 && cancellationToken.IsCancellationRequested)
            {
                return source;
            }

            int lastSolidY = -1;
            double lastSolidValue = 0;
            for (int y = 0; y < height; y++)
            {
                int index = y * width + x;
                if (featureBlockMask.Values[index] >= blockThreshold)
                {
                    lastSolidY = -1;
                    lastSolidValue = 0;
                    continue;
                }

                double value = source.Values[index];
                if (value > sourceThreshold)
                {
                    if (lastSolidY >= 0)
                    {
                        int gap = y - lastSolidY - 1;
                        if (gap > 0 && gap <= maxGap)
                        {
                            byte strength = ToSupportByte(Math.Min(lastSolidValue, value));
                            for (int fillY = lastSolidY + 1; fillY < y; fillY++)
                            {
                                int fillIndex = fillY * width + x;
                                if (source.Values[fillIndex] <= sourceThreshold &&
                                    featureBlockMask.Values[fillIndex] < blockThreshold)
                                {
                                    verticalSupport[fillIndex] = strength;
                                }
                            }
                        }
                    }

                    lastSolidY = y;
                    lastSolidValue = value;
                }
            }
        }

        MaskPlane result = source.Clone();
        for (int index = 0; index < result.Values.Length; index++)
        {
            if (horizontalSupport[index] == 0 || verticalSupport[index] == 0)
            {
                continue;
            }

            double block = featureBlockMask.Values[index];
            if (block >= blockThreshold)
            {
                continue;
            }

            double support = Math.Min(horizontalSupport[index], verticalSupport[index]) / 255d;
            double fill = support * (1 - block) * 0.92;
            result.Values[index] = Math.Max(result.Values[index], fill);
        }

        return result;
    }

    private static MaskPlane ApplyFeatureBlockMask(MaskPlane source, MaskPlane featureBlockMask)
    {
        MaskPlane.EnsureSameSize(source, featureBlockMask);
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int index = 0; index < source.Values.Length; index++)
        {
            result.Values[index] = Math.Clamp(source.Values[index] * (1 - featureBlockMask.Values[index]), 0, 1);
        }

        return result;
    }

    private static MaskPlane BuildFeatureBlockMask(int width, int height, FaceAnalysisResult analysis, FaceMaskSet? masks)
    {
        MaskPlane block = MaskPlane.Empty(width, height);
        if (masks is not null && IsCompatible(masks.SkinMask, width, height))
        {
            AddMask(block, masks.HardProtectMask);
            AddMask(block, masks.EyeMask);
            AddMask(block, masks.EyebrowMask);
            AddMask(block, masks.LipMask);
            AddMask(block, masks.InnerMouthMask);
            AddMask(block, masks.TeethMask);
            AddMask(block, masks.NostrilMask);
            AddMask(block, masks.NoseMask);
            AddMask(block, masks.NoseShadowMask);
            AddMask(block, masks.GlassesMask);
        }

        PaintEstimatedFeaturePaths(block, analysis);
        return block;
    }

    private static void PaintEstimatedFeaturePaths(MaskPlane block, FaceAnalysisResult analysis)
    {
        Int32Rect face = ClampRect(analysis.FaceBox, block.Width, block.Height);
        double faceWidth = Math.Max(1, face.Width);
        double faceHeight = Math.Max(1, face.Height);
        bool hasLeftEye = TryGetPoint(analysis, "left_eye", out WpfPoint leftEye);
        bool hasRightEye = TryGetPoint(analysis, "right_eye", out WpfPoint rightEye);
        bool hasNose = TryGetPoint(analysis, "nose_tip", out WpfPoint noseTip);
        bool hasMouth = TryGetPoint(analysis, "mouth_center", out WpfPoint mouthCenter);

        if (hasLeftEye)
        {
            PaintSoftEllipse(block, leftEye.X, leftEye.Y, faceWidth * 0.105, faceHeight * 0.052, 1);
            PaintSoftEllipse(block, leftEye.X, leftEye.Y - faceHeight * 0.075, faceWidth * 0.12, faceHeight * 0.035, 0.86);
        }

        if (hasRightEye)
        {
            PaintSoftEllipse(block, rightEye.X, rightEye.Y, faceWidth * 0.105, faceHeight * 0.052, 1);
            PaintSoftEllipse(block, rightEye.X, rightEye.Y - faceHeight * 0.075, faceWidth * 0.12, faceHeight * 0.035, 0.86);
        }

        if (hasLeftEye && hasRightEye)
        {
            double eyeDistance = Math.Max(1, Math.Abs(rightEye.X - leftEye.X));
            PaintSoftLine(block, leftEye, rightEye, Math.Max(3, eyeDistance * 0.035), 0.72);
            PaintSoftLine(
                block,
                new WpfPoint(leftEye.X - eyeDistance * 0.22, leftEye.Y),
                new WpfPoint(rightEye.X + eyeDistance * 0.22, rightEye.Y),
                Math.Max(3, eyeDistance * 0.024),
                0.58);
        }

        if (hasNose)
        {
            double bridgeY = hasLeftEye && hasRightEye
                ? (leftEye.Y + rightEye.Y) * 0.5 + faceHeight * 0.035
                : noseTip.Y - faceHeight * 0.17;
            PaintSoftLine(block, new WpfPoint(noseTip.X, bridgeY), noseTip, faceWidth * 0.035, 0.92);
            PaintSoftEllipse(block, noseTip.X, noseTip.Y + faceHeight * 0.035, faceWidth * 0.095, faceHeight * 0.055, 0.88);
        }

        if (hasMouth)
        {
            PaintSoftEllipse(block, mouthCenter.X, mouthCenter.Y, faceWidth * 0.175, faceHeight * 0.052, 1);
        }
    }

    private static void PaintSoftEllipse(MaskPlane mask, double centerX, double centerY, double radiusX, double radiusY, double opacity)
    {
        if (radiusX <= 0 || radiusY <= 0)
        {
            return;
        }

        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY));
        for (int y = top; y <= bottom; y++)
        {
            double dy = (y - centerY) / radiusY;
            for (int x = left; x <= right; x++)
            {
                double dx = (x - centerX) / radiusX;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                if (distance > 1)
                {
                    continue;
                }

                double amount = opacity * (1 - SmoothStep(0.62, 1, distance));
                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static void PaintSoftLine(MaskPlane mask, WpfPoint start, WpfPoint end, double radius, double opacity)
    {
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.0001 || radius <= 0)
        {
            PaintSoftEllipse(mask, start.X, start.Y, radius, radius, opacity);
            return;
        }

        int left = Math.Max(0, (int)Math.Floor(Math.Min(start.X, end.X) - radius));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(Math.Max(start.X, end.X) + radius));
        int top = Math.Max(0, (int)Math.Floor(Math.Min(start.Y, end.Y) - radius));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(Math.Max(start.Y, end.Y) + radius));
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double t = ((x - start.X) * dx + (y - start.Y) * dy) / lengthSquared;
                t = Math.Clamp(t, 0, 1);
                double closestX = start.X + dx * t;
                double closestY = start.Y + dy * t;
                double distance = Math.Sqrt((x - closestX) * (x - closestX) + (y - closestY) * (y - closestY));
                if (distance > radius)
                {
                    continue;
                }

                double amount = opacity * (1 - SmoothStep(0.55, 1, distance / radius));
                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static void AddMask(MaskPlane target, MaskPlane source)
    {
        if (!IsCompatible(source, target.Width, target.Height))
        {
            return;
        }

        for (int index = 0; index < target.Values.Length; index++)
        {
            target.Values[index] = Math.Max(target.Values[index], source.Values[index]);
        }
    }

    private static bool IsCompatible(MaskPlane mask, int width, int height)
    {
        return mask.Width == width && mask.Height == height;
    }

    private static bool TryGetPoint(FaceAnalysisResult analysis, string key, out WpfPoint point)
    {
        return analysis.FaceLandmarks.TryGetValue(key, out point);
    }

    private static byte ToSupportByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(Math.Clamp(value, 0, 1) * 255), 0, 255);
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

    private static Int32Rect ClampRect(Int32Rect rect, int width, int height)
    {
        int x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        int y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        int right = Math.Clamp(rect.X + rect.Width, x + 1, width);
        int bottom = Math.Clamp(rect.Y + rect.Height, y + 1, height);
        return new Int32Rect(x, y, right - x, bottom - y);
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
