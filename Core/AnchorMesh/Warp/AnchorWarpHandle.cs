namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorWarpHandle
{
    public string Name { get; init; } = string.Empty;

    public AnchorWarpHandleKind Kind { get; init; }

    public List<string> AnchorPointNames { get; } = new();

    public float X { get; set; }

    public float Y { get; set; }

    public float SafeRadius { get; init; }

    public float MaxDragDistance { get; init; }

    public bool IsUserVisible { get; init; } = true;
}
