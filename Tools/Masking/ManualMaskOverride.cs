using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record ManualMaskOverride(
    string ImageId,
    string SnapshotMaskCacheKey,
    int ManualOverrideVersion,
    MaskPlane ManualHardProtectAddMask,
    MaskPlane ManualHardProtectRemoveMask,
    MaskPlane ManualSoftProtectAddMask,
    MaskPlane ManualSoftProtectRemoveMask,
    MaskPlane ManualRetouchAllowAddMask,
    MaskPlane ManualRetouchAllowRemoveMask,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc)
{
    public static ManualMaskOverride Empty(FaceSnapshotMaskSet snapshot)
    {
        int width = snapshot.Masks.HardProtectMask.Width;
        int height = snapshot.Masks.HardProtectMask.Height;
        DateTime now = DateTime.UtcNow;
        return new ManualMaskOverride(
            snapshot.ImageId,
            snapshot.CacheKey.StableId,
            1,
            MaskPlane.Empty(width, height),
            MaskPlane.Empty(width, height),
            MaskPlane.Empty(width, height),
            MaskPlane.Empty(width, height),
            MaskPlane.Empty(width, height),
            MaskPlane.Empty(width, height),
            now,
            now);
    }

    public ManualMaskOverride Touch()
    {
        return this with
        {
            ManualOverrideVersion = ManualOverrideVersion + 1,
            UpdatedAtUtc = DateTime.UtcNow
        };
    }
}

public enum ManualMaskBrushMode
{
    Protect,
    Retouch,
    SoftProtect,
    Erase
}

public sealed record ManualMaskBrushOptions(
    ManualMaskBrushMode Mode,
    double BrushSize,
    double BrushOpacity,
    double BrushFeather,
    bool ShowBrushCursor = true,
    bool ShowMaskOverlay = true)
{
    public static ManualMaskBrushOptions Default { get; } = new(
        ManualMaskBrushMode.Protect,
        48,
        1.0,
        0.35);
}

public static class ManualMaskBrushEngine
{
    public static ManualMaskOverride ApplyStroke(
        ManualMaskOverride current,
        IEnumerable<WpfPoint> imagePoints,
        ManualMaskBrushOptions options)
    {
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(imagePoints);
        ArgumentNullException.ThrowIfNull(options);

        ManualMaskOverride next = Clone(current);
        foreach (WpfPoint point in imagePoints)
        {
            PaintPoint(next, point, options);
        }

        return next.Touch();
    }

    private static ManualMaskOverride Clone(ManualMaskOverride source)
    {
        return source with
        {
            ManualHardProtectAddMask = source.ManualHardProtectAddMask.Clone(),
            ManualHardProtectRemoveMask = source.ManualHardProtectRemoveMask.Clone(),
            ManualSoftProtectAddMask = source.ManualSoftProtectAddMask.Clone(),
            ManualSoftProtectRemoveMask = source.ManualSoftProtectRemoveMask.Clone(),
            ManualRetouchAllowAddMask = source.ManualRetouchAllowAddMask.Clone(),
            ManualRetouchAllowRemoveMask = source.ManualRetouchAllowRemoveMask.Clone()
        };
    }

