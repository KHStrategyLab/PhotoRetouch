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
        : this(new StandardMaskLoader(), new StandardAffineMaskWarper(), new OpenCvFaceAnalyzer(), new TemporaryFaceParsingDetector(), new NostrilDetector())
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

    public string MaskVersion => "standard_mask_warp_v1+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion + "+" + _nostrilDetector.DetectorVersion;

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
        MaskPlane skinMask = UseParsingSkinMask(parsingMasks?.SkinMask, warped.SkinMask, input.FaceBox);
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
            beardMask));
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
        MaskPlane noseSkinMask = MaskPlane.Subtract(warped.NoseMask, nostrilMask);
        MaskPlane conservativeRetouchBase = MaskPlane.Intersect(
            MaskPlane.Union(skinMask, noseSkinMask, neckMask),
            MaskPlane.Union(warped.SkinMask, warped.NoseMask, neckMask));
        MaskPlane retouchAllowMask = MaskPlane.Subtract(conservativeRetouchBase, hardProtectMask);
        MaskPlane softProtectMask = warped.SoftProtectMask;
        MaskPlane finalOverlayMask = MaskPlane.Subtract(
            MaskPlane.Union(retouchAllowMask, MaskPlane.Multiply(softProtectMask, 0.45)),
            hardProtectMask);

        FaceMaskSet masks = new(
            skinMask,
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            empty,
            warped.NoseMask,
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
            "affine_warp_only"
        };
        warnings.AddRange(faceAnalyzerResult.DebugWarnings);
        warnings.AddRange(standardMasks.DebugWarnings);
        if (parsingMasks is null)
        {
            warnings.Add("face_parsing_failed_fallback_to_warped_standard");
        }
        else
        {
            warnings.AddRange(parsingMasks.DebugWarnings);
            AddParsingAreaWarnings(warnings, parsingMasks, input.FaceBox);
        }
        warnings.AddRange(nostrilDetection.DebugWarnings);
        if (nostrilDetection.NostrilConfidence < 0.45)
        {
            warnings.Add("nose_lower_retouch_should_be_reduced");
        }

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
            warped.LipProtectMask,
            warped.NostrilProtectMask);
        MaskPlane noseSkinMask = MaskPlane.Subtract(warped.NoseMask, warped.NostrilProtectMask);
        MaskPlane retouchAllowMask = MaskPlane.Subtract(MaskPlane.Union(warped.SkinMask, noseSkinMask), hardProtectMask);
        MaskPlane finalOverlayMask = MaskPlane.Subtract(
            MaskPlane.Union(retouchAllowMask, MaskPlane.Multiply(warped.SoftProtectMask, 0.45)),
            hardProtectMask);

        return new FaceMaskSet(
            warped.SkinMask,
            warped.EyeProtectMask,
            warped.EyebrowProtectMask,
            warped.LipProtectMask,
            empty,
            empty,
            warped.NoseMask,
            noseSkinMask,
            warped.NostrilProtectMask,
            empty,
            empty,
            empty,
            empty,
            empty,
            hardProtectMask,
            warped.SoftProtectMask,
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

    private static MaskPlane UseParsingSkinMask(MaskPlane? parsingSkinMask, MaskPlane fallbackSkinMask, System.Windows.Int32Rect faceBox)
    {
        if (parsingSkinMask is null)
        {
            return fallbackSkinMask;
        }

        double average = parsingSkinMask.Average();
        double expectedFaceRatio = (double)faceBox.Width * faceBox.Height / (parsingSkinMask.Width * parsingSkinMask.Height);
        if (average < expectedFaceRatio * 0.12 || average > expectedFaceRatio * 1.20)
        {
            return fallbackSkinMask;
        }

        return MaskPlane.Intersect(parsingSkinMask, MaskPlane.Union(fallbackSkinMask, MaskPlane.Multiply(parsingSkinMask, 0.35)));
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
