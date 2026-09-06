using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Levels;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreeFlightResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreeFlightResearchEntry(int Index, int OriginalOffset, int FinalOffset,
    int OriginalLength, int FinalLength, string OriginalSha256, string FinalSha256);

/// <summary>
/// One exact-original Toasty research recipe. Known normal-load extents are a
/// precondition, not peak-memory, gameplay, or ordinary export approval.
/// </summary>
public sealed class NativeFreeFlightResearchCandidatePlan
{
    public const string Profile = "native-toasty-free-flight-research-v1";
    public const int TargetLevelId = 14;
    public const int RequiredNormalBufferGap = 0xA780;
    internal const string ImageName = "toasty-native-free-flight-r01.bin";
    internal const string CueName = "toasty-native-free-flight-r01.cue";
    internal const string ReceiptName = "native-free-flight.receipt.json";
    private readonly byte[] _wad, _root, _executable, _analysis;
    private readonly byte _wadFlags, _exeFlags;
    private readonly NativeFreeFlightResearchSourceCue _cue;

    private NativeFreeFlightResearchCandidatePlan(string source, NativeFreeFlightResearchSourceCue cue,
        NativePlayerAnimationMergePlan merge, NativeFreeFlightPatchPlan patch, NativeLevelRuntimeMemoryReport memory,
        DiscLayout layout, byte[] wad, byte[] root, byte[] executable, byte[] analysis,
        IReadOnlyList<NativeFreeFlightResearchEntry> entries, byte wadFlags, byte exeFlags)
    {
        SourceImagePath = source;
        _cue = cue;
        AnimationMerge = merge;
        ExecutablePatch = patch;
        Memory = memory;
        Layout = layout;
        _wad = wad;
        _root = root;
        _executable = executable;
        _analysis = analysis;
        Entries = Array.AsReadOnly(entries.ToArray());
        _wadFlags = wadFlags;
        _exeFlags = exeFlags;
        ExpectedExecutableSha256 = Hash(executable);
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Profile, sourceImageSha256 = NativeFreeFlightPatchPlan.SourceImageSha256,
            SourceImagePath, sourceCueSha256 = cue.SourceSha256, sourceCuePath = cue.CuePath,
            animationPlan = merge.PlanSha256, executablePlan = patch.PlanSha256,
            ExpectedExecutableSha256, expectedWadSha256 = Hash(wad), expectedRootSha256 = Hash(root),
            Memory, Entries, RuntimeVerified, NormalCreateBinApproved
        }));
    }

    public string SourceImagePath { get; }
    public string PlanSha256 { get; }
    public string ExpectedExecutableSha256 { get; }
    public int RelocatedExecutableLba => 53882;
    public NativePlayerAnimationMergePlan AnimationMerge { get; }
    public NativeFreeFlightPatchPlan ExecutablePatch { get; }
    public NativeLevelRuntimeMemoryReport Memory { get; }
    public IReadOnlyList<NativeFreeFlightResearchEntry> Entries { get; }
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
    internal DiscLayout Layout { get; }
    internal string SourceCuePath => _cue.CuePath;
    internal byte[] CopyCueBytes() => _cue.CopyOutputBytes();
    internal byte[] CopyAnalysisBytes() => _analysis.ToArray();
    internal byte[] CopyExecutable() => _executable.ToArray();
    internal NativeSkyRelocationPayload CopyInsertionPayload() => AnimationMerge.CopyInsertionPayload()
        ?? throw new InvalidDataException("Research profile requires its exact player-stream insertion.");

    public static async Task<NativeFreeFlightResearchCandidatePlan> CaptureAsync(string originalImagePath,
        string originalCuePath, LevelCatalog catalog, CancellationToken cancellationToken = default)
    {
        string source = Path.GetFullPath(originalImagePath);
        var cue = NativeFreeFlightResearchSourceCue.Capture(originalCuePath, source, ImageName);
        var patch = await NativeFreeFlightPatchPlan.CaptureAsync(source, TargetLevelId, true, cancellationToken);
        var player = await NativePlayerAnimationSource.CaptureAsync(source, cancellationToken);
        var merge = await player.CompileMergeAsync(source, 64, TargetLevelId, [33, 34, 35], cancellationToken);
        var inventory = NativeMobySourceInventory.Capture(source, catalog);
        var level = inventory.Levels.Single(l => l.LevelId == TargetLevelId);
        using FileStream image = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(source);
        byte[] originalExe = patch.CopySourceExecutable();
        byte[] executable = patch.ApplyToExecutable(originalExe);
        byte[] originalData = DiscImage.ReadFileBytes(image, layout, 37, level.DataWadOffset, level.SourceDataByteLength);
        byte[] peteHeader = DiscImage.ReadFileBytes(image, layout, 37, 0x7B8800, 0x800);
        byte[] pete = DiscImage.ReadFileBytes(image, layout, 37, 0x7B9000, 0x39C48);
        var memory = NativeLevelRuntimeMemoryReport.CaptureOriginal(level, originalData, originalExe, peteHeader, pete)
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
        var result = new NativeFreeFlightResearchCandidatePlan(source, cue, merge, patch, memory, layout,
            wad, root, executable, analysis, entries, Flags(53874), Flags(54078));
        await result.RequireFreshAsync(cancellationToken);
        return result;
    }

    public async Task RequireFreshAsync(CancellationToken cancellationToken = default)
    {
        _cue.RequireFresh();
        using FileStream image = new(SourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (image.Length != NativeMobySourceInventory.OriginalImageByteLength ||
            Convert.ToHexStringLower(await SHA256.HashDataAsync(image, cancellationToken)) != NativeMobySourceInventory.OriginalImageSha256 ||
            image.Position != image.Length || image.Length != NativeMobySourceInventory.OriginalImageByteLength)
            throw new InvalidDataException("Research source BIN changed its complete original identity.");
        _cue.RequireFresh();
    }

    internal void RequireRelocation(NativeSkyWadRelocationPlan relocation)
    {
        if (!relocation.Required || relocation.WadLba != 37 || relocation.OriginalWadSize != 0x6927000 ||
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

    internal byte[] BuildReceipt((string Hash, int Changed) verified) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        profile = Profile, planSha256 = PlanSha256, sourceImageSha256 = NativeFreeFlightPatchPlan.SourceImageSha256,
        SourceImagePath, sourceCuePath = SourceCuePath, sourceCueSha256 = _cue.SourceSha256,
        outputImageSha256 = verified.Hash, changedRawSectors = verified.Changed, outputCueSha256 = Hash(CopyCueBytes()),
        ExpectedExecutableSha256, animationMerge = AnimationMerge, executablePatch = ExecutablePatch,
        Memory, Entries, wholeImagePhysicalReadback = true, wholeTargetReadback = true,
        originalRowsModelsSceneOverlayUnchanged = true, sourceCueBoundAtFreshnessBoundaries = true,
        researchOnly = true, RuntimeVerified, NormalCreateBinApproved,
        exceptionRollbackPublication = true, crashAtomicPublication = false, hostileFilesystemRaceSafe = false,
        pending = new[] { "peak/transient/alternate-mode RAM", "full flight-state effects and sound closure",
            "native ascent/descent/controls/collision", "landing/death/respawn/level transitions", "ordinary editor integration" }
    }, new JsonSerializerOptions { WriteIndented = true });

    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static uint Word(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static void Put(byte[] bytes, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
    private static void Both(byte[] bytes, int offset, uint value)
    {
        Put(bytes, offset, value);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(offset + 4), value);
    }
}
