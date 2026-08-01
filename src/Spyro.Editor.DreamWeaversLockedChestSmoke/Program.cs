using System.Text.Json;
using Spyro.Editor.Core.Exporting;

if (args.Length is not 0 and not 5)
{
    Console.Error.WriteLine(
        "Usage: DreamWeaversLockedChestSmoke [<retail.bin> <retail.cue> <wad-analysis.json> <catalog-root> <output-directory>]");
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
    : Path.Combine(repositoryRoot, "_local", "research", "dream-weavers-native-locked-chest");

foreach (string required in new[] { retail, retailCue, analysis, Path.Combine(catalogRoot, "spyro-level-catalog.json") })
{
    if (!File.Exists(required))
        throw new FileNotFoundException("Required Dream Weavers candidate input is missing.", required);
}
Directory.CreateDirectory(outputDirectory);

string outputBin = Path.Combine(outputDirectory, "Dream-Weavers-Key-Locked-Chest-disposable-v1.bin");
string outputCue = Path.ChangeExtension(outputBin, ".cue");
string outputPlan = Path.Combine(outputDirectory, "Dream-Weavers-Key-Locked-Chest-disposable-v1.plan.json");
string outputChecklist = Path.Combine(outputDirectory, "Dream-Weavers-Key-Locked-Chest-disposable-v1.runtime-checklist.md");

DreamWeaversNativeLockedChestCandidateResult result = DreamWeaversNativeLockedChestCandidateExporter.Export(
    new DreamWeaversNativeLockedChestCandidateRequest(
        retail,
        outputBin,
        analysis,
        catalogRoot,
        EnableFastEntry: true));

if (!result.Verified ||
    result.Plan.RecipeId != DreamWeaversNativeLockedChestCandidateExporter.RecipeId ||
    result.Plan.WadGrowthBytes != 0x2000 ||
    result.Plan.RelocatedExecutableLba != 53879 ||
    result.Plan.OriginalCopyBufferAddress != "0x8008BB38" ||
    result.Plan.RelocatedCopyBufferAddress != "0x8008C280" ||
    result.Plan.HandlerPayloadLength != 0x740 ||
    result.Plan.KeyTrueIndex != 151 ||
    result.Plan.LockedChestTrueIndex != 152 ||
    !result.Plan.RewardMarkerTrueIndices.SequenceEqual(new[] { 153, 154, 155, 156, 157 }) ||
    result.Plan.SourceCountBefore != 151 ||
    result.Plan.SourceCountAfter != 158 ||
    result.Plan.ScenePointerFixupCountBefore != 0xB6 ||
    result.Plan.ScenePointerFixupCountAfter != 0xBD ||
    result.Plan.TreasureTargetBefore != 300 ||
    result.Plan.TreasureTargetAfter != 310 ||
    result.Plan.RebasedTexturedFaceCount != 146 ||
    result.Plan.TextureRegions.Count != 4 ||
    !result.Plan.FastEntryEnabled ||
    !string.Equals(result.Plan.HandlerPayloadSha256, "43e3a90871020521101c3ea186e06dff9e5907b7471eee833e11627978301805", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.RebasedLockedChestPackageSha256, "bac0346b563610859dbb443a4177e4a148e4a17bf97df47814558a06f6e3be24", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalOverlaySha256, "0cb9d8134f75d5af06eec5df4d8478d833546a10348a5a5569d09fb4628d3d83", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalDataEntrySha256, "6aeafcd27e4404dcac2ed1afc5d438fee949afcfae09df9f561ed9cf38a446be", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.FinalExecutableSha256, "e0f53657bf39d18119a227428cf29db547b4fd18411f6f1e1139bbb70366c3cd", StringComparison.Ordinal) ||
    !string.Equals(result.Plan.OutputImageSha256, "62de415acbb34f0c873b5d174b6199c98de864ec189125dc5c7de6ee289fb3e9", StringComparison.Ordinal))
{
    throw new InvalidOperationException("The Dream Weavers disposable candidate did not meet its focused structural contract.");
}

WriteCue(retailCue, outputCue, Path.GetFileName(outputBin));
File.WriteAllText(outputPlan, JsonSerializer.Serialize(result.Plan, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllText(outputChecklist, BuildChecklist(result));

Console.WriteLine("Dream Weavers native Key + Locked Chest disposable candidate: PASS");
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

static string BuildChecklist(DreamWeaversNativeLockedChestCandidateResult result) => $$"""
    # Dream Weavers Key + Locked Chest disposable runtime check

    This is a research-only CUE. It is not promoted to normal Add/Create BIN.

    ## Enter Dream Weavers quickly

    1. Mount `{{Path.GetFileNameWithoutExtension(result.OutputImagePath)}}.cue` in DuckStation and cold boot it.
    2. Open Inventory.
    3. Enter `{{TestLevelWarpPatch.ActivationSequence}}`.
    4. Press `{{TestLevelWarpPatch.TargetSelectionText(50)}}` to enter Dream Weavers.

    ## Candidate placement

    - Gold Key: near the entry at raw XYZ `(97500, 78000, 13824)`.
    - Locked Chest: beside it at raw XYZ `(101000, 78000, 13824)`.

    ## Required runtime evidence

    - Cold boot reaches Dream Weavers without a black screen or GTE assertion.
    - Key is gold, collectible once, and persists correctly through death/reload.
    - Locked Chest has the retail metal model and textures.
    - Chest cannot open before collecting the Key.
    - After collecting the Key, the chest opens with native animation and sound.
    - Exactly six gems worth `+10` appear once; Dream Weavers total becomes 310, not more.
    - Chest and reward do not repeat after death, reload, or leave/re-enter.
    - Nearby native actors, native class-0D rewards, thief/key progression, and debris remain normal.

    ## Static proof

    - Recipe: `{{result.Plan.RecipeId}}`
    - BIN SHA-256: `{{result.Plan.OutputImageSha256}}`
    - Handler SHA-256: `{{result.Plan.HandlerPayloadSha256}}`
    - Rebased package SHA-256: `{{result.Plan.RebasedLockedChestPackageSha256}}`
    - {{result.Verification}}
    """;
