using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public static class YuNetAnchorMapper
{
    public static YuNetAnchorSet FromFaceAnalyzerResult(FaceAnalyzerResult analysis)
    {
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
            LeftMouthCorner = new PointF((float)analysis.MouthLeft.X, (float)analysis.MouthLeft.Y),
            RightMouthCorner = new PointF((float)analysis.MouthRight.X, (float)analysis.MouthRight.Y),
            Score = (float)analysis.Confidence
        };
    }
}
