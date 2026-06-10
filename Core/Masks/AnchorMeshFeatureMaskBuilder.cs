using PhotoRetouch.AnchorMesh;

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
    public static AnchorMeshFeatureMaskSet Build(int width, int height, AnchorMeshResult? anchorMesh)
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
        MaskPlane leftEye = FillClosedFeature(width, height, features.LeftEye, 1.0, 2.0);
        MaskPlane rightEye = FillClosedFeature(width, height, features.RightEye, 1.0, 2.0);
        MaskPlane eyeMask = MaskPlane.Union(leftEye, rightEye);

        MaskPlane leftBrow = StrokeOpenFeature(width, height, features.LeftBrow, 1.0, GetFeatureStrokeRadius(features.LeftBrow, 0.22, 3.0, 10.0), 2.0);
        MaskPlane rightBrow = StrokeOpenFeature(width, height, features.RightBrow, 1.0, GetFeatureStrokeRadius(features.RightBrow, 0.22, 3.0, 10.0), 2.0);
        MaskPlane eyebrowMask = MaskPlane.Union(leftBrow, rightBrow);

        MaskPlane lipMask = MaskPlane.Union(
            FillClosedFeature(width, height, features.LipOuter, 1.0, 2.4),
            FillClosedFeature(width, height, features.LipInner, 1.0, 1.4));
        MaskPlane innerMouthMask = FillClosedFeature(width, height, features.LipInner, 1.0, 1.2);

        MaskPlane noseMask = StrokeOpenFeature(width, height, features.Nose, 0.48, GetFeatureStrokeRadius(features.Nose, 0.16, 8.0, 28.0), 14.0);
        MaskPlane noseSkinMask = MaskPlane.Multiply(noseMask, 0.55);
        MaskPlane nostrilMask = BuildNostrilMask(width, height, features.Nose);
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

    private static MaskPlane BuildNostrilMask(int width, int height, AnchorMeshFeature? noseFeature)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (noseFeature is null)
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

        double radiusX = Math.Clamp(noseFeature.Width * 0.045, 3.0, 12.0);
        double radiusY = Math.Clamp(noseFeature.Height * 0.030, 2.0, 8.0);
        foreach (AnchorMeshPoint point in nostrilPoints)
        {
            AddEllipse(mask, point.SnappedX, point.SnappedY, radiusX, radiusY, 1.0, 1.8);
        }

        return mask;
    }

    private static void AddEllipse(MaskPlane mask, double centerX, double centerY, double radiusX, double radiusY, double opacity, double featherRadius)
    {
        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX - featherRadius - 2));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX + featherRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY - featherRadius - 2));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY + featherRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double normalized = Math.Sqrt(
                    Math.Pow((x + 0.5 - centerX) / Math.Max(0.5, radiusX), 2) +
                    Math.Pow((y + 0.5 - centerY) / Math.Max(0.5, radiusY), 2));
                double amount = normalized <= 1
                    ? opacity
                    : opacity * (1 - SmoothStep(1, 1 + featherRadius / Math.Max(radiusX, radiusY), normalized));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }
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
