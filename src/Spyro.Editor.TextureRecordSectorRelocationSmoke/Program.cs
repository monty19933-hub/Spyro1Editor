using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Levels;

const int WadLba = 37;
const int DonorTextureId = 17;

string workspaceRoot = args.Length > 0 ? Path.GetFullPath(args[0]) : Directory.GetCurrentDirectory();
string sourceImagePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).bin");
string sourceCuePath = Path.Combine(workspaceRoot, "Spyro the Dragon (USA).cue");
string outputRoot = Path.Combine(workspaceRoot, "_local", "smoke", "texture-record-sector-relocation");
Directory.CreateDirectory(outputRoot);
foreach (string stale in Directory.EnumerateFiles(outputRoot))
    File.Delete(stale);
string wadAnalysisPath = Path.Combine(outputRoot, "source-bound-wad-analysis.json");
await WadAnalysisBuilder.BuildAsync(sourceImagePath, wadAnalysisPath);
WadAnalysisCompatibilityResult freshCompatibility =
    WadAnalysisBuilder.CheckSourceCompatibility(sourceImagePath, wadAnalysisPath);
Assert(freshCompatibility.Compatible && freshCompatibility.EntryCount > 0,
    $"A freshly generated WAD analysis did not bind to its source: {freshCompatibility.Failure}");
string compatibleHashBefore = Sha256File(wadAnalysisPath);
DateTime compatibleTimestampBefore = File.GetLastWriteTimeUtc(wadAnalysisPath);
WadAnalysisEnsureResult reusedAnalysis = await WadAnalysisBuilder.EnsureCompatibleAsync(
    sourceImagePath,
    wadAnalysisPath);
Assert(reusedAnalysis.ReusedCompatibleCache &&
       Sha256File(wadAnalysisPath) == compatibleHashBefore &&
       File.GetLastWriteTimeUtc(wadAnalysisPath) == compatibleTimestampBefore,
    "A source-compatible WAD analysis was rewritten instead of reused byte-for-byte.");

string refreshAnalysisPath = Path.Combine(outputRoot, "stale-cache-refresh-analysis.json");
JsonNode refreshFixture = JsonNode.Parse(await File.ReadAllTextAsync(wadAnalysisPath))
    ?? throw new InvalidDataException("Could not parse the WAD refresh fixture.");
JsonObject[] refreshEntries = refreshFixture["entries"]?.AsArray()
    .Select(node => node?.AsObject() ?? throw new InvalidDataException("The WAD refresh fixture contains a non-object entry."))
    .OrderBy(entry => entry["offset"]!.GetValue<long>())
    .ToArray()
    ?? throw new InvalidDataException("The WAD refresh fixture has no entries array.");
int refreshBoundary = Enumerable.Range(0, refreshEntries.Length - 1)
    .First(index =>
        refreshEntries[index]["offset"]!.GetValue<long>() + refreshEntries[index]["size"]!.GetValue<int>() ==
            refreshEntries[index + 1]["offset"]!.GetValue<long>() &&
        refreshEntries[index + 1]["size"]!.GetValue<int>() > 0x800);
refreshEntries[refreshBoundary]["size"] =
    checked(refreshEntries[refreshBoundary]["size"]!.GetValue<int>() + 0x800);
refreshEntries[refreshBoundary + 1]["offset"] =
    checked(refreshEntries[refreshBoundary + 1]["offset"]!.GetValue<long>() + 0x800);
refreshEntries[refreshBoundary + 1]["size"] =
    checked(refreshEntries[refreshBoundary + 1]["size"]!.GetValue<int>() - 0x800);
