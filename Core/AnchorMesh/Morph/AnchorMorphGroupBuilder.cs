namespace PhotoRetouch.AnchorMesh;

public static class AnchorMorphGroupBuilder
{
    public static AnchorMorphGroupSet BuildDefaultGroups()
    {
        AnchorMorphGroupSet set = new();
        set.Groups.Add(Create("LeftEyeMorphGroup", "scale", "eye_size", 36, Points("LeftEye", 16), Points("LeftBrow", 12), ["Nose_00", "Nose_08"]));
        set.Groups.Add(Create("RightEyeMorphGroup", "scale", "eye_size", 36, Points("RightEye", 16), Points("RightBrow", 12), ["Nose_00", "Nose_08"]));
        set.Groups.Add(Create("BrowMorphGroup", "vertical", "brow_height", 42, Points("LeftBrow", 12).Concat(Points("RightBrow", 12)), Points("LeftEye", 16).Concat(Points("RightEye", 16)), ["Nose_00"]));
        set.Groups.Add(Create("NoseAnchorGroup", "center", "nose_balance", 48, Points("Nose", 24), Points("LeftEye", 16).Concat(Points("RightEye", 16)), ["LeftEye_04", "RightEye_12"]));
        set.Groups.Add(Create("MouthMorphGroup", "scale", "mouth_shape", 46, Points("LipOuter", 24).Concat(Points("LipInner", 16)), Points("Nose", 24), ["Nose_08"]));
        set.Groups.Add(Create("ChinMorphGroup", "vertical", "chin_length", 70, ["FaceOutline_07", "FaceOutline_08", "FaceOutline_09"], ["LipOuter_18"], ["Nose_08", "LipOuter_18"]));
        set.Groups.Add(Create("FaceOutlineMorphGroup", "inward", "face_outline", 90, Points("FaceOutline", 32), Points("Nose", 24).Concat(Points("LipOuter", 24)), ["LeftEye_04", "RightEye_12", "Nose_08"]));
        return set;
    }

    private static AnchorMorphGroup Create(
        string name,
        string direction,
        string operation,
        float radius,
        IEnumerable<string> controlPoints,
        IEnumerable<string> falloffPoints,
        IEnumerable<string> lockedPoints)
    {
        AnchorMorphGroup group = new()
        {
            Name = name,
            Direction = direction,
            AllowedOperation = operation,
            InfluenceRadius = radius
        };
        group.ControlPoints.AddRange(controlPoints);
        group.FalloffPoints.AddRange(falloffPoints);
        group.LockedPoints.AddRange(lockedPoints);
        return group;
    }

    private static IEnumerable<string> Points(string feature, int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return $"{feature}_{i:00}";
        }
    }
}
