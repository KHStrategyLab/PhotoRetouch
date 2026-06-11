using PhotoRetouch.AnchorMesh;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record AnchorMeshFeatureMaskSet(
    MaskPlane EyeMask,
    MaskPlane EyebrowMask,
    MaskPlane LipMask,
    MaskPlane InnerMouthMask,
    MaskPlane NoseMask,
    MaskPlane NoseSkinMask,
    MaskPlane NostrilMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    MaskPlane FaceGuideMask,
    IReadOnlyList<string> DebugWarnings);

public static class AnchorMeshFeatureMaskBuilder
{
    private static readonly Dictionary<string, TemplateMask?> TemplateCache = new(StringComparer.OrdinalIgnoreCase);

    public static AnchorMeshFeatureMaskSet Build(int width, int height, AnchorMeshResult? anchorMesh, BitmapSource? source = null)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        List<string> warnings = new();
        if (anchorMesh?.IsValid != true || anchorMesh.Features is null)
        {
            warnings.Add("anchor_mesh_feature_masks_unavailable");
            return new AnchorMeshFeatureMaskSet(
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                warnings);
        }

        AnchorMeshFeatureSet features = anchorMesh.Features;
        MaskPlane leftEye = BuildEyeProtectMask(width, height, features.LeftEye, 1.0);
        MaskPlane rightEye = BuildEyeProtectMask(width, height, features.RightEye, 1.0);
        MaskPlane eyeMask = MaskPlane.Union(leftEye, rightEye);

        EyebrowAnalysisResult eyebrowAnalysis = EyebrowAnalyzer.Analyze(CreateEyebrowAnalyzerInput(width, height, anchorMesh, source));
        warnings.AddRange(eyebrowAnalysis.DebugOverlayData);
        MaskPlane eyebrowMask = MaskPlane.Subtract(eyebrowAnalysis.EyebrowProtectionMask, eyeMask);

        MaskPlane innerMouthMask = BuildInnerMouthProtectMask(width, height, features.LipInner ?? features.LipOuter, 1.0);
        MaskPlane lipMask = BuildLipSurfaceProtectMask(width, height, features.LipOuter, features.LipInner, innerMouthMask, warnings);

        MaskPlane nostrilMask = BuildNostrilMask(width, height, features.Nose);
        MaskPlane noseMask = BuildNoseSurfaceMask(width, height, features.Nose, nostrilMask, warnings);
        MaskPlane noseSkinMask = MaskPlane.Subtract(MaskPlane.Multiply(noseMask, 0.62), nostrilMask);
        ClipMouthMaskByNoseMouthDistance(lipMask, features.Nose, features.LipOuter, warnings);
        ClipMouthMaskByNoseMouthDistance(innerMouthMask, features.Nose, features.LipInner ?? features.LipOuter, warnings);
        lipMask = MaskPlane.Subtract(lipMask, nostrilMask);
        innerMouthMask = MaskPlane.Subtract(innerMouthMask, nostrilMask);
        MaskPlane faceGuideMask = FillClosedFeature(width, height, features.FaceOutline, 0.72, 4.0);

