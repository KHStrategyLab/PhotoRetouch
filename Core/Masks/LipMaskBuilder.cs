namespace PhotoRetouch;

public static class LipMaskBuilder
{
    public static MaskPlane Build(int width, int height, FaceFeatureMesh lipMesh)
    {
        return MeshMaskRasterizer.FillClosedMesh(width, height, lipMesh, 1.0, featherRadius: 2.0);
    }
}
