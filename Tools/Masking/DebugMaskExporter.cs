using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record AutoAiMaskPreviewOptions(
    double SkinSmoothAmount,
    double BlemishAmount,
    double ToneEvenAmount,
    double WrinkleAmount,
    double TextureAmount,
    double MaskOpacity = 1)
{
    public static AutoAiMaskPreviewOptions Default { get; } = new(0, 0, 0, 0, 0, 1);
}

public sealed record AutoAiMaskFilterLayers(
    MaskPlane? AverageColorDifferenceMask,
    MaskPlane? BlemishCandidateMask,
    MaskPlane? BlemishAppliedMask,
    MaskPlane? WrinkleCandidateMask,
    MaskPlane? WrinkleAppliedMask,
    MaskPlane? TextureRestoreMask,
    MaskPlane? TextureStrengthMap)
{
    public static AutoAiMaskFilterLayers Empty { get; } = new(null, null, null, null, null, null, null);
}

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
        SaveSkinToneDebugMasks(bitmap, result.Masks, outputDirectory);

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
        SaveBitmap(
            CreateMaskOnSolidBackgroundPreview(
                result.Masks.EyeMask,
                GetPreviewBackgroundColor(),
                90,
                170,
                255,
                0.92),
            Path.Combine(outputDirectory, "debug_eye_mask_on_preview_background.png"));
        SaveBitmap(CreateAutoAiMaskPreview(result.Masks), Path.Combine(outputDirectory, "debug_average_skin_mask_preview.png"));
        SaveBitmap(CreateRetouchLayerInspectionPreview(bitmap, result.Masks), Path.Combine(outputDirectory, "debug_retouch_layer_inspection.png"));
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

    public static BitmapSource CreateAverageColorMaskPreview(MaskPlane mask, System.Windows.Media.Color referenceColor)
    {
        int stride = mask.Width * 4;
        byte[] pixels = new byte[stride * mask.Height];
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                int index = y * stride + x * 4;
                double amount = Math.Clamp(mask[x, y], 0, 1);
                pixels[index] = (byte)Math.Clamp((int)Math.Round(referenceColor.B * amount), 0, 255);
                pixels[index + 1] = (byte)Math.Clamp((int)Math.Round(referenceColor.G * amount), 0, 255);
                pixels[index + 2] = (byte)Math.Clamp((int)Math.Round(referenceColor.R * amount), 0, 255);
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(mask.Width, mask.Height, pixels);
    }

    public static BitmapSource CreateSourceColorMaskPreview(BitmapSource source, MaskPlane mask, double opacity)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (width != mask.Width || height != mask.Height)
        {
            return CreateMaskPreview(mask);
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(sourcePixels, stride, 0);
        double maskOpacity = Math.Clamp(opacity, 0, 1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double amount = Math.Clamp(mask[x, y] * maskOpacity, 0, 1);
                pixels[index] = sourcePixels[index];
                pixels[index + 1] = sourcePixels[index + 1];
                pixels[index + 2] = sourcePixels[index + 2];
                pixels[index + 3] = ToByte(amount);
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    public static BitmapSource CreateSourceColorMaskOnBackgroundPreview(BitmapSource source, MaskPlane mask, double opacity, System.Windows.Media.Color backgroundColor)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (width != mask.Width || height != mask.Height)
        {
            return CreateMaskOnSolidBackgroundPreview(mask, backgroundColor, 80, 180, 255, opacity);
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(sourcePixels, stride, 0);
        double maskOpacity = Math.Clamp(opacity, 0, 1);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double sourceAlpha = sourcePixels[index + 3] / 255d;
                double amount = Math.Clamp(mask[x, y] * maskOpacity * sourceAlpha, 0, 1);
                pixels[index] = Blend(backgroundColor.B, sourcePixels[index], amount);
                pixels[index + 1] = Blend(backgroundColor.G, sourcePixels[index + 1], amount);
                pixels[index + 2] = Blend(backgroundColor.R, sourcePixels[index + 2], amount);
                pixels[index + 3] = 255;
            }
        }

        return CreateBitmap(width, height, pixels);
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

    public static BitmapSource CreateFeatureMeshOverlayPreview(BitmapSource source, FaceFeatureMeshSet meshes)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        DrawMesh(pixels, width, height, stride, meshes.EyeMesh, 90, 170, 255);
        DrawMesh(pixels, width, height, stride, meshes.BrowMesh, 230, 170, 70);
        DrawMesh(pixels, width, height, stride, meshes.NoseMesh, 255, 215, 70);
        DrawMesh(pixels, width, height, stride, meshes.LipMesh, 235, 80, 130);

        return CreateBitmap(width, height, pixels);
    }

    public static BitmapSource CreateMaskOverlayOnBackgroundPreview(
        BitmapSource source,
        MaskPlane mask,
        System.Windows.Media.Color backgroundColor,
        byte red,
        byte green,
        byte blue,
        double opacity)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        if (width != mask.Width || height != mask.Height)
        {
            return CreateMaskPreview(mask);
        }

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(sourcePixels, stride, 0);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double sourceAlpha = sourcePixels[index + 3] / 255d;
                pixels[index] = Blend(backgroundColor.B, sourcePixels[index], sourceAlpha);
                pixels[index + 1] = Blend(backgroundColor.G, sourcePixels[index + 1], sourceAlpha);
                pixels[index + 2] = Blend(backgroundColor.R, sourcePixels[index + 2], sourceAlpha);
                pixels[index + 3] = 255;
                ApplyOverlay(pixels, index, red, green, blue, mask[x, y] * opacity);
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    public static BitmapSource CreateMaskOnSolidBackgroundPreview(
        MaskPlane mask,
        System.Windows.Media.Color backgroundColor,
        byte red,
        byte green,
        byte blue,
        double opacity)
    {
        int stride = mask.Width * 4;
        byte[] pixels = new byte[stride * mask.Height];
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                int index = y * stride + x * 4;
                pixels[index] = backgroundColor.B;
                pixels[index + 1] = backgroundColor.G;
                pixels[index + 2] = backgroundColor.R;
                pixels[index + 3] = 255;
                ApplyOverlay(pixels, index, red, green, blue, mask[x, y] * opacity);
            }
        }

        return CreateBitmap(mask.Width, mask.Height, pixels);
    }

    public static BitmapSource CreateRetouchLayerInspectionPreview(BitmapSource source, FaceMaskSet masks)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(sourcePixels, stride, 0);
        Array.Fill<byte>(pixels, 255);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double hardProtect = masks.HardProtectMask[x, y];
                double hair = masks.HairMask[x, y];
                double beard = Math.Max(masks.BeardMask[x, y], masks.MustacheMask[x, y]);
                double feature = hardProtect;
                feature = Math.Max(feature, masks.EyeMask[x, y]);
                feature = Math.Max(feature, masks.EyebrowMask[x, y]);
                feature = Math.Max(feature, masks.LipMask[x, y]);
                feature = Math.Max(feature, masks.InnerMouthMask[x, y]);
                feature = Math.Max(feature, masks.NostrilMask[x, y]);
                feature = Math.Max(feature, masks.GlassesMask[x, y]);

                double visible = Math.Max(feature, Math.Max(hair * 0.82, beard * 0.58));
                if (visible <= 0.001)
                {
                    continue;
                }

                byte sourceBlue = sourcePixels[index];
                byte sourceGreen = sourcePixels[index + 1];
                byte sourceRed = sourcePixels[index + 2];
                double luminance = sourceRed * 0.299 + sourceGreen * 0.587 + sourceBlue * 0.114;

                double hairAmount = Math.Clamp(Math.Max(hair * 0.76, beard * 0.42), 0, 1);
                if (hairAmount > 0)
                {
                    byte hairDetail = (byte)Math.Clamp((int)Math.Round(255 - (255 - luminance) * 0.36), 172, 255);
                    pixels[index] = Blend(pixels[index], hairDetail, hairAmount);
                    pixels[index + 1] = Blend(pixels[index + 1], hairDetail, hairAmount);
                    pixels[index + 2] = Blend(pixels[index + 2], hairDetail, hairAmount);
                }

                double featureAmount = Math.Clamp(Math.Max(feature, hardProtect) * 0.84, 0, 1);
                if (featureAmount > 0)
                {
                    byte protectedDetail = (byte)Math.Clamp((int)Math.Round(255 - (255 - luminance) * 0.20), 204, 255);
                    pixels[index] = Blend(pixels[index], protectedDetail, featureAmount);
                    pixels[index + 1] = Blend(pixels[index + 1], protectedDetail, featureAmount);
                    pixels[index + 2] = Blend(pixels[index + 2], protectedDetail, featureAmount);
                }
            }
        }

        return CreateBitmap(width, height, pixels);
    }

    public static BitmapSource CreateAutoAiMaskPreview(FaceMaskSet masks)
    {
        return CreateAutoAiMaskPreview(masks, AutoAiMaskPreviewOptions.Default, AutoAiMaskFilterLayers.Empty);
    }

    public static BitmapSource CreateAutoAiMaskPreview(FaceMaskSet masks, AutoAiMaskPreviewOptions options)
    {
        return CreateAutoAiMaskPreview(masks, options, AutoAiMaskFilterLayers.Empty);
    }

    public static BitmapSource CreateAutoAiMaskPreview(FaceMaskSet masks, AutoAiMaskPreviewOptions options, AutoAiMaskFilterLayers layers)
    {
        int width = masks.SkinMask.Width;
        int height = masks.SkinMask.Height;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        if (layers.AverageColorDifferenceMask is { } averageMask)
        {
            double averageMaskOpacity = Math.Clamp(options.MaskOpacity, 0, 1);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * stride + x * 4;
                    byte value = ToByte(averageMask[x, y] * averageMaskOpacity);
                    pixels[index] = value;
                    pixels[index + 1] = value;
                    pixels[index + 2] = value;
                    pixels[index + 3] = 255;
                }
            }

            return CreateBitmap(width, height, pixels);
        }

        double skinSmooth = Math.Clamp(options.SkinSmoothAmount, 0, 1);
        double blemish = Math.Clamp(options.BlemishAmount, 0, 1);
        double tone = Math.Clamp(options.ToneEvenAmount, 0, 1);
        double wrinkle = Math.Clamp(options.WrinkleAmount, 0, 1);
        double texture = Math.Clamp(options.TextureAmount, 0, 1);
        double maskOpacity = Math.Clamp(options.MaskOpacity, 0, 1);
        double activeRetouch = Math.Max(
            Math.Max(skinSmooth, blemish),
            Math.Max(tone, Math.Max(wrinkle, texture)));
        double idleLayerOpacity = activeRetouch > 0.001 ? 0.10 : 0.22;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                pixels[index] = 18;
                pixels[index + 1] = 20;
                pixels[index + 2] = 22;
                pixels[index + 3] = 255;

                double hair = masks.HairMask[x, y];
                double beard = Math.Max(masks.BeardMask[x, y], masks.MustacheMask[x, y]);
                double hardProtect = masks.HardProtectMask[x, y];
                double feature = hardProtect;
                feature = Math.Max(feature, masks.EyeMask[x, y]);
                feature = Math.Max(feature, masks.EyebrowMask[x, y]);
                feature = Math.Max(feature, masks.LipMask[x, y]);
                feature = Math.Max(feature, masks.InnerMouthMask[x, y]);
                feature = Math.Max(feature, masks.TeethMask[x, y]);
                feature = Math.Max(feature, masks.NostrilMask[x, y]);
                feature = Math.Max(feature, masks.GlassesMask[x, y]);

                double blemishCandidate = layers.BlemishCandidateMask?[x, y] ?? 0;
                double blemishApplied = layers.BlemishAppliedMask?[x, y] ?? 0;
                double wrinkleCandidate = layers.WrinkleCandidateMask?[x, y] ?? 0;
                double wrinkleApplied = layers.WrinkleAppliedMask?[x, y] ?? 0;
                double textureCandidate = layers.TextureRestoreMask?[x, y] ?? 0;
                double textureStrength = layers.TextureStrengthMap?[x, y] ?? 0;
                double averageColorDifference = (layers.AverageColorDifferenceMask?[x, y] ?? 0) * maskOpacity;

                double maskAmount = Math.Clamp(0.72 + activeRetouch * 0.28, 0, 1);
                double smoothLayer = averageColorDifference * skinSmooth;
                double blemishLayer = Math.Max(averageColorDifference, Math.Max(blemishCandidate * 0.72, blemishApplied)) * Math.Max(blemish, 0.35);
                double toneLayer = averageColorDifference * Math.Max(tone, 0.35);
                double wrinkleLayer = Math.Max(averageColorDifference * 0.42, Math.Max(wrinkleCandidate * 0.62, wrinkleApplied)) * wrinkle;
                double textureLayer = Math.Max(averageColorDifference * 0.38, Math.Max(textureCandidate * 0.55, textureStrength)) * texture;
                double activeLayer = Math.Clamp(
                    Math.Max(averageColorDifference * maskAmount,
                        Math.Max(
                            Math.Max(smoothLayer, blemishLayer),
                            Math.Max(toneLayer, Math.Max(wrinkleLayer, textureLayer)))),
                    0,
                    1);

                ApplyOverlay(pixels, index, 184, 192, 198, activeLayer * 0.92);
                ApplyOverlay(pixels, index, 205, 211, 216, Math.Max(hair * 0.78, beard * 0.46));
                ApplyOverlay(pixels, index, 242, 245, 247, feature * 0.96);
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

    private static void SaveSkinToneDebugMasks(BitmapSource source, FaceMaskSet masks, string outputDirectory)
    {
        SkinToneMaskSet skinTone = SkinToneMaskBuilder.Build(masks);
        SaveMask(skinTone.SkinToneApplyMask, Path.Combine(outputDirectory, "debug_skin_tone_mask.png"));
        SaveBitmap(CreateMaskOverlayPreview(source, skinTone.SkinToneApplyMask, 70, 220, 120, 0.58), Path.Combine(outputDirectory, "debug_skin_tone_overlay.png"));
        SaveMask(skinTone.FaceOnlyWarpMask, Path.Combine(outputDirectory, "debug_face_only_warp_mask.png"));
        SaveMask(skinTone.HairExcludedMask, Path.Combine(outputDirectory, "debug_hair_excluded_mask.png"));
        SaveMask(skinTone.GlassesExcludedMask, Path.Combine(outputDirectory, "debug_glasses_excluded_mask.png"));
        SaveMask(skinTone.NostrilExcludedMask, Path.Combine(outputDirectory, "debug_nostril_excluded_mask.png"));
        SaveMask(skinTone.LipExcludedMask, Path.Combine(outputDirectory, "debug_lip_excluded_mask.png"));
        SaveMask(skinTone.BeardShadowMask, Path.Combine(outputDirectory, "debug_beard_shadow_mask.png"));
        SaveMask(skinTone.NoseStructureProtectMask, Path.Combine(outputDirectory, "debug_nose_structure_protect_mask.png"));
        SaveMask(skinTone.NoseShadowMask, Path.Combine(outputDirectory, "debug_nose_shadow_mask.png"));
        SaveMask(skinTone.NoseRetouchStrengthMap, Path.Combine(outputDirectory, "debug_nose_retouch_strength_map.png"));
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
                byte green = 0;
                byte blue = 0;
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

    private static void DrawMesh(byte[] pixels, int width, int height, int stride, FaceFeatureMesh mesh, byte red, byte green, byte blue)
    {
        foreach (IGrouping<string, FeatureMeshPoint> group in mesh.Points.GroupBy(point => point.Role))
        {
            FeatureMeshPoint[] points = group.OrderBy(point => point.Index).ToArray();
            if (points.Length == 0)
            {
                continue;
            }

            for (int index = 0; index < points.Length; index++)
            {
                FeatureMeshPoint current = points[index];
                FeatureMeshPoint next = points[(index + 1) % points.Length];
                DrawLine(
                    pixels,
                    width,
                    height,
                    stride,
                    (int)Math.Round(current.X),
                    (int)Math.Round(current.Y),
                    (int)Math.Round(next.X),
                    (int)Math.Round(next.Y),
                    red,
                    green,
                    blue);
                DrawPoint(pixels, width, height, stride, (int)Math.Round(current.X), (int)Math.Round(current.Y), red, green, blue);
            }
        }
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

    private static System.Windows.Media.Color GetPreviewBackgroundColor()
    {
        string colorText = PreviewBackgroundSettings.BackgroundColor.Trim();
        if (!colorText.StartsWith('#') && colorText.Length is 6 or 8)
        {
            colorText = "#" + colorText;
        }

        try
        {
            if (System.Windows.Media.ColorConverter.ConvertFromString(colorText) is System.Windows.Media.Color color)
            {
                return color;
            }
        }
        catch (FormatException)
        {
        }

        return System.Windows.Media.Color.FromRgb(16, 17, 18);
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
