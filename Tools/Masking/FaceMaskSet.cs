namespace PhotoRetouch;

public sealed record FaceMaskSet(
    MaskPlane SkinMask,
    MaskPlane EyeMask,
    MaskPlane EyebrowMask,
    MaskPlane LipMask,
    MaskPlane InnerMouthMask,
    MaskPlane TeethMask,
    MaskPlane NoseMask,
    MaskPlane NoseSkinMask,
    MaskPlane NostrilMask,
    MaskPlane NoseShadowMask,
    MaskPlane HairMask,
    MaskPlane BeardMask,
    MaskPlane MustacheMask,
    MaskPlane GlassesMask,
    MaskPlane HardProtectMask,
    MaskPlane SoftProtectMask,
    MaskPlane RetouchAllowMask,
    MaskPlane FinalOverlayMask);
