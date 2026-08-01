using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeTerrainTextureRecordRelocationExternalPatch(
    long SourceWadOffset,
    byte[] Before,
    byte[] After,
    string Kind,
    string RuntimeKey,
    string Description);

public sealed record NativeTerrainTextureRecordRelocatedExternalPatch(
    long SourceWadOffset,
    long RelocatedWadOffset,
    int SourceWadEntry,
    int ByteLength,
    byte[] Before,
    byte[] After,
    string BeforeSha256,
    string AfterSha256,
    string Kind,
    string RuntimeKey,
    string Description);

public sealed record NativeTerrainTextureRecordSectorRelocationRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    string WadAnalysisPath,
    LevelDefinition TargetLevel,
    NativeTerrainTextureRecordAppendSourceBinding SourceBinding,
    IReadOnlyList<NativeTerrainTexturePackedAppendRecord> Records,
    IReadOnlyList<NativeTerrainTextureRecordExistingPatch> ExistingLevelDataPatches,
    IReadOnlyList<NativeTerrainTextureRecordRelocationExternalPatch> ExternalPatches,
    NativeTerrainTexturePrivateRecordStagingSourceProof? StagingSourceProof = null,
    NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked);

public sealed record NativeTerrainTextureRecordSectorRelocationPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string SourceImageSha256,
    string TargetLevelKey,
    string TargetLevelName,
    int TargetWadEntry,
    int SectorGrowthBytes,
    int AvailableIsoGrowthBytes,
    int OriginalWadSize,
    int ExpandedWadSize,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    long OriginalTargetEntryWadOffset,
    long RelocatedTargetEntryWadOffset,
    long OriginalTexturePagesWadOffset,
    long RelocatedTexturePagesWadOffset,
    long OriginalLevelDataWadOffset,
    long RelocatedLevelDataWadOffset,
    NativeTerrainTextureRecordAppendPlan Append,
    IReadOnlyList<NativeTerrainTextureRecordRebasedPatchProof> RelocatedLevelDataPatchProofs,
    IReadOnlyList<NativeTerrainTextureRecordRelocatedExternalPatch> RelocatedExternalPatches,
    bool AppendSourceBindingVerified,
    bool ExistingLevelDataPatchesConsumed,
    bool ExternalPatchPreimagesVerified,
    bool RelocationOffsetsVerified,
    bool RequiresTexturePageProof,
    bool RequiresDuckStationRuntimeProof,
    IReadOnlyList<string> Notes)
{
    public NativeTerrainTextureStructuralGrowthPolicy StructuralGrowthPolicy { get; init; } =
        NativeTerrainTextureStructuralGrowthPolicy.NormalChecked;

    public bool StaticResearchOnly =>
        NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
            StructuralGrowthPolicy);
}

public sealed record NativeTerrainTextureRecordSectorRelocationExportResult(
    NativeTerrainTextureRecordSectorRelocationPlan Plan,
    string OutputImagePath,
    string OutputCuePath,
    string OutputImageSha256,
    bool SourceImagePreserved,
    bool RelocatedLevelDataReadbackVerified,
    bool RelocatedExternalPatchReadbackVerified,
    bool RuntimeTargetReadbackVerified,
    bool AtomicRenameCompleted);

/// <summary>
/// Production-neutral structural composer for destinations whose fixed
/// level-data zero tail cannot hold the requested complete 184-byte texture
/// record batch. It reuses <see cref="NativeSkyWadRelocator"/> for checked
/// normal +0x800/+0x1000 WAD/ISO relocation, or explicit static-research
/// aligned growth through +0x2800. It does not duplicate archive-moving logic
/// and it does not claim texture-page/runtime proof for allocator output.
/// </summary>
public static class NativeTerrainTextureRecordSectorRelocationComposer
{
    private const int WadLba = 37;
    private const int NestedArchiveHeaderBytes = 2048;
    private const int TexturePagesSubfileIndex = 0;
    private const int LevelDataSubfileIndex = 1;

