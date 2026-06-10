using PhotoRetouch.AnchorMesh;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record EyebrowAnalyzerInput(
    int Width,
    int Height,
    BitmapSource? Source,
    AnchorMeshFeature? LeftEye,
    AnchorMeshFeature? RightEye,
    AnchorMeshFeature? LeftPupil,
    AnchorMeshFeature? RightPupil,
    AnchorMeshFeature? LeftBrow,
    AnchorMeshFeature? RightBrow,
    double FaceW,
    double FaceH,
    double FaceCenterX,
    double EyeLineAngle,
    double FrontalPoseConfidence,
    MaskPlane? SkinMask = null,
    MaskPlane? HairMask = null,
    MaskPlane? ForeheadWrinkleMask = null);

public sealed record EyebrowAnalysisResult(
    EyebrowCandidate LeftEyebrowCandidate,
    EyebrowCandidate RightEyebrowCandidate,
    MaskPlane LeftEyebrowMask,
    MaskPlane RightEyebrowMask,
    MaskPlane EyebrowProtectionMask,
    double EyebrowConfidence,
    string EyebrowFailureReason,
    IReadOnlyList<string> DebugOverlayData);

public sealed record EyebrowCandidate(
    string Side,
    bool IsDetected,
    MaskPlane CandidateMask,
    MaskPlane SearchRoiMask,
    double Confidence,
    string FailureReason,
    double BrowToEyeDistance,
    double BrowLength,
    double BrowThickness,
    double BrowSlopeAngle,
    double BrowArchScore,
    double BrowColorScore,
    double BrowTextureScore,
    double BrowConnectednessScore,
    double DistanceScore,
    double LengthScore,
    double ThicknessScore,
    double DirectionScore,
    double ConfusionPenalty);

public static class EyebrowAnalyzer
{
    public static EyebrowAnalysisResult Analyze(EyebrowAnalyzerInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateOptionalMask(input.SkinMask, input.Width, input.Height, nameof(input.SkinMask));
        ValidateOptionalMask(input.HairMask, input.Width, input.Height, nameof(input.HairMask));
        ValidateOptionalMask(input.ForeheadWrinkleMask, input.Width, input.Height, nameof(input.ForeheadWrinkleMask));

        EyebrowPixelData? pixels = TryCreatePixelData(input.Source, input.Width, input.Height);
        List<string> debug = new();
        EyebrowCandidate left = AnalyzeOne(input, pixels, input.LeftBrow, input.LeftEye, input.LeftPupil, isRight: false, debug);
        EyebrowCandidate right = AnalyzeOne(input, pixels, input.RightBrow, input.RightEye, input.RightPupil, isRight: true, debug);
        MaskPlane protection = MaskPlane.Union(left.CandidateMask, right.CandidateMask);
        double confidence = Math.Max(left.Confidence, right.Confidence);
        string failure = confidence > 0.66
            ? string.Empty
            : string.Join(";", new[] { left.FailureReason, right.FailureReason }.Where(reason => !string.IsNullOrWhiteSpace(reason)));

        debug.Add("eyebrow_analyzer_no_visible_correction");
        debug.Add("eyebrow_analyzer_output_is_candidate_and_protection_mask_only");

        return new EyebrowAnalysisResult(
            left,
            right,
            left.CandidateMask,
            right.CandidateMask,
            protection,
            confidence,
            failure,
            debug);
    }

