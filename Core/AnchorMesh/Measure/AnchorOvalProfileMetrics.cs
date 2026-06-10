namespace PhotoRetouch.AnchorMesh;

public sealed class AnchorOvalProfileMetrics
{
    public float FaceHeight { get; set; }

    public float WTemple { get; set; }

    public float WCheek { get; set; }

    public float WMax { get; set; }

    public float WJaw { get; set; }

    public float WChin { get; set; }

    public float YMax { get; set; }

    public float FaceAspectRatio { get; set; }

    public float TempleRatio { get; set; }

    public float JawRatio { get; set; }

    public float ChinRatio { get; set; }

    public float SymmetryError { get; set; }

    public float SmoothnessError { get; set; }

    public float JawAngleLeft { get; set; }

    public float JawAngleRight { get; set; }

    public float AspectScore { get; set; }

    public float MaxWidthPositionScore { get; set; }

    public float TempleRatioScore { get; set; }

    public float JawRatioScore { get; set; }

    public float ChinRatioScore { get; set; }

    public float SymmetryScore { get; set; }

    public float SmoothnessScore { get; set; }

    public float JawAngleScore { get; set; }

    public float OvalScore { get; set; }

    public string Classification { get; set; } = "Unknown";

    public List<string> Warnings { get; } = new();
}
