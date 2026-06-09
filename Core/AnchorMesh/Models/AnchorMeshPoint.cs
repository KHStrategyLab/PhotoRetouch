namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshPoint
{
    public string Name { get; init; } = string.Empty;

    public string FeatureName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public int Index { get; init; }

    public float TemplateX { get; init; }

    public float TemplateY { get; init; }

    public float TemplateZ { get; init; }

    public float ImageX { get; set; }

    public float ImageY { get; set; }

    public float SnappedX { get; set; }

    public float SnappedY { get; set; }

    public float LocalX { get; set; }

    public float LocalY { get; set; }

    public float Confidence { get; set; } = 0.35f;

    public float SnapWeight { get; set; }

    public bool IsAnchor { get; init; }

    public bool IsLocked { get; set; }

    public string Source { get; set; } = "Template";

    public AnchorMeshPoint Clone()
    {
        return new AnchorMeshPoint
        {
            Name = Name,
            FeatureName = FeatureName,
            Role = Role,
            Index = Index,
            TemplateX = TemplateX,
            TemplateY = TemplateY,
            TemplateZ = TemplateZ,
            ImageX = ImageX,
            ImageY = ImageY,
            SnappedX = SnappedX,
            SnappedY = SnappedY,
            LocalX = LocalX,
            LocalY = LocalY,
            Confidence = Confidence,
            SnapWeight = SnapWeight,
            IsAnchor = IsAnchor,
            IsLocked = IsLocked,
            Source = Source
        };
    }
}
