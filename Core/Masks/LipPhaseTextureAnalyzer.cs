using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record LipPhaseTextureInput(
    BitmapSource Source,
    MaskPlane OuterLipMask,
    MaskPlane UpperLipMask,
    MaskPlane LowerLipMask,
    MaskPlane LipSurfaceMask,
    MaskPlane InnerMouthProtectionMask,
    MaskPlane VermilionBorderProtectionMask,
    double? MouthAxisAngleRad = null,
    MaskPlane? GuideSearchMask = null,
    MaskPlane? GuideCenterlineMask = null,
    double GuideBandRadiusPx = 16.0,
    double? GuideAxisAngleRad = null,
    double GuideLongitudinalExpansionPx = 30.0);

public sealed record LipPhaseTextureResult(
    bool IsEnabled,
    string Mode,
    double LipTextureConfidence,
    double LipLineStrength,
    double LipLineDirectionCoherence,
    double LipCrackSeverityScore,
    double LipDrynessScore,
    double LipRoughnessScore,
    double LipGlossScore,
    double LipBorderClarityScore,
    double LipCornerDrynessScore,
    double UpperLipCrackScore,
    double LowerLipCrackScore,
    double UpperLipDrynessScore,
    double LowerLipDrynessScore,
    MaskPlane DirectionalResponseMap,
    MaskPlane CrackCandidateMap,
    MaskPlane DrynessCandidateMap,
    MaskPlane GlossCandidateMap,
    MaskPlane BorderClarityMap,
    MaskPlane GuideEvidenceMask,
    IReadOnlyList<string> Warnings);

