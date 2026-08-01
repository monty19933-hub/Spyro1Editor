using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Text;

namespace Spyro.Editor.Core.Exporting;

public static class LevelTextPatchExporter
{
    private const int PsxExeHeaderSize = 0x800;
    private const int PsxExeLoadAddressOffset = 0x18;
    private const int LevelNameCount = 37;
    private const int HomeworldNameCount = 7;
    private const int ReturnHomeIndex = 36;
    private static readonly byte[] ReturnHomeBytes = Encoding.ASCII.GetBytes("RETURN HOME\0");
    private static readonly byte[] NativeHomeworldNameLengths = [8, 12, 13, 11, 12, 10, 12];

    public static async Task<LevelTextPatchResult> ExportAsync(LevelTextPatchRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);

        string replacement = ValidateReplacement(request.Target, request.ReplacementText);
        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", $"Spyro the Dragon (USA)-text-{request.Target.ScriptKey.ToLowerInvariant()}-{ToSafeSlug(replacement)}")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.level-text-plan.json";

        LevelTextPatchPlan plan;
        LevelTextBatchPatchPlan? expandedPlan = null;
        if (replacement.Length > request.Target.OriginalSlotLength)
        {
            DiscLayout layout = DiscImage.DetectLayout(request.SourceImagePath);
            using FileStream source = File.OpenRead(request.SourceImagePath);
            DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
            byte[] exeBytes = DiscImage.ReadFileBytes(source, layout, executable.Lba, 0, executable.Size);
            expandedPlan = BuildExpandedBatchPlan(
                request.SourceImagePath,
                outputImagePath,
                outputCuePath,
                layout,
                executable,
                exeBytes,
                [new LevelTextReplacement(request.Target, replacement)]);
            plan = expandedPlan.Patches.Single();
        }
        else
        {
            plan = BuildPlan(request.SourceImagePath, request.SourceCuePath, outputImagePath, outputCuePath, request.Target, replacement);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize((object?)expandedPlan ?? plan, NewJsonOptions()), cancellationToken);

        if (request.WriteImage)
        {
            if (expandedPlan != null)
                await WriteBatchImageAsync(
                    request.SourceImagePath,
                    request.SourceCuePath,
                    outputImagePath,
                    outputCuePath,
                    expandedPlan,
                    consumeDisposableSourceImage: false,
                    cancellationToken);
            else
                await WriteImageAsync(request.SourceImagePath, request.SourceCuePath, outputImagePath, outputCuePath, [plan], cancellationToken);
        }

