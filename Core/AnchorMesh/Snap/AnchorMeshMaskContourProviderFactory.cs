namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshMaskContourProviderFactory
{
    public static FeatureMaskContourProvider FromFaceMaskSet(FaceMaskSet masks)
    {
        FeatureMaskContourProvider provider = new();

        provider.WithMask("FaceOutline", SelectFaceOutlineMask(masks));
        provider.WithMask("LeftEye", masks.EyeMask);
        provider.WithMask("RightEye", masks.EyeMask);
        provider.WithMask("LeftBrow", masks.EyebrowMask);
        provider.WithMask("RightBrow", masks.EyebrowMask);
        provider.WithMask("Nose", SelectNoseMask(masks));
        provider.WithMask("LipOuter", masks.LipMask);
        provider.WithMask("LipInner", SelectLipInnerMask(masks));
        provider.WithMask("Hairline", masks.HairMask);
        provider.WithMask("Neck", masks.SoftProtectMask);

        return provider;
    }

    private static MaskPlane SelectFaceOutlineMask(FaceMaskSet masks)
    {
        return masks.SkinMask.Average() > 0.0001
            ? masks.SkinMask
            : MaskPlane.Union(masks.RetouchAllowMask, masks.SoftProtectMask, masks.HardProtectMask);
    }

    private static MaskPlane SelectNoseMask(FaceMaskSet masks)
    {
        return masks.NoseMask.Average() > 0.0001
            ? masks.NoseMask
            : MaskPlane.Union(masks.NoseSkinMask, masks.SoftProtectMask, masks.NostrilMask);
    }

    private static MaskPlane SelectLipInnerMask(FaceMaskSet masks)
    {
        return masks.InnerMouthMask.Average() > 0.0001
            ? masks.InnerMouthMask
            : masks.LipMask;
    }
}
