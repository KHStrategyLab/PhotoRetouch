using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class HeuristicPortraitMaskEngine : IPortraitMaskEngine
{
    public string MaskVersion => "heuristic_portrait_mask_v1";

    public PortraitMaskResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        FaceWorkArea area = faceWorkArea.Clamp();
        Int32Rect faceBox = CreateFaceBox(area, width, height);
        IReadOnlyDictionary<string, WpfPoint> landmarks = CreateHeuristicLandmarks(area, width, height);

        MaskPlane skinMask = BuildSkinMask(pixels, width, height, stride, area);
        MaskPlane eyeMask = BuildEyeMask(width, height, area);
        MaskPlane eyebrowMask = BuildEyebrowMask(width, height, area);
        MaskPlane lipMask = BuildLipMask(width, height, area);
        MaskPlane innerMouthMask = BuildInnerMouthMask(width, height, area);
        MaskPlane teethMask = BuildTeethMask(pixels, width, height, stride, innerMouthMask);
        MaskPlane noseMask = BuildNoseMask(width, height, area);
        MaskPlane nostrilMask = BuildNostrilMask(pixels, width, height, stride, area);
        MaskPlane noseShadowMask = BuildNoseShadowMask(pixels, width, height, stride, area);
        MaskPlane hairMask = BuildHairMask(pixels, width, height, stride, area);
        MaskPlane beardMask = BuildBeardMask(pixels, width, height, stride, area);
        MaskPlane mustacheMask = BuildMustacheMask(pixels, width, height, stride, area);
        MaskPlane glassesMask = MaskPlane.Empty(width, height);

        MaskPlane hardProtectMask = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            teethMask,
            nostrilMask,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask);
        MaskPlane softProtectMask = BuildSoftProtectMask(width, height, area, noseShadowMask);
        MaskPlane noseSkinMask = MaskPlane.Subtract(noseMask, MaskPlane.Union(nostrilMask, noseShadowMask));
        MaskPlane retouchAllowMask = MaskPlane.Subtract(MaskPlane.Union(skinMask, noseSkinMask), hardProtectMask);
        MaskPlane finalOverlayMask = MaskPlane.Subtract(MaskPlane.Union(retouchAllowMask, MaskPlane.Multiply(softProtectMask, 0.45)), hardProtectMask);

        List<string> warnings = new()
        {
            "heuristic_mask_engine",
            "face_detection_not_ai",
            "face_parsing_not_connected"
        };

        FaceAnalysisResult analysis = FaceAnalysisResult.Heuristic(faceBox, landmarks, warnings);
        FaceMaskSet masks = new(
            skinMask,
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            teethMask,
            noseMask,
            noseSkinMask,
            nostrilMask,
            noseShadowMask,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask,
            hardProtectMask,
            softProtectMask,
            retouchAllowMask,
            finalOverlayMask);
        MaskQualityReport report = MaskQualityReport.FromMasks(analysis, masks);
        return new PortraitMaskResult(analysis, masks, report);
    }

    private static Int32Rect CreateFaceBox(FaceWorkArea area, int width, int height)
    {
        int faceWidth = Math.Max(1, (int)Math.Round(area.Width * width));
        int faceHeight = Math.Max(1, (int)Math.Round(area.Height * height));
        int x = Math.Clamp((int)Math.Round(area.CenterX * width - faceWidth / 2d), 0, Math.Max(0, width - faceWidth));
        int y = Math.Clamp((int)Math.Round(area.CenterY * height - faceHeight / 2d), 0, Math.Max(0, height - faceHeight));
        return new Int32Rect(x, y, faceWidth, faceHeight);
    }

    private static IReadOnlyDictionary<string, WpfPoint> CreateHeuristicLandmarks(FaceWorkArea area, int width, int height)
    {
        Dictionary<string, WpfPoint> landmarks = new()
        {
            ["left_eye"] = ToImagePoint(area, width, height, -0.42, -0.38),
            ["right_eye"] = ToImagePoint(area, width, height, 0.42, -0.38),
            ["nose_tip"] = ToImagePoint(area, width, height, 0, 0.02),
            ["left_nostril"] = ToImagePoint(area, width, height, -0.14, 0.18),
            ["right_nostril"] = ToImagePoint(area, width, height, 0.14, 0.18),
            ["mouth_center"] = ToImagePoint(area, width, height, 0, 0.36),
            ["chin"] = ToImagePoint(area, width, height, 0, 0.83)
        };
        return landmarks;
    }

    private static MaskPlane BuildSkinMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double faceWeight = EllipseWeight(nx, ny, 0, 0.02, 0.76, 1.03);
            double neckWeight = EllipseWeight(nx, ny, 0, 1.04, 0.56, 0.46) * SmoothStep(0.58, 0.86, ny);
            int index = y * stride + x * 4;
            byte blue = pixels[index];
            byte green = pixels[index + 1];
            byte red = pixels[index + 2];
            double colorWeight = SkinColorWeight(red, green, blue);
            return Math.Max(faceWeight, neckWeight * 0.7) * colorWeight;
        });
    }

    private static MaskPlane BuildEyeMask(int width, int height, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            return Math.Max(
                EllipseWeight(nx, ny, -0.42, -0.38, 0.27, 0.16),
                EllipseWeight(nx, ny, 0.42, -0.38, 0.27, 0.16));
        });
    }

    private static MaskPlane BuildEyebrowMask(int width, int height, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            return Math.Max(
                EllipseWeight(nx, ny, -0.42, -0.58, 0.32, 0.12),
                EllipseWeight(nx, ny, 0.42, -0.58, 0.32, 0.12));
        });
    }

    private static MaskPlane BuildLipMask(int width, int height, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            return EllipseWeight(nx, ny, 0, 0.34, 0.38, 0.15);
        });
    }

    private static MaskPlane BuildInnerMouthMask(int width, int height, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            return EllipseWeight(nx, ny, 0, 0.36, 0.23, 0.06);
        });
    }

    private static MaskPlane BuildTeethMask(byte[] pixels, int width, int height, int stride, MaskPlane innerMouthMask)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            double mouthWeight = innerMouthMask[x, y];
            if (mouthWeight <= 0)
            {
                return 0;
            }

            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            return mouthWeight * SmoothStep(168, 228, luminance);
        });
    }

    private static MaskPlane BuildNoseMask(int width, int height, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double bridge = EllipseWeight(nx, ny, 0, -0.18, 0.22, 0.52);
            double tip = EllipseWeight(nx, ny, 0, 0.14, 0.34, 0.25);
            return Math.Max(bridge, tip);
        });
    }

    private static MaskPlane BuildNostrilMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double roi = EllipseWeight(nx, ny, 0, 0.20, 0.34, 0.18);
            if (roi <= 0)
            {
                return 0;
            }

            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            double darkWeight = 1 - SmoothStep(58, 128, luminance);
            double pairPrior = Math.Max(
                EllipseWeight(nx, ny, -0.14, 0.20, 0.10, 0.06),
                EllipseWeight(nx, ny, 0.14, 0.20, 0.10, 0.06));
            return Math.Max(pairPrior * 0.72, roi * darkWeight);
        });
    }

    private static MaskPlane BuildNoseShadowMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double region = EllipseWeight(nx, ny, 0, 0.26, 0.33, 0.13);
            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            return region * (1 - SmoothStep(72, 148, luminance));
        });
    }

    private static MaskPlane BuildHairMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double topRegion = SmoothStep(-1.20, -0.76, -ny) * (1 - SmoothStep(0.72, 1.04, Math.Abs(nx)));
            double sideRegion = SmoothStep(0.76, 1.04, Math.Abs(nx)) * (1 - SmoothStep(1.20, 1.60, Math.Abs(nx))) * (1 - SmoothStep(0.12, 0.68, ny));
            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            double darkWeight = 1 - SmoothStep(54, 130, luminance);
            return Math.Max(topRegion, sideRegion) * darkWeight;
        });
    }

    private static MaskPlane BuildBeardMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double lowerRegion = EllipseWeight(nx, ny, 0, 0.64, 0.46, 0.30);
            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            return lowerRegion * (1 - SmoothStep(70, 150, luminance)) * 0.72;
        });
    }

    private static MaskPlane BuildMustacheMask(byte[] pixels, int width, int height, int stride, FaceWorkArea area)
    {
        return BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double upperLipRegion = EllipseWeight(nx, ny, 0, 0.24, 0.34, 0.09);
            int index = y * stride + x * 4;
            double luminance = Luminance(pixels[index + 2], pixels[index + 1], pixels[index]);
            return upperLipRegion * (1 - SmoothStep(72, 148, luminance)) * 0.80;
        });
    }

    private static MaskPlane BuildSoftProtectMask(int width, int height, FaceWorkArea area, MaskPlane noseShadowMask)
    {
        MaskPlane structureMask = BuildFromFunction(width, height, (x, y) =>
        {
            (double nx, double ny) = NormalizeFacePoint(area, width, height, x, y);
            double underEye = Math.Max(
                EllipseWeight(nx, ny, -0.42, -0.19, 0.29, 0.12),
                EllipseWeight(nx, ny, 0.42, -0.19, 0.29, 0.12));
            double nasolabial = Math.Max(
                EllipseWeight(nx, ny, -0.26, 0.28, 0.10, 0.40),
                EllipseWeight(nx, ny, 0.26, 0.28, 0.10, 0.40));
            double noseTip = EllipseWeight(nx, ny, 0, 0.12, 0.28, 0.20);
            double forehead = EllipseWeight(nx, ny, 0, -0.78, 0.46, 0.14) * 0.56;
            double neck = EllipseWeight(nx, ny, 0, 1.12, 0.48, 0.20) * 0.64;
            return Math.Max(Math.Max(Math.Max(underEye, nasolabial), noseTip), Math.Max(forehead, neck));
        });

        return MaskPlane.Union(structureMask, noseShadowMask);
    }

    private static MaskPlane BuildFromFunction(int width, int height, Func<int, int, double> valueFactory)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                mask[x, y] = valueFactory(x, y);
            }
        }

        return mask;
    }

    private static WpfPoint ToImagePoint(FaceWorkArea area, int width, int height, double normalizedX, double normalizedY)
    {
        double radiusX = area.Width * width / 2;
        double radiusY = area.Height * height / 2;
        return new WpfPoint(
            area.CenterX * (width - 1) + normalizedX * radiusX,
            area.CenterY * (height - 1) + normalizedY * radiusY);
    }

    private static (double X, double Y) NormalizeFacePoint(FaceWorkArea area, int width, int height, int x, int y)
    {
        double radiusX = Math.Max(1, area.Width * width / 2);
        double radiusY = Math.Max(1, area.Height * height / 2);
        return (
            (x - area.CenterX * (width - 1)) / radiusX,
            (y - area.CenterY * (height - 1)) / radiusY);
    }

    private static double EllipseWeight(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        double dx = (x - centerX) / radiusX;
        double dy = (y - centerY) / radiusY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        return 1 - SmoothStep(0.72, 1.18, distance);
    }

    private static double SkinColorWeight(byte red, byte green, byte blue)
    {
        double redMinusBlue = red - blue;
        double greenMinusBlue = green - blue;
        double redMinusGreen = red - green;
        double channelRange = Math.Max(red, Math.Max(green, blue)) - Math.Min(red, Math.Min(green, blue));
        double warmWeight = SmoothStep(-6, 22, redMinusBlue) * SmoothStep(-14, 16, greenMinusBlue);
        double balanceWeight = 1 - SmoothStep(64, 118, Math.Abs(redMinusGreen));
        double chromaWeight = Math.Max(0.58, SmoothStep(5, 22, channelRange));
        double luminanceWeight = SmoothStep(42, 90, Luminance(red, green, blue)) * (1 - SmoothStep(220, 248, Luminance(red, green, blue)));
        return Math.Clamp(warmWeight * balanceWeight * chromaWeight * luminanceWeight, 0, 1);
    }

    private static double Luminance(byte red, byte green, byte blue)
    {
        return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (Math.Abs(edge1 - edge0) < 0.000001)
        {
            return value < edge0 ? 0 : 1;
        }

        double normalized = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return normalized * normalized * (3 - 2 * normalized);
    }
}
