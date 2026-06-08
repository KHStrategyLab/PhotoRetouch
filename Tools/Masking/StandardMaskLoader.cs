using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class StandardMaskLoader
{
    private const int GeneratedMaskSize = 512;
    private readonly string[] _resourceDirectories;
    private StandardMaskSet? _cachedMaskSet;

    public StandardMaskLoader()
        : this(new[]
        {
            Path.Combine(AppContext.BaseDirectory, "StandardMasks"),
            Path.Combine(Environment.CurrentDirectory, "StandardMasks")
        })
    {
    }

    public StandardMaskLoader(IEnumerable<string> resourceDirectories)
    {
        _resourceDirectories = resourceDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public StandardMaskSet Load()
    {
        if (_cachedMaskSet is not null)
        {
            return _cachedMaskSet;
        }

        List<string> warnings = new();
        MaskPlane skin = LoadOrGenerate("standard_skin_mask", warnings, GenerateSkinMask);
        MaskPlane eye = LoadOrGenerate("standard_eye_protect_mask", warnings, GenerateEyeProtectMask);
        MaskPlane eyebrow = LoadOrGenerate("standard_eyebrow_protect_mask", warnings, GenerateEyebrowProtectMask);
        MaskPlane lip = LoadOrGenerate("standard_lip_protect_mask", warnings, GenerateLipProtectMask);
        MaskPlane nose = LoadOrGenerate("standard_nose_mask", warnings, GenerateNoseMask);
        MaskPlane nostril = LoadOrGenerate("standard_nostril_mask", warnings, GenerateNostrilProtectMask);
        MaskPlane soft = LoadOrGenerate("standard_soft_protect_mask", warnings, GenerateSoftProtectMask);

        _cachedMaskSet = new StandardMaskSet(
            skin,
            eye,
            eyebrow,
            lip,
            nose,
            nostril,
            soft,
            "standard_masks_v1",
            warnings);
        return _cachedMaskSet;
    }

    private MaskPlane LoadOrGenerate(string resourceName, List<string> warnings, Func<MaskPlane> fallbackFactory)
    {
        foreach (string directory in _resourceDirectories)
        {
            string path = Path.Combine(directory, resourceName + ".png");
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                return LoadPngMask(path);
            }
            catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException)
            {
                warnings.Add(resourceName + "_load_failed");
                return fallbackFactory();
            }
        }

        warnings.Add(resourceName + "_generated");
        return fallbackFactory();
    }

    private static MaskPlane LoadPngMask(string path)
    {
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();

        BitmapSource source = bitmap.Format == PixelFormats.Bgra32
            ? bitmap
            : new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        source.Freeze();

        int width = source.PixelWidth;
        int height = source.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        source.CopyPixels(pixels, stride, 0);

        MaskPlane mask = MaskPlane.Empty(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double blue = pixels[index];
                double green = pixels[index + 1];
                double red = pixels[index + 2];
                mask[x, y] = (red + green + blue) / (255d * 3d);
            }
        }

        return mask;
    }

    private static MaskPlane GenerateSkinMask()
    {
        return BuildGeneratedMask((x, y) =>
            Math.Max(
                EllipseWeight(x, y, 0.5, 0.51, 0.36, 0.49),
                EllipseWeight(x, y, 0.5, 0.99, 0.26, 0.24) * SmoothStep(0.58, 0.84, y)));
    }

    private static MaskPlane GenerateEyeProtectMask()
    {
        return BuildGeneratedMask((x, y) =>
            Math.Max(
                EllipseWeight(x, y, 0.29, 0.31, 0.13, 0.075),
                EllipseWeight(x, y, 0.71, 0.31, 0.13, 0.075)));
    }

    private static MaskPlane GenerateEyebrowProtectMask()
    {
        return BuildGeneratedMask((x, y) =>
            Math.Max(
                EllipseWeight(x, y, 0.29, 0.22, 0.16, 0.055),
                EllipseWeight(x, y, 0.71, 0.22, 0.16, 0.055)));
    }

    private static MaskPlane GenerateLipProtectMask()
    {
        return BuildGeneratedMask((x, y) => EllipseWeight(x, y, 0.5, 0.68, 0.19, 0.075));
    }

    private static MaskPlane GenerateNoseMask()
    {
        return BuildGeneratedMask((x, y) => EllipseWeight(x, y, 0.5, 0.48, 0.13, 0.24));
    }

    private static MaskPlane GenerateNostrilProtectMask()
    {
        return BuildGeneratedMask((x, y) =>
            Math.Max(
                EllipseWeight(x, y, 0.44, 0.58, 0.035, 0.025),
                EllipseWeight(x, y, 0.56, 0.58, 0.035, 0.025)));
    }

    private static MaskPlane GenerateSoftProtectMask()
    {
        return BuildGeneratedMask((x, y) =>
            Math.Max(
                Math.Max(
                    EllipseWeight(x, y, 0.29, 0.42, 0.14, 0.055),
                    EllipseWeight(x, y, 0.71, 0.42, 0.14, 0.055)),
                Math.Max(
                    EllipseWeight(x, y, 0.5, 0.53, 0.17, 0.12),
                    EllipseWeight(x, y, 0.5, 0.78, 0.28, 0.08))));
    }

    private static MaskPlane BuildGeneratedMask(Func<double, double, double> valueFactory)
    {
        MaskPlane mask = MaskPlane.Empty(GeneratedMaskSize, GeneratedMaskSize);
        for (int y = 0; y < GeneratedMaskSize; y++)
        {
            double normalizedY = y / (GeneratedMaskSize - 1d);
            for (int x = 0; x < GeneratedMaskSize; x++)
            {
                double normalizedX = x / (GeneratedMaskSize - 1d);
                mask[x, y] = valueFactory(normalizedX, normalizedY);
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
