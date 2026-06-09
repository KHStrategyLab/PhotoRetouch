using System.Drawing;

namespace PhotoRetouch.AnchorMesh;

public readonly record struct AnchorMeshFitTransform2D(
    float ScaleX,
    float ScaleY,
    float RotationRad,
    float TranslateX,
    float TranslateY,
    float TemplateCenterX,
    float TemplateCenterY)
{
    public PointF Apply(float x, float y)
    {
        float centeredX = (x - TemplateCenterX) * ScaleX;
        float centeredY = (y - TemplateCenterY) * ScaleY;
        float cos = MathF.Cos(RotationRad);
        float sin = MathF.Sin(RotationRad);
        return new PointF(
            centeredX * cos - centeredY * sin + TranslateX,
            centeredX * sin + centeredY * cos + TranslateY);
    }
}
