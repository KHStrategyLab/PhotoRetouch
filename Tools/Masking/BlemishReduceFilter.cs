using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class BlemishReduceFilter : IBlemishReduceFilter
{
    private const int MaxAnalysisCacheEntries = 16;

    private readonly Dictionary<string, BlemishAnalysisCache> _analysisCache = new();

    public int AnalysisCacheCount => _analysisCache.Count;

    public void ClearAnalysisCache()
    {
        _analysisCache.Clear();
    }

    public BlemishReduceResult Apply(BlemishReduceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        int width = input.OriginalImage.PixelWidth;
        int height = input.OriginalImage.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = CopyPixels(input.OriginalImage);
        byte[] currentPixels = CopyPixels(input.CurrentRetouchedImage);

        BlemishAnalysisCache analysis = GetOrCreateAnalysis(input, originalPixels, width, height);
        byte[] correctedPixels = (byte[])currentPixels.Clone();
        MaskPlane blemishMask = MaskPlane.Empty(width, height);
        int appliedCount = 0;
        double strengthSum = 0;

        double qualityScale = GetQualityScale(input.MaskQualityReport);
        double baseAmount = Math.Clamp(input.StagePreset.BlemishReduceAmount * 1.25 * qualityScale, 0, 1);
        double softProtectScale = Math.Clamp(input.StagePreset.BlemishSearchSoftProtectOpacity, 0, 0.25);
        int applyFeatherRadius = Math.Max(1, (int)Math.Round(input.StagePreset.BlemishFeatherRadius * Math.Max(width, height) / 1200d));

        foreach (BlemishCandidate candidate in analysis.Candidates)
        {
            double candidateStrength = Math.Clamp(baseAmount * candidate.Score, 0, 0.92);
            if (candidate.IsSoftProtect)
            {
                candidateStrength *= Math.Max(0.08, softProtectScale);
            }

            if (candidateStrength < 0.015)
            {
                continue;
            }

            ColorSample target = SampleSurroundingSkin(originalPixels, input, candidate, stride, width, height);
            if (target.Weight <= 0)
            {
                continue;
            }

            for (int y = Math.Max(0, candidate.MinY - applyFeatherRadius); y <= Math.Min(height - 1, candidate.MaxY + applyFeatherRadius); y++)
            {
                for (int x = Math.Max(0, candidate.MinX - applyFeatherRadius); x <= Math.Min(width - 1, candidate.MaxX + applyFeatherRadius); x++)
                {
                    double hardProtect = input.HardProtectMask[x, y];
                    if (hardProtect >= 0.05)
                    {
                        continue;
                    }

                    double localMask = Math.Clamp(input.RetouchAllowMask[x, y], 0, 1);
                    if (candidate.IsSoftProtect)
                    {
                        localMask = Math.Max(localMask, input.SoftProtectMask[x, y] * softProtectScale);
                    }

                    double feather = Math.Clamp(analysis.CandidateMask[x, y], 0, 1);
                    double amount = Math.Clamp(candidateStrength * localMask * feather, 0, 0.92);
                    if (amount <= 0)
                    {
                        continue;
                    }

                    int index = y * stride + x * 4;
                    correctedPixels[index] = BlendChannel(correctedPixels[index], target.Blue, amount);
                    correctedPixels[index + 1] = BlendChannel(correctedPixels[index + 1], target.Green, amount);
                    correctedPixels[index + 2] = BlendChannel(correctedPixels[index + 2], target.Red, amount);
                    correctedPixels[index + 3] = currentPixels[index + 3];
                    blemishMask[x, y] = Math.Max(blemishMask[x, y], amount);
                }
            }

            appliedCount++;
            strengthSum += candidateStrength;
        }

        RestoreHardProtect(originalPixels, correctedPixels, input.HardProtectMask, width, height);
        BlemishProcessReport report = new(
            analysis.Candidates.Count,
            appliedCount,
            Math.Max(0, analysis.Candidates.Count - appliedCount),
            baseAmount,
            appliedCount == 0 ? 0 : strengthSum / appliedCount,
            analysis.DebugWarnings);

        return new BlemishReduceResult(
            CreateBitmap(width, height, correctedPixels),
            blemishMask,
            analysis.CandidateMask,
            report,
            analysis.DebugWarnings);
    }

    private BlemishAnalysisCache GetOrCreateAnalysis(BlemishReduceInput input, byte[] originalPixels, int width, int height)
    {
        string cacheKey = input.Snapshot.CacheKey.StableId + "|blemish_v1";
        if (_analysisCache.TryGetValue(cacheKey, out BlemishAnalysisCache? cached))
        {
            return cached;
        }

        BlemishAnalysisCache created = AnalyzeBlemishes(input, originalPixels, width, height);
        _analysisCache[cacheKey] = created;
        TrimAnalysisCache();
        return created;
    }

    private void TrimAnalysisCache()
    {
        while (_analysisCache.Count > MaxAnalysisCacheEntries)
        {
            _analysisCache.Remove(_analysisCache.Keys.First());
        }
    }

    private static BlemishAnalysisCache AnalyzeBlemishes(BlemishReduceInput input, byte[] originalPixels, int width, int height)
    {
        int stride = width * 4;
        byte[] localAverage = BoxBlur(originalPixels, width, height, GetAnalysisBlurRadius(width, height));
        bool[] candidateMap = new bool[width * height];
        MaskPlane searchMask = BuildSearchMask(input, width, height);
        MaskPlane candidatePreview = MaskPlane.Empty(width, height);
        List<string> warnings = new() { "blemish_reduce_filter_v1" };

        double scale = Math.Max(width, height) / 1200d;
        double minContrast = Math.Max(7, input.StagePreset.BlemishMinContrast);
        double maxDarkContrast = 96 + input.AppliedStage * 7;
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int pixelIndex = y * width + x;
                double mask = searchMask.Values[pixelIndex];
                if (mask < 0.12)
                {
                    continue;
                }

                int index = pixelIndex * 4;
                double sourceLum = GetLuminance(originalPixels, index);
                double averageLum = GetLuminance(localAverage, index);
                double darkDetail = averageLum - sourceLum;
                double redExcess = originalPixels[index + 2] - Math.Max(originalPixels[index + 1], originalPixels[index]);
                double averageRedExcess = localAverage[index + 2] - Math.Max(localAverage[index + 1], localAverage[index]);
                double redDetail = redExcess - averageRedExcess * 0.38;
                double chromaDifference =
                    Math.Abs(originalPixels[index] - localAverage[index]) +
                    Math.Abs(originalPixels[index + 1] - localAverage[index + 1]) +
                    Math.Abs(originalPixels[index + 2] - localAverage[index + 2]);
                double edgeScore = CalculateLocalEdge(originalPixels, width, height, x, y);
                double darkScore = SmoothStep(minContrast, minContrast + 22, darkDetail) * (1 - SmoothStep(maxDarkContrast, maxDarkContrast + 70, darkDetail));
                double redScore = SmoothStep(minContrast * 0.72, minContrast + 24, redDetail) * (1 - SmoothStep(120, 190, redDetail));
                double colorScore = SmoothStep(minContrast * 1.7, minContrast * 5.4, chromaDifference);
                double edgeProtection = 1 - SmoothStep(45, 96, edgeScore);
                double score = Math.Max(darkScore, Math.Max(redScore * 0.86, colorScore * 0.38)) * edgeProtection * mask;

                if (score > 0.24)
                {
                    candidateMap[pixelIndex] = true;
                    candidatePreview[x, y] = score;
                }
            }
        }

        List<BlemishCandidate> candidates = ExtractCandidates(
            candidateMap,
            candidatePreview,
            searchMask,
            input,
            width,
            height,
            scale,
            warnings);
        MaskPlane featheredCandidateMask = BuildCandidateMask(candidates, width, height, Math.Max(1, (int)Math.Round(input.StagePreset.BlemishFeatherRadius * scale)));
        return new BlemishAnalysisCache(input.Snapshot.CacheKey.StableId, featheredCandidateMask, candidates, warnings);
    }

    private static List<BlemishCandidate> ExtractCandidates(
        bool[] candidateMap,
        MaskPlane candidatePreview,
        MaskPlane searchMask,
        BlemishReduceInput input,
        int width,
        int height,
        double scale,
        List<string> warnings)
    {
        bool[] visited = new bool[candidateMap.Length];
        List<BlemishCandidate> candidates = new();
        int minArea = Math.Max(2, (int)Math.Round(2 * scale * scale));
        int maxArea = Math.Max(minArea + 2, (int)Math.Round(input.StagePreset.BlemishMaxArea * scale * scale));
        int hardProtectRadius = Math.Max(2, (int)Math.Round(5 * scale));

        for (int startIndex = 0; startIndex < candidateMap.Length; startIndex++)
        {
            if (!candidateMap[startIndex] || visited[startIndex])
            {
                continue;
            }

            List<int> pixels = FloodFill(candidateMap, visited, width, height, startIndex);
            if (pixels.Count < minArea || pixels.Count > maxArea)
            {
                continue;
            }

            ComponentStats stats = CalculateStats(pixels, width, candidatePreview, input.SoftProtectMask);
            if (stats.Width <= 0 || stats.Height <= 0)
            {
                continue;
            }

            double aspect = Math.Max(stats.Width, stats.Height) / (double)Math.Max(1, Math.Min(stats.Width, stats.Height));
            if (aspect > 3.6 && pixels.Count > minArea + 2)
            {
                continue;
            }

            if (IsNearHardProtect(input.HardProtectMask, width, height, stats.CenterX, stats.CenterY, hardProtectRadius))
            {
                continue;
            }

            double softAverage = stats.SoftProtectAverage;
            bool isSoftProtect = softAverage > 0.18;
            if (isSoftProtect && aspect > 1.9)
            {
                continue;
            }

            double compactness = pixels.Count / (double)Math.Max(1, stats.Width * stats.Height);
            if (compactness < 0.18)
            {
                continue;
            }

            double score = Math.Clamp(stats.ScoreAverage * (0.75 + compactness * 0.35), 0.15, 1);
            candidates.Add(new BlemishCandidate(
                pixels.ToArray(),
                stats.MinX,
                stats.MinY,
                stats.MaxX,
                stats.MaxY,
                stats.CenterX,
                stats.CenterY,
                score,
                isSoftProtect));
        }

        if (candidates.Count == 0)
        {
            warnings.Add("blemish_no_candidates");
        }

        return candidates;
    }

    private static List<int> FloodFill(bool[] map, bool[] visited, int width, int height, int startIndex)
    {
        List<int> pixels = new();
        Queue<int> queue = new();
        queue.Enqueue(startIndex);
        visited[startIndex] = true;

        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            pixels.Add(index);
            int x = index % width;
            int y = index / width;
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            {
                int ny = y + offsetY;
                if (ny < 0 || ny >= height)
                {
                    continue;
                }

                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                    {
                        continue;
                    }

                    int nx = x + offsetX;
                    if (nx < 0 || nx >= width)
                    {
                        continue;
                    }

                    int neighborIndex = ny * width + nx;
                    if (map[neighborIndex] && !visited[neighborIndex])
                    {
                        visited[neighborIndex] = true;
                        queue.Enqueue(neighborIndex);
                    }
                }
            }
        }

        return pixels;
    }

    private static ComponentStats CalculateStats(List<int> pixels, int width, MaskPlane candidatePreview, MaskPlane softProtectMask)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = 0;
        int maxY = 0;
        double xSum = 0;
        double ySum = 0;
        double scoreSum = 0;
        double softSum = 0;
        foreach (int pixelIndex in pixels)
        {
            int x = pixelIndex % width;
            int y = pixelIndex / width;
            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x);
            maxY = Math.Max(maxY, y);
            xSum += x;
            ySum += y;
            scoreSum += candidatePreview[x, y];
            softSum += softProtectMask[x, y];
        }

        double count = Math.Max(1, pixels.Count);
        return new ComponentStats(
            minX,
            minY,
            maxX,
            maxY,
            xSum / count,
            ySum / count,
            Math.Max(1, maxX - minX + 1),
            Math.Max(1, maxY - minY + 1),
            scoreSum / count,
            softSum / count);
    }

    private static MaskPlane BuildCandidateMask(List<BlemishCandidate> candidates, int width, int height, int featherRadius)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        foreach (BlemishCandidate candidate in candidates)
        {
            foreach (int pixelIndex in candidate.PixelIndexes)
            {
                int x = pixelIndex % width;
                int y = pixelIndex / width;
                for (int offsetY = -featherRadius; offsetY <= featherRadius; offsetY++)
                {
                    int targetY = y + offsetY;
                    if (targetY < 0 || targetY >= height)
                    {
                        continue;
                    }

                    for (int offsetX = -featherRadius; offsetX <= featherRadius; offsetX++)
                    {
                        int targetX = x + offsetX;
                        if (targetX < 0 || targetX >= width)
                        {
                            continue;
                        }

                        double distance = Math.Sqrt(offsetX * offsetX + offsetY * offsetY);
                        if (distance > featherRadius + 0.001)
                        {
                            continue;
                        }

                        double amount = featherRadius <= 0 ? 1 : 1 - distance / (featherRadius + 1);
                        mask[targetX, targetY] = Math.Max(mask[targetX, targetY], amount * candidate.Score);
                    }
                }
            }
        }

        return mask;
    }

    private static MaskPlane BuildSearchMask(BlemishReduceInput input, int width, int height)
    {
        MaskPlane searchMask = MaskPlane.Empty(width, height);
        double softOpacity = Math.Clamp(input.StagePreset.BlemishSearchSoftProtectOpacity, 0, 0.25);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                double value = Math.Clamp(input.RetouchAllowMask[x, y] + input.SoftProtectMask[x, y] * softOpacity, 0, 1);
                searchMask[x, y] = value * (1 - hardProtect);
            }
        }

        return searchMask;
    }

    private static ColorSample SampleSurroundingSkin(byte[] originalPixels, BlemishReduceInput input, BlemishCandidate candidate, int stride, int width, int height)
    {
        int componentWidth = candidate.MaxX - candidate.MinX + 1;
        int componentHeight = candidate.MaxY - candidate.MinY + 1;
        int radius = Math.Clamp(Math.Max(componentWidth, componentHeight) * 2 + 5, 5, 28);
        double blueSum = 0;
        double greenSum = 0;
        double redSum = 0;
        double weightSum = 0;

        int left = Math.Max(0, candidate.MinX - radius);
        int right = Math.Min(width - 1, candidate.MaxX + radius);
        int top = Math.Max(0, candidate.MinY - radius);
        int bottom = Math.Min(height - 1, candidate.MaxY + radius);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                bool insideCandidateBox = x >= candidate.MinX - 1 &&
                                          x <= candidate.MaxX + 1 &&
                                          y >= candidate.MinY - 1 &&
                                          y <= candidate.MaxY + 1;
                if (insideCandidateBox)
                {
                    continue;
                }

                double mask = input.RetouchAllowMask[x, y] * (1 - input.HardProtectMask[x, y]);
                if (mask < 0.18)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                blueSum += originalPixels[index] * mask;
                greenSum += originalPixels[index + 1] * mask;
                redSum += originalPixels[index + 2] * mask;
                weightSum += mask;
            }
        }

        if (weightSum <= 0)
        {
            return new ColorSample(0, 0, 0, 0);
        }

        return new ColorSample(blueSum / weightSum, greenSum / weightSum, redSum / weightSum, weightSum);
    }

    private static bool IsNearHardProtect(MaskPlane hardProtectMask, int width, int height, double centerX, double centerY, int radius)
    {
        int cx = (int)Math.Round(centerX);
        int cy = (int)Math.Round(centerY);
        for (int y = Math.Max(0, cy - radius); y <= Math.Min(height - 1, cy + radius); y++)
        {
            for (int x = Math.Max(0, cx - radius); x <= Math.Min(width - 1, cx + radius); x++)
            {
                if (hardProtectMask[x, y] > 0.20)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void RestoreHardProtect(byte[] originalPixels, byte[] correctedPixels, MaskPlane hardProtectMask, int width, int height)
    {
        int stride = width * 4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = hardProtectMask[x, y];
                if (hardProtect <= 0)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                correctedPixels[index] = BlendChannel(correctedPixels[index], originalPixels[index], hardProtect);
                correctedPixels[index + 1] = BlendChannel(correctedPixels[index + 1], originalPixels[index + 1], hardProtect);
                correctedPixels[index + 2] = BlendChannel(correctedPixels[index + 2], originalPixels[index + 2], hardProtect);
                correctedPixels[index + 3] = originalPixels[index + 3];
            }
        }
    }

    private static int GetAnalysisBlurRadius(int width, int height)
    {
        double scale = Math.Max(width, height) / 1200d;
        return Math.Clamp((int)Math.Round(4 * scale), 3, 13);
    }

    private static double GetQualityScale(MaskQualityReport report)
    {
        double scale = 1;
        if (!report.IsUsable)
        {
            scale *= 0.55;
        }

        if (report.SkinMaskQualityScore < 0.55)
        {
            scale *= 0.68;
        }

        if (report.EyeMaskQualityScore < 0.55 ||
            report.LipMaskQualityScore < 0.55 ||
            report.NostrilMaskQualityScore < 0.55)
        {
            scale *= 0.72;
        }

        return Math.Clamp(scale, 0.25, 1);
    }

    private static byte[] BoxBlur(byte[] sourcePixels, int width, int height, int radius)
    {
        int stride = width * 4;
        byte[] horizontal = new byte[sourcePixels.Length];
        byte[] output = new byte[sourcePixels.Length];

        for (int y = 0; y < height; y++)
        {
            int blueSum = 0;
            int greenSum = 0;
            int redSum = 0;
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                int addX = Math.Min(width - 1, x + radius);
                int addIndex = y * stride + addX * 4;
                blueSum += sourcePixels[addIndex];
                greenSum += sourcePixels[addIndex + 1];
                redSum += sourcePixels[addIndex + 2];
                count++;

                if (x > radius)
                {
                    int removeX = x - radius - 1;
                    int removeIndex = y * stride + removeX * 4;
                    blueSum -= sourcePixels[removeIndex];
                    greenSum -= sourcePixels[removeIndex + 1];
                    redSum -= sourcePixels[removeIndex + 2];
                    count--;
                }

                int index = y * stride + x * 4;
                horizontal[index] = (byte)(blueSum / Math.Max(1, count));
                horizontal[index + 1] = (byte)(greenSum / Math.Max(1, count));
                horizontal[index + 2] = (byte)(redSum / Math.Max(1, count));
                horizontal[index + 3] = sourcePixels[index + 3];
            }
        }

        for (int x = 0; x < width; x++)
        {
            int blueSum = 0;
            int greenSum = 0;
            int redSum = 0;
            int count = 0;
            for (int y = 0; y < height; y++)
            {
                int addY = Math.Min(height - 1, y + radius);
                int addIndex = addY * stride + x * 4;
                blueSum += horizontal[addIndex];
                greenSum += horizontal[addIndex + 1];
                redSum += horizontal[addIndex + 2];
                count++;

                if (y > radius)
                {
                    int removeY = y - radius - 1;
                    int removeIndex = removeY * stride + x * 4;
                    blueSum -= horizontal[removeIndex];
                    greenSum -= horizontal[removeIndex + 1];
                    redSum -= horizontal[removeIndex + 2];
                    count--;
                }

                int index = y * stride + x * 4;
                output[index] = (byte)(blueSum / Math.Max(1, count));
                output[index + 1] = (byte)(greenSum / Math.Max(1, count));
                output[index + 2] = (byte)(redSum / Math.Max(1, count));
                output[index + 3] = sourcePixels[index + 3];
            }
        }

        return output;
    }

    private static double CalculateLocalEdge(byte[] pixels, int width, int height, int x, int y)
    {
        int stride = width * 4;
        int centerIndex = y * stride + x * 4;
        double center = GetLuminance(pixels, centerIndex);
        double maxDifference = 0;
        for (int offsetY = -1; offsetY <= 1; offsetY++)
        {
            int sampleY = Math.Clamp(y + offsetY, 0, height - 1);
            for (int offsetX = -1; offsetX <= 1; offsetX++)
            {
                int sampleX = Math.Clamp(x + offsetX, 0, width - 1);
                int sampleIndex = sampleY * stride + sampleX * 4;
                maxDifference = Math.Max(maxDifference, Math.Abs(center - GetLuminance(pixels, sampleIndex)));
            }
        }

        return maxDifference;
    }

    private static double GetLuminance(byte[] pixels, int index)
    {
        return pixels[index + 2] * 0.299 + pixels[index + 1] * 0.587 + pixels[index] * 0.114;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (Math.Abs(edge1 - edge0) < 0.0001)
        {
            return value >= edge1 ? 1 : 0;
        }

        double t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static byte BlendChannel(byte source, double target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static byte[] CopyPixels(BitmapSource source)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed record BlemishAnalysisCache(
        string SnapshotStableId,
        MaskPlane CandidateMask,
        IReadOnlyList<BlemishCandidate> Candidates,
        IReadOnlyList<string> DebugWarnings);

    private sealed record BlemishCandidate(
        int[] PixelIndexes,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double CenterX,
        double CenterY,
        double Score,
        bool IsSoftProtect);

    private sealed record ComponentStats(
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double CenterX,
        double CenterY,
        int Width,
        int Height,
        double ScoreAverage,
        double SoftProtectAverage);

    private sealed record ColorSample(double Blue, double Green, double Red, double Weight);
}

public interface IBlemishReduceFilter
{
    int AnalysisCacheCount { get; }

    BlemishReduceResult Apply(BlemishReduceInput input);

    void ClearAnalysisCache();
}

public sealed record BlemishReduceInput(
    BitmapSource OriginalImage,
    BitmapSource CurrentRetouchedImage,
    FaceSnapshotMaskSet Snapshot,
    MaskPlane RetouchAllowMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    int AppliedStage,
    StagePreset StagePreset,
    MaskQualityReport MaskQualityReport);

public sealed record BlemishReduceResult(
    BitmapSource BlemishReducedImage,
    MaskPlane BlemishMask,
    MaskPlane BlemishCandidateMask,
    BlemishProcessReport Report,
    IReadOnlyList<string> DebugWarnings);

public sealed record BlemishProcessReport(
    int CandidateCount,
    int AppliedCount,
    int SkippedCount,
    double BlemishReduceAmount,
    double AverageCorrectionStrength,
    IReadOnlyList<string> DebugWarnings);
