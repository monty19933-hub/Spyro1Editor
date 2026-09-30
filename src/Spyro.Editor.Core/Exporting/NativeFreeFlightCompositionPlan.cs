using System.Security.Cryptography;
using System.Buffers.Binary;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Universal;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Source-issued flight resources and executable policy for composition with
/// ordinary edits. Call MergeLevelData before the normal Moby/resource composer,
/// then use that composer's final RAM budget and pack its final level bytes.
/// This class does not publish a disc or claim native runtime acceptance.
/// </summary>
public sealed class NativeFreeFlightCompositionPlan
{
    private readonly byte[] _geometry;
    private readonly IReadOnlyList<int> _sharedIds;
    private readonly IReadOnlyDictionary<int, NativePlayerAnimationSourceLevel> _levels;
    private readonly uint _originalLevel62ReadEnd;

    private NativeFreeFlightCompositionPlan(NativePlayerAnimationSource source, byte[] geometry,
        NativeFreeFlightPatchPlan executable, uint originalLevel62ReadEnd)
    {
        _geometry = geometry.ToArray();
        _sharedIds = Array.AsReadOnly(source.SharedAnimationIds.ToArray());
        _levels = source.Levels.Where(l => IsNormalLevel(l.LevelId)).ToDictionary(l => l.LevelId);
        NormalLevelIds = Array.AsReadOnly(_levels.Keys.Order().ToArray());
        ExecutablePatch = executable;
        SharedGeometrySha256 = source.SharedGeometrySha256;
        _originalLevel62ReadEnd = originalLevel62ReadEnd;
    }

    public bool Enabled => ExecutablePatch.Enabled;
    public NativeFreeFlightPatchPlan ExecutablePatch { get; }
    public IReadOnlyList<int> NormalLevelIds { get; }
    public string SourceImageSha256 => NativeFreeFlightPatchPlan.SourceImageSha256;
    public string SharedGeometrySha256 { get; }
    public bool RuntimeVerified => false;

    public static bool IsNormalLevel(int levelId) =>
        levelId >= 10 && levelId <= 64 && levelId % 10 <= 4;

