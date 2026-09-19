using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

internal static class SharedCollisionGenerationCases
{
    internal static void Run(Action<bool, string> require)
    {
        // Independent compatibility oracle for the old production one-cell
        // encoder. Exercise each dimension and both alignment parities; do not
        // derive expected bytes by calling the new shared generator.
        foreach (var (x, y, z) in new[]
        {
            (0, 0, 0), (1, 0, 0), (0, 1, 0), (0, 0, 1),
            (2, 7, 31), (31, 2, 7), (7, 31, 2), (63, 63, 63)
        })
        {
            var floor = new NativeId65AuthoredFloor(x * 4096, y * 4096,
                z * 4096, 1024, 1024, 0x708050, 0x406080);
            var bundle = NativeId65AuthoredSurfaceLayout.Compile(floor);
            byte[] collision = bundle.CopyComponent(NativeId65SurfaceComponentKind.Collision);
            int treeStart = 4 + Word(collision, 12);
            int blockStart = 4 + Word(collision, 16);
            int triangleStart = 4 + Word(collision, 20);
            int zBytes = (z + 2) * 2, yBytes = (y + 2) * 2, xBytes = (x + 2) * 2;
            byte[] expected = new byte[(zBytes + yBytes + xBytes + 3) & ~3];
            Segment(expected, 0, z, zBytes);
            Segment(expected, zBytes, y, zBytes + yBytes);
            Segment(expected, zBytes + yBytes, x, 0);
            require(treeStart == 32 && blockStart == treeStart + expected.Length &&
                collision.AsSpan(treeStart, expected.Length).SequenceEqual(expected),
                "Actual authored surface retains every old native tree segment, hole and zero-alignment byte.");
            require(triangleStart == blockStart + 8 &&
                collision.AsSpan(blockStart, 8).SequenceEqual(new byte[] { 1, 128, 0, 0, 0, 128, 0, 0 }),
                "Actual authored surface retains the exact old descending group, terminal sentinel and block padding.");
            require(NativeId65AuthoredSurfaceLayout.DecodeAndValidate(bundle.CopyBundle()) == floor,
                "Shared generation preserves complete single-floor canonical readback across sparse native coordinates.");
        }
    }

    private static void Segment(byte[] tree, int at, int selected, int target)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(at), (ushort)(selected + 1));
        for (int i = 0; i < selected; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(at + 2 + i * 2), 0xFFFF);
        BinaryPrimitives.WriteUInt16LittleEndian(tree.AsSpan(at + 2 + selected * 2), (ushort)target);
    }

    private static int Word(byte[] bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at));
}
