using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class TiledMaterialSavedCases
{
    internal const string ExpectedImage = "462c992f80f512f0b6be781b1a79e707e37a0e18f5e5e47356cd332ef61b2ba8";
    internal const string Variant = "schema4-two-static-material-flat-tiles-p0-n16-data1000";

    internal static async Task Free()
    {
        var document = SavedTiledMaterialDocumentFixture.Document();
        Check(document.SchemaVersion == 4 && document.LegacyContent == null && document.Placements.Count == 0 &&
            document.TiledContent!.Tiles.Select(t => t.EditorId).SequenceEqual(Enumerable.Range(1, 3).Select(i => i.ToString("x32"))),
            "Material fixture retains all three real tiles and stable IDs without a legacy Base.");
        Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(null) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(SavedTiledDocumentFixture.Document()) &&
            !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(Cases.Document()),
            "New neutral predicate neither adopts Base-only nor legacy material profiles.");
        foreach (var valid in new[] { document, SavedTiledMaterialDocumentFixture.Document([0, 1, 255, 1, 0, 255]),
            SavedTiledMaterialDocumentFixture.Document([255, 255, 0, 1, 0, 1]),
            SavedTiledMaterialDocumentFixture.BoundaryDocument(56), SavedTiledMaterialDocumentFixture.BoundaryDocument(58) })
        {
            Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(valid) &&
                !NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(valid),
                "Whole supported two-source material intent has a separate neutral predicate; old Base eligibility is unchanged.");
            var (_, saved) = Save("tiled-material-supported-preflight", valid);
            var error = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
            Check(Leaves(error)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
                "Supported complete saved material intent reaches source authentication through the existing single public factory.");
            await Throws<OperationCanceledException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(
                "\0", "\0", saved.Snapshot, new(true)));
        }
        foreach (var (name, invalid) in SavedTiledMaterialDocumentFixture.Unsupported())
        {
            Check(!NativeFreshSavedLevelResearchEligibility.IsSupportedTiledMaterialProfile(invalid),
                "Neutral material predicate inspects the complete intent: " + name);
            var (_, saved) = Save("tiled-material-unsupported-" + name, invalid);
            var error = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
            Check(!Leaves(error).Any(e => e is ArgumentException or FileNotFoundException or DirectoryNotFoundException),
                "Unsupported saved intent refuses before malformed source paths or projection: " + name);
        }
        foreach (var (zCell, expected) in new[] { (56, 0xFFC), (58, 0x1000), (60, 0x1004) })
        {
            var boundary = SavedTiledMaterialDocumentFixture.BoundaryDocument(zCell);
            int actual = NativeFlatTileSurfaceLayout.Compile(boundary.TiledContent!.Tiles.Select(t => t.ToNativeTile()), 0x406080)
                .CopyBundle().Length + NativeId65NormalComponentLayout.NonSurfaceByteLength + 2 * 184;
            Check(actual == expected, "Actual geometry and both complete material records establish the allocation boundary.");
        }
        foreach (string owner in new[] { "raw", "manifest" })
        {
            var (project, saved) = Save("tiled-material-stale-" + owner, document);
            File.AppendAllText(owner == "raw" ? saved.Snapshot.Path : project.ManifestPath, " ");
            var error = await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", saved.Snapshot));
            Check(!Leaves(error).Any(e => e is ArgumentException), "Actual saved raw/manifest staleness refuses before source path normalization.");
        }
        Check(NativeFreshSavedLevelResearchEligibility.IsSupportedTiledProfile(SavedTiledDocumentFixture.Document()),
            "The existing all-Base tiled arm remains eligible; zero retail choices are not newly invalid.");
        Console.WriteLine("TILED_MATERIAL_SAVED_FREE_OK originalOpened=false nativeImageWritten=false");
    }

    internal static async Task Source(string bin, string cue)
    {
        // Full-image references independently frozen by physical oracle352,
        // before production material Body/Disc compilation. No output BIN here.
        var fixtures = new[]
        {
            (Name: "six-textured", Faces: new[] { 0, 1, 0, 1, 0, 1 }, Image: ExpectedImage,
                Data: "ba244a6a9b496aa5507721d4817f32edeed337d72ac3700ed12021bbddd687aa", Packet: 72),
            (Name: "mixed-base", Faces: new[] { 0, 1, 255, 1, 0, 255 },
                Image: "af46a6a0861af60a92669128512a94b6fca115ab12da699e5c5e12e5354beb22",
                Data: "c0ac16f6e8e063dece5e9386649452f3d507693db656f996099350adb492bfa2", Packet: 48),
            (Name: "changed-third", Faces: new[] { 0, 1, 0, 1, 1, 0 },
                Image: "bb1c2263afe49a9a603c71981f88e1c750e982bc650156c81bb42ba550ec7578",
                Data: "03d9dc3a28685abe159fd0efb650e40bcd47dd992e8eaff31a70d05f20b1f853", Packet: 72)
        };
        foreach (var fixture in fixtures)
        {
            var (project, saved) = Save("tiled-material-saved-" + fixture.Name, SavedTiledMaterialDocumentFixture.Document(fixture.Faces));
            byte[] manifestBefore = File.ReadAllBytes(project.ManifestPath), rawBefore = File.ReadAllBytes(saved.Snapshot.Path);
            var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot);
            var compiled = plan.TiledCompilation ?? throw new InvalidDataException("Saved material candidate has no actual tiled compilation.");
            var material = compiled.TiledMaterialAncestor ?? throw new InvalidDataException("Saved material candidate erased its actual material issuer.");
            var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
            plan.RequireTiledAncestry(plan.Saved, compiled);
            Check(ReferenceEquals(plan.SavedSnapshot, saved.Snapshot) && ReferenceEquals(compiled.Document, saved.Document) &&
                ReferenceEquals(material.Document, saved.Document) && ReferenceEquals(material.FreshAncestor, fresh) &&
                ReferenceEquals(body.TiledMaterialAncestor, material) && ReferenceEquals(body.TiledSceneAncestor, fresh) &&
                body.ResourceProfile == NativeId65NormalResourceProfile.DirectLqTwoStaticMaterialsFlatTiles &&
                body.UpstreamPlanSha256 == material.PlanSha256,
                "Saved material candidate retains the same actual complete document, compilation, material instance and issued body chain.");
            Check(plan.MixedDisc == null && plan.FamilyCompilation == null && plan.ExpectedImageSha256 == fixture.Image &&
                plan.ExpectedImageByteLength == 661547040 && plan.ExpectedChangedRawSectors == 1253 &&
                !plan.RuntimeVerified && compiled.DiscPlan.SourceCertified && !compiled.DiscPlan.RuntimeVerified &&
                !compiled.DiscPlan.PublicationApproved && !material.RuntimeVerified && !material.PublicationApproved,
                "Actual saved candidate measurement matches the independent complete material image without borrowing old arm/runtime approval.");
            var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
            Check(ReferenceEquals(binding.SavedLevelPlan, plan) && binding.WizardPlan == null,
                "The same saved publication transaction owns the new variant; no separate writer or role is introduced.");
            var measured = new NativeId65FreshDiscVerification(fixture.Image, 1253);
            byte[] canonical = plan.CopyCanonicalDocument(), receipt = plan.BuildReceipt(measured);
            Check(canonical.SequenceEqual(compiled.CopyCanonicalDocument()) && Hash(canonical) == compiled.CanonicalSha256 &&
                receipt.Length is > 0 and <= 65536 && material.DataSha256 == fixture.Data &&
                material.UploadSha256 == "2ab548f540fbfadddc38d96e70a9633a1c6f37c37ee2d1a896919ac30f929333" &&
                fresh.UploadSha256 != material.UploadSha256 && fresh.DataSha256 != material.DataSha256,
                "The receipt must bind final material outputs, distinguish the intermediate fresh bytes, and retain complete bounded evidence.");
            Receipt(plan, compiled, receipt, fixture.Faces, fixture.Packet);
            await Throws<InvalidDataException>(() => { _ = plan.MaterialCompilation; return Task.CompletedTask; });
            await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(saved, compiled); return Task.CompletedTask; });
            await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(fixture.Image, 1252)); return Task.CompletedTask; });
            await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(new string('0', 64), 1253)); return Task.CompletedTask; });
            canonical[0] ^= 1; receipt[0] ^= 1;
            Check(plan.CopyCanonicalDocument()[0] != canonical[0] && plan.BuildReceipt(measured)[0] != receipt[0],
                "Returned complete canonical/receipt snapshots cannot mutate their saved owner.");
            Check(File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifestBefore) && File.ReadAllBytes(saved.Snapshot.Path).SequenceEqual(rawBefore),
                "Source compilation does not alter the actual saved document or project manifest.");

            if (fixture.Name == "six-textured")
            {
                var reopened = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
                Check(reopened.PlanSha256 == plan.PlanSha256 && reopened.BuildReceipt(measured).SequenceEqual(plan.BuildReceipt(measured)) &&
                    !ReferenceEquals(reopened.TiledCompilation, compiled),
                    "Unchanged saved material revision recompiles deterministically into a separate actual compilation.");
                await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(plan.Saved, reopened.TiledCompilation!); return Task.CompletedTask; });
                await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(reopened.Saved, compiled); return Task.CompletedTask; });

                var idOnly = SavedTiledMaterialDocumentFixture.Document(firstId: 4.ToString("x32"));
                NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), idOnly);
                var idPlan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
                Check(idPlan.ExpectedImageSha256 == ExpectedImage && idPlan.ExpectedChangedRawSectors == 1253 &&
                    idPlan.PlanSha256 != plan.PlanSha256 && !idPlan.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument()) &&
                    !idPlan.BuildReceipt(measured).SequenceEqual(plan.BuildReceipt(measured)) &&
                    idPlan.TiledCompilation!.DiscPlan.CopyBody().SequenceEqual(compiled.DiscPlan.CopyBody()),
                    "Editor-ID-only authorship keeps the exact native image but changes the full saved plan and receipt.");
                await Throws<InvalidDataException>(() => plan.RequireFreshAsync());
                await Throws<InvalidDataException>(() => { plan.RequireTiledAncestry(plan.Saved, idPlan.TiledCompilation!); return Task.CompletedTask; });
                File.AppendAllText(idPlan.SavedSnapshot.Path, " \n");
                await Throws<InvalidDataException>(() => idPlan.RequireFreshAsync());
                var rawReissue = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
                Check(rawReissue.ExpectedImageSha256 == ExpectedImage && rawReissue.PlanSha256 != idPlan.PlanSha256 &&
                    rawReissue.CopyCanonicalDocument().SequenceEqual(idPlan.CopyCanonicalDocument()) &&
                    !rawReissue.BuildReceipt(measured).SequenceEqual(idPlan.BuildReceipt(measured)),
                    "Raw-only staleness is not erased by equal canonical/native bytes and requires a newly issued receipt.");
                NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), SavedTiledMaterialDocumentFixture.Document());
                Check(NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentStore.Capture(project).Intent!)
                    .SequenceEqual(plan.CopyCanonicalDocument()) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifestBefore),
                    "Retained isolated physical fixture restores the exact reviewed IDs/materials, with original manifest unchanged.");
                Console.WriteLine($"TILED_MATERIAL_SAVED_PHYSICAL_FIXTURE root={project.RootPath} projectId={saved.Snapshot.ProjectId} image={ExpectedImage} restoredExactCanonical=true");
            }
            Console.WriteLine($"TILED_MATERIAL_SAVED_CASE_OK name={fixture.Name} image={fixture.Image} sectors=1253 plan={plan.PlanSha256} nativeImageWritten=false");
        }
        Console.WriteLine($"TILED_MATERIAL_SAVED_SOURCE_OK checks={Checks} nativeImageWritten=false runtime=false");
    }

    private static void Receipt(NativeFreshSavedLevelResearchCandidatePlan plan, NativeId65TiledDocumentCompilation compiled,
        byte[] receipt, int[] faces, int packetBytes)
    {
        var material = compiled.TiledMaterialAncestor!; var fresh = compiled.FreshResourceAncestor; var body = compiled.DiscPlan.BodyAncestor;
        using var json = JsonDocument.Parse(receipt);
        using var canonical = JsonDocument.Parse(plan.CopyCanonicalDocument());
        var root = json.RootElement; var saved = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
        Check(root.GetProperty("variant").GetString() == Variant && root.GetProperty("planSha256").GetString() == plan.PlanSha256 &&
            saved.GetProperty("rawSha256").GetString() == plan.SavedSnapshot.ContentSha256 &&
            saved.GetProperty("manifestSha256").GetString() == plan.SavedSnapshot.ManifestSha256 &&
            saved.GetProperty("canonicalSha256").GetString() == compiled.CanonicalSha256 &&
            saved.GetProperty("projectId").GetString() == plan.SavedSnapshot.ProjectId &&
            saved.GetProperty("root").GetString() == plan.SavedSnapshot.RootPath && saved.GetProperty("path").GetString() == plan.SavedSnapshot.Path &&
            JsonElement.DeepEquals(saved.GetProperty("canonical"), canonical.RootElement) &&
            root.GetProperty("wholeImageInverseRequired").GetBoolean() && root.GetProperty("savedRevisionBound").GetBoolean() &&
            !root.TryGetProperty("publicationGuardPassed", out _),
            "New variant records whole canonical intent and separate actual raw/manifest owners; inverse is a requirement, not claimed future success.");
        Check(root.GetProperty("sourceImageSha256").GetString() == Original && root.GetProperty("SourceImagePath").GetString() == plan.SourceImagePath &&
            root.GetProperty("SourceCuePath").GetString() == plan.SourceCuePath && root.GetProperty("SourceCueSha256").GetString() == plan.SourceCueSha256 &&
            root.GetProperty("imageSha256").GetString() == plan.ExpectedImageSha256 && root.GetProperty("imageByteLength").GetInt64() == 661547040 &&
            root.GetProperty("changedRawSectors").GetInt32() == 1253 && root.GetProperty("cueSha256").GetString() == Hash(plan.CopyCueBytes()),
            "Receipt binds exact original BIN/CUE owners and measured full output, not caller-supplied hash metadata.");
        Check(lineage.GetProperty("discPlan").GetString() == compiled.DiscPlan.PlanSha256 &&
            lineage.GetProperty("bodyPlan").GetString() == body.PlanSha256 && lineage.GetProperty("stagingPlan").GetString() == body.StagingPlanSha256 &&
            lineage.GetProperty("freshPlan").GetString() == fresh.PlanSha256 && lineage.GetProperty("materialPlan").GetString() == material.PlanSha256 &&
            lineage.GetProperty("DocumentSha256").GetString() == compiled.CanonicalSha256 &&
            lineage.GetProperty("ResourceProfile").GetInt32() == 3 && lineage.GetProperty("DataUsedByteLength").GetInt32() == 0xBC8 &&
            lineage.GetProperty("DataSectorByteLength").GetInt32() == 0x1000,
            "Actual material/source/body/disc lineage and measured final used length cannot be replaced with unresolved fresh metadata.");
        Check(lineage.GetProperty("UploadSha256").GetString() == material.UploadSha256 &&
            lineage.GetProperty("DataSha256").GetString() == material.DataSha256 &&
            lineage.GetProperty("ModelSha256").GetString() == material.ModelSha256 &&
            lineage.GetProperty("SceneSha256").GetString() == material.SceneSha256 &&
            lineage.GetProperty("BeforeUploadSha256").GetString() == fresh.UploadSha256 &&
            lineage.GetProperty("BeforeDataSha256").GetString() == fresh.DataSha256 &&
            lineage.GetProperty("NativeClip").GetInt32() == 0x8000 && lineage.GetProperty("AdditionalPacketBytesPerPass").GetInt32() == packetBytes &&
            lineage.GetProperty("OrderedFaceMaterials").EnumerateArray().Select(v => v.GetInt32()).SequenceEqual(faces) &&
            lineage.GetProperty("SourceHandles").EnumerateArray().Select(v => v.GetString()).SequenceEqual(material.SourceHandles),
            "Receipt preserves final four-domain hashes, distinct intermediate hashes, exact clip and all six ordered FF/material bindings.");
        var selections = lineage.GetProperty("Selections").EnumerateArray().ToArray();
        var sources = lineage.GetProperty("materials").EnumerateArray().ToArray();
        Check(selections.Length == 2 && sources.Length == 2, "Exactly two first-occurrence source slots are recorded, without truncation or deduplication by hash.");
        for (int slot = 0; slot < 2; slot++)
        {
            var actual = material.MaterialSources[slot]; var selection = selections[slot]; var source = sources[slot];
            Check(selection.GetProperty("SourceVariant").GetString() == Cases.Variant &&
                selection.GetProperty("SourceWadEntry").GetInt32() == actual.SourceWadEntry && selection.GetProperty("TextureId").GetInt32() == 0 &&
                source.GetProperty("slot").GetInt32() == slot && source.GetProperty("Handle").GetString() == actual.Handle &&
                source.GetProperty("SourceImageSha256").GetString() == Original &&
                source.GetProperty("SourceWadEntry").GetInt32() == actual.SourceWadEntry && source.GetProperty("SourceTextureId").GetInt32() == 0 &&
                source.GetProperty("SourceDataSha256").GetString() == actual.SourceDataSha256 &&
                source.GetProperty("sourceRecordSha256").GetString() == actual.Layout.RecordSha256 &&
                source.GetProperty("relocatedRecordSha256").GetString() == material.Allocation.Records[slot].RecordSha256,
                "Each ordered receipt source slot binds original descriptor, actual source handle and complete original/relocated record.");
        }
        Check(JsonElement.DeepEquals(lineage.GetProperty("Allocations"), JsonSerializer.SerializeToElement(material.Allocation.Allocations)) &&
            JsonElement.DeepEquals(lineage.GetProperty("ContainedAliases"), JsonSerializer.SerializeToElement(material.Allocation.ContainedAliases)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Evidence"), JsonSerializer.SerializeToElement(material.Evidence)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Descriptors"), JsonSerializer.SerializeToElement(body.Descriptors)) &&
            JsonElement.DeepEquals(lineage.GetProperty("Owners"), JsonSerializer.SerializeToElement(body.Owners)) &&
            JsonElement.DeepEquals(lineage.GetProperty("PhaseOverlaps"), JsonSerializer.SerializeToElement(body.PhaseOverlaps)) &&
            lineage.GetProperty("bodySha256").GetString() == Hash(compiled.DiscPlan.CopyBody()),
            "Receipt retains every actual allocation, alias, consumer evidence, body descriptor, complete owner and phase-overlap entry.");
        foreach (string flag in new[] { "nativePersistentSaveOwnerProven", "runtimeVerified", "normalCreateBinApproved",
            "completeNativeDependencyClosureProven", "hqRuntimeProven", "audioRuntimeProven", "peakRuntimeMemoryProven", "checkpointOrForeignStateSupported" })
            Check(!root.GetProperty(flag).GetBoolean(), "Saved material recipe does not claim pending owner: " + flag);
    }
}
