using System.Text.Json;
using System.Text.Json.Serialization;
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

string outputPrefix = Path.Combine(
    outputRoot,
    "Stone-Hill-slot-Town-Square-complete-level-RUNTIME-CANDIDATE");
StoneHillTownSquareReplacementCandidateResult result =
    await StoneHillTownSquareReplacementCandidateComposer.ExportAsync(new(
        manifest,
        catalog,
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue"));

Require(result.Plan.Patches.Count == 8,
    "The candidate no longer has exactly two payload, two header-size, one dispatch, and three demo-safety patches.");
Require(result.Plan.DonorSubfiles.Count == 8 &&
    result.Plan.DonorSubfiles.Select(subfile => subfile.SubfileIndex)
        .SequenceEqual(Enumerable.Range(0, 8)),
    "The candidate did not carry Town Square's complete eight-subfile archive.");
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

JsonSerializerOptions jsonOptions = new()
{
    WriteIndented = true,
    Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
};
string reportPath = outputPrefix + "-static-proof.json";
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(new
{
    status = "static-readback-passed-duckstation-runtime-pending",
    runtimeClaim = false,
    result.OutputImagePath,
    result.OutputCuePath,
    result.OutputImageSha256,
    result.ChangedWadBytes,
    result.ChangedExecutableBytes,
    result.RebuiltRawSectorCount,
    result.ExactLogicalDiffBoundaryVerified,
    result.ArtisansPortalPreimagesVerified,
    result.DonorEntriesPreserved,
    result.TownSquareFlyInRelocatedExactly,
    result.TownSquareReturnHomeRelocatedExactly,
    result.ResidualTargetCapacityPreserved,
    result.SourceImagePreserved,
    result.AtomicRenameCompleted,
    result.Plan
}, jsonOptions));

string checklistPath = outputPrefix + "-runtime-checklist.md";
await File.WriteAllTextAsync(checklistPath,
    $"""
    # V5 Stone Hill slot replacement — DuckStation checklist

    This is a disposable V5 research CUE. It does not change Beta V4, normal Create BIN,
    the retail source image, or any saved editor project.

    ## Start

    1. Use a fresh game and a disposable memory card. Existing Stone Hill collection masks
       can hide unrelated Town Square rows because this experiment intentionally keeps slot 11.
    2. Boot `{Path.GetFileName(result.OutputCuePath)}` from a cold start.
    3. Walk into the Artisans portal still labelled **Stone Hill**.

    ## Expected replacement

    - The portal should load the complete retail **Town Square** level payload in Stone Hill's slot.
    - Town Square geometry, collision, textures, sky, actors, camera, fly-in, and actor sounds
      should appear together. The UI/portal identity and music remain Stone Hill for this first proof.
    - The former Stone Hill title-demo slot is safely rerouted to the native Doctor Shemp demo so
      an idle title screen cannot read beyond Town Square's shorter scene package.

    ## Exercise

    - Run, jump, glide, charge, and flame around the opening and the raised town areas.
    - Defeat several enemies and collect several gems.
    - Rescue one dragon and complete its cutscene.
    - Chase/collect the egg thief if practical.
    - Pause, open Inventory, die/reload, and confirm the level remains functional.
    - Use Town Square's Return Home portal, arrive in Artisans, then re-enter Stone Hill.
    - Optionally return to the title screen and let the demo cycle once; it should run Doctor Shemp.

    ## Report immediately

    Record the last visible frame and whether music continued if there is a black screen,
    freeze, crash, missing actor, wrong collision, repeated cutscene, or broken Return Home.
    """);

Console.WriteLine("PASS: complete Town Square overlay/data pair installed in the Stone Hill retail slot.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {reportPath}");
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
