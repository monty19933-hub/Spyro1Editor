using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed record CrossLevelActorPackageExpansionCandidateRequest(
    string RetailImagePath,
    string RetailCuePath,
    string BaseCandidateImagePath,
    string WadAnalysisPath,
    string OutputPrefix,
    string Label,
    int TargetWadEntry,
    long DonorPackageWadOffset,
    int PackageLength,
    int ActorId,
    int UnsafeTargetEntryOffset,
    uint RuntimePayloadAddress = 0,
    int RuntimePayloadLength = 0,
    IReadOnlyList<CrossLevelActorRuntimePointerRelocation>? RuntimePointerRelocations = null,
    CrossLevelScenePointerFixupAppend? ScenePointerFixupAppend = null);

public sealed record CrossLevelActorRuntimePointerRelocation(
    string Label,
    uint OldAddress,
    uint NewAddress,
    int ExpectedMatches);

public sealed record CrossLevelActorRuntimePointerRelocationResult(
    string Label,
    string OldAddress,
    string NewAddress,
    int MatchCount);

public sealed record CrossLevelScenePointerFixupAppend(
    string Label,
    int SceneSubfileIndex,
    int CountOffset,
    int ListAppendOffset,
    int ExpectedCount,
    uint ExpectedPreviousFieldOffset,
    IReadOnlyList<uint> NewFieldOffsets,
    IReadOnlyList<uint> ExpectedFieldValues);

public sealed record CrossLevelScenePointerFixupAppendResult(
    string Label,
    int SceneSubfileIndex,
    string SceneSubfileStart,
    int OriginalCount,
    int FinalCount,
    IReadOnlyList<string> AppendedFieldOffsets);

public sealed record CrossLevelActorPackageExpansionCandidatePlan(
    DateTimeOffset GeneratedAt,
    string Label,
    string RetailImagePath,
    string BaseCandidateImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string PlanPath,
    int WadLba,
    int TargetWadEntry,
    long OriginalTargetEntryOffset,
    long RelocatedTargetEntryOffset,
    int OriginalTargetEntrySize,
    int ExpandedTargetEntrySize,
    int ActorSubfileIndex,
    string OriginalActorSubfileStart,
    string OriginalActorSubfileEnd,
    string ExpandedActorSubfileEnd,
    int PackageLength,
    string PackageSha256,
    string NewPackageRoot,
    int RootIndex,
    string RootSlot,
    string ActorIdSlot,
    string ActorId,
    int WadGrowthBytes,
    int AvailableWadGrowthBytes,
    int OriginalExecutableLba,
    int RelocatedExecutableLba,
    string RuntimePayloadAddress,
    int RuntimePayloadLength,
    IReadOnlyList<CrossLevelActorRuntimePointerRelocationResult> RuntimePointerRelocations,
    CrossLevelScenePointerFixupAppendResult? ScenePointerFixupAppend,
    bool UnsafeGapRestored,
    bool ShiftedEntrySuffixPreserved,
    bool FinalActorLayoutValid,
    string Verification);

/// <summary>
/// Research-only writer for the safe next step after an actor package was proven to
/// crash outside its native nested subfile. It takes an already patched disposable
/// candidate, restores that candidate's invalid zero-gap copy, grows the target
/// actor/model subfile by whole sectors, appends the package at the former subfile
/// end, and relocates the remaining WAD plus executable through the same checked ISO
/// path used by oversized native skies.
/// </summary>
public static class CrossLevelActorPackageExpansionCandidateExporter
{
    private const int SectorBytes = 2048;
    private const int RootTableOffset = 0x50;
    private const int ActorIdTableOffset = 0x150;

