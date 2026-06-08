using System.Windows;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record NostrilDetectorInput(
    BitmapSource OriginalImage,
    Int32Rect FaceBox,
    IReadOnlyDictionary<string, WpfPoint> FaceLandmarks,
    WpfPoint NoseTip,
    WpfPoint MouthCenter,
    WpfPoint LeftEyeCenter,
    WpfPoint RightEyeCenter,
    MaskPlane WarpedStandardNostrilMask,
    MaskPlane? LipMask = null,
    MaskPlane? BeardMask = null);

public sealed record NostrilCandidateComponent(
    int Id,
    Int32Rect BoundingBox,
    int Area,
    double AspectRatio,
    WpfPoint Center,
    double MeanBrightness,
    double Score,
    bool IsLeftSide,
    bool IsRightSide,
    bool IsSelected);

public sealed record NostrilDetectorResult(
    MaskPlane NostrilMask,
    Int32Rect NoseLowerRoi,
    MaskPlane DarkCandidateMask,
    MaskPlane ComponentMask,
    double NostrilConfidence,
    IReadOnlyList<string> DebugWarnings,
    IReadOnlyList<NostrilCandidateComponent> CandidateComponents);
