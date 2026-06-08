using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public interface IFaceAnalyzer
{
    string AnalyzerVersion { get; }

    FaceAnalyzerResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea);
}
