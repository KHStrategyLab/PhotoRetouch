namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorWarpHandleGroup
{
    public string Name { get; init; } = string.Empty;

    public AnchorWarpHandleTarget Target { get; init; }

    public List<AnchorWarpHandle> Handles { get; } = new();

    public List<string> ControlPointNames { get; } = new();

    public List<string> FalloffPointNames { get; } = new();

    public List<string> LockedPointNames { get; } = new();

    public float InfluenceRadius { get; init; }

    public float SafeZoneRadius { get; init; }

    public string SolverHint { get; init; } = "MlsSimilarity";
}
