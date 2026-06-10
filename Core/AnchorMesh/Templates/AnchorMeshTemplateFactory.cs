namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshTemplateFactory
{
    public AnchorMeshFeatureSet CreateDefaultTemplate()
    {
        return new AnchorMeshFeatureSet
        {
            FaceOutline = CreateEllipse("FaceOutline", "Jaw", 60, 0.0f, 0.04f, 0.43f, 0.58f, true),
            LeftEye = CreateEllipse("LeftEye", "EyeProtect", 16, -0.18f, -0.21f, 0.095f, 0.042f, true, z: 0.06f),
            RightEye = CreateEllipse("RightEye", "EyeProtect", 16, 0.18f, -0.21f, 0.095f, 0.042f, true, z: 0.06f),
            LeftBrow = CreateCurve("LeftBrow", "BrowProtect", 12, -0.285f, -0.31f, -0.075f, -0.305f, 0.028f, false),
            RightBrow = CreateCurve("RightBrow", "BrowProtect", 12, 0.075f, -0.305f, 0.285f, -0.31f, 0.028f, false),
            Nose = CreateNose(),
            LipOuter = CreateEllipse("LipOuter", "LipOuter", 24, 0.0f, 0.31f, 0.18f, 0.075f, true, z: 0.10f),
            LipInner = CreateEllipse("LipInner", "LipInner", 16, 0.0f, 0.315f, 0.105f, 0.028f, true, z: 0.09f),
            Hairline = CreateHairline(),
            Neck = CreateNeck(),
            ShirtShoulder = CreateShoulders()
        };
    }

    private static AnchorMeshFeature CreateEllipse(
        string name,
        string role,
        int count,
        float centerX,
        float centerY,
        float radiusX,
        float radiusY,
        bool closed,
        float startDeg = 0,
        float sweepDeg = 360,
        float z = -0.04f)
    {
        AnchorMeshFeature feature = new() { Name = name, IsClosedLoop = closed };
        float step = count == 1 ? 0 : sweepDeg / (closed ? count : count - 1);
        for (int i = 0; i < count; i++)
        {
            float rad = (startDeg + step * i) * MathF.PI / 180.0f;
            feature.Points.Add(CreatePoint(name, role, i, centerX + MathF.Cos(rad) * radiusX, centerY + MathF.Sin(rad) * radiusY, z: z));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static AnchorMeshFeature CreateCurve(string name, string role, int count, float startX, float startY, float endX, float endY, float arch, bool closed)
    {
        AnchorMeshFeature feature = new() { Name = name, IsClosedLoop = closed };
        for (int i = 0; i < count; i++)
        {
            float t = count == 1 ? 0 : (float)i / (count - 1);
            float x = Lerp(startX, endX, t);
            float y = Lerp(startY, endY, t) - MathF.Sin(t * MathF.PI) * arch;
            feature.Points.Add(CreatePoint(name, role, i, x, y, z: 0.04f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static AnchorMeshFeature CreateNose()
    {
        AnchorMeshFeature feature = new() { Name = "Nose", IsClosedLoop = false };
        AddPoint(feature, "Bridge", 0.0f, -0.18f, 0.12f, true);
        AddPoint(feature, "Bridge", -0.018f, -0.12f, 0.13f);
        AddPoint(feature, "Bridge", 0.018f, -0.12f, 0.13f);
        AddPoint(feature, "Bridge", -0.028f, -0.05f, 0.15f);
        AddPoint(feature, "Bridge", 0.028f, -0.05f, 0.15f);
        AddPoint(feature, "NoseSide", -0.055f, 0.04f, 0.13f);
        AddPoint(feature, "NoseSide", 0.055f, 0.04f, 0.13f);
        AddPoint(feature, "Tip", -0.032f, 0.11f, 0.22f);
        AddPoint(feature, "Tip", 0.0f, 0.13f, 0.25f, true);
        AddPoint(feature, "Tip", 0.032f, 0.11f, 0.22f);
        AddPoint(feature, "LeftWing", -0.092f, 0.105f, 0.14f);
        AddPoint(feature, "LeftWing", -0.085f, 0.145f, 0.13f);
        AddPoint(feature, "LeftWing", -0.060f, 0.166f, 0.12f);
        AddPoint(feature, "RightWing", 0.060f, 0.166f, 0.12f);
        AddPoint(feature, "RightWing", 0.085f, 0.145f, 0.13f);
        AddPoint(feature, "RightWing", 0.092f, 0.105f, 0.14f);
        AddPoint(feature, "LeftNostril", -0.055f, 0.153f, 0.10f);
        AddPoint(feature, "LeftNostril", -0.035f, 0.158f, 0.11f);
        AddPoint(feature, "LeftNostril", -0.045f, 0.177f, 0.09f);
        AddPoint(feature, "RightNostril", 0.035f, 0.158f, 0.11f);
        AddPoint(feature, "RightNostril", 0.055f, 0.153f, 0.10f);
        AddPoint(feature, "RightNostril", 0.045f, 0.177f, 0.09f);
        AddPoint(feature, "NoseSide", -0.026f, 0.195f, 0.10f);
        AddPoint(feature, "NoseSide", 0.026f, 0.195f, 0.10f);

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static AnchorMeshFeature CreateHairline()
    {
        AnchorMeshFeature feature = new() { Name = "Hairline", IsClosedLoop = false };
        for (int i = 0; i < 20; i++)
        {
            float t = (float)i / 19;
            float x = Lerp(-0.38f, 0.38f, t);
            float y = -0.52f - MathF.Sin(t * MathF.PI) * 0.055f + MathF.Sin(t * MathF.PI * 4) * 0.01f;
            feature.Points.Add(CreatePoint("Hairline", "Hairline", i, x, y, 0.1f, z: -0.08f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static AnchorMeshFeature CreateNeck()
    {
        AnchorMeshFeature feature = new() { Name = "Neck", IsClosedLoop = false };
        float[] xs = [-0.22f, -0.155f, -0.095f, -0.032f, 0.032f, 0.095f, 0.155f, 0.22f];
        for (int i = 0; i < xs.Length; i++)
        {
            feature.Points.Add(CreatePoint("Neck", "JawBottom", i, xs[i], 0.54f, 0.05f, z: -0.14f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static AnchorMeshFeature CreateShoulders()
    {
        AnchorMeshFeature feature = new() { Name = "ShirtShoulder", IsClosedLoop = false };
        for (int i = 0; i < 12; i++)
        {
            float t = (float)i / 11;
            float x = Lerp(-0.62f, 0.62f, t);
            float y = 0.86f + MathF.Abs(t - 0.5f) * 0.12f;
            feature.Points.Add(CreatePoint("ShirtShoulder", "ShirtShoulder", i, x, y, 0.0f, z: -0.20f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static void AddPoint(AnchorMeshFeature feature, string role, float x, float y, float z, bool anchor = false)
    {
        feature.Points.Add(CreatePoint(feature.Name, role, feature.Points.Count, x, y, z: z, anchor: anchor));
    }

    private static AnchorMeshPoint CreatePoint(string featureName, string role, int index, float x, float y, float snapWeight = 0.35f, float z = 0, bool anchor = false)
    {
        return new AnchorMeshPoint
        {
            Name = $"{featureName}_{index:00}",
            FeatureName = featureName,
            Role = role,
            Index = index,
            TemplateX = x,
            TemplateY = y,
            TemplateZ = z,
            PoseX = x,
            PoseY = y,
            PoseZ = z,
            ProjectedX = x,
            ProjectedY = y,
            ImageX = x,
            ImageY = y,
            SnappedX = x,
            SnappedY = y,
            Confidence = 0.35f,
            SnapWeight = snapWeight,
            IsAnchor = anchor,
            Source = "Template"
        };
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
