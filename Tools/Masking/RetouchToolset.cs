namespace PhotoRetouch;

public sealed record RetouchToolset(
    int CurrentStage,
    SkinSmoothToolset SkinSmooth,
    BlemishToolset Blemish,
    WrinkleToolset Wrinkle,
    ToneEvenToolset ToneEven,
    TextureRestoreToolset TextureRestore,
    MaskDebugOptions DebugOptions,
    RetouchUserOverrideFlags UserOverrideFlags)
{
    public static RetouchToolset FromStagePreset(StagePreset preset)
    {
        return new RetouchToolset(
            preset.Stage,
            SkinSmoothToolset.FromStagePreset(preset),
            BlemishToolset.FromStagePreset(preset),
            WrinkleToolset.FromStagePreset(preset),
            ToneEvenToolset.FromStagePreset(preset),
            TextureRestoreToolset.FromStagePreset(preset),
            MaskDebugOptions.Default,
            RetouchUserOverrideFlags.None);
    }

    public StagePreset ApplyTo(StagePreset preset)
    {
        StagePreset updated = preset;
        if (UserOverrideFlags.SkinSmooth)
        {
            updated = updated with
            {
                SkinSmoothAmount = SkinSmooth.EnableSkinSmooth ? SkinSmooth.GlobalSmoothAmount : 0,
                DetailRestoreAmount = SkinSmooth.DetailPreserveAmount,
                SoftProtectOpacity = Math.Clamp(
                    updated.SoftProtectOpacity * (0.65 + SkinSmooth.SoftProtectSmoothAmount * 0.45),
                    0,
                    1)
            };
        }

        if (UserOverrideFlags.Blemish)
        {
            double spotBalance = (Blemish.SmallSpotAmount + Blemish.RedSpotAmount + Blemish.BrownSpotAmount + Blemish.PatchySpotAmount) / 4d;
            updated = updated with
            {
                BlemishReduceAmount = Blemish.EnableBlemishReduce
                    ? Math.Clamp(Blemish.GlobalBlemishAmount * (0.72 + spotBalance * 0.42), 0, 1)
                    : 0
            };
        }

        if (UserOverrideFlags.ToneEven)
        {
            double toneBalance = (ToneEven.RednessReduceAmount + ToneEven.YellowReduceAmount + ToneEven.DullnessReduceAmount + ToneEven.PatchyToneReduceAmount) / 4d;
            updated = updated with
            {
                ToneEvenAmount = ToneEven.EnableToneEven
                    ? Math.Clamp(ToneEven.GlobalToneEvenAmount * (0.70 + toneBalance * 0.45), 0, 1)
                    : 0
            };
        }

        if (UserOverrideFlags.TextureRestore)
        {
            updated = updated with
            {
                TextureRestoreAmount = TextureRestore.EnableTextureRestore ? TextureRestore.GlobalTextureAmount : 0,
                PoreTextureAmount = TextureRestore.PoreTextureAmount,
                FineDetailAmount = TextureRestore.FineDetailAmount,
                SkinGrainAmount = TextureRestore.SkinGrainAmount,
                SoftProtectTextureRestoreAmount = TextureRestore.SoftProtectTextureAmount,
                PlasticSkinGuardAmount = TextureRestore.PlasticSkinGuardEnabled
                    ? Math.Max(updated.PlasticSkinGuardAmount, 0.35)
                    : 0
            };
        }

        return updated;
    }
}

public sealed record SkinSmoothToolset(
    bool EnableSkinSmooth,
    double GlobalSmoothAmount,
    double DetailPreserveAmount,
    double SoftProtectSmoothAmount)
{
    public static SkinSmoothToolset FromStagePreset(StagePreset preset)
    {
        return new SkinSmoothToolset(
            true,
            preset.SkinSmoothAmount,
            preset.DetailRestoreAmount,
            Math.Clamp(preset.SoftProtectOpacity, 0, 1));
    }
}

public sealed record BlemishToolset(
    bool EnableBlemishReduce,
    double GlobalBlemishAmount,
    double SmallSpotAmount,
    double RedSpotAmount,
    double BrownSpotAmount,
    double PatchySpotAmount)
{
    public static BlemishToolset FromStagePreset(StagePreset preset)
    {
        return new BlemishToolset(
            true,
            preset.BlemishReduceAmount,
            preset.BlemishReduceAmount,
            preset.BlemishReduceAmount,
            preset.BlemishReduceAmount,
            preset.BlemishReduceAmount);
    }
}

public sealed record ToneEvenToolset(
    bool EnableToneEven,
    double GlobalToneEvenAmount,
    double RednessReduceAmount,
    double YellowReduceAmount,
    double DullnessReduceAmount,
    double PatchyToneReduceAmount)
{
    public static ToneEvenToolset FromStagePreset(StagePreset preset)
    {
        return new ToneEvenToolset(
            true,
            preset.ToneEvenAmount,
            preset.ToneEvenAmount,
            preset.ToneEvenAmount,
            preset.ToneEvenAmount,
            preset.ToneEvenAmount);
    }
}

public sealed record MaskDebugOptions(
    bool ShowDebugOverlay,
    bool SaveDebugImages,
    bool ShowSkinMask,
    bool ShowHardProtectMask,
    bool ShowSoftProtectMask,
    bool ShowRetouchAllowMask,
    bool ShowNostrilMask,
    bool ShowBlemishMask,
    bool ShowWrinkleMask,
    bool ShowToneMask,
    bool ShowTextureMask)
{
    public static MaskDebugOptions Default { get; } = new(
        true,
        true,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false,
        false);
}

public sealed record RetouchUserOverrideFlags(
    bool SkinSmooth,
    bool Blemish,
    bool Wrinkle,
    bool ToneEven,
    bool TextureRestore,
    bool DebugOptions)
{
    public static RetouchUserOverrideFlags None { get; } = new(false, false, false, false, false, false);
}

public sealed record AppliedRetouchOptions(
    int RequestedStage,
    int AppliedStage,
    StagePreset StagePreset,
    RetouchToolset RetouchToolset,
    MaskQualityReport MaskQualityReport,
    double SkinSmoothAmount,
    double BlemishReduceAmount,
    double WrinkleReduceAmount,
    double ToneEvenAmount,
    double TextureRestoreAmount,
    bool HardProtectEnabled,
    double SoftProtectOpacity,
    double RetouchAllowOpacity,
    MaskDebugOptions DebugOptions)
{
    public static AppliedRetouchOptions Create(MaskQualityReport qualityReport, RetouchOptions options)
    {
        int requestedStage = Math.Clamp(options.RequestedStage, 1, 10);
        int maxAllowedStage = Math.Clamp(qualityReport.MaxAllowedStage, 1, 10);
        int appliedStage = Math.Min(requestedStage, maxAllowedStage);
        StagePreset basePreset = StagePresetMapper.Map(appliedStage);
        RetouchToolset toolset = options.Toolset ?? RetouchToolset.FromStagePreset(basePreset);
        StagePreset userAdjustedPreset = toolset.ApplyTo(basePreset);
        return new AppliedRetouchOptions(
            requestedStage,
            appliedStage,
            userAdjustedPreset,
            toolset,
            qualityReport,
            userAdjustedPreset.SkinSmoothAmount,
            userAdjustedPreset.BlemishReduceAmount,
            userAdjustedPreset.WrinkleReduceAmount,
            userAdjustedPreset.ToneEvenAmount,
            userAdjustedPreset.TextureRestoreAmount,
            true,
            userAdjustedPreset.SoftProtectOpacity,
            userAdjustedPreset.RetouchAllowOpacity,
            toolset.DebugOptions);
    }
}