    public static NativeTerrainTextureRecordSectorRelocationPlan BuildPlan(
        NativeTerrainTextureRecordSectorRelocationRequest request)
    {
        return BuildCore(request).PublicPlan;
    }

    public static async Task<NativeTerrainTextureRecordSectorRelocationExportResult> ExportAsync(
        NativeTerrainTextureRecordSectorRelocationRequest request,
        CancellationToken cancellationToken = default)
    {
        StructuralBuild build = BuildCore(request);
        NativeTerrainTextureRecordSectorRelocationPlan plan = build.PublicPlan;
        string outputImagePath = Path.GetFullPath(request.OutputPrefix + ".bin");
        string outputCuePath = Path.GetFullPath(request.OutputPrefix + ".cue");
        if (string.Equals(outputImagePath, Path.GetFullPath(request.SourceImagePath), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The sector-relocation output BIN must not overwrite its bound source BIN.");

        string outputDirectory = Path.GetDirectoryName(outputImagePath) ?? ".";
        Directory.CreateDirectory(outputDirectory);
        string temporaryImagePath = outputImagePath + $".{Guid.NewGuid():N}.tmp";
        string temporaryCuePath = outputCuePath + $".{Guid.NewGuid():N}.tmp";
        DeleteStale(outputImagePath);
        DeleteStale(outputCuePath);
        DeleteStale(temporaryImagePath);
        DeleteStale(temporaryCuePath);

        try
        {
            NativeSkyWadRelocator.WriteExpandedImage(
                request.SourceImagePath,
                temporaryImagePath,
                request.WadAnalysisPath,
                build.Relocation,
                [build.Payload]);

            DiscLayout layout = DiscImage.DetectLayout(temporaryImagePath);
            await using (FileStream output = File.Open(
                             temporaryImagePath,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.Read))
            {
                byte[] preparedLevelData = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    WadLba,
                    plan.RelocatedLevelDataWadOffset,
                    plan.Append.Patch.OutputByteLength);
                if (!preparedLevelData.AsSpan(0, plan.Append.Patch.SourceByteLength)
                        .SequenceEqual(plan.Append.Patch.Before) ||
                    preparedLevelData.AsSpan(plan.Append.Patch.SourceByteLength)
                        .IndexOfAnyExcept((byte)0) >= 0)
                {
                    throw new InvalidDataException(
                        "The relocated level-data capacity did not preserve the complete source preimage followed by the checked zero-sector extension.");
                }
                DiscImage.WriteFileBytes(
                    output,
                    layout,
                    WadLba,
                    plan.RelocatedLevelDataWadOffset,
                    plan.Append.Patch.After);

                foreach (NativeTerrainTextureRecordRelocatedExternalPatch patch in plan.RelocatedExternalPatches)
                {
                    byte[] before = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.RelocatedWadOffset,
                        patch.ByteLength);
                    if (!before.SequenceEqual(patch.Before) ||
                        !HashEquals(Sha256(before), patch.BeforeSha256))
                    {
                        throw new InvalidDataException(
                            $"Relocated external patch {patch.Kind}/{patch.RuntimeKey} no longer matches source-bound before bytes at WAD 0x{patch.RelocatedWadOffset:X}.");
                    }
                    DiscImage.WriteFileBytes(output, layout, WadLba, patch.RelocatedWadOffset, patch.After);
                }
                output.Flush(flushToDisk: true);

                byte[] levelDataReadback = DiscImage.ReadFileBytes(
                    output,
                    layout,
                    WadLba,
                    plan.RelocatedLevelDataWadOffset,
                    plan.Append.Patch.OutputByteLength);
                if (!levelDataReadback.SequenceEqual(plan.Append.Patch.After))
                    throw new InvalidDataException("Relocated level-data payload changed while external patches were applied.");
                foreach (NativeTerrainTextureRecordRelocatedExternalPatch patch in plan.RelocatedExternalPatches)
                {
                    byte[] after = DiscImage.ReadFileBytes(
                        output,
                        layout,
                        WadLba,
                        patch.RelocatedWadOffset,
                        patch.ByteLength);
                    if (!after.SequenceEqual(patch.After) || !HashEquals(Sha256(after), patch.AfterSha256))
                    {
                        throw new InvalidDataException(
                            $"Final relocated external patch readback failed for {patch.Kind}/{patch.RuntimeKey}.");
                    }
                }
            }

            NativeTerrainTextureRuntimeControlAudit runtime =
                NativeTerrainTextureRuntimeControlScanner.Inspect(temporaryImagePath, request.TargetLevel);
            int[] appendedIds = plan.Append.ResolvedRecords.Select(record => record.AssignedTextureId).ToArray();
            if (!runtime.Complete || runtime.TextureCount != plan.Append.OutputTextureCount ||
                appendedIds.Any(textureId => !runtime.IsRuntimePersistentTarget(textureId)))
            {
                throw new InvalidDataException("Relocated final image failed appended texture runtime-control readback.");
            }
            IReadOnlyList<TerrainTextureSlot> slots = TerrainPatchExporter.InspectTextureSlots(
                temporaryImagePath,
                request.TargetLevel);
            if (slots.Count != plan.Append.OutputTextureCount ||
                appendedIds.Any(textureId => slots.All(slot => slot.TextureId != textureId)))
            {
                throw new InvalidDataException("Relocated final image failed normal texture-table readback.");
            }

            string cue = DiscImage.BuildCueText(request.SourceCuePath, Path.GetFileName(outputImagePath));
            await File.WriteAllTextAsync(temporaryCuePath, cue, Encoding.ASCII, cancellationToken);
            File.Move(temporaryImagePath, outputImagePath, overwrite: true);
            File.Move(temporaryCuePath, outputCuePath, overwrite: true);

            VerifyFinalReadback(outputImagePath, request.TargetLevel, plan);
            bool sourcePreserved = HashEquals(Sha256File(request.SourceImagePath), plan.SourceImageSha256);
            if (!sourcePreserved)
                throw new InvalidDataException("Sector-relocation export modified its source BIN.");
            return new NativeTerrainTextureRecordSectorRelocationExportResult(
                plan,
                outputImagePath,
                outputCuePath,
                Sha256File(outputImagePath),
                true,
                true,
                true,
                true,
                true);
        }
        catch
        {
            DeleteStale(outputImagePath);
            DeleteStale(outputCuePath);
            throw;
        }
        finally
        {
            DeleteStale(temporaryImagePath);
            DeleteStale(temporaryCuePath);
        }
    }

