namespace PhotoRetouch;

public sealed record SkinToneMaskSet(
    MaskPlane SkinToneApplyMask,
    MaskPlane FaceOnlyWarpMask,
    MaskPlane HairExcludedMask,
    MaskPlane GlassesExcludedMask,
    MaskPlane NostrilExcludedMask,
    MaskPlane LipExcludedMask,
    MaskPlane BeardHairMask,
    MaskPlane BeardShadowMask,
    MaskPlane NoseStructureProtectMask,
    MaskPlane NoseShadowMask,
    MaskPlane NoseRetouchStrengthMap,
    MaskPlane HardExcludedMask);

public static class SkinToneMaskBuilder
{
    public static SkinToneMaskSet Build(FaceMaskSet masks)
    {
        ArgumentNullException.ThrowIfNull(masks);

        MaskPlane.EnsureSameSize(masks.SkinMask, masks.HardProtectMask);
        MaskPlane beardHair = NormalizeHardMask(MaskPlane.Union(masks.BeardMask, masks.MustacheMask), 0.28);
        MaskPlane facialHardProtect = MaskPlane.Union(
            masks.EyeMask,
            masks.EyebrowMask,
            masks.LipMask,
            masks.InnerMouthMask,
            masks.TeethMask,
            masks.NostrilMask,
            masks.GlassesMask,
            beardHair);
        MaskPlane hardExcluded = NormalizeHardMask(MaskPlane.Union(masks.HardProtectMask, facialHardProtect, masks.HairMask), 0.24);

        MaskPlane skinRegion = MaskPlane.Union(
            masks.SkinMask,
            masks.NoseSkinMask,
            MaskPlane.Subtract(masks.NoseMask, masks.NostrilMask),
            MaskPlane.Multiply(masks.RetouchAllowMask, 0.72));
        NoseStructureMaskSet noseStructure = BuildNoseStructureMasks(masks);
        MaskPlane skinToneApply = NormalizeSoftMask(MaskPlane.Subtract(skinRegion, hardExcluded), 0.02, 0.82);
        skinToneApply = MaskPlane.Subtract(skinToneApply, MaskPlane.Multiply(noseStructure.StructureProtectMask, 0.58));

        MaskPlane beardShadow = BuildBeardShadowMask(masks, beardHair, skinToneApply);
        MaskPlane faceOnlyWarp = BuildFaceOnlyWarpMask(masks, facialHardProtect);

        return new SkinToneMaskSet(
            skinToneApply,
            faceOnlyWarp,
            masks.HairMask.Clone(),
            masks.GlassesMask.Clone(),
            masks.NostrilMask.Clone(),
            MaskPlane.Union(masks.LipMask, masks.InnerMouthMask, masks.TeethMask),
            beardHair,
            beardShadow,
            noseStructure.StructureProtectMask,
            noseStructure.ShadowMask,
            noseStructure.RetouchStrengthMap,
            hardExcluded);
    }

    public static FaceMaskSet ApplyToFaceMaskSet(FaceMaskSet masks)
    {
        SkinToneMaskSet toneMasks = Build(masks);
        MaskPlane hardProtect = NormalizeHardMask(MaskPlane.Union(
            masks.HardProtectMask,
            toneMasks.HairExcludedMask,
            toneMasks.GlassesExcludedMask,
            toneMasks.NostrilExcludedMask,
            toneMasks.LipExcludedMask,
            toneMasks.BeardHairMask),
            0.24);
        MaskPlane softProtect = MaskPlane.Subtract(
            MaskPlane.Union(
                masks.SoftProtectMask,
                MaskPlane.Multiply(toneMasks.BeardShadowMask, 0.65),
                MaskPlane.Multiply(toneMasks.NoseStructureProtectMask, 0.88)),
            hardProtect);
        MaskPlane retouchAllow = MaskPlane.Subtract(
            MaskPlane.Intersect(toneMasks.SkinToneApplyMask, toneMasks.NoseRetouchStrengthMap),
            hardProtect);
        MaskPlane finalOverlay = MaskPlane.Subtract(
            MaskPlane.Union(retouchAllow, MaskPlane.Multiply(softProtect, 0.45)),
            hardProtect);

        return masks with
        {
            HardProtectMask = hardProtect,
            SoftProtectMask = softProtect,
            RetouchAllowMask = retouchAllow,
            FinalOverlayMask = finalOverlay
        };
    }

