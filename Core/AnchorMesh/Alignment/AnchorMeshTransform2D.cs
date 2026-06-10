using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public readonly record struct AnchorMeshTransform2D(
    float Scale,
    float RotationRad,
    float TranslateX,
    float TranslateY)
{
    public PointF Apply(float x, float y)
    {
        float cos = MathF.Cos(RotationRad);
        float sin = MathF.Sin(RotationRad);
        return new PointF(
            (x * cos - y * sin) * Scale + TranslateX,
            (x * sin + y * cos) * Scale + TranslateY);
    }
}
