using System.Windows.Media.Imaging;

namespace PhotoRetouch.AnchorMesh;

public sealed class KAnchorMeshEngine
{
    private readonly AnchorMeshTemplateFactory _templateFactory;
    private readonly AnchorMeshAligner _aligner;
    private readonly AnchorMeshSoftSnapper _snapper;

    public KAnchorMeshEngine()
        : this(new AnchorMeshTemplateFactory(), new AnchorMeshAligner(), new AnchorMeshSoftSnapper())
    {
    }

    public KAnchorMeshEngine(
        AnchorMeshTemplateFactory templateFactory,
        AnchorMeshAligner aligner,
        AnchorMeshSoftSnapper snapper)
    {
        _templateFactory = templateFactory;
        _aligner = aligner;
        _snapper = snapper;
    }

    public AnchorMeshResult Build(
        BitmapSource inputImage,
        YuNetAnchorSet yunetAnchors,
        FeatureMaskContourProvider? maskContours = null)
    {
        AnchorMeshFeatureSet template = _templateFactory.CreateDefaultTemplate();
        AnchorMeshFeatureSet aligned = _aligner.Align(template, yunetAnchors);
        AnchorMeshFeatureSet final = _snapper.Snap(aligned, maskContours);

        AnchorMeshResult result = new()
        {
            YuNetAnchors = yunetAnchors,
            TemplateFeatures = template,
            YuNetAlignedFeatures = aligned,
            Features = final,
            IsValid = yunetAnchors.EyeDistance > 1,
            Confidence = yunetAnchors.Score,
            Stage = maskContours is null ? "YuNetAligned" : "MaskSnapped"
        };

        if (inputImage.PixelWidth <= 0 || inputImage.PixelHeight <= 0)
        {
            result.IsValid = false;
            result.Warnings.Add("Input image is empty.");
        }

        if (yunetAnchors.EyeDistance <= 1)
        {
            result.Warnings.Add("YuNet eye distance is too small for anchor mesh alignment.");
        }

        return result;
    }

    public AnchorMeshResult BuildFromAnalyzerResult(
        BitmapSource inputImage,
        FaceAnalyzerResult analyzerResult,
        FeatureMaskContourProvider? maskContours = null)
    {
        YuNetAnchorSet anchors = YuNetAnchorMapper.FromFaceAnalyzerResult(analyzerResult);
        return Build(inputImage, anchors, maskContours);
    }
}
