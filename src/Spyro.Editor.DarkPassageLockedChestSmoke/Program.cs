using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: DarkPassageLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "dark-passage-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Dark Passage candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Dark-Passage-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Dark-Passage-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Dark-Passage-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

DarkPassageNativeLockedChestCandidateResult result = DarkPassageNativeLockedChestCandidateExporter.Export(
    new DarkPassageNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != DarkPassageNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x800880D4" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80088820" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 230 ||
    result.Plan.LockedChestTrueIndex != 231 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 232, 233, 234, 235, 236 }) ||
    result.Plan.SourceCountBefore != 230 ||
    result.Plan.SourceCountAfter != 237 ||
    result.Plan.ScenePointerFixupCountBefore != 261 ||
    result.Plan.ScenePointerFixupCountAfter != 268 ||
    result.Plan.TreasureTargetBefore != 500 ||
    result.Plan.TreasureTargetAfter != 510 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "1e47ce98002edff73b9f3fde2ab2d164184aac1c93049d4b70355ba09ecf245a", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.LockedChestPackageSha256, "8edc9e9ac1e5fb1a92224d2f5f8ca541371940c1ae3f9873c02f6b802d6e22e7", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "c81f03a6bc50f4a84d78479789827aed189e7be7fda732bf0702a55626e7da99", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "bdf04ebe9209693051f9078fbce893e01914d63ea644a5d8b83f7c07cf07a550", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "b82b28412e51e011eb419c65e99dda9ccb170348e24d4cdf020f4f25f459dd66", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "8665d6be5e92134fa97d94c0c3104e57ce8986af26bd52562e6b176dc44bcc21", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "a6c2fed945f6e58a7512d0ab8eea8b183e8678f2050643a54e8f35399a320f8f", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Dark Passage disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Dark Passage native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(DarkPassageNativeLockedChestCandidateResult result) => $$"""
    # Dark Passage Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Dark Passage quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(51)}}` to enter Dark Passage.

    ## Candidate placement

    - Gold Key: beside the first native dragon route at raw XYZ `(76000, 41933, 38920)`.
    - Locked Chest: beside it at raw XYZ `(73500, 41933, 38920)`.

    ## Required runtime evidence

    - Cold boot reaches Dark Passage without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Dark Passage total becomes 510, not more.
    - Lofty Castle's adjacent retail treasure target remains 400.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, the dragon rescue, native class-0D rewards, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
