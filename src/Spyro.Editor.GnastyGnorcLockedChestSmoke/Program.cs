using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: GnastyGnorcLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "gnasty-gnorc-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Gnasty Gnorc candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Gnasty-Gnorc-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Gnasty-Gnorc-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Gnasty-Gnorc-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

GnastyGnorcNativeLockedChestCandidateResult result = GnastyGnorcNativeLockedChestCandidateExporter.Export(
    new GnastyGnorcNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != GnastyGnorcNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x800854B4" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80085C10" ||
    result.Plan.HandlerPayloadLength != 0x75C ||
    result.Plan.KeyTrueIndex != 113 ||
    result.Plan.LockedChestTrueIndex != 114 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 115, 116, 117, 118, 119 }) ||
    result.Plan.SourceCountBefore != 113 ||
    result.Plan.SourceCountAfter != 120 ||
    result.Plan.ScenePointerFixupCountBefore != 119 ||
    result.Plan.ScenePointerFixupCountAfter != 126 ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.TextureAllocationProof.Contains("2,176 distinct target bytes", StringComparison.Ordinal) ||
    !result.Plan.Verification.Contains("independently indexed treasure slot 33", StringComparison.Ordinal) ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "6a193c09e8b44789a26db2131d7f6c0aadaf29b53a7c2b9528812164ed223d6f", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "dfcc832f3b8e349dc40d91f53927640e8eec7c257d2389c6ada276f1c1421cb6", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "7de8240362b3166183c2e30f661627b2a1fe8fc98e2c2fe362669761cf711068", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "d4082c808a8cbc81afd8ad33b24a2d5bbd079f05099176570024703418c0948a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "aa5b64b31b32acdd4e6aff48d8d3d327b88eadf9b60375af9023c864abe8a566", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "45cdbf83ecccb8967ccd0fc3ebb74814d26048e630fcfe06c985abc5a249ea63", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Gnasty Gnorc disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Gnasty Gnorc native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(GnastyGnorcNativeLockedChestCandidateResult result) => $$"""
    # Gnasty Gnorc Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Gnasty Gnorc quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(63)}}` to enter Gnasty Gnorc.

    ## Candidate placement

    - Gold Key: near the entrance at raw XYZ `(83500, 45500, 10614)`.
    - Locked Chest: beside it at raw XYZ `(85500, 45500, 10614)`.

    ## Required runtime evidence

    - Cold boot reaches Gnasty Gnorc without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Gnasty Gnorc total becomes 510, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - The Gnasty boss encounter, nearby native actors, class-0D rewards, key logic, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