        MaskPlane hardProtect = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            nostrilMask);
        MaskPlane softProtect = MaskPlane.Subtract(noseMask, hardProtect);

        if (hardProtect.Average() <= 0.0001)
        {
            warnings.Add("anchor_mesh_hard_protect_empty");
        }
        else
        {
            warnings.Add("anchor_mesh_component_masks_snapped_to_feature_landmarks");
        }

        return new AnchorMeshFeatureMaskSet(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            noseMask,
            noseSkinMask,
            nostrilMask,
            softProtect,
            hardProtect,
            faceGuideMask,
            warnings);
    }

    private static EyebrowAnalyzerInput CreateEyebrowAnalyzerInput(int width, int height, AnchorMeshResult anchorMesh, BitmapSource? source)
    {
        AnchorMeshFeatureSet features = anchorMesh.Features;
        double faceW = features.FaceOutline?.Width > 1 ? features.FaceOutline.Width : width;
        double faceH = features.FaceOutline?.Height > 1 ? features.FaceOutline.Height : height;
        double faceCenterX = features.FaceOutline?.CenterX ?? width * 0.5;
        double eyeLineAngle = CalculateEyeLineAngle(features.LeftEye, features.RightEye);
        double frontalPoseConfidence = anchorMesh.Pose?.Confidence ?? anchorMesh.Confidence;
        if (frontalPoseConfidence <= 0)
        {
            frontalPoseConfidence = 0.72;
        }

        return new EyebrowAnalyzerInput(
            width,
            height,
            source,
            features.LeftEye,
            features.RightEye,
            features.LeftPupil,
            features.RightPupil,
            features.LeftBrow,
            features.RightBrow,
            faceW,
            faceH,
            faceCenterX,
            eyeLineAngle,
            Math.Clamp(frontalPoseConfidence, 0, 1));
    }

    private static double CalculateEyeLineAngle(AnchorMeshFeature? leftEye, AnchorMeshFeature? rightEye)
    {
        if (leftEye is null || rightEye is null)
        {
            return leftEye?.AngleRad ?? rightEye?.AngleRad ?? 0;
        }

        return Math.Atan2(rightEye.CenterY - leftEye.CenterY, rightEye.CenterX - leftEye.CenterX);
    }

    private static MaskPlane BuildEyeProtectMask(int width, int height, AnchorMeshFeature? eyeFeature, double opacity)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (eyeFeature is null || eyeFeature.Points.Count == 0)
        {
            return mask;
        }

        double radiusX = Math.Clamp(eyeFeature.Width * 0.54, 8.0, 58.0);
        double radiusY = Math.Clamp(Math.Max(eyeFeature.Height * 0.42, eyeFeature.Width * 0.105), 4.0, 22.0);
        bool isRightEye = eyeFeature.Name.Equals("RightEye", StringComparison.OrdinalIgnoreCase);
        string templateName = isRightEye ? "right_eye_protect.png" : "left_eye_protect.png";
        if (AddImageTemplate(
            mask,
            templateName,
            eyeFeature.CenterX,
            eyeFeature.CenterY,
            radiusX,
            radiusY,
            eyeFeature.AngleRad,
            opacity,
            flipX: false)
            || (isRightEye && AddImageTemplate(
                mask,
                "left_eye_protect.png",
                eyeFeature.CenterX,
                eyeFeature.CenterY,
                radiusX,
                radiusY,
                eyeFeature.AngleRad,
                opacity,
                flipX: true)))
        {
            return mask;
        }

        AddTemplateShape(
            mask,
            eyeFeature.CenterX,
            eyeFeature.CenterY,
            radiusX,
            radiusY,
            eyeFeature.AngleRad,
            opacity,
            featherRadius: 2.0,
            ShapeEyeAlmond);
        return mask;
    }

    private static MaskPlane BuildBrowProtectMask(
        int width,
        int height,
        AnchorMeshFeature? browFeature,
        AnchorMeshFeature? eyeFeature,
        AnchorMeshFeature? pupilFeature,
        double opacity,
        bool isRightBrow,
        ImagePixelData? pixelData,
        List<string> warnings)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (browFeature is null || browFeature.Points.Count == 0)
        {
            return mask;
        }

        double centerShift = isRightBrow ? -browFeature.Width * 0.03 : browFeature.Width * 0.03;
        double roiCenterX = browFeature.CenterX + Math.Cos(browFeature.AngleRad) * centerShift;
        double roiCenterY = browFeature.CenterY;
        double roiRadiusX = Math.Clamp(browFeature.Width * 0.66, 14.0, 84.0);
        double roiRadiusY = Math.Clamp(Math.Max(browFeature.Height * 1.20, browFeature.Width * 0.145), 7.0, 28.0);

        MaskPlane roi = MaskPlane.Empty(width, height);
        AddTemplateShape(
            roi,
            roiCenterX,
            roiCenterY,
            roiRadiusX,
            roiRadiusY,
            browFeature.AngleRad,
            1.0,
            featherRadius: 2.0,
            isRightBrow ? ShapeRightBrowTemplate : ShapeLeftBrowTemplate);

        if (pixelData is not null)
        {
            ClipBrowRoiByOrbitalArcBand(roi, eyeFeature, pupilFeature, warnings, isRightBrow);
            ClipBrowRoiByEyeDistanceBand(roi, browFeature, eyeFeature, warnings, isRightBrow);
            MaskPlane evidenceMask = BuildBrowPixelEvidenceMask(roi, browFeature, pixelData, opacity);
            if (evidenceMask.Average() > 0.000015)
            {
                warnings.Add((isRightBrow ? "right" : "left") + "_eyebrow_pixel_evidence_mask_only_no_brush_cover");
                return MaskPlane.Intersect(evidenceMask, roi);
            }

            warnings.Add((isRightBrow ? "right" : "left") + "_eyebrow_absent_or_pixel_evidence_low_no_anchor_line_drawn");
            return mask;
        }
        else
        {
            warnings.Add((isRightBrow ? "right" : "left") + "_eyebrow_pixel_evidence_unavailable_no_anchor_shape_fallback");
        }

        return mask;
    }

    private static void ClipBrowRoiByEyeDistanceBand(MaskPlane mask, AnchorMeshFeature browFeature, AnchorMeshFeature? eyeFeature, List<string> warnings, bool isRightBrow)
    {
        if (eyeFeature is null || eyeFeature.Points.Count == 0 || browFeature.Width <= 1)
        {
            return;
        }

        double upX = Math.Sin(browFeature.AngleRad);
        double upY = -Math.Cos(browFeature.AngleRad);
        double minUp = Math.Clamp(eyeFeature.Width * 0.14, 6.0, 24.0);
        double maxUp = Math.Clamp(eyeFeature.Width * 1.04, 38.0, 104.0);
        double maxSide = Math.Clamp(browFeature.Width * 0.70, 28.0, 98.0);
        double axisX = Math.Cos(browFeature.AngleRad);
        double axisY = Math.Sin(browFeature.AngleRad);

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y] <= 0)
                {
                    continue;
                }

                double dx = x + 0.5 - eyeFeature.CenterX;
                double dy = y + 0.5 - eyeFeature.CenterY;
                double up = dx * upX + dy * upY;
                double side = dx * axisX + dy * axisY;
                if (up < minUp || up > maxUp || Math.Abs(side) > maxSide)
                {
                    mask[x, y] = 0;
                }
            }
        }

        warnings.Add((isRightBrow ? "right" : "left") + "_eyebrow_roi_clipped_by_eye_distance_ratio_band");
    }

    private static void ClipBrowRoiByOrbitalArcBand(MaskPlane mask, AnchorMeshFeature? eyeFeature, AnchorMeshFeature? pupilFeature, List<string> warnings, bool isRightBrow)
    {
        if (eyeFeature is null || eyeFeature.Points.Count == 0 || eyeFeature.Width <= 1)
        {
            return;
        }

        double eyeAngle = eyeFeature.AngleRad;
        double axisX = Math.Cos(eyeAngle);
        double axisY = Math.Sin(eyeAngle);
        double upX = Math.Sin(eyeAngle);
        double upY = -Math.Cos(eyeAngle);

        (double eyeInnerX, double eyeInnerY) = GetEyeRolePoint(eyeFeature, "InnerCorner", eyeFeature.CenterX - axisX * eyeFeature.Width * 0.5, eyeFeature.CenterY - axisY * eyeFeature.Width * 0.5);
        (double eyeOuterX, double eyeOuterY) = GetEyeRolePoint(eyeFeature, "OuterCorner", eyeFeature.CenterX + axisX * eyeFeature.Width * 0.5, eyeFeature.CenterY + axisY * eyeFeature.Width * 0.5);
        (double eyeUpperX, double eyeUpperY) = GetEyeRolePoint(eyeFeature, "UpperLidCenter", eyeFeature.CenterX + upX * eyeFeature.Height * 0.5, eyeFeature.CenterY + upY * eyeFeature.Height * 0.5);
        (double eyeLowerX, double eyeLowerY) = GetEyeRolePoint(eyeFeature, "LowerLidCenter", eyeFeature.CenterX - upX * eyeFeature.Height * 0.5, eyeFeature.CenterY - upY * eyeFeature.Height * 0.5);

        double eyeW = Math.Max(eyeFeature.Width, Distance(eyeInnerX, eyeInnerY, eyeOuterX, eyeOuterY));
        double eyeH = Math.Max(4.0, Distance(eyeUpperX, eyeUpperY, eyeLowerX, eyeLowerY));
        double pupilWeight = pupilFeature?.Points.Count > 0 ? 0.35 : 0.0;
        double orbitalCenterX = eyeFeature.CenterX * (1 - pupilWeight) + (pupilFeature?.CenterX ?? eyeFeature.CenterX) * pupilWeight;
        double orbitalCenterY = eyeFeature.CenterY * (1 - pupilWeight) + (pupilFeature?.CenterY ?? eyeFeature.CenterY) * pupilWeight;

        double minUp = Math.Clamp(Math.Max(eyeH * 0.28, eyeW * 0.055), 4.0, 22.0);
        double maxUp = Math.Clamp(Math.Max(eyeH * 2.55, eyeW * 0.58), 30.0, 108.0);
        double maxSide = Math.Clamp(eyeW * 0.88, 18.0, 96.0);
        double arcHalfBand = Math.Clamp(Math.Max(eyeH * 1.05, eyeW * 0.22), 12.0, 42.0);

        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y] <= 0)
                {
                    continue;
                }

                double px = x + 0.5;
                double py = y + 0.5;
                double side = (px - orbitalCenterX) * axisX + (py - orbitalCenterY) * axisY;
                double upFromUpperLid = (px - eyeUpperX) * upX + (py - eyeUpperY) * upY;
                if (Math.Abs(side) > maxSide || upFromUpperLid < minUp || upFromUpperLid > maxUp)
                {
                    mask[x, y] = 0;
                    continue;
                }

                double sideNorm = Math.Clamp(side / Math.Max(1.0, maxSide), -1.0, 1.0);
                double archLift = Math.Sin((sideNorm + 1.0) * Math.PI * 0.5) * eyeH * 0.34;
                double preferredUp = minUp + (maxUp - minUp) * 0.45 + archLift;
                double arcDistance = Math.Abs(upFromUpperLid - preferredUp);
                double arcWeight = 1.0 - SmoothStep(arcHalfBand, arcHalfBand * 1.85, arcDistance);
                if (arcWeight <= 0.04)
                {
                    mask[x, y] = 0;
                    continue;
                }

                mask[x, y] *= arcWeight;
            }
        }

        warnings.Add((isRightBrow ? "right" : "left") + "_eyebrow_roi_clipped_by_upper_orbital_arc_band");
    }

    private static (double X, double Y) GetEyeRolePoint(AnchorMeshFeature eyeFeature, string roleKey, double fallbackX, double fallbackY)
    {
        AnchorMeshPoint? point = eyeFeature.Points.FirstOrDefault(candidate => candidate.Role.Contains(roleKey, StringComparison.OrdinalIgnoreCase));
        return point is null ? (fallbackX, fallbackY) : (point.SnappedX, point.SnappedY);
    }

    private static MaskPlane BuildBrowPixelEvidenceMask(MaskPlane roi, AnchorMeshFeature browFeature, ImagePixelData pixelData, double opacity)
    {
        MaskPlane candidate = MaskPlane.Empty(roi.Width, roi.Height);
        List<double> lumas = new();
        int left = Math.Max(0, (int)Math.Floor(browFeature.Points.Min(point => point.SnappedX) - browFeature.Width * 0.25 - 4));
        int right = Math.Min(roi.Width - 1, (int)Math.Ceiling(browFeature.Points.Max(point => point.SnappedX) + browFeature.Width * 0.25 + 4));
        int top = Math.Max(0, (int)Math.Floor(browFeature.CenterY - Math.Max(browFeature.Height * 2.0, browFeature.Width * 0.22) - 4));
        int bottom = Math.Min(roi.Height - 1, (int)Math.Ceiling(browFeature.CenterY + Math.Max(browFeature.Height * 1.8, browFeature.Width * 0.20) + 4));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
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
        double angle = browFeature.AngleRad;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double roiWeight = roi[x, y];
                if (roiWeight <= 0.04)
                {
                    continue;
                }

                double luma = pixelData.GetLuma(x, y);
                double darkness = Math.Clamp((p65 + contrast * 0.18 - luma) / Math.Max(1, contrast * 1.55), 0, 1);
                if (darkness <= 0.02)
                {
                    continue;
                }

                double directional = GetBrowDirectionalScore(pixelData, x, y, cos, sin);
                double connectivity = GetNeighborhoodDarkness(pixelData, roi, x, y, p65 + contrast * 0.12);
                double colorCluster = GetBrowColorClusterScore(pixelData, x, y);
                double value = opacity * roiWeight * Math.Clamp(darkness * 0.56 + directional * 0.18 + connectivity * 0.18 + colorCluster * 0.08, 0, 1);
                if (value > 0.12)
                {
                    candidate[x, y] = Math.Clamp(value, 0, opacity);
                }
            }
        }

        return FeatherSmallMask(candidate, radius: 1);
    }

    private static double GetBrowDirectionalScore(ImagePixelData pixels, int x, int y, double cos, double sin)
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

    private static double GetNeighborhoodDarkness(ImagePixelData pixels, MaskPlane roi, int x, int y, double threshold)
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

    private static double GetBrowColorClusterScore(ImagePixelData pixels, int x, int y)
    {
        pixels.GetRgb(x, y, out byte red, out byte green, out byte blue);
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double saturation = max <= 1 ? 0 : (max - min) / max;
        double warmHair = red >= green - 12 && green >= blue - 18 ? 0.20 : 0.0;
        return Math.Clamp((1 - pixels.GetLuma(x, y) / 255.0) * 0.70 + saturation * 0.20 + warmHair, 0, 1);
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

    private static MaskPlane BuildMouthProtectMask(
        int width,
        int height,
        AnchorMeshFeature? mouthFeature,
        double opacity,
        bool innerOnly = false)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (mouthFeature is null || mouthFeature.Points.Count == 0)
        {
            return mask;
        }

        if (mouthFeature.IsClosedLoop && mouthFeature.Points.Count >= 3)
        {
            return FillClosedFeature(
                width,
                height,
                mouthFeature,
                opacity,
                innerOnly ? 1.5 : 2.6);
        }

        return StrokeOpenFeature(
            width,
            height,
            mouthFeature,
            opacity,
            GetFeatureStrokeRadius(mouthFeature, innerOnly ? 0.10 : 0.18, innerOnly ? 2.0 : 3.0, innerOnly ? 8.0 : 14.0),
            innerOnly ? 1.4 : 2.4);
    }

    private static MaskPlane BuildLipSurfaceProtectMask(
        int width,
        int height,
        AnchorMeshFeature? outerLip,
        AnchorMeshFeature? innerMouth,
        MaskPlane innerMouthProtectionMask,
        List<string> warnings)
    {
        if (outerLip is null || outerLip.Points.Count < 24 || innerMouth is null || innerMouth.Points.Count < 16)
        {
            warnings.Add("lip_surface_loop_fill_fallback_outer_lip_only");
            return MaskPlane.Subtract(BuildMouthProtectMask(width, height, outerLip, 1.0), MaskPlane.Multiply(innerMouthProtectionMask, 0.72));
        }

        MaskPlane upperLoop = FillPointLoop(
            width,
            height,
            CreateLipSurfaceLoop(
                outerLip,
                innerMouth,
                outerIndices: [0, 23, 22, 21, 20, 19, 18, 17, 16, 15, 14, 13, 12],
                innerIndices: [8, 9, 10, 11, 12, 13, 14, 15, 0]),
            opacity: 1.0,
            featherRadius: LipGuideProfile.UpperLipFeatherRadius);
        MaskPlane lowerLoop = FillPointLoop(
            width,
            height,
            CreateLipSurfaceLoop(
                outerLip,
                innerMouth,
                outerIndices: [0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12],
                innerIndices: [8, 7, 6, 5, 4, 3, 2, 1, 0]),
            opacity: 1.0,
            featherRadius: LipGuideProfile.LowerLipFeatherRadius);

        MaskPlane lipSurface = MaskPlane.Union(upperLoop, lowerLoop);
        lipSurface = MaskPlane.Union(lipSurface, MaskPlane.Multiply(lowerLoop, LipGuideProfile.LowerLipSupportOpacity));
        MaskPlane softLoopSupport = MaskPlane.Multiply(FillClosedFeature(width, height, outerLip, 1.0, 3.8), 0.36);
        lipSurface = MaskPlane.Union(lipSurface, MaskPlane.Subtract(softLoopSupport, MaskPlane.Multiply(innerMouthProtectionMask, 0.96)));
        lipSurface = MaskPlane.Subtract(lipSurface, MaskPlane.Multiply(innerMouthProtectionMask, 0.82));
        AddLipCornerSupport(lipSurface, outerLip);
        FillSmallLipSurfaceGaps(lipSurface, outerLip, innerMouthProtectionMask);

        double upperCoverage = CoverageRatio(lipSurface, upperLoop);
        double lowerCoverage = CoverageRatio(lipSurface, lowerLoop);
        warnings.Add("lip_surface_loop_soft_fill:upperCoverage=" + Math.Round(upperCoverage, 2) + ",lowerCoverage=" + Math.Round(lowerCoverage, 2));
        if (upperCoverage < 0.45 || lowerCoverage < 0.45)
        {
            warnings.Add("lip_surface_loop_coverage_low_soft_support_used");
        }

        return lipSurface;
    }

    private static void AddLipCornerSupport(MaskPlane lipSurface, AnchorMeshFeature outerLip)
    {
        AnchorMeshPoint? rightCorner = outerLip.Points.FirstOrDefault(point => point.Role.Contains("MouthRightCorner", StringComparison.OrdinalIgnoreCase));
        AnchorMeshPoint? leftCorner = outerLip.Points.FirstOrDefault(point => point.Role.Contains("MouthLeftCorner", StringComparison.OrdinalIgnoreCase));
        double radius = Math.Clamp(outerLip.Width * LipGuideProfile.CornerSupportRadiusRatio, LipGuideProfile.CornerSupportMinRadius, LipGuideProfile.CornerSupportMaxRadius);
        if (rightCorner is not null)
        {
            AddTemplateShape(lipSurface, rightCorner.SnappedX, rightCorner.SnappedY, radius, radius * 0.82, outerLip.AngleRad, LipGuideProfile.CornerSupportOpacity, 1.4, ShapeEllipse);
        }

        if (leftCorner is not null)
        {
            AddTemplateShape(lipSurface, leftCorner.SnappedX, leftCorner.SnappedY, radius, radius * 0.82, outerLip.AngleRad, LipGuideProfile.CornerSupportOpacity, 1.4, ShapeEllipse);
        }
    }

    private static MaskPlane BuildInnerMouthProtectMask(int width, int height, AnchorMeshFeature? innerMouthFeature, double opacity)
    {
        MaskPlane inner = BuildMouthProtectMask(width, height, innerMouthFeature, opacity, innerOnly: true);
        return SoftErode(inner, radius: 1);
    }

    private static IReadOnlyList<FeatureMeshPoint> CreateLipSurfaceLoop(
        AnchorMeshFeature outerLip,
        AnchorMeshFeature innerMouth,
        IReadOnlyList<int> outerIndices,
        IReadOnlyList<int> innerIndices)
    {
        List<FeatureMeshPoint> points = new();
        foreach (int index in outerIndices)
        {
            AnchorMeshPoint point = outerLip.Points[index];
            points.Add(new FeatureMeshPoint(points.Count, point.SnappedX, point.SnappedY, point.Confidence, point.Role));
        }

        foreach (int index in innerIndices)
        {
            AnchorMeshPoint point = innerMouth.Points[index];
            points.Add(new FeatureMeshPoint(points.Count, point.SnappedX, point.SnappedY, point.Confidence, point.Role));
        }

        return points;
    }

    private static MaskPlane FillPointLoop(int width, int height, IReadOnlyList<FeatureMeshPoint> points, double opacity, double featherRadius)
    {
        FaceFeatureMesh mesh = new(FaceFeatureType.Lip, points, 0.82, "lip_surface_loop_soft_fill");
        return MeshMaskRasterizer.FillClosedMesh(width, height, mesh, opacity, featherRadius);
    }

    private static void FillSmallLipSurfaceGaps(MaskPlane lipSurface, AnchorMeshFeature outerLip, MaskPlane innerMouthProtectionMask)
    {
        double minX = outerLip.Points.Min(point => point.SnappedX);
        double maxX = outerLip.Points.Max(point => point.SnappedX);
        double minY = outerLip.Points.Min(point => point.SnappedY);
        double maxY = outerLip.Points.Max(point => point.SnappedY);
        int left = Math.Max(1, (int)Math.Floor(minX) - 3);
        int right = Math.Min(lipSurface.Width - 2, (int)Math.Ceiling(maxX) + 3);
        int top = Math.Max(1, (int)Math.Floor(minY) - 3);
        int bottom = Math.Min(lipSurface.Height - 2, (int)Math.Ceiling(maxY) + 3);
        MaskPlane copy = lipSurface.Clone();
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (copy[x, y] >= 0.30 || innerMouthProtectionMask[x, y] > 0.60)
                {
                    continue;
                }

                double sum = 0;
                int count = 0;
                for (int yy = y - 1; yy <= y + 1; yy++)
                {
                    for (int xx = x - 1; xx <= x + 1; xx++)
                    {
                        if (copy[xx, yy] > 0.38)
                        {
                            sum += copy[xx, yy];
                            count++;
                        }
                    }
                }

                if (count >= 5)
                {
                    lipSurface[x, y] = Math.Max(lipSurface[x, y], Math.Clamp(sum / count * 0.62, 0.30, 0.66));
                }
            }
        }
    }

    private static MaskPlane SoftErode(MaskPlane source, int radius)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double min = source[x, y];
                for (int yy = Math.Max(0, y - radius); yy <= Math.Min(source.Height - 1, y + radius); yy++)
                {
                    for (int xx = Math.Max(0, x - radius); xx <= Math.Min(source.Width - 1, x + radius); xx++)
                    {
                        min = Math.Min(min, source[xx, yy]);
                    }
                }

                result[x, y] = min;
            }
        }

        return result;
    }

    private static double CoverageRatio(MaskPlane mask, MaskPlane loop)
    {
        MaskPlane.EnsureSameSize(mask, loop);
        double covered = 0;
        double total = 0;
        for (int index = 0; index < mask.Values.Length; index++)
        {
            double loopWeight = loop.Values[index];
            if (loopWeight <= 0.10)
            {
                continue;
            }

            total += loopWeight;
            covered += Math.Min(mask.Values[index], loopWeight);
        }

        return total <= 0 ? 0 : Math.Clamp(covered / total, 0, 1);
    }

    private static MaskPlane FillClosedFeature(int width, int height, AnchorMeshFeature? feature, double opacity, double featherRadius)
    {
        if (feature is null || feature.Points.Count < 3)
        {
            return MaskPlane.Empty(width, height);
        }

        FaceFeatureMesh mesh = ToFeatureMesh(feature);
        return MeshMaskRasterizer.FillClosedMesh(width, height, mesh, opacity, featherRadius);
    }

    private static MaskPlane StrokeOpenFeature(
        int width,
        int height,
        AnchorMeshFeature? feature,
        double opacity,
        double radius,
        double featherRadius)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (feature is null || feature.Points.Count == 0)
        {
            return mask;
        }

        double maxRadius = Math.Max(radius, radius + featherRadius);
        int left = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedX) - maxRadius - 2));
        int right = Math.Min(width - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedX) + maxRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedY) - maxRadius - 2));
        int bottom = Math.Min(height - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedY) + maxRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double distance = DistanceToFeaturePolyline(x + 0.5, y + 0.5, feature, closeLoop: feature.IsClosedLoop);
                double amount = distance <= radius
                    ? opacity
                    : opacity * (1 - SmoothStep(radius, radius + Math.Max(0.5, featherRadius), distance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }

        return mask;
    }

    private static void StrokeOpenFeatureInto(
        MaskPlane mask,
        AnchorMeshFeature feature,
        double opacity,
        double radius,
        double featherRadius)
    {
        double maxRadius = Math.Max(radius, radius + featherRadius);
        int left = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedX) - maxRadius - 2));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedX) + maxRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedY) - maxRadius - 2));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedY) + maxRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double distance = DistanceToFeaturePolyline(x + 0.5, y + 0.5, feature, closeLoop: feature.IsClosedLoop);
                double amount = distance <= radius
                    ? opacity
                    : opacity * (1 - SmoothStep(radius, radius + Math.Max(0.5, featherRadius), distance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }
    }

    private static MaskPlane BuildNostrilMask(int width, int height, AnchorMeshFeature? noseFeature)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (noseFeature is null || noseFeature.Points.Count == 0)
        {
            return mask;
        }

        AnchorMeshPoint[] nostrilPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nostrilPoints.Length == 0)
        {
            return mask;
        }

        AnchorMeshPoint[] leftPoints = nostrilPoints
            .Where(point => point.Role.Contains("LeftNostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AnchorMeshPoint[] rightPoints = nostrilPoints
            .Where(point => point.Role.Contains("RightNostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        AddNostrilOpening(mask, leftPoints, noseFeature, isRightSide: false);
        AddNostrilOpening(mask, rightPoints, noseFeature, isRightSide: true);

        return mask;
    }

    private static MaskPlane BuildNoseSurfaceMask(int width, int height, AnchorMeshFeature? noseFeature, MaskPlane nostrilProtectionMask, List<string> warnings)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        if (noseFeature is null || noseFeature.Points.Count == 0)
        {
            warnings.Add("nose_surface_mask_missing_anchors");
            return empty;
        }

        AnchorMeshPoint[] bridgePoints = noseFeature.Points
            .Where(point => point.Role.Contains("Bridge", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AnchorMeshPoint[] tipPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Tip", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AnchorMeshPoint[] wingPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Wing", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AnchorMeshPoint[] nostrilPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        MaskPlane bridgeSurface = MaskPlane.Empty(width, height);
        MaskPlane tipSurface = MaskPlane.Empty(width, height);
        MaskPlane leftWingSurface = MaskPlane.Empty(width, height);
        MaskPlane rightWingSurface = MaskPlane.Empty(width, height);
        MaskPlane baseSurface = MaskPlane.Empty(width, height);

        if (bridgePoints.Length > 0)
        {
            double bridgeCenterX = bridgePoints.Average(point => point.SnappedX);
            double bridgeCenterY = bridgePoints.Average(point => point.SnappedY);
            AddTemplateShape(
                bridgeSurface,
                bridgeCenterX,
                bridgeCenterY,
                Math.Clamp(noseFeature.Width * 0.22, 5.0, 26.0),
                Math.Clamp(noseFeature.Height * 0.48, 16.0, 76.0),
                noseFeature.AngleRad,
                0.72,
                featherRadius: 2.4,
                ShapeEllipse);
        }

        if (tipPoints.Length > 0)
        {
            double tipCenterX = tipPoints.Average(point => point.SnappedX);
            double tipCenterY = tipPoints.Average(point => point.SnappedY);
            AddTemplateShape(
                tipSurface,
                tipCenterX,
                tipCenterY,
                Math.Clamp(noseFeature.Width * 0.36, 8.0, 42.0),
                Math.Clamp(noseFeature.Height * 0.24, 8.0, 34.0),
                noseFeature.AngleRad,
                0.78,
                featherRadius: 2.8,
                ShapeEllipse);
        }

        if (wingPoints.Length > 0)
        {
            AddNoseWingSurface(leftWingSurface, wingPoints.Where(point => point.SnappedX <= noseFeature.CenterX).ToArray(), noseFeature, -1);
            AddNoseWingSurface(rightWingSurface, wingPoints.Where(point => point.SnappedX > noseFeature.CenterX).ToArray(), noseFeature, 1);
        }

        AnchorMeshPoint[] basePoints = nostrilPoints.Length > 0
            ? nostrilPoints.Concat(wingPoints).ToArray()
            : wingPoints;
        if (basePoints.Length > 0)
        {
            double baseCenterX = basePoints.Average(point => point.SnappedX);
            double baseCenterY = basePoints.Average(point => point.SnappedY);
            double baseWidth = Math.Max(1.0, basePoints.Max(point => point.SnappedX) - basePoints.Min(point => point.SnappedX));
            double baseHeight = Math.Max(1.0, basePoints.Max(point => point.SnappedY) - basePoints.Min(point => point.SnappedY));
            AddTemplateShape(
                baseSurface,
                baseCenterX,
                baseCenterY,
                Math.Clamp(Math.Max(baseWidth * 0.62, noseFeature.Width * 0.32), 8.0, 50.0),
                Math.Clamp(Math.Max(baseHeight * 0.95, noseFeature.Height * 0.10), 5.0, 24.0),
                noseFeature.AngleRad,
                0.58,
                featherRadius: 2.5,
                ShapeEllipse);
        }

        MaskPlane surface = MaskPlane.Union(bridgeSurface, tipSurface, leftWingSurface, rightWingSurface, baseSurface);
        ClipNoseMaskBelowBase(surface, noseFeature, nostrilPoints);
        MaskPlane final = MaskPlane.Subtract(surface, nostrilProtectionMask);
        double expectedMinArea = Math.Clamp(noseFeature.Width * noseFeature.Height * 0.000020, 0.00015, 0.006);
        if (bridgeSurface.Average() <= 0.000001 || tipSurface.Average() <= 0.000001 || final.Average() < expectedMinArea)
        {
            warnings.Add("nose_surface_mask_area_too_small_possible_line_or_nostril_collapse");
        }
        else
        {
            warnings.Add("nose_surface_mask_area_based_bridge_tip_wings_base_nostril_excluded");
        }

        return final;
    }

    private static void AddNoseWingSurface(MaskPlane mask, IReadOnlyList<AnchorMeshPoint> wingPoints, AnchorMeshFeature noseFeature, int side)
    {
        if (wingPoints.Count == 0)
        {
            return;
        }

        double wingCenterX = wingPoints.Average(point => point.SnappedX);
        double wingCenterY = wingPoints.Average(point => point.SnappedY);
        double wingWidth = Math.Max(1.0, wingPoints.Max(point => point.SnappedX) - wingPoints.Min(point => point.SnappedX));
        double wingHeight = Math.Max(1.0, wingPoints.Max(point => point.SnappedY) - wingPoints.Min(point => point.SnappedY));
        double sideOffset = Math.Cos(noseFeature.AngleRad) * Math.Clamp(noseFeature.Width * 0.035, 1.0, 5.0) * side;
        AddTemplateShape(
            mask,
            wingCenterX + sideOffset,
            wingCenterY,
            Math.Clamp(Math.Max(wingWidth * 0.95, noseFeature.Width * 0.16), 5.0, 26.0),
            Math.Clamp(Math.Max(wingHeight * 0.95, noseFeature.Height * 0.14), 7.0, 32.0),
            noseFeature.AngleRad,
            0.70,
            featherRadius: 2.8,
            ShapeEllipse);
    }

    private static void ClipNoseMaskBelowBase(MaskPlane mask, AnchorMeshFeature noseFeature, IReadOnlyList<AnchorMeshPoint> nostrilPoints)
    {
        if (nostrilPoints.Count == 0)
        {
            return;
        }

        double baseY = nostrilPoints.Max(point => point.SnappedY) + Math.Clamp(noseFeature.Width * 0.10, 5.0, 16.0);
        double feather = Math.Clamp(noseFeature.Width * 0.045, 3.0, 8.0);
        for (int y = Math.Max(0, (int)Math.Floor(baseY)); y < mask.Height; y++)
        {
            double keep = 1 - SmoothStep(baseY, baseY + feather, y + 0.5);
            if (keep <= 0)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    mask[x, y] = 0;
                }
                continue;
            }

            for (int x = 0; x < mask.Width; x++)
            {
                mask[x, y] *= keep;
            }
        }
    }

    private static void ClipMouthMaskByNoseMouthDistance(MaskPlane mask, AnchorMeshFeature? noseFeature, AnchorMeshFeature? mouthFeature, List<string> warnings)
    {
        if (noseFeature is null || mouthFeature is null || noseFeature.Points.Count == 0 || mouthFeature.Points.Count == 0)
        {
            return;
        }

        AnchorMeshPoint? tip = noseFeature.Points.FirstOrDefault(point => point.Role.Equals("NoseTipTriangleApex", StringComparison.OrdinalIgnoreCase))
            ?? noseFeature.Points.FirstOrDefault(point => point.Role.Contains("Tip", StringComparison.OrdinalIgnoreCase));
        if (tip is null)
        {
            return;
        }

        AnchorMeshPoint[] nostrilPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        double noseBaseY = nostrilPoints.Length > 0
            ? nostrilPoints.Max(point => point.SnappedY)
            : noseFeature.CenterY + noseFeature.Height * 0.36;
        double upperLipTopY = GetUpperLipTopY(mouthFeature);
        double noseToMouth = Math.Max(12.0, mouthFeature.CenterY - tip.SnappedY);
        double estimatedPhiltrumHeight = Math.Max(4.0, upperLipTopY - noseBaseY);
        double philtrumHeight = PhiltrumGuideProfile.ClampPhiltrumHeight(noseToMouth, estimatedPhiltrumHeight);
        double lipBoundaryAllowance = Math.Clamp(mouthFeature.Height * 0.08, 1.5, 5.0);
        double upperMouthLimit = Math.Min(
            upperLipTopY + lipBoundaryAllowance,
            noseBaseY + philtrumHeight + lipBoundaryAllowance);
        upperMouthLimit = Math.Max(
            noseBaseY + Math.Clamp(noseFeature.Width * 0.045, 2.0, 9.0),
            upperMouthLimit);
        double feather = Math.Clamp(Math.Max(noseFeature.Width * 0.045, mouthFeature.Height * 0.16), 4.0, 12.0);

        for (int y = 0; y < mask.Height; y++)
        {
            double keep = SmoothStep(upperMouthLimit - feather, upperMouthLimit, y + 0.5);
            if (keep >= 0.999)
            {
                continue;
            }

            for (int x = 0; x < mask.Width; x++)
            {
                mask[x, y] *= keep;
            }
        }

        warnings.Add("mouth_mask_clipped_by_nose_mouth_distance_and_philtrum_guide");
    }

    private static double GetUpperLipTopY(AnchorMeshFeature mouthFeature)
    {
        AnchorMeshPoint[] upperLipPoints = mouthFeature.Points
            .Where(point =>
                point.Role.Contains("UpperLipTopCenter", StringComparison.OrdinalIgnoreCase) ||
                point.Role.Contains("UpperLipCupid", StringComparison.OrdinalIgnoreCase) ||
                point.Role.Contains("UpperLip", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (upperLipPoints.Length > 0)
        {
            return upperLipPoints.Min(point => point.SnappedY);
        }

        return mouthFeature.Bounds.Top + mouthFeature.Height * 0.14;
    }

    private static void AddNostrilOpening(MaskPlane mask, IReadOnlyList<AnchorMeshPoint> points, AnchorMeshFeature noseFeature, bool isRightSide)
    {
        if (points.Count == 0)
        {
            return;
        }

        double meanX = points.Average(point => point.SnappedX);
        double meanY = points.Average(point => point.SnappedY);
        double minX = points.Min(point => point.SnappedX);
        double maxX = points.Max(point => point.SnappedX);
        double minY = points.Min(point => point.SnappedY);
        double maxY = points.Max(point => point.SnappedY);
        double estimatedWidth = Math.Max(1.0, (maxX - minX) * 1.30);
        double nostrilWidth = NoseGuideProfile.ClampNostrilWidth(noseFeature.Width, estimatedWidth);
        double estimatedHeight = Math.Max(1.0, (maxY - minY) * 1.10);
        double nostrilHeight = NoseGuideProfile.ClampNostrilHeight(nostrilWidth, estimatedHeight);
        double axisX = Math.Cos(noseFeature.AngleRad);
        double axisY = Math.Sin(noseFeature.AngleRad);
        double upX = Math.Sin(noseFeature.AngleRad);
        double upY = -Math.Cos(noseFeature.AngleRad);
        double sideSign = isRightSide ? -1.0 : 1.0;
        double centerX = meanX + axisX * nostrilWidth * NoseGuideProfile.NostrilInwardShiftRatio * sideSign;
        double centerY = meanY + axisY * nostrilWidth * NoseGuideProfile.NostrilInwardShiftRatio * sideSign;
        centerX += upX * nostrilHeight * NoseGuideProfile.NostrilUpwardShiftRatio;
        centerY += upY * nostrilHeight * NoseGuideProfile.NostrilUpwardShiftRatio;
        double openingAngle = noseFeature.AngleRad + (isRightSide ? -NoseGuideProfile.NostrilTiltRadians : NoseGuideProfile.NostrilTiltRadians);
        AddTemplateShape(mask, centerX, centerY, nostrilWidth * 0.5, nostrilHeight * 0.5, openingAngle, 1.0, 1.5, ShapeEllipse);
    }

    private static bool AddImageTemplate(
        MaskPlane mask,
        string fileName,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angle,
        double opacity,
        bool flipX)
    {
        TemplateMask? template = LoadTemplate(fileName);
        if (template is null)
        {
            return false;
        }

        double padding = 2.0;
        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX - padding));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX + padding));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY - padding));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY + padding));
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double localX = (dx * cos + dy * sin) / Math.Max(0.5, radiusX);
                double localY = (-dx * sin + dy * cos) / Math.Max(0.5, radiusY);
                if (localX < -1 || localX > 1 || localY < -1 || localY > 1)
                {
                    continue;
                }

                double u = template.CenterU + (flipX ? -localX : localX) * 0.5;
                double v = template.CenterV + localY * 0.5;
                double amount = template.Sample(u, v) * opacity;
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }

        return true;
    }

    private static TemplateMask? LoadTemplate(string fileName)
    {
        lock (TemplateCache)
        {
            if (TemplateCache.TryGetValue(fileName, out TemplateMask? cached))
            {
                return cached;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "MaskTemplates", fileName);
            if (!File.Exists(path))
            {
                TemplateCache[fileName] = null;
                return null;
            }

            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];
            BitmapSource bitmap = source.Format == System.Windows.Media.PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            bitmap.Freeze();

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);
            double[] values = new double[width * height];
            double weightedX = 0;
            double weightedY = 0;
            double weightTotal = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * stride + x * 4;
                    double alpha = pixels[pixelIndex + 3] / 255d;
                    double luma = (pixels[pixelIndex] + pixels[pixelIndex + 1] + pixels[pixelIndex + 2]) / (255d * 3d);
                    double value = alpha < 0.995
                        ? alpha
                        : 1 - luma;
                    values[y * width + x] = value;
                    if (value > 0.05)
                    {
                        weightedX += (x + 0.5) * value;
                        weightedY += (y + 0.5) * value;
                        weightTotal += value;
                    }
                }
            }

            double centerU = weightTotal > 0 ? weightedX / weightTotal / width : 0.5;
            double centerV = weightTotal > 0 ? weightedY / weightTotal / height : 0.5;
            TemplateMask template = new(width, height, values, centerU, centerV);
            TemplateCache[fileName] = template;
            return template;
        }
    }

    private static ImagePixelData? TryCreatePixelData(BitmapSource? source, int width, int height, List<string> warnings)
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
            return new ImagePixelData(width, height, stride, pixels);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add("anchor_mesh_pixel_evidence_unavailable:" + ex.GetType().Name);
            return null;
        }
    }

    private static void AddTemplateShape(
        MaskPlane mask,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angle,
        double opacity,
        double featherRadius,
        Func<double, double, double> shape)
    {
        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX - featherRadius - 2));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX + featherRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY - featherRadius - 2));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY + featherRadius + 2));
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double localX = (dx * cos + dy * sin) / Math.Max(0.5, radiusX);
                double localY = (-dx * sin + dy * cos) / Math.Max(0.5, radiusY);
                double signedDistance = shape(localX, localY);
                double amount = signedDistance <= 0
                    ? opacity
                    : opacity * (1 - SmoothStep(0, Math.Max(0.001, featherRadius / Math.Max(radiusX, radiusY)), signedDistance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }
    }

    private sealed record TemplateMask(int Width, int Height, double[] Values, double CenterU, double CenterV)
    {
        public double Sample(double u, double v)
        {
            double x = Math.Clamp(u, 0, 1) * (Width - 1);
            double y = Math.Clamp(v, 0, 1) * (Height - 1);
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(Width - 1, x0 + 1);
            int y1 = Math.Min(Height - 1, y0 + 1);
            double tx = x - x0;
            double ty = y - y0;

            double top = Lerp(Values[y0 * Width + x0], Values[y0 * Width + x1], tx);
            double bottom = Lerp(Values[y1 * Width + x0], Values[y1 * Width + x1], tx);
            return Math.Clamp(Lerp(top, bottom, ty), 0, 1);
        }

        private static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * amount;
        }
    }

    private sealed record ImagePixelData(int Width, int Height, int Stride, byte[] Pixels)
    {
        public double GetLuma(int x, int y)
        {
            int index = y * Stride + x * 4;
            return Pixels[index + 2] * 0.2126 + Pixels[index + 1] * 0.7152 + Pixels[index] * 0.0722;
        }

        public void GetRgb(int x, int y, out byte red, out byte green, out byte blue)
        {
            int index = y * Stride + x * 4;
            blue = Pixels[index];
            green = Pixels[index + 1];
            red = Pixels[index + 2];
        }
    }

    private static double ShapeEllipse(double x, double y)
    {
        return Math.Sqrt(x * x + y * y) - 1;
    }

    private static double ShapeNoseStructure(double x, double y)
    {
        if (y < -1.05 || y > 1.05)
        {
            return Math.Abs(y) - 1.05;
        }

        double yy = Math.Clamp(y, -1.0, 1.0);
        double halfWidth;
        if (yy < -0.35)
        {
            double t = (yy + 1.0) / 0.65;
            halfWidth = 0.18 + t * 0.15;
        }
        else if (yy < 0.25)
        {
            double t = (yy + 0.35) / 0.60;
            halfWidth = 0.30 + t * 0.20;
        }
        else
        {
            double t = (yy - 0.25) / 0.75;
            halfWidth = 0.50 + Math.Sin(Math.Clamp(t, 0, 1) * Math.PI) * 0.25;
        }

        double verticalCap = Math.Pow(Math.Abs(yy), 2.4);
        return Math.Max(Math.Abs(x) / Math.Max(0.001, halfWidth), verticalCap) - 1;
    }

    private static double ShapeEyeAlmond(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.04)
        {
            return ax - 1.04;
        }

        double lid = Math.Pow(Math.Max(0, 1 - Math.Pow(ax, 1.72)), 0.58);
        double upper = lid * 0.58 + Math.Exp(-Math.Pow((x + 0.18) / 0.55, 2)) * 0.06;
        double lower = lid * 0.42 + Math.Exp(-Math.Pow((x - 0.05) / 0.70, 2)) * 0.04;
        if (y < 0)
        {
            return (-y / Math.Max(0.001, upper)) - 1;
        }

        return (y / Math.Max(0.001, lower)) - 1;
    }

    private static double ShapeLeftBrowTemplate(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.02)
        {
            return ax - 1.02;
        }

        double arch = -0.30 * Math.Sin((x + 1) * Math.PI * 0.5) + 0.10 * x;
        double thickness = 0.42 * (1 - SmoothStep(0.60, 1.02, ax)) + 0.15;
        return Math.Abs(y - arch) / Math.Max(0.001, thickness) - 1;
    }

    private static double ShapeRightBrowTemplate(double x, double y)
    {
        return ShapeLeftBrowTemplate(-x, y);
    }

    private static double ShapeMouthTemplate(double x, double y)
    {
        double baseShape = Math.Sqrt(x * x + Math.Pow(y / 0.72, 2)) - 1;
        double upperNotch = Math.Exp(-Math.Pow(x / 0.26, 2)) * 0.22;
        if (y < -0.18 + upperNotch)
        {
            return Math.Max(baseShape, (-0.18 + upperNotch - y) / 0.18);
        }

        return baseShape;
    }

    private static double ShapeInnerMouthLine(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.0)
        {
            return ax - 1.0;
        }

        double curve = 0.08 * Math.Cos(x * Math.PI);
        double thickness = 0.20 * (1 - SmoothStep(0.72, 1.0, ax)) + 0.05;
        return Math.Abs(y - curve) / Math.Max(0.001, thickness) - 1;
    }

    private static FaceFeatureMesh ToFeatureMesh(AnchorMeshFeature feature)
    {
        List<FeatureMeshPoint> points = feature.Points
            .Select((point, index) => new FeatureMeshPoint(index, point.SnappedX, point.SnappedY, point.Confidence, point.Role))
            .ToList();
        return new FaceFeatureMesh(ToFeatureType(feature.Name), points, feature.Confidence, "anchor_mesh_" + feature.Name);
    }

    private static FaceFeatureType ToFeatureType(string featureName)
    {
        return featureName switch
        {
            "LeftEye" or "RightEye" => FaceFeatureType.Eye,
            "LeftBrow" or "RightBrow" => FaceFeatureType.Brow,
            "Nose" => FaceFeatureType.Nose,
            _ => FaceFeatureType.Lip
        };
    }

    private static double GetFeatureStrokeRadius(AnchorMeshFeature? feature, double heightRatio, double min, double max)
    {
        if (feature is null)
        {
            return min;
        }

        return Math.Clamp(feature.Height * heightRatio, min, max);
    }

    private static double DistanceToFeaturePolyline(double x, double y, AnchorMeshFeature feature, bool closeLoop)
    {
        double best = double.MaxValue;
        int segmentCount = closeLoop ? feature.Points.Count : feature.Points.Count - 1;
        for (int index = 0; index < segmentCount; index++)
        {
            AnchorMeshPoint a = feature.Points[index];
            AnchorMeshPoint b = feature.Points[(index + 1) % feature.Points.Count];
            best = Math.Min(best, DistanceToSegment(x, y, a.SnappedX, a.SnappedY, b.SnappedX, b.SnappedY));
        }

        return best;
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.0001)
        {
            return Distance(px, py, ax, ay);
        }

        double t = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        return Distance(px, py, ax + dx * t, ay + dy * t);
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
