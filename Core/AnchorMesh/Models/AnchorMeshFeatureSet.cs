namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshFeatureSet
{
    public AnchorMeshFeature? FaceOutline { get; set; }

    public AnchorMeshFeature? LeftEye { get; set; }

    public AnchorMeshFeature? RightEye { get; set; }

    public AnchorMeshFeature? LeftBrow { get; set; }

    public AnchorMeshFeature? RightBrow { get; set; }

    public AnchorMeshFeature? Nose { get; set; }

    public AnchorMeshFeature? LipOuter { get; set; }

    public AnchorMeshFeature? LipInner { get; set; }

    public AnchorMeshFeature? LeftEar { get; set; }

    public AnchorMeshFeature? RightEar { get; set; }

    public AnchorMeshFeature? Hairline { get; set; }

    public AnchorMeshFeature? Neck { get; set; }

    public AnchorMeshFeature? ShirtShoulder { get; set; }

    public IEnumerable<AnchorMeshFeature> GetAll()
    {
        if (FaceOutline is not null) yield return FaceOutline;
        if (LeftEye is not null) yield return LeftEye;
        if (RightEye is not null) yield return RightEye;
        if (LeftBrow is not null) yield return LeftBrow;
        if (RightBrow is not null) yield return RightBrow;
        if (Nose is not null) yield return Nose;
        if (LipOuter is not null) yield return LipOuter;
        if (LipInner is not null) yield return LipInner;
        if (LeftEar is not null) yield return LeftEar;
        if (RightEar is not null) yield return RightEar;
        if (Hairline is not null) yield return Hairline;
        if (Neck is not null) yield return Neck;
        if (ShirtShoulder is not null) yield return ShirtShoulder;
    }

    public AnchorMeshFeatureSet Clone()
    {
        return new AnchorMeshFeatureSet
        {
            FaceOutline = FaceOutline?.Clone(),
            LeftEye = LeftEye?.Clone(),
            RightEye = RightEye?.Clone(),
            LeftBrow = LeftBrow?.Clone(),
            RightBrow = RightBrow?.Clone(),
            Nose = Nose?.Clone(),
            LipOuter = LipOuter?.Clone(),
            LipInner = LipInner?.Clone(),
            LeftEar = LeftEar?.Clone(),
            RightEar = RightEar?.Clone(),
            Hairline = Hairline?.Clone(),
            Neck = Neck?.Clone(),
            ShirtShoulder = ShirtShoulder?.Clone()
        };
    }
}
