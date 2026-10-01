using System.Buffers.Binary;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;

namespace Spyro.Editor.Core.Universal;

/// <summary>Copies a count-derived simple untextured PETE model into a local alias. The native
/// shared registrations and resident PETE payload stay unchanged. Animated and textured grammars require independent ownership and are intentionally not guessed.</summary>
public sealed class UniversalSharedModelAssets
{
    private readonly byte[] header, payload;
    private readonly Dictionary<int, byte[]> packages = [];
    public UniversalSharedModelAssets(UniversalDiscImage source)
    {
        var owner = source.Entry(8);
        if (owner.Offset != 0x7B8800) throw new InvalidDataException("Shared model alias requires the verified USA PETE owner.");
        header = source.Wad.AsSpan(owner.Offset, 0x800).ToArray();
        payload = source.Wad.AsSpan(owner.Offset + 0x800, 0x39C48).ToArray();
        RequireHash(header, "b9a4de193b5cf0bd9592de15c57aa5daa3713c3575026154b45402f3aceef6f1");
        RequireHash(payload, "1703ac37e59e480909b6f66dc856d4e5f3ce80551f18cbc8c06129c3b54dd35d");
        foreach (var (offset, length, hash) in new (int, int, string)[]
        {
            (0x3BE0, 0x26C, "fede3c414aea2dce0372fce574549d961123cf6456a7a291acef48b9c4af8598"),
            (0x137EC, 0x11C, "74978cee8e81a0ca2062f9242b53073591e9f830d35eae77ad36ea74852459ca"),
            (0x13908, 0x160, "7afdca06a17dc2589bd62e03b00b3d2d7c601a0a9de9871cde5047ff97a33af1"),
            (0x13A68, 0x6F0, "c9cef2546764b5b14a58c4d9bc33a938512d378758258b97a469ef900f80cfa8")
        }) RequireHash(source.Exe.AsSpan(offset, length), hash);
    }

    public byte[] Package(int classId)
    {
        if (packages.TryGetValue(classId, out var cached)) return cached.ToArray();
        for (int field = 4; field + 8 <= 0x204; field += 8)
        {
            int root = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(field));
            if (root == 0) break;
            if (BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(field + 4)) != classId) continue;
            if (root < 0x800 || root + 0x18L > 0x800 + payload.Length)
                throw new InvalidDataException("Shared model alias root exceeds its verified complete PETE owner.");
            byte[] package;
            try { package = CountDerivedSimplePackage(payload.AsSpan(root - 0x800)); }
            catch (InvalidDataException e)
            { throw new InvalidDataException($"Shared class 0x{classId:X3} has no admitted simple untextured alias package: {e.Message}", e); }
            packages[classId] = package;
            return package.ToArray();
        }
        throw new InvalidDataException($"Shared class 0x{classId:X3} has no verified PETE registration.");
    }

    /// <summary>Bounded native simple-model grammar: negative header, signed-byte vertices,
    /// declared RGB interval and exactly faceCount eight-byte commands. All relative pointers
    /// stay byte-identical. Includes the separately discarded eight-byte pipeline read window.</summary>
    public static byte[] CountDerivedSimplePackage(ReadOnlySpan<byte> owner)
    {
        byte[] ownerCopy = owner.ToArray();
        int W(int offset) => BinaryPrimitives.ReadInt32LittleEndian(ownerCopy.AsSpan(offset));
        if (owner.Length < 24 || W(0) >= 0) throw new InvalidDataException("Not a bounded native simple model.");
        int vertices = unchecked((sbyte)owner[0]), faces = owner[1];
        int vertexStart = W(4), rgbStart = W(8), faceStart = W(12);
        // The verified native prefix path starts at root+16, reads one signed-byte
        // normal triple per vertex, and advances exactly three bytes. Its final
        // discarded triple preload stays inside padding/vertices. RGB face indices
        // then address the generated per-vertex scratch colors, not root+8's table.
        bool normalPrefix = vertices > 0 && vertexStart == UniversalLevelData.Align(16 + vertices * 3, 4);
        if (vertices <= 0 || faces <= 0 || vertexStart != 16 && !normalPrefix || rgbStart != UniversalLevelData.Align(vertexStart + vertices * 3, 4) ||
            faceStart < rgbStart + 4 || faceStart % 4 != 0)
            throw new InvalidDataException("Simple model arrays do not close the admitted normal/vertex pointer grammar.");
        int end = checked(faceStart + faces * 8), colors = normalPrefix ? vertices : (faceStart - rgbStart) / 4;
        if (end + 8 > owner.Length) throw new InvalidDataException("Simple model face/read-ahead window exceeds its source owner.");
        if (normalPrefix)
            for (int p = 16 + vertices * 3; p < vertexStart; p++)
                if (owner[p] != 0) throw new InvalidDataException("Simple normal alignment contains unexplained bytes.");
        for (int p = vertexStart + vertices * 3; p < rgbStart; p++)
            if (owner[p] != 0) throw new InvalidDataException("Simple vertex alignment contains unexplained bytes.");
        int[] Indices(uint word) => [(int)(word >> 23 & 127), (int)(word >> 16 & 127), (int)(word >> 9 & 127), (int)(word >> 2 & 127)];
        for (int p = faceStart; p < end; p += 8)
        {
            uint vw = BinaryPrimitives.ReadUInt32LittleEndian(owner[p..]), cw = BinaryPrimitives.ReadUInt32LittleEndian(owner[(p + 4)..]);
            int[] vi = Indices(vw), ci = Indices(cw);
            // Flags0 consume indexed RGB. Flat/profile flags carry an immediate
            // RGB/normal word instead; treating it as four RGB indices is incorrect.
            if (vi.Any(v => v >= vertices) || (vw & 3) == 0 && ci.Take(vi[2] == vi[3] ? 3 : 4).Any(c => c >= colors))
                throw new InvalidDataException("Simple face references geometry outside the declared vertex/RGB arrays.");
        }
        return owner[..(end + 8)].ToArray();
    }

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected)
    {
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != expected)
            throw new InvalidDataException("Shared model alias source/consumer owner changed from verified original USA bytes.");
    }
}
