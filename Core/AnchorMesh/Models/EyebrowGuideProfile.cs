namespace PhotoRetouch.AnchorMesh;

public static class EyebrowGuideProfile
{
    public const double BrowEyeDistanceFaceRatioMin = 0.015;
    public const double BrowEyeDistanceFaceRatioMax = 0.065;
    public const double BrowLengthToEyeWidthMin = 0.95;
    public const double BrowLengthToEyeWidthMax = 1.40;
    public const double BrowThicknessToEyeHeightMin = 0.12;
    public const double BrowThicknessToEyeHeightMax = 0.85;

    public const double RoiWidthToEyeWidthMin = 1.15;
    public const double RoiWidthToEyeWidthMax = 1.45;
    public const double RoiLowerToEyeHeightMin = 0.25;
    public const double RoiLowerToEyeHeightMax = 0.70;
    public const double RoiCenterToEyeHeightMin = 0.50;
    public const double RoiCenterToEyeHeightMax = 1.15;
    public const double RoiUpperToEyeHeightMin = 0.85;
    public const double RoiUpperToEyeHeightMax = 1.60;
    public const double HeadOutsideInnerCornerToEyeWidthMax = 0.05;
    public const double HeadInsideInnerCornerToEyeWidthMax = 0.20;
    public const double TailOutsideOuterCornerToEyeWidthMin = 0.15;
    public const double TailOutsideOuterCornerToEyeWidthMax = 0.35;

    public const double ArchPositionFromHeadMin = 0.55;
    public const double ArchPositionFromHeadMax = 0.78;
    public const double ArchRiseToEyeHeightMin = 0.10;
    public const double ArchRiseToEyeHeightMax = 0.35;

    public const double FrontHairWeight = 0.34;
    public const double BodyHairWeight = 0.48;
    public const double TailHairWeight = 0.18;
}
