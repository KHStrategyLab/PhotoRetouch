using System.Windows;

namespace PhotoRetouch;

internal static class MeshMaskRasterizer
{
    public static MaskPlane FillClosedMesh(int width, int height, FaceFeatureMesh mesh, double opacity, double featherRadius)
    {
        if (mesh.Points.Count < 3)
        {
            return MaskPlane.Empty(width, height);
        }

        return FillPolygon(width, height, mesh.Points, opacity, featherRadius);
    }

    public static MaskPlane FillRoleGroupedMeshes(int width, int height, FaceFeatureMesh mesh, double opacity, double featherRadius)
    {
        MaskPlane result = MaskPlane.Empty(width, height);
        foreach (IGrouping<string, FeatureMeshPoint> group in mesh.Points.GroupBy(point => point.Role))
        {
            IReadOnlyList<FeatureMeshPoint> points = group.ToArray();
            if (points.Count < 3)
            {
                continue;
            }

            MaskPlane part = FillPolygon(width, height, points, opacity, featherRadius);
            result = MaskPlane.Union(result, part);
        }

        return result;
    }

    private static MaskPlane FillPolygon(int width, int height, IReadOnlyList<FeatureMeshPoint> points, double opacity, double featherRadius)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        Rect bounds = GetBounds(points);
        int left = Math.Max(0, (int)Math.Floor(bounds.Left - featherRadius - 2));
        int right = Math.Min(width - 1, (int)Math.Ceiling(bounds.Right + featherRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(bounds.Top - featherRadius - 2));
        int bottom = Math.Min(height - 1, (int)Math.Ceiling(bounds.Bottom + featherRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                bool inside = IsInsidePolygon(x + 0.5, y + 0.5, points);
                double distance = inside ? 0 : DistanceToPolyline(x + 0.5, y + 0.5, points);
                double amount = inside
                    ? opacity
                    : opacity * (1 - SmoothStep(0, Math.Max(0.5, featherRadius), distance));
                if (amount <= 0)
                {
                    continue;
                }

                mask[x, y] = Math.Max(mask[x, y], amount);
            }
        }

        return mask;
    }

    private static Rect GetBounds(IReadOnlyList<FeatureMeshPoint> points)
    {
        double minX = points.Min(point => point.X);
        double maxX = points.Max(point => point.X);
        double minY = points.Min(point => point.Y);
        double maxY = points.Max(point => point.Y);
        return new Rect(minX, minY, Math.Max(0, maxX - minX), Math.Max(0, maxY - minY));
    }

    private static bool IsInsidePolygon(double x, double y, IReadOnlyList<FeatureMeshPoint> points)
    {
        bool inside = false;
        int previous = points.Count - 1;
        for (int current = 0; current < points.Count; current++)
        {
            FeatureMeshPoint a = points[current];
            FeatureMeshPoint b = points[previous];
            if ((a.Y > y) != (b.Y > y) &&
                x < (b.X - a.X) * (y - a.Y) / Math.Max(0.0001, b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }

            previous = current;
        }

        return inside;
    }

    private static double DistanceToPolyline(double x, double y, IReadOnlyList<FeatureMeshPoint> points)
    {
        double best = double.MaxValue;
        for (int index = 0; index < points.Count; index++)
        {
            FeatureMeshPoint a = points[index];
            FeatureMeshPoint b = points[(index + 1) % points.Count];
            best = Math.Min(best, DistanceToSegment(x, y, a.X, a.Y, b.X, b.Y));
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
