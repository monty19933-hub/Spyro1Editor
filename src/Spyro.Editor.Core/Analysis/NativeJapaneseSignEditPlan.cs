using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public sealed record NativeJapaneseSignEditRequest(
    NativeJapaneseSignSource Target, NativeJapaneseSignMessage Replacement);

/// <summary>An immutable exact-size private-block patch, not permission to write a disc.</summary>
public sealed class NativeJapaneseSignEditPatch
{
    private readonly byte[] _before, _after;
    internal NativeJapaneseSignEditPatch(NativeJapaneseSignSource target, byte[] before, byte[] after,
        int fixupOffset, ReadOnlySpan<byte> fixups)
    {
        DataWadEntry = target.DataWadEntry; TrueIndex = target.TrueIndex;
        SceneOffset = target.PropertiesSceneOffset;
        DataOffset = checked(target.SceneDataOffset + SceneOffset);
        WadOffset = target.RecordWadOffset - target.RowSceneOffset + SceneOffset;
        RowSceneOffset = target.RowSceneOffset; TargetRecordSha256 = target.RecordSha256;
        SourceDataSha256 = target.SourceDataSha256;
        _before = before.ToArray(); _after = after.ToArray();
        BeforeSha256 = NativeJapaneseSignEditPlan.Hash(_before);
        AfterSha256 = NativeJapaneseSignEditPlan.Hash(_after);
        FixupSceneOffset = fixupOffset; FixupByteLength = fixups.Length;
        FixupSha256 = NativeJapaneseSignEditPlan.Hash(fixups);
        RegisteredFieldSceneOffsets = Array.AsReadOnly(new[] { RowSceneOffset, SceneOffset + 12, SceneOffset + 16 });
    }
    public int DataWadEntry { get; }
    public int TrueIndex { get; }
    public int SceneOffset { get; }
    public int DataOffset { get; }
    public long WadOffset { get; }
    public int RowSceneOffset { get; }
    public int ByteLength => _before.Length;
    // Includes only padding inside the original NUL-derived aligned block.
    public int MaximumMessageBytes => ByteLength - NativeJapaneseSignMessage.PrivateHeaderBytes - 1;
    public string TargetRecordSha256 { get; }
    public string SourceDataSha256 { get; }
    public string BeforeSha256 { get; }
    public string AfterSha256 { get; }
    public int FixupSceneOffset { get; }
    public int FixupByteLength { get; }
    public string FixupSha256 { get; }
    public IReadOnlyList<int> RegisteredFieldSceneOffsets { get; }
    public byte[] CopyBeforeBytes() => _before.ToArray();
    public byte[] CopyAfterBytes() => _after.ToArray();
}

/// <summary>
/// Same-revision edits of existing authenticated Japanese signs only. Capacity
/// is the original message's NUL-terminated aligned block, never the distance
/// to the next properties pointer. Rows, pointers, fixups and all other bytes
/// remain unchanged. This class has no disc writer or export approval.
/// </summary>
public sealed class NativeJapaneseSignEditPlan
{
    private const string JapaneseImageSha256 = "1bcf4353d6a4d4d0db152d2ebd60879ef85516feaf170f364ff419c54f599ea7";
    private const long JapaneseImageByteLength = 727179600;
    private sealed record LevelPreimage(int ByteLength, string Sha256);
    private readonly IReadOnlyDictionary<int, LevelPreimage> _levels;

    private NativeJapaneseSignEditPlan(string imageHash, long imageLength,
        IEnumerable<NativeJapaneseSignEditPatch> writes, Dictionary<int, LevelPreimage> levels)
    {
        SourceImageSha256 = imageHash; SourceImageByteLength = imageLength;
        Writes = Array.AsReadOnly(writes.OrderBy(w => w.WadOffset).ToArray());
        _levels = new System.Collections.ObjectModel.ReadOnlyDictionary<int, LevelPreimage>(levels);
        for (int i = 1; i < Writes.Count; i++)
            if (Writes[i - 1].WadOffset + Writes[i - 1].ByteLength > Writes[i].WadOffset)
                throw new InvalidDataException("Native sign edit targets overlap.");
    }
    public string SourceImageSha256 { get; }
    public long SourceImageByteLength { get; }
    public IReadOnlyList<NativeJapaneseSignEditPatch> Writes { get; }
    public bool NativeExportApproved => false;
    public bool PublicationApproved => false;
    public bool RuntimeVerified => false;

    public static NativeJapaneseSignEditPlan Compile(NativeRegionalSourceSession source,
        IEnumerable<NativeJapaneseSignEditRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(source);
        RequireSourceIdentity(source.Revision, source.SourceImageSha256, source.SourceImageByteLength);
        source.VerifyUnchanged();
        var batch = SnapshotRequests(requests);
        var result = CompileCore(source.SourceImageSha256, source.SourceImageByteLength, batch,
            entry => (source.ReadWadEntry(entry), source.Entries[entry].Offset));
        source.VerifyUnchanged();
        return result;
    }

