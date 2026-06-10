namespace PhotoRetouch;

public sealed record FeatureMeshPoint(
    int Index,
    double X,
    double Y,
    double Weight = 1,
    string Role = "");