    private static EyebrowCandidate AnalyzeOne(
        EyebrowAnalyzerInput input,
        EyebrowPixelData? pixels,
        AnchorMeshFeature? browFeature,
        AnchorMeshFeature? eyeFeature,
        AnchorMeshFeature? pupilFeature,
        bool isRight,
        List<string> debug)
    {
        string side = isRight ? "right" : "left";
        MaskPlane empty = MaskPlane.Empty(input.Width, input.Height);
        if (eyeFeature is null || eyeFeature.Points.Count == 0)
        {
            debug.Add(side + "_eyebrow_failed_missing_eye_anchor");
            return EmptyCandidate(side, empty, "missing_eye_anchor");
        }

        MaskPlane roi = BuildOrbitalSearchRoi(input, browFeature, eyeFeature, pupilFeature, isRight);
        if (roi.Average() <= 0.000002)
        {
            debug.Add(side + "_eyebrow_failed_empty_orbital_roi");
            return EmptyCandidate(side, roi, "empty_orbital_roi");
        }

        if (pixels is null)
        {
            debug.Add(side + "_eyebrow_pixel_evidence_unavailable_no_anchor_shape_fallback");
            return new EyebrowCandidate(
                side,
                false,
                empty,
                roi,
                0,
                "pixel_evidence_unavailable_no_anchor_shape_fallback",
                0,
                browFeature?.Width ?? 0,
                browFeature?.Height ?? 0,
                browFeature?.AngleRad ?? eyeFeature.AngleRad,
                0,
                0,
                0,
                0,
                0.35,
                0.35,
                0.35,
                0.35,
                0.25);
        }

        MaskPlane evidence = BuildPixelEvidenceMask(roi, browFeature, eyeFeature, pixels, input);
        BrowEvidenceStats stats = MeasureEvidence(evidence, roi, browFeature, eyeFeature, input);
        MaskPlane mask = evidence.Average() > 0.000015 ? BuildSurfaceMaskFromEvidence(evidence, roi, stats) : empty;

        double distanceScore = ScoreRange(stats.BrowToEyeDistance / Math.Max(1.0, input.FaceH), 0.015, 0.065, 0.008, 0.085);
        double lengthScore = ScoreRange(stats.BrowLength / Math.Max(1.0, stats.EyeW), 1.05, 1.35, 0.90, 1.50);
        double thicknessScore = ScoreRange(stats.BrowThickness / Math.Max(1.0, stats.EyeH), 0.35, 0.85, 0.25, 1.10);
        double directionScore = 1 - Math.Clamp(Math.Abs(NormalizeAngle(stats.BrowSlopeAngle - eyeFeature.AngleRad)) / (Math.PI / 3.0), 0, 1);
        double archScore = stats.ArchScore;
        double colorScore = stats.ColorScore;
        double textureScore = stats.TextureScore;
        double connectednessScore = stats.ConnectednessScore;
        double confusionPenalty = CalculateConfusionPenalty(mask, input, distanceScore, thicknessScore);
        double rawScore =
            distanceScore * 0.16 +
            lengthScore * 0.12 +
            thicknessScore * 0.13 +
            colorScore * 0.16 +
            textureScore * 0.15 +
            connectednessScore * 0.12 +
            directionScore * 0.10 +
            archScore * 0.06 -
            confusionPenalty;
        double confidence = Math.Clamp(rawScore * Math.Clamp(input.FrontalPoseConfidence, 0.45, 1.0), 0, 1);

        bool detected = confidence >= 0.52 && evidence.Average() > 0.000015;
        string failure = detected
            ? string.Empty
            : confidence >= 0.34 ? "medium_confidence_protection_only" : BuildFailureReason(evidence, stats, distanceScore, lengthScore, thicknessScore, colorScore, textureScore, connectednessScore, confusionPenalty);

        if (!detected)
        {
            debug.Add(side + "_eyebrow_" + failure);
        }
        else
        {
            debug.Add(side + "_eyebrow_candidate_confirmed:confidence=" + Math.Round(confidence, 3) +
                      ",distance=" + Math.Round(stats.BrowToEyeDistance, 1) +
                      ",length=" + Math.Round(stats.BrowLength, 1) +
                      ",thickness=" + Math.Round(stats.BrowThickness, 1) +
                      ",angle=" + Math.Round(stats.BrowSlopeAngle, 3));
        }

        return new EyebrowCandidate(
            side,
            detected,
            mask,
            roi,
            confidence,
            failure,
            stats.BrowToEyeDistance,
            stats.BrowLength,
            stats.BrowThickness,
            stats.BrowSlopeAngle,
            archScore,
            colorScore,
            textureScore,
            connectednessScore,
            distanceScore,
            lengthScore,
            thicknessScore,
            directionScore,
            confusionPenalty);
    }

    private static MaskPlane BuildOrbitalSearchRoi(EyebrowAnalyzerInput input, AnchorMeshFeature? browFeature, AnchorMeshFeature eyeFeature, AnchorMeshFeature? pupilFeature, bool isRight)
    {
        MaskPlane roi = MaskPlane.Empty(input.Width, input.Height);
        double eyeAngle = eyeFeature.AngleRad;
        double axisX = Math.Cos(eyeAngle);
        double axisY = Math.Sin(eyeAngle);
        double upX = Math.Sin(eyeAngle);
        double upY = -Math.Cos(eyeAngle);
        EyeGeometry eye = GetEyeGeometry(eyeFeature);
        double pupilWeight = pupilFeature?.Points.Count > 0 ? 0.35 : 0.0;
        double orbitalCenterX = eyeFeature.CenterX * (1 - pupilWeight) + (pupilFeature?.CenterX ?? eyeFeature.CenterX) * pupilWeight;
        double orbitalCenterY = eyeFeature.CenterY * (1 - pupilWeight) + (pupilFeature?.CenterY ?? eyeFeature.CenterY) * pupilWeight;
        double eyeW = eye.EyeW;
        double eyeH = eye.EyeH;
        double minUp = Math.Max(input.FaceH * 0.008, eyeH * 0.28);
        double maxUp = Math.Min(input.FaceH * 0.085, eyeH * 2.55);
        if (maxUp <= minUp + 2)
        {
            maxUp = minUp + Math.Clamp(eyeW * 0.42, 18.0, 74.0);
        }

        double sideInner = Math.Clamp(eyeW * 0.64, 16.0, 80.0);
        double sideOuter = Math.Clamp(eyeW * 0.92, 20.0, 110.0);
        double maxSide = Math.Max(sideInner, sideOuter);
        double arcHalfBand = Math.Clamp(Math.Max(eyeH * 1.05, eyeW * 0.22), 12.0, 42.0);

        if (browFeature is not null && browFeature.Points.Count > 0)
        {
            double left = browFeature.Points.Min(point => point.SnappedX) - browFeature.Width * 0.28 - 6;
            double right = browFeature.Points.Max(point => point.SnappedX) + browFeature.Width * 0.28 + 6;
            double top = browFeature.CenterY - Math.Max(browFeature.Height * 2.4, browFeature.Width * 0.24) - 6;
            double bottom = browFeature.CenterY + Math.Max(browFeature.Height * 2.1, browFeature.Width * 0.22) + 6;
            for (int y = Math.Max(0, (int)Math.Floor(top)); y <= Math.Min(input.Height - 1, (int)Math.Ceiling(bottom)); y++)
            {
                for (int x = Math.Max(0, (int)Math.Floor(left)); x <= Math.Min(input.Width - 1, (int)Math.Ceiling(right)); x++)
                {
                    roi[x, y] = 0.42;
                }
            }
        }

        int roiLeft = Math.Max(0, (int)Math.Floor(orbitalCenterX - maxSide - 8));
        int roiRight = Math.Min(input.Width - 1, (int)Math.Ceiling(orbitalCenterX + maxSide + 8));
        int roiTop = Math.Max(0, (int)Math.Floor(eye.UpperY - maxUp - arcHalfBand - 6));
        int roiBottom = Math.Min(input.Height - 1, (int)Math.Ceiling(eye.UpperY - minUp + arcHalfBand + 6));

        for (int y = roiTop; y <= roiBottom; y++)
        {
            for (int x = roiLeft; x <= roiRight; x++)
            {
                double px = x + 0.5;
                double py = y + 0.5;
                double side = (px - orbitalCenterX) * axisX + (py - orbitalCenterY) * axisY;
                double upFromUpperLid = (px - eye.UpperX) * upX + (py - eye.UpperY) * upY;
                double sideLimit = side < 0 ? sideInner : sideOuter;
                if (Math.Abs(side) > sideLimit || upFromUpperLid < minUp || upFromUpperLid > maxUp)
                {
                    continue;
                }

                double sideNorm = Math.Clamp(side / Math.Max(1.0, sideLimit), -1.0, 1.0);
                double archLift = Math.Sin((sideNorm + 1.0) * Math.PI * 0.5) * eyeH * 0.34;
                double preferredUp = minUp + (maxUp - minUp) * 0.45 + archLift;
                double arcDistance = Math.Abs(upFromUpperLid - preferredUp);
                double arcWeight = 1.0 - SmoothStep(arcHalfBand, arcHalfBand * 1.85, arcDistance);
                if (arcWeight <= 0.04)
                {
                    continue;
                }

                roi[x, y] = Math.Max(roi[x, y], arcWeight);
            }
        }

        return roi;
    }

