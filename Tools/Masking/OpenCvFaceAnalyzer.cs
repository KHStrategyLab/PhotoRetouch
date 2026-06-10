using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using OpenCvSharp;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class OpenCvFaceAnalyzer : IFaceAnalyzer
{
    private const string ModelFileName = "face_detection_yunet_2023mar.onnx";
    private const float ScoreThreshold = 0.55f;
    private const float NmsThreshold = 0.30f;
    private const int TopK = 5000;

    private readonly string _modelPath;

    public OpenCvFaceAnalyzer()
        : this(ResolveModelPath())
    {
    }

    public OpenCvFaceAnalyzer(string modelPath)
    {
        _modelPath = modelPath;
    }

    public string AnalyzerVersion => "opencv_yunet_2023mar_v4_facebox_initial_container_anchor_chin";

    public FaceAnalyzerResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        if (!File.Exists(_modelPath))
        {
            throw new InvalidOperationException("face_detection_model_missing:" + _modelPath);
        }

        using Mat bgr = CreateBgrMat(source);
        using FaceDetectorYN detector = FaceDetectorYN.Create(
            _modelPath,
            string.Empty,
            new OpenCvSharp.Size(source.PixelWidth, source.PixelHeight),
            ScoreThreshold,
            NmsThreshold,
            TopK);
        using Mat faces = new();
        detector.Detect(bgr, faces);
        if (faces.Empty() || faces.Rows == 0)
        {
            throw new InvalidOperationException("face_detection_failed");
        }

        DetectedFace selectedFace = SelectFace(faces, source.PixelWidth, source.PixelHeight);
        IReadOnlyList<string> warnings = BuildWarnings(faces.Rows, selectedFace);
        return new FaceAnalyzerResult(
            selectedFace.FaceBox,
            selectedFace.FaceAngle,
            selectedFace.LeftEyeCenter,
            selectedFace.RightEyeCenter,
            selectedFace.NoseTip,
            selectedFace.MouthCenter,
            selectedFace.ChinPoint,
            selectedFace.Confidence,
            warnings);
    }

    private static string ResolveModelPath()
    {
        string[] candidates =
        {
            Path.Combine(AppContext.BaseDirectory, "Assets", "AiModels", ModelFileName),
            Path.Combine(Environment.CurrentDirectory, "Assets", "AiModels", ModelFileName),
            Path.Combine(AppContext.BaseDirectory, ModelFileName),
            Path.Combine(Environment.CurrentDirectory, ModelFileName)
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static Mat CreateBgrMat(BitmapSource source)
    {
        BitmapSource bgraSource = source.Format == PixelFormats.Bgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int stride = bgraSource.PixelWidth * 4;
        byte[] pixels = new byte[stride * bgraSource.PixelHeight];
        bgraSource.CopyPixels(pixels, stride, 0);

        using Mat bgra = new(bgraSource.PixelHeight, bgraSource.PixelWidth, MatType.CV_8UC4);
        Marshal.Copy(pixels, 0, bgra.Data, pixels.Length);

        Mat bgr = new();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
        return bgr;
    }

    private static DetectedFace SelectFace(Mat faces, int imageWidth, int imageHeight)
    {
        DetectedFace? bestFace = null;
        double bestScore = double.MinValue;
        WpfPoint imageCenter = new(imageWidth / 2d, imageHeight / 2d);
        double maxCenterDistance = Math.Max(1, Math.Sqrt(imageCenter.X * imageCenter.X + imageCenter.Y * imageCenter.Y));

        int rows = faces.Rows;
        for (int row = 0; row < rows; row++)
        {
            DetectedFace face = ReadFace(faces, row, imageWidth, imageHeight);
            WpfPoint faceCenter = new(face.FaceBox.X + face.FaceBox.Width / 2d, face.FaceBox.Y + face.FaceBox.Height / 2d);
            double centerDistance = Distance(faceCenter, imageCenter) / maxCenterDistance;
            double areaRatio = (double)face.FaceBox.Width * face.FaceBox.Height / Math.Max(1, imageWidth * imageHeight);
            double rankingScore = areaRatio * 0.78 + (1 - Math.Clamp(centerDistance, 0, 1)) * 0.22 + face.Confidence * 0.08;
            if (rankingScore > bestScore)
            {
                bestScore = rankingScore;
                bestFace = face;
            }
        }

        return bestFace ?? throw new InvalidOperationException("face_detection_failed");
    }

    private static DetectedFace ReadFace(Mat faces, int row, int imageWidth, int imageHeight)
    {
        double x = faces.At<float>(row, 0);
        double y = faces.At<float>(row, 1);
        double width = faces.At<float>(row, 2);
        double height = faces.At<float>(row, 3);
        WpfPoint eyeA = new(faces.At<float>(row, 4), faces.At<float>(row, 5));
        WpfPoint eyeB = new(faces.At<float>(row, 6), faces.At<float>(row, 7));
        WpfPoint noseTip = new(faces.At<float>(row, 8), faces.At<float>(row, 9));
        WpfPoint mouthA = new(faces.At<float>(row, 10), faces.At<float>(row, 11));
        WpfPoint mouthB = new(faces.At<float>(row, 12), faces.At<float>(row, 13));
        double detectionScore = faces.Cols > 14 ? faces.At<float>(row, 14) : 0.75;

        WpfPoint leftEye = eyeA.X <= eyeB.X ? eyeA : eyeB;
        WpfPoint rightEye = eyeA.X <= eyeB.X ? eyeB : eyeA;
        WpfPoint mouthCenter = new((mouthA.X + mouthB.X) / 2d, (mouthA.Y + mouthB.Y) / 2d);
        Int32Rect rawFaceBox = ClampRect(x, y, width, height, imageWidth, imageHeight);
        WpfPoint chinPoint = EstimateChinPointFromAnchors(leftEye, rightEye, noseTip, mouthCenter, imageWidth, imageHeight);
        double faceAngle = Math.Atan2(rightEye.Y - leftEye.Y, rightEye.X - leftEye.X) * 180d / Math.PI;
        double confidence = Math.Clamp(detectionScore * CalculateLandmarkPlausibility(rawFaceBox, leftEye, rightEye, noseTip, mouthCenter, chinPoint), 0, 1);

        return new DetectedFace(
            rawFaceBox,
            faceAngle,
            ClampPoint(leftEye, imageWidth, imageHeight),
            ClampPoint(rightEye, imageWidth, imageHeight),
            ClampPoint(noseTip, imageWidth, imageHeight),
            ClampPoint(mouthCenter, imageWidth, imageHeight),
            ClampPoint(chinPoint, imageWidth, imageHeight),
            confidence);
    }

    private static IReadOnlyList<string> BuildWarnings(int faceCount, DetectedFace selectedFace)
    {
        List<string> warnings = new()
        {
            "opencv_yunet_face_analyzer",
            "facebox_initial_detection_container_only",
            "chin_estimated_from_eye_nose_mouth_anchors"
        };
        if (faceCount > 1)
        {
            warnings.Add("multiple_faces_detected_largest_center_face_selected");
        }

        if (Math.Abs(selectedFace.FaceAngle) > 15)
        {
            warnings.Add("face_angle_warning:" + selectedFace.FaceAngle.ToString("0.0", CultureInfo.InvariantCulture));
        }

        if (selectedFace.Confidence < 0.65)
        {
            warnings.Add("face_landmark_confidence_warning");
        }

        return warnings;
    }

    private static double CalculateLandmarkPlausibility(
        Int32Rect faceBox,
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        WpfPoint chinPoint)
    {
        double score = 1;
        if (leftEye.X >= rightEye.X)
        {
            score -= 0.35;
        }

        double eyeDistance = Distance(leftEye, rightEye);
        if (eyeDistance < faceBox.Width * 0.18 || eyeDistance > faceBox.Width * 0.70)
        {
            score -= 0.25;
        }

        if (noseTip.Y <= Math.Min(leftEye.Y, rightEye.Y) || mouthCenter.Y <= noseTip.Y || chinPoint.Y <= mouthCenter.Y)
        {
            score -= 0.25;
        }

        if (!Contains(faceBox, leftEye) || !Contains(faceBox, rightEye) || !Contains(faceBox, noseTip) || !Contains(faceBox, mouthCenter))
        {
            score -= 0.20;
        }

        return Math.Clamp(score, 0.20, 1);
    }

    private static Int32Rect ClampRect(double x, double y, double width, double height, int imageWidth, int imageHeight)
    {
        int rectWidth = Math.Clamp((int)Math.Round(width), 1, imageWidth);
        int rectHeight = Math.Clamp((int)Math.Round(height), 1, imageHeight);
        int rectX = Math.Clamp((int)Math.Round(x), 0, Math.Max(0, imageWidth - rectWidth));
        int rectY = Math.Clamp((int)Math.Round(y), 0, Math.Max(0, imageHeight - rectHeight));
        return new Int32Rect(rectX, rectY, rectWidth, rectHeight);
    }

    private static WpfPoint EstimateChinPointFromAnchors(
        WpfPoint leftEye,
        WpfPoint rightEye,
        WpfPoint noseTip,
        WpfPoint mouthCenter,
        int imageWidth,
        int imageHeight)
    {
        double eyeDistance = Distance(leftEye, rightEye);
        double eyeCenterX = (leftEye.X + rightEye.X) * 0.5;
        double noseToMouth = Math.Max(eyeDistance * 0.30, mouthCenter.Y - noseTip.Y);
        double mouthToChin = Math.Clamp(Math.Max(eyeDistance * 0.72, noseToMouth * 1.08), eyeDistance * 0.55, eyeDistance * 1.35);
        double chinX = eyeCenterX * 0.18 + noseTip.X * 0.24 + mouthCenter.X * 0.58;
        double chinY = mouthCenter.Y + mouthToChin;
        return ClampPoint(new WpfPoint(chinX, chinY), imageWidth, imageHeight);
    }

    private static WpfPoint ClampPoint(WpfPoint point, int imageWidth, int imageHeight)
    {
        return new WpfPoint(
            Math.Clamp(point.X, 0, Math.Max(0, imageWidth - 1)),
            Math.Clamp(point.Y, 0, Math.Max(0, imageHeight - 1)));
    }

    private static bool Contains(Int32Rect rect, WpfPoint point)
    {
        return point.X >= rect.X &&
               point.X <= rect.X + rect.Width &&
               point.Y >= rect.Y &&
               point.Y <= rect.Y + rect.Height;
    }

    private static double Distance(WpfPoint first, WpfPoint second)
    {
        double dx = first.X - second.X;
        double dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private sealed record DetectedFace(
        Int32Rect FaceBox,
        double FaceAngle,
        WpfPoint LeftEyeCenter,
        WpfPoint RightEyeCenter,
        WpfPoint NoseTip,
        WpfPoint MouthCenter,
        WpfPoint ChinPoint,
        double Confidence);
}
