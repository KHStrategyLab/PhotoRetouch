namespace PhotoRetouch;

public static class BrowProtectMaskBuilder
{
    public static MaskPlane Build(int width, int height, FaceFeatureMesh browMesh)
    {
        return MeshMaskRasterizer.FillRoleGroupedMeshes(width, height, browMesh, 0.85, featherRadius: 2.0);
    }
}
