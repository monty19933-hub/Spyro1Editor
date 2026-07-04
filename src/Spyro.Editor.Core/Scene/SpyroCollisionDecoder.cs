namespace Spyro.Editor.Core.Scene;

public static class SpyroCollisionDecoder
{
    public static SpyroCollisionTable Decode(byte[] ram)
    {
        SpyroSceneCollisionPointers pointers = FindSceneAndCollisionPointers(ram)
            ?? throw new InvalidDataException("Could not find the Spyro 1 scene/collision pointer table in RAM.");

        int headerOffset = pointers.CollisionOffset;
        int triangleCount = ReadInt32(ram, headerOffset);
        int blockTreeOffset = PsxPointerToOffset(ReadUInt32(ram, headerOffset + 8));
        int blocksOffset = PsxPointerToOffset(ReadUInt32(ram, headerOffset + 12));
        int triangleOffset = PsxPointerToOffset(ReadUInt32(ram, headerOffset + 16));
        if (triangleCount <= 0 || triangleCount > 50000)
            throw new InvalidDataException("Collision table triangle count is outside the expected range.");
        if (triangleOffset < 0 || triangleOffset + (triangleCount * 12L) > ram.Length)
            throw new InvalidDataException("Collision table triangle pointer is outside the RAM dump.");

        List<SpyroCollisionTriangle> triangles = new(triangleCount);
        for (int i = 0; i < triangleCount; i++)
            triangles.Add(ReadTriangle(ram, triangleOffset + (i * 12), i));

        return new SpyroCollisionTable(
            Pointers: pointers,
            HeaderOffset: headerOffset,
            TriangleCount: triangleCount,
            BlockTreeOffset: blockTreeOffset,
            BlocksOffset: blocksOffset,
            TriangleOffset: triangleOffset,
            Etc1: ReadUInt32(ram, headerOffset + 20),
            Etc2: ReadUInt32(ram, headerOffset + 24),
            Triangles: triangles);
    }

    private static SpyroSceneCollisionPointers? FindSceneAndCollisionPointers(byte[] ram)
    {
        for (int offset = 0; offset <= ram.Length - 32; offset += 4)
        {
            uint w0 = ReadUInt32(ram, offset);
            if (w0 != 0x02023021u)
                continue;

            uint w1 = ReadUInt32(ram, offset + 4);
            uint w2 = ReadUInt32(ram, offset + 8);
            uint w3 = ReadUInt32(ram, offset + 12);
            uint w4 = ReadUInt32(ram, offset + 16);
            uint w5 = ReadUInt32(ram, offset + 20);
            uint w6 = ReadUInt32(ram, offset + 24);
            if (w1 != 0x00C08021u || w2 != 0x24C60004u || w3 != 0x8CC20000u || w4 != 0x24C60004u)
                continue;
            if ((w5 >> 16) != 0x3C01u || (w6 >> 16) != 0xAC26u)
                continue;

            int pointerTableOffset = BuildAddress(w5, w6);
            if (pointerTableOffset < 0 || pointerTableOffset + 48 > ram.Length)
                continue;

            uint scenePointer = ReadUInt32(ram, pointerTableOffset);
            uint collisionPointer = ReadUInt32(ram, pointerTableOffset + 44);
            if (!IsPsxPointer(scenePointer) || !IsPsxPointer(collisionPointer))
                continue;

            return new SpyroSceneCollisionPointers(
                CodeOffset: offset,
                PointerTableOffset: pointerTableOffset,
                ScenePointer: scenePointer,
                SceneOffset: PsxPointerToOffset(scenePointer - 12),
                CollisionPointer: collisionPointer,
                CollisionOffset: PsxPointerToOffset(collisionPointer));
        }

        return null;
    }

    private static SpyroCollisionTriangle ReadTriangle(byte[] ram, int offset, int index)
    {
        uint xWord = ReadUInt32(ram, offset);
        uint yWord = ReadUInt32(ram, offset + 4);
        uint zWord = ReadUInt32(ram, offset + 8);
        int p1X = (int)(xWord & 0x3FFF);
        int p1Y = (int)(yWord & 0x3FFF);
        int p1Z = (int)(zWord & 0x3FFF);

        SpyroCollisionPoint p1 = new(p1X, p1Y, p1Z);
        SpyroCollisionPoint p2 = new(
            p1X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1Z + (int)((zWord >> 16) & 0xFF));
        SpyroCollisionPoint p3 = new(
            p1X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1Z + (int)((zWord >> 24) & 0xFF));

        return new SpyroCollisionTriangle(index, offset, xWord, yWord, zWord, zWord & 0xC000u, p1, p2, p3);
    }

    private static int BuildAddress(uint upperInstruction, uint lowerInstruction)
    {
        long upper = upperInstruction & 0xFFFF;
        long lower = lowerInstruction & 0xFFFF;
        if (lower >= 0x8000)
            lower -= 0x10000;
        return (int)(((upper << 16) + lower) & 0x003FFFFF);
    }

    private static bool IsPsxPointer(uint value)
    {
        return value >= 0x80000000u && value < 0x80200000u && (value & 3u) == 0;
    }

    private static int PsxPointerToOffset(uint value)
    {
        return IsPsxPointer(value) ? (int)(value & 0x001FFFFFu) : -1;
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        int mask = 1 << bits;
        return (value & sign) != 0 ? value - mask : value;
    }

    private static uint ReadUInt32(byte[] bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length ? BitConverter.ToUInt32(bytes, offset) : 0;
    }

    private static int ReadInt32(byte[] bytes, int offset)
    {
        return offset >= 0 && offset + 4 <= bytes.Length ? BitConverter.ToInt32(bytes, offset) : 0;
    }
}

public sealed record SpyroSceneCollisionPointers(
    int CodeOffset,
    int PointerTableOffset,
    uint ScenePointer,
    int SceneOffset,
    uint CollisionPointer,
    int CollisionOffset);

public sealed record SpyroCollisionTable(
    SpyroSceneCollisionPointers Pointers,
    int HeaderOffset,
    int TriangleCount,
    int BlockTreeOffset,
    int BlocksOffset,
    int TriangleOffset,
    uint Etc1,
    uint Etc2,
    IReadOnlyList<SpyroCollisionTriangle> Triangles);

public sealed record SpyroCollisionTriangle(
    int Index,
    int Offset,
    uint XWord,
    uint YWord,
    uint ZWord,
    uint ZFlags,
    SpyroCollisionPoint P1,
    SpyroCollisionPoint P2,
    SpyroCollisionPoint P3)
{
    public IReadOnlyList<SpyroCollisionPoint> Points { get; } = [P1, P2, P3];
}

public sealed record SpyroCollisionPoint(int X, int Y, int Z);
