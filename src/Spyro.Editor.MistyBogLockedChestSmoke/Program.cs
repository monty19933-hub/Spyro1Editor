using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: MistyBogLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "misty-bog-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Misty Bog candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Misty-Bog-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Misty-Bog-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Misty-Bog-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

MistyBogNativeLockedChestCandidateResult result = MistyBogNativeLockedChestCandidateExporter.Export(
    new MistyBogNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != MistyBogNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2800 ||
    result.Plan.RelocatedExecutableLba != 53880 ||
    result.Plan.OriginalCopyBufferAddress != "0x80087130" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80088238" ||
    result.Plan.HandlerPayloadLength != 0xBC0 ||
    result.Plan.KeyTrueIndex != 212 ||
    result.Plan.LockedChestTrueIndex != 213 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 214, 215, 216, 217, 218 }) ||
    result.Plan.SourceCountBefore != 212 ||
    result.Plan.SourceCountAfter != 219 ||
    result.Plan.ScenePointerFixupCountBefore != 289 ||
    result.Plan.ScenePointerFixupCountAfter != 296 ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "4e416ab0dd16e405f5902866cf2677c3f04c5f7cf9c8f76e3e1479d8cac172be", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "d2e4120bb22b25ff1617737daf2a62c5cab9b05951b28fe3695f0908f85815fe", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "ba14713f69a0207db03b3d3dceaf9a5f809f39828a799ef2457eba453fb60241", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "19a8f49c5cfb63f15dcd4137b7446f92a1a23cfcd57d0f78be78bd2b12a53164", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "bbbf53094be237a42f3024b6a1c7136787e6b5fa81d1fcffcf16c7527031bd9c", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "0f66307f031463a57faf2cd7e074db23eb2c6736dae36452b781a90e2b2f99f0", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Misty Bog disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Misty Bog native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(MistyBogNativeLockedChestCandidateResult result) => $$"""
    # Misty Bog Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Misty Bog quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(42)}}` to enter Misty Bog.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(82000, 68000, 4037)`.
    - Locked Chest: beside it at raw XYZ `(84048, 68000, 4037)`.

    ## Required runtime evidence

    - Cold boot reaches Misty Bog without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Misty Bog total becomes 510, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native Key logic, reward actors, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
