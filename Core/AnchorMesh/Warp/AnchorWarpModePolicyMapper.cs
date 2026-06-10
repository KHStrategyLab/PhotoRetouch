namespace PhotoRetouch.AnchorMesh;

public static class AnchorWarpModePolicyMapper
{
    public static AnchorWarpModePolicy GetPolicy(AnchorWarpInteractionMode mode)
    {
        return mode switch
        {
            AnchorWarpInteractionMode.AdvancedLiquify => new AnchorWarpModePolicy
            {
                Mode = mode,
                ShowSimpleHandles = true,
                ShowAdvancedHandles = true,
                ShowFalloffControls = true,
                ShowDebugGuides = true,
                AllowAutoProposal = false,
                AllowDirectAutoApply = false,
                DefaultHandleOpacity = 0.92f,
                MaxDragScale = 1.0f
            },
            AnchorWarpInteractionMode.AutoAssist => new AnchorWarpModePolicy
            {
                Mode = mode,
                ShowSimpleHandles = true,
                ShowAdvancedHandles = false,
                ShowFalloffControls = false,
                ShowDebugGuides = true,
                AllowAutoProposal = true,
                AllowDirectAutoApply = false,
                DefaultHandleOpacity = 0.86f,
                MaxDragScale = 0.55f
            },
            AnchorWarpInteractionMode.FullAuto => new AnchorWarpModePolicy
            {
                Mode = mode,
                ShowSimpleHandles = false,
                ShowAdvancedHandles = false,
                ShowFalloffControls = false,
                ShowDebugGuides = true,
                AllowAutoProposal = true,
                AllowDirectAutoApply = true,
                DefaultHandleOpacity = 0.72f,
                MaxDragScale = 0.35f
            },
            _ => new AnchorWarpModePolicy
            {
                Mode = AnchorWarpInteractionMode.EasyLiquify,
                ShowSimpleHandles = true,
                ShowAdvancedHandles = false,
                ShowFalloffControls = false,
                ShowDebugGuides = false,
                AllowAutoProposal = false,
                AllowDirectAutoApply = false,
                DefaultHandleOpacity = 1.0f,
                MaxDragScale = 0.65f
            }
        };
    }
}
