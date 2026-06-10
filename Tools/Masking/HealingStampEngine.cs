namespace PhotoRetouch;

public static class HealingStampEngine
{
    public static HealingStampResult Apply(HealingStampInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        MaskPlane.EnsureSameSize(input.DefectMask, input.RetouchAllowMask);
        MaskPlane.EnsureSameSize(input.DefectMask, input.SoftProtectMask);
        MaskPlane.EnsureSameSize(input.DefectMask, input.HardProtectMask);

        int width = input.Width;
        int height = input.Height;
        int stride = width * 4;
        int boxWidth = Math.Max(1, input.MaxX - input.MinX + 1);
        int boxHeight = Math.Max(1, input.MaxY - input.MinY + 1);
        int stampGrow = Math.Clamp((int)Math.Round(Math.Max(boxWidth, boxHeight) * 0.32) + input.FeatherRadius, 2, 18);
        int edgeBlurRadius = Math.Clamp(input.FeatherRadius + (int)Math.Round(Math.Max(boxWidth, boxHeight) * 0.22), 3, 24);
        int stampPadding = Math.Clamp((int)Math.Round(Math.Max(boxWidth, boxHeight) * 0.72) + edgeBlurRadius * 2, 7, 56);
        int left = Math.Max(0, input.MinX - stampPadding);
        int top = Math.Max(0, input.MinY - stampPadding);
        int right = Math.Min(width - 1, input.MaxX + stampPadding);
        int bottom = Math.Min(height - 1, input.MaxY + stampPadding);

        List<HealingSourceSample> samples = CollectSourceSamples(input, left, top, right, bottom, stride);
        if (samples.Count == 0)
        {
            return new HealingStampResult(0, 0);
        }

        if (input.Strength >= 0.75)
        {
            samples = SelectCleanSkinSamples(samples);
        }

        HealingSampleAverages averages = CalculateAverages(samples);
        MaskPlane stampMask = BuildExpandedStampMask(input.DefectMask, input.MinX, input.MinY, input.MaxX, input.MaxY, stampGrow, edgeBlurRadius);
        int changed = 0;
        double strengthSum = 0;

        for (int y = Math.Max(0, input.MinY - stampGrow - edgeBlurRadius); y <= Math.Min(height - 1, input.MaxY + stampGrow + edgeBlurRadius); y++)
        {
            for (int x = Math.Max(0, input.MinX - stampGrow - edgeBlurRadius); x <= Math.Min(width - 1, input.MaxX + stampGrow + edgeBlurRadius); x++)
            {
                double hardProtect = Math.Clamp(input.HardProtectMask[x, y], 0, 1);
                if (hardProtect >= 0.05)
                {
                    continue;
                }

                double maskWeight = Math.Clamp(input.RetouchAllowMask[x, y], 0, 1);
                if (input.IsSoftProtect)
                {
                    maskWeight = Math.Max(maskWeight, input.SoftProtectMask[x, y] * input.SoftProtectScale);
                }

                double feather = Math.Clamp(stampMask[x, y], 0, 1);
                bool isMaximumStrength = input.Strength >= 0.9;
                double effectiveFeather = isMaximumStrength ? Math.Sqrt(feather) : feather;
                double maxAmount = isMaximumStrength ? 0.98 : 0.88;
                double amount = Math.Clamp(input.Strength * maskWeight * effectiveFeather * (1 - hardProtect), 0, maxAmount);
                if (amount <= 0.0001)
                {
                    continue;
                }

                HealingColor stamp = BuildHealingStamp(samples, averages, x, y);
                int index = y * stride + x * 4;
                input.TargetPixels[index] = BlendChannel(input.TargetPixels[index], stamp.Blue, amount);
                input.TargetPixels[index + 1] = BlendChannel(input.TargetPixels[index + 1], stamp.Green, amount);
                input.TargetPixels[index + 2] = BlendChannel(input.TargetPixels[index + 2], stamp.Red, amount);
                input.TargetPixels[index + 3] = input.CurrentPixels[index + 3];
                input.OutputMask[x, y] = Math.Max(input.OutputMask[x, y], amount);
                changed++;
                strengthSum += amount;
            }
        }

        return new HealingStampResult(changed, changed == 0 ? 0 : strengthSum / changed);
    }

