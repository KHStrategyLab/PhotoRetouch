namespace PhotoRetouch.AnchorMesh;

public static class AnchorWarpHandleGroupBuilder
{
    public static AnchorWarpHandleGroupSet BuildDefaultGroups(AnchorMeshFeatureSet features)
    {
        AnchorWarpHandleGroupSet set = new();
        AddFeatureGroup(set, features.LeftEye, AnchorWarpHandleTarget.LeftEye, "LeftEyeWarpGroup", 32, 18, locked: ["Nose_00", "Nose_08"]);
        AddFeatureGroup(set, features.RightEye, AnchorWarpHandleTarget.RightEye, "RightEyeWarpGroup", 32, 18, locked: ["Nose_00", "Nose_08"]);
        AddFeatureGroup(set, features.LeftBrow, AnchorWarpHandleTarget.LeftBrow, "LeftBrowWarpGroup", 34, 16, locked: ["LeftEye_00", "LeftEye_08"]);
        AddFeatureGroup(set, features.RightBrow, AnchorWarpHandleTarget.RightBrow, "RightBrowWarpGroup", 34, 16, locked: ["RightEye_00", "RightEye_08"]);
        AddFeatureGroup(set, features.Nose, AnchorWarpHandleTarget.Nose, "NoseWarpGroup", 42, 18, locked: ["LeftEye_04", "RightEye_12", "LipOuter_18"]);
        AddMouthGroup(set, features);
        AddChinGroup(set, features.FaceOutline);
        AddJawlineGroup(set, features.FaceOutline);
        AddFeatureGroup(set, features.FaceOutline, AnchorWarpHandleTarget.FaceOutline, "FaceOutlineWarpGroup", 84, 24, locked: ["LeftEye_04", "RightEye_12", "Nose_08", "LipOuter_18"]);
        return set;
    }

    private static void AddFeatureGroup(
        AnchorWarpHandleGroupSet set,
        AnchorMeshFeature? feature,
        AnchorWarpHandleTarget target,
        string name,
        float influenceRadius,
        float safeZoneRadius,
        IEnumerable<string>? locked = null)
    {
        if (feature is null || feature.Points.Count == 0)
        {
            return;
        }

        AnchorWarpHandleGroup group = CreateGroup(name, target, influenceRadius, safeZoneRadius);
        group.ControlPointNames.AddRange(feature.Points.Select(point => point.Name));
        if (locked is not null)
        {
            group.LockedPointNames.AddRange(locked);
        }

        group.Handles.Add(CreateHandle($"{target}Center", AnchorWarpHandleKind.Center, feature.Points.Select(point => point.Name), feature.CenterX, feature.CenterY, safeZoneRadius));
        if (feature.IsClosedLoop)
        {
            AddExtremeHandles(group, feature, target, safeZoneRadius);
        }
        else
        {
            group.Handles.Add(CreateHandle($"{target}Curve", AnchorWarpHandleKind.Curve, feature.Points.Select(point => point.Name), feature.CenterX, feature.CenterY, safeZoneRadius));
        }

        set.Groups.Add(group);
    }

    private static void AddMouthGroup(AnchorWarpHandleGroupSet set, AnchorMeshFeatureSet features)
    {
        if (features.LipOuter is null)
        {
            return;
        }

        AnchorWarpHandleGroup group = CreateGroup("MouthWarpGroup", AnchorWarpHandleTarget.Mouth, 44, 18);
        group.ControlPointNames.AddRange(features.LipOuter.Points.Select(point => point.Name));
        if (features.LipInner is not null)
        {
            group.ControlPointNames.AddRange(features.LipInner.Points.Select(point => point.Name));
            group.FalloffPointNames.AddRange(features.LipInner.Points.Select(point => point.Name));
        }

        group.FalloffPointNames.AddRange(Points("Nose", 24));
        group.LockedPointNames.AddRange(["Nose_08", "LeftEye_04", "RightEye_12"]);
        group.Handles.Add(CreateHandle("MouthCenter", AnchorWarpHandleKind.Center, group.ControlPointNames, features.LipOuter.CenterX, features.LipOuter.CenterY, group.SafeZoneRadius));
        AddExtremeHandles(group, features.LipOuter, AnchorWarpHandleTarget.Mouth, group.SafeZoneRadius);
        set.Groups.Add(group);
    }