    /// <summary>Return an exact-source detached level copy. No source or disc is modified.</summary>
    public byte[] ApplyToCapturedLevel(int dataWadEntry, ReadOnlySpan<byte> originalLevel)
    {
        if (!_levels.TryGetValue(dataWadEntry, out LevelPreimage? level) ||
            originalLevel.Length != level.ByteLength || Hash(originalLevel) != level.Sha256)
            throw new InvalidDataException("Sign edit level does not match its complete captured source preimage.");
        byte[] result = originalLevel.ToArray();
        foreach (NativeJapaneseSignEditPatch write in Writes.Where(w => w.DataWadEntry == dataWadEntry))
        {
            if (!originalLevel.Slice(write.DataOffset, write.ByteLength).SequenceEqual(write.CopyBeforeBytes()))
                throw new InvalidDataException("Sign private-block preimage changed.");
            write.CopyAfterBytes().CopyTo(result, write.DataOffset);
        }
        return result;
    }

    /// <summary>
    /// Compare the entire level, not just message bytes. This also proves the
    /// original rows, pointer words, relocation list and unrelated data stayed exact.
    /// </summary>
    public void VerifyLevelReadback(int dataWadEntry, ReadOnlySpan<byte> originalLevel, ReadOnlySpan<byte> editedLevel)
    {
        byte[] expected = ApplyToCapturedLevel(dataWadEntry, originalLevel);
        if (!editedLevel.SequenceEqual(expected))
            throw new InvalidDataException("Sign edit readback differs from the exact planned level, including unchanged dependencies.");
    }

    internal static void RequireSourceIdentity(NativeDiscRevision revision, string hash, long length)
    {
        if (revision != NativeDiscRevision.JapanScps10083 || hash != JapaneseImageSha256 || length != JapaneseImageByteLength)
            throw new InvalidDataException("Native sign edits require the exact authenticated Japanese source; cross-region or modified sources are refused.");
    }

    internal static NativeJapaneseSignEditPlan CompileFixture(byte[] data, int entry, int wadOffset,
        IEnumerable<NativeJapaneseSignEditRequest> requests) =>
        CompileCore("fixture-no-source-certificate", 0, SnapshotRequests(requests),
            requestedEntry => requestedEntry == entry ? (data.ToArray(), wadOffset) :
                throw new InvalidDataException("Fixture has no requested source level."));

    private static IReadOnlyList<NativeJapaneseSignEditRequest> SnapshotRequests(IEnumerable<NativeJapaneseSignEditRequest> requests)
    {
        ArgumentNullException.ThrowIfNull(requests);
        List<NativeJapaneseSignEditRequest> batch = [];
        HashSet<(int Entry, int Index)> targets = [];
        foreach (var request in requests)
        {
            if (batch.Count >= 330 || request?.Target == null || request.Replacement == null)
                throw new InvalidDataException("Native sign edits require at most 330 complete existing-sign requests.");
            if (!targets.Add((request.Target.DataWadEntry, request.Target.TrueIndex)))
                throw new InvalidDataException("Duplicate native sign edit target; combine the requested text before planning.");
            request.Replacement.RequireCompleteDialog();
            batch.Add(request);
        }
        if (batch.Count == 0) throw new InvalidDataException("Native sign edit plan requires at least one existing-sign request.");
        return batch.AsReadOnly();
    }

