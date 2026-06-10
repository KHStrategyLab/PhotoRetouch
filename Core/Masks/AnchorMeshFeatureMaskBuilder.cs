using PhotoRetouch.AnchorMesh;
using System.IO;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record AnchorMeshFeatureMaskSet(
    MaskPlane EyeMask,
    MaskPlane EyebrowMask,
    MaskPlane LipMask,
    MaskPlane InnerMouthMask,
    MaskPlane NoseMask,
    MaskPlane NoseSkinMask,
    MaskPlane NostrilMask,
    MaskPlane SoftProtectMask,
    MaskPlane HardProtectMask,
    MaskPlane FaceGuideMask,
    IReadOnlyList<string> DebugWarnings);

public static class AnchorMeshFeatureMaskBuilder
{
    private static readonly Dictionary<string, TemplateMask?> TemplateCache = new(StringComparer.OrdinalIgnoreCase);

    public static AnchorMeshFeatureMaskSet Build(int width, int height, AnchorMeshResult? anchorMesh)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        List<string> warnings = new();
        if (anchorMesh?.IsValid != true || anchorMesh.Features is null)
        {
            warnings.Add("anchor_mesh_feature_masks_unavailable");
            return new AnchorMeshFeatureMaskSet(
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                empty,
                warnings);
        }

        AnchorMeshFeatureSet features = anchorMesh.Features;
        MaskPlane leftEye = BuildEyeProtectMask(width, height, features.LeftEye, 1.0);
        MaskPlane rightEye = BuildEyeProtectMask(width, height, features.RightEye, 1.0);
        MaskPlane eyeMask = MaskPlane.Union(leftEye, rightEye);

        MaskPlane leftBrow = BuildBrowProtectMask(width, height, features.LeftBrow, 1.0, isRightBrow: false);
        MaskPlane rightBrow = BuildBrowProtectMask(width, height, features.RightBrow, 1.0, isRightBrow: true);
        MaskPlane eyebrowMask = MaskPlane.Union(leftBrow, rightBrow);

        MaskPlane lipMask = BuildMouthProtectMask(width, height, features.LipOuter, 1.0);
        MaskPlane innerMouthMask = BuildMouthProtectMask(width, height, features.LipInner ?? features.LipOuter, 1.0, innerOnly: true);

        MaskPlane noseMask = StrokeOpenFeature(width, height, features.Nose, 0.48, GetFeatureStrokeRadius(features.Nose, 0.16, 8.0, 28.0), 14.0);
        MaskPlane noseSkinMask = MaskPlane.Multiply(noseMask, 0.55);
        MaskPlane nostrilMask = BuildNostrilMask(width, height, features.Nose);
        MaskPlane faceGuideMask = FillClosedFeature(width, height, features.FaceOutline, 0.72, 4.0);

