using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace PhotoRetouch;

public sealed class StandardMaskWarpEngine : IPortraitMaskEngine
{
    private readonly IFaceAnalyzer _faceAnalyzer;
    private readonly IFaceParsingDetector _parsingDetector;

    public StandardMaskWarpEngine()
        : this(new OpenCvFaceAnalyzer(), new NoFaceParsingDetector())
    {
    }

    public StandardMaskWarpEngine(IFaceAnalyzer faceAnalyzer, IFaceParsingDetector parsingDetector)
    {
        _faceAnalyzer = faceAnalyzer;
        _parsingDetector = parsingDetector;
    }

    public StandardMaskWarpEngine(
        StandardMaskLoader loader,
        IStandardMaskWarper warper,
        IFaceAnalyzer faceAnalyzer,
        IFaceParsingDetector parsingDetector,
        NostrilDetector nostrilDetector)
        : this(faceAnalyzer, parsingDetector)
    {
    }

    public string MaskVersion => "color_mask_only_v1+no_standard_dummy+nostril_disabled+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion;

    public PortraitMaskResult Analyze(BitmapSource source, FaceWorkArea faceWorkArea)
    {
        int width = source.PixelWidth;
        int height = source.PixelHeight;
        FaceAnalyzerResult faceAnalyzerResult = _faceAnalyzer.Analyze(source, faceWorkArea);
        MaskWarpInput input = faceAnalyzerResult.ToMaskWarpInput(width, height);
        MaskPlane empty = MaskPlane.Empty(width, height);

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

        MaskPlane eyeMask = parsingEyeMask;
        MaskPlane eyebrowMask = parsingEyebrowMask;
        MaskPlane lipMask = parsingLipMask;
        MaskPlane innerMouthMask = parsingMasks?.InnerMouthMask ?? empty;
        MaskPlane hairMask = parsingMasks?.HairMask ?? empty;
        MaskPlane beardMask = parsingMasks?.BeardMask ?? empty;
        MaskPlane mustacheMask = parsingMasks?.MustacheMask ?? empty;
        MaskPlane glassesMask = parsingMasks?.GlassesMask ?? empty;
        MaskPlane skinMask = parsingMasks?.SkinMask ?? empty;
        MaskPlane nostrilMask = empty;
        MaskPlane hardProtectMask = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
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
            "standard_dummy_masks_removed",
            "color_only_mask_mode"
        };
        warnings.AddRange(faceAnalyzerResult.DebugWarnings);
        if (parsingMasks is null)
        {
            warnings.Add("face_parsing_not_connected_no_dummy_fallback");
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
        return new PortraitMaskResult(analysis, masks, report, parsingMasks);
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
