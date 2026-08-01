using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: TerraceVillageLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "terrace-village-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Terrace Village candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Terrace-Village-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Terrace-Village-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Terrace-Village-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

TerraceVillageNativeLockedChestCandidateResult result = TerraceVillageNativeLockedChestCandidateExporter.Export(
    new TerraceVillageNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != TerraceVillageNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x80087944" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80088090" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 164 ||
    result.Plan.LockedChestTrueIndex != 165 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 166, 167, 168, 169, 170 }) ||
    result.Plan.SourceCountBefore != 164 ||
    result.Plan.SourceCountAfter != 171 ||
    result.Plan.ScenePointerFixupCountBefore != 207 ||
    result.Plan.ScenePointerFixupCountAfter != 214 ||
    result.Plan.TreasureTargetBefore != 400 ||
    result.Plan.TreasureTargetAfter != 410 ||
    result.Plan.HandlerPayloadSha256 != "c229cb1715c5b2f19129fa52e394fad4649d489dc25c63d5a3cd9d3c2c3bac6b" ||
    result.Plan.LockedChestPackageSha256 != "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7" ||
    result.Plan.RebasedLockedChestPackageSha256 != "ad9489c9546acae2d67d61ce6821231bc9a9926eb8e256515c54c521fd4d5f0d" ||
    result.Plan.FinalOverlaySha256 != "925b5a3bbaf208225ccec61b5402018119a0d9a3438d547c20dd0ad02ffcd3cc" ||
    result.Plan.FinalDataEntrySha256 != "9a0c2495d3f7ebdeb6d276ca189954c6ad2849e6a0749aad75c9d8bb8161aaf4" ||
    result.Plan.FinalExecutableSha256 != "eb32cfcf5111e7689c96df16dc20d23f8ac7a38930887ec46a90e5fe2c1d1525" ||
    result.Plan.OutputImageSha256 != "c5cb8daf81eb0c3956cfe70e9a7e192f665c120d6c08a40f362b028318ae57be" ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled)
{
    throw new InvalidOperationException("The Terrace Village disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Terrace Village native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(TerraceVillageNativeLockedChestCandidateResult result) => $$"""
    # Terrace Village Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Terrace Village quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(41)}}` to enter Terrace Village.

    ## Candidate placement

    - Gold Key: beside the first gem route at raw XYZ `(58000, 96000, 10240)`.
    - Locked Chest: beside it at raw XYZ `(60048, 96000, 10240)`.

    ## Required runtime evidence

    - Cold boot reaches Terrace Village without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Terrace Village total becomes 410, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, class-0D rewards, key logic, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
