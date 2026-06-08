using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record FaceAnalyzerResult(
    Int32Rect FaceBox,
    double FaceAngle,
    WpfPoint LeftEyeCenter,
    WpfPoint RightEyeCenter,
    WpfPoint NoseTip,
    WpfPoint MouthCenter,
    WpfPoint ChinPoint,
    double Confidence,
    IReadOnlyList<string> DebugWarnings)
{
    public IReadOnlyDictionary<string, WpfPoint> ToLandmarks()
    {
        return new Dictionary<string, WpfPoint>
        {
            ["left_eye"] = LeftEyeCenter,
            ["right_eye"] = RightEyeCenter,
            ["nose_tip"] = NoseTip,
            ["mouth_center"] = MouthCenter,
            ["chin"] = ChinPoint
        };
    }

    public MaskWarpInput ToMaskWarpInput(int targetImageWidth, int targetImageHeight)
    {
        return new MaskWarpInput(
            targetImageWidth,
            targetImageHeight,
            FaceBox,
            FaceAngle,
            LeftEyeCenter,
            RightEyeCenter,
            NoseTip,
            MouthCenter,
            ChinPoint);
    }
}
