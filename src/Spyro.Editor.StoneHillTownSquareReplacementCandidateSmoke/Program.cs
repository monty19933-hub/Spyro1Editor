using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string sourceImage = Path.GetFullPath(args.ElementAtOrDefault(1) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin"));
string sourceCue = Path.GetFullPath(args.ElementAtOrDefault(2) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue"));
if (!File.Exists(sourceImage) || !File.Exists(sourceCue))
    throw new FileNotFoundException("The replacement candidate needs the clean USA retail BIN/CUE.");

LevelCatalog catalog = LevelCatalog.Load(repositoryRoot);
string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "town-square-complete-pair-candidate");
Directory.CreateDirectory(outputRoot);
NativeLevelReplacementManifest manifest = await NativeLevelReplacementStore.StartStoneHillAsync(
    outputRoot,
    sourceImage,
    catalog);
NativeLevelReplacementIntent intent = await NativeLevelReplacementIntentStore.StartAsync(
    outputRoot,
    NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId,
    manifest,
    catalog);
NativeLevelReplacementIntent loadedIntent = NativeLevelReplacementIntentStore.Load(
        outputRoot,
        "stonehill",
        manifest,
        catalog)
    ?? throw new InvalidOperationException("The persisted replacement intent was not loaded.");
Require(loadedIntent == intent,
    "The exact replacement intent changed during save/load.");

string outputPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
StoneHillTownSquareReplacementCandidateRequest request = new(
        manifest,
        loadedIntent,
        catalog,
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue");
await ExpectFailureAsync(
    () => StoneHillTownSquareReplacementCandidateComposer.BuildPlanAsync(
        request with
        {
            Intent = loadedIntent with
            {
                SourceImageSha256 = new string('0', 64)
            }
        }),
    "A forged replacement intent reached candidate planning.");
string invalidCue = Path.Combine(outputRoot, "invalid-source.cue");
await File.WriteAllTextAsync(
    invalidCue,
    "FILE \"wrong.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
await ExpectFailureAsync(
    () => StoneHillTownSquareReplacementCandidateComposer.BuildPlanAsync(
        request with { SourceCuePath = invalidCue }),
    "A CUE that did not reference the bound retail BIN passed safety planning.");

StoneHillTownSquareReplacementArtifactResult artifact =
    await StoneHillTownSquareReplacementArtifactWriter.ExportAsync(request);
StoneHillTownSquareReplacementCandidateResult result = artifact.Candidate;

Require(result.Plan.Patches.Count == 8,
    "The candidate no longer has exactly two payload, two header-size, one dispatch, and three demo-safety patches.");
Require(result.Plan.DonorSubfiles.Count == 8 &&
    result.Plan.DonorSubfiles.Select(subfile => subfile.SubfileIndex)
        .SequenceEqual(Enumerable.Range(0, 8)),
    "The candidate did not carry Town Square's complete eight-subfile archive.");
Require(result.Plan.ProfileId ==
        NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillProfileId &&
    result.Plan.ProfileRecipeVersion == 1 &&
    result.Plan.Evidence == NativeLevelReplacementEvidenceStatus.RuntimeProven &&
    result.Plan.ExpectedOutputImageSha256 ==
        NativeLevelReplacementProfileRegistry.TownSquareIntoStoneHillOutputImageSha256 &&
    result.OutputImageSha256 == result.Plan.ExpectedOutputImageSha256 &&
    result.Plan.Safety.Status == "runtime-proven-profile-guarded" &&
    !result.Plan.Safety.RequiresDuckStationRuntimeProof,
    "The generated artifact was not bound to the exact runtime-proven profile and output SHA-256.");
Require(result.Plan.TargetOverlayBefore.ByteLength >= result.Plan.OutputOverlayByteLength &&
    result.Plan.TargetDataBefore.ByteLength >= result.Plan.OutputDataByteLength,
    "The donor pair exceeded Stone Hill's fixed retail capacity.");
Require(result.ChangedWadBytes > 0 && result.ChangedExecutableBytes > 0 &&
    result.RebuiltRawSectorCount > 0 && result.ExactLogicalDiffBoundaryVerified &&
    result.ArtisansPortalPreimagesVerified && result.DonorEntriesPreserved &&
    result.TownSquareFlyInRelocatedExactly && result.TownSquareReturnHomeRelocatedExactly &&
    result.ResidualTargetCapacityPreserved && result.SourceImagePreserved &&
    result.AtomicRenameCompleted,
    "The final candidate omitted a source, diff, raw-sector, portal, donor, scene, or atomic proof.");
Require(File.Exists(artifact.StaticProofPath) && File.Exists(artifact.RuntimeChecklistPath) &&
    !Directory.EnumerateFiles(outputRoot, "*.tmp").Any(),
    "The artifact writer did not publish both evidence files with atomic per-file writes.");
using (JsonDocument report = JsonDocument.Parse(await File.ReadAllTextAsync(artifact.StaticProofPath)))
{
    Require(report.RootElement.GetProperty("status").GetString() ==
            "runtime-proven-profile-guarded" &&
        report.RootElement.GetProperty("runtimeClaim").GetBoolean() &&
        report.RootElement.GetProperty("profileId").GetString() == result.Plan.ProfileId &&
        report.RootElement.GetProperty("expectedOutputImageSha256").GetString() ==
            result.OutputImageSha256,
        "The generated static-proof report did not record the exact promoted profile/output.");
}
Require((await File.ReadAllTextAsync(artifact.RuntimeChecklistPath))
        .Contains("Doctor Shemp is the expected safety reroute", StringComparison.Ordinal),
    "The generated regression checklist did not record the proven title-demo behavior.");

Console.WriteLine("PASS: complete Town Square overlay/data pair installed in the Stone Hill retail slot.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"Checklist: {artifact.RuntimeChecklistPath}");
Console.WriteLine($"Static proof: {artifact.StaticProofPath}");
Console.WriteLine(
    $"Changed logical bytes: WAD {result.ChangedWadBytes:N0}, SCUS {result.ChangedExecutableBytes:N0}; " +
    $"rebuilt raw sectors: {result.RebuiltRawSectorCount:N0}.");

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