await File.WriteAllTextAsync(
    refreshAnalysisPath,
    refreshFixture.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
WadAnalysisCompatibilityResult staleCompatibility =
    WadAnalysisBuilder.CheckSourceCompatibility(sourceImagePath, refreshAnalysisPath);
Assert(!staleCompatibility.Compatible &&
       staleCompatibility.Failure.Contains("live archive header", StringComparison.OrdinalIgnoreCase),
    "A same-size repartitioned cache was not rejected by the cheap source-compatibility check.");
string wrongExtentAnalysisPath = Path.Combine(outputRoot, "wrong-extent-analysis.json");
JsonNode wrongExtentFixture = JsonNode.Parse(await File.ReadAllTextAsync(wadAnalysisPath))
    ?? throw new InvalidDataException("Could not parse the wrong-extent WAD fixture.");
JsonObject wrongExtentWad = wrongExtentFixture["wad"]?.AsObject()
    ?? throw new InvalidDataException("The wrong-extent WAD fixture has no WAD object.");
wrongExtentWad["lba"] = checked(wrongExtentWad["lba"]!.GetValue<int>() + 1);
await File.WriteAllTextAsync(
    wrongExtentAnalysisPath,
    wrongExtentFixture.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
WadAnalysisCompatibilityResult wrongExtentCompatibility =
    WadAnalysisBuilder.CheckSourceCompatibility(sourceImagePath, wrongExtentAnalysisPath);
Assert(!wrongExtentCompatibility.Compatible &&
       wrongExtentCompatibility.Failure.Contains("extent", StringComparison.OrdinalIgnoreCase),
    "A cached analysis bound to a different WAD LBA was not rejected.");

string failedRefreshPath = Path.Combine(outputRoot, "failed-refresh-must-preserve-analysis.json");
File.Copy(refreshAnalysisPath, failedRefreshPath, overwrite: true);
string failedRefreshHash = Sha256File(failedRefreshPath);
bool failedRefreshRejected = false;
try
{
    await WadAnalysisBuilder.EnsureCompatibleAsync(
        Path.Combine(outputRoot, "missing-source.bin"),
        failedRefreshPath);
}
catch (FileNotFoundException)
{
    failedRefreshRejected = true;
}
Assert(failedRefreshRejected &&
       Sha256File(failedRefreshPath) == failedRefreshHash &&
       !Directory.EnumerateFiles(outputRoot, $".{Path.GetFileName(failedRefreshPath)}.*.tmp").Any(),
    "A failed atomic cache rebuild changed the existing analysis or left a partial temporary file.");

string staleCacheHash = Sha256File(refreshAnalysisPath);
WadAnalysisEnsureResult refreshedAnalysis = await WadAnalysisBuilder.EnsureCompatibleAsync(
    sourceImagePath,
    refreshAnalysisPath);
WadAnalysisCompatibilityResult refreshedCompatibility =
    WadAnalysisBuilder.CheckSourceCompatibility(sourceImagePath, refreshAnalysisPath);
Assert(!refreshedAnalysis.ReusedCompatibleCache &&
       refreshedCompatibility.Compatible &&
       Sha256File(refreshAnalysisPath) != staleCacheHash &&
       !Directory.EnumerateFiles(outputRoot, $".{Path.GetFileName(refreshAnalysisPath)}.*.tmp").Any(),
    $"An incompatible cache was not regenerated and installed atomically: {refreshedCompatibility.Failure}");
string refreshedHash = Sha256File(refreshAnalysisPath);
WadAnalysisEnsureResult reusedRefreshedAnalysis = await WadAnalysisBuilder.EnsureCompatibleAsync(
    sourceImagePath,
    refreshAnalysisPath);
Assert(reusedRefreshedAnalysis.ReusedCompatibleCache && Sha256File(refreshAnalysisPath) == refreshedHash,
    "A refreshed compatible cache was not reused on the next request.");

string sourceSha256 = Sha256File(sourceImagePath);
long sourceLength = new FileInfo(sourceImagePath).Length;
LevelCatalog catalog = LevelCatalog.Load(workspaceRoot);
LevelDefinition artisans = catalog.FindByKey("artisans")
    ?? throw new InvalidOperationException("Artisans is missing from the level catalog.");
LevelDefinition stoneHill = catalog.FindByKey("stonehill")
    ?? throw new InvalidOperationException("Stone Hill is missing from the level catalog.");
LevelDefinition gnastysWorld = catalog.FindByKey("gnastysworld")
    ?? throw new InvalidOperationException("Gnasty's World is missing from the level catalog.");

// Use the independent fixed-size research path only to extract one exact donor
// row pair. The structural composer below targets Stone Hill, whose level-data
// subfile cannot fit another record without WAD/ISO relocation.
bool fixtureBuilt = NativeTerrainTextureRecordAppendResearch.TryBuild(
    new NativeTerrainTextureRecordAppendResearchRequest(
        sourceImagePath,
        sourceCuePath,
        artisans,
        gnastysWorld,
        DonorTextureId),
    out NativeTerrainTextureRecordAppendResearchPlan? fixtureCandidate,
    out string fixtureFailure);
Assert(fixtureBuilt && fixtureCandidate != null, $"Packed donor-row fixture failed: {fixtureFailure}");
NativeTerrainTextureRecordAppendResearchPlan fixture = fixtureCandidate!;
int donorLowOffset = 8 + (fixture.SourceTextureCount * NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes);
int donorHighOffset = fixture.OutputTextureComponentByteLength - NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes;
byte[] donorLow = fixture.OutputLevelData
    .AsSpan(donorLowOffset, NativeTerrainTextureRecordAppendBuilder.LowDetailRecordBytes)
    .ToArray();
byte[] donorHigh = fixture.OutputLevelData
    .AsSpan(donorHighOffset, NativeTerrainTextureRecordAppendBuilder.HighDetailRecordBytes)
    .ToArray();

NativeTerrainTextureRecordAppendSourceBinding binding =
    NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(sourceImagePath, stoneHill);
NativeTerrainTexturePackedAppendRecord record = new(
    StableEditId: "sector-relocation-smoke:stonehill:gnastysworld:17",
    DonorLevelKey: gnastysWorld.Key,
    DonorWadEntry: gnastysWorld.SourceWadEntry,
    DonorTextureId: DonorTextureId,
    MaterialTemplateTextureId: 0,
    LowDetailRow: donorLow,
    HighDetailRow: donorHigh,
    ExpectedLowDetailSha256: Sha256Bytes(donorLow),
    ExpectedHighDetailSha256: Sha256Bytes(donorHigh));

string outputPrefix = Path.Combine(outputRoot, "Stone-Hill-Append-One-Sector-STRUCTURAL-DO-NOT-RUN");
NativeTerrainTextureRecordSectorRelocationRequest request = new(
    sourceImagePath,
    sourceCuePath,
    outputPrefix,
    wadAnalysisPath,
    stoneHill,
    binding,
    [record],
    Array.Empty<NativeTerrainTextureRecordExistingPatch>(),
    Array.Empty<NativeTerrainTextureRecordRelocationExternalPatch>());

NativeTerrainTextureRecordSectorRelocationPlan initial =
    NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(request);
NativeTerrainTextureRecordAppendCapacity stoneHillCapacity =
    NativeTerrainTextureRecordAppendBuilder.InspectCapacity(sourceImagePath, stoneHill, wadAnalysisPath);
Assert(initial.SectorGrowthBytes == 0x800 &&
       initial.ExpandedWadSize == initial.OriginalWadSize + 0x800 &&
       initial.RelocatedExecutableLba == initial.OriginalExecutableLba + 1,
    "The structural composer did not plan exact one-sector WAD/executable relocation.");
Assert(initial.Append.Patch.SourceByteLength + 0x800 == initial.Append.Patch.OutputByteLength &&
       initial.Append.OutputTextureCount == initial.Append.SourceTextureCount + 1 &&
       !initial.Append.Patch.IsFixedLength &&
       !initial.Append.FixedSubfileBoundaryPreserved &&
       !initial.Append.ArchiveHeadersRequireNoFixup,
    "The append plan did not expose its expanded subfile boundary and required archive fixups.");
Assert(initial.OriginalTexturePagesWadOffset == initial.RelocatedTexturePagesWadOffset &&
       initial.OriginalLevelDataWadOffset == initial.RelocatedLevelDataWadOffset,
    "Stone Hill's target-entry base or pre-growth nested subfile bases unexpectedly moved.");

// Preserve the same outer WAD size and a fully packed JSON layout while moving
// one later boundary. The structural writer must compare every analyzed entry
// to the live WAD header rather than trusting total-size/target-only checks.
string staleAnalysisPath = Path.Combine(outputRoot, "stale-repartitioned-wad-analysis.json");
JsonNode staleAnalysis = JsonNode.Parse(await File.ReadAllTextAsync(wadAnalysisPath))
    ?? throw new InvalidDataException("Could not parse the WAD analysis fixture.");
JsonArray staleEntries = staleAnalysis["entries"]?.AsArray()
    ?? throw new InvalidDataException("The WAD analysis fixture has no entries array.");
JsonObject[] laterEntries = staleEntries
    .Select(node => node?.AsObject() ?? throw new InvalidDataException("The WAD analysis contains a non-object entry."))
    .Where(entry => entry["offset"]!.GetValue<long>() > initial.OriginalTargetEntryWadOffset)
    .OrderBy(entry => entry["offset"]!.GetValue<long>())
    .Take(2)
    .ToArray();
Assert(laterEntries.Length == 2 && laterEntries[1]["size"]!.GetValue<int>() > 0x800,
    "The WAD analysis fixture has no safe later boundary pair for the stale-analysis regression.");
laterEntries[0]["size"] = checked(laterEntries[0]["size"]!.GetValue<int>() + 0x800);
laterEntries[1]["offset"] = checked(laterEntries[1]["offset"]!.GetValue<long>() + 0x800);
laterEntries[1]["size"] = checked(laterEntries[1]["size"]!.GetValue<int>() - 0x800);
await File.WriteAllTextAsync(
    staleAnalysisPath,
    staleAnalysis.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
bool staleAnalysisRejected = false;
try
{
    NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(request with
    {
        WadAnalysisPath = staleAnalysisPath
    });
}
catch (Exception ex) when (
    (ex is InvalidDataException or InvalidOperationException) &&
    ex.Message.Contains("live WAD archive header", StringComparison.OrdinalIgnoreCase))
{
    staleAnalysisRejected = true;
}
Assert(staleAnalysisRejected,
    "A same-size but repartitioned WAD analysis was not rejected against every live archive-header boundary.");
string staleOutputPrefix = Path.Combine(outputRoot, "stale-analysis-must-not-write");
bool staleExportRejected = false;
try
{
    await NativeTerrainTextureRecordSectorRelocationComposer.ExportAsync(request with
    {
        OutputPrefix = staleOutputPrefix,
        WadAnalysisPath = staleAnalysisPath
    });
}
catch (Exception ex) when (
    (ex is InvalidDataException or InvalidOperationException) &&
    ex.Message.Contains("live WAD archive header", StringComparison.OrdinalIgnoreCase))
{
    staleExportRejected = true;
}
Assert(staleExportRejected &&
       !File.Exists(staleOutputPrefix + ".bin") &&
       !File.Exists(staleOutputPrefix + ".cue"),
    "A stale/repartitioned WAD analysis was not rejected before any output BIN/CUE was written.");

// Exercise the external allocator contract with a source-bound byte inside the
// texture-pages subfile. The artifact is deliberately labeled DO-NOT-RUN: this
// tests mapping, write ordering, and final readback, not visual page allocation.
long sourcePagePatchOffset = initial.OriginalTexturePagesWadOffset + 0x100;
byte sourcePageByte = ReadWadBytes(sourceImagePath, sourcePagePatchOffset, 1)[0];
byte changedPageByte = (byte)(sourcePageByte ^ 0x01);
NativeTerrainTextureRecordRelocationExternalPatch pagePatch = new(
    sourcePagePatchOffset,
    [sourcePageByte],
    [changedPageByte],
    "texture-page-smoke",
    "stonehill-page-byte",
    "One-byte source-bound page patch used only to prove relocated external patch composition and readback.");
Assert(stoneHillCapacity.FollowingSubfileIndex.HasValue &&
       stoneHillCapacity.FollowingSubfileRelativeOffset > 0,
    "Stone Hill no longer exposes a later nested subfile for the relocation smoke.");
long laterNestedPatchOffset = initial.OriginalTargetEntryWadOffset +
    stoneHillCapacity.FollowingSubfileRelativeOffset + 0x100;
byte laterNestedByte = ReadWadBytes(sourceImagePath, laterNestedPatchOffset, 1)[0];
byte changedLaterNestedByte = (byte)(laterNestedByte ^ 0x01);
NativeTerrainTextureRecordRelocationExternalPatch laterNestedPatch = new(
    laterNestedPatchOffset,
    [laterNestedByte],
    [changedLaterNestedByte],
    "later-nested-subfile-smoke",
    "later-nested-byte",
    "One-byte source-bound patch used only to prove later nested subfiles shift with the expanded level-data subfile.");
long nextEntryOffset = FindNextWadEntryOffset(wadAnalysisPath, initial.OriginalTargetEntryWadOffset);
long laterEntryPatchOffset = nextEntryOffset + 0x900;
byte laterEntryByte = ReadWadBytes(sourceImagePath, laterEntryPatchOffset, 1)[0];
byte changedLaterEntryByte = (byte)(laterEntryByte ^ 0x01);
NativeTerrainTextureRecordRelocationExternalPatch laterEntryPatch = new(
    laterEntryPatchOffset,
    [laterEntryByte],
    [changedLaterEntryByte],
    "later-wad-entry-smoke",
    "later-entry-byte",
    "One-byte source-bound patch used only to prove later WAD entries shift with the expanded target entry.");
request = request with { ExternalPatches = [pagePatch, laterNestedPatch, laterEntryPatch] };

NativeTerrainTextureRecordSectorRelocationPlan plan =
    NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(request);
NativeTerrainTextureRecordRelocatedExternalPatch mappedPagePatch = plan.RelocatedExternalPatches
    .Single(patch => patch.RuntimeKey == pagePatch.RuntimeKey);
NativeTerrainTextureRecordRelocatedExternalPatch mappedLaterEntryPatch = plan.RelocatedExternalPatches
    .Single(patch => patch.RuntimeKey == laterEntryPatch.RuntimeKey);
NativeTerrainTextureRecordRelocatedExternalPatch mappedLaterNestedPatch = plan.RelocatedExternalPatches
    .Single(patch => patch.RuntimeKey == laterNestedPatch.RuntimeKey);
Assert(plan.RelocatedExternalPatches.Count == 3 &&
       mappedPagePatch.SourceWadOffset == sourcePagePatchOffset &&
       mappedPagePatch.RelocatedWadOffset == plan.RelocatedTexturePagesWadOffset + 0x100 &&
       mappedLaterNestedPatch.RelocatedWadOffset == laterNestedPatchOffset + 0x800 &&
       mappedLaterEntryPatch.RelocatedWadOffset == laterEntryPatchOffset + 0x800 &&
       plan.ExternalPatchPreimagesVerified &&
       plan.RelocationOffsetsVerified &&
       plan.RequiresTexturePageProof &&
       plan.RequiresDuckStationRuntimeProof,
    "The external page patch was not source-bound, remapped, or retained behind runtime proof gates.");

bool staleExternalRejected = false;
try
{
    NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(request with
    {
        ExternalPatches =
        [
            pagePatch with { Before = [(byte)(sourcePageByte ^ 0x80)] },
            laterNestedPatch,
            laterEntryPatch
        ]
    });
}
catch (InvalidDataException ex) when (ex.Message.Contains("source bytes", StringComparison.OrdinalIgnoreCase))
{
    staleExternalRejected = true;
}
Assert(staleExternalRejected, "A stale external page-patch preimage was not rejected before export.");

bool structuralHeaderRejected = false;
try
{
    long headerOffset = initial.OriginalTargetEntryWadOffset + 8;
    byte headerByte = ReadWadBytes(sourceImagePath, headerOffset, 1)[0];
    NativeTerrainTextureRecordSectorRelocationComposer.BuildPlan(request with
    {
        ExternalPatches =
        [
            new NativeTerrainTextureRecordRelocationExternalPatch(
                headerOffset,
                [headerByte],
                [(byte)(headerByte ^ 0x01)],
                "forbidden-structural-header-smoke",
                "target-entry-header",
                "Must be rejected because the relocator owns nested-subfile headers.")
        ]
    });
}
catch (InvalidDataException ex) when (ex.Message.Contains("structural", StringComparison.OrdinalIgnoreCase))
{
    structuralHeaderRejected = true;
}
Assert(structuralHeaderRejected, "An external patch was allowed to overwrite a relocator-owned nested-subfile header.");

NativeTerrainTextureRecordSectorRelocationExportResult exported =
    await NativeTerrainTextureRecordSectorRelocationComposer.ExportAsync(request);
Assert(File.Exists(exported.OutputImagePath) &&
       File.Exists(exported.OutputCuePath) &&
       exported.SourceImagePreserved &&
       exported.RelocatedLevelDataReadbackVerified &&
       exported.RelocatedExternalPatchReadbackVerified &&
       exported.RuntimeTargetReadbackVerified &&
       exported.AtomicRenameCompleted,
    "Sector-relocation export omitted an atomic write/readback proof.");
Assert(ReadWadBytes(exported.OutputImagePath, mappedPagePatch.RelocatedWadOffset, 1)[0] == changedPageByte,
    "The final BIN did not retain the relocated external page patch.");
Assert(ReadWadBytes(exported.OutputImagePath, mappedLaterNestedPatch.RelocatedWadOffset, 1)[0] == changedLaterNestedByte,
    "The final BIN did not retain the external patch shifted with a later nested subfile.");
Assert(ReadWadBytes(exported.OutputImagePath, mappedLaterEntryPatch.RelocatedWadOffset, 1)[0] == changedLaterEntryByte,
    "The final BIN did not retain the external patch shifted with a later WAD entry.");
IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(
    exported.OutputImagePath,
    stoneHill);
Assert(slots.Count == binding.ExpectedSourceTextureCount + 1 &&
       slots[^1].TextureId == binding.ExpectedSourceTextureCount,
    "The ordinary texture-table decoder did not read the appended Stone Hill record from the final BIN.");
Assert(Sha256File(sourceImagePath).Equals(sourceSha256, StringComparison.OrdinalIgnoreCase) &&
       new FileInfo(sourceImagePath).Length == sourceLength,
    "Sector-relocation smoke modified the retail source BIN.");

Console.WriteLine("Native terrain texture record sector relocation smoke passed.");
Console.WriteLine($"Stone Hill: T{binding.ExpectedSourceTextureCount} appended; level data {plan.Append.Patch.SourceByteLength:N0}->{plan.Append.Patch.OutputByteLength:N0} bytes.");
Console.WriteLine($"WAD: {plan.OriginalWadSize:N0}->{plan.ExpandedWadSize:N0}; executable LBA {plan.OriginalExecutableLba}->{plan.RelocatedExecutableLba}.");
Console.WriteLine($"Texture pages: 0x{plan.OriginalTexturePagesWadOffset:X}->0x{plan.RelocatedTexturePagesWadOffset:X}; external patch read back at 0x{mappedPagePatch.RelocatedWadOffset:X}.");
Console.WriteLine($"Later nested patch: 0x{mappedLaterNestedPatch.SourceWadOffset:X}->0x{mappedLaterNestedPatch.RelocatedWadOffset:X}.");
Console.WriteLine($"Later WAD patch: 0x{mappedLaterEntryPatch.SourceWadOffset:X}->0x{mappedLaterEntryPatch.RelocatedWadOffset:X}.");
Console.WriteLine($"Atomic final BIN: {exported.OutputImagePath}");
Console.WriteLine("Source-bound WAD cache: compatible reuse and stale same-size atomic refresh passed.");
Console.WriteLine("Artifact remains STRUCTURAL-DO-NOT-RUN until a real allocator supplies protected page storage and DuckStation evidence.");

static byte[] ReadWadBytes(string imagePath, long wadOffset, int length)
{
    (int sectorSize, int userOffset) = DetectLayout(imagePath);
    byte[] result = new byte[length];
    using FileStream stream = File.OpenRead(imagePath);
    int written = 0;
    long fileOffset = wadOffset;
    while (written < length)
    {
        int sectorOffset = checked((int)(fileOffset % 2048));
        int sector = checked(WadLba + (int)(fileOffset / 2048));
        int count = Math.Min(length - written, 2048 - sectorOffset);
        stream.Position = ((long)sector * sectorSize) + userOffset + sectorOffset;
        stream.ReadExactly(result.AsSpan(written, count));
        written += count;
        fileOffset += count;
    }
    return result;
}

static (int SectorSize, int UserOffset) DetectLayout(string imagePath)
{
    using FileStream stream = File.OpenRead(imagePath);
    Span<byte> signature = stackalloc byte[6];
    foreach ((int sectorSize, int userOffset) in new[] { (2048, 0), (2352, 24), (2336, 8) })
    {
        long offset = (16L * sectorSize) + userOffset;
        if (offset + 6 > stream.Length)
            continue;
        stream.Position = offset;
        stream.ReadExactly(signature);
        if (signature[0] == 1 && Encoding.ASCII.GetString(signature[1..]) == "CD001")
            return (sectorSize, userOffset);
    }
    throw new InvalidDataException("Could not detect the smoke image's ISO9660 sector layout.");
}

static long FindNextWadEntryOffset(string wadAnalysisPath, long targetEntryOffset)
{
    using JsonDocument document = JsonDocument.Parse(File.ReadAllText(wadAnalysisPath));
    return document.RootElement.GetProperty("entries")
        .EnumerateArray()
        .Select(entry => entry.GetProperty("offset").GetInt64())
        .Where(offset => offset > targetEntryOffset)
        .Order()
        .First();
}

static void Assert(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream));
}

static string Sha256Bytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes));