    private static MaskPlane BuildExpandedStampMask(MaskPlane source, int minX, int minY, int maxX, int maxY, int growRadius, int blurRadius)
    {
        MaskPlane expanded = MaskPlane.Empty(source.Width, source.Height);
        double radiusSquared = Math.Max(1, growRadius * growRadius);
        int left = Math.Max(0, minX - growRadius);
        int top = Math.Max(0, minY - growRadius);
        int right = Math.Min(source.Width - 1, maxX + growRadius);
        int bottom = Math.Min(source.Height - 1, maxY + growRadius);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double best = source[x, y];
                if (best <= 0.0001)
                {
                    for (int sampleY = Math.Max(0, y - growRadius); sampleY <= Math.Min(source.Height - 1, y + growRadius); sampleY++)
                    {
                        for (int sampleX = Math.Max(0, x - growRadius); sampleX <= Math.Min(source.Width - 1, x + growRadius); sampleX++)
                        {
                            double sourceValue = source[sampleX, sampleY];
                            if (sourceValue <= 0.0001)
                            {
                                continue;
                            }

                            int dx = sampleX - x;
                            int dy = sampleY - y;
                            double distanceSquared = dx * dx + dy * dy;
                            if (distanceSquared > radiusSquared)
                            {
                                continue;
                            }

                            double falloff = 1 - Math.Sqrt(distanceSquared / radiusSquared);
                            best = Math.Max(best, sourceValue * (0.42 + falloff * 0.48));
                        }
                    }
                }

                expanded[x, y] = Math.Clamp(best, 0, 1);
            }
        }

        return BlurMask(expanded, blurRadius, left, top, right, bottom);
    }

    private static MaskPlane BlurMask(MaskPlane source, int radius, int left, int top, int right, int bottom)
    {
        MaskPlane output = source.Clone();
        int blurLeft = Math.Max(0, left - radius);
        int blurTop = Math.Max(0, top - radius);
        int blurRight = Math.Min(source.Width - 1, right + radius);
        int blurBottom = Math.Min(source.Height - 1, bottom + radius);

        for (int y = blurTop; y <= blurBottom; y++)
        {
            for (int x = blurLeft; x <= blurRight; x++)
            {
                double sum = 0;
                double weight = 0;
                for (int sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(source.Height - 1, y + radius); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(source.Width - 1, x + radius); sampleX++)
                    {
                        double dx = sampleX - x;
                        double dy = sampleY - y;
                        double distance = Math.Sqrt(dx * dx + dy * dy);
                        if (distance > radius)
                        {
                            continue;
                        }

                        double sampleWeight = 1 - distance / (radius + 1d);
                        sum += source[sampleX, sampleY] * sampleWeight;
                        weight += sampleWeight;
                    }
                }

                output[x, y] = weight > 0 ? Math.Clamp(sum / weight, 0, 1) : source[x, y];
            }
        }

        return output;
    }

    private static List<HealingSourceSample> CollectSourceSamples(HealingStampInput input, int left, int top, int right, int bottom, int stride)
    {
        List<HealingSourceSample> samples = new();
        int step = Math.Max(1, Math.Max(right - left + 1, bottom - top + 1) / 48);
        for (int y = top; y <= bottom; y += step)
        {
            for (int x = left; x <= right; x += step)
            {
                bool insideDefectBounds = x >= input.MinX &&
                    x <= input.MaxX &&
                    y >= input.MinY &&
                    y <= input.MaxY;
                if (insideDefectBounds ||
                    input.DefectMask[x, y] > 0.08 ||
                    input.HardProtectMask[x, y] > 0.05)
                {
                    continue;
                }

                double maskWeight = Math.Max(input.RetouchAllowMask[x, y], input.SoftProtectMask[x, y] * input.SoftProtectScale);
                if (maskWeight < 0.18)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                samples.Add(new HealingSourceSample(
                    input.OriginalPixels[index],
                    input.OriginalPixels[index + 1],
                    input.OriginalPixels[index + 2],
                    input.CurrentPixels[index],
                    input.CurrentPixels[index + 1],
                    input.CurrentPixels[index + 2]));
            }
        }

        return samples;
    }

    private static List<HealingSourceSample> SelectCleanSkinSamples(List<HealingSourceSample> samples)
    {
        if (samples.Count < 12)
        {
            return samples;
        }

        double[] luminanceValues = samples
            .Select(sample => GetLuminance(sample.CurrentRed, sample.CurrentGreen, sample.CurrentBlue))
            .OrderBy(value => value)
            .ToArray();
        double[] redExcessValues = samples
            .Select(sample => GetRedExcess(sample.CurrentRed, sample.CurrentGreen, sample.CurrentBlue))
            .OrderBy(value => value)
            .ToArray();

        double medianLuminance = luminanceValues[luminanceValues.Length / 2];
        double medianRedExcess = redExcessValues[redExcessValues.Length / 2];
        double darkLimit = medianLuminance - 20;
        double redLimit = medianRedExcess + 10;

        List<HealingSourceSample> clean = samples
            .Where(sample =>
            {
                double luminance = GetLuminance(sample.CurrentRed, sample.CurrentGreen, sample.CurrentBlue);
                double redExcess = GetRedExcess(sample.CurrentRed, sample.CurrentGreen, sample.CurrentBlue);
                return luminance >= darkLimit && redExcess <= redLimit;
            })
            .ToList();

        return clean.Count >= Math.Max(6, samples.Count / 4) ? clean : samples;
    }

    private static double GetLuminance(byte red, byte green, byte blue)
    {
        return red * 0.299 + green * 0.587 + blue * 0.114;
    }

    private static double GetRedExcess(byte red, byte green, byte blue)
    {
        return red - (green + blue) * 0.5;
    }

    private static HealingSampleAverages CalculateAverages(IReadOnlyList<HealingSourceSample> samples)
    {
        double originalBlue = 0;
        double originalGreen = 0;
        double originalRed = 0;
        double currentBlue = 0;
        double currentGreen = 0;
        double currentRed = 0;

        foreach (HealingSourceSample sample in samples)
        {
            originalBlue += sample.OriginalBlue;
            originalGreen += sample.OriginalGreen;
            originalRed += sample.OriginalRed;
            currentBlue += sample.CurrentBlue;
            currentGreen += sample.CurrentGreen;
            currentRed += sample.CurrentRed;
        }

        double count = Math.Max(1, samples.Count);
        return new HealingSampleAverages(
            originalBlue / count,
            originalGreen / count,
            originalRed / count,
            currentBlue / count,
            currentGreen / count,
            currentRed / count);
    }

    private static HealingColor BuildHealingStamp(IReadOnlyList<HealingSourceSample> samples, HealingSampleAverages averages, int x, int y)
    {
        double blue = 0;
        double green = 0;
        double red = 0;
        const int taps = 3;

        for (int tap = 0; tap < taps; tap++)
        {
            HealingSourceSample sample = samples[PositiveHash(x, y, tap) % samples.Count];
            blue += averages.CurrentBlue + (sample.OriginalBlue - averages.OriginalBlue) * 0.82;
            green += averages.CurrentGreen + (sample.OriginalGreen - averages.OriginalGreen) * 0.82;
            red += averages.CurrentRed + (sample.OriginalRed - averages.OriginalRed) * 0.82;
        }

        return new HealingColor(
            Math.Clamp(blue / taps, 0, 255),
            Math.Clamp(green / taps, 0, 255),
            Math.Clamp(red / taps, 0, 255));
    }

    private static int PositiveHash(int x, int y, int tap)
    {
        unchecked
        {
            int hash = x * 73856093 ^ y * 19349663 ^ (tap + 17) * 83492791;
            return hash & 0x7fffffff;
        }
    }

    private static byte BlendChannel(byte source, double target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private sealed record HealingSourceSample(
        byte OriginalBlue,
        byte OriginalGreen,
        byte OriginalRed,
        byte CurrentBlue,
        byte CurrentGreen,
        byte CurrentRed);

    private sealed record HealingSampleAverages(
        double OriginalBlue,
        double OriginalGreen,
        double OriginalRed,
        double CurrentBlue,
        double CurrentGreen,
        double CurrentRed);

    private sealed record HealingColor(double Blue, double Green, double Red);
}

public sealed record HealingStampInput(
    byte[] OriginalPixels,
    byte[] CurrentPixels,
    byte[] TargetPixels,
    int Width,
    int Height,
    MaskPlane DefectMask,
    MaskPlane RetouchAllowMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    MaskPlane OutputMask,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    double Strength,
    double SoftProtectScale,
    int FeatherRadius,
    bool IsSoftProtect);

public sealed record HealingStampResult(int ChangedPixelCount, double AverageStrength);
