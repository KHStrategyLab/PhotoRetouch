using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record ShapeBalanceMap(
    int SourceImageWidth,
    int SourceImageHeight,
    int TargetImageWidth,
    int TargetImageHeight,
    ShapeBalanceGlobalTransform GlobalTransform,
    WpfPoint FaceCenter,
    Int32Rect FaceBox,
    double EyeLevelDelta,
    double EyebrowLevelDelta,
    double MouthCornerDelta,
    double NoseCenterDelta,
    double ChinCenterDelta,
    double MaxDisplacementPixels,
    double PreserveIdentityStrength,
    bool ProtectHardFeatures,
    IReadOnlyList<ShapeBalanceWarpRegion> LocalWarpRegions,
    IReadOnlyList<ShapeBalanceProtectedRegion> ProtectedFeatureRegions,
    ShapeBalanceWarpStrengthMap WarpStrengthMap,
    SymmetryBalanceAnalysisReport SymmetryAnalysisReport,
    SymmetryBalanceMap SymmetryBalanceMap,
    IReadOnlyList<ShapeBalanceDebugVector> DebugVectors,
    DateTime CreatedAtUtc,
    string ShapeBalanceVersion)
{
    public int Width => TargetImageWidth;

    public int Height => TargetImageHeight;

    public double RotationRadians => GlobalTransform.RotationRadians;

    public static ShapeBalanceMap Identity(int width, int height, Int32Rect faceBox)
    {
        WpfPoint center = new(faceBox.X + faceBox.Width / 2d, faceBox.Y + faceBox.Height / 2d);
        return new ShapeBalanceMap(
            width,
            height,
            width,
            height,
            ShapeBalanceGlobalTransform.Identity(center),
            center,
            faceBox,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            true,
            Array.Empty<ShapeBalanceWarpRegion>(),
            Array.Empty<ShapeBalanceProtectedRegion>(),
            ShapeBalanceWarpStrengthMap.Empty(width, height),
            SymmetryBalanceAnalysisReport.Empty(center),
            SymmetryBalanceMap.Empty(width, height, center),
            Array.Empty<ShapeBalanceDebugVector>(),
            DateTime.UtcNow,
            "shape_balance_map_v4_face_only_yaw_symmetry");
    }

    public bool IsIdentity =>
        Math.Abs(RotationRadians) < 0.00001 &&
        Math.Abs(GlobalTransform.TranslateX) < 0.00001 &&
        Math.Abs(GlobalTransform.TranslateY) < 0.00001 &&
        Math.Abs(GlobalTransform.ScaleX - 1) < 0.00001 &&
        Math.Abs(GlobalTransform.ScaleY - 1) < 0.00001 &&
        Math.Abs(GlobalTransform.PitchShear) < 0.00001 &&
        LocalWarpRegions.Count == 0 &&
        SymmetryBalanceMap.SymmetryWarpRegions.Count == 0;

    public WpfPoint MapSourceToBalanced(WpfPoint source)
    {
        WpfPoint rotated = GlobalTransform.Apply(source);
        WpfPoint displacement = CalculateLocalDisplacement(rotated);
        return new WpfPoint(
            Clamp(rotated.X + displacement.X, 0, Width - 1),
            Clamp(rotated.Y + displacement.Y, 0, Height - 1));
    }

    public WpfPoint MapBalancedToSource(double balancedX, double balancedY)
    {
        WpfPoint balanced = new(balancedX, balancedY);
        WpfPoint displacement = CalculateLocalDisplacement(balanced);
        WpfPoint unwarped = new(
            balanced.X - displacement.X,
            balanced.Y - displacement.Y);
        WpfPoint source = GlobalTransform.Invert(unwarped);
        return new WpfPoint(
            Clamp(source.X, 0, Width - 1),
            Clamp(source.Y, 0, Height - 1));
    }

    private WpfPoint CalculateLocalDisplacement(WpfPoint point)
    {
        double dx = 0;
        double dy = 0;
        foreach (ShapeBalanceWarpRegion region in LocalWarpRegions)
        {
            AddRegionDisplacement(region, point, ref dx, ref dy);
        }

        foreach (ShapeBalanceWarpRegion region in SymmetryBalanceMap.SymmetryWarpRegions)
        {
            AddRegionDisplacement(region, point, ref dx, ref dy);
        }

        double max = Math.Max(0, MaxDisplacementPixels);
        if (max > 0)
        {
            dx = Clamp(dx, -max, max);
            dy = Clamp(dy, -max, max);
        }

        return new WpfPoint(dx, dy);
    }

    private void AddRegionDisplacement(ShapeBalanceWarpRegion region, WpfPoint point, ref double dx, ref double dy)
    {
        double protectedDamping = CalculateProtectedDamping(point);
        double weight = region.WeightAt(point.X, point.Y) * protectedDamping;
        if (weight <= 0)
        {
            return;
        }

        dx += region.DeltaX * weight;
        dy += region.DeltaY * weight;
    }

    private double CalculateProtectedDamping(WpfPoint point)
    {
        if (!ProtectHardFeatures || ProtectedFeatureRegions.Count == 0)
        {
            return 1;
        }

        double damping = 1;
        foreach (ShapeBalanceProtectedRegion region in ProtectedFeatureRegions)
        {
            damping *= 1 - region.WeightAt(point.X, point.Y);
        }

        return Math.Clamp(damping, 0.12, 1);
    }

    private static double Clamp(double value, double min, double max)
    {
        return Math.Clamp(value, min, Math.Max(min, max));
    }
}

