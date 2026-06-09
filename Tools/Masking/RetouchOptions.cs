namespace PhotoRetouch;

public sealed record RetouchOptions(
    int RequestedStage,
    bool EnableSkinSmooth = true,
    bool EnableBlemishReduce = true,
    bool EnableWrinkleReduce = true,
    bool EnableToneEven = true,
    bool EnableTextureRestore = true,
    RetouchToolset? Toolset = null,
    WrinkleToolset? WrinkleToolset = null,
    TextureRestoreToolset? TextureRestoreToolset = null,
    bool ShowDebugOverlay = true,
    bool SaveDebugImages = true);