        return new LevelTextPatchResult(outputImagePath, outputCuePath, outputPlanPath, plan, request.WriteImage);
    }

    public static async Task<LevelTextBatchPatchResult> ExportBatchAsync(
        LevelTextBatchPatchRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.SourceImagePath))
            throw new FileNotFoundException("Missing source disc image.", request.SourceImagePath);
        if (request.Edits.Count == 0)
            throw new InvalidOperationException("Choose at least one saved level-name edit.");

        List<LevelTextReplacement> edits = request.Edits
            .GroupBy(edit => edit.Target.LevelKey, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .ToList();
        string outputPrefix = string.IsNullOrWhiteSpace(request.OutputPrefix)
            ? Path.Combine(Path.GetDirectoryName(request.SourceImagePath) ?? "", "Spyro the Dragon (USA)-level-names")
            : request.OutputPrefix;
        string outputImagePath = $"{outputPrefix}.bin";
        string outputCuePath = $"{outputPrefix}.cue";
        string outputPlanPath = $"{outputPrefix}.level-text-batch-plan.json";

        DiscLayout layout = DiscImage.DetectLayout(request.SourceImagePath);
        using FileStream source = File.OpenRead(request.SourceImagePath);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(source, layout, IsExecutableName);
        byte[] exeBytes = DiscImage.ReadFileBytes(source, layout, executable.Lba, 0, executable.Size);
        List<LevelTextReplacement> normalizedEdits = edits
            .Select(edit => new LevelTextReplacement(edit.Target, ValidateReplacement(edit.Target, edit.ReplacementText)))
            .ToList();
        bool needsExpandedPool = normalizedEdits.Any(edit => edit.ReplacementText.Length > edit.Target.OriginalSlotLength);
        LevelTextBatchPatchPlan batchPlan;
        if (needsExpandedPool)
        {
            batchPlan = BuildExpandedBatchPlan(
                request.SourceImagePath,
                outputImagePath,
                outputCuePath,
                layout,
                executable,
                exeBytes,
                normalizedEdits);
        }
        else
        {
            List<LevelTextPatchPlan> patches = normalizedEdits
                .Select(edit => BuildPlanCore(
                    request.SourceImagePath,
                    outputImagePath,
                    outputCuePath,
                    layout,
                    executable,
                    exeBytes,
                    edit.Target,
                    edit.ReplacementText))
                .ToList();
            batchPlan = new LevelTextBatchPatchPlan(
                GeneratedAt: DateTimeOffset.UtcNow,
                SourceImagePath: request.SourceImagePath,
                OutputImagePath: outputImagePath,
                OutputCuePath: outputCuePath,
                PatchCount: patches.Count,
                LevelNames: patches.Select(patch => patch.LevelDisplayName).ToArray(),
                Patches: patches,
                StorageMode: "fixed-slots",
                StringPoolFileOffset: 0,
                StringPoolCapacity: 0,
                StringPoolBytesUsed: 0,
                StringPoolBytesRemaining: 0,
                BinaryPatchCount: 0,
                BinaryPatches: []);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(outputPlanPath) ?? ".");
        await File.WriteAllTextAsync(outputPlanPath, JsonSerializer.Serialize(batchPlan, NewJsonOptions()), cancellationToken);

        if (request.WriteImage)
            await WriteBatchImageAsync(
                request.SourceImagePath,
                request.SourceCuePath,
                outputImagePath,
                outputCuePath,
                batchPlan,
                request.ConsumeDisposableSourceImage,
                cancellationToken);

        return new LevelTextBatchPatchResult(outputImagePath, outputCuePath, outputPlanPath, batchPlan, request.WriteImage);
    }

    public static LevelTextPatchPlan BuildPlan(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        TextTargetEntry target,
        string replacement)
    {
        string normalized = ValidateReplacement(target, replacement);
        if (normalized.Length > target.OriginalSlotLength)
            throw new InvalidOperationException("Longer level names require the batch exporter so the shared native name pool can be repacked safely.");
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        using FileStream stream = File.OpenRead(sourceImagePath);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(stream, layout, IsExecutableName);
        byte[] exeBytes = DiscImage.ReadFileBytes(stream, layout, executable.Lba, 0, executable.Size);
        return BuildPlanCore(sourceImagePath, outputImagePath, outputCuePath, layout, executable, exeBytes, target, normalized);
    }

    private static LevelTextPatchPlan BuildPlanCore(
        string sourceImagePath,
        string outputImagePath,
        string outputCuePath,
        DiscLayout layout,
        DiscFileRecord executable,
        byte[] exeBytes,
        TextTargetEntry target,
        string replacement)
    {
        LevelTextTableLayout tableLayout = LocateNameTables(exeBytes);
        int pointerTableOffset = target.TableKind == LevelTextTableKind.LevelNames
            ? tableLayout.LevelNamesTableOffset
            : tableLayout.HomeworldNamesTableOffset;
        int tableCount = target.TableKind == LevelTextTableKind.LevelNames ? LevelNameCount : HomeworldNameCount;
        if (target.TableIndex < 0 || target.TableIndex >= tableCount)
            throw new InvalidOperationException($"{target.DisplayName} has an invalid {target.TableKind} slot index {target.TableIndex}.");

        uint pointer = ReadUInt32(exeBytes, pointerTableOffset + target.TableIndex * sizeof(uint));
        if (!TryPointerToFileOffset(pointer, tableLayout.LoadAddress, exeBytes.Length, out int targetOffset))
            throw new InvalidOperationException($"{target.DisplayName}'s name pointer 0x{pointer:X8} is outside the executable payload.");
        if (replacement.Length > target.OriginalSlotLength)
            throw new InvalidOperationException($"{target.DisplayName}'s longer name must use the shared name-pool exporter.");
        if (targetOffset + target.OriginalSlotLength >= exeBytes.Length || exeBytes[targetOffset + target.OriginalSlotLength] != 0)
            throw new InvalidOperationException($"{target.DisplayName}'s fixed name slot is not terminated at its expected {target.OriginalSlotLength}-character boundary.");

        byte[] beforeBytes = exeBytes.AsSpan(targetOffset, target.OriginalSlotLength).ToArray();
        string beforeText = ReadFixedSlotText(beforeBytes);
        if (!TextTargetCatalog.IsSafeReplacement(beforeText, target.OriginalSlotLength))
            throw new InvalidOperationException($"{target.DisplayName}'s current name slot contains unsupported bytes.");

        byte[] replacementBytes = Encoding.ASCII.GetBytes(replacement);
        byte[] patchBytes = new byte[target.OriginalSlotLength];
        Array.Copy(replacementBytes, patchBytes, replacementBytes.Length);
        string scope = target.AffectsPortalLettering
            ? "portal lettering, transition text, and guidebook name"
            : "homeworld title, balloon transition text, and guidebook heading";

        return new LevelTextPatchPlan(
            GeneratedAt: DateTimeOffset.UtcNow,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            TargetLevelKey: target.LevelKey,
            LevelDisplayName: target.DisplayName,
            OriginalName: target.OriginalText,
            BeforeName: beforeText,
            ReplacementName: replacement,
            TableKind: target.TableKind,
            TableIndex: target.TableIndex,
            PointerTableFileOffset: pointerTableOffset,
            StringPointer: $"0x{pointer:X8}",
            ExeName: executable.Name,
            ExeLba: executable.Lba,
            ExeFileOffset: targetOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, executable.Lba, targetOffset),
            ByteLength: patchBytes.Length,
            BeforeHexPreview: ToHex(beforeBytes),
            AfterHexPreview: ToHex(patchBytes),
            SectorSize: layout.SectorSize,
            UserOffset: layout.UserOffset,
            Notes:
            [
                $"Patches the exact indexed {target.TableKind} slot used for {scope}.",
                "Replacement uses the original fixed slot; shorter names are nulled through the remaining bytes.",
                $"This target can use up to {target.MaxLength} characters through guarded shared-pool repacking.",
                "Portal lettering supports uppercase A-Z, spaces, and apostrophes."
            ]);
    }

    private static LevelTextBatchPatchPlan BuildExpandedBatchPlan(
        string sourceImagePath,
        string outputImagePath,
        string outputCuePath,
        DiscLayout layout,
        DiscFileRecord executable,
        byte[] exeBytes,
        IReadOnlyList<LevelTextReplacement> edits)
    {
        LevelTextTableLayout tableLayout = LocateNameTables(exeBytes);
        Dictionary<(LevelTextTableKind Kind, int Index), LevelTextReplacement> editsBySlot = edits
            .GroupBy(edit => (edit.Target.TableKind, edit.Target.TableIndex))
            .ToDictionary(group => group.Key, group => group.Last());
        List<NameTableSlot> slots = [];
        AddNameTableSlots(LevelTextTableKind.HomeworldNames, HomeworldNameCount, tableLayout.HomeworldNamesTableOffset);
        AddNameTableSlots(LevelTextTableKind.LevelNames, LevelNameCount, tableLayout.LevelNamesTableOffset);

        NameTableSlot poolAnchor = slots.Single(slot =>
            slot.TableKind == LevelTextTableKind.HomeworldNames &&
            slot.TableIndex == HomeworldNameCount - 1);
        if (!string.Equals(poolAnchor.BeforeText, "THIGH MASTERS", StringComparison.Ordinal))
            throw new InvalidOperationException("The native level-name pool anchor did not match this supported Spyro executable.");

        int poolStart = poolAnchor.OriginalStringOffset;
        NameTableSlot[] nearbyPoolSlots = slots
            .Where(slot => slot.OriginalStringOffset >= poolStart && slot.OriginalStringOffset < poolStart + 0x400)
            .ToArray();
        int poolEnd = Align4(nearbyPoolSlots.Max(slot =>
            slot.OriginalStringOffset + Encoding.ASCII.GetByteCount(slot.BeforeText) + 1));
        int nativePoolCapacity = poolEnd - poolStart;
        if (nativePoolCapacity is < 400 or > 1024)
            throw new InvalidOperationException($"The native level-name pool had an unexpected {nativePoolCapacity}-byte extent.");
        for (int offset = poolStart; offset < poolEnd; offset++)
        {
            byte value = exeBytes[offset];
            if (value != 0 && value is not (>= (byte)'A' and <= (byte)'Z') && value is not (byte)' ' and not (byte)'\'')
                throw new InvalidOperationException($"The native level-name pool contains an unexpected byte at executable offset 0x{offset:X}.");
        }

        HashSet<int> tablePointerLocations = slots
            .Select(slot => slot.PointerTableOffset + slot.TableIndex * sizeof(uint))
            .ToHashSet();
        HashSet<int> protectedStringOffsets = FindExternallyReferencedPoolStrings(
            exeBytes,
            slots,
            tableLayout.LoadAddress,
            poolStart,
            poolEnd,
            tablePointerLocations);
        NameTableSlot gnorcGnexus = slots.Single(slot =>
            slot.TableKind == LevelTextTableKind.LevelNames && slot.TableIndex == 30);
        if (!string.Equals(gnorcGnexus.BeforeText, "GNORC GNEXUS", StringComparison.Ordinal))
            throw new InvalidOperationException("The protected GNORC GNEXUS native string did not match this supported Spyro executable.");
        protectedStringOffsets.Add(gnorcGnexus.OriginalStringOffset);

        List<PoolRange> protectedRanges = protectedStringOffsets
            .Select(offset =>
            {
                NameTableSlot slot = slots.First(candidate => candidate.OriginalStringOffset == offset);
                return new PoolRange(offset, Encoding.ASCII.GetByteCount(slot.BeforeText) + 1);
            })
            .OrderBy(range => range.Start)
            .ToList();
        List<PoolRange> writableRanges = BuildWritablePoolRanges(poolStart, poolEnd, protectedRanges);
        int writableCapacity = writableRanges.Sum(range => range.Length);

        Dictionary<string, int> allocatedTextOffsets = new(StringComparer.Ordinal);
        foreach (int protectedOffset in protectedStringOffsets)
        {
            NameTableSlot slot = slots.First(candidate => candidate.OriginalStringOffset == protectedOffset);
            allocatedTextOffsets.TryAdd(slot.BeforeText, protectedOffset);
        }

        NameTableSlot[] packedSlots = slots
            .Where(slot =>
                !protectedStringOffsets.Contains(slot.OriginalStringOffset) &&
                (IsWithinPool(slot.OriginalStringOffset, poolStart, poolEnd) || slot.IsEdited))
            .ToArray();
        string[] textsToAllocate = packedSlots
            .Select(slot => slot.AfterText)
            .Where(text => !allocatedTextOffsets.ContainsKey(text))
            .Distinct(StringComparer.Ordinal)
            .OrderByDescending(text => Encoding.ASCII.GetByteCount(text) + 1)
            .ThenBy(text => text, StringComparer.Ordinal)
            .ToArray();
        int requiredBytes = textsToAllocate.Sum(text => Encoding.ASCII.GetByteCount(text) + 1);
        if (requiredBytes > writableCapacity)
        {
            int overflow = requiredBytes - writableCapacity;
            throw new InvalidOperationException(
                $"The saved longer names need {overflow} more byte(s) than Spyro's shared native name pool. " +
                $"Shorten the saved names by at least {overflow} character(s), then create the BIN again.");
        }

        int[] rangeCursors = writableRanges.Select(range => range.Start).ToArray();
        foreach (string text in textsToAllocate)
        {
            int byteLength = Encoding.ASCII.GetByteCount(text) + 1;
            int bestRange = -1;
            int bestRemaining = int.MaxValue;
            for (int index = 0; index < writableRanges.Count; index++)
            {
                PoolRange range = writableRanges[index];
                int remaining = range.End - rangeCursors[index];
                if (remaining >= byteLength && remaining - byteLength < bestRemaining)
                {
                    bestRange = index;
                    bestRemaining = remaining - byteLength;
                }
            }

            if (bestRange < 0)
            {
                throw new InvalidOperationException(
                    "The saved longer names fit by byte count but cannot be placed around a protected native string. " +
                    "Shorten one of the longest saved names and create the BIN again.");
            }

            allocatedTextOffsets[text] = rangeCursors[bestRange];
            rangeCursors[bestRange] += byteLength;
        }

        Dictionary<(LevelTextTableKind Kind, int Index), int> finalStringOffsets = [];
        foreach (NameTableSlot slot in slots)
        {
            int finalOffset;
            if (protectedStringOffsets.Contains(slot.OriginalStringOffset))
            {
                finalOffset = slot.OriginalStringOffset;
            }
            else if (IsWithinPool(slot.OriginalStringOffset, poolStart, poolEnd) || slot.IsEdited)
            {
                finalOffset = allocatedTextOffsets[slot.AfterText];
            }
            else
            {
                finalOffset = slot.OriginalStringOffset;
            }
            finalStringOffsets[(slot.TableKind, slot.TableIndex)] = finalOffset;
        }

        List<LevelTextBinaryPatch> binaryPatches = [];
        foreach (PoolRange range in writableRanges)
        {
            byte[] afterBytes = new byte[range.Length];
            foreach ((string text, int offset) in allocatedTextOffsets)
            {
                if (offset < range.Start || offset >= range.End)
                    continue;
                byte[] textBytes = Encoding.ASCII.GetBytes(text + "\0");
                textBytes.CopyTo(afterBytes, offset - range.Start);
            }
            AddBinaryPatchIfChanged(
                binaryPatches,
                layout,
                executable,
                exeBytes,
                "name-pool",
                range.Start,
                afterBytes,
                "Repacked native level names while preserving directly referenced strings in place.");
        }

        AddPointerTablePatch(LevelTextTableKind.HomeworldNames, HomeworldNameCount, tableLayout.HomeworldNamesTableOffset);
        AddPointerTablePatch(LevelTextTableKind.LevelNames, LevelNameCount, tableLayout.LevelNamesTableOffset);

        LevelTextReplacement[] homeworldEdits = edits
            .Where(edit => edit.Target.TableKind == LevelTextTableKind.HomeworldNames)
            .ToArray();
        if (homeworldEdits.Length > 0)
        {
            int lengthTableOffset = FindUniqueSequence(exeBytes, NativeHomeworldNameLengths, "homeworld title-length table");
            byte[] lengths = exeBytes.AsSpan(lengthTableOffset, NativeHomeworldNameLengths.Length).ToArray();
            foreach (LevelTextReplacement edit in homeworldEdits)
                lengths[edit.Target.TableIndex] = checked((byte)edit.ReplacementText.Count(ch => ch != ' '));
            AddBinaryPatchIfChanged(
                binaryPatches,
                layout,
                executable,
                exeBytes,
                "homeworld-title-lengths",
                lengthTableOffset,
                lengths,
                "Updated the title animation's visible-character count for edited homeworld names.");
        }

        DateTimeOffset generatedAt = DateTimeOffset.UtcNow;
        List<LevelTextPatchPlan> semanticPatches = [];
        foreach (LevelTextReplacement edit in edits)
        {
            NameTableSlot slot = slots.Single(candidate =>
                candidate.TableKind == edit.Target.TableKind && candidate.TableIndex == edit.Target.TableIndex);
            int finalOffset = finalStringOffsets[(slot.TableKind, slot.TableIndex)];
            uint finalPointer = FileOffsetToPointer(finalOffset, tableLayout.LoadAddress);
            byte[] afterBytes = Encoding.ASCII.GetBytes(edit.ReplacementText + "\0");
            byte[] beforeBytes = exeBytes.AsSpan(finalOffset, afterBytes.Length).ToArray();
            string scope = edit.Target.AffectsPortalLettering
                ? "portal lettering, transition text, and guidebook name"
                : "homeworld title, balloon transition text, and guidebook heading";
            semanticPatches.Add(new LevelTextPatchPlan(
                GeneratedAt: generatedAt,
                SourceImagePath: sourceImagePath,
                OutputImagePath: outputImagePath,
                OutputCuePath: outputCuePath,
                TargetLevelKey: edit.Target.LevelKey,
                LevelDisplayName: edit.Target.DisplayName,
                OriginalName: edit.Target.OriginalText,
                BeforeName: slot.BeforeText,
                ReplacementName: edit.ReplacementText,
                TableKind: edit.Target.TableKind,
                TableIndex: edit.Target.TableIndex,
                PointerTableFileOffset: slot.PointerTableOffset,
                StringPointer: $"0x{finalPointer:X8}",
                ExeName: executable.Name,
                ExeLba: executable.Lba,
                ExeFileOffset: finalOffset,
                ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, executable.Lba, finalOffset),
                ByteLength: afterBytes.Length,
                BeforeHexPreview: ToHex(beforeBytes),
                AfterHexPreview: ToHex(afterBytes),
                SectorSize: layout.SectorSize,
                UserOffset: layout.UserOffset,
                Notes:
                [
                    $"Patches the indexed {edit.Target.TableKind} slot used for {scope}.",
                    "The native shared name pool was repacked and this slot's pointer was updated.",
                    $"This target allows up to {edit.Target.MaxLength} characters.",
                    "Protected native strings with direct code references remain at their original addresses."
                ]));
        }

        int bytesUsed = allocatedTextOffsets
            .Where(pair => writableRanges.Any(range => pair.Value >= range.Start && pair.Value < range.End))
            .Sum(pair => Encoding.ASCII.GetByteCount(pair.Key) + 1);
        return new LevelTextBatchPatchPlan(
            GeneratedAt: generatedAt,
            SourceImagePath: sourceImagePath,
            OutputImagePath: outputImagePath,
            OutputCuePath: outputCuePath,
            PatchCount: semanticPatches.Count,
            LevelNames: semanticPatches.Select(patch => patch.LevelDisplayName).ToArray(),
            Patches: semanticPatches,
            StorageMode: "repacked-name-pool",
            StringPoolFileOffset: poolStart,
            StringPoolCapacity: writableCapacity,
            StringPoolBytesUsed: bytesUsed,
            StringPoolBytesRemaining: writableCapacity - bytesUsed,
            BinaryPatchCount: binaryPatches.Count,
            BinaryPatches: binaryPatches);

        void AddNameTableSlots(LevelTextTableKind tableKind, int count, int pointerTableOffset)
        {
            for (int index = 0; index < count; index++)
            {
                uint pointer = ReadUInt32(exeBytes, pointerTableOffset + index * sizeof(uint));
                if (!TryPointerToFileOffset(pointer, tableLayout.LoadAddress, exeBytes.Length, out int stringOffset) ||
                    !TryReadName(exeBytes, pointer, tableLayout.LoadAddress, out string beforeText))
                {
                    throw new InvalidOperationException($"Could not resolve {tableKind} slot {index} while repacking level names.");
                }

                bool isEdited = editsBySlot.TryGetValue((tableKind, index), out LevelTextReplacement? edit);
                slots.Add(new NameTableSlot(
                    tableKind,
                    index,
                    pointerTableOffset,
                    pointer,
                    stringOffset,
                    beforeText,
                    isEdited ? edit!.ReplacementText : beforeText,
                    isEdited));
            }
        }

        void AddPointerTablePatch(LevelTextTableKind tableKind, int count, int pointerTableOffset)
        {
            byte[] pointers = exeBytes.AsSpan(pointerTableOffset, count * sizeof(uint)).ToArray();
            for (int index = 0; index < count; index++)
            {
                int finalOffset = finalStringOffsets[(tableKind, index)];
                BinaryPrimitives.WriteUInt32LittleEndian(
                    pointers.AsSpan(index * sizeof(uint), sizeof(uint)),
                    FileOffsetToPointer(finalOffset, tableLayout.LoadAddress));
            }
            AddBinaryPatchIfChanged(
                binaryPatches,
                layout,
                executable,
                exeBytes,
                tableKind == LevelTextTableKind.HomeworldNames ? "homeworld-name-pointers" : "level-name-pointers",
                pointerTableOffset,
                pointers,
                "Updated indexed name pointers to the guarded shared-pool locations.");
        }
    }

    private static HashSet<int> FindExternallyReferencedPoolStrings(
        byte[] exeBytes,
        IReadOnlyList<NameTableSlot> slots,
        uint loadAddress,
        int poolStart,
        int poolEnd,
        IReadOnlySet<int> tablePointerLocations)
    {
        HashSet<int> protectedOffsets = [];
        foreach (int stringOffset in slots
            .Select(slot => slot.OriginalStringOffset)
            .Where(offset => IsWithinPool(offset, poolStart, poolEnd))
            .Distinct())
        {
            uint pointer = FileOffsetToPointer(stringOffset, loadAddress);
            byte[] pointerBytes = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(pointerBytes, pointer);
            int referenceOffset = DiscImage.IndexOfBytes(exeBytes, pointerBytes, 0);
            while (referenceOffset >= 0)
            {
                if (!tablePointerLocations.Contains(referenceOffset))
                {
                    protectedOffsets.Add(stringOffset);
                    break;
                }
                referenceOffset = DiscImage.IndexOfBytes(exeBytes, pointerBytes, referenceOffset + 1);
            }
        }
        return protectedOffsets;
    }

    private static List<PoolRange> BuildWritablePoolRanges(int poolStart, int poolEnd, IReadOnlyList<PoolRange> protectedRanges)
    {
        List<PoolRange> ranges = [];
        int cursor = poolStart;
        foreach (PoolRange protectedRange in protectedRanges)
        {
            if (protectedRange.Start < poolStart || protectedRange.End > poolEnd)
                throw new InvalidOperationException("A protected native name falls outside the detected level-name pool.");
            if (protectedRange.Start > cursor)
                ranges.Add(new PoolRange(cursor, protectedRange.Start - cursor));
            cursor = Math.Max(cursor, protectedRange.End);
        }
        if (cursor < poolEnd)
            ranges.Add(new PoolRange(cursor, poolEnd - cursor));
        return ranges;
    }

    private static void AddBinaryPatchIfChanged(
        ICollection<LevelTextBinaryPatch> patches,
        DiscLayout layout,
        DiscFileRecord executable,
        byte[] exeBytes,
        string kind,
        int fileOffset,
        byte[] afterBytes,
        string note)
    {
        byte[] beforeBytes = exeBytes.AsSpan(fileOffset, afterBytes.Length).ToArray();
        if (beforeBytes.SequenceEqual(afterBytes))
            return;
        patches.Add(new LevelTextBinaryPatch(
            Kind: kind,
            ExeLba: executable.Lba,
            ExeFileOffset: fileOffset,
            ImageOffset: DiscImage.ConvertFileOffsetToImageOffset(layout, executable.Lba, fileOffset),
            ByteLength: afterBytes.Length,
            BeforeHexPreview: ToHex(beforeBytes),
            AfterHexPreview: ToHex(afterBytes),
            SectorSize: layout.SectorSize,
            UserOffset: layout.UserOffset,
            Note: note));
    }

    private static int FindUniqueSequence(byte[] bytes, byte[] sequence, string description)
    {
        List<int> offsets = [];
        int offset = DiscImage.IndexOfBytes(bytes, sequence, 0);
        while (offset >= 0)
        {
            offsets.Add(offset);
            offset = DiscImage.IndexOfBytes(bytes, sequence, offset + 1);
        }
        if (offsets.Count != 1)
            throw new InvalidOperationException($"Expected one {description}, found {offsets.Count}.");
        return offsets[0];
    }

    private static bool IsWithinPool(int offset, int poolStart, int poolEnd) => offset >= poolStart && offset < poolEnd;

    private static int Align4(int value) => (value + 3) & ~3;

    private static uint FileOffsetToPointer(int fileOffset, uint loadAddress) =>
        checked(loadAddress + (uint)(fileOffset - PsxExeHeaderSize));

    private static LevelTextTableLayout LocateNameTables(byte[] exeBytes)
    {
        if (exeBytes.Length < PsxExeHeaderSize || !exeBytes.AsSpan(0, 8).SequenceEqual("PS-X EXE"u8))
            throw new InvalidOperationException("The selected executable does not have a valid PS-X EXE header.");

        uint loadAddress = ReadUInt32(exeBytes, PsxExeLoadAddressOffset);
        List<int> candidates = [];
        int returnStringOffset = DiscImage.IndexOfBytes(exeBytes, ReturnHomeBytes, PsxExeHeaderSize);
        while (returnStringOffset >= 0)
        {
            uint returnPointer = checked(loadAddress + (uint)(returnStringOffset - PsxExeHeaderSize));
            byte[] pointerBytes = new byte[sizeof(uint)];
            BinaryPrimitives.WriteUInt32LittleEndian(pointerBytes, returnPointer);
            int pointerOffset = DiscImage.IndexOfBytes(exeBytes, pointerBytes, 0);
            while (pointerOffset >= 0)
            {
                int candidate = pointerOffset - ReturnHomeIndex * sizeof(uint);
                if (ValidateLevelNameTable(exeBytes, candidate, loadAddress, returnPointer))
                    candidates.Add(candidate);
                pointerOffset = DiscImage.IndexOfBytes(exeBytes, pointerBytes, pointerOffset + 1);
            }

            returnStringOffset = DiscImage.IndexOfBytes(exeBytes, ReturnHomeBytes, returnStringOffset + 1);
        }

        int[] uniqueCandidates = candidates.Distinct().ToArray();
        if (uniqueCandidates.Length != 1)
            throw new InvalidOperationException($"Expected one indexed level-name pointer table, found {uniqueCandidates.Length}.");

        int levelNamesOffset = uniqueCandidates[0];
        int homeworldNamesOffset = levelNamesOffset - HomeworldNameCount * sizeof(uint);
        if (!ValidateHomeworldNameTable(exeBytes, homeworldNamesOffset, loadAddress))
            throw new InvalidOperationException("The homeworld-name pointer table next to the level-name table did not validate.");

        return new LevelTextTableLayout(loadAddress, homeworldNamesOffset, levelNamesOffset);
    }

    private static bool ValidateLevelNameTable(byte[] exeBytes, int tableOffset, uint loadAddress, uint returnPointer)
    {
        if (tableOffset < PsxExeHeaderSize || tableOffset + LevelNameCount * sizeof(uint) > exeBytes.Length)
            return false;

        uint[] pointers = new uint[LevelNameCount];
        for (int i = 0; i < pointers.Length; i++)
        {
            pointers[i] = ReadUInt32(exeBytes, tableOffset + i * sizeof(uint));
            if (!TryReadName(exeBytes, pointers[i], loadAddress, out _))
                return false;
        }

        return pointers[ReturnHomeIndex] == returnPointer &&
            pointers[0] == pointers[6] &&
            pointers[0] == pointers[12] &&
            pointers[0] == pointers[18] &&
            pointers[0] == pointers[24] &&
            TryReadName(exeBytes, pointers[0], loadAddress, out string home) && home == "HOME" &&
            TryReadName(exeBytes, pointers[ReturnHomeIndex], loadAddress, out string returnHome) && returnHome == "RETURN HOME";
    }

    private static bool ValidateHomeworldNameTable(byte[] exeBytes, int tableOffset, uint loadAddress)
    {
        if (tableOffset < PsxExeHeaderSize || tableOffset + HomeworldNameCount * sizeof(uint) > exeBytes.Length)
            return false;

        for (int i = 0; i < HomeworldNameCount; i++)
        {
            uint pointer = ReadUInt32(exeBytes, tableOffset + i * sizeof(uint));
            if (!TryReadName(exeBytes, pointer, loadAddress, out string name) || name.Length > 20)
                return false;
        }

        uint finalPointer = ReadUInt32(exeBytes, tableOffset + (HomeworldNameCount - 1) * sizeof(uint));
        return TryReadName(exeBytes, finalPointer, loadAddress, out string finalName) && finalName == "THIGH MASTERS";
    }

    private static bool TryReadName(byte[] exeBytes, uint pointer, uint loadAddress, out string name)
    {
        name = "";
        if (!TryPointerToFileOffset(pointer, loadAddress, exeBytes.Length, out int offset))
            return false;

        int end = offset;
        while (end < exeBytes.Length && end - offset <= 31 && exeBytes[end] != 0)
        {
            byte value = exeBytes[end];
            if (value is not (>= (byte)'A' and <= (byte)'Z') && value is not (byte)' ' and not (byte)'\'')
                return false;
            end++;
        }

        if (end == offset || end >= exeBytes.Length || end - offset > 31 || exeBytes[end] != 0)
            return false;

        name = Encoding.ASCII.GetString(exeBytes, offset, end - offset);
        return true;
    }

    private static bool TryPointerToFileOffset(uint pointer, uint loadAddress, int exeLength, out int fileOffset)
    {
        fileOffset = -1;
        if (pointer < loadAddress)
            return false;

        ulong offset = (ulong)(pointer - loadAddress) + PsxExeHeaderSize;
        if (offset >= (ulong)exeLength)
            return false;

        fileOffset = (int)offset;
        return true;
    }

    private static async Task WriteImageAsync(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        IReadOnlyList<LevelTextPatchPlan> patches,
        CancellationToken cancellationToken)
    {
        await DiscImageWorkingCopy.StageAsync(
            sourceImagePath,
            outputImagePath,
            consumeDisposableSource: false,
            cancellationToken);
        await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            foreach (LevelTextPatchPlan patch in patches)
            {
                DiscImage.WriteFileBytes(
                    stream,
                    new DiscLayout(patch.SectorSize, patch.UserOffset, 0, 0),
                    patch.ExeLba,
                    patch.ExeFileOffset,
                    HexToBytes(patch.AfterHexPreview));
            }
        }

        string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
    }

    private static async Task WriteBatchImageAsync(
        string sourceImagePath,
        string sourceCuePath,
        string outputImagePath,
        string outputCuePath,
        LevelTextBatchPatchPlan plan,
        bool consumeDisposableSourceImage,
        CancellationToken cancellationToken)
    {
        if (plan.BinaryPatches.Count == 0)
        {
            if (consumeDisposableSourceImage)
            {
                await DiscImageWorkingCopy.StageAsync(
                    sourceImagePath,
                    outputImagePath,
                    consumeDisposableSource: true,
                    cancellationToken);
                await ApplyFixedSlotPatchesAsync(
                    outputImagePath,
                    outputCuePath,
                    sourceCuePath,
                    plan.Patches,
                    cancellationToken);
            }
            else
            {
                await WriteImageAsync(
                    sourceImagePath,
                    sourceCuePath,
                    outputImagePath,
                    outputCuePath,
                    plan.Patches,
                    cancellationToken);
            }
            return;
        }

        await DiscImageWorkingCopy.StageAsync(
            sourceImagePath,
            outputImagePath,
            consumeDisposableSourceImage,
            cancellationToken);
        await using (FileStream stream = File.Open(outputImagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
        {
            foreach (LevelTextBinaryPatch patch in plan.BinaryPatches)
            {
                DiscImage.WriteFileBytes(
                    stream,
                    new DiscLayout(patch.SectorSize, patch.UserOffset, 0, 0),
                    patch.ExeLba,
                    patch.ExeFileOffset,
                    HexToBytes(patch.AfterHexPreview));
            }
        }

        string cueText = DiscImage.BuildCueText(sourceCuePath, Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(outputCuePath, cueText, Encoding.ASCII, cancellationToken);
    }

    private static async Task ApplyFixedSlotPatchesAsync(
        string outputImagePath,
        string outputCuePath,
        string sourceCuePath,
        IReadOnlyList<LevelTextPatchPlan> patches,
        CancellationToken cancellationToken)
    {
        await using (FileStream stream = File.Open(
                         outputImagePath,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.Read))
        {
            foreach (LevelTextPatchPlan patch in patches)
            {
                DiscImage.WriteFileBytes(
                    stream,
                    new DiscLayout(patch.SectorSize, patch.UserOffset, 0, 0),
                    patch.ExeLba,
                    patch.ExeFileOffset,
                    HexToBytes(patch.AfterHexPreview));
            }
        }

        string cueText = DiscImage.BuildCueText(
            sourceCuePath,
            Path.GetFileName(outputImagePath));
        await File.WriteAllTextAsync(
            outputCuePath,
            cueText,
            Encoding.ASCII,
            cancellationToken);
    }

    private static string ValidateReplacement(TextTargetEntry target, string replacementText)
    {
        string replacement = TextTargetCatalog.NormalizeReplacement(replacementText);
        if (!TextTargetCatalog.IsSafeReplacement(replacement, target.MaxLength))
            throw new InvalidOperationException($"Use {target.MaxLength} or fewer letters, spaces, or apostrophes for {target.OriginalText}.");
        return replacement;
    }

    private static bool IsExecutableName(string name)
    {
        string upper = name.ToUpperInvariant();
        return upper.StartsWith("SCUS", StringComparison.Ordinal)
            || upper.StartsWith("SCES", StringComparison.Ordinal)
            || upper.StartsWith("SCPS", StringComparison.Ordinal)
            || upper.StartsWith("SLUS", StringComparison.Ordinal)
            || upper.StartsWith("SLES", StringComparison.Ordinal)
            || upper.StartsWith("SLPS", StringComparison.Ordinal);
    }

    private static uint ReadUInt32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, sizeof(uint)));

    private static string ReadFixedSlotText(byte[] bytes)
    {
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0)
            length = bytes.Length;
        return Encoding.ASCII.GetString(bytes, 0, length);
    }

    private static JsonSerializerOptions NewJsonOptions() => new() { WriteIndented = true };

    private static string ToHex(byte[] bytes) => string.Join(" ", bytes.Select(value => $"{value:X2}"));

    private static byte[] HexToBytes(string text)
    {
        string[] parts = text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Select(part => Convert.ToByte(part, 16)).ToArray();
    }

    private static string ToSafeSlug(string value)
    {
        string slug = new string(value.ToLowerInvariant().Select(ch => char.IsAsciiLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        return string.IsNullOrWhiteSpace(slug) ? "text" : slug;
    }

    private sealed record LevelTextTableLayout(uint LoadAddress, int HomeworldNamesTableOffset, int LevelNamesTableOffset);

    private sealed record NameTableSlot(
        LevelTextTableKind TableKind,
        int TableIndex,
        int PointerTableOffset,
        uint OriginalPointer,
        int OriginalStringOffset,
        string BeforeText,
        string AfterText,
        bool IsEdited);

    private sealed record PoolRange(int Start, int Length)
    {
        public int End => Start + Length;
    }
}

public sealed record LevelTextPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    TextTargetEntry Target,
    string ReplacementText,
    bool WriteImage);

