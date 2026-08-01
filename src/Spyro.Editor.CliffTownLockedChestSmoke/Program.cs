using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: CliffTownLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "cliff-town-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Cliff Town candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Cliff-Town-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Cliff-Town-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Cliff-Town-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

CliffTownNativeLockedChestCandidateResult result = CliffTownNativeLockedChestCandidateExporter.Export(
    new CliffTownNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != CliffTownNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x1800 ||
    result.Plan.RelocatedExecutableLba != 53878 ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 167 ||
    result.Plan.LockedChestTrueIndex != 168 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 169, 170, 171, 172, 173 }) ||
    result.Plan.SourceCountBefore != 167 ||
    result.Plan.SourceCountAfter != 174 ||
    result.Plan.ScenePointerFixupCountBefore != 210 ||
    result.Plan.ScenePointerFixupCountAfter != 217 ||
    result.Plan.TreasureTargetBefore != 400 ||
    result.Plan.TreasureTargetAfter != 410 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "282c1a087b85e4e12a3ecc4d3e34fa5ed89df47bcabd48b93917b6d0bccf5a7e", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "fcf85a53c3fccfc42f45ff95c011602196f10b29f7cf83a600e3a8fd16eabca4", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "bb2ff657277b98cf1b29ebdba4bb88bfe3efd92b5d826d288d93a6e7ee8ab85a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "6d6964c43933542ab2c3f0b8d4398689bc4d9cc9aa583f757763e376fbd02b98", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "9c66571d0454d3230c46806f3987ada4c24cde93b93b714943450d2f6b39bc57", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "47f674825d737df8ed91c6a8bd3991ece0094b425d8d7e8a140d1c828187e8dc", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Cliff Town disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Cliff Town native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(CliffTownNativeLockedChestCandidateResult result) => $$"""
    # Cliff Town Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Cliff Town quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(22)}}` to enter Cliff Town.

    ## Candidate placement

    - Gold Key: near the level entrance at raw XYZ `(145000, 48000, 15360)`.
    - Locked Chest: beside it at raw XYZ `(148000, 48000, 15360)`.

    ## Required runtime evidence

    - Cold boot reaches Cliff Town without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Cliff Town total becomes 410, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors and metal debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
