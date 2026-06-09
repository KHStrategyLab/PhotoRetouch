namespace PhotoRetouch;

public sealed record ShapeBalanceToolset(
    bool EnableShapeBalance,
    int ShapeStage,
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
    bool ProtectHardFeatures,
    double PreserveIdentityStrength,
    double MaxAllowedWarpStrength,
    bool DebugShapeOverlay,
    SymmetryBalanceToolset SymmetryToolset,
    double ManualFaceBalanceShift,
    double ManualEyeLevelShift,
    double ManualEyebrowLevelShift,
    double ManualMouthCornerShift,
    double ManualNoseCenterShift,
    double ManualChinCenterShift,
    double ManualOvalFaceAmount,
    double ManualCheekboneSoftenAmount,
    double ManualChinWidthShift,
    double ManualChinLengthShift)
{
    public static ShapeBalanceToolset FromStagePreset(ShapeBalanceStagePreset preset)
    {
        return new ShapeBalanceToolset(
            true,
            preset.Stage,
            preset.GlobalShapeBalanceAmount,
            preset.HeadTiltCorrectAmount,
            preset.HeadTurnCorrectAmount,
            preset.HeadPitchCorrectAmount,
            preset.EyeLevelBalanceAmount,
            preset.EyebrowBalanceAmount,
            preset.MouthCornerBalanceAmount,
            preset.NoseCenterBalanceAmount,
            preset.ChinCenterBalanceAmount,
            preset.FaceContourBalanceAmount,
            true,
            preset.PreserveIdentityStrength,
            preset.MaxAllowedWarpStrength,
            false,
            SymmetryBalanceToolset.Default,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0,
            0);
    }
}

public sealed record ShapeBalanceStagePreset(
    int Stage,
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
    double PreserveIdentityStrength,
    double MaxAllowedWarpStrength);

public static class ShapeBalancePresetMapper
{
    private static readonly ShapeBalanceStagePreset[] Presets =
    {
        new(1, 0.18, 0.22, 0.08, 0.05, 0.16, 0.06, 0.06, 0.10, 0.10, 0.03, 0.96, 0.14),
        new(2, 0.26, 0.32, 0.12, 0.07, 0.24, 0.09, 0.09, 0.16, 0.16, 0.05, 0.94, 0.19),
        new(3, 0.34, 0.42, 0.16, 0.10, 0.32, 0.12, 0.12, 0.23, 0.23, 0.07, 0.92, 0.24),
        new(4, 0.40, 0.50, 0.20, 0.14, 0.40, 0.16, 0.16, 0.30, 0.30, 0.09, 0.88, 0.30),
        new(5, 0.45, 0.55, 0.25, 0.18, 0.45, 0.20, 0.20, 0.34, 0.34, 0.10, 0.85, 0.34),
        new(6, 0.50, 0.60, 0.29, 0.21, 0.50, 0.24, 0.24, 0.38, 0.38, 0.12, 0.82, 0.37),
        new(7, 0.55, 0.64, 0.33, 0.24, 0.55, 0.28, 0.28, 0.42, 0.42, 0.14, 0.79, 0.40),
        new(8, 0.60, 0.68, 0.37, 0.27, 0.60, 0.32, 0.32, 0.46, 0.46, 0.16, 0.76, 0.43),
        new(9, 0.65, 0.72, 0.41, 0.30, 0.65, 0.36, 0.36, 0.50, 0.50, 0.18, 0.73, 0.46),
        new(10, 0.70, 0.76, 0.45, 0.33, 0.70, 0.40, 0.40, 0.54, 0.54, 0.20, 0.70, 0.50)
    };

    public static ShapeBalanceStagePreset Map(int requestedStage)
    {
        int stage = Math.Clamp(requestedStage, 1, 10);
        return Presets[stage - 1];
    }
}

