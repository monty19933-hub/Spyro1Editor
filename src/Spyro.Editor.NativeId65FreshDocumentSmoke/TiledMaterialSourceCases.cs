using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Primitives;

// Source-issued tiled material composition only. These tests issue no staging,
// Body, disc, candidate file, publication permission or runtime certificate.
internal static class TiledMaterialSourceCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalLength = 661547040;
    // Independent exact-source Python oracle341, frozen before the first new
    // production invocation. These are not learned from the C# implementation.
    private const string UploadReference = "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333";
    private const string BaselineReference = "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa";
    private const string MixedReference = "c0ac16f6e8e063dece5e9386649452f3d507693db656f996099350adb492bfa2";
    private const string ChangedThirdReference = "03d9dc3a28685abe159fd0efb650e40bcd47dd992e8eaff31a70d05f20b1f853";
    private static int _checks;

    internal static async Task Free()
    {
        _checks = 0;
        Type type = typeof(NativeId65TiledMaterialPlan);
        var constructors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        Check(type.IsSealed && !type.IsVisible && constructors.Length > 0 && constructors.All(c => c.IsPrivate),
            "Tiled material issuance is internal, sealed and privately constructed.");
        Check(type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .All(p => p.SetMethod == null), "Source and document identities cannot be replaced through setters.");
        Check(!type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Any(m => m.Name is "CreateNeutral" or "FromMetadata"), "Caller metadata cannot issue a material plan.");

        var document = Document();
        byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(document);
        var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document);
        Check(prepared.Canonical.SequenceEqual(canonical) && document.LegacyContent == null &&
            document.TiledContent!.Tiles.Select(t => t.EditorId).SequenceEqual(Enumerable.Range(1, 3).Select(i => i.ToString("x32"))),
            "Preparation keeps the actual schema4 canonical bytes and ordered stable tile IDs, without a legacy Base.");
        Check(prepared.Surface.CopyBundle().Length == 0x2D4 &&
            Hash(prepared.Surface.CopyBundle()) == "809c2442de920c36fe2bb97db36abd82b2e5888322c95bee51e661c1408ec3d1" &&
            prepared.Surface.Tiles.Select(t => t.X).SequenceEqual(new[] { 8448, 10496, 12544 }),
            "Material preparation retains all three actual native tiles and the independent surface reference.");
        Check(prepared.Selections.Select(s => (s.SourceVariant, s.SourceWadEntry, s.TextureId))
            .SequenceEqual(new[] { (Variant, 14, 0), (Variant, 70, 0) }) &&
            prepared.OrderedFaceMaterials.SequenceEqual(new[] { 0, 1, 0, 1, 0, 1 }),
            "Repeated descriptor values share exactly two deterministic first-occurrence slots.");
        var mixed = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(Document([0, 1, 255, 1, 0, 255]));
        Check(mixed.OrderedFaceMaterials.SequenceEqual(new[] { 0, 1, 255, 1, 0, 255 }) &&
            mixed.Selections.Select(s => s.SourceWadEntry).SequenceEqual(new[] { 14, 70 }),
            "Base-color FF is retained per face and never consumes a source slot.");
        var reversed = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(Document([1, 255, 0, 1, 0, 255]));
        Check(reversed.Selections.Select(s => s.SourceWadEntry).SequenceEqual(new[] { 70, 14 }) &&
            reversed.OrderedFaceMaterials.SequenceEqual(new[] { 0, 255, 1, 0, 1, 255 }),
            "Slot order follows the first actual face occurrence, not source-entry sorting or tile selection.");
        var content = document.TiledContent!;
        var sameEntry = document.WithTiledContent(content.WithTiles(content.Tiles.Select(t =>
            NativeFreshFlatTileIntent.Create(t.EditorId, t.X, t.Y, t.Z, t.Width, t.Depth, t.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, 0),
                    NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, 1))))));
        var sameEntryPrepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(sameEntry);
        Check(sameEntryPrepared.Selections.Select(s => s.TextureId).SequenceEqual(new[] { 0, 1 }) &&
            sameEntryPrepared.OrderedFaceMaterials.SequenceEqual(new[] { 0, 1, 0, 1, 0, 1 }),
            "Neutral deduplication keys include the complete descriptor tuple, not only the donor entry; static eligibility remains unproved.");
        prepared.Canonical.AsSpan().Clear(); prepared.OrderedFaceMaterials[0] = 127;
        prepared.Selections[0] = (NativeFreshRetailTerrainMaterialIntent)NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 10, 0);
        var repeated = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document);
        Check(repeated.Canonical.SequenceEqual(canonical) && repeated.OrderedFaceMaterials[0] == 0 &&
            repeated.Selections[0].SourceWadEntry == 14, "Neutral preparation arrays do not alias the immutable document or another call.");

        const string invalidPath = "\0";
        var legacy = NativeFreshLevelDocumentIntent.Create(NativeId65FreshDocumentIntent.Create(8448, 8448, 8704,
            1024, 1024, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19), []);
        var wizard = NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8960, 13568, 8960);
        var moby = NativeFreshMobyPlacementIntent.Create(new string('b', 32),
            NativeFreshRetailMobyReference.Create(Variant, 10, 0, 0x78), 13568, 8960, 8960, 0);
        var invalidDocuments = new[] { legacy, legacy.WithFloorMaterials(Pair(0, 1)),
            legacy.UpgradeForNativeMobys(), Document([255, 255, 255, 255, 255, 255]),
            Document([0, 255, 0, 255, 0, 255]), Document([0, 1, 0, 1, 0, 2]),
            document.WithPlacements([wizard]), document.WithPlacements([moby]), BoundaryDocument(60) };
        foreach (var invalid in invalidDocuments)
        {
            Reject<InvalidDataException>(() => NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(invalid),
                "Only actual schema4 P0 geometry with exactly two descriptors and a complete fitting allocation can prepare.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForMaterialsAsync(invalidPath, invalid),
                "Unsupported intent refuses before even malformed original-source path I/O.");
        }
        foreach (int zCell in new[] { 56, 58 })
        {
            var boundary = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(BoundaryDocument(zCell));
            int bytes = boundary.Surface.CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength + 2 * 184;
            Check(bytes == (zCell == 56 ? 0xFFC : 0x1000), "Two complete metadata records count toward exact measured allocation capacity.");
        }
        foreach (var retailDocument in new[] { document, Document([0, 1, 255, 1, 0, 255]), Document([255, 255, 255, 255, 0, 1]) })
        {
            Reject<InvalidDataException>(() => NativeId65TiledSurfaceBundle.PrepareDocument(retailDocument),
                "Old Base-only neutral preparation does not silently omit retail choices on any tile.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureAsync(invalidPath, retailDocument),
                "Old Base-only source capture refuses actual retail geometry before I/O.");
        }
        Reject<ArgumentNullException>(() => NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(null!), "Null neutral intent refuses explicitly.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForMaterialsAsync(invalidPath, null!),
            "Null source intent refuses before I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForMaterialsAsync(invalidPath, document, new(true)),
            "Canceled material geometry capture refuses before source I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, null!, null!, null!, new(true)),
            "Canceled material-plan capture refuses before ancestors or source I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, null!, null!, null!),
            "No neutral scalar or metadata substitute can replace the actual fresh resource ancestor.");
        Console.WriteLine($"NATIVE_ID65_TILED_MATERIAL_FREE_OK checks={_checks} sourceRead=false nativeCpuExecuted=false");
    }

    internal static async Task Source(string original)
    {
        await Free();
        original = Path.GetFullPath(original);
        RequireOriginal(original);
        try
        {
            var companion = await NativeId65CompanionAssetPlan.CaptureAsync(original);
            var lists = await NativeId65ZeroSceneAnimationLists.CaptureAsync(original);
            var dark = NativeStaticTerrainTextureSource.Capture(original, 14, 0);
            var gnasty = NativeStaticTerrainTextureSource.Capture(original, 70, 0);
            var document = Document();
            var fresh = await Fresh(document);
            var baseline = await NativeId65TiledMaterialPlan.CaptureAsync(original, fresh, dark, gnasty);
            Validate(baseline, document, fresh, dark, gnasty, [0, 1, 0, 1, 0, 1], BaselineReference, 72);
            var repeat = await NativeId65TiledMaterialPlan.CaptureAsync(original, fresh, dark, gnasty);
            Check(repeat.PlanSha256 == baseline.PlanSha256 && repeat.CopyDataComponents().SequenceEqual(baseline.CopyDataComponents()) &&
                repeat.CopyUploadPayload().SequenceEqual(baseline.CopyUploadPayload()),
                "Repeated composition from the same actual ancestors is deterministic.");
            baseline.RequireAncestry(fresh, document, dark, gnasty);
            var equivalentDocument = NativeFreshLevelDocumentCodec.Parse(NativeFreshLevelDocumentCodec.SerializeCanonical(document));
            Check(!ReferenceEquals(document, equivalentDocument) && NativeFreshLevelDocumentCodec.SerializeCanonical(equivalentDocument)
                .SequenceEqual(baseline.CopyCanonicalDocument()), "Separate document ancestor deliberately has equal complete canonical bytes.");
            Reject<InvalidDataException>(() => baseline.RequireAncestry(fresh, equivalentDocument, dark, gnasty),
                "Equal canonical document hashes cannot substitute the actual retained document ancestor.");
            var equalFresh = await NativeId65FreshSceneBundle.CaptureTiledAsync(original, fresh.ResourceChainAncestor);
            Check(!ReferenceEquals(equalFresh, fresh) && equalFresh.PlanSha256 == fresh.PlanSha256 &&
                equalFresh.UploadSha256 == fresh.UploadSha256 && equalFresh.DataSha256 == fresh.DataSha256,
                "Separate issued fresh ancestor deliberately has identical complete payloads and plan identity.");
            Reject<InvalidDataException>(() => baseline.RequireAncestry(equalFresh, document, dark, gnasty),
                "Equal-hash separately issued fresh ancestry cannot be rebound.");
            var equalDark = NativeStaticTerrainTextureSource.Capture(original, 14, 0);
            Check(!ReferenceEquals(equalDark, dark) && equalDark.Handle == dark.Handle &&
                equalDark.Layout.RecordSha256 == dark.Layout.RecordSha256,
                "Separate actual source capture deliberately has the same certified handle and record.");
            Reject<InvalidDataException>(() => baseline.RequireAncestry(fresh, document, equalDark, gnasty),
                "Equal handles and records cannot replace actual ordered source references.");
            Reject<InvalidDataException>(() => baseline.RequireAncestry(fresh, document, gnasty, dark),
                "Ordered descriptor ancestry cannot be exchanged.");
            const string invalidPath = "\0";
            await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, fresh, gnasty, dark),
                "Source descriptors must match first-occurrence document slots before new source I/O.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, fresh, dark, dark),
                "Repeating one source cannot satisfy the second distinct document descriptor before I/O.");
            await RejectAsync<ArgumentNullException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, fresh, null!, gnasty),
                "A missing actual source issuer cannot be replaced with descriptor metadata.");
            await RejectAsync<OperationCanceledException>(() => NativeId65TiledMaterialPlan.CaptureAsync(invalidPath, fresh, dark, gnasty, new(true)),
                "Canceled valid material capture performs no further source I/O.");
            await ClosedConsumers(fresh, dark, gnasty);
            MutationsAndCopies(baseline);

            var mixedDocument = Document([0, 1, 255, 1, 0, 255]);
            var mixedFresh = await Fresh(mixedDocument);
            var mixed = await NativeId65TiledMaterialPlan.CaptureAsync(original, mixedFresh, dark, gnasty);
            Validate(mixed, mixedDocument, mixedFresh, dark, gnasty, [0, 1, 255, 1, 0, 255], MixedReference, 48);
            Check(mixed.UploadSha256 == baseline.UploadSha256 && mixed.PlanSha256 != baseline.PlanSha256 &&
                mixed.CanonicalDocumentSha256 != baseline.CanonicalDocumentSha256,
                "Reused source slots and Base faces preserve the upload but change actual document and composed data ownership.");
            await ClosedConsumers(mixedFresh, dark, gnasty);

            var changedDocument = Document([0, 1, 0, 1, 1, 0]);
            var changedFresh = await Fresh(changedDocument);
            var changed = await NativeId65TiledMaterialPlan.CaptureAsync(original, changedFresh, dark, gnasty);
            Validate(changed, changedDocument, changedFresh, dark, gnasty, [0, 1, 0, 1, 1, 0], ChangedThirdReference, 72);
            byte[] baselineData = baseline.CopyDataComponents(), changedData = changed.CopyDataComponents();
            Check(Enumerable.Range(0, baselineData.Length).Where(i => baselineData[i] != changedData[i])
                .SequenceEqual(new[] { 0x350, 0x360 }) && changed.UploadSha256 == baseline.UploadSha256,
                "Changing the third tile changes both actual remote face bindings and no selected-tile substitute or other byte.");

            var tiles = document.TiledContent!.Tiles.ToArray(); var first = tiles[0];
            tiles[0] = NativeFreshFlatTileIntent.Create(new string('d', 32), first.X, first.Y, first.Z,
                first.Width, first.Depth, first.FloorColor, first.Materials);
            var idOnlyDocument = document.WithTiledContent(document.TiledContent!.WithTiles(tiles));
            var idOnlyFresh = await Fresh(idOnlyDocument);
            var idOnly = await NativeId65TiledMaterialPlan.CaptureAsync(original, idOnlyFresh, dark, gnasty);
            Check(idOnly.CopyDataComponents().SequenceEqual(baselineData) && idOnly.CopyScene().SequenceEqual(baseline.CopyScene()) &&
                idOnly.UploadSha256 == baseline.UploadSha256 && idOnly.CanonicalDocumentSha256 != baseline.CanonicalDocumentSha256 &&
                idOnly.PlanSha256 != baseline.PlanSha256 && ReferenceEquals(idOnly.Document, idOnlyDocument),
                "An editor-ID-only edit remains actual canonical ancestry despite identical complete native outputs.");
            Reject<InvalidDataException>(() => baseline.RequireAncestry(idOnlyFresh, idOnlyDocument, dark, gnasty),
                "An identical native result from another actual tile-ID owner cannot rebind the baseline plan.");
            Console.WriteLine($"TILED_MATERIAL_SOURCE_IDENTITIES document={baseline.CanonicalDocumentSha256} plan={baseline.PlanSha256} upload={baseline.UploadSha256} data={baseline.DataSha256}");

            async Task<NativeId65FreshSceneBundle> Fresh(NativeFreshLevelDocumentIntent intent)
            {
                var surface = await NativeId65TiledSurfaceBundle.CaptureGeometryForMaterialsAsync(original, intent);
                Check(ReferenceEquals(surface.Document, intent) && surface.CopyDocumentBytes().SequenceEqual(
                    NativeFreshLevelDocumentCodec.SerializeCanonical(intent)), "Source geometry retains the same full retail-material document.");
                var resources = await NativeId65NormalResourceBundle.CaptureTiledAsync(original, surface, companion);
                var chain = NativeId65NormalResourceChain.Compile(resources, lists);
                Check(chain.CopyDataComponents().Length == 0xA58 && chain.DataSha256 ==
                    "fef347e230881f796927562fd4d9482a94c15a43ceae050cbfe5821b6edb59cb",
                    "Unresolved source geometry uses the full independently established three-tile chain, not a legacy floor.");
                return await NativeId65FreshSceneBundle.CaptureTiledAsync(original, chain);
            }
        }
        finally { RequireOriginal(original); }
        Console.WriteLine($"NATIVE_ID65_TILED_MATERIAL_SOURCE_OK checks={_checks} sourceWritten=false bodyIssued=false nativeCpuExecuted=false publicationApproved=false");
    }

    private static void Validate(NativeId65TiledMaterialPlan plan, NativeFreshLevelDocumentIntent document,
        NativeId65FreshSceneBundle fresh, NativeStaticTerrainTextureSource dark, NativeStaticTerrainTextureSource gnasty,
        int[] faces, string dataReference, int packetBytes)
    {
        byte[] upload = plan.CopyUploadPayload(), data = plan.CopyDataComponents(), model = plan.CopyModelPayload(), scene = plan.CopyScene();
        Check(ReferenceEquals(plan.Document, document) && ReferenceEquals(plan.FreshAncestor, fresh) &&
            ReferenceEquals(plan.SurfaceAncestor, fresh.TiledSurfaceAncestor) &&
            ReferenceEquals(plan.MaterialSources[0], dark) && ReferenceEquals(plan.MaterialSources[1], gnasty),
            "Actual document, surface, fresh scene and ordered certified source objects remain retained ancestors.");
        Check(plan.SourceImageSha256 == OriginalHash && plan.UpstreamPlanSha256 == fresh.PlanSha256 &&
            plan.CanonicalDocumentSha256 == Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(document)) &&
            plan.CopyCanonicalDocument().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)) &&
            plan.SourceHandles.SequenceEqual(new[] { dark.Handle, gnasty.Handle }) && plan.OrderedFaceMaterials.SequenceEqual(faces),
            "Complete canonical identity, source handles and all ordered face bindings are explicit and exact.");
        Check(plan.SourceCertified && plan.DestinationAllocationProven && !plan.BodyIssued && !plan.RuntimeVerified &&
            !plan.PublicationApproved && !plan.Allocation.SourceCertified && !plan.Allocation.RuntimeVerified &&
            !plan.Allocation.PublicationApproved, "Actual source binding does not promote neutral allocation, body, runtime or publication authority.");
        Check(plan.NativeClip == 0x8000 && plan.AdditionalPacketBytesPerPass == packetBytes && plan.Evidence.Count == 6,
            "Exact source-consumer clip and extra packet bytes count only actual non-FF faces, not a whole-frame capacity claim.");
        Check(data.Length == 0xBC8 && plan.DataSha256 == dataReference && Hash(data) == dataReference &&
            upload.Length == 0xDE000 && plan.UploadSha256 == UploadReference && Hash(upload) == UploadReference,
            "Complete actual-source upload/data match independently reconstructed pre-production golden hashes.");
        Check(model.SequenceEqual(fresh.CopyModelPayload()) && scene.SequenceEqual(fresh.CopyScene()) &&
            Hash(model) == plan.ModelSha256 && Hash(scene) == plan.SceneSha256 &&
            plan.ModelSha256 == fresh.ModelSha256 && plan.SceneSha256 == fresh.SceneSha256,
            "Every model and scene byte remains the actual upstream source owner.");
        plan.RequireExactOutputs(upload, data, model, scene);
        byte[] before = fresh.CopyDataComponents();
        int[] offsets = FaceOffsets(before);
        Check(Word(data, 0) == 0x178 && Word(data, 4) == 2, "Complete native metadata owns two full184-byte records.");
        var records = NativeTerrainMaterialTableCodec.Decode(data.AsSpan(0, 0x178));
        Check(records.Count == 2 && records.Select(r => r.RecordSha256).SequenceEqual(plan.Allocation.Records.Select(r => r.RecordSha256)),
            "Every LQ and HQ metadata byte binds the actual relocated record, not only a descriptor ID.");
        for (int i = 0; i < offsets.Length; i++)
            Check(Word(data, offsets[i] + 0x170) == faces[i] && Word(data, offsets[i] + 0x174) == 0x80,
                "All six actual sector-derived faces retain exact unmasked material IDs and direct-LQ80 semantics.");
        var editable = offsets.SelectMany(o => Enumerable.Range(o, 4)).ToHashSet();
        Check(Enumerable.Range(8, before.Length - 8).Where(i => !editable.Contains(i)).All(i => before[i] == data[i + 0x170]),
            "Every translated environment, visibility, collision, sky, portal, particle, sound and guard byte is protected.");
        byte[] inverse = new byte[before.Length]; before.AsSpan(0, 8).CopyTo(inverse); data.AsSpan(0x178).CopyTo(inverse.AsSpan(8));
        foreach (int at in offsets) BinaryPrimitives.WriteUInt32LittleEndian(inverse.AsSpan(at), 255);
        Check(inverse.SequenceEqual(before), "Full material data inverse recovers the exact original source-derived tiled chain.");
        Reject<InvalidDataException>(() => NativeId65TiledNormalDataLayout.DecodeUntextured(data),
            "The old untextured tiled decoder is not widened to material metadata.");
        Reject<InvalidDataException>(() => NativeId65NormalDataLayout.DecodeUntextured(data),
            "Legacy one-floor normal admission remains closed to the new composed bytes.");
        UploadComplement(plan);
    }

    private static void UploadComplement(NativeId65TiledMaterialPlan plan)
    {
        byte[] before = plan.FreshAncestor.CopyUploadPayload(), after = plan.CopyUploadPayload();
        var allocated = new bool[before.Length];
        var sourceVram = plan.MaterialSources.Select(s => s.CopySourceVram()).ToArray();
        foreach (var region in plan.Allocation.Allocations)
        {
            Check(region.MaterialId is 0 or 1 && region.SourceIdentity == plan.MaterialSources[region.MaterialId].Handle,
                "Every GPU allocation binds its actual ordered certified source.");
            for (int row = 0; row < region.RowCount; row++)
            {
                int sourceAt = region.SourceOffset + row * 1024, targetAt = region.TargetOffset + row * 1024;
                Check(targetAt >= 0 && targetAt + region.RowByteLength <= 0x80000 &&
                    after.AsSpan(targetAt, region.RowByteLength).SequenceEqual(sourceVram[region.MaterialId].AsSpan(sourceAt, region.RowByteLength)),
                    "Each allocated GPU row equals its complete original source bytes and cannot enter SPU storage.");
                for (int at = targetAt; at < targetAt + region.RowByteLength; at++)
                { Check(!allocated[at], "Allocated native rows do not overlap independent storage owners."); allocated[at] = true; }
            }
        }
        Check(plan.Allocation.AllocatedByteCount == 28160 && plan.Allocation.Allocations.Count == 31 &&
            plan.Allocation.ContainedAliases.Count == 16 && allocated.Count(a => a) == 28160,
            "Actual-source allocation/alias budgets retain the independent allocator controls.");
        Check(Enumerable.Range(0, before.Length).Where(i => !allocated[i]).All(i => before[i] == after[i]) &&
            before.AsSpan(0x80000).SequenceEqual(after.AsSpan(0x80000)),
            "Full unallocated/protected/presentation/companion/zero-valued/SPU complement remains byte exact.");
        foreach (var owner in plan.FreshAncestor.UploadOwners.Where(o => o.Provenance != "fresh-zero-upload-background"))
            Check(!allocated.AsSpan(owner.Offset, owner.ByteLength).Contains(true) &&
                after.AsSpan(owner.Offset, owner.ByteLength).SequenceEqual(before.AsSpan(owner.Offset, owner.ByteLength)),
                "Nonfree ownership stays protected even where its source bytes happen to be zero.");
    }

    private static async Task ClosedConsumers(NativeId65FreshSceneBundle fresh,
        NativeStaticTerrainTextureSource dark, NativeStaticTerrainTextureSource gnasty)
    {
        Reject<InvalidDataException>(() => NativeId65NormalResourceRevision.FromTiled(fresh),
            "Retail material intent cannot escape as an untextured tiled resource revision.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalUploadStaging.CaptureTiledAsync("\0", fresh),
            "Unresolved retail geometry cannot stage an untextured body before source I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshTerrainTexturePlan.CaptureAsync("\0", fresh, dark, gnasty),
            "The original two-face issuer refuses actual tiled ancestry before source I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshTerrainTexturePlan.CaptureSelectedAsync("\0", fresh, dark, gnasty),
            "The selected two-face issuer does not project away additional actual tiles.");
    }

    private static void MutationsAndCopies(NativeId65TiledMaterialPlan plan)
    {
        byte[] upload = plan.CopyUploadPayload(), data = plan.CopyDataComponents(), model = plan.CopyModelPayload(), scene = plan.CopyScene();
        // Every actual data byte is checked, including all metadata, six face IDs
        // and flags, variable collision pointers, retained tail and guard bytes.
        byte[] badData = data.ToArray();
        for (int i = 0; i < data.Length; i++)
        {
            badData[i] ^= 1;
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, badData, model, scene), "Every complete data owner byte rejects mutation.");
            badData[i] ^= 1;
        }
        var uploadOffsets = plan.FreshAncestor.UploadOwners.SelectMany(o => new[] { o.Offset, o.Offset + o.ByteLength - 1 })
            .Concat(plan.Allocation.Allocations.SelectMany(r => new[] { r.TargetOffset, r.TargetOffset + (r.RowCount - 1) * 1024 + r.RowByteLength - 1 }))
            .Concat(new[] { 0, 0x7FFFF, 0x80000, upload.Length - 1 }).Distinct().ToList();
        int protectedZero = plan.FreshAncestor.UploadOwners.Where(o => o.Provenance != "fresh-zero-upload-background")
            .SelectMany(o => Enumerable.Range(o.Offset, o.ByteLength)).First(i => upload[i] == 0);
        uploadOffsets.Add(protectedZero);
        byte[] badUpload = upload.ToArray();
        foreach (int at in uploadOffsets)
        {
            badUpload[at] ^= 1;
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(badUpload, data, model, scene),
                "Allocated, unallocated, protected-zero and GPU/SPU owner boundaries all reject mutation.");
            badUpload[at] ^= 1;
        }
        byte[] badModel = model.ToArray();
        foreach (int at in plan.FreshAncestor.ResourceChainAncestor.Resources.Owners
            .Where(o => o.Domain == NativeId65ResourceDomain.ModelPayload).SelectMany(o => new[] { o.Offset, o.Offset + o.ByteLength - 1 }).Distinct())
        {
            badModel[at] ^= 1;
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, data, badModel, scene), "Every retained model owner boundary rejects mutation.");
            badModel[at] ^= 1;
        }
        byte[] badScene = scene.ToArray();
        foreach (int at in plan.FreshAncestor.SceneLayout.Owners.SelectMany(o => new[] { o.Offset, o.Offset + o.ByteLength - 1 }).Distinct())
        {
            badScene[at] ^= 1;
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, data, model, badScene), "Every scene owner and padding boundary rejects mutation.");
            badScene[at] ^= 1;
        }
        foreach (bool extra in new[] { false, true })
        {
            byte[] Resize(byte[] bytes) => extra ? bytes.Concat(new byte[4]).ToArray() : bytes[..^1];
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(Resize(upload), data, model, scene), "Complete upload rejects truncation and suffix bytes.");
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, Resize(data), model, scene), "Complete data rejects truncation and suffix bytes.");
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, data, Resize(model), scene), "Complete model rejects truncation and suffix bytes.");
            Reject<InvalidDataException>(() => plan.RequireExactOutputs(upload, data, model, Resize(scene)), "Complete scene rejects truncation and suffix bytes.");
        }
        byte[] canonical = plan.CopyCanonicalDocument(); canonical.AsSpan().Clear();
        upload.AsSpan().Clear(); data.AsSpan().Clear(); model.AsSpan().Clear(); scene.AsSpan().Clear();
        byte[] allocatedCopy = plan.Allocation.CopyUploadPayload(); allocatedCopy.AsSpan().Clear();
        byte[] recordCopy = plan.Allocation.Records[0].CopyRecord(); recordCopy.AsSpan().Clear();
        Check(Hash(plan.CopyCanonicalDocument()) == plan.CanonicalDocumentSha256 && Hash(plan.CopyUploadPayload()) == plan.UploadSha256 &&
            Hash(plan.CopyDataComponents()) == plan.DataSha256 && Hash(plan.CopyModelPayload()) == plan.ModelSha256 &&
            Hash(plan.CopyScene()) == plan.SceneSha256 && Hash(plan.Allocation.CopyUploadPayload()) == plan.UploadSha256,
            "Every returned document/output/allocation copy is detached from the immutable source plan.");
        if (plan.OrderedFaceMaterials is IList<int> faceList)
            Reject<NotSupportedException>(() => faceList[0] = 255, "Face bindings cannot be changed through a mutable interface.");
        if (plan.MaterialSources is IList<NativeStaticTerrainTextureSource> sourceList)
            Reject<NotSupportedException>(() => sourceList[0] = sourceList[1], "Ordered source ancestors cannot be changed through a mutable interface.");
        plan.RequireExactOutputs(plan.CopyUploadPayload(), plan.CopyDataComponents(), plan.CopyModelPayload(), plan.CopyScene());
    }

    private static int[] FaceOffsets(byte[] data)
    {
        var decoded = NativeId65TiledNormalDataLayout.DecodeUntextured(data);
        int environment = decoded.Layout.Components[0].Offset;
        Check(Word(data, environment + 4) == 3 && decoded.Surface.Tiles.Count == 3, "Actual canonical fixture has three native environment sectors.");
        var offsets = new List<int>();
        for (int i = 0; i < 3; i++)
        {
            int sector = environment + 4 + Word(data, environment + 8 + 4 * i);
            Check(data.AsSpan(sector + 16, 8).SequenceEqual(new byte[] { 4, 4, 2, 0, 4, 4, 2, 12 }),
                "Validated sector header counts determine high-detail face placement.");
            int firstFace = sector + 28 + 4 * data[sector + 23] + 4 * data[sector + 20] + 8 * data[sector + 21];
            for (int face = 0; face < data[sector + 22]; face++) offsets.Add(firstFace + face * 16 + 8);
        }
        Check(offsets.SequenceEqual(new[] { 0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0 }),
            "All six derived face positions match the independently reconstructed fixture offsets.");
        return offsets.ToArray();
    }

    private static int Word(ReadOnlySpan<byte> bytes, int offset) => BinaryPrimitives.ReadInt32LittleEndian(bytes[offset..]);
    private static void RequireOriginal(string original)
    {
        using var input = new FileStream(original, FileMode.Open, FileAccess.Read, FileShare.Read);
        Check(input.Length == OriginalLength && Convert.ToHexStringLower(SHA256.HashData(input)) == OriginalHash &&
            input.Position == OriginalLength && input.ReadByte() == -1 && input.Length == OriginalLength,
            "The named complete source image remains unchanged through actual EOF.");
    }

    private static NativeFreshLevelDocumentIntent Document(int[]? bindings = null)
    {
        bindings ??= [0, 1, 0, 1, 0, 1];
        return Make(Enumerable.Range(0, 3).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"),
            8448 + 2048 * i, 8448, 8704, 2048, 2048, new(80, 128, 112), Pair(bindings[2 * i], bindings[2 * i + 1]))),
            8960, 8960, 9216);
    }

    private static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell) => Make(
        Enumerable.Range(0, 8).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"),
            16 * i, 0, 4096 * zCell, 16, 16, new(80, 128, 112), Pair(0, 1))), 8, 8, 4096 * zCell + 512);

    private static NativeFreshLevelDocumentIntent Make(IEnumerable<NativeFreshFlatTileIntent> tiles, int sx, int sy, int sz) =>
        NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles, sx, sy, sz, 64, new(128, 96, 64), 19), []);

    private static NativeFreshFloorMaterialsIntent Pair(int first, int second) => NativeFreshFloorMaterialsIntent.Create(Material(first), Material(second));
    private static NativeFreshFloorMaterialIntent Material(int slot) => slot == 255 ? NativeFreshFloorMaterialIntent.BaseFloorColor() :
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, slot switch { 0 => 14, 1 => 70, _ => 10 }, 0);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidDataException(label); _checks++; }
    private static void Reject<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { Check(true, label); return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
    private static async Task RejectAsync<T>(Func<Task> action, string label) where T : Exception
    { try { await action(); } catch (T) { Check(true, label); return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
}
