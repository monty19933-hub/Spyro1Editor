using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;

// Actual tiled-material source compilation only. Candidate publication, UI
// readiness, native runtime and gameplay remain separate closed gates.
internal static class TiledMaterialCompilationCases
{
    private const string Variant = "spyro1-usa-retail-mode2-2352-v1";
    private const long OriginalLength = 661547040;
    private const string OriginalHash = "fc866b2a02e010a6658f8af2de28bb3001eb33513e5924af014e35643c6dee37";
    private const string UploadReference = "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333";
    // Root's independent physical oracle352 froze all three full Body/image
    // references before the new production path existed. Data refs are341.
    private static readonly Fixture[] Fixtures =
    [
        new("six-textured", [0, 1, 0, 1, 0, 1],
            "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa",
            "461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be",
            "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8"),
        new("mixed-base", [0, 1, 255, 1, 0, 255],
            "c0ac16f6e8e063dece5e9386649452f3d507693db656f996099350adb492bfa2",
            "94c11a4de02529ccac39f09207f4c23d294a55652194966a8a4cb9499c73df49",
            "af46a6a0861af60a92669128512a94b6fca115ab12da699e5c5e12e5354beb22"),
        new("changed-third-tile", [0, 1, 0, 1, 1, 0],
            "03d9dc3a28685abe159fd0efb650e40bcd47dd992e8eaff31a70d05f20b1f853",
            "675a29ec5f0876feb32b03a47101ee62ab0c5b656b2e724107c8f18517321898",
            "bb1c2263afe49a9a603c71981f88e1c750e982bc650156c81bb42ba550ec7578")
    ];
    private static int _checks;

