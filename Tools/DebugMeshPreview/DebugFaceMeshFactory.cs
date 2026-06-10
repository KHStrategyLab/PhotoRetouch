namespace PhotoRetouch;

public static class DebugFaceMeshFactory
{
    public static DebugFaceMesh CreateDefaultMesh()
    {
        DebugMeshPoint[] points =
        {
            new("Face_00_Top", 0.00f, -0.95f, -0.05f),
            new("Face_01_TopLeft1", -0.22f, -0.88f, -0.04f),
            new("Face_02_TopLeft2", -0.42f, -0.75f, -0.04f),
            new("Face_03_LeftTemple", -0.58f, -0.52f, -0.05f),
            new("Face_04_LeftCheek1", -0.66f, -0.20f, -0.05f),
            new("Face_05_LeftCheek2", -0.69f, 0.12f, -0.05f),
            new("Face_06_LeftJaw", -0.56f, 0.52f, -0.04f),
            new("Face_07_LeftChin", -0.25f, 0.86f, -0.03f),
            new("Face_08_Chin", 0.00f, 0.96f, -0.02f),
            new("Face_09_RightChin", 0.25f, 0.86f, -0.03f),
            new("Face_10_RightJaw", 0.56f, 0.52f, -0.04f),
            new("Face_11_RightCheek2", 0.69f, 0.12f, -0.05f),
            new("Face_12_RightCheek1", 0.66f, -0.20f, -0.05f),
            new("Face_13_RightTemple", 0.58f, -0.52f, -0.05f),
            new("Face_14_TopRight2", 0.42f, -0.75f, -0.04f),
            new("Face_15_TopRight1", 0.22f, -0.88f, -0.04f),

            new("LEye_00_LeftCorner", -0.36f, -0.20f, 0.05f),
            new("LEye_01_UpperLeft", -0.30f, -0.24f, 0.06f),
            new("LEye_02_UpperCenter", -0.22f, -0.25f, 0.06f),
            new("LEye_03_UpperRight", -0.14f, -0.23f, 0.06f),
            new("LEye_04_RightCorner", -0.10f, -0.20f, 0.05f),
            new("LEye_05_LowerRight", -0.15f, -0.16f, 0.05f),
            new("LEye_06_LowerCenter", -0.22f, -0.15f, 0.05f),
            new("LEye_07_LowerLeft", -0.30f, -0.16f, 0.05f),

            new("REye_00_LeftCorner", 0.10f, -0.20f, 0.05f),
            new("REye_01_UpperLeft", 0.14f, -0.23f, 0.06f),
            new("REye_02_UpperCenter", 0.22f, -0.25f, 0.06f),
            new("REye_03_UpperRight", 0.30f, -0.24f, 0.06f),
            new("REye_04_RightCorner", 0.36f, -0.20f, 0.05f),
            new("REye_05_LowerRight", 0.30f, -0.16f, 0.05f),
            new("REye_06_LowerCenter", 0.22f, -0.15f, 0.05f),
            new("REye_07_LowerLeft", 0.15f, -0.16f, 0.05f),

            new("LBrow_00", -0.37f, -0.31f, 0.03f),
            new("LBrow_01", -0.31f, -0.35f, 0.04f),
            new("LBrow_02", -0.24f, -0.37f, 0.04f),
            new("LBrow_03", -0.18f, -0.36f, 0.04f),
            new("LBrow_04", -0.12f, -0.33f, 0.03f),
            new("LBrow_05", -0.08f, -0.30f, 0.03f),
            new("RBrow_00", 0.08f, -0.30f, 0.03f),
            new("RBrow_01", 0.12f, -0.33f, 0.03f),
            new("RBrow_02", 0.18f, -0.36f, 0.04f),
            new("RBrow_03", 0.24f, -0.37f, 0.04f),
            new("RBrow_04", 0.31f, -0.35f, 0.04f),
            new("RBrow_05", 0.37f, -0.31f, 0.03f),

            new("Nose_00_BridgeTop", 0.00f, -0.14f, 0.10f),
            new("Nose_01_BridgeMid", 0.00f, -0.02f, 0.14f),
            new("Nose_02_BridgeLow", 0.00f, 0.08f, 0.18f),
            new("Nose_03_LeftSide", -0.10f, 0.12f, 0.15f),
            new("Nose_04_RightSide", 0.10f, 0.12f, 0.15f),
            new("Nose_05_LeftWing", -0.15f, 0.20f, 0.13f),
            new("Nose_06_Tip", 0.00f, 0.20f, 0.25f),
            new("Nose_07_RightWing", 0.15f, 0.20f, 0.13f),
            new("Nose_08_LeftNostril", -0.08f, 0.25f, 0.12f),
            new("Nose_09_RightNostril", 0.08f, 0.25f, 0.12f),

            new("Lip_00_LeftCorner", -0.30f, 0.36f, 0.08f),
            new("Lip_01_UpperLeft2", -0.22f, 0.32f, 0.09f),
            new("Lip_02_UpperLeft1", -0.12f, 0.29f, 0.10f),
            new("Lip_03_UpperCenter", 0.00f, 0.28f, 0.11f),
            new("Lip_04_UpperRight1", 0.12f, 0.29f, 0.10f),
            new("Lip_05_UpperRight2", 0.22f, 0.32f, 0.09f),
            new("Lip_06_RightCorner", 0.30f, 0.36f, 0.08f),
            new("Lip_07_LowerRight2", 0.22f, 0.41f, 0.09f),
            new("Lip_08_LowerRight1", 0.12f, 0.45f, 0.10f),
            new("Lip_09_LowerCenter", 0.00f, 0.47f, 0.11f),
            new("Lip_10_LowerLeft1", -0.12f, 0.45f, 0.10f),
            new("Lip_11_LowerLeft2", -0.22f, 0.41f, 0.09f),

            new("LEar_00_Top", -0.73f, -0.18f, -0.10f),
            new("LEar_01_Front", -0.68f, -0.05f, -0.09f),
            new("LEar_02_Mid", -0.73f, 0.10f, -0.10f),
            new("LEar_03_Back", -0.79f, 0.02f, -0.11f),
            new("LEar_04_Lobe", -0.72f, 0.26f, -0.10f),
            new("LEar_05_Inner", -0.70f, 0.05f, -0.09f),
            new("REar_00_Top", 0.73f, -0.18f, -0.10f),
            new("REar_01_Front", 0.68f, -0.05f, -0.09f),
            new("REar_02_Mid", 0.73f, 0.10f, -0.10f),
            new("REar_03_Back", 0.79f, 0.02f, -0.11f),
            new("REar_04_Lobe", 0.72f, 0.26f, -0.10f),
            new("REar_05_Inner", 0.70f, 0.05f, -0.09f),

            new("Hair_00_LeftSide", -0.62f, -0.78f, -0.06f),
            new("Hair_01_LeftTop1", -0.42f, -1.02f, -0.06f),
            new("Hair_02_LeftTop2", -0.18f, -1.10f, -0.05f),
            new("Hair_03_Top", 0.00f, -1.12f, -0.05f),
            new("Hair_04_RightTop2", 0.18f, -1.10f, -0.05f),
            new("Hair_05_RightTop1", 0.42f, -1.02f, -0.06f),
            new("Hair_06_RightSide", 0.62f, -0.78f, -0.06f),
            new("Hair_07_LeftFront", -0.30f, -0.76f, -0.03f),
            new("Hair_08_Front", 0.00f, -0.73f, -0.02f),
            new("Hair_09_RightFront", 0.30f, -0.76f, -0.03f),

            new("Neck_00_LeftTop", -0.18f, 0.92f, -0.08f),
            new("Neck_01_RightTop", 0.18f, 0.92f, -0.08f),
            new("Neck_02_LeftBottom", -0.22f, 1.18f, -0.12f),
            new("Neck_03_RightBottom", 0.22f, 1.18f, -0.12f),

            new("Shirt_00_LeftCollar", -0.30f, 1.10f, -0.12f),
            new("Shirt_01_RightCollar", 0.30f, 1.10f, -0.12f),
            new("Shirt_02_LeftShoulder", -0.75f, 1.30f, -0.18f),
            new("Shirt_03_RightShoulder", 0.75f, 1.30f, -0.18f),
            new("Shirt_04_LeftChest", -0.42f, 1.55f, -0.20f),
            new("Shirt_05_RightChest", 0.42f, 1.55f, -0.20f)
        };

        return new DebugFaceMesh(points, CreateEdges());
    }

