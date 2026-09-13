using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Shared exact-original flight image mechanics. No publication, saved-project
/// decision or runtime capability is issued here. Research naming/receipts stay
/// in their existing wrapper; production exposes no diagnostic selector input.
/// </summary>
internal sealed class NativeFreeFlightImageComposition
{
    internal const int TargetLevelId = 14;
    internal const int RequiredNormalBufferGap = 0xA780;
    private readonly byte[] _wad, _root, _executable, _analysis;
    private readonly byte _wadFlags, _exeFlags;

    private NativeFreeFlightImageComposition(string source, NativePlayerAnimationMergePlan? merge,
        NativeFreeFlightPatchPlan patch, NativeLevelRuntimeMemoryReport? memory, DiscLayout layout,
        byte[] wad, byte[] root, byte[] executable, byte[] analysis,
        IReadOnlyList<NativeFreeFlightResearchEntry> entries, byte wadFlags, byte exeFlags, MobySourcePatch? selector)
    {
        SourceImagePath = source; AnimationMerge = merge; ExecutablePatch = patch; Memory = memory; Layout = layout;
        _wad = wad; _root = root; _executable = executable; _analysis = analysis;
        Entries = Array.AsReadOnly(entries.ToArray()); _wadFlags = wadFlags; _exeFlags = exeFlags;
        TestSelector = selector;
    }

    internal string SourceImagePath { get; }
    internal NativePlayerAnimationMergePlan? AnimationMerge { get; }
    internal NativeFreeFlightPatchPlan ExecutablePatch { get; }
    internal NativeLevelRuntimeMemoryReport? Memory { get; }
    internal DiscLayout Layout { get; }
    internal IReadOnlyList<NativeFreeFlightResearchEntry> Entries { get; }
    internal MobySourcePatch? TestSelector { get; }
    internal int RelocatedExecutableLba => AnimationMerge == null ? 53875 : 53882;
    internal string ExpectedExecutableSha256 => Hash(_executable);
    internal string ExpectedWadSha256 => Hash(_wad);
    internal string ExpectedRootSha256 => Hash(_root);
    internal byte[] CopyAnalysisBytes() => _analysis.ToArray();
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal byte[] ApplyToExecutable(ReadOnlySpan<byte> original) => ComposeExecutable(original, ExecutablePatch, TestSelector);
    internal NativeSkyRelocationPayload CopyInsertionPayload() => AnimationMerge?.CopyInsertionPayload()
        ?? throw new InvalidDataException("Enabled flight composition requires its exact player-stream insertion.");

    internal static Task<NativeFreeFlightImageComposition> CaptureResearchAsync(string originalImagePath,
        LevelCatalog catalog, bool testLevelSelector, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return CaptureEnabledAsync(originalImagePath, catalog, testLevelSelector, cancellationToken);
    }

    internal static async Task<NativeFreeFlightImageComposition> CaptureProductionAsync(string originalImagePath,
        int? enabledLevelId, CancellationToken cancellationToken)
    {
        if (enabledLevelId != null && enabledLevelId != TargetLevelId)
            throw new InvalidDataException("Production flight ON supports only the exact Toasty target.");
        cancellationToken.ThrowIfCancellationRequested();
        if (enabledLevelId != null)
            return await CaptureEnabledAsync(originalImagePath, null, false, cancellationToken);
        string source = Path.GetFullPath(originalImagePath);
        var patch = await NativeFreeFlightPatchPlan.CaptureAsync(source, TargetLevelId, false, cancellationToken);
        var result = new NativeFreeFlightImageComposition(source, null, patch, null, DiscImage.DetectLayout(source),
            [], [], patch.CopySourceExecutable(), [], [], 0, 0, null);
        await result.RequireOriginalAsync(cancellationToken);
        return result;
    }

