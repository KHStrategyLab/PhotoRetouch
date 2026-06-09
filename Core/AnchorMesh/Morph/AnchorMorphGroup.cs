namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMorphGroup
{
    public string Name { get; init; } = string.Empty;

    public List<string> ControlPoints { get; } = new();

    public List<string> FalloffPoints { get; } = new();

    public List<string> LockedPoints { get; } = new();

    public float InfluenceRadius { get; init; }

    public float Strength { get; set; }

    public string Direction { get; init; } = string.Empty;

    public string AllowedOperation { get; init; } = string.Empty;
}
