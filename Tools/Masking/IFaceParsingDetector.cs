using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public interface IFaceParsingDetector
{
    string DetectorVersion { get; }

    ParsingMaskSet? Detect(BitmapSource source, FaceParsingInput input);
}
