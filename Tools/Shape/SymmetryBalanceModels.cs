using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record SymmetryBalanceToolset(
    bool EnableSymmetryBalance,
    double SymmetryAmount,
    bool SymmetryOvershootEnabled,
    double PreserveIdentityStrength,
    bool ProtectHardFeatures,
    double SymmetryMasterWeight,
    double MouthCornerBalanceAmount,
    double LowerEyeLineBalanceAmount,
    double UpperEyebrowBalanceAmount,
    double PupilSizeBalanceAmount,
    double EyeWidthBalanceAmount,
    double EyeHeightBalanceAmount,
    double NostrilSizeBalanceAmount,
    double NostrilHeightBalanceAmount,
    double NostrilPositionBalanceAmount,
    double NoseWingWidthBalanceAmount,
    double NoseWingContourBalanceAmount,
    double JawlineContourBalanceAmount,
    double JawWidthBalanceAmount,
    double ChinCenterBalanceAmount,
    double FaceOutlineBalanceAmount)
{
    public static SymmetryBalanceToolset Default { get; } = new(
        true,
        35,
        true,
        0.88,
        true,
        1,
        0.58,
        0.58,
        0.48,
        0.24,
        0.32,
        0.32,
        0.16,
        0.16,
        0.22,
        0.24,
        0.24,
        0.36,
        0.32,
        0.38,
        0.20);

    public double EffectiveSymmetryScale
    {
        get
        {
            if (!EnableSymmetryBalance)
            {
                return 0;
            }

            double amount = Math.Clamp(SymmetryAmount, 0, 100);
            double baseScale = amount <= 90
                ? amount / 90d
                : 1 + (amount - 90) / 10d * 0.08;
            if (!SymmetryOvershootEnabled)
            {
                baseScale = Math.Min(baseScale, 1);
            }

            return Math.Clamp(baseScale * Math.Clamp(SymmetryMasterWeight, 0, 1), 0, 1.08);
        }
    }

    public bool IsOvershootZone => SymmetryOvershootEnabled && SymmetryAmount >= 93;
}

public sealed record SymmetryBalanceAnalysisReport(
    WpfPoint LeftRightCenterLine,
    double MouthCornerHeightDelta,
    double LowerEyeLineHeightDelta,
    double UpperEyebrowHeightDelta,
    double PupilSizeDelta,
    double EyeWidthDelta,
    double EyeHeightDelta,
    double NostrilSizeDelta,
    double NostrilHeightDelta,
    double NostrilPositionDelta,
    double NoseWingWidthDelta,
    double NoseWingContourDelta,
    double JawlineContourDelta,
    double JawWidthDelta,
    double ChinCenterDelta,
    double FaceOutlineDelta,
    double SymmetryBalanceScore,
    double SuggestedSymmetryAmount,
    IReadOnlyList<string> DebugWarnings)
{
    public static SymmetryBalanceAnalysisReport Empty(WpfPoint centerLine)
    {
        return new SymmetryBalanceAnalysisReport(
            centerLine,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            1,
            0,
            Array.Empty<string>());
    }
}

public sealed record SymmetryBalanceMap(
    WpfPoint SymmetryCenterLine,
    IReadOnlyList<ShapeBalanceWarpRegion> SymmetryWarpRegions,
    ShapeBalanceWarpStrengthMap SymmetryStrengthMap,
    double IdentityPreserveStrength,
    double HardFeatureProtectionStrength,
    bool OvershootApplied,
    IReadOnlyList<ShapeBalanceDebugVector> DebugVectors)
{
    public static SymmetryBalanceMap Empty(int width, int height, WpfPoint centerLine)
    {
        return new SymmetryBalanceMap(
            centerLine,
            Array.Empty<ShapeBalanceWarpRegion>(),
            ShapeBalanceWarpStrengthMap.Empty(width, height),
            1,
            0,
            false,
            Array.Empty<ShapeBalanceDebugVector>());
    }
}