    private static async Task<NativeFreeFlightImageComposition> CaptureEnabledAsync(string originalImagePath,
        LevelCatalog? catalog, bool testLevelSelector, CancellationToken cancellationToken)
    {
        string source = Path.GetFullPath(originalImagePath);
        var patch = await NativeFreeFlightPatchPlan.CaptureAsync(source, TargetLevelId, true, cancellationToken);
        var player = await NativePlayerAnimationSource.CaptureAsync(source, cancellationToken);
        var merge = await player.CompileMergeAsync(source, 64, TargetLevelId, [33, 34, 35], cancellationToken);
        // Research retains its existing complete inventory/catalogue policy.
        // Production uses the actual player source's already-bound level owner.
        var inventoryLevel = catalog == null ? null : NativeMobySourceInventory.Capture(source, catalog)
            .Levels.Single(l => l.LevelId == TargetLevelId);
        var level = player.Levels.Single(l => l.LevelId == TargetLevelId);
        using FileStream image = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(source);
        // Issue the existing guarded selector from the untouched original EXE,
        // never from the already-patched candidate or caller-authored metadata.
        MobySourcePatch? selector = testLevelSelector
            ? TestLevelWarpPatch.CreateGuarded(image, layout, catalog!.Levels.Single(l => l.LevelId == TargetLevelId)) : null;
        byte[] originalExe = patch.CopySourceExecutable();
        byte[] executable = ComposeExecutable(originalExe, patch, selector);
        byte[] originalData = DiscImage.ReadFileBytes(image, layout, 37, level.DataWadOffset, level.DataByteLength);
        byte[] peteHeader = DiscImage.ReadFileBytes(image, layout, 37, 0x7B8800, 0x800);
        byte[] pete = DiscImage.ReadFileBytes(image, layout, 37, 0x7B9000, 0x39C48);
        var memory = (inventoryLevel == null
            ? NativeLevelRuntimeMemoryReport.CaptureOriginal(level, originalData, originalExe, peteHeader, pete)
            : NativeLevelRuntimeMemoryReport.CaptureOriginal(inventoryLevel, originalData, originalExe, peteHeader, pete))
            .AnalyzePlayerMerge(merge);
        if (merge.ExactInsertedByteLength != 0x33C8 || merge.SectorRoundedGrowth != 0x3800 ||
            merge.FinalDataSha256 != "251770f0eade8981fd6fca94d3a0bf434baeaff23ee83520dda0bf9d78079c5b" ||
            memory.CopyBufferAddress != 0x80084A10 || memory.ModelRamStart != 0x800F2C30 ||
            memory.SceneRamStart != 0x8016FC30 || memory.SceneReadEnd != 0x8017D430 ||
            memory.NormalReservedBufferStart != 0x80187BB0 ||
            !memory.KnownPostLoadExtentsDisjoint || memory.NormalReservedBufferGapBytes != RequiredNormalBufferGap)
            throw new InvalidDataException("Research recipe lost its exact positive normal-load extent precondition.");
        // The patch is not allowed to change any copy-buffer/loader/normal-PETE
        // instruction or table: all non-window bytes, not selected labels, agree.
        HashSet<int> owned = patch.Instructions.SelectMany(w => Enumerable.Range(w.ExecutableOffset, w.ByteLength)).ToHashSet();
        if (selector != null)
        {
            int[] selectorBytes = Enumerable.Range(checked((int)TestLevelWarpPatch.ExecutableFileOffset), selector.ByteLength).ToArray();
            if (selectorBytes.Any(owned.Contains)) throw new InvalidDataException("Research executable stages overlap.");
            owned.UnionWith(selectorBytes);
        }
        if (Enumerable.Range(0, executable.Length).Any(i => !owned.Contains(i) && originalExe[i] != executable[i]) ||
            patch.Instructions.Any(w => w.ExecutableOffset < 0x31000 || w.ExecutableOffset + w.ByteLength > 0x37000) ||
            !originalExe.AsSpan(0x4B130, 16).SequenceEqual(executable.AsSpan(0x4B130, 16)))
            throw new InvalidDataException("Research executable changed an unowned native memory/loader consumer.");

        byte[] header = DiscImage.ReadFileBytes(image, layout, 37, 0, 0x800);
        List<NativeFreeFlightResearchEntry> entries = [];
        var sourceEntries = Enumerable.Range(0, 256).Select(i =>
            (Index: i, Offset: checked((int)Word(header, i * 8)), Length: checked((int)Word(header, i * 8 + 4))))
            .Where(e => e.Offset != 0 || e.Length != 0).OrderBy(e => e.Offset).ToArray();
        byte[] wad = new byte[0x692A800];
        int oldCursor = 0x800, finalCursor = 0x800;
        foreach (var entry in sourceEntries)
        {
            if (entry.Offset != oldCursor || entry.Length <= 0 || entry.Length % 0x800 != 0 ||
                entry.Offset + (long)entry.Length > 0x6927000)
                throw new InvalidDataException("Research WAD source owners are not complete and packed.");
            byte[] before = DiscImage.ReadFileBytes(image, layout, 37, entry.Offset, entry.Length);
            byte[] after = entry.Index == level.DataWadEntry ? merge.CopyFinalLevelData() : before;
            if (entry.Index == level.DataWadEntry && Hash(before) != merge.OriginalDataSha256)
                throw new InvalidDataException("Research target does not match the typed animation source owner.");
            entries.Add(new(entry.Index, entry.Offset, finalCursor, before.Length, after.Length, Hash(before), Hash(after)));
            Put(header, entry.Index * 8, (uint)finalCursor);
            Put(header, entry.Index * 8 + 4, (uint)after.Length);
            after.CopyTo(wad, finalCursor);
            oldCursor = checked(oldCursor + before.Length);
            finalCursor = checked(finalCursor + after.Length);
        }
        if (oldCursor != 0x6927000 || finalCursor != wad.Length || entries.Single(e => e.Index == 18).FinalSha256 != merge.FinalDataSha256)
            throw new InvalidDataException("Research WAD allocation or target identity did not balance.");
        header.CopyTo(wad, 0);
        byte[] analysis = JsonSerializer.SerializeToUtf8Bytes(new
        {
            wad = new { lba = 37, size = 0x6927000 },
            entries = sourceEntries.Select(e => new { index = e.Index, offset = e.Offset, size = e.Length })
        });
        byte[] root = DiscImage.ReadFileBytes(image, layout, layout.RootExtent, 0, layout.RootLength);
        var files = NativeWadGrowthSectorPreparation.ReadFlatRootFiles(image, layout);
        foreach (var change in new[] { ("WAD.WAD", 37, wad.Length), ("SCUS_942.28", 53882, executable.Length) })
        {
            var file = files.Single(f => f.Name == change.Item1);
            Both(root, file.RecordOffset + 2, (uint)change.Item2);
            Both(root, file.RecordOffset + 10, (uint)change.Item3);
        }
        byte Flags(int lba)
        {
            image.Position = (long)lba * 2352 + 18;
            return (byte)(image.ReadByte() & 0x81);
        }
        var result = new NativeFreeFlightImageComposition(source, merge, patch, memory, layout,
            wad, root, executable, analysis, entries, Flags(53874), Flags(54078), selector);
        await result.RequireOriginalAsync(cancellationToken);
        return result;
    }

