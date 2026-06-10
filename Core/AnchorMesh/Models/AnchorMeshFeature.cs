using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshFeature
{
    public string Name { get; init; } = string.Empty;

    public List<AnchorMeshPoint> Points { get; } = new();

    public bool IsClosedLoop { get; init; }

    public bool IsValid { get; set; }

    public RectangleF Bounds { get; set; }

    public float CenterX { get; set; }

    public float CenterY { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public float AngleRad { get; set; }

    public float Confidence { get; set; }

    public string SourceMaskName { get; set; } = string.Empty;

    public string SnapMode { get; set; } = "None";

    public AnchorMeshFeature Clone()
    {
        AnchorMeshFeature clone = new()
        {
            Name = Name,
            IsClosedLoop = IsClosedLoop,
            IsValid = IsValid,
            Bounds = Bounds,
            CenterX = CenterX,
            CenterY = CenterY,
            Width = Width,
            Height = Height,
            AngleRad = AngleRad,
            Confidence = Confidence,
            SourceMaskName = SourceMaskName,
            SnapMode = SnapMode
        };
        clone.Points.AddRange(Points.Select(point => point.Clone()));
        return clone;
    }
}
