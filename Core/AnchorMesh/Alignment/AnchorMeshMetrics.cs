using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshMetrics
{
    public static void Update(AnchorMeshFeature feature, float angleRad)
    {
        if (feature.Points.Count == 0)
        {
            feature.IsValid = false;
            feature.Bounds = RectangleF.Empty;
            return;
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        float sumX = 0;
        float sumY = 0;

        foreach (AnchorMeshPoint point in feature.Points)
        {
            float x = point.SnappedX;
            float y = point.SnappedY;
            minX = MathF.Min(minX, x);
            minY = MathF.Min(minY, y);
            maxX = MathF.Max(maxX, x);
            maxY = MathF.Max(maxY, y);
            sumX += x;
            sumY += y;
        }

        feature.CenterX = sumX / feature.Points.Count;
        feature.CenterY = sumY / feature.Points.Count;
        feature.Width = MathF.Max(0, maxX - minX);
        feature.Height = MathF.Max(0, maxY - minY);
        feature.Bounds = new RectangleF(minX, minY, feature.Width, feature.Height);
        feature.AngleRad = angleRad;
        feature.IsValid = true;
        feature.Confidence = feature.Points.Average(point => point.Confidence);

        foreach (AnchorMeshPoint point in feature.Points)
        {
            point.LocalX = point.SnappedX - feature.CenterX;
            point.LocalY = point.SnappedY - feature.CenterY;
        }
    }
}
