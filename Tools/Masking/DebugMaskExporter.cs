using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public static class DebugMaskExporter
{
    public static void SaveAll(BitmapSource source, PortraitMaskResult result, string outputDirectory)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(result);

        Directory.CreateDirectory(outputDirectory);

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        SaveBitmap(bitmap, Path.Combine(outputDirectory, "debug_original.png"));
        SaveBitmap(CreateFaceBoxOverlay(bitmap, result.Analysis), Path.Combine(outputDirectory, "debug_face_box.png"));
        SaveBitmap(CreateLandmarkOverlay(bitmap, result.Analysis), Path.Combine(outputDirectory, "debug_landmarks.png"));
        SaveBitmap(CreateParsingPlaceholder(result.Masks), Path.Combine(outputDirectory, "debug_parsing.png"));
        SaveParsingDebugMasks(result, outputDirectory);
        SaveNostrilDebugMasks(bitmap, result, outputDirectory);
        SaveQualityDebugMasks(bitmap, result, outputDirectory);

        SaveMask(result.Masks.SkinMask, Path.Combine(outputDirectory, "debug_skin_mask.png"));
        SaveMask(result.Masks.EyeMask, Path.Combine(outputDirectory, "debug_eye_mask.png"));
        SaveMask(result.Masks.EyebrowMask, Path.Combine(outputDirectory, "debug_eyebrow_mask.png"));
        SaveMask(result.Masks.LipMask, Path.Combine(outputDirectory, "debug_lip_mask.png"));
        SaveMask(result.Masks.InnerMouthMask, Path.Combine(outputDirectory, "debug_inner_mouth_mask.png"));
        SaveMask(result.Masks.NoseMask, Path.Combine(outputDirectory, "debug_nose_mask.png"));
        SaveMask(result.Masks.NoseSkinMask, Path.Combine(outputDirectory, "debug_nose_skin_mask.png"));
        SaveMask(result.Masks.NostrilMask, Path.Combine(outputDirectory, "debug_nostril_mask.png"));
        SaveMask(result.Masks.HairMask, Path.Combine(outputDirectory, "debug_hair_mask.png"));
        SaveMask(result.Masks.BeardMask, Path.Combine(outputDirectory, "debug_beard_mask.png"));
        SaveMask(result.Masks.GlassesMask, Path.Combine(outputDirectory, "debug_glasses_mask.png"));
        SaveMask(result.Masks.HardProtectMask, Path.Combine(outputDirectory, "debug_hard_protect.png"));
        SaveMask(result.Masks.SoftProtectMask, Path.Combine(outputDirectory, "debug_soft_protect.png"));
        SaveMask(result.Masks.RetouchAllowMask, Path.Combine(outputDirectory, "debug_retouch_allow.png"));
        SaveBitmap(CreateFinalOverlay(bitmap, result.Masks), Path.Combine(outputDirectory, "debug_final_overlay.png"));
        SaveBitmap(CreateFinalOverlay(bitmap, result.Masks), Path.Combine(outputDirectory, "debug_final_overlay_after_parsing.png"));
        SaveBitmap(CreateFinalOverlay(bitmap, result.Masks), Path.Combine(outputDirectory, "debug_final_overlay_with_nostril.png"));
    }

    public static BitmapSource CreateMaskPreview(MaskPlane mask)
    {
        int stride = mask.Width * 4;
        byte[] pixels = new byte[stride * mask.Height];
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                int index = y * stride + x * 4;
                byte value = ToByte(mask[x, y]);
                pixels[index] = value;
                pixels[index + 1] = value;
                pixels[index + 2] = value;
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(mask.Width, mask.Height, pixels);
    }

    public static BitmapSource CreateFinalOverlayPreview(BitmapSource source, FaceMaskSet masks)
    {
        return CreateFinalOverlay(source, masks);
    }

    public static BitmapSource CreateMaskOverlayPreview(BitmapSource source, MaskPlane mask, byte red, byte green, byte blue, double opacity)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                ApplyOverlay(pixels, index, red, green, blue, mask[x, y] * opacity);
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static void SaveMask(MaskPlane mask, string path)
    {
        SaveBitmap(CreateMaskPreview(mask), path);
    }

    private static void SaveParsingDebugMasks(PortraitMaskResult result, string outputDirectory)
    {
        if (result.ParsingMasks is null)
        {
            return;
        }

        ParsingMaskSet parsing = result.ParsingMasks;
        SaveBitmap(CreateParsingLabelsPreview(result.Masks.SkinMask.Width, result.Masks.SkinMask.Height, parsing), Path.Combine(outputDirectory, "debug_parsing_raw.png"));
        SaveBitmap(CreateParsingLabelsPreview(result.Masks.SkinMask.Width, result.Masks.SkinMask.Height, parsing), Path.Combine(outputDirectory, "debug_parsing_labels.png"));
        SaveOptionalMask(parsing.SkinMask, Path.Combine(outputDirectory, "debug_parsing_skin_mask.png"));
        SaveMask(UnionOptional(result.Masks.SkinMask.Width, result.Masks.SkinMask.Height, parsing.LeftEyeMask, parsing.RightEyeMask), Path.Combine(outputDirectory, "debug_parsing_eye_mask.png"));
        SaveMask(UnionOptional(result.Masks.SkinMask.Width, result.Masks.SkinMask.Height, parsing.LeftEyebrowMask, parsing.RightEyebrowMask), Path.Combine(outputDirectory, "debug_parsing_eyebrow_mask.png"));
        SaveMask(UnionOptional(result.Masks.SkinMask.Width, result.Masks.SkinMask.Height, parsing.UpperLipMask, parsing.LowerLipMask), Path.Combine(outputDirectory, "debug_parsing_lip_mask.png"));
        SaveOptionalMask(parsing.InnerMouthMask, Path.Combine(outputDirectory, "debug_parsing_inner_mouth_mask.png"));
        SaveOptionalMask(parsing.HairMask, Path.Combine(outputDirectory, "debug_parsing_hair_mask.png"));

        if (result.WarpedStandardMasks is not null)
        {
            SaveBitmap(CreateParsingPlaceholder(result.WarpedStandardMasks), Path.Combine(outputDirectory, "debug_warped_standard_mask.png"));
        }

        SaveMask(result.Masks.EyeMask, Path.Combine(outputDirectory, "debug_merged_eye_mask.png"));
        SaveMask(result.Masks.EyebrowMask, Path.Combine(outputDirectory, "debug_merged_eyebrow_mask.png"));
        SaveMask(result.Masks.LipMask, Path.Combine(outputDirectory, "debug_merged_lip_mask.png"));
        SaveMask(result.Masks.SkinMask, Path.Combine(outputDirectory, "debug_merged_skin_mask.png"));
        SaveMask(result.Masks.HardProtectMask, Path.Combine(outputDirectory, "debug_hard_protect_after_parsing.png"));
        SaveMask(result.Masks.RetouchAllowMask, Path.Combine(outputDirectory, "debug_retouch_allow_after_parsing.png"));
    }

    private static void SaveNostrilDebugMasks(BitmapSource source, PortraitMaskResult result, string outputDirectory)
    {
        if (result.NostrilDetection is null)
        {
            return;
        }

        NostrilDetectorResult nostril = result.NostrilDetection;
        SaveBitmap(CreateRoiOverlay(source, nostril.NoseLowerRoi), Path.Combine(outputDirectory, "debug_nose_lower_roi.png"));
        SaveMask(nostril.DarkCandidateMask, Path.Combine(outputDirectory, "debug_nostril_dark_candidates.png"));
        SaveMask(nostril.ComponentMask, Path.Combine(outputDirectory, "debug_nostril_components.png"));
        if (result.WarpedStandardMasks is not null)
        {
            SaveMask(result.WarpedStandardMasks.NostrilMask, Path.Combine(outputDirectory, "debug_warped_standard_nostril.png"));
        }

        SaveMask(nostril.NostrilMask, Path.Combine(outputDirectory, "debug_final_nostril_mask.png"));
        SaveMask(result.Masks.HardProtectMask, Path.Combine(outputDirectory, "debug_hard_protect_with_nostril.png"));
    }

    private static void SaveQualityDebugMasks(BitmapSource source, PortraitMaskResult result, string outputDirectory)
    {
        SaveBitmap(CreateFaceBoxOverlay(source, result.Analysis), Path.Combine(outputDirectory, "debug_quality_face.png"));
        SaveBitmap(CreateLandmarkOverlay(source, result.Analysis), Path.Combine(outputDirectory, "debug_quality_landmark.png"));
        SaveMask(result.Masks.SkinMask, Path.Combine(outputDirectory, "debug_quality_skin_mask.png"));
        SaveMask(result.Masks.EyeMask, Path.Combine(outputDirectory, "debug_quality_eye_mask.png"));
        SaveMask(result.Masks.LipMask, Path.Combine(outputDirectory, "debug_quality_lip_mask.png"));
        SaveMask(result.Masks.NostrilMask, Path.Combine(outputDirectory, "debug_quality_nostril_mask.png"));
        SaveMask(result.Masks.HairMask, Path.Combine(outputDirectory, "debug_quality_hair_mask.png"));
        SaveMask(result.Masks.RetouchAllowMask, Path.Combine(outputDirectory, "debug_quality_retouch_allow.png"));
        SaveBitmap(CreateStageGateOverlay(source, result), Path.Combine(outputDirectory, "debug_stage_gate_overlay.png"));
        SaveMask(result.Masks.FinalOverlayMask, Path.Combine(outputDirectory, "debug_final_safe_mask.png"));
        SaveQualityReport(result.QualityReport, Path.Combine(outputDirectory, "debug_mask_quality_report.txt"));
    }

    private static BitmapSource CreateStageGateOverlay(BitmapSource source, PortraitMaskResult result)
    {
        BitmapSource overlay = CreateFinalOverlay(source, result.Masks);
        int width = overlay.PixelWidth;
        int height = overlay.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        overlay.CopyPixels(pixels, stride, 0);
        double quality = Math.Clamp(result.QualityReport.Score, 0, 1);
        double allowed = Math.Clamp(result.QualityReport.MaxAllowedStage / 10d, 0, 1);
        int barHeight = Math.Clamp(height / 80, 4, 18);
        DrawHorizontalBar(pixels, width, height, stride, 0, barHeight, quality, 70, 210, 110);
        DrawHorizontalBar(pixels, width, height, stride, barHeight + 2, barHeight, allowed, 255, 210, 70);
        return CreateBitmap(width, height, pixels);
    }

    private static void DrawHorizontalBar(byte[] pixels, int width, int height, int stride, int y, int barHeight, double amount, byte red, byte green, byte blue)
    {
        int barWidth = (int)Math.Round(width * Math.Clamp(amount, 0, 1));
        for (int row = y; row < Math.Min(height, y + barHeight); row++)
        {
            for (int x = 0; x < barWidth; x++)
            {
                int index = row * stride + x * 4;
                pixels[index] = Blend(pixels[index], blue, 0.78);
                pixels[index + 1] = Blend(pixels[index + 1], green, 0.78);
                pixels[index + 2] = Blend(pixels[index + 2], red, 0.78);
                pixels[index + 3] = 255;
            }
        }
    }

    private static void SaveQualityReport(MaskQualityReport report, string path)
    {
        string[] lines =
        {
            "MaskQualityReport",
            "OverallQualityScore: " + report.OverallQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "FaceQualityScore: " + report.FaceQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "LandmarkQualityScore: " + report.LandmarkQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "ParsingQualityScore: " + report.ParsingQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "SkinMaskQualityScore: " + report.SkinMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "EyeMaskQualityScore: " + report.EyeMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "EyebrowMaskQualityScore: " + report.EyebrowMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "LipMaskQualityScore: " + report.LipMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "NostrilMaskQualityScore: " + report.NostrilMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "HairMaskQualityScore: " + report.HairMaskQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "HardProtectQualityScore: " + report.HardProtectQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "RetouchAllowQualityScore: " + report.RetouchAllowQualityScore.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            "MaxAllowedStage: " + report.MaxAllowedStage,
            "IsSafeForStrongRetouch: " + report.IsSafeForStrongRetouch,
            "Warnings:",
            string.Join(Environment.NewLine, report.Warnings.Select(warning => "- " + warning)),
            "FatalErrors:",
            string.Join(Environment.NewLine, report.FatalErrors.Select(error => "- " + error))
        };
        File.WriteAllLines(path, lines, System.Text.Encoding.UTF8);
    }

    private static void SaveOptionalMask(MaskPlane? mask, string path)
    {
        if (mask is null)
        {
            return;
        }

        SaveMask(mask, path);
    }

    private static MaskPlane UnionOptional(int width, int height, params MaskPlane?[] masks)
    {
        MaskPlane[] availableMasks = masks
            .Where(mask => mask is not null)
            .Cast<MaskPlane>()
            .ToArray();
        return availableMasks.Length == 0
            ? MaskPlane.Empty(width, height)
            : MaskPlane.Union(availableMasks);
    }

    private static BitmapSource CreateParsingLabelsPreview(int width, int height, ParsingMaskSet parsing)
    {
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        ApplyLabelColor(pixels, width, height, parsing.SkinMask, 80, 210, 110, 0.72);
        ApplyLabelColor(pixels, width, height, parsing.HairMask, 70, 85, 95, 0.84);
        ApplyLabelColor(pixels, width, height, parsing.NeckMask, 80, 170, 160, 0.56);
        ApplyLabelColor(pixels, width, height, parsing.LeftEyeMask, 80, 170, 255, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.RightEyeMask, 80, 170, 255, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.LeftEyebrowMask, 45, 70, 95, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.RightEyebrowMask, 45, 70, 95, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.UpperLipMask, 230, 80, 120, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.LowerLipMask, 230, 80, 120, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.InnerMouthMask, 120, 30, 55, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.GlassesMask, 215, 215, 230, 0.95);
        ApplyLabelColor(pixels, width, height, parsing.BeardMask, 65, 70, 78, 0.88);
        ApplyLabelColor(pixels, width, height, parsing.MustacheMask, 65, 70, 78, 0.88);
        ApplyLabelColor(pixels, width, height, parsing.ClothMask, 75, 90, 180, 0.75);
        ApplyLabelColor(pixels, width, height, parsing.BackgroundMask, 35, 38, 42, 0.35);
        for (int index = 3; index < pixels.Length; index += 4)
        {
            if (pixels[index] == 0)
            {
                pixels[index] = 255;
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static void ApplyLabelColor(byte[] pixels, int width, int height, MaskPlane? mask, byte red, byte green, byte blue, double opacity)
    {
        if (mask is null)
        {
            return;
        }

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width * 4 + x * 4;
                double amount = Math.Clamp(mask[x, y] * opacity, 0, 1);
                if (amount <= 0)
                {
                    continue;
                }

                pixels[index] = Blend(pixels[index], blue, amount);
                pixels[index + 1] = Blend(pixels[index + 1], green, amount);
                pixels[index + 2] = Blend(pixels[index + 2], red, amount);
                pixels[index + 3] = 255;
            }
        }
    }

    private static BitmapSource CreateParsingPlaceholder(FaceMaskSet masks)
    {
        int width = masks.SkinMask.Width;
        int height = masks.SkinMask.Height;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                byte red = ToByte(Math.Max(masks.HardProtectMask[x, y], masks.LipMask[x, y]));
                byte green = ToByte(masks.SkinMask[x, y]);
                byte blue = ToByte(Math.Max(masks.SoftProtectMask[x, y], masks.NoseMask[x, y] * 0.7));
                pixels[index] = blue;
                pixels[index + 1] = green;
                pixels[index + 2] = red;
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static BitmapSource CreateRoiOverlay(BitmapSource source, Int32Rect roi)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        DrawRectangle(pixels, width, height, stride, roi, 255, 210, 40);
        for (int y = roi.Y; y < roi.Y + roi.Height; y++)
        {
            for (int x = roi.X; x < roi.X + roi.Width; x++)
            {
                int index = y * stride + x * 4;
                pixels[index] = Blend(pixels[index], 40, 0.18);
                pixels[index + 1] = Blend(pixels[index + 1], 210, 0.18);
                pixels[index + 2] = Blend(pixels[index + 2], 255, 0.18);
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static BitmapSource CreateFinalOverlay(BitmapSource source, FaceMaskSet masks)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                ApplyOverlay(pixels, index, 50, 210, 90, masks.RetouchAllowMask[x, y] * 0.42);
                ApplyOverlay(pixels, index, 255, 210, 50, masks.SoftProtectMask[x, y] * 0.48);
                ApplyOverlay(pixels, index, 235, 60, 70, masks.HardProtectMask[x, y] * 0.58);
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    private static BitmapSource CreateFaceBoxOverlay(BitmapSource source, FaceAnalysisResult analysis)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        DrawRectangle(pixels, width, height, stride, analysis.FaceBox, 255, 220, 40);
        return CreateBitmap(width, height, pixels);
    }

    private static BitmapSource CreateLandmarkOverlay(BitmapSource source, FaceAnalysisResult analysis)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);
        DrawRectangle(pixels, width, height, stride, analysis.FaceBox, 255, 220, 40);
        if (analysis.FaceLandmarks.TryGetValue("left_eye", out WpfPoint leftEye) &&
            analysis.FaceLandmarks.TryGetValue("right_eye", out WpfPoint rightEye))
        {
            DrawLine(
                pixels,
                width,
                height,
                stride,
                (int)Math.Round(leftEye.X),
                (int)Math.Round(leftEye.Y),
                (int)Math.Round(rightEye.X),
                (int)Math.Round(rightEye.Y),
                255,
                210,
                40);
        }

        if (analysis.FaceLandmarks.TryGetValue("nose_tip", out WpfPoint noseTip) &&
            analysis.FaceLandmarks.TryGetValue("mouth_center", out WpfPoint mouthCenter))
        {
            DrawLine(
                pixels,
                width,
                height,
                stride,
                (int)Math.Round(noseTip.X),
                (int)Math.Round(noseTip.Y),
                (int)Math.Round(mouthCenter.X),
                (int)Math.Round(mouthCenter.Y),
                80,
                210,
                255);
        }

        foreach (WpfPoint point in analysis.FaceLandmarks.Values)
        {
            DrawPoint(pixels, width, height, stride, (int)Math.Round(point.X), (int)Math.Round(point.Y), 50, 210, 255);
        }

        return CreateBitmap(width, height, pixels);
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

    private static void ApplyOverlay(byte[] pixels, int index, byte red, byte green, byte blue, double amount)
    {
        amount = Math.Clamp(amount, 0, 1);
        if (amount <= 0)
        {
            return;
        }

        pixels[index] = Blend(pixels[index], blue, amount);
        pixels[index + 1] = Blend(pixels[index + 1], green, amount);
        pixels[index + 2] = Blend(pixels[index + 2], red, amount);
    }

    private static byte Blend(byte source, byte target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * amount), 0, 255);
    }

    private static byte ToByte(double value)
    {
        return (byte)Math.Clamp((int)Math.Round(Math.Clamp(value, 0, 1) * 255), 0, 255);
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private static void SaveBitmap(BitmapSource bitmap, string path)
    {
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }
}