public static class LipPhaseTextureAnalyzer
{
    public static LipPhaseTextureResult Analyze(LipPhaseTextureInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        int width = input.Source.PixelWidth;
        int height = input.Source.PixelHeight;
        ValidateMaskSize(input, width, height);

        MaskPlane directionalMap = MaskPlane.Empty(width, height);
        MaskPlane crackMap = MaskPlane.Empty(width, height);
        MaskPlane drynessMap = MaskPlane.Empty(width, height);
        MaskPlane glossMap = MaskPlane.Empty(width, height);
        MaskPlane borderMap = MaskPlane.Empty(width, height);
        MaskPlane guideEvidenceMap = MaskPlane.Empty(width, height);
        List<string> warnings = new()
        {
            "lip_guide_texture_evidence_no_visible_correction",
            "lip_guide_texture_evidence_requires_3d_guide"
        };

        MaskPlane lipSurface = BuildEffectiveLipSurface(input);
        double fullSurfaceAverage = lipSurface.Average();
        if (fullSurfaceAverage <= 0.00002)
        {
            warnings.Add("lip_phase_detection_skipped_missing_lip_surface_mask");
            return CreateDisabled(width, height, "protect_only_missing_lip_surface_mask", warnings);
        }

        double mouthAxis = input.GuideAxisAngleRad ?? input.MouthAxisAngleRad ?? EstimateMouthAxisAngle(lipSurface);
        MaskPlane guideSearchSurface = BuildGuideSearchSurface(input, lipSurface, mouthAxis, warnings);
        double guideSurfaceAverage = guideSearchSurface.Average();
        if (guideSurfaceAverage <= 0.000002)
        {
            warnings.Add("lip_phase_detection_skipped_missing_lip_guide_search_band");
            return CreateDisabled(width, height, "protect_only_missing_lip_guide_search_band", warnings);
        }

        BitmapSource bitmap = input.Source.Format == PixelFormats.Bgra32
            ? input.Source
            : new FormatConvertedBitmap(input.Source, PixelFormats.Bgra32, null, 0);
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        double normal = mouthAxis + Math.PI * 0.5;
        double[] lineOrientations =
        [
            normal,
            normal - Math.PI / 7.0,
            normal + Math.PI / 7.0
        ];

        double lineSum = 0;
        double lineWeight = 0;
        double coherenceSum = 0;
        double crackSum = 0;
        double drynessSum = 0;
        double roughnessSum = 0;
        double glossSum = 0;
        double cornerDrynessSum = 0;
        double cornerWeight = 0;
        double upperCrackSum = 0;
        double upperDrynessSum = 0;
        double upperWeight = 0;
        double lowerCrackSum = 0;
        double lowerDrynessSum = 0;
        double lowerWeight = 0;

        LipBounds bounds = GetMaskBounds(guideSearchSurface);
        if (!bounds.IsValid)
        {
            warnings.Add("lip_phase_detection_skipped_empty_lip_guide_bounds");
            return CreateDisabled(width, height, "protect_only_empty_lip_guide_bounds", warnings);
        }

        for (int y = Math.Max(1, bounds.Top); y <= Math.Min(height - 2, bounds.Bottom); y++)
        {
            for (int x = Math.Max(1, bounds.Left); x <= Math.Min(width - 2, bounds.Right); x++)
            {
                double surfaceWeight = guideSearchSurface[x, y];
                if (surfaceWeight <= 0.05)
                {
                    continue;
                }

                double bestLine = 0;
                double secondLine = 0;
                for (int i = 0; i < lineOrientations.Length; i++)
                {
                    double response = TextureFlowAnalyzer.GetMultiScaleLineResponse(pixels, width, height, stride, x, y, lineOrientations[i]);
                    if (response > bestLine)
                    {
                        secondLine = bestLine;
                        bestLine = response;
                    }
                    else if (response > secondLine)
                    {
                        secondLine = response;
                    }
                }

                double horizontalSuppression = TextureFlowAnalyzer.GetMultiScaleLineResponse(pixels, width, height, stride, x, y, mouthAxis);
                double lineResponse = Math.Clamp(bestLine * (1 - horizontalSuppression * 0.35), 0, 1);
                double coherence = bestLine <= 0.0001 ? 0 : Math.Clamp((bestLine - secondLine) / bestLine, 0, 1);
                double luminance = TextureFlowAnalyzer.GetLuminance(pixels, stride, x, y) / 255.0;
                double localMean = TextureFlowAnalyzer.GetLocalMeanLuma(pixels, width, height, stride, x, y, 3) / 255.0;
                double localVariance = TextureFlowAnalyzer.GetLocalVarianceLuma(pixels, width, height, stride, x, y, 2);
                double sharpness = TextureFlowAnalyzer.GetSharpness(pixels, width, height, stride, x, y);
                double darkSplit = Math.Clamp((localMean - luminance) * 2.7, 0, 1);
                double brightFlake = Math.Clamp((luminance - localMean) * 3.2, 0, 1);
                double crack = Math.Clamp(lineResponse * (0.55 + coherence * 0.45) * (darkSplit * 0.70 + sharpness * 0.30), 0, 1);
                double dryness = Math.Clamp((localVariance * 1.35 + brightFlake * 0.85 + sharpness * 0.35) * (1 - lineResponse * 0.22), 0, 1);
                double roughness = Math.Clamp(localVariance * 1.55 + sharpness * 0.35, 0, 1);
                double gloss = Math.Clamp((luminance - 0.58) * 2.6, 0, 1) * Math.Clamp(1 - localVariance * 1.8, 0, 1) * Math.Clamp(1 - lineResponse, 0, 1);

                directionalMap[x, y] = Math.Max(directionalMap[x, y], lineResponse * surfaceWeight);
                crackMap[x, y] = Math.Max(crackMap[x, y], crack * surfaceWeight);
                drynessMap[x, y] = Math.Max(drynessMap[x, y], dryness * surfaceWeight);
                glossMap[x, y] = Math.Max(glossMap[x, y], gloss * surfaceWeight);
                guideEvidenceMap[x, y] = Math.Max(guideEvidenceMap[x, y], Math.Max(lineResponse, Math.Max(crack, dryness)) * surfaceWeight);

                lineSum += lineResponse * surfaceWeight;
                coherenceSum += coherence * lineResponse * surfaceWeight;
                crackSum += crack * surfaceWeight;
                drynessSum += dryness * surfaceWeight;
                roughnessSum += roughness * surfaceWeight;
                glossSum += gloss * surfaceWeight;
                lineWeight += surfaceWeight;

                double upper = input.UpperLipMask[x, y] * surfaceWeight;
                double lower = input.LowerLipMask[x, y] * surfaceWeight;
                upperCrackSum += crack * upper;
                upperDrynessSum += dryness * upper;
                upperWeight += upper;
                lowerCrackSum += crack * lower;
                lowerDrynessSum += dryness * lower;
                lowerWeight += lower;

                double xRatio = bounds.Width <= 1 ? 0.5 : (x - bounds.Left) / (double)bounds.Width;
                double corner = Math.Max(0, Math.Max(0.16 - xRatio, xRatio - 0.84) / 0.16) * surfaceWeight;
                cornerDrynessSum += Math.Max(dryness, crack) * corner;
                cornerWeight += corner;
            }
        }

        BuildBorderClarityMap(input.OuterLipMask, lipSurface, pixels, width, height, stride, borderMap);
        double borderClarity = AverageMasked(borderMap, input.VermilionBorderProtectionMask);
        if (input.VermilionBorderProtectionMask.Average() <= 0.00002)
        {
            warnings.Add("lip_border_clarity_low_confidence_missing_vermilion_border_mask");
            borderClarity = AverageMask(borderMap);
        }

        double guideToSurfaceRatio = fullSurfaceAverage <= 0.000001
            ? 0
            : Math.Clamp(guideSurfaceAverage / fullSurfaceAverage, 0, 1);
        double confidence = Math.Clamp(
            Math.Sqrt(guideSurfaceAverage * 2200.0) *
            (0.55 + guideToSurfaceRatio * 0.45) *
            (lineWeight > 32 ? 1.0 : 0.55) *
            (input.GuideSearchMask is not null || input.GuideCenterlineMask is not null ? 1.0 : 0.0),
            0,
            1);

        warnings.Add("lip_phase_detection_lip_surface_and_guide_band_only");
        warnings.Add("lip_phase_detection_can_create_candidate_mask_only_after_guide_confidence");
        warnings.Add("lip_phase_detection_requires_user_control_for_visible_correction");

        return new LipPhaseTextureResult(
            true,
            "guide_centered_two_long_surface_planes",
            confidence,
            SafeAverage(lineSum, lineWeight),
            SafeAverage(coherenceSum, Math.Max(lineSum, 0.0001)),
            SafeAverage(crackSum, lineWeight),
            SafeAverage(drynessSum, lineWeight),
            SafeAverage(roughnessSum, lineWeight),
            SafeAverage(glossSum, lineWeight),
            borderClarity,
            SafeAverage(cornerDrynessSum, cornerWeight),
            SafeAverage(upperCrackSum, upperWeight),
            SafeAverage(lowerCrackSum, lowerWeight),
            SafeAverage(upperDrynessSum, upperWeight),
            SafeAverage(lowerDrynessSum, lowerWeight),
            directionalMap,
            crackMap,
            drynessMap,
            glossMap,
            borderMap,
            guideEvidenceMap,
            warnings);
    }

