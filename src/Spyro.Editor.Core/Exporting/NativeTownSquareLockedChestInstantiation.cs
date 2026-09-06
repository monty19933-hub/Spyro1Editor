using System.Buffers.Binary;
using Spyro.Editor.Core.Scene;

namespace Spyro.Editor.Core.Exporting;

internal sealed record NativeTownSquareLockedChestInstanceCapture(NativeMobyDependencyInstanceTemplate Template,
    NativeMobyDependencyInstantiationPlan Empty, NativeMobyDependencyInstantiationPlan Allocated);

/// <summary>
/// Exact native factory for the one accepted seven-row recipe. Two simultaneous families are
/// intentionally not authorized: the native key state and tracked chest gem are mutable globals.
/// This factory does not invent a sequential scheduler, global reset, or second gameplay recipe.
/// </summary>
internal static class NativeTownSquareLockedChestInstantiation
{
    internal const string FamilyId = "peace-native-chest-family";
    internal static NativeTownSquareLockedChestInstanceCapture Capture(NativeAddressSpace peace,
        NativeAddressSpace sceneOriginal, NativeAddressSpace sceneFinal, NativeAddressSpace executable,
        IEnumerable<NativeAddressSpace> finalDomains, NativeMobyDependencyClosurePlan shared,
        Func<NativeByteExtent, byte[]> readSource)
    {
        // Recheck actual bytes at the factory boundary; domain labels are not source authority.
        byte[] donor = readSource(new(peace, 0, peace.ByteLength));
        byte[] exe = readSource(new(executable, 0, executable.ByteLength));
        if (NativeMobyDependencyInstanceTemplate.Hash(donor) != "c140bd422471c66cd57bd85b29912819f798f5b080779153674714889306c9d3" ||
            NativeMobyDependencyInstanceTemplate.Hash(exe) != "a533d75cab8afaae6107ec35a02a9a5fe979a92c7c955f9cf1ee50f693a1b998")
            throw new InvalidDataException("Native chest instantiation requires the certified donor/executable source bytes.");
        byte[] scene = readSource(new(sceneOriginal, 0, sceneOriginal.ByteLength));
        RequireExactNativeScene(scene);
        const int first = 107, rowOffset = 0x170 + first * 0x58, propertyOffset = 0x5F0C;
        if (U32(scene, 0x16C) != first || U32(scene, 0x4E38) != 0x10D4 ||
            scene.AsSpan(rowOffset, 7 * 0x58).ContainsAnyExcept((byte)0))
            throw new InvalidDataException("Native chest row append arena is occupied or not source-owned.");
        RequireNoPlacedKeyFamily(scene);
        NativeMobyDependencyRead Read(NativeAddressSpace space, int offset, int length, string id = "native") =>
            new(id, new(space, offset, length), NativeMobyDependencyInstanceTemplate.Hash(readSource(new(space, offset, length))));
        int[] indices = [78,79,118,119,120,121,122];
        int[] offsets = [0xEA94,0xEA98,0xEF9C,0xEFC4,0xEFEC,0xF014,0xF03C];
        int[] lengths = [4,0x18,0x28,0x28,0x28,0x28,0x28];
        int fixupCount = checked((int)U32(donor, 0x1C2800 + 0x11234));
        if (fixupCount <= 0 || 0x11238L + fixupCount * 4L > 0x11800)
            throw new InvalidDataException("Native donor fixup table changed.");
        var fixups = Enumerable.Range(0,fixupCount).Select(i => U32(donor, 0x1C2800 + 0x11238 + i * 4)).ToArray();
        List<NativeMobyDependencyPrivateSchema> rows = [];
        for (int i = 0; i < indices.Length; i++)
        {
            if (!fixups.Contains((uint)(0x8260 + indices[i] * 0x58)) ||
                fixups.Any(f => f >= offsets[i] && f < offsets[i] + lengths[i]))
                throw new InvalidDataException("Native key/chest/reward properties have an unknown internal pointer obligation.");
            rows.Add(new(i == 0 ? "key" : i == 1 ? "chest" : "reward." + (i - 1), indices[i],
                (ushort)(i == 0 ? 0xAD : i == 1 ? 0xAE : 0xD),
                Read(peace, 0x1CAA60 + indices[i] * 0x58, 0x58), Read(peace, 0x1C2800 + offsets[i], lengths[i]),
                0x1C2800, i == 0 ? [] : [new(0, i == 1 ? 0 : 1)], i != 0));
        }
        // These exact consumers are already closed/hashed by the native shared resource graph.
        // Pickup writes one g5830; every chest reads that same state; the award tail compares one
        // g5758 and writes g5830=3. Private pointers cannot make those globals per-family.
        var keyConsumer = shared.Nodes.Single(n => n.Id == "key.native-handler").Reads.Single();
        var chestConsumer = shared.Nodes.Single(n => n.Id == "behavior.bundle").Reads.Single(r => r.Id == "state");
        NativeMobyDependencyMutableGlobal[] globals = [
            new("key-state", executable, 0x80075830, 4, [keyConsumer, chestConsumer]),
            new("tracked-chest-gem", executable, 0x80075758, 4, [chestConsumer, Read(executable, 0x2C21C, 0x14, "award-tracked-gem")])
        ];
        var template = NativeMobyDependencyInstanceTemplate.Capture(
            "native-chest.peace-to-town.source-bound-relocation.v2", rows, globals, shared, readSource);
        var empty = NativeMobyDependencyInstantiationPlan.Create(new(sceneFinal, rowOffset, 7 * 0x58),
            new(sceneFinal, propertyOffset, 0xE4), 0x170, first, finalDomains, executable);
        return new(template, empty, empty.Allocate([new(FamilyId, template)]));
    }
    // This narrow absence rule is derived from the exact retail Town/donor executable closure,
    // not a universal catalog-name test for arbitrary classes or modified native overlays.
    internal static void RequireNoPlacedKeyFamily(ReadOnlySpan<byte> scene)
    {
        if (scene.Length < 0x170 || BinaryPrimitives.ReadUInt32LittleEndian(scene.Slice(0x16C,4)) != 107 || scene.Length < 0x170 + 107 * 0x58)
            throw new InvalidDataException("Native destination instance-table ownership changed.");
        for (int i=0;i<107;i++)
            if (BinaryPrimitives.ReadUInt16LittleEndian(scene.Slice(0x170+i*0x58+0x36,2)) is 0xAD or 0xAE or 0xD)
                throw new InvalidDataException("Destination-native key-family singleton users are already present.");
    }
    internal static void RequireExactNativeScene(ReadOnlySpan<byte> scene)
    {
        if (scene.Length != 0x8800 || NativeMobyDependencyInstanceTemplate.Hash(scene) != "63b5e699b3f175c0289a795c4f6f36786b9bc33ce608e1f1e9bd7bb4c04f09f6")
            throw new InvalidDataException("Native chest instantiation requires the complete exact original Town scene preimage.");
    }
    private static uint U32(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset, 4));
}
