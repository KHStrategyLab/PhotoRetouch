using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class FeatureMeshGenerator
{
    public const int PointsPerFeature = FaceFeatureMesh.DefaultPointCount;

    public static FaceFeatureMeshSet Generate(MaskWarpInput input, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(landmarks);

        return new FaceFeatureMeshSet(
            GenerateLipMesh(input, landmarks),
            GenerateEyeMesh(input),
            GenerateNoseMesh(input),
            GenerateBrowMesh(input));
    }

    public static FaceFeatureMeshSet GenerateFromMaskGuides(
        MaskWarpInput input,
        IReadOnlyDictionary<string, WpfPoint> landmarks,
        MaskPlane eyeGuideMask,
        MaskPlane lipGuideMask,
        MaskPlane noseGuideMask,
        MaskPlane browGuideMask)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(landmarks);
        ArgumentNullException.ThrowIfNull(eyeGuideMask);
        ArgumentNullException.ThrowIfNull(lipGuideMask);
        ArgumentNullException.ThrowIfNull(noseGuideMask);
        ArgumentNullException.ThrowIfNull(browGuideMask);

        return new FaceFeatureMeshSet(
            GenerateSingleFeatureFromMask(
                FaceFeatureType.Lip,
                lipGuideMask,
                input.MouthCenter,
                PointsPerFeature,
                "lip_bw_contour",
                "bw_lip_mask_contour_50",
                () => GenerateLipMesh(input, landmarks)),
            GeneratePairedFeatureFromMask(
                FaceFeatureType.Eye,
                eyeGuideMask,
                input.LeftEyeCenter,
                input.RightEyeCenter,
                "left_eye_bw_contour",
                "right_eye_bw_contour",
                "bw_eye_mask_contour_50",
                () => GenerateEyeMesh(input)),
            GenerateSingleFeatureFromMask(
                FaceFeatureType.Nose,
                noseGuideMask,
                input.NoseTip,
                PointsPerFeature,
                "nose_bw_contour",
                "bw_nose_soft_mask_contour_50",
                () => GenerateNoseMesh(input)),
            GeneratePairedFeatureFromMask(
                FaceFeatureType.Brow,
                browGuideMask,
                new WpfPoint(input.LeftEyeCenter.X, input.LeftEyeCenter.Y - input.FaceBox.Height * 0.075),
                new WpfPoint(input.RightEyeCenter.X, input.RightEyeCenter.Y - input.FaceBox.Height * 0.075),
                "left_brow_bw_contour",
                "right_brow_bw_contour",
                "bw_brow_mask_contour_50",
                () => GenerateBrowMesh(input)));
    }

    public static FaceFeatureMeshSet Generate(int targetImageWidth, int targetImageHeight, FaceAnalysisResult analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);
        WpfPoint leftEye = GetLandmarkOrDefault(analysis, "left_eye", analysis.FaceBox.X + analysis.FaceBox.Width * 0.35, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.38);
        WpfPoint rightEye = GetLandmarkOrDefault(analysis, "right_eye", analysis.FaceBox.X + analysis.FaceBox.Width * 0.65, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.38);
        WpfPoint noseTip = GetLandmarkOrDefault(analysis, "nose_tip", analysis.FaceBox.X + analysis.FaceBox.Width * 0.50, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.56);
        WpfPoint mouthCenter = GetLandmarkOrDefault(analysis, "mouth_center", analysis.FaceBox.X + analysis.FaceBox.Width * 0.50, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.72);
        WpfPoint chin = GetLandmarkOrDefault(analysis, "chin", analysis.FaceBox.X + analysis.FaceBox.Width * 0.50, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.92);
        double angle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X);
        MaskWarpInput input = new(
            targetImageWidth,
            targetImageHeight,
            analysis.FaceBox,
            angle,
            leftEye,
            rightEye,
            noseTip,
            mouthCenter,
            chin);
        return Generate(input, analysis.FaceLandmarks);
    }

    public static FaceFeatureMesh GenerateLipMesh(MaskWarpInput input, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        WpfPoint left = landmarks.TryGetValue("mouth_left", out WpfPoint mouthLeft)
            ? mouthLeft
            : new WpfPoint(input.MouthCenter.X - input.FaceBox.Width * 0.10, input.MouthCenter.Y);
        WpfPoint right = landmarks.TryGetValue("mouth_right", out WpfPoint mouthRight)
            ? mouthRight
            : new WpfPoint(input.MouthCenter.X + input.FaceBox.Width * 0.10, input.MouthCenter.Y);

        double mouthWidth = Math.Max(2, Distance(left, right));
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        double angle = Math.Atan2(right.Y - left.Y, right.X - left.X);
        WpfPoint center = Midpoint(left, right);
        double halfWidth = Math.Clamp(mouthWidth * 0.55, input.FaceBox.Width * 0.065, input.FaceBox.Width * 0.18);
        double upperHeight = Math.Clamp(mouthWidth * 0.092, faceHeight * 0.012, faceHeight * 0.032);
        double lowerHeight = Math.Clamp(mouthWidth * 0.118, faceHeight * 0.017, faceHeight * 0.044);
        double centerOffset = Math.Clamp(faceHeight * 0.004, 1, 4);

        List<FeatureMeshPoint> points = new(PointsPerFeature);
        for (int index = 0; index < PointsPerFeature; index++)
        {
            double t = index / (double)PointsPerFeature;
            double theta = Math.PI * 2 * t;
            double localX = Math.Cos(theta) * halfWidth;
            double nx = Math.Abs(localX) / Math.Max(1, halfWidth);
            double taper = Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 1.92)), 0.58);
            double cupidBowLift = Math.Exp(-Math.Pow(localX / Math.Max(1, halfWidth * 0.22), 2)) * upperHeight * 0.50;
            double upperLobeLift = (
                Math.Exp(-Math.Pow((localX - halfWidth * 0.34) / Math.Max(1, halfWidth * 0.20), 2)) +
                Math.Exp(-Math.Pow((localX + halfWidth * 0.34) / Math.Max(1, halfWidth * 0.20), 2))) * upperHeight * 0.22;
            double upperLimit = upperHeight * taper - cupidBowLift + upperLobeLift - centerOffset;
            double lowerBulge = Math.Exp(-Math.Pow(localX / Math.Max(1, halfWidth * 0.58), 2)) * lowerHeight * 0.22;
            double lowerLimit = lowerHeight * Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 2.20)), 0.50) + lowerBulge + centerOffset;
            double localY = Math.Sin(theta) < 0
                ? -upperLimit
                : lowerLimit;
            points.Add(CreatePoint(index, center, localX, localY, angle, "lip_contour"));
        }

        return new FaceFeatureMesh(FaceFeatureType.Lip, points, 0.45, "landmark_mouth_corners_dummy_50");
    }

    public static FaceFeatureMesh GenerateEyeMesh(MaskWarpInput input)
    {
        double eyeDistance = Math.Max(2, Distance(input.LeftEyeCenter, input.RightEyeCenter));
        double faceWidth = Math.Max(1, input.FaceBox.Width);
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        double radiusX = Math.Clamp(Math.Min(faceWidth * 0.100, eyeDistance * 0.205), 7, faceWidth * 0.145);
        double radiusY = Math.Clamp(faceHeight * 0.026, 3, faceHeight * 0.045);
        double angle = Math.Atan2(input.RightEyeCenter.Y - input.LeftEyeCenter.Y, input.RightEyeCenter.X - input.LeftEyeCenter.X);
        List<FeatureMeshPoint> points = new(PointsPerFeature);
        AddEyePoints(points, input.LeftEyeCenter, radiusX, radiusY, angle, 0, "left_eye_contour");
        AddEyePoints(points, input.RightEyeCenter, radiusX, radiusY, angle, 25, "right_eye_contour");
        return new FaceFeatureMesh(FaceFeatureType.Eye, points, 0.45, "landmark_eye_centers_dummy_50");
    }

    public static FaceFeatureMesh GenerateNoseMesh(MaskWarpInput input)
    {
        double eyeDistance = Math.Max(2, Distance(input.LeftEyeCenter, input.RightEyeCenter));
        double faceWidth = Math.Max(1, input.FaceBox.Width);
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        WpfPoint eyeMid = Midpoint(input.LeftEyeCenter, input.RightEyeCenter);
        double angle = Math.Atan2(input.RightEyeCenter.Y - input.LeftEyeCenter.Y, input.RightEyeCenter.X - input.LeftEyeCenter.X);
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        WpfPoint axisX = new(cos, sin);
        WpfPoint axisY = new(-sin, cos);
        WpfPoint topCenter = Add(eyeMid, axisY, -Math.Clamp(faceHeight * 0.090, 18, 60));
        WpfPoint bottomCenter = Add(input.NoseTip, axisY, Math.Clamp(faceHeight * 0.055, 10, 38));
        double topHalfWidth = Math.Clamp(Math.Min(faceWidth * 0.070, eyeDistance * 0.170), 14, faceWidth * 0.115);
        double bottomHalfWidth = Math.Clamp(Math.Min(faceWidth * 0.125, eyeDistance * 0.285), 16, faceWidth * 0.18);

        List<FeatureMeshPoint> points = new(PointsPerFeature);
        for (int index = 0; index < PointsPerFeature; index++)
        {
            double t = index / (double)PointsPerFeature;
            double theta = Math.PI * 2 * t;
            double yRatio = (Math.Sin(theta) + 1) * 0.5;
            double widthAtY = topHalfWidth + (bottomHalfWidth - topHalfWidth) * SmoothStep(0, 1, yRatio);
            double localX = Math.Cos(theta) * widthAtY;
            double localY = yRatio * Distance(topCenter, bottomCenter);
            WpfPoint point = Add(Add(topCenter, axisX, localX), axisY, localY);
            points.Add(new FeatureMeshPoint(index, point.X, point.Y, 1, "nose_soft_shield_contour"));
        }

        return new FaceFeatureMesh(FaceFeatureType.Nose, points, 0.42, "landmark_nose_soft_shield_dummy_50");
    }

    public static FaceFeatureMesh GenerateBrowMesh(MaskWarpInput input)
    {
        double eyeDistance = Math.Max(2, Distance(input.LeftEyeCenter, input.RightEyeCenter));
        double faceWidth = Math.Max(1, input.FaceBox.Width);
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        double angle = Math.Atan2(input.RightEyeCenter.Y - input.LeftEyeCenter.Y, input.RightEyeCenter.X - input.LeftEyeCenter.X);
        double halfLength = Math.Clamp(Math.Min(faceWidth * 0.115, eyeDistance * 0.245), 9, faceWidth * 0.16);
        double thickness = Math.Clamp(faceHeight * 0.013, 2.0, faceHeight * 0.026);
        double lift = Math.Clamp(faceHeight * 0.075, 12, 42);
        List<FeatureMeshPoint> points = new(PointsPerFeature);
        AddBrowPoints(points, new WpfPoint(input.LeftEyeCenter.X, input.LeftEyeCenter.Y - lift), halfLength, thickness, angle, 0, "left_brow_contour");
        AddBrowPoints(points, new WpfPoint(input.RightEyeCenter.X, input.RightEyeCenter.Y - lift), halfLength, thickness, angle, 25, "right_brow_contour");
        return new FaceFeatureMesh(FaceFeatureType.Brow, points, 0.35, "landmark_eye_centers_brow_dummy_50");
    }

    private static void AddEyePoints(List<FeatureMeshPoint> points, WpfPoint center, double radiusX, double radiusY, double angle, int startIndex, string role)
    {
        for (int offset = 0; offset < 25; offset++)
        {
            double theta = Math.PI * 2 * offset / 25d;
            double localX = Math.Cos(theta) * radiusX;
            double nx = Math.Abs(localX) / Math.Max(1, radiusX);
            double lidLimit = radiusY * Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 1.54)), 0.70);
            double localY = Math.Sin(theta) < 0 ? -lidLimit : lidLimit;
            points.Add(CreatePoint(startIndex + offset, center, localX, localY, angle, role));
        }
    }

    private static void AddBrowPoints(List<FeatureMeshPoint> points, WpfPoint center, double halfLength, double thickness, double angle, int startIndex, string role)
    {
        for (int offset = 0; offset < 25; offset++)
        {
            double theta = Math.PI * 2 * offset / 25d;
            double localX = Math.Cos(theta) * halfLength;
            double arch = -Math.Sin((localX / Math.Max(1, halfLength) + 1) * Math.PI) * thickness * 0.75;
            double localY = Math.Sin(theta) * thickness + arch;
            points.Add(CreatePoint(startIndex + offset, center, localX, localY, angle, role));
        }
    }

    private static FeatureMeshPoint CreatePoint(int index, WpfPoint center, double localX, double localY, double angle, string role)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        return new FeatureMeshPoint(
            index,
            center.X + localX * cos - localY * sin,
            center.Y + localX * sin + localY * cos,
            1,
            role);
    }

    private static WpfPoint Add(WpfPoint point, WpfPoint direction, double distance)
    {
        return new WpfPoint(point.X + direction.X * distance, point.Y + direction.Y * distance);
    }

    private static WpfPoint Midpoint(WpfPoint left, WpfPoint right)
    {
        return new WpfPoint((left.X + right.X) / 2, (left.Y + right.Y) / 2);
    }

    private static WpfPoint GetLandmarkOrDefault(FaceAnalysisResult analysis, string key, double defaultX, double defaultY)
    {
        return analysis.FaceLandmarks.TryGetValue(key, out WpfPoint point)
            ? point
            : new WpfPoint(defaultX, defaultY);
    }

    private static FaceFeatureMesh GenerateSingleFeatureFromMask(
        FaceFeatureType featureType,
        MaskPlane mask,
        WpfPoint centerHint,
        int pointCount,
        string role,
        string source,
        Func<FaceFeatureMesh> fallbackFactory)
    {
        List<FeatureMeshPoint> points = SampleBoundaryPoints(mask, centerHint, pointCount, role, 0);
        return points.Count == pointCount
            ? new FaceFeatureMesh(featureType, points, 0.58, source)
            : fallbackFactory();
    }

    private static FaceFeatureMesh GeneratePairedFeatureFromMask(
        FaceFeatureType featureType,
        MaskPlane mask,
        WpfPoint leftCenterHint,
        WpfPoint rightCenterHint,
        string leftRole,
        string rightRole,
        string source,
        Func<FaceFeatureMesh> fallbackFactory)
    {
        double splitX = (leftCenterHint.X + rightCenterHint.X) / 2;
        List<FeatureMeshPoint> points = new(PointsPerFeature);
        List<FeatureMeshPoint> left = SampleBoundaryPoints(
            mask,
            leftCenterHint,
            PointsPerFeature / 2,
            leftRole,
            0,
            candidate => candidate.X <= splitX);
        List<FeatureMeshPoint> right = SampleBoundaryPoints(
            mask,
            rightCenterHint,
            PointsPerFeature / 2,
            rightRole,
            PointsPerFeature / 2,
            candidate => candidate.X > splitX);

        points.AddRange(left);
        points.AddRange(right);
        return points.Count == PointsPerFeature
            ? new FaceFeatureMesh(featureType, points, 0.58, source)
            : fallbackFactory();
    }

    private static List<FeatureMeshPoint> SampleBoundaryPoints(
        MaskPlane mask,
        WpfPoint centerHint,
        int pointCount,
        string role,
        int startIndex,
        Func<WpfPoint, bool>? candidateFilter = null)
    {
        List<WpfPoint> boundary = new();
        const double threshold = 0.08;
        for (int y = 1; y < mask.Height - 1; y++)
        {
            for (int x = 1; x < mask.Width - 1; x++)
            {
                if (mask[x, y] < threshold)
                {
                    continue;
                }

                WpfPoint point = new(x, y);
                if (candidateFilter is not null && !candidateFilter(point))
                {
                    continue;
                }

                bool edge =
                    mask[x - 1, y] < threshold ||
                    mask[x + 1, y] < threshold ||
                    mask[x, y - 1] < threshold ||
                    mask[x, y + 1] < threshold;
                if (edge)
                {
                    boundary.Add(point);
                }
            }
        }

        if (boundary.Count < pointCount)
        {
            boundary.Clear();
            for (int y = 0; y < mask.Height; y++)
            {
                for (int x = 0; x < mask.Width; x++)
                {
                    if (mask[x, y] < threshold)
                    {
                        continue;
                    }

                    WpfPoint point = new(x, y);
                    if (candidateFilter is null || candidateFilter(point))
                    {
                        boundary.Add(point);
                    }
                }
            }
        }

        if (boundary.Count < pointCount)
        {
            return new List<FeatureMeshPoint>();
        }

        WpfPoint center = GetCentroid(boundary, centerHint);
        List<WpfPoint> ordered = boundary
            .OrderBy(point => Math.Atan2(point.Y - center.Y, point.X - center.X))
            .ThenBy(point => Distance(point, center))
            .ToList();
        List<FeatureMeshPoint> result = new(pointCount);
        for (int index = 0; index < pointCount; index++)
        {
            int sampleIndex = Math.Clamp((int)Math.Round(index * (ordered.Count - 1) / Math.Max(1d, pointCount - 1d)), 0, ordered.Count - 1);
            WpfPoint point = ordered[sampleIndex];
            result.Add(new FeatureMeshPoint(startIndex + index, point.X, point.Y, 1, role));
        }

        return result;
    }

    private static WpfPoint GetCentroid(IReadOnlyList<WpfPoint> points, WpfPoint fallback)
    {
        if (points.Count == 0)
        {
            return fallback;
        }

        double x = 0;
        double y = 0;
        foreach (WpfPoint point in points)
        {
            x += point.X;
            y += point.Y;
        }

        return new WpfPoint(x / points.Count, y / points.Count);
    }

    private static double Distance(WpfPoint left, WpfPoint right)
    {
        double dx = right.X - left.X;
        double dy = right.Y - left.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
