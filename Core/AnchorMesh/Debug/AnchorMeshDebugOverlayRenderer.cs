using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaPen = System.Windows.Media.Pen;
using MediaSolidColorBrush = System.Windows.Media.SolidColorBrush;
using WpfFlowDirection = System.Windows.FlowDirection;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshDebugOverlayRenderer
{
    private readonly AnchorMeshTemplateFactory _templateFactory = new();

    public void SaveTemplateOverlay(string path, int width = 1200, int height = 1400)
    {
        AnchorMeshFeatureSet template = _templateFactory.CreateDefaultTemplate();
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawRectangle(new MediaSolidColorBrush(MediaColor.FromRgb(22, 24, 28)), null, new WpfRect(0, 0, width, height));
            DrawTemplateGrid(context, width, height);
            DrawTemplateFeatures(context, template, width, height);
        }

        SaveVisual(path, visual, width, height);
    }

    public void SaveYuNetAlignedOverlay(BitmapSource source, AnchorMeshResult result, string path)
    {
        SaveFeatureOverlay(source, result.YuNetAlignedFeatures ?? result.Features, path, "YuNet aligned");
    }

    public void SaveSnappedOverlay(BitmapSource source, AnchorMeshResult result, string path)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(source, new WpfRect(0, 0, width, height));
            DrawFitBoxes(context, result);
            DrawEyeCenterAxisGuide(context, result, width);
            DrawEyeCenterPerpendicularGuide(context, result, width, height);
            DrawJawStartGuide(context, result, width);
            DrawFirstDarkThickSegmentGuide(context, source, result, height);
            DrawFeatures(context, result.Features, 1.0, 1.0);
            DrawLabel(context, "K-AnchorMesh snapped");
        }

        SaveVisual(path, visual, width, height);
    }

    public void SaveTopologyOverlay(BitmapSource source, AnchorMeshResult result, string path)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(source, new WpfRect(0, 0, width, height));
            DrawFitBoxes(context, result);
            DrawEyeCenterAxisGuide(context, result, width);
            DrawEyeCenterPerpendicularGuide(context, result, width, height);
            DrawJawStartGuide(context, result, width);
            DrawFirstDarkThickSegmentGuide(context, source, result, height);
            DrawTopologyEdges(context, result.Features, result.TopologyEdges);
            DrawFeatures(context, result.Features, 1.0, 1.0);
            DrawLabel(context, "K-AnchorMesh topology");
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        SaveBitmap(path, bitmap);
    }

    private static void DrawEyeCenterAxisGuide(DrawingContext context, AnchorMeshResult result, int width)
    {
        if (result.YuNetAnchors is null)
        {
            return;
        }

        System.Drawing.PointF leftEye = result.YuNetAnchors.LeftEye;
        System.Drawing.PointF rightEye = result.YuNetAnchors.RightEye;
        float dx = rightEye.X - leftEye.X;
        if (MathF.Abs(dx) < 0.001f)
        {
            return;
        }

        float slope = (rightEye.Y - leftEye.Y) / dx;
        float startY = leftEye.Y + slope * (0 - leftEye.X);
        float endY = leftEye.Y + slope * (width - leftEye.X);
        MediaPen guidePen = new(new MediaSolidColorBrush(MediaColor.FromArgb(245, 40, 235, 105)), 1.0);
        context.DrawLine(guidePen, new WpfPoint(0, startY), new WpfPoint(width, endY));
    }

    private static void DrawJawStartGuide(DrawingContext context, AnchorMeshResult result, int width)
    {
        if (result.YuNetAnchors is null ||
            !TryGetEyeAxisDownVector(result, out float downX, out float downY))
        {
            return;
        }

        WpfPoint browCenter = EstimateBrowCenter(result);
        WpfPoint nostrilCenter = EstimateNostrilCenter(result);
        float guideDistance = Distance((float)browCenter.X, (float)browCenter.Y, (float)nostrilCenter.X, (float)nostrilCenter.Y);
        if (guideDistance <= 1)
        {
            return;
        }

        WpfPoint guidePoint = new(nostrilCenter.X + downX * guideDistance, nostrilCenter.Y + downY * guideDistance);
        MediaPen crossPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(245, 40, 235, 105)), 1.0);
        MediaBrush guideBrush = new MediaSolidColorBrush(MediaColor.FromArgb(245, 40, 235, 105));
        float crossHalfLength = MathF.Max(24.0f, result.YuNetAnchors.EyeDistance * 0.22f);
        WpfPoint crossStart = new(guidePoint.X - downX * crossHalfLength, guidePoint.Y - downY * crossHalfLength);
        WpfPoint crossEnd = new(guidePoint.X + downX * crossHalfLength, guidePoint.Y + downY * crossHalfLength);
        WpfPoint sideStart = new(guidePoint.X - downY * crossHalfLength, guidePoint.Y + downX * crossHalfLength);
        WpfPoint sideEnd = new(guidePoint.X + downY * crossHalfLength, guidePoint.Y - downX * crossHalfLength);

        context.DrawLine(crossPen, crossStart, crossEnd);
        context.DrawLine(crossPen, sideStart, sideEnd);
        context.DrawEllipse(guideBrush, null, guidePoint, 4.0, 4.0);
    }

    private static WpfPoint EstimateBrowCenter(AnchorMeshResult result)
    {
        List<AnchorMeshFeature> brows = new();
        if (result.Features?.LeftBrow is { Points.Count: > 0 } leftBrow)
        {
            brows.Add(leftBrow);
        }

        if (result.Features?.RightBrow is { Points.Count: > 0 } rightBrow)
        {
            brows.Add(rightBrow);
        }

        if (brows.Count == 0)
        {
            System.Drawing.PointF eyeCenter = result.YuNetAnchors?.EyeCenter ?? default;
            return new WpfPoint(eyeCenter.X, eyeCenter.Y);
        }

        double x = 0;
        double y = 0;
        double weight = 0;
        foreach (AnchorMeshFeature brow in brows)
        {
            double browWeight = Math.Max(1, brow.Points.Count);
            x += brow.CenterX * browWeight;
            y += brow.CenterY * browWeight;
            weight += browWeight;
        }

        return weight <= 0 ? default : new WpfPoint(x / weight, y / weight);
    }

    private static WpfPoint EstimateNostrilCenter(AnchorMeshResult result)
    {
        AnchorMeshFeature? nose = result.Features?.Nose;
        if (nose is null || nose.Points.Count == 0)
        {
            System.Drawing.PointF noseTip = result.YuNetAnchors?.NoseTip ?? default;
            return new WpfPoint(noseTip.X, noseTip.Y);
        }

        List<AnchorMeshPoint> nostrils = nose.Points
            .Where(point => point.Role.Contains("Nostril", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (nostrils.Count == 0)
        {
            return new WpfPoint(nose.CenterX, nose.CenterY);
        }

        double x = 0;
        double y = 0;
        foreach (AnchorMeshPoint point in nostrils)
        {
            x += point.SnappedX;
            y += point.SnappedY;
        }

        return new WpfPoint(x / nostrils.Count, y / nostrils.Count);
    }

    private static void DrawFirstDarkThickSegmentGuide(DrawingContext context, BitmapSource source, AnchorMeshResult result, int height)
    {
        if (!TryFindLeftEyeDarkRunPoint(source, result, out WpfPoint segmentPoint))
        {
            if (TryGetLeftEyeScanStartPoint(source, result, out WpfPoint scanStart))
            {
                MediaBrush missBrush = new MediaSolidColorBrush(MediaColor.FromArgb(245, 255, 70, 40));
                context.DrawEllipse(missBrush, null, scanStart, 5.0, 5.0);
            }

            return;
        }

        if (!TryGetEyeAxisDownVector(result, out float downX, out float downY))
        {
            return;
        }

        float guideLength = MathF.Max(source.PixelWidth, source.PixelHeight) * 1.5f;
        MediaPen shadowPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(170, 0, 0, 0)), 4.0);
        MediaPen guidePen = new(new MediaSolidColorBrush(MediaColor.FromArgb(245, 255, 70, 40)), 1.6);
        WpfPoint start = new(segmentPoint.X - downX * guideLength, segmentPoint.Y - downY * guideLength);
        WpfPoint end = new(segmentPoint.X + downX * guideLength, segmentPoint.Y + downY * guideLength);
        context.DrawLine(shadowPen, start, end);
        context.DrawLine(guidePen, start, end);
    }

    private static bool TryGetLeftEyeScanStartPoint(BitmapSource source, AnchorMeshResult result, out WpfPoint scanStart)
    {
        scanStart = default;
        if (result.YuNetAnchors is null)
        {
            return false;
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        System.Drawing.PointF leftEye = result.YuNetAnchors.LeftEye;
        System.Drawing.PointF rightEye = result.YuNetAnchors.RightEye;
        double dx = rightEye.X - leftEye.X;
        double dy = rightEye.Y - leftEye.Y;
        if (Math.Abs(dx) < 0.001)
        {
            return false;
        }

        double eyeDistance = Math.Sqrt(dx * dx + dy * dy);
        int x = Math.Clamp((int)Math.Round(leftEye.X - Math.Max(6.0, eyeDistance * 0.20)), 0, width - 1);
        int y = Math.Clamp((int)Math.Round(leftEye.Y + (dy / dx) * (x - leftEye.X)), 0, height - 1);
        scanStart = new WpfPoint(x, y);
        return true;
    }

    private static void DrawEyeCenterPerpendicularGuide(DrawingContext context, AnchorMeshResult result, int width, int height)
    {
        if (result.YuNetAnchors is null)
        {
            return;
        }

        System.Drawing.PointF eyeCenter = result.YuNetAnchors.EyeCenter;
        if (!TryGetEyeAxisDownVector(result, out float downX, out float downY))
        {
            return;
        }

        float guideLength = MathF.Max(width, height) * 1.5f;
        WpfPoint start = new(eyeCenter.X - downX * guideLength, eyeCenter.Y - downY * guideLength);
        WpfPoint end = new(eyeCenter.X + downX * guideLength, eyeCenter.Y + downY * guideLength);
        WpfPoint center = new(eyeCenter.X, eyeCenter.Y);
        MediaPen shadowPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(150, 0, 0, 0)), 4.0);
        MediaPen guidePen = new(new MediaSolidColorBrush(MediaColor.FromArgb(245, 40, 235, 105)), 2.0);
        MediaBrush guideBrush = new MediaSolidColorBrush(MediaColor.FromArgb(245, 40, 235, 105));

        context.DrawLine(shadowPen, start, end);
        context.DrawLine(guidePen, start, end);
        context.DrawEllipse(guideBrush, null, center, 4.2, 4.2);
    }

    private static bool TryGetEyeAxisDownVector(AnchorMeshResult result, out float downX, out float downY)
    {
        downX = 0;
        downY = 1;
        if (result.YuNetAnchors is null)
        {
            return false;
        }

        System.Drawing.PointF leftEye = result.YuNetAnchors.LeftEye;
        System.Drawing.PointF rightEye = result.YuNetAnchors.RightEye;
        float axisX = rightEye.X - leftEye.X;
        float axisY = rightEye.Y - leftEye.Y;
        float axisLength = MathF.Sqrt(axisX * axisX + axisY * axisY);
        if (axisLength < 0.001f)
        {
            return false;
        }

        axisX /= axisLength;
        axisY /= axisLength;
        downX = -axisY;
        downY = axisX;
        if (downY < 0)
        {
            downX = -downX;
            downY = -downY;
        }

        return true;
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        float dx = ax - bx;
        float dy = ay - by;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private static bool TryFindLeftEyeDarkRunPoint(BitmapSource source, AnchorMeshResult result, out WpfPoint segmentPoint)
    {
        segmentPoint = default;
        if (result.YuNetAnchors is null)
        {
            return false;
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        System.Drawing.PointF leftEye = result.YuNetAnchors.LeftEye;
        System.Drawing.PointF rightEye = result.YuNetAnchors.RightEye;
        double dx = rightEye.X - leftEye.X;
        if (Math.Abs(dx) < 0.001)
        {
            return false;
        }

        double slope = (rightEye.Y - leftEye.Y) / dx;
        double eyeDistance = Math.Sqrt(dx * dx + Math.Pow(rightEye.Y - leftEye.Y, 2));
        int leftEyeOuterX = Math.Clamp((int)Math.Round(leftEye.X - Math.Max(6.0, eyeDistance * 0.20)), 0, width - 1);
        int skinRun = 0;
        bool passedSkin = false;
        int darkRun = 0;
        WpfPoint runStart = default;
        for (int x = leftEyeOuterX; x >= 0; x--)
        {
            int y = (int)Math.Round(leftEye.Y + slope * (x - leftEye.X));
            if (y < 0 || y >= height)
            {
                darkRun = 0;
                continue;
            }

            bool skinLike = IsSkinLikeBandAt(pixels, width, height, stride, x, y);
            if (!passedSkin)
            {
                skinRun = skinLike ? skinRun + 1 : 0;
                passedSkin = skinRun >= 3;
                darkRun = 0;
                continue;
            }

            if (IsVeryDarkBandAt(pixels, width, height, stride, x, y))
            {
                if (darkRun == 0)
                {
                    runStart = new WpfPoint(x, y);
                }

                darkRun++;
                if (darkRun >= 5)
                {
                    segmentPoint = runStart;
                    return true;
                }
            }
            else
            {
                darkRun = 0;
            }
        }

        return false;
    }

    private static bool IsSkinLikeBandAt(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        int count = 0;
        for (int yy = y - 2; yy <= y + 2; yy++)
        {
            if (IsSkinLikeAt(pixels, width, height, stride, x, yy))
            {
                count++;
            }
        }

        return count >= 2;
    }

    private static bool IsVeryDarkBandAt(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        int count = 0;
        for (int yy = y - 2; yy <= y + 2; yy++)
        {
            if (IsVeryDarkAt(pixels, width, height, stride, x, yy))
            {
                count++;
            }
        }

        return count >= 2;
    }

    private static bool IsSkinLikeAt(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        int index = y * stride + x * 4;
        int blue = pixels[index];
        int green = pixels[index + 1];
        int red = pixels[index + 2];
        int max = Math.Max(red, Math.Max(green, blue));
        int min = Math.Min(red, Math.Min(green, blue));
        return red >= 70 &&
            green >= 45 &&
            blue >= 30 &&
            red >= green * 0.82 &&
            red >= blue * 1.05 &&
            max - min >= 12;
    }

    private static bool IsVeryDarkAt(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return false;
        }

        int index = y * stride + x * 4;
        int blue = pixels[index];
        int green = pixels[index + 1];
        int red = pixels[index + 2];
        double luminance = red * 0.299 + green * 0.587 + blue * 0.114;
        return luminance <= 65;
    }

    public void SaveAlignedVsSnappedOverlay(BitmapSource source, AnchorMeshResult result, string path)
    {
        if (result.YuNetAlignedFeatures is null)
        {
            SaveSnappedOverlay(source, result, path);
            return;
        }

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(source, new WpfRect(0, 0, width, height));
            DrawFitBoxes(context, result);
            DrawGhostFeatures(context, result.YuNetAlignedFeatures);
            DrawSnapVectors(context, result.YuNetAlignedFeatures, result.Features);
            DrawFeatures(context, result.Features, 1.0, 1.0);
            DrawLabel(context, "aligned gray + snapped color");
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        SaveBitmap(path, bitmap);
    }

    public BitmapSource RenderFeatureOverlay(BitmapSource source, AnchorMeshFeatureSet features, string label)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        DrawingVisual visual = new();
        using (DrawingContext context = visual.RenderOpen())
        {
            context.DrawImage(source, new WpfRect(0, 0, width, height));
            DrawFeatures(context, features, 1.0, 1.0);
            DrawLabel(context, label);
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    public void SaveFeatureOverlay(BitmapSource source, AnchorMeshFeatureSet features, string path, string label)
    {
        BitmapSource bitmap = RenderFeatureOverlay(source, features, label);
        SaveBitmap(path, bitmap);
    }

    private static void DrawTemplateGrid(DrawingContext context, int width, int height)
    {
        MediaPen pen = new(new MediaSolidColorBrush(MediaColor.FromRgb(48, 52, 60)), 1);
        for (int x = 0; x <= width; x += 100)
        {
            context.DrawLine(pen, new WpfPoint(x, 0), new WpfPoint(x, height));
        }

        for (int y = 0; y <= height; y += 100)
        {
            context.DrawLine(pen, new WpfPoint(0, y), new WpfPoint(width, y));
        }
    }

    private static void DrawTemplateFeatures(DrawingContext context, AnchorMeshFeatureSet features, int width, int height)
    {
        double scale = Math.Min(width, height) * 0.62;
        double centerX = width * 0.5;
        double centerY = height * 0.47;
        AnchorMeshFeatureSet projected = features.Clone();
        foreach (AnchorMeshFeature feature in projected.GetAll())
        {
            foreach (AnchorMeshPoint point in feature.Points)
            {
                point.SnappedX = (float)(centerX + point.TemplateX * scale);
                point.SnappedY = (float)(centerY + point.TemplateY * scale);
            }
        }

        DrawFeatures(context, projected, 1.0, 1.0);
    }

    private static void DrawFeatures(DrawingContext context, AnchorMeshFeatureSet features, double scaleX, double scaleY)
    {
        foreach (AnchorMeshFeature feature in features.GetAll())
        {
            if (feature.Points.Count == 0)
            {
                continue;
            }

            MediaColor color = GetFeatureColor(feature.Name);
            MediaPen linePen = new(new MediaSolidColorBrush(color), GetFeatureStroke(feature.Name));
            MediaBrush pointBrush = new MediaSolidColorBrush(color);
            DrawFeatureLines(context, feature, linePen, scaleX, scaleY);

            foreach (AnchorMeshPoint point in feature.Points)
            {
                double radius = point.IsAnchor ? 4.2 : 2.6;
                WpfPoint p = new(point.SnappedX * scaleX, point.SnappedY * scaleY);
                context.DrawEllipse(pointBrush, null, p, radius, radius);
            }
        }
    }

    private static void DrawGhostFeatures(DrawingContext context, AnchorMeshFeatureSet features)
    {
        MediaPen ghostPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(115, 255, 255, 255)), 1.0);
        foreach (AnchorMeshFeature feature in features.GetAll())
        {
            DrawFeatureLines(context, feature, ghostPen, 1.0, 1.0);
            foreach (AnchorMeshPoint point in feature.Points)
            {
                context.DrawEllipse(
                    new MediaSolidColorBrush(MediaColor.FromArgb(115, 255, 255, 255)),
                    null,
                    new WpfPoint(point.SnappedX, point.SnappedY),
                    1.8,
                    1.8);
            }
        }
    }

    private static void DrawSnapVectors(DrawingContext context, AnchorMeshFeatureSet aligned, AnchorMeshFeatureSet snapped)
    {
        MediaPen vectorPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(210, 255, 255, 255)), 1.3);
        Dictionary<string, AnchorMeshFeature> alignedByName = aligned.GetAll().ToDictionary(feature => feature.Name, StringComparer.OrdinalIgnoreCase);
        foreach (AnchorMeshFeature snappedFeature in snapped.GetAll())
        {
            if (!alignedByName.TryGetValue(snappedFeature.Name, out AnchorMeshFeature? alignedFeature))
            {
                continue;
            }

            int count = Math.Min(alignedFeature.Points.Count, snappedFeature.Points.Count);
            for (int i = 0; i < count; i++)
            {
                AnchorMeshPoint before = alignedFeature.Points[i];
                AnchorMeshPoint after = snappedFeature.Points[i];
                double dx = after.SnappedX - before.SnappedX;
                double dy = after.SnappedY - before.SnappedY;
                if (dx * dx + dy * dy < 2.0)
                {
                    continue;
                }

                context.DrawLine(vectorPen, new WpfPoint(before.SnappedX, before.SnappedY), new WpfPoint(after.SnappedX, after.SnappedY));
            }
        }
    }

    private static void DrawTopologyEdges(DrawingContext context, AnchorMeshFeatureSet features, IReadOnlyList<AnchorMeshEdge> edges)
    {
        Dictionary<string, AnchorMeshPoint> points = features.GetAll()
            .SelectMany(feature => feature.Points)
            .ToDictionary(point => point.Name, StringComparer.OrdinalIgnoreCase);

        foreach (AnchorMeshEdge edge in edges)
        {
            if (!edge.IsDebugVisible ||
                !points.TryGetValue(edge.From, out AnchorMeshPoint? from) ||
                !points.TryGetValue(edge.To, out AnchorMeshPoint? to))
            {
                continue;
            }

            MediaColor color = edge.Kind switch
            {
                AnchorMeshEdgeKind.Anchor => MediaColor.FromArgb(175, 255, 210, 80),
                AnchorMeshEdgeKind.Contour => MediaColor.FromArgb(115, 255, 255, 255),
                AnchorMeshEdgeKind.Boundary => MediaColor.FromArgb(135, 120, 210, 255),
                AnchorMeshEdgeKind.Surface => MediaColor.FromArgb(125, 80, 245, 150),
                AnchorMeshEdgeKind.Protection => MediaColor.FromArgb(185, 255, 85, 95),
                AnchorMeshEdgeKind.Measurement => MediaColor.FromArgb(115, 220, 220, 225),
                AnchorMeshEdgeKind.Structural => MediaColor.FromArgb(150, 220, 220, 225),
                AnchorMeshEdgeKind.MorphControl => MediaColor.FromArgb(210, 185, 105, 255),
                _ => MediaColor.FromArgb(125, 255, 255, 255)
            };
            double thickness = edge.Kind switch
            {
                AnchorMeshEdgeKind.MorphControl => 2.2,
                AnchorMeshEdgeKind.Protection => 1.8,
                AnchorMeshEdgeKind.Anchor => 1.6,
                AnchorMeshEdgeKind.Surface => 1.4,
                _ => 1.1
            };
            MediaPen pen = new(new MediaSolidColorBrush(color), thickness);
            context.DrawLine(pen, new WpfPoint(from.SnappedX, from.SnappedY), new WpfPoint(to.SnappedX, to.SnappedY));
        }
    }

    private static void DrawFitBoxes(DrawingContext context, AnchorMeshResult result)
    {
        if (result.FitBox is not null)
        {
            DrawRotatedRect(
                context,
                result.FitBox.CenterX,
                result.FitBox.CenterY,
                result.FitBox.Width,
                result.FitBox.Height,
                result.FitBox.RotationRad,
                new MediaPen(new MediaSolidColorBrush(MediaColor.FromArgb(235, 80, 255, 135)), 2.4));
        }
    }

    private static void DrawRotatedRect(DrawingContext context, float centerX, float centerY, float width, float height, float angle, MediaPen pen)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        WpfPoint[] points =
        [
            RotatePoint(-halfWidth, -halfHeight, centerX, centerY, angle),
            RotatePoint(halfWidth, -halfHeight, centerX, centerY, angle),
            RotatePoint(halfWidth, halfHeight, centerX, centerY, angle),
            RotatePoint(-halfWidth, halfHeight, centerX, centerY, angle)
        ];

        for (int i = 0; i < points.Length; i++)
        {
            context.DrawLine(pen, points[i], points[(i + 1) % points.Length]);
        }
    }

    private static WpfPoint RotatePoint(float x, float y, float centerX, float centerY, float angle)
    {
        float cos = MathF.Cos(angle);
        float sin = MathF.Sin(angle);
        return new WpfPoint(
            x * cos - y * sin + centerX,
            x * sin + y * cos + centerY);
    }

    private static void DrawFeatureLines(DrawingContext context, AnchorMeshFeature feature, MediaPen pen, double scaleX, double scaleY)
    {
        for (int i = 1; i < feature.Points.Count; i++)
        {
            AnchorMeshPoint a = feature.Points[i - 1];
            AnchorMeshPoint b = feature.Points[i];
            context.DrawLine(pen, new WpfPoint(a.SnappedX * scaleX, a.SnappedY * scaleY), new WpfPoint(b.SnappedX * scaleX, b.SnappedY * scaleY));
        }

        if (feature.IsClosedLoop && feature.Points.Count > 2)
        {
            AnchorMeshPoint a = feature.Points[^1];
            AnchorMeshPoint b = feature.Points[0];
            context.DrawLine(pen, new WpfPoint(a.SnappedX * scaleX, a.SnappedY * scaleY), new WpfPoint(b.SnappedX * scaleX, b.SnappedY * scaleY));
        }
    }

    private static void DrawLabel(DrawingContext context, string label)
    {
        FormattedText text = new(
            label,
            System.Globalization.CultureInfo.CurrentCulture,
            WpfFlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            24,
            MediaBrushes.White,
            1.0);
        context.DrawRectangle(new MediaSolidColorBrush(MediaColor.FromArgb(130, 0, 0, 0)), null, new WpfRect(16, 16, text.Width + 24, text.Height + 16));
        context.DrawText(text, new WpfPoint(28, 24));
    }

    private static MediaColor GetFeatureColor(string featureName)
    {
        return featureName switch
        {
            "FaceOutline" => MediaColor.FromRgb(70, 150, 255),
            "LeftEye" or "RightEye" => MediaColor.FromRgb(88, 220, 255),
            "LeftPupil" or "RightPupil" => MediaColor.FromRgb(120, 245, 255),
            "LeftBrow" or "RightBrow" => MediaColor.FromRgb(255, 164, 70),
            "Nose" => MediaColor.FromRgb(255, 230, 75),
            "LipOuter" => MediaColor.FromRgb(255, 105, 175),
            "LipInner" => MediaColor.FromRgb(255, 55, 95),
            "LeftEar" or "RightEar" => MediaColor.FromRgb(184, 105, 255),
            "Hairline" => MediaColor.FromRgb(90, 220, 120),
            "Neck" or "ShirtShoulder" => MediaColor.FromRgb(170, 178, 188),
            _ => MediaColor.FromRgb(240, 240, 240)
        };
    }

    private static double GetFeatureStroke(string featureName)
    {
        return featureName is "FaceOutline" or "LipOuter" ? 2.2 :
            featureName is "LeftPupil" or "RightPupil" ? 1.2 :
            1.6;
    }

    private static void SaveVisual(string path, DrawingVisual visual, int width, int height)
    {
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        SaveBitmap(path, bitmap);
    }

    private static void SaveBitmap(string path, BitmapSource bitmap)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
