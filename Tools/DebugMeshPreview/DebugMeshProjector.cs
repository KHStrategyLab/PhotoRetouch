using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class DebugMeshProjector
{
    public static WpfPoint Project(DebugMeshPoint point, double centerX, double centerY, double scale)
    {
        const double perspective = 0.35;
        double projectedScale = scale / Math.Max(0.15, 1.0 - point.Z * perspective);
        return new WpfPoint(
            centerX + point.X * projectedScale,
            centerY + point.Y * projectedScale);
    }
}
