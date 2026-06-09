namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorMeshSession
{
    public AnchorMeshSession(int imageWidth, int imageHeight)
    {
        ImageWidth = imageWidth;
        ImageHeight = imageHeight;
        CreatedAt = DateTimeOffset.Now;
    }

    public int ImageWidth { get; }

    public int ImageHeight { get; }

    public DateTimeOffset CreatedAt { get; }
}
