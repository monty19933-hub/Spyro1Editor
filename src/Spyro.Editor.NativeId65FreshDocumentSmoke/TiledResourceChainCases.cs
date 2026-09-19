using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

// This slice ends at an issued data/list pair. No scene, body, executable patch,
// candidate file, native execution or publication is produced by these cases.
internal static class TiledResourceChainCases
{
    private const long OriginalLength = 661547040;
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private static int _checks;

    internal static async Task SourceFree()
    {
        _checks = 0;
        Type type = typeof(NativeId65TiledSurfaceBundle);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Check(!type.IsVisible && type.IsSealed && constructors.Length > 0 && constructors.All(c => c.IsPrivate),
            "Tiled source issuance is internal, sealed and privately constructed.");
        Check(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(p => p.SetMethod == null), "Issued document, byte owners and certificate properties have no setters.");
        Check(!type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(m => m.Name == "CreateNeutral"), "No neutral tiled surface can be upgraded through an issuer factory.");

        string missing = MissingSourcePath();
        var @base = NativeId65FreshDocumentIntent.Create(8448, 8448, 8704, 1024, 1024,
            8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19);
        var schema1 = NativeFreshLevelDocumentIntent.Create(@base, []);
        var schema2 = schema1.WithFloorMaterials(BaseMaterials());
        foreach (var legacy in new[] { schema1, schema2, schema2.UpgradeForNativeMobys() })
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(missing, legacy),
                "Every legacy schema refuses before nonexistent-source I/O; no dummy tiled Base is issued.");
        foreach (int tile in new[] { 0, 2 })
            foreach (int face in new[] { 0, 1 })
                await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(missing,
                    Document(texturedTile: tile, texturedFace: face)),
                    "Both faces of every tile require BaseFloorColor before source I/O.");
        var wizard = NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8960, 13568, 8960);
        var moby = NativeFreshMobyPlacementIntent.Create(new string('b', 32),
            NativeFreshRetailMobyReference.Create(Variant, 10, 0, 0x78), 13568, 8960, 8960, 0);
        foreach (NativeFreshPlacementIntent placement in new NativeFreshPlacementIntent[] { wizard, moby })
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(missing,
                Document().WithPlacements([placement])), "All placement variants refuse before source I/O in this zero-placement slice.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(missing, null!),
            "A missing document cannot reach source I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(missing,
            Document(), new CancellationToken(canceled: true)), "Canceled valid tiled capture stops before nonexistent-source I/O.");
        Check(!File.Exists(missing) && !Directory.Exists(Path.GetDirectoryName(missing)),
            "Source-free refusal creates no input path or artifact.");
        Console.WriteLine($"NATIVE_ID65_TILED_RESOURCE_FREE_OK checks={_checks} sourceRead=false nativeCpuExecuted=false");
    }

    internal static async Task Source(string original)
    {
        await SourceFree();
        original = Path.GetFullPath(original);
        RequireOriginal(original, "before");
        try
        {
            var document = Document();
            var issued = await NativeId65TiledSurfaceBundle.CaptureAsync(original, document);
            byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(document);
            byte[] surface = issued.CopyBundle();
            Check(issued.SourceCertified && issued.SourceImageSha256 == OriginalHash &&
                !issued.PublicationApproved && !issued.RuntimeVerified && !issued.IndependentLevelSupported,
                "Exact original source capture issues no runtime, independent-level or publication permission.");
            Check(issued.Document.SchemaVersion == 4 && issued.Document.LegacyContent == null &&
                issued.CopyDocumentBytes().SequenceEqual(canonical) &&
                NativeFreshLevelDocumentCodec.SerializeCanonical(issued.Document).SequenceEqual(canonical) &&
                issued.DocumentSha256 == Hash(canonical), "Issuer binds the complete actual canonical schema4 document, without fabricated Base.");
            Check(issued.ByteLength == 0x2D4 && surface.Length == 0x2D4 && issued.BundleSha256 == Hash(surface),
                "Three actual native tiles own the complete 724-byte surface.");
            var decoded = NativeFlatTileSurfaceLayout.DecodeAndValidate(surface);
            Check(decoded.Tiles.Select(t => t.X).SequenceEqual(new[] { 8448, 10496, 12544 }) &&
                decoded.Tiles.All(t => t.Y == 8448 && t.Z == 8704 && t.Width == 2048 && t.Depth == 2048),
                "Source-issued bytes decode to all three ordered authored tiles, not a substituted original floor.");
            Check(issued.Components.Select(c => c.BlockOffset).SequenceEqual(new[] { 0, 500, 532, 540, 708 }) &&
                issued.Components.Select(c => c.ByteLength).SequenceEqual(new[] { 500, 32, 8, 168, 16 }),
                "All five source-issued components retain actual variable offsets.");
            int cursor = 0;
            foreach (var owner in issued.Components)
            {
                Check(owner.BlockOffset == cursor && Hash(surface.AsSpan(owner.BlockOffset, owner.ByteLength)) == owner.Sha256,
                    "Each surface component owns an exact contiguous byte span and hash.");
                cursor += owner.ByteLength;
            }
            Check(cursor == surface.Length, "Surface owners partition the entire emitted bundle.");
            issued.RequireExactBundle(surface);
            foreach (int offset in issued.Components.SelectMany(c => new[] { c.BlockOffset, c.BlockOffset + c.ByteLength - 1 }))
            {
                byte[] bad = surface.ToArray(); bad[offset] ^= 1;
                Reject<InvalidDataException>(() => issued.RequireExactBundle(bad), "No issued owner, pointer, guard or complement can mutate.");
            }
            Reject<InvalidDataException>(() => issued.RequireExactBundle(surface[..^1]), "Truncated issued surface refuses.");
            Reject<InvalidDataException>(() => issued.RequireExactBundle(surface.Concat(new byte[4]).ToArray()), "Extra issued surface bytes refuse.");

            var companion = await NativeId65CompanionAssetPlan.CaptureAsync(original);
            var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(original);
            byte[] executable = issued.CopySourceExecutable();
            Check(executable.SequenceEqual(companion.EngineProfile.CopyExecutable()),
                "Tiled issuer and existing normal companion bind the same complete original executable.");
            lists.RequireSourceExecutable(executable);
            var resources = await NativeId65NormalResourceBundle.CaptureTiledAsync(original, issued, companion);
            Reject<InvalidOperationException>(() => _ = resources.Surface,
                "A tiled resource revision refuses the public legacy Surface accessor instead of fabricating Base.");
            Check(resources.SourceCertified && !resources.CompleteDataChainAvailable && !resources.CompleteLevelResourcesProven &&
                !resources.CompletePlayerParticleClosureProven && !resources.IndependentLevelSupported &&
                !resources.PublicationApproved && !resources.RuntimeVerified,
                "Existing resource closure/runtime/publication barriers remain false for tiled capture.");
            Reject<InvalidOperationException>(() => resources.CopyDataComponents(), "Resource bundle alone cannot invent the paired scene-list owner.");
            Reject<InvalidDataException>(() => NativeId65NormalResourceChain.Compile(resources,
                NativeId65ZeroSceneAnimationLists.CreateNeutral()), "Neutral zero-scene-list bytes cannot confer source pairing authority.");
            var chain = NativeId65NormalResourceChain.Compile(resources, lists);
            byte[] data = chain.CopyDataComponents(), prefix = chain.CopySceneAnimationComponents();
            var layout = NativeId65TiledNormalDataLayout.DecodeUntextured(data).Layout;
            Check(data.Length == 0xA58 && layout.SurfaceByteLength == 0x2D4 && layout.CollisionTreeByteLength == 28 &&
                layout.Sound.Offset == 864 && layout.UsedByteLength == data.Length && layout.SectorByteLength == 0x1000,
                "Actual issued chain uses the three-tile dataA58/surface2D4/tree28 component-derived layout.");
            Reject<InvalidDataException>(() => NativeId65NormalDataLayout.DecodeUntextured(data),
                "Tiled source issuance does not widen old single-floor 8BC..A38 admission.");
            Check(Read(data, 0) == 8 && Read(data, 4) == 0 && data.AsSpan(8, surface.Length).SequenceEqual(surface) &&
                data.AsSpan(8).SequenceEqual(resources.CopyDataComponentTail()) && prefix.SequenceEqual(lists.CopyComponents()) &&
                chain.DataSha256 == Hash(data) && chain.SceneComponentsSha256 == Hash(prefix),
                "Only paired zero metadata precedes the complete issued tail; every output hash matches actual bytes.");
            Check(chain.DataComponentsEncoded && chain.SceneListBytesEncoded && !chain.CompleteLevelResourcesProven &&
                !chain.SceneInitialized && !chain.PublicationApproved && !chain.RuntimeVerified,
                "Data/list encoding does not promote scene, complete-level, runtime or publication gates.");
            Check(chain.ScenePlacementRequirement == lists.PlacementRequirement &&
                NativeId65NormalResourceChain.Compile(resources, lists).PlanSha256 == chain.PlanSha256,
                "Repeated compilation is deterministic and retains the exact paired scene-list requirement.");
            Owners(resources, chain, companion.EngineProfile.CopyTownData());
            CopiesAndExactBytes(issued, resources, chain, canonical, surface, executable);

            string missing = MissingSourcePath();
            await RejectAsync<InvalidDataException>(() => NativeId65FreshSceneBundle.CaptureAsync(missing, chain,
                new(document.SpawnX, document.SpawnY, document.SpawnZ, document.Heading), 16),
                "Issued tiled chain is explicitly refused before FreshScene source I/O; this slice ends at the chain.");
            Check(!File.Exists(missing), "Scene refusal creates no file.");

            var twoDocument = NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(
                Enumerable.Range(0, 2).Select(i => NativeFreshFlatTileIntent.Create(new string((char)('1' + i), 32),
                    i * 16, 0, 0, 16, 16, new ColorRgba(80, 128, 112), BaseMaterials())),
                8, 8, 512, 64, new ColorRgba(128, 96, 64), 19), []);
            var twoIssued = await NativeId65TiledSurfaceBundle.CaptureAsync(original, twoDocument);
            var twoResources = await NativeId65NormalResourceBundle.CaptureTiledAsync(original, twoIssued, companion);
            var twoChain = NativeId65NormalResourceChain.Compile(twoResources, lists);
            byte[] twoData = twoChain.CopyDataComponents();
            NativeId65NormalDataLayout.RequireBoundedUsedLength(twoData.Length);
            Check(NativeId65TiledNormalDataLayout.DecodeUntextured(twoData).Surface.Tiles.Count == 2,
                "A genuinely issued two-tile chain fits the legacy numeric used-length range.");
            Reject<InvalidDataException>(() => NativeId65NormalDataLayout.DecodeUntextured(twoData),
                "In-range source-issued tiled bytes still cannot pass legacy canonical surface admission.");
            await RejectAsync<InvalidDataException>(() => NativeId65FreshSceneBundle.CaptureAsync(missing, twoChain,
                new(twoDocument.SpawnX, twoDocument.SpawnY, twoDocument.SpawnZ, twoDocument.Heading), 16),
                "The tiled scene barrier uses the active source revision, not a legacy-size heuristic, before I/O.");

            foreach (var alternative in new[]
            {
                Document(firstId: new string('4', 32)),
                document.WithTiledContent(document.TiledContent!.WithSettings(8976, document.SpawnY, document.SpawnZ,
                    document.Heading, document.SkyColor, document.MusicTrackId)),
                document.WithTiledContent(document.TiledContent!.WithSettings(document.SpawnX, document.SpawnY, document.SpawnZ,
                    document.Heading, document.SkyColor, 2)),
            })
            {
                var otherIssued = await NativeId65TiledSurfaceBundle.CaptureAsync(original, alternative);
                var otherResources = await NativeId65NormalResourceBundle.CaptureTiledAsync(original, otherIssued, companion);
                var otherChain = NativeId65NormalResourceChain.Compile(otherResources, lists);
                Check(otherIssued.DocumentSha256 != issued.DocumentSha256 && otherIssued.BundleSha256 == issued.BundleSha256 &&
                    otherIssued.CopyBundle().SequenceEqual(surface), "Editor ID, spawn and music each change document identity without changing native terrain.");
                Check(otherResources.UploadSha256 == resources.UploadSha256 && otherResources.ModelSha256 == resources.ModelSha256 &&
                    otherResources.DataTailSha256 == resources.DataTailSha256 && otherChain.DataSha256 == chain.DataSha256 &&
                    otherChain.SceneComponentsSha256 == chain.SceneComponentsSha256 && otherChain.PlanSha256 != chain.PlanSha256,
                    "The complete chain binds canonical document identity even when every emitted payload byte is identical.");
            }
            Console.WriteLine($"TILED_RESOURCE_CHAIN_IDENTITIES document={issued.DocumentSha256} surface={issued.BundleSha256} data={chain.DataSha256} chain={chain.PlanSha256}");
        }
        finally { RequireOriginal(original, "after"); }
        Console.WriteLine($"NATIVE_ID65_TILED_RESOURCE_SOURCE_OK checks={_checks} sourceWritten=false nativeCpuExecuted=false sceneIssued=false");
    }

    private static void Owners(NativeId65NormalResourceBundle resources, NativeId65NormalResourceChain chain, byte[] sourceData)
    {
        byte[] upload = resources.CopyUploadPayload(), tail = resources.CopyDataComponentTail(), model = resources.CopyModelPayload();
        Check(upload.Length == 0xDE000 && model.Length == 0xF800 && tail.Length == 0xA50 &&
            resources.UploadSha256 == "ea5e5e56b7ba35abaaa55f7fc3cabe388cc64a47a00a5a07b8d72fecd475e1e4" &&
            resources.ModelSha256 == "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d",
            "Tiled terrain preserves independent retained normal upload/model goldens and actual tail length.");
        foreach (var domain in Enum.GetValues<NativeId65ResourceDomain>())
        {
            byte[] bytes = domain switch { NativeId65ResourceDomain.UploadPayload => upload,
                NativeId65ResourceDomain.DataComponentTail => tail, _ => model };
            int end = 0;
            foreach (var owner in resources.Owners.Where(o => o.Domain == domain).OrderBy(o => o.Offset))
            {
                Check(owner.Offset == end && owner.ByteLength > 0 && Hash(bytes.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                    "Each resource domain retains a complete ordered partition with exact per-owner byte hashes.");
                end += owner.ByteLength;
            }
            Check(end == bytes.Length, "Resource owner partition reaches the actual payload end.");
        }
        byte[] data = chain.CopyDataComponents();
        int cursor = 0;
        foreach (var owner in chain.DataOwners)
        {
            Check(owner.Offset == cursor && Hash(data.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "Data-chain owners preserve actual offsets and hash the complete variable chain.");
            cursor += owner.ByteLength;
        }
        Check(cursor == data.Length && data.AsSpan(736, 0x80).SequenceEqual(sourceData.AsSpan(0x172590, 0x80)) &&
            data.AsSpan(864, 0x6F8).SequenceEqual(sourceData.AsSpan(0x172610, 0x6F8)) &&
            upload.AsSpan(0x80000).SequenceEqual(sourceData.AsSpan(0x80800, 0x5E000)),
            "Moved particle/sound components retain complete actual source descriptors and SPU bank, not synthetic headers.");
        foreach (var link in resources.SoundLinks)
            Check(model[link.ModelRelativeFieldOffset] == link.RetainedDescriptorIndex &&
                Hash(tail.AsSpan(link.DataTailDescriptorOffset, 20)) == link.DescriptorSha256 &&
                Read(tail, link.DataTailDescriptorOffset) == link.SampleBankRelativeOffset,
                "Companion byte-local sound references target their moved actual descriptor owner.");
    }

    private static void CopiesAndExactBytes(NativeId65TiledSurfaceBundle issued, NativeId65NormalResourceBundle resources,
        NativeId65NormalResourceChain chain, byte[] canonical, byte[] surface, byte[] executable)
    {
        issued.CopyDocumentBytes().AsSpan().Fill(0xFF);
        issued.CopyBundle().AsSpan().Fill(0xFF);
        issued.CopySourceExecutable().AsSpan().Fill(0xFF);
        Check(issued.CopyDocumentBytes().SequenceEqual(canonical) && issued.CopyBundle().SequenceEqual(surface) &&
            issued.CopySourceExecutable().SequenceEqual(executable), "Returned canonical, surface and executable arrays cannot mutate source-issued owners.");
        byte[] upload = resources.CopyUploadPayload(), tail = resources.CopyDataComponentTail(), model = resources.CopyModelPayload();
        byte[] data = chain.CopyDataComponents(), prefix = chain.CopySceneAnimationComponents();
        resources.RequireExactPayloads(upload, tail, model);
        chain.RequireExactPairedBytes(data, prefix);
        foreach (var owner in chain.DataOwners)
        {
            byte[] bad = data.ToArray(); bad[owner.Offset] ^= 1;
            Reject<InvalidDataException>(() => chain.RequireExactPairedBytes(bad, prefix), "Every paired data owner remains exactly bound.");
        }
        Reject<InvalidDataException>(() => chain.RequireExactPairedBytes(data[..^1], prefix), "Truncated data owner refuses.");
        Reject<InvalidDataException>(() => chain.RequireExactPairedBytes(data.Concat(new byte[4]).ToArray(), prefix), "Extra data owner refuses.");
        Reject<InvalidDataException>(() => chain.RequireExactPairedBytes(data, prefix[..^1]), "Truncated paired scene lists refuse.");
        byte[] changedPrefix = prefix.ToArray(); changedPrefix[^1] ^= 1;
        Reject<InvalidDataException>(() => chain.RequireExactPairedBytes(data, changedPrefix), "Scene-list guard complement remains exactly bound.");
        foreach (var (domain, bytes) in new[] { (0, upload), (1, tail), (2, model) })
        {
            byte[] bad = bytes.ToArray(); bad[^1] ^= 1;
            Reject<InvalidDataException>(() => resources.RequireExactPayloads(domain == 0 ? bad : upload,
                domain == 1 ? bad : tail, domain == 2 ? bad : model), "Resource payload complements cannot mutate after source capture.");
        }
        resources.CopyUploadPayload().AsSpan().Clear(); resources.CopyDataComponentTail().AsSpan().Clear(); resources.CopyModelPayload().AsSpan().Clear();
        chain.CopyDataComponents().AsSpan().Clear(); chain.CopySceneAnimationComponents().AsSpan().Clear();
        Check(resources.CopyUploadPayload().SequenceEqual(upload) && resources.CopyDataComponentTail().SequenceEqual(tail) &&
            resources.CopyModelPayload().SequenceEqual(model) && chain.CopyDataComponents().SequenceEqual(data) &&
            chain.CopySceneAnimationComponents().SequenceEqual(prefix) && chain.CopyUploadPayload().SequenceEqual(upload) &&
            chain.CopyModelPayload().SequenceEqual(model), "Resource and paired-chain payload copies are immutable and retain shared ownership.");
    }

    private static NativeFreshLevelDocumentIntent Document(string? firstId = null, int texturedTile = -1, int texturedFace = -1)
    {
        var tiles = Enumerable.Range(0, 3).Select(i => NativeFreshFlatTileIntent.Create(
            i == 0 && firstId != null ? firstId : new string((char)('1' + i), 32),
            8448 + i * 2048, 8448, 8704, 2048, 2048, new ColorRgba(80, 128, 112),
            i == texturedTile ? NativeFreshFloorMaterialsIntent.Create(
                texturedFace == 0 ? NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 10, 0) : NativeFreshFloorMaterialIntent.BaseFloorColor(),
                texturedFace == 1 ? NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 10, 0) : NativeFreshFloorMaterialIntent.BaseFloorColor()) : BaseMaterials()));
        return NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles,
            8960, 8960, 9216, 64, new ColorRgba(128, 96, 64), 19), []);
    }

    private static NativeFreshFloorMaterialsIntent BaseMaterials() => NativeFreshFloorMaterialsIntent.Create(
        NativeFreshFloorMaterialIntent.BaseFloorColor(), NativeFreshFloorMaterialIntent.BaseFloorColor());
    private static string MissingSourcePath() => Path.Combine(Path.GetTempPath(), "spyro-tiled-resource-missing-" + Guid.NewGuid().ToString("N"), "original.bin");
    private static uint Read(byte[] bytes, int offset) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(offset));
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool value, string label)
    {
        if (!value) throw new InvalidDataException(label);
        _checks++;
    }
    private static void Reject<T>(Action action, string label) where T : Exception
    {
        try { action(); }
        catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label);
    }
    private static async Task RejectAsync<T>(Func<Task> action, string label) where T : Exception
    {
        try { await action(); }
        catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label);
    }
    private static void RequireOriginal(string path, string phase)
    {
        using var input = File.OpenRead(path);
        Check(input.Length == OriginalLength && HashStream(input) == OriginalHash && input.Position == OriginalLength &&
            input.ReadByte() == -1 && input.Length == OriginalLength, "Whole exact original hash, length and EOF remain unchanged before/after capture.");
        Console.WriteLine($"TILED_RESOURCE_ORIGINAL_{phase.ToUpperInvariant()} sha256={OriginalHash} length={input.Length} eof=true");
    }
    private static string HashStream(Stream input) => Convert.ToHexStringLower(SHA256.HashData(input));
}
