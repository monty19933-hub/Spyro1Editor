using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: GnastysWorldLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "gnastys-world-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Gnasty's World candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Gnastys-World-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Gnastys-World-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Gnastys-World-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

GnastysWorldNativeLockedChestCandidateResult result = GnastysWorldNativeLockedChestCandidateExporter.Export(
    new GnastysWorldNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != GnastysWorldNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x80085CE0" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80086440" ||
    result.Plan.HandlerPayloadLength != 0x760 ||
    result.Plan.KeyTrueIndex != 55 ||
    result.Plan.LockedChestTrueIndex != 56 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 57, 58, 59, 60, 61 }) ||
    result.Plan.SourceCountBefore != 55 ||
    result.Plan.SourceCountAfter != 62 ||
    result.Plan.ScenePointerFixupCountBefore != 0x42 ||
    result.Plan.ScenePointerFixupCountAfter != 0x49 ||
    result.Plan.TreasureTargetBefore != 200 ||
    result.Plan.TreasureTargetAfter != 210 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "e65417345c3bd24b95c23fe00dec532e04ea25704a00357a0bb6f3140bbef6f6", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "48ce27b83c49561f955801ce6eefa4e43d97e8cd79cbb72a883d67d34346875a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "f02fe6f1c3d4c72cc7efde05f3d86c909f5ca2684fe67b4b87fc3be104a3d82c", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "2346cbee3ec6ed96be36248d23c5173267453e126f6d86fbd603dd7d93c3e995", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "5958f1ccb5a430bbba0d55ed9181da97d52ae16e64e3120ca335dedaf8149131", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "8566a1e809b4fce9e1b29319147d7b6d84db26520bebc3f1c65f6d67020da76d", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Gnasty's World disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Gnasty's World native Key + Locked Chest disposable candidate: PASS");
Console.WriteLine($"BIN: {outputBin}");
Console.WriteLine($"CUE: {outputCue}");
Console.WriteLine($"Plan: {outputPlan}");
Console.WriteLine($"Checklist: {outputChecklist}");
Console.WriteLine($"SHA-256: {result.Plan.OutputImageSha256}");
Console.WriteLine($"Handler SHA-256: {result.Plan.HandlerPayloadSha256}");
Console.WriteLine($"Rebased package SHA-256: {result.Plan.RebasedLockedChestPackageSha256}");
Console.WriteLine($"Overlay SHA-256: {result.Plan.FinalOverlaySha256}");
Console.WriteLine($"Data SHA-256: {result.Plan.FinalDataEntrySha256}");
Console.WriteLine($"Executable SHA-256: {result.Plan.FinalExecutableSha256}");
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

static string BuildChecklist(GnastysWorldNativeLockedChestCandidateResult result) => $$"""
    # Gnasty's World Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Gnasty's World quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(60)}}` to enter Gnasty's World.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(84000, 85000, 7211)`.
    - Locked Chest: beside it at raw XYZ `(87500, 85000, 7211)`.

    ## Required runtime evidence

    - Cold boot reaches Gnasty's World without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Gnasty's World total becomes 210, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native class-0D rewards, thief/key progression, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
