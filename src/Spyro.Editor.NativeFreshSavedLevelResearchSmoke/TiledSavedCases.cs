using System.Buffers.Binary;
using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class TiledSavedCases
{
    internal const string ExpectedImage = "7ba0b29b68a1bee415901382c3a3b5345ccb01ca84872502131f68361afdc305";
    internal const string Variant = "schema4-base-color-flat-tiles-p0-n16-data1000";

    internal static async Task Free()
    {
        var document = SavedTiledDocumentFixture.Document();
        Check(document.SchemaVersion == 4 && document.LegacyContent == null && document.Placements.Count == 0 &&
            document.TiledContent!.Tiles.Select(t => t.EditorId).SequenceEqual(Enumerable.Range(1, 3).Select(i => i.ToString("x32"))),
            "Exact tiled fixture retains three stable IDs, actual content and no dummy legacy Base.");
        foreach (var (zCell, expected) in new[] { (44, 0xFFC), (46, 0x1000), (48, 0x1004) })
            Check(NeutralData(SavedTiledDocumentFixture.BoundaryDocument(zCell)).Length == expected,
                "Actual packed geometry establishes the fixed-body allocation boundary, not a tile-count approximation.");
        foreach (var valid in new[] { document, SavedTiledDocumentFixture.BoundaryDocument(46) })
        {
            var (_, saved) = Save("tiled-supported-preflight", valid);
            var error = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
            Check(Leaves(error)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
                "Canonical supported tiled content reaches original authentication through the same saved factory.");
            await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                Missing, Missing, saved.Snapshot, new(true)));
        }
        foreach (var (name, invalid) in SavedTiledDocumentFixture.Unsupported())
        {
            var (_, saved) = Save("tiled-unsupported-" + name, invalid);
            if (name.EndsWith("-retail", StringComparison.Ordinal))
            {
                Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(invalid) &&
                    NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(invalid),
                    "Single-retail whole intent remains outside Base-only readiness but is neutrally supported through selected materials: " + name);
                await Throws<ArgumentException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
                var missing = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
                Check(Leaves(missing)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
                    "Supported selected N1 intent reaches source ownership checks without discarding the retail face: " + name);
                continue;
            }
            var error = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
            Check(!Leaves(error).Any(e => e is FileNotFoundException or DirectoryNotFoundException),
                "Unsupported complete tiled content refuses before source I/O: " + name);
        }
        foreach (string owner in new[] { "raw", "manifest" })
        {
            var (project, saved) = Save("tiled-stale-" + owner, document);
            File.AppendAllText(owner == "raw" ? saved.Snapshot.Path : project.ManifestPath, " ");
            await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
        }
        Console.WriteLine("TILED_SAVED_FREE_OK originalOpened=false nativeImageWritten=false");
    }

    internal static async Task Source(string bin, string cue)
    {
        var (project, saved) = Save("tiled-saved-publication", SavedTiledDocumentFixture.Document());
        var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot);
        var compiled = plan.TiledCompilation!;
        plan.RequireTiledAncestry(plan.Saved, compiled);
        Check(ReferenceEquals(plan.SavedSnapshot, saved.Snapshot) && ReferenceEquals(compiled.Document, saved.Document) &&
            ReferenceEquals(compiled.FreshResourceAncestor.TiledSurfaceAncestor!.Document, saved.Document) &&
            ReferenceEquals(compiled.DiscPlan.BodyAncestor.TiledSceneAncestor, compiled.FreshResourceAncestor),
            "Saved candidate retains its actual complete tiled document and source-issued scene/body chain.");
        Check(plan.MixedDisc == null && plan.FamilyCompilation == null &&
            plan.ExpectedImageSha256 == ExpectedImage && plan.ExpectedImageByteLength == 661547040 &&
            plan.ExpectedChangedRawSectors == 1253 && !plan.RuntimeVerified &&
            compiled.DiscPlan.SourceCertified && !compiled.DiscPlan.RuntimeVerified && !compiled.DiscPlan.PublicationApproved &&
            !compiled.FreshResourceAncestor.IndependentLevelSupported,
            "Measured full image agrees with independent tiled reference without borrowing any old arm or runtime approval.");
        var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        Check(ReferenceEquals(binding.SavedLevelPlan, plan) && binding.WizardPlan == null,
            "The existing saved transaction binding owns tiled publication; no separate writer.");
        byte[] canonical = plan.CopyCanonicalDocument();
        var measured = new NativeId65FreshDiscVerification(ExpectedImage, 1253);
        byte[] receipt = plan.BuildReceipt(measured);
        Check(canonical.SequenceEqual(compiled.CopyCanonicalDocument()) && Hash(canonical) == compiled.CanonicalSha256,
            "Candidate canonical bytes are the complete actual compilation input.");
        using (var json = JsonDocument.Parse(receipt))
        using (var expected = JsonDocument.Parse(canonical))
        {
            var root = json.RootElement; var revision = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
            Check(root.GetProperty("variant").GetString() == Variant && root.GetProperty("planSha256").GetString() == plan.PlanSha256 &&
                revision.GetProperty("rawSha256").GetString() == saved.Snapshot.ContentSha256 &&
                revision.GetProperty("manifestSha256").GetString() == saved.Snapshot.ManifestSha256 &&
                revision.GetProperty("canonicalSha256").GetString() == compiled.CanonicalSha256 &&
                JsonElement.DeepEquals(revision.GetProperty("canonical"), expected.RootElement) &&
                lineage.GetProperty("discPlan").GetString() == compiled.DiscPlan.PlanSha256 &&
                lineage.GetProperty("freshPlan").GetString() == compiled.FreshResourceAncestor.PlanSha256 &&
                root.GetProperty("wholeImageInverseRequired").GetBoolean() && root.GetProperty("savedRevisionBound").GetBoolean(),
                "Receipt preserves all ordered tile IDs, settings, materials and actual lineage with an explicit inverse requirement.");
            foreach (string flag in new[] { "nativePersistentSaveOwnerProven", "runtimeVerified", "normalCreateBinApproved",
                "completeNativeDependencyClosureProven", "hqRuntimeProven", "audioRuntimeProven", "peakRuntimeMemoryProven", "checkpointOrForeignStateSupported" })
                Check(!root.GetProperty(flag).GetBoolean(), "Publication recipe does not claim pending owner: " + flag);
        }
        var reopened = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(reopened.PlanSha256 == plan.PlanSha256 && reopened.BuildReceipt(measured).SequenceEqual(receipt),
            "Unchanged actual saved revision recompiles deterministically.");
        await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(plan.Saved, reopened.TiledCompilation!); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(saved, compiled); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { _ = plan.MaterialCompilation; return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(ExpectedImage, 1252)); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(new string('0', 64), 1253)); return Task.CompletedTask; });
        canonical[0] ^= 1; receipt[0] ^= 1;
        Check(plan.CopyCanonicalDocument()[0] != canonical[0] && plan.BuildReceipt(measured)[0] != receipt[0],
            "Canonical and receipt copies cannot mutate the saved candidate owner.");

        // Stable tile IDs are authorship even when they compile to identical native bytes.
        var changedId = SavedTiledDocumentFixture.Document(firstId: 4.ToString("x32"));
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), changedId);
        var idPlan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(idPlan.ExpectedImageSha256 == plan.ExpectedImageSha256 && idPlan.ExpectedChangedRawSectors == 1253 &&
            idPlan.PlanSha256 != plan.PlanSha256 && !idPlan.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument()) &&
            !idPlan.BuildReceipt(measured).SequenceEqual(plan.BuildReceipt(measured)) &&
            idPlan.TiledCompilation!.DiscPlan.CopyBody().SequenceEqual(compiled.DiscPlan.CopyBody()),
            "ID-only authorship keeps the exact BIN/body but changes the saved plan and receipt.");
        await Throws<InvalidDataException>(() => plan.RequireFreshAsync());
        await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(plan.Saved, idPlan.TiledCompilation!); return Task.CompletedTask; });
        File.AppendAllText(idPlan.SavedSnapshot.Path, " \n");
        await Throws<InvalidDataException>(() => idPlan.RequireFreshAsync());
        var reissued = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(reissued.PlanSha256 != idPlan.PlanSha256 && reissued.ExpectedImageSha256 == idPlan.ExpectedImageSha256 &&
            reissued.CopyCanonicalDocument().SequenceEqual(idPlan.CopyCanonicalDocument()) &&
            !reissued.BuildReceipt(measured).SequenceEqual(idPlan.BuildReceipt(measured)),
            "Raw-only change is stale despite equal canonical/native bytes and requires a new saved-revision receipt.");
        NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), SavedTiledDocumentFixture.Document());
        Check(NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentStore.Capture(project).Intent!)
            .SequenceEqual(plan.CopyCanonicalDocument()), "Retained isolated project is restored to the exact reviewed physical fixture, not the equal-image ID alternate.");
        Console.WriteLine($"TILED_SAVED_SOURCE_OK checks={Checks} image={ExpectedImage} sectors=1253 nativeImageWritten=false runtime=false root={project.RootPath} projectId={saved.Snapshot.ProjectId}");
    }

    private static byte[] NeutralData(NativeFreshLevelDocumentIntent document)
    {
        byte[] surface = NativeFlatTileSurfaceLayout.Compile(document.TiledContent!.Tiles.Select(t => t.ToNativeTile()), 0x406080).CopyBundle();
        byte[] data = new byte[surface.Length + 0x784];
        void W(int at, int value) => BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(at), value);
        W(0, 8); surface.CopyTo(data, 8);
        int particles = surface.Length + 12, sound = particles + 0x80;
        W(particles, 0x80); W(particles + 4, 10); W(sound, 0x6F8); W(sound + 0x104, 76);
        return data;
    }
}
