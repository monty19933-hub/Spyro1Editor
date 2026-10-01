using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Spyro.Editor.Core.Universal;

public sealed record UniversalPlayerDamageDescriptor(int GroupSlot, int GroupOffset,
    int CommandOffset, int Shape, uint Command, uint DamageFlags);
public sealed record UniversalPlayerDamageCertificate(string Id, int ExecutableOffset, int ByteLength, string Sha256);
public sealed record UniversalPlayerDamageAnalysis(uint DamageMask, IReadOnlyList<int> AnimationIds,
    IReadOnlyList<UniversalPlayerDamageDescriptor> Descriptors, IReadOnlyList<int> PolygonGroupSlots,
    bool SimpleModel, IReadOnlyList<UniversalPlayerDamageCertificate> Certificates)
{
    // Overlay calls/writes, spawned classes and polygon surface behavior require
    // separate closure. This result never certifies all native player dependencies.
    public string Scope => "primary-model-collider-damage";
    public bool CompleteNativeDependenciesVerified => false;
}

/// <summary>Source-certified primary collider mask -> Spyro state -> animation dependency.
/// Analyze pristine source packages before model/texture relocation. Bounds alone
/// are not original-source provenance; the caller must authenticate package ownership.
/// </summary>
public static class UniversalPlayerDamageDependencies
{
    private static readonly IReadOnlyList<UniversalPlayerDamageCertificate> Evidence = Array.AsReadOnly<UniversalPlayerDamageCertificate>(
    [
        new("model-collider-relocation", 0x3BE0, 0x84, "a5e7717afede0e5c34bf356ba542ad85c30ea5f5ea8f062ada3543be3fc77eac"),
        new("model-collider-partition-selector", 0x42EA8, 0xB0, "6957574246c153803d760eb6e0001529b8016eaec09da84f94aa893cc5560c21"),
        new("primary-collider-mask-consumer", 0x3EBC8, 0x7E0, "7bb841a0f73d9d42c747f8cc51667d7ce1721b725f77abbec6d4f40275c17504"),
        new("player-collider-accumulator", 0x310EC, 0x38, "f507f7314696fc58e8d6fcf49d44034bce1837ae58e193ea06ca3d3d1f1009b7"),
        new("damage-state-selector", 0x31768, 0x308, "f5cb321da9444ee83d3b6a2bc042f4aa5383e987c6558498fb8345835a0333c1"),
        new("contact-damage-state-selector", 0x31A70, 0x2B8, "5899077857f33f80234f11411315db0db42604413970408582dcd85ae8f88edc"),
        new("state-writer-animation-consumer", 0x30550, 0x50, "5266a1997876840d432cfca3c1f7ece643c3981c8f6b9cda26c76a0a1b6a4c0f"),
        new("state-animation-map", 0x5CC70, 0x2D, "9f502dce2b68cb8497072c5c9c92d3f914710cdbbcbc9c25c86cbe29a4241e0b"),
        new("polygon-secondary-table", 0x3DE50, 0x38, "ae5d79860f5cba47ffb214b3611f8324b213360fca5ba0d0f72d618131cc11e5")
    ]);

    public static IReadOnlyList<int> MapDamageMask(uint mask, byte[] executable)
    {
        ArgumentNullException.ThrowIfNull(executable);
        RequireExecutable(executable);
        return Array.AsReadOnly(MapCertifiedMask(mask, executable));
    }

    private static int[] MapCertifiedMask(uint mask, byte[] executable)
    {
        SortedSet<int> states = [];
        // Flags accumulate from several colliders. Taking only the highest
        // priority bit of a union would miss another independently contacted
        // shape. Union every reachable state; ordinary shared slots are later
        // reused by the source-bound animation merge, not duplicated here.
        if ((mask & 1) != 0 || (mask & 6) == 6) states.Add(14);
        if ((mask & 6) != 0 || (mask & 0x8000) != 0) states.Add(12);
        if ((mask & 0x10) != 0) { states.Add(25); states.Add(31); }
        if ((mask & 0x20) != 0) states.Add(7);
        if ((mask & 0x40) != 0) states.Add(27);
        if ((mask & 0x80) != 0) states.Add(28);
        if ((mask & 0x100) != 0) states.Add(22);
        if ((mask & 0x400) != 0) states.Add(29);
        if ((mask & 0x800) != 0) { states.Add(17); states.Add(15); }
        return states.Select(s => (int)executable[0x5CC70 + s]).Distinct().Order().ToArray();
    }

    private static void RequireExecutable(byte[] executable)
    {
        if (executable.Length != 0x66000)
            throw new InvalidDataException("Player damage dependencies require the complete fixed USA executable owner.");
        foreach (var c in Evidence)
            if (c.ExecutableOffset > executable.Length - c.ByteLength ||
                Convert.ToHexStringLower(SHA256.HashData(executable.AsSpan(c.ExecutableOffset, c.ByteLength))) != c.Sha256)
                throw new InvalidDataException($"Player damage dependency consumer differs from certified USA source: {c.Id}.");
    }

