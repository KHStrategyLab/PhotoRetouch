namespace PhotoRetouch;

public sealed record StagePreset(
    int Stage,
    double SkinSmoothAmount,
    double BlemishReduceAmount,
    double BlemishMaxArea,
    double BlemishMinContrast,
    double BlemishFeatherRadius,
    double BlemishSearchSoftProtectOpacity,
    double WrinkleReduceAmount,
    double WrinkleMaxWidth,
    double WrinkleMinLength,
    double WrinkleContrastThreshold,
    double WrinkleFeatherRadius,
    double WrinkleSoftProtectOpacity,
    double WrinkleStructureKeepAmount,
    double UnderEyeWrinkleDefault,
    double GlabellaWrinkleDefault,
    double ForeheadWrinkleDefault,
    double NasolabialFoldDefault,
    double MouthCornerWrinkleDefault,
    double NeckWrinkleDefault,
    double NoseShadowWrinkleDefault,
    double ToneEvenAmount,
    double TextureRestoreAmount,
    double PoreTextureAmount,
    double FineDetailAmount,
    double SkinGrainAmount,
    double DetailLayerBlurRadius,
    double DetailSharpnessLimit,
    double PlasticSkinGuardAmount,
    double SoftProtectTextureRestoreAmount,
    double SoftProtectOpacity,
    double RetouchAllowOpacity,
    double DetailRestoreAmount);

public static class StagePresetMapper
{
    private static readonly StagePreset[] Presets =
    {
        new(1, 0.08, 0.04, 18, 24, 1.0, 0.00, 0.03, 3.2, 7, 18, 1.0, 0.10, 0.92, 0.36, 0.42, 0.50, 0.32, 0.28, 0.34, 0.24, 0.04, 0.96, 0.92, 0.96, 0.70, 2.0, 0.28, 0.12, 0.55, 0.12, 0.18, 0.92),
        new(2, 0.14, 0.08, 24, 22, 1.0, 0.02, 0.06, 3.4, 7, 17, 1.0, 0.12, 0.88, 0.38, 0.46, 0.54, 0.34, 0.30, 0.36, 0.25, 0.08, 0.90, 0.86, 0.90, 0.66, 2.0, 0.30, 0.14, 0.50, 0.16, 0.28, 0.86),
        new(3, 0.22, 0.13, 34, 20, 1.2, 0.04, 0.10, 3.6, 8, 16, 1.1, 0.14, 0.84, 0.40, 0.50, 0.58, 0.36, 0.32, 0.39, 0.27, 0.13, 0.84, 0.80, 0.84, 0.62, 2.2, 0.32, 0.18, 0.46, 0.20, 0.38, 0.78),
        new(4, 0.32, 0.20, 48, 18, 1.3, 0.06, 0.16, 3.8, 8, 15, 1.2, 0.17, 0.80, 0.44, 0.54, 0.62, 0.39, 0.34, 0.43, 0.30, 0.20, 0.76, 0.72, 0.76, 0.56, 2.4, 0.34, 0.22, 0.42, 0.25, 0.50, 0.68),
        new(5, 0.43, 0.28, 66, 16, 1.5, 0.08, 0.23, 4.0, 9, 14, 1.3, 0.20, 0.76, 0.48, 0.58, 0.66, 0.42, 0.37, 0.47, 0.33, 0.28, 0.68, 0.66, 0.70, 0.50, 2.6, 0.36, 0.28, 0.38, 0.30, 0.62, 0.58),
        new(6, 0.53, 0.36, 82, 15, 1.6, 0.10, 0.30, 4.4, 10, 13, 1.4, 0.23, 0.72, 0.52, 0.62, 0.70, 0.46, 0.40, 0.51, 0.36, 0.36, 0.60, 0.58, 0.64, 0.46, 2.8, 0.38, 0.34, 0.34, 0.34, 0.70, 0.50),
        new(7, 0.63, 0.45, 104, 14, 1.8, 0.12, 0.38, 4.8, 10, 12, 1.5, 0.26, 0.68, 0.56, 0.66, 0.74, 0.50, 0.44, 0.55, 0.39, 0.44, 0.52, 0.50, 0.58, 0.42, 3.0, 0.40, 0.42, 0.31, 0.38, 0.78, 0.42),
        new(8, 0.72, 0.53, 126, 13, 2.0, 0.14, 0.46, 5.2, 11, 11, 1.7, 0.29, 0.64, 0.60, 0.70, 0.78, 0.54, 0.48, 0.59, 0.42, 0.52, 0.45, 0.44, 0.52, 0.38, 3.2, 0.42, 0.50, 0.28, 0.42, 0.85, 0.35),
        new(9, 0.80, 0.60, 148, 12, 2.1, 0.16, 0.54, 5.6, 12, 10, 1.8, 0.32, 0.60, 0.64, 0.74, 0.82, 0.58, 0.52, 0.63, 0.45, 0.60, 0.38, 0.38, 0.46, 0.34, 3.4, 0.44, 0.58, 0.25, 0.46, 0.91, 0.30),
        new(10, 0.88, 0.68, 174, 11, 2.2, 0.18, 0.62, 6.0, 12, 9, 2.0, 0.35, 0.56, 0.68, 0.78, 0.86, 0.62, 0.56, 0.67, 0.48, 0.66, 0.34, 0.34, 0.42, 0.32, 3.6, 0.46, 0.66, 0.22, 0.50, 0.96, 0.25)
    };

