namespace PhotoRetouch;

public readonly record struct TextureFlowAxis(
    bool IsValid,
    double CenterX,
    double CenterY,
    double AxisX,
    double AxisY,
    double UpX,
    double UpY,
    double AngleRad);

public static class TextureFlowAnalyzer
{
    public static TextureFlowAxis EstimateWeightedAxis(MaskPlane evidence, double threshold = 0.10)
    {
        double total = 0;
        double meanX = 0;
        double meanY = 0;
        for (int y = 0; y < evidence.Height; y++)
        {
            for (int x = 0; x < evidence.Width; x++)
            {
                double weight = evidence[x, y];
                if (weight <= threshold)
                {
                    continue;
                }

                double px = x + 0.5;
                double py = y + 0.5;
                meanX += px * weight;
                meanY += py * weight;
                total += weight;
            }
        }

        if (total <= 0)
        {
            return default;
        }

        meanX /= total;
        meanY /= total;
        double covXX = 0;
        double covYY = 0;
        double covXY = 0;
        for (int y = 0; y < evidence.Height; y++)
        {
            for (int x = 0; x < evidence.Width; x++)
            {
                double weight = evidence[x, y];
                if (weight <= threshold)
                {
                    continue;
                }

                double dx = x + 0.5 - meanX;
                double dy = y + 0.5 - meanY;
                covXX += dx * dx * weight;
                covYY += dy * dy * weight;
                covXY += dx * dy * weight;
            }
        }

        double angle = 0.5 * Math.Atan2(2 * covXY, covXX - covYY);
        double axisX = Math.Cos(angle);
        double axisY = Math.Sin(angle);
        if (axisX < 0)
        {
            axisX = -axisX;
            axisY = -axisY;
            angle += Math.PI;
        }

        return new TextureFlowAxis(
            true,
            meanX,
            meanY,
            axisX,
            axisY,
            -axisY,
            axisX,
            Math.Atan2(axisY, axisX));
    }

    public static double GetDirectionalContrastScore(Func<int, int, double> sampleLuma, int width, int height, int x, int y, double axisX, double axisY, int radius = 2)
    {
        int alongX = Math.Clamp((int)Math.Round(x + axisX * radius), 0, width - 1);
        int alongY = Math.Clamp((int)Math.Round(y + axisY * radius), 0, height - 1);
        int crossX = Math.Clamp((int)Math.Round(x - axisY * radius), 0, width - 1);
        int crossY = Math.Clamp((int)Math.Round(y + axisX * radius), 0, height - 1);
        double center = sampleLuma(x, y);
        double alongDiff = Math.Abs(center - sampleLuma(alongX, alongY));
        double crossDiff = Math.Abs(center - sampleLuma(crossX, crossY));
        return Math.Clamp((crossDiff - alongDiff + 8) / 28, 0, 1);
    }

    public static double EstimateDirectionalEvidenceScore(MaskPlane evidence, TextureFlowAxis axis, double threshold = 0.10, int radius = 2)
    {
        if (!axis.IsValid)
        {
            return 0;
        }

        double sum = 0;
        double weight = 0;
        for (int y = 1; y < evidence.Height - 1; y++)
        {
            for (int x = 1; x < evidence.Width - 1; x++)
            {
                double center = evidence[x, y];
                if (center <= threshold)
                {
                    continue;
                }

                int alongX = Math.Clamp((int)Math.Round(x + axis.AxisX * radius), 0, evidence.Width - 1);
                int alongY = Math.Clamp((int)Math.Round(y + axis.AxisY * radius), 0, evidence.Height - 1);
                int crossX = Math.Clamp((int)Math.Round(x + axis.UpX * radius), 0, evidence.Width - 1);
                int crossY = Math.Clamp((int)Math.Round(y + axis.UpY * radius), 0, evidence.Height - 1);
                double along = Math.Abs(center - evidence[alongX, alongY]);
                double cross = Math.Abs(center - evidence[crossX, crossY]);
                sum += Math.Clamp(center + cross * 0.5 - along * 0.25, 0, 1) * center;
                weight += center;
            }
        }

        return weight <= 0 ? 0 : Math.Clamp(sum / weight, 0, 1);
    }

