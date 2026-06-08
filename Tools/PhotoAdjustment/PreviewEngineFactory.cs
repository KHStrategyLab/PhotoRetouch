namespace PhotoRetouch;

public static class PreviewEngineFactory
{
    public static IPreviewEngine Create()
    {
        return Create(PerformanceSettings.PreviewEngine);
    }

    public static IPreviewEngine Create(PreviewEngineMode mode)
    {
        return mode switch
        {
            PreviewEngineMode.Gpu => new CSharpPreviewEngine(),
            _ => new CSharpPreviewEngine()
        };
    }
}
