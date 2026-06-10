using System.Windows.Media.Imaging;

namespace PhotoRetouch.AnchorMesh;

public sealed class KAnchorMeshEngine
{
    private readonly AnchorMeshTemplateFactory _templateFactory;
    private readonly AnchorMeshAligner _aligner;
    private readonly AnchorMeshSoftSnapper _snapper;
    private readonly KAnchorMeasureEngine _measureEngine;
    private readonly KAnchorPoseEngine _poseEngine;
    private readonly FaceFitBoxCalculator _fitBoxCalculator;
    private readonly AnchorOvalProfileAnalyzer _ovalProfileAnalyzer;

    public KAnchorMeshEngine()
        : this(new AnchorMeshTemplateFactory(), new AnchorMeshAligner(), new AnchorMeshSoftSnapper(), new KAnchorMeasureEngine(), new KAnchorPoseEngine(), new FaceFitBoxCalculator(), new AnchorOvalProfileAnalyzer())
    {
    }

    public KAnchorMeshEngine(
        AnchorMeshTemplateFactory templateFactory,
        AnchorMeshAligner aligner,
        AnchorMeshSoftSnapper snapper,
        KAnchorMeasureEngine measureEngine,
        KAnchorPoseEngine poseEngine,
        FaceFitBoxCalculator fitBoxCalculator,
        AnchorOvalProfileAnalyzer ovalProfileAnalyzer)
    {
        _templateFactory = templateFactory;
        _aligner = aligner;
        _snapper = snapper;
        _measureEngine = measureEngine;
        _poseEngine = poseEngine;
        _fitBoxCalculator = fitBoxCalculator;
        _ovalProfileAnalyzer = ovalProfileAnalyzer;
    }

    public AnchorMeshResult Build(
        BitmapSource inputImage,
        YuNetAnchorSet yunetAnchors,
        FeatureMaskContourProvider? maskContours = null,
        MaskPlane? faceMeasureMask = null)
    {
        ArgumentNullException.ThrowIfNull(inputImage);
        AnchorMeshResult result = Build(yunetAnchors, maskContours, faceMeasureMask);
        if (inputImage.PixelWidth <= 0 || inputImage.PixelHeight <= 0)
        {
            result.IsValid = false;
            result.Warnings.Add("Input image is empty.");
        }

        return result;
    }

    public AnchorMeshResult Build(
        YuNetAnchorSet yunetAnchors,
        FeatureMaskContourProvider? maskContours = null,
        MaskPlane? faceMeasureMask = null)
    {
        AnchorFaceMeasurements measurements = _measureEngine.Measure(yunetAnchors, faceMeasureMask);
        FaceFitBox fitBox = _fitBoxCalculator.Calculate(yunetAnchors, measurements, faceMeasureMask);
        AnchorMeshFeatureSet template = _templateFactory.CreateDefaultTemplate();
        AnchorPoseInfo pose = _poseEngine.EstimatePose(yunetAnchors, measurements);
        AnchorMeshFeatureSet posed = _poseEngine.ApplyPose(template, pose);
        AnchorMeshFeatureSet aligned = _aligner.Align(posed, yunetAnchors, fitBox);
        AnchorMeshFeatureSet final = _snapper.Snap(aligned, maskContours);
        _aligner.LockPrimaryFeatureCenters(final, yunetAnchors);
        bool chinLimited = _aligner.ConstrainFaceOutlineChinToNostrilCompassLimit(final, yunetAnchors);

        AnchorMeshResult result = new()
        {
            YuNetAnchors = yunetAnchors,
            TemplateFeatures = posed,
            YuNetAlignedFeatures = aligned,
            Features = final,
            Measurements = measurements,
            OvalProfile = _ovalProfileAnalyzer.Analyze(final.FaceOutline),
            FitBox = fitBox,
            Pose = pose,
            MorphGroups = AnchorMorphGroupBuilder.BuildDefaultGroups(),
            WarpHandleGroups = AnchorWarpHandleGroupBuilder.BuildDefaultGroups(final),
            IsValid = yunetAnchors.EyeDistance > 1,
            Confidence = yunetAnchors.Score,
            Stage = maskContours is null ? "YuNetAligned" : "MaskSnapped"
        };
        result.TopologyEdges.AddRange(AnchorMeshTopologyBuilder.BuildDefaultEdges());
        result.Warnings.Add(
            "topology_edges_available:" +
            "anchor=" + result.TopologyEdges.Count(edge => edge.Kind == AnchorMeshEdgeKind.Anchor) + "," +
            "surface=" + result.TopologyEdges.Count(edge => edge.Kind == AnchorMeshEdgeKind.Surface) + "," +
            "protection=" + result.TopologyEdges.Count(edge => edge.Kind == AnchorMeshEdgeKind.Protection) + "," +
            "measurement=" + result.TopologyEdges.Count(edge => edge.Kind == AnchorMeshEdgeKind.Measurement));
        result.Warnings.Add("mouth_topology_shared_corner_double_almond_loop");

        if (yunetAnchors.EyeDistance <= 1)
        {
            result.Warnings.Add("YuNet eye distance is too small for anchor mesh alignment.");
        }

        result.Warnings.AddRange(measurements.Warnings);
        if (chinLimited)
        {
            result.Warnings.Add("face_outline_chin_limited_by_nostril_compass_radius");
        }

        return result;
    }

    public AnchorMeshResult BuildFromAnalyzerResult(
        BitmapSource inputImage,
        FaceAnalyzerResult analyzerResult,
        FeatureMaskContourProvider? maskContours = null,
        MaskPlane? faceMeasureMask = null)
    {
        YuNetAnchorSet anchors = YuNetAnchorMapper.FromFaceAnalyzerResult(analyzerResult);
        return Build(inputImage, anchors, maskContours, faceMeasureMask);
    }
}
