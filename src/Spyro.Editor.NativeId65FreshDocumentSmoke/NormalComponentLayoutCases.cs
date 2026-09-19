using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

// Synthetic tail headers establish neutral component geometry only. They are
// deliberately not source certificates, resource assets or native loader proof.
internal static class NormalComponentLayoutCases
{
    private static int _checks;

    internal static void Run()
    {
        _checks = 0;
        byte[] single = Data(NativeId65AuthoredSurfaceLayout.Compile(
            new(8448, 8448, 8704, 1024, 1024, 0x708050, 0x406080)).CopyBundle());
        var one = NativeId65NormalComponentLayout.Decode(single);
        Check(one.SurfaceOffset == 8 && one.SurfaceByteLength == 324 &&
            one.UsedByteLength == 0x8C8 && one.SectorByteLength == 0x1000,
            "Retained one-floor geometry derives its complete used end.");
        Check(one.Components.Select(c => c.Offset).SequenceEqual(new[] { 8, 180, 212, 220, 316 }) &&
            one.Components.Select(c => c.ByteLength).SequenceEqual(new[] { 172, 32, 8, 96, 16 }) &&
            one.Components.Select(c => c.Kind).SequenceEqual(Enum.GetValues<NativeId65SurfaceComponentKind>()),
            "All five one-floor spans retain actual data-relative offsets and order.");
        Check(one.Portal.Offset == 332 && one.Portal.ByteLength == 4 &&
            one.Particles.Offset == 336 && one.Particles.ByteLength == 0x80 &&
            one.Sound.Offset == 464 && one.Sound.ByteLength == 0x6F8 &&
            one.CollisionTreeOffset == 252 && one.CollisionTreeByteLength == 24,
            "Portal, particle, sound and collision-pointer owners are independently located.");

        var tiles = new NativeFlatSurfaceTile[]
        {
            new(8448, 8448, 8704, 2048, 2048, 0x708050),
            new(10496, 8448, 8704, 2048, 2048, 0x806050),
            new(12544, 8448, 8704, 2048, 2048, 0x607050),
        };
        byte[] tiled = Data(NativeFlatTileSurfaceLayout.Compile(tiles, 0x406080).CopyBundle());
        var three = NativeId65TiledNormalDataLayout.DecodeUntextured(tiled);
        Check(three.Layout.SurfaceOffset == 8 && three.Layout.SurfaceByteLength == 724 &&
            three.Layout.UsedByteLength == 0xA58 && three.Layout.SectorByteLength == 0x1000,
            "Three actual tiles derive a different used end without a new CD-sector policy.");
        Check(three.Layout.Components.Select(c => c.Offset).SequenceEqual(new[] { 8, 508, 540, 548, 716 }) &&
            three.Layout.Components.Select(c => c.ByteLength).SequenceEqual(new[] { 500, 32, 8, 168, 16 }) &&
            three.Layout.Portal.Offset == 732 && three.Layout.Particles.Offset == 736 &&
            three.Layout.Sound.Offset == 864 && three.Layout.CollisionTreeOffset == 580 &&
            three.Layout.CollisionTreeByteLength == 28,
            "Tiled component walk uses actual pointers, not the legacy surface-minus-300 shortcut.");
        Check(three.Surface.Tiles.SequenceEqual(tiles) &&
            three.Surface.CopyBundle().SequenceEqual(tiled.AsSpan(8, 724).ToArray()),
            "Distinct tiled wrapper validates complete canonical geometry and tile order.");
        Reject(() => NativeId65NormalDataLayout.DecodeUntextured(tiled),
            "Neutral tiled admission cannot widen legacy 8BC..A38 or certify the source.");

        var lengths = new HashSet<int>();
        for (int sum = 0; sum <= 189; sum++)
        {
            int x = Math.Min(63, sum), y = Math.Min(63, sum - x), z = sum - x - y;
            byte[] data = Data(NativeId65AuthoredSurfaceLayout.Compile(
                new(x * 4096, y * 4096, z * 4096, 16, 16, 0x708050, 0x406080)).CopyBundle());
            var legacy = NativeId65NormalDataLayout.DecodeUntextured(data);
            var shared = NativeId65NormalComponentLayout.Decode(data);
            int tree = (2 * (sum + 6) + 3) & ~3;
            Check(shared.UsedByteLength == 0x8B0 + tree && shared.CollisionTreeByteLength == tree &&
                shared.UsedByteLength == legacy.UsedByteLength && shared.SurfaceByteLength == legacy.SurfaceByteLength &&
                shared.CollisionTreeByteLength == legacy.CollisionTreeByteLength && shared.SectorByteLength == legacy.SectorByteLength,
                "Shared walker preserves every old one-floor cell sum and exact used end.");
            lengths.Add(shared.UsedByteLength);
        }
        Check(lengths.Count == 96 && lengths.Min() == 0x8BC && lengths.Max() == 0xA38,
            "The exact old 96-length admission remains unchanged.");
        VariantBoundaries(single, tiled);
        Corruption(single, one);
        Corruption(tiled, three.Layout);
        Immutability(tiled, three);
        Console.WriteLine($"NATIVE_ID65_NORMAL_COMPONENT_LAYOUT_OK checks={_checks} nativeCpuExecuted=false sourceAuthority=false");
    }

