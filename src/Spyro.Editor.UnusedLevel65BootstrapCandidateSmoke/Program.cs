using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string sourceImage = Path.GetFullPath(args.ElementAtOrDefault(1) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin"));
string sourceCue = Path.GetFullPath(args.ElementAtOrDefault(2) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue"));
if (!File.Exists(sourceImage) || !File.Exists(sourceCue))
    throw new FileNotFoundException("The level-65 bootstrap needs the clean USA retail BIN/CUE.");

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-bootstrap");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(
    outputRoot,
    "Unused-Level-65-Town-Square-flight65-exception-RUNTIME-CANDIDATE");
UnusedLevel65BootstrapCandidateResult result =
    await UnusedLevel65BootstrapCandidateExporter.ExportAsync(new(
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue",
        ExcludeLevel65FromFlightClassification: true));

Require(result.Plan.ProfileId == UnusedLevel65BootstrapCandidateExporter.Flight65ExceptionProfileId &&
        result.OutputImageSha256 == UnusedLevel65BootstrapCandidateExporter.ExpectedFlight65ExceptionOutputImageSha256 &&
        result.Plan.LevelId == 65 &&
        result.Plan.OverlayDirectoryIndex == 79 &&
        result.Plan.DataDirectoryIndex == 80 &&
        result.Plan.Patches.Count == 5 &&
        result.Plan.RequiresDuckStationRuntimeProof &&
        result.Plan.ExcludesLevel65FromFlightClassification,
    "The candidate was not bound to the exact level-65 flight-classifier exception recipe.");
Require(result.ChangedWadLogicalBytes == 9 &&
        result.ChangedExecutableLogicalBytes == 13 &&
        result.RebuiltRawSectorCount == 5 &&
        result.ExactLogicalDiffBoundaryVerified &&
        result.DonorPayloadsPreserved &&
        result.RetailRootDirectoryPreserved &&
        result.SourceImagePreserved,
    "The candidate omitted a source, diff, donor, extent, or raw-sector proof.");
Require(result.Plan.AliasedOverlayWadOffset == 0x118E800 &&
        result.Plan.AliasedOverlayByteLength == 0xF800 &&
        result.Plan.AliasedDataWadOffset == 0x119E000 &&
        result.Plan.AliasedDataByteLength == 0x2E2000 &&
        result.Plan.DonorOverlaySha256 ==
            "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" &&
        result.Plan.DonorDataSha256 ==
            "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
    "The exact Town Square alias offsets, lengths, or payload hashes changed.");
UnusedLevel65BootstrapPatch flightPatch = result.Plan.Patches.Single(patch =>
    patch.Kind == "exclude-level-65-from-flight-classification");
Require(flightPatch.LogicalOffset == 0x40C0 &&
        flightPatch.ByteLength == 4 &&
        flightPatch.BeforeHex == "66 66 02 3C" &&
        flightPatch.AfterHex == "00 5E 02 3C",
    "The flight-classifier exception lost its exact preimage or replacement.");
int[] retailFlightIds = [5, 15, 25, 35, 45, 55];
int[] patchedFlightIds = Enumerable.Range(0, 100)
    .Where(levelId => IsClassifiedAsFlight(levelId, 0x5E006667))
    .ToArray();
Require(patchedFlightIds.SequenceEqual(retailFlightIds) &&
        IsClassifiedAsFlight(65, 0x66666667) &&
        !IsClassifiedAsFlight(65, 0x5E006667),
    "The patched magic constant no longer preserves every retail flight while excluding ID65.");
string safetyChecklist = string.Join('\n', result.Plan.RuntimeChecklist);
Require(safetyChecklist.Contains("Disable memory-card insertion completely", StringComparison.Ordinal) &&
        safetyChecklist.Contains("Do not attack or kill enemies", StringComparison.Ordinal) &&
        safetyChecklist.Contains("do not save", StringComparison.OrdinalIgnoreCase) &&
        safetyChecklist.Contains("do not select Exit Level or Quit Game", StringComparison.Ordinal) &&
        safetyChecklist.Contains("Do not enter Town Square's Return Home portal", StringComparison.Ordinal) &&
        safetyChecklist.Contains("Sunny Flight still enters flight mode normally", StringComparison.Ordinal),
    "A mandatory no-card, no-interaction, no-save, no-menu-exit, no-Return-Home, or retail-flight sanity rule is missing.");
Require(File.Exists(result.OutputImagePath) && File.Exists(result.OutputCuePath),
    "The candidate BIN/CUE was not published.");
Require(!Directory.EnumerateFiles(outputRoot, ".*.tmp").Any() &&
        !Directory.EnumerateFiles(outputRoot, ".*.bak").Any(),
    "The transactional publisher left a temporary or backup artifact behind.");

string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";
object proof = new
{
    status = "static-proven-runtime-pending",
    runtimeClaim = false,
    generatedAtUtc = result.Plan.GeneratedAtUtc,
    profileId = result.Plan.ProfileId,
    sourceImageSha256 = result.Plan.SourceImageSha256,
    outputImageSha256 = result.OutputImageSha256,
    levelId = result.Plan.LevelId,
    directoryRows = new
    {
        overlay = result.Plan.OverlayDirectoryIndex,
        data = result.Plan.DataDirectoryIndex
    },
    alias = new
    {
        donor = "Town Square",
        overlayWadOffset = $"0x{result.Plan.AliasedOverlayWadOffset:X}",
        overlayByteLength = $"0x{result.Plan.AliasedOverlayByteLength:X}",
        overlaySha256 = result.Plan.DonorOverlaySha256,
        dataWadOffset = $"0x{result.Plan.AliasedDataWadOffset:X}",
        dataByteLength = $"0x{result.Plan.AliasedDataByteLength:X}",
        dataSha256 = result.Plan.DonorDataSha256
    },
    result.ChangedWadLogicalBytes,
    result.ChangedExecutableLogicalBytes,
    result.RebuiltRawSectorCount,
    result.ExactLogicalDiffBoundaryVerified,
    result.DonorPayloadsPreserved,
    result.RetailRootDirectoryPreserved,
    result.SourceImagePreserved,
    patches = result.Plan.Patches,
    limits = new[]
    {
        "This is a disposable ID-65 recognition/load discriminator, not an editor feature.",
        "Rows 79/80 are independent directory entries but intentionally alias Town Square's retail payload bytes in this phase.",
        "The prior native-index-35 alias candidate reached a frozen black transition and raised an Address Error Load at badvaddr 0x40000827 before rendering.",
        "The retail loader classifies every level ID ending in 5 as a flight; this incorrectly sends ID65 with Town Square data into flight initialization before the first frame.",
        "This control preserves level ID 65, world ID 5, continuous index 35, the ID-65 dispatch, and rows 79/80 while changing only the classifier's private magic LUI. Exhaustive integer evaluation over IDs 0 through 99 preserves exactly the retail flights 5/15/25/35/45/55. ID65 is the only supported level ID affected; unused IDs 75/85/95 also no longer satisfy the retail units-digit classifier.",
        "Neither superseded diagnostic is combined with this candidate: SCUS +0x40F4 retains retail bytes 29 00 44 14, and +0x5E58 retains retail bytes 21 10 65 00.",
        "Name, music, totals, save/load, portal ownership, and independent physical payload storage are not promoted; memory cards must remain disabled.",
        "DuckStation runtime proof is required before any physical-append experiment."
    }
};
await File.WriteAllTextAsync(
    staticProofPath,
    JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }) + "\n",
    Encoding.UTF8);

