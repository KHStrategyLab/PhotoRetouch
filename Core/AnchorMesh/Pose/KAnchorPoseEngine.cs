namespace PhotoRetouch.AnchorMesh;

public sealed class KAnchorPoseEngine
{
    public AnchorPoseInfo EstimatePose(YuNetAnchorSet anchors, AnchorFaceMeasurements measurements)
    {
        float yaw = EstimateYaw(anchors, measurements);
        float pitch = EstimatePitch(anchors, measurements);
        return new AnchorPoseInfo(
            0,
            yaw,
            pitch,
            1.0f,
            0,
            0,
            2.8f,
            0.58f,
            anchors.Score);
    }

    public AnchorMeshFeatureSet ApplyPose(AnchorMeshFeatureSet template, AnchorPoseInfo pose)
    {
        AnchorMeshFeatureSet projected = template.Clone();
        foreach (AnchorMeshFeature feature in projected.GetAll())
        {
            foreach (AnchorMeshPoint point in feature.Points)
            {
                Point3F rotated = AnchorMesh3DRotator.Rotate(
                    new Point3F(point.TemplateX, point.TemplateY, point.TemplateZ),
                    pose);
                Point2F projectedPoint = AnchorMeshProjector.Project(rotated, pose);
                point.PoseX = rotated.X;
                point.PoseY = rotated.Y;
                point.PoseZ = rotated.Z;
                point.ProjectedX = projectedPoint.X;
                point.ProjectedY = projectedPoint.Y;
                point.Source = "PoseProjected";
            }
        }

        return projected;
    }

    private static float EstimateYaw(YuNetAnchorSet anchors, AnchorFaceMeasurements measurements)
    {
        if (measurements.EyeDistance <= 1)
        {
            return 0;
        }

        float noseOffset = anchors.NoseTip.X - anchors.EyeCenter.X;
        float normalized = Math.Clamp(noseOffset / measurements.EyeDistance, -0.38f, 0.38f);
        return normalized * 0.42f;
    }

    private static float EstimatePitch(YuNetAnchorSet anchors, AnchorFaceMeasurements measurements)
    {
        if (measurements.EyeDistance <= 1 || measurements.NoseToMouthDistance <= 1)
        {
            return 0;
        }

        float expected = measurements.EyeDistance * 0.48f;
        float delta = Math.Clamp((measurements.NoseToMouthDistance - expected) / measurements.EyeDistance, -0.25f, 0.25f);
        return -delta * 0.24f;
    }
}