    private static void VariantBoundaries(byte[] single, byte[] tiled)
    {
        byte[] shortSurface = new byte[20];
        for (int offset = 0; offset < shortSurface.Length; offset += 4) Write(shortSurface, offset, 4);
        RefuseBoth(Data(shortSurface),
            "Even a complete minimum five-component/tail partition must own the collision pointer header.");
        Check(NativeId65TiledNormalDataLayout.DecodeUntextured(single).Surface.Tiles.Count == 1,
            "The neutral tiled grammar also validates the actual compatible single-tile bytes.");
        byte[] two = Data(NativeFlatTileSurfaceLayout.Compile(new NativeFlatSurfaceTile[]
        {
            new(0, 0, 0, 16, 16, 0x708050), new(16, 0, 0, 16, 16, 0x806050),
        }, 0x406080).CopyBundle());
        NativeId65NormalDataLayout.RequireBoundedUsedLength(two.Length);
        Check(NativeId65TiledNormalDataLayout.DecodeUntextured(two).Surface.Tiles.Count == 2,
            "Actual two-tile bytes can fall inside the old numeric length range.");
        Reject(() => NativeId65NormalDataLayout.DecodeUntextured(two),
            "Matching a legacy length does not weaken its exact single-floor grammar.");

        // Insert an owned word before the tree and rebase every native pointer.
        // The structural walker must derive BOTH endpoints from collision+4;
        // neither assuming tree+32 nor surfaceLength-300 is correct here.
        byte[] shifted = new byte[single.Length + 4];
        single.AsSpan(0, 252).CopyTo(shifted);
        single.AsSpan(252).CopyTo(shifted.AsSpan(256));
        Write(shifted, 220, 100);
        for (int field = 232; field <= 248; field += 4)
            Write(shifted, field, Read(single, field) + 4);
        var moved = NativeId65NormalComponentLayout.Decode(shifted);
        Check(moved.CollisionTreeOffset == 256 && moved.CollisionTreeByteLength == 24 &&
            moved.SurfaceByteLength == 328 && moved.Portal.Offset == 336 && moved.Sound.Offset == 468,
            "Shared pointer geometry remains structural and tracks a rebased complete chain.");
        Reject(() => NativeId65NormalDataLayout.DecodeUntextured(shifted),
            "Legacy canonical decoder refuses a structurally valid noncanonical header gap.");
        Reject(() => NativeId65TiledNormalDataLayout.DecodeUntextured(shifted),
            "Tiled canonical decoder also refuses a structurally valid noncanonical header gap.");

        // An in-range pointer/flag mutation can preserve every component span;
        // only actual canonical surface validation may accept the tiled wrapper.
        byte[] changedFlag = tiled.ToArray();
        changedFlag[708] ^= 1;
        Check(NativeId65NormalComponentLayout.Decode(changedFlag).UsedByteLength == tiled.Length,
            "Neutral component geometry deliberately makes no triangle-flag certification.");
        Reject(() => NativeId65TiledNormalDataLayout.DecodeUntextured(changedFlag),
            "Actual tiled surface validation binds collision flags, not just length/count headers.");

        // Sixteen ordinary adjacent tiles cross a CD-sector boundary. Reporting
        // alignment here is arithmetic, not permission for old body/disc paths.
        byte[] larger = Data(NativeFlatTileSurfaceLayout.Compile(Enumerable.Range(0, 16)
            .Select(i => new NativeFlatSurfaceTile(i * 2048, 0, 0, 2048, 2048, 0x708050)), 0x406080).CopyBundle());
        var extended = NativeId65TiledNormalDataLayout.DecodeUntextured(larger);
        Check(extended.Layout.UsedByteLength == larger.Length && larger.Length > 0x1000 &&
            extended.Layout.SectorByteLength == 0x1800 && extended.Surface.Tiles.Count == 16,
            "Neutral alignment follows actual component extent rather than fixed legacy 1000 storage.");
        Reject(() => NativeId65NormalDataLayout.DecodeUntextured(larger),
            "Larger neutral allocation cannot enter the old body data profile.");
    }

