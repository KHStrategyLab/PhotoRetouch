namespace PhotoRetouch;

public sealed record FaceWorkArea(double CenterX, double CenterY, double Width, double Height)
{
    public static FaceWorkArea Default { get; } = new(0.5, 0.48, 0.34, 0.54);

    public FaceWorkArea Clamp()
    {
        return new FaceWorkArea(
            Math.Clamp(CenterX, 0, 1),
            Math.Clamp(CenterY, 0, 1),
            Math.Clamp(Width, 0.05, 1),
            Math.Clamp(Height, 0.05, 1));
    }
}