    internal async Task RequireOriginalAsync(CancellationToken cancellationToken = default)
    {
        using FileStream image = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength ||
            Convert.ToHexStringLower(await SHA256.HashDataAsync(image, cancellationToken)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Position != image.Length || image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Flight source BIN changed its complete original identity.");
    }

    // The caller owns the private destination and analysis leaves. This method
    // never publishes artifacts or consumes the original source.
    internal async Task WriteInitialImageAsync(string outputImagePath, string analysisPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (AnimationMerge == null)
        {
            using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            using FileStream output = new(outputImagePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await source.CopyToAsync(output, cancellationToken);
            output.Flush(flushToDisk: true);
            return;
        }
        var payload = CopyInsertionPayload();
        var relocation = NativeSkyWadRelocator.BuildPlan(SourceImagePath, analysisPath, [payload]);
        RequireRelocation(relocation);
        await Task.Run(() => NativeSkyWadRelocator.WriteExpandedImage(SourceImagePath, outputImagePath,
            analysisPath, relocation, [payload]), cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    internal void ApplyExecutableToImage(string path)
    {
        DiscLayout liveLayout = DiscImage.DetectLayout(path);
        using FileStream output = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        DiscFileRecord liveExe = DiscImage.FindRootFileRecord(output, liveLayout, name => name == "SCUS_942.28");
        if (liveExe.Lba != RelocatedExecutableLba || liveExe.Size != 0x66000)
            throw new InvalidDataException("Flight composition lost its exact live executable owner.");
        byte[] current = DiscImage.ReadFileBytes(output, liveLayout, liveExe.Lba, 0, liveExe.Size);
        byte[] changed = ApplyToExecutable(current);
        if (!changed.AsSpan().SequenceEqual(_executable))
            throw new InvalidDataException("Flight executable differs from its independently bound byte plan.");
        if (AnimationMerge != null)
        {
            DiscImage.WriteFileBytes(output, liveLayout, liveExe.Lba, 0, changed);
            output.Flush(flushToDisk: true);
        }
    }

    internal void RequireRelocation(NativeSkyWadRelocationPlan relocation)
    {
        if (AnimationMerge == null || !relocation.Required || relocation.WadLba != 37 || relocation.OriginalWadSize != 0x6927000 ||
            relocation.ExpandedWadSize != _wad.Length || relocation.WadGrowthBytes != 0x3800 ||
            relocation.OriginalExecutableLba != 53875 || relocation.RelocatedExecutableLba != RelocatedExecutableLba ||
            relocation.ExecutableSize != _executable.Length || relocation.EntryGrowthBytes.Count != 1 ||
            relocation.EntryGrowthBytes.GetValueOrDefault(18) != 0x3800 ||
            relocation.RelocatedEntryOffsets.Count != Entries.Count ||
            Entries.Any(e => relocation.RelocatedEntryOffsets.GetValueOrDefault(e.Index, -1) != e.FinalOffset))
            throw new InvalidDataException("Shared relocation differs from the independently composed research byte owners.");
    }

    internal (string Hash, int Changed) VerifyImage(string path)
    {
        using FileStream source = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        using FileStream output = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (AnimationMerge == null)
            return NativeTownSquareClosurePhysicalReadback.Verify(source, output, NativeMobySourceInventory.OriginalImageByteLength,
                NativeMobySourceInventory.OriginalImageSha256, (_, before) => before);
        var files = NativeWadGrowthSectorPreparation.ReadFlatRootFiles(output, Layout);
        if (files.Single(f => f.Name == "SCUS_942.28").Lba != RelocatedExecutableLba ||
            files.Single(f => f.Name == "WAD.WAD").Size != _wad.Length)
            throw new InvalidDataException("Research output lost its live relocated ISO owners.");
        byte[] target = DiscImage.ReadFileBytes(output, Layout, 37, Entries.Single(e => e.Index == 18).FinalOffset,
            AnimationMerge.FinalDataByteLength);
        if (!target.AsSpan().SequenceEqual(AnimationMerge.CopyFinalLevelData()))
            throw new InvalidDataException("Complete research target differs from the independent typed animation merge.");
        return NativeTownSquareClosurePhysicalReadback.Verify(source, output, NativeMobySourceInventory.OriginalImageByteLength,
            NativeMobySourceInventory.OriginalImageSha256, ExpectedSector);
    }

    private byte[] ExpectedSector(int lba, byte[] before)
    {
        byte[] expected = before.ToArray();
        if (lba >= 54079 && lba < 54086)
            expected = RawMode2Form1SectorIntegrity.PrepareOwnedZeroPaddingSector(before, Layout, lba);
        if (lba >= 37 && lba < RelocatedExecutableLba)
            _wad.AsSpan((lba - 37) * 2048, 2048).CopyTo(expected.AsSpan(24));
        if (lba >= RelocatedExecutableLba && lba < 54086)
            _executable.AsSpan((lba - RelocatedExecutableLba) * 2048, 2048).CopyTo(expected.AsSpan(24));
        int rootOffset = (lba - Layout.RootExtent) * 2048;
        if (rootOffset >= 0 && rootOffset < _root.Length)
            _root.AsSpan(rootOffset, Math.Min(2048, _root.Length - rootOffset)).CopyTo(expected.AsSpan(24));
        if (lba is 53874 or 54078 or 53881 or 54085)
        {
            byte flags = lba == 53881 ? _wadFlags : lba == 54085 ? _exeFlags : (byte)0;
            expected[18] = expected[22] = (byte)((expected[18] & ~0x81) | flags);
        }
        return expected.AsSpan().SequenceEqual(before) ? expected : RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
    }

    private static byte[] ComposeExecutable(ReadOnlySpan<byte> original, NativeFreeFlightPatchPlan patch, MobySourcePatch? selector)
    {
        if (Hash(original) != NativeFreeFlightPatchPlan.SourceExecutableSha256)
            throw new InvalidDataException("Research executable composition requires the complete original preimage.");
        byte[] result = patch.ApplyToExecutable(original);
        if (selector == null) return result;
        int offset = checked((int)TestLevelWarpPatch.ExecutableFileOffset);
        byte[] before = Convert.FromHexString(selector.BeforeHexPreview.Replace(" ", "", StringComparison.Ordinal));
        byte[] after = Convert.FromHexString(selector.AfterHexPreview.Replace(" ", "", StringComparison.Ordinal));
        if (selector.Kind != TestLevelWarpPatch.PatchKind || before.Length != 8 || after.Length != 8 ||
            selector.ByteLength != 8 || !result.AsSpan(offset, 8).SequenceEqual(before) ||
            patch.Instructions.Any(w => w.ExecutableOffset < offset + 8 && w.ExecutableOffset + w.ByteLength > offset))
            throw new InvalidDataException("Source-issued diagnostic selector does not have a disjoint exact preimage.");
        after.CopyTo(result, offset);
        return result;
    }

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Both(byte[] bytes, int offset, uint value)
    {
        Put(bytes, offset, value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4), value);
    }
}
