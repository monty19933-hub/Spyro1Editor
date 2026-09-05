using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

internal static class ConsumerCases
{
    internal static void Run(Action<bool, string> check)
    {
        // Exercise the existing production mapper, not a copied offset formula.
        Type compiler = typeof(NativeTerrainTexturePrivateMultiLevelBatchCompiler);
        object entries = ArrayOf(compiler, "ArchiveEntryMap", new object[][]
        {
            [1, 0x800L, 0x3000], [2, 0x3800L, 0x3000], [3, 0x6800L, 0x1000]
        });
        object insertions = ArrayOf(compiler, "EntryInsertion", new object[][]
        {
            [1, 0x1800L, 0x800, "first"], [2, 0x1000L, 0x800, "second"]
        });
        Type relocationType = compiler.Assembly.GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocationPlan")!;
        const string relocationJson = """
            {"Required":true,"WadLba":37,"OriginalWadSize":30720,"ExpandedWadSize":34816,
             "WadGrowthBytes":4096,"EntryGrowthBytes":{"1":2048,"2":2048},
             "RelocatedEntryOffsets":{"1":2048,"2":16384,"3":30720},"RelocatedPatches":[]}
            """;
        object relocation = JsonSerializer.Deserialize(relocationJson, relocationType)!;
        foreach ((long before, long after) in new[] { (0x1000L, 0x1000L), (0x1FFCL, 0x1FFCL),
                     (0x2000L, 0x2800L), (0x47FCL, 0x4FFCL), (0x4800L, 0x5800L), (0x7000L, 0x8000L) })
        {
            byte[] bytes = [1, 2, 3, 4];
            NativeTerrainTexturePrivateStructuralPatch patch = new(before, 4, bytes, [5, 6, 7, 8],
                Hash(bytes), Hash([5, 6, 7, 8]), "fixture", "row", "synthetic");
            var mapped = MapAggregate(patch, relocation);
            check(mapped.SourceWadOffset == before && mapped.RelocatedWadOffset == after &&
                  mapped.Before.SequenceEqual(bytes) && mapped.After.SequenceEqual(new byte[] { 5, 6, 7, 8 }),
                "Existing aggregate mapping no longer preserves the known source/final byte contract.");
        }
        foreach (long offset in new[] { 0xFFCL, 0x1FFEL, 0x47FEL, 0x77FEL })
        {
            byte[] bytes = [1, 2, 3, 4];
            var patch = new NativeTerrainTexturePrivateStructuralPatch(offset, 4, bytes, bytes,
                Hash(bytes), Hash(bytes), "fixture", "row", "synthetic");
            bool rejected = false;
            try { _ = MapAggregate(patch, relocation); }
            catch (InvalidDataException) { rejected = true; }
            check(rejected, "Aggregate mapping accepted a structural header, insertion crossing or unowned span.");
        }

        // A caller-provided final entry table cannot be trusted merely because
        // each patch's offset arithmetic succeeds. The old consumer accepted
        // this inconsistent final extent and returned the wrong byte address.
        object inconsistentRelocation = JsonSerializer.Deserialize(
            relocationJson.Replace("\"3\":30720", "\"3\":30724", StringComparison.Ordinal), relocationType)!;
        byte[] guardBytes = [1, 2, 3, 4];
        bool geometryRejected = false;
        try
        {
            _ = MapAggregate(new NativeTerrainTexturePrivateStructuralPatch(0x7000, 4, guardBytes, guardBytes,
                Hash(guardBytes), Hash(guardBytes), "fixture", "row", "synthetic"), inconsistentRelocation);
        }
        catch (InvalidDataException) { geometryRejected = true; }
        check(geometryRejected, "Actual aggregate mapper accepted a final entry offset inconsistent with its packed extent/growth contract.");

        Type append = typeof(NativeTerrainTextureRecordAppendBuilder);
        Type layoutType = append.GetNestedType("LevelDataLayout", BindingFlags.NonPublic)!;
        object layout = JsonSerializer.Deserialize("""
            {"TextureComponentByteLength":376,"TextureCount":2,"UsedByteLength":512,
             "ZeroTailByteCount":512,"Components":[]}
            """, layoutType)!;
        byte[] source = Enumerable.Range(0, 1024).Select(value => (byte)value).ToArray();
        // Two 16-byte LQ records start at8, two168-byte HQ records start at40.
        // One new row adds16 bytes before HQ and184 before following components.
        foreach ((int before, int after) in new[] { (8, 8), (36, 36), (40, 56), (372, 388), (376, 560), (508, 692) })
        {
            byte[] composed = source.ToArray();
            var patch = new NativeTerrainTextureRecordExistingPatch(0x1000 + before,
                source.AsSpan(before, 4).ToArray(), [9, 8, 7, 6], "fixture", "row");
            object?[] arguments = { new[] { patch }, 0x1000L, layout, source, composed, 1, null, null };
            bool accepted = (bool)Invoke(append, "TryComposeExistingPatches", arguments)!;
            var proofs = (NativeTerrainTextureRecordRebasedPatchProof[])arguments[6]!;
            byte[] expected = source.ToArray();
            patch.After.CopyTo(expected, before);
            check(accepted && proofs.Single().OutputWadOffset == 0x1000 + after && composed.SequenceEqual(expected),
                "Existing LQ/HQ/body mapping or pre-insertion patch bytes changed.");
        }
        foreach (int offset in new[] { 4, 38, 374, 510 })
        {
            byte[] composed = source.ToArray();
            var patch = new NativeTerrainTextureRecordExistingPatch(0x1000 + offset,
                source.AsSpan(offset, 4).ToArray(), [9, 8, 7, 6], "fixture", "row");
            object?[] arguments = { new[] { patch }, 0x1000L, layout, source, composed, 1, null, null };
            check(!(bool)Invoke(append, "TryComposeExistingPatches", arguments)! && composed.SequenceEqual(source),
                "Rejected append-boundary patch changed pre-insertion bytes.");
        }
        VerifySingleDestination(check);

        NativeTerrainTexturePrivateAggregatePatch MapAggregate(NativeTerrainTexturePrivateStructuralPatch patch, object plan)
        {
            MethodInfo mapper = compiler.GetMethod("MapPatch", BindingFlags.NonPublic | BindingFlags.Static)!;
            // Retain the same behavioral fixture against the pre-extraction DLL
            // for baseline/red evidence; both branches invoke production code.
            return (NativeTerrainTexturePrivateAggregatePatch)(mapper.GetParameters().Length == 4
                ? Invoke(compiler, "MapPatch", patch, entries, insertions, plan)
                : Invoke(compiler, "MapPatch", patch, entries, Invoke(compiler, "CreatePatchMap", entries, insertions, plan)))!;
        }
    }

