using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch.AnchorMesh;

public static class AnchorMeshMaskImageLoader
{
    public static MaskPlane LoadPngMask(string path)
    {
        BitmapImage bitmap = new();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return FromBitmapSource(bitmap);
    }

    public static MaskPlane FromBitmapSource(BitmapSource source)
    {
        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bgraSource.Freeze();

        int width = bgraSource.PixelWidth;
        int height = bgraSource.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];
        bgraSource.CopyPixels(pixels, stride, 0);

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