    private static StructuralBuild BuildCore(NativeTerrainTextureRecordSectorRelocationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.TargetLevel);
        ArgumentNullException.ThrowIfNull(request.SourceBinding);
        ArgumentNullException.ThrowIfNull(request.Records);
        ArgumentNullException.ThrowIfNull(request.ExistingLevelDataPatches);
        ArgumentNullException.ThrowIfNull(request.ExternalPatches);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.WadAnalysisPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputPrefix);
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing retail source image.", request.SourceImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("Missing source-bound WAD analysis.", request.WadAnalysisPath);

        NativeTerrainTextureRecordAppendCapacity capacity =
            NativeTerrainTextureRecordAppendBuilder.InspectCapacityForRecordCount(
                request.SourceImagePath,
                request.TargetLevel,
                request.Records.Count,
                request.StructuralGrowthPolicy,
                request.WadAnalysisPath);
        if (!capacity.WouldRequireTargetEntryGrowth ||
            capacity.RequiredSectorAlignedEntryGrowthBytes <= 0 ||
            !capacity.SectorRelocationPlanVerified)
        {
            throw new InvalidOperationException(
                capacity.CanAppendInsideCurrentSubfile
                    ? $"{request.TargetLevel.DisplayName} does not need sector relocation; use the fixed-subfile append builder."
                    : $"A checked sector relocation is unavailable: {capacity.SectorRelocationFailure}");
        }

        NativeTerrainTextureRecordAppendRequest appendRequest = new(
            request.SourceImagePath,
            request.TargetLevel,
            request.SourceBinding,
            request.Records,
            request.ExistingLevelDataPatches,
            request.StagingSourceProof);
        if (!NativeTerrainTextureRecordAppendBuilder.TryBuildWithOutputCapacityExtension(
                appendRequest,
                capacity.RequiredSectorAlignedEntryGrowthBytes,
                out NativeTerrainTextureRecordAppendPlan? append,
                out string appendFailure) || append == null)
        {
            throw new InvalidOperationException(
                $"Expanded-capacity texture-record append failed: {appendFailure}");
        }
        append = append with
        {
            StructuralGrowthPolicy = request.StructuralGrowthPolicy
        };
        if (append.Patch.IsFixedLength ||
            append.Patch.OutputByteLength - append.Patch.SourceByteLength != capacity.RequiredSectorAlignedEntryGrowthBytes ||
            append.FixedSubfileBoundaryPreserved || append.ArchiveHeadersRequireNoFixup)
        {
            throw new InvalidDataException("Expanded append plan did not declare the exact sector growth and required archive fixups.");
        }

        NativeSkyRelocationPayload payload = new(
            PatchIndex: 0,
            WadLba: WadLba,
            OriginalWadOffset: append.LevelDataWadOffset + append.Patch.SourceByteLength,
            StorageWadEntry: request.TargetLevel.SourceWadEntry,
            ModelBlockOffset: append.Patch.SourceByteLength,
            OriginalLength: 0,
            Bytes: new byte[capacity.RequiredSectorAlignedEntryGrowthBytes],
            SubfileIndex: LevelDataSubfileIndex,
            RequireLengthPrefix: false);
        NativeSkyWadRelocationPlan relocation = NativeSkyWadRelocator.BuildPlan(
            request.SourceImagePath,
            request.WadAnalysisPath,
            [payload]);
        int sectorGrowth = capacity.RequiredSectorAlignedEntryGrowthBytes;
        if (!NativeTerrainTextureRecordAppendBuilder.IsSectorGrowthAuthorized(
                sectorGrowth,
                request.StructuralGrowthPolicy))
        {
            throw new InvalidDataException(
                $"The requested row batch needs unsupported +0x{sectorGrowth:X} structural growth under {request.StructuralGrowthPolicy}; the authorized aligned ceiling is +0x{NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(request.StructuralGrowthPolicy):X}.");
        }
        if (!relocation.Required || relocation.WadGrowthBytes != sectorGrowth ||
            relocation.EntryGrowthBytes.GetValueOrDefault(request.TargetLevel.SourceWadEntry) != sectorGrowth ||
            relocation.RelocatedExecutableLba !=
                relocation.OriginalExecutableLba + (sectorGrowth / 0x800))
        {
            throw new InvalidDataException(
                "WAD relocator did not reproduce the capacity-audited aligned structural-growth plan.");
        }

        ArchiveEntryMap[] wadEntries = LoadWadEntries(request.WadAnalysisPath);
        ArchiveEntryMap targetEntry = wadEntries.Single(entry => entry.Index == request.TargetLevel.SourceWadEntry);
        (int texturePagesRelativeOffset, int texturePagesLength) = ReadNestedSubfile(
            request.SourceImagePath,
            targetEntry.Offset,
            targetEntry.ByteLength,
            TexturePagesSubfileIndex);
        (int levelDataRelativeOffset, int levelDataLength) = ReadNestedSubfile(
            request.SourceImagePath,
            targetEntry.Offset,
            targetEntry.ByteLength,
            LevelDataSubfileIndex);
        if (targetEntry.Offset != append.TargetEntryWadOffset ||
            levelDataLength != append.Patch.SourceByteLength ||
            targetEntry.Offset + levelDataRelativeOffset != append.LevelDataWadOffset)
        {
            throw new InvalidDataException("WAD analysis, nested subfile table, and append source binding disagree.");
        }
        long relocatedTargetEntry = relocation.RelocatedEntryOffsets[request.TargetLevel.SourceWadEntry];
        long relocatedTexturePages = relocatedTargetEntry + texturePagesRelativeOffset;
        long relocatedLevelData = relocatedTargetEntry + levelDataRelativeOffset;

        NativeTerrainTextureRecordRelocatedExternalPatch[] external = MapExternalPatches(
            request,
            append,
            relocation,
            wadEntries,
            targetEntry,
            levelDataRelativeOffset,
            levelDataLength,
            relocatedLevelData);
        NativeTerrainTextureRecordRebasedPatchProof[] relocatedLevelProofs = append.RebasedPatches
            .Select(proof => proof with
            {
                OutputWadOffset = relocatedLevelData + (proof.OutputWadOffset - append.LevelDataWadOffset)
            })
            .ToArray();

        NativeTerrainTextureRecordSectorRelocationPlan publicPlan = new(
            DateTimeOffset.UtcNow,
            Path.GetFullPath(request.SourceImagePath),
            request.SourceBinding.SourceImageSha256,
            request.TargetLevel.Key,
            request.TargetLevel.DisplayName,
            request.TargetLevel.SourceWadEntry,
            sectorGrowth,
            relocation.AvailableGrowthBytes,
            relocation.OriginalWadSize,
            relocation.ExpandedWadSize,
            relocation.OriginalExecutableLba,
            relocation.RelocatedExecutableLba,
            targetEntry.Offset,
            relocatedTargetEntry,
            targetEntry.Offset + texturePagesRelativeOffset,
            relocatedTexturePages,
            append.LevelDataWadOffset,
            relocatedLevelData,
            append,
            relocatedLevelProofs,
            external,
            true,
            append.RebasedPatches.Count == request.ExistingLevelDataPatches.Count,
            true,
            true,
            true,
            true,
            [
                $"The target level-data subfile grows by {sectorGrowth / 0x800} aligned sector(s) (0x{sectorGrowth:X}); only {append.GrowthByteCount} bytes become texture-record data and the remaining added capacity stays zero.",
                "NativeSkyWadRelocator owns all nested-subfile, later-WAD-entry, executable, and ISO root-directory relocation/fixups.",
                $"Texture-pages base remaps from WAD 0x{targetEntry.Offset + texturePagesRelativeOffset:X} to 0x{relocatedTexturePages:X}; {external.Length} external allocator patch(es) were source-bound and remapped.",
                NativeTerrainTextureRecordAppendBuilder.IsStaticResearchOnly(
                    request.StructuralGrowthPolicy)
                    ? "The extended structural-growth authorization is static-research-only and cannot authorize normal Create BIN without separate runtime evidence."
                    : $"Structural growth policy {request.StructuralGrowthPolicy} authorizes checked aligned growth through +0x{NativeTerrainTextureRecordAppendBuilder.GetMaximumSectorGrowthBytes(request.StructuralGrowthPolicy):X}.",
                "This structural plan still requires the supplying page allocator's protected-storage/logical-pixel proof and DuckStation runtime evidence before editor promotion."
            ])
        {
            StructuralGrowthPolicy = request.StructuralGrowthPolicy
        };
        return new StructuralBuild(publicPlan, relocation, payload);
    }

    private static NativeTerrainTextureRecordRelocatedExternalPatch[] MapExternalPatches(
        NativeTerrainTextureRecordSectorRelocationRequest request,
        NativeTerrainTextureRecordAppendPlan append,
        NativeSkyWadRelocationPlan relocation,
        IReadOnlyList<ArchiveEntryMap> wadEntries,
        ArchiveEntryMap targetEntry,
        int levelDataRelativeOffset,
        int levelDataLength,
        long relocatedLevelData)
    {
        DiscLayout layout = DiscImage.DetectLayout(request.SourceImagePath);
        using FileStream source = File.OpenRead(request.SourceImagePath);
        List<NativeTerrainTextureRecordRelocatedExternalPatch> mapped = [];
        foreach (NativeTerrainTextureRecordRelocationExternalPatch patch in request.ExternalPatches)
        {
            if (patch.Before == null || patch.After == null || patch.Before.Length == 0 || patch.Before.Length != patch.After.Length)
                throw new InvalidDataException($"External patch {patch.Kind}/{patch.RuntimeKey} must have equal non-empty before/after bytes.");
            ArchiveEntryMap entry = wadEntries.SingleOrDefault(candidate =>
                    patch.SourceWadOffset >= candidate.Offset &&
                    patch.SourceWadOffset + patch.Before.Length <= candidate.EndExclusive)
                ?? throw new InvalidDataException($"External patch at WAD 0x{patch.SourceWadOffset:X} is outside one analyzed WAD entry.");
            long relative = patch.SourceWadOffset - entry.Offset;
            if (relative < NestedArchiveHeaderBytes)
            {
                throw new InvalidDataException(
                    $"External patch {patch.Kind}/{patch.RuntimeKey} enters WAD entry {entry.Index}'s structural nested-subfile header; the relocation composer owns archive headers.");
            }
            if (entry.Index == targetEntry.Index &&
                RangesOverlap(
                    relative,
                    relative + patch.Before.Length,
                    levelDataRelativeOffset,
                    levelDataRelativeOffset + levelDataLength))
            {
                throw new InvalidDataException(
                    $"External patch {patch.Kind}/{patch.RuntimeKey} overlaps level data and must be supplied to ExistingLevelDataPatches for atomic rebasing.");
            }
            long internalShift = entry.Index == targetEntry.Index && relative >= levelDataRelativeOffset + levelDataLength
                ? relocation.EntryGrowthBytes.GetValueOrDefault(targetEntry.Index)
                : 0;
            long relocated = checked(relocation.RelocatedEntryOffsets[entry.Index] + relative + internalShift);
            if (RangesOverlap(
                    relocated,
                    relocated + patch.Before.Length,
                    relocatedLevelData,
                    relocatedLevelData + append.Patch.OutputByteLength))
            {
                throw new InvalidDataException($"Relocated external patch {patch.Kind}/{patch.RuntimeKey} overlaps the expanded level-data payload.");
            }
            byte[] sourceBefore = DiscImage.ReadFileBytes(
                source,
                layout,
                WadLba,
                patch.SourceWadOffset,
                patch.Before.Length);
            if (!sourceBefore.SequenceEqual(patch.Before))
                throw new InvalidDataException($"External patch {patch.Kind}/{patch.RuntimeKey} no longer matches source bytes.");
            mapped.Add(new NativeTerrainTextureRecordRelocatedExternalPatch(
                patch.SourceWadOffset,
                relocated,
                entry.Index,
                patch.Before.Length,
                patch.Before.ToArray(),
                patch.After.ToArray(),
                Sha256(patch.Before),
                Sha256(patch.After),
                patch.Kind,
                patch.RuntimeKey,
                patch.Description));
        }

        NativeTerrainTextureRecordRelocatedExternalPatch[] ordered = mapped
            .OrderBy(patch => patch.RelocatedWadOffset)
            .ToArray();
        for (int index = 1; index < ordered.Length; index++)
        {
            NativeTerrainTextureRecordRelocatedExternalPatch previous = ordered[index - 1];
            NativeTerrainTextureRecordRelocatedExternalPatch current = ordered[index];
            if (RangesOverlap(
                    previous.RelocatedWadOffset,
                    previous.RelocatedWadOffset + previous.ByteLength,
                    current.RelocatedWadOffset,
                    current.RelocatedWadOffset + current.ByteLength))
            {
                throw new InvalidDataException(
                    $"Relocated external patches {previous.Kind}/{previous.RuntimeKey} and {current.Kind}/{current.RuntimeKey} overlap.");
            }
        }
        return ordered;
    }

    private static void VerifyFinalReadback(
        string outputImagePath,
        LevelDefinition level,
        NativeTerrainTextureRecordSectorRelocationPlan plan)
    {
        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        using FileStream output = File.OpenRead(outputImagePath);
        byte[] levelData = DiscImage.ReadFileBytes(
            output,
            layout,
            WadLba,
            plan.RelocatedLevelDataWadOffset,
            plan.Append.Patch.OutputByteLength);
        if (!levelData.SequenceEqual(plan.Append.Patch.After))
            throw new InvalidDataException("Final renamed BIN failed relocated level-data readback.");
        foreach (NativeTerrainTextureRecordRelocatedExternalPatch patch in plan.RelocatedExternalPatches)
        {
            byte[] bytes = DiscImage.ReadFileBytes(
                output,
                layout,
                WadLba,
                patch.RelocatedWadOffset,
                patch.ByteLength);
            if (!bytes.SequenceEqual(patch.After))
                throw new InvalidDataException($"Final renamed BIN failed {patch.Kind}/{patch.RuntimeKey} readback.");
        }
        NativeTerrainTextureRuntimeControlAudit runtime =
            NativeTerrainTextureRuntimeControlScanner.Inspect(outputImagePath, level);
        if (!runtime.Complete || runtime.TextureCount != plan.Append.OutputTextureCount ||
            plan.Append.ResolvedRecords.Any(record => !runtime.IsRuntimePersistentTarget(record.AssignedTextureId)))
        {
            throw new InvalidDataException("Final renamed BIN failed runtime-control readback.");
        }
    }

    private static (int Offset, int ByteLength) ReadNestedSubfile(
        string sourceImagePath,
        long targetEntryOffset,
        int targetEntryLength,
        int subfileIndex)
    {
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream source = File.OpenRead(sourceImagePath);
        byte[] slot = DiscImage.ReadFileBytes(
            source,
            layout,
            WadLba,
            targetEntryOffset + (subfileIndex * 8L),
            8);
        int offset = checked((int)ReadUInt32(slot, 0));
        int length = checked((int)ReadUInt32(slot, 4));
        if (offset <= 0 || length <= 0 || (long)offset + length > targetEntryLength)
            throw new InvalidDataException($"Target WAD entry has no valid nested subfile {subfileIndex}.");
        return (offset, length);
    }

    private static ArchiveEntryMap[] LoadWadEntries(string wadAnalysisPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(wadAnalysisPath));
        ArchiveEntryMap[] entries = document.RootElement.GetProperty("entries")
            .EnumerateArray()
            .Select(element => new ArchiveEntryMap(
                JsonValue.GetInt32(element, "index", -1),
                JsonValue.GetInt64(element, "offset", -1),
                JsonValue.GetInt32(element, "size", -1)))
            .Where(entry => entry.Index >= 0 && entry.Offset >= 0 && entry.ByteLength > 0)
            .OrderBy(entry => entry.Offset)
            .ToArray();
        if (entries.Length == 0)
            throw new InvalidDataException("WAD analysis contains no valid entries.");
        return entries;
    }

    private static bool RangesOverlap(long firstStart, long firstEnd, long secondStart, long secondEnd) =>
        firstStart < secondEnd && secondStart < firstEnd;

    private static uint ReadUInt32(byte[] bytes, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes));

    private static string Sha256File(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static bool HashEquals(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

    private static void DeleteStale(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private sealed record ArchiveEntryMap(int Index, long Offset, int ByteLength)
    {
        public long EndExclusive => checked(Offset + ByteLength);
    }

    private sealed record StructuralBuild(
        NativeTerrainTextureRecordSectorRelocationPlan PublicPlan,
        NativeSkyWadRelocationPlan Relocation,
        NativeSkyRelocationPayload Payload);
}
