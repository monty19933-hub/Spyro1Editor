using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: AlpineRidgeLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "alpine-ridge-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Alpine Ridge candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Alpine-Ridge-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Alpine-Ridge-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Alpine-Ridge-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

NativeLockedChestResearchCandidateResult result = NativeLockedChestResearchCandidateExporter.Export(
    new NativeLockedChestResearchCandidateRequest(
        "alpineridge",
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != AlpineRidgeNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x8008DEC0" ||
    result.Plan.RelocatedCopyBufferAddress != "0x8008E600" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 198 ||
    result.Plan.LockedChestTrueIndex != 199 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 200, 201, 202, 203, 204 }) ||
    result.Plan.SourceCountBefore != 198 ||
    result.Plan.SourceCountAfter != 205 ||
    result.Plan.ScenePointerFixupCountBefore != 0xF6 ||
    result.Plan.ScenePointerFixupCountAfter != 0xFD ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegionCount != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "a3b846afc6faf9f22f1841e39b34471563e273a227cd595645bcb64e24961e73", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "e7cb14dba979212f5411ff5916cad0ded3e615c6f5457f57e274c5d50fe74861", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "1a7873be32b9213d54b8b22e38de43e9d445e3b34b1cdc1ed199f409a782fad9", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "385a9b6741b9b4467bbec73790cc6d0c430502d6bff38416b2d734dac62fdec8", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "eca503941be0e6c184f83b9119c148caaf7347083d6c83e4da5744bf3f1f8c02", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "69b242ab6d61350e8c4c83b6022b70ea99b1158247082ca613afef33b9e1e308", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Alpine Ridge disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.DestinationPlan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Alpine Ridge native Key + Locked Chest disposable candidate: PASS");
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
    # Alpine Ridge Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Alpine Ridge quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(31)}}` to enter Alpine Ridge.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(100000, 120000, 23468)`.
    - Locked Chest: beside it at raw XYZ `(103000, 120000, 23468)`.

    ## Required runtime evidence

    - Cold boot reaches Alpine Ridge without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Alpine Ridge total becomes 510, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native class-0D rewards, thief/key progression, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
