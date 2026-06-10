using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshAligner
{
    private const float TemplateFaceWidth = 0.86f;
    private const float TemplateFaceHeight = 1.16f;
    private static readonly PointF TemplateFaceCenter = new(0.0f, 0.04f);

    public AnchorMeshFeatureSet Align(AnchorMeshFeatureSet template, YuNetAnchorSet anchors)
    {
        float scale = anchors.EyeDistance / 0.36f;
        FaceFitBox fallbackFitBox = new()
        {
            CenterX = anchors.EyeCenter.X,
            CenterY = anchors.EyeCenter.Y + scale * 0.25f,
            Width = scale * TemplateFaceWidth,
            Height = scale * TemplateFaceHeight,
            RotationRad = anchors.FaceAngleRad,
            Confidence = anchors.Score,
            Source = "YuNet"
        };

        return Align(template, anchors, fallbackFitBox);
    }

    public AnchorMeshFeatureSet Align(AnchorMeshFeatureSet template, YuNetAnchorSet anchors, FaceFitBox fitBox)
    {
        AnchorMeshFitTransform2D transform = CreateFitTransform(fitBox);
        AnchorMeshFeatureSet aligned = template.Clone();

        foreach (AnchorMeshFeature feature in aligned.GetAll())
        {
            foreach (AnchorMeshPoint point in feature.Points)
            {
                PointF imagePoint = transform.Apply(GetProjectedX(point), GetProjectedY(point));
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

        LockPrimaryFeatureCenters(aligned, anchors);

        return aligned;
    }

    public void LockPrimaryFeatureCenters(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        if (features.LeftEye is not null)
        {
            TranslateFeatureCenterTo(features.LeftEye, anchors.LeftEye.X, anchors.LeftEye.Y, anchors.FaceAngleRad);
        }

        if (features.RightEye is not null)
        {
            TranslateFeatureCenterTo(features.RightEye, anchors.RightEye.X, anchors.RightEye.Y, anchors.FaceAngleRad);
        }

        if (features.LipOuter is not null)
        {
            float lipScale = ScaleLipWidthToMouthCorners(features.LipOuter, anchors);
            if (features.LipInner is not null && lipScale > 0)
            {
                ScaleFeatureAlongAxis(features.LipInner, features.LipOuter.CenterX, features.LipOuter.CenterY, GetMouthAxisX(anchors), GetMouthAxisY(anchors), lipScale, anchors.FaceAngleRad);
            }

            float dx = anchors.MouthCenter.X - features.LipOuter.CenterX;
            float dy = anchors.MouthCenter.Y - features.LipOuter.CenterY;
            TranslateFeature(features.LipOuter, dx, dy, anchors.FaceAngleRad);
            if (features.LipInner is not null)
            {
                TranslateFeature(features.LipInner, dx, dy, anchors.FaceAngleRad);
            }
        }
    }

    private static float ScaleLipWidthToMouthCorners(AnchorMeshFeature lipOuter, YuNetAnchorSet anchors)
    {
        float targetWidth = Distance(anchors.LeftMouthCorner.X, anchors.LeftMouthCorner.Y, anchors.RightMouthCorner.X, anchors.RightMouthCorner.Y);
        if (targetWidth < 4 || lipOuter.Points.Count < 4)
        {
            return 0;
        }

        float axisX = GetMouthAxisX(anchors);
        float axisY = GetMouthAxisY(anchors);
        (float min, float max) = ProjectFeatureRange(lipOuter, lipOuter.CenterX, lipOuter.CenterY, axisX, axisY);
        float currentWidth = max - min;
        if (currentWidth < 1)
        {
            return 0;
        }

        float scale = Math.Clamp(targetWidth / currentWidth, 0.62f, 1.58f);
        ScaleFeatureAlongAxis(lipOuter, lipOuter.CenterX, lipOuter.CenterY, axisX, axisY, scale, anchors.FaceAngleRad);
        return scale;
    }

    private static void ScaleFeatureAlongAxis(AnchorMeshFeature feature, float centerX, float centerY, float axisX, float axisY, float scale, float angleRad)
    {
        float normalX = -axisY;
        float normalY = axisX;
        foreach (AnchorMeshPoint point in feature.Points)
        {
            float dx = point.SnappedX - centerX;
            float dy = point.SnappedY - centerY;
            float along = dx * axisX + dy * axisY;
            float across = dx * normalX + dy * normalY;
            point.SnappedX = centerX + along * scale * axisX + across * normalX;
            point.SnappedY = centerY + along * scale * axisY + across * normalY;
            point.ImageX = point.SnappedX;
            point.ImageY = point.SnappedY;
            point.Source = point.Source == "MaskSnapped" ? point.Source : "YuNetAligned";
        }

        AnchorMeshMetrics.Update(feature, angleRad);
    }

    private static (float Min, float Max) ProjectFeatureRange(AnchorMeshFeature feature, float centerX, float centerY, float axisX, float axisY)
    {
        float min = float.MaxValue;
        float max = float.MinValue;
        foreach (AnchorMeshPoint point in feature.Points)
        {
            float projection = (point.SnappedX - centerX) * axisX + (point.SnappedY - centerY) * axisY;
            min = MathF.Min(min, projection);
            max = MathF.Max(max, projection);
        }

        return (min, max);
    }

    private static float GetMouthAxisX(YuNetAnchorSet anchors)
    {
        float dx = anchors.RightMouthCorner.X - anchors.LeftMouthCorner.X;
        float dy = anchors.RightMouthCorner.Y - anchors.LeftMouthCorner.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        return length < 1 ? MathF.Cos(anchors.FaceAngleRad) : dx / length;
    }

    private static float GetMouthAxisY(YuNetAnchorSet anchors)
    {
        float dx = anchors.RightMouthCorner.X - anchors.LeftMouthCorner.X;
        float dy = anchors.RightMouthCorner.Y - anchors.LeftMouthCorner.Y;
        float length = MathF.Sqrt(dx * dx + dy * dy);
        return length < 1 ? MathF.Sin(anchors.FaceAngleRad) : dy / length;
    }

    private static void TranslateFeatureCenterTo(AnchorMeshFeature feature, float targetX, float targetY, float angleRad)
    {
        TranslateFeature(feature, targetX - feature.CenterX, targetY - feature.CenterY, angleRad);
    }

    private static void TranslateFeature(AnchorMeshFeature feature, float dx, float dy, float angleRad)
    {
        foreach (AnchorMeshPoint point in feature.Points)
        {
            point.ImageX += dx;
            point.ImageY += dy;
            point.SnappedX += dx;
            point.SnappedY += dy;
            point.Source = point.Source == "MaskSnapped" ? point.Source : "YuNetAligned";
        }

        AnchorMeshMetrics.Update(feature, angleRad);
    }

    private static float Distance(float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static AnchorMeshFitTransform2D CreateFitTransform(FaceFitBox fitBox)
    {
        float scaleX = fitBox.Width / TemplateFaceWidth;
        float scaleY = fitBox.Height / TemplateFaceHeight;
        (scaleX, scaleY) = ClampScaleDifference(scaleX, scaleY);
        return new AnchorMeshFitTransform2D(
            scaleX,
            scaleY,
            fitBox.RotationRad,
            fitBox.CenterX,
            fitBox.CenterY,
            TemplateFaceCenter.X,
            TemplateFaceCenter.Y);
    }

    private static (float ScaleX, float ScaleY) ClampScaleDifference(float scaleX, float scaleY)
    {
        float average = (scaleX + scaleY) * 0.5f;
        if (average <= 0.001f)
        {
            return (scaleX, scaleY);
        }

        float min = average * 0.82f;
        float max = average * 1.18f;
        return (Math.Clamp(scaleX, min, max), Math.Clamp(scaleY, min, max));
    }

    private static float GetProjectedX(AnchorMeshPoint point)
    {
        return point.Source == "PoseProjected" ? point.ProjectedX : point.TemplateX;
    }

    private static float GetProjectedY(AnchorMeshPoint point)
    {
        return point.Source == "PoseProjected" ? point.ProjectedY : point.TemplateY;
    }
}
