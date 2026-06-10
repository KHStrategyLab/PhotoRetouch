namespace PhotoRetouch;

public static class NoseProtectMaskBuilder
{
    public static MaskPlane BuildSoftProtect(int width, int height, FaceFeatureMesh noseMesh, double opacity = 0.50)
    {
        double featherRadius = Math.Clamp(noseMesh.Bounds.Height * 0.11, 16.0, 64.0);
        return MeshMaskRasterizer.FillClosedMesh(width, height, noseMesh, opacity, featherRadius);
    }
}