    public static CrossLevelActorPackageExpansionCandidatePlan Export(
        CrossLevelActorPackageExpansionCandidateRequest request)
    {
        ValidateRequest(request);
        WadEntryLayout targetEntry = LoadTargetEntry(request.WadAnalysisPath, request.TargetWadEntry);
        DiscLayout retailLayout = DiscImage.DetectLayout(request.RetailImagePath);
        DiscLayout baseLayout = DiscImage.DetectLayout(request.BaseCandidateImagePath);
        if (retailLayout != baseLayout)
            throw new InvalidDataException("The retail image and base candidate use different disc layouts.");

        CrossLevelActorPackageSubfileLayout actorLayout;
        byte[] packageBytes;
        byte[] retailUnsafeBytes;
        int rootIndex;
        int rootSlot;
        int actorIdSlot;
        using (FileStream retail = File.Open(request.RetailImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            if (!CrossLevelActorPackageLayoutSafety.TryReadLayout(
                    retail,
                    retailLayout,
                    targetEntry.WadLba,
                    targetEntry.Offset,
                    out CrossLevelActorPackageSubfileLayout? inferred,
                    out string reason) || inferred == null)
            {
                throw new InvalidDataException($"Could not infer the retail actor/model layout: {reason}");
            }

            actorLayout = inferred;
            packageBytes = DiscImage.ReadFileBytes(
                retail,
                retailLayout,
                targetEntry.WadLba,
                request.DonorPackageWadOffset,
                request.PackageLength);
            retailUnsafeBytes = DiscImage.ReadFileBytes(
                retail,
                retailLayout,
                targetEntry.WadLba,
                targetEntry.Offset + request.UnsafeTargetEntryOffset,
                request.PackageLength);
            if (retailUnsafeBytes.Any(value => value != 0))
                throw new InvalidDataException("The historical unsafe target range is no longer zero in the selected retail image.");

            rootIndex = actorLayout.NativeRootCount;
            if (rootIndex < 0 || rootIndex >= actorLayout.NativeRoots.Count)
                throw new InvalidDataException("The retail actor-root table has no contiguous terminator slot for the appended package.");
            rootSlot = RootTableOffset + (rootIndex * 4);
            actorIdSlot = ActorIdTableOffset + (rootIndex * 2);

            byte[] retailRoot = DiscImage.ReadFileBytes(retail, retailLayout, targetEntry.WadLba, targetEntry.Offset + rootSlot, 4);
            byte[] retailActor = DiscImage.ReadFileBytes(retail, retailLayout, targetEntry.WadLba, targetEntry.Offset + actorIdSlot, 2);
            if (retailRoot.Any(value => value != 0) || retailActor.Any(value => value != 0))
                throw new InvalidDataException("The inferred actor-root terminator slot is not empty in the retail image.");
        }

        if (actorLayout.Length > int.MaxValue)
            throw new InvalidDataException("The target actor/model subfile is too large for the checked relocation writer.");
        if (request.ActorId is < 0 or > ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(request.ActorId));

        uint newPackageRoot = actorLayout.EndExclusive;
        using (FileStream candidate = File.Open(request.BaseCandidateImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            byte[] unsafeCopy = DiscImage.ReadFileBytes(
                candidate,
                baseLayout,
                targetEntry.WadLba,
                targetEntry.Offset + request.UnsafeTargetEntryOffset,
                request.PackageLength);
            if (!unsafeCopy.AsSpan().SequenceEqual(packageBytes))
                throw new InvalidDataException("The base candidate no longer contains the expected historical package bytes in its unsafe gap.");

            uint unsafeRoot = BinaryPrimitives.ReadUInt32LittleEndian(
                DiscImage.ReadFileBytes(candidate, baseLayout, targetEntry.WadLba, targetEntry.Offset + rootSlot, 4));
            ushort unsafeActor = BinaryPrimitives.ReadUInt16LittleEndian(
                DiscImage.ReadFileBytes(candidate, baseLayout, targetEntry.WadLba, targetEntry.Offset + actorIdSlot, 2));
            if (unsafeRoot != (uint)request.UnsafeTargetEntryOffset || unsafeActor != request.ActorId)
            {
                throw new InvalidDataException(
                    $"The base candidate's failed root evidence changed: expected root 0x{request.UnsafeTargetEntryOffset:X}/actor 0x{request.ActorId:X4}, " +
                    $"found 0x{unsafeRoot:X}/0x{unsafeActor:X4}.");
            }
        }

        NativeSkyRelocationPayload payload = new(
            PatchIndex: 0,
            WadLba: targetEntry.WadLba,
            OriginalWadOffset: targetEntry.Offset + newPackageRoot,
            StorageWadEntry: request.TargetWadEntry,
            ModelBlockOffset: checked((int)actorLayout.Length),
            OriginalLength: 0,
            Bytes: packageBytes,
            SubfileIndex: actorLayout.SubfileIndex,
            RequireLengthPrefix: false);
        NativeSkyWadRelocationPlan relocation = NativeSkyWadRelocator.BuildPlan(
            request.BaseCandidateImagePath,
            request.WadAnalysisPath,
            [payload]);
        int expectedGrowth = AlignSector(request.PackageLength);
        if (!relocation.Required || relocation.WadGrowthBytes != expectedGrowth ||
            relocation.EntryGrowthBytes.GetValueOrDefault(request.TargetWadEntry) != expectedGrowth)
        {
            throw new InvalidDataException(
                $"Actor-package growth expected 0x{expectedGrowth:X} bytes, got 0x{relocation.WadGrowthBytes:X}.");
        }

        string outputImagePath = $"{request.OutputPrefix}.bin";
        string outputCuePath = $"{request.OutputPrefix}.cue";
        string planPath = $"{request.OutputPrefix}.actor-package-expansion-plan.json";
        Directory.CreateDirectory(Path.GetDirectoryName(outputImagePath) ?? ".");
        NativeSkyWadRelocator.WriteExpandedImage(
            request.BaseCandidateImagePath,
            outputImagePath,
            request.WadAnalysisPath,
            relocation,
            [payload]);

        long relocatedEntryOffset = relocation.RelocatedEntryOffsets[request.TargetWadEntry];
        IReadOnlyList<CrossLevelActorRuntimePointerRelocationResult> runtimeRelocations;
        CrossLevelScenePointerFixupAppendResult? sceneFixupResult;
        using (FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            DiscImage.WriteFileBytes(
                output,
                baseLayout,
                targetEntry.WadLba,
                relocatedEntryOffset + request.UnsafeTargetEntryOffset,
                retailUnsafeBytes);

            byte[] rootBytes = new byte[4];
            BinaryPrimitives.WriteUInt32LittleEndian(rootBytes, newPackageRoot);
            DiscImage.WriteFileBytes(output, baseLayout, targetEntry.WadLba, relocatedEntryOffset + rootSlot, rootBytes);

            byte[] actorBytes = new byte[2];
            BinaryPrimitives.WriteUInt16LittleEndian(actorBytes, checked((ushort)request.ActorId));
            DiscImage.WriteFileBytes(output, baseLayout, targetEntry.WadLba, relocatedEntryOffset + actorIdSlot, actorBytes);

            sceneFixupResult = AppendScenePointerFixups(
                output,
                baseLayout,
                targetEntry.WadLba,
                relocatedEntryOffset,
                request.ScenePointerFixupAppend);
            runtimeRelocations = PatchRuntimePointers(output, baseLayout, relocation, request);
            output.Flush();
        }

        string cueText = DiscImage.BuildCueText(request.RetailCuePath, Path.GetFileName(outputImagePath));
        File.WriteAllText(outputCuePath, cueText, Encoding.ASCII);

        VerificationResult verification = Verify(
            request,
            targetEntry,
            actorLayout,
            packageBytes,
            retailUnsafeBytes,
            relocation,
            relocatedEntryOffset,
            outputImagePath,
            newPackageRoot,
            rootIndex,
            rootSlot,
            actorIdSlot,
            runtimeRelocations,
            sceneFixupResult);

        CrossLevelActorPackageExpansionCandidatePlan plan = new(
            GeneratedAt: DateTimeOffset.Now,
            Label: request.Label,
            RetailImagePath: request.RetailImagePath,
            BaseCandidateImagePath: request.BaseCandidateImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            PlanPath: planPath,
            WadLba: targetEntry.WadLba,
            TargetWadEntry: request.TargetWadEntry,
            OriginalTargetEntryOffset: targetEntry.Offset,
            RelocatedTargetEntryOffset: relocatedEntryOffset,
            OriginalTargetEntrySize: targetEntry.Size,
            ExpandedTargetEntrySize: checked(targetEntry.Size + expectedGrowth),
            ActorSubfileIndex: actorLayout.SubfileIndex,
            OriginalActorSubfileStart: Hex(actorLayout.Start),
            OriginalActorSubfileEnd: Hex(actorLayout.EndExclusive),
            ExpandedActorSubfileEnd: Hex(actorLayout.EndExclusive + (uint)expectedGrowth),
            PackageLength: request.PackageLength,
            PackageSha256: Convert.ToHexString(SHA256.HashData(packageBytes)).ToLowerInvariant(),
            NewPackageRoot: Hex(newPackageRoot),
            RootIndex: rootIndex,
            RootSlot: Hex(rootSlot),
            ActorIdSlot: Hex(actorIdSlot),
            ActorId: $"0x{request.ActorId:X4}",
            WadGrowthBytes: relocation.WadGrowthBytes,
            AvailableWadGrowthBytes: relocation.AvailableGrowthBytes,
            OriginalExecutableLba: relocation.OriginalExecutableLba,
            RelocatedExecutableLba: relocation.RelocatedExecutableLba,
            RuntimePayloadAddress: request.RuntimePayloadAddress == 0 ? "none" : $"0x{request.RuntimePayloadAddress:X8}",
            RuntimePayloadLength: request.RuntimePayloadLength,
            RuntimePointerRelocations: runtimeRelocations,
            ScenePointerFixupAppend: sceneFixupResult,
            UnsafeGapRestored: verification.UnsafeGapRestored,
            ShiftedEntrySuffixPreserved: verification.ShiftedSuffixPreserved,
            FinalActorLayoutValid: verification.ActorLayoutValid,
            Verification: verification.Summary);
        File.WriteAllText(planPath, JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = true }));
        return plan;
    }