    private static DebugMeshEdge[] CreateEdges()
    {
        List<DebugMeshEdge> edges = new();
        AddLoop(edges, "Face_00_Top", "Face_01_TopLeft1", "Face_02_TopLeft2", "Face_03_LeftTemple", "Face_04_LeftCheek1", "Face_05_LeftCheek2", "Face_06_LeftJaw", "Face_07_LeftChin", "Face_08_Chin", "Face_09_RightChin", "Face_10_RightJaw", "Face_11_RightCheek2", "Face_12_RightCheek1", "Face_13_RightTemple", "Face_14_TopRight2", "Face_15_TopRight1");
        AddLoop(edges, "LEye_00_LeftCorner", "LEye_01_UpperLeft", "LEye_02_UpperCenter", "LEye_03_UpperRight", "LEye_04_RightCorner", "LEye_05_LowerRight", "LEye_06_LowerCenter", "LEye_07_LowerLeft");
        AddLoop(edges, "REye_00_LeftCorner", "REye_01_UpperLeft", "REye_02_UpperCenter", "REye_03_UpperRight", "REye_04_RightCorner", "REye_05_LowerRight", "REye_06_LowerCenter", "REye_07_LowerLeft");
        AddChain(edges, "LBrow_00", "LBrow_01", "LBrow_02", "LBrow_03", "LBrow_04", "LBrow_05");
        AddChain(edges, "RBrow_00", "RBrow_01", "RBrow_02", "RBrow_03", "RBrow_04", "RBrow_05");
        AddChain(edges, "Nose_00_BridgeTop", "Nose_01_BridgeMid", "Nose_02_BridgeLow", "Nose_06_Tip");
        edges.AddRange(new[]
        {
            new DebugMeshEdge("Nose_03_LeftSide", "Nose_06_Tip"),
            new DebugMeshEdge("Nose_04_RightSide", "Nose_06_Tip"),
            new DebugMeshEdge("Nose_05_LeftWing", "Nose_06_Tip"),
            new DebugMeshEdge("Nose_06_Tip", "Nose_07_RightWing"),
            new DebugMeshEdge("Nose_05_LeftWing", "Nose_08_LeftNostril"),
            new DebugMeshEdge("Nose_07_RightWing", "Nose_09_RightNostril")
        });
        AddLoop(edges, "Lip_00_LeftCorner", "Lip_01_UpperLeft2", "Lip_02_UpperLeft1", "Lip_03_UpperCenter", "Lip_04_UpperRight1", "Lip_05_UpperRight2", "Lip_06_RightCorner", "Lip_07_LowerRight2", "Lip_08_LowerRight1", "Lip_09_LowerCenter", "Lip_10_LowerLeft1", "Lip_11_LowerLeft2");
        AddLoop(edges, "LEar_00_Top", "LEar_01_Front", "LEar_02_Mid", "LEar_03_Back", "LEar_04_Lobe", "LEar_05_Inner");
        AddLoop(edges, "REar_00_Top", "REar_01_Front", "REar_02_Mid", "REar_03_Back", "REar_04_Lobe", "REar_05_Inner");
        AddChain(edges, "Hair_00_LeftSide", "Hair_01_LeftTop1", "Hair_02_LeftTop2", "Hair_03_Top", "Hair_04_RightTop2", "Hair_05_RightTop1", "Hair_06_RightSide");
        AddChain(edges, "Hair_07_LeftFront", "Hair_08_Front", "Hair_09_RightFront");
        edges.AddRange(new[]
        {
            new DebugMeshEdge("Neck_00_LeftTop", "Neck_02_LeftBottom"),
            new DebugMeshEdge("Neck_01_RightTop", "Neck_03_RightBottom"),
            new DebugMeshEdge("Neck_00_LeftTop", "Neck_01_RightTop"),
            new DebugMeshEdge("Neck_02_LeftBottom", "Neck_03_RightBottom"),
            new DebugMeshEdge("Shirt_00_LeftCollar", "Shirt_01_RightCollar"),
            new DebugMeshEdge("Shirt_00_LeftCollar", "Shirt_02_LeftShoulder"),
            new DebugMeshEdge("Shirt_01_RightCollar", "Shirt_03_RightShoulder"),
            new DebugMeshEdge("Shirt_02_LeftShoulder", "Shirt_04_LeftChest"),
            new DebugMeshEdge("Shirt_03_RightShoulder", "Shirt_05_RightChest"),
            new DebugMeshEdge("Shirt_04_LeftChest", "Shirt_05_RightChest")
        });
        return edges.ToArray();
    }

    private static void AddLoop(List<DebugMeshEdge> edges, params string[] names)
    {
        AddChain(edges, names);
        edges.Add(new DebugMeshEdge(names[^1], names[0]));
    }

    private static void AddChain(List<DebugMeshEdge> edges, params string[] names)
    {
        for (int index = 0; index < names.Length - 1; index++)
        {
            edges.Add(new DebugMeshEdge(names[index], names[index + 1]));
        }
    }
}
