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
    "Unused-Level-65-Town-Square-alias-RUNTIME-CANDIDATE");
UnusedLevel65BootstrapCandidateResult result =
    await UnusedLevel65BootstrapCandidateExporter.ExportAsync(new(
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue"));

Require(result.Plan.ProfileId == UnusedLevel65BootstrapCandidateExporter.ProfileId &&
        result.OutputImageSha256 == UnusedLevel65BootstrapCandidateExporter.ExpectedOutputImageSha256 &&
        result.Plan.LevelId == 65 &&
        result.Plan.OverlayDirectoryIndex == 79 &&
        result.Plan.DataDirectoryIndex == 80 &&
        result.Plan.Patches.Count == 4 &&
        result.Plan.RequiresDuckStationRuntimeProof,
    "The candidate was not bound to the exact static-only level-65 alias recipe.");
Require(result.ChangedWadLogicalBytes == 9 &&
        result.ChangedExecutableLogicalBytes == 11 &&
        result.RebuiltRawSectorCount == 4 &&
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
string safetyChecklist = string.Join('\n', result.Plan.RuntimeChecklist);
Require(safetyChecklist.Contains("Disable memory-card insertion completely", StringComparison.Ordinal) &&
        safetyChecklist.Contains("Do not attack or kill enemies", StringComparison.Ordinal) &&
        safetyChecklist.Contains("do not save", StringComparison.OrdinalIgnoreCase) &&
        safetyChecklist.Contains("do not select Exit Level or Quit Game", StringComparison.Ordinal) &&
        safetyChecklist.Contains("Do not enter Town Square's Return Home portal", StringComparison.Ordinal),
    "A mandatory no-card, no-interaction, no-save, no-menu-exit, or no-Return-Home runtime rule is missing.");
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
        "Name, music, totals, save/load, portal ownership, and independent physical payload storage are not promoted. Retail save code can serialize level ID 65 and slot-35 state; memory cards must remain disabled.",
        "DuckStation runtime proof is required before the next physical-append experiment."
    }
};
await File.WriteAllTextAsync(
    staticProofPath,
    JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }) + "\n",
    Encoding.UTF8);

StringBuilder checklist = new();
checklist.AppendLine("# Unused level 65 / Town Square alias — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{result.OutputImageSha256}`");
checklist.AppendLine($"- Profile: `{result.Plan.ProfileId}`");
checklist.AppendLine("- Status: static proof complete; runtime proof pending.");
checklist.AppendLine();
checklist.AppendLine("This candidate does **not** replace Stone Hill or any other retail level. It fills the reserved level-65 WAD directory row and deliberately aliases Town Square's checked retail bytes so this test isolates ID-65 loader/dispatch recognition before physical WAD growth.");
checklist.AppendLine();
checklist.AppendLine("## Test");
checklist.AppendLine();
for (int index = 0; index < result.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {result.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine("Please report the last visible screen and whether loading, movement through several sectors, camera, initial music, opening/closing pause or Inventory, reset, and re-entry each passed. Keep memory cards disabled; do not attack, collect, interact, die, save, choose Exit Level/Quit Game, or use Return Home in this phase. A failure here should not be interpreted as a content failure; this phase is specifically locating any remaining ID-65 executable or progression assumption.");
await File.WriteAllTextAsync(checklistPath, checklist.ToString(), Encoding.UTF8);

Console.WriteLine("PASS: reserved WAD rows 79/80 and executable ID-65 dispatch are statically proven.");
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
