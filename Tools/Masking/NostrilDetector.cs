using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class NostrilDetector
{
    public string DetectorVersion => "nostril_detector_v1";

    public NostrilDetectorResult Detect(NostrilDetectorInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        BitmapSource bitmap = input.OriginalImage.Format == PixelFormats.Bgra32
            ? input.OriginalImage
            : new FormatConvertedBitmap(input.OriginalImage, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        MaskPlane.EnsureSameSize(input.WarpedStandardNostrilMask, MaskPlane.Empty(width, height));
        Int32Rect roi = CreateNoseLowerRoi(input, width, height);
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        List<string> warnings = new();
        MaskPlane darkCandidateMask = BuildDarkCandidateMask(pixels, width, height, stride, roi, input, warnings);
        IReadOnlyList<RawComponent> rawComponents = FindComponents(darkCandidateMask, roi);
        IReadOnlyList<NostrilCandidateComponent> scoredComponents = ScoreComponents(rawComponents, pixels, stride, input, roi);
        IReadOnlyList<NostrilCandidateComponent> selectedComponents = SelectComponents(scoredComponents, input.NoseTip.X);
        if (selectedComponents.Count == 0)
        {
            warnings.Add("nostril_dark_candidate_not_selected");
        }

        MaskPlane selectedComponentMask = BuildSelectedComponentMask(width, height, rawComponents, selectedComponents);
        MaskPlane standardFallback = MaskPlane.Multiply(input.WarpedStandardNostrilMask, selectedComponents.Count == 0 ? 1.0 : 0.42);
        MaskPlane finalMask = MaskPlane.Union(selectedComponentMask, standardFallback);
        finalMask = Dilate(finalMask, 2);
        finalMask = Feather(finalMask);
        double confidence = CalculateConfidence(selectedComponents, input.WarpedStandardNostrilMask, selectedComponentMask, roi, warnings);
        if (confidence < 0.45)
        {
            warnings.Add("nostril_detection_low_confidence");
            finalMask = MaskPlane.Union(finalMask, MaskPlane.Multiply(CreateRoiMask(width, height, roi), 0.25));
        }

        return new NostrilDetectorResult(
            finalMask,
            roi,
            darkCandidateMask,
            BuildComponentDebugMask(width, height, rawComponents),
            confidence,
            warnings,
            scoredComponents);
    }

    private static Int32Rect CreateNoseLowerRoi(NostrilDetectorInput input, int width, int height)
    {
        double verticalDistance = Math.Max(8, input.MouthCenter.Y - input.NoseTip.Y);
        double roiTop = input.NoseTip.Y;
        double roiBottom = input.NoseTip.Y + verticalDistance * 0.55;
        double roiWidth = Math.Clamp(input.FaceBox.Width * 0.28, input.FaceBox.Width * 0.22, input.FaceBox.Width * 0.32);
        int x = Math.Clamp((int)Math.Round(input.NoseTip.X - roiWidth / 2), 0, Math.Max(0, width - 1));
        int y = Math.Clamp((int)Math.Round(roiTop), 0, Math.Max(0, height - 1));
        int right = Math.Clamp((int)Math.Round(input.NoseTip.X + roiWidth / 2), x + 1, width);
        int bottom = Math.Clamp((int)Math.Round(roiBottom), y + 1, height);
        return new Int32Rect(x, y, Math.Max(1, right - x), Math.Max(1, bottom - y));
    }

    private static MaskPlane BuildDarkCandidateMask(
        byte[] pixels,
        int width,
        int height,
        int stride,
        Int32Rect roi,
        NostrilDetectorInput input,
        List<string> warnings)
    {
        List<double> luminanceValues = new(roi.Width * roi.Height);
        double sum = 0;
        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                double luminance = GetLuminance(pixels, y * stride + x * 4);
                luminanceValues.Add(luminance);
                sum += luminance;
            }
        }

        if (luminanceValues.Count == 0)
        {
            warnings.Add("nose_lower_roi_empty");
            return MaskPlane.Empty(width, height);
        }

        luminanceValues.Sort();
        double average = sum / luminanceValues.Count;
        double percentile = luminanceValues[Math.Clamp((int)Math.Round(luminanceValues.Count * 0.16), 0, luminanceValues.Count - 1)];
        double threshold = Math.Min(percentile, average - 14);
        MaskPlane candidateMask = MaskPlane.Empty(width, height);

        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                int index = y * stride + x * 4;
                double luminance = GetLuminance(pixels, index);
                double lipBlock = input.LipMask?[x, y] ?? 0;
                double beardBlock = input.BeardMask?[x, y] ?? 0;
                if (luminance <= threshold &&
                    lipBlock < 0.35 &&
                    beardBlock < 0.65)
                {
                    candidateMask[x, y] = 1;
                }
            }
        }

        if (candidateMask.Average() <= 0)
        {
            warnings.Add("nostril_dark_candidate_empty");
        }

        return candidateMask;
    }

    private static IReadOnlyList<RawComponent> FindComponents(MaskPlane candidateMask, Int32Rect roi)
    {
        bool[] visited = new bool[candidateMask.Values.Length];
        List<RawComponent> components = new();
        int id = 1;
        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                int index = y * candidateMask.Width + x;
                if (visited[index] || candidateMask[x, y] <= 0)
                {
                    continue;
                }

                components.Add(FloodFillComponent(candidateMask, visited, roi, x, y, id++));
            }
        }

        return components;
    }

    private static RawComponent FloodFillComponent(MaskPlane candidateMask, bool[] visited, Int32Rect roi, int startX, int startY, int id)
    {
        Queue<(int X, int Y)> queue = new();
        List<(int X, int Y)> pixels = new();
        queue.Enqueue((startX, startY));
        visited[startY * candidateMask.Width + startX] = true;
        int minX = startX;
        int maxX = startX;
        int minY = startY;
        int maxY = startY;

        while (queue.Count > 0)
        {
            (int x, int y) = queue.Dequeue();
            pixels.Add((x, y));
            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
            minY = Math.Min(minY, y);
            maxY = Math.Max(maxY, y);

            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int nextY = y + offsetY;
                if (nextY < roi.Y || nextY >= roi.Y + roi.Height)
                {
                    continue;
                }

                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int nextX = x + offsetX;
                    if (nextX < roi.X || nextX >= roi.X + roi.Width)
                    {
                        continue;
                    }

                    int nextIndex = nextY * candidateMask.Width + nextX;
                    if (!visited[nextIndex] && candidateMask[nextX, nextY] > 0)
                    {
                        visited[nextIndex] = true;
                        queue.Enqueue((nextX, nextY));
                    }
                }
            }
        }

        return new RawComponent(id, new Int32Rect(minX, minY, maxX - minX + 1, maxY - minY + 1), pixels);
    }

    private static IReadOnlyList<NostrilCandidateComponent> ScoreComponents(
        IReadOnlyList<RawComponent> rawComponents,
        byte[] pixels,
        int stride,
        NostrilDetectorInput input,
        Int32Rect roi)
    {
        List<NostrilCandidateComponent> components = new();
        double roiArea = Math.Max(1, roi.Width * roi.Height);
        foreach (RawComponent component in rawComponents)
        {
            int area = component.Pixels.Count;
            double componentWidth = component.BoundingBox.Width;
            double componentHeight = component.BoundingBox.Height;
            double aspectRatio = componentWidth / Math.Max(1, componentHeight);
            double centerX = component.BoundingBox.X + component.BoundingBox.Width / 2d;
            double centerY = component.BoundingBox.Y + component.BoundingBox.Height / 2d;
            double meanBrightness = component.Pixels.Average(pixel => GetLuminance(pixels, pixel.Y * stride + pixel.X * 4));
            double score = 0;

            double areaRatio = area / roiArea;
            if (areaRatio >= 0.006 && areaRatio <= 0.18)
            {
                score += 0.24;
            }

            if (aspectRatio >= 0.35 && aspectRatio <= 3.2)
            {
                score += 0.18;
            }

            double normalizedY = (centerY - roi.Y) / Math.Max(1, roi.Height);
            if (normalizedY >= 0.18 && normalizedY <= 0.82)
            {
                score += 0.16;
            }

            double centerDistance = Math.Abs(centerX - input.NoseTip.X) / Math.Max(1, roi.Width / 2d);
            if (centerDistance >= 0.12 && centerDistance <= 0.82)
            {
                score += 0.18;
            }

            double mouthDistance = Math.Abs(input.MouthCenter.Y - centerY) / Math.Max(1, input.MouthCenter.Y - input.NoseTip.Y);
            if (mouthDistance >= 0.42)
            {
                score += 0.12;
            }

            double standardOverlap = component.Pixels.Average(pixel => input.WarpedStandardNostrilMask[pixel.X, pixel.Y]);
            score += Math.Clamp(standardOverlap, 0, 1) * 0.24;

            bool isLeft = centerX < input.NoseTip.X;
            components.Add(new NostrilCandidateComponent(
                component.Id,
                component.BoundingBox,
                area,
                aspectRatio,
                new WpfPoint(centerX, centerY),
                meanBrightness,
                Math.Clamp(score, 0, 1),
                isLeft,
                !isLeft,
                false));
        }

        return components
            .OrderByDescending(component => component.Score)
            .ToArray();
    }

    private static IReadOnlyList<NostrilCandidateComponent> SelectComponents(IReadOnlyList<NostrilCandidateComponent> components, double noseCenterX)
    {
        NostrilCandidateComponent? left = components
            .Where(component => component.IsLeftSide && component.Score >= 0.34)
            .OrderByDescending(component => component.Score)
            .FirstOrDefault();
        NostrilCandidateComponent? right = components
            .Where(component => component.IsRightSide && component.Score >= 0.34)
            .OrderByDescending(component => component.Score)
            .FirstOrDefault();

        List<NostrilCandidateComponent> selected = new();
        if (left is not null && right is not null)
        {
            double yDifference = Math.Abs(left.Center.Y - right.Center.Y);
            double areaRatio = Math.Min(left.Area, right.Area) / (double)Math.Max(left.Area, right.Area);
            if (yDifference <= Math.Max(left.BoundingBox.Height, right.BoundingBox.Height) * 1.8 &&
                areaRatio >= 0.28)
            {
                selected.Add(left with { IsSelected = true });
                selected.Add(right with { IsSelected = true });
                return selected;
            }
        }

        NostrilCandidateComponent? single = components.FirstOrDefault(component => component.Score >= 0.48);
        if (single is not null)
        {
            selected.Add(single with { IsSelected = true });
        }

        return selected;
    }

    private static MaskPlane BuildSelectedComponentMask(int width, int height, IReadOnlyList<RawComponent> rawComponents, IReadOnlyList<NostrilCandidateComponent> selectedComponents)
    {
        HashSet<int> selectedIds = selectedComponents.Select(component => component.Id).ToHashSet();
        MaskPlane mask = MaskPlane.Empty(width, height);
        foreach (RawComponent component in rawComponents)
        {
            if (!selectedIds.Contains(component.Id))
            {
                continue;
            }

            foreach ((int x, int y) in component.Pixels)
            {
                mask[x, y] = 1;
            }
        }

        return mask;
    }

    private static MaskPlane BuildComponentDebugMask(int width, int height, IReadOnlyList<RawComponent> rawComponents)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        foreach (RawComponent component in rawComponents)
        {
            double value = Math.Clamp(component.Id / 12d, 0.18, 1);
            foreach ((int x, int y) in component.Pixels)
            {
                mask[x, y] = value;
            }
        }

        return mask;
    }

    private static double CalculateConfidence(
        IReadOnlyList<NostrilCandidateComponent> selectedComponents,
        MaskPlane warpedStandardMask,
        MaskPlane selectedComponentMask,
        Int32Rect roi,
        List<string> warnings)
    {
        double confidence = selectedComponents.Count switch
        {
            >= 2 => 0.58,
            1 => 0.38,
            _ => 0.20
        };

        double overlap = 0;
        double selectedSum = 0;
        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                double selected = selectedComponentMask[x, y];
                selectedSum += selected;
                overlap += Math.Min(selected, warpedStandardMask[x, y]);
            }
        }

        if (selectedSum > 0)
        {
            confidence += Math.Clamp(overlap / selectedSum, 0, 1) * 0.22;
        }
        else
        {
            warnings.Add("nostril_using_warped_standard_fallback");
        }

        if (selectedComponents.Count >= 2)
        {
            confidence += 0.14;
        }

        return Math.Clamp(confidence, 0, 1);
    }

    private static MaskPlane CreateRoiMask(int width, int height, Int32Rect roi)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                mask[x, y] = 1;
            }
        }

        return mask;
    }

    private static MaskPlane Dilate(MaskPlane source, int radius)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double value = 0;
                for (int sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(source.Height - 1, y + radius); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(source.Width - 1, x + radius); sampleX++)
                    {
                        value = Math.Max(value, source[sampleX, sampleY]);
                    }
                }

                result[x, y] = value;
            }
        }

        return result;
    }

    private static MaskPlane Feather(MaskPlane source)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double sum = 0;
                int count = 0;
                for (int sampleY = Math.Max(0, y - 1); sampleY <= Math.Min(source.Height - 1, y + 1); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - 1); sampleX <= Math.Min(source.Width - 1, x + 1); sampleX++)
                    {
                        sum += source[sampleX, sampleY];
                        count++;
                    }
                }

                result[x, y] = sum / count;
            }
        }

        return result;
    }

    private static double GetLuminance(byte[] pixels, int index)
    {
        return pixels[index + 2] * 0.299 + pixels[index + 1] * 0.587 + pixels[index] * 0.114;
    }

    private sealed record RawComponent(int Id, Int32Rect BoundingBox, IReadOnlyList<(int X, int Y)> Pixels);
}