    public static StagePreset Map(int stage)
    {
        int clampedStage = Math.Clamp(stage, 1, 10);
        return ApplyOrder19Tuning(Presets[clampedStage - 1]);
    }

    public static IReadOnlyList<StagePreset> GetAll()
    {
        return Enumerable.Range(1, 10)
            .Select(Map)
            .ToArray();
    }

    private static StagePreset ApplyOrder19Tuning(StagePreset preset)
    {
        return preset.Stage switch
        {
            1 => preset with
            {
                SkinSmoothAmount = 0.06,
                BlemishReduceAmount = 0.03,
                WrinkleReduceAmount = 0.02,
                ToneEvenAmount = 0.03,
                TextureRestoreAmount = 0.98,
                PlasticSkinGuardAmount = 0.34,
                SoftProtectOpacity = 0.10,
                RetouchAllowOpacity = 0.12,
                DetailRestoreAmount = 0.96
            },
            2 => preset with
            {
                SkinSmoothAmount = 0.12,
                BlemishReduceAmount = 0.07,
                WrinkleReduceAmount = 0.05,
                ToneEvenAmount = 0.07,
                TextureRestoreAmount = 0.92,
                PlasticSkinGuardAmount = 0.36,
                RetouchAllowOpacity = 0.24,
                DetailRestoreAmount = 0.88
            },
            3 => preset with
            {
                SkinSmoothAmount = 0.20,
                BlemishReduceAmount = 0.13,
                WrinkleReduceAmount = 0.09,
                ToneEvenAmount = 0.12,
                TextureRestoreAmount = 0.86,
                PlasticSkinGuardAmount = 0.40,
                RetouchAllowOpacity = 0.36,
                DetailRestoreAmount = 0.80
            },
            4 => preset with
            {
                SkinSmoothAmount = 0.30,
                BlemishReduceAmount = 0.21,
                WrinkleReduceAmount = 0.15,
                ToneEvenAmount = 0.20,
                TextureRestoreAmount = 0.78,
                PlasticSkinGuardAmount = 0.45,
                RetouchAllowOpacity = 0.48,
                DetailRestoreAmount = 0.70
            },
            5 => preset with
            {
                SkinSmoothAmount = 0.40,
                BlemishReduceAmount = 0.30,
                WrinkleReduceAmount = 0.21,
                ToneEvenAmount = 0.30,
                TextureRestoreAmount = 0.72,
                PlasticSkinGuardAmount = 0.50,
                RetouchAllowOpacity = 0.60,
                DetailRestoreAmount = 0.62
            },
            6 => preset with
            {
                SkinSmoothAmount = 0.50,
                BlemishReduceAmount = 0.39,
                WrinkleReduceAmount = 0.28,
                ToneEvenAmount = 0.39,
                TextureRestoreAmount = 0.66,
                PlasticSkinGuardAmount = 0.56,
                RetouchAllowOpacity = 0.68,
                DetailRestoreAmount = 0.54
            },
            7 => preset with
            {
                SkinSmoothAmount = 0.60,
                BlemishReduceAmount = 0.48,
                WrinkleReduceAmount = 0.36,
                ToneEvenAmount = 0.48,
                TextureRestoreAmount = 0.60,
                PlasticSkinGuardAmount = 0.62,
                RetouchAllowOpacity = 0.76,
                DetailRestoreAmount = 0.47
            },
            8 => preset with
            {
                SkinSmoothAmount = 0.68,
                BlemishReduceAmount = 0.56,
                WrinkleReduceAmount = 0.44,
                ToneEvenAmount = 0.56,
                TextureRestoreAmount = 0.54,
                PlasticSkinGuardAmount = 0.68,
                RetouchAllowOpacity = 0.84,
                DetailRestoreAmount = 0.41
            },
            9 => preset with
            {
                SkinSmoothAmount = 0.75,
                BlemishReduceAmount = 0.64,
                WrinkleReduceAmount = 0.52,
                ToneEvenAmount = 0.62,
                TextureRestoreAmount = 0.50,
                PoreTextureAmount = 0.42,
                FineDetailAmount = 0.46,
                SkinGrainAmount = 0.38,
                PlasticSkinGuardAmount = 0.74,
                RetouchAllowOpacity = 0.90,
                DetailRestoreAmount = 0.38
            },
            10 => preset with
            {
                SkinSmoothAmount = 0.82,
                BlemishReduceAmount = 0.70,
                WrinkleReduceAmount = 0.58,
                ToneEvenAmount = 0.66,
                TextureRestoreAmount = 0.48,
                PoreTextureAmount = 0.48,
                FineDetailAmount = 0.52,
                SkinGrainAmount = 0.44,
                PlasticSkinGuardAmount = 0.80,
                SoftProtectOpacity = 0.46,
                RetouchAllowOpacity = 0.94,
                DetailRestoreAmount = 0.36
            },
            _ => preset
        };
    }
}
