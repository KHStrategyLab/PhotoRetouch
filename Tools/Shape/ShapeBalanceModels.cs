using System.Windows;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record ShapeBalanceOptions(
    bool EnableShapeBalance,
    double GlobalShapeBalanceAmount,
    double HeadTiltCorrectAmount,
    double HeadTurnCorrectAmount,
    double HeadPitchCorrectAmount,
    double EyeLevelBalanceAmount,
    double EyebrowBalanceAmount,
    double MouthCornerBalanceAmount,
    double NoseCenterBalanceAmount,
    double ChinCenterBalanceAmount,
    double FaceContourBalanceAmount,
    bool NostrilObservationEnabled,
    double ExperimentalNostrilBalanceAmount,
    double MaxAllowedWarpStrength,
    double PreserveIdentityStrength,
    bool ProtectHardFeatures,
    bool DebugShapeOverlay,
    double ManualFaceBalanceShift,
    double ManualEyeLevelShift,
    double ManualEyebrowLevelShift,
    double ManualMouthCornerShift,
    double ManualNoseCenterShift,
    double ManualChinCenterShift)
{
    public static ShapeBalanceOptions Default { get; } = Natural();

    public static ShapeBalanceOptions Disabled { get; } = Natural() with { EnableShapeBalance = false };

    public string StableKey => string.Join(
        "|",
        EnableShapeBalance ? "1" : "0",
        GlobalShapeBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        HeadTiltCorrectAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        HeadTurnCorrectAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        HeadPitchCorrectAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        EyeLevelBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        EyebrowBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        MouthCornerBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        NoseCenterBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ChinCenterBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        FaceContourBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        NostrilObservationEnabled ? "1" : "0",
        ExperimentalNostrilBalanceAmount.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        MaxAllowedWarpStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        PreserveIdentityStrength.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ProtectHardFeatures ? "1" : "0",
        ManualFaceBalanceShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ManualEyeLevelShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ManualEyebrowLevelShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ManualMouthCornerShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ManualNoseCenterShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        ManualChinCenterShift.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture));

    public static ShapeBalanceOptions Natural()
    {
        return new ShapeBalanceOptions(
            true,
            0.45,
            0.55,
            0.25,
            0.18,
            0.45,
            0.20,
            0.20,
            0.34,
            0.34,
            0.10,
            true,
            0,
            0.34,
            0.85,
            true,
            false,
            0,
            0,
            0,
            0,
            0,
            0);
    }
}

public sealed record ShapeBalancePreset(
    string Id,
    string DisplayName,
    ShapeBalanceOptions Options)
{
    public static ShapeBalancePreset ShapeNatural { get; } = new("shape_natural", "Shape Natural", ShapeBalanceOptions.Natural());

    public static ShapeBalancePreset ShapePortrait { get; } = new(
        "shape_portrait",
        "Shape Portrait",
        ShapeBalanceOptions.Natural() with
        {
            GlobalShapeBalanceAmount = 0.58,
            HeadTiltCorrectAmount = 0.65,
            EyeLevelBalanceAmount = 0.52,
            NoseCenterBalanceAmount = 0.42,
            ChinCenterBalanceAmount = 0.42,
            MaxAllowedWarpStrength = 0.40
        });

    public static ShapeBalancePreset ShapeStrongTest { get; } = new(
        "shape_strong_test",
        "Shape Strong Test",
        ShapeBalanceOptions.Natural() with
        {
            GlobalShapeBalanceAmount = 0.78,
            HeadTiltCorrectAmount = 0.82,
            EyeLevelBalanceAmount = 0.70,
            NoseCenterBalanceAmount = 0.56,
            ChinCenterBalanceAmount = 0.56,
            MaxAllowedWarpStrength = 0.55
        });
}

public sealed record NostrilBalanceObservation(
    double LeftNostrilAreaEstimate,
    double RightNostrilAreaEstimate,
    double NostrilExposureDelta,
    bool IsNostrilBalanceReliable,
    double BeforeAfterNostrilShift = 0,
    bool IsNostrilWarpSafe = true);

public sealed record ShapeBalanceAnalysisReport(
    string ImageId,
    double FaceRollAngle,
    double FaceYawLikeBias,
    double FacePitchLikeBias,
    double EyeLevelDelta,
    double EyebrowLevelDelta,
    double MouthCornerDelta,
    double NoseLineTilt,
    double ChinCenterDelta,
    double LeftRightBalanceScore,
    double ShapeBalanceSuggestedStrength,
    NostrilBalanceObservation NostrilBalanceObservation,
    IReadOnlyList<string> DebugWarnings);

public sealed record ShapeBalanceReport(
    ShapeBalanceAnalysisReport AnalysisReport,
    bool ShapeBalanceApplied,
    double ShapeBalanceStrength,
    double BalancedMaskQualityScore,
    int MaxAllowedShapeStage,
    IReadOnlyList<string> DebugWarnings);

public sealed record BalancedMaskQualityReport(
    double BalancedMaskQualityScore,
    double ShapeBalanceSafetyScore,
    double WarpAlignmentScore,
    int MaxAllowedShapeStage,
    IReadOnlyList<string> DebugWarnings);

public sealed record BalancedImageBundle(
    BitmapSource SourceImage,
    BitmapSource BalancedImage,
    FaceSnapshotMaskSet SourceSnapshot,
    FaceSnapshotMaskSet BalancedSnapshot,
    IReadOnlyDictionary<string, WpfPoint> BalancedLandmarks,
    Int32Rect BalancedFaceBox,
    ShapeBalanceMap ShapeBalanceMap,
    ShapeBalanceReport ShapeBalanceReport,
    BalancedMaskQualityReport BalancedMaskQualityReport);
