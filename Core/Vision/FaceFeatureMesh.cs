using System.Windows;

namespace PhotoRetouch;

public sealed record FaceFeatureMesh(
    FaceFeatureType FeatureType,
    IReadOnlyList<FeatureMeshPoint> Points,
    double Confidence,
    string Source)
{
    public const int DefaultPointCount = 50;

    public bool IsReady => Points.Count == DefaultPointCount;

    public Rect Bounds
    {
        get
        {
            if (Points.Count == 0)
            {
                return Rect.Empty;
            }

            double minX = Points.Min(point => point.X);
            double maxX = Points.Max(point => point.X);
            double minY = Points.Min(point => point.Y);
            double maxY = Points.Max(point => point.Y);
            return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
        }
    }
}
