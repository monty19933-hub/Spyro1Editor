using System.Collections;
using System.Buffers.Binary;
using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Analysis;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Explicit selected-material source tests. Neutral preparation and caller labels
// do not authenticate static records or admit UI, publication or native runtime.
internal static class SelectedTiledMaterialCompilationCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const long OriginalLength = 661547040;
    // Independent source/data401, original-native loader402 and complete-disc403
    // references frozen before the first production selected-source invocation.
    private const string ModelReference = "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d";
    private const string SceneReference = "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433";
    private static readonly SourceFixture[] SourceFixtures =
    [
        new("one-six-textured", [new(14, 0)], [0, 0, 0, 0, 0, 0], 0xB10,
            "39a2be62e958b399724ef79496129973d62afa8ca1faf0923dbcfaa81094ede3",
            "fbbb180406f460d2acb2258009fe3a5b020d63842a4bc90a352f53c0a56890a4",
            "11435c190e2103134e1eb2e054469eec8aff51d539cc39e97beab5cc89a045b5",
            "14e5b3606f652c250df9cdd6c94de1d393186d1b74f1ab64decd5dd35130a77f"),
        new("one-mixed-base", [new(14, 0)], [0, 255, 0, 255, 0, 255], 0xB10,
            "39a2be62e958b399724ef79496129973d62afa8ca1faf0923dbcfaa81094ede3",
            "178da404c785c309575c2b92affe578f26dfe05c87c6c7a986e50492399b0bd3",
            "90e6886b604301263a03e7247be38a9256845c109259fbdcfe0b78e459a9e6ca",
            "79a3668eb410c763656ff4d9d4e80863b13b0b0572608304db93b2c4f26b833d"),
        new("three-six-textured", [new(14, 0), new(70, 0), new(14, 1)], [0, 1, 2, 0, 1, 2], 0xC80,
            "8a8ca0bed06c7f622d8ad6420e9e65cbb1a3391a1a86942f333921dfc75b4478",
            "398dbf0543d30e5c30a49c9c94ab319fab2900ca96c835d916ddad4a049194bd",
            "a78a1ffcc8870ef91d26170adcd6b735d28466efdd2c24fb7018a293f439ec8c",
            "f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32"),
        new("three-mixed-base", [new(14, 0), new(70, 0), new(14, 1)], [0, 1, 255, 2, 0, 255], 0xC80,
            "8a8ca0bed06c7f622d8ad6420e9e65cbb1a3391a1a86942f333921dfc75b4478",
            "013948deb77d1e1aaa0fa02a16ada97ce9a1991f051ddc891ec52cd927039967",
            "4dd7f151969892774cbdd71adc24d5b544021ce19fe42aabf45101f50a555f8b",
            "329223507fde96b0359c791b2f7f84e009561de016dd021a3c9f4976f99671d5"),
        new("three-reordered", [new(14, 1), new(14, 0), new(70, 0)], [0, 1, 2, 0, 2, 1], 0xC80,
            "404f96e3373726cce320f44c561bf6d829c7d72ecdc24fa2bb1a6754afa8b566",
            "a36b9d4769bbc7115cd37e21318e6485105c722eca2d687474d3323b2d2b0581",
            "ddc62e5d5e2fa5fac28d33f7b706ba0930a7c7972f8ff6bc9bc67868a948a0c8",
            "e36914228311541e8984d7a44a83cdaea335805ff72b8763c5769666018c68b5")
    ];
    private static int _checks;

    internal static async Task Free()
    {
        _checks = 0;
        Check((int)NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles == 0 &&
            (int)NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials == 1 &&
            (int)NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles == 2 &&
            (int)NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles == 3 &&
            (int)NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles == 4,
            "Selected profile is appended without renumbering any old serialized identity.");
        var type = typeof(NativeId65TiledMaterialPlan);
        Check(type.IsSealed && !type.IsVisible && type.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(c => c.IsPrivate) && type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .All(p => p.SetMethod == null), "The selected issuer remains internal, sealed, privately constructed and immutable.");
        foreach (var fixture in new[]
        {
            (Faces: new[] { 0, 0, 0, 0, 0, 0 }, Entries: new[] { 14 }, Slots: new[] { 0, 0, 0, 0, 0, 0 }),
            (Faces: new[] { 0, 1, 2, 0, 1, 2 }, Entries: new[] { 14, 70, 10 }, Slots: new[] { 0, 1, 2, 0, 1, 2 }),
            (Faces: new[] { 2, 255, 1, 2, 0, 255 }, Entries: new[] { 10, 70, 14 }, Slots: new[] { 0, 255, 1, 0, 2, 255 }),
            (Faces: new[] { 255, 255, 255, 255, 0, 0 }, Entries: new[] { 14 }, Slots: new[] { 255, 255, 255, 255, 0, 0 }),
            (Faces: new[] { 0, 1, 0, 1, 0, 2 }, Entries: new[] { 14, 70, 10 }, Slots: new[] { 0, 1, 0, 1, 0, 2 })
        })
        {
            var document = Document(fixture.Faces);
            byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(document);
            var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(document);
            Check(prepared.Canonical.SequenceEqual(canonical) && document.LegacyContent == null &&
                prepared.Surface.CopyBundle().Length == 0x2D4 &&
                Hash(prepared.Surface.CopyBundle()) == "809c2442de920c36fe2bb97db36abd82b2e5888322c95bee51e661c1408ec3d1" &&
                prepared.Surface.Tiles.Select(t => t.X).SequenceEqual(new[] { 8448, 10496, 12544 }),
                "Selected preparation retains the whole actual three-tile geometry and canonical document, not a Base projection.");
            Check(prepared.Selections.Select(s => s.SourceWadEntry).SequenceEqual(fixture.Entries) &&
                prepared.Selections.All(s => s.SourceVariant == Variant && s.TextureId == 0) &&
                prepared.OrderedFaceMaterials.SequenceEqual(fixture.Slots),
                "Every ordered face, including the final nonselected tile and FF, participates in descriptor-first-occurrence slots.");
            Check(prepared.Surface.CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength +
                prepared.Selections.Length * 184 == 0xA58 + fixture.Entries.Length * 184,
                "Every complete selected record contributes184 bytes to the final native used endpoint.");
            prepared.Canonical.AsSpan().Clear(); prepared.OrderedFaceMaterials[0] = 127;
            prepared.Selections[0] = (NativeFreshRetailTerrainMaterialIntent)Material(1);
            var repeat = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(document);
            Check(repeat.Canonical.SequenceEqual(canonical) && repeat.OrderedFaceMaterials.SequenceEqual(fixture.Slots) &&
                repeat.Selections.Select(s => s.SourceWadEntry).SequenceEqual(fixture.Entries),
                "Returned neutral arrays cannot replace immutable document intent or another preparation result.");
            Reject<InvalidDataException>(() => NativeId65TiledSurfaceBundle.PrepareDocument(document),
                "The old Base geometry adapter still refuses retail intent anywhere in the whole document.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureAsync("\0", document),
                "The Base source adapter cannot omit unresolved retail faces before source I/O.");
            if (fixture.Entries.Length != 2)
            {
                Reject<InvalidDataException>(() => NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document),
                    "The existing exact-two preparation adapter is not widened by selected-N support.");
                await RejectAsync<InvalidDataException>(() => NativeId65TiledDocumentCompilation.CaptureMaterialsAsync("\0", document),
                    "The old exact-two compilation adapter refuses N1/N3 before malformed source paths.");
                Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document),
                    "This backend slice does not silently widen current exact-two UI eligibility.");
            }
            await RejectAsync<OperationCanceledException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForSelectedMaterialsAsync("\0", document, new(true)),
                "Canceled selected surface capture cannot reach source I/O.");
            await RejectAsync<OperationCanceledException>(() => NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync("\0", document, new(true)),
                "Canceled selected whole-document compilation cannot reach source I/O.");
        }

        var two = Document([0, 1, 0, 1, 0, 1]);
        var oldTwo = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(two);
        var newTwo = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(two);
        Check(oldTwo.Canonical.SequenceEqual(newTwo.Canonical) && oldTwo.Surface.CopyBundle().SequenceEqual(newTwo.Surface.CopyBundle()) &&
            oldTwo.Selections.SequenceEqual(newTwo.Selections) && oldTwo.OrderedFaceMaterials.SequenceEqual(newTwo.OrderedFaceMaterials),
            "N2 selected preparation is byte- and descriptor-identical to the preserved adapter.");
        var sameEntry = two.WithTiledContent(two.TiledContent!.WithTiles(two.TiledContent.Tiles.Select((t, i) =>
            NativeFreshFlatTileIntent.Create(t.EditorId, t.X, t.Y, t.Z, t.Width, t.Depth, t.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, i),
                    NativeFreshFloorMaterialIntent.RetailTerrain(Variant, 14, i))))));
        var sameEntryPrepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(sameEntry);
        Check(sameEntryPrepared.Selections.Select(s => s.TextureId).SequenceEqual(new[] { 0, 1, 2 }) &&
            sameEntryPrepared.OrderedFaceMaterials.SequenceEqual(new[] { 0, 0, 1, 1, 2, 2 }),
            "Neutral slots use the complete descriptor tuple, not donor entry or equality of future source bytes.");
        var allBase = Document([255, 255, 255, 255, 255, 255]);
        Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(allBase) &&
            NativeId65TiledSurfaceBundle.PrepareDocument(allBase).Surface.CopyBundle().SequenceEqual(newTwo.Surface.CopyBundle()),
            "All-Base remains the existing untextured arm rather than a zero-record selected profile.");

        foreach (var (count, expected) in new[] { (1, 0xF48), (2, 0x1000) })
        {
            var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(BoundaryDocument(58, count));
            Check(prepared.Selections.Length == count && prepared.Surface.CopyBundle().Length +
                NativeId65NormalComponentLayout.NonSurfaceByteLength + count * 184 == expected,
                "The real complete geometry establishes count-sensitive allocation bounds, including exact1000.");
        }
        var legacy = NativeFreshLevelDocumentIntent.Create(NativeId65FreshDocumentIntent.Create(8448, 8448, 8704,
            1024, 1024, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19), []);
        var wizard = NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8704, 13568, 8960);
        var moby = NativeFreshMobyPlacementIntent.Create(new string('b', 32),
            NativeFreshRetailMobyReference.Create(Variant, 10, 0, 0x78), 13568, 8960, 8704, 0);
        foreach (var invalid in new[] { legacy, legacy.UpgradeForNativeMobys(), allBase,
            Document().WithPlacements([wizard]), Document().WithPlacements([moby]),
            BoundaryDocument(58, 3), BoundaryDocument(60, 2) })
        {
            Reject<InvalidDataException>(() => NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(invalid),
                "Unsupported schema, P>0, empty selections or complete data overflow cannot prepare selected material geometry.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForSelectedMaterialsAsync("\0", invalid),
                "Selected geometry checks the whole document before malformed source paths.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync("\0", invalid),
                "Selected compilation does not truncate unsupported rows or bypass pre-I/O allocation checks.");
        }
        Reject<ArgumentNullException>(() => NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(null!), "Null selected document refuses explicitly.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledSurfaceBundle.CaptureGeometryForSelectedMaterialsAsync("\0", null!), "Missing source geometry intent refuses before I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync("\0", null!), "Missing actual compilation document refuses before I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", null!, []), "Neutral records cannot replace actual fresh ancestry.");
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", null!, null!, new(true)), "Cancellation precedes selected-source enumeration and source I/O.");
        Console.WriteLine($"NATIVE_ID65_SELECTED_TILED_MATERIAL_FREE_OK checks={_checks} sourceRead=false nativeOutput=false uiAdmission=false");
    }

    internal static async Task Source(string original)
    {
        await Free();
        foreach (string pin in SourceFixtures.SelectMany(f => new[] { f.Upload, f.Data, f.Body, f.Image }).Concat([ModelReference, SceneReference]))
            Check(pin.Length == 64 && pin.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f'),
                "Independent selected-material oracle pins must be explicitly frozen before any source access.");
        original = Path.GetFullPath(original);
        RequireOriginal(original);
        try
        {
            var baseControl = await NativeId65TiledDocumentCompilation.CaptureAsync(original, Document([255, 255, 255, 255, 255, 255]));
            foreach (var fixture in SourceFixtures)
            {
                var document = SourceDocument(fixture);
                var compiled = await NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync(original, document);
                Validate(compiled, document, fixture, baseControl);
                await ClosedConsumers(compiled);
                VerifyStreams(original, compiled.DiscPlan, fixture.Image);
                if (fixture.Name is "one-six-textured" or "three-six-textured")
                    await ActualAncestry(original, compiled);
                if (fixture.Name == "three-six-textured")
                {
                    foreach (int lba in new[] { 0, 53875 })
                    {
                        using var corrupted = new CandidateStream(original, compiled.DiscPlan, lba);
                        Reject<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(compiled.DiscPlan, corrupted, default),
                            "Selected-profile inverse refuses corruption in both changed and unchanged physical ownership.");
                    }
                }
                Console.WriteLine($"SELECTED_TILED_MATERIAL_REFERENCE name={fixture.Name} upload={fixture.Upload} data={fixture.Data} body={fixture.Body} image={fixture.Image} changedSectors=1253 nativeOutput=false");
            }
            await TwoMaterialParity(original);
        }
        finally { RequireOriginal(original); }
        Console.WriteLine($"NATIVE_ID65_SELECTED_TILED_MATERIAL_SOURCE_OK checks={_checks} nativeOutput=false nativeCpuExecuted=false uiAdmission=false publicationApproved=false");
    }

    private static void Validate(NativeId65TiledDocumentCompilation compiled, NativeFreshLevelDocumentIntent document,
        SourceFixture fixture, NativeId65TiledDocumentCompilation baseControl)
    {
        var material = compiled.TiledMaterialAncestor ?? throw new InvalidDataException("Selected compilation lost its material issuer.");
        var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
        var revision = NativeId65NormalResourceRevision.FromTiledMaterials(material);
        int count = fixture.Sources.Length;
        Check(ReferenceEquals(compiled.Document, document) && ReferenceEquals(material.Document, document) &&
            ReferenceEquals(material.FreshAncestor, fresh) && ReferenceEquals(material.SurfaceAncestor, fresh.TiledSurfaceAncestor) &&
            ReferenceEquals(body.TiledMaterialAncestor, material) && ReferenceEquals(body.TiledSceneAncestor, fresh) &&
            ReferenceEquals(revision.TiledMaterialAncestor, material) && ReferenceEquals(fresh.TiledSurfaceAncestor!.Document, document),
            "Selected document, surface, fresh scene, material, revision and body retain the same actual complete ancestry.");
        Check(material.MaterialSources.Count == count && material.Allocation.Records.Count == count && material.SourceHandles.Count == count &&
            material.MaterialSources.Select(s => (s.SourceWadEntry, s.SourceTextureId)).SequenceEqual(fixture.Sources.Select(s => (s.Entry, s.Texture))) &&
            material.SourceHandles.SequenceEqual(material.MaterialSources.Select(s => s.Handle)) &&
            material.OrderedFaceMaterials.SequenceEqual(fixture.Faces) &&
            material.CopyCanonicalDocument().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(document)) &&
            compiled.CopyCanonicalDocument().SequenceEqual(material.CopyCanonicalDocument()) && compiled.CanonicalSha256 == material.CanonicalDocumentSha256,
            "Every ordered actual captured source, all six bindings and complete canonical intent survive without truncation or content deduplication.");
        material.RequireSelectedAncestry(fresh, document, material.MaterialSources);
        Check(material.ResourceProfile == NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles &&
            body.ResourceProfile == material.ResourceProfile && revision.ResourceProfile == material.ResourceProfile &&
            material.InstanceProfileId != NativeId65TiledMaterialPlan.ProfileId && revision.PlanSha256 == material.PlanSha256 &&
            body.UpstreamPlanSha256 == material.PlanSha256 && material.UpstreamPlanSha256 == fresh.PlanSha256,
            "Actual N1/N3 plans use profile4 and a selected instance identity, not old exact-two profile3 metadata.");
        byte[] upload = material.CopyUploadPayload(), data = material.CopyDataComponents(), model = material.CopyModelPayload(), scene = material.CopyScene();
        Check(Hash(upload) == fixture.Upload && material.UploadSha256 == fixture.Upload && Hash(data) == fixture.Data && material.DataSha256 == fixture.Data &&
            Hash(model) == ModelReference && material.ModelSha256 == ModelReference && Hash(scene) == SceneReference && material.SceneSha256 == SceneReference &&
            model.SequenceEqual(fresh.CopyModelPayload()) && scene.SequenceEqual(fresh.CopyScene()),
            "All four complete output domains match independent frozen hashes; model and scene remain actual unchanged source owners.");
        Check(data.Length == fixture.Used && revision.DataUsedByteLength == fixture.Used && body.DataUsedByteLength == fixture.Used &&
            body.DataSectorByteLength == 0x1000 && body.ModelRuntimeAddress == 0x80089ECCu + (uint)fixture.Used &&
            body.SceneRuntimeAddress == body.ModelRuntimeAddress + 0xF800 && body.KnownSceneEndAddress == body.SceneRuntimeAddress + 0x3000 &&
            material.NativeClip == 0x8000 && material.AdditionalPacketBytesPerPass == 12 * fixture.Faces.Count(f => f != 255),
            "Real N-dependent final used data drives live model/scene addresses and per-emitted-face packet accounting.");
        material.RequireExactOutputs(upload, data, model, scene);
        revision.RequireExactOutputs(upload, data, model, scene);
        DataAndUploadComplements(material, fixture);
        byte[] bodyBytes = compiled.DiscPlan.CopyBody();
        Check(bodyBytes.Length == 0x1FB800 && Hash(bodyBytes) == fixture.Body && body.LevelDataSha256 == fixture.Body &&
            body.Descriptors[0].Offset == 0x800 && body.Descriptors[1].Offset == 0xDE800 &&
            body.Descriptors[2].Offset == 0xDF800 && body.Descriptors[3].Offset == 0xEF000,
            "Selected body uses the unchanged fixed physical allocations and independent complete body golden.");
        Check(Hash(bodyBytes.AsSpan(0, 0x800)) == "cd5658f83da4a953c5a3e66c848a8be606aabde9fea8b32d049c2bcf364c472d" &&
            Hash(compiled.DiscPlan.CopyOverlay()) == "9bc923cc8d27703537b81b01f51fde351e58aabee87d38a8afc630f1031878a5" &&
            Hash(compiled.DiscPlan.CopyExecutable()) == "3473dc92e087c87858434125d5fb1ec879450682e3be64c508de6805029ce7a2" &&
            Hash(compiled.DiscPlan.CopyWadDirectory()) == "0a227cbf0d9f6b269ae7d41ce610129b0b48590519bbaaa2eac375e8818e23e0" &&
            Hash(compiled.DiscPlan.CopyIsoRootDirectory()) == "2412be680f008e5ebbf19e64948e84ad1d5eeadf07ab1c858c825c6608631089",
            "All common physical header/overlay/executable/directory domains match independent oracle403 pins.");
        byte[] complement = baseControl.DiscPlan.CopyBody();
        upload.CopyTo(complement, 0x800); complement.AsSpan(0xDE800, 0x1000).Clear(); data.CopyTo(complement, 0xDE800);
        Check(bodyBytes.SequenceEqual(complement) && compiled.DiscPlan.CopyOverlay().SequenceEqual(baseControl.DiscPlan.CopyOverlay()) &&
            compiled.DiscPlan.CopyExecutable().SequenceEqual(baseControl.DiscPlan.CopyExecutable()) &&
            compiled.DiscPlan.CopyWadDirectory().SequenceEqual(baseControl.DiscPlan.CopyWadDirectory()) &&
            compiled.DiscPlan.CopyIsoRootDirectory().SequenceEqual(baseControl.DiscPlan.CopyIsoRootDirectory()),
            "Every body header/model/scene/auxiliary, overlay, executable and directory complement stays byte exact.");
        int cursor = 0;
        foreach (var owner in body.Owners)
        {
            Check(owner.Offset == cursor && owner.ByteLength > 0 && Hash(bodyBytes.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "Complete body ownership is contiguous, nonempty and byte-hashed.");
            cursor += owner.ByteLength;
        }
        Check(cursor == bodyBytes.Length, "No complete physical body byte lacks an owner.");
        byte[] overlay = body.CopyEngineOverlay(), badBody = bodyBytes.ToArray();
        int paddingStart = body.Descriptors[1].Offset + body.DataUsedByteLength;
        int paddingLength = body.DataSectorByteLength - body.DataUsedByteLength;
        int[] paddingProbes = paddingLength > 0 ? [paddingStart, paddingStart + paddingLength / 2, paddingStart + paddingLength - 1] : [];
        foreach (int at in body.Owners.SelectMany(o => new[] { o.Offset, o.Offset + o.ByteLength - 1 }).Concat(paddingProbes).Distinct())
        {
            badBody[at] ^= 1;
            Reject<InvalidDataException>(() => body.RequireExactPayloads(badBody, overlay), "Selected profile rejects body-owner boundaries and data-sector padding mutations.");
            badBody[at] ^= 1;
        }
        // Full hashes/equality and complement scans above cover every byte.
        // Mutations target different structural owners instead of repeating
        // the same rejection for every byte of five complete data afterimages.
        int growth = fixture.Sources.Length * 184;
        var dataProbes = new HashSet<int> { 0, 3, 4, 7, 8 + growth - 1, 8 + growth, data.Length - 1 };
        for (int record = 0; record < fixture.Sources.Length; record++)
        {
            int start = 8 + record * 184;
            foreach (int relative in new[] { 0, 23, 24, 183 }) dataProbes.Add(start + relative);
        }
        foreach (int face in new[] { 0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0 })
            foreach (int relative in Enumerable.Range(0, 8)) dataProbes.Add(face + growth + relative);
        var priorLayout = NativeId65TiledNormalDataLayout.DecodeUntextured(fresh.CopyDataComponents()).Layout;
        foreach (var component in priorLayout.Components) AddSpan(component.Offset, component.ByteLength);
        AddSpan(priorLayout.Portal.Offset, priorLayout.Portal.ByteLength);
        AddSpan(priorLayout.Particles.Offset, priorLayout.Particles.ByteLength);
        AddSpan(priorLayout.Sound.Offset, priorLayout.Sound.ByteLength);
        AddSpan(priorLayout.CollisionTreeOffset, priorLayout.CollisionTreeByteLength);
        byte[] badData = data.ToArray();
        foreach (int at in dataProbes.Order())
        {
            badData[at] ^= 1;
            Reject<InvalidDataException>(() => material.RequireExactOutputs(upload, badData, model, scene),
                "Selected metadata/record boundaries, all face words/flags and complete component/tail endpoints reject mutation.");
            badData[at] ^= 1;
        }
        Reject<InvalidDataException>(() => material.RequireExactOutputs(upload, data[..^1], model, scene), "Truncated selected data is not the complete issued owner.");
        Reject<InvalidDataException>(() => material.RequireExactOutputs(upload, data.Concat(new byte[1]).ToArray(), model, scene), "Extra selected data cannot extend the issued owner.");
        upload.AsSpan().Clear(); data.AsSpan().Clear(); model.AsSpan().Clear(); scene.AsSpan().Clear(); bodyBytes.AsSpan().Clear();
        Check(material.UploadSha256 == Hash(material.CopyUploadPayload()) && material.DataSha256 == Hash(material.CopyDataComponents()) &&
            material.ModelSha256 == Hash(material.CopyModelPayload()) && material.SceneSha256 == Hash(material.CopyScene()) && Hash(body.CopyLevelData()) == fixture.Body,
            "Returned complete resource and body copies cannot mutate retained issuers.");
        Check(material.SourceCertified && !material.Allocation.SourceCertified && !material.RuntimeVerified && !material.PublicationApproved &&
            !body.RuntimeVerified && !body.PublicationApproved && !compiled.DiscPlan.RuntimeVerified && !compiled.DiscPlan.PublicationApproved &&
            !compiled.DiscPlan.NormalCreateBinEnabled, "Selected native bytes do not promote neutral allocation, UI, publication or runtime approval.");

        void AddSpan(int offset, int length)
        {
            if (length <= 0) return;
            dataProbes.Add(offset + growth);
            dataProbes.Add(offset + growth + length - 1);
        }
    }

    private static void DataAndUploadComplements(NativeId65TiledMaterialPlan material, SourceFixture fixture)
    {
        byte[] before = material.FreshAncestor.CopyDataComponents(), after = material.CopyDataComponents();
        int growth = fixture.Sources.Length * 184, metadata = 8 + growth;
        int[] faceOffsets = [0xA0, 0xB0, 0x140, 0x150, 0x1E0, 0x1F0];
        Check(before.Length == 0xA58 && Word(after, 0) == metadata && Word(after, 4) == fixture.Sources.Length,
            "Complete metadata header contains exactly N full records and the preserved whole three-tile prior chain.");
        var decoded = NativeTerrainMaterialTableCodec.Decode(after.AsSpan(0, metadata));
        string[] recordPins = fixture.Sources.Length == 1
            ? ["9cb7f5685d0c696873c9024b0f189650ff81db8a421f667d7115cbaf2719d316"]
            : fixture.Name == "three-reordered"
                ? ["aa236de2e80514a9dc834deb5b73ce3d261c97e15f4216cdf81aa23d57ffd25f",
                    "f36fb64fb37da0ddafad2058e0b9acb20a806606aef221faa04a65079857b97d",
                    "28472fcd6740a1f280a329bd638b3ca6d7264db63038e12149298dfdae65c8a5"]
                : ["3a4325e57bff4b9f2782e470c0c912dd26312fdc7d83ba0fdf2b22adcf131857",
                    "0bfb871defa6040da03b5affac130b31a8803beb7cef245b3792e34b5e480f5d",
                    "3709f7c62173d4ad128c5203563d03c5fb772f20bf6a0ba0f871206c036f298a"];
        Check(decoded.Select(r => r.RecordSha256).SequenceEqual(recordPins) &&
            material.Allocation.Records.Select(r => r.RecordSha256).SequenceEqual(recordPins),
            "All LQ/HQ bytes of every ordered relocated record survive metadata serialization.");
        for (int face = 0; face < faceOffsets.Length; face++)
            Check(Word(before, faceOffsets[face]) == 255 && Word(after, faceOffsets[face] + growth) == fixture.Faces[face] &&
                Word(after, faceOffsets[face] + growth + 4) == 0x80, "Independent fixture offsets bind all six material words and retain direct-LQ flags.");
        var writable = faceOffsets.SelectMany(o => Enumerable.Range(o, 4)).ToHashSet();
        Check(Enumerable.Range(8, before.Length - 8).Where(i => !writable.Contains(i)).All(i => before[i] == after[i + growth]),
            "Every unowned translated environment/collision/visibility/retained-tail byte is unchanged.");
        byte[] restored = new byte[before.Length]; before.AsSpan(0, 8).CopyTo(restored); after.AsSpan(metadata).CopyTo(restored.AsSpan(8));
        foreach (int at in faceOffsets) BinaryPrimitives.WriteInt32LittleEndian(restored.AsSpan(at), 255);
        Check(restored.SequenceEqual(before), "Independent complete data inverse restores every prior byte.");
        byte[] oldUpload = material.FreshAncestor.CopyUploadPayload(), upload = material.CopyUploadPayload();
        var owned = new bool[upload.Length]; var sourceVram = material.MaterialSources.Select(s => s.CopySourceVram()).ToArray();
        foreach (var region in material.Allocation.Allocations)
        {
            Check(region.MaterialId >= 0 && region.MaterialId < fixture.Sources.Length &&
                region.SourceIdentity == material.MaterialSources[region.MaterialId].Handle,
                "Each independent allocation names its actual captured material slot, never merely equal content.");
            for (int row = 0; row < region.RowCount; row++)
            {
                int target = region.TargetOffset + row * 1024, source = region.SourceOffset + row * 1024;
                Check(target >= 0 && target + region.RowByteLength <= 0x80000 && !owned.AsSpan(target, region.RowByteLength).Contains(true) &&
                    upload.AsSpan(target, region.RowByteLength).SequenceEqual(sourceVram[region.MaterialId].AsSpan(source, region.RowByteLength)),
                    "Each complete source row has disjoint GPU ownership and cannot enter protected SPU storage.");
                owned.AsSpan(target, region.RowByteLength).Fill(true);
            }
        }
        Check(owned.Count(x => x) == material.Allocation.AllocatedByteCount &&
            material.Allocation.AllocatedByteCount == (fixture.Sources.Length == 1 ? 22528 : 50176) &&
            material.Allocation.FreeByteCount == (fixture.Sources.Length == 1 ? 396640 : 368992) &&
            Enumerable.Range(0, upload.Length).Where(i => !owned[i]).All(i => oldUpload[i] == upload[i]) &&
            oldUpload.AsSpan(0x80000).SequenceEqual(upload.AsSpan(0x80000)),
            "Complete protected/unallocated/zero-valued/SPU complements survive selected allocation.");
        foreach (var owner in material.FreshAncestor.UploadOwners.Where(o => o.Provenance != "fresh-zero-upload-background"))
            Check(!owned.AsSpan(owner.Offset, owner.ByteLength).Contains(true), "Selected allocation never borrows a declared nonfree owner.");
    }

    private static async Task ActualAncestry(string original, NativeId65TiledDocumentCompilation compiled)
    {
        var material = compiled.TiledMaterialAncestor!; var fresh = material.FreshAncestor; var document = material.Document;
        var sources = material.MaterialSources.ToArray();
        var equalSources = sources.Select(s => NativeStaticTerrainTextureSource.Capture(original, s.SourceWadEntry, s.SourceTextureId)).ToArray();
        for (int index = 0; index < sources.Length; index++)
        {
            Check(!ReferenceEquals(sources[index], equalSources[index]) && sources[index].Handle == equalSources[index].Handle &&
                sources[index].Layout.RecordSha256 == equalSources[index].Layout.RecordSha256, "Foreign source fixture has an equal certified handle and complete record hash.");
            var substituted = sources.ToArray(); substituted[index] = equalSources[index];
            Reject<InvalidDataException>(() => material.RequireSelectedAncestry(fresh, document, substituted),
                "Equal-hash actual source substitution is rejected at every index, including trailing material slots.");
        }
        var wrongSequences = new List<NativeStaticTerrainTextureSource[]> { sources[..^1], sources.Append(sources[0]).ToArray() };
        if (sources.Length > 1) { wrongSequences.Add([]); wrongSequences.Add(sources.Reverse().ToArray()); }
        foreach (var wrong in wrongSequences)
        {
            Reject<InvalidDataException>(() => material.RequireSelectedAncestry(fresh, document, wrong), "Whole source ancestry requires exact ordered count and every actual instance.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, wrong), "Wrong selected count/order refuses before new source I/O.");
        }
        var equalDocument = NativeFreshLevelDocumentCodec.Parse(material.CopyCanonicalDocument());
        Reject<InvalidDataException>(() => material.RequireSelectedAncestry(fresh, equalDocument, sources), "Equal canonical content is not the actual document ancestor.");
        var equalFresh = await NativeId65FreshSceneBundle.CaptureTiledAsync(original, fresh.ResourceChainAncestor);
        Check(equalFresh.PlanSha256 == fresh.PlanSha256 && !ReferenceEquals(equalFresh, fresh), "Foreign fresh fixture has the same complete plan hash.");
        Reject<InvalidDataException>(() => material.RequireSelectedAncestry(equalFresh, document, sources), "Equal fresh hashes cannot replace actual source ancestry.");
        Reject<InvalidDataException>(() => material.RequireAncestry(fresh, document, sources[0], sources[^1]), "Old pair ancestry cannot replace the actual N1/N3 source list.");
        await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureAsync("\0", fresh, sources[0], sources[^1]), "Old pair issuer refuses complete N1/N3 intent before I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, null!), "Null source sequence is refused explicitly.");
        var nullProbe = new HostileSources([null!]);
        await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, nullProbe), "Null yielded source is refused before I/O.");
        Check(nullProbe.Disposed && nullProbe.Yielded == 1, "Null source capture disposes its iterator.");
        var excessive = new HostileSources(TooMany(sources[0]));
        await RejectAsync<InvalidDataException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, excessive), "Source capture has a hard128-reference limit before I/O.");
        Check(excessive.Disposed && excessive.Yielded == 129 && excessive.CountReads == 0, "The129th actual yield stops capture independently of hostile Count.");
        var ancestryExcess = new HostileSources(TooMany(sources[0]));
        Reject<InvalidDataException>(() => material.RequireSelectedAncestry(fresh, document, ancestryExcess), "Ancestry comparison cannot accept extra trailing sources or unbounded iteration.");
        Check(ancestryExcess.Disposed && ancestryExcess.Yielded <= 129 && ancestryExcess.CountReads == 0, "Ancestry comparison is independently bounded and disposes excess input.");
        var throwing = new HostileSources(ThrowAfterOne(sources[0]));
        await RejectAsync<IteratorFailure>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, throwing), "Source iterator failure cannot issue a partial plan.");
        Check(throwing.Disposed, "Exceptional source enumeration is disposed.");
        using (var cancellation = new CancellationTokenSource())
        {
            var canceled = new HostileSources(CancelDuringEnumeration(sources[0], cancellation));
            await RejectAsync<OperationCanceledException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, canceled, cancellation.Token),
                "Cancellation at a yield is observed before requesting the following hostile MoveNext.");
            Check(canceled.Disposed && canceled.Yielded == 2, "Yield-boundary cancellation disposes the iterator before its throwing sentinel.");
        }
        using (var cancellation = new CancellationTokenSource())
        {
            var canceled = new HostileSources(CancelOnDispose(sources, cancellation));
            await RejectAsync<OperationCanceledException>(() => NativeId65TiledMaterialPlan.CaptureSelectedAsync("\0", fresh, canceled, cancellation.Token),
                "Cancellation from final enumeration disposal is observed before source I/O.");
            Check(canceled.Disposed && canceled.Yielded == sources.Length, "Post-enumeration cancellation control exercised the complete iterator.");
        }
        var mutable = sources.ToArray();
        var probe = new HostileSources(MutateAfterYield());
        var captured = await NativeId65TiledMaterialPlan.CaptureSelectedAsync(original, fresh, probe);
        Check(probe.Disposed && probe.CountReads == 0 && probe.Yielded == sources.Length &&
            captured.MaterialSources.Select((s, i) => ReferenceEquals(s, sources[i])).All(x => x) &&
            mutable.Select((s, i) => ReferenceEquals(s, equalSources[i])).All(x => x) && captured.PlanSha256 == material.PlanSha256,
            "Each yielded actual immutable source reference is retained before later collection mutation, without trusting Count/indexer/CopyTo.");
        var ancestryProbe = new HostileSources(sources);
        captured.RequireSelectedAncestry(fresh, document, ancestryProbe);
        Check(ancestryProbe.Disposed && ancestryProbe.CountReads == 0, "Successful whole-source identity comparison uses bounded enumeration, not caller collection metadata.");
        if (captured.MaterialSources is IList<NativeStaticTerrainTextureSource> sourceList)
            Reject<NotSupportedException>(() => sourceList[^1] = equalSources[^1], "The captured ordered source list cannot be replaced through a mutable collection interface.");
        var staging = await NativeId65NormalUploadStaging.CaptureTiledMaterialsAsync(original, material);
        Check(ReferenceEquals(staging.TiledMaterialAncestor, material) && staging.ResourceProfile == material.ResourceProfile, "Selected staging retains the actual material issuer.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync("\0", captured, staging),
            "Equal-hash plans with the SAME fresh and source objects cannot borrow each other's staging before I/O.");
        Reject<InvalidDataException>(() => NativeId65NormalBodySource.RequirePair(NativeId65NormalResourceRevision.FromTiledMaterials(captured), staging),
            "Shared selected-profile pair guard checks the actual material instance, not only named factories.");
        var body = await NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync(original, material, staging);
        Check(body.PlanSha256 == compiled.DiscPlan.BodyAncestor.PlanSha256, "Correct actual pairing remains deterministic.");
        Reject<InvalidDataException>(() => compiled.DiscPlan.RequireAncestry(body), "Equal-byte separately issued bodies cannot replace the retained disc ancestor.");

        IEnumerable<NativeStaticTerrainTextureSource> MutateAfterYield()
        {
            for (int i = 0; i < mutable.Length; i++) { yield return mutable[i]; mutable[i] = equalSources[i]; }
        }
    }

    private static async Task TwoMaterialParity(string original)
    {
        var document = Document([0, 1, 0, 1, 0, 1]);
        var legacy = await NativeId65TiledDocumentCompilation.CaptureMaterialsAsync(original, document);
        var selected = await NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync(original, document);
        var oldMaterial = legacy.TiledMaterialAncestor!; var newMaterial = selected.TiledMaterialAncestor!;
        Check(newMaterial.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles &&
            newMaterial.InstanceProfileId == NativeId65TiledMaterialPlan.ProfileId && oldMaterial.PlanSha256 == newMaterial.PlanSha256 &&
            legacy.DiscPlan.PlanSha256 == selected.DiscPlan.PlanSha256 && legacy.DiscPlan.BodyAncestor.PlanSha256 == selected.DiscPlan.BodyAncestor.PlanSha256 &&
            legacy.DiscPlan.BodyAncestor.StagingPlanSha256 == selected.DiscPlan.BodyAncestor.StagingPlanSha256 &&
            legacy.DiscPlan.CopyBody().SequenceEqual(selected.DiscPlan.CopyBody()) &&
            Hash(selected.DiscPlan.CopyBody()) == "461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be" &&
            newMaterial.UploadSha256 == "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333" &&
            newMaterial.DataSha256 == "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa",
            "N2 selected compilation preserves old source/staging/body/disc plan serialization and complete frozen outputs exactly.");
        var sameAncestors = await NativeId65TiledMaterialPlan.CaptureSelectedAsync(original, oldMaterial.FreshAncestor, oldMaterial.MaterialSources);
        Check(sameAncestors.PlanSha256 == oldMaterial.PlanSha256 && sameAncestors.InstanceProfileId == NativeId65TiledMaterialPlan.ProfileId,
            "Selected N2 issuer is identical to the old pair adapter on the same actual ancestry.");
        sameAncestors.RequireAncestry(oldMaterial.FreshAncestor, document, oldMaterial.MaterialSources[0], oldMaterial.MaterialSources[1]);
        VerifyStreams(original, selected.DiscPlan, "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8");
    }

    private static async Task ClosedConsumers(NativeId65TiledDocumentCompilation compiled)
    {
        var material = compiled.TiledMaterialAncestor!; var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
        Reject<InvalidDataException>(() => NativeId65NormalResourceRevision.FromTiled(fresh), "Unresolved retail geometry is never an old Base-only revision.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalUploadStaging.CaptureTiledAsync("\0", fresh), "Old Base staging refuses before source I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureAsync("\0", body, 19), "Old untextured disc refuses selected profile4 before source I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTexturedAsync("\0", body, 19), "Legacy two-face texture disc does not accept selected tiled bytes.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTiledAsync("\0", body), "Old Base-only tiled disc cannot erase selected materials.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscResearchCandidatePlan.CaptureAsync("\0", "\0", compiled.DiscPlan), "Fixed starter candidate cannot adopt the selected profile.");
        Reject<InvalidDataException>(() => material.RequireAncestry(fresh, compiled.Document, material.MaterialSources[0], material.MaterialSources[^1]),
            "Old exact-pair ancestry remains closed for both N1 and N3 plans.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(compiled.Document), "Backend source compilation alone does not widen current UI admission.");
    }

    private static void VerifyStreams(string original, NativeId65FreshDiscPlan disc, string expected)
    {
        using var source = File.OpenRead(original);
        using var candidate = new CandidateStream(original, disc);
        var forward = disc.VerifyImage(source, candidate);
        Check(forward.OutputSha256 == expected && forward.ChangedSectors == 1253 && source.Position == OriginalLength && candidate.Position == OriginalLength,
            "Shared full raw forward verification matches the independent image golden through actual EOF.");
        candidate.Position = 0;
        var inverse = NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default);
        Check(inverse.OutputSha256 == expected && inverse.ChangedSectors == 1253 && candidate.Position == OriginalLength && candidate.ReadByte() == -1,
            "Shared full raw inverse restores the exact original image and authenticates changed and unchanged sectors through EOF.");
    }

    private static IEnumerable<NativeStaticTerrainTextureSource> TooMany(NativeStaticTerrainTextureSource source)
    { for (int i = 0; i < 129; i++) yield return source; throw new IteratorFailure(); }
    private static IEnumerable<NativeStaticTerrainTextureSource> ThrowAfterOne(NativeStaticTerrainTextureSource source)
    { yield return source; throw new IteratorFailure(); }
    private static IEnumerable<NativeStaticTerrainTextureSource> CancelOnDispose(IEnumerable<NativeStaticTerrainTextureSource> sources, CancellationTokenSource cancellation)
    { try { foreach (var source in sources) yield return source; } finally { cancellation.Cancel(); } }
    private static IEnumerable<NativeStaticTerrainTextureSource> CancelDuringEnumeration(NativeStaticTerrainTextureSource source, CancellationTokenSource cancellation)
    { yield return source; cancellation.Cancel(); yield return source; throw new IteratorFailure(); }
    private sealed class IteratorFailure : Exception { }
    private sealed class HostileSources(IEnumerable<NativeStaticTerrainTextureSource> sources) : IList<NativeStaticTerrainTextureSource>
    {
        internal int CountReads, Yielded;
        internal bool Disposed;
        public int Count { get { CountReads++; return int.MaxValue; } }
        public bool IsReadOnly => true;
        public NativeStaticTerrainTextureSource this[int index] { get => throw new IteratorFailure(); set => throw new IteratorFailure(); }
        public IEnumerator<NativeStaticTerrainTextureSource> GetEnumerator()
        { try { foreach (var source in sources) { Yielded++; yield return source; } } finally { Disposed = true; } }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public void CopyTo(NativeStaticTerrainTextureSource[] array, int index) => throw new IteratorFailure();
        public void Add(NativeStaticTerrainTextureSource value) => throw new IteratorFailure();
        public void Clear() => throw new IteratorFailure();
        public bool Contains(NativeStaticTerrainTextureSource value) => throw new IteratorFailure();
        public int IndexOf(NativeStaticTerrainTextureSource value) => throw new IteratorFailure();
        public void Insert(int index, NativeStaticTerrainTextureSource value) => throw new IteratorFailure();
        public bool Remove(NativeStaticTerrainTextureSource value) => throw new IteratorFailure();
        public void RemoveAt(int index) => throw new IteratorFailure();
    }

    // Read-only generated candidate; expected hashes above are independently
    // frozen and never learned from this stream. No output image is written.
    private sealed class CandidateStream(string original, NativeId65FreshDiscPlan disc, int corruptLba = -1) : Stream
    {
        private readonly FileStream _source = File.OpenRead(original);
        private byte[] _sector = [];
        private int _lba = -1;
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => OriginalLength;
        public override long Position { get => _position; set { if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value)); _position = value; } }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            while (!buffer.IsEmpty && _position < Length)
            {
                int lba = (int)(_position / 2352), within = (int)(_position % 2352);
                if (lba != _lba)
                {
                    byte[] before = new byte[2352]; _source.Position = lba * 2352L; _source.ReadExactly(before);
                    _sector = disc.CreateExpectedRawSector(lba, before); if (lba == corruptLba) _sector[24] ^= 1; _lba = lba;
                }
                int take = Math.Min(buffer.Length, 2352 - within); _sector.AsSpan(within, take).CopyTo(buffer);
                buffer = buffer[take..]; total += take; _position += take;
            }
            return total;
        }
        public override long Seek(long offset, SeekOrigin origin)
        { Position = origin switch { SeekOrigin.Begin => offset, SeekOrigin.Current => Position + offset, SeekOrigin.End => Length + offset, _ => throw new ArgumentOutOfRangeException(nameof(origin)) }; return Position; }
        protected override void Dispose(bool disposing) { if (disposing) _source.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private static NativeFreshLevelDocumentIntent SourceDocument(SourceFixture fixture) => Make(Enumerable.Range(0, 3).Select(i =>
        NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"), 8448 + i * 2048, 8448, 8704, 2048, 2048, new(80, 128, 112),
            NativeFreshFloorMaterialsIntent.Create(Face(fixture, fixture.Faces[2 * i]), Face(fixture, fixture.Faces[2 * i + 1])))), 8960, 8960, 9216);

    private static NativeFreshFloorMaterialIntent Face(SourceFixture fixture, int slot) => slot == 255 ? NativeFreshFloorMaterialIntent.BaseFloorColor() :
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, fixture.Sources[slot].Entry, fixture.Sources[slot].Texture);
    private sealed record SourceSelection(int Entry, int Texture);
    private sealed record SourceFixture(string Name, SourceSelection[] Sources, int[] Faces, int Used, string Upload, string Data, string Body, string Image);
    private static int Word(ReadOnlySpan<byte> bytes, int at) => BinaryPrimitives.ReadInt32LittleEndian(bytes[at..]);
    private static void RequireOriginal(string original)
    {
        using var stream = File.OpenRead(original);
        Check(stream.Length == OriginalLength && Convert.ToHexStringLower(SHA256.HashData(stream)) == OriginalHash &&
            stream.Position == OriginalLength && stream.ReadByte() == -1 && stream.Length == OriginalLength,
            "Named original retains its exact full SHA, length and EOF before and after source operations.");
    }

    private static NativeFreshLevelDocumentIntent Document(int[]? faces = null)
    {
        faces ??= [0, 1, 2, 0, 1, 2];
        if (faces.Length != 6) throw new ArgumentException("Every fixture face is explicit.", nameof(faces));
        return Make(Enumerable.Range(0, 3).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"),
            8448 + i * 2048, 8448, 8704, 2048, 2048, new(80, 128, 112),
            NativeFreshFloorMaterialsIntent.Create(Material(faces[2 * i]), Material(faces[2 * i + 1])))), 8960, 8960, 9216);
    }
    private static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell, int count) => Make(
        Enumerable.Range(0, 8).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"), i * 16, 0, zCell * 4096,
            16, 16, new(80, 128, 112), NativeFreshFloorMaterialsIntent.Create(Material((2 * i) % count), Material((2 * i + 1) % count)))),
        8, 8, zCell * 4096 + 512);
    private static NativeFreshFloorMaterialIntent Material(int slot) => slot == 255 ? NativeFreshFloorMaterialIntent.BaseFloorColor() :
        NativeFreshFloorMaterialIntent.RetailTerrain(Variant, slot switch { 0 => 14, 1 => 70, 2 => 10, _ => throw new ArgumentOutOfRangeException(nameof(slot)) }, 0);
    private static NativeFreshLevelDocumentIntent Make(IEnumerable<NativeFreshFlatTileIntent> tiles, int x, int y, int z) =>
        NativeFreshLevelDocumentIntent.CreateTiled(NativeFreshTiledLevelContent.Create(tiles, x, y, z, 64, new(128, 96, 64), 19), []);
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Check(bool condition, string message) { if (!condition) throw new InvalidDataException(message); _checks++; }
    private static void Reject<T>(Action action, string message) where T : Exception
    {
        try { action(); } catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected refusal: " + message);
    }
    private static async Task RejectAsync<T>(Func<Task> action, string message) where T : Exception
    {
        try { await action(); } catch (T) { _checks++; return; }
        throw new InvalidDataException("Expected refusal: " + message);
    }
}
