using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public interface IPreviewEngine
{
    BitmapSource Render(BitmapSource source, PreviewAdjustment adjustment);

    bool HasEffectiveAdjustment(PreviewAdjustment adjustment);
}
