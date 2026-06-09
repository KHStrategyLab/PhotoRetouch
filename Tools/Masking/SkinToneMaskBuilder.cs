namespace PhotoRetouch;

public sealed record SkinToneMaskSet(
    MaskPlane SkinToneApplyMask,
    MaskPlane FaceOnlyWarpMask,
    MaskPlane HairExcludedMask,
    MaskPlane GlassesExcludedMask,
    MaskPlane NostrilExcludedMask,
    MaskPlane LipExcludedMask,
    MaskPlane BeardHairMask,
    MaskPlane BeardShadowMask,
    MaskPlane NoseStructureProtectMask,
    MaskPlane NoseShadowMask,
    MaskPlane NoseRetouchStrengthMap,
    MaskPlane HardExcludedMask);

public static class SkinToneMaskBuilder
{
    public static SkinToneMaskSet Build(FaceMaskSet masks)
    {
        ArgumentNullException.ThrowIfNull(masks);
        MaskPlane.EnsureSameSize(masks.SkinMask, masks.HardProtectMask);

        MaskPlane empty = MaskPlane.Empty(masks.SkinMask.Width, masks.SkinMask.Height);
        MaskPlane lipExcluded = MaskPlane.Union(masks.LipMask, masks.InnerMouthMask, masks.TeethMask);
        MaskPlane beardHair = MaskPlane.Union(masks.BeardMask, masks.MustacheMask);
        MaskPlane hardExcluded = MaskPlane.Union(
            masks.HardProtectMask,
            masks.HairMask,
            masks.GlassesMask,
            masks.NostrilMask,
            lipExcluded,
            beardHair);

        return new SkinToneMaskSet(
            empty,
            empty,
            masks.HairMask.Clone(),
            masks.GlassesMask.Clone(),
            masks.NostrilMask.Clone(),
            lipExcluded,
            beardHair,
            empty,
            empty,
            empty,
            empty,
            hardExcluded);
    }

    public static FaceMaskSet ApplyToFaceMaskSet(FaceMaskSet masks)
    {
        MaskPlane empty = MaskPlane.Empty(masks.SkinMask.Width, masks.SkinMask.Height);
        return masks with
        {
            SkinMask = empty,
            NoseMask = empty,
            NoseSkinMask = empty,
            NoseShadowMask = empty,
            SoftProtectMask = empty,
            RetouchAllowMask = empty,
            FinalOverlayMask = masks.HardProtectMask.Clone()
        };
    }
}
