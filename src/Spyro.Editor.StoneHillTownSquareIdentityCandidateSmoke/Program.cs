using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string baseRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "town-square-complete-pair-candidate");
string basePrefix = Path.Combine(
    baseRoot,
    "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
string baseImage = basePrefix + ".bin";
string baseCue = basePrefix + ".cue";

if (!await IsExactBaseCandidateAsync(baseImage, baseCue))
{
    (string sourceImage, string sourceCue) = ResolveCleanSource(repositoryRoot, args);
    LevelCatalog catalog = LevelCatalog.Load(repositoryRoot);
    Directory.CreateDirectory(baseRoot);
    NativeLevelReplacementManifest manifest = await NativeLevelReplacementStore.StartStoneHillAsync(
        baseRoot,
        sourceImage,
        catalog);
    NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
        baseRoot,
        NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
        manifest,
        catalog);
    StoneHillTownSquareReplacementCandidateRequest baseRequest = new(
        manifest,
        intent,
        catalog,
        sourceImage,
        sourceCue,
        baseImage,
        baseCue);
    StoneHillTownSquareReplacementCandidateResult generatedBase =
        await StoneHillTownSquareReplacementCandidateComposer.ExportAsync(baseRequest);
    Require(
        generatedBase.OutputImageSha256 ==
            NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256,
        "The regenerated base candidate did not match the exact runtime-proven hash.");
}

string baseHashBefore = await HashFileAsync(baseImage);
string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "town-square-display-identity-candidate");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-complete-level-with-Town-Square-display-name-RUNTIME-CANDIDATE");
StoneHillTownSquareIdentityCandidateRequest request = new(
    baseImage,
    baseCue,
    outputPrefix + ".bin",
    outputPrefix + ".cue");

string invalidCue = Path.Combine(outputRoot, "invalid-base.cue");
try
{
    await File.WriteAllTextAsync(
        invalidCue,
        "FILE \"wrong.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
    await ExpectFailureAsync(
        () => StoneHillTownSquareIdentityCandidateComposer.BuildPlanAsync(
            request with { BaseCuePath = invalidCue }),
        "A CUE that did not reference the exact base BIN passed identity planning.");
}
finally
{
    if (File.Exists(invalidCue))
        File.Delete(invalidCue);
}

StoneHillTownSquareIdentityCandidatePlan plan =
    await StoneHillTownSquareIdentityCandidateComposer.BuildPlanAsync(request);
Require(
    plan.RecipeId == StoneHillTownSquareIdentityCandidateComposer.RecipeId &&
    plan.RecipeVersion == StoneHillTownSquareIdentityCandidateComposer.RecipeVersion &&
    plan.BaseProfileId == NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId &&
    plan.Evidence == NativeLevelReplacementEvidenceStatus.StaticBaselineOnly &&
    plan.BaseImageSha256 == NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256 &&
    plan.BaseExecutableSha256 == "558d4f5f0f7dd482b035d5f5793bfc6cf886d9cdd218562f4cedd4b1effbfab9" &&
    plan.OutputExecutableSha256 == "b17fd7679d586562ef325813b7c469aff58430f5200633f64b8e6900409e3304",
    "The identity plan was not bound to the exact pending-runtime recipe and proven base.");
Require(
    plan.NamePointerTableFileOffset == 0x5FFF0 &&
    plan.StoneHillNamePointerFileOffset == 0x5FFF4 &&
    plan.TownSquareNamePointerFileOffset == 0x5FFFC &&
    plan.StoneHillNamePointerBefore == "FC010180" &&
    plan.StoneHillNamePointerAfter == "E4010180",
    "The exact one-pointer display-identity contract changed.");
Require(
    plan.StoneHillTotals == new StoneHillTownSquareIdentityTotals(200, 4, 1) &&
    plan.TownSquareTotals == new StoneHillTownSquareIdentityTotals(200, 4, 1),
    "Stone Hill and Town Square no longer share the guarded 200/4/1 totals.");
Require(
    plan.Patches.Count == 1 &&
    plan.Patches[0].File == "SCUS_942.28" &&
    plan.Patches[0].Kind == "alias-stone-hill-level-name-to-town-square" &&
    plan.Patches[0].LogicalOffset == 0x5FFF4 &&
    plan.Patches[0].ByteLength == 4 &&
    plan.Safety.Status == "runtime-candidate-pending-duckstation" &&
    plan.Safety.RequiresDuckStationRuntimeProof,
    "The identity plan is no longer exactly one pending-runtime SCUS pointer patch.");

string expectedProofPath = outputPrefix + "-static-proof.json";
string expectedChecklistPath = outputPrefix + "-runtime-checklist.md";
await File.WriteAllTextAsync(expectedProofPath, "{\"status\":\"stale-interrupted-sidecar\"}");
await File.WriteAllTextAsync(expectedChecklistPath, "stale interrupted sidecar");
StoneHillTownSquareIdentityArtifactResult artifact =
    await StoneHillTownSquareIdentityArtifactWriter.ExportAsync(request);
StoneHillTownSquareIdentityCandidateResult result = artifact.Candidate;
Require(
    result.OutputImageSha256 == StoneHillTownSquareIdentityCandidateComposer.ExpectedOutputImageSha256,
    "The identity output did not match its pinned final BIN hash.");
Require(
    result.ChangedLogicalExecutableBytes == 1 &&
    result.ChangedPhysicalImageBytes == 31 &&
    result.RebuiltRawSectorCount == 1 &&
    result.ExactLogicalDiffBoundaryVerified &&
    result.ExactPhysicalSectorBoundaryVerified &&
    result.NamePointerReadbackVerified &&
    result.CountTablesPreserved &&
    result.BaseCandidatePreserved &&
    result.BinCuePublishCompleted,
    "The final identity candidate omitted a logical, physical, pointer, count, source, or publication proof.");
Require(
    await HashFileAsync(baseImage) == baseHashBefore &&
    baseHashBefore == NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256,
    "The exact runtime-proven base candidate changed.");
Require(
    artifact.StaticProofPath == expectedProofPath &&
    artifact.RuntimeChecklistPath == expectedChecklistPath &&
    File.Exists(artifact.StaticProofPath) &&
    File.Exists(artifact.RuntimeChecklistPath) &&
    !Directory.EnumerateFiles(outputRoot, "*.tmp").Any() &&
    !Directory.EnumerateFiles(outputRoot, "*.bak").Any(),
    "The identity artifact writer did not publish a clean, complete output set.");

using (JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(artifact.StaticProofPath)))
{
    Require(
        report.RootElement.GetProperty("status").GetString() ==
            "runtime-candidate-pending-duckstation" &&
        !report.RootElement.GetProperty("runtimeClaim").GetBoolean() &&
        report.RootElement.GetProperty("requiresDuckStationRuntimeProof").GetBoolean() &&
        report.RootElement.GetProperty("evidencePublication").GetString() ==
            "derived-regenerable-sidecars" &&
        report.RootElement.GetProperty("outputImageSha256").GetString() == result.OutputImageSha256,
        "The static-proof report made an invalid runtime claim or omitted the final hash.");
}
string checklist = await File.ReadAllTextAsync(artifact.RuntimeChecklistPath);
Require(
    checklist.Contains("0/200 gems", StringComparison.Ordinal) &&
    checklist.Contains("0/4 dragons", StringComparison.Ordinal) &&
    checklist.Contains("0/1 egg", StringComparison.Ordinal) &&
    checklist.Contains("original Town Square portal", StringComparison.Ordinal) &&
    checklist.Contains("Music remains Stone Hill", StringComparison.Ordinal),
    "The runtime checklist omitted identity, progress-isolation, or known-unchanged checks.");

