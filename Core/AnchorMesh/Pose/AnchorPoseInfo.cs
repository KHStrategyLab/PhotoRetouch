namespace PhotoRetouch.AnchorMesh;

public readonly record struct AnchorPoseInfo(
    float RollRad,
    float YawRad,
    float PitchRad,
    float Scale,
    float CenterX,
    float CenterY,
    float CameraDistance,
    float PerspectiveStrength,
    float Confidence);
