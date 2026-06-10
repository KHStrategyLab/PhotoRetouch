namespace PhotoRetouch.AnchorMesh;

public sealed class FaceFitBox
{
    public float CenterX { get; set; }

    public float CenterY { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public float RotationRad { get; set; }

    public float Confidence { get; set; }

    public string Source { get; set; } = "YuNet";
}
