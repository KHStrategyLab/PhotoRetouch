namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorWarpToolSession
{
    public AnchorWarpToolFamily ToolFamily { get; set; } = AnchorWarpToolFamily.Liquify;

    public AnchorWarpInteractionMode InteractionMode { get; set; } = AnchorWarpInteractionMode.EasyLiquify;

    public LiquifyBrushMode LiquifyMode { get; set; } = LiquifyBrushMode.Push;

    public MeshToolEditState MeshState { get; set; } = MeshToolEditState.Idle;

    public string? SelectedHandleGroupName { get; set; }

    public string? SelectedHandleName { get; set; }

    public float BrushRadius { get; set; } = 36.0f;

    public float BrushStrength { get; set; } = 0.35f;

    public bool IsProtectMaskVisible { get; set; }

    public bool HasPendingMeshEdit { get; set; }

    public LiquifyToolProfile LiquifyProfile { get; set; } = new();

    public AnchorWarpModePolicy CurrentPolicy => AnchorWarpModePolicyMapper.GetPolicy(InteractionMode);
}
