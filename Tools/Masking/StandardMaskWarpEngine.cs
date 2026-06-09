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

    public string MaskVersion => "standard_mask_warp_v1+skin_tone_v1+nose_structure_v1+nostril_disabled+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion;

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

        MaskPlane parsingEyeMask = UnionOptional(
            width,
            height,
            parsingMasks?.LeftEyeMask,
            parsingMasks?.RightEyeMask);
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

        MaskPlane eyeMask = MaskPlane.Union(warped.EyeProtectMask, parsingEyeMask);
        MaskPlane eyebrowMask = MaskPlane.Union(warped.EyebrowProtectMask, parsingEyebrowMask);
        MaskPlane lipMask = MaskPlane.Union(warped.LipProtectMask, parsingLipMask);
        MaskPlane innerMouthMask = parsingMasks?.InnerMouthMask ?? empty;
        MaskPlane hairMask = parsingMasks?.HairMask ?? empty;
        MaskPlane beardMask = parsingMasks?.BeardMask ?? empty;
        MaskPlane mustacheMask = parsingMasks?.MustacheMask ?? empty;
        MaskPlane glassesMask = parsingMasks?.GlassesMask ?? empty;
        MaskPlane neckMask = parsingMasks?.NeckMask ?? empty;
        MaskPlane skinMask = parsingMasks?.SkinMask ?? empty;
        NostrilDetectorResult nostrilDetection = CreateDisabledNostrilDetection(width, height);
        MaskPlane nostrilMask = empty;
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
        MaskPlane noseSkinMask = empty;
        MaskPlane retouchAllowMask = empty;
        MaskPlane softProtectMask = empty;
        MaskPlane finalOverlayMask = hardProtectMask.Clone();

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
        warnings.Add("nostril_detector_disabled_for_face_color_only_mask");

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
        return new PortraitMaskResult(analysis, masks, report, parsingMasks, warpedStandardMasks, nostrilDetection);
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

    private static NostrilDetectorResult CreateDisabledNostrilDetection(int width, int height)
    {
        MaskPlane empty = MaskPlane.Empty(width, height);
        return new NostrilDetectorResult(
            empty,
            new System.Windows.Int32Rect(0, 0, 1, 1),
            empty,
            empty,
            0,
            new[] { "nostril_detector_disabled" },
            Array.Empty<NostrilCandidateComponent>());
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
}
