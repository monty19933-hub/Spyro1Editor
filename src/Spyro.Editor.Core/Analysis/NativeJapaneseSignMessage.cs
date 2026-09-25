using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Analysis;

public enum NativeJapaneseSignTokenKind { Glyph, Space, LineBreak, TextColor }
public sealed record NativeJapaneseSignToken(NativeJapaneseSignTokenKind Kind, byte NativeValue);

/// <summary>
/// Lossless SCPS-10083 sign byte stream, independent of editor object labels.
/// Glyph values are native atlas indices, not ASCII, Shift-JIS or Unicode.
/// This class constructs private-data bytes only; it does not enable USA imports.
/// </summary>
public sealed class NativeJapaneseSignMessage
{
    // The display routine copies each line into SP+24..71 then adds NUL;
    // saved S0 begins at SP+72 (8001AEEC and 8001B268..B304).
    public const int MaximumNativeLineBytes = 47;
    // Deliberate authoring policy: the largest observed original message and
    // line count among all 330 signs. These are not claimed engine capacities.
    public const int MaximumAuthoredMessageBytes = 153;
    public const int MaximumAuthoredLineSegments = 9;
    public const int PrivateHeaderBytes = 0x18;
    private readonly byte[] _bytes;

    private NativeJapaneseSignMessage(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is < 1 or > MaximumAuthoredMessageBytes)
            throw new InvalidDataException("Sign message exceeds the original-message authoring policy.");
        List<NativeJapaneseSignToken> tokens = [];
        int lineBytes = 0, lines = 1;
        foreach (byte value in bytes)
        {
            NativeJapaneseSignTokenKind kind;
            if (value == 0xFF) { kind = NativeJapaneseSignTokenKind.LineBreak; lines++; lineBytes = 0; }
            else
            {
                if (++lineBytes > MaximumNativeLineBytes)
                    throw new InvalidDataException("Sign line would overrun the native 48-byte NUL-terminated line buffer.");
                if (value == 0x20) kind = NativeJapaneseSignTokenKind.Space;
                else if (value is >= 0x40 and <= 0x46) kind = NativeJapaneseSignTokenKind.TextColor;
                else if (IsDefinedGlyph(value)) kind = NativeJapaneseSignTokenKind.Glyph;
                else throw new InvalidDataException("Sign byte has no established native glyph or control meaning.");
            }
            tokens.Add(new(kind, value));
        }
        if (lines > MaximumAuthoredLineSegments)
            throw new InvalidDataException("Sign message exceeds the original-message line-count authoring policy.");
        _bytes = bytes.ToArray();
        Tokens = Array.AsReadOnly(tokens.ToArray());
        LineSegments = lines;
    }

    public int ByteLength => _bytes.Length;
    public int LineSegments { get; }
    public IReadOnlyList<NativeJapaneseSignToken> Tokens { get; }
    public string NativeBytesHex => Convert.ToHexStringLower(_bytes);
    public bool HasFinalLineTerminator => _bytes[^1] == 0xFF;
    public bool NativeImportApproved => false;
    // A raw lossless stream can represent an unfinished authoring fragment.
    // It is not necessarily a complete modal dialog suitable for serialization.
    public static NativeJapaneseSignMessage FromNativeBytes(ReadOnlySpan<byte> bytes) => new(bytes);
    public byte[] CopyNativeBytes() => _bytes.ToArray();

    /// <summary>
    /// Complete the final native display line without changing a raw codec's
    /// round-trip identity. Native modal PC8001B2B0 flushes at FF; its NUL exit
    /// at8001B334 does not flush. A final FF is therefore required even when
    /// the human-authored text has no visible trailing blank line.
    /// </summary>
    public NativeJapaneseSignMessage AsCompleteDialog() => HasFinalLineTerminator
        ? this : new(_bytes.Concat(new byte[] { 0xFF }).ToArray());

    internal void RequireCompleteDialog()
    {
        if (!HasFinalLineTerminator)
            throw new InvalidDataException("Native sign dialogs require a final FF line terminator before NUL; call AsCompleteDialog before planning or serialization.");
    }

    // Derived from the 256 halfword atlas table at 800795B8. 0x80 is the
    // first hiragana glyph and has descriptor zero, unlike undefined ASCII.
    private static bool IsDefinedGlyph(byte value) => value is >= 1 and <= 0x18 or
        >= 0x21 and <= 0x29 or >= 0x2B and <= 0x39 or 0x3D or 0x3F or >= 0x80 and <= 0xFC;

    /// <summary>
    /// Create a fresh per-instance native private block at a declared scene
    /// offset. Both relative pointers are rebuilt; runtime counters start zero.
    /// The caller still owns scene allocation, row relocation and dependency closure.
    /// </summary>
    public byte[] BuildPrivateData(int destinationSceneOffset)
    {
        RequireCompleteDialog();
        int length = checked((PrivateHeaderBytes + _bytes.Length + 1 + 3) & ~3);
        if (destinationSceneOffset < 0 || destinationSceneOffset % 4 != 0 ||
            destinationSceneOffset > int.MaxValue - length)
            throw new InvalidDataException("Sign private-data destination has no aligned bounded scene offset.");
        byte[] block = new byte[length];
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(0x0C), destinationSceneOffset + 0x18);
        BinaryPrimitives.WriteInt32LittleEndian(block.AsSpan(0x10), destinationSceneOffset + 0x14);
        _bytes.CopyTo(block, PrivateHeaderBytes);
        return block;
    }
}

