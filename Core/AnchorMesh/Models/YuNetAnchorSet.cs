using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public sealed class YuNetAnchorSet
{
    public RectangleF FaceBox { get; init; }

    public PointF LeftEye { get; init; }

    public PointF RightEye { get; init; }

    public PointF NoseTip { get; init; }

    public PointF LeftMouthCorner { get; init; }

    public PointF RightMouthCorner { get; init; }

    public float Score { get; init; }

    public PointF EyeCenter => new((LeftEye.X + RightEye.X) * 0.5f, (LeftEye.Y + RightEye.Y) * 0.5f);

    public PointF MouthCenter => new((LeftMouthCorner.X + RightMouthCorner.X) * 0.5f, (LeftMouthCorner.Y + RightMouthCorner.Y) * 0.5f);

    public PointF FaceCenter => new(FaceBox.X + FaceBox.Width * 0.5f, FaceBox.Y + FaceBox.Height * 0.5f);

    public float EyeDistance
    {
        get
        {
            float dx = RightEye.X - LeftEye.X;
            float dy = RightEye.Y - LeftEye.Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }
    }

    public float FaceAngleRad => MathF.Atan2(RightEye.Y - LeftEye.Y, RightEye.X - LeftEye.X);
}
