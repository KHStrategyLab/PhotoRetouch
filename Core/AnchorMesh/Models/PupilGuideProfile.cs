namespace PhotoRetouch.AnchorMesh;

public static class PupilGuideProfile
{
    public const double ReferenceEyeWidthPx = 100.0;
    public const double ReferencePupilDiameterPx = 8.0;
    public const double ReferenceCatchlightSizePx = 4.0;

    public const double PupilDiameterToEyeWidthRatio = ReferencePupilDiameterPx / ReferenceEyeWidthPx;
    public const double CatchlightToEyeWidthRatio = ReferenceCatchlightSizePx / ReferenceEyeWidthPx;

    public static double GetExpectedPupilDiameterPx(double eyeWidthPx)
    {
        return Math.Max(1.0, eyeWidthPx * PupilDiameterToEyeWidthRatio);
    }

    public static double GetExpectedCatchlightSizePx(double eyeWidthPx)
    {
        return Math.Max(1.0, eyeWidthPx * CatchlightToEyeWidthRatio);
    }

    public static bool IsPupilDiameterPlausible(double eyeWidthPx, double pupilWidthPx)
    {
        if (eyeWidthPx <= 1.0 || pupilWidthPx <= 0.5)
        {
            return false;
        }

        double expected = GetExpectedPupilDiameterPx(eyeWidthPx);
        double min = Math.Max(1.5, expected * 0.45);
        double max = Math.Max(min + 0.5, expected * 2.25);
        return pupilWidthPx >= min && pupilWidthPx <= max;
    }
}
