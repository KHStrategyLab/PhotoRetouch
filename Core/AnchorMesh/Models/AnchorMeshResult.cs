namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshResult
{
    public YuNetAnchorSet? YuNetAnchors { get; set; }

    public AnchorMeshFeatureSet TemplateFeatures { get; set; } = new();

    public AnchorMeshFeatureSet YuNetAlignedFeatures { get; set; } = new();

    public AnchorMeshFeatureSet Features { get; set; } = new();

    public AnchorFaceMeasurements? Measurements { get; set; }

    public AnchorOvalProfileMetrics? OvalProfile { get; set; }

    public FaceFitBox? FitBox { get; set; }

    public AnchorPoseInfo? Pose { get; set; }

    public List<AnchorMeshEdge> TopologyEdges { get; } = new();

    public AnchorMorphGroupSet MorphGroups { get; set; } = new();

    public AnchorWarpHandleGroupSet WarpHandleGroups { get; set; } = new();

    public bool IsValid { get; set; }

    public float Confidence { get; set; }

    public string Stage { get; set; } = string.Empty;

    public List<string> Warnings { get; } = new();
}