    private static void Corruption(byte[] canonical, NativeId65NormalComponentLayout layout)
    {
        byte[] before = canonical.ToArray();
        foreach (int length in new[] { 0, 4, 0x794, canonical.Length - 1, canonical.Length - 4, 0x20788 })
        {
            byte[] wrong = new byte[length];
            canonical.AsSpan(0, Math.Min(canonical.Length, length)).CopyTo(wrong);
            RefuseBoth(wrong, "Missing, unaligned or excessive used owner refuses before unsafe slicing.");
        }
        foreach (var component in layout.Components)
            foreach (int value in new[] { int.MinValue, -4, 0, 2, 7, int.MaxValue, component.ByteLength + 4 })
                Changed(component.Offset, value, "Every component length is bounded and owns exactly its span.");
        foreach (var (offset, value) in new[]
        {
            (0, 12), (4, 1), (layout.Portal.Offset, 1),
            (layout.Particles.Offset, 4), (layout.Particles.Offset + 4, 9),
            (layout.Sound.Offset, 4), (layout.Sound.Offset + 0x104, 75),
        })
            Changed(offset, value, "Metadata, raw portals and complete retained tail headers remain exact.");
        var collision = layout.Components.Single(c => c.Kind == NativeId65SurfaceComponentKind.Collision);
        for (int field = 12; field <= 28; field += 4)
        {
            foreach (int value in new[] { int.MinValue, -1, int.MaxValue, 0, 27, 29, collision.ByteLength - 4 })
                Changed(collision.Offset + field, value,
                    "Unsigned native pointer addition cannot escape, wrap, misalign or target a header/end.");
            if (field > 12)
                Changed(collision.Offset + field, Read(canonical, collision.Offset + field - 4),
                    "Distinct collision regions cannot alias their preceding target start.");
        }
        byte[] trailing = canonical.Concat(new byte[4]).ToArray();
        RefuseBoth(trailing, "Sector padding is not part of the exact parser used owner.");
        Check(canonical.SequenceEqual(before), "Every malformed-input refusal preserves the caller's canonical bytes.");

        void Changed(int offset, int value, string label)
        {
            byte[] bad = canonical.ToArray();
            Write(bad, offset, value);
            byte[] originalBad = bad.ToArray();
            RefuseBoth(bad, label);
            Check(bad.SequenceEqual(originalBad), "A failed walk must not normalize caller corruption.");
        }
    }

    private static void Immutability(byte[] input, NativeId65TiledNormalDataLayout decoded)
    {
        byte[] surface = decoded.Surface.CopyBundle();
        byte[] copy = decoded.Surface.CopyBundle();
        copy[0] ^= 1;
        input.AsSpan().Fill(0xFF);
        Check(decoded.Layout.UsedByteLength == 0xA58 && decoded.Layout.Components[0].Offset == 8 &&
            decoded.Surface.CopyBundle().SequenceEqual(surface),
            "Both parser metadata and canonical surface snapshot are detached from mutable input/copies.");
        var list = decoded.Layout.Components as IList<NativeId65NormalComponentSpan>;
        Check(list != null && list.IsReadOnly, "Surface component collection exposes no writable array alias.");
        try { list![0] = list[0] with { Offset = 0 }; }
        catch (NotSupportedException) { _checks++; return; }
        throw new InvalidDataException("Component spans unexpectedly permit mutation.");
    }

    private static void RefuseBoth(byte[] bytes, string label)
    {
        Reject(() => NativeId65NormalComponentLayout.Decode(bytes), label);
        Reject(() => NativeId65TiledNormalDataLayout.DecodeUntextured(bytes), label);
    }

    private static byte[] Data(byte[] surface)
    {
        byte[] data = new byte[surface.Length + 0x784];
        Write(data, 0, 8);
        surface.CopyTo(data, 8);
        int particles = surface.Length + 12, sound = particles + 0x80;
        Write(data, particles, 0x80);
        Write(data, particles + 4, 10);
        Write(data, sound, 0x6F8);
        Write(data, sound + 0x104, 76);
        return data;
    }

    private static void Write(byte[] bytes, int offset, int value) =>
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(offset), value);
    private static int Read(byte[] bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(offset));
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidDataException(label);
        _checks++;
    }
    private static void Reject(Action action, string label)
    {
        try { action(); }
        catch (InvalidDataException) { _checks++; return; }
        throw new InvalidDataException("Expected refusal: " + label);
    }
}
