using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class HardProtectFinalRestoreFilter : IHardProtectFinalRestoreFilter
{
    private const double DiffTolerance = 2.5;

    public HardProtectFinalRestoreResult Apply(HardProtectFinalRestoreInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        int width = input.OriginalImage.PixelWidth;
        int height = input.OriginalImage.PixelHeight;
        int stride = width * 4;
        byte[] originalPixels = CopyPixels(input.OriginalImage);
        byte[] currentPixels = CopyPixels(input.CurrentRetouchedImage);
        byte[] finalPixels = (byte[])currentPixels.Clone();
        MaskPlane beforeDiffMask = MaskPlane.Empty(width, height);
        MaskPlane afterDiffMask = MaskPlane.Empty(width, height);

        int hardProtectPixelCount = 0;
        int restoredPixelCount = 0;
        int changedBeforeCount = 0;
        int changedAfterCount = 0;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double hardProtect = Math.Clamp(input.HardProtectMask[x, y], 0, 1);
                if (hardProtect <= 0)
                {
                    continue;
                }

                hardProtectPixelCount++;
                int index = y * stride + x * 4;
                double beforeDiff = GetColorDifference(originalPixels, currentPixels, index);
                if (beforeDiff > DiffTolerance)
                {
                    changedBeforeCount++;
                }

                beforeDiffMask[x, y] = Math.Clamp(beforeDiff / 96d, 0, 1) * hardProtect;
                finalPixels[index] = BlendChannel(currentPixels[index], originalPixels[index], hardProtect);
                finalPixels[index + 1] = BlendChannel(currentPixels[index + 1], originalPixels[index + 1], hardProtect);
                finalPixels[index + 2] = BlendChannel(currentPixels[index + 2], originalPixels[index + 2], hardProtect);
                finalPixels[index + 3] = originalPixels[index + 3];
                restoredPixelCount++;

                double afterDiff = GetColorDifference(originalPixels, finalPixels, index);
                if (afterDiff > DiffTolerance)
                {
                    changedAfterCount++;
                }

                afterDiffMask[x, y] = Math.Clamp(afterDiff / 96d, 0, 1) * hardProtect;
            }
        }

        HardProtectRestoreReport report = new(
            input.AppliedStage,
            hardProtectPixelCount,
            restoredPixelCount,
            changedBeforeCount,
            changedAfterCount,
            HasPartChanged(input.Snapshot.Masks.EyeMask, originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.EyebrowMask, originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.LipMask, originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.InnerMouthMask, originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.NostrilMask, originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.HairMask, originalPixels, finalPixels, width, height),
            HasPartChanged(MaskPlane.Union(input.Snapshot.Masks.BeardMask, input.Snapshot.Masks.MustacheMask), originalPixels, finalPixels, width, height),
            HasPartChanged(input.Snapshot.Masks.GlassesMask, originalPixels, finalPixels, width, height),
            changedAfterCount == 0,
            CreateWarnings(changedAfterCount, hardProtectPixelCount));

        return new HardProtectFinalRestoreResult(
            CreateBitmap(width, height, finalPixels),
            beforeDiffMask,
            afterDiffMask,
            report,
            report.DebugWarnings);
    }

    private static IReadOnlyList<string> CreateWarnings(int changedAfterCount, int hardProtectPixelCount)
    {
        List<string> warnings = new() { "hardprotect_final_restore_v1" };
        if (hardProtectPixelCount == 0)
        {
            warnings.Add("hardprotect_mask_empty");
        }

        if (changedAfterCount > 0)
        {
            warnings.Add("hardprotect_after_restore_diff_remaining");
        }

        return warnings;
    }

    private static bool HasPartChanged(MaskPlane partMask, byte[] originalPixels, byte[] finalPixels, int width, int height)
    {
        int stride = width * 4;
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (partMask[x, y] <= 0.10)
                {
                    continue;
                }

                int index = y * stride + x * 4;
                if (GetColorDifference(originalPixels, finalPixels, index) > DiffTolerance)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static double GetColorDifference(byte[] first, byte[] second, int index)
    {
        return (Math.Abs(first[index] - second[index]) +
                Math.Abs(first[index + 1] - second[index + 1]) +
                Math.Abs(first[index + 2] - second[index + 2])) / 3d;
    }

    private static byte BlendChannel(byte source, byte target, double amount)
    {
        return (byte)Math.Clamp((int)Math.Round(source + (target - source) * Math.Clamp(amount, 0, 1)), 0, 255);
    }

    private static byte[] CopyPixels(BitmapSource source)
    {
        BitmapSource bitmap = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        bitmap.Freeze();
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }

    private static BitmapSource CreateBitmap(int width, int height, byte[] pixels)
    {
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }
}

public interface IHardProtectFinalRestoreFilter
{
    HardProtectFinalRestoreResult Apply(HardProtectFinalRestoreInput input);
}

public sealed record HardProtectFinalRestoreInput(
    BitmapSource OriginalImage,
    BitmapSource CurrentRetouchedImage,
    FaceSnapshotMaskSet Snapshot,
    MaskPlane HardProtectMask,
    MaskPlane SoftProtectMask,
    MaskPlane RetouchAllowMask,
    int AppliedStage,
    MaskQualityReport MaskQualityReport);

public sealed record HardProtectFinalRestoreResult(
    BitmapSource FinalProtectedImage,
    MaskPlane BeforeRestoreDiffMask,
    MaskPlane AfterRestoreDiffMask,
    HardProtectRestoreReport Report,
    IReadOnlyList<string> DebugWarnings);

public sealed record HardProtectRestoreReport(
    int AppliedStage,
    int HardProtectPixelCount,
    int RestoredPixelCount,
    int ChangedPixelBeforeRestoreCount,
    int ChangedPixelAfterRestoreCount,
    bool EyeChanged,
    bool EyebrowChanged,
    bool LipChanged,
    bool InnerMouthChanged,
    bool NostrilChanged,
    bool HairChanged,
    bool BeardChanged,
    bool GlassesChanged,
    bool IsHardProtectClean,
    IReadOnlyList<string> DebugWarnings);