/// <summary>One authenticated placed Japanese sign and its bounded text observation.</summary>
public sealed class NativeJapaneseSignSource
{
    private readonly byte[] _row;
    private NativeJapaneseSignSource(int entry, int index, int sceneDataOffset, int rowSceneOffset,
        int propertiesOffset, int propertiesEnd, int wadOffset, string dataHash, byte[] row,
        NativeJapaneseSignMessage message)
    {
        DataWadEntry = entry; TrueIndex = index; SceneDataOffset = sceneDataOffset;
        RowSceneOffset = rowSceneOffset; PropertiesSceneOffset = propertiesOffset;
        ObservedNextPropertiesBoundarySceneOffset = propertiesEnd;
        RecordWadOffset = wadOffset + (long)sceneDataOffset + rowSceneOffset;
        SourceDataSha256 = dataHash; RecordSha256 = Hash(row); _row = row;
        Message = message;
    }
    public const ushort ActorId = 0x47;
    public int DataWadEntry { get; }
    public int TrueIndex { get; }
    public int SceneDataOffset { get; }
    public int RowSceneOffset { get; }
    public int PropertiesSceneOffset { get; }
    // Next greater observed row-property pointer is a conservative parsing
    // boundary, not proof of complete allocation ownership or all native aliases.
    public int ObservedNextPropertiesBoundarySceneOffset { get; }
    public long RecordWadOffset { get; }
    public string SourceDataSha256 { get; }
    public string RecordSha256 { get; }
    public byte AnimationVariant => _row[0x3C];
    public NativeJapaneseSignMessage Message { get; }
    public byte[] CopyRecord() => _row.ToArray();
    public bool NativeImportApproved => false;