    private static VerificationResult Verify(
        CrossLevelActorPackageExpansionCandidateRequest request,
        WadEntryLayout targetEntry,
        CrossLevelActorPackageSubfileLayout actorLayout,
        byte[] packageBytes,
        byte[] retailUnsafeBytes,
        NativeSkyWadRelocationPlan relocation,
        long relocatedEntryOffset,
        string outputImagePath,
        uint newPackageRoot,
        int rootIndex,
        int rootSlot,
        int actorIdSlot,
        IReadOnlyList<CrossLevelActorRuntimePointerRelocationResult> runtimeRelocations,
        CrossLevelScenePointerFixupAppendResult? sceneFixupResult)
    {
        DiscLayout layout = DiscImage.DetectLayout(outputImagePath);
        using FileStream output = File.Open(outputImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] packageReadback = DiscImage.ReadFileBytes(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset + newPackageRoot,
            packageBytes.Length);
        if (!packageReadback.AsSpan().SequenceEqual(packageBytes))
            throw new InvalidDataException("Expanded actor package readback failed.");

        int paddingLength = relocation.EntryGrowthBytes[request.TargetWadEntry] - packageBytes.Length;
        byte[] padding = DiscImage.ReadFileBytes(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset + newPackageRoot + packageBytes.Length,
            paddingLength);
        if (padding.Any(value => value != 0))
            throw new InvalidDataException("Expanded actor/model subfile padding is not zero.");

        byte[] unsafeReadback = DiscImage.ReadFileBytes(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset + request.UnsafeTargetEntryOffset,
            retailUnsafeBytes.Length);
        bool unsafeGapRestored = unsafeReadback.AsSpan().SequenceEqual(retailUnsafeBytes);
        if (!unsafeGapRestored)
            throw new InvalidDataException("The historical invalid package gap was not restored to retail bytes.");

        uint root = BinaryPrimitives.ReadUInt32LittleEndian(
            DiscImage.ReadFileBytes(output, layout, targetEntry.WadLba, relocatedEntryOffset + rootSlot, 4));
        ushort actor = BinaryPrimitives.ReadUInt16LittleEndian(
            DiscImage.ReadFileBytes(output, layout, targetEntry.WadLba, relocatedEntryOffset + actorIdSlot, 2));
        if (root != newPackageRoot || actor != request.ActorId)
            throw new InvalidDataException("Expanded actor root/actor-id readback failed.");

        bool actorLayoutValid = CrossLevelActorPackageLayoutSafety.TryReadLayout(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset,
            out CrossLevelActorPackageSubfileLayout? expandedLayout,
            out string actorReason) &&
            expandedLayout != null &&
            expandedLayout.SubfileIndex == actorLayout.SubfileIndex &&
            expandedLayout.Start == actorLayout.Start &&
            expandedLayout.EndExclusive == actorLayout.EndExclusive + (uint)relocation.EntryGrowthBytes[request.TargetWadEntry] &&
            expandedLayout.NativeRootCount == actorLayout.NativeRootCount + 1 &&
            expandedLayout.LastNativeRoot == newPackageRoot &&
            expandedLayout.NativeActorIds[rootIndex] == request.ActorId;
        if (!actorLayoutValid)
            throw new InvalidDataException($"Expanded actor/model layout failed validation: {actorReason}");

        byte[] baseSuffix;
        DiscLayout baseLayout = DiscImage.DetectLayout(request.BaseCandidateImagePath);
        using (FileStream candidate = File.Open(request.BaseCandidateImagePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            baseSuffix = DiscImage.ReadFileBytes(
                candidate,
                baseLayout,
                targetEntry.WadLba,
                targetEntry.Offset + actorLayout.EndExclusive,
                checked((int)(targetEntry.Size - actorLayout.EndExclusive)));
        }
        byte[] shiftedSuffix = DiscImage.ReadFileBytes(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset + actorLayout.EndExclusive + relocation.EntryGrowthBytes[request.TargetWadEntry],
            baseSuffix.Length);
        byte[] expectedSuffix = baseSuffix.ToArray();
        if (request.ScenePointerFixupAppend is { } sceneFixups)
        {
            (long finalSceneOffset, _) = ReadNestedSubfileBounds(
                output,
                layout,
                targetEntry.WadLba,
                relocatedEntryOffset,
                sceneFixups.SceneSubfileIndex);
            long expectedFinalSceneOffset = actorLayout.EndExclusive + relocation.EntryGrowthBytes[request.TargetWadEntry];
            if (finalSceneOffset != expectedFinalSceneOffset)
            {
                throw new InvalidDataException(
                    $"Scene subfile expected to follow expanded actor/model data at 0x{expectedFinalSceneOffset:X}, found 0x{finalSceneOffset:X}.");
            }

            BinaryPrimitives.WriteUInt32LittleEndian(
                expectedSuffix.AsSpan(sceneFixups.CountOffset, 4),
                checked((uint)(sceneFixups.ExpectedCount + sceneFixups.NewFieldOffsets.Count)));
            for (int i = 0; i < sceneFixups.NewFieldOffsets.Count; i++)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(
                    expectedSuffix.AsSpan(sceneFixups.ListAppendOffset + (i * 4), 4),
                    sceneFixups.NewFieldOffsets[i]);
            }
        }
        bool shiftedSuffixPreserved = shiftedSuffix.AsSpan().SequenceEqual(expectedSuffix);
        if (!shiftedSuffixPreserved)
            throw new InvalidDataException("One or more nested subfiles after the actor/model subfile changed beyond the requested pointer-fixup append.");

        DiscFileRecord wad = DiscImage.FindRootFileRecord(output, layout, name =>
            string.Equals(name, "WAD.WAD", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "WAD", StringComparison.OrdinalIgnoreCase));
        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        if (wad.Lba != targetEntry.WadLba || wad.Size != relocation.ExpandedWadSize ||
            executable.Lba != relocation.RelocatedExecutableLba)
        {
            throw new InvalidDataException("Expanded ISO WAD/executable directory readback failed.");
        }


        VerifyRuntimePointers(output, layout, executable, request, runtimeRelocations);
        VerifyScenePointerFixups(
            output,
            layout,
            targetEntry.WadLba,
            relocatedEntryOffset,
            request.ScenePointerFixupAppend,
            sceneFixupResult);

        return new VerificationResult(
            unsafeGapRestored,
            shiftedSuffixPreserved,
            actorLayoutValid,
            $"Package {packageBytes.Length} bytes at {Hex(newPackageRoot)}; root index {rootIndex} remains contiguous/ascending; " +
            $"actor/model subfile grew by 0x{relocation.EntryGrowthBytes[request.TargetWadEntry]:X}; later nested subfiles, WAD entries, relocated executable" +
            (runtimeRelocations.Count == 0 ? "" : $", and {runtimeRelocations.Sum(item => item.MatchCount)} shifted helper pointer load(s)") +
            (sceneFixupResult == null ? "" : $", plus {sceneFixupResult.AppendedFieldOffsets.Count} appended scene pointer fixup(s)") +
            " passed byte readback.");
    }

