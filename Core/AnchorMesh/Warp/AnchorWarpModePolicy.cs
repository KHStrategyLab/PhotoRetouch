namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorWarpModePolicy
{
    public AnchorWarpInteractionMode Mode { get; init; }

    public bool ShowSimpleHandles { get; init; }

    public bool ShowAdvancedHandles { get; init; }

    public bool ShowFalloffControls { get; init; }

    public bool ShowDebugGuides { get; init; }

    public bool AllowAutoProposal { get; init; }

    public bool AllowDirectAutoApply { get; init; }

    public float DefaultHandleOpacity { get; init; } = 1.0f;

    public float MaxDragScale { get; init; } = 1.0f;
}
