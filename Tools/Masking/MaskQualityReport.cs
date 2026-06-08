namespace PhotoRetouch;

public sealed record MaskQualityReport(
    double Score,
    double FaceQualityScore,
    double LandmarkQualityScore,
    double ParsingQualityScore,
    double SkinMaskQualityScore,
    double EyeMaskQualityScore,
    double EyebrowMaskQualityScore,
    double LipMaskQualityScore,
    double NostrilMaskQualityScore,
    double HairMaskQualityScore,
    double HardProtectQualityScore,
    double RetouchAllowQualityScore,
    bool IsUsable,
    int MaxAllowedStage,
    bool IsSafeForStrongRetouch,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<string> FatalErrors)
{
    public double OverallQualityScore => Score;

    public IReadOnlyList<string> DebugWarnings => Warnings;

    public bool HasWarning => Warnings.Count > 0;

    public bool HasFatalError => FatalErrors.Count > 0;

    public static MaskQualityReport FromMasks(FaceAnalysisResult analysis, FaceMaskSet masks)
    {
        return MaskQualityValidator.Validate(analysis, masks);
    }
}
