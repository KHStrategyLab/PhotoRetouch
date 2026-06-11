namespace PhotoRetouch.AnchorMesh;

public static class NoseGuideProfile
{
    public const double FaceThirdRatio = 1.0 / 3.0;
    public const double NoseWidthToFaceWidthMin = 0.18;
    public const double NoseWidthToFaceWidthMax = 0.25;
    public const double NoseWidthToEyeGapMin = 0.90;
    public const double NoseWidthToEyeGapMax = 1.25;

    public const double NostrilWidthToNoseWidthMin = 0.08;
    public const double NostrilWidthToNoseWidthMax = 0.22;
    public const double NostrilHeightToWidthMin = 0.45;
    public const double NostrilHeightToWidthMax = 0.95;
    public const double NostrilInwardShiftRatio = 0.10;
    public const double NostrilUpwardShiftRatio = 0.12;
    public const double NostrilTiltRadians = 0.18;

    public static double ClampNostrilWidth(double noseWidth, double estimatedWidth)
    {
        double min = Math.Max(2.0, noseWidth * NostrilWidthToNoseWidthMin);
        double max = Math.Max(min + 0.5, noseWidth * NostrilWidthToNoseWidthMax);
        return Math.Clamp(estimatedWidth, min, max);
    }

    public static double ClampNostrilHeight(double nostrilWidth, double estimatedHeight)
    {
        double min = Math.Max(1.5, nostrilWidth * NostrilHeightToWidthMin);
        double max = Math.Max(min + 0.5, nostrilWidth * NostrilHeightToWidthMax);
        return Math.Clamp(estimatedHeight, min, max);
    }
}
