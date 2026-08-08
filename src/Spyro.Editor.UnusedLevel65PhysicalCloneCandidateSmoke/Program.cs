using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string sourceImage = Path.GetFullPath(args.ElementAtOrDefault(1) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).bin"));
string sourceCue = Path.GetFullPath(args.ElementAtOrDefault(2) ??
    Path.Combine(repositoryRoot, "Spyro the Dragon (USA).cue"));
if (!File.Exists(sourceImage) || !File.Exists(sourceCue))
    throw new FileNotFoundException("The physical level-65 clone needs the clean USA retail BIN/CUE.");

string outputRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-physical-clone");
Directory.CreateDirectory(outputRoot);
string outputPrefix = Path.Combine(
    outputRoot,
    "Unused-Level-65-Town-Square-independent-storage-RUNTIME-CANDIDATE");
UnusedLevel65PhysicalCloneCandidateResult result =
    await UnusedLevel65PhysicalCloneCandidateExporter.ExportAsync(new(
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue"));

Require(result.Plan.ProfileId == UnusedLevel65PhysicalCloneCandidateExporter.ProfileId &&
        result.OutputImageSha256 == UnusedLevel65PhysicalCloneCandidateExporter.ExpectedOutputImageSha256 &&
        result.Plan.SourceImageSha256 ==
            "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37" &&
        result.Plan.BaseAliasProfileId ==
            UnusedLevel65BootstrapCandidateExporter.Flight65ExceptionProfileId &&
        result.Plan.BaseAliasImageSha256 ==
            UnusedLevel65BootstrapCandidateExporter.ExpectedFlight65ExceptionOutputImageSha256 &&
        result.Plan.LevelId == 65 &&
        result.Plan.OverlayDirectoryIndex == 79 &&
        result.Plan.DataDirectoryIndex == 80 &&
        result.Plan.RequiresDuckStationRuntimeProof,
    "The physical candidate lost its exact clean-source, proven-base, ID65, or runtime-pending identity.");
Require(result.Plan.TargetOverlayWadOffset == 0x6927000 &&
        result.Plan.TargetDataWadOffset == 0x6936800 &&
        result.Plan.OriginalWadByteLength == 0x6927000 &&
        result.Plan.ExpandedWadByteLength == 0x6C18800 &&
        result.Plan.OriginalExecutableLba == 53875 &&
        result.Plan.RelocatedExecutableLba == 55382 &&
        result.Plan.ExecutableSectorCount == 204 &&
        result.Plan.NextFileLba == 60000,
    "The physical WAD/SCUS layout changed.");
Require(result.CopiedPayloadSectorCount == 1507 &&
        result.CopiedExecutableSectorCount == 204 &&
        result.RebuiltMetadataSectorCount == 2 &&
        result.RetaggedFileBoundarySectorCount == 2 &&
        result.VerifiedMode2Form1SectorCount == 1714 &&
        result.ChangedRawSectorCount == 1714 &&
        result.ExactRawDiffBoundaryVerified &&
        result.IndependentPayloadReadbackVerified &&
        result.RetailPayloadsPreserved &&
        result.RootRelocationVerified &&
        result.SourceImagePreserved &&
        result.AtomicRenameCompleted,
    "The physical candidate omitted a raw-sector, payload, root, source, or transactional proof.");
Require(result.Plan.PhysicalSectorCopies.Count == 3 &&
        result.Plan.PhysicalSectorCopies[0].SourceLba == 53875 &&
        result.Plan.PhysicalSectorCopies[0].DestinationLba == 55382 &&
        result.Plan.PhysicalSectorCopies[0].SectorCount == 204 &&
        result.Plan.PhysicalSectorCopies[1].SourceLba == 9026 &&
        result.Plan.PhysicalSectorCopies[1].DestinationLba == 53875 &&
        result.Plan.PhysicalSectorCopies[1].SectorCount == 31 &&
        result.Plan.PhysicalSectorCopies[2].SourceLba == 9057 &&
        result.Plan.PhysicalSectorCopies[2].DestinationLba == 53906 &&
        result.Plan.PhysicalSectorCopies[2].SectorCount == 1476,
    "The guarded SCUS-first physical copy order or source/destination ranges changed.");
Require(result.Plan.XaBoundaryRewrites.Count == 2 &&
        result.Plan.XaBoundaryRewrites[0] ==
            new UnusedLevel65PhysicalCloneXaBoundaryRewrite(53874, 0x89, 0x08, 0x81) &&
        result.Plan.XaBoundaryRewrites[1] ==
            new UnusedLevel65PhysicalCloneXaBoundaryRewrite(55381, 0x08, 0x89, 0x81),
    "The guarded WAD XA EOR/EOF boundary moves changed.");
Require(result.Plan.LogicalPatches.Count == 7 &&
        result.Plan.LogicalPatches.Any(patch =>
            patch.Kind == "reserved-level-65-independent-overlay-and-data-directory-row" &&
            patch.AfterHex == "00 70 92 06 00 F8 00 00 00 68 93 06 00 20 2E 00") &&
        result.Plan.LogicalPatches.Any(patch =>
            patch.Kind == "relocate-SCUS-942-28-extent" &&
            patch.LogicalOffset == 0x232 &&
            patch.BeforeHex == "73 D2 00 00 00 00 D2 73" &&
            patch.AfterHex == "56 D8 00 00 00 00 D8 56") &&
        result.Plan.LogicalPatches.Any(patch =>
            patch.Kind == "expand-WAD-WAD-byte-length" &&
            patch.LogicalOffset == 0x2B2 &&
            patch.BeforeHex == "00 70 92 06 06 92 70 00" &&
            patch.AfterHex == "00 88 C1 06 06 C1 88 00"),
    "The physical directory-row or ISO-root exact patch evidence changed.");
string runtimeChecklist = string.Join('\n', result.Plan.RuntimeChecklist);
Require(runtimeChecklist.Contains("memory-card insertion completely", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("Left, then Down", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("Collect exactly one loose gem", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("retail Town Square", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("Gnasty's Loot", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("Sunny Flight", StringComparison.Ordinal) &&
        runtimeChecklist.Contains("Do not select Exit Level or Quit Game", StringComparison.Ordinal),
    "The mandatory no-card, ID65, retail-level, flight, or interaction safety check is missing.");
Require(File.Exists(result.OutputImagePath) && File.Exists(result.OutputCuePath),
    "The physical ID65 BIN/CUE was not published.");
Require(!Directory.EnumerateFiles(outputRoot, ".*.tmp").Any() &&
        !Directory.EnumerateFiles(outputRoot, ".*.bak").Any() &&
        !Directory.EnumerateFiles(outputRoot, ".*.alias.cue").Any(),
    "The transactional exporter left a temporary, backup, or alias CUE behind.");

UnusedLevel65PhysicalCloneCandidateResult repeat =
    await UnusedLevel65PhysicalCloneCandidateExporter.ExportAsync(new(
        sourceImage,
        sourceCue,
        outputPrefix + ".bin",
        outputPrefix + ".cue"));
Require(repeat.OutputImageSha256 == result.OutputImageSha256 &&
        repeat.AtomicRenameCompleted &&
        repeat.ChangedRawSectorCount == 1714 &&
        File.Exists(repeat.OutputImagePath) &&
        File.Exists(repeat.OutputCuePath) &&
        !Directory.EnumerateFiles(outputRoot, ".*.tmp").Any() &&
        !Directory.EnumerateFiles(outputRoot, ".*.bak").Any() &&
        !Directory.EnumerateFiles(outputRoot, ".*.alias.cue").Any(),
    "A deterministic replacement export changed the SHA or left transactional debris.");

string staticProofPath = outputPrefix + "-static-proof.json";
string checklistPath = outputPrefix + "-runtime-checklist.md";
object proof = new
{
    status = "static-proven-runtime-pending",
    runtimeClaim = false,
    generatedAtUtc = result.Plan.GeneratedAtUtc,
    profileId = result.Plan.ProfileId,
    sourceImageSha256 = result.Plan.SourceImageSha256,
    baseAliasProfileId = result.Plan.BaseAliasProfileId,
    baseAliasImageSha256 = result.Plan.BaseAliasImageSha256,
    outputImageSha256 = result.OutputImageSha256,
    levelId = result.Plan.LevelId,
    directoryRows = new
    {
        overlay = result.Plan.OverlayDirectoryIndex,
        data = result.Plan.DataDirectoryIndex
    },
    layout = new
    {
        targetOverlayWadOffset = $"0x{result.Plan.TargetOverlayWadOffset:X}",
        targetDataWadOffset = $"0x{result.Plan.TargetDataWadOffset:X}",
        originalWadByteLength = $"0x{result.Plan.OriginalWadByteLength:X}",
        expandedWadByteLength = $"0x{result.Plan.ExpandedWadByteLength:X}",
        originalExecutableLba = result.Plan.OriginalExecutableLba,
        relocatedExecutableLba = result.Plan.RelocatedExecutableLba,
        result.Plan.ExecutableSectorCount,
        result.Plan.NextFileLba
    },
    result.CopiedPayloadSectorCount,
    result.CopiedExecutableSectorCount,
    result.RebuiltMetadataSectorCount,
    result.RetaggedFileBoundarySectorCount,
    result.VerifiedMode2Form1SectorCount,
    result.ChangedRawSectorCount,
    result.ExactRawDiffBoundaryVerified,
    result.IndependentPayloadReadbackVerified,
    result.RetailPayloadsPreserved,
    result.RootRelocationVerified,
    result.SourceImagePreserved,
    result.AtomicRenameCompleted,
    physicalSectorCopies = result.Plan.PhysicalSectorCopies,
    xaBoundaryRewrites = result.Plan.XaBoundaryRewrites,
    logicalPatches = result.Plan.LogicalPatches,
    limits = new[]
    {
        "This is a disposable ID65 physical-storage discriminator, not an editor feature.",
        "ID65 still displays the placeholder A identity and intentionally reuses Town Square content semantics.",
        "Only physical payload independence, WAD/SCUS extents, and the already runtime-proven ID65 dispatch/classifier recipe are in scope.",
        "Names, totals, portals, music ownership, save ownership, Return Home, and editor workspace integration remain later gates.",
        "DuckStation runtime proof is mandatory before any identity or editor promotion."
    }
};
await File.WriteAllTextAsync(
    staticProofPath,
    JsonSerializer.Serialize(proof, new JsonSerializerOptions { WriteIndented = true }) + "\n",
    Encoding.UTF8);

StringBuilder checklist = new();
checklist.AppendLine("# Unused level 65 / physically independent Town Square — DuckStation checklist");
checklist.AppendLine();
checklist.AppendLine($"- BIN SHA-256: `{result.OutputImageSha256}`");
checklist.AppendLine($"- Profile: `{result.Plan.ProfileId}`");
checklist.AppendLine("- Status: static proof complete; runtime proof pending.");
checklist.AppendLine();
checklist.AppendLine("This candidate keeps the runtime-proven ID65 dispatch and flight-classifier exception, but rows 79/80 now point to a physically separate copy of Town Square's overlay/data. The patched SCUS has also moved to its own guarded extent. It still is not a new authored level: placeholder name, totals, portals, music ownership, saving, and Return Home remain out of scope.");
checklist.AppendLine();
checklist.AppendLine("## Test");
checklist.AppendLine();
for (int index = 0; index < result.Plan.RuntimeChecklist.Count; index++)
    checklist.AppendLine($"{index + 1}. {result.Plan.RuntimeChecklist[index]}");
checklist.AppendLine();
checklist.AppendLine("## Report back");
checklist.AppendLine();
checklist.AppendLine("Please report whether ID65 loaded, movement across several sectors, camera, music, enemies, pause/Inventory, one-gem collection, reset/re-entry, retail Town Square, Gnasty's Loot, and Sunny Flight each passed. Keep memory cards disabled and follow the interaction limits above.");
await File.WriteAllTextAsync(checklistPath, checklist.ToString(), Encoding.UTF8);

Console.WriteLine("PASS: ID65 Town Square payload is physically independent and statically verified; DuckStation runtime proof is pending.");
Console.WriteLine($"CUE: {result.OutputCuePath}");
Console.WriteLine($"BIN: {result.OutputImagePath}");
Console.WriteLine($"Checklist: {checklistPath}");
Console.WriteLine($"Static proof: {staticProofPath}");
Console.WriteLine($"BIN SHA-256: {result.OutputImageSha256}");
Console.WriteLine(
    $"Copied sectors: payload {result.CopiedPayloadSectorCount:N0}, " +
    $"SCUS {result.CopiedExecutableSectorCount:N0}; changed/verified raw sectors: " +
    $"{result.ChangedRawSectorCount:N0}/{result.VerifiedMode2Form1SectorCount:N0}.");

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