    private static MaskPlane BuildFaceOnlyWarpMask(FaceMaskSet masks, MaskPlane facialHardProtect)
    {
        MaskPlane faceCandidate = MaskPlane.Union(
            masks.SkinMask,
            masks.NoseMask,
            masks.NoseSkinMask,
            masks.SoftProtectMask,
            masks.RetouchAllowMask,
            facialHardProtect);
        return NormalizeSoftMask(MaskPlane.Subtract(faceCandidate, masks.HairMask), 0.015, 0.78);
    }

    private static MaskPlane BuildBeardShadowMask(FaceMaskSet masks, MaskPlane beardHair, MaskPlane skinToneApply)
    {
        MaskPlane lowerFaceCandidate = MaskPlane.Union(
            masks.NoseShadowMask,
            MaskPlane.Multiply(masks.SoftProtectMask, 0.55),
            MaskPlane.Multiply(masks.NoseSkinMask, 0.20));
        MaskPlane notHair = MaskPlane.Subtract(lowerFaceCandidate, MaskPlane.Union(beardHair, masks.NostrilMask, masks.LipMask, masks.InnerMouthMask));
        return NormalizeSoftMask(MaskPlane.Intersect(notHair, MaskPlane.Union(skinToneApply, MaskPlane.Multiply(masks.SkinMask, 0.35))), 0.04, 0.70);
    }

    private static NoseStructureMaskSet BuildNoseStructureMasks(FaceMaskSet masks)
    {
        MaskPlane noseMask = MaskPlane.Subtract(masks.NoseMask, masks.NostrilMask);
        Int32RectBox bounds = FindMaskBounds(noseMask, 0.04);
        MaskPlane ridge = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        MaskPlane tip = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        MaskPlane wing = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        MaskPlane sideShadow = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        MaskPlane underShadow = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        MaskPlane strength = MaskPlane.Empty(noseMask.Width, noseMask.Height);
        if (!bounds.IsValid)
        {
            for (int index = 0; index < strength.Values.Length; index++)
            {
                strength.Values[index] = 1;
            }

            return new NoseStructureMaskSet(ridge, tip, wing, MaskPlane.Union(sideShadow, underShadow, masks.NoseShadowMask), strength);
        }

        double centerX = bounds.Left + bounds.Width / 2d;
        double centerY = bounds.Top + bounds.Height / 2d;
        double ridgeWidth = Math.Max(2, bounds.Width * 0.13);
        for (int y = 0; y < noseMask.Height; y++)
        {
            for (int x = 0; x < noseMask.Width; x++)
            {
                int index = y * noseMask.Width + x;
                double nose = noseMask.Values[index];
                if (nose <= 0.001)
                {
                    strength.Values[index] = 1;
                    continue;
                }

                double nx = (x - centerX) / Math.Max(1, bounds.Width / 2d);
                double ny = (y - bounds.Top) / Math.Max(1, bounds.Height);
                double centerDistance = Math.Abs(x - centerX);
                double ridgeWeight = Math.Clamp(1 - centerDistance / ridgeWidth, 0, 1) * Math.Clamp(1 - Math.Abs(ny - 0.44) / 0.46, 0, 1) * nose;
                double tipWeight = EllipseWeight(x, y, centerX, bounds.Top + bounds.Height * 0.72, bounds.Width * 0.24, bounds.Height * 0.18) * nose;
                double wingWeight =
                    Math.Max(
                        EllipseWeight(x, y, bounds.Left + bounds.Width * 0.27, bounds.Top + bounds.Height * 0.70, bounds.Width * 0.20, bounds.Height * 0.18),
                        EllipseWeight(x, y, bounds.Left + bounds.Width * 0.73, bounds.Top + bounds.Height * 0.70, bounds.Width * 0.20, bounds.Height * 0.18)) * nose;
                double sideWeight = Math.Clamp((Math.Abs(nx) - 0.38) / 0.36, 0, 1) * Math.Clamp(1 - Math.Abs(ny - 0.50) / 0.42, 0, 1) * nose;
                double underWeight = Math.Clamp((ny - 0.74) / 0.22, 0, 1) * nose;

                ridge.Values[index] = ridgeWeight;
                tip.Values[index] = tipWeight;
                wing.Values[index] = wingWeight;
                sideShadow.Values[index] = sideWeight;
                underShadow.Values[index] = Math.Max(underWeight, masks.NoseShadowMask.Values[index]);
                double structural = Math.Max(ridgeWeight, Math.Max(tipWeight * 0.72, Math.Max(wingWeight * 0.72, Math.Max(sideWeight, underShadow.Values[index]))));
                strength.Values[index] = Math.Clamp(1 - structural * 0.64, 0.22, 1);
            }
        }

        MaskPlane shadow = NormalizeSoftMask(MaskPlane.Union(sideShadow, underShadow, masks.NoseShadowMask), 0.02, 0.72);
        MaskPlane structure = NormalizeSoftMask(MaskPlane.Union(ridge, tip, wing, shadow), 0.02, 0.72);
        MaskPlane retouchStrength = MaskPlane.Subtract(strength, masks.NostrilMask);
        return new NoseStructureMaskSet(structure, tip, wing, shadow, retouchStrength);
    }

