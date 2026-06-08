namespace PhotoRetouch;

public sealed record RetouchBindingReport(
    string EventName,
    string? ChangedControlId,
    double? ChangedValue,
    int RequestedStage,
    int AppliedStage,
    bool SnapshotMaskReused,
    bool AnalysisReexecuted,
    bool RetouchExecuted,
    bool IsStrongRetouchLimited)
{
    public string ToStatusText()
    {
        string source = ChangedControlId is null
            ? EventName
            : EventName + ":" + ChangedControlId;
        string cacheText = SnapshotMaskReused ? "cache" : AnalysisReexecuted ? "rebuild" : "no-mask";
        string limitedText = IsStrongRetouchLimited ? " limited" : string.Empty;
        return $"{source} {cacheText} req {RequestedStage} / app {AppliedStage}{limitedText}";
    }

    public static RetouchBindingReport Empty { get; } = new(
        "Ready",
        null,
        null,
        1,
        1,
        false,
        false,
        false,
        false);
}
