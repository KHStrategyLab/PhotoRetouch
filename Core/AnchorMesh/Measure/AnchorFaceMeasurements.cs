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

    public float FaceHeightToWidthRatio { get; set; }

    public float EyeCenterYToFaceHeightRatio { get; set; }

    public float EyeDistanceToFaceWidthRatio { get; set; }

    public float EyeHeightBalanceScore { get; set; }

    public float EyeLevelScore { get; set; }

    public float EyeCenterOffsetToFaceWidth { get; set; }

    public float EyeLineAngleDeg { get; set; }

    public float EyeMetricGuideConfidence { get; set; }

    public float NoseTipYToFaceHeightRatio { get; set; }

    public float NoseBaseYToFaceHeightRatio { get; set; }

    public float NoseCenterOffsetToFaceWidth { get; set; }

    public float NoseEyeCenterOffsetToFaceWidth { get; set; }

    public float EstimatedNoseLengthToFaceHeightRatio { get; set; }

    public float EstimatedNoseLengthToEyeDistanceRatio { get; set; }

    public float MouthCenterYToFaceHeightRatio { get; set; }

    public float MouthCenterOffsetToFaceWidth { get; set; }

    public float MouthNoseCenterOffsetToFaceWidth { get; set; }

    public float MouthEyeCenterOffsetToFaceWidth { get; set; }

    public float MouthWidthToEyeDistanceRatio { get; set; }

    public float MouthWidthToFaceWidthRatio { get; set; }

    public float MouthCornerLevelScore { get; set; }

    public float MouthCornerSlopeDeg { get; set; }

    public float MouthWidthBalanceScore { get; set; }

    public float MouthMetricGuideConfidence { get; set; }

    public float PhiltrumToLowerFaceRatio { get; set; }

    public float LowerFacePhiltrumLipChinGuideRatio { get; set; }

    public float CheekFaceWidthRatio { get; set; }

    public float JawFaceWidthRatio { get; set; }

    public float ChinFaceWidthRatio { get; set; }

    public float JawWidthToCheekWidthRatio { get; set; }

    public float JawMidToCheekWidthRatio { get; set; }

    public float ChinWidthToCheekWidthRatio { get; set; }

    public float JawToChinTaperRatio { get; set; }

    public float LowerFaceToFaceHeightRatio { get; set; }

    public float MouthToChinToLowerFaceRatio { get; set; }

    public float WidthAt20 { get; set; }

    public float WidthAt35 { get; set; }

    public float WidthAt50 { get; set; }

    public float WidthAt65 { get; set; }

    public float WidthAt80 { get; set; }

    public float WidthAt90 { get; set; }

    public float ForeheadWidthRatio { get; set; }

    public float EyeLevelWidthRatio { get; set; }

    public float CheekLevelWidthRatio { get; set; }

    public float MouthLevelWidthRatio { get; set; }

    public float JawLevelWidthRatio { get; set; }

    public float ChinLevelWidthRatio { get; set; }

    public float ContourBalanceScore { get; set; }

    public float LowerContourBalanceScore { get; set; }

    public float ContourMetricGuideConfidence { get; set; }

    public float FeatureRatioGuideConfidence { get; set; }

    public float MaskCoverageConfidence { get; set; }

    public List<string> Warnings { get; } = new();
}