    public static async Task<NativeFreeFlightCompositionPlan> CaptureAsync(string originalImagePath,
        bool enabled, CancellationToken cancellationToken = default)
    {
        var source = await NativePlayerAnimationSource.CaptureAsync(originalImagePath, cancellationToken);
        var executable = await NativeFreeFlightPatchPlan.CaptureGlobalAsync(originalImagePath, enabled, cancellationToken);
        using FileStream image = new(originalImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        DiscLayout layout = DiscImage.DetectLayout(originalImagePath);
        byte[] package = DiscImage.ReadFileBytes(image, layout, 37,
            NativePlayerAnimationSource.SharedPlayerPackageWadOffset,
            NativePlayerAnimationSource.SharedPlayerPackageByteLength);
        if (Hash(package) != source.SharedPlayerPackageSha256)
            throw new InvalidDataException("Flight donor player package changed after source capture.");
        byte[] geometry = package[0xAA8..];
        if (Hash(geometry) != source.SharedGeometrySha256)
            throw new InvalidDataException("Flight donor geometry differs from its source-issued animation domain.");
        RequireResidentAirborne(package, executable.CopySourceExecutable());
        var owner62 = source.Levels.Single(l => l.LevelId == 62);
        byte[] original62 = DiscImage.ReadFileBytes(image, layout, 37, owner62.DataWadOffset, owner62.DataByteLength);
        if (Hash(original62) != owner62.DataSha256)
            throw new InvalidDataException("Original level62 memory owner changed after source capture.");
        var sourceBindings62 = UniversalExeBindings.Read(executable.CopySourceExecutable(), 62);
        uint original62End = UniversalMobyComposer.RuntimeEnd(new UniversalLevelData(original62),
            sourceBindings62[UniversalExeBindings.CopyBufferGlobal].Value);
        if ((long)UniversalMobyComposer.NormalBufferFloor - original62End != 700)
            throw new InvalidDataException("Original level62 reserved-buffer headroom changed.");
        cancellationToken.ThrowIfCancellationRequested();
        return new(source, geometry, executable, original62End);
    }

    /// <summary>
    /// Compose into the actual staged body, which may already contain ordinary
    /// edits. ID65 is admitted as a caller-issued fresh normal body by the same
    /// complete native schema; it is never represented as a retail source level.
    /// Native courses are exact no-ops. OFF is an exact no-op for all bodies;
    /// the export dispatcher must rebuild OFF from original/fresh source rather
    /// than submit a previously flight-enabled output as its new baseline.
    /// </summary>
    public NativeFreeFlightLevelComposition MergeLevelData(int levelId, ReadOnlySpan<byte> stagedData)
    {
        bool retail = _levels.ContainsKey(levelId);
        bool nativeCourse = levelId is 15 or 25 or 35 or 45 or 55;
        if (!retail && !nativeCourse && levelId != 65)
            throw new InvalidDataException("Flight composition requires a captured retail level or an issued fresh normal ID65 body.");
        if (!Enabled || nativeCourse)
            return new(levelId, stagedData.ToArray(), null, nativeCourse, retail, Hash(stagedData));
        if (stagedData.Length < 0x2800 || stagedData.Length > 0x800000 || stagedData.Length % 0x800 != 0)
            throw new InvalidDataException("Global flight requires a complete bounded sector-aligned native level body.");
        // Validate the actual edited schema and root0 stream. The executable
        // loader supplies missing33..35 from resident16, then parses these local
        // records unchanged. Native64's exact flight clips retain precedence.
        byte[] bytes = stagedData.ToArray();
        int model = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x10));
        int length = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x14));
        _ = new NativeLevelDataGrowthLayout(bytes.AsSpan(0, 0x800), bytes.Length,
            [new(0, 37, checked((long)model + length), 0, length, 0, [], 2, false)]);
        int start = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x50));
        int end = BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(0x54));
        if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(0x150)) != 0 ||
            start < model || end <= start || end > (long)model + length)
            throw new InvalidDataException("Global flight requires the actual bounded class0 player stream.");
        var stream = NativePlayerAnimationStreamCodec.Decode(bytes.AsSpan(start, end - start), _geometry);
        if (stream.Records.Any(r => _sharedIds.Contains(r.AnimationId)))
            throw new InvalidDataException("Global flight cannot replace an owned resident player animation.");
        return new(levelId, bytes, null, false, retail, Hash(stagedData));
    }

    public byte[] ApplyToExecutable(ReadOnlySpan<byte> stagedExecutable) =>
        ExecutablePatch.ApplyToExecutable(stagedExecutable);

    /// <summary>Validate the actual final resident package before publication.
    /// Pass PETE entry8 bytes at+0x800 for the declared player package length.
    /// Local shared-model aliases do not acquire permission to edit this owner.</summary>
    public void RequireResidentPlayerPackage(ReadOnlySpan<byte> package)
    {
        if (package.Length != NativePlayerAnimationSource.SharedPlayerPackageByteLength ||
            Hash(package) != "46f570bf0ad2d433f8b42874ae40a3e5d7155d7206dddd1cd08a2d4be16e0e76")
            throw new InvalidDataException("Global flight requires the unchanged source-owned resident player package.");
    }

    /// <summary>The complete resident WAD entry also owns the allocator's
    /// size/header and remaining resident models. Keep the fixed buffer-floor
    /// proof tied to the actual published owner, not just the player subset.</summary>
    public void RequireResidentEntry8(ReadOnlySpan<byte> entry)
    {
        if (entry.Length != 0x3A000 || Hash(entry) !=
            "b73da4852629d2035f9a1f6541f0ebda45ce61762f8b09ec62d8fe293f5e1f86")
            throw new InvalidDataException("Global flight requires the unchanged complete source-owned resident entry8.");
    }

    /// <summary>Final byte/normal-buffer gate, after all ordinary composers.
    /// Caller must invoke for every NormalLevelIds owner, plus65 when present.
    /// It cannot reuse a pre-import memory report or omit untouched levels from
    /// a global ON export. Known buffers and the existing safety margin remain
    /// unchanged. This is address accounting, not peak/runtime certification.</summary>
    public NativeFreeFlightMemoryEvidence RequireFinalLevelClosure(int levelId, ReadOnlySpan<byte> levelData,
        ReadOnlySpan<byte> executable)
    {
        if (!Enabled || !IsNormalLevel(levelId) && levelId != 65)
            throw new InvalidDataException("Final flight closure requires an enabled normal-player owner.");
        var closure = MergeLevelData(levelId, levelData);
        if (closure.ExactInsertedByteLength != 0 || !closure.CopyLevelData().AsSpan().SequenceEqual(levelData))
            throw new InvalidDataException($"Global flight changed staged level {levelId} during its final validation.");
        // Exact inverse validates every afterimage and the untouched complete
        // classifier, shared initializer, frame metadata and player consumers.
        _ = ExecutablePatch.RevertExecutable(executable);
        var bindings = UniversalExeBindings.Read(executable, levelId);
        if (!bindings.TryGetValue(UniversalExeBindings.CopyBufferGlobal, out var binding))
            throw new InvalidDataException("Final flight output has no actual native copy-buffer binding.");
        var body = new UniversalLevelData(levelData.ToArray());
        uint end = UniversalMobyComposer.RuntimeEnd(body, binding.Value);
        bool sourceBaselineException = levelId == 62 && end <= _originalLevel62ReadEnd;
        var evidence = new NativeFreeFlightMemoryEvidence(levelId, binding.Value, end,
            UniversalMobyComposer.NormalBufferFloor, UniversalMobyComposer.RamSafetyMargin,
            (long)UniversalMobyComposer.NormalBufferFloor - end, sourceBaselineException,
            levelId == 62 ? _originalLevel62ReadEnd : null);
        // Source retail62 already has only700bytes before the same reserved
        // floor. Global flight adds no bytes. Preserve that authenticated end
        // only; any later owner growth must meet the full1024margin. No buffer
        // address, reservation, or general admission threshold is relaxed.
        if (evidence.RemainingBytes < evidence.SafetyMarginBytes && !sourceBaselineException)
            throw new InvalidDataException($"Fly Anywhere level {levelId} reaches 0x{end:X8}; " +
                $"normal reserved buffers start at 0x{evidence.ReservedBufferStart:X8}. " +
                $"Remaining {evidence.RemainingBytes} bytes is below the unchanged {evidence.SafetyMarginBytes}-byte margin.");
        return evidence;
    }

    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void RequireResidentAirborne(ReadOnlySpan<byte> package, ReadOnlySpan<byte> executable)
    {
        int root = BinaryPrimitives.ReadInt32LittleEndian(package[(0x38 + 16 * 4)..]);
        ReadOnlySpan<byte> blob = package[root..];
        if (root != 0x72C || BinaryPrimitives.ReadInt16LittleEndian(blob) != 11 || blob[4] != 1)
            throw new InvalidDataException("Resident airborne animation16 changed from the source-owned11-frame type1 clip.");
        int geometry = BinaryPrimitives.ReadInt32LittleEndian(package[0x34..]);
        foreach (int field in new[] { 0x10, 0x14, 0x18 })
        {
            long target = geometry + (long)BinaryPrimitives.ReadUInt32LittleEndian(blob[field..]);
            if (target % 4 != 0 || target < geometry || target + 4 > package.Length)
                throw new InvalidDataException("Resident airborne geometry reference leaves its source package.");
        }
        for (int i = 0; i < 11; i++)
        {
            uint frame = BinaryPrimitives.ReadUInt32LittleEndian(blob[(0x24 + 4 * i)..]);
            long target = geometry + (long)(frame & 0x1FFFFF);
            if (target % 2 != 0 || target < geometry || target + 2 > package.Length)
                throw new InvalidDataException("Resident airborne packed frame leaves its source-owned geometry.");
        }
        // The native controller's three flight metadata rows bound frame0..10
        // at four ticks/frame, the same bounds as the reused airborne clip.
        foreach (int id in new[] { 16, 33, 34, 35 })
        {
            ReadOnlySpan<byte> metadata = executable.Slice(0x5CCA2 + 4 * id, 4);
            if (metadata[0] != 10 || metadata[1] != 4)
                throw new InvalidDataException("Native free-flight frame range/timing no longer matches resident airborne16.");
        }
    }
}