public sealed record LevelTextPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    LevelTextPatchPlan Plan,
    bool WroteImage);

public sealed record LevelTextReplacement(TextTargetEntry Target, string ReplacementText);

public sealed record LevelTextBatchPatchRequest(
    string SourceImagePath,
    string SourceCuePath,
    string OutputPrefix,
    IReadOnlyList<LevelTextReplacement> Edits,
    bool WriteImage,
    bool ConsumeDisposableSourceImage = false);

public sealed record LevelTextBatchPatchResult(
    string OutputImagePath,
    string OutputCuePath,
    string OutputPlanPath,
    LevelTextBatchPatchPlan Plan,
    bool WroteImage);

public sealed record LevelTextBatchPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    int PatchCount,
    IReadOnlyList<string> LevelNames,
    IReadOnlyList<LevelTextPatchPlan> Patches,
    string StorageMode,
    int StringPoolFileOffset,
    int StringPoolCapacity,
    int StringPoolBytesUsed,
    int StringPoolBytesRemaining,
    int BinaryPatchCount,
    IReadOnlyList<LevelTextBinaryPatch> BinaryPatches);

public sealed record LevelTextBinaryPatch(
    string Kind,
    int ExeLba,
    int ExeFileOffset,
    long ImageOffset,
    int ByteLength,
    string BeforeHexPreview,
    string AfterHexPreview,
    int SectorSize,
    int UserOffset,
    string Note);

public sealed record LevelTextPatchPlan(
    DateTimeOffset GeneratedAt,
    string SourceImagePath,
    string OutputImagePath,
    string OutputCuePath,
    string TargetLevelKey,
    string LevelDisplayName,
    string OriginalName,
    string BeforeName,
    string ReplacementName,
    LevelTextTableKind TableKind,
    int TableIndex,
    int PointerTableFileOffset,
    string StringPointer,
    string ExeName,
    int ExeLba,
    int ExeFileOffset,
    long ImageOffset,
    int ByteLength,
    string BeforeHexPreview,
    string AfterHexPreview,
    int SectorSize,
    int UserOffset,
    IReadOnlyList<string> Notes);
