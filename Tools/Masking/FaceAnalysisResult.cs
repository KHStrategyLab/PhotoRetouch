using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record FaceAnalysisResult(
    Int32Rect FaceBox,
    IReadOnlyDictionary<string, WpfPoint> FaceLandmarks,
    int[]? FaceParsingLabels,
    double FaceAngle,
    double FaceQualityScore,
    double LandmarkConfidence,
    double ParsingConfidence,
    IReadOnlyList<string> DebugWarnings)
{
    public static FaceAnalysisResult Heuristic(Int32Rect faceBox, IReadOnlyDictionary<string, WpfPoint> landmarks, IReadOnlyList<string> warnings)
    {
        return new FaceAnalysisResult(
            faceBox,
            landmarks,
            null,
            0,
            0.35,
            0.25,
            0,
            warnings);
    }
}
