using System.Buffers.Binary;
using Spyro.Editor.Core.Exporting;

// Pure surface words only. Exhaustive patterns do not expand any source,
// saved-document, UI or publication profile.
internal static class FlatTileMaterialBindingCases
{
    private static readonly int[] Offsets = [156, 172, 316, 332, 476, 492, 636, 652];

    internal static void Free(Action<bool, string> check)
    {
        var intent = NativeFreshTiledFamilyTerrainIntent.Create(TiledFamilyTerrainIntentCases.Document());
        byte[] surface = intent.CopySurface();
        for (int mask = 1; mask < 128; mask++)
        {
            int[] faces = Enumerable.Range(0, 8).Select(i => i == 0 ? 0 : (mask >> (i - 1)) & 1).ToArray();
            byte[] expected = surface.ToArray();
            for (int i = 0; i < 8; i++) Write(expected, Offsets[i], faces[i]);
            byte[] actual = NativeFlatTileMaterialBinding.Bind(surface, faces, 2);
            check(actual.SequenceEqual(expected), "All127 ordered two-material patterns alter only independent eight face words.");
            check(NativeFlatTileMaterialBinding.RevertExact(surface, actual, faces, 2).SequenceEqual(surface),
                "Complete bound surface inverses to its exact canonical preimage.");
            byte[] changed = actual.ToArray(); changed[Offsets[^1]] ^= 1;
            Reject(() => NativeFlatTileMaterialBinding.RevertExact(surface, changed, faces, 2), check);
        }
        check(NativeFlatTileMaterialBinding.Bind(surface, intent.OrderedFaceMaterials, 2)
            .SequenceEqual(intent.CopyBoundSurface()), "The closed family golden remains byte-exact.");
        check(NativeFlatTileMaterialBinding.Bind(surface, Enumerable.Repeat(255, 8), 0).SequenceEqual(surface),
            "Empty material table requires all base-color FF faces and is identity.");
        int[] boundary = [0, 127, 255, 127, 0, 255, 1, 127];
        byte[] boundaryResult = NativeFlatTileMaterialBinding.Bind(surface, boundary, 128);
        check(Offsets.Select(at => BinaryPrimitives.ReadInt32LittleEndian(boundaryResult.AsSpan(at)))
            .SequenceEqual(boundary), "Direct indexes0..127 and FF remain unmasked.");
        foreach (int count in new[] { -1, 129, int.MaxValue })
            Reject(() => NativeFlatTileMaterialBinding.Bind(surface, boundary, count), check);
        foreach (int count in new[] { 0, 7, 9 })
            Reject(() => NativeFlatTileMaterialBinding.Bind(surface, Enumerable.Repeat(0, count), 2), check);
        foreach (int value in new[] { -1, 2, 128, 254, 256, int.MinValue, int.MaxValue })
        {
            int[] bad = new int[8]; bad[^1] = value;
            Reject(() => NativeFlatTileMaterialBinding.Bind(surface, bad, 2), check);
        }
        Reject(() => NativeFlatTileMaterialBinding.Bind(surface, new int[8], 0), check);
        foreach (int at in new[] { 0, 4, 8, 180, 664, 700, 720, 780, 908 }.Concat(Offsets).Concat(Offsets.Select(i => i + 4)))
        {
            byte[] bad = surface.ToArray(); bad[at] ^= 1;
            Reject(() => NativeFlatTileMaterialBinding.Bind(bad, new int[8], 2), check);
            bad = intent.CopyBoundSurface(); bad[at] ^= 1;
            Reject(() => NativeFlatTileMaterialBinding.RevertExact(surface, bad, intent.OrderedFaceMaterials, 2), check);
        }
        Reject(() => NativeFlatTileMaterialBinding.Bind(surface[..^1], new int[8], 2), check);
        Reject(() => NativeFlatTileMaterialBinding.Bind(surface.Concat(new byte[4]).ToArray(), new int[8], 2), check);
        Reject(() => NativeFlatTileMaterialBinding.Bind(intent.CopyBoundSurface(), new int[8], 2), check);
        Reject(() => NativeFlatTileMaterialBinding.RevertExact(surface, intent.CopyBoundSurface()[..^1], intent.OrderedFaceMaterials, 2), check);
        int moves = 0, disposed = 0;
        IEnumerable<int> Excess()
        {
            try { while (true) { moves++; yield return 0; } }
            finally { disposed++; }
        }
        Reject(() => NativeFlatTileMaterialBinding.Bind(surface, Excess(), 2), check);
        check(moves == 9 && disposed == 1, "Bounded enumeration stops at one excess face and disposes.");
        byte[] mutable = surface.ToArray();
        IEnumerable<int> Mutating()
        {
            mutable[0] ^= 1;
            foreach (int face in intent.OrderedFaceMaterials) yield return face;
        }
        check(NativeFlatTileMaterialBinding.Bind(mutable, Mutating(), 2).SequenceEqual(intent.CopyBoundSurface()),
            "Canonical input is captured before callbacks can mutate caller storage.");
        byte[] boundMutable = intent.CopyBoundSurface();
        IEnumerable<int> MutatingBound()
        {
            boundMutable[^1] ^= 1;
            foreach (int face in intent.OrderedFaceMaterials) yield return face;
        }
        check(NativeFlatTileMaterialBinding.RevertExact(surface, boundMutable, MutatingBound(), 2).SequenceEqual(surface),
            "Inverse captures both full inputs before invoking caller enumeration.");
        byte[] detached = NativeFlatTileMaterialBinding.Bind(surface, intent.OrderedFaceMaterials, 2); detached[0] ^= 1;
        check(surface.SequenceEqual(intent.CopySurface()) && NativeFlatTileMaterialBinding.Bind(surface, intent.OrderedFaceMaterials, 2)
            .SequenceEqual(intent.CopyBoundSurface()), "No mutation of input, retained intent, or future calls.");
        Console.WriteLine("FLAT_TILE_MATERIAL_BINDING_FREE_OK patterns=127 sourceOpened=false admissionExpanded=false");
    }

    private static void Write(byte[] data, int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
    private static void Reject(Action action, Action<bool, string> check)
    {
        try { action(); }
        catch (InvalidDataException) { check(true, "Bounded complete-surface refusal."); return; }
        throw new InvalidDataException("Expected complete-surface refusal.");
    }
}
