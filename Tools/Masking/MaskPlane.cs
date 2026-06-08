namespace PhotoRetouch;

public sealed class MaskPlane
{
    public MaskPlane(int width, int height)
        : this(width, height, new double[checked(width * height)])
    {
    }

    public MaskPlane(int width, int height, double[] values)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width));
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (values.Length != width * height)
        {
            throw new ArgumentException("Mask value count must match width * height.", nameof(values));
        }

        Width = width;
        Height = height;
        Values = values;
    }

    public int Width { get; }

    public int Height { get; }

    public double[] Values { get; }

    public double this[int x, int y]
    {
        get => Values[y * Width + x];
        set => Values[y * Width + x] = Clamp01(value);
    }

    public MaskPlane Clone()
    {
        return new MaskPlane(Width, Height, (double[])Values.Clone());
    }

    public static MaskPlane Empty(int width, int height)
    {
        return new MaskPlane(width, height);
    }

    public static MaskPlane Union(params MaskPlane[] masks)
    {
        if (masks.Length == 0)
        {
            throw new ArgumentException("At least one mask is required.", nameof(masks));
        }

        MaskPlane result = Empty(masks[0].Width, masks[0].Height);
        foreach (MaskPlane mask in masks)
        {
            EnsureSameSize(result, mask);
            for (int index = 0; index < result.Values.Length; index++)
            {
                result.Values[index] = Math.Max(result.Values[index], mask.Values[index]);
            }
        }

        return result;
    }

    public static MaskPlane Subtract(MaskPlane source, MaskPlane subtract)
    {
        EnsureSameSize(source, subtract);
        MaskPlane result = Empty(source.Width, source.Height);
        for (int index = 0; index < result.Values.Length; index++)
        {
            result.Values[index] = Clamp01(source.Values[index] * (1 - subtract.Values[index]));
        }

        return result;
    }

    public static MaskPlane Intersect(params MaskPlane[] masks)
    {
        if (masks.Length == 0)
        {
            throw new ArgumentException("At least one mask is required.", nameof(masks));
        }

        MaskPlane result = masks[0].Clone();
        foreach (MaskPlane mask in masks.Skip(1))
        {
            EnsureSameSize(result, mask);
            for (int index = 0; index < result.Values.Length; index++)
            {
                result.Values[index] = Math.Min(result.Values[index], mask.Values[index]);
            }
        }

        return result;
    }

    public static MaskPlane Multiply(MaskPlane source, double amount)
    {
        MaskPlane result = Empty(source.Width, source.Height);
        for (int index = 0; index < result.Values.Length; index++)
        {
            result.Values[index] = Clamp01(source.Values[index] * amount);
        }

        return result;
    }

    public double Average()
    {
        double sum = 0;
        foreach (double value in Values)
        {
            sum += value;
        }

        return sum / Values.Length;
    }

    public static void EnsureSameSize(MaskPlane left, MaskPlane right)
    {
        if (left.Width != right.Width || left.Height != right.Height)
        {
            throw new InvalidOperationException("Mask sizes must match.");
        }
    }

    public static double Clamp01(double value)
    {
        return Math.Clamp(value, 0, 1);
    }
}
