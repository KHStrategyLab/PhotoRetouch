namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshTemplateFactory
{
    public AnchorMeshFeatureSet CreateDefaultTemplate()
    {
        return new AnchorMeshFeatureSet
        {
            FaceOutline = CreateFaceOutline(),
            LeftEye = CreateEye("LeftEye", -0.18f, -0.21f, isLeftEye: true),
            RightEye = CreateEye("RightEye", 0.18f, -0.21f, isLeftEye: false),
            LeftPupil = CreatePupil("LeftPupil", -0.18f, -0.21f, isLeftPupil: true),
            RightPupil = CreatePupil("RightPupil", 0.18f, -0.21f, isLeftPupil: false),
            LeftBrow = CreateBrow("LeftBrow", -0.285f, -0.31f, -0.075f, -0.305f, 0.028f, isLeftBrow: true),
            RightBrow = CreateBrow("RightBrow", 0.075f, -0.305f, 0.285f, -0.31f, 0.028f, isLeftBrow: false),
            Nose = CreateNose(),
            LipOuter = CreateOuterLipAlmond(),
            LipInner = CreateInnerMouthLoop(),
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

    private static AnchorMeshFeature CreateFaceOutline()
    {
        const int count = 50;
        const float centerX = 0.0f;
        const float centerY = 0.04f;
        const float radiusX = 0.43f;
        const float radiusY = 0.58f;
        AnchorMeshFeature feature = new() { Name = "FaceOutline", IsClosedLoop = true };
        for (int i = 0; i < count; i++)
        {
            float rad = (360.0f / count * i) * MathF.PI / 180.0f;
            string role = GetFaceOutlinePointRole(i);
            feature.Points.Add(CreatePoint("FaceOutline", role, i, centerX + MathF.Cos(rad) * radiusX, centerY + MathF.Sin(rad) * radiusY, z: -0.04f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static string GetFaceOutlinePointRole(int index)
    {
        return index switch
        {
            13 => "ChinTip",
            11 or 12 => "RightChinLine",
            14 or 15 => "LeftChinLine",
            7 or 8 or 9 or 10 => "RightJawLine",
            16 or 17 or 18 or 19 => "LeftJawLine",
            20 or 21 or 22 or 23 or 24 or 25 => "LeftCheekContour",
            0 or 1 or 2 or 3 or 4 or 5 or 6 => "RightCheekContour",
            26 or 27 or 28 or 29 or 30 or 31 or 32 or 33 or 34 or 35 or 36 or 37 => "LeftUpperFaceContour",
            _ => "RightUpperFaceContour"
        };
    }

    private static AnchorMeshFeature CreateEye(string name, float centerX, float centerY, bool isLeftEye)
    {
        const int count = 16;
        AnchorMeshFeature feature = new() { Name = name, IsClosedLoop = true };
        for (int i = 0; i < count; i++)
        {
            float rad = (360.0f / count * i) * MathF.PI / 180.0f;
            string role = GetEyePointRole(i, isLeftEye);
            feature.Points.Add(CreatePoint(name, role, i, centerX + MathF.Cos(rad) * 0.095f, centerY + MathF.Sin(rad) * 0.042f, z: 0.06f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static string GetEyePointRole(int index, bool isLeftEye)
    {
        return index switch
        {
            0 => isLeftEye ? "LeftEyeInnerCorner" : "RightEyeOuterCorner",
            4 => isLeftEye ? "LeftEyeLowerLidCenter" : "RightEyeLowerLidCenter",
            8 => isLeftEye ? "LeftEyeOuterCorner" : "RightEyeInnerCorner",
            12 => isLeftEye ? "LeftEyeUpperLidCenter" : "RightEyeUpperLidCenter",
            _ => isLeftEye ? "LeftEyeContour" : "RightEyeContour"
        };
    }

    private static AnchorMeshFeature CreatePupil(string name, float centerX, float centerY, bool isLeftPupil)
    {
        const int count = 12;
        AnchorMeshFeature feature = new() { Name = name, IsClosedLoop = true };
        for (int i = 0; i < count; i++)
        {
            float rad = (360.0f / count * i) * MathF.PI / 180.0f;
            string role = GetPupilPointRole(i, isLeftPupil);
            feature.Points.Add(CreatePoint(name, role, i, centerX + MathF.Cos(rad) * 0.030f, centerY + MathF.Sin(rad) * 0.030f, z: 0.08f, anchor: i == 0 || i == 3 || i == 6 || i == 9));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static string GetPupilPointRole(int index, bool isLeftPupil)
    {
        return index switch
        {
            0 => isLeftPupil ? "LeftPupilRightEdge" : "RightPupilRightEdge",
            3 => isLeftPupil ? "LeftPupilBottomEdge" : "RightPupilBottomEdge",
            6 => isLeftPupil ? "LeftPupilLeftEdge" : "RightPupilLeftEdge",
            9 => isLeftPupil ? "LeftPupilTopEdge" : "RightPupilTopEdge",
            _ => isLeftPupil ? "LeftPupilCircle" : "RightPupilCircle"
        };
    }

    private static AnchorMeshFeature CreateBrow(string name, float startX, float startY, float endX, float endY, float arch, bool isLeftBrow)
    {
        const int contourCount = 15;
        AnchorMeshFeature feature = new() { Name = name, IsClosedLoop = true };
        float dx = endX - startX;
        float dy = endY - startY;
        float length = MathF.Max(0.0001f, MathF.Sqrt(dx * dx + dy * dy));
        float downX = -dy / length;
        float downY = dx / length;

        for (int i = 0; i < contourCount; i++)
        {
            float t = (float)i / (contourCount - 1);
            (float centerX, float centerY, float thickness) = EvaluateBrowBundleCenter(startX, startY, endX, endY, arch, t, isLeftBrow);
            string role = GetBrowShapePointRole(i, t, isUpper: true, isLeftBrow: isLeftBrow);
            feature.Points.Add(CreatePoint(name, role, i, centerX - downX * thickness * 0.42f, centerY - downY * thickness * 0.42f, z: 0.045f));
        }

        for (int j = 0; j < contourCount; j++)
        {
            float t = 1.0f - (float)j / (contourCount - 1);
            (float centerX, float centerY, float thickness) = EvaluateBrowBundleCenter(startX, startY, endX, endY, arch, t, isLeftBrow);
            string role = GetBrowShapePointRole(contourCount + j, t, isUpper: false, isLeftBrow: isLeftBrow);
            feature.Points.Add(CreatePoint(name, role, contourCount + j, centerX + downX * thickness * 0.58f, centerY + downY * thickness * 0.58f, z: 0.035f));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static (float X, float Y, float Thickness) EvaluateBrowBundleCenter(float startX, float startY, float endX, float endY, float arch, float t, bool isLeftBrow)
    {
        float x = Lerp(startX, endX, t);
        float y = Lerp(startY, endY, t) - MathF.Sin(t * MathF.PI) * arch;
        float innerness = isLeftBrow ? t : 1.0f - t;
        float headThickness = 0.026f;
        float tailThickness = 0.013f;
        float bodyFullness = MathF.Sin(t * MathF.PI) * 0.006f;
        float thickness = Lerp(tailThickness, headThickness, innerness) + bodyFullness;
        return (x, y, thickness);
    }

    private static string GetBrowShapePointRole(int index, float t, bool isUpper, bool isLeftBrow)
    {
        float innerness = isLeftBrow ? t : 1.0f - t;
        float outerness = 1.0f - innerness;
        float archT = isLeftBrow ? 0.43f : 0.57f;
        string side = isLeftBrow ? "Left" : "Right";
        string edge = isUpper ? "Upper" : "Lower";

        if (innerness >= 0.96f)
        {
            return side + "Brow" + edge + "InnerEnd";
        }

        if (outerness >= 0.96f)
        {
            return side + "Brow" + edge + "OuterEnd";
        }

        if (MathF.Abs(t - archT) <= 0.04f)
        {
            return side + "Brow" + edge + "Arch";
        }

        if (innerness > 0.70f)
        {
            return side + "Brow" + edge + "HeadContour";
        }

        if (outerness > 0.70f)
        {
            return side + "Brow" + edge + "TailContour";
        }

        return side + "Brow" + edge + "BodyContour";
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

    private static AnchorMeshFeature CreateOuterLipAlmond()
    {
        const int count = 24;
        const float centerX = 0.0f;
        const float centerY = 0.31f;
        const float radiusX = 0.18f;
        const float radiusY = 0.074f;
        AnchorMeshFeature feature = new() { Name = "LipOuter", IsClosedLoop = true };
        for (int i = 0; i < count; i++)
        {
            float rad = (360.0f / count * i) * MathF.PI / 180.0f;
            float sin = MathF.Sin(rad);
            float yScale = 0.38f + 0.62f * MathF.Pow(MathF.Abs(sin), 0.72f);
            string role = GetOuterLipPointRole(i);
            bool isAnchor = i is 0 or 6 or 12 or 18;
            feature.Points.Add(CreatePoint(
                "LipOuter",
                role,
                i,
                centerX + MathF.Cos(rad) * radiusX,
                centerY + sin * radiusY * yScale,
                snapWeight: 0.42f,
                z: 0.10f,
                anchor: isAnchor));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static string GetOuterLipPointRole(int index)
    {
        return index switch
        {
            0 => "MouthRightCorner",
            1 or 2 or 3 or 4 or 5 => "LowerLipRightCurve",
            6 => "LowerLipBottomCenter",
            7 or 8 or 9 or 10 or 11 => "LowerLipLeftCurve",
            12 => "MouthLeftCorner",
            13 or 14 => "UpperLipLeftPeak",
            15 or 16 => "UpperLipCupidLeft",
            17 or 19 => "UpperLipCupidBow",
            18 => "UpperLipTopCenter",
            20 or 21 => "UpperLipCupidRight",
            22 or 23 => "UpperLipRightPeak",
            _ => "LipOuter"
        };
    }

    private static AnchorMeshFeature CreateInnerMouthLoop()
    {
        const int count = 16;
        const float centerX = 0.0f;
        const float centerY = 0.318f;
        const float radiusX = 0.108f;
        const float radiusY = 0.020f;
        AnchorMeshFeature feature = new() { Name = "LipInner", IsClosedLoop = true };
        for (int i = 0; i < count; i++)
        {
            float rad = (360.0f / count * i) * MathF.PI / 180.0f;
            string role = GetInnerMouthPointRole(i);
            bool isAnchor = i is 0 or 4 or 8 or 12;
            feature.Points.Add(CreatePoint(
                "LipInner",
                role,
                i,
                centerX + MathF.Cos(rad) * radiusX,
                centerY + MathF.Sin(rad) * radiusY,
                snapWeight: 0.40f,
                z: 0.09f,
                anchor: isAnchor));
        }

        AnchorMeshMetrics.Update(feature, 0);
        return feature;
    }

    private static string GetInnerMouthPointRole(int index)
    {
        return index switch
        {
            0 => "MouthInnerRight",
            4 => "MouthInnerBottom",
            8 => "MouthInnerLeft",
            12 => "MouthInnerTop",
            1 or 2 or 3 => "InnerMouthLowerRight",
            5 or 6 or 7 => "InnerMouthLowerLeft",
            9 or 10 or 11 => "InnerMouthUpperLeft",
            13 or 14 or 15 => "InnerMouthUpperRight",
            _ => "InnerMouth"
        };
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
        AddPoint(feature, "NoseTipLeftSupport", -0.032f, 0.11f, 0.22f);
        AddPoint(feature, "NoseTipTriangleApex", 0.0f, 0.13f, 0.25f, true);
        AddPoint(feature, "NoseTipRightSupport", 0.032f, 0.11f, 0.22f);
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
        float[] xs = [-0.18f, -0.13f, -0.075f, -0.025f, 0.025f, 0.075f, 0.13f, 0.18f];
        for (int i = 0; i < xs.Length; i++)
        {
            feature.Points.Add(CreatePoint("Neck", "Neck", i, xs[i], 0.68f + MathF.Abs(xs[i]) * 0.08f, 0.05f, z: -0.14f));
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
