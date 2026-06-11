namespace PhotoRetouch.AnchorMesh;

public static class PhiltrumGuideProfile
{
    public const double HeightToNoseMouthCenterRatioMin = 0.45;
    public const double HeightToNoseMouthCenterRatioMax = 0.70;
    public const double RidgeWidthToNoseWidthMin = 0.30;
    public const double RidgeWidthToNoseWidthMax = 0.60;
    public const double CenterAlignmentToleranceToFaceWidth = 0.02;

    public static double ClampPhiltrumHeight(double noseToMouthCenterDistance, double estimatedHeight)
    {
        double min = Math.Max(4.0, noseToMouthCenterDistance * HeightToNoseMouthCenterRatioMin);
        double max = Math.Max(min + 1.0, noseToMouthCenterDistance * HeightToNoseMouthCenterRatioMax);
        return Math.Clamp(estimatedHeight, min, max);
    }
}
