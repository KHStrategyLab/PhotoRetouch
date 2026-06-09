namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorFaceMeasurements
{
    public float EyeDistance { get; set; }

    public float FaceBoxWidth { get; set; }

    public float FaceBoxHeight { get; set; }

    public float FaceMaskWidth { get; set; }

    public float FaceMaskHeight { get; set; }

    public float FaceMaskTopY { get; set; }

    public float FaceMaskBottomY { get; set; }

    public float FaceMaskLeftX { get; set; }

    public float FaceMaskRightX { get; set; }

    public float HorizontalFaceOutlineWidth { get; set; }

    public float HorizontalCheekWidth { get; set; }

    public float HorizontalJawWidth { get; set; }

    public float VerticalForeheadToChinDistance { get; set; }

    public float CorrectedCenterX { get; set; }

    public float CorrectedCenterY { get; set; }

    public float CenterOffsetX { get; set; }

    public float CenterOffsetY { get; set; }

    public float RotationRad { get; set; }

    public float RotationDeg { get; set; }

    public float EyeToNoseDistance { get; set; }

    public float NoseToMouthDistance { get; set; }

    public float EyeLineToMouthDistance { get; set; }

    public float MouthToChinDistance { get; set; }

    public float EyeLineToChinDistance { get; set; }

    public float PhiltrumLength { get; set; }

    public float ChinLength { get; set; }

    public float FaceWidthToEyeDistanceRatio { get; set; }

    public float FaceHeightToEyeDistanceRatio { get; set; }

    public float MaskHeightToEyeDistanceRatio { get; set; }

    public float EyeLineToChinToEyeDistanceRatio { get; set; }

    public float MaskCoverageConfidence { get; set; }

    public List<string> Warnings { get; } = new();
}
