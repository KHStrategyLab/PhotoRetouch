using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class StandardMaskWarpEngine : IPortraitMaskEngine
{
    private readonly StandardMaskLoader _loader;
    private readonly IStandardMaskWarper _warper;
    private readonly IFaceAnalyzer _faceAnalyzer;
    private readonly IFaceParsingDetector _parsingDetector;
    private readonly NostrilDetector _nostrilDetector;

    public StandardMaskWarpEngine()
        : this(new StandardMaskLoader(), new StandardAffineMaskWarper(), new OpenCvFaceAnalyzer(), new NoFaceParsingDetector(), new NostrilDetector())
    {
    }

    public StandardMaskWarpEngine(
        StandardMaskLoader loader,
        IStandardMaskWarper warper,
        IFaceAnalyzer faceAnalyzer,
        IFaceParsingDetector parsingDetector,
        NostrilDetector nostrilDetector)
    {
        _loader = loader;
        _warper = warper;
        _faceAnalyzer = faceAnalyzer;
        _parsingDetector = parsingDetector;
        _nostrilDetector = nostrilDetector;
    }

    public string MaskVersion => "standard_mask_warp_v1+feature_mesh_50_bw_v1+skin_tone_v1+nose_structure_v1+skin_hole_eye_path_v1+lip_landmark_path_v1+nostril_detector_v1+nose_soft_shield_v1+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion;

    public PortraitMaskResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        FaceAnalyzerResult faceAnalyzerResult = _faceAnalyzer.Analyze(source, faceWorkArea);
        MaskWarpInput input = faceAnalyzerResult.ToMaskWarpInput(width, height);
        StandardMaskSet standardMasks = _loader.Load();
        WarpedMaskSet warped = _warper.Warp(standardMasks, input);
        MaskPlane empty = MaskPlane.Empty(width, height);

        FaceMaskSet warpedStandardMasks = BuildFaceMaskSetFromWarpedMasks(warped, empty);
        IReadOnlyDictionary<string, WpfPoint> landmarks = faceAnalyzerResult.ToLandmarks();
        ParsingMaskSet? parsingMasks = _parsingDetector.Detect(source, new FaceParsingInput(input.FaceBox, landmarks, input.FaceAngle));
        FaceAnalysisResult preliminaryAnalysis = new(
            input.FaceBox,
            landmarks,
            null,
            input.FaceAngle,
            faceAnalyzerResult.Confidence,
            faceAnalyzerResult.Confidence,
            parsingMasks?.ParsingConfidence ?? 0,
            faceAnalyzerResult.DebugWarnings);

        MaskPlane parsingEyeMask = UnionOptional(
            width,
            height,
            parsingMasks?.LeftEyeMask,
            parsingMasks?.RightEyeMask);
        MaskPlane landmarkEyeShapeMask = BuildLandmarkEyeShapeMask(width, height, input);
        MaskPlane skinHoleEyePathMask = BuildSkinHoleEyePathMask(source, preliminaryAnalysis, input, landmarkEyeShapeMask);
        MaskPlane parsingEyebrowMask = UnionOptional(
            width,
            height,
            parsingMasks?.LeftEyebrowMask,
            parsingMasks?.RightEyebrowMask);
        MaskPlane parsingLipMask = UnionOptional(
            width,
            height,
            parsingMasks?.UpperLipMask,
            parsingMasks?.LowerLipMask);
        MaskPlane landmarkLipShapeMask = BuildLandmarkLipShapeMask(width, height, input, landmarks);

        MaskPlane eyeMask = ExpandAndFeatherProtectMask(
            MaskPlane.Union(warped.EyeProtectMask, parsingEyeMask, skinHoleEyePathMask),
            GetEyeProtectExpandRadius(input.FaceBox),
            2);
        MaskPlane eyebrowMask = MaskPlane.Union(warped.EyebrowProtectMask, parsingEyebrowMask);
        MaskPlane lipMask = ExpandAndFeatherProtectMask(
            MaskPlane.Union(warped.LipProtectMask, parsingLipMask, landmarkLipShapeMask),
            GetLipProtectExpandRadius(input.FaceBox),
            2);
        MaskPlane innerMouthMask = parsingMasks?.InnerMouthMask ?? empty;
        MaskPlane hairMask = parsingMasks?.HairMask ?? empty;
        MaskPlane beardMask = parsingMasks?.BeardMask ?? empty;
        MaskPlane mustacheMask = parsingMasks?.MustacheMask ?? empty;
        MaskPlane glassesMask = parsingMasks?.GlassesMask ?? empty;
        MaskPlane neckMask = parsingMasks?.NeckMask ?? empty;
        MaskPlane skinMask = parsingMasks?.SkinMask ?? empty;
        NostrilDetectorResult nostrilDetection = _nostrilDetector.Detect(new NostrilDetectorInput(
            source,
            input.FaceBox,
            landmarks,
            input.NoseTip,
            input.MouthCenter,
            input.LeftEyeCenter,
            input.RightEyeCenter,
            warped.NostrilProtectMask,
            lipMask,
            MaskPlane.Union(beardMask, mustacheMask)));
        MaskPlane nostrilMask = nostrilDetection.NostrilMask;
        MaskPlane hardProtectMask = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            warped.EyeProtectMask,
            nostrilMask,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask);
        MaskPlane noseSoftProtectMask = BuildNoseSoftShieldProtectMask(width, height, input);
        MaskPlane noseSkinMask = empty;
        MaskPlane retouchAllowMask = empty;
        MaskPlane softProtectMask = MaskPlane.Union(warped.SoftProtectMask, noseSoftProtectMask);
        MaskPlane finalOverlayMask = hardProtectMask.Clone();
        FaceFeatureMeshSet featureMeshes = FeatureMeshGenerator.GenerateFromMaskGuides(
            input,
            landmarks,
            eyeMask,
            lipMask,
            noseSoftProtectMask,
            eyebrowMask);

        FaceMaskSet masks = new(
            skinMask,
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            empty,
            empty,
            noseSkinMask,
            nostrilMask,
            empty,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask,
            hardProtectMask,
            softProtectMask,
            retouchAllowMask,
            finalOverlayMask);

        List<string> warnings = new()
        {
            "standard_mask_warp_engine",
            "blob_mask_generators_removed"
        };
        warnings.AddRange(faceAnalyzerResult.DebugWarnings);
        warnings.AddRange(standardMasks.DebugWarnings);
        if (parsingMasks is null)
        {
            warnings.Add("face_parsing_not_connected_no_blob_fallback");
        }
        else
        {
            warnings.AddRange(parsingMasks.DebugWarnings);
            AddParsingAreaWarnings(warnings, parsingMasks, input.FaceBox);
        }
        warnings.Add(skinHoleEyePathMask.Average() > landmarkEyeShapeMask.Average() * 0.12
            ? "skin_hole_eye_path_mask"
            : "skin_hole_eye_path_fallback_landmark_shape");
        warnings.Add("lip_landmark_path_mask");
        warnings.Add("feature_mesh_50_points_from_bw_guides_lip_eye_nose_brow");
        warnings.AddRange(nostrilDetection.DebugWarnings);
        warnings.Add("nose_soft_shield_protect_50_percent");

        FaceAnalysisResult analysis = new(
            input.FaceBox,
            landmarks,
            null,
            input.FaceAngle,
            faceAnalyzerResult.Confidence,
            faceAnalyzerResult.Confidence,
            parsingMasks?.ParsingConfidence ?? 0,
            warnings);
        MaskQualityReport report = MaskQualityReport.FromMasks(analysis, masks);
        return new PortraitMaskResult(analysis, masks, report, parsingMasks, warpedStandardMasks, nostrilDetection, featureMeshes);
    }

    private static FaceMaskSet BuildFaceMaskSetFromWarpedMasks(WarpedMaskSet warped, MaskPlane empty)
    {
        MaskPlane hardProtectMask = MaskPlane.Union(
            warped.EyeProtectMask,
            warped.EyebrowProtectMask,
            warped.LipProtectMask);
        MaskPlane noseSkinMask = empty;
        MaskPlane retouchAllowMask = empty;
        MaskPlane softProtectMask = empty;
        MaskPlane finalOverlayMask = hardProtectMask.Clone();

        return new FaceMaskSet(
            empty,
            warped.EyeProtectMask,
            warped.EyebrowProtectMask,
            warped.LipProtectMask,
            empty,
            empty,
            empty,
            noseSkinMask,
            empty,
            empty,
            empty,
            empty,
            empty,
            empty,
            hardProtectMask,
            softProtectMask,
            retouchAllowMask,
            finalOverlayMask);
    }

    private static MaskPlane UnionOptional(int width, int height, params MaskPlane?[] masks)
    {
        MaskPlane[] availableMasks = masks
            .Where(mask => mask is not null)
            .Cast<MaskPlane>()
            .ToArray();
        return availableMasks.Length == 0
            ? MaskPlane.Empty(width, height)
            : MaskPlane.Union(availableMasks);
    }

    private static int GetEyeProtectExpandRadius(System.Windows.Int32Rect faceBox)
    {
        return Math.Clamp((int)Math.Round(Math.Max(faceBox.Width, faceBox.Height) * 0.0025), 1, 4);
    }

    private static int GetLipProtectExpandRadius(System.Windows.Int32Rect faceBox)
    {
        return Math.Clamp((int)Math.Round(Math.Max(faceBox.Width, faceBox.Height) * 0.0022), 1, 3);
    }

    private static MaskPlane BuildLandmarkLipShapeMask(int width, int height, MaskWarpInput input, IReadOnlyDictionary<string, WpfPoint> landmarks)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        WpfPoint left = landmarks.TryGetValue("mouth_left", out WpfPoint landmarkLeft)
            ? landmarkLeft
            : new WpfPoint(input.MouthCenter.X - input.FaceBox.Width * 0.10, input.MouthCenter.Y);
        WpfPoint right = landmarks.TryGetValue("mouth_right", out WpfPoint landmarkRight)
            ? landmarkRight
            : new WpfPoint(input.MouthCenter.X + input.FaceBox.Width * 0.10, input.MouthCenter.Y);

        double mouthWidth = Distance(left, right);
        if (mouthWidth <= 2)
        {
            return mask;
        }

        double faceHeight = Math.Max(1, input.FaceBox.Height);
        double faceAngle = Math.Atan2(right.Y - left.Y, right.X - left.X);
        WpfPoint center = new((left.X + right.X) / 2, (left.Y + right.Y) / 2);
        double halfWidth = Math.Clamp(mouthWidth * 0.55, input.FaceBox.Width * 0.065, input.FaceBox.Width * 0.18);
        double upperHeight = Math.Clamp(mouthWidth * 0.092, faceHeight * 0.012, faceHeight * 0.032);
        double lowerHeight = Math.Clamp(mouthWidth * 0.118, faceHeight * 0.017, faceHeight * 0.044);
        double centerOffset = Math.Clamp(faceHeight * 0.004, 1, 4);

        PaintLipShape(mask, center, halfWidth, upperHeight, lowerHeight, centerOffset, faceAngle, 1);
        return mask;
    }

    private static MaskPlane BuildNoseSoftShieldProtectMask(int width, int height, MaskWarpInput input)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        double eyeDistance = Distance(input.LeftEyeCenter, input.RightEyeCenter);
        if (eyeDistance <= 1)
        {
            return mask;
        }

        double faceWidth = Math.Max(1, input.FaceBox.Width);
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        WpfPoint eyeMid = new(
            (input.LeftEyeCenter.X + input.RightEyeCenter.X) / 2,
            (input.LeftEyeCenter.Y + input.RightEyeCenter.Y) / 2);
        double faceAngle = Math.Atan2(input.RightEyeCenter.Y - input.LeftEyeCenter.Y, input.RightEyeCenter.X - input.LeftEyeCenter.X);
        double cos = Math.Cos(faceAngle);
        double sin = Math.Sin(faceAngle);
        WpfPoint axisX = new(cos, sin);
        WpfPoint axisY = new(-sin, cos);

        WpfPoint topCenter = Add(eyeMid, axisY, -Math.Clamp(faceHeight * 0.090, 18, 60));
        WpfPoint lowerNoseCenter = Add(input.NoseTip, axisY, Math.Clamp(faceHeight * 0.055, 10, 38));
        double topHalfWidth = Math.Clamp(Math.Min(faceWidth * 0.070, eyeDistance * 0.170), 14, faceWidth * 0.115);
        double noseHalfWidth = Math.Clamp(Math.Min(faceWidth * 0.125, eyeDistance * 0.285), 16, faceWidth * 0.18);
        double featherRadius = Math.Clamp(faceHeight * 0.055, 18, 64);

        PaintSoftNoseShield(mask, topCenter, lowerNoseCenter, axisX, axisY, topHalfWidth, noseHalfWidth, featherRadius, 0.50);
        for (int pass = 0; pass < 3; pass++)
        {
            mask = Feather(mask);
        }

        return mask;
    }

    private static WpfPoint Add(WpfPoint point, WpfPoint direction, double distance)
    {
        return new WpfPoint(point.X + direction.X * distance, point.Y + direction.Y * distance);
    }

    private static void PaintSoftNoseShield(
        MaskPlane mask,
        WpfPoint topCenter,
        WpfPoint bottomCenter,
        WpfPoint axisX,
        WpfPoint axisY,
        double topHalfWidth,
        double bottomHalfWidth,
        double featherRadius,
        double opacity)
    {
        double span = Distance(topCenter, bottomCenter);
        if (span <= 1)
        {
            return;
        }

        double maxHalfWidth = Math.Max(topHalfWidth, bottomHalfWidth);
        double minX = Math.Min(topCenter.X, bottomCenter.X) - maxHalfWidth - featherRadius;
        double maxX = Math.Max(topCenter.X, bottomCenter.X) + maxHalfWidth + featherRadius;
        double minY = Math.Min(topCenter.Y, bottomCenter.Y) - maxHalfWidth - featherRadius;
        double maxY = Math.Max(topCenter.Y, bottomCenter.Y) + maxHalfWidth + featherRadius;
        int left = Math.Max(0, (int)Math.Floor(minX));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(maxX));
        int top = Math.Max(0, (int)Math.Floor(minY));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(maxY));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x - topCenter.X;
                double dy = y - topCenter.Y;
                double localX = dx * axisX.X + dy * axisX.Y;
                double localY = dx * axisY.X + dy * axisY.Y;
                double yRatio = Math.Clamp(localY / span, 0, 1);
                double easedY = SmoothStep(0, 1, yRatio);
                double halfWidth = topHalfWidth + (bottomHalfWidth - topHalfWidth) * easedY;
                double horizontal = 1 - SmoothStep(halfWidth, halfWidth + featherRadius, Math.Abs(localX));
                double verticalTop = SmoothStep(-featherRadius, 0, localY);
                double verticalBottom = 1 - SmoothStep(span, span + featherRadius, localY);
                double amount = Math.Min(horizontal, Math.Min(verticalTop, verticalBottom)) * opacity;
                if (amount <= 0)
                {
                    continue;
                }

                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static void PaintLipShape(
        MaskPlane mask,
        WpfPoint center,
        double halfWidth,
        double upperHeight,
        double lowerHeight,
        double centerOffset,
        double angle,
        double opacity)
    {
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double searchRadiusX = halfWidth * 1.18;
        double searchRadiusY = Math.Max(upperHeight, lowerHeight) * 1.65;
        int left = Math.Max(0, (int)Math.Floor(center.X - searchRadiusX));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(center.X + searchRadiusX));
        int top = Math.Max(0, (int)Math.Floor(center.Y - searchRadiusY));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(center.Y + searchRadiusY));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                double localX = dx * cos + dy * sin;
                double localY = -dx * sin + dy * cos;
                double nx = Math.Abs(localX) / Math.Max(1, halfWidth);
                if (nx > 1.08)
                {
                    continue;
                }

                double lipEdgeTaper = Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 1.92)), 0.58);
                double cupidBowLift = Math.Exp(-Math.Pow(localX / Math.Max(1, halfWidth * 0.22), 2)) * upperHeight * 0.50;
                double upperLobeLift = (
                    Math.Exp(-Math.Pow((localX - halfWidth * 0.34) / Math.Max(1, halfWidth * 0.20), 2)) +
                    Math.Exp(-Math.Pow((localX + halfWidth * 0.34) / Math.Max(1, halfWidth * 0.20), 2))) * upperHeight * 0.22;
                double upperLimit = upperHeight * lipEdgeTaper - cupidBowLift + upperLobeLift - centerOffset;
                double lowerBulge = Math.Exp(-Math.Pow(localX / Math.Max(1, halfWidth * 0.58), 2)) * lowerHeight * 0.22;
                double lowerLimit = lowerHeight * Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 2.20)), 0.50) + lowerBulge + centerOffset;
                double distanceFromLip = localY < -upperLimit
                    ? -upperLimit - localY
                    : localY > lowerLimit
                        ? localY - lowerLimit
                        : 0;
                double feather = Math.Max(1.2, Math.Min(upperHeight, lowerHeight) * 0.42);
                double amount = distanceFromLip <= 0
                    ? opacity
                    : opacity * (1 - SmoothStep(0, feather, distanceFromLip));

                if (amount <= 0)
                {
                    continue;
                }

                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static MaskPlane BuildLandmarkEyeShapeMask(int width, int height, MaskWarpInput input)
    {
        MaskPlane mask = MaskPlane.Empty(width, height);
        double eyeDistance = Distance(input.LeftEyeCenter, input.RightEyeCenter);
        if (eyeDistance <= 1)
        {
            return mask;
        }

        double faceWidth = Math.Max(1, input.FaceBox.Width);
        double faceHeight = Math.Max(1, input.FaceBox.Height);
        double radiusX = Math.Clamp(Math.Min(faceWidth * 0.100, eyeDistance * 0.205), 7, faceWidth * 0.145);
        double radiusY = Math.Clamp(faceHeight * 0.026, 3, faceHeight * 0.045);
        double angle = Math.Atan2(input.RightEyeCenter.Y - input.LeftEyeCenter.Y, input.RightEyeCenter.X - input.LeftEyeCenter.X);

        PaintEyeShape(mask, input.LeftEyeCenter, radiusX, radiusY, angle, 1);
        PaintEyeShape(mask, input.RightEyeCenter, radiusX, radiusY, angle, 1);
        return mask;
    }

    private static MaskPlane BuildSkinHoleEyePathMask(BitmapSource source, FaceAnalysisResult analysis, MaskWarpInput input, MaskPlane guideMask)
    {
        AverageFaceColorMaskResult colorMask = AverageFaceColorMaskBuilder.Build(source, analysis, null, 0.45, CancellationToken.None);
        if (colorMask.ColorDifferenceMask.Width != guideMask.Width ||
            colorMask.ColorDifferenceMask.Height != guideMask.Height ||
            colorMask.AverageSignal <= 0.000001)
        {
            return guideMask;
        }

        int searchRadius = Math.Clamp((int)Math.Round(Math.Max(input.FaceBox.Width, input.FaceBox.Height) * 0.010), 4, 14);
        MaskPlane searchGuide = Dilate(guideMask, searchRadius);
        searchGuide = Feather(searchGuide);

        MaskPlane result = MaskPlane.Empty(guideMask.Width, guideMask.Height);
        for (int index = 0; index < result.Values.Length; index++)
        {
            double blackHole = 1 - colorMask.ColorDifferenceMask.Values[index];
            double candidate = SmoothStep(0.42, 0.86, blackHole);
            result.Values[index] = Math.Clamp(candidate * searchGuide.Values[index], 0, 1);
        }

        result = KeepStrongEyeHoleCore(result, guideMask);
        return result.Average() > guideMask.Average() * 0.12
            ? result
            : guideMask;
    }

    private static MaskPlane KeepStrongEyeHoleCore(MaskPlane source, MaskPlane guideMask)
    {
        MaskPlane.EnsureSameSize(source, guideMask);
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int index = 0; index < source.Values.Length; index++)
        {
            double value = source.Values[index];
            if (value < 0.08)
            {
                continue;
            }

            double guide = guideMask.Values[index];
            result.Values[index] = Math.Clamp(Math.Max(value, guide * value * 0.65), 0, 1);
        }

        return result;
    }

    private static void PaintEyeShape(MaskPlane mask, WpfPoint center, double radiusX, double radiusY, double angle, double opacity)
    {
        if (radiusX <= 0 || radiusY <= 0)
        {
            return;
        }

        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        double searchRadius = Math.Max(radiusX, radiusY) * 1.35;
        int left = Math.Max(0, (int)Math.Floor(center.X - searchRadius));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(center.X + searchRadius));
        int top = Math.Max(0, (int)Math.Floor(center.Y - searchRadius));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(center.Y + searchRadius));

        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double dx = x - center.X;
                double dy = y - center.Y;
                double localX = dx * cos + dy * sin;
                double localY = -dx * sin + dy * cos;
                double nx = Math.Abs(localX) / radiusX;
                if (nx > 1.03)
                {
                    continue;
                }

                double lidLimit = radiusY * Math.Pow(Math.Max(0, 1 - Math.Pow(Math.Min(nx, 1), 1.54)), 0.70);
                double distanceFromLid = Math.Abs(localY) - lidLimit;
                double amount;
                if (distanceFromLid <= 0)
                {
                    amount = opacity;
                }
                else
                {
                    double feather = Math.Max(0.75, radiusY * 0.20);
                    amount = opacity * (1 - SmoothStep(0, feather, distanceFromLid));
                }

                if (amount <= 0)
                {
                    continue;
                }

                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static void PaintRotatedLine(MaskPlane mask, WpfPoint center, double halfLength, double radius, double angle, double opacity)
    {
        WpfPoint start = new(center.X - Math.Cos(angle) * halfLength, center.Y - Math.Sin(angle) * halfLength);
        WpfPoint end = new(center.X + Math.Cos(angle) * halfLength, center.Y + Math.Sin(angle) * halfLength);
        double dx = end.X - start.X;
        double dy = end.Y - start.Y;
        double lengthSquared = dx * dx + dy * dy;
        if (lengthSquared <= 0.0001 || radius <= 0)
        {
            return;
        }

        int left = Math.Max(0, (int)Math.Floor(Math.Min(start.X, end.X) - radius));
        int right = Math.Min(mask.Width - 1, (int)Math.Ceiling(Math.Max(start.X, end.X) + radius));
        int top = Math.Max(0, (int)Math.Floor(Math.Min(start.Y, end.Y) - radius));
        int bottom = Math.Min(mask.Height - 1, (int)Math.Ceiling(Math.Max(start.Y, end.Y) + radius));
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                double t = ((x - start.X) * dx + (y - start.Y) * dy) / lengthSquared;
                t = Math.Clamp(t, 0, 1);
                double closestX = start.X + dx * t;
                double closestY = start.Y + dy * t;
                double distance = Math.Sqrt((x - closestX) * (x - closestX) + (y - closestY) * (y - closestY));
                if (distance > radius)
                {
                    continue;
                }

                double amount = opacity * (1 - SmoothStep(0.40, 1, distance / radius));
                int index = y * mask.Width + x;
                mask.Values[index] = Math.Max(mask.Values[index], amount);
            }
        }
    }

    private static MaskPlane ExpandAndFeatherProtectMask(MaskPlane source, int expandRadius, int featherPasses)
    {
        if (source.Average() <= 0.000001)
        {
            return source;
        }

        MaskPlane result = Dilate(source, expandRadius);
        for (int pass = 0; pass < featherPasses; pass++)
        {
            result = Feather(result);
        }

        return result;
    }

    private static MaskPlane Dilate(MaskPlane source, int radius)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double value = 0;
                for (int sampleY = Math.Max(0, y - radius); sampleY <= Math.Min(source.Height - 1, y + radius); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - radius); sampleX <= Math.Min(source.Width - 1, x + radius); sampleX++)
                    {
                        value = Math.Max(value, source[sampleX, sampleY]);
                    }
                }

                result[x, y] = value;
            }
        }

        return result;
    }

    private static MaskPlane Feather(MaskPlane source)
    {
        MaskPlane result = MaskPlane.Empty(source.Width, source.Height);
        for (int y = 0; y < source.Height; y++)
        {
            for (int x = 0; x < source.Width; x++)
            {
                double sum = 0;
                double weight = 0;
                for (int sampleY = Math.Max(0, y - 1); sampleY <= Math.Min(source.Height - 1, y + 1); sampleY++)
                {
                    for (int sampleX = Math.Max(0, x - 1); sampleX <= Math.Min(source.Width - 1, x + 1); sampleX++)
                    {
                        double distance = Math.Max(Math.Abs(sampleX - x), Math.Abs(sampleY - y));
                        double sampleWeight = distance == 0 ? 1 : 0.55;
                        sum += source[sampleX, sampleY] * sampleWeight;
                        weight += sampleWeight;
                    }
                }

                result[x, y] = weight > 0 ? sum / weight : source[x, y];
            }
        }

        return result;
    }

    private static void AddParsingAreaWarnings(List<string> warnings, ParsingMaskSet parsingMasks, System.Windows.Int32Rect faceBox)
    {
        double faceRatio = faceBox.Width * faceBox.Height /
            (double)Math.Max(1, (parsingMasks.SkinMask?.Width ?? 1) * (parsingMasks.SkinMask?.Height ?? 1));

        AddOptionalAreaWarning(warnings, parsingMasks.SkinMask, faceRatio * 0.12, faceRatio * 1.20, "parsing_skin_area");
        AddOptionalAreaWarning(warnings, parsingMasks.HairMask, 0.0001, faceRatio * 1.1, "parsing_hair_area");
        AddOptionalAreaWarning(warnings, parsingMasks.LeftEyeMask, 0.00005, faceRatio * 0.08, "parsing_left_eye_area");
        AddOptionalAreaWarning(warnings, parsingMasks.RightEyeMask, 0.00005, faceRatio * 0.08, "parsing_right_eye_area");
        AddOptionalAreaWarning(warnings, parsingMasks.UpperLipMask, 0.00005, faceRatio * 0.08, "parsing_upper_lip_area");
        AddOptionalAreaWarning(warnings, parsingMasks.LowerLipMask, 0.00005, faceRatio * 0.08, "parsing_lower_lip_area");
    }

    private static void AddOptionalAreaWarning(List<string> warnings, MaskPlane? mask, double minAverage, double maxAverage, string warning)
    {
        if (mask is null)
        {
            warnings.Add(warning + "_missing");
            return;
        }

        double average = mask.Average();
        if (average < minAverage || average > maxAverage)
        {
            warnings.Add(warning);
        }
    }

    private static double Distance(WpfPoint first, WpfPoint second)
    {
        double dx = first.X - second.X;
        double dy = first.Y - second.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    private static double SmoothStep(double edge0, double edge1, double value)
    {
        if (edge1 <= edge0)
        {
            return value >= edge1 ? 1 : 0;
        }

        double t = Math.Clamp((value - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }
}
