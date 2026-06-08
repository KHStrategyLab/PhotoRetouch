namespace PhotoRetouch;

public sealed record ParsingMaskSet(
    MaskPlane? SkinMask,
    MaskPlane? LeftEyeMask,
    MaskPlane? RightEyeMask,
    MaskPlane? LeftEyebrowMask,
    MaskPlane? RightEyebrowMask,
    MaskPlane? UpperLipMask,
    MaskPlane? LowerLipMask,
    MaskPlane? InnerMouthMask,
    MaskPlane? HairMask,
    MaskPlane? NeckMask,
    MaskPlane? GlassesMask,
    MaskPlane? BeardMask,
    MaskPlane? MustacheMask,
    MaskPlane? ClothMask,
    MaskPlane? BackgroundMask,
    double ParsingConfidence,
    IReadOnlyList<string> DebugWarnings);
