using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: LockedChestResearchArtifactSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
    return 2;
}

string repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
string retail = args.Length == 5
    ? Path.GetFullPath(args[0])
    : Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin");
string retailCue = args.Length == 5
    ? Path.GetFullPath(args[1])
    : Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue");
string analysis = args.Length == 5
    ? Path.GetFullPath(args[2])
    : Path.Combine(repositoryRoot, "spyro-wad-analysis.json");
string catalogRoot = args.Length == 5
    ? Path.GetFullPath(args[3])
    : repositoryRoot;
string outputDirectory = args.Length == 5
    ? Path.GetFullPath(args[4])
    : Path.Combine(repositoryRoot, "_local", "research", "editor-locked-chest-artifact-smoke");
string outputPrefix = Path.Combine(outputDirectory, "Town-Square-Key-Locked-Chest-RESEARCH-ONLY");

Expect(!NativeLockedChestResearchArtifactWriter.CanWrite("Stone Hill"), "Stone Hill must never be exposed by the disposable artifact writer.");
Expect(NativeLockedChestResearchArtifactWriter.CanWrite("Town Square"), "Town Square's checked disposable writer must be available.");
await ExpectThrowsAsync<NotSupportedException>(
    () => NativeLockedChestResearchArtifactWriter.WriteAsync(new(
        DestinationLevelKey: "Stone Hill",
        SourceImagePath: retail,
        SourceCuePath: retailCue,
        OutputPrefix: Path.Combine(outputDirectory, "must-not-write-stone-hill"),
        WadAnalysisPath: analysis,
        LevelCatalogRootPath: catalogRoot)),
    "Stone Hill artifact request was not rejected.");

NativeLockedChestResearchArtifactResult result = await NativeLockedChestResearchArtifactWriter.WriteAsync(new(
    DestinationLevelKey: "Town Square",
    SourceImagePath: retail,
    SourceCuePath: retailCue,
    OutputPrefix: outputPrefix,
    WadAnalysisPath: analysis,
    LevelCatalogRootPath: catalogRoot,
    EnableFastEntry: true));

Expect(result.Verified && result.Candidate.Verified, "The generic writer did not preserve guarded candidate verification.");
foreach (string path in new[]
{
    result.OutputImagePath,
    result.OutputCuePath,
    result.OutputPlanPath,
    result.OutputChecklistPath
})
{
    Expect(File.Exists(path), $"Missing generic research artifact: {path}");
}

string cue = File.ReadAllText(result.OutputCuePath);
Expect(
    cue.Contains($"FILE \"{Path.GetFileName(result.OutputImagePath)}\" BINARY", StringComparison.Ordinal),
    "The disposable CUE does not point at its generated BIN.");
string checklist = File.ReadAllText(result.OutputChecklistPath);
Expect(checklist.Contains(NativeLockedChestResearchArtifactWriter.ResearchOnlyNotice, StringComparison.Ordinal), "The checklist lost its research-only warning.");
Expect(checklist.Contains("not part of normal Add/Create BIN", StringComparison.Ordinal), "The checklist does not distinguish this artifact from normal Create BIN.");
Expect(checklist.Contains("Do not treat a successful boot as completion", StringComparison.Ordinal), "The checklist lost the full DuckStation runtime gate.");

NativeLockedChestResearchCandidatePlan? plan = JsonSerializer.Deserialize<NativeLockedChestResearchCandidatePlan>(
    File.ReadAllText(result.OutputPlanPath));
Expect(plan != null && plan.DestinationLevelKey == "townsquare", "The generic plan does not identify Town Square.");
Expect(plan!.OutputImageSha256 == result.Candidate.Plan.OutputImageSha256, "The generic plan digest diverged from the guarded candidate.");

Console.WriteLine("Locked Chest editor research-artifact smoke passed.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN SHA-256: {result.Candidate.Plan.OutputImageSha256}");
Console.WriteLine("- complete BIN/CUE/plan/checklist set written through the generic Core writer");
Console.WriteLine("- research-only warning retained and Stone Hill rejected before any write");
return 0;

static void Expect(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static async Task ExpectThrowsAsync<T>(Func<Task> action, string message)
    where T : Exception
{
    try
    {
        await action();
    }
    catch (T)
    {
        return;
    }

    throw new InvalidOperationException(message);
}
