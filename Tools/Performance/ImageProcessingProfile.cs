using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PhotoRetouch;

public sealed record ImageProcessingProfile(
    int PreviewMaxWidth,
    int PreviewMaxHeight,
    bool ExportUseOriginalResolution,
    long MaxWorkingImagePixels,
    bool EnableDownscalePreview,
    bool EnableFullResolutionExport)
{
    public static ImageProcessingProfile Default { get; } = new(
        1600,
        1600,
        true,
        48_000_000,
        true,
        true);

    public int PreviewMaxLongSide => Math.Max(PreviewMaxWidth, PreviewMaxHeight);
}

public sealed record ImageProcessingDecision(
    int SourceWidth,
    int SourceHeight,
    int PreviewWidth,
    int PreviewHeight,
    double PreviewScale,
    bool UsesDownscalePreview,
    bool AllowsFullResolutionExport,
    bool MemoryWarning);

public static class HighResolutionProcessingPolicy
{
    public static ImageProcessingProfile CurrentProfile { get; set; } = ImageProcessingProfile.Default;

    public static ImageProcessingDecision Decide(BitmapSource source, int? visibleMaxLongSide = null)
    {
        int sourceWidth = Math.Max(source.PixelWidth, 1);
        int sourceHeight = Math.Max(source.PixelHeight, 1);
        int sourceLongSide = Math.Max(sourceWidth, sourceHeight);
        int maxLongSide = CurrentProfile.EnableDownscalePreview
            ? Math.Min(
                CurrentProfile.PreviewMaxLongSide,
                visibleMaxLongSide is null ? CurrentProfile.PreviewMaxLongSide : Math.Clamp(visibleMaxLongSide.Value, 320, CurrentProfile.PreviewMaxLongSide))
            : sourceLongSide;
        double scale = sourceLongSide <= maxLongSide ? 1 : (double)maxLongSide / sourceLongSide;
        int previewWidth = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int previewHeight = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        long sourcePixels = (long)sourceWidth * sourceHeight;
        return new ImageProcessingDecision(
            sourceWidth,
            sourceHeight,
            previewWidth,
            previewHeight,
            scale,
            scale < 0.999,
            CurrentProfile.EnableFullResolutionExport && CurrentProfile.ExportUseOriginalResolution,
            sourcePixels > CurrentProfile.MaxWorkingImagePixels);
    }

    public static BitmapSource CreatePreviewSource(BitmapSource source, int? visibleMaxLongSide = null)
    {
        ImageProcessingDecision decision = Decide(source, visibleMaxLongSide);
        if (!decision.UsesDownscalePreview)
        {
            return source;
        }

        TransformedBitmap preview = new(source, new ScaleTransform(decision.PreviewScale, decision.PreviewScale));
        preview.Freeze();
        return preview;
    }
}

public sealed class PipelinePerformanceTimer
{
    private readonly Stopwatch _totalStopwatch = Stopwatch.StartNew();
    private readonly Dictionary<string, double> _elapsedByStep = new(StringComparer.OrdinalIgnoreCase);

    public IDisposable Measure(string stepName)
    {
        return new StepScope(this, stepName);
    }

    public PipelinePerformanceReport CreateReport(
        string imageId,
        int width,
        int height,
        bool snapshotMaskReused,
        bool cacheHit,
        IReadOnlyList<string>? warnings = null)
    {
        _totalStopwatch.Stop();
        string slowStepName = _elapsedByStep.Count == 0
            ? string.Empty
            : _elapsedByStep.MaxBy(pair => pair.Value).Key;
        return new PipelinePerformanceReport(
            imageId,
            width,
            height,
            _totalStopwatch.Elapsed.TotalMilliseconds,
            snapshotMaskReused,
            cacheHit,
            slowStepName,
            _elapsedByStep,
            warnings ?? Array.Empty<string>());
    }

    private void Add(string stepName, double elapsedMilliseconds)
    {
        _elapsedByStep[stepName] = _elapsedByStep.TryGetValue(stepName, out double existing)
            ? existing + elapsedMilliseconds
            : elapsedMilliseconds;
    }

    private sealed class StepScope : IDisposable
    {
        private readonly PipelinePerformanceTimer _owner;
        private readonly string _stepName;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private bool _disposed;

        public StepScope(PipelinePerformanceTimer owner, string stepName)
        {
            _owner = owner;
            _stepName = stepName;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stopwatch.Stop();
            _owner.Add(_stepName, _stopwatch.Elapsed.TotalMilliseconds);
        }
    }
}

public sealed record PipelinePerformanceReport(
    string ImageId,
    int ImageWidth,
    int ImageHeight,
    double TotalPipelineTimeMs,
    bool SnapshotMaskReused,
    bool CacheHit,
    string SlowStepName,
    IReadOnlyDictionary<string, double> StepTimesMs,
    IReadOnlyList<string> Warnings)
{
    public long CurrentImagePixels => (long)ImageWidth * ImageHeight;
}