        MaskPlane hardProtect = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            nostrilMask);
        MaskPlane softProtect = MaskPlane.Subtract(noseMask, hardProtect);

        if (hardProtect.Average() <= 0.0001)
        {
            warnings.Add("anchor_mesh_hard_protect_empty");
        }

        return new AnchorMeshFeatureMaskSet(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            noseMask,
            noseSkinMask,
            nostrilMask,
            softProtect,
            hardProtect,
            faceGuideMask,
            warnings);
    }

    private static MaskPlane BuildEyeProtectMask(int width, int height, AnchorMeshFeature? eyeFeature, double opacity)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (eyeFeature is null || eyeFeature.Points.Count == 0)
        {
            return mask;
        }

        double radiusX = Math.Clamp(eyeFeature.Width * 0.54, 8.0, 58.0);
        double radiusY = Math.Clamp(Math.Max(eyeFeature.Height * 0.42, eyeFeature.Width * 0.105), 4.0, 22.0);
        bool isRightEye = eyeFeature.Name.Equals("RightEye", StringComparison.OrdinalIgnoreCase);
        string templateName = isRightEye ? "right_eye_protect.png" : "left_eye_protect.png";
        if (AddImageTemplate(
            mask,
            templateName,
            eyeFeature.CenterX,
            eyeFeature.CenterY,
            radiusX,
            radiusY,
            eyeFeature.AngleRad,
            opacity,
            flipX: false)
            || (isRightEye && AddImageTemplate(
                mask,
                "left_eye_protect.png",
                eyeFeature.CenterX,
                eyeFeature.CenterY,
                radiusX,
                radiusY,
                eyeFeature.AngleRad,
                opacity,
                flipX: true)))
        {
            return mask;
        }

        AddTemplateShape(
            mask,
            eyeFeature.CenterX,
            eyeFeature.CenterY,
            radiusX,
            radiusY,
            eyeFeature.AngleRad,
            opacity,
            featherRadius: 2.0,
            ShapeEyeAlmond);
        return mask;
    }

    private static MaskPlane BuildBrowProtectMask(int width, int height, AnchorMeshFeature? browFeature, double opacity, bool isRightBrow)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (browFeature is null || browFeature.Points.Count == 0)
        {
            return mask;
        }

        double radius = GetFeatureStrokeRadius(browFeature, 0.35, 4.0, 14.0);
        double centerShift = isRightBrow ? -browFeature.Width * 0.03 : browFeature.Width * 0.03;
        if (AddImageTemplate(
            mask,
            isRightBrow ? "right_brow_protect.png" : "left_brow_protect.png",
            browFeature.CenterX + Math.Cos(browFeature.AngleRad) * centerShift,
            browFeature.CenterY,
            Math.Clamp(browFeature.Width * 0.56, 12.0, 72.0),
            Math.Clamp(Math.Max(browFeature.Height * 0.70, browFeature.Width * 0.095), 4.0, 18.0),
            browFeature.AngleRad,
            opacity,
            flipX: false))
        {
            return mask;
        }

        StrokeOpenFeatureInto(mask, browFeature, opacity, radius, 2.4);
        AddTemplateShape(
            mask,
            browFeature.CenterX + Math.Cos(browFeature.AngleRad) * centerShift,
            browFeature.CenterY,
            Math.Clamp(browFeature.Width * 0.56, 12.0, 72.0),
            Math.Clamp(Math.Max(browFeature.Height * 0.70, browFeature.Width * 0.095), 4.0, 18.0),
            browFeature.AngleRad,
            opacity * 0.88,
            featherRadius: 2.0,
            isRightBrow ? ShapeRightBrowTemplate : ShapeLeftBrowTemplate);
        return mask;
    }

    private static MaskPlane BuildMouthProtectMask(
        int width,
        int height,
        AnchorMeshFeature? mouthFeature,
        double opacity,
        bool innerOnly = false)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (mouthFeature is null || mouthFeature.Points.Count == 0)
        {
            return mask;
        }

        double angle = mouthFeature.AngleRad;
        double faceUpX = -Math.Sin(angle);
        double faceUpY = -Math.Cos(angle);
        double centerLift = innerOnly ? mouthFeature.Height * 0.20 : mouthFeature.Height * 0.34;
        double radiusX = Math.Clamp(mouthFeature.Width * (innerOnly ? 0.40 : 0.56), 12.0, 96.0);
        double radiusY = Math.Clamp(Math.Max(mouthFeature.Height * (innerOnly ? 0.34 : 0.54), mouthFeature.Width * (innerOnly ? 0.050 : 0.085)), 4.0, 28.0);
        if (!innerOnly &&
            AddImageTemplate(
                mask,
                "mouth_protect.png",
                mouthFeature.CenterX + faceUpX * centerLift,
                mouthFeature.CenterY + faceUpY * centerLift,
                radiusX,
                radiusY,
                angle,
                opacity,
                flipX: false))
        {
            return mask;
        }

        AddTemplateShape(
            mask,
            mouthFeature.CenterX + faceUpX * centerLift,
            mouthFeature.CenterY + faceUpY * centerLift,
            radiusX,
            radiusY,
            angle,
            opacity,
            featherRadius: innerOnly ? 1.4 : 2.4,
            innerOnly ? ShapeInnerMouthLine : ShapeMouthTemplate);
        return mask;
    }

    private static MaskPlane FillClosedFeature(int width, int height, AnchorMeshFeature? feature, double opacity, double featherRadius)
    {
        if (feature is null || feature.Points.Count < 3)
        {
            return MaskPlane.Empty(width, height);
        }

        FaceFeatureMesh mesh = ToFeatureMesh(feature);
        return MeshMaskRasterizer.FillClosedMesh(width, height, mesh, opacity, featherRadius);
    }

    private static MaskPlane StrokeOpenFeature(
        int width,
        int height,
        AnchorMeshFeature? feature,
        double opacity,
        double radius,
        double featherRadius)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (feature is null || feature.Points.Count == 0)
        {
            return mask;
        }

        double maxRadius = Math.Max(radius, radius + featherRadius);
        int left = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedX) - maxRadius - 2));
        int right = Math.Min(width - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedX) + maxRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedY) - maxRadius - 2));
        int bottom = Math.Min(height - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedY) + maxRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double distance = DistanceToFeaturePolyline(x + 0.5, y + 0.5, feature, closeLoop: feature.IsClosedLoop);
                double amount = distance <= radius
                    ? opacity
                    : opacity * (1 - SmoothStep(radius, radius + Math.Max(0.5, featherRadius), distance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }

        return mask;
    }

    private static void StrokeOpenFeatureInto(
        MaskPlane mask,
        AnchorMeshFeature feature,
        double opacity,
        double radius,
        double featherRadius)
    {
        double maxRadius = Math.Max(radius, radius + featherRadius);
        int left = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedX) - maxRadius - 2));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedX) + maxRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(feature.Points.Min(point => point.SnappedY) - maxRadius - 2));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(feature.Points.Max(point => point.SnappedY) + maxRadius + 2));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double distance = DistanceToFeaturePolyline(x + 0.5, y + 0.5, feature, closeLoop: feature.IsClosedLoop);
                double amount = distance <= radius
                    ? opacity
                    : opacity * (1 - SmoothStep(radius, radius + Math.Max(0.5, featherRadius), distance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }
    }

    private static MaskPlane BuildNostrilMask(int width, int height, AnchorMeshFeature? noseFeature)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        if (noseFeature is null)
        {
            return mask;
        }

        AnchorMeshPoint[] nostrilPoints = noseFeature.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (nostrilPoints.Length == 0)
        {
            return mask;
        }

        AnchorMeshPoint[] leftPoints = nostrilPoints
            .Where(point => point.Role.Contains("LeftNostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();
        AnchorMeshPoint[] rightPoints = nostrilPoints
            .Where(point => point.Role.Contains("RightNostril", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        double radiusX = Math.Clamp(noseFeature.Width * 0.062, 4.0, 14.0);
        double radiusY = Math.Clamp(radiusX * 0.48, 2.0, 7.5);
        AddNostrilEllipse(mask, leftPoints, noseFeature.AngleRad, radiusX, radiusY);
        AddNostrilEllipse(mask, rightPoints, noseFeature.AngleRad, radiusX, radiusY);

        return mask;
    }

    private static void AddNostrilEllipse(MaskPlane mask, IReadOnlyList<AnchorMeshPoint> points, double angle, double radiusX, double radiusY)
    {
        if (points.Count == 0)
        {
            return;
        }

        double centerX = points.Average(point => point.SnappedX);
        double centerY = points.Average(point => point.SnappedY);
        if (AddImageTemplate(mask, "nostril_protect.png", centerX, centerY, radiusX, radiusY, angle, 1.0, flipX: false))
        {
            return;
        }

        AddTemplateShape(mask, centerX, centerY, radiusX, radiusY, angle, 1.0, 1.5, ShapeEllipse);
    }

    private static bool AddImageTemplate(
        MaskPlane mask,
        string fileName,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angle,
        double opacity,
        bool flipX)
    {
        TemplateMask? template = LoadTemplate(fileName);
        if (template is null)
        {
            return false;
        }

        double padding = 2.0;
        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX - padding));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX + padding));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY - padding));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY + padding));
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double localX = (dx * cos + dy * sin) / Math.Max(0.5, radiusX);
                double localY = (-dx * sin + dy * cos) / Math.Max(0.5, radiusY);
                if (localX < -1 || localX > 1 || localY < -1 || localY > 1)
                {
                    continue;
                }

                double u = template.CenterU + (flipX ? -localX : localX) * 0.5;
                double v = template.CenterV + localY * 0.5;
                double amount = template.Sample(u, v) * opacity;
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }

        return true;
    }

    private static TemplateMask? LoadTemplate(string fileName)
    {
        lock (TemplateCache)
        {
            if (TemplateCache.TryGetValue(fileName, out TemplateMask? cached))
            {
                return cached;
            }

            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "MaskTemplates", fileName);
            if (!File.Exists(path))
            {
                TemplateCache[fileName] = null;
                return null;
            }

            BitmapDecoder decoder = BitmapDecoder.Create(
                new Uri(path, UriKind.Absolute),
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            BitmapSource source = decoder.Frames[0];
            BitmapSource bitmap = source.Format == System.Windows.Media.PixelFormats.Bgra32
                ? source
                : new FormatConvertedBitmap(source, System.Windows.Media.PixelFormats.Bgra32, null, 0);
            bitmap.Freeze();

            int width = bitmap.PixelWidth;
            int height = bitmap.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            bitmap.CopyPixels(pixels, stride, 0);
            double[] values = new double[width * height];
            double weightedX = 0;
            double weightedY = 0;
            double weightTotal = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int pixelIndex = y * stride + x * 4;
                    double alpha = pixels[pixelIndex + 3] / 255d;
                    double luma = (pixels[pixelIndex] + pixels[pixelIndex + 1] + pixels[pixelIndex + 2]) / (255d * 3d);
                    double value = alpha < 0.995
                        ? alpha
                        : 1 - luma;
                    values[y * width + x] = value;
                    if (value > 0.05)
                    {
                        weightedX += (x + 0.5) * value;
                        weightedY += (y + 0.5) * value;
                        weightTotal += value;
                    }
                }
            }

            double centerU = weightTotal > 0 ? weightedX / weightTotal / width : 0.5;
            double centerV = weightTotal > 0 ? weightedY / weightTotal / height : 0.5;
            TemplateMask template = new(width, height, values, centerU, centerV);
            TemplateCache[fileName] = template;
            return template;
        }
    }

    private static void AddTemplateShape(
        MaskPlane mask,
        double centerX,
        double centerY,
        double radiusX,
        double radiusY,
        double angle,
        double opacity,
        double featherRadius,
        Func<double, double, double> shape)
    {
        int left = Math.Max(0, (int)Math.Floor(centerX - radiusX - featherRadius - 2));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(centerX + radiusX + featherRadius + 2));
        int top = Math.Max(0, (int)Math.Floor(centerY - radiusY - featherRadius - 2));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(centerY + radiusY + featherRadius + 2));
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double localX = (dx * cos + dy * sin) / Math.Max(0.5, radiusX);
                double localY = (-dx * sin + dy * cos) / Math.Max(0.5, radiusY);
                double signedDistance = shape(localX, localY);
                double amount = signedDistance <= 0
                    ? opacity
                    : opacity * (1 - SmoothStep(0, Math.Max(0.001, featherRadius / Math.Max(radiusX, radiusY)), signedDistance));
                if (amount > mask[x, y])
                {
                    mask[x, y] = amount;
                }
            }
        }
    }

    private sealed record TemplateMask(int Width, int Height, double[] Values, double CenterU, double CenterV)
    {
        public double Sample(double u, double v)
        {
            double x = Math.Clamp(u, 0, 1) * (Width - 1);
            double y = Math.Clamp(v, 0, 1) * (Height - 1);
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            int x1 = Math.Min(Width - 1, x0 + 1);
            int y1 = Math.Min(Height - 1, y0 + 1);
            double tx = x - x0;
            double ty = y - y0;

            double top = Lerp(Values[y0 * Width + x0], Values[y0 * Width + x1], tx);
            double bottom = Lerp(Values[y1 * Width + x0], Values[y1 * Width + x1], tx);
            return Math.Clamp(Lerp(top, bottom, ty), 0, 1);
        }

        private static double Lerp(double from, double to, double amount)
        {
            return from + (to - from) * amount;
        }
    }

    private static double ShapeEllipse(double x, double y)
    {
        return Math.Sqrt(x * x + y * y) - 1;
    }

    private static double ShapeEyeAlmond(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.04)
        {
            return ax - 1.04;
        }

        double lid = Math.Pow(Math.Max(0, 1 - Math.Pow(ax, 1.72)), 0.58);
        double upper = lid * 0.58 + Math.Exp(-Math.Pow((x + 0.18) / 0.55, 2)) * 0.06;
        double lower = lid * 0.42 + Math.Exp(-Math.Pow((x - 0.05) / 0.70, 2)) * 0.04;
        if (y < 0)
        {
            return (-y / Math.Max(0.001, upper)) - 1;
        }

        return (y / Math.Max(0.001, lower)) - 1;
    }

    private static double ShapeLeftBrowTemplate(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.02)
        {
            return ax - 1.02;
        }

        double arch = -0.30 * Math.Sin((x + 1) * Math.PI * 0.5) + 0.10 * x;
        double thickness = 0.42 * (1 - SmoothStep(0.60, 1.02, ax)) + 0.15;
        return Math.Abs(y - arch) / Math.Max(0.001, thickness) - 1;
    }

    private static double ShapeRightBrowTemplate(double x, double y)
    {
        return ShapeLeftBrowTemplate(-x, y);
    }

    private static double ShapeMouthTemplate(double x, double y)
    {
        double baseShape = Math.Sqrt(x * x + Math.Pow(y / 0.72, 2)) - 1;
        double upperNotch = Math.Exp(-Math.Pow(x / 0.26, 2)) * 0.22;
        if (y < -0.18 + upperNotch)
        {
            return Math.Max(baseShape, (-0.18 + upperNotch - y) / 0.18);
        }

        return baseShape;
    }

    private static double ShapeInnerMouthLine(double x, double y)
    {
        double ax = Math.Abs(x);
        if (ax > 1.0)
        {
            return ax - 1.0;
        }

        double curve = 0.08 * Math.Cos(x * Math.PI);
        double thickness = 0.20 * (1 - SmoothStep(0.72, 1.0, ax)) + 0.05;
        return Math.Abs(y - curve) / Math.Max(0.001, thickness) - 1;
    }

    private static FaceFeatureMesh ToFeatureMesh(AnchorMeshFeature feature)
    {
        List<FeatureMeshPoint> points = feature.Points
            .Select((point, index) => new FeatureMeshPoint(index, point.SnappedX, point.SnappedY, point.Confidence, point.Role))
            .ToList();
        return new FaceFeatureMesh(ToFeatureType(feature.Name), points, feature.Confidence, "anchor_mesh_" + feature.Name);
    }

    private static FaceFeatureType ToFeatureType(string featureName)
    {
        return featureName switch
        {
            "LeftEye" or "RightEye" => FaceFeatureType.Eye,
            "LeftBrow" or "RightBrow" => FaceFeatureType.Brow,
            "Nose" => FaceFeatureType.Nose,
            _ => FaceFeatureType.Lip
        };
    }

    private static double GetFeatureStrokeRadius(AnchorMeshFeature? feature, double heightRatio, double min, double max)
    {
        if (feature is null)
        {
            return min;
        }

        return Math.Clamp(feature.Height * heightRatio, min, max);
    }

    private static double DistanceToFeaturePolyline(double x, double y, AnchorMeshFeature feature, bool closeLoop)
    {
        double best = double.MaxValue;
        int segmentCount = closeLoop ? feature.Points.Count : feature.Points.Count - 1;
        for (int index = 0; index < segmentCount; index++)
        {
            AnchorMeshPoint a = feature.Points[index];
            AnchorMeshPoint b = feature.Points[(index + 1) % feature.Points.Count];
            best = Math.Min(best, DistanceToSegment(x, y, a.SnappedX, a.SnappedY, b.SnappedX, b.SnappedY));
        }

        return best;
    }

    private static double DistanceToSegment(double px, double py, double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.0001)
        {
            return Distance(px, py, ax, ay);
        }

        double t = ((px - ax) * dx + (py - ay) * dy) / lengthSquared;
        t = Math.Clamp(t, 0, 1);
        return Distance(px, py, ax + dx * t, ay + dy * t);
    }

    private static double Distance(double ax, double ay, double bx, double by)
    {
        double dx = bx - ax;
        double dy = by - ay;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double t = Math.Clamp((value - edge0) / Math.Max(0.0001, edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
