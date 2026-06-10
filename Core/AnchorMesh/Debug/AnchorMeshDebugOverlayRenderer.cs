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
        SaveFeatureOverlay(source, result.Features, path, "K-AnchorMesh snapped");
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
            DrawTopologyEdges(context, result.Features, result.TopologyEdges);
            DrawFeatures(context, result.Features, 1.0, 1.0);
            DrawLabel(context, "K-AnchorMesh topology");
        }

        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        SaveBitmap(path, bitmap);
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
        if (result.YuNetAnchors is not null)
        {
            System.Drawing.RectangleF box = result.YuNetAnchors.FaceBox;
            MediaPen yuNetPen = new(new MediaSolidColorBrush(MediaColor.FromArgb(220, 80, 170, 255)), 2.0);
            context.DrawRectangle(null, yuNetPen, new WpfRect(box.X, box.Y, box.Width, box.Height));
        }

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
