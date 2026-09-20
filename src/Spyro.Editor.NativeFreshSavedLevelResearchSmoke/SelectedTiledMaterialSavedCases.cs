using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

// The public saved candidate is the implementation seam. These isolated
// fixtures check neutral readiness without executing UI or writing a native image.
internal static class SelectedTiledMaterialSavedCases
{
    internal const string Variant = "schema4-selected-static-material-flat-tiles-p0-n16-data1000";

    internal static async Task Free()
    {
        var one = Document([0, 0, 0, 0, 0, 0]);
        var three = Document([0, 1, 2, 0, 1, 2]);
        Check(one.SchemaVersion == 4 && three.SchemaVersion == 4 && one.LegacyContent == null && three.LegacyContent == null &&
            three.TiledContent!.Tiles.Select(t => t.EditorId).SequenceEqual(Enumerable.Range(1, 3).Select(i => i.ToString("x32"))) &&
            one.Placements.Count == 0 && three.Placements.Count == 0,
            "Selected saved fixtures retain whole actual schema4/P0 intent and all stable tile IDs without a dummy Base.");
        foreach (var document in new[] { one, three, Document([0, 255, 0, 255, 0, 255]),
            Document([0, 1, 255, 2, 0, 255]), Document([255, 255, 255, 255, 0, 0]),
            Document([0, 1, 0, 1, 0, 2]), BoundaryDocument(56, 1), BoundaryDocument(58, 1) })
        {
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(document) &&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(document),
                "Fitting whole P0 selected material intent is neutrally ready while Base-only readiness remains separate.");
            var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(document);
            Check(prepared.Selections.Length is 1 or 3 && prepared.Surface.CopyBundle().Length +
                NativeId65NormalComponentLayout.NonSurfaceByteLength + prepared.Selections.Length * 184 <= 0x1000,
                "Whole neutral geometry and every selected record fit before saved/source admission is attempted.");
            var (project, saved) = Save("selected-tiled-material-supported", document);
            byte[] raw = File.ReadAllBytes(saved.Snapshot.Path), manifest = File.ReadAllBytes(project.ManifestPath);
            // Deliberately malformed paths distinguish reaching path validation
            // from the old exact-two material preflight. No source can be opened.
            var error = await Throws<ArgumentException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                "\0", "\0", saved.Snapshot));
            Check(!Leaves(error).Any(e => e is InvalidDataException),
                "Supported N1/N3 saved intent reaches malformed source-path validation instead of the old two-record refusal.");
            var missing = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                Missing, Missing, saved.Snapshot));
            Check(Leaves(missing)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
                "The same public candidate factory reaches existing source ownership checks for complete selected intent.");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            var cancellation = await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                "\0", "\0", saved.Snapshot, cancelled.Token));
            Check(cancellation.CancellationToken == cancelled.Token && File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(raw) &&
                File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                "Pre-cancellation is exact and all failed attempts preserve the saved raw document and manifest.");
        }
        foreach (var (zCell, expected) in new[] { (56, 0xFFC), (58, 0x1000), (60, 0x1004) })
        {
            var boundary = BoundaryDocument(zCell, 1);
            int actual = NativeFlatTileSurfaceLayout.Compile(boundary.TiledContent!.Tiles.Select(t => t.ToNativeTile()), 0x406080)
                .CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength + 184;
            Check(actual == expected, "Actual high-Z collision tree plus one complete record establishes FFC/1000/1004, not an assumed tile count.");
        }
        var wizard = NativeFreshWizardRouteIntent.Create(new string('a', 32), 13056, 8960, 8704, 13568, 8960);
        var moby = NativeFreshMobyPlacementIntent.Create(new string('b', 32),
            NativeFreshRetailMobyReference.Create(Cases.Variant, 10, 0, 0x78), 13568, 8960, 8704, 0);
        foreach (var (label, document) in new[]
        {
            ("one-wizard", one.WithPlacements([wizard])), ("three-native-moby", three.WithPlacements([moby])),
            ("one-native-moby", one.WithPlacements([moby])), ("three-wizard", three.WithPlacements([wizard])),
            ("one-data1004", BoundaryDocument(60, 1)), ("three-final-record-overflow", BoundaryDocument(58, 3)),
            ("complete-geometry-overflow", LargeGeometry()), ("too-many-distinct-selections", TooManySelections())
        })
        {
            var (project, saved) = Save("selected-tiled-material-unsupported-" + label, document);
            byte[] raw = File.ReadAllBytes(saved.Snapshot.Path), manifest = File.ReadAllBytes(project.ManifestPath);
            var error = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
            Check(!Leaves(error).Any(e => e is ArgumentException or FileNotFoundException or DirectoryNotFoundException) &&
                File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(raw) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
                "Unsupported whole selected intent refuses before malformed paths/source I/O without losing rows: " + label);
        }
        foreach (string owner in new[] { "raw", "manifest" })
        {
            var (project, saved) = Save("selected-tiled-material-stale-" + owner, three);
            File.AppendAllText(owner == "raw" ? saved.Snapshot.Path : project.ManifestPath, " \n");
            var error = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
            Check(!Leaves(error).Any(e => e is ArgumentException), "Actual saved raw/manifest freshness precedes source paths for selected N3.");
            await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                "\0", "\0", saved.Snapshot, new(true)));
        }
        foreach (var document in new[] { SavedTiledDocumentFixture.Document(), SavedTiledMaterialDocumentFixture.Document() })
        {
            var (_, saved) = Save("selected-preserved-base-or-n2", document);
            await Throws<ArgumentException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
        }
        Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(SavedTiledDocumentFixture.Document()) &&
            NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(SavedTiledMaterialDocumentFixture.Document()),
            "Existing Base and two-source intent remain neutrally ready after selected-material readiness is generalized.");
        Console.WriteLine($"SELECTED_TILED_MATERIAL_SAVED_FREE_OK checks={Checks} refusals={Refusals} originalOpened=false nativeImageWritten=false selectedNeutralReadiness=true uiExecuted=false");
    }

    // Literal references from independent source/data401, native loader402 and
    // complete raw-disc403, frozen before selected production source execution.
    private const string ModelReference = "88c78343c283256de6ce8d3151b43fa72ba4d96b046d869bd0a7209cbff4c23d";
    private const string SceneReference = "4a6e7b5c6c53199b8a7aa6ae0d24ac9bce32634aa259d5f49535282080641433";
    private static readonly Fixture[] Fixtures =
    [
        new("one", [0, 0, 0, 0, 0, 0], [(14, 0)], 0xB10,
            "39a2be62e958b399724ef79496129973d62afa8ca1faf0923dbcfaa81094ede3",
            "fbbb180406f460d2acb2258009fe3a5b020d63842a4bc90a352f53c0a56890a4",
            "11435c190e2103134e1eb2e054469eec8aff51d539cc39e97beab5cc89a045b5",
            "14e5b3606f652c250df9cdd6c94de1d393186d1b74f1ab64decd5dd35130a77f"),
        new("three", [0, 1, 2, 0, 1, 2], [(14, 0), (70, 0), (14, 1)], 0xC80,
            "8a8ca0bed06c7f622d8ad6420e9e65cbb1a3391a1a86942f333921dfc75b4478",
            "398dbf0543d30e5c30a49c9c94ab319fab2900ca96c835d916ddad4a049194bd",
            "a78a1ffcc8870ef91d26170adcd6b735d28466efdd2c24fb7018a293f439ec8c",
            "f1c137476833d1c046a78df812b40a47f8dc6558051c823437d7c5b3f86d8e32"),
        // Existing N2 independent physical oracle352 remains a separate control.
        new("preserved-two", [0, 1, 0, 1, 0, 1], [(14, 0), (70, 0)], 0xBC8,
            "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333",
            "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa",
            "461326c0e362a7c0bdb4c15c21996d15f6b1674e2156d60802f0efc94e6c36be",
            "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8")
    ];

    internal static async Task Source(string bin, string cue)
    {
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(bin), Path.GetFullPath(cue));
        Exception? primary = null;
        try
        {
            owners.Open(CancellationToken.None);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, CancellationToken.None);
            foreach (var fixture in Fixtures) await SourceCase(owners.ImagePath, owners.CuePath, fixture);
            Check(!Directory.EnumerateFiles(Root, "*.bin", SearchOption.AllDirectories).Any() &&
                !Directory.EnumerateFiles(Root, "*.cue", SearchOption.AllDirectories).Any(),
                "Selected saved source tests retain only tiny project documents; native images and CUE files are never written.");
        }
        catch (Exception error) { primary = error; }
        owners.Complete(primary); // Exact held and named BIN/CUE, true EOF and all closes, including failure paths.
        Console.WriteLine($"SELECTED_TILED_MATERIAL_SAVED_SOURCE_OK checks={Checks} refusals={Refusals} originalFinal=true nativeImageWritten=false runtime=false selectedNeutralReadiness=true uiExecuted=false");
    }

    private static async Task SourceCase(string bin, string cue, Fixture fixture)
    {
        var (project, saved) = Save("selected-tiled-material-saved-" + fixture.Name, Document(fixture.Faces));
        byte[] raw = File.ReadAllBytes(saved.Snapshot.Path), manifest = File.ReadAllBytes(project.ManifestPath);
        var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot);
        var compiled = plan.TiledCompilation ?? throw new InvalidDataException("Selected saved candidate lost its actual tiled compilation.");
        var material = compiled.TiledMaterialAncestor ?? throw new InvalidDataException("Selected saved candidate lost its actual selected material issuer.");
        var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
        bool legacyTwo = fixture.Sources.Length == 2;
        plan.RequireTiledAncestry(plan.Saved, compiled);
        material.RequireSelectedAncestry(fresh, saved.Document, material.MaterialSources);
        Check(ReferenceEquals(plan.SavedSnapshot, saved.Snapshot) && ReferenceEquals(compiled.Document, saved.Document) &&
            ReferenceEquals(material.Document, saved.Document) && ReferenceEquals(material.FreshAncestor, fresh) &&
            ReferenceEquals(material.SurfaceAncestor.Document, saved.Document) && ReferenceEquals(body.TiledMaterialAncestor, material) &&
            ReferenceEquals(body.TiledSceneAncestor, fresh) && body.UpstreamPlanSha256 == material.PlanSha256 &&
            body.ResourceProfile == (legacyTwo ? NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles :
                NativeId65NormalResourceProfile.DirectLqSelectedStaticMaterialsFlatTiles),
            "Public saved capture retains the same complete document, actual ordered-source material instance, fresh/body ancestry and count-independent selected profile.");
        Check(plan.MixedDisc == null && plan.FamilyCompilation == null && plan.ExpectedImageSha256 == fixture.Image &&
            plan.ExpectedImageByteLength == 661547040 && plan.ExpectedChangedRawSectors == 1253 &&
            material.UploadSha256 == fixture.Upload && material.DataSha256 == fixture.Data &&
            material.ModelSha256 == ModelReference && material.SceneSha256 == SceneReference &&
            Hash(compiled.DiscPlan.CopyBody()) == fixture.Body && body.DataUsedByteLength == fixture.Used &&
            body.DataSectorByteLength == 0x1000 && compiled.DiscPlan.SourceCertified && material.SourceCertified &&
            !plan.RuntimeVerified && !compiled.DiscPlan.RuntimeVerified && !compiled.DiscPlan.PublicationApproved &&
            !material.RuntimeVerified && !material.PublicationApproved,
            "Final saved N1/N3 and preserved N2 outputs match independent full-domain/full-image references, without runtime or broad publication approval.");
        Check(material.MaterialSources.Select(s => (s.SourceWadEntry, s.SourceTextureId)).SequenceEqual(fixture.Sources) &&
            material.MaterialSources.Count == fixture.Sources.Length && material.OrderedFaceMaterials.SequenceEqual(fixture.Faces) &&
            !material.CopyDataComponents().SequenceEqual(fresh.CopyDataComponents()) && fresh.UploadSha256 != fixture.Upload &&
            fresh.DataSha256 == "fef347e230881f796927562fd4d9482a94c15a43ceae050cbfe5821b6edb59cb",
            "All ordered source descriptors and every tile face survive; final material data cannot be replaced by unresolved fresh data.");
        var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        Check(ReferenceEquals(binding.SavedLevelPlan, plan) && binding.WizardPlan == null,
            "Selected saved candidates reuse the existing publication transaction and source ownership role.");
        var measured = new NativeId65FreshDiscVerification(fixture.Image, 1253);
        byte[] canonical = plan.CopyCanonicalDocument(), receipt = plan.BuildReceipt(measured);
        Check(canonical.SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)) &&
            canonical.SequenceEqual(compiled.CopyCanonicalDocument()) && receipt.Length is > 0 and <= 65536,
            "Complete canonical saved intent and the untruncated receipt remain within their actual byte budgets.");
        Receipt(plan, compiled, receipt, fixture);
        await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(saved, compiled); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { _ = plan.MaterialCompilation; return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(fixture.Image, 1252)); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(new string('0', 64), 1253)); return Task.CompletedTask; });
        canonical[0] ^= 1; receipt[^1] ^= 1;
        Check(plan.CopyCanonicalDocument()[0] != canonical[0] && plan.BuildReceipt(measured)[^1] != receipt[^1],
            "Detached canonical and full receipt copies cannot mutate the source-bound candidate.");
        await plan.RequireFreshAsync();
        Check(File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(raw) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest),
            "Actual source compilation and verification leave raw saved bytes and the manifest unchanged.");
        if (!legacyTwo)
        {
            VerifyStreams(bin, compiled.DiscPlan, fixture.Image);
            var reopened = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
            Check(reopened.PlanSha256 == plan.PlanSha256 && reopened.BuildReceipt(measured).SequenceEqual(plan.BuildReceipt(measured)) &&
                !ReferenceEquals(reopened.TiledCompilation, compiled) && !ReferenceEquals(reopened.Saved, plan.Saved),
                "Equal saved bytes deterministically reproduce the complete selected receipt without becoming the same actual ancestor.");
            await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(plan.Saved, reopened.TiledCompilation!); return Task.CompletedTask; });
            await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(reopened.Saved, compiled); return Task.CompletedTask; });
            await Throws<InvalidDataException>(() => { material.RequireSelectedAncestry(fresh, saved.Document,
                reopened.TiledCompilation!.TiledMaterialAncestor!.MaterialSources); return Task.CompletedTask; });
            if (fixture.Sources.Length == 3) await Staleness(bin, cue, project, plan, fixture);
        }
        Check(!legacyTwo || NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(saved.Document),
            "Preserved exact-two readiness remains supported independently of selected backend capture.");
        Check(legacyTwo || NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(saved.Document),
            "Fitting whole P0 selected intent has neutral readiness independently of actual source capture and pending runtime proof.");
        Console.WriteLine($"SELECTED_TILED_MATERIAL_SAVED_CASE_OK name={fixture.Name} image={fixture.Image} sectors=1253 receiptBytes={plan.BuildReceipt(measured).Length} plan={plan.PlanSha256} root={project.RootPath} projectId={saved.Snapshot.ProjectId} nativeImageWritten=false");
    }

    private static async Task Staleness(string bin, string cue, Spyro.Editor.Core.Persistence.EditorProjectLayout project,
        NativeFreshSavedLevelResearchCandidatePlan baseline, Fixture fixture)
    {
        var document = baseline.Saved.Document;
        var content = document.TiledContent!;
        var idOnly = document.WithTiledContent(content.WithTiles(content.Tiles.Select((tile, index) => index == 0
            ? NativeFreshFlatTileIntent.Create(4.ToString("x32"), tile.X, tile.Y, tile.Z, tile.Width, tile.Depth, tile.FloorColor, tile.Materials) : tile)));
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), idOnly);
        await Throws<InvalidDataException>(() => baseline.RequireFreshAsync());
        var idPlan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        var measured = new NativeId65FreshDiscVerification(fixture.Image, 1253);
        Check(idPlan.ExpectedImageSha256 == fixture.Image && idPlan.PlanSha256 != baseline.PlanSha256 &&
            !idPlan.CopyCanonicalDocument().SequenceEqual(baseline.CopyCanonicalDocument()) &&
            !idPlan.BuildReceipt(measured).SequenceEqual(baseline.BuildReceipt(measured)) &&
            idPlan.TiledCompilation!.DiscPlan.CopyBody().SequenceEqual(baseline.TiledCompilation!.DiscPlan.CopyBody()),
            "N3 editor-ID-only authorship keeps native image bytes but changes the full saved candidate and receipt.");
        File.AppendAllText(idPlan.SavedSnapshot.Path, " \n");
        await Throws<InvalidDataException>(() => idPlan.RequireFreshAsync());
        var reissued = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(reissued.ExpectedImageSha256 == fixture.Image && reissued.PlanSha256 != idPlan.PlanSha256 &&
            reissued.CopyCanonicalDocument().SequenceEqual(idPlan.CopyCanonicalDocument()) &&
            !reissued.BuildReceipt(measured).SequenceEqual(idPlan.BuildReceipt(measured)),
            "Raw-only selected document staleness remains binding despite equal canonical and native bytes.");
        byte[] manifest = File.ReadAllBytes(project.ManifestPath);
        File.AppendAllText(project.ManifestPath, " \n");
        await Throws<InvalidDataException>(() => reissued.RequireFreshAsync());
        await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", reissued.SavedSnapshot));
        File.WriteAllBytes(project.ManifestPath, manifest);
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), document);
        Check(NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentStore.Capture(project).Intent!).SequenceEqual(baseline.CopyCanonicalDocument()),
            "Retained N3 test project restores the complete independently reviewed fixture, including stable IDs and all face selections.");
    }

    private static void Receipt(NativeFreshSavedLevelResearchCandidatePlan plan, NativeId65TiledDocumentCompilation compiled,
        byte[] receipt, Fixture fixture)
    {
        var material = compiled.TiledMaterialAncestor!; var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
        using var json = JsonDocument.Parse(receipt); using var canonical = JsonDocument.Parse(plan.CopyCanonicalDocument());
        var root = json.RootElement; var saved = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
        Check(root.GetProperty("variant").GetString() == (fixture.Sources.Length == 2 ? TiledMaterialSavedCases.Variant : Variant) &&
            root.GetProperty("planSha256").GetString() == plan.PlanSha256 &&
            saved.GetProperty("projectId").GetString() == plan.SavedSnapshot.ProjectId &&
            saved.GetProperty("root").GetString() == plan.SavedSnapshot.RootPath && saved.GetProperty("path").GetString() == plan.SavedSnapshot.Path &&
            saved.GetProperty("rawSha256").GetString() == plan.SavedSnapshot.ContentSha256 &&
            saved.GetProperty("manifestSha256").GetString() == plan.SavedSnapshot.ManifestSha256 &&
            saved.GetProperty("canonicalSha256").GetString() == compiled.CanonicalSha256 &&
            JsonElement.DeepEquals(saved.GetProperty("canonical"), canonical.RootElement) &&
            root.GetProperty("wholeImageInverseRequired").GetBoolean() && root.GetProperty("savedRevisionBound").GetBoolean() &&
            !root.TryGetProperty("publicationGuardPassed", out _),
            "Selected receipt binds separate saved raw/canonical/manifest owners and the complete actual document; N2 keeps its old variant.");
        Check(root.GetProperty("sourceImageSha256").GetString() == Original && root.GetProperty("SourceImagePath").GetString() == plan.SourceImagePath &&
            root.GetProperty("SourceCuePath").GetString() == plan.SourceCuePath && root.GetProperty("SourceCueSha256").GetString() == plan.SourceCueSha256 &&
            root.GetProperty("imageSha256").GetString() == fixture.Image && root.GetProperty("imageByteLength").GetInt64() == 661547040 &&
            root.GetProperty("changedRawSectors").GetInt32() == 1253 && root.GetProperty("cueSha256").GetString() == Hash(plan.CopyCueBytes()),
            "Original BIN/CUE identities and the complete independently measured candidate remain in the receipt.");
        Check(lineage.GetProperty("discPlan").GetString() == compiled.DiscPlan.PlanSha256 &&
            lineage.GetProperty("bodyPlan").GetString() == body.PlanSha256 && lineage.GetProperty("stagingPlan").GetString() == body.StagingPlanSha256 &&
            lineage.GetProperty("materialPlan").GetString() == material.PlanSha256 && lineage.GetProperty("freshPlan").GetString() == fresh.PlanSha256 &&
            lineage.GetProperty("resourceChainPlan").GetString() == fresh.ResourceChainAncestor.PlanSha256 &&
            lineage.GetProperty("DocumentSha256").GetString() == compiled.CanonicalSha256 &&
            lineage.GetProperty("CanonicalDocumentSha256").GetString() == compiled.CanonicalSha256 &&
            lineage.GetProperty("ResourceProfile").GetInt32() == (fixture.Sources.Length == 2 ? 3 : 4) &&
            lineage.GetProperty("DataUsedByteLength").GetInt32() == fixture.Used && lineage.GetProperty("DataSectorByteLength").GetInt32() == 0x1000 &&
            lineage.GetProperty("tileCount").GetInt32() == 3 && lineage.GetProperty("placedCount").GetInt32() == 0 &&
            lineage.GetProperty("nativeNominalCapacity").GetInt32() == 16,
            "One selected profile binds actual full compilation, all three tiles, P0/N16 and the measured material-dependent used endpoint.");
        Check(lineage.GetProperty("UploadSha256").GetString() == fixture.Upload && lineage.GetProperty("DataSha256").GetString() == fixture.Data &&
            lineage.GetProperty("ModelSha256").GetString() == ModelReference && lineage.GetProperty("SceneSha256").GetString() == SceneReference &&
            lineage.GetProperty("BeforeUploadSha256").GetString() == fresh.UploadSha256 &&
            lineage.GetProperty("BeforeDataSha256").GetString() == fresh.DataSha256 &&
            lineage.GetProperty("NativeClip").GetInt32() == 0x8000 && lineage.GetProperty("AdditionalPacketBytesPerPass").GetInt32() == 72 &&
            lineage.GetProperty("OrderedFaceMaterials").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(fixture.Faces) &&
            lineage.GetProperty("SourceHandles").EnumerateArray().Select(v => v.GetString()).SequenceEqual(material.SourceHandles),
            "Final independent hashes, distinct intermediate hashes, exact clip and every ordered face/source handle are explicit without truncation.");
        var selections = lineage.GetProperty("Selections").EnumerateArray().ToArray();
        var sources = lineage.GetProperty("materials").EnumerateArray().ToArray();
        Check(selections.Length == fixture.Sources.Length && sources.Length == fixture.Sources.Length &&
            material.Allocation.Records.Count == fixture.Sources.Length,
            "All N1/N3 descriptors and complete material rows fit the receipt; N3 never silently keeps only its first two sources.");
        for (int slot = 0; slot < fixture.Sources.Length; slot++)
        {
            var actual = material.MaterialSources[slot]; var selection = selections[slot]; var source = sources[slot];
            Check(selection.GetProperty("SourceVariant").GetString() == Cases.Variant &&
                selection.GetProperty("SourceWadEntry").GetInt32() == fixture.Sources[slot].Entry &&
                selection.GetProperty("TextureId").GetInt32() == fixture.Sources[slot].Texture &&
                source.GetProperty("slot").GetInt32() == slot && source.GetProperty("Handle").GetString() == actual.Handle &&
                source.GetProperty("SourceImageSha256").GetString() == Original &&
                source.GetProperty("SourceWadEntry").GetInt32() == fixture.Sources[slot].Entry &&
                source.GetProperty("SourceTextureId").GetInt32() == fixture.Sources[slot].Texture &&
                source.GetProperty("SourceDataSha256").GetString() == actual.SourceDataSha256 &&
                source.GetProperty("sourceRecordSha256").GetString() == actual.Layout.RecordSha256 &&
                source.GetProperty("relocatedRecordSha256").GetString() == material.Allocation.Records[slot].RecordSha256 &&
                material.Allocation.Allocations.Any(a => a.MaterialId == slot && a.SourceIdentity == actual.Handle),
                "Each first-occurrence descriptor binds its actual source data, full source/relocated record and allocated source handle, including the third slot.");
        }
        Check(JsonElement.DeepEquals(lineage.GetProperty("Allocations"), JsonSerializer.SerializeToElement(material.Allocation.Allocations)) &&
            JsonElement.DeepEquals(lineage.GetProperty("ContainedAliases"), JsonSerializer.SerializeToElement(material.Allocation.ContainedAliases)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Evidence"), JsonSerializer.SerializeToElement(material.Evidence)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Descriptors"), JsonSerializer.SerializeToElement(body.Descriptors)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Owners"), JsonSerializer.SerializeToElement(body.Owners)) &&
            JsonElement.DeepEquals(lineage.GetProperty("PhaseOverlaps"), JsonSerializer.SerializeToElement(body.PhaseOverlaps)) &&
            lineage.GetProperty("bodySha256").GetString() == fixture.Body &&
            lineage.GetProperty("overlaySha256").GetString() == Hash(compiled.DiscPlan.CopyOverlay()) &&
            lineage.GetProperty("executableSha256").GetString() == Hash(compiled.DiscPlan.CopyExecutable()) &&
            lineage.GetProperty("wadDirectorySha256").GetString() == Hash(compiled.DiscPlan.CopyWadDirectory()) &&
            lineage.GetProperty("isoRootSha256").GetString() == Hash(compiled.DiscPlan.CopyIsoRootDirectory()),
            "Every allocation, contained alias, consumer evidence, body owner/phase and physical output component is retained in the bounded receipt.");
        foreach (string flag in new[] { "nativePersistentSaveOwnerProven", "runtimeVerified", "normalCreateBinApproved",
            "completeNativeDependencyClosureProven", "hqRuntimeProven", "audioRuntimeProven", "peakRuntimeMemoryProven", "checkpointOrForeignStateSupported" })
            Check(!root.GetProperty(flag).GetBoolean(), "No selected receipt claims pending owner: " + flag);
    }

    private static void VerifyStreams(string original, NativeId65FreshDiscPlan disc, string expected)
    {
        using var source = File.OpenRead(original);
        using var candidate = new CandidateStream(original, disc);
        var forward = disc.VerifyImage(source, candidate);
        Check(forward.OutputSha256 == expected && forward.ChangedSectors == 1253 &&
            source.Position == 661547040 && candidate.Position == 661547040,
            "Saved selected disc passes complete raw forward readback against the independent image reference through EOF.");
        candidate.Position = 0;
        var inverse = NativeId65FreshDiscStreaming.VerifyInverse(disc, candidate, default);
        Check(inverse.OutputSha256 == expected && inverse.ChangedSectors == 1253 && candidate.Position == 661547040 && candidate.ReadByte() == -1,
            "The shared C# full-image inverse restores the exact original SHA/length/EOF with every changed and unchanged raw sector checked; no physical image is written.");
    }

    private sealed record Fixture(string Name, int[] Faces, (int Entry, int Texture)[] Sources, int Used,
        string Upload, string Data, string Body, string Image);

    // Read-only generated candidate for the existing full forward/inverse
    // verifier. Its expected hash is an independent literal, not learned here.
    private sealed class CandidateStream(string original, NativeId65FreshDiscPlan disc) : Stream
    {
        private readonly FileStream _source = File.OpenRead(original);
        private byte[] _sector = [];
        private int _lba = -1;
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => 661547040;
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
                    _sector = disc.CreateExpectedRawSector(lba, before); _lba = lba;
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

    internal static NativeFreshLevelDocumentIntent Document(int[] faces)
    {
        if (faces.Length != 6) throw new ArgumentException("All six selected fixture face bindings are required.", nameof(faces));
        var document = SavedTiledDocumentFixture.Document();
        return document.WithTiledContent(document.TiledContent!.WithTiles(document.TiledContent.Tiles.Select((tile, index) =>
            NativeFreshFlatTileIntent.Create(tile.EditorId, tile.X, tile.Y, tile.Z, tile.Width, tile.Depth, tile.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(Material(faces[2 * index]), Material(faces[2 * index + 1]))))));
    }

    private static NativeFreshLevelDocumentIntent BoundaryDocument(int zCell, int count)
    {
        var document = SavedTiledMaterialDocumentFixture.BoundaryDocument(zCell);
        // One fewer184-byte metadata record is balanced by92 additional native
        // tree links:46 in X and46 in Y, both inside the actual packed domain.
        int shift = count == 1 ? 46 * 4096 : 0;
        var content = document.TiledContent!;
        return document.WithTiledContent(NativeFreshTiledLevelContent.Create(content.Tiles.Select((tile, index) =>
            NativeFreshFlatTileIntent.Create(tile.EditorId, tile.X + shift, tile.Y + shift, tile.Z, tile.Width, tile.Depth, tile.FloorColor,
                NativeFreshFloorMaterialsIntent.Create(Material(2 * index % count), Material((2 * index + 1) % count)))),
            content.SpawnX + shift, content.SpawnY + shift, content.SpawnZ, content.Heading, content.SkyColor, content.MusicTrackId));
    }

    private static NativeFreshLevelDocumentIntent LargeGeometry() => NativeFreshLevelDocumentIntent.CreateTiled(
        NativeFreshTiledLevelContent.Create(Enumerable.Range(0, 16).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"),
            8448 + i * 2048, 8448, 8704, 2048, 2048, new(80, 128, 112), NativeFreshFloorMaterialsIntent.Create(Material(0), Material(0)))),
            8960, 8960, 9216, 64, new(128, 96, 64), 19), []);

    private static NativeFreshLevelDocumentIntent TooManySelections() => NativeFreshLevelDocumentIntent.CreateTiled(
        NativeFreshTiledLevelContent.Create(Enumerable.Range(0, 65).Select(i => NativeFreshFlatTileIntent.Create((i + 1).ToString("x32"),
            i * 16, 0, 8704, 16, 16, new(80, 128, 112), NativeFreshFloorMaterialsIntent.Create(Selection(2 * i), Selection(2 * i + 1)))),
            8, 8, 9216, 64, new(128, 96, 64), 19), []);

    private static NativeFreshRetailTerrainMaterialIntent Selection(int number) =>
        NativeFreshFloorMaterialIntent.RetailTerrain(Cases.Variant, number < 128 ? 14 : 70, number % 128);
    private static NativeFreshFloorMaterialIntent Material(int slot) => slot switch
    {
        0 => Selection(0), 1 => NativeFreshFloorMaterialIntent.RetailTerrain(Cases.Variant, 70, 0), 2 => Selection(1),
        255 => NativeFreshFloorMaterialIntent.BaseFloorColor(), _ => throw new ArgumentOutOfRangeException(nameof(slot))
    };
}
