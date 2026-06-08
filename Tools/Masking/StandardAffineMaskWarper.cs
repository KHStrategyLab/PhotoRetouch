namespace PhotoRetouch;

public sealed class StandardAffineMaskWarper : IStandardMaskWarper
{
    public WarpedMaskSet Warp(StandardMaskSet standardMasks, MaskWarpInput input)
    {
        ArgumentNullException.ThrowIfNull(standardMasks);
        ArgumentNullException.ThrowIfNull(input);

        return new WarpedMaskSet(
            WarpMask(standardMasks.SkinMask, input),
            WarpMask(standardMasks.EyeProtectMask, input),
            WarpMask(standardMasks.EyebrowProtectMask, input),
            WarpMask(standardMasks.LipProtectMask, input),
            WarpMask(standardMasks.NoseMask, input),
            WarpMask(standardMasks.NostrilProtectMask, input),
            WarpMask(standardMasks.SoftProtectMask, input));
    }

    private static MaskPlane WarpMask(MaskPlane source, MaskWarpInput input)
    {
        MaskPlane target = MaskPlane.Empty(input.TargetImageWidth, input.TargetImageHeight);
        double centerX = input.FaceBox.X + input.FaceBox.Width / 2d;
        double centerY = input.FaceBox.Y + input.FaceBox.Height / 2d;
        double angle = input.FaceAngle * Math.PI / 180d;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double scaleX = Math.Max(1, input.FaceBox.Width);
        double scaleY = Math.Max(1, input.FaceBox.Height);

        for (int y = 0; y < input.TargetImageHeight; y++)
        {
            for (int x = 0; x < input.TargetImageWidth; x++)
            {
                double dx = x + 0.5 - centerX;
                double dy = y + 0.5 - centerY;
                double unrotatedX = dx * cos + dy * sin;
                double unrotatedY = -dx * sin + dy * cos;
                double sourceX = unrotatedX / scaleX + 0.5;
                double sourceY = unrotatedY / scaleY + 0.5;

                if (sourceX < 0 || sourceX > 1 || sourceY < 0 || sourceY > 1)
                {
                    continue;
                }

                target[x, y] = SampleBilinear(source, sourceX * (source.Width - 1), sourceY * (source.Height - 1));
            }
        }

        return target;
    }

    private static double SampleBilinear(MaskPlane source, double x, double y)
    {
        int x0 = Math.Clamp((int)Math.Floor(x), 0, source.Width - 1);
        int y0 = Math.Clamp((int)Math.Floor(y), 0, source.Height - 1);
        int x1 = Math.Clamp(x0 + 1, 0, source.Width - 1);
        int y1 = Math.Clamp(y0 + 1, 0, source.Height - 1);
        double tx = x - x0;
        double ty = y - y0;

        double top = source[x0, y0] + (source[x1, y0] - source[x0, y0]) * tx;
        double bottom = source[x0, y1] + (source[x1, y1] - source[x0, y1]) * tx;
        return top + (bottom - top) * ty;
    }
}
