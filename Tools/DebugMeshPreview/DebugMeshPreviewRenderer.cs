using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class DebugMeshPreviewRenderer
{
    public static BitmapSource RenderDefaultPreview(int width = 1200, int height = 1400)
    {
        return Render(DebugFaceMeshFactory.CreateDefaultMesh(), width, height);
    }

    public static void SaveDefaultPreview(string path, int width = 1200, int height = 1400)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        SavePng(RenderDefaultPreview(width, height), path);
    }

    public static BitmapSource RenderFeatureMeshOverlay(BitmapSource source, FaceFeatureMeshSet meshSet)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(meshSet);

        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bgraSource.PixelWidth;
        int height = bgraSource.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bgraSource.CopyPixels(pixels, stride, 0);

        DrawFeatureMesh(pixels, width, height, stride, meshSet.EyeMesh, MediaColor.FromRgb(70, 224, 255));
        DrawFeatureMesh(pixels, width, height, stride, meshSet.BrowMesh, MediaColor.FromRgb(188, 96, 255));
        DrawFeatureMesh(pixels, width, height, stride, meshSet.NoseMesh, MediaColor.FromRgb(255, 218, 82));
        DrawFeatureMesh(pixels, width, height, stride, meshSet.LipMesh, MediaColor.FromRgb(255, 104, 150));

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static void SaveFeatureMeshOverlay(BitmapSource source, FaceFeatureMeshSet meshSet, string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        SavePng(RenderFeatureMeshOverlay(source, meshSet), path);
    }

    public static BitmapSource Render(DebugFaceMesh mesh, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(mesh);
        width = Math.Max(64, width);
        height = Math.Max(64, height);

        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        FillBackground(pixels, MediaColor.FromRgb(18, 19, 21));

        double centerX = width / 2d;
        double centerY = height * 0.43d;
        double scale = Math.Min(width * 0.44d, height * 0.40d);
        Dictionary<string, WpfPoint> projectedPoints = mesh.Points.ToDictionary(
            point => point.Name,
            point => DebugMeshProjector.Project(point, centerX, centerY, scale));

        foreach (DebugMeshEdge edge in mesh.Edges)
        {
            if (!projectedPoints.TryGetValue(edge.From, out WpfPoint from) ||
                !projectedPoints.TryGetValue(edge.To, out WpfPoint to))
            {
                continue;
            }

            MediaColor color = GetEdgeColor(edge);
            DrawLine(
                pixels,
                width,
                height,
                stride,
                (int)Math.Round(from.X),
                (int)Math.Round(from.Y),
                (int)Math.Round(to.X),
                (int)Math.Round(to.Y),
                color.R,
                color.G,
                color.B);
        }

        foreach (DebugMeshPoint point in mesh.Points.OrderBy(point => point.Z))
        {
            WpfPoint projected = projectedPoints[point.Name];
            MediaColor color = GetPointColor(point.Name);
            DrawPoint(pixels, width, height, stride, (int)Math.Round(projected.X), (int)Math.Round(projected.Y), color.R, color.G, color.B);
        }

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    private static MediaColor GetEdgeColor(DebugMeshEdge edge)
    {
        return GetPointColor(edge.From);
    }

    private static void DrawFeatureMesh(byte[] pixels, int width, int height, int stride, FaceFeatureMesh mesh, MediaColor color)
    {
        if (mesh.Points.Count == 0)
        {
            return;
        }

        foreach (IGrouping<string, FeatureMeshPoint> group in mesh.Points.GroupBy(GetMeshGroupKey))
        {
            FeatureMeshPoint[] points = group.OrderBy(point => point.Index).ToArray();
            if (points.Length == 0)
            {
                continue;
            }

            for (int index = 0; index < points.Length; index++)
            {
                FeatureMeshPoint from = points[index];
                FeatureMeshPoint to = points[(index + 1) % points.Length];
                DrawAlphaLine(
                    pixels,
                    width,
                    height,
                    stride,
                    (int)Math.Round(from.X),
                    (int)Math.Round(from.Y),
                    (int)Math.Round(to.X),
                    (int)Math.Round(to.Y),
                    color.R,
                    color.G,
                    color.B,
                    0.88);
            }

            foreach (FeatureMeshPoint point in points)
            {
                DrawAlphaPoint(
                    pixels,
                    width,
                    height,
                    stride,
                    (int)Math.Round(point.X),
                    (int)Math.Round(point.Y),
                    color.R,
                    color.G,
                    color.B,
                    0.95);
            }
        }
    }

    private static string GetMeshGroupKey(FeatureMeshPoint point)
    {
        if (point.Role.StartsWith("left_", StringComparison.Ordinal))
        {
            return "left";
        }

        if (point.Role.StartsWith("right_", StringComparison.Ordinal))
        {
            return "right";
        }

        return "single";
    }

    private static MediaColor GetPointColor(string name)
    {
        if (name.StartsWith("Face_", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(105, 170, 255);
        }

        if (name.Contains("Eye", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(80, 220, 255);
        }

        if (name.Contains("Brow", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(188, 96, 255);
        }

        if (name.StartsWith("Nose_", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(255, 218, 82);
        }

        if (name.StartsWith("Lip_", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(255, 104, 150);
        }

        if (name.Contains("Ear", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(175, 145, 255);
        }

        if (name.StartsWith("Hair_", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(130, 220, 150);
        }

        if (name.StartsWith("Neck_", StringComparison.Ordinal))
        {
            return MediaColor.FromRgb(170, 190, 210);
        }

        return MediaColor.FromRgb(205, 205, 205);
    }

    private static void FillBackground(byte[] pixels, MediaColor color)
    {
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = color.B;
            pixels[index + 1] = color.G;
            pixels[index + 2] = color.R;
            pixels[index + 3] = 255;
        }
    }

    private static void DrawPoint(byte[] pixels, int width, int height, int stride, int centerX, int centerY, byte red, byte green, byte blue)
    {
        for (int y = centerY - 3; y <= centerY + 3; y++)
        {
            for (int x = centerX - 3; x <= centerX + 3; x++)
            {
                if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= 9)
                {
                    DrawPixel(pixels, width, height, stride, x, y, red, green, blue);
                }
            }
        }
    }

    private static void DrawLine(byte[] pixels, int width, int height, int stride, int x0, int y0, int x1, int y1, byte red, byte green, byte blue)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        int x = x0;
        int y = y0;

        while (true)
        {
            DrawPixel(pixels, width, height, stride, x, y, red, green, blue);
            if (x == x1 && y == y1)
            {
                break;
            }

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static void DrawAlphaPoint(byte[] pixels, int width, int height, int stride, int centerX, int centerY, byte red, byte green, byte blue, double alpha)
    {
        for (int y = centerY - 3; y <= centerY + 3; y++)
        {
            for (int x = centerX - 3; x <= centerX + 3; x++)
            {
                if ((x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= 9)
                {
                    BlendPixel(pixels, width, height, stride, x, y, red, green, blue, alpha);
                }
            }
        }
    }

    private static void DrawAlphaLine(byte[] pixels, int width, int height, int stride, int x0, int y0, int x1, int y1, byte red, byte green, byte blue, double alpha)
    {
        int dx = Math.Abs(x1 - x0);
        int dy = -Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        int x = x0;
        int y = y0;

        while (true)
        {
            BlendPixel(pixels, width, height, stride, x, y, red, green, blue, alpha);
            if (x == x1 && y == y1)
            {
                break;
            }

            int doubledError = 2 * error;
            if (doubledError >= dy)
            {
                error += dy;
                x += sx;
            }

            if (doubledError <= dx)
            {
                error += dx;
                y += sy;
            }
        }
    }

    private static void DrawPixel(byte[] pixels, int width, int height, int stride, int x, int y, byte red, byte green, byte blue)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = y * stride + x * 4;
        pixels[index] = blue;
        pixels[index + 1] = green;
        pixels[index + 2] = red;
        pixels[index + 3] = 255;
    }

    private static void BlendPixel(byte[] pixels, int width, int height, int stride, int x, int y, byte red, byte green, byte blue, double alpha)
    {
        if (x < 0 || x >= width || y < 0 || y >= height)
        {
            return;
        }

        int index = y * stride + x * 4;
        pixels[index] = Blend(pixels[index], blue, alpha);
        pixels[index + 1] = Blend(pixels[index + 1], green, alpha);
        pixels[index + 2] = Blend(pixels[index + 2], red, alpha);
        pixels[index + 3] = 255;
    }

    private static byte Blend(byte destination, byte source, double alpha)
    {
        return (byte)Math.Clamp((int)Math.Round(destination * (1 - alpha) + source * alpha), 0, 255);
    }

    private static void SavePng(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
