namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshTopologyBuilder
{
    public static List<AnchorMeshEdge> BuildDefaultEdges()
    {
        List<AnchorMeshEdge> edges = new();
        AddFeatureEdges(edges, "FaceOutline", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.FaceOutline, closed: true);
        AddFeatureEdges(edges, "LeftEye", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.LeftEyeLoop, closed: true);
        AddFeatureEdges(edges, "RightEye", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.RightEyeLoop, closed: true);
        AddFeatureEdges(edges, "LeftBrow", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.LeftBrowCurve, closed: false);
        AddFeatureEdges(edges, "RightBrow", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.RightBrowCurve, closed: false);
        AddFeatureEdges(edges, "Nose", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.NoseStructure, closed: false);
        AddFeatureEdges(edges, "LipOuter", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.LipOuterLoop, closed: true);
        AddFeatureEdges(edges, "LipInner", AnchorMeshEdgeKind.Contour, AnchorMeshEdgeGroup.LipInnerLoop, closed: true);

        Add(edges, "Nose_00", "Nose_08", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.CenterLine);
        Add(edges, "Nose_08", "Nose_23", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.CenterLine);
        Add(edges, "Nose_23", "LipOuter_18", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.CenterLine);
        Add(edges, "LipOuter_18", "FaceOutline_15", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.CenterLine);

        Add(edges, "LeftEye_00", "RightEye_08", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.EyeNoseLinks);
        Add(edges, "LeftEye_04", "Nose_00", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.EyeNoseLinks);
        Add(edges, "RightEye_12", "Nose_00", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.EyeNoseLinks);
        Add(edges, "Nose_21", "LipOuter_00", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.NoseMouthLinks);
        Add(edges, "Nose_18", "LipOuter_12", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.NoseMouthLinks);
        Add(edges, "LipOuter_00", "FaceOutline_19", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.MouthChinLinks);
        Add(edges, "LipOuter_12", "FaceOutline_11", AnchorMeshEdgeKind.Structural, AnchorMeshEdgeGroup.MouthChinLinks);

        Add(edges, "LeftEye_08", "FaceOutline_26", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "RightEye_00", "FaceOutline_34", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "FaceOutline_23", "FaceOutline_15", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "FaceOutline_37", "FaceOutline_45", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        return edges;
    }

    private static void AddFeatureEdges(
        List<AnchorMeshEdge> edges,
        string featureName,
        AnchorMeshEdgeKind kind,
        AnchorMeshEdgeGroup group,
        bool closed)
    {
        int count = featureName switch
        {
            "FaceOutline" => 60,
            "LeftEye" or "RightEye" => 16,
            "LeftBrow" or "RightBrow" => 12,
            "Nose" => 24,
            "LipOuter" => 24,
            "LipInner" => 16,
            _ => 0
        };

        int last = closed ? count : count - 1;
        for (int i = 0; i < last; i++)
        {
            int next = (i + 1) % count;
            Add(edges, $"{featureName}_{i:00}", $"{featureName}_{next:00}", kind, group);
        }
    }

    private static void Add(
        List<AnchorMeshEdge> edges,
        string from,
        string to,
        AnchorMeshEdgeKind kind,
        AnchorMeshEdgeGroup group,
        float weight = 1.0f)
    {
        edges.Add(new AnchorMeshEdge(from, to, kind, group, weight));
    }
}
