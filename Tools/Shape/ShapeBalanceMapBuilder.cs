using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class ShapeBalanceMapBuilder
{
    public ShapeBalanceMap Build(
        FaceSnapshotMaskSet snapshot,
        ShapeBalanceAnalysisReport analysis,
        ShapeBalanceOptions options,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentNullException.ThrowIfNull(options);

        Int32Rect faceBox = snapshot.Analysis.FaceBox;
        if (!options.EnableShapeBalance)
        {
            return ShapeBalanceMap.Identity(width, height, faceBox);
        }

        double global = Math.Clamp(options.GlobalShapeBalanceAmount, 0, 1);
        double identityPreserve = Math.Clamp(options.PreserveIdentityStrength, 0, 1);
        double maxDisplacement = Math.Max(1, Math.Min(faceBox.Width, faceBox.Height) * 0.035 * Math.Clamp(options.MaxAllowedWarpStrength, 0.05, 1));
        double manualMaxDisplacement = Math.Max(
            maxDisplacement,
            Math.Min(faceBox.Width, faceBox.Height) * 0.085 * Math.Clamp(options.MaxAllowedWarpStrength, 0.05, 1));
        double maxRotationRadians = DegreesToRadians(4.0 * Math.Clamp(options.MaxAllowedWarpStrength, 0.05, 1));
        double rotationRadians = DegreesToRadians(-analysis.FaceRollAngle) *
            Math.Clamp(options.HeadTiltCorrectAmount, 0, 1) *
            global *
            (1 - identityPreserve * 0.35);
        rotationRadians = Math.Clamp(rotationRadians, -maxRotationRadians, maxRotationRadians);
        double pitchShear = Math.Clamp(
            -analysis.FacePitchLikeBias * Math.Clamp(options.HeadPitchCorrectAmount, 0, 1) * global * 0.035,
            -0.018,
            0.018);
        IReadOnlyDictionary<string, WpfPoint> landmarks = snapshot.Analysis.FaceLandmarks;
        TryGetPoint(landmarks, "left_eye", out WpfPoint leftEye);
        TryGetPoint(landmarks, "right_eye", out WpfPoint rightEye);
        TryGetPoint(landmarks, "nose_tip", out WpfPoint noseTip);
        TryGetPoint(landmarks, "mouth_center", out WpfPoint mouthCenter);
        TryGetPoint(landmarks, "chin", out WpfPoint chinPoint);
        FeatureCenterLine featureCenterLine = FeatureCenterLineEstimator.Estimate(snapshot);
        WpfPoint transformCenter = noseTip == default
            ? featureCenterLine.Center
            : noseTip;
        ShapeBalanceGlobalTransform globalTransform = new(
            transformCenter,
            rotationRadians,
            0,
            0,
            1,
            1,
            pitchShear);

        List<ShapeBalanceWarpRegion> regions = new();
        List<ShapeBalanceProtectedRegion> protectedRegions = BuildProtectedRegions(snapshot, faceBox, leftEye, rightEye, noseTip, mouthCenter);
        List<ShapeBalanceDebugVector> vectors = new();
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double centerLineX = featureCenterLine.Center.X;

        AddYawLikeBalanceRegions(regions, vectors, faceBox, featureCenterLine.Center, noseTip, mouthCenter, chinPoint, analysis, options, global, maxDisplacement, manualMaxDisplacement);
        AddPairedEyeLevelRegions(regions, vectors, leftEye, rightEye, analysis.EyeLevelDelta, faceWidth, faceHeight, options, global, maxDisplacement, manualMaxDisplacement);
        AddPairedHeightRegions(
            regions,
            vectors,
            EstimateEyebrowPoint(leftEye, faceHeight),
            EstimateEyebrowPoint(rightEye, faceHeight),
            analysis.EyebrowLevelDelta,
            options.ManualEyebrowLevelShift,
            faceWidth * 0.20,
            faceHeight * 0.11,
            options.EyebrowBalanceAmount,
            global,
            maxDisplacement,
            manualMaxDisplacement,
            "left_eyebrow_level",
            "right_eyebrow_level",
            "eyebrow_level");
        AddPairedHeightRegions(
            regions,
            vectors,
            EstimateMouthCornerPoint(mouthCenter, faceWidth, -1),
            EstimateMouthCornerPoint(mouthCenter, faceWidth, 1),
            analysis.MouthCornerDelta,
            options.ManualMouthCornerShift,
            faceWidth * 0.14,
            faceHeight * 0.10,
            options.MouthCornerBalanceAmount,
            global,
            maxDisplacement,
            manualMaxDisplacement,
            "left_mouth_corner",
            "right_mouth_corner",
            "mouth_corner");
        AddCenterLineRegion(
            regions,
            vectors,
            "yaw_like_nose_center",
            noseTip,
            analysis.NoseLineTilt,
            noseTip.X - centerLineX,
            options.ManualNoseCenterShift,
            faceWidth * 0.18,
            faceHeight * 0.28,
            options.NoseCenterBalanceAmount,
            global,
            maxDisplacement,
            manualMaxDisplacement);
        AddCenterLineRegion(
            regions,
            vectors,
            "chin_center",
            chinPoint,
            analysis.ChinCenterDelta,
            chinPoint.X - centerLineX,
            options.ManualChinCenterShift,
            faceWidth * 0.24,
            faceHeight * 0.22,
            options.ChinCenterBalanceAmount,
            global,
            maxDisplacement,
            manualMaxDisplacement);
        AddManualFaceContourRegions(
            regions,
            vectors,
            faceBox,
            featureCenterLine.Center,
            chinPoint,
            options,
            global,
            maxDisplacement,
            manualMaxDisplacement);

        SymmetryBalanceAnalysisReport symmetryAnalysis = BuildSymmetryAnalysis(snapshot, analysis, faceBox, featureCenterLine, leftEye, rightEye, noseTip, mouthCenter, chinPoint);
        SymmetryBalanceMap symmetryMap = BuildSymmetryBalanceMap(
            symmetryAnalysis,
            options.SymmetryToolset,
            faceBox,
            featureCenterLine.Center,
            leftEye,
            rightEye,
            noseTip,
            mouthCenter,
            chinPoint,
            global,
            Math.Max(maxDisplacement, manualMaxDisplacement),
            width,
            height);
        vectors.AddRange(symmetryMap.DebugVectors);

        double noseDelta = noseTip == default ? 0 : noseTip.X - centerLineX;
        double localStrength = regions.Count == 0 ? 0 : regions.Average(region => region.Strength);
        double symmetryStrength = symmetryMap.SymmetryWarpRegions.Count == 0
            ? 0
            : symmetryMap.SymmetryWarpRegions.Average(region => region.Strength);
        return new ShapeBalanceMap(
            width,
            height,
            width,
            height,
            globalTransform,
            featureCenterLine.Center,
            faceBox,
            analysis.EyeLevelDelta,
            analysis.EyebrowLevelDelta,
            analysis.MouthCornerDelta,
            noseDelta,
            analysis.ChinCenterDelta,
            maxDisplacement,
            identityPreserve,
            options.ProtectHardFeatures,
            regions,
            protectedRegions,
            new ShapeBalanceWarpStrengthMap(
                width,
                height,
                global,
                Math.Max(localStrength, symmetryStrength),
                options.ProtectHardFeatures ? 0.72 : 0,
                0.35),
            symmetryAnalysis,
            symmetryMap,
            vectors,
            DateTime.UtcNow,
            "shape_balance_map_v4_face_only_yaw_symmetry");
    }

    private static SymmetryBalanceAnalysisReport BuildSymmetryAnalysis(
        FaceSnapshotMaskSet snapshot,
        ShapeBalanceAnalysisReport analysis,
        Int32Rect faceBox,
        FeatureCenterLine featureCenterLine,
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        WpfPoint chinPoint)
    {
        double centerX = featureCenterLine.Center.X;
        List<string> warnings = new() { "symmetry_balance_analysis_v1" };
        warnings.AddRange(featureCenterLine.DebugWarnings);
        if (leftEye == default || rightEye == default)
        {
            warnings.Add("symmetry_eye_landmarks_missing_or_estimated");
        }

        if (noseTip == default)
        {
            warnings.Add("symmetry_nose_landmark_missing");
        }

        if (mouthCenter == default)
        {
            warnings.Add("symmetry_mouth_landmark_missing");
        }

        if (chinPoint == default)
        {
            warnings.Add("symmetry_chin_landmark_missing");
        }

        SideMaskStats eyeStats = CalculateSideMaskStats(snapshot.Masks.EyeMask, centerX);
        SideMaskStats nostrilStats = CalculateSideMaskStats(snapshot.Masks.NostrilMask, noseTip == default ? centerX : noseTip.X);
        SideMaskStats noseStats = CalculateSideMaskStats(snapshot.Masks.NoseMask, centerX);
        SideMaskStats jawStats = CalculateSideMaskStats(snapshot.Masks.SkinMask, centerX, faceBox.Y + faceBox.Height * 0.55);
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double eyeWidthDelta = eyeStats.NormalizedAreaDelta;
        double eyeHeightDelta = eyeStats.CenterYDelta / faceHeight;
        double nostrilSizeDelta = nostrilStats.NormalizedAreaDelta;
        double nostrilHeightDelta = nostrilStats.CenterYDelta / faceHeight;
        double nostrilPositionDelta = nostrilStats.CenterXDelta / faceWidth;
        double noseWingWidthDelta = noseStats.NormalizedAreaDelta;
        double noseWingContourDelta = noseStats.CenterXDelta / faceWidth;
        double jawlineContourDelta = jawStats.CenterXDelta / faceWidth;
        double jawWidthDelta = jawStats.NormalizedAreaDelta;
        double faceOutlineDelta = (jawlineContourDelta + analysis.FaceYawLikeBias) * 0.5;
        double asymmetry = Math.Clamp((
            Math.Abs(analysis.EyeLevelDelta) / faceHeight +
            Math.Abs(analysis.MouthCornerDelta) / faceHeight +
            Math.Abs(analysis.ChinCenterDelta) / faceWidth +
            Math.Abs(nostrilSizeDelta) +
            Math.Abs(noseWingContourDelta) +
            Math.Abs(jawlineContourDelta)) / 6d,
            0,
            1);
        double score = 1 - asymmetry;
        double suggestedAmount = Math.Clamp(25 + asymmetry * 55, 20, 80);

        return new SymmetryBalanceAnalysisReport(
            featureCenterLine.Center,
            analysis.MouthCornerDelta,
            analysis.EyeLevelDelta,
            analysis.EyebrowLevelDelta,
            0,
            eyeWidthDelta,
            eyeHeightDelta,
            nostrilSizeDelta,
            nostrilHeightDelta,
            nostrilPositionDelta,
            noseWingWidthDelta,
            noseWingContourDelta,
            jawlineContourDelta,
            jawWidthDelta,
            analysis.ChinCenterDelta,
            faceOutlineDelta,
            score,
            suggestedAmount,
            warnings);
    }

    private static SymmetryBalanceMap BuildSymmetryBalanceMap(
        SymmetryBalanceAnalysisReport analysis,
        SymmetryBalanceToolset toolset,
        Int32Rect faceBox,
        WpfPoint faceCenter,
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        WpfPoint chinPoint,
        double global,
        double maxDisplacement,
        int width,
        int height)
    {
        double scale = Math.Clamp(toolset.EffectiveSymmetryScale * global * (1 - Math.Clamp(toolset.PreserveIdentityStrength, 0, 1) * 0.32), 0, 1.08);
        if (!toolset.EnableSymmetryBalance || scale <= 0.0001)
        {
            return SymmetryBalanceMap.Empty(width, height, analysis.LeftRightCenterLine);
        }

        List<ShapeBalanceWarpRegion> regions = new();
        List<ShapeBalanceDebugVector> vectors = new();
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double overshootScale = toolset.IsOvershootZone ? 1.04 : 1;
        double safeMax = maxDisplacement * 0.58 * overshootScale;

        AddSymmetryPairedHeightRegions(
            regions,
            vectors,
            leftEye,
            rightEye,
            analysis.LowerEyeLineHeightDelta,
            faceWidth * 0.21,
            faceHeight * 0.12,
            toolset.LowerEyeLineBalanceAmount,
            scale,
            safeMax,
            "symmetry_lower_eye_line");
        AddSymmetryPairedHeightRegions(
            regions,
            vectors,
            EstimateEyebrowPoint(leftEye, faceHeight),
            EstimateEyebrowPoint(rightEye, faceHeight),
            analysis.UpperEyebrowHeightDelta,
            faceWidth * 0.20,
            faceHeight * 0.10,
            toolset.UpperEyebrowBalanceAmount,
            scale,
            safeMax,
            "symmetry_eyebrow_height");
        AddSymmetryPairedHeightRegions(
            regions,
            vectors,
            EstimateMouthCornerPoint(mouthCenter, faceWidth, -1),
            EstimateMouthCornerPoint(mouthCenter, faceWidth, 1),
            analysis.MouthCornerHeightDelta,
            faceWidth * 0.14,
            faceHeight * 0.10,
            toolset.MouthCornerBalanceAmount,
            scale,
            safeMax,
            "symmetry_mouth_corner");

        WpfPoint nostrilCenter = noseTip == default
            ? new WpfPoint(faceCenter.X, faceBox.Y + faceHeight * 0.52)
            : new WpfPoint(noseTip.X, noseTip.Y + faceHeight * 0.065);
        AddSymmetryPairedWidthRegions(
            regions,
            vectors,
            new WpfPoint(nostrilCenter.X - faceWidth * 0.055, nostrilCenter.Y),
            new WpfPoint(nostrilCenter.X + faceWidth * 0.055, nostrilCenter.Y),
            analysis.NostrilPositionDelta,
            faceWidth * 0.075,
            faceHeight * 0.055,
            toolset.NostrilPositionBalanceAmount * 0.42,
            scale,
            safeMax * 0.32,
            "symmetry_nostril_observation");
        AddSymmetryPairedWidthRegions(
            regions,
            vectors,
            new WpfPoint(faceCenter.X - faceWidth * 0.12, nostrilCenter.Y - faceHeight * 0.02),
            new WpfPoint(faceCenter.X + faceWidth * 0.12, nostrilCenter.Y - faceHeight * 0.02),
            analysis.NoseWingContourDelta,
            faceWidth * 0.12,
            faceHeight * 0.12,
            toolset.NoseWingContourBalanceAmount,
            scale,
            safeMax * 0.44,
            "symmetry_nose_wing_contour");
        AddSymmetryPairedWidthRegions(
            regions,
            vectors,
            new WpfPoint(faceBox.X + faceWidth * 0.23, faceBox.Y + faceHeight * 0.69),
            new WpfPoint(faceBox.X + faceWidth * 0.77, faceBox.Y + faceHeight * 0.69),
            analysis.JawlineContourDelta,
            faceWidth * 0.22,
            faceHeight * 0.22,
            toolset.JawlineContourBalanceAmount,
            scale,
            safeMax * 0.52,
            "symmetry_jawline_contour");
        AddSymmetryCenterLineRegion(
            regions,
            vectors,
            chinPoint,
            analysis.ChinCenterDelta,
            faceWidth * 0.22,
            faceHeight * 0.18,
            toolset.ChinCenterBalanceAmount,
            scale,
            safeMax * 0.50,
            "symmetry_chin_center");

        double averageStrength = regions.Count == 0 ? 0 : regions.Average(region => region.Strength);
        return new SymmetryBalanceMap(
            analysis.LeftRightCenterLine,
            regions,
            new ShapeBalanceWarpStrengthMap(width, height, scale, averageStrength, toolset.ProtectHardFeatures ? 0.74 : 0, 0.42),
            toolset.PreserveIdentityStrength,
            toolset.ProtectHardFeatures ? 0.74 : 0,
            toolset.IsOvershootZone,
            vectors);
    }

    private static void AddYawLikeBalanceRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        Int32Rect faceBox,
        WpfPoint faceCenter,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        WpfPoint chinPoint,
        ShapeBalanceAnalysisReport analysis,
        ShapeBalanceOptions options,
        double global,
        double maxDisplacement,
        double manualMaxDisplacement)
    {
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double yawSignal = Math.Clamp(
            Math.Clamp(options.ManualFaceBalanceShift, -1, 1) * 0.72 -
            Math.Clamp(analysis.FaceYawLikeBias, -1, 1) * Math.Clamp(options.HeadTurnCorrectAmount, 0, 1) * 0.58,
            -1,
            1);
        if (Math.Abs(yawSignal) < 0.001)
        {
            return;
        }

        double limit = Math.Abs(options.ManualFaceBalanceShift) > 0.001
            ? manualMaxDisplacement
            : maxDisplacement;
        double dx = Math.Clamp(yawSignal * limit * global * 0.92, -limit, limit);
        WpfPoint leftContour = new(faceBox.X + faceWidth * 0.24, faceBox.Y + faceHeight * 0.52);
        WpfPoint rightContour = new(faceBox.X + faceWidth * 0.76, faceBox.Y + faceHeight * 0.52);
        AddYawRegion(regions, vectors, leftContour, faceWidth * 0.28, faceHeight * 0.52, dx * 0.42, "yaw_like_left_face_contour");
        AddYawRegion(regions, vectors, rightContour, faceWidth * 0.28, faceHeight * 0.52, -dx * 0.42, "yaw_like_right_face_contour");
        AddYawRegion(regions, vectors, faceCenter, faceWidth * 0.38, faceHeight * 0.48, dx * 0.20, "yaw_like_face_center");
        AddYawRegion(regions, vectors, noseTip, faceWidth * 0.24, faceHeight * 0.34, dx * 0.34, "yaw_like_nose_axis");
        AddYawRegion(regions, vectors, mouthCenter, faceWidth * 0.30, faceHeight * 0.22, dx * 0.22, "yaw_like_mouth_center");
        AddYawRegion(regions, vectors, chinPoint, faceWidth * 0.26, faceHeight * 0.24, dx * 0.24, "yaw_like_chin_center");
    }

    private static void AddManualFaceContourRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        Int32Rect faceBox,
        WpfPoint faceCenter,
        WpfPoint chinPoint,
        ShapeBalanceOptions options,
        double global,
        double maxDisplacement,
        double manualMaxDisplacement)
    {
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double scale = Math.Clamp(global * (1 - Math.Clamp(options.PreserveIdentityStrength, 0, 1) * 0.18), 0.16, 0.78);
        double safeMax = maxDisplacement * 0.92;
        double oval = Math.Clamp(options.ManualOvalFaceAmount, 0, 1);
        if (oval > 0.001)
        {
            double dx = Math.Clamp(oval * safeMax * scale * 1.35, 0, safeMax);
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceBox.X + faceWidth * 0.20, faceBox.Y + faceHeight * 0.62),
                faceWidth * 0.24,
                faceHeight * 0.34,
                dx,
                0,
                0.40,
                "manual_oval_face_left");
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceBox.X + faceWidth * 0.80, faceBox.Y + faceHeight * 0.62),
                faceWidth * 0.24,
                faceHeight * 0.34,
                -dx,
                0,
                0.40,
                "manual_oval_face_right");
        }

        double cheekbone = Math.Clamp(options.ManualCheekboneSoftenAmount, 0, 1);
        if (cheekbone > 0.001)
        {
            double dx = Math.Clamp(cheekbone * safeMax * scale * 1.05, 0, safeMax);
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceBox.X + faceWidth * 0.18, faceBox.Y + faceHeight * 0.48),
                faceWidth * 0.20,
                faceHeight * 0.22,
                dx,
                0,
                0.34,
                "manual_cheekbone_soften_left");
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceBox.X + faceWidth * 0.82, faceBox.Y + faceHeight * 0.48),
                faceWidth * 0.20,
                faceHeight * 0.22,
                -dx,
                0,
                0.34,
                "manual_cheekbone_soften_right");
        }

        double chinWidth = Math.Clamp(options.ManualChinWidthShift, -1, 1);
        if (Math.Abs(chinWidth) > 0.001)
        {
            double dx = Math.Clamp(chinWidth * safeMax * scale * 1.20, -safeMax, safeMax);
            double chinY = chinPoint == default ? faceBox.Y + faceHeight * 0.82 : chinPoint.Y - faceHeight * 0.02;
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceCenter.X - faceWidth * 0.10, chinY),
                faceWidth * 0.15,
                faceHeight * 0.16,
                -dx,
                0,
                0.36,
                "manual_chin_width_left");
            AddManualWarpRegion(
                regions,
                vectors,
                new WpfPoint(faceCenter.X + faceWidth * 0.10, chinY),
                faceWidth * 0.15,
                faceHeight * 0.16,
                dx,
                0,
                0.36,
                "manual_chin_width_right");
        }

        double chinLength = Math.Clamp(options.ManualChinLengthShift, -1, 1);
        if (Math.Abs(chinLength) > 0.001)
        {
            double dy = Math.Clamp(chinLength * safeMax * scale * 1.22, -safeMax, safeMax);
            WpfPoint center = chinPoint == default
                ? new WpfPoint(faceCenter.X, faceBox.Y + faceHeight * 0.84)
                : chinPoint;
            AddManualWarpRegion(
                regions,
                vectors,
                center,
                faceWidth * 0.22,
                faceHeight * 0.18,
                0,
                dy,
                0.38,
                "manual_chin_length");
        }
    }

    private static void AddManualWarpRegion(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint center,
        double radiusX,
        double radiusY,
        double deltaX,
        double deltaY,
        double strength,
        string regionId)
    {
        if (center == default || (Math.Abs(deltaX) < 0.001 && Math.Abs(deltaY) < 0.001))
        {
            return;
        }

        regions.Add(new ShapeBalanceWarpRegion(center, radiusX, radiusY, deltaX, deltaY, strength, regionId));
        vectors.Add(new ShapeBalanceDebugVector(center, new WpfPoint(center.X + deltaX, center.Y + deltaY), regionId));
    }

    private static void AddYawRegion(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint center,
        double radiusX,
        double radiusY,
        double deltaX,
        string regionId)
    {
        if (center == default || Math.Abs(deltaX) < 0.001)
        {
            return;
        }

        regions.Add(new ShapeBalanceWarpRegion(center, radiusX, radiusY, deltaX, 0, 0.42, regionId));
        vectors.Add(new ShapeBalanceDebugVector(center, new WpfPoint(center.X + deltaX, center.Y), regionId));
    }

    private static List<ShapeBalanceProtectedRegion> BuildProtectedRegions(
        FaceSnapshotMaskSet snapshot,
        Int32Rect faceBox,
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter)
    {
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        List<ShapeBalanceProtectedRegion> regions = new();
        AddRegion(regions, leftEye, faceWidth * 0.20, faceHeight * 0.12, 0.62, "eye_protect_left");
        AddRegion(regions, rightEye, faceWidth * 0.20, faceHeight * 0.12, 0.62, "eye_protect_right");
        AddRegion(regions, mouthCenter, faceWidth * 0.28, faceHeight * 0.12, 0.58, "lip_protect");
        AddRegion(regions, noseTip, faceWidth * 0.14, faceHeight * 0.11, 0.70, "nostril_protect");

        if (snapshot.Masks.HairMask.Average() > 0.0005)
        {
            regions.Add(new ShapeBalanceProtectedRegion(
                new WpfPoint(faceBox.X + faceWidth / 2d, faceBox.Y + faceHeight * 0.08),
                faceWidth * 0.60,
                faceHeight * 0.22,
                0.48,
                "hair_boundary_protect"));
        }

        if (snapshot.Masks.BeardMask.Average() > 0.0005)
        {
            regions.Add(new ShapeBalanceProtectedRegion(
                new WpfPoint(faceBox.X + faceWidth / 2d, faceBox.Y + faceHeight * 0.72),
                faceWidth * 0.42,
                faceHeight * 0.24,
                0.52,
                "beard_protect"));
        }

        if (snapshot.Masks.GlassesMask.Average() > 0.0005)
        {
            regions.Add(new ShapeBalanceProtectedRegion(
                new WpfPoint(faceBox.X + faceWidth / 2d, faceBox.Y + faceHeight * 0.36),
                faceWidth * 0.48,
                faceHeight * 0.18,
                0.68,
                "glasses_protect"));
        }

        return regions;
    }

    private static void AddRegion(
        List<ShapeBalanceProtectedRegion> regions,
        WpfPoint center,
        double radiusX,
        double radiusY,
        double damping,
        string regionId)
    {
        if (center == default)
        {
            return;
        }

        regions.Add(new ShapeBalanceProtectedRegion(center, radiusX, radiusY, damping, regionId));
    }

    private static void AddPairedEyeLevelRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint leftEye,
        WpfPoint rightEye,
        double eyeLevelDelta,
        double faceWidth,
        double faceHeight,
        ShapeBalanceOptions options,
        double global,
        double maxDisplacement,
        double manualMaxDisplacement)
    {
        if (leftEye == default || rightEye == default || Math.Abs(eyeLevelDelta) < 0.5)
        {
            if (Math.Abs(options.ManualEyeLevelShift) < 0.001)
            {
                return;
            }
        }

        double amount = Math.Clamp(options.EyeLevelBalanceAmount, 0, 1) * global * 0.55;
        double leftDy = Math.Clamp(eyeLevelDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        double rightDy = Math.Clamp(-eyeLevelDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        double limit = Math.Abs(options.ManualEyeLevelShift) > 0.001
            ? manualMaxDisplacement
            : maxDisplacement;
        double manualDy = Math.Clamp(options.ManualEyeLevelShift, -1, 1) * manualMaxDisplacement * 0.78;
        leftDy = Math.Clamp(leftDy + manualDy, -limit, limit);
        rightDy = Math.Clamp(rightDy - manualDy, -limit, limit);
        double radiusX = faceWidth * 0.22;
        double radiusY = faceHeight * 0.13;
        regions.Add(new ShapeBalanceWarpRegion(leftEye, radiusX, radiusY, 0, leftDy, 0.75, "left_eye_level"));
        regions.Add(new ShapeBalanceWarpRegion(rightEye, radiusX, radiusY, 0, rightDy, 0.75, "right_eye_level"));
        vectors.Add(new ShapeBalanceDebugVector(leftEye, new WpfPoint(leftEye.X, leftEye.Y + leftDy), "left_eye_level"));
        vectors.Add(new ShapeBalanceDebugVector(rightEye, new WpfPoint(rightEye.X, rightEye.Y + rightDy), "right_eye_level"));
    }

    private static void AddCenterLineRegion(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        string regionId,
        WpfPoint center,
        double reportValue,
        double centerDelta,
        double manualShift,
        double radiusX,
        double radiusY,
        double optionAmount,
        double global,
        double maxDisplacement,
        double manualMaxDisplacement)
    {
        if (center == default || (Math.Abs(centerDelta) < 0.5 && Math.Abs(manualShift) < 0.001))
        {
            return;
        }

        double dx = Math.Clamp(-centerDelta * Math.Clamp(optionAmount, 0, 1) * global * 0.28, -maxDisplacement, maxDisplacement);
        double limit = Math.Abs(manualShift) > 0.001
            ? manualMaxDisplacement
            : maxDisplacement;
        dx = Math.Clamp(dx + Math.Clamp(manualShift, -1, 1) * manualMaxDisplacement * 0.82, -limit, limit);
        double strength = Math.Abs(reportValue) > 0.001 ? 0.62 : 0.52;
        regions.Add(new ShapeBalanceWarpRegion(center, radiusX, radiusY, dx, 0, strength, regionId));
        vectors.Add(new ShapeBalanceDebugVector(center, new WpfPoint(center.X + dx, center.Y), regionId));
    }

    private static void AddPairedHeightRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint leftPoint,
        WpfPoint rightPoint,
        double measuredDelta,
        double manualShift,
        double radiusX,
        double radiusY,
        double optionAmount,
        double global,
        double maxDisplacement,
        double manualMaxDisplacement,
        string leftRegionId,
        string rightRegionId,
        string vectorLabel)
    {
        if (leftPoint == default || rightPoint == default || (Math.Abs(measuredDelta) < 0.5 && Math.Abs(manualShift) < 0.001))
        {
            return;
        }

        double amount = Math.Clamp(optionAmount, 0, 1) * global * 0.45;
        double leftDy = Math.Clamp(measuredDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        double rightDy = Math.Clamp(-measuredDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        double limit = Math.Abs(manualShift) > 0.001
            ? manualMaxDisplacement
            : maxDisplacement;
        double manualDy = Math.Clamp(manualShift, -1, 1) * manualMaxDisplacement * 0.68;
        leftDy = Math.Clamp(leftDy + manualDy, -limit, limit);
        rightDy = Math.Clamp(rightDy - manualDy, -limit, limit);
        regions.Add(new ShapeBalanceWarpRegion(leftPoint, radiusX, radiusY, 0, leftDy, 0.52, leftRegionId));
        regions.Add(new ShapeBalanceWarpRegion(rightPoint, radiusX, radiusY, 0, rightDy, 0.52, rightRegionId));
        vectors.Add(new ShapeBalanceDebugVector(leftPoint, new WpfPoint(leftPoint.X, leftPoint.Y + leftDy), vectorLabel + "_left"));
        vectors.Add(new ShapeBalanceDebugVector(rightPoint, new WpfPoint(rightPoint.X, rightPoint.Y + rightDy), vectorLabel + "_right"));
    }

    private static void AddSymmetryPairedHeightRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint leftPoint,
        WpfPoint rightPoint,
        double measuredDelta,
        double radiusX,
        double radiusY,
        double optionAmount,
        double scale,
        double maxDisplacement,
        string regionId)
    {
        if (leftPoint == default || rightPoint == default || Math.Abs(measuredDelta) < 0.35)
        {
            return;
        }

        double amount = Math.Clamp(optionAmount, 0, 1) * scale * 0.36;
        double leftDy = Math.Clamp(measuredDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        double rightDy = Math.Clamp(-measuredDelta * 0.5 * amount, -maxDisplacement, maxDisplacement);
        if (Math.Abs(leftDy) < 0.05 && Math.Abs(rightDy) < 0.05)
        {
            return;
        }

        regions.Add(new ShapeBalanceWarpRegion(leftPoint, radiusX, radiusY, 0, leftDy, 0.42, regionId + "_left"));
        regions.Add(new ShapeBalanceWarpRegion(rightPoint, radiusX, radiusY, 0, rightDy, 0.42, regionId + "_right"));
        vectors.Add(new ShapeBalanceDebugVector(leftPoint, new WpfPoint(leftPoint.X, leftPoint.Y + leftDy), regionId + "_left"));
        vectors.Add(new ShapeBalanceDebugVector(rightPoint, new WpfPoint(rightPoint.X, rightPoint.Y + rightDy), regionId + "_right"));
    }

    private static void AddSymmetryPairedWidthRegions(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint leftPoint,
        WpfPoint rightPoint,
        double measuredDelta,
        double radiusX,
        double radiusY,
        double optionAmount,
        double scale,
        double maxDisplacement,
        string regionId)
    {
        if (leftPoint == default || rightPoint == default || Math.Abs(measuredDelta) < 0.002)
        {
            return;
        }

        double amount = Math.Clamp(optionAmount, 0, 1) * scale * 0.34;
        double dx = Math.Clamp(measuredDelta * maxDisplacement * amount, -maxDisplacement, maxDisplacement);
        if (Math.Abs(dx) < 0.05)
        {
            return;
        }

        regions.Add(new ShapeBalanceWarpRegion(leftPoint, radiusX, radiusY, dx * 0.5, 0, 0.36, regionId + "_left"));
        regions.Add(new ShapeBalanceWarpRegion(rightPoint, radiusX, radiusY, -dx * 0.5, 0, 0.36, regionId + "_right"));
        vectors.Add(new ShapeBalanceDebugVector(leftPoint, new WpfPoint(leftPoint.X + dx * 0.5, leftPoint.Y), regionId + "_left"));
        vectors.Add(new ShapeBalanceDebugVector(rightPoint, new WpfPoint(rightPoint.X - dx * 0.5, rightPoint.Y), regionId + "_right"));
    }

    private static void AddSymmetryCenterLineRegion(
        List<ShapeBalanceWarpRegion> regions,
        List<ShapeBalanceDebugVector> vectors,
        WpfPoint center,
        double measuredDelta,
        double radiusX,
        double radiusY,
        double optionAmount,
        double scale,
        double maxDisplacement,
        string regionId)
    {
        if (center == default || Math.Abs(measuredDelta) < 0.35)
        {
            return;
        }

        double dx = Math.Clamp(-measuredDelta * Math.Clamp(optionAmount, 0, 1) * scale * 0.26, -maxDisplacement, maxDisplacement);
        if (Math.Abs(dx) < 0.05)
        {
            return;
        }

        regions.Add(new ShapeBalanceWarpRegion(center, radiusX, radiusY, dx, 0, 0.38, regionId));
        vectors.Add(new ShapeBalanceDebugVector(center, new WpfPoint(center.X + dx, center.Y), regionId));
    }

    private static SideMaskStats CalculateSideMaskStats(MaskPlane mask, double centerX, double minY = 0)
    {
        double leftArea = 0;
        double rightArea = 0;
        double leftX = 0;
        double rightX = 0;
        double leftY = 0;
        double rightY = 0;
        int yStart = Math.Clamp((int)Math.Floor(minY), 0, Math.Max(0, mask.Height - 1));
        for (int y = yStart; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                double value = mask[x, y];
                if (value <= 0.0001)
                {
                    continue;
                }

                if (x < centerX)
                {
                    leftArea += value;
                    leftX += x * value;
                    leftY += y * value;
                }
                else
                {
                    rightArea += value;
                    rightX += x * value;
                    rightY += y * value;
                }
            }
        }

        double total = leftArea + rightArea;
        if (total <= 0.0001 || leftArea <= 0.0001 || rightArea <= 0.0001)
        {
            return SideMaskStats.Empty;
        }

        WpfPoint leftCenter = new(leftX / leftArea, leftY / leftArea);
        WpfPoint rightCenter = new(rightX / rightArea, rightY / rightArea);
        return new SideMaskStats(
            leftArea,
            rightArea,
            (rightArea - leftArea) / total,
            rightCenter.X - (mask.Width - leftCenter.X),
            rightCenter.Y - leftCenter.Y);
    }

    private static WpfPoint EstimateEyebrowPoint(WpfPoint eyePoint, double faceHeight)
    {
        return eyePoint == default
            ? default
            : new WpfPoint(eyePoint.X, eyePoint.Y - faceHeight * 0.12);
    }

    private static WpfPoint EstimateMouthCornerPoint(WpfPoint mouthCenter, double faceWidth, int side)
    {
        return mouthCenter == default
            ? default
            : new WpfPoint(mouthCenter.X + side * faceWidth * 0.16, mouthCenter.Y);
    }

    private static bool TryGetPoint(IReadOnlyDictionary<string, WpfPoint> landmarks, string key, out WpfPoint point)
    {
        return landmarks.TryGetValue(key, out point);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180d;
    }

    private sealed record SideMaskStats(
        double LeftArea,
        double RightArea,
        double NormalizedAreaDelta,
        double CenterXDelta,
        double CenterYDelta)
    {
        public static SideMaskStats Empty { get; } = new(0, 0, 0, 0, 0);
    }
}
