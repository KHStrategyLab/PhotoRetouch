namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshSoftSnapper
{
    public AnchorMeshFeatureSet Snap(AnchorMeshFeatureSet aligned, FeatureMaskContourProvider? contours)
    {
        AnchorMeshFeatureSet snapped = aligned.Clone();
        if (contours is null)
        {
            return snapped;
        }

        foreach (AnchorMeshFeature feature in snapped.GetAll())
        {
            if (!contours.TryGetMask(feature.Name, out MaskPlane mask))
            {
                continue;
            }

            (float X, float Y)[] originalPositions = feature.Points
                .Select(point => (point.SnappedX, point.SnappedY))
                .ToArray();
            double maxDistance = GetMaxSnapDistance(feature.Name);
            bool movedAny = false;
            foreach (AnchorMeshPoint point in feature.Points)
            {
                if (point.IsLocked || point.SnapWeight <= 0)
                {
                    continue;
                }

                bool foundTarget = feature.Name.Equals("FaceOutline", StringComparison.OrdinalIgnoreCase)
                    ? TryFindRadialMaskBoundary(mask, feature.CenterX, feature.CenterY, point.SnappedX, point.SnappedY, maxDistance, out float targetX, out float targetY)
                    : TryFindNearestMaskBoundary(mask, point.SnappedX, point.SnappedY, maxDistance, out targetX, out targetY);
                if (!foundTarget)
                {
                    continue;
                }

                if (IsUnsafeBrowMove(feature.Name, point.SnappedY, targetY, maxDistance))
                {
                    continue;
                }

                float weight = Math.Clamp(MathF.Max(point.SnapWeight, GetMinimumSnapWeight(feature.Name)), 0.0f, 1.0f);
                point.SnappedX = Lerp(point.SnappedX, targetX, weight);
                point.SnappedY = Lerp(point.SnappedY, targetY, weight);
                point.Source = "MaskSnapped";
                point.Confidence = MathF.Min(1.0f, point.Confidence + 0.15f);
                movedAny = true;
            }

            if (movedAny)
            {
                if (feature.Name.Equals("FaceOutline", StringComparison.OrdinalIgnoreCase))
                {
                    ConstrainClosedFeatureSpacing(feature, originalPositions, 0.72f, 1.42f, 4);
                    SmoothClosedFeature(feature, 0.28f);
                    ConstrainClosedFeatureSpacing(feature, originalPositions, 0.76f, 1.36f, 2);
                }

                feature.SnapMode = "SoftSnap";
                AnchorMeshMetrics.Update(feature, feature.AngleRad);
            }
        }

        return snapped;
    }

    private static bool TryFindNearestMaskBoundary(MaskPlane mask, float x, float y, double maxDistance, out float targetX, out float targetY)
    {
        int centerX = (int)Math.Round(x);
        int centerY = (int)Math.Round(y);
        int radius = Math.Max(1, (int)Math.Ceiling(maxDistance));
        double bestDistanceSq = maxDistance * maxDistance;
        targetX = x;
        targetY = y;
        bool found = false;

        int minX = Math.Max(1, centerX - radius);
        int maxX = Math.Min(mask.Width - 2, centerX + radius);
        int minY = Math.Max(1, centerY - radius);
        int maxY = Math.Min(mask.Height - 2, centerY + radius);

        for (int yy = minY; yy <= maxY; yy++)
        {
            for (int xx = minX; xx <= maxX; xx++)
            {
                if (mask[xx, yy] < 0.35 || !IsBoundary(mask, xx, yy))
                {
                    continue;
                }

                double dx = xx - x;
                double dy = yy - y;
                double distanceSq = dx * dx + dy * dy;
                if (distanceSq > bestDistanceSq)
                {
                    continue;
                }

                bestDistanceSq = distanceSq;
                targetX = xx;
                targetY = yy;
                found = true;
            }
        }

        return found;
    }

    private static bool TryFindRadialMaskBoundary(
        MaskPlane mask,
        float centerX,
        float centerY,
        float pointX,
        float pointY,
        double maxDistance,
        out float targetX,
        out float targetY)
    {
        targetX = pointX;
        targetY = pointY;
        float dx = pointX - centerX;
        float dy = pointY - centerY;
        float radius = MathF.Sqrt(dx * dx + dy * dy);
        if (radius < 1)
        {
            return false;
        }

        float ux = dx / radius;
        float uy = dy / radius;
        double minT = Math.Max(1, radius - maxDistance);
        double maxT = radius + maxDistance;
        double bestDistance = maxDistance + 1;
        bool found = false;

        for (double t = minT; t <= maxT; t += 1.0)
        {
            int x = (int)Math.Round(centerX + ux * t);
            int y = (int)Math.Round(centerY + uy * t);
            if (x <= 0 || y <= 0 || x >= mask.Width - 1 || y >= mask.Height - 1)
            {
                continue;
            }

            if (mask[x, y] < 0.35 || !IsBoundary(mask, x, y))
            {
                continue;
            }

            double distance = Math.Abs(t - radius);
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            targetX = x;
            targetY = y;
            found = true;
        }

        return found;
    }

    private static bool IsBoundary(MaskPlane mask, int x, int y)
    {
        return mask[x - 1, y] < 0.35
            || mask[x + 1, y] < 0.35
            || mask[x, y - 1] < 0.35
            || mask[x, y + 1] < 0.35;
    }

    private static bool IsUnsafeBrowMove(string featureName, float currentY, float targetY, double maxDistance)
    {
        return featureName.Contains("Brow", StringComparison.OrdinalIgnoreCase)
            && targetY > currentY + maxDistance * 0.35;
    }

    private static double GetMaxSnapDistance(string featureName)
    {
        return featureName switch
        {
            "LeftEye" or "RightEye" => 7,
            "LeftBrow" or "RightBrow" => 14,
            "LipOuter" or "LipInner" => 20,
            "Nose" => 18,
            "FaceOutline" => 46,
            "Hairline" => 18,
            _ => 6
        };
    }

    private static float GetMinimumSnapWeight(string featureName)
    {
        return featureName switch
        {
            "FaceOutline" => 0.86f,
            "LeftEye" or "RightEye" => 0.92f,
            "LipOuter" or "LipInner" => 0.90f,
            "Nose" => 0.58f,
            "LeftBrow" or "RightBrow" => 0.48f,
            "Hairline" => 0.34f,
            _ => 0.30f
        };
    }

    private static void SmoothClosedFeature(AnchorMeshFeature feature, float amount)
    {
        if (!feature.IsClosedLoop || feature.Points.Count < 4)
        {
            return;
        }

        (float X, float Y)[] smoothed = new (float X, float Y)[feature.Points.Count];
        for (int i = 0; i < feature.Points.Count; i++)
        {
            AnchorMeshPoint previous = feature.Points[(i - 1 + feature.Points.Count) % feature.Points.Count];
            AnchorMeshPoint current = feature.Points[i];
            AnchorMeshPoint next = feature.Points[(i + 1) % feature.Points.Count];
            float averageX = (previous.SnappedX + current.SnappedX * 2 + next.SnappedX) * 0.25f;
            float averageY = (previous.SnappedY + current.SnappedY * 2 + next.SnappedY) * 0.25f;
            smoothed[i] = (
                Lerp(current.SnappedX, averageX, amount),
                Lerp(current.SnappedY, averageY, amount));
        }

        for (int i = 0; i < feature.Points.Count; i++)
        {
            feature.Points[i].SnappedX = smoothed[i].X;
            feature.Points[i].SnappedY = smoothed[i].Y;
        }
    }

    private static void ConstrainClosedFeatureSpacing(
        AnchorMeshFeature feature,
        IReadOnlyList<(float X, float Y)> originalPositions,
        float minRatio,
        float maxRatio,
        int iterations)
    {
        if (!feature.IsClosedLoop || feature.Points.Count < 4 || originalPositions.Count != feature.Points.Count)
        {
            return;
        }

        for (int pass = 0; pass < iterations; pass++)
        {
            for (int i = 0; i < feature.Points.Count; i++)
            {
                int nextIndex = (i + 1) % feature.Points.Count;
                AnchorMeshPoint first = feature.Points[i];
                AnchorMeshPoint second = feature.Points[nextIndex];
                float originalLength = Distance(originalPositions[i].X, originalPositions[i].Y, originalPositions[nextIndex].X, originalPositions[nextIndex].Y);
                if (originalLength < 1)
                {
                    continue;
                }

                float dx = second.SnappedX - first.SnappedX;
                float dy = second.SnappedY - first.SnappedY;
                float currentLength = MathF.Sqrt(dx * dx + dy * dy);
                if (currentLength < 0.001f)
                {
                    continue;
                }

                float minLength = originalLength * minRatio;
                float maxLength = originalLength * maxRatio;
                float targetLength = currentLength;
                if (currentLength < minLength)
                {
                    targetLength = minLength;
                }
                else if (currentLength > maxLength)
                {
                    targetLength = maxLength;
                }
                else
                {
                    continue;
                }

                float correction = (targetLength - currentLength) * 0.5f;
                float ux = dx / currentLength;
                float uy = dy / currentLength;
                if (!first.IsLocked)
                {
                    first.SnappedX -= ux * correction;
                    first.SnappedY -= uy * correction;
                }

                if (!second.IsLocked)
                {
                    second.SnappedX += ux * correction;
                    second.SnappedY += uy * correction;
                }
            }
        }
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = bx - ax;
        float dy = by - ay;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static float Lerp(float a, float b, float t)
    {
        return a + (b - a) * t;
    }
}
