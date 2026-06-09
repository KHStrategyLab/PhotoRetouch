namespace PhotoRetouch;

public sealed record FaceSnapshotMaskSet(
    string ImageId,
    SnapshotMaskCacheKey CacheKey,
    string SourcePath,
    DateTime SourceLastWriteTimeUtc,
    long SourceLength,
    FaceAnalysisResult Analysis,
    FaceMaskSet Masks,
    MaskQualityReport QualityReport,
    DateTime CreatedAtUtc,
    ParsingMaskSet? ParsingMasks = null,
    FaceMaskSet? WarpedStandardMasks = null,
    NostrilDetectorResult? NostrilDetection = null,
    FaceFeatureMeshSet? FeatureMeshes = null)
{
    public int MaxAllowedStage => QualityReport.MaxAllowedStage;

    public bool HasMaskWarning => QualityReport.HasWarning;

    public bool HasFatalMaskError => QualityReport.HasFatalError;

    public bool MatchesSource(string path, DateTime lastWriteTimeUtc, long sourceLength, SnapshotMaskCacheKey cacheKey)
    {
        return string.Equals(SourcePath, path, StringComparison.OrdinalIgnoreCase) &&
               SourceLastWriteTimeUtc == lastWriteTimeUtc &&
               SourceLength == sourceLength &&
               CacheKey == cacheKey;
    }
}