    public static double GetMultiScaleLineResponse(byte[] pixels, int width, int height, int stride, int x, int y, double lineAngle)
    {
        double response = 0;
        int[] radii = [1, 2, 4];
        foreach (int radius in radii)
        {
            response = Math.Max(response, GetLineResponse(pixels, width, height, stride, x, y, lineAngle, radius));
        }

        return response;
    }

    public static double GetLineResponse(byte[] pixels, int width, int height, int stride, int x, int y, double lineAngle, int radius)
    {
        double lineX = Math.Cos(lineAngle);
        double lineY = Math.Sin(lineAngle);
        double crossX = -lineY;
        double crossY = lineX;
        double center = GetLuminance(pixels, stride, x, y);
        double crossA = SampleLuma(pixels, width, height, stride, x + crossX * radius, y + crossY * radius);
        double crossB = SampleLuma(pixels, width, height, stride, x - crossX * radius, y - crossY * radius);
        double lineA = SampleLuma(pixels, width, height, stride, x + lineX * radius, y + lineY * radius);
        double lineB = SampleLuma(pixels, width, height, stride, x - lineX * radius, y - lineY * radius);
        double crossContrast = Math.Abs(center - (crossA + crossB) * 0.5) / 255.0;
        double alongChange = Math.Abs(lineA - lineB) / 255.0;
        return Math.Clamp(crossContrast * (1 - alongChange * 0.55), 0, 1);
    }

    public static double GetSharpness(byte[] pixels, int width, int height, int stride, int x, int y)
    {
        double gx = Math.Abs(GetLuminance(pixels, stride, Math.Min(width - 1, x + 1), y) - GetLuminance(pixels, stride, Math.Max(0, x - 1), y));
        double gy = Math.Abs(GetLuminance(pixels, stride, x, Math.Min(height - 1, y + 1)) - GetLuminance(pixels, stride, x, Math.Max(0, y - 1)));
        return Math.Clamp(Math.Sqrt(gx * gx + gy * gy) / 255.0, 0, 1);
    }

    public static double GetLocalMeanLuma(byte[] pixels, int width, int height, int stride, int x, int y, int radius)
    {
        double sum = 0;
        int count = 0;
        for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
        {
            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
            {
                sum += GetLuminance(pixels, stride, xx, yy);
                count++;
            }
        }

        return count == 0 ? GetLuminance(pixels, stride, x, y) : sum / count;
    }

    public static double GetLocalVarianceLuma(byte[] pixels, int width, int height, int stride, int x, int y, int radius)
    {
        double mean = GetLocalMeanLuma(pixels, width, height, stride, x, y, radius);
        double sum = 0;
        int count = 0;
        for (int yy = Math.Max(0, y - radius); yy <= Math.Min(height - 1, y + radius); yy++)
        {
            for (int xx = Math.Max(0, x - radius); xx <= Math.Min(width - 1, x + radius); xx++)
            {
                double delta = GetLuminance(pixels, stride, xx, yy) - mean;
                sum += delta * delta;
                count++;
            }
        }

        return count == 0 ? 0 : Math.Clamp(Math.Sqrt(sum / count) / 64.0, 0, 1);
    }

    public static double SampleLuma(byte[] pixels, int width, int height, int stride, double x, double y)
    {
        int ix = Math.Clamp((int)Math.Round(x), 0, width - 1);
        int iy = Math.Clamp((int)Math.Round(y), 0, height - 1);
        return GetLuminance(pixels, stride, ix, iy);
    }

    public static double GetLuminance(byte[] pixels, int stride, int x, int y)
    {
        int index = y * stride + x * 4;
        return pixels[index + 2] * 0.299 + pixels[index + 1] * 0.587 + pixels[index] * 0.114;
    }
}
