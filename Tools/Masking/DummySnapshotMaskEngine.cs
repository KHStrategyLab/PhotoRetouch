using System.Windows;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class DummySnapshotMaskEngine : IPortraitMaskEngine
{
    public string MaskVersion => "dummy_snapshot_mask_v1";

    public PortraitMaskResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        FaceWorkArea area = faceWorkArea.Clamp();
        Int32Rect faceBox = CreateFaceBox(area, width, height);

        MaskPlane skinMask = BuildEllipseMask(width, height, area, 0, 0.04, 0.72, 0.98);
        MaskPlane eyeMask = MaskPlane.Union(
            BuildEllipseMask(width, height, area, -0.38, -0.38, 0.22, 0.14),
            BuildEllipseMask(width, height, area, 0.38, -0.38, 0.22, 0.14));
        MaskPlane lipMask = BuildEllipseMask(width, height, area, 0, 0.36, 0.34, 0.13);
        MaskPlane nostrilMask = MaskPlane.Union(
            BuildEllipseMask(width, height, area, -0.12, 0.15, 0.065, 0.045),
            BuildEllipseMask(width, height, area, 0.12, 0.15, 0.065, 0.045));
        MaskPlane softProtectMask = MaskPlane.Union(
            BuildEllipseMask(width, height, area, -0.38, -0.20, 0.24, 0.10),
            BuildEllipseMask(width, height, area, 0.38, -0.20, 0.24, 0.10),
            BuildEllipseMask(width, height, area, 0, 0.10, 0.28, 0.18));

        MaskPlane empty = MaskPlane.Empty(width, height);
        MaskPlane noseMask = BuildEllipseMask(width, height, area, 0, -0.04, 0.26, 0.45);
        MaskPlane noseSkinMask = MaskPlane.Subtract(noseMask, nostrilMask);
        MaskPlane hardProtectMask = MaskPlane.Union(eyeMask, lipMask, nostrilMask);
        MaskPlane retouchAllowMask = MaskPlane.Subtract(skinMask, hardProtectMask);
        MaskPlane finalOverlayMask = MaskPlane.Subtract(MaskPlane.Union(retouchAllowMask, MaskPlane.Multiply(softProtectMask, 0.45)), hardProtectMask);

        IReadOnlyDictionary<string, WpfPoint> landmarks = new Dictionary<string, WpfPoint>
        {
            ["left_eye"] = ToImagePoint(area, width, height, -0.38, -0.38),
            ["right_eye"] = ToImagePoint(area, width, height, 0.38, -0.38),
            ["left_nostril"] = ToImagePoint(area, width, height, -0.12, 0.15),
            ["right_nostril"] = ToImagePoint(area, width, height, 0.12, 0.15),
            ["mouth_center"] = ToImagePoint(area, width, height, 0, 0.36)
        };
        FaceAnalysisResult analysis = new(
            faceBox,
            landmarks,
            null,
            0,
            1,
            1,
            0,
            new[] { "dummy_snapshot_mask" });
        FaceMaskSet masks = new(
            skinMask,
            eyeMask,
            empty,
            lipMask,
            empty,
            empty,
            noseMask,
            noseSkinMask,
            nostrilMask,
            empty,
            empty,
            empty,
            empty,
            empty,
            hardProtectMask,
            softProtectMask,
            retouchAllowMask,
            finalOverlayMask);
        MaskQualityReport report = MaskQualityReport.FromMasks(analysis, masks);
        return new PortraitMaskResult(analysis, masks, report);
    }

    private static Int32Rect CreateFaceBox(FaceWorkArea area, int width, int height)
    {
        int faceWidth = Math.Max(1, (int)Math.Round(area.Width * width));
        int faceHeight = Math.Max(1, (int)Math.Round(area.Height * height));
        int x = Math.Clamp((int)Math.Round(area.CenterX * width - faceWidth / 2d), 0, Math.Max(0, width - faceWidth));
        int y = Math.Clamp((int)Math.Round(area.CenterY * height - faceHeight / 2d), 0, Math.Max(0, height - faceHeight));
        return new Int32Rect(x, y, faceWidth, faceHeight);
    }

    private static MaskPlane BuildEllipseMask(int width, int height, FaceWorkArea area, double centerX, double centerY, double radiusX, double radiusY)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                (double normalizedX, double normalizedY) = NormalizeFacePoint(area, width, height, x, y);
                double dx = (normalizedX - centerX) / radiusX;
                double dy = (normalizedY - centerY) / radiusY;
                double distance = Math.Sqrt(dx * dx + dy * dy);
                mask[x, y] = 1 - SmoothStep(0.72, 1.08, distance);
            }
        }

        return mask;
    }

    private static WpfPoint ToImagePoint(FaceWorkArea area, int width, int height, double normalizedX, double normalizedY)
    {
        double radiusX = area.Width * width / 2;
        double radiusY = area.Height * height / 2;
        return new WpfPoint(
            area.CenterX * (width - 1) + normalizedX * radiusX,
            area.CenterY * (height - 1) + normalizedY * radiusY);
    }

    private static (double X, double Y) NormalizeFacePoint(FaceWorkArea area, int width, int height, int x, int y)
    {
        return (
            (x - area.CenterX * (width - 1)) / Math.Max(1, area.Width * width / 2),
            (y - area.CenterY * (height - 1)) / Math.Max(1, area.Height * height / 2));
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double normalized = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return normalized * normalized * (3 - 2 * normalized);
    }
}
