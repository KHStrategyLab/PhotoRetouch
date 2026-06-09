using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class ShapeBalanceDebugExporter
{
    public static void SaveAll(BitmapSource original, BalancedImageBundle bundle, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(bundle);

        Directory.CreateDirectory(outputDirectory);
        BitmapSource source = bundle.SourceImage;
        SaveBitmap(CreateVectorOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_global_transform.png"));
        SaveBitmap(CreateLocalWarpRegionOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_local_warp_regions.png"));
        SaveBitmap(CreateWarpStrengthMap(bundle), Path.Combine(outputDirectory, "debug_shape_warp_strength_map.png"));
        SaveBitmap(CreateProtectedRegionOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_protected_regions.png"));
        SaveBitmap(CreateLandmarkOverlay(source, bundle.SourceSnapshot.Analysis.FaceLandmarks), Path.Combine(outputDirectory, "debug_shape_original_landmarks.png"));
        SaveBitmap(CreateVectorOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_balance_vectors.png"));
        SaveBitmap(CreateVectorOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_map_overlay.png"));
        SaveBitmap(CreateCenterLineOverlay(source, bundle), Path.Combine(outputDirectory, "debug_shape_face_centerline.png"));
        SaveBitmap(CreateSingleVectorOverlay(source, bundle, "left_eye_level", "right_eye_level"), Path.Combine(outputDirectory, "debug_shape_eye_level_delta.png"));
        SaveBitmap(CreateSingleVectorOverlay(source, bundle, "nose_center"), Path.Combine(outputDirectory, "debug_shape_nose_line.png"));
        SaveBitmap(CreateSingleVectorOverlay(source, bundle, "chin_center"), Path.Combine(outputDirectory, "debug_shape_chin_center.png"));
        SaveBitmap(CreatePlaceholderOverlay(source, bundle, "eyebrow_pending"), Path.Combine(outputDirectory, "debug_shape_eyebrow_balance.png"));
        SaveBitmap(CreatePlaceholderOverlay(source, bundle, "mouth_corner_pending"), Path.Combine(outputDirectory, "debug_shape_mouth_corner_balance.png"));
        SaveBitmap(DebugMaskExporter.CreateFinalOverlayPreview(bundle.BalancedImage, bundle.BalancedSnapshot.Masks), Path.Combine(outputDirectory, "debug_shape_balanced_mask_overlay.png"));
        SaveBitmap(CreateLandmarkOverlay(bundle.BalancedImage, bundle.BalancedLandmarks), Path.Combine(outputDirectory, "debug_shape_balanced_landmarks.png"));
        SaveBitmap(CreateBeforeAfterSplit(source, bundle.BalancedImage), Path.Combine(outputDirectory, "debug_shape_before_after_compare.png"));
        SaveBitmap(CreateBeforeAfterSplit(source, bundle.BalancedImage), Path.Combine(outputDirectory, "debug_shape_image_before_after.png"));
        SaveBitmap(CreateMaskBeforeAfter(bundle.SourceSnapshot.Masks.FinalOverlayMask, bundle.BalancedSnapshot.Masks.FinalOverlayMask), Path.Combine(outputDirectory, "debug_shape_mask_before_after.png"));
        SaveBitmap(CreateMaskBeforeAfter(bundle.SourceSnapshot.Masks.HardProtectMask, bundle.BalancedSnapshot.Masks.HardProtectMask), Path.Combine(outputDirectory, "debug_shape_hardprotect_before_after.png"));
        SaveBitmap(CreateMaskBeforeAfter(bundle.SourceSnapshot.Masks.NostrilMask, bundle.BalancedSnapshot.Masks.NostrilMask), Path.Combine(outputDirectory, "debug_shape_nostril_before_after.png"));
        SaveBitmap(CreateScoreImage(bundle.BalancedMaskQualityReport.WarpAlignmentScore), Path.Combine(outputDirectory, "debug_shape_warp_alignment_score.png"));
        SaveBitmap(DebugMaskExporter.CreateMaskOverlayPreview(bundle.BalancedImage, bundle.BalancedSnapshot.Masks.NostrilMask, 70, 180, 255, 0.82), Path.Combine(outputDirectory, "debug_shape_nostril_observation.png"));

        ShapeBalanceDebugReportDto report = new(
            bundle.ShapeBalanceReport.AnalysisReport.FaceRollAngle,
            bundle.ShapeBalanceReport.AnalysisReport.FaceYawLikeBias,
            bundle.ShapeBalanceReport.AnalysisReport.FacePitchLikeBias,
            bundle.ShapeBalanceReport.AnalysisReport.EyeLevelDelta,
            bundle.ShapeBalanceReport.AnalysisReport.EyebrowLevelDelta,
            bundle.ShapeBalanceReport.AnalysisReport.MouthCornerDelta,
            bundle.ShapeBalanceReport.AnalysisReport.NoseLineTilt,
            bundle.ShapeBalanceReport.AnalysisReport.ChinCenterDelta,
            bundle.ShapeBalanceReport.AnalysisReport.LeftRightBalanceScore,
            bundle.ShapeBalanceReport.ShapeBalanceApplied,
            bundle.ShapeBalanceReport.ShapeBalanceStrength,
            bundle.ShapeBalanceReport.BalancedMaskQualityScore,
            bundle.ShapeBalanceReport.AnalysisReport.NostrilBalanceObservation,
            bundle.ShapeBalanceReport.DebugWarnings);
        File.WriteAllText(
            Path.Combine(outputDirectory, "debug_shape_balance_report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8);
        File.WriteAllText(
            Path.Combine(outputDirectory, "debug_balanced_mask_quality_report.json"),
            JsonSerializer.Serialize(bundle.BalancedMaskQualityReport, new JsonSerializerOptions { WriteIndented = true }),
            System.Text.Encoding.UTF8);
    }

    private static void SaveBitmap(BitmapSource image, string path)
    {
        BitmapEncoder encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(stream);
    }

    private static BitmapSource CreateVectorOverlay(BitmapSource source, BalancedImageBundle bundle)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        foreach (ShapeBalanceDebugVector vector in bundle.ShapeBalanceMap.DebugVectors)
        {
            DrawLine(pixels, width, height, stride, vector.From, vector.To, 90, 190, 235);
        }

        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateSingleVectorOverlay(BitmapSource source, BalancedImageBundle bundle, params string[] labels)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        HashSet<string> selectedLabels = labels.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (ShapeBalanceDebugVector vector in bundle.ShapeBalanceMap.DebugVectors.Where(vector => selectedLabels.Contains(vector.Label)))
        {
            DrawLine(pixels, width, height, stride, vector.From, vector.To, 100, 210, 245);
        }

        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateLocalWarpRegionOverlay(BitmapSource source, BalancedImageBundle bundle)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        foreach (ShapeBalanceWarpRegion region in bundle.ShapeBalanceMap.LocalWarpRegions)
        {
            DrawEllipse(pixels, width, height, stride, region.Center, region.RadiusX, region.RadiusY, 90, 190, 235);
        }

        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateProtectedRegionOverlay(BitmapSource source, BalancedImageBundle bundle)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        foreach (ShapeBalanceProtectedRegion region in bundle.ShapeBalanceMap.ProtectedFeatureRegions)
        {
            DrawEllipse(pixels, width, height, stride, region.Center, region.RadiusX, region.RadiusY, 245, 170, 90);
        }

        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateWarpStrengthMap(BalancedImageBundle bundle)
    {
        int width = bundle.ShapeBalanceMap.TargetImageWidth;
        int height = bundle.ShapeBalanceMap.TargetImageHeight;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double strength = 0;
                foreach (ShapeBalanceWarpRegion region in bundle.ShapeBalanceMap.LocalWarpRegions)
                {
                    strength = Math.Max(strength, region.WeightAt(x, y));
                }

                byte value = (byte)Math.Clamp((int)Math.Round(strength * 255), 0, 255);
                int index = (y * width + x) * 4;
                pixels[index] = 40;
                pixels[index + 1] = value;
                pixels[index + 2] = (byte)Math.Clamp(value + 30, 0, 255);
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, 96, 96, pixels);
    }

    private static BitmapSource CreateCenterLineOverlay(BitmapSource source, BalancedImageBundle bundle)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        int top = bundle.BalancedFaceBox.Y;
        int bottom = bundle.BalancedFaceBox.Y + bundle.BalancedFaceBox.Height;
        double centerX = bundle.ShapeBalanceMap.FaceCenter.X;
        DrawLine(pixels, width, height, stride, new WpfPoint(centerX, top), new WpfPoint(centerX, bottom), 80, 220, 180);
        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreatePlaceholderOverlay(BitmapSource source, BalancedImageBundle bundle, string label)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        double y = label.Contains("eyebrow", StringComparison.OrdinalIgnoreCase)
            ? bundle.BalancedFaceBox.Y + bundle.BalancedFaceBox.Height * 0.28
            : bundle.BalancedFaceBox.Y + bundle.BalancedFaceBox.Height * 0.63;
        DrawLine(
            pixels,
            width,
            height,
            stride,
            new WpfPoint(bundle.BalancedFaceBox.X, y),
            new WpfPoint(bundle.BalancedFaceBox.X + bundle.BalancedFaceBox.Width, y),
            135,
            135,
            135);
        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateLandmarkOverlay(BitmapSource source, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        BitmapSource bitmap = ToBgra(source);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);
        foreach (WpfPoint point in landmarks.Values)
        {
            DrawCross(pixels, width, height, stride, point, 255, 220, 120);
        }

        return CreateBitmap(width, height, bitmap.DpiX, bitmap.DpiY, pixels);
    }

    private static BitmapSource CreateBeforeAfterSplit(BitmapSource before, BitmapSource after)
    {
        BitmapSource beforeBgra = ToBgra(before);
        BitmapSource afterBgra = ToBgra(after);
        int width = Math.Min(beforeBgra.PixelWidth, afterBgra.PixelWidth);
        int height = Math.Min(beforeBgra.PixelHeight, afterBgra.PixelHeight);
        int stride = width * 4;
        byte[] beforePixels = new byte[stride * height];
        byte[] afterPixels = new byte[stride * height];
        beforeBgra.CopyPixels(beforePixels, stride, 0);
        afterBgra.CopyPixels(afterPixels, stride, 0);
        byte[] output = new byte[beforePixels.Length];
        int splitX = width / 2;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                byte[] source = x < splitX ? beforePixels : afterPixels;
                output[index] = source[index];
                output[index + 1] = source[index + 1];
                output[index + 2] = source[index + 2];
                output[index + 3] = source[index + 3];
            }
        }

        for (int y = 0; y < height; y++)
        {
            int index = y * stride + splitX * 4;
            output[index] = 255;
            output[index + 1] = 255;
            output[index + 2] = 255;
            output[index + 3] = 255;
        }

        return CreateBitmap(width, height, beforeBgra.DpiX, beforeBgra.DpiY, output);
    }

    private static BitmapSource CreateMaskBeforeAfter(MaskPlane before, MaskPlane after)
    {
        BitmapSource beforePreview = DebugMaskExporter.CreateMaskPreview(before);
        BitmapSource afterPreview = DebugMaskExporter.CreateMaskPreview(after);
        return CreateBeforeAfterSplit(beforePreview, afterPreview);
    }

    private static BitmapSource CreateScoreImage(double score)
    {
        const int width = 420;
        const int height = 64;
        byte[] pixels = new byte[width * height * 4];
        int filled = Math.Clamp((int)Math.Round(width * Math.Clamp(score, 0, 1)), 0, width);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = (y * width + x) * 4;
                bool isFilled = x < filled;
                pixels[index] = isFilled ? (byte)150 : (byte)45;
                pixels[index + 1] = isFilled ? (byte)190 : (byte)45;
                pixels[index + 2] = isFilled ? (byte)90 : (byte)45;
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, 96, 96, pixels);
    }

    private static BitmapSource ToBgra(BitmapSource source)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static void DrawCross(byte[] pixels, int width, int height, int stride, WpfPoint point, byte red, byte green, byte blue)
    {
        DrawLine(pixels, width, height, stride, new WpfPoint(point.X - 5, point.Y), new WpfPoint(point.X + 5, point.Y), red, green, blue);
        DrawLine(pixels, width, height, stride, new WpfPoint(point.X, point.Y - 5), new WpfPoint(point.X, point.Y + 5), red, green, blue);
    }

    private static void DrawEllipse(
        byte[] pixels,
        int width,
        int height,
        int stride,
        WpfPoint center,
        double radiusX,
        double radiusY,
        byte red,
        byte green,
        byte blue)
    {
        const int steps = 96;
        WpfPoint previous = new(center.X + radiusX, center.Y);
        for (int step = 1; step <= steps; step++)
        {
            double angle = step * Math.PI * 2 / steps;
            WpfPoint current = new(center.X + Math.Cos(angle) * radiusX, center.Y + Math.Sin(angle) * radiusY);
            DrawLine(pixels, width, height, stride, previous, current, red, green, blue);
            previous = current;
        }
    }

    private static void DrawLine(byte[] pixels, int width, int height, int stride, WpfPoint from, WpfPoint to, byte red, byte green, byte blue)
    {
        int x0 = Math.Clamp((int)Math.Round(from.X), 0, width - 1);
        int y0 = Math.Clamp((int)Math.Round(from.Y), 0, height - 1);
        int x1 = Math.Clamp((int)Math.Round(to.X), 0, width - 1);
        int y1 = Math.Clamp((int)Math.Round(to.Y), 0, height - 1);
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            int index = y0 * stride + x0 * 4;
            pixels[index] = blue;
            pixels[index + 1] = green;
            pixels[index + 2] = red;
            pixels[index + 3] = 255;
            if (x0 == x1 && y0 == y1)
            {
                break;
            }

            int error2 = 2 * error;
            if (error2 >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (error2 <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    private static BitmapSource CreateBitmap(int width, int height, double dpiX, double dpiY, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, dpiX, dpiY, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed record ShapeBalanceDebugReportDto(
        double FaceRollAngle,
        double FaceYawLikeBias,
        double FacePitchLikeBias,
        double EyeLevelDelta,
        double EyebrowLevelDelta,
        double MouthCornerDelta,
        double NoseLineTilt,
        double ChinCenterDelta,
        double LeftRightBalanceScore,
        bool ShapeBalanceApplied,
        double ShapeBalanceStrength,
        double BalancedMaskQualityScore,
        NostrilBalanceObservation NostrilBalanceObservation,
        IReadOnlyList<string> DebugWarnings);
}
