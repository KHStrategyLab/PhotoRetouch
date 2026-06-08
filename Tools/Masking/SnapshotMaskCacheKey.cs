using System.Windows;

namespace PhotoRetouch;

public sealed record SnapshotMaskCacheKey(
    string ImageId,
    int ImageWidth,
    int ImageHeight,
    Int32Rect FaceBox,
    double FaceAngle,
    string CropVersion,
    string MaskVersion)
{
    public string StableId => string.Join(
        "|",
        ImageId,
        ImageWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ImageHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FaceBox.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FaceBox.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FaceBox.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FaceBox.Height.ToString(System.Globalization.CultureInfo.InvariantCulture),
        FaceAngle.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
        CropVersion,
        MaskVersion);
}
