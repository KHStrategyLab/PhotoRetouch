namespace PhotoRetouch.AnchorMesh;

public sealed class FaceFitBoxCalculator
{
    public FaceFitBox Calculate(YuNetAnchorSet anchors, AnchorFaceMeasurements measurements, MaskPlane? faceMask)
    {
        float width = measurements.HorizontalFaceOutlineWidth > 1
            ? measurements.HorizontalFaceOutlineWidth
            : anchors.FaceBox.Width;
        float height = measurements.VerticalForeheadToChinDistance > 1
            ? measurements.VerticalForeheadToChinDistance
            : anchors.FaceBox.Height;

        (width, height) = ClampScaleRatio(width, height, anchors.FaceBox.Width, anchors.FaceBox.Height);

        string source = faceMask is not null && measurements.MaskCoverageConfidence > 0.15f
            ? "SkinMask"
            : "YuNet";

        return new FaceFitBox
        {
            CenterX = measurements.CorrectedCenterX,
            CenterY = CalculateFitCenterY(anchors, measurements, height),
            Width = width,
            Height = height,
            RotationRad = measurements.RotationRad,
            Confidence = CalculateConfidence(measurements, faceMask),
            Source = source
        };
    }

    private static float CalculateFitCenterY(YuNetAnchorSet anchors, AnchorFaceMeasurements measurements, float height)
    {
        if (measurements.FaceMaskTopY > 0 && measurements.FaceMaskBottomY > measurements.FaceMaskTopY)
        {
            return (measurements.FaceMaskTopY + measurements.FaceMaskBottomY) * 0.5f;
        }

        float chinY = anchors.MouthCenter.Y + measurements.ChinLength;
        float foreheadY = chinY - height;
        return (foreheadY + chinY) * 0.5f;
    }

    private static (float Width, float Height) ClampScaleRatio(float width, float height, float fallbackWidth, float fallbackHeight)
    {
        if (width <= 1)
        {
            width = fallbackWidth;
        }

        if (height <= 1)
        {
            height = fallbackHeight;
        }

        float ratio = height / Math.Max(1, width);
        float fallbackRatio = fallbackHeight / Math.Max(1, fallbackWidth);
        float minRatio = fallbackRatio * 0.78f;
        float maxRatio = fallbackRatio * 1.24f;
        ratio = Math.Clamp(ratio, minRatio, maxRatio);
        height = width * ratio;
        return (width, height);
    }

    private static float CalculateConfidence(AnchorFaceMeasurements measurements, MaskPlane? faceMask)
    {
        float confidence = faceMask is null ? 0.45f : 0.65f;
        confidence += measurements.MaskCoverageConfidence * 0.28f;
        if (measurements.EyeLineToChinToEyeDistanceRatio is > 1.55f and < 2.45f)
        {
            confidence += 0.08f;
        }

        return Math.Clamp(confidence, 0.0f, 1.0f);
    }
}
