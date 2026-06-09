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
        WpfPoint faceCenter = new(faceBox.X + faceBox.Width / 2d, faceBox.Y + faceBox.Height / 2d);
        if (!options.EnableShapeBalance)
        {
            return ShapeBalanceMap.Identity(width, height, faceBox);
        }

        double global = Math.Clamp(options.GlobalShapeBalanceAmount, 0, 1);
        double identityPreserve = Math.Clamp(options.PreserveIdentityStrength, 0, 1);
        double maxDisplacement = Math.Max(1, Math.Min(faceBox.Width, faceBox.Height) * 0.035 * Math.Clamp(options.MaxAllowedWarpStrength, 0.05, 1));
        double maxRotationRadians = DegreesToRadians(4.0 * Math.Clamp(options.MaxAllowedWarpStrength, 0.05, 1));
        double rotationRadians = DegreesToRadians(-analysis.FaceRollAngle) *
            Math.Clamp(options.HeadTiltCorrectAmount, 0, 1) *
            global *
            (1 - identityPreserve * 0.35);
        rotationRadians = Math.Clamp(rotationRadians, -maxRotationRadians, maxRotationRadians);
        double manualFaceDx = Math.Clamp(options.ManualFaceBalanceShift, -1, 1) * maxDisplacement * 0.35;
        double pitchShear = Math.Clamp(
            -analysis.FacePitchLikeBias * Math.Clamp(options.HeadPitchCorrectAmount, 0, 1) * global * 0.035,
            -0.018,
            0.018);
        ShapeBalanceGlobalTransform globalTransform = new(
            faceCenter,
            rotationRadians,
            manualFaceDx,
            0,
            1,
            1,
            pitchShear);

        IReadOnlyDictionary<string, WpfPoint> landmarks = snapshot.Analysis.FaceLandmarks;
        TryGetPoint(landmarks, "left_eye", out WpfPoint leftEye);
        TryGetPoint(landmarks, "right_eye", out WpfPoint rightEye);
        TryGetPoint(landmarks, "nose_tip", out WpfPoint noseTip);
        TryGetPoint(landmarks, "mouth_center", out WpfPoint mouthCenter);
        TryGetPoint(landmarks, "chin", out WpfPoint chinPoint);

        List<ShapeBalanceWarpRegion> regions = new();
        List<ShapeBalanceProtectedRegion> protectedRegions = BuildProtectedRegions(snapshot, faceBox, leftEye, rightEye, noseTip, mouthCenter);
        List<ShapeBalanceDebugVector> vectors = new();
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double eyeCenterX = leftEye != default || rightEye != default
            ? (leftEye.X + rightEye.X) / 2d
            : faceCenter.X;

        AddPairedEyeLevelRegions(regions, vectors, leftEye, rightEye, analysis.EyeLevelDelta, faceWidth, faceHeight, options, global, maxDisplacement);
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
            "left_mouth_corner",
            "right_mouth_corner",
            "mouth_corner");
        AddCenterLineRegion(
            regions,
            vectors,
            "nose_center",
            noseTip,
            analysis.NoseLineTilt,
            noseTip.X - eyeCenterX,
            options.ManualNoseCenterShift,
            faceWidth * 0.18,
            faceHeight * 0.28,
            options.NoseCenterBalanceAmount,
            global,
            maxDisplacement);
        AddCenterLineRegion(
            regions,
            vectors,
            "chin_center",
            chinPoint,
            analysis.ChinCenterDelta,
            chinPoint.X - eyeCenterX,
            options.ManualChinCenterShift,
            faceWidth * 0.24,
            faceHeight * 0.22,
            options.ChinCenterBalanceAmount,
            global,
            maxDisplacement);

        if (Math.Abs(analysis.FaceYawLikeBias) > 0.015 && mouthCenter != default)
        {
            double yawDx = Math.Clamp(-analysis.FaceYawLikeBias * faceWidth * options.HeadTurnCorrectAmount * global * 0.18, -maxDisplacement, maxDisplacement);
            regions.Add(new ShapeBalanceWarpRegion(mouthCenter, faceWidth * 0.34, faceHeight * 0.38, yawDx, 0, 0.45, "head_turn_balance"));
            vectors.Add(new ShapeBalanceDebugVector(mouthCenter, new WpfPoint(mouthCenter.X + yawDx, mouthCenter.Y), "head_turn"));
        }

        double noseDelta = noseTip == default ? 0 : noseTip.X - eyeCenterX;
        return new ShapeBalanceMap(
            width,
            height,
            width,
            height,
            globalTransform,
            faceCenter,
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
                regions.Count == 0 ? 0 : regions.Average(region => region.Strength),
                options.ProtectHardFeatures ? 0.72 : 0,
                0.35),
            vectors,
            DateTime.UtcNow,
            "shape_balance_map_v2");
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
        double maxDisplacement)
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
        double manualDy = Math.Clamp(options.ManualEyeLevelShift, -1, 1) * maxDisplacement * 0.70;
        leftDy = Math.Clamp(leftDy + manualDy, -maxDisplacement, maxDisplacement);
        rightDy = Math.Clamp(rightDy - manualDy, -maxDisplacement, maxDisplacement);
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
        double maxDisplacement)
    {
        if (center == default || (Math.Abs(centerDelta) < 0.5 && Math.Abs(manualShift) < 0.001))
        {
            return;
        }

        double dx = Math.Clamp(-centerDelta * Math.Clamp(optionAmount, 0, 1) * global * 0.28, -maxDisplacement, maxDisplacement);
        dx = Math.Clamp(dx + Math.Clamp(manualShift, -1, 1) * maxDisplacement * 0.75, -maxDisplacement, maxDisplacement);
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
        double manualDy = Math.Clamp(manualShift, -1, 1) * maxDisplacement * 0.58;
        leftDy = Math.Clamp(leftDy + manualDy, -maxDisplacement, maxDisplacement);
        rightDy = Math.Clamp(rightDy - manualDy, -maxDisplacement, maxDisplacement);
        regions.Add(new ShapeBalanceWarpRegion(leftPoint, radiusX, radiusY, 0, leftDy, 0.52, leftRegionId));
        regions.Add(new ShapeBalanceWarpRegion(rightPoint, radiusX, radiusY, 0, rightDy, 0.52, rightRegionId));
        vectors.Add(new ShapeBalanceDebugVector(leftPoint, new WpfPoint(leftPoint.X, leftPoint.Y + leftDy), vectorLabel + "_left"));
        vectors.Add(new ShapeBalanceDebugVector(rightPoint, new WpfPoint(rightPoint.X, rightPoint.Y + rightDy), vectorLabel + "_right"));
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
}
