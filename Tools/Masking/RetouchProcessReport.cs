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
    IReadOnlyList<string> DebugWarnings)
{
    public bool IsStageLimited => AppliedStage < RequestedStage;
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
    int AppliedStage,
    RetouchProcessReport Report,
    IReadOnlyList<string> DebugWarnings);
