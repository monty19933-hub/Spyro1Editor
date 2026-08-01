using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: TownSquareLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "town-square-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Town Square candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Town-Square-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Town-Square-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Town-Square-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

TownSquareNativeLockedChestCandidateResult result = TownSquareNativeLockedChestCandidateExporter.Export(
    new TownSquareNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != TownSquareNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2800 ||
    result.Plan.RelocatedExecutableLba != 53880 ||
    result.Plan.HandlerPayloadLength != 0xBC0 ||
    result.Plan.KeyTrueIndex != 107 ||
    result.Plan.LockedChestTrueIndex != 108 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 109, 110, 111, 112, 113 }) ||
    result.Plan.SourceCountBefore != 107 ||
    result.Plan.SourceCountAfter != 114 ||
    result.Plan.ScenePointerFixupCountBefore != 129 ||
    result.Plan.ScenePointerFixupCountAfter != 136 ||
    result.Plan.TreasureTargetBefore != 200 ||
    result.Plan.TreasureTargetAfter != 210 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "08f4372710b7162982397e247b75ed9b00cc5290ef9630d4e2fdd320611012b9", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "077d08948f51220f31796ded65463b0e07dc372b9bb76f1bde491095dfc93c89", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "fd859bff13985e18c3aff74cc889006c48b2743bf57a2b0f9d80662d25a09c2d", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "1c187183be611138b1058750ab099de71d8e4f9beb48c39a604068f0ef11bd48", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "4268f9943233b9f8c24d1d4b0924cba8747547b44b82eddd13f0a2ac10ac17de", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "a672a57dd6d592c53898bffd88c38aa7d009d6acc40247820d0d6b8496137f21", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Town Square disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Town Square native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(TownSquareNativeLockedChestCandidateResult result) => $$"""
    # Town Square Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Town Square quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(13)}}` to enter Town Square.

    ## Candidate placement

    - Gold Key: near native chest T27 at raw XYZ `(130150, 111032, 11264)`.
    - Locked Chest: at the proven T27 test position, raw XYZ `(132198, 111032, 11264)`.

    ## Required runtime evidence

    - Cold boot reaches Town Square without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Town Square total becomes 210, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors and metal debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
