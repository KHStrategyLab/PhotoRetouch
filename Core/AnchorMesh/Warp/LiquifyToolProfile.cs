namespace PhotoRetouch.AnchorMesh;

public sealed class LiquifyToolProfile
{
    public string Name { get; init; } = "Photoshop CS3 Style";

    public bool IsBrushBased { get; init; } = true;

    public bool IsAutomaticFaceBeautify { get; init; }

    public float DefaultBrushRadius { get; init; } = 36.0f;

    public float DefaultBrushStrength { get; init; } = 0.35f;

    public float MaxBrushStrength { get; init; } = 0.85f;

    public float DefaultPreviewQuality { get; init; } = 0.65f;

    public bool SupportsPush { get; init; } = true;

    public bool SupportsBloat { get; init; } = true;

    public bool SupportsPinch { get; init; } = true;

    public bool SupportsRestore { get; init; } = true;

    public bool SupportsProtect { get; init; } = true;
}
