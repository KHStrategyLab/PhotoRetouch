namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshResult
{
    public YuNetAnchorSet? YuNetAnchors { get; set; }

    public AnchorMeshFeatureSet TemplateFeatures { get; set; } = new();

    public AnchorMeshFeatureSet YuNetAlignedFeatures { get; set; } = new();

    public AnchorMeshFeatureSet Features { get; set; } = new();

    public bool IsValid { get; set; }

    public float Confidence { get; set; }

    public string Stage { get; set; } = string.Empty;

    public List<string> Warnings { get; } = new();
}
