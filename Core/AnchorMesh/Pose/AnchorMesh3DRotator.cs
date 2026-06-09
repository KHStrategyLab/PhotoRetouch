namespace PhotoRetouch.AnchorMesh;

public static class AnchorMesh3DRotator
{
    public static Point3F Rotate(Point3F point, AnchorPoseInfo pose)
    {
        float cy = MathF.Cos(pose.YawRad);
        float sy = MathF.Sin(pose.YawRad);

        float x1 = point.X * cy + point.Z * sy;
        float y1 = point.Y;
        float z1 = -point.X * sy + point.Z * cy;

        float cp = MathF.Cos(pose.PitchRad);
        float sp = MathF.Sin(pose.PitchRad);

        float x2 = x1;
        float y2 = y1 * cp - z1 * sp;
        float z2 = y1 * sp + z1 * cp;

        float cr = MathF.Cos(pose.RollRad);
        float sr = MathF.Sin(pose.RollRad);

        float x3 = x2 * cr - y2 * sr;
        float y3 = x2 * sr + y2 * cr;
        float z3 = z2;

        return new Point3F(x3, y3, z3);
    }
}