    private static MaskPlane BuildPixelEvidenceMask(MaskPlane roi, AnchorMeshFeature? browFeature, AnchorMeshFeature eyeFeature, EyebrowPixelData pixelData, EyebrowAnalyzerInput input)
    {
        MaskPlane candidate = MaskPlane.Empty(roi.Width, roi.Height);
        List<double> lumas = new();
        MaskBounds bounds = GetMaskBounds(roi);
        if (!bounds.IsValid)
        {
            return candidate;
        }

        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x <= bounds.Right; x++)
            {
                if (roi[x, y] > 0.04)
                {
                    lumas.Add(pixelData.GetLuma(x, y));
                }
            }
        }

        if (lumas.Count < 12)
        {
            return candidate;
        }

        lumas.Sort();
        double p35 = lumas[(int)Math.Clamp(Math.Round((lumas.Count - 1) * 0.35), 0, lumas.Count - 1)];
        double p65 = lumas[(int)Math.Clamp(Math.Round((lumas.Count - 1) * 0.65), 0, lumas.Count - 1)];
        double contrast = Math.Max(6.0, p65 - p35);
        SkinReference skinReference = EstimateSkinReference(pixelData, roi, bounds, input, p35, p65, contrast);
        double angle = browFeature?.AngleRad ?? eyeFeature.AngleRad;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x <= bounds.Right; x++)
            {
                double roiWeight = roi[x, y];
                if (roiWeight <= 0.04)
                {
                    continue;
                }

                double luma = pixelData.GetLuma(x, y);
                double darkness = Math.Clamp((p65 + contrast * 0.18 - luma) / Math.Max(1, contrast * 1.55), 0, 1);
                double skinBoundary = GetSkinBoundaryScore(pixelData, x, y, skinReference);
                if (darkness <= 0.02 && skinBoundary <= 0.05)
                {
                    continue;
                }

                double directional = GetDirectionalHairScore(pixelData, x, y, cos, sin);
                double connectivity = GetNeighborhoodDarkness(pixelData, roi, x, y, p65 + contrast * 0.12);
                double colorCluster = GetColorClusterScore(pixelData, x, y);
                double skinSupport = input.SkinMask is null ? 1.0 : Math.Clamp(1.15 - input.SkinMask[x, y] * 0.25, 0.72, 1.0);
                double value = roiWeight * skinSupport * Math.Clamp(
                    darkness * 0.38 +
                    skinBoundary * 0.24 +
                    directional * 0.16 +
                    connectivity * 0.14 +
                    colorCluster * 0.08,
                    0,
                    1);
                if (value > 0.12)
                {
                    candidate[x, y] = value;
                }
            }
        }

        return FeatherSmallMask(candidate, radius: 1);
    }

    private static SkinReference EstimateSkinReference(EyebrowPixelData pixels, MaskPlane roi, MaskBounds bounds, EyebrowAnalyzerInput input, double p35, double p65, double contrast)
    {
        double red = 0;
        double green = 0;
        double blue = 0;
        double lumaSum = 0;
        int count = 0;
        double minLuma = p35 + contrast * 0.18;
        double maxLuma = Math.Min(245.0, p65 + contrast * 1.15);

        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x <= bounds.Right; x++)
            {
                if (roi[x, y] <= 0.04)
                {
                    continue;
                }

                if (input.SkinMask is not null && input.SkinMask[x, y] <= 0.03)
                {
                    continue;
                }

                double luma = pixels.GetLuma(x, y);
                if (luma < minLuma || luma > maxLuma)
                {
                    continue;
                }

                pixels.GetRgb(x, y, out byte r, out byte g, out byte b);
                red += r;
                green += g;
                blue += b;
                lumaSum += luma;
                count++;
            }
        }

        if (count < 10 && input.SkinMask is not null)
        {
            return EstimateSkinReference(pixels, roi, bounds, input with { SkinMask = null }, p35, p65, contrast);
        }

        if (count < 10)
        {
            return new SkinReference(165, 135, 118, p65, 0);
        }

        return new SkinReference(red / count, green / count, blue / count, lumaSum / count, Math.Clamp(count / 80.0, 0.25, 1.0));
    }

    private static double GetSkinBoundaryScore(EyebrowPixelData pixels, int x, int y, SkinReference skin)
    {
        if (skin.Confidence <= 0)
        {
            return 0;
        }

        pixels.GetRgb(x, y, out byte red, out byte green, out byte blue);
        double colorDistance = ColorDistance(red, green, blue, skin.Red, skin.Green, skin.Blue);
        double luma = pixels.GetLuma(x, y);
        double boundary = SmoothStep(16, 52, colorDistance);
        double lumaDrop = Math.Clamp((skin.Luma - luma - 3) / 58.0, 0, 1);
        return Math.Clamp((boundary * 0.56 + lumaDrop * 0.44) * skin.Confidence, 0, 1);
    }

    private static double ColorDistance(double red, double green, double blue, double referenceRed, double referenceGreen, double referenceBlue)
    {
        double dr = red - referenceRed;
        double dg = green - referenceGreen;
        double db = blue - referenceBlue;
        return Math.Sqrt(dr * dr * 0.30 + dg * dg * 0.45 + db * db * 0.25);
    }

    private static BrowEvidenceStats MeasureEvidence(MaskPlane evidence, MaskPlane roi, AnchorMeshFeature? browFeature, AnchorMeshFeature eyeFeature, EyebrowAnalyzerInput input)
    {
        BrowEvidenceAxis axis = EstimateAxis(evidence, browFeature, eyeFeature);
        List<BrowEvidenceSample> samples = CollectSamples(evidence, axis);
        EyeGeometry eye = GetEyeGeometry(eyeFeature);
        if (samples.Count < 8)
        {
            double fallbackLength = browFeature?.Width ?? eye.EyeW * 1.12;
            double fallbackThickness = browFeature?.Height ?? eye.EyeH * 0.54;
            double fallbackDistance = browFeature is null ? Math.Max(0, eye.UpperY - (eyeFeature.CenterY - eye.EyeH * 1.4)) : Math.Max(0, eye.UpperY - browFeature.CenterY);
            return new BrowEvidenceStats(
                axis,
                samples,
                fallbackDistance,
                fallbackLength,
                fallbackThickness,
                browFeature?.AngleRad ?? eyeFeature.AngleRad,
                0.25,
                AverageMask(evidence),
                0.18,
                0.18,
                eye.EyeW,
                eye.EyeH);
        }

        double minS = samples.Min(sample => sample.S);
        double maxS = samples.Max(sample => sample.S);
        double length = Math.Max(1, maxS - minS);
        double startU = WeightedBandAverage(samples, minS + length * 0.06, length * 0.18);
        double midU = WeightedBandAverage(samples, minS + length * 0.50, length * 0.20);
        double endU = WeightedBandAverage(samples, maxS - length * 0.06, length * 0.18);
        double thickness = EstimateThickness(samples, minS, maxS);
        double arch = Math.Clamp(Math.Abs(midU - (startU + endU) * 0.5) / Math.Max(1.0, thickness), 0, 1);
        double color = AverageMask(evidence);
        double texture = EstimateDirectionalTexture(evidence, axis);
        double connectedness = EstimateConnectedness(evidence, samples, length, thickness);
        double distance = Math.Max(0, eye.UpperY - axis.CenterY);

        return new BrowEvidenceStats(
            axis,
            samples,
            distance,
            length,
            thickness,
            axis.AngleRad,
            arch,
            color,
            texture,
            connectedness,
            eye.EyeW,
            eye.EyeH);
    }

    private static MaskPlane BuildSurfaceMaskFromEvidence(MaskPlane evidence, MaskPlane roi, BrowEvidenceStats stats)
    {
        MaskPlane.EnsureSameSize(evidence, roi);
        if (stats.Samples.Count < 8)
        {
            return MaskPlane.Intersect(evidence, roi);
        }

        double minS = stats.Samples.Min(sample => sample.S);
        double maxS = stats.Samples.Max(sample => sample.S);
        double length = Math.Max(1.0, maxS - minS);
        const int bandCount = 30;
        double bandStep = length / bandCount;
        double searchRadius = Math.Max(2.0, bandStep * 0.92);
        double[] lower = new double[bandCount];
        double[] upper = new double[bandCount];
        double[] support = new double[bandCount];
        bool[] valid = new bool[bandCount];

        for (int band = 0; band < bandCount; band++)
        {
            double centerS = minS + (band + 0.5) * bandStep;
            List<BrowEvidenceSample> local = stats.Samples
                .Where(sample => Math.Abs(sample.S - centerS) <= searchRadius && sample.Weight > 0.08)
                .ToList();
            if (local.Count < 2)
            {
                continue;
            }

            double lowU = WeightedUQuantile(local, 0.10);
            double highU = WeightedUQuantile(local, 0.90);
            double centerU = local.Sum(sample => sample.U * sample.Weight) / Math.Max(0.0001, local.Sum(sample => sample.Weight));
            double halfThickness = Math.Max(1.8, Math.Max(Math.Abs(highU - lowU) * 0.5, stats.BrowThickness * 0.22));
            lower[band] = centerU - halfThickness;
            upper[band] = centerU + halfThickness;
            support[band] = Math.Clamp(local.Sum(sample => sample.Weight) / Math.Max(1.0, searchRadius * Math.Max(2.0, stats.BrowThickness) * 0.45), 0.18, 1.0);
            valid[band] = true;
        }

        FillMissingSurfaceBands(lower, upper, support, valid);
        if (!valid.Any(value => value))
        {
            return MaskPlane.Intersect(evidence, roi);
        }

        MaskPlane surface = MaskPlane.Empty(evidence.Width, evidence.Height);
        MaskBounds bounds = GetMaskBounds(roi);
        double edgeFeather = Math.Max(1.0, stats.BrowThickness * 0.18);
        for (int y = bounds.Top; y <= bounds.Bottom; y++)
        {
            for (int x = bounds.Left; x <= bounds.Right; x++)
            {
                double roiWeight = roi[x, y];
                if (roiWeight <= 0.04)
                {
                    continue;
                }

                double dx = x - stats.Axis.CenterX;
                double dy = y - stats.Axis.CenterY;
                double s = dx * stats.Axis.AxisX + dy * stats.Axis.AxisY;
                if (s < minS - bandStep || s > maxS + bandStep)
                {
                    continue;
                }

                double u = dx * stats.Axis.UpX + dy * stats.Axis.UpY;
                double position = Math.Clamp((s - minS) / Math.Max(0.0001, length) * bandCount - 0.5, 0, bandCount - 1);
                int leftBand = Math.Clamp((int)Math.Floor(position), 0, bandCount - 1);
                int rightBand = Math.Clamp(leftBand + 1, 0, bandCount - 1);
                double t = position - leftBand;
                double lowU = lower[leftBand] * (1 - t) + lower[rightBand] * t;
                double highU = upper[leftBand] * (1 - t) + upper[rightBand] * t;
                double bandSupport = support[leftBand] * (1 - t) + support[rightBand] * t;
                if (u < lowU || u > highU)
                {
                    continue;
                }

                double edgeDistance = Math.Min(u - lowU, highU - u);
                double interior = SmoothStep(0, edgeFeather, edgeDistance);
                double evidenceBoost = Math.Clamp(evidence[x, y] * 0.65, 0, 0.65);
                double value = roiWeight * bandSupport * Math.Clamp(0.28 + interior * 0.52 + evidenceBoost, 0, 1);
                surface[x, y] = Math.Max(surface[x, y], value);
            }
        }

        return FeatherSmallMask(MaskPlane.Intersect(MaskPlane.Union(surface, evidence), roi), radius: 1);
    }

    private static double WeightedUQuantile(IReadOnlyList<BrowEvidenceSample> samples, double quantile)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        List<BrowEvidenceSample> ordered = samples.OrderBy(sample => sample.U).ToList();
        double total = Math.Max(0.0001, ordered.Sum(sample => sample.Weight));
        double target = total * Math.Clamp(quantile, 0, 1);
        double accumulated = 0;
        foreach (BrowEvidenceSample sample in ordered)
        {
            accumulated += sample.Weight;
            if (accumulated >= target)
            {
                return sample.U;
            }
        }

        return ordered[^1].U;
    }

    private static void FillMissingSurfaceBands(double[] lower, double[] upper, double[] support, bool[] valid)
    {
        if (!valid.Any(value => value))
        {
            return;
        }

        int count = valid.Length;
        for (int index = 0; index < count; index++)
        {
            if (valid[index])
            {
                continue;
            }

            int left = index - 1;
            while (left >= 0 && !valid[left])
            {
                left--;
            }

            int right = index + 1;
            while (right < count && !valid[right])
            {
                right++;
            }

            if (left >= 0 && right < count)
            {
                double t = (index - left) / (double)Math.Max(1, right - left);
                lower[index] = lower[left] * (1 - t) + lower[right] * t;
                upper[index] = upper[left] * (1 - t) + upper[right] * t;
                support[index] = Math.Min(support[left], support[right]) * 0.72;
                valid[index] = true;
            }
            else if (left >= 0 && index - left <= 2)
            {
                lower[index] = lower[left];
                upper[index] = upper[left];
                support[index] = support[left] * 0.55;
                valid[index] = true;
            }
            else if (right < count && right - index <= 2)
            {
                lower[index] = lower[right];
                upper[index] = upper[right];
                support[index] = support[right] * 0.55;
                valid[index] = true;
            }
        }
    }

    private static double CalculateConfusionPenalty(MaskPlane mask, EyebrowAnalyzerInput input, double distanceScore, double thicknessScore)
    {
        double penalty = 0;
        if (input.HairMask is not null)
        {
            penalty += OverlapRatio(mask, input.HairMask) * 0.34;
        }

        if (input.ForeheadWrinkleMask is not null)
        {
            penalty += OverlapRatio(mask, input.ForeheadWrinkleMask) * 0.30;
        }

        penalty += (1 - distanceScore) * 0.18;
        penalty += (1 - thicknessScore) * 0.10;
        return Math.Clamp(penalty, 0, 0.72);
    }

    private static string BuildFailureReason(MaskPlane evidence, BrowEvidenceStats stats, double distanceScore, double lengthScore, double thicknessScore, double colorScore, double textureScore, double connectednessScore, double confusionPenalty)
    {
        if (evidence.Average() <= 0.000015)
        {
            return "no_hair_pixel_evidence";
        }

        if (distanceScore < 0.25)
        {
            return "eye_to_brow_distance_out_of_range";
        }

        if (lengthScore < 0.25)
        {
            return "brow_length_implausible";
        }

        if (thicknessScore < 0.25)
        {
            return "brow_thickness_implausible";
        }

        if (colorScore < 0.18)
        {
            return "brow_color_contrast_low";
        }

        if (textureScore < 0.18)
        {
            return "hair_like_texture_low";
        }

        if (connectednessScore < 0.18)
        {
            return "connectedness_low";
        }

        if (confusionPenalty > 0.30)
        {
            return "confusion_overlap_high";
        }

        return "low_confidence_protection_only";
    }

    private static EyeGeometry GetEyeGeometry(AnchorMeshFeature eyeFeature)
    {
        double angle = eyeFeature.AngleRad;
        double axisX = Math.Cos(angle);
        double axisY = Math.Sin(angle);
        double upX = Math.Sin(angle);
        double upY = -Math.Cos(angle);
        (double innerX, double innerY) = GetRolePoint(eyeFeature, "InnerCorner", eyeFeature.CenterX - axisX * eyeFeature.Width * 0.5, eyeFeature.CenterY - axisY * eyeFeature.Width * 0.5);
        (double outerX, double outerY) = GetRolePoint(eyeFeature, "OuterCorner", eyeFeature.CenterX + axisX * eyeFeature.Width * 0.5, eyeFeature.CenterY + axisY * eyeFeature.Width * 0.5);
        (double upperX, double upperY) = GetRolePoint(eyeFeature, "UpperLidCenter", eyeFeature.CenterX + upX * eyeFeature.Height * 0.5, eyeFeature.CenterY + upY * eyeFeature.Height * 0.5);
        (double lowerX, double lowerY) = GetRolePoint(eyeFeature, "LowerLidCenter", eyeFeature.CenterX - upX * eyeFeature.Height * 0.5, eyeFeature.CenterY - upY * eyeFeature.Height * 0.5);
        return new EyeGeometry(
            innerX,
            innerY,
            outerX,
            outerY,
            upperX,
            upperY,
            Math.Max(eyeFeature.Width, Distance(innerX, innerY, outerX, outerY)),
            Math.Max(4.0, Distance(upperX, upperY, lowerX, lowerY)));
    }

    private static (double X, double Y) GetRolePoint(AnchorMeshFeature feature, string roleKey, double fallbackX, double fallbackY)
    {
        AnchorMeshPoint? point = feature.Points.FirstOrDefault(candidate => candidate.Role.Contains(roleKey, StringComparison.OrdinalIgnoreCase));
        return point is null ? (fallbackX, fallbackY) : (point.SnappedX, point.SnappedY);
    }

    private static BrowPoint GetBrowEndpointCenter(AnchorMeshFeature browFeature, string endpointRole)
    {
        List<AnchorMeshPoint> endpointPoints = browFeature.Points
            .Where(point => point.Role.Contains(endpointRole, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (endpointPoints.Count == 0)
        {
            AnchorMeshPoint fallback = endpointRole.Contains("Outer", StringComparison.OrdinalIgnoreCase)
                ? browFeature.Points.OrderBy(point => point.SnappedX).First()
                : browFeature.Points.OrderBy(point => point.SnappedX).Last();
            if (browFeature.Name.Contains("Right", StringComparison.OrdinalIgnoreCase))
            {
                fallback = endpointRole.Contains("Outer", StringComparison.OrdinalIgnoreCase)
                    ? browFeature.Points.OrderBy(point => point.SnappedX).Last()
                    : browFeature.Points.OrderBy(point => point.SnappedX).First();
            }

            return new BrowPoint(fallback.SnappedX, fallback.SnappedY);
        }

        return new BrowPoint(endpointPoints.Average(point => point.SnappedX), endpointPoints.Average(point => point.SnappedY));
    }

    private static BrowEvidenceAxis EstimateAxis(MaskPlane evidenceMask, AnchorMeshFeature? browFeature, AnchorMeshFeature eyeFeature)
    {
        double total = 0;
        double meanX = 0;
        double meanY = 0;
        for (int y = 0; y < evidenceMask.Height; y++)
        {
            for (int x = 0; x < evidenceMask.Width; x++)
            {
                double weight = evidenceMask[x, y];
                if (weight <= 0.10)
                {
                    continue;
                }

                double px = x + 0.5;
                double py = y + 0.5;
                meanX += px * weight;
                meanY += py * weight;
                total += weight;
            }
        }

        if (total <= 0)
        {
            double fallbackAngle = browFeature?.AngleRad ?? eyeFeature.AngleRad;
            double fallbackAxisX = Math.Cos(fallbackAngle);
            double fallbackAxisY = Math.Sin(fallbackAngle);
            return new BrowEvidenceAxis(
                browFeature?.CenterX ?? eyeFeature.CenterX,
                browFeature?.CenterY ?? eyeFeature.CenterY - eyeFeature.Height * 1.5,
                fallbackAxisX,
                fallbackAxisY,
                Math.Sin(fallbackAngle),
                -Math.Cos(fallbackAngle),
                fallbackAngle);
        }

        meanX /= total;
        meanY /= total;
        double covXX = 0;
        double covYY = 0;
        double covXY = 0;
        for (int y = 0; y < evidenceMask.Height; y++)
        {
            for (int x = 0; x < evidenceMask.Width; x++)
            {
                double weight = evidenceMask[x, y];
                if (weight <= 0.10)
                {
                    continue;
                }

                double dx = x + 0.5 - meanX;
                double dy = y + 0.5 - meanY;
                covXX += dx * dx * weight;
                covYY += dy * dy * weight;
                covXY += dx * dy * weight;
            }
        }

        double angle = 0.5 * Math.Atan2(2 * covXY, covXX - covYY);
        double axisX = Math.Cos(angle);
        double axisY = Math.Sin(angle);
        if (axisX < 0)
        {
            axisX = -axisX;
            axisY = -axisY;
            angle += Math.PI;
        }

        return new BrowEvidenceAxis(meanX, meanY, axisX, axisY, -axisY, axisX, Math.Atan2(axisY, axisX));
    }

    private static List<BrowEvidenceSample> CollectSamples(MaskPlane evidenceMask, BrowEvidenceAxis axis)
    {
        List<BrowEvidenceSample> samples = new();
        for (int y = 0; y < evidenceMask.Height; y++)
        {
            for (int x = 0; x < evidenceMask.Width; x++)
            {
                double weight = evidenceMask[x, y];
                if (weight <= 0.10)
                {
                    continue;
                }

                double dx = x + 0.5 - axis.CenterX;
                double dy = y + 0.5 - axis.CenterY;
                samples.Add(new BrowEvidenceSample(
                    dx * axis.AxisX + dy * axis.AxisY,
                    dx * axis.UpX + dy * axis.UpY,
                    weight));
            }
        }

        return samples;
    }

    private static double EstimateEyeToBrowDistance(MaskPlane mask, AnchorMeshFeature eyeFeature)
    {
        double total = 0;
        double meanY = 0;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                double weight = mask[x, y];
                if (weight <= 0.03)
                {
                    continue;
                }

                meanY += y * weight;
                total += weight;
            }
        }

        return total <= 0 ? 0 : Math.Max(0, GetEyeGeometry(eyeFeature).UpperY - meanY / total);
    }

    private static double GetDirectionalHairScore(EyebrowPixelData pixels, int x, int y, double cos, double sin)
    {
        int alongX = Math.Clamp((int)Math.Round(x + cos * 2), 0, pixels.Width - 1);
        int alongY = Math.Clamp((int)Math.Round(y + sin * 2), 0, pixels.Height - 1);
        int crossX = Math.Clamp((int)Math.Round(x - sin * 2), 0, pixels.Width - 1);
        int crossY = Math.Clamp((int)Math.Round(y + cos * 2), 0, pixels.Height - 1);
        double center = pixels.GetLuma(x, y);
        double alongDiff = Math.Abs(center - pixels.GetLuma(alongX, alongY));
        double crossDiff = Math.Abs(center - pixels.GetLuma(crossX, crossY));
        return Math.Clamp((crossDiff - alongDiff + 8) / 28, 0, 1);
    }

    private static double GetNeighborhoodDarkness(EyebrowPixelData pixels, MaskPlane roi, int x, int y, double threshold)
    {
        int hits = 0;
        int count = 0;
        for (int yy = Math.Max(0, y - 1); yy <= Math.Min(pixels.Height - 1, y + 1); yy++)
        {
            for (int xx = Math.Max(0, x - 1); xx <= Math.Min(pixels.Width - 1, x + 1); xx++)
            {
                if (roi[xx, yy] <= 0.04)
                {
                    continue;
                }

                count++;
                if (pixels.GetLuma(xx, yy) <= threshold)
                {
                    hits++;
                }
            }
        }

        return count == 0 ? 0 : hits / (double)count;
    }

    private static double GetColorClusterScore(EyebrowPixelData pixels, int x, int y)
    {
        pixels.GetRgb(x, y, out byte red, out byte green, out byte blue);
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double saturation = max <= 1 ? 0 : (max - min) / max;
        double warmHair = red >= green - 12 && green >= blue - 18 ? 0.20 : 0.0;
        return Math.Clamp((1 - pixels.GetLuma(x, y) / 255.0) * 0.70 + saturation * 0.20 + warmHair, 0, 1);
    }

    private static double EstimateDirectionalTexture(MaskPlane evidence, BrowEvidenceAxis axis)
    {
        double sum = 0;
        double weight = 0;
        for (int y = 1; y < evidence.Height - 1; y++)
        {
            for (int x = 1; x < evidence.Width - 1; x++)
            {
                double center = evidence[x, y];
                if (center <= 0.10)
                {
                    continue;
                }

                int alongX = Math.Clamp((int)Math.Round(x + axis.AxisX * 2), 0, evidence.Width - 1);
                int alongY = Math.Clamp((int)Math.Round(y + axis.AxisY * 2), 0, evidence.Height - 1);
                int crossX = Math.Clamp((int)Math.Round(x + axis.UpX * 2), 0, evidence.Width - 1);
                int crossY = Math.Clamp((int)Math.Round(y + axis.UpY * 2), 0, evidence.Height - 1);
                double along = Math.Abs(center - evidence[alongX, alongY]);
                double cross = Math.Abs(center - evidence[crossX, crossY]);
                sum += Math.Clamp(center + cross * 0.5 - along * 0.25, 0, 1) * center;
                weight += center;
            }
        }

        return weight <= 0 ? 0 : Math.Clamp(sum / weight, 0, 1);
    }

    private static double EstimateConnectedness(MaskPlane evidence, IReadOnlyList<BrowEvidenceSample> samples, double length, double thickness)
    {
        if (samples.Count == 0)
        {
            return 0;
        }

        double strong = samples.Count(sample => sample.Weight > 0.35) / (double)samples.Count;
        double density = samples.Sum(sample => sample.Weight) / Math.Max(1.0, length * Math.Max(1.0, thickness));
        return Math.Clamp(strong * 0.55 + density * 4.0 * 0.45, 0, 1);
    }

    private static double EstimateThickness(IReadOnlyList<BrowEvidenceSample> samples, double minS, double maxS)
    {
        double startU = WeightedBandAverage(samples, minS + (maxS - minS) * 0.08, (maxS - minS) * 0.18);
        double midU = WeightedBandAverage(samples, (minS + maxS) * 0.5, (maxS - minS) * 0.22);
        double endU = WeightedBandAverage(samples, maxS - (maxS - minS) * 0.08, (maxS - minS) * 0.18);
        double weightedDeviation = 0;
        double total = 0;
        foreach (BrowEvidenceSample sample in samples)
        {
            double predicted = EvaluateQuadraticU(sample.S, minS, startU, (minS + maxS) * 0.5, midU, maxS, endU);
            weightedDeviation += Math.Abs(sample.U - predicted) * sample.Weight;
            total += sample.Weight;
        }

        return Math.Max(4.0, total <= 0 ? 4.0 : weightedDeviation / total * 3.6);
    }

    private static double WeightedBandAverage(IReadOnlyList<BrowEvidenceSample> samples, double centerS, double radiusS)
    {
        double weighted = 0;
        double total = 0;
        double radius = Math.Max(1.0, radiusS);
        foreach (BrowEvidenceSample sample in samples)
        {
            double distance = Math.Abs(sample.S - centerS);
            if (distance > radius)
            {
                continue;
            }

            double bandWeight = sample.Weight * (1 - distance / radius);
            weighted += sample.U * bandWeight;
            total += bandWeight;
        }

        return total <= 0 ? samples.Average(sample => sample.U) : weighted / total;
    }

    private static double EvaluateQuadraticU(double s, double startS, double startU, double controlS, double controlU, double endS, double endU)
    {
        double denominator = Math.Max(1.0, endS - startS);
        double t = Math.Clamp((s - startS) / denominator, 0, 1);
        double oneMinusT = 1 - t;
        return oneMinusT * oneMinusT * startU + 2 * oneMinusT * t * controlU + t * t * endU;
    }

    private static MaskPlane FeatherSmallMask(MaskPlane source, int radius)
    {
        MaskPlane result = source.Clone();
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double max = source[x, y];
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(source.Height - 1, y + radius); yy++)
                {
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(source.Width - 1, x + radius); xx++)
                    {
                        double distance = Math.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                        if (distance > radius + 0.001)
                        {
                            continue;
                        }

                        max = Math.Max(max, source[xx, yy] * (1 - distance / (radius + 1.0)));
                    }
                }

                result[x, y] = Math.Clamp(max, 0, 1);
            }
        }

        return result;
    }

    private static MaskBounds GetMaskBounds(MaskPlane mask)
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

        return new MaskBounds(left, top, right, bottom);
    }

    private static double AverageMask(MaskPlane mask)
    {
        double sum = 0;
        double count = 0;
        for (int index = 0; index < mask.Values.Length; index++)
        {
            if (mask.Values[index] <= 0)
            {
                continue;
            }

            sum += mask.Values[index];
            count++;
        }

        return count <= 0 ? 0 : Math.Clamp(sum / count, 0, 1);
    }

    private static double OverlapRatio(MaskPlane source, MaskPlane other)
    {
        MaskPlane.EnsureSameSize(source, other);
        double overlap = 0;
        double total = 0;
        for (int index = 0; index < source.Values.Length; index++)
        {
            double weight = source.Values[index];
            if (weight <= 0.02)
            {
                continue;
            }

            total += weight;
            overlap += Math.Min(weight, other.Values[index]);
        }

        return total <= 0 ? 0 : overlap / total;
    }

    private static EyebrowCandidate EmptyCandidate(string side, MaskPlane roi, string reason)
    {
        MaskPlane empty = MaskPlane.Empty(roi.Width, roi.Height);
        return new EyebrowCandidate(
            side,
            false,
            empty,
            roi,
            0,
            reason,
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
            0);
    }

    private static EyebrowPixelData? TryCreatePixelData(BitmapSource? source, int width, int height)
    {
        if (source is null || source.PixelWidth != width || source.PixelHeight != height)
        {
            return null;
        }

        try
        {
            BitmapSource bitmap = source.Format == PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
            bitmap.Freeze();
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);
            return new EyebrowPixelData(width, height, stride, pixels);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static void ValidateOptionalMask(MaskPlane? mask, int width, int height, string name)
    {
        if (mask is not null && (mask.Width != width || mask.Height != height))
        {
            throw new InvalidOperationException(name + " size must match source image.");
        }
    }

    private static double ScoreRange(double value, double goodMin, double goodMax, double softMin, double softMax)
    {
        if (value >= goodMin && value <= goodMax)
        {
            return 1;
        }

        if (value < softMin || value > softMax)
        {
            return 0;
        }

        if (value < goodMin)
        {
            return Math.Clamp((value - softMin) / Math.Max(0.0001, goodMin - softMin), 0, 1);
        }

        return Math.Clamp((softMax - value) / Math.Max(0.0001, softMax - goodMax), 0, 1);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = ax - bx;
        double dy = ay - by;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2;
        }

        while (angle < -Math.PI)
        {
            angle += Math.PI * 2;
        }

        return angle;
    }

    private sealed record EyebrowPixelData(int Width, int Height, int Stride, byte[] Pixels)
    {
        public double GetLuma(int x, int y)
        {
            int index = y * Stride + x * 4;
            return Pixels[index + 2] * 0.299 + Pixels[index + 1] * 0.587 + Pixels[index] * 0.114;
        }

        public void GetRgb(int x, int y, out byte red, out byte green, out byte blue)
        {
            int index = y * Stride + x * 4;
            blue = Pixels[index];
            green = Pixels[index + 1];
            red = Pixels[index + 2];
        }
    }

    private sealed record SkinReference(double Red, double Green, double Blue, double Luma, double Confidence);

    private sealed record BrowEvidenceStats(
        BrowEvidenceAxis Axis,
        IReadOnlyList<BrowEvidenceSample> Samples,
        double BrowToEyeDistance,
        double BrowLength,
        double BrowThickness,
        double BrowSlopeAngle,
        double ArchScore,
        double ColorScore,
        double TextureScore,
        double ConnectednessScore,
        double EyeW,
        double EyeH);

    private sealed record BrowEvidenceSample(double S, double U, double Weight);

    private sealed record BrowEvidenceAxis(double CenterX, double CenterY, double AxisX, double AxisY, double UpX, double UpY, double AngleRad);

    private sealed record BrowPoint(double X, double Y);

    private readonly record struct EyeGeometry(double InnerX, double InnerY, double OuterX, double OuterY, double UpperX, double UpperY, double EyeW, double EyeH);

    private readonly record struct MaskBounds(int Left, int Top, int Right, int Bottom)
    {
        public bool IsValid => Right >= Left && Bottom >= Top;
    }
}
