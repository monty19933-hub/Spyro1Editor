using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string LockedChestVariant = "schema5-r03-wizard-grass-locked-chest-p9-n32";
    private readonly NativeFreshSavedLockedChestFamilyCompilation? _lockedChestCompilation;
    internal NativeFreshSavedLockedChestFamilyCompilation? LockedChestCompilation => _lockedChestCompilation;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedLockedChestFamilyCompilation family,
        NativeFreshResearchSourceOwners owners, NativeId65FreshDiscVerification measured)
    {
        Saved = family.Saved; _lockedChestCompilation = family;
        SourceImagePath = owners.ImagePath; SourceCuePath = owners.CuePath;
        _imageIdentity = owners.ImageIdentity!; _cueIdentity = owners.CueIdentity!;
        _sourceCue = owners.CueSnapshot!.ToArray(); SourceCueSha256 = Hash(_sourceCue);
        _outputCue = Encoding.UTF8.GetBytes("FILE \"" + ImageName + "\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected = measured; _canonical = family.CopyCanonicalDocument();
        RequireLockedChestAncestry(Saved, family);

        var disc = family.Disc; var exe = family.Executable;
        var material = family.BaseBody.MixedResourceAncestor!;
        using var document = JsonDocument.Parse(_canonical);
        var snapshot = Saved.Snapshot;
        var revision = new
        {
            projectId = snapshot.ProjectId, root = snapshot.RootPath, path = snapshot.Path,
            rawSha256 = snapshot.ContentSha256, manifestSha256 = snapshot.ManifestSha256,
            canonicalSha256 = Hash(_canonical), canonical = document.RootElement
        };
        var lineage = new
        {
            discPlan = disc.PlanSha256, executablePlan = exe.PlanSha256,
            baseSubdocumentSha256 = Hash(NativeFreshLevelDocumentCodec.SerializeCanonical(family.BaseDocument)),
            baseBodyPlan = family.BaseBody.PlanSha256, baseHostPlan = family.BaseBody.HostAncestor.PlanSha256,
            baseScenePlan = family.BaseBody.HostAncestor.SceneAncestor.PlanSha256,
            mixedResourcePlan = material.PlanSha256, familyCompositionPlan = family.Composition.PlanSha256,
            chestHostPlan = family.Host.PlanSha256, chestResourcePlan = family.Resources.PlanSha256,
            chestScenePlan = family.Scene.PlanSha256, chestBodyPlan = family.Body.PlanSha256,
            placementPlan = family.Placement.PlanSha256,
            wizardInitialClearancePlan = family.WizardClearance.PlanSha256,
            chestInitialLandingClearancePlan = family.Clearance.PlanSha256,
            clearanceScope = "retained-wizard-initial-pose-and-chest-static-landing-only; no trajectory or lifecycle certificate",
            floor = Saved.Document.Base, authoredPlacementCount = Saved.Document.Placements.Count,
            nativePlacedCount = family.Scene.Layout.PlacedCount, nativeNominalCapacity = family.Scene.Layout.DynamicCapacity,
            families = family.Scene.RowSpans.Placements.Select((span, i) => new
            {
                span.EditorId, span.FirstRowIndex, span.RowCount,
                authoredPlacement = document.RootElement.GetProperty("placements")[i]
            }).ToArray(),
            nativeRowOwners = family.Scene.Allocation.Rows.Select((row, i) => new
            {
                nativeIndex = i, actorClass = exe.Rows[i].ActorClassId,
                rowOffset = row.Row.Offset, rowLength = row.Row.ByteLength,
                privateOffset = row.Properties.Offset, privateLength = row.Properties.ByteLength,
                finalRowSha256 = exe.Rows[i].SceneRowSha256, sourceRowSha256 = exe.Rows[i].SourceRowSha256
            }).ToArray(),
            rewardDestinationOverrides = family.Placement.RewardDestinationsAncestor!.Values,
            coordinateWrites = family.Placement.CoordinateWrites,
            materials = material.MaterialSources.Select((s, i) => new
            {
                slot = i, s.Handle, s.SourceImageSha256, s.SourceWadEntry, s.SourceTextureId,
                s.SourceDataSha256, sourceRecordSha256 = s.Layout.RecordSha256
            }).ToArray(),
            material.UploadSha256, material.DataSha256,
            sceneSha256 = family.Placement.SceneSha256, bodySha256 = disc.BodySha256,
            overlaySha256 = disc.OverlaySha256, executableSha256 = disc.ExecutableSha256,
            wadDirectorySha256 = disc.WadDirectorySha256, isoRootSha256 = disc.IsoRootSha256,
            disc.FinalExecutableLba, disc.FinalWadByteLength,
            sourceSelectorRows = exe.Rows, exe.KnownSelectorCount, exe.KnownDeterministicSelectorSubtotal,
            exe.ExperimentalTreasureTarget, experimentalKeyZeroAssumption = true,
            keyZeroContributionProven = false, completeStaticCensusProven = false,
            runtimeDropGuarantee = false, footprintAndTrajectoryProven = false,
            upstreamPendingOwnerScope = "detached-disc producer only; saved Candidate adds guarded experimental publication, not native approval",
            upstreamPendingOwners = disc.PendingOwners
        };
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, variant = LockedChestVariant, saved = revision,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage, expectedImageSha256 = measured.OutputSha256, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueSha256 = Hash(_outputCue),
            wholeImageInverseRequired = true,
            savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
        _receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, variant = LockedChestVariant, planSha256 = PlanSha256, saved = revision,
            SourceImagePath, SourceCuePath, SourceCueSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256, lineage,
            imageFile = ImageName, imageSha256 = measured.OutputSha256, imageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueFile = CueName, cueSha256 = Hash(_outputCue), receiptFile = ReceiptName,
            savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            hqRuntimeProven = false, audioRuntimeProven = false, peakRuntimeMemoryProven = false,
            checkpointOrForeignStateSupported = false, publicationApproved = false,
            wholeImageInverseRequired = true,
            keyZeroContributionProven = false, completeStaticCensusProven = false,
            runtimeDropGuarantee = false, experimentalKeyZeroAssumption = true
        }, new JsonSerializerOptions { WriteIndented = true });
        if (_receipt.Length is < 1 or > 65536)
            throw new InvalidDataException("Complete saved chest-family receipt exceeds companion bound; no evidence is truncated.");
    }

    private static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureLockedChestAsync(string originalBin,
        string sourceCue, NativeFreshSavedLevelDocumentReadContext saved, CancellationToken token)
    {
        // Validate the complete schema5 content before even normalizing source
        // paths. Neither a tiled document nor a projected Wizard is admitted.
        _ = NativeFreshSavedLockedChestFamilyCompilation.ProjectBaseDocument(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin), Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result = null; Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
            var family = await NativeFreshSavedLockedChestFamilyCompilation.CaptureAsync(owners.ImagePath, saved, token);
            owners.Bin!.Position = 0;
            var measured = NativeId65FreshDiscStreaming.Measure(family.Disc, owners.Bin, token);
            result = new(family, owners, measured);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, saved, primary);
        token.ThrowIfCancellationRequested();
        return result!;
    }

    internal void RequireLockedChestAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshSavedLockedChestFamilyCompilation family)
    {
        if (!ReferenceEquals(saved, Saved) || family is null || !ReferenceEquals(family, _lockedChestCompilation) ||
            _material != null || _mixedDisc != null || _mixedClearance != null ||
            _familyCompilation != null || _tiledCompilation != null || _tiledFamilyCompilation != null)
            throw new InvalidDataException("Saved chest Candidate requires its actual complete saved family compilation, not an equal or projected arm.");
        family.RequireAncestry(saved);
        if (family.SourceImagePath != SourceImagePath ||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Saved chest Candidate source path or complete canonical document changed.");
    }
}
