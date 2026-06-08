using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record MaskWarpInput(
    int TargetImageWidth,
    int TargetImageHeight,
    Int32Rect FaceBox,
    double FaceAngle,
    WpfPoint LeftEyeCenter,
    WpfPoint RightEyeCenter,
    WpfPoint NoseTip,
    WpfPoint MouthCenter,
    WpfPoint ChinPoint)
{
    public static MaskWarpInput FromFaceWorkArea(int width, int height, FaceWorkArea faceWorkArea)
    {
        FaceWorkArea area = faceWorkArea.Clamp();
        Int32Rect faceBox = CreateFaceBox(area, width, height);
        return new MaskWarpInput(
            width,
            height,
            faceBox,
            0,
            ToImagePoint(area, width, height, -0.42, -0.38),
            ToImagePoint(area, width, height, 0.42, -0.38),
            ToImagePoint(area, width, height, 0, 0.04),
            ToImagePoint(area, width, height, 0, 0.36),
            ToImagePoint(area, width, height, 0, 0.83));
    }

    private static Int32Rect CreateFaceBox(FaceWorkArea area, int width, int height)
    {
        int faceWidth = Math.Max(1, (int)Math.Round(area.Width * width));
        int faceHeight = Math.Max(1, (int)Math.Round(area.Height * height));
        int x = Math.Clamp((int)Math.Round(area.CenterX * width - faceWidth / 2d), 0, Math.Max(0, width - faceWidth));
        int y = Math.Clamp((int)Math.Round(area.CenterY * height - faceHeight / 2d), 0, Math.Max(0, height - faceHeight));
        return new Int32Rect(x, y, faceWidth, faceHeight);
    }

    private static WpfPoint ToImagePoint(FaceWorkArea area, int width, int height, double normalizedX, double normalizedY)
    {
        double radiusX = area.Width * width / 2;
        double radiusY = area.Height * height / 2;
        return new WpfPoint(
            area.CenterX * (width - 1) + normalizedX * radiusX,
            area.CenterY * (height - 1) + normalizedY * radiusY);
    }
}