    internal static async Task Free()
    {
        _checks = 0;
        Check((int)NativeId65NormalResourceProfile.UntexturedGouraudTwoTriangles == 0 &&
            (int)NativeId65NormalResourceProfile.DirectLqTwoStaticMaterials == 1 &&
            (int)NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles == 2 &&
            (int)NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles == 3,
            "New material-tile profile is appended; existing serialized enum identities are unchanged.");
        foreach (Type type in new[] { typeof(NativeId65NormalResourceRevision), typeof(NativeId65NormalUploadStaging),
            typeof(NativeId65NormalBodyPlan), typeof(NativeId65TiledDocumentCompilation) })
        {
            var ancestor = type.GetProperty("TiledMaterialAncestor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            Check(ancestor != null && ancestor.PropertyType == typeof(NativeId65TiledMaterialPlan) && ancestor.SetMethod == null &&
                ancestor.GetMethod?.IsAssembly == true,
                "Closed consumer retains its actual internal immutable material-plan ancestor, not caller metadata.");
        }
        var bodyMethods = typeof(NativeId65NormalBodyLayout).GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(m => m.Name == "CompileTiledMaterial").ToArray();
        Check(bodyMethods.Length == 1 && bodyMethods[0].GetParameters().Select(p => p.ParameterType)
            .SequenceEqual(new[] { typeof(NativeId65TiledMaterialPlan), typeof(IReadOnlyList<byte[]>) }),
            "Material body packing consumes an actual typed issuer and auxiliary owners; no arbitrary byte/length overload is added.");
        Reject<ArgumentNullException>(() => NativeId65NormalResourceRevision.FromTiledMaterials(null!),
            "No metadata can replace the actual source-issued material revision.");
        Reject<ArgumentNullException>(() => NativeId65NormalBodyLayout.CompileTiledMaterial(null!, Array.Empty<byte[]>()),
            "Typed material body packing refuses a missing actual material issuer.");
        await RejectAsync<ArgumentNullException>(() => NativeId65NormalUploadStaging.CaptureTiledMaterialsAsync("\0", null!),
            "Material staging refuses a missing issuer before source I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync("\0", null!, null!),
            "Material body capture refuses missing source ancestors before I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65FreshDiscPlan.CaptureTiledMaterialsAsync("\0", null!),
            "Material disc capture refuses a missing actual body before source I/O.");
        await RejectAsync<ArgumentNullException>(() => NativeId65TiledDocumentCompilation.CaptureMaterialsAsync("\0", null!),
            "Whole material compilation refuses a missing document before source I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65TiledDocumentCompilation.CaptureMaterialsAsync("\0", Document(), new(true)),
            "Canceled valid whole-document compilation stops before malformed source path I/O.");

        var legacy = NativeFreshLevelDocumentIntent.Create(NativeId65FreshDocumentIntent.Create(8448, 8448, 8704,
            1024, 1024, 8960, 8960, 64, new(80, 128, 112), new(128, 96, 64), 19), []);
        var wizard = NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8960, 13568, 8960);
        var moby = NativeFreshMobyPlacementIntent.Create(new string('b', 32),
            NativeFreshRetailMobyReference.Create(Variant, 10, 0, 0x78), 13568, 8960, 8960, 0);
        foreach (var unsupported in new[] { legacy, legacy.WithFloorMaterials(Pair(0, 1)), legacy.UpgradeForNativeMobys(),
            Document([255, 255, 255, 255, 255, 255]), Document([0, 255, 0, 255, 0, 255]),
            Document([0, 1, 0, 1, 0, 2]), Document().WithPlacements([wizard]), Document().WithPlacements([moby]), BoundaryDocument(60) })
            await RejectAsync<InvalidDataException>(() => NativeId65TiledDocumentCompilation.CaptureMaterialsAsync("\0", unsupported),
                "Whole unsupported schema/material-count/placement/allocation intent refuses before any source I/O or projection.");
        foreach (var document in new[] { Document(), Document([0, 1, 255, 1, 0, 255]), Document([0, 1, 0, 1, 1, 0]) })
        {
            var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(document);
            Check(prepared.Surface.CopyBundle().Length == 0x2D4 &&
                prepared.Surface.CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength + 2 * 184 == 0xBC8,
                "Every exact three-tile material fixture derives BC8 used data without changing the two-sector allocation.");
            await RejectAsync<InvalidDataException>(() => NativeId65TiledDocumentCompilation.CaptureAsync("\0", document),
                "Existing Base-only compilation does not acquire textured authority through a named new factory.");
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document),
                "Source compilation does not broaden saved-research UI material eligibility.");
        }
        foreach (var (zCell, expected) in new[] { (56, 0xFFC), (58, 0x1000) })
        {
            var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForMaterials(BoundaryDocument(zCell));
            Check(prepared.Surface.CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength + 2 * 184 == expected,
                "Complete measured metadata and collision tree establish below/full allocation fixtures independently of tile count.");
        }
        Console.WriteLine($"NATIVE_ID65_TILED_MATERIAL_COMPILATION_FREE_OK checks={_checks} sourceRead=false nativeCpuExecuted=false");
    }

    internal static async Task Source(string original)
    {
        await Free();
        original = Path.GetFullPath(original);
        RequireOriginal(original);
        try
        {
            // This separate Base-only control has its own actual document. It
            // never replaces the material document in an issued consumer chain.
            var baseDocument = Document([255, 255, 255, 255, 255, 255]);
            var baseControl = await NativeId65TiledDocumentCompilation.CaptureAsync(original, baseDocument);
            Check(baseControl.TiledMaterialAncestor == null && Hash(baseControl.DiscPlan.CopyBody()) ==
                "37157a7248c90665309d8cee21c739a846aca284426cd602c65d627788c06009",
                "Existing Base-only compilation and its independent full-body reference remain unchanged.");
            NativeId65TiledDocumentCompilation? baseline = null;
            foreach (var fixture in Fixtures)
            {
                var document = Document(fixture.Faces);
                var compiled = await NativeId65TiledDocumentCompilation.CaptureMaterialsAsync(original, document);
                ValidateCompilation(compiled, document, fixture, baseControl);
                await ClosedConsumers(compiled);
                VerifyPhysicalStreams(original, compiled.DiscPlan, fixture.ImageSha256);
                if (baseline == null)
                {
                    baseline = compiled;
                    await Pairing(original, compiled);
                    VerifyCorruptionRefusal(original, compiled.DiscPlan);
                }
                else
                {
                    Check(compiled.CanonicalSha256 != baseline.CanonicalSha256 &&
                        compiled.DiscPlan.PlanSha256 != baseline.DiscPlan.PlanSha256 &&
                        compiled.TiledMaterialAncestor!.UploadSha256 == baseline.TiledMaterialAncestor!.UploadSha256,
                        "Remote face edits retain shared upload bytes but change actual canonical/material/body/disc ownership.");
                }
                Console.WriteLine($"TILED_MATERIAL_COMPILATION_REFERENCE name={fixture.Name} body={fixture.BodySha256} image={fixture.ImageSha256} changedSectors=1253 physicalOutputWritten=false");
            }

            var full = await NativeId65TiledDocumentCompilation.CaptureMaterialsAsync(original, BoundaryDocument(58));
            var fullBody = full.DiscPlan.BodyAncestor;
            Check(fullBody.DataUsedByteLength == 0x1000 && fullBody.DataSectorByteLength == 0x1000 &&
                fullBody.ModelRuntimeAddress == 0x8008AECC && fullBody.SceneRuntimeAddress == 0x8009A6CC &&
                fullBody.KnownSceneEndAddress == 0x8009D6CC &&
                fullBody.Owners.All(o => o.ByteLength > 0) && fullBody.PhaseOverlaps.All(o => o.ByteLength > 0) &&
                fullBody.Owners.All(o => o.Id != "fresh-data-sector-padding") &&
                fullBody.PhaseOverlaps.All(o => o.EarlierOwner != "data-sector-padding"),
                "Exact1000 material data reaches the derived live addresses without fictional zero-length padding ownership or overlap.");
            OwnersAndCopies(fullBody);

            // Numeric legacy-size equivalence is deliberately insufficient for
            // source profile admission. Do not falsely reject neutral packing.
            var oneDocument = Make([NativeFreshFlatTileIntent.Create(1.ToString("x32"), 8448, 8448, 8704,
                1024, 1024, new(80, 128, 112), Pair(0, 1))], 8960, 8960, 9216);
            var one = await NativeId65TiledDocumentCompilation.CaptureMaterialsAsync(original, oneDocument);
            Check(one.DiscPlan.BodyAncestor.DataUsedByteLength == 0xA38 &&
                one.DiscPlan.BodyAncestor.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles,
                "Actual one-tile material body fits legacy numeric shape but retains distinct document/source profile authority.");
            await ClosedConsumers(one);
            await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTiledMaterialsAsync("\0", baseControl.DiscPlan.BodyAncestor),
                "New material disc factory refuses the old Base-only tiled body before source I/O.");
            var baseStage = await NativeId65NormalUploadStaging.CaptureTiledAsync(original, baseControl.FreshResourceAncestor);
            await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync("\0",
                baseline!.TiledMaterialAncestor!, baseStage), "Old Base-only staging cannot substitute a source-issued material staging profile.");
            byte[] canonicalCopy = baseline!.CopyCanonicalDocument(); canonicalCopy.AsSpan().Clear();
            Check(Hash(baseline.CopyCanonicalDocument()) == baseline.CanonicalSha256,
                "Shared compilation exposes detached canonical bytes while retaining its actual complete document.");
        }
        finally { RequireOriginal(original); }
        Console.WriteLine($"NATIVE_ID65_TILED_MATERIAL_COMPILATION_SOURCE_OK checks={_checks} physicalOutputWritten=false nativeCpuExecuted=false publicationApproved=false");
    }

    private static void ValidateCompilation(NativeId65TiledDocumentCompilation compiled, NativeFreshLevelDocumentIntent document,
        Fixture fixture, NativeId65TiledDocumentCompilation baseControl)
    {
        var material = compiled.TiledMaterialAncestor ?? throw new InvalidDataException("Material compilation lost its typed actual ancestor.");
        var fresh = compiled.FreshResourceAncestor; var disc = compiled.DiscPlan; var body = disc.BodyAncestor;
        var revision = NativeId65NormalResourceRevision.FromTiledMaterials(material);
        byte[] canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(document), bytes = disc.CopyBody();
        Check(ReferenceEquals(compiled.Document, document) && ReferenceEquals(material.Document, document) &&
            ReferenceEquals(material.FreshAncestor, fresh) && ReferenceEquals(material.SurfaceAncestor, fresh.TiledSurfaceAncestor) &&
            ReferenceEquals(fresh.TiledSurfaceAncestor!.Document, document) && ReferenceEquals(body.TiledSceneAncestor, fresh) &&
            ReferenceEquals(body.TiledMaterialAncestor, material) && ReferenceEquals(revision.TiledMaterialAncestor, material) &&
            ReferenceEquals(revision.TiledSceneAncestor, fresh),
            "Actual canonical document, source surface, material plan, fresh scene, resource revision and body stay one instance-bound chain.");
        Check(compiled.CopyCanonicalDocument().SequenceEqual(canonical) && compiled.CanonicalSha256 == Hash(canonical) &&
            compiled.CanonicalSha256 == material.CanonicalDocumentSha256 && material.OrderedFaceMaterials.SequenceEqual(fixture.Faces) &&
            document.TiledContent!.Tiles.Select(t => t.EditorId).SequenceEqual(Enumerable.Range(1, 3).Select(i => i.ToString("x32"))),
            "Shared compilation keeps every actual tile ID and material choice in the full schema4 canonical bytes.");
        Check(revision.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles &&
            body.ResourceProfile == revision.ResourceProfile && revision.DataUsedByteLength == 0xBC8 &&
            revision.PlanSha256 == material.PlanSha256 && body.UpstreamPlanSha256 == material.PlanSha256 &&
            material.UploadSha256 == UploadReference && material.DataSha256 == fixture.DataSha256 &&
            revision.CopyUploadPayload().SequenceEqual(material.CopyUploadPayload()) &&
            revision.CopyDataComponents().SequenceEqual(material.CopyDataComponents()) &&
            revision.CopyModelPayload().SequenceEqual(material.CopyModelPayload()) && revision.CopyScene().SequenceEqual(material.CopyScene()),
            "Closed resource revision binds all four exact issued outputs without using the unresolved untextured data as final output.");
        revision.RequireExactOutputs(material.CopyUploadPayload(), material.CopyDataComponents(), material.CopyModelPayload(), material.CopyScene());
        Check(body.DataUsedByteLength == 0xBC8 && body.DataSectorByteLength == 0x1000 &&
            body.ModelRuntimeAddress == 0x8008AA94 && body.SceneRuntimeAddress == 0x8009A294 && body.KnownSceneEndAddress == 0x8009D294,
            "Live model/scene addresses agree with the independent executed native-loader oracle346.");
        Check(bytes.Length == 0x1FB800 && Hash(bytes) == fixture.BodySha256 && body.LevelDataSha256 == fixture.BodySha256 &&
            body.Descriptors[0].Offset == 0x800 && body.Descriptors[1].Offset == 0xDE800 &&
            body.Descriptors[2].Offset == 0xDF800 && body.Descriptors[3].Offset == 0xEF000 &&
            bytes.AsSpan(0xDE800, 0xBC8).SequenceEqual(material.CopyDataComponents()) &&
            bytes.AsSpan(0xDF3C8, 0x438).IndexOfAnyExcept((byte)0) == -1,
            "Complete material body matches the independent golden; physical sector placement stays fixed and only actual used-end padding is zero.");
        byte[] expectedComplement = baseControl.DiscPlan.CopyBody();
        material.CopyUploadPayload().CopyTo(expectedComplement, 0x800);
        expectedComplement.AsSpan(0xDE800, 0x1000).Clear();
        material.CopyDataComponents().CopyTo(expectedComplement, 0xDE800);
        Check(expectedComplement.SequenceEqual(bytes) && disc.CopyOverlay().SequenceEqual(baseControl.DiscPlan.CopyOverlay()) &&
            disc.CopyExecutable().SequenceEqual(baseControl.DiscPlan.CopyExecutable()) &&
            disc.CopyWadDirectory().SequenceEqual(baseControl.DiscPlan.CopyWadDirectory()) &&
            disc.CopyIsoRootDirectory().SequenceEqual(baseControl.DiscPlan.CopyIsoRootDirectory()),
            "Every physical header/model/scene/auxiliary/overlay/EXE/directory complement remains exact beyond the new complete upload/data owners.");
        Check(body.SourceCertified && disc.SourceCertified && !body.RuntimeVerified && !body.PublicationApproved &&
            !body.CompleteNativeDependencyClosureProven && !disc.RuntimeVerified && !disc.PublicationApproved &&
            !disc.NormalCreateBinEnabled && !material.BodyIssued && !material.RuntimeVerified && !material.PublicationApproved,
            "A downstream source body does not retroactively promote the material issuer or confer publication/runtime authority.");
        Check(fresh.SceneLayout.Spawn == new NativeId65SceneSpawn(document.SpawnX, document.SpawnY, document.SpawnZ, document.Heading) &&
            disc.Executable.SelectedBuiltInTrack == document.MusicTrackId && document.MusicTrackId == 19,
            "Scene and executable common settings belong to the same complete actual document.");
        disc.RequireAncestry(body);
        OwnersAndCopies(body);
        var auxiliary = body.Descriptors.Where(d => d.RetainedAuxiliary).Select(d => bytes.AsSpan(d.Offset, d.ByteLength).ToArray()).ToArray();
        var neutral = NativeId65NormalBodyLayout.CompileTiledMaterial(material, auxiliary);
        Check(!neutral.SourceCertified && neutral.CopyLevelData().SequenceEqual(bytes),
            "Typed shared packing consumes actual material bytes but does not certify caller-supplied auxiliaries.");
        foreach (byte[] allocation in auxiliary) allocation.AsSpan().Clear();
        Check(Hash(neutral.CopyLevelData()) == fixture.BodySha256,
            "Typed shared body packing snapshots every auxiliary input before returning.");
    }

    private static async Task Pairing(string original, NativeId65TiledDocumentCompilation compiled)
    {
        var material = compiled.TiledMaterialAncestor!;
        var fresh = material.FreshAncestor;
        var staging = await NativeId65NormalUploadStaging.CaptureTiledMaterialsAsync(original, material);
        Check(ReferenceEquals(staging.TiledMaterialAncestor, material) && ReferenceEquals(staging.TiledSceneAncestor, fresh) &&
            staging.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles &&
            staging.UpstreamPlanSha256 == material.PlanSha256 && staging.PairedUploadSha256 == material.UploadSha256 &&
            !staging.RuntimeVerified && !staging.PublicationApproved && !staging.CompleteSpuStateProven,
            "Material staging retains the actual material instance and source-upload identity without transfer/runtime promotion.");
        var equalMaterial = await NativeId65TiledMaterialPlan.CaptureAsync(original, fresh, material.MaterialSources[0], material.MaterialSources[1]);
        Check(!ReferenceEquals(equalMaterial, material) && ReferenceEquals(equalMaterial.FreshAncestor, fresh) &&
            ReferenceEquals(equalMaterial.MaterialSources[0], material.MaterialSources[0]) &&
            ReferenceEquals(equalMaterial.MaterialSources[1], material.MaterialSources[1]) &&
            equalMaterial.PlanSha256 == material.PlanSha256 && equalMaterial.UploadSha256 == material.UploadSha256 &&
            equalMaterial.DataSha256 == material.DataSha256,
            "Pairing refusal fixture has the SAME fresh/SAME source instances and every equal hash, but a distinct issued material plan.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync("\0", equalMaterial, staging),
            "Body pairing rejects a distinct equal-hash material instance before source I/O; equal scene identity is insufficient.");
        Reject<InvalidDataException>(() => NativeId65NormalBodySource.RequirePair(
            NativeId65NormalResourceRevision.FromTiledMaterials(equalMaterial), staging),
            "The shared source guard enforces actual material-plan reference identity, not just named factory checks.");
        var repeatedBody = await NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync(original, material, staging);
        Check(repeatedBody.PlanSha256 == compiled.DiscPlan.BodyAncestor.PlanSha256 &&
            repeatedBody.CopyLevelData().SequenceEqual(compiled.DiscPlan.CopyBody()) &&
            ReferenceEquals(repeatedBody.TiledMaterialAncestor, material),
            "Correct actual-instance pairing is deterministic through the shared body producer.");
        Reject<InvalidDataException>(() => compiled.DiscPlan.RequireAncestry(repeatedBody),
            "An equal-byte separately issued body cannot replace the disc's actual retained ancestor.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureTiledAsync("\0", fresh, staging),
            "Old Base-only body factory cannot erase unresolved retail choices even with valid material staging.");
        await RejectAsync<InvalidDataException>(() => NativeId65NormalBodyPlan.CaptureAsync("\0", fresh, staging),
            "Old one-floor body factory remains closed to actual tiled ancestry.");
        await RejectAsync<OperationCanceledException>(() => NativeId65NormalUploadStaging.CaptureTiledMaterialsAsync("\0", material, new(true)),
            "Canceled valid material staging stops before source I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65NormalBodyPlan.CaptureTiledMaterialsAsync("\0", material, staging, new(true)),
            "Canceled correctly paired material body stops before source I/O.");
        await RejectAsync<OperationCanceledException>(() => NativeId65FreshDiscPlan.CaptureTiledMaterialsAsync("\0", repeatedBody, new(true)),
            "Canceled valid material disc stops before source I/O.");
        byte[] transfer = staging.CopyTransferSourceImage(), originalTransfer = transfer.ToArray(); transfer.AsSpan().Clear();
        staging.RequireExactTransferSource(originalTransfer);
        Check(staging.CopyTransferSourceImage().SequenceEqual(originalTransfer), "Transfer source snapshots remain immutable.");
        originalTransfer[0] ^= 1;
        Reject<InvalidDataException>(() => staging.RequireExactTransferSource(originalTransfer), "Complete staging owner mutation refuses.");
    }

    private static async Task ClosedConsumers(NativeId65TiledDocumentCompilation compiled)
    {
        var disc = compiled.DiscPlan; var body = disc.BodyAncestor;
        Reject<InvalidDataException>(() => NativeId65NormalResourceRevision.FromTiled(compiled.FreshResourceAncestor),
            "Unresolved geometry remains refused as a Base-only resource revision after a material body is issued.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureAsync("\0", body, 19),
            "Legacy untextured disc factory refuses actual material-tile profile before I/O.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTexturedAsync("\0", body, 19),
            "Legacy two-face disc factory refuses actual material-tile profile, including exactA38 numeric shape.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscPlan.CaptureTiledAsync("\0", body),
            "Old Base-only tiled disc factory cannot omit authored retail materials.");
        await RejectAsync<InvalidDataException>(() => NativeId65FreshDiscResearchCandidatePlan.CaptureAsync("\0", "\0", disc),
            "Fixed research candidate refuses the new actual profile before malformed BIN/CUE paths can be opened.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(compiled.Document),
            "Complete material source compilation does not change saved-research UI readiness.");
    }

    private static void OwnersAndCopies(NativeId65NormalBodyPlan body)
    {
        byte[] bytes = body.CopyLevelData(), overlay = body.CopyEngineOverlay(); int cursor = 0;
        foreach (var owner in body.Owners)
        {
            Check(owner.Offset == cursor && owner.ByteLength > 0 && Hash(bytes.AsSpan(owner.Offset, owner.ByteLength)) == owner.Sha256,
                "Actual body owners form a complete contiguous nonempty hashed partition.");
            cursor += owner.ByteLength;
        }
        Check(cursor == bytes.Length, "All body bytes, not only changed material bytes, have explicit ownership.");
        byte[] bad = bytes.ToArray();
        foreach (int at in body.Owners.SelectMany(o => new[] { o.Offset, o.Offset + o.ByteLength - 1 }).Distinct())
        {
            bad[at] ^= 1;
            Reject<InvalidDataException>(() => body.RequireExactPayloads(bad, overlay), "Every complete body-owner boundary rejects mutation.");
            bad[at] ^= 1;
        }
        Reject<InvalidDataException>(() => body.RequireExactPayloads(bytes[..^1], overlay), "Truncated actual body refuses.");
        Reject<InvalidDataException>(() => body.RequireExactPayloads(bytes.Concat(new byte[4]).ToArray(), overlay), "Extra body bytes refuse.");
        byte[] badOverlay = overlay.ToArray(); badOverlay[0] ^= 1;
        Reject<InvalidDataException>(() => body.RequireExactPayloads(bytes, badOverlay), "Source overlay owner mutation refuses.");
        bytes.AsSpan().Clear(); overlay.AsSpan().Clear();
        Check(Hash(body.CopyLevelData()) == body.LevelDataSha256 && Hash(body.CopyEngineOverlay()) == body.OverlaySha256,
            "Returned whole body and overlay buffers cannot mutate the source-issued plan.");
    }

    private static void VerifyPhysicalStreams(string original, NativeId65FreshDiscPlan disc, string expectedImage)
    {
        using var source = File.OpenRead(original);
        using var candidate = new CandidateStream(original, disc);
        var forward = disc.VerifyImage(source, candidate);
        Check(forward.OutputSha256 == expectedImage && forward.ChangedSectors == 1253 &&
            source.Position == OriginalLength && candidate.Position == OriginalLength,
            "Shared physical forward/readback reaches actual EOF and matches the independently reconstructed full raw image.");
        candidate.Position = 0;
        var inverse = NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default);
        Check(inverse.OutputSha256 == expectedImage && inverse.ChangedSectors == 1253 &&
            candidate.Position == OriginalLength && candidate.ReadByte() == -1,
            "Shared complete raw inverse authenticates every changed/unchanged sector and restores the original full SHA through actual EOF.");
    }

    private static void VerifyCorruptionRefusal(string original, NativeId65FreshDiscPlan disc)
    {
        foreach (int lba in new[] { 0, 53875 })
        {
            using var candidate = new CandidateStream(original, disc, lba);
            Reject<InvalidDataException>(() => NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default),
                "Actual candidate corruption in untouched or changed physical ownership cannot pass the new profile's inverse.");
        }
    }

    // A read-only generated stream, never an output image or independently
    // learned golden. The full expected hashes above come from Python oracle352.
    private sealed class CandidateStream : Stream
    {
        private readonly FileStream _source;
        private readonly NativeId65FreshDiscPlan _disc;
        private readonly int _corruptLba;
        private byte[] _sector = [];
        private int _sectorLba = -1;
        private long _position;
        internal CandidateStream(string original, NativeId65FreshDiscPlan disc, int corruptLba = -1)
        { _source = File.OpenRead(original); _disc = disc; _corruptLba = corruptLba; }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => OriginalLength;
        public override long Position
        {
            get => _position;
            set { if (value < 0 || value > Length) throw new ArgumentOutOfRangeException(nameof(value)); _position = value; }
        }
        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));
        public override int Read(Span<byte> buffer)
        {
            int total = 0;
            while (!buffer.IsEmpty && _position < Length)
            {
                int lba = (int)(_position / 2352), within = (int)(_position % 2352);
                if (lba != _sectorLba)
                {
                    byte[] original = new byte[2352]; _source.Position = lba * 2352L; _source.ReadExactly(original);
                    _sector = _disc.CreateExpectedRawSector(lba, original);
                    if (lba == _corruptLba) _sector[24] ^= 1;
                    _sectorLba = lba;
                }
                int take = Math.Min(buffer.Length, 2352 - within);
                _sector.AsSpan(within, take).CopyTo(buffer);
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

    private static void RequireOriginal(string original)
    {
        using var input = File.OpenRead(original);
        Check(input.Length == OriginalLength && Convert.ToHexStringLower(SHA256.HashData(input)) == OriginalHash &&
            input.Position == OriginalLength && input.ReadByte() == -1 && input.Length == OriginalLength,
            "Named original image retains its full SHA, size and actual EOF before and after every source operation.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private sealed record Fixture(string Name, int[] Faces, string DataSha256, string BodySha256, string ImageSha256);

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
    private static void Check(bool condition, string label)
    { if (!condition) throw new InvalidDataException(label); _checks++; }
    private static void Reject<T>(Action action, string label) where T : Exception
    { try { action(); } catch (T) { Check(true, label); return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
    private static async Task RejectAsync<T>(Func<Task> action, string label) where T : Exception
    { try { await action(); } catch (T) { Check(true, label); return; } throw new InvalidDataException("Expected " + typeof(T).Name + ": " + label); }
}
