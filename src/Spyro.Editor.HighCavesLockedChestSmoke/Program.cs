using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: HighCavesLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "high-caves-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required High Caves candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "High-Caves-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "High-Caves-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "High-Caves-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

HighCavesNativeLockedChestCandidateResult result = HighCavesNativeLockedChestCandidateExporter.Export(
    new HighCavesNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != HighCavesNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x8008C73C" ||
    result.Plan.RelocatedCopyBufferAddress != "0x8008CE80" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 148 ||
    result.Plan.LockedChestTrueIndex != 149 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 150, 151, 152, 153, 154 }) ||
    result.Plan.SourceCountBefore != 148 ||
    result.Plan.SourceCountAfter != 155 ||
    result.Plan.ScenePointerFixupCountBefore != 199 ||
    result.Plan.ScenePointerFixupCountAfter != 206 ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "33c3a49ca369ce67ba367c4d7f1d018707d430bc3c5357ee7e7cdc4e356d44ac", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "1738444d7138a919d30ecb511385d9e15731b51f0d75e9224fafa9b262394957", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "9dbeef58b99304e3bc542d92a56e2228aa391d409e2084cfe50f15b16db96bd6", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "8afff03c91a72fcdb1ddc2a6eb0e6f69cdff6f5a26e41de2d92eca9699b91555", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "7855e54e19a251610a3a679df8b8bd0c0e9915523f59b046c7fd8a72edd87480", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "82e1164b319d1aec1620839c4fcd9a0feaa3a10f0cde14dcb7523f3105b93d9f", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The High Caves disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("High Caves native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(HighCavesNativeLockedChestCandidateResult result) => $$"""
    # High Caves Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter High Caves quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(32)}}` to enter High Caves.

    ## Candidate placement

    - Gold Key: beside the first gem route at raw XYZ `(33000, 58000, 35840)`.
    - Locked Chest: beside it at raw XYZ `(35048, 58000, 35840)`.

    ## Required runtime evidence

    - Cold boot reaches High Caves without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; High Caves total becomes 510, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, class-0D rewards, key logic, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
