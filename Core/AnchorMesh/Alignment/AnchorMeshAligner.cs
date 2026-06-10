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
        FitFaceOutlineToNoseCenteredOval(aligned, anchors);
        FitJawBottomLineToFaceBoxBottom(aligned, anchors);

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

    private static void FitFaceOutlineToNoseCenteredOval(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        AnchorMeshFeature? outline = features.FaceOutline;
        if (outline is null || outline.Points.Count < 8)
        {
            return;
        }

        float axisX = MathF.Cos(anchors.FaceAngleRad);
        float axisY = MathF.Sin(anchors.FaceAngleRad);
        float downX = -axisY;
        float downY = axisX;

        float centerX = anchors.EyeCenter.X;
        float centerY = anchors.EyeCenter.Y;
        float boxTopLocalY = ProjectLocalY(anchors.FaceBox.X + anchors.FaceBox.Width * 0.5f, anchors.FaceBox.Top, centerX, centerY, downX, downY);
        float chinY = anchors.FaceBox.Top + anchors.FaceBox.Height * 0.92f;
        float chinLocalY = ProjectLocalY(anchors.FaceBox.X + anchors.FaceBox.Width * 0.5f, chinY, centerX, centerY, downX, downY);

        float minTopDistance = MathF.Max(anchors.FaceBox.Height * 0.34f, anchors.EyeDistance * 0.86f);
        float minBottomDistance = MathF.Max(anchors.FaceBox.Height * 0.23f, anchors.EyeDistance * 0.72f);
        float topLocalY = MathF.Min(boxTopLocalY, -minTopDistance);
        float bottomLocalY = MathF.Max(chinLocalY, minBottomDistance);
        if (bottomLocalY <= topLocalY + anchors.EyeDistance)
        {
            bottomLocalY = topLocalY + MathF.Max(anchors.FaceBox.Height * 0.78f, anchors.EyeDistance * 2.4f);
        }

        float centerLocalY = (topLocalY + bottomLocalY) * 0.5f;
        float radiusY = MathF.Max(1.0f, (bottomLocalY - topLocalY) * 0.5f);
        float radiusX = MathF.Max(anchors.FaceBox.Width * 0.50f, anchors.EyeDistance * 1.03f);

        for (int i = 0; i < outline.Points.Count; i++)
        {
            float t = i / (float)outline.Points.Count;
            float theta = t * MathF.Tau;
            float localX = MathF.Cos(theta) * radiusX;
            float localY = centerLocalY + MathF.Sin(theta) * radiusY;
            AnchorMeshPoint point = outline.Points[i];
            point.ImageX = centerX + localX * axisX + localY * downX;
            point.ImageY = centerY + localX * axisY + localY * downY;
            point.SnappedX = point.ImageX;
            point.SnappedY = point.ImageY;
            point.Source = "EyeCenterOval";
            point.Confidence = MathF.Max(point.Confidence, anchors.Score * 0.88f);
        }

        outline.SnapMode = "EyeCenterOval";
        AnchorMeshMetrics.Update(outline, anchors.FaceAngleRad);
    }

    private static void FitJawBottomLineToFaceBoxBottom(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        AnchorMeshFeature? jawBottom = features.Neck;
        if (jawBottom is null || jawBottom.Points.Count == 0)
        {
            return;
        }

        float axisX = MathF.Cos(anchors.FaceAngleRad);
        float axisY = MathF.Sin(anchors.FaceAngleRad);
        float downX = -axisY;
        float downY = axisX;
        float centerX = anchors.FaceBox.X + anchors.FaceBox.Width * 0.5f;
        float centerY = anchors.FaceBox.Bottom;
        float halfWidth = MathF.Max(anchors.FaceBox.Width * 0.33f, anchors.EyeDistance * 0.72f);

        int count = jawBottom.Points.Count;
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0.5f : i / (float)(count - 1);
            float along = (t - 0.5f) * 2.0f * halfWidth;
            float softArc = MathF.Sin(t * MathF.PI) * anchors.EyeDistance * 0.018f;
            AnchorMeshPoint point = jawBottom.Points[i];
            point.ImageX = centerX + along * axisX + softArc * downX;
            point.ImageY = centerY + along * axisY + softArc * downY;
            point.SnappedX = point.ImageX;
            point.SnappedY = point.ImageY;
            point.Source = "FaceBoxBottomJawLine";
            point.Confidence = MathF.Max(point.Confidence, anchors.Score * 0.90f);
        }

        jawBottom.SnapMode = "FaceBoxBottomJawLine";
        AnchorMeshMetrics.Update(jawBottom, anchors.FaceAngleRad);
    }

    private static float ProjectLocalY(float x, float y, float originX, float originY, float downX, float downY)
    {
        return (x - originX) * downX + (y - originY) * downY;
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
