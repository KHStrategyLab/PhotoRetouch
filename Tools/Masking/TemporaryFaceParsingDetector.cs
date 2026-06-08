using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class TemporaryFaceParsingDetector : IFaceParsingDetector
{
    public string DetectorVersion => "temporary_face_parsing_v1";

    public ParsingMaskSet? Detect(BitmapSource source, FaceParsingInput input)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(input);

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bitmap.CopyPixels(pixels, stride, 0);

        MaskPlane skin = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            Math.Max(
                EllipseWeight(nx, ny, 0, 0.03, 0.72, 0.98),
                EllipseWeight(nx, ny, 0, 1.02, 0.50, 0.32) * SmoothStep(0.62, 0.86, ny)));
        MaskPlane leftEye = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, -0.42, -0.38, 0.18, 0.105));
        MaskPlane rightEye = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0.42, -0.38, 0.18, 0.105));
        MaskPlane leftBrow = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, -0.42, -0.56, 0.22, 0.075));
        MaskPlane rightBrow = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0.42, -0.56, 0.22, 0.075));
        MaskPlane upperLip = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0, 0.32, 0.31, 0.075));
        MaskPlane lowerLip = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0, 0.42, 0.29, 0.085));
        MaskPlane innerMouth = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0, 0.37, 0.19, 0.035));
        MaskPlane hair = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            Math.Max(
                EllipseWeight(nx, ny, 0, -0.86, 0.76, 0.32),
                Math.Max(
                    EllipseWeight(nx, ny, -0.72, -0.20, 0.20, 0.58),
                    EllipseWeight(nx, ny, 0.72, -0.20, 0.20, 0.58))));
        MaskPlane neck = BuildMask(width, height, input, (nx, ny, _, _, _) =>
            EllipseWeight(nx, ny, 0, 1.05, 0.46, 0.34));
        MaskPlane beard = BuildMask(width, height, input, (nx, ny, x, y, pixelIndex) =>
        {
            if (ny < 0.30 || ny > 0.90 || Math.Abs(nx) > 0.55)
            {
                return 0;
            }

            byte blue = pixels[pixelIndex];
            byte green = pixels[pixelIndex + 1];
            byte red = pixels[pixelIndex + 2];
            double luminance = (red * 0.299 + green * 0.587 + blue * 0.114) / 255d;
            double lowerFace = EllipseWeight(nx, ny, 0, 0.62, 0.52, 0.36);
            return lowerFace * (1 - SmoothStep(0.28, 0.56, luminance));
        });
        MaskPlane mustache = BuildMask(width, height, input, (nx, ny, x, y, pixelIndex) =>
        {
            if (ny < 0.12 || ny > 0.34 || Math.Abs(nx) > 0.34)
            {
                return 0;
            }

            byte blue = pixels[pixelIndex];
            byte green = pixels[pixelIndex + 1];
            byte red = pixels[pixelIndex + 2];
            double luminance = (red * 0.299 + green * 0.587 + blue * 0.114) / 255d;
            return EllipseWeight(nx, ny, 0, 0.23, 0.30, 0.10) * (1 - SmoothStep(0.25, 0.52, luminance));
        });

        MaskPlane background = MaskPlane.Subtract(CreateFullMask(width, height), MaskPlane.Union(skin, hair, neck));

        return new ParsingMaskSet(
            skin,
            leftEye,
            rightEye,
            leftBrow,
            rightBrow,
            upperLip,
            lowerLip,
            innerMouth,
            hair,
            neck,
            null,
            beard,
            mustache,
            null,
            background,
            0.30,
            new[] { "temporary_face_parsing_detector", "face_parsing_model_not_connected" });
    }

    private static MaskPlane CreateFullMask(int width, int height)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        Array.Fill(mask.Values, 1d);
        return mask;
    }

    private static MaskPlane BuildMask(int width, int height, FaceParsingInput input, Func<double, double, int, int, int, double> valueFactory)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        int stride = width * 4;
        double centerX = input.FaceBox.X + input.FaceBox.Width / 2d;
        double centerY = input.FaceBox.Y + input.FaceBox.Height / 2d;
        double angle = input.FaceAngle * Math.PI / 180d;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double radiusX = Math.Max(1, input.FaceBox.Width / 2d);
        double radiusY = Math.Max(1, input.FaceBox.Height / 2d);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double normalizedX = (dx * cos + dy * sin) / radiusX;
                double normalizedY = (-dx * sin + dy * cos) / radiusY;
                int pixelIndex = y * stride + x * 4;
                mask[x, y] = valueFactory(normalizedX, normalizedY, x, y, pixelIndex);
            }
        }

        return mask;
    }

    private static double EllipseWeight(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        double dx = (x - centerX) / radiusX;
        double dy = (y - centerY) / radiusY;
        double distance = Math.Sqrt(dx * dx + dy * dy);
        return 1 - SmoothStep(0.78, 1.08, distance);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        double normalized = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return normalized * normalized * (3 - 2 * normalized);
    }
}