    public static IReadOnlyList<NativeJapaneseSignSource> CaptureAll(NativeRegionalSourceSession source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Revision != NativeDiscRevision.JapanScps10083)
            throw new InvalidDataException("Japanese signs require their independently authenticated regional source.");
        List<NativeJapaneseSignSource> result = [];
        for (int entry = 23; entry <= 91; entry += 2)
        {
            byte[] data = source.ReadWadEntry(entry);
            result.AddRange(DecodeLevel(data, entry, source.Entries[entry].Offset));
        }
        source.VerifyUnchanged();
        return Array.AsReadOnly(result.ToArray());
    }

    internal static IReadOnlyList<NativeJapaneseSignSource> DecodeLevel(ReadOnlySpan<byte> data, int entry, int wadOffset)
    {
        if (entry is < 23 or > 91 || entry % 2 == 0 || wadOffset < 0)
            throw new InvalidDataException("Unidentified Japanese level-data owner.");
        int sceneOffset = Int(data, 0x18), sceneLength = Int(data, 0x1C);
        Range(sceneOffset, sceneLength, data.Length, 0x90);
        ReadOnlySpan<byte> scene = data.Slice(sceneOffset, sceneLength);
        int cursor = 0x88;
        for (int i = 0; i < 7; i++) cursor = checked(cursor + Component(scene, cursor, 8));
        int componentLength = Component(scene, cursor, 8), count = Int(scene, cursor + 4);
        if (count > 4096 || 8L + count * 0x58L > componentLength)
            throw new InvalidDataException("Japanese Moby rows escape their scene component.");
        int props = checked(cursor + componentLength), propsSize = Component(scene, props, 4);
        int propsStart = props + 4, propsEnd = props + propsSize;
        List<(int Index, int RowOffset, int Property, byte[] Bytes)> allRows = [];
        for (int index = 0; index < count; index++)
        {
            int rowOffset = cursor + 8 + index * 0x58;
            byte[] row = scene.Slice(rowOffset, 0x58).ToArray();
            uint property = BinaryPrimitives.ReadUInt32LittleEndian(row);
            allRows.Add((index, rowOffset, property <= int.MaxValue ? (int)property : -1, row));
        }
        string dataHash = Hash(data);
        List<NativeJapaneseSignSource> signs = [];
        foreach (var row in allRows)
        {
            if (BinaryPrimitives.ReadUInt16LittleEndian(row.Bytes.AsSpan(0x36)) != ActorId) continue;
            if (row.Property < propsStart || row.Property >= propsEnd || row.Property % 4 != 0 ||
                allRows.Count(other => other.Property == row.Property) != 1)
                throw new InvalidDataException("Sign private-data observation is absent, row-aliased or outside its component.");
            int privateEnd = allRows.Where(other => other.Property > row.Property && other.Property <= propsEnd)
                .Select(other => other.Property).DefaultIfEmpty(propsEnd).Min();
            Range(row.Property, NativeJapaneseSignMessage.PrivateHeaderBytes, privateEnd, 0x18);
            ReadOnlySpan<byte> header = scene.Slice(row.Property, 0x18);
            if (Int(header, 0x0C) != row.Property + 0x18 || Int(header, 0x10) != row.Property + 0x14 ||
                Int(header, 0) != 0 || Int(header, 4) != 0 || Int(header, 8) != 0 || Int(header, 0x14) != 0)
                throw new InvalidDataException("Japanese sign pointer/counter header differs from its native owner.");
            if (row.Bytes[0x3C] > 2 || row.Bytes[0x3C] != row.Bytes[0x3D])
                throw new InvalidDataException("Japanese sign animation variant is not one of the three native pairs.");
            int textStart = row.Property + 0x18;
            ReadOnlySpan<byte> textOwner = scene.Slice(textStart, privateEnd - textStart);
            int nul = textOwner.IndexOf((byte)0);
            if (nul < 0) throw new InvalidDataException("Japanese sign lacks a terminator before the next observed property pointer.");
            var message = NativeJapaneseSignMessage.FromNativeBytes(textOwner[..nul]);
            message.RequireCompleteDialog();
            signs.Add(new(entry, row.Index, sceneOffset, row.RowOffset, row.Property, privateEnd,
                wadOffset, dataHash, row.Bytes, message));
        }
        return Array.AsReadOnly(signs.ToArray());
    }

    private static int Component(ReadOnlySpan<byte> data, int offset, int minimum)
    { int n = Int(data, offset); Range(offset, n, data.Length, minimum); return n; }
    private static int Int(ReadOnlySpan<byte> bytes, int offset)
    {
        Range(offset, 4, bytes.Length, 4);
        uint value = BinaryPrimitives.ReadUInt32LittleEndian(bytes[offset..]);
        if (value > int.MaxValue) throw new InvalidDataException("Japanese source pointer/count exceeds its bounded owner.");
        return (int)value;
    }
    private static void Range(int offset, int length, int owner, int minimum)
    {
        if (offset < 0 || offset % 4 != 0 || length < minimum || length % 4 != 0 || offset + (long)length > owner)
            throw new InvalidDataException("Japanese source range exceeds its aligned owner.");
    }
    private static string Hash(ReadOnlySpan<byte> data) => Convert.ToHexStringLower(SHA256.HashData(data));
}
