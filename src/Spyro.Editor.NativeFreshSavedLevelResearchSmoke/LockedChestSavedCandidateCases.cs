using System.Text.Json;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using static Cases;

internal static class LockedChestSavedCandidateCases
{
    internal static async Task FreeAsync()
    {
        var document = SavedLockedChestCompilationCases.Document();
        var (_, saved) = Save("chest-candidate-preflight", document);
        var error = await Throws<IOException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(Missing, Missing, saved.Snapshot));
        Check(Leaves(error)[0].Message.StartsWith("Textured research source path is missing", StringComparison.Ordinal),
            "Exact complete saved R03 reaches the existing source authentication gate, never an old projected arm.");
        foreach (var (name, invalid) in SavedLockedChestCompilationCases.Unsupported().Where(v => v.Document.SchemaVersion == 5))
        {
            var (_, bad) = Save("chest-candidate-unsupported-" + name, invalid);
            await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", bad.Snapshot));
            Check(NativeFreshLevelDocumentCodec.SerializeCanonical(bad.Document).SequenceEqual(
                NativeFreshLevelDocumentCodec.SerializeCanonical(invalid)), "Unsupported complete saved document remains intact.");
        }
        foreach (string owner in new[] { "raw", "manifest" })
        {
            var (project, stale) = Save("chest-candidate-stale-" + owner, document);
            File.AppendAllText(owner == "raw" ? stale.Snapshot.Path : project.ManifestPath, " ");
            await Throws<InvalidDataException>(() => NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync("\0", "\0", stale.Snapshot));
        }
    }

    internal static async Task SourceAsync(string bin, string cue, string? exactCandidateBin)
    {
        var (project, saved) = Save("chest-saved-publication", SavedLockedChestCompilationCases.Document());
        var plan = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, saved.Snapshot);
        var chest = plan.LockedChestCompilation!;
        plan.RequireLockedChestAncestry(plan.Saved, chest);
        await SavedLockedChestCompilationCases.VerifySourceAsync(bin, chest);
        Check(ReferenceEquals(plan.SavedSnapshot, saved.Snapshot) && ReferenceEquals(chest.Document, saved.Document) &&
            plan.MixedDisc == null && plan.MixedClearance == null && plan.FamilyCompilation == null && plan.TiledCompilation == null &&
            plan.ExpectedImageSha256 == SavedLockedChestCompilationCases.ExpectedImage && plan.ExpectedChangedRawSectors == 1297 && !plan.RuntimeVerified,
            "Actual saved chest is an exclusive physical arm and preserves observed R03 native bytes, not P2/P0 fallthrough.");
        var binding = NativeId65FreshResearchPublicationBinding.FromSavedLevel(plan);
        Check(ReferenceEquals(binding.SavedLevelPlan, plan) && binding.WizardPlan == null,
            "Experimental chest uses the existing saved publisher, not a separate writer.");
        var measured = new NativeId65FreshDiscVerification(plan.ExpectedImageSha256, 1297);
        byte[] canonical = plan.CopyCanonicalDocument(), receipt = plan.BuildReceipt(measured);
        using (var json = JsonDocument.Parse(receipt))
        {
            var root = json.RootElement; var revision = root.GetProperty("saved"); var lineage = root.GetProperty("lineage");
            Check(root.GetProperty("variant").GetString() == "schema5-r03-wizard-grass-locked-chest-p9-n32" &&
                root.GetProperty("wholeImageInverseRequired").GetBoolean() &&
                revision.GetProperty("rawSha256").GetString() == saved.Snapshot.ContentSha256 &&
                revision.GetProperty("manifestSha256").GetString() == saved.Snapshot.ManifestSha256 &&
                revision.GetProperty("canonicalSha256").GetString() == Hash(canonical), "Complete saved revision and enforced inverse scope.");
            using var actual = JsonDocument.Parse(canonical);
            Check(JsonElement.DeepEquals(revision.GetProperty("canonical"), actual.RootElement) &&
                lineage.GetProperty("authoredPlacementCount").GetInt32() == 3 && lineage.GetProperty("nativePlacedCount").GetInt32() == 9 &&
                lineage.GetProperty("nativeNominalCapacity").GetInt32() == 32,
                "Receipt retains all content/placements and distinguishes three editor entries from nine native rows.");
            var families = lineage.GetProperty("families"); var authored = actual.RootElement.GetProperty("placements");
            Check(families.GetArrayLength() == 3 && Enumerable.Range(0, 3).All(i =>
                JsonElement.DeepEquals(families[i].GetProperty("authoredPlacement"), authored[i]) &&
                families[i].GetProperty("EditorId").GetString() == saved.Document.Placements[i].EditorId &&
                families[i].GetProperty("FirstRowIndex").GetInt32() == i && families[i].GetProperty("RowCount").GetInt32() == (i == 2 ? 7 : 1)),
                "Every actual authored ID maps to its full contiguous native row span.");
            var owners = lineage.GetProperty("nativeRowOwners");
            Check(owners.GetArrayLength() == 9 && Enumerable.Range(0, 9).All(i =>
                owners[i].GetProperty("nativeIndex").GetInt32() == i &&
                owners[i].GetProperty("actorClass").GetInt32() == chest.Executable.Rows[i].ActorClassId &&
                owners[i].GetProperty("rowOffset").GetInt64() == chest.Scene.Allocation.Rows[i].Row.Offset &&
                owners[i].GetProperty("rowLength").GetInt64() == 0x58 &&
                owners[i].GetProperty("privateOffset").GetInt64() == chest.Scene.Allocation.Rows[i].Properties.Offset &&
                owners[i].GetProperty("privateLength").GetInt64() == chest.Scene.Allocation.Rows[i].Properties.ByteLength &&
                owners[i].GetProperty("finalRowSha256").GetString() == chest.Executable.Rows[i].SceneRowSha256 &&
                owners[i].GetProperty("sourceRowSha256").GetString() == chest.Executable.Rows[i].SourceRowSha256),
                "Every actual native row index/class, final/source row hash and complete row/private extent is retained.");
            Check(lineage.GetProperty("chestResourcePlan").GetString() == chest.Resources.PlanSha256 &&
                lineage.GetProperty("placementPlan").GetString() == chest.Placement.PlanSha256 &&
                lineage.GetProperty("ExperimentalTreasureTarget").GetInt32() == 20 &&
                lineage.GetProperty("rewardDestinationOverrides").GetArrayLength() == 1 &&
                JsonElement.DeepEquals(lineage.GetProperty("rewardDestinationOverrides"), JsonSerializer.SerializeToElement(
                    ((NativeFreshLockedChestFamilyPlacementIntent)saved.Document.Placements[2]).RewardDestinations!.Values)),
                "Actual resource/placement/experimental target and complete override owner are retained.");
            foreach (string flag in new[] { "runtimeVerified", "normalCreateBinApproved", "nativePersistentSaveOwnerProven",
                "completeNativeDependencyClosureProven", "publicationApproved", "keyZeroContributionProven", "completeStaticCensusProven", "runtimeDropGuarantee" })
                Check(!root.GetProperty(flag).GetBoolean(), "No saved experiment promotes pending native acceptance: " + flag);
            Check(root.GetProperty("experimentalKeyZeroAssumption").GetBoolean(), "Key-zero remains an explicit premise.");
        }
        var repeated = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(repeated.PlanSha256 == plan.PlanSha256 && repeated.BuildReceipt(measured).SequenceEqual(receipt),
            "Unchanged saved revision reissues deterministic image/receipt without making equal owners interchangeable.");
        await Throws<InvalidDataException>(() => { plan.RequireLockedChestAncestry(plan.Saved, repeated.LockedChestCompilation!); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.RequireLockedChestAncestry(saved, chest); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.RequireFamilyAncestry(plan.Saved, null!); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.RequireMixedAncestry(plan.Saved, null!, null!); return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { _ = plan.MaterialCompilation; return Task.CompletedTask; });
        await Throws<InvalidDataException>(() => { plan.BuildReceipt(new(plan.ExpectedImageSha256, 1292)); return Task.CompletedTask; });
        canonical[0] ^= 1; receipt[0] ^= 1;
        Check(plan.CopyCanonicalDocument()[0] != canonical[0] && plan.BuildReceipt(measured)[0] != receipt[0], "Canonical and receipt copies are detached.");
        if (exactCandidateBin != null) await SavedLockedChestInverseCases.SourceAsync(plan, exactCandidateBin);

        File.AppendAllText(saved.Snapshot.Path, " \n");
        await Throws<InvalidDataException>(() => plan.RequireFreshAsync());
        var rawChanged = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, NativeFreshLevelDocumentStore.Capture(project));
        Check(rawChanged.PlanSha256 != plan.PlanSha256 && rawChanged.ExpectedImageSha256 == plan.ExpectedImageSha256 &&
            rawChanged.CopyCanonicalDocument().SequenceEqual(plan.CopyCanonicalDocument()),
            "Observed raw-only revision keeps native bytes but requires a new saved binding.");
        var original = saved.Document;
        var wizard = (NativeFreshWizardRouteIntent)original.Placements[0];
        var idOnly = original.WithPlacements([NativeFreshWizardRouteIntent.Create(new string('a', 32), wizard.OriginX, wizard.OriginY, wizard.OriginZ,
            wizard.EndX, wizard.EndY), original.Placements[1], original.Placements[2]]);
        var next = NativeFreshLevelDocumentStore.Save(NativeFreshLevelDocumentStore.Capture(project), idOnly);
        var reidentified = await NativeFreshSavedLevelResearchCandidatePlan.CaptureSavedAsync(bin, cue, next);
        Check(reidentified.ExpectedImageSha256 == plan.ExpectedImageSha256 && reidentified.PlanSha256 != rawChanged.PlanSha256 &&
            reidentified.LockedChestCompilation!.Scene.RowSpans.Placements[0].EditorId == new string('a', 32),
            "An actual authoring identity change preserves native bytes but remains visible in the full saved compilation.");
        byte[] idOnlyCanonical = NativeFreshLevelDocumentCodec.SerializeCanonical(idOnly);
        using (var idOnlyDocument = JsonDocument.Parse(idOnlyCanonical))
        using (var idOnlyReceipt = JsonDocument.Parse(reidentified.BuildReceipt(measured)))
        {
            var revision = idOnlyReceipt.RootElement.GetProperty("saved");
            var families = idOnlyReceipt.RootElement.GetProperty("lineage").GetProperty("families");
            var authored = idOnlyDocument.RootElement.GetProperty("placements");
            Check(reidentified.CopyCanonicalDocument().SequenceEqual(idOnlyCanonical) &&
                !idOnlyCanonical.SequenceEqual(plan.CopyCanonicalDocument()) &&
                revision.GetProperty("canonicalSha256").GetString() == Hash(idOnlyCanonical) &&
                JsonElement.DeepEquals(revision.GetProperty("canonical"), idOnlyDocument.RootElement),
                "The ID-only receipt retains the complete newly authored canonical document and hash, not the prior saved revision.");
            Check(families.GetArrayLength() == 3 && Enumerable.Range(0, 3).All(i =>
                families[i].GetProperty("EditorId").GetString() == idOnly.Placements[i].EditorId &&
                JsonElement.DeepEquals(families[i].GetProperty("authoredPlacement"), authored[i]) &&
                families[i].GetProperty("FirstRowIndex").GetInt32() == i &&
                families[i].GetProperty("RowCount").GetInt32() == (i == 2 ? 7 : 1)),
                "The ID-only receipt maps the changed Wizard ID and unchanged Grass/chest IDs to their actual complete authored placements and row spans.");
        }
        File.AppendAllText(project.ManifestPath, " ");
        await Throws<InvalidDataException>(() => reidentified.RequireFreshAsync());
        Console.WriteLine($"LOCKED_CHEST_SAVED_CANDIDATE_SOURCE_OK checks={Checks} image={plan.ExpectedImageSha256} runtime=false nativeOutputWritten=false root={project.RootPath}");
    }
}
