namespace PhotoRetouch;

public sealed record FaceFeatureMeshSet(
    FaceFeatureMesh LipMesh,
    FaceFeatureMesh EyeMesh,
    FaceFeatureMesh NoseMesh,
    FaceFeatureMesh BrowMesh)
{
    public FaceFeatureMesh Get(FaceFeatureType featureType)
    {
        return featureType switch
        {
            FaceFeatureType.Lip => LipMesh,
            FaceFeatureType.Eye => EyeMesh,
            FaceFeatureType.Nose => NoseMesh,
            FaceFeatureType.Brow => BrowMesh,
            _ => throw new ArgumentOutOfRangeException(nameof(featureType), featureType, null)
        };
    }
}
