namespace PhotoRetouch;

public sealed record DebugFaceMesh(
    IReadOnlyList<DebugMeshPoint> Points,
    IReadOnlyList<DebugMeshEdge> Edges);