    private static double EllipseWeight(double x, double y, double centerX, double centerY, double radiusX, double radiusY)
    {
        double nx = (x - centerX) / Math.Max(1, radiusX);
        double ny = (y - centerY) / Math.Max(1, radiusY);
        double distance = Math.Sqrt(nx * nx + ny * ny);
        if (distance >= 1)
        {
            return 0;
        }

        double smooth = 1 - distance;
        return Math.Clamp(smooth * smooth * (3 - 2 * smooth), 0, 1);
    }

    private static Int32RectBox FindMaskBounds(MaskPlane mask, double threshold)
    {
        int left = mask.Width;
        int top = mask.Height;
        int right = -1;
        int bottom = -1;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                if (mask[x, y] < threshold)
                {
                    continue;
                }

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        return right < left || bottom < top
            ? Int32RectBox.Empty
            : new Int32RectBox(left, top, right - left + 1, bottom - top + 1);
    }

    private static MaskPlane NormalizeHardMask(MaskPlane source, double threshold)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int index = 0; index < source.Values.Length; index++)
        {
            double value = source.Values[index];
            result.Values[index] = value >= threshold
                ? Math.Clamp(0.82 + value * 0.18, 0, 1)
                : Math.Clamp(value * 0.35, 0, 1);
        }

        return result;
    }

    private static MaskPlane NormalizeSoftMask(MaskPlane source, double floor, double scale)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int index = 0; index < source.Values.Length; index++)
        {
            double value = source.Values[index];
            result.Values[index] = value <= floor
                ? 0
                : Math.Clamp((value - floor) / Math.Max(0.001, scale), 0, 1);
        }

        return result;
    }
}

internal sealed record NoseStructureMaskSet(
    MaskPlane StructureProtectMask,
    MaskPlane TipMask,
    MaskPlane WingMask,
    MaskPlane ShadowMask,
    MaskPlane RetouchStrengthMap);

internal sealed record Int32RectBox(int Left, int Top, int Width, int Height)
{
    public static Int32RectBox Empty { get; } = new(0, 0, 0, 0);

    public bool IsValid => Width > 0 && Height > 0;
}
