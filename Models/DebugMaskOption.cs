namespace PhotoRetouch;

public sealed class DebugMaskOption
{
    public DebugMaskOption(string id, string name)
    {
        Id = id;
        Name = name;
    }

    public string Id { get; }

    public string Name { get; }
}

