using System.Windows.Media.Imaging;
using PhotoRetouch.AnchorMesh;
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

    public string MaskVersion => "color_mask_only_v1+anchor_template_masks_v16+eye_corner_roles+pupil_circle_roles+brow_endpoint_roles+brow_peak_roles+chin_jawline_roles+nose_tip_nostril_equilateral+neck_restored+face_outline_60_eye_center_color_boundary_snap+no_ear_features+no_standard_dummy+nostril_disabled+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion;

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

        List<string> warnings = new()
        {
            "standard_dummy_masks_removed",
            "color_only_mask_mode"
        };
        warnings.AddRange(faceAnalyzerResult.DebugWarnings);

        AnchorMeshFeatureMaskSet anchorMasks = BuildAnchorFeatureMasks(source, faceAnalyzerResult, width, height, landmarks, warnings);

        MaskPlane eyeMask = MaskPlane.Union(parsingEyeMask, anchorMasks.EyeMask);
        MaskPlane eyebrowMask = MaskPlane.Union(parsingEyebrowMask, anchorMasks.EyebrowMask);
        MaskPlane lipMask = MaskPlane.Union(parsingLipMask, anchorMasks.LipMask);
        MaskPlane innerMouthMask = MaskPlane.Union(parsingMasks?.InnerMouthMask ?? empty, anchorMasks.InnerMouthMask);
        MaskPlane hairMask = parsingMasks?.HairMask ?? empty;
        MaskPlane beardMask = parsingMasks?.BeardMask ?? empty;
        MaskPlane mustacheMask = parsingMasks?.MustacheMask ?? empty;
        MaskPlane glassesMask = parsingMasks?.GlassesMask ?? empty;
        MaskPlane nostrilMask = anchorMasks.NostrilMask;
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
        MaskPlane noseMask = anchorMasks.NoseMask;
        MaskPlane noseSkinMask = MaskPlane.Subtract(anchorMasks.NoseSkinMask, hardProtectMask);
        MaskPlane softProtectMask = MaskPlane.Subtract(
            MaskPlane.Union(anchorMasks.SoftProtectMask, noseSkinMask),
            hardProtectMask);

        FaceMaskSet protectionOnlyMasks = new(
            empty,
            eyeMask,
            eyebrowMask,
            lipMask,
            innerMouthMask,
            empty,
            noseMask,
            noseSkinMask,
            nostrilMask,
            empty,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask,
            hardProtectMask,
            softProtectMask,
            empty,
            hardProtectMask.Clone());

        FaceAnalysisResult analysis = new(
            input.FaceBox,
            landmarks,
            null,
            input.FaceAngle,
            faceAnalyzerResult.Confidence,
            faceAnalyzerResult.Confidence,
            parsingMasks?.ParsingConfidence ?? 0,
            warnings);

        MaskPlane skinMask = parsingMasks?.SkinMask ?? AverageFaceColorMaskBuilder.Build(
            source,
            analysis,
            protectionOnlyMasks,
            1.0,
            CancellationToken.None,
            null).ColorDifferenceMask;
        skinMask = MaskPlane.Subtract(skinMask, hardProtectMask);
        MaskPlane retouchAllowMask = MaskPlane.Subtract(
            MaskPlane.Union(skinMask, noseSkinMask),
            hardProtectMask);
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
            noseMask,
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

        if (parsingMasks is null)
        {
            warnings.Add("face_parsing_not_connected_no_dummy_fallback");
        }
        else
        {
            warnings.AddRange(parsingMasks.DebugWarnings);
            AddParsingAreaWarnings(warnings, parsingMasks, input.FaceBox);
        }
        warnings.Add("nostril_detector_replaced_by_anchor_mesh_observation");

        MaskQualityReport report = MaskQualityReport.FromMasks(analysis, masks);
        return new PortraitMaskResult(analysis, masks, report, parsingMasks);
    }

    private static AnchorMeshFeatureMaskSet BuildAnchorFeatureMasks(
        BitmapSource source,
        FaceAnalyzerResult faceAnalyzerResult,
        int width,
        int height,
        IReadOnlyDictionary<string, WpfPoint> landmarks,
        List<string> warnings)
    {
        try
        {
            FeatureMaskContourProvider contourProvider = BuildFaceOutlineColorContourProvider(source, faceAnalyzerResult, landmarks, width, height, warnings);
            AnchorMeshResult anchorMesh = new KAnchorMeshEngine().BuildFromAnalyzerResult(source, faceAnalyzerResult, contourProvider);
            warnings.Add("anchor_mesh_feature_masks_enabled:" + anchorMesh.Stage);
            warnings.AddRange(anchorMesh.Warnings.Select(warning => "anchor_mesh_" + warning));
            AnchorMeshFeatureMaskSet masks = AnchorMeshFeatureMaskBuilder.Build(width, height, anchorMesh);
            warnings.AddRange(masks.DebugWarnings);
            return masks;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add("anchor_mesh_feature_masks_failed:" + ex.GetType().Name);
            return AnchorMeshFeatureMaskBuilder.Build(width, height, null);
        }
    }

    private static FeatureMaskContourProvider BuildFaceOutlineColorContourProvider(
        BitmapSource source,
        FaceAnalyzerResult faceAnalyzerResult,
        IReadOnlyDictionary<string, WpfPoint> landmarks,
        int width,
        int height,
        List<string> warnings)
    {
        FeatureMaskContourProvider provider = new();
        try
        {
            FaceAnalysisResult analysis = new(
                faceAnalyzerResult.FaceBox,
                landmarks,
                null,
                faceAnalyzerResult.FaceAngle,
                faceAnalyzerResult.Confidence,
                faceAnalyzerResult.Confidence,
                0,
                faceAnalyzerResult.DebugWarnings);
            AverageFaceColorMaskResult colorMask = AverageFaceColorMaskBuilder.Build(
                source,
                analysis,
                null,
                1.0,
                CancellationToken.None,
                null);
            if (colorMask.ColorDifferenceMask.Width == width &&
                colorMask.ColorDifferenceMask.Height == height &&
                colorMask.ColorDifferenceMask.Average() > 0.0001)
            {
                provider.WithMask("FaceOutline", colorMask.ColorDifferenceMask);
                warnings.Add("anchor_mesh_face_outline_color_boundary_snap_enabled");
            }
            else
            {
                warnings.Add("anchor_mesh_face_outline_color_boundary_snap_empty");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add("anchor_mesh_face_outline_color_boundary_snap_failed:" + ex.GetType().Name);
        }

        return provider;
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
