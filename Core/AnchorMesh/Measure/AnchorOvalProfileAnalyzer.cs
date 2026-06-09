namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorOvalProfileAnalyzer
{
    private const int SampleCount = 101;

    public AnchorOvalProfileMetrics Analyze(AnchorMeshFeature? faceOutline)
    {
        AnchorOvalProfileMetrics metrics = new();
        if (faceOutline is null || faceOutline.Points.Count < 8)
        {
            metrics.Warnings.Add("face_outline_missing_for_oval_profile");
            return metrics;
        }

        List<(float X, float Y)> local = ToLocalPoints(faceOutline);
        float minY = local.Min(point => point.Y);
        float maxY = local.Max(point => point.Y);
        float height = maxY - minY;
        if (height <= 1)
        {
            metrics.Warnings.Add("face_outline_height_invalid_for_oval_profile");
            return metrics;
        }

        float[] widths = new float[SampleCount];
        float[] lefts = new float[SampleCount];
        float[] rights = new float[SampleCount];
        for (int i = 0; i < SampleCount; i++)
        {
            float yNorm = i / (float)(SampleCount - 1);
            float y = minY + height * yNorm;
            (lefts[i], rights[i], widths[i]) = MeasureWidthAtY(local, y);
        }

        metrics.FaceHeight = height;
        metrics.WMax = widths.Max();
        if (metrics.WMax <= 1)
        {
            metrics.Warnings.Add("face_outline_width_invalid_for_oval_profile");
            return metrics;
        }

        int maxIndex = Array.IndexOf(widths, metrics.WMax);
        metrics.YMax = maxIndex / (float)(SampleCount - 1);
        metrics.WTemple = AverageBand(widths, 0.18f, 0.24f);
        metrics.WCheek = MaxBand(widths, 0.38f, 0.46f);
        metrics.WJaw = AverageBand(widths, 0.70f, 0.78f);
        metrics.WChin = AverageBand(widths, 0.92f, 0.97f);
        metrics.FaceAspectRatio = SafeRatio(metrics.FaceHeight, metrics.WMax);
        metrics.TempleRatio = SafeRatio(metrics.WTemple, metrics.WMax);
        metrics.JawRatio = SafeRatio(metrics.WJaw, metrics.WMax);
        metrics.ChinRatio = SafeRatio(metrics.WChin, metrics.WMax);
        metrics.SymmetryError = CalculateSymmetryError(lefts, rights, metrics.WMax);
        metrics.SmoothnessError = CalculateSmoothnessError(widths, metrics.WMax);
        metrics.JawAngleLeft = CalculateJawAngle(local, minY, height, leftSide: true);
        metrics.JawAngleRight = CalculateJawAngle(local, minY, height, leftSide: false);

        metrics.AspectScore = RangeScore(metrics.FaceAspectRatio, 1.30f, 1.50f, 0.18f);
        metrics.MaxWidthPositionScore = RangeScore(metrics.YMax, 0.38f, 0.46f, 0.12f);
        metrics.TempleRatioScore = RangeScore(metrics.TempleRatio, 0.84f, 0.92f, 0.10f);
        metrics.JawRatioScore = RangeScore(metrics.JawRatio, 0.72f, 0.82f, 0.12f);
        metrics.ChinRatioScore = RangeScore(metrics.ChinRatio, 0.36f, 0.52f, 0.14f);
        metrics.SymmetryScore = ErrorScore(metrics.SymmetryError, 0.03f, 0.09f);
        metrics.SmoothnessScore = ErrorScore(metrics.SmoothnessError, 0.020f, 0.075f) * MonotonicAfterMaxScore(widths, maxIndex);
        metrics.JawAngleScore = (
            RangeScore(metrics.JawAngleLeft, 125.0f, 145.0f, 20.0f) +
            RangeScore(metrics.JawAngleRight, 125.0f, 145.0f, 20.0f)) * 0.5f;
        metrics.OvalScore =
            metrics.AspectScore * 0.15f +
            metrics.MaxWidthPositionScore * 0.15f +
            metrics.TempleRatioScore * 0.10f +
            metrics.JawRatioScore * 0.15f +
            metrics.ChinRatioScore * 0.15f +
            metrics.SymmetryScore * 0.10f +
            metrics.SmoothnessScore * 0.10f +
            metrics.JawAngleScore * 0.10f;
        metrics.Classification = Classify(metrics.OvalScore);
        return metrics;
    }

    private static List<(float X, float Y)> ToLocalPoints(AnchorMeshFeature faceOutline)
    {
        float cos = MathF.Cos(faceOutline.AngleRad);
        float sin = MathF.Sin(faceOutline.AngleRad);
        List<(float X, float Y)> local = new(faceOutline.Points.Count);
        foreach (AnchorMeshPoint point in faceOutline.Points)
        {
            float dx = point.SnappedX - faceOutline.CenterX;
            float dy = point.SnappedY - faceOutline.CenterY;
            local.Add((dx * cos + dy * sin, -dx * sin + dy * cos));
        }

        return local;
    }

    private static (float Left, float Right, float Width) MeasureWidthAtY(IReadOnlyList<(float X, float Y)> points, float y)
    {
        List<float> intersections = new();
        for (int i = 0; i < points.Count; i++)
        {
            (float x1, float y1) = points[i];
            (float x2, float y2) = points[(i + 1) % points.Count];
            if (Math.Abs(y2 - y1) < 0.001f)
            {
                continue;
            }

            bool crosses = (y1 <= y && y2 > y) || (y2 <= y && y1 > y);
            if (!crosses)
            {
                continue;
            }

            float t = (y - y1) / (y2 - y1);
            intersections.Add(x1 + (x2 - x1) * t);
        }

        if (intersections.Count < 2)
        {
            return (0, 0, 0);
        }

        intersections.Sort();
        float left = intersections.First();
        float right = intersections.Last();
        return (left, right, MathF.Max(0, right - left));
    }

    private static float AverageBand(IReadOnlyList<float> values, float start, float end)
    {
        (int first, int last) = BandIndexes(values.Count, start, end);
        float sum = 0;
        int count = 0;
        for (int i = first; i <= last; i++)
        {
            if (values[i] <= 0)
            {
                continue;
            }

            sum += values[i];
            count++;
        }

        return count == 0 ? 0 : sum / count;
    }

    private static float MaxBand(IReadOnlyList<float> values, float start, float end)
    {
        (int first, int last) = BandIndexes(values.Count, start, end);
        float max = 0;
        for (int i = first; i <= last; i++)
        {
            max = MathF.Max(max, values[i]);
        }

        return max;
    }

    private static (int First, int Last) BandIndexes(int count, float start, float end)
    {
        int first = Math.Clamp((int)MathF.Round(start * (count - 1)), 0, count - 1);
        int last = Math.Clamp((int)MathF.Round(end * (count - 1)), first, count - 1);
        return (first, last);
    }

    private static float CalculateSymmetryError(IReadOnlyList<float> lefts, IReadOnlyList<float> rights, float wMax)
    {
        float sum = 0;
        int count = 0;
        for (int i = 0; i < lefts.Count; i++)
        {
            float width = rights[i] - lefts[i];
            if (width <= 1)
            {
                continue;
            }

            sum += MathF.Abs((lefts[i] + rights[i]) * 0.5f) / wMax;
            count++;
        }

        return count == 0 ? 1 : sum / count;
    }

    private static float CalculateSmoothnessError(IReadOnlyList<float> widths, float wMax)
    {
        float sum = 0;
        int count = 0;
        for (int i = 2; i < widths.Count - 2; i++)
        {
            if (widths[i - 1] <= 0 || widths[i] <= 0 || widths[i + 1] <= 0)
            {
                continue;
            }

            float secondDifference = widths[i - 1] - widths[i] * 2 + widths[i + 1];
            sum += MathF.Abs(secondDifference) / wMax;
            count++;
        }

        return count == 0 ? 1 : sum / count;
    }

    private static float CalculateJawAngle(IReadOnlyList<(float X, float Y)> points, float minY, float height, bool leftSide)
    {
        (float X, float Y) cheek = SidePointAtY(points, minY + height * 0.52f, leftSide);
        (float X, float Y) jaw = SidePointAtY(points, minY + height * 0.76f, leftSide);
        (float X, float Y) chin = SidePointAtY(points, minY + height * 0.96f, leftSide);
        float ax = cheek.X - jaw.X;
        float ay = cheek.Y - jaw.Y;
        float bx = chin.X - jaw.X;
        float by = chin.Y - jaw.Y;
        float lengthA = MathF.Sqrt(ax * ax + ay * ay);
        float lengthB = MathF.Sqrt(bx * bx + by * by);
        if (lengthA < 0.001f || lengthB < 0.001f)
        {
            return 0;
        }

        float dot = Math.Clamp((ax * bx + ay * by) / (lengthA * lengthB), -1.0f, 1.0f);
        return MathF.Acos(dot) * 180.0f / MathF.PI;
    }

    private static (float X, float Y) SidePointAtY(IReadOnlyList<(float X, float Y)> points, float y, bool leftSide)
    {
        (float left, float right, _) = MeasureWidthAtY(points, y);
        return (leftSide ? left : right, y);
    }

    private static float RangeScore(float value, float min, float max, float falloff)
    {
        if (value >= min && value <= max)
        {
            return 1.0f;
        }

        float distance = value < min ? min - value : value - max;
        return Math.Clamp(1.0f - distance / Math.Max(0.0001f, falloff), 0.0f, 1.0f);
    }

    private static float ErrorScore(float error, float good, float bad)
    {
        if (error <= good)
        {
            return 1.0f;
        }

        return Math.Clamp(1.0f - (error - good) / Math.Max(0.0001f, bad - good), 0.0f, 1.0f);
    }

    private static float MonotonicAfterMaxScore(IReadOnlyList<float> widths, int maxIndex)
    {
        int violations = 0;
        int count = 0;
        for (int i = Math.Max(maxIndex + 1, 1); i < widths.Count; i++)
        {
            if (widths[i] <= 0 || widths[i - 1] <= 0)
            {
                continue;
            }

            if (widths[i] > widths[i - 1] * 1.012f)
            {
                violations++;
            }

            count++;
        }

        if (count == 0)
        {
            return 0;
        }

        return Math.Clamp(1.0f - violations / Math.Max(1.0f, count * 0.22f), 0.0f, 1.0f);
    }

    private static string Classify(float ovalScore)
    {
        return ovalScore switch
        {
            >= 0.85f => "OvalVeryClose",
            >= 0.70f => "OvalLeaning",
            >= 0.55f => "Neutral",
            _ => "OtherFaceShapeLeaning"
        };
    }

    private static float SafeRatio(float value, float divisor)
    {
        return divisor <= 0.001f ? 0 : value / divisor;
    }
}
