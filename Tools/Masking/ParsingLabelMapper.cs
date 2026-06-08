namespace PhotoRetouch;

public enum StandardParsingLabel
{
    Unknown,
    Skin,
    LeftEye,
    RightEye,
    LeftEyebrow,
    RightEyebrow,
    UpperLip,
    LowerLip,
    InnerMouth,
    Hair,
    Neck,
    Glasses,
    Beard,
    Mustache,
    Cloth,
    Background
}

public static class ParsingLabelMapper
{
    private static readonly Dictionary<string, StandardParsingLabel> Labels = new(StringComparer.OrdinalIgnoreCase)
    {
        ["skin"] = StandardParsingLabel.Skin,
        ["face"] = StandardParsingLabel.Skin,
        ["left_eye"] = StandardParsingLabel.LeftEye,
        ["l_eye"] = StandardParsingLabel.LeftEye,
        ["right_eye"] = StandardParsingLabel.RightEye,
        ["r_eye"] = StandardParsingLabel.RightEye,
        ["left_eyebrow"] = StandardParsingLabel.LeftEyebrow,
        ["l_brow"] = StandardParsingLabel.LeftEyebrow,
        ["right_eyebrow"] = StandardParsingLabel.RightEyebrow,
        ["r_brow"] = StandardParsingLabel.RightEyebrow,
        ["upper_lip"] = StandardParsingLabel.UpperLip,
        ["u_lip"] = StandardParsingLabel.UpperLip,
        ["lower_lip"] = StandardParsingLabel.LowerLip,
        ["l_lip"] = StandardParsingLabel.LowerLip,
        ["inner_mouth"] = StandardParsingLabel.InnerMouth,
        ["mouth"] = StandardParsingLabel.InnerMouth,
        ["hair"] = StandardParsingLabel.Hair,
        ["neck"] = StandardParsingLabel.Neck,
        ["glasses"] = StandardParsingLabel.Glasses,
        ["eyeglass"] = StandardParsingLabel.Glasses,
        ["beard"] = StandardParsingLabel.Beard,
        ["mustache"] = StandardParsingLabel.Mustache,
        ["moustache"] = StandardParsingLabel.Mustache,
        ["cloth"] = StandardParsingLabel.Cloth,
        ["clothes"] = StandardParsingLabel.Cloth,
        ["background"] = StandardParsingLabel.Background,
        ["bg"] = StandardParsingLabel.Background
    };

    public static StandardParsingLabel ToStandardLabel(string? modelLabel)
    {
        if (string.IsNullOrWhiteSpace(modelLabel))
        {
            return StandardParsingLabel.Unknown;
        }

        return Labels.TryGetValue(modelLabel.Trim(), out StandardParsingLabel label)
            ? label
            : StandardParsingLabel.Unknown;
    }
}
