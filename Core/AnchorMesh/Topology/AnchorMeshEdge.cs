namespace PhotoRetouch.AnchorMesh;

public sealed record AnchorMeshEdge(
    string From,
    string To,
    AnchorMeshEdgeKind Kind,
    AnchorMeshEdgeGroup Group,
    float Weight = 1.0f,
    bool IsDebugVisible = true);