StringBuilder checklist = new();
checklist.AppendLine("# Unused level 65 / Town Square flight-65 exception — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{result.OutputImageSha256}`");
checklist.AppendLine($"- Profile: `{result.Plan.ProfileId}`");
checklist.AppendLine("- Status: static proof complete; runtime proof pending.");
checklist.AppendLine();
checklist.AppendLine("This candidate does **not** replace Stone Hill or any other retail level. The prior native-index-35 candidate reached a frozen black transition with an Address Error Load before rendering. The game classifies every level ID ending in 5 as a flight, so this follow-up keeps level ID 65, world ID 5, continuous index 35, the ID-65 Town Square dispatch, and reserved rows 79/80 while changing one private classifier instruction. Exactly the retail flights 5/15/25/35/45/55 remain classified as flights; ID65 is the only supported level ID affected and now takes normal-level initialization. Neither earlier diagnostic is included: SCUS +0x40F4 and +0x5E58 retain their exact retail instructions.");
checklist.AppendLine();
checklist.AppendLine("## Test");
checklist.AppendLine();
for (int index = 0; index < result.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {result.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine("Please report the last visible screen and whether loading, movement through several sectors, camera, initial music, opening/closing pause or Inventory, reset, and re-entry each passed. Keep memory cards disabled; do not attack, collect, interact, die, save, choose Exit Level/Quit Game, or use Return Home. If this loads, retail flight misclassification caused the prior crash; if it crashes at the same point, another ID65/index35 assumption remains. Finally report whether Sunny Flight still starts in normal flight mode.");
await File.WriteAllTextAsync(checklistPath, checklist.ToString(), Encoding.UTF8);

Console.WriteLine("PASS: level ID 65 and continuous index 35 are preserved while the flight classifier excludes only post-retail IDs ending in 5.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {staticProofPath}");
Console.WriteLine($"BIN SHA-256: {result.OutputImageSha256}");
Console.WriteLine(
    $"Changed logical bytes: WAD {result.ChangedWadLogicalBytes:N0}, " +
    $"SCUS {result.ChangedExecutableLogicalBytes:N0}; rebuilt raw sectors: {result.RebuiltRawSectorCount:N0}.");

static string FindRepositoryRoot(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    DirectoryInfo? cursor = new(Directory.GetCurrentDirectory());
    while (cursor != null)
    {
        if (File.Exists(Path.Combine(cursor.FullName, "spyro-level-catalog.json")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "src", "Spyro.Editor.Core")))
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static bool IsClassifiedAsFlight(int levelId, int magic)
{
    long product = (long)levelId * magic;
    int highWord = (int)(product >> 32);
    int quotient = (highWord >> 2) - (levelId >> 31);
    return quotient * 10 == levelId - 5;
}
