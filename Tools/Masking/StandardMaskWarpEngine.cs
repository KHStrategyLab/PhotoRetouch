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

    public string MaskVersion => "color_mask_only_v1+color_mask_no_eyebrow_protect_subtract_v1+internal_soft_masks_no_channel_cutout_v1+anchor_template_masks_v16+eye_corner_roles+pupil_circle_roles+eyebrow_analyzer_module_v1+brow_30pt_closed_free_polygon_shape_v1+brow_orbital_arc_roi_wider_lower_to_eye_v1+brow_evidence_only_no_brush_cover_v1+brow_no_anchor_draw_when_absent_v1+brow_eye_anchor_roi_pixel_evidence_v3+brow_skin_average_boundary_free_polygon_v1+brow_presnap_boundary_moves_3d_line_v1+brow_eye_distance_ratio_guard_v1+nose_surface_masks_bridge_tip_wings_base_v1+nostril_anchor_ellipse_no_png_v1+mouth_shared_corner_double_almond_loop_v1+lip_anchor_primary_no_png_dummy_v1+lip_surface_loop_soft_fill_v1+lip_no_edit_not_hardprotect_overlay_v1+lip_phase_two_long_surface_planes_v1+mouth_nose_proximity_guard_v1+chin_jawline_roles+nose_tip_nostril_equilateral+face_outline_chin_eye_nose_distance_limit_v1+neck_restored+face_outline_60_eye_nose_band_component_position_snap+anchormesh_final_mask_policy_v1+no_ear_features+no_standard_dummy+nostril_standalone_mask_removed_v1+" + _faceAnalyzer.AnalyzerVersion + "+" + _parsingDetector.DetectorVersion;

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
        MaskPlane lipMask = UseAnchorPrimaryMask(anchorMasks.LipMask, parsingLipMask);
        MaskPlane innerMouthMask = UseAnchorPrimaryMask(anchorMasks.InnerMouthMask, parsingMasks?.InnerMouthMask ?? empty);
        MaskPlane hairMask = parsingMasks?.HairMask ?? empty;
        MaskPlane beardMask = parsingMasks?.BeardMask ?? empty;
        MaskPlane mustacheMask = parsingMasks?.MustacheMask ?? empty;
        MaskPlane glassesMask = parsingMasks?.GlassesMask ?? empty;
        MaskPlane nostrilExclusionMask = anchorMasks.NostrilMask;
        MaskPlane nostrilMask = empty;
        MaskPlane lipNoEditMask = MaskPlane.Union(lipMask, innerMouthMask);
        MaskPlane hardProtectMask = MaskPlane.Union(
            eyeMask,
            eyebrowMask,
            hairMask,
            beardMask,
            mustacheMask,
            glassesMask);
        MaskPlane noseMask = anchorMasks.NoseMask;
        MaskPlane noEditProtectionMask = MaskPlane.Union(hardProtectMask, lipNoEditMask, nostrilExclusionMask);
        MaskPlane noseSkinMask = MaskPlane.Subtract(anchorMasks.NoseSkinMask, noEditProtectionMask);
        MaskPlane softProtectMask = MaskPlane.Subtract(
            MaskPlane.Union(anchorMasks.SoftProtectMask, noseSkinMask),
            noEditProtectionMask);

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
            noEditProtectionMask,
            softProtectMask,
            empty,
            noEditProtectionMask.Clone());

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
        MaskPlane internalNoEditMask = noEditProtectionMask;
        skinMask = MaskPlane.Subtract(skinMask, internalNoEditMask);
        MaskPlane retouchAllowMask = MaskPlane.Subtract(
            MaskPlane.Union(skinMask, noseSkinMask),
            internalNoEditMask);
        MaskPlane finalOverlayMask = MaskPlane.Subtract(
            MaskPlane.Union(retouchAllowMask, MaskPlane.Multiply(softProtectMask, 0.45)),
            internalNoEditMask);

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
        warnings.Add("lip_mask_anchor_primary_no_png_dummy");
        warnings.Add("lip_no_edit_mask_removed_from_hardprotect_overlay");
        warnings.Add("standalone_nose_hole_mask_removed_internal_exclusion_only");

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
            AnchorMeshFeatureMaskSet masks = AnchorMeshFeatureMaskBuilder.Build(width, height, anchorMesh, source);
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

        TryAddEyebrowBoundaryContours(source, faceAnalyzerResult, width, height, provider, warnings);

        return provider;
    }

    private static void TryAddEyebrowBoundaryContours(
        BitmapSource source,
        FaceAnalyzerResult faceAnalyzerResult,
        int width,
        int height,
        FeatureMaskContourProvider provider,
        List<string> warnings)
    {
        try
        {
            double eyeDistance = Distance(faceAnalyzerResult.LeftEyeCenter, faceAnalyzerResult.RightEyeCenter);
            if (eyeDistance <= 2)
            {
                warnings.Add("anchor_mesh_brow_boundary_snap_skipped_eye_distance");
                return;
            }

            AnchorMeshFeature leftEye = CreateEyeAnchorFeature("LeftEye", faceAnalyzerResult.LeftEyeCenter, faceAnalyzerResult.FaceAngle, eyeDistance);
            AnchorMeshFeature rightEye = CreateEyeAnchorFeature("RightEye", faceAnalyzerResult.RightEyeCenter, faceAnalyzerResult.FaceAngle, eyeDistance);
            EyebrowAnalysisResult brow = EyebrowAnalyzer.Analyze(new EyebrowAnalyzerInput(
                width,
                height,
                source,
                leftEye,
                rightEye,
                null,
                null,
                null,
                null,
                faceAnalyzerResult.FaceBox.Width,
                faceAnalyzerResult.FaceBox.Height,
                faceAnalyzerResult.FaceBox.X + faceAnalyzerResult.FaceBox.Width * 0.5,
                faceAnalyzerResult.FaceAngle,
                faceAnalyzerResult.Confidence));

            bool added = false;
            if (brow.LeftEyebrowMask.Average() > 0.000015)
            {
                provider.WithMask("LeftBrow", brow.LeftEyebrowMask);
                added = true;
            }

            if (brow.RightEyebrowMask.Average() > 0.000015)
            {
                provider.WithMask("RightBrow", brow.RightEyebrowMask);
                added = true;
            }

            warnings.Add(added
                ? "anchor_mesh_brow_skin_boundary_snap_enabled"
                : "anchor_mesh_brow_skin_boundary_snap_empty");
            warnings.AddRange(brow.DebugOverlayData.Select(warning => "pre_snap_" + warning));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            warnings.Add("anchor_mesh_brow_skin_boundary_snap_failed:" + ex.GetType().Name);
        }
    }

    private static AnchorMeshFeature CreateEyeAnchorFeature(string name, WpfPoint center, double angle, double eyeDistance)
    {
        double width = Math.Max(8.0, eyeDistance * 0.42);
        double height = Math.Max(4.0, eyeDistance * 0.16);
        double axisX = Math.Cos(angle);
        double axisY = Math.Sin(angle);
        double upX = Math.Sin(angle);
        double upY = -Math.Cos(angle);
        AnchorMeshFeature feature = new()
        {
            Name = name,
            IsClosedLoop = true,
            CenterX = (float)center.X,
            CenterY = (float)center.Y,
            Width = (float)width,
            Height = (float)height,
            AngleRad = (float)angle,
            Confidence = 0.70f,
            SnapMode = "AnalyzerAnchor"
        };
        AddEyeAnchorPoint(feature, 0, center.X - axisX * width * 0.5, center.Y - axisY * width * 0.5, "InnerCorner");
        AddEyeAnchorPoint(feature, 1, center.X + axisX * width * 0.5, center.Y + axisY * width * 0.5, "OuterCorner");
        AddEyeAnchorPoint(feature, 2, center.X + upX * height * 0.5, center.Y + upY * height * 0.5, "UpperLidCenter");
        AddEyeAnchorPoint(feature, 3, center.X - upX * height * 0.5, center.Y - upY * height * 0.5, "LowerLidCenter");
        return feature;
    }

    private static void AddEyeAnchorPoint(AnchorMeshFeature feature, int index, double x, double y, string role)
    {
        feature.Points.Add(new AnchorMeshPoint
        {
            Name = feature.Name + "_Analyzer_" + index,
            FeatureName = feature.Name,
            Role = feature.Name + role,
            Index = index,
            ImageX = (float)x,
            ImageY = (float)y,
            SnappedX = (float)x,
            SnappedY = (float)y,
            Confidence = 0.70f,
            SnapWeight = 0.0f,
            Source = "AnalyzerAnchor"
        });
    }

    private static double Distance(WpfPoint left, WpfPoint right)
    {
        double dx = left.X - right.X;
        double dy = left.Y - right.Y;
        return Math.Sqrt(dx * dx + dy * dy);
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

    private static MaskPlane UseAnchorPrimaryMask(MaskPlane anchorMask, MaskPlane fallbackMask)
    {
        MaskPlane.EnsureSameSize(anchorMask, fallbackMask);
        return anchorMask.Average() > 0.00001
            ? anchorMask
            : fallbackMask;
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
