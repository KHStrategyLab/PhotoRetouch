namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshProjector
{
    public static Point2F Project(Point3F point, AnchorPoseInfo pose)
    {
        float depth = pose.CameraDistance - point.Z * pose.PerspectiveStrength;
        if (depth < 0.001f)
        {
            depth = 0.001f;
        }

        float perspectiveScale = pose.CameraDistance / depth;
        return new Point2F(
            pose.CenterX + point.X * pose.Scale * perspectiveScale,
            pose.CenterY + point.Y * pose.Scale * perspectiveScale);
    }
}