    private static NativeJapaneseSignEditPlan CompileCore(string imageHash, long imageLength,
        IReadOnlyList<NativeJapaneseSignEditRequest> requests, Func<int, (byte[] Data, int WadOffset)> readLevel)
    {
        List<NativeJapaneseSignEditPatch> writes = [];
        Dictionary<int, LevelPreimage> levels = [];
        foreach (var group in requests.GroupBy(r => r.Target.DataWadEntry))
        {
            var (data, wadOffset) = readLevel(group.Key);
            var captured = NativeJapaneseSignSource.DecodeLevel(data, group.Key, wadOffset).ToDictionary(s => s.TrueIndex);
            int sceneOffset = Word(data, 0x18), sceneLength = Word(data, 0x1C);
            ReadOnlySpan<byte> scene = Slice(data, sceneOffset, sceneLength);
            int cursor = 0x88;
            for (int i = 0; i < 7; i++) cursor = ComponentEnd(scene, cursor, 8);
            int properties = ComponentEnd(scene, cursor, 8);
            int propertiesEnd = ComponentEnd(scene, properties, 4);
            int fixupOffset = ComponentEnd(scene, ComponentEnd(scene, propertiesEnd, 4), 4);
            int fixupCount = Word(scene, fixupOffset);
            if (fixupCount > (scene.Length - fixupOffset - 4) / 4)
                throw new InvalidDataException("Native sign scene relocation count exceeds its owner.");
            ReadOnlySpan<byte> fixups = Slice(scene, fixupOffset, 4 + fixupCount * 4);
            Dictionary<int, int> targets = [];
            for (int i = 0; i < fixupCount; i++)
            {
                int field = Word(fixups, 4 + i * 4);
                if (field % 4 != 0 || field > fixupOffset - 4 || !targets.TryAdd(field, Word(scene, field)))
                    throw new InvalidDataException("Native sign scene relocation field is invalid, overlapping or duplicated.");
                if (targets[field] >= scene.Length)
                    throw new InvalidDataException("Native sign scene relocation target exceeds its owner.");
            }
            foreach (var request in group)
            {
                if (!captured.TryGetValue(request.Target.TrueIndex, out var target) || !SameTarget(request.Target, target))
                    throw new InvalidDataException("Sign edit target no longer matches its captured row, message, offsets and complete level hash.");
                byte[] expectedOriginal = target.Message.BuildPrivateData(target.PropertiesSceneOffset);
                byte[] before = Slice(scene, target.PropertiesSceneOffset, expectedOriginal.Length).ToArray();
                if (!before.AsSpan().SequenceEqual(expectedOriginal))
                    throw new InvalidDataException("Original sign's exact terminated/aligned block or zero padding changed.");
                int start = target.PropertiesSceneOffset, end = checked(start + before.Length);
                if (end > target.ObservedNextPropertiesBoundarySceneOffset)
                    throw new InvalidDataException("Original sign block overlaps another observed properties consumer.");
                var required = new Dictionary<int, int> { [target.RowSceneOffset] = start, [start + 12] = start + 24, [start + 16] = start + 20 };
                foreach (var pair in required)
                    if (!targets.TryGetValue(pair.Key, out int actual) || actual != pair.Value)
                        throw new InvalidDataException("Sign edit requires each of its three exact native relocation registrations.");
                foreach (var pair in targets)
                {
                    bool fieldOverlaps = pair.Key < end && pair.Key + 4 > start;
                    bool targetInside = pair.Value >= start && pair.Value < end;
                    if ((fieldOverlaps || targetInside) && !required.ContainsKey(pair.Key))
                        throw new InvalidDataException("Another native relocation aliases the exact sign block; isolated editing is refused.");
                }
                byte[] replacement = request.Replacement.BuildPrivateData(start);
                if (replacement.Length > before.Length)
                    throw new InvalidDataException($"Replacement sign needs {replacement.Length} bytes but its exact original block has only {before.Length}; native growth is not supported.");
                byte[] after = new byte[before.Length]; replacement.CopyTo(after, 0);
                if (!before.AsSpan(0, 24).SequenceEqual(after.AsSpan(0, 24)))
                    throw new InvalidDataException("A same-region text edit cannot change native pointers or counters.");
                writes.Add(new(target, before, after, fixupOffset, fixups));
            }
            levels.Add(group.Key, new(data.Length, Hash(data)));
        }
        return new(imageHash, imageLength, writes, levels);
    }

    private static bool SameTarget(NativeJapaneseSignSource expected, NativeJapaneseSignSource actual) =>
        expected.DataWadEntry == actual.DataWadEntry && expected.TrueIndex == actual.TrueIndex &&
        expected.SceneDataOffset == actual.SceneDataOffset && expected.RowSceneOffset == actual.RowSceneOffset &&
        expected.PropertiesSceneOffset == actual.PropertiesSceneOffset &&
        expected.ObservedNextPropertiesBoundarySceneOffset == actual.ObservedNextPropertiesBoundarySceneOffset &&
        expected.RecordWadOffset == actual.RecordWadOffset && expected.SourceDataSha256 == actual.SourceDataSha256 &&
        expected.RecordSha256 == actual.RecordSha256 && expected.Message.NativeBytesHex == actual.Message.NativeBytesHex &&
        expected.CopyRecord().AsSpan().SequenceEqual(actual.CopyRecord());

    private static int ComponentEnd(ReadOnlySpan<byte> bytes, int offset, int minimum)
    {
        int length = Word(bytes, offset);
        if (offset % 4 != 0 || length < minimum || length % 4 != 0)
            throw new InvalidDataException("Native sign scene component has invalid alignment or length.");
        _ = Slice(bytes, offset, length);
        return checked(offset + length);
    }
    private static int Word(ReadOnlySpan<byte> bytes, int offset)
    {
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(Slice(bytes, offset, 4));
        return value <= int.MaxValue ? (int)value : throw new InvalidDataException("Native sign scene address/count exceeds its owner.");
    }
    private static ReadOnlySpan<byte> Slice(ReadOnlySpan<byte> bytes, int offset, int length)
    {
        if (offset < 0 || length < 0 || offset > bytes.Length || length > bytes.Length - offset)
            throw new InvalidDataException("Native sign edit range exceeds its captured owner.");
        return bytes.Slice(offset, length);
    }
    internal static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
