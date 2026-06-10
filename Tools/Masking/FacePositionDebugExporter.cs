using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class FacePositionDebugExporter
{
    private const string WhitePositionFileName = "debug_face_position_white.png";
    private const string ReportFileName = "debug_face_position_report.txt";

    public static void SaveWhiteBackground(BitmapSource source, FaceAnalysisResult analysis, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(analysis);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);

        Directory.CreateDirectory(outputDirectory);
        SaveBitmap(
            CreateWhiteBackgroundPreview(source.PixelWidth, source.PixelHeight, analysis),
            Path.Combine(outputDirectory, WhitePositionFileName));
        File.WriteAllLines(
            Path.Combine(outputDirectory, ReportFileName),
            CreateReport(source.PixelWidth, source.PixelHeight, analysis),
            System.Text.Encoding.UTF8);
    }

    public static BitmapSource CreateWhiteBackgroundPreview(int width, int height, FaceAnalysisResult analysis)
    {
        width = Math.Max(1, width);
        height = Math.Max(1, height);
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        FillWhite(pixels);

        Int32Rect faceBox = ClampRect(analysis.FaceBox, width, height);
        DrawRectangle(pixels, width, height, stride, faceBox, 245, 170, 0);
        DrawFaceGuideLines(pixels, width, height, stride, faceBox, analysis.FaceLandmarks);
        DrawNeckGuide(pixels, width, height, stride, faceBox, analysis.FaceLandmarks);
        DrawLandmarkPoints(pixels, width, height, stride, analysis.FaceLandmarks);

        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        bitmap.Freeze();
        return bitmap;
    }

    public static IReadOnlyList<string> CreateReport(int width, int height, FaceAnalysisResult analysis)
    {
        List<string> lines = new()
        {
            "Face Position Debug",
            "Mode: white_background_current_analysis",
            "ImageSize: " + width.ToString(CultureInfo.InvariantCulture) + "x" + height.ToString(CultureInfo.InvariantCulture),
            "FaceBox: " + FormatRect(analysis.FaceBox),
            "FaceAngle: " + analysis.FaceAngle.ToString("0.###", CultureInfo.InvariantCulture),
            "FaceQualityScore: " + analysis.FaceQualityScore.ToString("0.###", CultureInfo.InvariantCulture),
            "LandmarkConfidence: " + analysis.LandmarkConfidence.ToString("0.###", CultureInfo.InvariantCulture)
        };

        foreach ((string key, WpfPoint point) in analysis.FaceLandmarks.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            lines.Add("Landmark." + key + ": " + FormatPoint(point));
        }

        Int32Rect faceBox = ClampRect(analysis.FaceBox, width, height);
        NeckGuide neck = CreateNeckGuide(faceBox, analysis.FaceLandmarks, width, height);
        lines.Add("EstimatedNeckGuide.TopCenter: " + FormatPoint(neck.TopCenter));
        lines.Add("EstimatedNeckGuide.BottomCenter: " + FormatPoint(neck.BottomCenter));
        lines.Add("EstimatedNeckGuide.TopHalfWidth: " + neck.TopHalfWidth.ToString("0.###", CultureInfo.InvariantCulture));
        lines.Add("EstimatedNeckGuide.BottomHalfWidth: " + neck.BottomHalfWidth.ToString("0.###", CultureInfo.InvariantCulture));

        foreach (string warning in analysis.DebugWarnings)
        {
            lines.Add("Warning: " + warning);
        }

        return lines;
    }

    private static void DrawFaceGuideLines(byte[] pixels, int width, int height, int stride, Int32Rect faceBox, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        if (TryGetPoint(landmarks, "left_eye", out WpfPoint leftEye) &&
            TryGetPoint(landmarks, "right_eye", out WpfPoint rightEye))
        {
            DrawLine(pixels, width, height, stride, leftEye, rightEye, 235, 60, 55, thickness: 2);
            if (TryGetPoint(landmarks, "nose_tip", out WpfPoint noseTip))
            {
                WpfPoint eyeCenter = Midpoint(leftEye, rightEye);
                WpfPoint eyeToNoseVerticalEnd = new(eyeCenter.X, noseTip.Y);
                DrawLine(pixels, width, height, stride, eyeCenter, eyeToNoseVerticalEnd, 40, 155, 235, thickness: 2);
            }
        }

        if (TryGetPoint(landmarks, "mouth_center", out WpfPoint mouth) &&
            TryGetPoint(landmarks, "chin", out WpfPoint chin))
        {
            DrawLine(pixels, width, height, stride, mouth, chin, 35, 170, 85, thickness: 2);
        }
    }

    private static void DrawNeckGuide(byte[] pixels, int width, int height, int stride, Int32Rect faceBox, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        NeckGuide neck = CreateNeckGuide(faceBox, landmarks, width, height);
        WpfPoint topLeft = new(neck.TopCenter.X - neck.TopHalfWidth, neck.TopCenter.Y);
        WpfPoint topRight = new(neck.TopCenter.X + neck.TopHalfWidth, neck.TopCenter.Y);
        WpfPoint bottomLeft = new(neck.BottomCenter.X - neck.BottomHalfWidth, neck.BottomCenter.Y);
        WpfPoint bottomRight = new(neck.BottomCenter.X + neck.BottomHalfWidth, neck.BottomCenter.Y);
        DrawLine(pixels, width, height, stride, topLeft, topRight, 50, 180, 160, thickness: 2);
        DrawLine(pixels, width, height, stride, bottomLeft, bottomRight, 50, 180, 160, thickness: 2);
        DrawLine(pixels, width, height, stride, topLeft, bottomLeft, 50, 180, 160, thickness: 2);
        DrawLine(pixels, width, height, stride, topRight, bottomRight, 50, 180, 160, thickness: 2);
        DrawLine(pixels, width, height, stride, neck.TopCenter, neck.BottomCenter, 50, 180, 160, thickness: 1);
    }

    private static void DrawLandmarkPoints(byte[] pixels, int width, int height, int stride, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        foreach ((string key, WpfPoint point) in landmarks)
        {
            (byte red, byte green, byte blue) = key switch
            {
                "left_eye" or "right_eye" => ((byte)235, (byte)45, (byte)45),
                "nose_tip" => ((byte)30, (byte)130, (byte)240),
                "mouth_center" => ((byte)30, (byte)170, (byte)75),
                "chin" => ((byte)170, (byte)70, (byte)235),
                _ => ((byte)45, (byte)45, (byte)45)
            };
            DrawPoint(pixels, width, height, stride, point, red, green, blue, radius: 8);
        }
    }

    private static NeckGuide CreateNeckGuide(Int32Rect faceBox, IReadOnlyDictionary<string, WpfPoint> landmarks, int width, int height)
    {
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        WpfPoint chin = TryGetPoint(landmarks, "chin", out WpfPoint detectedChin)
            ? detectedChin
            : new WpfPoint(faceBox.X + faceWidth * 0.5, faceBox.Y + faceHeight * 0.92);
        double centerX = chin.X;
        double centerWeight = 1.0;
        if (TryGetPoint(landmarks, "mouth_center", out WpfPoint mouth))
        {
            centerX += mouth.X * 0.55;
            centerWeight += 0.55;
        }

        if (TryGetPoint(landmarks, "nose_tip", out WpfPoint nose))
        {
            centerX += nose.X * 0.35;
            centerWeight += 0.35;
        }

        centerX = Math.Clamp(centerX / centerWeight, 0, Math.Max(0, width - 1));
        double top = Math.Clamp(chin.Y - faceHeight * 0.035, faceBox.Y + faceHeight * 0.78, height - 1);
        double bottom = Math.Clamp(chin.Y + faceHeight * 0.30, top + faceHeight * 0.10, height - 1);
        return new NeckGuide(
            new WpfPoint(centerX, top),
            new WpfPoint(centerX, bottom),
            faceWidth * 0.30,
            faceWidth * 0.21);
    }

    private static void FillWhite(byte[] pixels)
    {
        for (int index = 0; index < pixels.Length; index += 4)
        {
            pixels[index] = 255;
            pixels[index + 1] = 255;
            pixels[index + 2] = 255;
            pixels[index + 3] = 255;
        }
    }

    private static bool TryGetPoint(IReadOnlyDictionary<string, WpfPoint> landmarks, string key, out WpfPoint point)
    {
        return landmarks.TryGetValue(key, out point);
    }

    private static WpfPoint Midpoint(WpfPoint first, WpfPoint second)
    {
        return new WpfPoint((first.X + second.X) * 0.5, (first.Y + second.Y) * 0.5);
    }

    private static Int32Rect ClampRect(Int32Rect rect, int width, int height)
    {
        int x = Math.Clamp(rect.X, 0, Math.Max(0, width - 1));
        int y = Math.Clamp(rect.Y, 0, Math.Max(0, height - 1));
        int right = Math.Clamp(rect.X + rect.Width, x + 1, width);
        int bottom = Math.Clamp(rect.Y + rect.Height, y + 1, height);
        return new Int32Rect(x, y, right - x, bottom - y);
    }

    private static void DrawRectangle(byte[] pixels, int width, int height, int stride, Int32Rect rect, byte red, byte green, byte blue)
    {
        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            DrawPixel(pixels, width, height, stride, x, rect.Y, red, green, blue);
            DrawPixel(pixels, width, height, stride, x, rect.Y + rect.Height - 1, red, green, blue);
        }

        for (int y = rect.Y; y < rect.Y + rect.Height; y++)
        {
            DrawPixel(pixels, width, height, stride, rect.X, y, red, green, blue);
            DrawPixel(pixels, width, height, stride, rect.X + rect.Width - 1, y, red, green, blue);
        }
    }

    private static void DrawLine(byte[] pixels, int width, int height, int stride, WpfPoint start, WpfPoint end, byte red, byte green, byte blue, int thickness)
    {
        int steps = (int)Math.Ceiling(Math.Max(Math.Abs(end.X - start.X), Math.Abs(end.Y - start.Y)));
        if (steps <= 0)
        {
            DrawPoint(pixels, width, height, stride, start, red, green, blue, thickness + 1);
            return;
        }

        for (int step = 0; step <= steps; step++)
        {
            double amount = step / (double)steps;
            DrawPoint(
                pixels,
                width,
                height,
                stride,
                new WpfPoint(start.X + (end.X - start.X) * amount, start.Y + (end.Y - start.Y) * amount),
                red,
                green,
                blue,
                thickness);
        }
    }

    private static void DrawPoint(byte[] pixels, int width, int height, int stride, WpfPoint point, byte red, byte green, byte blue, int radius)
    {
        int centerX = (int)Math.Round(point.X);
        int centerY = (int)Math.Round(point.Y);
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (dx * dx + dy * dy <= radius * radius)
                {
                    DrawPixel(pixels, width, height, stride, centerX + dx, centerY + dy, red, green, blue);
                }
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

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static string FormatPoint(WpfPoint point)
    {
        return point.X.ToString("0.###", CultureInfo.InvariantCulture) + "," + point.Y.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string FormatRect(Int32Rect rect)
    {
        return rect.X.ToString(CultureInfo.InvariantCulture) + "," +
            rect.Y.ToString(CultureInfo.InvariantCulture) + "," +
            rect.Width.ToString(CultureInfo.InvariantCulture) + "," +
            rect.Height.ToString(CultureInfo.InvariantCulture);
    }

    private sealed record NeckGuide(WpfPoint TopCenter, WpfPoint BottomCenter, double TopHalfWidth, double BottomHalfWidth);
}
