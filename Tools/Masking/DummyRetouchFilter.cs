using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public static class DummyRetouchFilter
{
    public static BitmapSource Apply(BitmapSource source, FaceSnapshotMaskSet snapshot, double stage)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(snapshot);

        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();

        int width = bitmap.PixelWidth;
        int height = bitmap.PixelHeight;
        FaceMaskSet masks = snapshot.Masks;
        if (masks.RetouchAllowMask.Width != width || masks.RetouchAllowMask.Height != height)
        {
            throw new InvalidOperationException("Snapshot mask size must match the preview source size.");
        }

        int stride = width * 4;
        byte[] sourcePixels = new byte[stride * height];
        byte[] outputPixels = new byte[sourcePixels.Length];
        bitmap.CopyPixels(sourcePixels, stride, 0);
        Buffer.BlockCopy(sourcePixels, 0, outputPixels, 0, sourcePixels.Length);

        double stageStrength = Math.Clamp((stage - 1) / 9d, 0, 1);
        double brightnessLift = 5 + stageStrength * 24;
        double colorBlend = 0.035 + stageStrength * 0.12;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * stride + x * 4;
                double allow = masks.RetouchAllowMask[x, y] * (1 - masks.HardProtectMask[x, y]);
                if (allow <= 0)
                {
                    continue;
                }

                double amount = Math.Clamp(allow, 0, 1);
                byte blue = sourcePixels[index];
                byte green = sourcePixels[index + 1];
                byte red = sourcePixels[index + 2];

                double average = (red + green + blue) / 3d;
                outputPixels[index] = AdjustChannel(blue, average, brightnessLift, colorBlend, amount);
                outputPixels[index + 1] = AdjustChannel(green, average, brightnessLift, colorBlend, amount);
                outputPixels[index + 2] = AdjustChannel(red, average, brightnessLift, colorBlend, amount);
            }
        }

        BitmapSource output = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, outputPixels, stride);
        output.Freeze();
        return output;
    }

    private static byte AdjustChannel(byte source, double average, double brightnessLift, double colorBlend, double amount)
    {
        double toneEven = source + (average - source) * colorBlend;
        double lifted = toneEven + brightnessLift;
        return (byte)Math.Clamp((int)Math.Round(source + (lifted - source) * amount), 0, 255);
    }
}
