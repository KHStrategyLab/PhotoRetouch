using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class WrinkleSoftReduceFilter : IWrinkleSoftReduceFilter
{
    private readonly Dictionary<string, WrinkleAnalysisCache> _analysisCache = new();

    public WrinkleSoftReduceResult Apply(WrinkleSoftReduceInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        WrinkleToolset toolset = input.Toolset ?? WrinkleToolset.FromStagePreset(input.StagePreset);
        if (!toolset.EnableWrinkleReduce || toolset.GlobalWrinkleAmount <= 0)
        {
            WrinkleMaskSet emptyMasks = WrinkleMaskSet.Empty(input.OriginalImage.PixelWidth, input.OriginalImage.PixelHeight);
            WrinkleProcessReport emptyReport = WrinkleProcessReport.Empty(Array.Empty<string>());
            return new WrinkleSoftReduceResult(input.CurrentRetouchedImage, emptyMasks, emptyMasks.CombinedWrinkleMask, emptyMasks.CombinedWrinkleMask, emptyReport, emptyReport.DebugWarnings);
        }

        int width = input.OriginalImage.PixelWidth;
        int height = input.OriginalImage.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = CopyPixels(input.OriginalImage);
        byte[] currentPixels = CopyPixels(input.CurrentRetouchedImage);
        byte[] correctedPixels = (byte[])currentPixels.Clone();
        WrinkleAnalysisCache analysis = GetOrCreateAnalysis(input, originalPixels, width, height);
        MaskPlane appliedMask = MaskPlane.Empty(width, height);

        double qualityScale = GetQualityScale(input.MaskQualityReport);
        double globalStrength = Math.Clamp(input.StagePreset.WrinkleReduceAmount * toolset.GlobalWrinkleAmount * qualityScale, 0, 0.72);
        int appliedCount = 0;
        double strengthSum = 0;

        foreach (WrinkleCandidate candidate in analysis.Candidates)
        {
            double partAmount = toolset.GetPartAmount(candidate.Part);
            if (partAmount <= 0)
            {
                continue;
            }

            double structuralCap = GetStructuralCap(candidate.Part);
            double candidateStrength = Math.Clamp(globalStrength * partAmount * candidate.Score * structuralCap, 0, 0.46);
            if (candidateStrength < 0.012)
            {
                continue;
            }

            ColorSample target = SampleSurroundingSkin(originalPixels, input, candidate, stride, width, height);
            if (target.Weight <= 0)
            {
                continue;
            }

            double structureKeep = Math.Clamp(input.StagePreset.WrinkleStructureKeepAmount, 0.45, 0.96);
            for (int y = candidate.MinY; y <= candidate.MaxY; y++)
            {
                for (int x = candidate.MinX; x <= candidate.MaxX; x++)
                {
                    double candidateMask = analysis.MaskSet.GetMask(candidate.Part)[x, y];
                    if (candidateMask <= 0)
                    {
                        continue;
                    }

                    double hardProtect = input.HardProtectMask[x, y];
                    if (hardProtect >= 0.04)
                    {
                        continue;
                    }

                    int index = y * stride + x * 4;
                    double originalLuminance = GetLuminance(originalPixels, index);
                    double targetLuminance = target.Red * 0.299 + target.Green * 0.587 + target.Blue * 0.114;
                    double darkOnly = SmoothStep(1, 18, targetLuminance - originalLuminance);
                    if (darkOnly <= 0)
                    {
                        continue;
                    }

                    double maskWeight = Math.Clamp(input.SoftProtectMask[x, y] * input.StagePreset.WrinkleSoftProtectOpacity + input.RetouchAllowMask[x, y] * 0.28, 0, 1);
                    double amount = Math.Clamp(candidateStrength * candidateMask * maskWeight * darkOnly * (1 - hardProtect) * (1 - structureKeep * 0.38), 0, 0.42);
                    if (amount <= 0)
                    {
                        continue;
                    }

                    correctedPixels[index] = BlendChannel(correctedPixels[index], target.Blue, amount);
                    correctedPixels[index + 1] = BlendChannel(correctedPixels[index + 1], target.Green, amount);
                    correctedPixels[index + 2] = BlendChannel(correctedPixels[index + 2], target.Red, amount);
                    correctedPixels[index + 3] = currentPixels[index + 3];
                    appliedMask[x, y] = Math.Max(appliedMask[x, y], amount);
                }
            }

            appliedCount++;
            strengthSum += candidateStrength;
        }

        RestoreHardProtect(originalPixels, correctedPixels, input.HardProtectMask, width, height);
        WrinkleProcessReport report = CreateReport(
            analysis.Candidates,
            appliedCount,
            Math.Max(0, analysis.Candidates.Count - appliedCount),
            globalStrength,
            appliedCount == 0 ? 0 : strengthSum / appliedCount,
            analysis.DebugWarnings);
        return new WrinkleSoftReduceResult(
            CreateBitmap(width, height, correctedPixels),
            analysis.MaskSet,
            analysis.CandidateMask,
            appliedMask,
            report,
            analysis.DebugWarnings);
    }

    private WrinkleAnalysisCache GetOrCreateAnalysis(WrinkleSoftReduceInput input, byte[] originalPixels, int width, int height)
    {
        string cacheKey = input.Snapshot.CacheKey.StableId + "|wrinkle_v1";
        if (_analysisCache.TryGetValue(cacheKey, out WrinkleAnalysisCache? cached))
        {
            return cached;
        }

        WrinkleAnalysisCache created = AnalyzeWrinkles(input, originalPixels, width, height);
        _analysisCache[cacheKey] = created;
        return created;
    }

    private static WrinkleProcessReport CreateReport(
        IReadOnlyList<WrinkleCandidate> candidates,
        int appliedCount,
        int skippedCount,
        double globalWrinkleAmount,
        double averageCorrectionStrength,
        IReadOnlyList<string> warnings)
    {
        return new WrinkleProcessReport(
            candidates.Count(candidate => candidate.Part == WrinklePart.UnderEye),
            candidates.Count(candidate => candidate.Part == WrinklePart.Glabella),
            candidates.Count(candidate => candidate.Part == WrinklePart.Forehead),
            candidates.Count(candidate => candidate.Part == WrinklePart.Nasolabial),
            candidates.Count(candidate => candidate.Part == WrinklePart.MouthCorner),
            candidates.Count(candidate => candidate.Part == WrinklePart.Neck),
            candidates.Count(candidate => candidate.Part == WrinklePart.NoseShadow),
            appliedCount,
            skippedCount,
            globalWrinkleAmount,
            averageCorrectionStrength,
            warnings);
    }

    private static WrinkleAnalysisCache AnalyzeWrinkles(WrinkleSoftReduceInput input, byte[] originalPixels, int width, int height)
    {
        int stride = width * 4;
        byte[] localAverage = FastBoxBlur(originalPixels, width, height, GetAnalysisBlurRadius(width, height));
        MaskPlane searchMask = BuildSearchMask(input, width, height);
        MaskPlane candidatePreview = MaskPlane.Empty(width, height);
        bool[] candidateMap = new bool[width * height];
        List<string> warnings = new() { "wrinkle_soft_reduce_filter_v1" };
        double contrastThreshold = 13;

        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                int pixelIndex = y * width + x;
                double mask = searchMask.Values[pixelIndex];
                if (mask < 0.10)
                {
                    continue;
                }

                int index = pixelIndex * 4;
                double sourceLum = GetLuminance(originalPixels, index);
                double averageLum = GetLuminance(localAverage, index);
                double darkLine = averageLum - sourceLum;
                double gradient = CalculateLocalGradient(originalPixels, width, height, x, y);
                double score = SmoothStep(contrastThreshold, contrastThreshold + 26, darkLine) *
                               SmoothStep(6, 30, gradient) *
                               (1 - SmoothStep(110, 170, gradient)) *
                               mask;
                if (score > 0.22)
                {
                    candidateMap[pixelIndex] = true;
                    candidatePreview[x, y] = score;
                }
            }
        }

        List<WrinkleCandidate> candidates = ExtractCandidates(candidateMap, candidatePreview, input, width, height);
        WrinkleMaskSet maskSet = BuildMaskSet(candidates, width, height, GetFeatherRadius(width, height));
        MaskPlane combined = maskSet.CombinedWrinkleMask;
        return new WrinkleAnalysisCache(input.Snapshot.CacheKey.StableId, combined, maskSet, candidates, warnings);
    }

    private static List<WrinkleCandidate> ExtractCandidates(bool[] candidateMap, MaskPlane candidatePreview, WrinkleSoftReduceInput input, int width, int height)
    {
        bool[] visited = new bool[candidateMap.Length];
        List<WrinkleCandidate> candidates = new();
        double scale = Math.Max(width, height) / 1200d;
        int minArea = Math.Max(3, (int)Math.Round(3 * scale * scale));
        int maxArea = Math.Max(18, (int)Math.Round(620 * scale * scale));
        int hardRadius = Math.Max(2, (int)Math.Round(5 * scale));

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

            WrinkleComponentStats stats = CalculateStats(pixels, width, candidatePreview);
            double longSide = Math.Max(stats.Width, stats.Height);
            double shortSide = Math.Max(1, Math.Min(stats.Width, stats.Height));
            double aspect = longSide / shortSide;
            if (longSide < Math.Max(6, 7 * scale) || aspect < 1.75)
            {
                continue;
            }

            if (shortSide > Math.Max(4, 7 * scale))
            {
                continue;
            }

            if (IsNearHardProtect(input.HardProtectMask, width, height, stats.CenterX, stats.CenterY, hardRadius))
            {
                continue;
            }

            WrinklePart part = ClassifyPart(input, stats);
            if (part == WrinklePart.None)
            {
                continue;
            }

            if (part == WrinklePart.Glabella && stats.Height < stats.Width * 1.15)
            {
                continue;
            }

            if (part == WrinklePart.Forehead && stats.Width < stats.Height * 1.15)
            {
                continue;
            }

            double compactness = pixels.Count / Math.Max(1d, stats.Width * stats.Height);
            if (compactness < 0.10)
            {
                continue;
            }

            candidates.Add(new WrinkleCandidate(
                pixels.ToArray(),
                stats.MinX,
                stats.MinY,
                stats.MaxX,
                stats.MaxY,
                stats.CenterX,
                stats.CenterY,
                Math.Clamp(stats.ScoreAverage * (0.78 + Math.Min(aspect, 5) * 0.045), 0.15, 1),
                part));
        }

        return candidates;
    }

    private static WrinklePart ClassifyPart(WrinkleSoftReduceInput input, WrinkleComponentStats stats)
    {
        Int32Rect faceBox = input.Snapshot.Analysis.FaceBox;
        WpfPoint leftEye = GetLandmark(input, "left_eye", new WpfPoint(faceBox.X + faceBox.Width * 0.35, faceBox.Y + faceBox.Height * 0.38));
        WpfPoint rightEye = GetLandmark(input, "right_eye", new WpfPoint(faceBox.X + faceBox.Width * 0.65, faceBox.Y + faceBox.Height * 0.38));
        WpfPoint noseTip = GetLandmark(input, "nose_tip", new WpfPoint(faceBox.X + faceBox.Width * 0.50, faceBox.Y + faceBox.Height * 0.56));
        WpfPoint mouth = GetLandmark(input, "mouth_center", new WpfPoint(faceBox.X + faceBox.Width * 0.50, faceBox.Y + faceBox.Height * 0.72));

        double nx = (stats.CenterX - faceBox.X) / Math.Max(1, faceBox.Width);
        double ny = (stats.CenterY - faceBox.Y) / Math.Max(1, faceBox.Height);
        double eyeY = ((leftEye.Y + rightEye.Y) / 2d - faceBox.Y) / Math.Max(1, faceBox.Height);
        double noseY = (noseTip.Y - faceBox.Y) / Math.Max(1, faceBox.Height);
        double mouthY = (mouth.Y - faceBox.Y) / Math.Max(1, faceBox.Height);
        double centerDistance = Math.Abs(nx - 0.5);

        if (ny < eyeY - 0.10 && nx > 0.18 && nx < 0.82)
        {
            return WrinklePart.Forehead;
        }

        if (ny >= eyeY - 0.12 && ny <= noseY - 0.04 && centerDistance < 0.12)
        {
            return WrinklePart.Glabella;
        }

        if (ny >= eyeY + 0.02 && ny <= noseY + 0.02 && (Math.Abs(nx - 0.34) < 0.16 || Math.Abs(nx - 0.66) < 0.16))
        {
            return WrinklePart.UnderEye;
        }

        if (ny >= noseY - 0.04 && ny <= mouthY + 0.12 && centerDistance >= 0.12 && centerDistance <= 0.34)
        {
            return WrinklePart.Nasolabial;
        }

        if (ny >= mouthY - 0.08 && ny <= mouthY + 0.18 && centerDistance >= 0.20 && centerDistance <= 0.42)
        {
            return WrinklePart.MouthCorner;
        }

        if (ny >= noseY - 0.10 && ny <= mouthY + 0.02 && centerDistance < 0.16)
        {
            return WrinklePart.NoseShadow;
        }

        if (ny > 0.86)
        {
            return WrinklePart.Neck;
        }

        return WrinklePart.None;
    }

    private static WpfPoint GetLandmark(WrinkleSoftReduceInput input, string key, WpfPoint fallback)
    {
        return input.Snapshot.Analysis.FaceLandmarks.TryGetValue(key, out WpfPoint point)
            ? point
            : fallback;
    }

    private static WrinkleMaskSet BuildMaskSet(IReadOnlyList<WrinkleCandidate> candidates, int width, int height, int featherRadius)
    {
        MaskPlane underEye = MaskPlane.Empty(width, height);
        MaskPlane glabella = MaskPlane.Empty(width, height);
        MaskPlane forehead = MaskPlane.Empty(width, height);
        MaskPlane nasolabial = MaskPlane.Empty(width, height);
        MaskPlane mouthCorner = MaskPlane.Empty(width, height);
        MaskPlane neck = MaskPlane.Empty(width, height);
        MaskPlane noseShadow = MaskPlane.Empty(width, height);

        foreach (WrinkleCandidate candidate in candidates)
        {
            MaskPlane target = candidate.Part switch
            {
                WrinklePart.UnderEye => underEye,
                WrinklePart.Glabella => glabella,
                WrinklePart.Forehead => forehead,
                WrinklePart.Nasolabial => nasolabial,
                WrinklePart.MouthCorner => mouthCorner,
                WrinklePart.Neck => neck,
                WrinklePart.NoseShadow => noseShadow,
                _ => underEye
            };
            PaintCandidate(target, candidate, width, height, featherRadius);
        }

        MaskPlane combined = MaskPlane.Union(underEye, glabella, forehead, nasolabial, mouthCorner, neck, noseShadow);
        return new WrinkleMaskSet(underEye, glabella, forehead, nasolabial, mouthCorner, neck, noseShadow, combined);
    }

    private static void PaintCandidate(MaskPlane mask, WrinkleCandidate candidate, int width, int height, int featherRadius)
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

    private static MaskPlane BuildSearchMask(WrinkleSoftReduceInput input, int width, int height)
    {
        MaskPlane searchMask = MaskPlane.Empty(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = input.HardProtectMask[x, y];
                double value = Math.Clamp(input.SoftProtectMask[x, y] * 0.92 + input.RetouchAllowMask[x, y] * 0.32, 0, 1);
                searchMask[x, y] = value * (1 - hardProtect);
            }
        }

        return searchMask;
    }

    private static ColorSample SampleSurroundingSkin(byte[] originalPixels, WrinkleSoftReduceInput input, WrinkleCandidate candidate, int stride, int width, int height)
    {
        int radius = Math.Clamp(Math.Max(candidate.MaxX - candidate.MinX, candidate.MaxY - candidate.MinY) + 8, 8, 42);
        double blueSum = 0;
        double greenSum = 0;
        double redSum = 0;
        double weightSum = 0;
        for (int y = Math.Max(0, candidate.MinY - radius); y <= Math.Min(height - 1, candidate.MaxY + radius); y++)
        {
            for (int x = Math.Max(0, candidate.MinX - radius); x <= Math.Min(width - 1, candidate.MaxX + radius); x++)
            {
                if (x >= candidate.MinX - 2 && x <= candidate.MaxX + 2 && y >= candidate.MinY - 2 && y <= candidate.MaxY + 2)
                {
                    continue;
                }

                double mask = Math.Clamp(input.RetouchAllowMask[x, y] + input.SoftProtectMask[x, y] * 0.25, 0, 1) * (1 - input.HardProtectMask[x, y]);
                if (mask < 0.12)
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

        return weightSum <= 0
            ? new ColorSample(0, 0, 0, 0)
            : new ColorSample(blueSum / weightSum, greenSum / weightSum, redSum / weightSum, weightSum);
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

    private static WrinkleComponentStats CalculateStats(List<int> pixels, int width, MaskPlane candidatePreview)
    {
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = 0;
        int maxY = 0;
        double xSum = 0;
        double ySum = 0;
        double scoreSum = 0;
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
        }

        double count = Math.Max(1, pixels.Count);
        return new WrinkleComponentStats(minX, minY, maxX, maxY, xSum / count, ySum / count, Math.Max(1, maxX - minX + 1), Math.Max(1, maxY - minY + 1), scoreSum / count);
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

    private static double GetStructuralCap(WrinklePart part)
    {
        return part switch
        {
            WrinklePart.Nasolabial => 0.62,
            WrinklePart.NoseShadow => 0.52,
            WrinklePart.UnderEye => 0.68,
            WrinklePart.Glabella => 0.72,
            WrinklePart.MouthCorner => 0.70,
            WrinklePart.Neck => 0.74,
            _ => 0.82
        };
    }

    private static double GetQualityScale(MaskQualityReport report)
    {
        double scale = 1;
        if (!report.IsUsable)
        {
            scale *= 0.52;
        }

        if (report.EyeMaskQualityScore < 0.55 || report.EyebrowMaskQualityScore < 0.55)
        {
            scale *= 0.72;
        }

        if (report.LipMaskQualityScore < 0.55 || report.NostrilMaskQualityScore < 0.55)
        {
            scale *= 0.78;
        }

        if (report.SkinMaskQualityScore < 0.55 || report.RetouchAllowQualityScore < 0.55)
        {
            scale *= 0.70;
        }

        return Math.Clamp(scale, 0.24, 1);
    }

    private static int GetAnalysisBlurRadius(int width, int height)
    {
        return Math.Clamp((int)Math.Round(Math.Max(width, height) / 1200d * 5), 3, 15);
    }

    private static int GetFeatherRadius(int width, int height)
    {
        return Math.Clamp((int)Math.Round(Math.Max(width, height) / 1200d * 2), 1, 5);
    }

    private static byte[] FastBoxBlur(byte[] sourcePixels, int width, int height, int radius)
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

    private static double CalculateLocalGradient(byte[] pixels, int width, int height, int x, int y)
    {
        int stride = width * 4;
        double left = GetLuminance(pixels, y * stride + Math.Max(0, x - 1) * 4);
        double right = GetLuminance(pixels, y * stride + Math.Min(width - 1, x + 1) * 4);
        double top = GetLuminance(pixels, Math.Max(0, y - 1) * stride + x * 4);
        double bottom = GetLuminance(pixels, Math.Min(height - 1, y + 1) * stride + x * 4);
        return Math.Abs(right - left) + Math.Abs(bottom - top);
    }

    private static double GetLuminance(byte[] pixels, int index)
    {
        return pixels[index + 2] * 0.299 + pixels[index + 1] * 0.587 + pixels[index] * 0.114;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
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

    private sealed record WrinkleAnalysisCache(
        string SnapshotStableId,
        MaskPlane CandidateMask,
        WrinkleMaskSet MaskSet,
        IReadOnlyList<WrinkleCandidate> Candidates,
        IReadOnlyList<string> DebugWarnings);

    private sealed record WrinkleCandidate(
        int[] PixelIndexes,
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double CenterX,
        double CenterY,
        double Score,
        WrinklePart Part);

    private sealed record WrinkleComponentStats(
        int MinX,
        int MinY,
        int MaxX,
        int MaxY,
        double CenterX,
        double CenterY,
        int Width,
        int Height,
        double ScoreAverage);

    private sealed record ColorSample(double Blue, double Green, double Red, double Weight);
}

public interface IWrinkleSoftReduceFilter
{
    WrinkleSoftReduceResult Apply(WrinkleSoftReduceInput input);
}

public enum WrinklePart
{
    None,
    UnderEye,
    Glabella,
    Forehead,
    Nasolabial,
    MouthCorner,
    Neck,
    NoseShadow
}

public sealed record WrinkleToolset(
    bool EnableWrinkleReduce,
    double GlobalWrinkleAmount,
    double UnderEyeWrinkleAmount,
    double GlabellaWrinkleAmount,
    double ForeheadWrinkleAmount,
    double NasolabialFoldAmount,
    double MouthCornerWrinkleAmount,
    double NeckWrinkleAmount,
    double NoseShadowWrinkleAmount)
{
    public static WrinkleToolset FromStagePreset(StagePreset preset)
    {
        return new WrinkleToolset(
            true,
            1,
            preset.UnderEyeWrinkleDefault,
            preset.GlabellaWrinkleDefault,
            preset.ForeheadWrinkleDefault,
            preset.NasolabialFoldDefault,
            preset.MouthCornerWrinkleDefault,
            preset.NeckWrinkleDefault,
            preset.NoseShadowWrinkleDefault);
    }

    public double GetPartAmount(WrinklePart part)
    {
        return part switch
        {
            WrinklePart.UnderEye => UnderEyeWrinkleAmount,
            WrinklePart.Glabella => GlabellaWrinkleAmount,
            WrinklePart.Forehead => ForeheadWrinkleAmount,
            WrinklePart.Nasolabial => NasolabialFoldAmount,
            WrinklePart.MouthCorner => MouthCornerWrinkleAmount,
            WrinklePart.Neck => NeckWrinkleAmount,
            WrinklePart.NoseShadow => NoseShadowWrinkleAmount,
            _ => 0
        };
    }
}

public sealed record WrinkleMaskSet(
    MaskPlane UnderEyeWrinkleMask,
    MaskPlane GlabellaWrinkleMask,
    MaskPlane ForeheadWrinkleMask,
    MaskPlane NasolabialFoldMask,
    MaskPlane MouthCornerWrinkleMask,
    MaskPlane NeckWrinkleMask,
    MaskPlane NoseShadowWrinkleMask,
    MaskPlane CombinedWrinkleMask)
{
    public static WrinkleMaskSet Empty(int width, int height)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        return new WrinkleMaskSet(empty, empty, empty, empty, empty, empty, empty, empty);
    }

    public MaskPlane GetMask(WrinklePart part)
    {
        return part switch
        {
            WrinklePart.UnderEye => UnderEyeWrinkleMask,
            WrinklePart.Glabella => GlabellaWrinkleMask,
            WrinklePart.Forehead => ForeheadWrinkleMask,
            WrinklePart.Nasolabial => NasolabialFoldMask,
            WrinklePart.MouthCorner => MouthCornerWrinkleMask,
            WrinklePart.Neck => NeckWrinkleMask,
            WrinklePart.NoseShadow => NoseShadowWrinkleMask,
            _ => CombinedWrinkleMask
        };
    }
}

public sealed record WrinkleSoftReduceInput(
    BitmapSource OriginalImage,
    BitmapSource CurrentRetouchedImage,
    FaceSnapshotMaskSet Snapshot,
    MaskPlane RetouchAllowMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    int AppliedStage,
    StagePreset StagePreset,
    WrinkleToolset? Toolset,
    MaskQualityReport MaskQualityReport,
    MaskPlane? BlemishMask = null);

public sealed record WrinkleSoftReduceResult(
    BitmapSource WrinkleReducedImage,
    WrinkleMaskSet WrinkleMaskSet,
    MaskPlane WrinkleCandidateMask,
    MaskPlane WrinkleAppliedMask,
    WrinkleProcessReport Report,
    IReadOnlyList<string> DebugWarnings);

public sealed record WrinkleProcessReport(
    int UnderEyeCandidateCount,
    int GlabellaCandidateCount,
    int ForeheadCandidateCount,
    int NasolabialCandidateCount,
    int MouthCornerCandidateCount,
    int NeckCandidateCount,
    int NoseShadowCandidateCount,
    int AppliedCount,
    int SkippedCount,
    double GlobalWrinkleAmount,
    double AverageCorrectionStrength,
    IReadOnlyList<string> DebugWarnings)
{
    public static WrinkleProcessReport Empty(IReadOnlyList<string> warnings)
    {
        return new WrinkleProcessReport(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, warnings);
    }

}
