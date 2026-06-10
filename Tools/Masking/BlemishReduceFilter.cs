using System.Windows;
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
        bool isMaximumBlemishReduce = input.StagePreset.BlemishReduceAmount >= 0.98;
        double baseBoost = isMaximumBlemishReduce ? 1.65 : 1.25;
        double strengthLimit = isMaximumBlemishReduce ? 1.0 : 0.92;
        double baseAmount = Math.Clamp(input.StagePreset.BlemishReduceAmount * baseBoost * qualityScale, 0, 1);
        double softProtectScale = Math.Clamp(input.StagePreset.BlemishSearchSoftProtectOpacity, 0, 0.25);
        double blemishFeatherRadius = isMaximumBlemishReduce
            ? Math.Max(input.StagePreset.BlemishFeatherRadius * 2.4, 4.8)
            : input.StagePreset.BlemishFeatherRadius;
        int applyFeatherRadius = Math.Max(1, (int)Math.Round(blemishFeatherRadius * Math.Max(width, height) / 1200d));

        foreach (BlemishCandidate candidate in analysis.Candidates)
        {
            double effectiveCandidateScore = isMaximumBlemishReduce
                ? Math.Max(candidate.Score, 0.72)
                : candidate.Score;
            double candidateStrength = Math.Clamp(baseAmount * effectiveCandidateScore, 0, strengthLimit);
            candidateStrength *= GetCandidateKindStrengthScale(candidate.Kind, isMaximumBlemishReduce);
            candidateStrength = Math.Clamp(candidateStrength, 0, strengthLimit);
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

            if (isMaximumBlemishReduce)
            {
                target = AdjustMaximumBlemishTarget(target, candidate.Kind);
                target = BlendTargetTowardSkinAverage(target, analysis.SkinAverage, candidate.Kind);
            }

            HealingStampResult stampResult = HealingStampEngine.Apply(new HealingStampInput(
                originalPixels,
                currentPixels,
                correctedPixels,
                width,
                height,
                analysis.CandidateMask,
                input.RetouchAllowMask,
                input.SoftProtectMask,
                input.HardProtectMask,
                blemishMask,
                candidate.MinX,
                candidate.MinY,
                candidate.MaxX,
                candidate.MaxY,
                candidateStrength,
                softProtectScale,
                applyFeatherRadius,
                candidate.IsSoftProtect));

            if (stampResult.ChangedPixelCount == 0 || isMaximumBlemishReduce)
            {
                for (int y = Math.Max(0, candidate.MinY - applyFeatherRadius); y <= Math.Min(height - 1, candidate.MaxY + applyFeatherRadius); y++)
                {
                    for (int x = Math.Max(0, candidate.MinX - applyFeatherRadius); x <= Math.Min(width - 1, candidate.MaxX + applyFeatherRadius); x++)
                    {
                        double hardProtect = input.HardProtectMask[x, y];
                        if (hardProtect >= 0.05)
                        {
                            continue;
                        }

                        double localMask = Math.Clamp(Math.Max(input.RetouchAllowMask[x, y], analysis.WorkMask[x, y]), 0, 1);
                        if (candidate.IsSoftProtect)
                        {
                            localMask = Math.Max(localMask, input.SoftProtectMask[x, y] * softProtectScale);
                        }

                        double feather = Math.Clamp(analysis.CandidateMask[x, y], 0, 1);
                        double effectiveFeather = isMaximumBlemishReduce ? Math.Pow(feather, 0.35) : feather;
                        double settleScale = stampResult.ChangedPixelCount == 0 ? 1.0 : 1.35;
                        double amount = Math.Clamp(candidateStrength * localMask * effectiveFeather * settleScale, 0, strengthLimit);
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
            }

            if (baseAmount >= 0.28)
            {
                ApplySkinCleanColorSettle(
                    originalPixels,
                    currentPixels,
                    correctedPixels,
                    input,
                    analysis.CandidateMask,
                    analysis.WorkMask,
                    blemishMask,
                    candidate,
                    target,
                    analysis.SkinAverage,
                    candidateStrength,
                    softProtectScale,
                    applyFeatherRadius,
                    width,
                    height,
                    stride);
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
            analysis.DebugWarnings,
            analysis.CandidatePoints);

        return new BlemishReduceResult(
            CreateBitmap(width, height, correctedPixels),
            blemishMask,
            analysis.CandidateMask,
            report,
            analysis.DebugWarnings);
    }

    private BlemishAnalysisCache GetOrCreateAnalysis(BlemishReduceInput input, byte[] originalPixels, int width, int height)
    {
        string cacheKey = input.Snapshot.CacheKey.StableId + "|blemish_v2_skin_clean";
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
        BlemishCandidateKind[] candidateKindMap = new BlemishCandidateKind[width * height];
        MaskPlane searchMask = BuildSearchMask(input, width, height);
        SkinAverage skinAverage = CalculateSkinAverage(originalPixels, width, height, stride, searchMask);
        MaskPlane candidatePreview = MaskPlane.Empty(width, height);
        List<string> warnings = new() { "blemish_reduce_filter_v1" };

        double scale = Math.Max(width, height) / 1200d;
        double minContrast = Math.Max(7, input.StagePreset.BlemishMinContrast);
        double maxDarkContrast = 96 + input.AppliedStage * 7;
        double strongBlemishScale = SmoothStep(0.34, 0.70, input.StagePreset.BlemishReduceAmount);
        double candidateThreshold = 0.24 - strongBlemishScale * 0.14;
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
                double averageSkinRedExcess = GetRedExcess(skinAverage.Red, skinAverage.Green, skinAverage.Blue);
                double globalRedDetail = redExcess - averageSkinRedExcess * 0.52;
                double chromaDifference =
                    Math.Abs(originalPixels[index] - localAverage[index]) +
                    Math.Abs(originalPixels[index + 1] - localAverage[index + 1]) +
                    Math.Abs(originalPixels[index + 2] - localAverage[index + 2]);
                double globalColorDifference = GetColorDistance(
                    originalPixels[index + 2],
                    originalPixels[index + 1],
                    originalPixels[index],
                    skinAverage.Red,
                    skinAverage.Green,
                    skinAverage.Blue);
                double edgeScore = CalculateLocalEdge(originalPixels, width, height, x, y);
                double darkScore = SmoothStep(minContrast, minContrast + 22, darkDetail) * (1 - SmoothStep(maxDarkContrast, maxDarkContrast + 70, darkDetail));
                double redScore = SmoothStep(minContrast * 0.72, minContrast + 24, redDetail) * (1 - SmoothStep(120, 190, redDetail));
                double globalRedScore = SmoothStep(minContrast * 0.68, minContrast + 30, globalRedDetail) * (1 - SmoothStep(124, 196, globalRedDetail));
                double colorScore = SmoothStep(minContrast * 1.7, minContrast * 5.4, chromaDifference);
                double globalColorScore = SmoothStep(minContrast * 2.1, minContrast * 7.4, globalColorDifference);
                double edgeProtection = 1 - SmoothStep(45 + strongBlemishScale * 20, 96 + strongBlemishScale * 42, edgeScore);
                double score = Math.Max(
                    darkScore,
                    Math.Max(
                        Math.Max(redScore * 0.86, globalRedScore * 1.28),
                        Math.Max(colorScore * 0.46, globalColorScore * 0.62))) * edgeProtection * mask;

                if (score > candidateThreshold)
                {
                    candidateMap[pixelIndex] = true;
                    candidateKindMap[pixelIndex] = globalRedScore > Math.Max(redScore, darkScore) && globalRedDetail > 7
                        ? BlemishCandidateKind.Redness
                        : ClassifyCandidateKind(
                            darkScore,
                            redScore,
                            Math.Max(colorScore, globalColorScore),
                            darkDetail,
                            Math.Max(redDetail, globalRedDetail),
                            Math.Max(chromaDifference, globalColorDifference));
                    candidatePreview[x, y] = score;
                }
            }
        }

        List<BlemishCandidate> candidates = ExtractCandidates(
            candidateMap,
            candidateKindMap,
            candidatePreview,
            searchMask,
            input,
            width,
            height,
            scale,
            warnings);
        if (strongBlemishScale > 0.35)
        {
            List<BlemishCandidate> pointCandidates = ExtractFallbackColorPointCandidates(
                originalPixels,
                skinAverage,
                candidatePreview,
                searchMask,
                input,
                width,
                height,
                scale,
                warnings);
            if (pointCandidates.Count > 0)
            {
                candidates = pointCandidates;
            }
        }
        double blemishFeatherRadius = input.StagePreset.BlemishReduceAmount >= 0.98
            ? Math.Max(input.StagePreset.BlemishFeatherRadius * 2.4, 4.8)
            : input.StagePreset.BlemishFeatherRadius;
        MaskPlane featheredCandidateMask = BuildCandidateMask(candidates, width, height, Math.Max(1, (int)Math.Round(blemishFeatherRadius * scale)));
        IReadOnlyList<BlemishCandidatePoint> candidatePoints = candidates
            .Select(candidate => new BlemishCandidatePoint(
                candidate.Kind,
                candidate.CenterX,
                candidate.CenterY,
                candidate.MinX,
                candidate.MinY,
                candidate.MaxX,
                candidate.MaxY,
                candidate.Score,
                candidate.IsSoftProtect))
            .ToArray();
        return new BlemishAnalysisCache(input.Snapshot.CacheKey.StableId, featheredCandidateMask, searchMask, skinAverage, candidates, candidatePoints, warnings);
    }

    private static List<BlemishCandidate> ExtractCandidates(
        bool[] candidateMap,
        BlemishCandidateKind[] candidateKindMap,
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
        double strongBlemishScale = SmoothStep(0.34, 0.70, input.StagePreset.BlemishReduceAmount);
        int maxArea = Math.Max(minArea + 2, (int)Math.Round(input.StagePreset.BlemishMaxArea * (1 + strongBlemishScale * 1.6) * scale * scale));
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
            if (aspect > 6.2 && pixels.Count > minArea + 2)
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
            if (compactness < 0.06)
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
                isSoftProtect,
                ResolveCandidateKind(pixels, candidateKindMap)));
        }

        if (candidates.Count == 0)
        {
            warnings.Add("blemish_no_candidates");
        }

        return candidates;
    }

    private static BlemishCandidateKind ClassifyCandidateKind(
        double darkScore,
        double redScore,
        double colorScore,
        double darkDetail,
        double redDetail,
        double chromaDifference)
    {
        if (redScore >= darkScore && redScore >= colorScore * 0.72 && redDetail > 7)
        {
            return BlemishCandidateKind.Redness;
        }

        if (darkScore >= redScore && darkScore >= colorScore * 0.62 && darkDetail > 15)
        {
            return darkDetail > 34 && chromaDifference < 82
                ? BlemishCandidateKind.DarkSpot
                : BlemishCandidateKind.Freckle;
        }

        if (colorScore > 0.35)
        {
            return chromaDifference > 78
                ? BlemishCandidateKind.SmallBlob
                : BlemishCandidateKind.Freckle;
        }

        return BlemishCandidateKind.SmallBlob;
    }

    private static BlemishCandidateKind ResolveCandidateKind(IReadOnlyList<int> pixels, BlemishCandidateKind[] candidateKindMap)
    {
        Dictionary<BlemishCandidateKind, int> counts = new();
        foreach (int index in pixels)
        {
            BlemishCandidateKind kind = candidateKindMap[index];
            if (kind == BlemishCandidateKind.None)
            {
                continue;
            }

            counts[kind] = counts.TryGetValue(kind, out int count) ? count + 1 : 1;
        }

        return counts.Count == 0
            ? BlemishCandidateKind.SmallBlob
            : counts.OrderByDescending(pair => pair.Value).First().Key;
    }

    private static List<BlemishCandidate> ExtractFallbackColorPointCandidates(
        byte[] originalPixels,
        SkinAverage skinAverage,
        MaskPlane candidatePreview,
        MaskPlane searchMask,
        BlemishReduceInput input,
        int width,
        int height,
        double scale,
        List<string> warnings)
    {
        int stride = width * 4;
        List<(int X, int Y, double Score, BlemishCandidateKind Kind)> hotSpots = new();
        double averageRedExcess = GetRedExcess(skinAverage.Red, skinAverage.Green, skinAverage.Blue);
        for (int y = 1; y < height - 1; y += 2)
        {
            for (int x = 1; x < width - 1; x += 2)
            {
                double mask = searchMask[x, y];
                if (mask < 0.20 || input.HardProtectMask[x, y] > 0.05)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                double blue = originalPixels[index];
                double green = originalPixels[index + 1];
                double red = originalPixels[index + 2];
                double redDetail = GetRedExcess(red, green, blue) - averageRedExcess * 0.52;
                double colorDistance = GetColorDistance(red, green, blue, skinAverage.Red, skinAverage.Green, skinAverage.Blue);
                double redScore = SmoothStep(10, 42, redDetail);
                double colorScore = SmoothStep(30, 118, colorDistance);
                double score = Math.Max(redScore * 1.16, colorScore * 0.72) * mask;
                if (score < 0.34)
                {
                    continue;
                }

                BlemishCandidateKind kind = redScore >= colorScore * 0.55
                    ? BlemishCandidateKind.Redness
                    : BlemishCandidateKind.SmallBlob;
                hotSpots.Add((x, y, score, kind));
            }
        }

        if (hotSpots.Count == 0)
        {
            return new List<BlemishCandidate>();
        }

        List<BlemishCandidate> candidates = new();
        int minDistance = Math.Max(6, (int)Math.Round(9 * scale));
        int maxCandidates = input.StagePreset.BlemishReduceAmount >= 0.66 ? 260 : 100;
        foreach ((int x, int y, double score, BlemishCandidateKind kind) in hotSpots.OrderByDescending(item => item.Score))
        {
            if (candidates.Count >= maxCandidates)
            {
                break;
            }

            if (candidates.Any(candidate =>
            {
                double dx = candidate.CenterX - x;
                double dy = candidate.CenterY - y;
                return dx * dx + dy * dy < minDistance * minDistance;
            }))
            {
                continue;
            }

            int radius = Math.Clamp((int)Math.Round((4 + score * 11) * scale), 3, Math.Max(5, (int)Math.Round(18 * scale)));
            List<int> pixels = new();
            int minX = width;
            int minY = height;
            int maxX = 0;
            int maxY = 0;
            for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
            {
                for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
                {
                    double dx = xx - x;
                    double dy = yy - y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > radius || input.HardProtectMask[xx, yy] > 0.05 || searchMask[xx, yy] < 0.14)
                    {
                        continue;
                    }

                    int pixelIndex = yy * width + xx;
                    pixels.Add(pixelIndex);
                    minX = Math.Min(minX, xx);
                    minY = Math.Min(minY, yy);
                    maxX = Math.Max(maxX, xx);
                    maxY = Math.Max(maxY, yy);
                    candidatePreview[xx, yy] = Math.Max(candidatePreview[xx, yy], score * (1 - distance / (radius + 1d)));
                }
            }

            if (pixels.Count == 0)
            {
                continue;
            }

            candidates.Add(new BlemishCandidate(
                pixels.ToArray(),
                minX,
                minY,
                maxX,
                maxY,
                x,
                y,
                Math.Clamp(score, 0.20, 1),
                false,
                kind));
        }

        if (candidates.Count > 0)
        {
            warnings.Add("blemish_fallback_color_point_candidates");
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
            int candidateFeatherRadius = GetDynamicBrushRadius(candidate, featherRadius, width, height);
            int centerX = (int)Math.Round(candidate.CenterX);
            int centerY = (int)Math.Round(candidate.CenterY);
            int left = Math.Max(0, centerX - candidateFeatherRadius);
            int right = Math.Min(width - 1, centerX + candidateFeatherRadius);
            int top = Math.Max(0, centerY - candidateFeatherRadius);
            int bottom = Math.Min(height - 1, centerY + candidateFeatherRadius);
            double coreRadius = Math.Max(1, Math.Max(candidate.MaxX - candidate.MinX + 1, candidate.MaxY - candidate.MinY + 1) * 0.46);
            for (int targetY = top; targetY <= bottom; targetY++)
            {
                for (int targetX = left; targetX <= right; targetX++)
                {
                    double dx = targetX - candidate.CenterX;
                    double dy = targetY - candidate.CenterY;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance > candidateFeatherRadius + 0.001)
                    {
                        continue;
                    }

                    double amount = distance <= coreRadius
                        ? 1
                        : 1 - (distance - coreRadius) / Math.Max(1, candidateFeatherRadius - coreRadius + 1);
                    mask[targetX, targetY] = Math.Max(mask[targetX, targetY], Math.Clamp(amount, 0, 1) * candidate.Score);
                }
            }
        }

        return mask;
    }

    private static int GetDynamicBrushRadius(BlemishCandidate candidate, int baseFeatherRadius, int width, int height)
    {
        int boxWidth = candidate.MaxX - candidate.MinX + 1;
        int boxHeight = candidate.MaxY - candidate.MinY + 1;
        double area = Math.Max(1, candidate.PixelIndexes.Length);
        double scale = Math.Max(width, height) / 1200d;
        double areaBrushFactor = SmoothStep(12 * scale * scale, 650 * scale * scale, area);
        double multiplier = 1.15 + areaBrushFactor * (8.0 - 1.15);
        int radius = (int)Math.Round((Math.Max(boxWidth, boxHeight) * 0.32 + baseFeatherRadius) * multiplier);
        int maxRadius = Math.Min(54, (int)Math.Round(Math.Max(width, height) * 0.042));
        return Math.Clamp(radius, Math.Max(1, baseFeatherRadius), Math.Max(baseFeatherRadius, maxRadius));
    }

    private static void ApplySkinCleanColorSettle(
        byte[] originalPixels,
        byte[] currentPixels,
        byte[] correctedPixels,
        BlemishReduceInput input,
        MaskPlane candidateMask,
        MaskPlane workMask,
        MaskPlane blemishMask,
        BlemishCandidate candidate,
        ColorSample target,
        SkinAverage skinAverage,
        double candidateStrength,
        double softProtectScale,
        int baseFeatherRadius,
        int width,
        int height,
        int stride)
    {
        if (target.Weight <= 0)
        {
            return;
        }

        int radius = GetDynamicBrushRadius(candidate, baseFeatherRadius, width, height);
        int left = Math.Max(0, candidate.MinX - radius);
        int right = Math.Min(width - 1, candidate.MaxX + radius);
        int top = Math.Max(0, candidate.MinY - radius);
        int bottom = Math.Min(height - 1, candidate.MaxY + radius);
        double targetLuminance = GetLuminance(target.Red, target.Green, target.Blue);
        double kindScale = candidate.Kind switch
        {
            BlemishCandidateKind.Redness => 1.34,
            BlemishCandidateKind.SmallBlob => 1.20,
            BlemishCandidateKind.DarkSpot => 1.04,
            BlemishCandidateKind.Freckle => 0.92,
            BlemishCandidateKind.SkinMaskHole => 1.0,
            _ => 0.86
        };

        double settleStrength = Math.Clamp(candidateStrength * kindScale * 1.08, 0, 0.96);
        double averageRedExcess = GetRedExcess(skinAverage.Red, skinAverage.Green, skinAverage.Blue);
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                if (hardProtect >= 0.05)
                {
                    continue;
                }

                double workMaskValue = Math.Max(input.RetouchAllowMask[x, y], workMask[x, y]);
                if (candidate.IsSoftProtect)
                {
                    workMaskValue = Math.Max(workMaskValue, input.SoftProtectMask[x, y] * softProtectScale);
                }

                if (workMaskValue <= 0.001)
                {
                    continue;
                }

                double feather = candidateMask[x, y];
                if (feather <= 0.001)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                double red = originalPixels[index + 2];
                double green = originalPixels[index + 1];
                double blue = originalPixels[index];
                double redDetail = GetRedExcess(red, green, blue) - averageRedExcess;
                double colorDistance = GetColorDistance(red, green, blue, skinAverage.Red, skinAverage.Green, skinAverage.Blue);
                double localNeed = Math.Max(SmoothStep(4, 28, redDetail), SmoothStep(16, 72, colorDistance));
                double amount = Math.Clamp(settleStrength * workMaskValue * Math.Pow(feather, 0.38) * (0.55 + localNeed * 0.55) * (1 - hardProtect), 0, 0.96);
                if (amount <= 0.001)
                {
                    continue;
                }

                double sourceLuminance = GetLuminance(originalPixels[index + 2], originalPixels[index + 1], originalPixels[index]);
                double luminanceOffset = (sourceLuminance - targetLuminance) * 0.24;
                double targetBlue = Math.Clamp(target.Blue + luminanceOffset, 0, 255);
                double targetGreen = Math.Clamp(target.Green + luminanceOffset, 0, 255);
                double targetRed = Math.Clamp(target.Red + luminanceOffset, 0, 255);

                correctedPixels[index] = BlendChannel(correctedPixels[index], targetBlue, amount);
                correctedPixels[index + 1] = BlendChannel(correctedPixels[index + 1], targetGreen, amount);
                correctedPixels[index + 2] = BlendChannel(correctedPixels[index + 2], targetRed, amount);
                correctedPixels[index + 3] = currentPixels[index + 3];
                blemishMask[x, y] = Math.Max(blemishMask[x, y], amount);
            }
        }
    }

    private static MaskPlane BuildSearchMask(BlemishReduceInput input, int width, int height)
    {
        MaskPlane searchMask = MaskPlane.Empty(width, height);
        double softOpacity = Math.Clamp(input.StagePreset.BlemishSearchSoftProtectOpacity, 0, 0.25);
        double maskSum = 0;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                double value = Math.Clamp(input.RetouchAllowMask[x, y] + input.SoftProtectMask[x, y] * softOpacity, 0, 1);
                double searchValue = value * (1 - hardProtect);
                searchMask[x, y] = searchValue;
                maskSum += searchValue;
            }
        }

        if (maskSum < width * height * 0.002)
        {
            searchMask = BuildFallbackEggSearchMask(input, width, height);
        }

        return searchMask;
    }

    private static MaskPlane BuildFallbackEggSearchMask(BlemishReduceInput input, int width, int height)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        Int32Rect face = input.Snapshot.Analysis.FaceBox;
        if (face.Width <= 0 || face.Height <= 0)
        {
            return mask;
        }

        double centerX = face.X + face.Width * 0.5;
        double centerY = face.Y + face.Height * 0.52;
        double halfHeight = Math.Max(1, face.Height * 0.56);
        double maxHalfWidth = Math.Max(1, face.Width * 0.50);
        double angle = input.Snapshot.Analysis.FaceAngle;
        double cos = Math.Cos(-angle);
        double sin = Math.Sin(-angle);
        int left = Math.Max(0, face.X - (int)(face.Width * 0.08));
        int right = Math.Min(width - 1, face.X + (int)(face.Width * 1.08));
        int top = Math.Max(0, face.Y - (int)(face.Height * 0.12));
        int bottom = Math.Min(height - 1, face.Y + (int)(face.Height * 1.08));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x - centerX;
                double dy = y - centerY;
                double rx = dx * cos - dy * sin;
                double ry = dx * sin + dy * cos;
                double t = (ry + halfHeight) / (halfHeight * 2);
                if (t < 0 || t > 1)
                {
                    continue;
                }

                double profile = GetEggWidthProfile(t);
                double halfWidth = maxHalfWidth * profile;
                if (halfWidth <= 1)
                {
                    continue;
                }

                double distance = Math.Abs(rx) / halfWidth;
                double value = 1 - SmoothStep(0.88, 1.03, distance);
                if (value <= 0)
                {
                    continue;
                }

                double hardProtect = input.HardProtectMask[x, y];
                double softProtect = input.SoftProtectMask[x, y];
                mask[x, y] = Math.Clamp(value * (1 - hardProtect) * (1 - softProtect * 0.72), 0, 1);
            }
        }

        return mask;
    }

    private static double GetEggWidthProfile(double t)
    {
        (double T, double W)[] points =
        [
            (0.00, 0.12),
            (0.12, 0.62),
            (0.22, 0.88),
            (0.42, 1.00),
            (0.58, 0.92),
            (0.74, 0.76),
            (0.90, 0.50),
            (1.00, 0.18)
        ];

        for (int i = 0; i < points.Length - 1; i++)
        {
            if (t >= points[i].T && t <= points[i + 1].T)
            {
                double local = (t - points[i].T) / Math.Max(0.0001, points[i + 1].T - points[i].T);
                local = local * local * (3 - 2 * local);
                return points[i].W + (points[i + 1].W - points[i].W) * local;
            }
        }

        return 0;
    }

    private static ColorSample SampleSurroundingSkin(byte[] originalPixels, BlemishReduceInput input, BlemishCandidate candidate, int stride, int width, int height)
    {
        int componentWidth = candidate.MaxX - candidate.MinX + 1;
        int componentHeight = candidate.MaxY - candidate.MinY + 1;
        int radius = Math.Clamp(Math.Max(componentWidth, componentHeight) * 2 + 5, 5, 28);
        List<ColorSample> samples = new();

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
                samples.Add(new ColorSample(
                    originalPixels[index],
                    originalPixels[index + 1],
                    originalPixels[index + 2],
                    mask));
            }
        }

        if (samples.Count == 0)
        {
            MaskPlane fallbackMask = BuildFallbackEggSearchMask(input, width, height);
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

                    double mask = fallbackMask[x, y];
                    if (mask < 0.18)
                    {
                        continue;
                    }

                    int index = y * stride + x * 4;
                    samples.Add(new ColorSample(
                        originalPixels[index],
                        originalPixels[index + 1],
                        originalPixels[index + 2],
                        mask));
                }
            }
        }

        if (samples.Count == 0)
        {
            return new ColorSample(0, 0, 0, 0);
        }

        IReadOnlyList<ColorSample> cleanSamples = SelectCleanColorSamples(samples);
        double blueSum = 0;
        double greenSum = 0;
        double redSum = 0;
        double weightSum = 0;
        foreach (ColorSample sample in cleanSamples)
        {
            blueSum += sample.Blue * sample.Weight;
            greenSum += sample.Green * sample.Weight;
            redSum += sample.Red * sample.Weight;
            weightSum += sample.Weight;
        }

        if (weightSum <= 0)
        {
            return new ColorSample(0, 0, 0, 0);
        }

        return new ColorSample(blueSum / weightSum, greenSum / weightSum, redSum / weightSum, weightSum);
    }

    private static SkinAverage CalculateSkinAverage(byte[] pixels, int width, int height, int stride, MaskPlane searchMask)
    {
        List<(double R, double G, double B, double L)> samples = new();
        for (int y = 0; y < height; y += 2)
        {
            for (int x = 0; x < width; x += 2)
            {
                double mask = searchMask[x, y];
                if (mask < 0.45)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                double blue = pixels[index];
                double green = pixels[index + 1];
                double red = pixels[index + 2];
                samples.Add((red, green, blue, GetLuminance(red, green, blue)));
            }
        }

        if (samples.Count == 0)
        {
            return new SkinAverage(210, 170, 148, 0);
        }

        double[] luminance = samples.Select(sample => sample.L).OrderBy(value => value).ToArray();
        double low = luminance[(int)Math.Clamp(luminance.Length * 0.12, 0, luminance.Length - 1)];
        double high = luminance[(int)Math.Clamp(luminance.Length * 0.88, 0, luminance.Length - 1)];
        var clean = samples.Where(sample => sample.L >= low && sample.L <= high).ToArray();
        if (clean.Length == 0)
        {
            clean = samples.ToArray();
        }

        return new SkinAverage(
            clean.Average(sample => sample.R),
            clean.Average(sample => sample.G),
            clean.Average(sample => sample.B),
            clean.Length);
    }

    private static IReadOnlyList<ColorSample> SelectCleanColorSamples(IReadOnlyList<ColorSample> samples)
    {
        if (samples.Count < 12)
        {
            return samples;
        }

        double[] luminanceValues = samples
            .Select(sample => GetLuminance(sample.Red, sample.Green, sample.Blue))
            .OrderBy(value => value)
            .ToArray();
        double[] redExcessValues = samples
            .Select(sample => GetRedExcess(sample.Red, sample.Green, sample.Blue))
            .OrderBy(value => value)
            .ToArray();

        double medianLuminance = luminanceValues[luminanceValues.Length / 2];
        double medianRedExcess = redExcessValues[redExcessValues.Length / 2];
        double darkLimit = medianLuminance - 20;
        double redLimit = medianRedExcess + 10;

        ColorSample[] clean = samples
            .Where(sample =>
            {
                double luminance = GetLuminance(sample.Red, sample.Green, sample.Blue);
                double redExcess = GetRedExcess(sample.Red, sample.Green, sample.Blue);
                return luminance >= darkLimit && redExcess <= redLimit;
            })
            .ToArray();

        return clean.Length >= Math.Max(6, samples.Count / 4) ? clean : samples;
    }

    private static double GetLuminance(double red, double green, double blue)
    {
        return red * 0.299 + green * 0.587 + blue * 0.114;
    }

    private static double GetRedExcess(double red, double green, double blue)
    {
        return red - (green + blue) * 0.5;
    }

    private static double GetColorDistance(double red, double green, double blue, double targetRed, double targetGreen, double targetBlue)
    {
        double dr = red - targetRed;
        double dg = green - targetGreen;
        double db = blue - targetBlue;
        return Math.Sqrt(dr * dr + dg * dg + db * db);
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

    private static double GetCandidateKindStrengthScale(BlemishCandidateKind kind, bool isMaximumBlemishReduce)
    {
        if (isMaximumBlemishReduce)
        {
            return kind switch
            {
                BlemishCandidateKind.Redness => 1.06,
                BlemishCandidateKind.Freckle => 0.96,
                BlemishCandidateKind.DarkSpot => 1.16,
                BlemishCandidateKind.SmallBlob => 1.08,
                BlemishCandidateKind.SkinMaskHole => 1.2,
                _ => 1.0
            };
        }

        return kind switch
        {
            BlemishCandidateKind.Redness => 0.78,
            BlemishCandidateKind.Freckle => 0.68,
            BlemishCandidateKind.DarkSpot => 1.06,
            BlemishCandidateKind.SmallBlob => 0.94,
            BlemishCandidateKind.SkinMaskHole => 1.12,
            _ => 0.88
        };
    }

    private static ColorSample AdjustMaximumBlemishTarget(ColorSample target, BlemishCandidateKind kind)
    {
        if (target.Weight <= 0)
        {
            return target;
        }

        double blue = target.Blue;
        double green = target.Green;
        double red = target.Red;
        switch (kind)
        {
            case BlemishCandidateKind.Redness:
            case BlemishCandidateKind.SmallBlob:
                red = Math.Min(red, (green + blue) * 0.5 + 14);
                green = green * 0.985 + red * 0.015;
                break;
            case BlemishCandidateKind.DarkSpot:
            case BlemishCandidateKind.Freckle:
            case BlemishCandidateKind.SkinMaskHole:
                double luminance = GetLuminance(red, green, blue);
                double lift = Math.Clamp(156 - luminance, 0, 18);
                red += lift * 0.78;
                green += lift * 0.86;
                blue += lift * 0.72;
                break;
        }

        return new ColorSample(
            Math.Clamp(blue, 0, 255),
            Math.Clamp(green, 0, 255),
            Math.Clamp(red, 0, 255),
            target.Weight);
    }

    private static ColorSample BlendTargetTowardSkinAverage(ColorSample target, SkinAverage skinAverage, BlemishCandidateKind kind)
    {
        if (target.Weight <= 0 || skinAverage.Weight <= 0)
        {
            return target;
        }

        double amount = kind switch
        {
            BlemishCandidateKind.Redness => 0.42,
            BlemishCandidateKind.SmallBlob => 0.34,
            BlemishCandidateKind.DarkSpot => 0.26,
            BlemishCandidateKind.Freckle => 0.22,
            BlemishCandidateKind.SkinMaskHole => 0.18,
            _ => 0.24
        };
        double targetLuminance = GetLuminance(target.Red, target.Green, target.Blue);
        double averageLuminance = GetLuminance(skinAverage.Red, skinAverage.Green, skinAverage.Blue);
        double luminanceOffset = (targetLuminance - averageLuminance) * 0.42;
        double averageRed = Math.Clamp(skinAverage.Red + luminanceOffset, 0, 255);
        double averageGreen = Math.Clamp(skinAverage.Green + luminanceOffset, 0, 255);
        double averageBlue = Math.Clamp(skinAverage.Blue + luminanceOffset, 0, 255);

        return new ColorSample(
            target.Blue + (averageBlue - target.Blue) * amount,
            target.Green + (averageGreen - target.Green) * amount,
            target.Red + (averageRed - target.Red) * amount,
            target.Weight);
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
        MaskPlane WorkMask,
        SkinAverage SkinAverage,
        IReadOnlyList<BlemishCandidate> Candidates,
        IReadOnlyList<BlemishCandidatePoint> CandidatePoints,
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
        bool IsSoftProtect,
        BlemishCandidateKind Kind);

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

    private sealed record SkinAverage(double Red, double Green, double Blue, int Weight);

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
    IReadOnlyList<string> DebugWarnings,
    IReadOnlyList<BlemishCandidatePoint> CandidatePoints);

public sealed record BlemishCandidatePoint(
    BlemishCandidateKind Kind,
    double CenterX,
    double CenterY,
    int MinX,
    int MinY,
    int MaxX,
    int MaxY,
    double Score,
    bool IsSoftProtect);

public enum BlemishCandidateKind
{
    None,
    SkinMaskHole,
    SmallBlob,
    Redness,
    DarkSpot,
    Freckle
}
