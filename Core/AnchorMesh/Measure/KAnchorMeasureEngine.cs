namespace PhotoRetouch.AnchorMesh;

public sealed class KAnchorMeasureEngine
{
    public AnchorFaceMeasurements Measure(YuNetAnchorSet anchors, MaskPlane? faceMask = null)
    {
        AnchorFaceMeasurements measurements = new()
        {
            EyeDistance = anchors.EyeDistance,
            FaceBoxWidth = anchors.FaceBox.Width,
            FaceBoxHeight = anchors.FaceBox.Height,
            RotationRad = anchors.FaceAngleRad,
            RotationDeg = anchors.FaceAngleRad * 180.0f / MathF.PI,
            EyeToNoseDistance = Distance(anchors.EyeCenter.X, anchors.EyeCenter.Y, anchors.NoseTip.X, anchors.NoseTip.Y),
            NoseToMouthDistance = Distance(anchors.NoseTip.X, anchors.NoseTip.Y, anchors.MouthCenter.X, anchors.MouthCenter.Y)
        };

        measurements.FaceWidthToEyeDistanceRatio = SafeRatio(measurements.FaceBoxWidth, measurements.EyeDistance);
        measurements.FaceHeightToEyeDistanceRatio = SafeRatio(measurements.FaceBoxHeight, measurements.EyeDistance);

        if (faceMask is not null)
        {
            AddMaskMeasurements(measurements, anchors, faceMask);
        }
        else
        {
            measurements.FaceMaskLeftX = anchors.FaceBox.Left;
            measurements.FaceMaskRightX = anchors.FaceBox.Right;
            measurements.FaceMaskTopY = anchors.FaceBox.Top;
            measurements.FaceMaskBottomY = anchors.FaceBox.Bottom;
            measurements.FaceMaskWidth = anchors.FaceBox.Width;
            measurements.FaceMaskHeight = anchors.FaceBox.Height;
            measurements.MaskHeightToEyeDistanceRatio = measurements.FaceHeightToEyeDistanceRatio;
            measurements.MaskCoverageConfidence = 0.35f;
            measurements.Warnings.Add("face_mask_missing_measurements_use_facebox");
        }

        float estimatedChinY = measurements.FaceMaskBottomY > 0
            ? measurements.FaceMaskBottomY
            : anchors.FaceBox.Bottom;
        measurements.MouthToChinDistance = MathF.Max(0, estimatedChinY - anchors.MouthCenter.Y);
        measurements.EyeLineToMouthDistance = MathF.Max(0, anchors.MouthCenter.Y - anchors.EyeCenter.Y);
        measurements.EyeLineToChinDistance = MathF.Max(0, estimatedChinY - anchors.EyeCenter.Y);
        measurements.PhiltrumLength = MathF.Max(0, anchors.MouthCenter.Y - anchors.NoseTip.Y);
        measurements.ChinLength = MathF.Max(0, estimatedChinY - anchors.MouthCenter.Y);
        measurements.EyeLineToChinToEyeDistanceRatio = SafeRatio(measurements.EyeLineToChinDistance, measurements.EyeDistance);
        measurements.HorizontalFaceOutlineWidth = measurements.FaceMaskWidth > 0 ? measurements.FaceMaskWidth : measurements.FaceBoxWidth;
        measurements.HorizontalCheekWidth = MeasureHorizontalMaskWidthAtY(faceMask, anchors.FaceBox, anchors.EyeCenter.Y + measurements.EyeDistance * 0.48f);
        measurements.HorizontalJawWidth = MeasureHorizontalMaskWidthAtY(faceMask, anchors.FaceBox, anchors.MouthCenter.Y + measurements.EyeDistance * 0.58f);
        measurements.VerticalForeheadToChinDistance = MathF.Max(0, estimatedChinY - measurements.FaceMaskTopY);
        measurements.CorrectedCenterX = (anchors.EyeCenter.X * 0.45f) + (anchors.NoseTip.X * 0.35f) + (anchors.MouthCenter.X * 0.20f);
        measurements.CorrectedCenterY = (anchors.EyeCenter.Y * 0.35f) + (anchors.NoseTip.Y * 0.35f) + (anchors.MouthCenter.Y * 0.30f);
        measurements.CenterOffsetX = measurements.CorrectedCenterX - (measurements.FaceMaskLeftX + measurements.FaceMaskRightX) * 0.5f;
        measurements.CenterOffsetY = measurements.CorrectedCenterY - (measurements.FaceMaskTopY + measurements.FaceMaskBottomY) * 0.5f;

        Validate(measurements);
        return measurements;
    }