    private static void VerifySingleDestination(Action<bool, string> check)
    {
        string directory = Directory.CreateTempSubdirectory("spyro-single-relocation-fixture-").FullName;
        try
        {
            byte[] image = new byte[64 * 2048];
            Span<byte> pvd = image.AsSpan(16 * 2048, 2048);
            pvd[0] = pvd[6] = 1;
            "CD001"u8.CopyTo(pvd[1..]);
            BinaryPrimitives.WriteUInt16LittleEndian(pvd[128..], 2048);
            BinaryPrimitives.WriteUInt16BigEndian(pvd[130..], 2048);
            pvd[156] = 34; pvd[188] = 1;
            BinaryPrimitives.WriteInt32LittleEndian(pvd[158..], 20);
            BinaryPrimitives.WriteInt32BigEndian(pvd[162..], 20);
            BinaryPrimitives.WriteInt32LittleEndian(pvd[166..], 2048);
            BinaryPrimitives.WriteInt32BigEndian(pvd[170..], 2048);
            for (int offset = 0; offset < 0x7800; offset++) image[37 * 2048 + offset] = (byte)(offset % 251);
            string path = Path.Combine(directory, "synthetic.iso");
            File.WriteAllBytes(path, image);
            Type composer = typeof(NativeTerrainTextureRecordSectorRelocationComposer);
            Array entries = ArrayOf(composer, "ArchiveEntryMap", new object[][]
                { [1, 0x800L, 0x3000], [2, 0x3800L, 0x3000], [3, 0x6800L, 0x1000] });
            Type relocationType = composer.Assembly.GetType("Spyro.Editor.Core.Exporting.NativeSkyWadRelocationPlan")!;
            object relocation = JsonSerializer.Deserialize("""
                {"Required":true,"WadLba":37,"OriginalWadSize":30720,"ExpandedWadSize":32768,
                 "WadGrowthBytes":2048,"EntryGrowthBytes":{"1":2048},
                 "RelocatedEntryOffsets":{"1":2048,"2":16384,"3":28672},"RelocatedPatches":[]}
                """, relocationType)!;
            var append = JsonSerializer.Deserialize<NativeTerrainTextureRecordAppendPlan>("{\"Patch\":{\"OutputByteLength\":4096}}")!;
            var request = JsonSerializer.Deserialize<NativeTerrainTextureRecordSectorRelocationRequest>("{}")! with { SourceImagePath = path };
            foreach ((long before, long after) in new[] { (0x1000L, 0x1000L), (0x2000L, 0x2800L), (0x4000L, 0x4800L), (0x7000L, 0x7800L) })
            {
                NativeTerrainTextureRecordRelocationExternalPatch patch = Patch(before);
                var result = (NativeTerrainTextureRecordRelocatedExternalPatch[])Invoke(composer, "MapExternalPatches",
                    request with { ExternalPatches = [patch] }, append, relocation, entries, entries.GetValue(0), 0x1000, 0x800, 0x1800L)!;
                check(result.Single().RelocatedWadOffset == after && result.Single().Before.SequenceEqual(patch.Before) &&
                      result.Single().After.SequenceEqual(patch.After), "Single-destination source-bound patch mapping changed its exact byte result.");
            }
            foreach (long offset in new[] { 0xFFCL, 0x1800L, 0x1FFEL })
            {
                bool rejected = false;
                try { _ = Invoke(composer, "MapExternalPatches", request with { ExternalPatches = [Patch(offset)] },
                    append, relocation, entries, entries.GetValue(0), 0x1000, 0x800, 0x1800L); }
                catch (InvalidDataException) { rejected = true; }
                check(rejected, "Single-destination mapping accepted a header, separately owned level-data patch or insertion crossing.");
            }
            NativeTerrainTextureRecordRelocationExternalPatch stale = Patch(0x1000) with { Before = [255, 255, 255, 255] };
            bool staleRejected = false;
            try { _ = Invoke(composer, "MapExternalPatches", request with { ExternalPatches = [stale] },
                append, relocation, entries, entries.GetValue(0), 0x1000, 0x800, 0x1800L); }
            catch (InvalidDataException) { staleRejected = true; }
            check(staleRejected && File.ReadAllBytes(path).SequenceEqual(image), "Mapping changed the source or accepted stale source bytes.");

            NativeTerrainTextureRecordRelocationExternalPatch Patch(long offset) => new(offset,
                image.AsSpan(37 * 2048 + (int)offset, 4).ToArray(), [255, 254, 253, 252], "fixture", "row", "synthetic");
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static Array ArrayOf(Type parent, string name, object[][] rows)
    {
        Type type = parent.GetNestedType(name, BindingFlags.NonPublic)!;
        Array array = Array.CreateInstance(type, rows.Length);
        for (int index = 0; index < rows.Length; index++) array.SetValue(Activator.CreateInstance(type, rows[index]), index);
        return array;
    }
    private static object? Invoke(Type type, string method, params object?[] arguments)
    {
        try { return type.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, arguments); }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        { ExceptionDispatchInfo.Capture(ex.InnerException).Throw(); throw; }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
