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

        return aligned;
    }

    public void LockPrimaryFeatureCenters(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        bool leftEyeFollowedPupil = TryFollowEyeCenterToPupil(features.LeftEye, features.LeftPupil, anchors.FaceAngleRad);
        if (!leftEyeFollowedPupil && features.LeftEye is not null)
        {
            TranslateFeatureCenterTo(features.LeftEye, anchors.LeftEye.X, anchors.LeftEye.Y, anchors.FaceAngleRad);
        }

        if (!leftEyeFollowedPupil && features.LeftPupil is not null)
        {
            TranslateFeatureCenterTo(features.LeftPupil, anchors.LeftEye.X, anchors.LeftEye.Y, anchors.FaceAngleRad);
        }

        bool rightEyeFollowedPupil = TryFollowEyeCenterToPupil(features.RightEye, features.RightPupil, anchors.FaceAngleRad);
        if (!rightEyeFollowedPupil && features.RightEye is not null)
        {
            TranslateFeatureCenterTo(features.RightEye, anchors.RightEye.X, anchors.RightEye.Y, anchors.FaceAngleRad);
        }

        if (!rightEyeFollowedPupil && features.RightPupil is not null)
        {
            TranslateFeatureCenterTo(features.RightPupil, anchors.RightEye.X, anchors.RightEye.Y, anchors.FaceAngleRad);
        }

        ConstrainBrowToEyeRatioBand(features.LeftBrow, anchors.LeftEye, anchors);
        ConstrainBrowToEyeRatioBand(features.RightBrow, anchors.RightEye, anchors);
        ConstrainNoseToAnchorRatioBand(features.Nose, anchors);

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

        if (features.Nose is not null)
        {
            AnchorMeshMetrics.Update(features.Nose, anchors.FaceAngleRad);
        }
    }

    private static void ConstrainBrowToEyeRatioBand(AnchorMeshFeature? brow, PointF eyeCenter, YuNetAnchorSet anchors)
    {
        if (brow is null || brow.Points.Count == 0 || anchors.EyeDistance <= 1)
        {
            return;
        }

        if (brow.SnapMode.Equals("SoftSnap", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        float axisX = MathF.Cos(anchors.FaceAngleRad);
        float axisY = MathF.Sin(anchors.FaceAngleRad);
        float upX = MathF.Sin(anchors.FaceAngleRad);
        float upY = -MathF.Cos(anchors.FaceAngleRad);
        float dx0 = brow.CenterX - eyeCenter.X;
        float dy0 = brow.CenterY - eyeCenter.Y;
        float currentUp = dx0 * upX + dy0 * upY;
        float currentSide = dx0 * axisX + dy0 * axisY;
        float targetUp = Math.Clamp(currentUp, anchors.EyeDistance * 0.18f, anchors.EyeDistance * 0.42f);
        float targetSide = Math.Clamp(currentSide, -anchors.EyeDistance * 0.34f, anchors.EyeDistance * 0.34f);
        float targetX = eyeCenter.X + upX * targetUp + axisX * targetSide;
        float targetY = eyeCenter.Y + upY * targetUp + axisY * targetSide;
        float dx = targetX - brow.CenterX;
        float dy = targetY - brow.CenterY;
        if (MathF.Abs(dx) > 0.5f || MathF.Abs(dy) > 0.5f)
        {
            TranslateFeature(brow, dx, dy, anchors.FaceAngleRad);
            brow.SnapMode = "EyeRatioConstrainedBrowRoi";
        }
    }

    private static void ConstrainNoseToAnchorRatioBand(AnchorMeshFeature? nose, YuNetAnchorSet anchors)
    {
        if (nose is null || nose.Points.Count == 0 || anchors.EyeDistance <= 1)
        {
            return;
        }

        AnchorMeshPoint? tip = nose.Points.FirstOrDefault(point => point.Role.Equals("NoseTipTriangleApex", StringComparison.OrdinalIgnoreCase))
            ?? nose.Points.FirstOrDefault(point => point.Name == "Nose_08");
        if (tip is null)
        {
            return;
        }

        float axisX = MathF.Cos(anchors.FaceAngleRad);
        float axisY = MathF.Sin(anchors.FaceAngleRad);
        float downX = -axisY;
        float downY = axisX;
        float tipDx = tip.SnappedX - anchors.NoseTip.X;
        float tipDy = tip.SnappedY - anchors.NoseTip.Y;
        float tipSide = tipDx * axisX + tipDy * axisY;
        float tipDown = tipDx * downX + tipDy * downY;
        float targetTipSide = Math.Clamp(tipSide, -anchors.EyeDistance * 0.07f, anchors.EyeDistance * 0.07f);
        float targetTipDown = Math.Clamp(tipDown, -anchors.EyeDistance * 0.10f, anchors.EyeDistance * 0.10f);
        float targetTipX = anchors.NoseTip.X + axisX * targetTipSide + downX * targetTipDown;
        float targetTipY = anchors.NoseTip.Y + axisY * targetTipSide + downY * targetTipDown;
        float translateX = targetTipX - tip.SnappedX;
        float translateY = targetTipY - tip.SnappedY;
        if (MathF.Abs(translateX) > 0.5f || MathF.Abs(translateY) > 0.5f)
        {
            TranslateFeature(nose, translateX, translateY, anchors.FaceAngleRad);
        }

        ConstrainNoseBaseBetweenTipAndMouth(nose, anchors, axisX, axisY, downX, downY);
        nose.SnapMode = "NoseRatioConstrained";
    }

    private static void ConstrainNoseBaseBetweenTipAndMouth(AnchorMeshFeature nose, YuNetAnchorSet anchors, float axisX, float axisY, float downX, float downY)
    {
        List<AnchorMeshPoint> basePoints = nose.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase) ||
                            point.Role.Contains("Wing", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (basePoints.Count == 0)
        {
            return;
        }

        float noseToMouth = MathF.Max(anchors.EyeDistance * 0.22f, Distance(anchors.NoseTip.X, anchors.NoseTip.Y, anchors.MouthCenter.X, anchors.MouthCenter.Y));
        float averageBaseDown = 0;
        float averageBaseSide = 0;
        foreach (AnchorMeshPoint point in basePoints)
        {
            float dx = point.SnappedX - anchors.NoseTip.X;
            float dy = point.SnappedY - anchors.NoseTip.Y;
            averageBaseDown += dx * downX + dy * downY;
            averageBaseSide += dx * axisX + dy * axisY;
        }

        averageBaseDown /= basePoints.Count;
        averageBaseSide /= basePoints.Count;
        float targetBaseDown = Math.Clamp(averageBaseDown, noseToMouth * 0.28f, noseToMouth * 0.68f);
        float targetBaseSide = Math.Clamp(averageBaseSide, -anchors.EyeDistance * 0.06f, anchors.EyeDistance * 0.06f);
        float deltaDown = targetBaseDown - averageBaseDown;
        float deltaSide = targetBaseSide - averageBaseSide;
        if (MathF.Abs(deltaDown) <= 0.5f && MathF.Abs(deltaSide) <= 0.5f)
        {
            return;
        }

        TranslateFeature(nose, downX * deltaDown + axisX * deltaSide, downY * deltaDown + axisY * deltaSide, anchors.FaceAngleRad);
    }

    public void ApplyNoseTipFromNostrilTriangle(AnchorMeshFeatureSet features, float angleRad)
    {
        AnchorMeshFeature? nose = features.Nose;
        if (nose is null || nose.Points.Count < 22)
        {
            return;
        }

        List<AnchorMeshPoint> leftNostril = nose.Points
            .Where(point => point.Role.Contains("LeftNostril", StringComparison.OrdinalIgnoreCase))
            .ToList();
        List<AnchorMeshPoint> rightNostril = nose.Points
            .Where(point => point.Role.Contains("RightNostril", StringComparison.OrdinalIgnoreCase))
            .ToList();
        AnchorMeshPoint? tip = nose.Points.FirstOrDefault(point => point.Role.Equals("NoseTipTriangleApex", StringComparison.OrdinalIgnoreCase))
            ?? nose.Points.FirstOrDefault(point => point.Name == "Nose_08");
        if (leftNostril.Count == 0 || rightNostril.Count == 0 || tip is null)
        {
            return;
        }

        PointF leftCenter = AveragePoint(leftNostril);
        PointF rightCenter = AveragePoint(rightNostril);
        float midX = (leftCenter.X + rightCenter.X) * 0.5f;
        float midY = (leftCenter.Y + rightCenter.Y) * 0.5f;
        float dx = rightCenter.X - leftCenter.X;
        float dy = rightCenter.Y - leftCenter.Y;
        float baseLength = MathF.Sqrt(dx * dx + dy * dy);
        if (baseLength < 1.0f)
        {
            return;
        }

        float upX = MathF.Sin(angleRad);
        float upY = -MathF.Cos(angleRad);
        float apexDistance = baseLength * 0.8660254f;
        tip.ImageX = midX + upX * apexDistance;
        tip.ImageY = midY + upY * apexDistance;
        tip.SnappedX = tip.ImageX;
        tip.SnappedY = tip.ImageY;
        tip.Source = "NostrilEquilateralApex";
        tip.Confidence = MathF.Max(tip.Confidence, 0.72f);

        AdjustNoseTipSupportPoint(nose, "Nose_07", tip, leftCenter, angleRad);
        AdjustNoseTipSupportPoint(nose, "Nose_09", tip, rightCenter, angleRad);
        AnchorMeshMetrics.Update(nose, angleRad);
    }

    private static PointF AveragePoint(IReadOnlyList<AnchorMeshPoint> points)
    {
        float x = 0;
        float y = 0;
        foreach (AnchorMeshPoint point in points)
        {
            x += point.SnappedX;
            y += point.SnappedY;
        }

        return new PointF(x / points.Count, y / points.Count);
    }

    private static void AdjustNoseTipSupportPoint(AnchorMeshFeature nose, string pointName, AnchorMeshPoint tip, PointF nostrilCenter, float angleRad)
    {
        AnchorMeshPoint? support = nose.Points.FirstOrDefault(point => point.Name == pointName);
        if (support is null)
        {
            return;
        }

        support.ImageX = tip.SnappedX * 0.62f + nostrilCenter.X * 0.38f;
        support.ImageY = tip.SnappedY * 0.62f + nostrilCenter.Y * 0.38f;
        support.SnappedX = support.ImageX;
        support.SnappedY = support.ImageY;
        support.Source = "NostrilEquilateralSupport";
        support.Confidence = MathF.Max(support.Confidence, 0.62f);
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

    private static bool TryFollowEyeCenterToPupil(AnchorMeshFeature? eye, AnchorMeshFeature? pupil, float angleRad)
    {
        if (eye is null || pupil is null || pupil.Points.Count == 0)
        {
            return false;
        }

        if (!HasGuidedPupilCenter(eye, pupil))
        {
            return false;
        }

        TranslateFeatureCenterTo(eye, pupil.CenterX, pupil.CenterY, angleRad);
        eye.SnapMode = "FollowPupilCenterGuide";
        return true;
    }

    private static bool HasGuidedPupilCenter(AnchorMeshFeature eye, AnchorMeshFeature pupil)
    {
        bool hasSnappedGuide = pupil.Points.Any(point => point.Source.Equals("MaskSnapped", StringComparison.OrdinalIgnoreCase));
        if (!hasSnappedGuide)
        {
            return false;
        }

        return PupilGuideProfile.IsPupilDiameterPlausible(eye.Width, pupil.Width);
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

        PointF center = ClampFaceOutlineCenterToEyeNoseBand(outline.CenterX, outline.CenterY, anchors, downX, downY);
        float centerX = center.X;
        float centerY = center.Y;
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
            point.Source = "EyeNoseBandOval";
            point.Confidence = MathF.Max(point.Confidence, anchors.Score * 0.88f);
        }

        outline.SnapMode = "EyeNoseBandOval";
        AnchorMeshMetrics.Update(outline, anchors.FaceAngleRad);
    }

    public bool ConstrainFaceOutlineChinToNostrilCompassLimit(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        AnchorMeshFeature? outline = features.FaceOutline;
        if (outline is null || outline.Points.Count < 8)
        {
            return false;
        }

        PointF browCenter = EstimateBrowCenter(features, anchors);
        PointF nostrilCenter = EstimateNostrilCenter(features, anchors);
        float compassRadius = Distance(browCenter.X, browCenter.Y, nostrilCenter.X, nostrilCenter.Y);
        if (compassRadius <= 1)
        {
            return false;
        }

        float axisX = MathF.Cos(anchors.FaceAngleRad);
        float axisY = MathF.Sin(anchors.FaceAngleRad);
        float downX = -axisY;
        float downY = axisX;
        bool moved = false;

        foreach (AnchorMeshPoint point in outline.Points)
        {
            if (!IsChinLimitPoint(point))
            {
                continue;
            }

            float dx = point.SnappedX - nostrilCenter.X;
            float dy = point.SnappedY - nostrilCenter.Y;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance >= compassRadius)
            {
                continue;
            }

            float ux;
            float uy;
            if (distance <= 0.001f)
            {
                ux = downX;
                uy = downY;
            }
            else
            {
                ux = dx / distance;
                uy = dy / distance;
            }

            point.SnappedX = nostrilCenter.X + ux * compassRadius;
            point.SnappedY = nostrilCenter.Y + uy * compassRadius;
            point.ImageX = point.SnappedX;
            point.ImageY = point.SnappedY;
            point.Source = "ChinNostrilCompassLimited";
            point.Confidence = MathF.Max(point.Confidence, anchors.Score * 0.82f);
            moved = true;
        }

        if (moved)
        {
            outline.SnapMode = "ChinNostrilCompassLimited";
            AnchorMeshMetrics.Update(outline, anchors.FaceAngleRad);
        }

        return moved;
    }

    private static PointF EstimateNostrilCenter(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        AnchorMeshFeature? nose = features.Nose;
        if (nose is null || nose.Points.Count == 0)
        {
            return anchors.NoseTip;
        }

        List<AnchorMeshPoint> nostrils = nose.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nostrils.Count == 0)
        {
            return anchors.NoseTip;
        }

        float x = 0;
        float y = 0;
        foreach (AnchorMeshPoint point in nostrils)
        {
            x += point.SnappedX;
            y += point.SnappedY;
        }

        return new PointF(x / nostrils.Count, y / nostrils.Count);
    }

    private static PointF EstimateBrowCenter(AnchorMeshFeatureSet features, YuNetAnchorSet anchors)
    {
        List<AnchorMeshFeature> brows = new();
        if (features.LeftBrow is { Points.Count: > 0 } leftBrow)
        {
            brows.Add(leftBrow);
        }

        if (features.RightBrow is { Points.Count: > 0 } rightBrow)
        {
            brows.Add(rightBrow);
        }

        if (brows.Count == 0)
        {
            return anchors.EyeCenter;
        }

        float x = 0;
        float y = 0;
        float weight = 0;
        foreach (AnchorMeshFeature brow in brows)
        {
            float browWeight = MathF.Max(1.0f, brow.Points.Count);
            x += brow.CenterX * browWeight;
            y += brow.CenterY * browWeight;
            weight += browWeight;
        }

        return weight <= 0
            ? anchors.EyeCenter
            : new PointF(x / weight, y / weight);
    }

    private static bool IsChinLimitPoint(AnchorMeshPoint point)
    {
        if (point.FeatureName.Equals("FaceOutline", StringComparison.OrdinalIgnoreCase) &&
            point.Index is >= 10 and <= 16)
        {
            return true;
        }

        return point.Role.Contains("Chin", StringComparison.OrdinalIgnoreCase);
    }

    private static PointF ClampFaceOutlineCenterToEyeNoseBand(float candidateX, float candidateY, YuNetAnchorSet anchors, float downX, float downY)
    {
        float noseLocalY = ProjectLocalY(anchors.NoseTip.X, anchors.NoseTip.Y, anchors.EyeCenter.X, anchors.EyeCenter.Y, downX, downY);
        if (MathF.Abs(noseLocalY) < 1.0f)
        {
            return new PointF(candidateX, candidateY);
        }

        float minLocalY = MathF.Min(0, noseLocalY);
        float maxLocalY = MathF.Max(0, noseLocalY);
        float candidateLocalY = ProjectLocalY(candidateX, candidateY, anchors.EyeCenter.X, anchors.EyeCenter.Y, downX, downY);
        float clampedLocalY = Math.Clamp(candidateLocalY, minLocalY, maxLocalY);
        float deltaLocalY = clampedLocalY - candidateLocalY;

        return new PointF(
            candidateX + downX * deltaLocalY,
            candidateY + downY * deltaLocalY);
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
