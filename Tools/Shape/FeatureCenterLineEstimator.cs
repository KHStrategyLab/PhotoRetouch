using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed record FeatureCenterLine(
    WpfPoint Center,
    WpfPoint EyeCenter,
    WpfPoint EyebrowCenter,
    WpfPoint MouthCenter,
    WpfPoint NostrilCenter,
    IReadOnlyList<string> DebugWarnings);

public static class FeatureCenterLineEstimator
{
    public static FeatureCenterLine Estimate(FaceSnapshotMaskSet snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        Int32Rect faceBox = snapshot.Analysis.FaceBox;
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        WpfPoint fallbackCenter = new(faceBox.X + faceWidth / 2d, faceBox.Y + faceHeight / 2d);
        IReadOnlyDictionary<string, WpfPoint> landmarks = snapshot.Analysis.FaceLandmarks;
        TryGetPoint(landmarks, "left_eye", out WpfPoint leftEye);
        TryGetPoint(landmarks, "right_eye", out WpfPoint rightEye);
        TryGetPoint(landmarks, "nose_tip", out WpfPoint noseTip);
        TryGetPoint(landmarks, "mouth_center", out WpfPoint mouthCenter);

        List<string> warnings = new();
        WpfPoint eyeCenter = leftEye != default && rightEye != default
            ? Midpoint(leftEye, rightEye)
            : fallbackCenter;
        if (leftEye == default || rightEye == default)
        {
            warnings.Add("feature_center_eye_center_estimated");
        }

        WpfPoint eyebrowCenter = eyeCenter == default
            ? fallbackCenter
            : new WpfPoint(eyeCenter.X, eyeCenter.Y - faceHeight * 0.12);
        WpfPoint effectiveMouthCenter = mouthCenter == default
            ? new WpfPoint(fallbackCenter.X, faceBox.Y + faceHeight * 0.63)
            : mouthCenter;
        if (mouthCenter == default)
        {
            warnings.Add("feature_center_mouth_center_estimated");
        }

        WpfPoint nostrilCenter = CalculateMaskCenter(snapshot.Masks.NostrilMask);
        if (double.IsNaN(nostrilCenter.X))
        {
            nostrilCenter = noseTip == default
                ? new WpfPoint(fallbackCenter.X, faceBox.Y + faceHeight * 0.52)
                : new WpfPoint(noseTip.X, noseTip.Y + faceHeight * 0.065);
            warnings.Add("feature_center_nostril_center_estimated");
        }

        double weightedX =
            eyeCenter.X * 1.15 +
            eyebrowCenter.X * 0.85 +
            effectiveMouthCenter.X * 1.0 +
            nostrilCenter.X * 1.25;
        double totalWeight = 1.15 + 0.85 + 1.0 + 1.25;
        double centerX = weightedX / totalWeight;
        double centerY = (eyeCenter.Y + eyebrowCenter.Y + effectiveMouthCenter.Y + nostrilCenter.Y) / 4d;
        return new FeatureCenterLine(
            new WpfPoint(centerX, centerY),
            eyeCenter,
            eyebrowCenter,
            effectiveMouthCenter,
            nostrilCenter,
            warnings);
    }

    private static bool TryGetPoint(IReadOnlyDictionary<string, WpfPoint> landmarks, string key, out WpfPoint point)
    {
        return landmarks.TryGetValue(key, out point);
    }

    private static WpfPoint Midpoint(WpfPoint left, WpfPoint right)
    {
        return new WpfPoint((left.X + right.X) / 2d, (left.Y + right.Y) / 2d);
    }

    private static WpfPoint CalculateMaskCenter(MaskPlane mask)
    {
        double sum = 0;
        double xSum = 0;
        double ySum = 0;
        for (int y = 0; y < mask.Height; y++)
        {
            for (int x = 0; x < mask.Width; x++)
            {
                double value = mask[x, y];
                if (value <= 0.001)
                {
                    continue;
                }

                sum += value;
                xSum += x * value;
                ySum += y * value;
            }
        }

        return sum <= Math.Max(4, mask.Width * mask.Height * 0.00005)
            ? new WpfPoint(double.NaN, double.NaN)
            : new WpfPoint(xSum / sum, ySum / sum);
    }
}
