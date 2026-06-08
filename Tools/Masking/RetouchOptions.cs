namespace PhotoRetouch;

public sealed record RetouchOptions(
    int RequestedStage,
    bool EnableSkinSmooth = true,
    bool EnableToneEven = false,
    bool EnableTextureRestore = true,
    WrinkleToolset? WrinkleToolset = null,
    TextureRestoreToolset? TextureRestoreToolset = null,
    bool ShowDebugOverlay = true,
    bool SaveDebugImages = true);
