using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: GnastyLootLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "gnastys-loot-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Gnasty's Loot candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Gnastys-Loot-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Gnastys-Loot-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Gnastys-Loot-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

NativeLockedChestResearchCandidateResult result = NativeLockedChestResearchCandidateExporter.Export(
    new NativeLockedChestResearchCandidateRequest(
        "gnastysloot",
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != GnastyLootNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x1800 ||
    result.Plan.RelocatedExecutableLba != 53878 ||
    result.Plan.RelocatedCopyBufferAddress != "0x80086A38" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 129 ||
    result.Plan.LockedChestTrueIndex != 130 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 131, 132, 133, 134, 135 }) ||
    result.Plan.SourceCountBefore != 129 ||
    result.Plan.SourceCountAfter != 136 ||
    result.Plan.ScenePointerFixupCountBefore != 0x92 ||
    result.Plan.ScenePointerFixupCountAfter != 0x99 ||
    result.Plan.TreasureTargetBefore != 2000 ||
    result.Plan.TreasureTargetAfter != 2010 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegionCount != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "244daaa7703fc60f04e761c3399b3343e392d467b2774780facfcb1ddb77487c", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "25d4c82db0e984a0db173bdc4efc623d3f27b0af5a72fa4cb702dc6d78b3544b", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "4c2d04d8b02d7311abc3ac46ca4b453c8c70388604f334146f899cbfb5939065", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "5ca1f09a60aab2c1619dbacfd0b4dd3b621b4f2dcf6345db3393e0e33dabda26", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "441d7f7085784d53d9ffa8ae6e0a131f90bc88c52ed3b64a73d5ead81564660b", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "64066d313e755f90a1e82687b5fc8bba405bdb033478279316b6c2a6f162bd98", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Gnasty's Loot disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.DestinationPlan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Gnasty's Loot native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(NativeLockedChestResearchCandidateResult result) => $$"""
    # Gnasty's Loot Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Gnasty's Loot quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(64)}}` to enter Gnasty's Loot.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(92500, 43000, 14848)`.
    - Locked Chest: beside it at raw XYZ `(94000, 43000, 14848)`.

    ## Required runtime evidence

    - Cold boot reaches Gnasty's Loot without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Gnasty's Loot total becomes 2010, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native class-0D rewards, thief/key progression, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