public sealed record AppliedShapeBalanceOptions(
    int RequestedShapeStage,
    int AppliedShapeStage,
    ShapeBalanceStagePreset StagePreset,
    ShapeBalanceToolset Toolset,
    double MaskQualitySafetyFactor,
    ShapeBalanceOptions Options)
{
    public static AppliedShapeBalanceOptions Create(ShapeBalanceToolset toolset, MaskQualityReport? qualityReport)
    {
        int requestedStage = Math.Clamp(toolset.ShapeStage, 1, 10);
        int maxAllowedStage = Math.Clamp(qualityReport?.MaxAllowedStage ?? 10, 1, 10);
        int appliedStage = Math.Min(requestedStage, maxAllowedStage);
        ShapeBalanceStagePreset preset = ShapeBalancePresetMapper.Map(appliedStage);
        double safetyFactor = CalculateSafetyFactor(qualityReport);

        ShapeBalanceOptions options = ShapeBalanceOptions.Natural() with
        {
            EnableShapeBalance = toolset.EnableShapeBalance,
            GlobalShapeBalanceAmount = Clamp01(toolset.GlobalShapeBalanceAmount * safetyFactor),
            HeadTiltCorrectAmount = Clamp01(toolset.HeadTiltCorrectAmount * safetyFactor),
            HeadTurnCorrectAmount = Clamp01(toolset.HeadTurnCorrectAmount * safetyFactor),
            HeadPitchCorrectAmount = Clamp01(toolset.HeadPitchCorrectAmount * safetyFactor),
            EyeLevelBalanceAmount = Clamp01(toolset.EyeLevelBalanceAmount * safetyFactor),
            EyebrowBalanceAmount = Clamp01(toolset.EyebrowBalanceAmount * safetyFactor),
            MouthCornerBalanceAmount = Clamp01(toolset.MouthCornerBalanceAmount * safetyFactor),
            NoseCenterBalanceAmount = Clamp01(toolset.NoseCenterBalanceAmount * safetyFactor),
            ChinCenterBalanceAmount = Clamp01(toolset.ChinCenterBalanceAmount * safetyFactor),
            FaceContourBalanceAmount = Clamp01(toolset.FaceContourBalanceAmount * safetyFactor),
            ProtectHardFeatures = toolset.ProtectHardFeatures,
            PreserveIdentityStrength = Clamp01(Math.Max(toolset.PreserveIdentityStrength, preset.PreserveIdentityStrength)),
            MaxAllowedWarpStrength = Clamp01(Math.Min(toolset.MaxAllowedWarpStrength, 0.50) * safetyFactor),
            DebugShapeOverlay = toolset.DebugShapeOverlay,
            SymmetryToolset = toolset.SymmetryToolset,
            ManualFaceBalanceShift = ClampSigned(toolset.ManualFaceBalanceShift),
            ManualEyeLevelShift = ClampSigned(toolset.ManualEyeLevelShift),
            ManualEyebrowLevelShift = ClampSigned(toolset.ManualEyebrowLevelShift),
            ManualMouthCornerShift = ClampSigned(toolset.ManualMouthCornerShift),
            ManualNoseCenterShift = ClampSigned(toolset.ManualNoseCenterShift),
            ManualChinCenterShift = ClampSigned(toolset.ManualChinCenterShift),
            ManualOvalFaceAmount = Clamp01(toolset.ManualOvalFaceAmount),
            ManualCheekboneSoftenAmount = Clamp01(toolset.ManualCheekboneSoftenAmount),
            ManualChinWidthShift = ClampSigned(toolset.ManualChinWidthShift),
            ManualChinLengthShift = ClampSigned(toolset.ManualChinLengthShift),
            NostrilObservationEnabled = true,
            ExperimentalNostrilBalanceAmount = 0
        };

        return new AppliedShapeBalanceOptions(
            requestedStage,
            appliedStage,
            preset,
            toolset,
            safetyFactor,
            options);
    }

    private static double CalculateSafetyFactor(MaskQualityReport? qualityReport)
    {
        if (qualityReport is null)
        {
            return 1;
        }

        double quality = Math.Min(
            qualityReport.LandmarkQualityScore,
            Math.Min(
                qualityReport.HardProtectQualityScore,
                Math.Min(qualityReport.EyeMaskQualityScore, qualityReport.LipMaskQualityScore)));

        if (!qualityReport.IsUsable || qualityReport.HasFatalError)
        {
            return 0.35;
        }

        return Math.Clamp(0.55 + quality * 0.45, 0.45, 1);
    }

    private static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }

    private static double ClampSigned(double value)
    {
        return Math.Clamp(value, -1, 1);
    }
}
