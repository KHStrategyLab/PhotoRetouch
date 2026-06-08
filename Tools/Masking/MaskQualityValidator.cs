using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class MaskQualityValidator
{
    public static MaskQualityReport Validate(FaceAnalysisResult analysis, FaceMaskSet masks)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(masks);

        List<string> warnings = new(analysis.DebugWarnings);
        List<string> fatalErrors = new();
        FaceMetrics metrics = FaceMetrics.From(analysis.FaceBox, masks.SkinMask.Width, masks.SkinMask.Height);

        double faceScore = ValidateFace(analysis, metrics, warnings, fatalErrors);
        double landmarkScore = ValidateLandmarks(analysis, warnings, fatalErrors);
        double parsingScore = Clamp01(analysis.ParsingConfidence);
        if (parsingScore < 0.35)
        {
            warnings.Add("parsing_quality_low");
        }

        double skinScore = ValidateSkinMask(masks, metrics, warnings);
        double eyeScore = ValidateEyeMask(analysis, masks, metrics, warnings, fatalErrors);
        double eyebrowScore = ValidateEyebrowMask(masks, metrics, warnings);
        double lipScore = ValidateLipMask(analysis, masks, metrics, warnings);
        double nostrilScore = ValidateNostrilMask(masks, metrics, warnings);
        double hairScore = ValidateHairMask(masks, metrics, warnings);
        double hardProtectScore = ValidateHardProtect(masks, warnings, fatalErrors);
        double retouchAllowScore = ValidateRetouchAllow(masks, metrics, warnings, fatalErrors);

        double overall = Clamp01(
            faceScore * 0.16 +
            landmarkScore * 0.13 +
            parsingScore * 0.09 +
            skinScore * 0.14 +
            eyeScore * 0.11 +
            eyebrowScore * 0.07 +
            lipScore * 0.09 +
            nostrilScore * 0.08 +
            hairScore * 0.04 +
            hardProtectScore * 0.05 +
            retouchAllowScore * 0.04);

        if (fatalErrors.Count > 0)
        {
            overall = Math.Min(overall, 0.24);
        }

        int maxAllowedStage = CalculateMaxAllowedStage(overall, warnings, fatalErrors);
        bool isUsable = fatalErrors.Count == 0 && overall >= 0.45;
        bool isSafeForStrongRetouch = fatalErrors.Count == 0 && overall >= 0.70 && maxAllowedStage >= 7;

        return new MaskQualityReport(
            overall,
            faceScore,
            landmarkScore,
            parsingScore,
            skinScore,
            eyeScore,
            eyebrowScore,
            lipScore,
            nostrilScore,
            hairScore,
            hardProtectScore,
            retouchAllowScore,
            isUsable,
            maxAllowedStage,
            isSafeForStrongRetouch,
            warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            fatalErrors.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static double ValidateFace(FaceAnalysisResult analysis, FaceMetrics metrics, List<string> warnings, List<string> fatalErrors)
    {
        double score = Clamp01(analysis.FaceQualityScore);
        if (analysis.FaceBox.Width <= 0 || analysis.FaceBox.Height <= 0)
        {
            fatalErrors.Add("face_not_found");
            return 0;
        }

        if (metrics.FaceAreaRatio < 0.015)
        {
            warnings.Add("face_too_small");
            score -= 0.25;
        }

        if (metrics.FaceTouchesImageEdge)
        {
            warnings.Add("face_clipped_or_near_edge");
            score -= 0.16;
        }

        if (Math.Abs(analysis.FaceAngle) > 18)
        {
            warnings.Add("face_angle_high");
            score -= 0.20;
        }

        return Clamp01(score);
    }

    private static double ValidateLandmarks(FaceAnalysisResult analysis, List<string> warnings, List<string> fatalErrors)
    {
        double score = Clamp01(analysis.LandmarkConfidence);
        bool hasLeftEye = TryGetPoint(analysis, "left_eye", out WpfPoint leftEye);
        bool hasRightEye = TryGetPoint(analysis, "right_eye", out WpfPoint rightEye);
        bool hasNose = TryGetPoint(analysis, "nose_tip", out WpfPoint noseTip);
        bool hasMouth = TryGetPoint(analysis, "mouth_center", out WpfPoint mouthCenter);
        bool hasChin = TryGetPoint(analysis, "chin", out WpfPoint chin);

        if (!hasLeftEye || !hasRightEye || !hasNose || !hasMouth || !hasChin)
        {
            warnings.Add("landmark_missing_required_points");
            score -= 0.28;
        }

        if (hasLeftEye && hasRightEye)
        {
            if (leftEye.X >= rightEye.X)
            {
                fatalErrors.Add("landmark_eye_order_invalid");
                score -= 0.55;
            }

            double eyeDistance = Math.Abs(rightEye.X - leftEye.X);
            if (eyeDistance < analysis.FaceBox.Width * 0.18 || eyeDistance > analysis.FaceBox.Width * 0.82)
            {
                warnings.Add("landmark_eye_distance_invalid");
                score -= 0.18;
            }
        }

        if (hasNose && hasLeftEye && hasRightEye && noseTip.Y < Math.Min(leftEye.Y, rightEye.Y))
        {
            warnings.Add("landmark_nose_above_eyes");
            score -= 0.20;
        }

        if (hasMouth && hasNose && mouthCenter.Y < noseTip.Y)
        {
            warnings.Add("landmark_mouth_above_nose");
            score -= 0.24;
        }

        if (hasChin && hasMouth && chin.Y < mouthCenter.Y)
        {
            warnings.Add("landmark_chin_above_mouth");
            score -= 0.20;
        }

        foreach ((string name, WpfPoint point) in analysis.FaceLandmarks)
        {
            if (!IsInside(analysis.FaceBox, point))
            {
                warnings.Add("landmark_outside_facebox_" + name);
                score -= 0.04;
            }
        }

        return Clamp01(score);
    }

    private static double ValidateSkinMask(FaceMaskSet masks, FaceMetrics metrics, List<string> warnings)
    {
        double skinRatio = masks.SkinMask.Average();
        double score = ScoreArea(skinRatio, metrics.FaceAreaRatio * 0.28, metrics.FaceAreaRatio * 1.10, "skin_mask_area", warnings);
        double skinHardOverlap = OverlapRatio(masks.SkinMask, masks.HardProtectMask);
        if (skinHardOverlap > 0.18)
        {
            warnings.Add("skin_mask_overlaps_hard_protect");
            score -= 0.22;
        }

        double skinHairOverlap = OverlapRatio(masks.SkinMask, masks.HairMask);
        if (skinHairOverlap > 0.12)
        {
            warnings.Add("skin_mask_overlaps_hair");
            score -= 0.16;
        }

        return Clamp01(score);
    }

    private static double ValidateEyeMask(FaceAnalysisResult analysis, FaceMaskSet masks, FaceMetrics metrics, List<string> warnings, List<string> fatalErrors)
    {
        double score = ScoreArea(masks.EyeMask.Average(), metrics.FaceAreaRatio * 0.006, metrics.FaceAreaRatio * 0.13, "eye_mask_area", warnings);
        if (OverlapRatio(masks.EyeMask, masks.RetouchAllowMask) > 0.015)
        {
            fatalErrors.Add("eye_mask_inside_retouch_allow");
            score -= 0.55;
        }

        if (TryGetPoint(analysis, "left_eye", out WpfPoint leftEye) &&
            TryGetPoint(analysis, "right_eye", out WpfPoint rightEye))
        {
            if (MaskValueNear(masks.EyeMask, leftEye) < 0.08 || MaskValueNear(masks.EyeMask, rightEye) < 0.08)
            {
                warnings.Add("eye_mask_landmark_overlap_low");
                score -= 0.22;
            }
        }

        return Clamp01(score);
    }

    private static double ValidateEyebrowMask(FaceMaskSet masks, FaceMetrics metrics, List<string> warnings)
    {
        double score = ScoreArea(masks.EyebrowMask.Average(), metrics.FaceAreaRatio * 0.002, metrics.FaceAreaRatio * 0.12, "eyebrow_mask_area", warnings);
        if (OverlapRatio(masks.EyebrowMask, masks.RetouchAllowMask) > 0.02)
        {
            warnings.Add("eyebrow_mask_inside_retouch_allow");
            score -= 0.28;
        }

        return Clamp01(score);
    }

    private static double ValidateLipMask(FaceAnalysisResult analysis, FaceMaskSet masks, FaceMetrics metrics, List<string> warnings)
    {
        double score = ScoreArea(masks.LipMask.Average(), metrics.FaceAreaRatio * 0.003, metrics.FaceAreaRatio * 0.12, "lip_mask_area", warnings);
        if (OverlapRatio(masks.LipMask, masks.RetouchAllowMask) > 0.02)
        {
            warnings.Add("lip_mask_inside_retouch_allow");
            score -= 0.28;
        }

        if (TryGetPoint(analysis, "mouth_center", out WpfPoint mouth) && MaskValueNear(masks.LipMask, mouth) < 0.04)
        {
            warnings.Add("lip_mask_landmark_overlap_low");
            score -= 0.16;
        }

        if (OverlapRatio(masks.LipMask, masks.NoseMask) > 0.28)
        {
            warnings.Add("lip_mask_overlaps_nose");
            score -= 0.20;
        }

        return Clamp01(score);
    }

    private static double ValidateNostrilMask(FaceMaskSet masks, FaceMetrics metrics, List<string> warnings)
    {
        double score = ScoreArea(masks.NostrilMask.Average(), metrics.FaceAreaRatio * 0.00008, metrics.FaceAreaRatio * 0.035, "nostril_mask_area", warnings);
        if (OverlapRatio(masks.NostrilMask, masks.RetouchAllowMask) > 0.005)
        {
            warnings.Add("nostril_mask_inside_retouch_allow");
            score -= 0.32;
        }

        if (OverlapRatio(masks.NostrilMask, masks.LipMask) > 0.12)
        {
            warnings.Add("nostril_lip_overlap_risk");
            score -= 0.22;
        }

        return Clamp01(score);
    }

    private static double ValidateHairMask(FaceMaskSet masks, FaceMetrics metrics, List<string> warnings)
    {
        double hairRatio = masks.HairMask.Average();
        if (hairRatio <= 0.00001)
        {
            warnings.Add("hair_mask_missing");
            return 0.58;
        }

        double score = ScoreArea(hairRatio, 0.00001, metrics.FaceAreaRatio * 1.2, "hair_mask_area", warnings);
        if (OverlapRatio(masks.HairMask, masks.RetouchAllowMask) > 0.02)
        {
            warnings.Add("hair_mask_inside_retouch_allow");
            score -= 0.24;
        }

        return Clamp01(score);
    }

    private static double ValidateHardProtect(FaceMaskSet masks, List<string> warnings, List<string> fatalErrors)
    {
        double score = 1;
        if (masks.HardProtectMask.Average() <= 0.0001)
        {
            fatalErrors.Add("hard_protect_mask_empty");
            return 0;
        }

        double protectRetouchOverlap = OverlapRatio(masks.HardProtectMask, masks.RetouchAllowMask);
        if (protectRetouchOverlap > 0.001)
        {
            fatalErrors.Add("hard_protect_overlaps_retouch_allow");
            score -= 0.55;
        }

        return Clamp01(score);
    }

    private static double ValidateRetouchAllow(FaceMaskSet masks, FaceMetrics metrics, List<string> warnings, List<string> fatalErrors)
    {
        double score = ScoreArea(masks.RetouchAllowMask.Average(), metrics.FaceAreaRatio * 0.08, metrics.FaceAreaRatio * 1.05, "retouch_allow_area", warnings);
        if (OverlapRatio(masks.RetouchAllowMask, masks.HardProtectMask) > 0.001)
        {
            fatalErrors.Add("retouch_allow_overlaps_hard_protect");
            score -= 0.65;
        }

        if (masks.RetouchAllowMask.Average() <= 0.0001)
        {
            fatalErrors.Add("retouch_allow_mask_empty");
            score = 0;
        }

        return Clamp01(score);
    }

    private static int CalculateMaxAllowedStage(double score, IReadOnlyList<string> warnings, IReadOnlyList<string> fatalErrors)
    {
        if (fatalErrors.Count > 0)
        {
            return 1;
        }

        int maxStage = score switch
        {
            >= 0.85 => 10,
            >= 0.70 => 8,
            >= 0.50 => 6,
            _ => 3
        };

        foreach (string warning in warnings)
        {
            if (warning.Contains("eye", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("lip", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("nostril", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("hard_protect", StringComparison.OrdinalIgnoreCase))
            {
                maxStage = Math.Min(maxStage, 6);
            }

            if (warning.Contains("low_confidence", StringComparison.OrdinalIgnoreCase) ||
                warning.Contains("missing", StringComparison.OrdinalIgnoreCase))
            {
                maxStage = Math.Min(maxStage, 7);
            }
        }

        return Math.Clamp(maxStage - Math.Min(2, warnings.Count / 6), 1, 10);
    }

    private static double ScoreArea(double area, double min, double max, string warning, List<string> warnings)
    {
        if (area < min)
        {
            warnings.Add(warning + "_too_small");
            return Clamp01(area / Math.Max(min, 0.000001));
        }

        if (area > max)
        {
            warnings.Add(warning + "_too_large");
            return Clamp01(max / Math.Max(area, 0.000001));
        }

        return 1;
    }

    private static double OverlapRatio(MaskPlane mask, MaskPlane other)
    {
        MaskPlane.EnsureSameSize(mask, other);
        double overlap = 0;
        double area = 0;
        for (int index = 0; index < mask.Values.Length; index++)
        {
            area += mask.Values[index];
            overlap += Math.Min(mask.Values[index], other.Values[index]);
        }

        return area <= 0 ? 0 : overlap / area;
    }

    private static double MaskValueNear(MaskPlane mask, WpfPoint point)
    {
        int centerX = Math.Clamp((int)Math.Round(point.X), 0, mask.Width - 1);
        int centerY = Math.Clamp((int)Math.Round(point.Y), 0, mask.Height - 1);
        double max = 0;
        for (int y = Math.Max(0, centerY - 3); y <= Math.Min(mask.Height - 1, centerY + 3); y++)
        {
            for (int x = Math.Max(0, centerX - 3); x <= Math.Min(mask.Width - 1, centerX + 3); x++)
            {
                max = Math.Max(max, mask[x, y]);
            }
        }

        return max;
    }

    private static bool TryGetPoint(FaceAnalysisResult analysis, string key, out WpfPoint point)
    {
        return analysis.FaceLandmarks.TryGetValue(key, out point);
    }

    private static bool IsInside(Int32Rect rect, WpfPoint point)
    {
        return point.X >= rect.X &&
               point.Y >= rect.Y &&
               point.X < rect.X + rect.Width &&
               point.Y < rect.Y + rect.Height;
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private sealed record FaceMetrics(Int32Rect FaceBox, int Width, int Height)
    {
        public double FaceAreaRatio => FaceBox.Width * FaceBox.Height / (double)Math.Max(1, Width * Height);

        public bool FaceTouchesImageEdge =>
            FaceBox.X <= 1 ||
            FaceBox.Y <= 1 ||
            FaceBox.X + FaceBox.Width >= Width - 1 ||
            FaceBox.Y + FaceBox.Height >= Height - 1;

        public static FaceMetrics From(Int32Rect faceBox, int width, int height)
        {
            return new FaceMetrics(faceBox, width, height);
        }
    }
}