    private static void AddChinGroup(AnchorWarpHandleGroupSet set, AnchorMeshFeature? faceOutline)
    {
        if (faceOutline is null || faceOutline.Points.Count < 12)
        {
            return;
        }

        AnchorWarpHandleGroup group = CreateGroup("ChinWarpGroup", AnchorWarpHandleTarget.Chin, 58, 20);
        string[] chinPoints = ["FaceOutline_10", "FaceOutline_11", "FaceOutline_12", "FaceOutline_13", "FaceOutline_14", "FaceOutline_15", "FaceOutline_16"];
        group.ControlPointNames.AddRange(chinPoints);
        group.FalloffPointNames.AddRange(["FaceOutline_07", "FaceOutline_08", "FaceOutline_09", "FaceOutline_17", "FaceOutline_18", "FaceOutline_19"]);
        group.LockedPointNames.AddRange(["Nose_08", "LipOuter_18"]);
        AnchorMeshPoint? chin = faceOutline.Points.FirstOrDefault(point => point.Name == "FaceOutline_13");
        if (chin is not null)
        {
            group.Handles.Add(CreateHandle("ChinBottom", AnchorWarpHandleKind.Bottom, chinPoints, chin.SnappedX, chin.SnappedY, group.SafeZoneRadius));
        }

        set.Groups.Add(group);
    }

    private static void AddJawlineGroup(AnchorWarpHandleGroupSet set, AnchorMeshFeature? faceOutline)
    {
        if (faceOutline is null || faceOutline.Points.Count < 24)
        {
            return;
        }

        AnchorWarpHandleGroup group = CreateGroup("JawlineWarpGroup", AnchorWarpHandleTarget.Jawline, 72, 22);
        string[] jawPoints = ["FaceOutline_07", "FaceOutline_08", "FaceOutline_09", "FaceOutline_10", "FaceOutline_16", "FaceOutline_17", "FaceOutline_18", "FaceOutline_19"];
        group.ControlPointNames.AddRange(jawPoints);
        group.FalloffPointNames.AddRange(["FaceOutline_05", "FaceOutline_06", "FaceOutline_12", "FaceOutline_14", "FaceOutline_20", "FaceOutline_21"]);
        group.LockedPointNames.AddRange(["Nose_08", "LipOuter_18"]);
        AddNamedPointHandle(group, faceOutline, "JawLeft", AnchorWarpHandleKind.Left, ["FaceOutline_07", "FaceOutline_08", "FaceOutline_09", "FaceOutline_10"], "FaceOutline_08");
        AddNamedPointHandle(group, faceOutline, "JawRight", AnchorWarpHandleKind.Right, ["FaceOutline_16", "FaceOutline_17", "FaceOutline_18", "FaceOutline_19"], "FaceOutline_17");
        set.Groups.Add(group);
    }

    private static AnchorWarpHandleGroup CreateGroup(string name, AnchorWarpHandleTarget target, float influenceRadius, float safeZoneRadius)
    {
        return new AnchorWarpHandleGroup
        {
            Name = name,
            Target = target,
            InfluenceRadius = influenceRadius,
            SafeZoneRadius = safeZoneRadius,
            SolverHint = "MlsSimilarity"
        };
    }

    private static void AddExtremeHandles(AnchorWarpHandleGroup group, AnchorMeshFeature feature, AnchorWarpHandleTarget target, float safeZoneRadius)
    {
        AddPointHandle(group, feature, $"{target}Left", AnchorWarpHandleKind.Left, feature.Points.OrderBy(point => point.SnappedX).First(), safeZoneRadius);
        AddPointHandle(group, feature, $"{target}Right", AnchorWarpHandleKind.Right, feature.Points.OrderByDescending(point => point.SnappedX).First(), safeZoneRadius);
        AddPointHandle(group, feature, $"{target}Top", AnchorWarpHandleKind.Top, feature.Points.OrderBy(point => point.SnappedY).First(), safeZoneRadius);
        AddPointHandle(group, feature, $"{target}Bottom", AnchorWarpHandleKind.Bottom, feature.Points.OrderByDescending(point => point.SnappedY).First(), safeZoneRadius);
    }

    private static void AddNamedPointHandle(AnchorWarpHandleGroup group, AnchorMeshFeature feature, string name, AnchorWarpHandleKind kind, IEnumerable<string> pointNames, string positionPointName)
    {
        AnchorMeshPoint? point = feature.Points.FirstOrDefault(item => item.Name == positionPointName);
        if (point is null)
        {
            return;
        }

        group.Handles.Add(CreateHandle(name, kind, pointNames, point.SnappedX, point.SnappedY, group.SafeZoneRadius));
    }

    private static void AddPointHandle(AnchorWarpHandleGroup group, AnchorMeshFeature feature, string name, AnchorWarpHandleKind kind, AnchorMeshPoint point, float safeZoneRadius)
    {
        group.Handles.Add(CreateHandle(name, kind, [point.Name], point.SnappedX, point.SnappedY, safeZoneRadius));
    }

    private static AnchorWarpHandle CreateHandle(string name, AnchorWarpHandleKind kind, IEnumerable<string> pointNames, float x, float y, float safeZoneRadius)
    {
        AnchorWarpHandle handle = new()
        {
            Name = name,
            Kind = kind,
            X = x,
            Y = y,
            SafeRadius = safeZoneRadius,
            MaxDragDistance = safeZoneRadius
        };
        handle.AnchorPointNames.AddRange(pointNames);
        return handle;
    }

    private static IEnumerable<string> Points(string feature, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return $"{feature}_{i:00}";
        }
    }
}
