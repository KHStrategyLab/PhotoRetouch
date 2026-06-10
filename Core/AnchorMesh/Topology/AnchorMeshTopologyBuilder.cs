namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshTopologyBuilder
{
    public static List<AnchorMeshEdge> BuildDefaultEdges()
    {
        List<AnchorMeshEdge> edges = new();
        AddFeatureEdges(edges, "FaceOutline", AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.FaceOutline, closed: true);
        AddFeatureEdges(edges, "LeftEye", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.LeftEyeOpeningLoop, closed: true);
        AddFeatureEdges(edges, "RightEye", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.RightEyeOpeningLoop, closed: true);
        AddFeatureEdges(edges, "LeftPupil", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.LeftPupilProtectionLoop, closed: true);
        AddFeatureEdges(edges, "RightPupil", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.RightPupilProtectionLoop, closed: true);
        AddFeatureEdges(edges, "LeftBrow", AnchorMeshEdgeKind.Anchor, AnchorMeshEdgeGroup.LeftBrowAnchorCurve, closed: false);
        AddFeatureEdges(edges, "RightBrow", AnchorMeshEdgeKind.Anchor, AnchorMeshEdgeGroup.RightBrowAnchorCurve, closed: false);
        AddFeatureEdges(edges, "LipOuter", AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.LipOuterLoop, closed: true);
        AddFeatureEdges(edges, "LipInner", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.InnerMouthProtectionLoop, closed: true);

        AddFaceCenterAndWidthMeasurements(edges);
        AddEyeBrowMeasurements(edges);
        AddNoseTopology(edges);
        AddLipTopology(edges);
        AddCheekJawTopology(edges);
        return edges;
    }

    private static void AddFaceCenterAndWidthMeasurements(List<AnchorMeshEdge> edges)
    {
        AddChain(edges, AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.FaceCenterAlignment, "Nose_00", "Nose_08", "Nose_23", "LipOuter_18", "FaceOutline_15");
        Add(edges, "LeftEye_00", "RightEye_08", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.FaceWidthMeasurements);
        Add(edges, "FaceOutline_26", "FaceOutline_34", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.FaceWidthMeasurements);
        Add(edges, "FaceOutline_22", "FaceOutline_08", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.FaceWidthMeasurements);
        Add(edges, "FaceOutline_17", "FaceOutline_13", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.FaceWidthMeasurements);
    }

    private static void AddEyeBrowMeasurements(List<AnchorMeshEdge> edges)
    {
        Add(edges, "LeftEye_12", "LeftBrow_05", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.LeftBrowEyeMeasurement);
        Add(edges, "LeftEye_12", "LeftBrow_11", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.LeftBrowEyeMeasurement);
        Add(edges, "RightEye_12", "RightBrow_06", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.RightBrowEyeMeasurement);
        Add(edges, "RightEye_12", "RightBrow_00", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.RightBrowEyeMeasurement);
    }

    private static void AddNoseTopology(List<AnchorMeshEdge> edges)
    {
        AddChain(edges, AnchorMeshEdgeKind.Anchor, AnchorMeshEdgeGroup.NoseCenterAxis, "Nose_00", "Nose_08", "Nose_22", "Nose_23");

        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.NoseBridgeSurfaceLoop, "Nose_01", "Nose_02", "Nose_04", "Nose_06", "Nose_09", "Nose_07", "Nose_05", "Nose_03");
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.NoseTipSurfaceLoop, "Nose_07", "Nose_08", "Nose_09", "Nose_13", "Nose_22", "Nose_12");
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.LeftNoseWingSurfaceLoop, "Nose_10", "Nose_11", "Nose_12", "Nose_18", "Nose_17", "Nose_16");
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.RightNoseWingSurfaceLoop, "Nose_13", "Nose_14", "Nose_15", "Nose_20", "Nose_19", "Nose_21");
        AddChain(edges, AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.NoseBaseEdge, "Nose_12", "Nose_22", "Nose_23", "Nose_13");
        AddLoop(edges, AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.LeftNostrilProtectionLoop, "Nose_16", "Nose_17", "Nose_18");
        AddLoop(edges, AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.RightNostrilProtectionLoop, "Nose_19", "Nose_20", "Nose_21");
    }

    private static void AddLipTopology(List<AnchorMeshEdge> edges)
    {
        Add(edges, "LipOuter_00", "LipOuter_12", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.LipOuterLoop);
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.UpperLipSurfaceLoop, "LipOuter_00", "LipOuter_23", "LipOuter_22", "LipOuter_21", "LipOuter_20", "LipOuter_19", "LipOuter_18", "LipOuter_17", "LipOuter_16", "LipOuter_15", "LipOuter_14", "LipOuter_13", "LipOuter_12", "LipInner_08", "LipInner_09", "LipInner_10", "LipInner_11", "LipInner_12", "LipInner_13", "LipInner_14", "LipInner_15", "LipInner_00");
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.LowerLipSurfaceLoop, "LipOuter_00", "LipInner_00", "LipInner_01", "LipInner_02", "LipInner_03", "LipInner_04", "LipInner_05", "LipInner_06", "LipInner_07", "LipInner_08", "LipOuter_12", "LipOuter_11", "LipOuter_10", "LipOuter_09", "LipOuter_08", "LipOuter_07", "LipOuter_06", "LipOuter_05", "LipOuter_04", "LipOuter_03", "LipOuter_02", "LipOuter_01");
        AddFeatureEdges(edges, "LipOuter", AnchorMeshEdgeKind.Protection, AnchorMeshEdgeGroup.VermilionProtectionEdge, closed: true, weight: 0.75f);
        AddChain(edges, AnchorMeshEdgeKind.Anchor, AnchorMeshEdgeGroup.PhiltrumGuide, "Nose_23", "LipOuter_18");
        Add(edges, "Nose_22", "LipOuter_17", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.NoseMouthLinks);
        Add(edges, "Nose_23", "LipOuter_18", AnchorMeshEdgeKind.Measurement, AnchorMeshEdgeGroup.NoseMouthLinks);
    }

    private static void AddCheekJawTopology(List<AnchorMeshEdge> edges)
    {
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.LeftCheekRegionLoop, "LeftEye_08", "FaceOutline_27", "FaceOutline_23", "FaceOutline_20", "LipOuter_00", "Nose_10", "LeftEye_00");
        AddLoop(edges, AnchorMeshEdgeKind.Surface, AnchorMeshEdgeGroup.RightCheekRegionLoop, "RightEye_00", "FaceOutline_33", "FaceOutline_37", "FaceOutline_40", "LipOuter_12", "Nose_15", "RightEye_08");
        AddChain(edges, AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.LeftJawlineEdge, "FaceOutline_23", "FaceOutline_22", "FaceOutline_21", "FaceOutline_20", "FaceOutline_19", "FaceOutline_18", "FaceOutline_17", "FaceOutline_16", "FaceOutline_15");
        AddChain(edges, AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.RightJawlineEdge, "FaceOutline_07", "FaceOutline_08", "FaceOutline_09", "FaceOutline_10", "FaceOutline_11", "FaceOutline_12", "FaceOutline_13", "FaceOutline_14", "FaceOutline_15");
        AddChain(edges, AnchorMeshEdgeKind.Boundary, AnchorMeshEdgeGroup.ChinContourEdge, "FaceOutline_17", "FaceOutline_16", "FaceOutline_15", "FaceOutline_14", "FaceOutline_13");

        Add(edges, "LeftEye_08", "FaceOutline_26", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "RightEye_00", "FaceOutline_34", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "FaceOutline_23", "FaceOutline_15", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
        Add(edges, "FaceOutline_37", "FaceOutline_15", AnchorMeshEdgeKind.MorphControl, AnchorMeshEdgeGroup.CheekJawLinks);
    }

    private static void AddFeatureEdges(
        List<AnchorMeshEdge> edges,
        string featureName,
        AnchorMeshEdgeKind kind,
        AnchorMeshEdgeGroup group,
        bool closed,
        float weight = 1.0f)
    {
        int count = featureName switch
        {
            "FaceOutline" => 60,
            "LeftEye" or "RightEye" => 16,
            "LeftPupil" or "RightPupil" => 12,
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
            Add(edges, $"{featureName}_{i:00}", $"{featureName}_{next:00}", kind, group, weight);
        }
    }

    private static void AddLoop(List<AnchorMeshEdge> edges, AnchorMeshEdgeKind kind, AnchorMeshEdgeGroup group, params string[] names)
    {
        if (names.Length < 2)
        {
            return;
        }

        AddChain(edges, kind, group, names);
        Add(edges, names[^1], names[0], kind, group);
    }

    private static void AddChain(List<AnchorMeshEdge> edges, AnchorMeshEdgeKind kind, AnchorMeshEdgeGroup group, params string[] names)
    {
        for (int index = 0; index < names.Length - 1; index++)
        {
            Add(edges, names[index], names[index + 1], kind, group);
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