    public static MaskPlane BuildLipSurfaceMask(MaskPlane outerLipMask, MaskPlane innerMouthProtectionMask)
    {
        return MaskPlane.Subtract(outerLipMask, innerMouthProtectionMask);
    }

    public static MaskPlane BuildGuideSearchBand(MaskPlane guideCenterlineMask, double radiusPx, MaskPlane lipSurfaceMask)
    {
        MaskPlane.EnsureSameSize(guideCenterlineMask, lipSurfaceMask);
        int radius = Math.Max(1, (int)Math.Ceiling(radiusPx));
        MaskPlane result = MaskPlane.Empty(guideCenterlineMask.Width, guideCenterlineMask.Height);
        for (int y = 0; y < guideCenterlineMask.Height; y++)
        {
            for (int x = 0; x < guideCenterlineMask.Width; x++)
            {
                double guide = guideCenterlineMask[x, y];
                if (guide <= 0.01)
                {
                    continue;
                }

                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(guideCenterlineMask.Height - 1, y + radius); yy++)
                {
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(guideCenterlineMask.Width - 1, x + radius); xx++)
                    {
                        double distance = Math.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                        if (distance > radius)
                        {
                            continue;
                        }

                        double feather = 1.0 - distance / Math.Max(1.0, radius);
                        result[xx, yy] = Math.Max(result[xx, yy], guide * (0.35 + feather * 0.65));
                    }
                }
            }
        }

        return MaskPlane.Intersect(result, lipSurfaceMask);
    }

    private static LipPhaseTextureResult CreateDisabled(int width, int height, string mode, IReadOnlyList<string> warnings)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        return new LipPhaseTextureResult(
            false,
            mode,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            warnings);
    }

    private static MaskPlane BuildEffectiveLipSurface(LipPhaseTextureInput input)
    {
        MaskPlane boldSurface = MaskPlane.Union(input.LipSurfaceMask, MaskPlane.Multiply(input.OuterLipMask, 0.52));
        MaskPlane lipOnly = MaskPlane.Subtract(boldSurface, MaskPlane.Multiply(input.InnerMouthProtectionMask, 0.86));
        return MaskPlane.Subtract(lipOnly, MaskPlane.Multiply(input.VermilionBorderProtectionMask, 0.05));
    }

    private static MaskPlane BuildGuideSearchSurface(LipPhaseTextureInput input, MaskPlane effectiveLipSurface, double mouthAxis, List<string> warnings)
    {
        MaskPlane guideSurface = MaskPlane.Empty(effectiveLipSurface.Width, effectiveLipSurface.Height);
        if (input.GuideSearchMask is not null)
        {
            MaskPlane.EnsureSameSize(input.GuideSearchMask, effectiveLipSurface);
            guideSurface = MaskPlane.Union(guideSurface, MaskPlane.Intersect(input.GuideSearchMask, effectiveLipSurface));
            warnings.Add("lip_guide_texture_search_uses_supplied_guide_search_mask");
        }

        if (input.GuideCenterlineMask is not null)
        {
            MaskPlane.EnsureSameSize(input.GuideCenterlineMask, effectiveLipSurface);
            guideSurface = MaskPlane.Union(guideSurface, BuildGuideSearchBand(input.GuideCenterlineMask, input.GuideBandRadiusPx, effectiveLipSurface));
            warnings.Add("lip_guide_texture_search_uses_centerline_band");
        }

        if (input.GuideSearchMask is null && input.GuideCenterlineMask is null)
        {
            warnings.Add("lip_guide_texture_search_missing_guide_no_standalone_scan");
        }

        if (guideSurface.Average() > 0.000002)
        {
            double crossRadius = Math.Max(8.0, input.GuideBandRadiusPx * 1.15);
            double alongRadius = Math.Max(input.GuideLongitudinalExpansionPx, input.GuideBandRadiusPx * 2.30);
            guideSurface = ExpandGuideSearchAlongLipAxis(guideSurface, effectiveLipSurface, mouthAxis, alongRadius, crossRadius);
            guideSurface = MaskPlane.Union(guideSurface, BuildTwoLongLipSurfacePlanes(input, effectiveLipSurface));
            warnings.Add("lip_guide_texture_search_range_expanded_bold_to_lip_ends");
            warnings.Add("lip_guide_texture_search_uses_two_long_upper_lower_surface_planes");
        }

        return guideSurface;
    }

    private static MaskPlane BuildTwoLongLipSurfacePlanes(LipPhaseTextureInput input, MaskPlane effectiveLipSurface)
    {
        MaskPlane.EnsureSameSize(input.UpperLipMask, effectiveLipSurface);
        MaskPlane.EnsureSameSize(input.LowerLipMask, effectiveLipSurface);
        MaskPlane upperPlane = MaskPlane.Intersect(MaskPlane.Multiply(input.UpperLipMask, 0.68), effectiveLipSurface);
        MaskPlane lowerPlane = MaskPlane.Intersect(MaskPlane.Multiply(input.LowerLipMask, 0.68), effectiveLipSurface);
        return MaskPlane.Union(upperPlane, lowerPlane);
    }

    private static MaskPlane ExpandGuideSearchAlongLipAxis(
        MaskPlane guideSurface,
        MaskPlane lipSurface,
        double mouthAxis,
        double alongRadius,
        double crossRadius)
    {
        MaskPlane.EnsureSameSize(guideSurface, lipSurface);
        double axisX = Math.Cos(mouthAxis);
        double axisY = Math.Sin(mouthAxis);
        double normalX = -axisY;
        double normalY = axisX;
        int searchRadius = Math.Max(1, (int)Math.Ceiling(Math.Max(alongRadius, crossRadius)));
        MaskPlane expanded = guideSurface.Clone();
        LipBounds bounds = GetMaskBounds(guideSurface);
        if (!bounds.IsValid)
        {
            return MaskPlane.Intersect(expanded, lipSurface);
        }

        int minY = Math.Max(0, bounds.Top - searchRadius);
        int maxY = Math.Min(guideSurface.Height - 1, bounds.Bottom + searchRadius);
        int minX = Math.Max(0, bounds.Left - searchRadius);
        int maxX = Math.Min(guideSurface.Width - 1, bounds.Right + searchRadius);
        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x <= bounds.Right; x++)
            {
                double guide = guideSurface[x, y];
                if (guide <= 0.04)
                {
                    continue;
                }

                for (int yy = minY; yy <= maxY; yy++)
                {
                    for (int xx = minX; xx <= maxX; xx++)
                    {
                        double lip = lipSurface[xx, yy];
                        if (lip <= 0.02)
                        {
                            continue;
                        }

                        double dx = xx - x;
                        double dy = yy - y;
                        double along = Math.Abs(dx * axisX + dy * axisY);
                        double cross = Math.Abs(dx * normalX + dy * normalY);
                        double normalized = Math.Sqrt(
                            along * along / Math.Max(1.0, alongRadius * alongRadius) +
                            cross * cross / Math.Max(1.0, crossRadius * crossRadius));
                        if (normalized > 1.0)
                        {
                            continue;
                        }

                        double feather = 1.0 - normalized;
                        double expandedWeight = guide * (0.22 + feather * 0.58) * lip;
                        expanded[xx, yy] = Math.Max(expanded[xx, yy], expandedWeight);
                    }
                }
            }
        }

        return MaskPlane.Intersect(expanded, lipSurface);
    }

    private static void ValidateMaskSize(LipPhaseTextureInput input, int width, int height)
    {
        Validate(input.OuterLipMask, width, height, nameof(input.OuterLipMask));
        Validate(input.UpperLipMask, width, height, nameof(input.UpperLipMask));
        Validate(input.LowerLipMask, width, height, nameof(input.LowerLipMask));
        Validate(input.LipSurfaceMask, width, height, nameof(input.LipSurfaceMask));
        Validate(input.InnerMouthProtectionMask, width, height, nameof(input.InnerMouthProtectionMask));
        Validate(input.VermilionBorderProtectionMask, width, height, nameof(input.VermilionBorderProtectionMask));
        if (input.GuideSearchMask is not null)
        {
            Validate(input.GuideSearchMask, width, height, nameof(input.GuideSearchMask));
        }

        if (input.GuideCenterlineMask is not null)
        {
            Validate(input.GuideCenterlineMask, width, height, nameof(input.GuideCenterlineMask));
        }
    }

    private static void Validate(MaskPlane mask, int width, int height, string name)
    {
        if (mask.Width != width || mask.Height != height)
        {
            throw new InvalidOperationException(name + " size must match source image.");
        }
    }

    private static double EstimateMouthAxisAngle(MaskPlane lipSurface)
    {
        double total = 0;
        double meanX = 0;
        double meanY = 0;
        for (int y = 0; y < lipSurface.Height; y++)
        {
            for (int x = 0; x < lipSurface.Width; x++)
            {
                double weight = lipSurface[x, y];
                if (weight <= 0.05)
                {
                    continue;
                }

                meanX += x * weight;
                meanY += y * weight;
                total += weight;
            }
        }

        if (total <= 0)
        {
            return 0;
        }

        meanX /= total;
        meanY /= total;
        double covXX = 0;
        double covYY = 0;
        double covXY = 0;
        for (int y = 0; y < lipSurface.Height; y++)
        {
            for (int x = 0; x < lipSurface.Width; x++)
            {
                double weight = lipSurface[x, y];
                if (weight <= 0.05)
                {
                    continue;
                }

                double dx = x - meanX;
                double dy = y - meanY;
                covXX += dx * dx * weight;
                covYY += dy * dy * weight;
                covXY += dx * dy * weight;
            }
        }

        return 0.5 * Math.Atan2(2 * covXY, covXX - covYY);
    }

    private static void BuildBorderClarityMap(MaskPlane outerLipMask, MaskPlane lipSurface, byte[] pixels, int width, int height, int stride, MaskPlane target)
    {
        for (int y = 1; y < height - 1; y++)
        {
            for (int x = 1; x < width - 1; x++)
            {
                double mask = outerLipMask[x, y];
                if (mask <= 0.02 || lipSurface[x, y] > 0.45)
                {
                    continue;
                }

                bool nearBoundary = false;
                for (int yy = y - 1; yy <= y + 1 && !nearBoundary; yy++)
                {
                    for (int xx = x - 1; xx <= x + 1; xx++)
                    {
                        if (Math.Abs(outerLipMask[xx, yy] - mask) > 0.22)
                        {
                            nearBoundary = true;
                            break;
                        }
                    }
                }

                if (!nearBoundary)
                {
                    continue;
                }

                target[x, y] = TextureFlowAnalyzer.GetSharpness(pixels, width, height, stride, x, y);
            }
        }
    }

    private static LipBounds GetMaskBounds(MaskPlane mask)
    {
        int left = mask.Width;
        int top = mask.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y] <= 0.05)
                {
                    continue;
                }

                left = Math.Min(left, x);
                right = Math.Max(right, x);
                top = Math.Min(top, y);
                bottom = Math.Max(bottom, y);
            }
        }

        return new LipBounds(left, top, right, bottom);
    }

    private static double AverageMasked(MaskPlane values, MaskPlane mask)
    {
        double sum = 0;
        double weight = 0;
        MaskPlane.EnsureSameSize(values, mask);
        for (int index = 0; index < values.Values.Length; index++)
        {
            sum += values.Values[index] * mask.Values[index];
            weight += mask.Values[index];
        }

        return SafeAverage(sum, weight);
    }

    private static double AverageMask(MaskPlane values)
    {
        double sum = 0;
        double weight = 0;
        for (int index = 0; index < values.Values.Length; index++)
        {
            if (values.Values[index] <= 0)
            {
                continue;
            }

            sum += values.Values[index];
            weight++;
        }

        return SafeAverage(sum, weight);
    }

    private static double SafeAverage(double sum, double weight)
    {
        return weight <= 0.000001 ? 0 : Math.Clamp(sum / weight, 0, 1);
    }

    private readonly record struct LipBounds(int Left, int Top, int Right, int Bottom)
    {
        public bool IsValid => Right >= Left && Bottom >= Top;

        public int Width => Math.Max(0, Right - Left + 1);
    }
}

public static class LipGuideTextureEvidenceAnalyzer
{
    public static LipPhaseTextureResult Analyze(LipPhaseTextureInput input)
    {
        return LipPhaseTextureAnalyzer.Analyze(input);
    }

    public static MaskPlane BuildGuideSearchBand(MaskPlane guideCenterlineMask, double radiusPx, MaskPlane lipSurfaceMask)
    {
        return LipPhaseTextureAnalyzer.BuildGuideSearchBand(guideCenterlineMask, radiusPx, lipSurfaceMask);
    }
}
