using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: HauntedTowersLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "haunted-towers-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Haunted Towers candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Haunted-Towers-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Haunted-Towers-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Haunted-Towers-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

HauntedTowersNativeLockedChestCandidateResult result = HauntedTowersNativeLockedChestCandidateExporter.Export(
    new HauntedTowersNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != HauntedTowersNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x80089820" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80089F60" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 177 ||
    result.Plan.LockedChestTrueIndex != 178 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 179, 180, 181, 182, 183 }) ||
    result.Plan.SourceCountBefore != 177 ||
    result.Plan.SourceCountAfter != 184 ||
    result.Plan.ScenePointerFixupCountBefore != 267 ||
    result.Plan.ScenePointerFixupCountAfter != 274 ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.TextureAllocationProof.Contains("2,176 distinct target bytes", StringComparison.Ordinal) ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "51b368913cad288f875bf489553d0a9b74374d9bb2a4ddca1f686ef12e810c21", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "e657f9fe5f6088689c3ec040977cbee7dda945dac6fed4be90b90abe24b12f47", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "ec6863b71ab75ca8df6b8443ca77b9c37b611ee94b566518775ecec69b9f52ac", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "ce2bac072bc9325ce389a04a776013e30b4d4c6aaee553dc0ce630ab159583a5", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "8764022d27d6bbccc72bed88052211eba2d5884541ec2576c4e992f64f462e84", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "dd8e30ebb21bd2603104b3d1d45fa04f588f09591b4d0e088966092a32566c6f", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Haunted Towers disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Haunted Towers native Key + Locked Chest disposable candidate: PASS");
Console.WriteLine($"BIN: {outputBin}");
Console.WriteLine($"CUE: {outputCue}");
Console.WriteLine($"Plan: {outputPlan}");
Console.WriteLine($"Checklist: {outputChecklist}");
Console.WriteLine($"Handler SHA-256: {result.Plan.HandlerPayloadSha256}");
Console.WriteLine($"Rebased package SHA-256: {result.Plan.RebasedLockedChestPackageSha256}");
Console.WriteLine($"Overlay SHA-256: {result.Plan.FinalOverlaySha256}");
Console.WriteLine($"Data SHA-256: {result.Plan.FinalDataEntrySha256}");
Console.WriteLine($"Executable SHA-256: {result.Plan.FinalExecutableSha256}");
Console.WriteLine($"BIN SHA-256: {result.Plan.OutputImageSha256}");
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

static string BuildChecklist(HauntedTowersNativeLockedChestCandidateResult result) => $$"""
    # Haunted Towers Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Haunted Towers quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(53)}}` to enter Haunted Towers.

    ## Candidate placement

    - Gold Key: near the entrance at raw XYZ `(43000, 87500, 25600)`.
    - Locked Chest: beside it at raw XYZ `(41000, 87500, 25600)`.

    ## Required runtime evidence

    - Cold boot reaches Haunted Towers without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Haunted Towers total becomes 510, not more.
    - Jacques remains a 500-treasure level; its adjacent executable table slot is unchanged.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, class-0D rewards, key logic, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