    private static CrossLevelScenePointerFixupAppendResult? AppendScenePointerFixups(
        FileStream output,
        DiscLayout layout,
        int wadLba,
        long entryOffset,
        CrossLevelScenePointerFixupAppend? request)
    {
        if (request == null)
            return null;

        (long sceneOffset, int sceneSize) = ReadNestedSubfileBounds(
            output,
            layout,
            wadLba,
            entryOffset,
            request.SceneSubfileIndex);
        ValidateSceneFixupBounds(request, sceneSize);

        int count = checked((int)ReadUInt32(output, layout, wadLba, entryOffset + sceneOffset + request.CountOffset));
        uint previous = ReadUInt32(output, layout, wadLba, entryOffset + sceneOffset + request.ListAppendOffset - 4);
        if (count != request.ExpectedCount || previous != request.ExpectedPreviousFieldOffset)
        {
            throw new InvalidDataException(
                $"Scene pointer-fixup preimage changed: expected count 0x{request.ExpectedCount:X}/previous 0x{request.ExpectedPreviousFieldOffset:X}, " +
                $"found 0x{count:X}/0x{previous:X}.");
        }


        for (int i = 0; i < request.NewFieldOffsets.Count; i++)
        {
            uint fieldValue = ReadUInt32(
                output,
                layout,
                wadLba,
                entryOffset + sceneOffset + request.NewFieldOffsets[i]);
            if (fieldValue != request.ExpectedFieldValues[i])
            {
                throw new InvalidDataException(
                    $"Scene pointer field 0x{request.NewFieldOffsets[i]:X} expected value 0x{request.ExpectedFieldValues[i]:X8}, found 0x{fieldValue:X8}.");
            }
        }

        for (int i = 0; i < request.NewFieldOffsets.Count; i++)
        {
            long slot = entryOffset + sceneOffset + request.ListAppendOffset + (i * 4L);
            if (ReadUInt32(output, layout, wadLba, slot) != 0)
                throw new InvalidDataException($"Scene pointer-fixup append slot {i} is not blank.");
            WriteUInt32(output, layout, wadLba, slot, request.NewFieldOffsets[i]);
        }
        WriteUInt32(
            output,
            layout,
            wadLba,
            entryOffset + sceneOffset + request.CountOffset,
            checked((uint)(count + request.NewFieldOffsets.Count)));

        return new CrossLevelScenePointerFixupAppendResult(
            request.Label,
            request.SceneSubfileIndex,
            Hex(sceneOffset),
            count,
            checked(count + request.NewFieldOffsets.Count),
            request.NewFieldOffsets.Select(offset => $"0x{offset:X}").ToArray());
    }

