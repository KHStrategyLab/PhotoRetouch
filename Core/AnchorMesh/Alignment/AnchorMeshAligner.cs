using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshAligner
{
    private const float TemplateEyeDistance = 0.36f;
    private static readonly PointF TemplateEyeCenter = new(0.0f, -0.21f);

    public AnchorMeshFeatureSet Align(AnchorMeshFeatureSet template, YuNetAnchorSet anchors)
    {
        AnchorMeshTransform2D transform = CreateTransform(anchors);
        AnchorMeshFeatureSet aligned = template.Clone();

        foreach (AnchorMeshFeature feature in aligned.GetAll())
        {
            foreach (AnchorMeshPoint point in feature.Points)
            {
                PointF imagePoint = transform.Apply(point.TemplateX, point.TemplateY);
                point.ImageX = imagePoint.X;
                point.ImageY = imagePoint.Y;
                point.SnappedX = imagePoint.X;
                point.SnappedY = imagePoint.Y;
                point.Source = "YuNetAligned";
                point.Confidence = MathF.Max(point.Confidence, anchors.Score);
            }

            AnchorMeshMetrics.Update(feature, anchors.FaceAngleRad);
            feature.SnapMode = "None";
        }

        return aligned;
    }

    private static AnchorMeshTransform2D CreateTransform(YuNetAnchorSet anchors)
    {
        float scale = anchors.EyeDistance / TemplateEyeDistance;
        float rotation = anchors.FaceAngleRad;
        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);
        float templateEyeX = (TemplateEyeCenter.X * cos - TemplateEyeCenter.Y * sin) * scale;
        float templateEyeY = (TemplateEyeCenter.X * sin + TemplateEyeCenter.Y * cos) * scale;
        return new AnchorMeshTransform2D(
            scale,
            rotation,
            anchors.EyeCenter.X - templateEyeX,
            anchors.EyeCenter.Y - templateEyeY);
    }
}