    private static void AddMaskMeasurements(AnchorFaceMeasurements measurements, YuNetAnchorSet anchors, MaskPlane faceMask)
    {
        const double threshold = 0.35;
        int minX = faceMask.Width;
        int minY = faceMask.Height;
        int maxX = -1;
        int maxY = -1;
        int coveredPixels = 0;
        int faceBoxPixels = Math.Max(1, (int)Math.Round(anchors.FaceBox.Width * anchors.FaceBox.Height));

        int faceLeft = Math.Clamp((int)MathF.Floor(anchors.FaceBox.Left), 0, faceMask.Width - 1);
        int faceTop = Math.Clamp((int)MathF.Floor(anchors.FaceBox.Top), 0, faceMask.Height - 1);
        int faceRight = Math.Clamp((int)MathF.Ceiling(anchors.FaceBox.Right), 0, faceMask.Width - 1);
        int faceBottom = Math.Clamp((int)MathF.Ceiling(anchors.FaceBox.Bottom), 0, faceMask.Height - 1);

        for (int y = faceTop; y <= faceBottom; y++)
        {
            for (int x = faceLeft; x <= faceRight; x++)
            {
                if (faceMask[x, y] < threshold)
                {
                    continue;
                }

                minX = Math.Min(minX, x);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x);
                maxY = Math.Max(maxY, y);
                coveredPixels++;
            }
        }

        if (maxX < minX || maxY < minY)
        {
            measurements.FaceMaskLeftX = anchors.FaceBox.Left;
            measurements.FaceMaskRightX = anchors.FaceBox.Right;
            measurements.FaceMaskTopY = anchors.FaceBox.Top;
            measurements.FaceMaskBottomY = anchors.FaceBox.Bottom;
            measurements.FaceMaskWidth = anchors.FaceBox.Width;
            measurements.FaceMaskHeight = anchors.FaceBox.Height;
            measurements.MaskHeightToEyeDistanceRatio = measurements.FaceHeightToEyeDistanceRatio;
            measurements.MaskCoverageConfidence = 0.1f;
            measurements.Warnings.Add("face_mask_empty_inside_facebox");
            return;
        }

        measurements.FaceMaskLeftX = minX;
        measurements.FaceMaskRightX = maxX;
        measurements.FaceMaskTopY = minY;
        measurements.FaceMaskBottomY = maxY;
        measurements.FaceMaskWidth = maxX - minX + 1;
        measurements.FaceMaskHeight = maxY - minY + 1;
        measurements.MaskHeightToEyeDistanceRatio = SafeRatio(measurements.FaceMaskHeight, measurements.EyeDistance);
        measurements.MaskCoverageConfidence = Math.Clamp((float)(coveredPixels / (double)faceBoxPixels), 0.0f, 1.0f);
    }

    private static void Validate(AnchorFaceMeasurements measurements)
    {
        if (measurements.EyeDistance < 20)
        {
            measurements.Warnings.Add("eye_distance_too_small");
        }

        if (measurements.FaceMaskHeight <= 0 || measurements.FaceMaskWidth <= 0)
        {
            measurements.Warnings.Add("face_mask_size_invalid");
        }

        if (measurements.MaskHeightToEyeDistanceRatio is < 2.2f or > 6.2f)
        {
            measurements.Warnings.Add("face_mask_height_eye_distance_ratio_outside_expected_range");
        }
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float MeasureHorizontalMaskWidthAtY(MaskPlane? mask, System.Drawing.RectangleF fallbackFaceBox, float y)
    {
        if (mask is null)
        {
            return fallbackFaceBox.Width;
        }

        int yy = Math.Clamp((int)MathF.Round(y), 0, mask.Height - 1);
        int minX = mask.Width;
        int maxX = -1;
        int left = Math.Clamp((int)MathF.Floor(fallbackFaceBox.Left), 0, mask.Width - 1);
        int right = Math.Clamp((int)MathF.Ceiling(fallbackFaceBox.Right), 0, mask.Width - 1);
        for (int x = left; x <= right; x++)
        {
            if (mask[x, yy] < 0.35)
            {
                continue;
            }

            minX = Math.Min(minX, x);
            maxX = Math.Max(maxX, x);
        }

        return maxX < minX ? fallbackFaceBox.Width : maxX - minX + 1;
    }

    private static float SafeRatio(float value, float divisor)
    {
        return divisor <= 0.001f ? 0 : value / divisor;
    }
}