    private static void PaintPoint(ManualMaskOverride target, WpfPoint point, ManualMaskBrushOptions options)
    {
        int width = target.ManualHardProtectAddMask.Width;
        int height = target.ManualHardProtectAddMask.Height;
        double radius = Math.Clamp(options.BrushSize, 1, Math.Max(width, height)) / 2d;
        double feather = Math.Clamp(options.BrushFeather, 0, 1);
        double hardRadius = radius * (1 - feather);
        int left = Math.Clamp((int)Math.Floor(point.X - radius), 0, width - 1);
        int right = Math.Clamp((int)Math.Ceiling(point.X + radius), 0, width - 1);
        int top = Math.Clamp((int)Math.Floor(point.Y - radius), 0, height - 1);
        int bottom = Math.Clamp((int)Math.Ceiling(point.Y + radius), 0, height - 1);

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double distance = Math.Sqrt(Math.Pow(x - point.X, 2) + Math.Pow(y - point.Y, 2));
                if (distance > radius)
                {
                    continue;
                }

                double falloff = distance <= hardRadius || radius <= hardRadius
                    ? 1
                    : 1 - (distance - hardRadius) / Math.Max(radius - hardRadius, 0.001);
                double amount = Math.Clamp(options.BrushOpacity * falloff, 0, 1);
                PaintMaskValue(target, x, y, options.Mode, amount);
            }
        }
    }

    private static void PaintMaskValue(ManualMaskOverride target, int x, int y, ManualMaskBrushMode mode, double amount)
    {
        switch (mode)
        {
            case ManualMaskBrushMode.Protect:
                Add(target.ManualHardProtectAddMask, x, y, amount);
                Add(target.ManualRetouchAllowRemoveMask, x, y, amount);
                break;
            case ManualMaskBrushMode.Retouch:
                Add(target.ManualRetouchAllowAddMask, x, y, amount);
                Add(target.ManualHardProtectRemoveMask, x, y, amount);
                break;
            case ManualMaskBrushMode.SoftProtect:
                Add(target.ManualSoftProtectAddMask, x, y, amount);
                Add(target.ManualRetouchAllowRemoveMask, x, y, amount * 0.5);
                break;
            case ManualMaskBrushMode.Erase:
                Remove(target.ManualHardProtectAddMask, x, y, amount);
                Remove(target.ManualHardProtectRemoveMask, x, y, amount);
                Remove(target.ManualSoftProtectAddMask, x, y, amount);
                Remove(target.ManualSoftProtectRemoveMask, x, y, amount);
                Remove(target.ManualRetouchAllowAddMask, x, y, amount);
                Remove(target.ManualRetouchAllowRemoveMask, x, y, amount);
                break;
        }
    }

    private static void Add(MaskPlane mask, int x, int y, double amount)
    {
        mask[x, y] = Math.Max(mask[x, y], amount);
    }

    private static void Remove(MaskPlane mask, int x, int y, double amount)
    {
        mask[x, y] = Math.Max(0, mask[x, y] - amount);
    }
}

public static class ManualMaskOverrideApplier
{
    public static FaceSnapshotMaskSet Apply(FaceSnapshotMaskSet snapshot, ManualMaskOverride? manualOverride)
    {
        if (manualOverride is null)
        {
            return snapshot;
        }

        FaceMaskSet auto = snapshot.Masks;
        FaceMaskSet finalMasks = Apply(auto, manualOverride);
        return snapshot with
        {
            Masks = finalMasks
        };
    }

    public static FaceMaskSet Apply(FaceMaskSet auto, ManualMaskOverride manualOverride)
    {
        MaskPlane hardProtect = MaskPlane.Subtract(
            MaskPlane.Union(auto.HardProtectMask, manualOverride.ManualHardProtectAddMask),
            manualOverride.ManualHardProtectRemoveMask);
        MaskPlane softProtect = MaskPlane.Subtract(
            MaskPlane.Subtract(MaskPlane.Union(auto.SoftProtectMask, manualOverride.ManualSoftProtectAddMask), manualOverride.ManualSoftProtectRemoveMask),
            hardProtect);
        MaskPlane retouchAllow = MaskPlane.Subtract(
            MaskPlane.Subtract(MaskPlane.Union(auto.RetouchAllowMask, manualOverride.ManualRetouchAllowAddMask), manualOverride.ManualRetouchAllowRemoveMask),
            hardProtect);
        MaskPlane finalOverlay = MaskPlane.Union(retouchAllow, softProtect, hardProtect);

        return auto with
        {
            HardProtectMask = hardProtect,
            SoftProtectMask = softProtect,
            RetouchAllowMask = retouchAllow,
            FinalOverlayMask = finalOverlay
        };
    }
}
