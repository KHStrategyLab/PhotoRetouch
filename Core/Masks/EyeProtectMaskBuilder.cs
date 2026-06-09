namespace PhotoRetouch;

public static class EyeProtectMaskBuilder
{
    public static MaskPlane Build(int width, int height, FaceFeatureMesh eyeMesh)
    {
        return MeshMaskRasterizer.FillRoleGroupedMeshes(width, height, eyeMesh, 1.0, featherRadius: 1.4);
    }
}
