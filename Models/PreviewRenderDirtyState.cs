namespace PhotoRetouch;

public sealed record PreviewRenderDirtyState(
    bool MaskDirty,
    bool ShapeDirty,
    bool SkinDirty,
    bool PreviewDirty,
    bool ExportDirty,
    string Reason)
{
    public static PreviewRenderDirtyState Clean { get; } = new(false, false, false, false, false, "clean");

    public PreviewRenderDirtyState MarkMaskDirty(string reason)
    {
        return this with
        {
            MaskDirty = true,
            ShapeDirty = true,
            SkinDirty = true,
            PreviewDirty = true,
            ExportDirty = true,
            Reason = reason
        };
    }

    public PreviewRenderDirtyState MarkShapeDirty(string reason)
    {
        return this with
        {
            ShapeDirty = true,
            SkinDirty = true,
            PreviewDirty = true,
            ExportDirty = true,
            Reason = reason
        };
    }

    public PreviewRenderDirtyState MarkSkinDirty(string reason)
    {
        return this with
        {
            SkinDirty = true,
            PreviewDirty = true,
            ExportDirty = true,
            Reason = reason
        };
    }

    public PreviewRenderDirtyState MarkPreviewRendered(string reason)
    {
        return this with
        {
            MaskDirty = false,
            ShapeDirty = false,
            SkinDirty = false,
            PreviewDirty = false,
            ExportDirty = true,
            Reason = reason
        };
    }

    public PreviewRenderDirtyState MarkExportClean(string reason)
    {
        return this with
        {
            ExportDirty = false,
            Reason = reason
        };
    }
}
