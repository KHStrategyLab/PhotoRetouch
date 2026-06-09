using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed class NoFaceParsingDetector : IFaceParsingDetector
{
    public string DetectorVersion => "no_face_parsing_v1";

    public ParsingMaskSet? Detect(BitmapSource source, FaceParsingInput input)
    {
        return null;
    }
}
