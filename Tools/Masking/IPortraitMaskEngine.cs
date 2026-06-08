using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public interface IPortraitMaskEngine
{
    string MaskVersion { get; }

    PortraitMaskResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea);
}

public sealed record PortraitMaskResult(
    FaceAnalysisResult Analysis,
    FaceMaskSet Masks,
    MaskQualityReport QualityReport,
    ParsingMaskSet? ParsingMasks = null,
    FaceMaskSet? WarpedStandardMasks = null,
    NostrilDetectorResult? NostrilDetection = null);
