using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class StandardMaskLoader
{
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
        MaskPlane skin = LoadOrEmpty("standard_skin_mask", warnings);
        MaskPlane eye = LoadOrEmpty("standard_eye_protect_mask", warnings);
        MaskPlane eyebrow = LoadOrEmpty("standard_eyebrow_protect_mask", warnings);
        MaskPlane lip = LoadOrEmpty("standard_lip_protect_mask", warnings);
        MaskPlane nose = LoadOrEmpty("standard_nose_mask", warnings);
        MaskPlane nostril = LoadOrEmpty("standard_nostril_mask", warnings);
        MaskPlane soft = LoadOrEmpty("standard_soft_protect_mask", warnings);

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

    private MaskPlane LoadOrEmpty(string resourceName, List<string> warnings)
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
                return MaskPlane.Empty(1, 1);
            }
        }

        warnings.Add(resourceName + "_missing_no_blob_fallback");
        return MaskPlane.Empty(1, 1);
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

}
