using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record FaceParsingInput(
    Int32Rect FaceBox,
    IReadOnlyDictionary<string, WpfPoint> FaceLandmarks,
    double FaceAngle);
