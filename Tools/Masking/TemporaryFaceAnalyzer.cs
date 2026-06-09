using System.Windows;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class TemporaryFaceAnalyzer : IFaceAnalyzer
{
    public string AnalyzerVersion => "temporary_face_analyzer_v1";

    public FaceAnalyzerResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        ArgumentNullException.ThrowIfNull(source);

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        Int32Rect faceBox = CreateCenteredFaceBox(width, height);
        WpfPoint leftEye = ToFaceBoxPoint(faceBox, 0.35, 0.38);
        WpfPoint rightEye = ToFaceBoxPoint(faceBox, 0.65, 0.38);
        WpfPoint noseTip = ToFaceBoxPoint(faceBox, 0.50, 0.56);
        WpfPoint mouthLeft = ToFaceBoxPoint(faceBox, 0.40, 0.72);
        WpfPoint mouthRight = ToFaceBoxPoint(faceBox, 0.60, 0.72);
        WpfPoint mouthCenter = ToFaceBoxPoint(faceBox, 0.50, 0.72);
        WpfPoint chin = ToFaceBoxPoint(faceBox, 0.50, 0.92);
        double faceAngle = CalculateFaceAngle(leftEye, rightEye);
        List<string> warnings = new()
        {
            "temporary_face_analyzer",
            "face_detection_model_not_connected",
            "face_landmark_model_not_connected"
        };
        double confidence = CalculateConfidence(width, height, faceBox, leftEye, rightEye, noseTip, mouthCenter, chin, warnings);

        return new FaceAnalyzerResult(
            faceBox,
            faceAngle,
            leftEye,
            rightEye,
            noseTip,
            mouthLeft,
            mouthRight,
            mouthCenter,
            chin,
            confidence,
            warnings);
    }

    private static Int32Rect CreateCenteredFaceBox(int width, int height)
    {
        int faceWidth = Math.Max(1, (int)Math.Round(width * 0.34));
        int faceHeight = Math.Max(1, (int)Math.Round(height * 0.54));
        double centerX = width * 0.50;
        double centerY = height * 0.48;
        int x = Math.Clamp((int)Math.Round(centerX - faceWidth / 2d), 0, Math.Max(0, width - faceWidth));
        int y = Math.Clamp((int)Math.Round(centerY - faceHeight / 2d), 0, Math.Max(0, height - faceHeight));
        return new Int32Rect(x, y, faceWidth, faceHeight);
    }

    private static WpfPoint ToFaceBoxPoint(Int32Rect faceBox, double x, double y)
    {
        return new WpfPoint(
            faceBox.X + faceBox.Width * x,
            faceBox.Y + faceBox.Height * y);
    }

    private static double CalculateFaceAngle(WpfPoint leftEye, WpfPoint rightEye)
    {
        double dx = rightEye.X - leftEye.X;
        double dy = rightEye.Y - leftEye.Y;
        return Math.Atan2(dy, dx) * 180d / Math.PI;
    }

    private static double CalculateConfidence(
        int width,
        int height,
        Int32Rect faceBox,
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        WpfPoint chin,
        List<string> warnings)
    {
        double confidence = 0.45;
        double faceAreaRatio = faceBox.Width * faceBox.Height / (double)Math.Max(1, width * height);
        if (faceAreaRatio < 0.05 || faceAreaRatio > 0.45)
        {
            warnings.Add("temporary_facebox_size_warning");
            confidence -= 0.08;
        }

        if (leftEye.X >= rightEye.X)
        {
            warnings.Add("temporary_eye_order_warning");
            confidence -= 0.18;
        }

        if (noseTip.Y <= Math.Min(leftEye.Y, rightEye.Y) ||
            mouthCenter.Y <= noseTip.Y ||
            chin.Y <= mouthCenter.Y)
        {
            warnings.Add("temporary_landmark_order_warning");
            confidence -= 0.14;
        }

        foreach (WpfPoint point in new[] { leftEye, rightEye, noseTip, mouthCenter, chin })
        {
            if (point.X < 0 || point.Y < 0 || point.X >= width || point.Y >= height)
            {
                warnings.Add("temporary_landmark_outside_image");
                confidence -= 0.08;
                break;
            }
        }

        return Math.Clamp(confidence, 0, 1);
    }
}
