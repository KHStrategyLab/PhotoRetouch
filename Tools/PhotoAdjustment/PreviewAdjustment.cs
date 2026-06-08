namespace PhotoRetouch;

public sealed record PreviewAdjustment(
    double Exposure,
    double Contrast,
    double Saturation,
    double WhiteBalance,
    double BlurSharpen,
    double BlemishRemove,
    double SkinSmooth,
    double PoreClean,
    double ToneEven,
    double OvalFace,
    double FaceBalance,
    double CheekboneSoften,
    double JawlineDefine,
    double ChinLength,
    double ChinWidth,
    FaceWorkArea FaceWorkArea,
    double CurveAmount,
    CurveChannel CurveChannel,
    byte[] CurveLookup)
{
    public static PreviewAdjustment None { get; } = new(
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        FaceWorkArea.Default,
        0,
        CurveChannel.All,
        CurveLookupTables.CreateIdentity());
}
