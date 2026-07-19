using System.Buffers.Binary;

namespace Spyro.Editor.SurfaceLayoutSmoke;

internal sealed record SyntheticSurfaceLevelDataFixture(
    byte[] Bytes,
    long LevelDataWadOffset,
    int SpecialSurfaceStart,
    int CollisionStart,
    int CollisionEnd,
    int UsedLevelEnd,
    int TreeStart,
    int BlocksStart,
    int TrianglesStart,
    int OcclusionStart,
    int FlagsStart,
    int TriangleCount,
    int FlagCount)
{
    private const int PrefixComponentCount = 3;
    private const int EmptyComponentBytes = 4;

    public static SyntheticSurfaceLevelDataFixture Build(
        IReadOnlyList<byte[]> descriptors,
        int flagCount = 4,
        int zeroTailBytes = 64)
    {
        ArgumentNullException.ThrowIfNull(descriptors);
        if (descriptors.Count > 63)
            throw new ArgumentOutOfRangeException(nameof(descriptors));
        if (descriptors.Any(record => record.Length < 4 || (record.Length & 3) != 0))
            throw new ArgumentException("Synthetic special-surface records must be nonempty and 4-byte aligned.", nameof(descriptors));
        if (flagCount is < 1 or > 8)
            throw new ArgumentOutOfRangeException(nameof(flagCount));
        if (zeroTailBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(zeroTailBytes));

        const long levelDataWadOffset = 0x100000;
        const int triangleCount = 8;
        int specialStart = PrefixComponentCount * EmptyComponentBytes;
        byte[] special = BuildSpecialSurfaceComponent(descriptors);
        int collisionStart = specialStart + special.Length;
        const int collisionBodyRelative = 4;
        const int treeRelative = collisionBodyRelative + 0x1C;
        const int blocksRelative = treeRelative + 4;
        const int trianglesRelative = blocksRelative + (triangleCount * 2);
        const int occlusionRelative = trianglesRelative + (triangleCount * 12);
        const int flagsRelative = occlusionRelative + triangleCount;
        int flagStorageBytes = Align4(flagCount);
        int collisionLength = flagsRelative + flagStorageBytes;
        int collisionEnd = collisionStart + collisionLength;
        const int suffixBytes = 32;
        int usedLevelEnd = collisionEnd + suffixBytes;
        byte[] bytes = new byte[usedLevelEnd + zeroTailBytes];

        for (int offset = 0; offset < specialStart; offset += EmptyComponentBytes)
            WriteInt32(bytes, offset, EmptyComponentBytes);
        special.CopyTo(bytes, specialStart);

        int body = collisionStart + collisionBodyRelative;
        int treeStart = collisionStart + treeRelative;
        int blocksStart = collisionStart + blocksRelative;
        int trianglesStart = collisionStart + trianglesRelative;
        int occlusionStart = collisionStart + occlusionRelative;
        int flagsStart = collisionStart + flagsRelative;
        WriteInt32(bytes, collisionStart, collisionLength);
        WriteInt32(bytes, body, triangleCount);
        WriteInt32(bytes, body + 4, flagCount);
        WriteInt32(bytes, body + 0x08, treeStart - body);
        WriteInt32(bytes, body + 0x0C, blocksStart - body);
        WriteInt32(bytes, body + 0x10, trianglesStart - body);
        WriteInt32(bytes, body + 0x14, occlusionStart - body);
        WriteInt32(bytes, body + 0x18, flagsStart - body);

        for (int index = 0; index < triangleCount; index++)
        {
            WriteUInt16(bytes, blocksStart + (index * 2), (ushort)(index == 0 ? 0x8000 : index));
            for (int part = 0; part < 12; part++)
                bytes[trianglesStart + (index * 12) + part] = (byte)(0x10 + index + part);
            bytes[occlusionStart + index] = (byte)(0x80 + index);
        }

        byte[] flags = [0x3F, 0xFF, 0x3F, 0xC0, 0x3F, 0x3F, 0x3F, 0x3F];
        flags.AsSpan(0, flagCount).CopyTo(bytes.AsSpan(flagsStart, flagCount));

        int suffix = collisionEnd;
        WriteInt32(bytes, suffix, 12); // cyclorama component
        bytes[suffix + 4] = 0x11;
        bytes[suffix + 5] = 0x22;
        bytes[suffix + 6] = 0x33;
        WriteInt32(bytes, suffix + 8, 0);
        WriteInt32(bytes, suffix + 12, 0); // portal count
        WriteInt32(bytes, suffix + 16, 8); // particle component
        WriteInt32(bytes, suffix + 20, 0);
        WriteInt32(bytes, suffix + 24, 8); // sound component
        WriteInt32(bytes, suffix + 28, 0);

        return new SyntheticSurfaceLevelDataFixture(
            bytes,
            levelDataWadOffset,
            specialStart,
            collisionStart,
            collisionEnd,
            usedLevelEnd,
            treeStart,
            blocksStart,
            trianglesStart,
            occlusionStart,
            flagsStart,
            triangleCount,
            flagCount);
    }

    public byte[] TriangleBytes(byte[] bytes, int index) =>
        bytes.AsSpan(TrianglesStart + (index * 12), 12).ToArray();

    public int ReadBlockTriangleIndex(byte[] bytes, int index) =>
        BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(BlocksStart + (index * 2), 2)) & 0x7FFF;

    private static byte[] BuildSpecialSurfaceComponent(IReadOnlyList<byte[]> descriptors)
    {
        int pointerBytes = descriptors.Count * 4;
        int recordBytes = descriptors.Sum(record => record.Length);
        int componentLength = 8 + pointerBytes + recordBytes;
        byte[] bytes = new byte[componentLength];
        WriteInt32(bytes, 0, componentLength);
        WriteInt32(bytes, 4, descriptors.Count);
        int relativeRecordOffset = 4 + pointerBytes;
        int recordOffset = 8 + pointerBytes;
        for (int index = 0; index < descriptors.Count; index++)
        {
            WriteInt32(bytes, 8 + (index * 4), relativeRecordOffset);
            descriptors[index].CopyTo(bytes, recordOffset);
            relativeRecordOffset += descriptors[index].Length;
            recordOffset += descriptors[index].Length;
        }
        return bytes;
    }

    private static int Align4(int value) => (value + 3) & ~3;

    private static void WriteInt32(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset, 4), value);

    private static void WriteUInt16(byte[] bytes, int offset, ushort value) =>
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset, 2), value);
}
