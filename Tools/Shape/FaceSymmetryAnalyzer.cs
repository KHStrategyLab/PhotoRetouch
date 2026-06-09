using System.Windows;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class FaceSymmetryAnalyzer
{
    public ShapeBalanceAnalysisReport Analyze(FaceSnapshotMaskSet snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        IReadOnlyDictionary<string, WpfPoint> landmarks = snapshot.Analysis.FaceLandmarks;
        List<string> warnings = new() { "shape_balance_analysis_v1" };
        Int32Rect faceBox = snapshot.Analysis.FaceBox;
        WpfPoint faceCenter = new(faceBox.X + faceBox.Width / 2d, faceBox.Y + faceBox.Height / 2d);
        FeatureCenterLine featureCenterLine = FeatureCenterLineEstimator.Estimate(snapshot);
        warnings.AddRange(featureCenterLine.DebugWarnings);

        bool hasLeftEye = TryGetPoint(landmarks, "left_eye", out WpfPoint leftEye);
        bool hasRightEye = TryGetPoint(landmarks, "right_eye", out WpfPoint rightEye);
        bool hasNose = TryGetPoint(landmarks, "nose_tip", out WpfPoint noseTip);
        bool hasMouth = TryGetPoint(landmarks, "mouth_center", out WpfPoint mouthCenter);
        bool hasChin = TryGetPoint(landmarks, "chin", out WpfPoint chinPoint);

        double faceRollAngle = snapshot.Analysis.FaceAngle;
        double eyeLevelDelta = hasLeftEye && hasRightEye ? rightEye.Y - leftEye.Y : 0;
        double faceWidth = Math.Max(1, faceBox.Width);
        double faceHeight = Math.Max(1, faceBox.Height);
        double centerLineX = featureCenterLine.Center.X;
        double noseCenterDelta = hasNose ? noseTip.X - centerLineX : 0;
        double chinCenterDelta = hasChin ? chinPoint.X - centerLineX : 0;
        double faceYawLikeBias = hasNose ? (noseTip.X - centerLineX) / faceWidth : 0;
        double facePitchLikeBias = hasMouth && hasNose
            ? ((mouthCenter.Y - noseTip.Y) / faceHeight) - 0.22
            : 0;
        double noseLineTilt = hasNose && hasChin
            ? Math.Atan2(chinPoint.X - noseTip.X, Math.Max(1, chinPoint.Y - noseTip.Y)) * 180d / Math.PI
            : 0;

        double eyebrowLevelDelta = 0;
        double mouthCornerDelta = 0;
        warnings.Add("eyebrow_level_delta_estimated_pending_brow_landmarks");
        warnings.Add("mouth_corner_delta_estimated_pending_mouth_corner_landmarks");

        if (!hasLeftEye || !hasRightEye)
        {
            warnings.Add("eye_landmarks_missing");
        }

        if (!hasNose)
        {
            warnings.Add("nose_landmark_missing");
        }

        if (!hasMouth)
        {
            warnings.Add("mouth_landmark_missing");
        }

        if (!hasChin)
        {
            warnings.Add("chin_landmark_missing");
        }

        NostrilBalanceObservation nostrilObservation = CreateNostrilObservation(snapshot, featureCenterLine.NostrilCenter.X, warnings);
        double normalizedEyeDelta = Math.Abs(eyeLevelDelta) / faceHeight;
        double normalizedNoseDelta = Math.Abs(noseCenterDelta) / faceWidth;
        double normalizedChinDelta = Math.Abs(chinCenterDelta) / faceWidth;
        double rollScore = Math.Abs(faceRollAngle) / 12d;
        double asymmetry = Math.Clamp((normalizedEyeDelta + normalizedNoseDelta + normalizedChinDelta + rollScore) / 4d, 0, 1);
        double leftRightBalanceScore = 1 - asymmetry;
        double suggestedStrength = Math.Clamp(0.22 + asymmetry * 0.55, 0.18, 0.62);

        return new ShapeBalanceAnalysisReport(
            snapshot.ImageId,
            faceRollAngle,
            faceYawLikeBias,
            facePitchLikeBias,
            eyeLevelDelta,
            eyebrowLevelDelta,
            mouthCornerDelta,
            noseLineTilt,
            chinCenterDelta,
            leftRightBalanceScore,
            suggestedStrength,
            nostrilObservation,
            warnings);
    }

    private static bool TryGetPoint(IReadOnlyDictionary<string, WpfPoint> landmarks, string key, out WpfPoint point)
    {
        return landmarks.TryGetValue(key, out point);
    }

    private static NostrilBalanceObservation CreateNostrilObservation(
        FaceSnapshotMaskSet snapshot,
        double noseCenterX,
        List<string> warnings)
    {
        MaskPlane nostrilMask = snapshot.Masks.NostrilMask;
        if (nostrilMask.Width <= 0 || nostrilMask.Height <= 0)
        {
            warnings.Add("nostril_observation_mask_empty");
            return new NostrilBalanceObservation(0, 0, 0, false);
        }

        double leftArea = 0;
        double rightArea = 0;
        for (int y = 0; y < nostrilMask.Height; y++)
        {
            for (int x = 0; x < nostrilMask.Width; x++)
            {
                double value = nostrilMask[x, y];
                if (x < noseCenterX)
                {
                    leftArea += value;
                }
                else
                {
                    rightArea += value;
                }
            }
        }

        double total = leftArea + rightArea;
        bool reliable = total > Math.Max(8, nostrilMask.Width * nostrilMask.Height * 0.00008);
        if (!reliable)
        {
            warnings.Add("nostril_balance_observation_low_confidence");
        }

        double delta = total <= 0 ? 0 : (rightArea - leftArea) / total;
        return new NostrilBalanceObservation(leftArea, rightArea, delta, reliable);
    }
}
