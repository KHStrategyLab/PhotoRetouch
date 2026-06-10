using System.Drawing;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch.AnchorMesh;

public static class YuNetAnchorMapper
{
    public static YuNetAnchorSet FromFaceAnalyzerResult(FaceAnalyzerResult analysis)
    {
        PointF mouthCenter = new((float)analysis.MouthCenter.X, (float)analysis.MouthCenter.Y);
        float mouthHalfWidth = Math.Max(6.0f, (float)analysis.FaceBox.Width * 0.10f);
        float angle = (float)analysis.FaceAngle;
        float axisX = MathF.Cos(angle);
        float axisY = MathF.Sin(angle);

        return new YuNetAnchorSet
        {
            FaceBox = new RectangleF(
                analysis.FaceBox.X,
                analysis.FaceBox.Y,
                analysis.FaceBox.Width,
                analysis.FaceBox.Height),
            LeftEye = new PointF((float)analysis.LeftEyeCenter.X, (float)analysis.LeftEyeCenter.Y),
            RightEye = new PointF((float)analysis.RightEyeCenter.X, (float)analysis.RightEyeCenter.Y),
            NoseTip = new PointF((float)analysis.NoseTip.X, (float)analysis.NoseTip.Y),
            LeftMouthCorner = new PointF(mouthCenter.X - axisX * mouthHalfWidth, mouthCenter.Y - axisY * mouthHalfWidth),
            RightMouthCorner = new PointF(mouthCenter.X + axisX * mouthHalfWidth, mouthCenter.Y + axisY * mouthHalfWidth),
            Score = (float)analysis.Confidence
        };
    }

    public static YuNetAnchorSet FromFaceAnalysisResult(FaceAnalysisResult analysis)
    {
        WpfPoint leftEye = GetLandmarkOrDefault(analysis, "left_eye", analysis.FaceBox.X + analysis.FaceBox.Width * 0.35, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.38);
        WpfPoint rightEye = GetLandmarkOrDefault(analysis, "right_eye", analysis.FaceBox.X + analysis.FaceBox.Width * 0.65, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.38);
        WpfPoint noseTip = GetLandmarkOrDefault(analysis, "nose_tip", analysis.FaceBox.X + analysis.FaceBox.Width * 0.50, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.56);
        WpfPoint mouthCenter = GetLandmarkOrDefault(analysis, "mouth_center", analysis.FaceBox.X + analysis.FaceBox.Width * 0.50, analysis.FaceBox.Y + analysis.FaceBox.Height * 0.72);
        double angle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X);
        double mouthHalfWidth = Math.Max(6.0, analysis.FaceBox.Width * 0.10);
        WpfPoint leftMouth = GetLandmarkOrDefault(
            analysis,
            "mouth_left",
            mouthCenter.X - Math.Cos(angle) * mouthHalfWidth,
            mouthCenter.Y - Math.Sin(angle) * mouthHalfWidth);
        WpfPoint rightMouth = GetLandmarkOrDefault(
            analysis,
            "mouth_right",
            mouthCenter.X + Math.Cos(angle) * mouthHalfWidth,
            mouthCenter.Y + Math.Sin(angle) * mouthHalfWidth);

        return new YuNetAnchorSet
        {
            FaceBox = new RectangleF(
                analysis.FaceBox.X,
                analysis.FaceBox.Y,
                analysis.FaceBox.Width,
                analysis.FaceBox.Height),
            LeftEye = new PointF((float)leftEye.X, (float)leftEye.Y),
            RightEye = new PointF((float)rightEye.X, (float)rightEye.Y),
            NoseTip = new PointF((float)noseTip.X, (float)noseTip.Y),
            LeftMouthCorner = new PointF((float)leftMouth.X, (float)leftMouth.Y),
            RightMouthCorner = new PointF((float)rightMouth.X, (float)rightMouth.Y),
            Score = (float)Math.Clamp(analysis.LandmarkConfidence, 0, 1)
        };
    }

    private static WpfPoint GetLandmarkOrDefault(FaceAnalysisResult analysis, string key, double defaultX, double defaultY)
    {
        return analysis.FaceLandmarks.TryGetValue(key, out WpfPoint point)
            ? point
            : new WpfPoint(defaultX, defaultY);
    }
}
