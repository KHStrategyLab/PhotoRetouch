namespace PhotoRetouch;

public sealed record RetouchProcessReport(
    int RequestedStage,
    int AppliedStage,
    int MaxAllowedStage,
    double SkinSmoothAmount,
    double BlemishReduceAmount,
    double WrinkleReduceAmount,
    double ToneEvenAmount,
    double TextureRestoreAmount,
    double DetailPreserveAmount,
    bool HardProtectApplied,
    double SoftProtectOpacity,
    double RetouchAllowOpacity,
    double MaskQualityScore,
    int BlemishCandidateCount,
    int BlemishAppliedCount,
    double BlemishAverageCorrectionStrength,
    int WrinkleAppliedCount,
    double WrinkleAverageCorrectionStrength,
    double TextureRetouchAllowAmount,
    double TextureSoftProtectAmount,
    double PlasticSkinRiskScore,
    int HardProtectChangedBeforeRestoreCount,
    int HardProtectChangedAfterRestoreCount,
    bool IsHardProtectClean,
    IReadOnlyList<string> DebugWarnings)
{
    public bool IsStageLimited => AppliedStage < RequestedStage;
}

public sealed record PipelineDebugReport(
    string ImageId,
    string SnapshotMaskCacheKey,
    int RequestedStage,
    int AppliedStage,
    DateTime PipelineStartedAtUtc,
    DateTime PipelineFinishedAtUtc,
    bool AnalysisExecuted,
    bool SnapshotMaskReused,
    bool QualityReportReused,
    IReadOnlyList<string> FiltersExecuted,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> Errors)
{
    public double DurationMilliseconds => (PipelineFinishedAtUtc - PipelineStartedAtUtc).TotalMilliseconds;
}

public sealed record RetouchStageProcessorOutput(
    System.Windows.Media.Imaging.BitmapSource FinalImage,
    System.Windows.Media.Imaging.BitmapSource SmoothBaseImage,
    System.Windows.Media.Imaging.BitmapSource DetailLayerImage,
    System.Windows.Media.Imaging.BitmapSource TextureRestoredImage,
    System.Windows.Media.Imaging.BitmapSource RetouchAllowAppliedImage,
    System.Windows.Media.Imaging.BitmapSource SoftProtectAppliedImage,
    System.Windows.Media.Imaging.BitmapSource HardProtectRestoredImage,
    System.Windows.Media.Imaging.BitmapSource BlemishReducedImage,
    MaskPlane BlemishCandidateMask,
    MaskPlane BlemishMask,
    BlemishProcessReport BlemishReport,
    System.Windows.Media.Imaging.BitmapSource WrinkleReducedImage,
    WrinkleMaskSet WrinkleMaskSet,
    MaskPlane WrinkleCandidateMask,
    MaskPlane WrinkleAppliedMask,
    WrinkleProcessReport WrinkleReport,
    System.Windows.Media.Imaging.BitmapSource ToneEvenImage,
    System.Windows.Media.Imaging.BitmapSource FinalTextureRestoredImage,
    System.Windows.Media.Imaging.BitmapSource FinalTextureBlurOriginalImage,
    System.Windows.Media.Imaging.BitmapSource FinalTextureDetailLayerImage,
    MaskPlane TextureRestoreMask,
    MaskPlane TextureRestoreStrengthMap,
    MaskPlane PlasticSkinRiskMap,
    TextureRestoreProcessReport TextureRestoreReport,
    System.Windows.Media.Imaging.BitmapSource HardProtectFinalImage,
    MaskPlane HardProtectBeforeRestoreDiffMask,
    MaskPlane HardProtectAfterRestoreDiffMask,
    HardProtectRestoreReport HardProtectRestoreReport,
    int AppliedStage,
    RetouchProcessReport Report,
    PipelineDebugReport PipelineReport,
    IReadOnlyList<string> DebugWarnings);