Console.WriteLine("PASS: pending-runtime Town Square display identity candidate built from the exact proven base.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"BIN SHA-256: {result.OutputImageSha256}");
Console.WriteLine($"Checklist: {artifact.RuntimeChecklistPath}");
Console.WriteLine($"Static proof: {artifact.StaticProofPath}");
Console.WriteLine(
    $"Changed logical SCUS bytes: {result.ChangedLogicalExecutableBytes}; " +
    $"changed physical raw-sector bytes: {result.ChangedPhysicalImageBytes}; " +
    $"rebuilt raw sectors: {result.RebuiltRawSectorCount}.");

static async Task<bool> IsExactBaseCandidateAsync(string imagePath, string cuePath)
{
    if (!File.Exists(imagePath) || !File.Exists(cuePath))
        return false;
    return await HashFileAsync(imagePath) ==
        NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256;
}

static (string ImagePath, string CuePath) ResolveCleanSource(
    string repositoryRoot,
    string[] arguments)
{
    string? suppliedImage = arguments.ElementAtOrDefault(1);
    string? suppliedCue = arguments.ElementAtOrDefault(2);
    if (!string.IsNullOrWhiteSpace(suppliedImage) && !string.IsNullOrWhiteSpace(suppliedCue))
        return (Path.GetFullPath(suppliedImage), Path.GetFullPath(suppliedCue));

    string settingsPath = Path.Combine(repositoryRoot, "_local", "settings", "source-disc.json");
    if (!File.Exists(settingsPath))
        throw new FileNotFoundException(
            "The exact proven base is missing and no clean source-disc settings are available.",
            settingsPath);
    using JsonDocument settings = JsonDocument.Parse(File.ReadAllText(settingsPath));
    string imagePath = settings.RootElement.GetProperty("imagePath").GetString() ?? "";
    string cuePath = settings.RootElement.GetProperty("cuePath").GetString() ?? "";
    if (!File.Exists(imagePath) || !File.Exists(cuePath))
        throw new FileNotFoundException("The configured clean USA BIN/CUE is missing.");
    return (Path.GetFullPath(imagePath), Path.GetFullPath(cuePath));
}

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (File.Exists(Path.Combine(cursor.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src", "Spyro.Editor.Core")))
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static async Task<string> HashFileAsync(string path)
{
    await using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(await SHA256.HashDataAsync(stream)).ToLowerInvariant();
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static async Task ExpectFailureAsync(Func<Task> action, string message)
{
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is InvalidOperationException or InvalidDataException or ArgumentException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}