    private static void ValidateSimpleModel(byte[] owner)
    {
        int vertices = unchecked((sbyte)owner[0]), faces = owner[1];
        int At(int field) => BinaryPrimitives.ReadInt32LittleEndian(owner.AsSpan(field));
        int vertexStart = At(4), colourStart = At(8), faceStart = At(12);
        bool normals = vertices > 0 && vertexStart == (16 + vertices * 3 + 3) / 4 * 4;
        if (vertices <= 0 || faces <= 0 || vertexStart != 16 && !normals ||
            colourStart != (vertexStart + vertices * 3 + 3) / 4 * 4 ||
            faceStart < colourStart + 4 || faceStart % 4 != 0 || faceStart + (long)faces * 8 > owner.Length)
            throw new InvalidDataException("Simple model has no bounded native vertex/colour/face grammar.");
        int colours = normals ? vertices : (faceStart - colourStart) / 4;
        int[] Indices(uint w) => [(int)(w >> 23 & 127), (int)(w >> 16 & 127), (int)(w >> 9 & 127), (int)(w >> 2 & 127)];
        for (int p = faceStart; p < faceStart + faces * 8; p += 8)
        {
            uint vw = BinaryPrimitives.ReadUInt32LittleEndian(owner.AsSpan(p)), cw = BinaryPrimitives.ReadUInt32LittleEndian(owner.AsSpan(p + 4));
            int[] vi = Indices(vw);
            if (vi.Any(v => v >= vertices) || (vw & 3) == 0 && Indices(cw).Take(vi[2] == vi[3] ? 3 : 4).Any(c => c >= colours))
                throw new InvalidDataException("Simple model face leaves its source vertex/colour owner.");
        }
        // Unlike a relocated alias issuer, dependency inspection does not need
        // to manufacture the renderer's discarded eight-byte pipeline preload.
    }

    public static UniversalPlayerDamageAnalysis Analyze(byte[] modelPackage, byte[] executable)
    {
        ArgumentNullException.ThrowIfNull(modelPackage);
        ArgumentNullException.ThrowIfNull(executable);
        RequireExecutable(executable);
        if (modelPackage.Length < 16 || modelPackage.Length > 0x200000 || modelPackage.Length % 4 != 0)
            throw new InvalidDataException("Player damage dependency model needs a complete aligned bounded package.");
        uint Word(int at) => BinaryPrimitives.ReadUInt32LittleEndian(modelPackage.AsSpan(at, 4));
        if ((Word(0) & 0x80000000) != 0)
        {
            // Negative native model headers select the simple array loader,
            // not the animated header's eight collision pointers at +0x14.
            ValidateSimpleModel(modelPackage);
            return Result(0, [], [], true);
        }
        uint animationCount = Word(0);
        if (modelPackage.Length < 0x38 || animationCount > 256 || 0x38L + animationCount * 4 > modelPackage.Length)
            throw new InvalidDataException("Animated model header/animation roots exceed the source package.");
        int headerEnd = checked(0x38 + (int)animationCount * 4);
        uint firstGroup = Word(0x14);
        List<UniversalPlayerDamageDescriptor> descriptors = [];
        List<int> polygons = [];
        uint mask = 0;
        for (int slot = 0; slot < 8; slot++)
        {
            uint raw = Word(0x14 + slot * 4);
            if (raw == 0) continue;
            if (firstGroup == 0 || (raw & 0x80000000) != (firstGroup & 0x80000000))
                throw new InvalidDataException("Model collider groups have unsupported mixed native table partitions.");
            uint offset = raw & 0x7FFFFFFF;
            if (offset % 4 != 0 || offset < headerEnd || offset > modelPackage.Length - 8L)
                throw new InvalidDataException($"Model collision group {slot} leaves its source package or aliases its header.");
            if ((raw & 0x80000000) == 0)
            {
                // 8005272C..38 places positive roots in table+0x1000;
                // 8004D668 reads that table for ground/polygon queries.
                // 8004E440 reads only the primary table. Polygon bytes are
                // not the command grammar and must never become damage flags.
                polygons.Add(slot);
                continue;
            }
            int group = (int)offset, cursor = group + 4;
            bool terminal = false;
            for (int commandIndex = 0; commandIndex < 256; commandIndex++)
            {
                if (cursor > modelPackage.Length - 4)
                    throw new InvalidDataException($"Primary collider group {slot} has no complete terminal command.");
                uint command = Word(cursor);
                int shape = (int)(command >> 8 & 255);
                int stride = shape switch { 0 => 8, 1 => 12, 2 => 20, 3 => 12, _ => 0 };
                if (stride == 0 || (command & 0xF8) != 0 || cursor > modelPackage.Length - stride)
                    throw new InvalidDataException($"Primary collider group {slot} has unsupported or truncated shape-command grammar.");
                uint flags = Word(cursor + stride - 4);
                descriptors.Add(new(slot, group, cursor, shape, command, flags));
                mask |= flags;
                cursor += stride;
                if ((command & 1) != 0) { terminal = true; break; }
            }
            if (!terminal)
                throw new InvalidDataException($"Primary collider group {slot} exceeds the bounded terminal-command limit.");
        }
        return Result(mask, descriptors, polygons, false);

        UniversalPlayerDamageAnalysis Result(uint flags, List<UniversalPlayerDamageDescriptor> commands, List<int> polygonSlots, bool simple)
        {
            int[] ids = MapCertifiedMask(flags, executable);
            return new(flags, Array.AsReadOnly(ids), commands.AsReadOnly(), polygonSlots.AsReadOnly(), simple, Evidence);
        }
    }
}
