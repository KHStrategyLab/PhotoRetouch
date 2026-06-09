namespace PhotoRetouch.AnchorMesh;

public sealed class FeatureMaskContourProvider
{
    private readonly Dictionary<string, MaskPlane> _masks = new(StringComparer.OrdinalIgnoreCase);

    public FeatureMaskContourProvider WithMask(string featureName, MaskPlane mask)
    {
        _masks[featureName] = mask;
        return this;
    }

    public bool TryGetMask(string featureName, out MaskPlane mask)
    {
        return _masks.TryGetValue(featureName, out mask!);
    }
}