public sealed record ShapeBalanceGlobalTransform(
    WpfPoint Center,
    double RotationRadians,
    double TranslateX,
    double TranslateY,
    double ScaleX,
    double ScaleY,
    double PitchShear)
{
    public static ShapeBalanceGlobalTransform Identity(WpfPoint center)
    {
        return new ShapeBalanceGlobalTransform(center, 0, 0, 0, 1, 1, 0);
    }

    public WpfPoint Apply(WpfPoint point)
    {
        double dx = point.X - Center.X;
        double dy = point.Y - Center.Y;
        double shearedX = dx + dy * PitchShear;
        double scaledX = shearedX * ScaleX;
        double scaledY = dy * ScaleY;
        double cos = Math.Cos(RotationRadians);
        double sin = Math.Sin(RotationRadians);
        return new WpfPoint(
            Center.X + scaledX * cos - scaledY * sin + TranslateX,
            Center.Y + scaledX * sin + scaledY * cos + TranslateY);
    }

    public WpfPoint Invert(WpfPoint point)
    {
        double cos = Math.Cos(-RotationRadians);
        double sin = Math.Sin(-RotationRadians);
        double dx = point.X - Center.X - TranslateX;
        double dy = point.Y - Center.Y - TranslateY;
        double unrotatedX = dx * cos - dy * sin;
        double unrotatedY = dx * sin + dy * cos;
        double sourceY = unrotatedY / Math.Max(0.0001, ScaleY);
        double sourceX = unrotatedX / Math.Max(0.0001, ScaleX) - sourceY * PitchShear;
        return new WpfPoint(Center.X + sourceX, Center.Y + sourceY);
    }
}

public sealed record ShapeBalanceWarpRegion(
    WpfPoint Center,
    double RadiusX,
    double RadiusY,
    double DeltaX,
    double DeltaY,
    double Strength,
    string RegionId)
{
    public double WeightAt(double x, double y)
    {
        double rx = Math.Max(1, RadiusX);
        double ry = Math.Max(1, RadiusY);
        double nx = (x - Center.X) / rx;
        double ny = (y - Center.Y) / ry;
        double distance = Math.Sqrt(nx * nx + ny * ny);
        if (distance >= 1)
        {
            return 0;
        }

        double smooth = 1 - distance;
        return Math.Clamp(smooth * smooth * (3 - 2 * smooth) * Strength, 0, 1);
    }
}

public sealed record ShapeBalanceProtectedRegion(
    WpfPoint Center,
    double RadiusX,
    double RadiusY,
    double Damping,
    string RegionId)
{
    public double WeightAt(double x, double y)
    {
        double rx = Math.Max(1, RadiusX);
        double ry = Math.Max(1, RadiusY);
        double nx = (x - Center.X) / rx;
        double ny = (y - Center.Y) / ry;
        double distance = Math.Sqrt(nx * nx + ny * ny);
        if (distance >= 1)
        {
            return 0;
        }

        double smooth = 1 - distance;
        return Math.Clamp(smooth * smooth * (3 - 2 * smooth) * Damping, 0, 1);
    }
}

public sealed record ShapeBalanceWarpStrengthMap(
    int Width,
    int Height,
    double GlobalStrength,
    double LocalStrength,
    double HardProtectDamping,
    double FaceBoundaryDamping)
{
    public static ShapeBalanceWarpStrengthMap Empty(int width, int height)
    {
        return new ShapeBalanceWarpStrengthMap(width, height, 0, 0, 0, 0);
    }
}

public sealed record ShapeBalanceDebugVector(
    WpfPoint From,
    WpfPoint To,
    string Label);
