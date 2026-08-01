using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: TwilightHarborLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "twilight-harbor-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Twilight Harbor candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Twilight-Harbor-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Twilight-Harbor-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Twilight-Harbor-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

NativeLockedChestResearchCandidateResult result = NativeLockedChestResearchCandidateExporter.Export(
    new NativeLockedChestResearchCandidateRequest(
        "twilightharbor",
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != TwilightHarborNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x1800 ||
    result.Plan.RelocatedExecutableLba != 53878 ||
    result.Plan.OriginalCopyBufferAddress != "0x80086004" ||
    result.Plan.RelocatedCopyBufferAddress != "0x80086004" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 132 ||
    result.Plan.LockedChestTrueIndex != 133 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 134, 135, 136, 137, 138 }) ||
    result.Plan.SourceCountBefore != 132 ||
    result.Plan.SourceCountAfter != 139 ||
    result.Plan.ScenePointerFixupCountBefore != 0xAB ||
    result.Plan.ScenePointerFixupCountAfter != 0xB2 ||
    result.Plan.TreasureTargetBefore != 400 ||
    result.Plan.TreasureTargetAfter != 410 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegionCount != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "35fdca36ef421be9aca67d2a4e78c06580b4fe522cbc5da7222dad94933155d4", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "620cc533ea373db554a05356ce8b71f07fb2c8760ed80f060a9beec2934674b4", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "70ee8b4cf778e00c1fa3da23ec5a9c3fea6758cdf4ec27bfa31eb5339de7e834", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "072d4836b5ed78897abb1b01ebf141a0f8d2dfbd82ee0206f2fff8dc4d5cc9db", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "fa8c5d98e45f27801fd943a8e247d781d6a52b03687e544d4cc9fdd0fa7450a5", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "1709517d93c5856edf68254bd8b121ba62b3babf6049055e5887730e23432ac0", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Twilight Harbor disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.DestinationPlan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Twilight Harbor native Key + Locked Chest disposable candidate: PASS");
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
    # Twilight Harbor Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Twilight Harbor quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(62)}}` to enter Twilight Harbor.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(25000, 100000, 8192)`.
    - Locked Chest: beside it at raw XYZ `(27000, 100000, 8192)`.

    ## Required runtime evidence

    - Cold boot reaches Twilight Harbor without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Twilight Harbor total becomes 410, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native class-0D rewards, thief/key progression, and debris remain normal.

    ## Twilight Harbor RAM proof required

    - After the expanded level loads, capture RAM and confirm that `*(uint32_t *)0x80075828 + 0x30BC` begins with the exact handler SHA-256 below.
    - Confirm the loaded scene still ends at `0x801878F4`, leaving the retail `0x2BC` margin below the lower polygon buffer at `0x80187BB0`.
    - Confirm the overlay copy buffer remains `0x80086004`; this candidate deliberately does not move it.
    - This CUE owns SCUS `0x8007314C-0x80073173` and is incompatible with Spring Chest/object-environment helper experiments that use overlapping bytes.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
