namespace PhotoRetouch;

public sealed record StandardMaskSet(
    MaskPlane SkinMask,
    MaskPlane EyeProtectMask,
    MaskPlane EyebrowProtectMask,
    MaskPlane LipProtectMask,
    MaskPlane NoseMask,
    MaskPlane NostrilProtectMask,
    MaskPlane SoftProtectMask,
    string Version,
    IReadOnlyList<string> DebugWarnings);

public sealed record WarpedMaskSet(
    MaskPlane SkinMask,
    MaskPlane EyeProtectMask,
    MaskPlane EyebrowProtectMask,
    MaskPlane LipProtectMask,
    MaskPlane NoseMask,
    MaskPlane NostrilProtectMask,
    MaskPlane SoftProtectMask);
