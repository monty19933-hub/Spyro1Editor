using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: WizardPeakLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "wizard-peak-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Wizard Peak candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Wizard-Peak-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Wizard-Peak-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Wizard-Peak-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

WizardPeakNativeLockedChestCandidateResult result = WizardPeakNativeLockedChestCandidateExporter.Export(
    new WizardPeakNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != WizardPeakNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2800 ||
    result.Plan.RelocatedExecutableLba != 53880 ||
    result.Plan.OriginalCopyBufferAddress != "0x8008A8A0" ||
    result.Plan.RelocatedCopyBufferAddress != "0x8008BA38" ||
    result.Plan.HandlerPayloadLength != 0xBC0 ||
    result.Plan.KeyTrueIndex != 165 ||
    result.Plan.LockedChestTrueIndex != 166 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 167, 168, 169, 170, 171 }) ||
    result.Plan.SourceCountBefore != 165 ||
    result.Plan.SourceCountAfter != 172 ||
    result.Plan.ScenePointerFixupCountBefore != 191 ||
    result.Plan.ScenePointerFixupCountAfter != 198 ||
    result.Plan.SceneFixupTailInsertionBytes != 0x1C ||
    !string.Equals(result.Plan.ShiftedSceneSuffixSha256, "2e13770149961880250628cd95711483a4b955b30e8847846539ea7cda55d438", StringComparison.Ordinal) ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    result.Plan.TextureRegions.Any(region =>
        !string.Equals(region.TargetTilePreimageSha256, "076a27c79e5ace2a3d47f9dd2e83e4ff6ea8872b3c2218f66c92b89b55f36560", StringComparison.Ordinal) ||
        !string.Equals(region.TargetPalettePreimageSha256, "66687aadf862bd776c8fc18b8e9f8e20089714856ee233b3902a591d0d5f2925", StringComparison.Ordinal)) ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "c26a6a53c55e2fccc66a0f869904bb573f401a0708084d5663c7db9167fdfc73", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "30931d0506bc9392fa6e4767486dd52a9a1db0d8797a871e55ad544fb389f19d", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "1129de0ba79d4f4ddb76c5ea29aab17af022303fb1d32591d28cabac66d62c02", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "6a89b78579e81a870242348d8654911a74fb69b441079bad584e6027edd7ef45", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "e2dc46fb63423a9e6caadae11b7acdf307dfc9966a4c7bca2958ae59d1038929", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "7c47881a1302771d7cafcbc94e4385f40281edfe8794f27f37d0252f4f8414db", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Wizard Peak disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Wizard Peak native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(WizardPeakNativeLockedChestCandidateResult result) => $$"""
    # Wizard Peak Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Wizard Peak quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(33)}}` to enter Wizard Peak.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(132000, 100000, 22527)`.
    - Locked Chest: beside it at raw XYZ `(135000, 100000, 22527)`.

    ## Required runtime evidence

    - Cold boot reaches Wizard Peak without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Wizard Peak total becomes 510, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native Key logic, reward actors, and debris remain normal.
    - Pay special attention to loading, reset, death, and re-entry: this candidate uses an exact `0x1C` scene fixup-tail insertion that shifts a live `0xFF0`-byte suffix and still needs runtime proof.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