public sealed record NativeFreeFlightMemoryEvidence(int LevelId, uint CopyBufferAddress, uint LevelReadEnd,
    uint ReservedBufferStart, int SafetyMarginBytes, long RemainingBytes,
    bool UnchangedSourceBaselineException = false, uint? OriginalSourceReadEnd = null)
{
    public bool PeakTransientFitProven => false;
    public bool RuntimeVerified => false;
}

public sealed class NativeFreeFlightLevelComposition
{
    private readonly byte[] _data;
    internal NativeFreeFlightLevelComposition(int levelId, byte[] data, NativePlayerAnimationMergeLayout? layout,
        bool nativeCourse, bool capturedRetail, string beforeHash)
    {
        _data = data.ToArray(); LevelId = levelId; NativeCourseUnchanged = nativeCourse;
        CapturedRetailIdentity = capturedRetail; StagedBeforeSha256 = beforeHash;
        FinalSha256 = NativePlayerAnimationStreamCodec.Hash(_data);
        ExactInsertedByteLength = layout?.ExactGrowth ?? 0;
        AddedAnimationIds = Array.AsReadOnly(layout?.AddedIds.ToArray() ?? []);
        Preservations = Array.AsReadOnly(layout?.Preservations.ToArray() ?? []);
        HeaderChanges = Array.AsReadOnly(layout?.HeaderChanges.ToArray() ?? []);
    }
    public int LevelId { get; }
    public bool NativeCourseUnchanged { get; }
    public bool CapturedRetailIdentity { get; }
    public string StagedBeforeSha256 { get; }
    public string FinalSha256 { get; }
    public int ExactInsertedByteLength { get; }
    public int FinalDataByteLength => _data.Length;
    public IReadOnlyList<int> AddedAnimationIds { get; }
    public IReadOnlyList<NativePlayerAnimationPreservation> Preservations { get; }
    public IReadOnlyList<NativePlayerAnimationHeaderChange> HeaderChanges { get; }
    public bool RuntimeFitProven => false;
    public byte[] CopyLevelData() => _data.ToArray();
}
