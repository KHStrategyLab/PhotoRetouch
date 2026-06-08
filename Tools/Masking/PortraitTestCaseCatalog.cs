using System.IO;
using System.Text.Json;

namespace PhotoRetouch;

public static class PortraitTestCaseCatalog
{
    public static readonly string DefaultRoot = Path.Combine("test_assets", "portraits");
    public static readonly string DefaultManifestPath = Path.Combine(DefaultRoot, "portrait_test_cases.json");
    public static readonly string OriginalDirectory = Path.Combine(DefaultRoot, "original");
    public static readonly string StageOutputDirectory = Path.Combine(DefaultRoot, "stage_outputs");
    public static readonly string DebugOutputDirectory = Path.Combine(DefaultRoot, "debug_outputs");
    public static readonly string ReportDirectory = Path.Combine(DefaultRoot, "reports");

    public static IReadOnlyList<string> RequiredMinimumTestIds { get; } =
    [
        "HP_NOSTRIL_01",
        "HP_EYEBROW_01",
        "HP_LIP_EDGE_01",
        "HP_GLASSES_01",
        "HP_BEARD_01",
        "WR_GLABELLA_01",
        "WR_NASOLABIAL_01",
        "SK_BLEMISH_01",
        "TN_RED_DULL_01",
        "HP_HAIR_FACE_01"
    ];

    public static void EnsureLayout(string rootDirectory)
    {
        Directory.CreateDirectory(Path.Combine(rootDirectory, OriginalDirectory));
        Directory.CreateDirectory(Path.Combine(rootDirectory, StageOutputDirectory));
        Directory.CreateDirectory(Path.Combine(rootDirectory, DebugOutputDirectory));
        Directory.CreateDirectory(Path.Combine(rootDirectory, ReportDirectory));
    }

    public static IReadOnlyList<PortraitTestCase> Load(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new FileNotFoundException("Portrait test manifest was not found.", manifestPath);
        }

        string json = File.ReadAllText(manifestPath, System.Text.Encoding.UTF8);
        return JsonSerializer.Deserialize<List<PortraitTestCase>>(json) ?? [];
    }

    public static PortraitTestCaseValidationReport Validate(string rootDirectory, IReadOnlyList<PortraitTestCase> testCases)
    {
        HashSet<string> ids = testCases.Select(testCase => testCase.TestId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        List<string> missingRequiredIds = RequiredMinimumTestIds
            .Where(requiredId => !ids.Contains(requiredId))
            .ToList();
        List<string> missingOriginalFiles = testCases
            .Where(testCase => !string.IsNullOrWhiteSpace(testCase.FileName))
            .Select(testCase => Path.Combine(rootDirectory, OriginalDirectory, testCase.FileName))
            .Where(path => !File.Exists(path))
            .ToList();
        List<string> emptyFileNameCases = testCases
            .Where(testCase => string.IsNullOrWhiteSpace(testCase.FileName))
            .Select(testCase => testCase.TestId)
            .ToList();

        return new PortraitTestCaseValidationReport(
            testCases.Count,
            missingRequiredIds,
            missingOriginalFiles,
            emptyFileNameCases);
    }
}

public sealed record PortraitTestCase(
    string TestId,
    string FileName,
    string AgeGroup,
    string GenderOptional,
    IReadOnlyList<string> SkinConditionTags,
    IReadOnlyList<string> FaceFeatureTags,
    IReadOnlyList<string> LightingTags,
    IReadOnlyList<string> AccessoryTags,
    IReadOnlyList<string> HardProtectTargets,
    IReadOnlyList<string> MainExpectedRisks,
    IReadOnlyList<string> RequiredDebugMasks,
    string Notes);

public sealed record PortraitTestCaseValidationReport(
    int TestCaseCount,
    IReadOnlyList<string> MissingRequiredIds,
    IReadOnlyList<string> MissingOriginalFiles,
    IReadOnlyList<string> EmptyFileNameCases)
{
    public bool IsReadyForRun => MissingRequiredIds.Count == 0 &&
                                 MissingOriginalFiles.Count == 0 &&
                                 EmptyFileNameCases.Count == 0;
}