    private static void VerifyScenePointerFixups(
        FileStream output,
        DiscLayout layout,
        int wadLba,
        long entryOffset,
        CrossLevelScenePointerFixupAppend? request,
        CrossLevelScenePointerFixupAppendResult? result)
    {
        if (request == null)
        {
            if (result != null)
                throw new InvalidDataException("Unexpected scene pointer-fixup result without a request.");
            return;
        }
        if (result == null)
            throw new InvalidDataException("Scene pointer-fixup result is missing.");

        (long sceneOffset, int sceneSize) = ReadNestedSubfileBounds(
            output,
            layout,
            wadLba,
            entryOffset,
            request.SceneSubfileIndex);
        ValidateSceneFixupBounds(request, sceneSize);
        uint finalCount = ReadUInt32(output, layout, wadLba, entryOffset + sceneOffset + request.CountOffset);
        if (finalCount != request.ExpectedCount + request.NewFieldOffsets.Count ||
            result.FinalCount != finalCount ||
            !string.Equals(result.SceneSubfileStart, Hex(sceneOffset), StringComparison.Ordinal))
        {
            throw new InvalidDataException("Scene pointer-fixup count or scene-subfile readback changed.");
        }

        for (int i = 0; i < request.NewFieldOffsets.Count; i++)
        {
            uint value = ReadUInt32(
                output,
                layout,
                wadLba,
                entryOffset + sceneOffset + request.ListAppendOffset + (i * 4L));
            if (value != request.NewFieldOffsets[i])
                throw new InvalidDataException($"Scene pointer-fixup entry {i} failed readback.");

            uint fieldValue = ReadUInt32(
                output,
                layout,
                wadLba,
                entryOffset + sceneOffset + request.NewFieldOffsets[i]);
            if (fieldValue != request.ExpectedFieldValues[i])
                throw new InvalidDataException($"Scene pointer field 0x{request.NewFieldOffsets[i]:X} changed after fixup append.");
        }
    }

