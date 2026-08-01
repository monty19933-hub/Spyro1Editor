using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: ToastyLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Spyro Editor", "Projects", "default-project", "spyro-wad-analysis.json");
string catalogRoot = args.Length == 5
    ? Path.GetFullPath(args[3])
    : repositoryRoot;
string outputDirectory = args.Length == 5
    ? Path.GetFullPath(args[4])
    : Path.Combine(repositoryRoot, "_local", "research", "toasty-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Toasty candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Toasty-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Toasty-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Toasty-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

ToastyNativeLockedChestCandidateResult result = ToastyNativeLockedChestCandidateExporter.Export(
    new ToastyNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != ToastyNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.HandlerPayloadLength != 0x760 ||
    result.Plan.KeyTrueIndex != 59 ||
    result.Plan.LockedChestTrueIndex != 60 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 61, 62, 63, 64, 65 }) ||
    result.Plan.SourceCountBefore != 59 ||
    result.Plan.SourceCountAfter != 66 ||
    result.Plan.ScenePointerFixupCountBefore != 0x62 ||
    result.Plan.ScenePointerFixupCountAfter != 0x69 ||
    result.Plan.TreasureTargetBefore != 100 ||
    result.Plan.TreasureTargetAfter != 110 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "d43541718b37b104fa409648e6f355af3d8ac51032d15dda50dde5b27189ff87", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "d7d90e7067295370587e0faf4708d6f63002b71e84ebb1a2602344b6a28496fd", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "0c1b7fcf5997f83a268b050f7487974a8937ee17a9373ad74a57f730472fb26b", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "cc35e035616a5c3d73f469b02d262f81cca3ba2d262d0be86e49a688b017c961", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "d168697a95bc2f00de9271e2da3c551a27a9b713b2f95bcbb789398c4f915eb1", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "05959619954eb515f98c1d06133459a88905a89d6019b8f8a1dce13e74e309df", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Toasty disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Toasty native Key + Locked Chest disposable candidate: PASS");
Console.WriteLine($"BIN: {outputBin}");
Console.WriteLine($"CUE: {outputCue}");
Console.WriteLine($"Plan: {outputPlan}");
Console.WriteLine($"Checklist: {outputChecklist}");
Console.WriteLine($"SHA-256: {result.Plan.OutputImageSha256}");
Console.WriteLine($"Fast entry: {result.Plan.FastEntryInstructions}");
return 0;

static void WriteCue(string sourceCuePath, string outputCuePath, string outputBinName)
{
    string[] lines = File.ReadAllLines(sourceCuePath);
    int fileLine = Array.FindIndex(lines, line => line.TrimStart().StartsWith("FILE ", StringComparison.OrdinalIgnoreCase));
    if (fileLine < 0)
        throw new InvalidDataException("The retail CUE does not contain a FILE line.");
    string indent = lines[fileLine][..(lines[fileLine].Length - lines[fileLine].TrimStart().Length)];
    lines[fileLine] = $"{indent}FILE \"{outputBinName}\" BINARY";
    File.WriteAllLines(outputCuePath, lines);
}

static string BuildChecklist(ToastyNativeLockedChestCandidateResult result) => $$"""
    # Toasty Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Toasty quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(14)}}` to enter Toasty.

    ## Candidate placement

    - Gold Key: near the native chest cluster at raw XYZ `(108319, 118467, 19421)`.
    - Locked Chest: beside it at raw XYZ `(108534, 116541, 19421)`.

    ## Required runtime evidence

    - Cold boot reaches Toasty without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Toasty total becomes 110, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native Toasty class-0D rows T54/T55, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
