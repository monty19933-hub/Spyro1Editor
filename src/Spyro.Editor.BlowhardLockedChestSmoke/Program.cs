using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: BlowhardLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "blowhard-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Blowhard candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Blowhard-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Blowhard-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Blowhard-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

BlowhardNativeLockedChestCandidateResult result = BlowhardNativeLockedChestCandidateExporter.Export(
    new BlowhardNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != BlowhardNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.HandlerPayloadLength != 0x754 ||
    result.Plan.KeyTrueIndex != 87 ||
    result.Plan.LockedChestTrueIndex != 88 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 89, 90, 91, 92, 93 }) ||
    result.Plan.SourceCountBefore != 87 ||
    result.Plan.SourceCountAfter != 94 ||
    result.Plan.ScenePointerFixupCountBefore != 101 ||
    result.Plan.ScenePointerFixupCountAfter != 108 ||
    result.Plan.TreasureTargetBefore != 400 ||
    result.Plan.TreasureTargetAfter != 410 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "0f92841d92ef87da63613864dff1b171594d73064ef8c71a2ed64c170443cf5a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "dfcc832f3b8e349dc40d91f53927640e8eec7c257d2389c6ada276f1c1421cb6", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "c2420538dedb1f92fe878a528fda5d7a5658af0ddb778a9a89bb4ccc8050b36a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "4d4d95bf7655f5024ebe43ec7dc1fee50ca1e374f06ab1d9d59c5be448fe575a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "a7dd0d17a8de606526d15ca7084918796931590df748365db43e271855b60eba", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "c81119bad5ddf0647bdd5dc8cbf43e90b913d8381d9ab89b3be014e96576408c", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Blowhard disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Blowhard native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(BlowhardNativeLockedChestCandidateResult result) => $$"""
    # Blowhard Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Blowhard quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(34)}}` to enter Blowhard.

    ## Candidate placement

    - Gold Key: near the level entrance at raw XYZ `(78000, 58000, 12957)`.
    - Locked Chest: beside it at raw XYZ `(80000, 58000, 12957)`.

    ## Required runtime evidence

    - Cold boot reaches Blowhard without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Blowhard total becomes 410, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors and metal debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