    private static (long Offset, int Size) ReadNestedSubfileBounds(
        FileStream stream,
        DiscLayout layout,
        int wadLba,
        long entryOffset,
        int subfileIndex)
    {
        byte[] row = DiscImage.ReadFileBytes(stream, layout, wadLba, entryOffset + (subfileIndex * 8L), 8);
        uint offset = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(0, 4));
        uint size = BinaryPrimitives.ReadUInt32LittleEndian(row.AsSpan(4, 4));
        if (offset < SectorBytes || size == 0 || size > int.MaxValue)
            throw new InvalidDataException($"Nested subfile {subfileIndex} has invalid bounds 0x{offset:X}+0x{size:X}.");
        return (offset, checked((int)size));
    }

    private static void ValidateSceneFixupBounds(CrossLevelScenePointerFixupAppend request, int sceneSize)
    {
        long listEnd = request.ListAppendOffset + (request.NewFieldOffsets.Count * 4L);
        if (request.SceneSubfileIndex < 0 || request.CountOffset < 0 || request.CountOffset + 4L > sceneSize ||
            request.ListAppendOffset < 4 || listEnd > sceneSize)
        {
            throw new InvalidDataException("Scene pointer-fixup offsets are outside the selected nested subfile.");
        }
    }

    private static uint ReadUInt32(FileStream stream, DiscLayout layout, int wadLba, long offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(DiscImage.ReadFileBytes(stream, layout, wadLba, offset, 4));

    private static void WriteUInt32(FileStream stream, DiscLayout layout, int wadLba, long offset, uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        DiscImage.WriteFileBytes(stream, layout, wadLba, offset, bytes);
    }

    private static IReadOnlyList<CrossLevelActorRuntimePointerRelocationResult> PatchRuntimePointers(
        FileStream output,
        DiscLayout layout,
        NativeSkyWadRelocationPlan relocation,
        CrossLevelActorPackageExpansionCandidateRequest request)
    {
        IReadOnlyList<CrossLevelActorRuntimePointerRelocation> requested = request.RuntimePointerRelocations ?? [];
        if (requested.Count == 0)
            return [];

        DiscFileRecord executable = DiscImage.FindRootFileRecord(output, layout, IsExecutableName);
        if (executable.Lba != relocation.RelocatedExecutableLba)
            throw new InvalidDataException("Could not locate the relocated executable before helper-pointer patching.");

        long payloadFileOffset = RuntimeAddressToExecutableOffset(request.RuntimePayloadAddress);
        byte[] payload = DiscImage.ReadFileBytes(
            output,
            layout,
            executable.Lba,
            payloadFileOffset,
            request.RuntimePayloadLength);
        List<CrossLevelActorRuntimePointerRelocationResult> results = [];
        foreach (CrossLevelActorRuntimePointerRelocation item in requested)
        {
            int matches = RewriteMipsAbsoluteLoads(payload, item.OldAddress, item.NewAddress);
            if (matches != item.ExpectedMatches)
            {
                throw new InvalidDataException(
                    $"Runtime pointer '{item.Label}' expected {item.ExpectedMatches} helper load(s) from 0x{item.OldAddress:X8}, found {matches}.");
            }

            results.Add(new CrossLevelActorRuntimePointerRelocationResult(
                item.Label,
                $"0x{item.OldAddress:X8}",
                $"0x{item.NewAddress:X8}",
                matches));
        }

        DiscImage.WriteFileBytes(output, layout, executable.Lba, payloadFileOffset, payload);
        return results;
    }

    private static void VerifyRuntimePointers(
        FileStream output,
        DiscLayout layout,
        DiscFileRecord executable,
        CrossLevelActorPackageExpansionCandidateRequest request,
        IReadOnlyList<CrossLevelActorRuntimePointerRelocationResult> results)
    {
        IReadOnlyList<CrossLevelActorRuntimePointerRelocation> requested = request.RuntimePointerRelocations ?? [];
        if (requested.Count == 0)
            return;
        if (results.Count != requested.Count)
            throw new InvalidDataException("Runtime pointer relocation result count changed before readback.");

        long payloadFileOffset = RuntimeAddressToExecutableOffset(request.RuntimePayloadAddress);
        byte[] payload = DiscImage.ReadFileBytes(
            output,
            layout,
            executable.Lba,
            payloadFileOffset,
            request.RuntimePayloadLength);
        for (int i = 0; i < requested.Count; i++)
        {
            CrossLevelActorRuntimePointerRelocation item = requested[i];
            int oldMatches = CountMipsAbsoluteLoads(payload, item.OldAddress);
            int newMatches = CountMipsAbsoluteLoads(payload, item.NewAddress);
            if (oldMatches != 0 || newMatches != item.ExpectedMatches || results[i].MatchCount != item.ExpectedMatches)
            {
                throw new InvalidDataException(
                    $"Runtime pointer '{item.Label}' readback expected 0 old/{item.ExpectedMatches} new load(s), found {oldMatches}/{newMatches}.");
            }
        }
    }

    private static int RewriteMipsAbsoluteLoads(byte[] payload, uint oldAddress, uint newAddress)
    {
        int matches = 0;
        for (int offset = 0; offset <= payload.Length - 8; offset += 4)
        {
            uint lui = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
            uint ori = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + 4, 4));
            if (!IsMipsAbsoluteLoad(lui, ori, oldAddress))
                continue;

            uint relocatedLui = (lui & 0xFFFF0000u) | (newAddress >> 16);
            uint relocatedOri = (ori & 0xFFFF0000u) | (newAddress & 0xFFFFu);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset, 4), relocatedLui);
            BinaryPrimitives.WriteUInt32LittleEndian(payload.AsSpan(offset + 4, 4), relocatedOri);
            matches++;
        }
        return matches;
    }

    private static int CountMipsAbsoluteLoads(byte[] payload, uint address)
    {
        int matches = 0;
        for (int offset = 0; offset <= payload.Length - 8; offset += 4)
        {
            uint lui = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset, 4));
            uint ori = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(offset + 4, 4));
            if (IsMipsAbsoluteLoad(lui, ori, address))
                matches++;
        }
        return matches;
    }

    private static bool IsMipsAbsoluteLoad(uint lui, uint ori, uint address)
    {
        if ((lui >> 26) != 0x0F || (ori >> 26) != 0x0D)
            return false;
        uint register = (lui >> 16) & 0x1F;
        if (((ori >> 21) & 0x1F) != register || ((ori >> 16) & 0x1F) != register)
            return false;
        return ((lui & 0xFFFFu) << 16 | (ori & 0xFFFFu)) == address;
    }

    private static long RuntimeAddressToExecutableOffset(uint address)
    {
        const uint executableDestination = 0x80010000;
        const int executableHeaderBytes = 0x800;
        if (address < executableDestination)
            throw new ArgumentOutOfRangeException(nameof(address), $"Runtime address 0x{address:X8} is before the executable load address.");
        return checked(executableHeaderBytes + (long)(address - executableDestination));
    }

    private static WadEntryLayout LoadTargetEntry(string path, int targetWadEntry)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement root = document.RootElement;
        int wadLba = JsonValue.GetInt32(root.GetProperty("wad"), "lba", 37);
        JsonElement? entry = root.GetProperty("entries")
            .EnumerateArray()
            .Cast<JsonElement?>()
            .FirstOrDefault(candidate => JsonValue.GetInt32(candidate!.Value, "index", -1) == targetWadEntry);
        if (entry == null)
            throw new InvalidDataException($"WAD entry {targetWadEntry} is missing from {path}.");
        return new WadEntryLayout(
            wadLba,
            targetWadEntry,
            JsonValue.GetInt64(entry.Value, "offset", -1),
            JsonValue.GetInt32(entry.Value, "size", -1));
    }

    private static void ValidateRequest(CrossLevelActorPackageExpansionCandidateRequest request)
    {
        if (!File.Exists(request.RetailImagePath))
            throw new FileNotFoundException("Missing retail source image.", request.RetailImagePath);
        if (!File.Exists(request.RetailCuePath))
            throw new FileNotFoundException("Missing retail source CUE.", request.RetailCuePath);
        if (!File.Exists(request.BaseCandidateImagePath))
            throw new FileNotFoundException("Missing base candidate image.", request.BaseCandidateImagePath);
        if (!File.Exists(request.WadAnalysisPath))
            throw new FileNotFoundException("Missing WAD analysis.", request.WadAnalysisPath);
        if (request.TargetWadEntry < 0 || request.DonorPackageWadOffset < 0 ||
            request.PackageLength <= 0 || request.UnsafeTargetEntryOffset < SectorBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Actor-package expansion offsets and lengths must be positive and target a nested entry body.");
        }


        IReadOnlyList<CrossLevelActorRuntimePointerRelocation> runtimePointers = request.RuntimePointerRelocations ?? [];
        if (runtimePointers.Count == 0)
        {
            if (request.RuntimePayloadAddress != 0 || request.RuntimePayloadLength != 0)
                throw new ArgumentException("Runtime helper bounds were supplied without pointer relocations.", nameof(request));
        }

        if (runtimePointers.Count > 0 &&
            (request.RuntimePayloadAddress == 0 || request.RuntimePayloadLength < 8 ||
            runtimePointers.Any(item => string.IsNullOrWhiteSpace(item.Label) || item.OldAddress == item.NewAddress || item.ExpectedMatches <= 0) ||
            runtimePointers.Select(item => item.OldAddress).Distinct().Count() != runtimePointers.Count ||
            runtimePointers.Select(item => item.NewAddress).Distinct().Count() != runtimePointers.Count))
        {
            throw new ArgumentException("Runtime helper pointer relocations require valid bounds, distinct old/new addresses, and positive exact match counts.", nameof(request));
        }


        CrossLevelScenePointerFixupAppend? sceneFixups = request.ScenePointerFixupAppend;
        if (sceneFixups != null &&
            (string.IsNullOrWhiteSpace(sceneFixups.Label) || sceneFixups.SceneSubfileIndex < 0 ||
             sceneFixups.ExpectedCount < 0 || sceneFixups.NewFieldOffsets.Count == 0 ||
             sceneFixups.ExpectedFieldValues.Count != sceneFixups.NewFieldOffsets.Count ||
             sceneFixups.NewFieldOffsets.Any(offset => offset == 0) ||
             sceneFixups.NewFieldOffsets.Distinct().Count() != sceneFixups.NewFieldOffsets.Count))
        {
            throw new ArgumentException("Scene pointer-fixup append metadata is incomplete or ambiguous.", nameof(request));
        }
    }

    private static bool IsExecutableName(string name) =>
        name.StartsWith("SCUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLUS_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SLES_", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("SCES_", StringComparison.OrdinalIgnoreCase);

    private static int AlignSector(int value) => checked(((value + SectorBytes - 1) / SectorBytes) * SectorBytes);
    private static string Hex(long value) => $"0x{value:X}";

    private sealed record WadEntryLayout(int WadLba, int Index, long Offset, int Size);
    private sealed record VerificationResult(bool UnsafeGapRestored, bool ShiftedSuffixPreserved, bool ActorLayoutValid, string Summary);
}
