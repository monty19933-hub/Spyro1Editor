using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string TiledMaterialVariant = "schema4-two-static-material-flat-tiles-p0-n16-data1000";
    private const string TiledSelectedMaterialVariant = "schema4-selected-static-material-flat-tiles-p0-n16-data1000";

    // A distinct serialization branch keeps the existing Base-only plan and
    // receipt byte-for-byte unchanged. The actual compilation, not a caller
    // label or a projected Base document, selects this branch.
    private (string PlanSha256, byte[] Receipt) BuildTiledMaterialEvidence(
        NativeId65TiledDocumentCompilation tiled, NativeId65TiledMaterialPlan material,
        NativeId65FreshDiscVerification measured)
    {
        var disc = tiled.DiscPlan; var body = disc.BodyAncestor; var fresh = tiled.FreshResourceAncestor;
        var surface = material.SurfaceAncestor;
        var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(tiled.Document);
        string variant = material.MaterialSources.Count == 2 ? TiledMaterialVariant : TiledSelectedMaterialVariant;
        using var document = JsonDocument.Parse(_canonical);
        var snapshot = Saved.Snapshot;
        var revision = new
        {
            projectId = snapshot.ProjectId, root = snapshot.RootPath, path = snapshot.Path,
            rawSha256 = snapshot.ContentSha256, manifestSha256 = snapshot.ManifestSha256,
            canonicalSha256 = tiled.CanonicalSha256, canonical = document.RootElement
        };
        var lineage = new
        {
            discPlan = disc.PlanSha256, bodyPlan = body.PlanSha256, stagingPlan = body.StagingPlanSha256,
            materialPlan = material.PlanSha256,
            freshPlan = fresh.PlanSha256, resourceChainPlan = fresh.ResourceChainAncestor.PlanSha256,
            surface.DocumentSha256, surface.BundleSha256,
            material.UpstreamPlanSha256, material.CanonicalDocumentSha256,
            tileCount = tiled.Document.TiledContent!.Tiles.Count,
            spawn = fresh.SceneLayout.Spawn, placedCount = fresh.SceneLayout.PlacedCount,
            nativeNominalCapacity = fresh.SceneLayout.NativeNominalCapacity,
            body.ResourceProfile, body.DataUsedByteLength, body.DataSectorByteLength,
            body.ModelRuntimeAddress, body.SceneRuntimeAddress, body.KnownSceneEndAddress,
            body.Descriptors, body.Owners, body.PhaseOverlaps,
            Selections = prepared.Selections.Select(s => new { s.SourceVariant, s.SourceWadEntry, s.TextureId }).ToArray(),
            material.SourceHandles, material.OrderedFaceMaterials,
            materials = material.MaterialSources.Select((s, i) => new
            {
                slot = i, s.Handle, s.SourceImageSha256, s.SourceWadEntry, s.SourceTextureId,
                s.SourceDataSha256, sourceRecordSha256 = s.Layout.RecordSha256,
                relocatedRecordSha256 = material.Allocation.Records[i].RecordSha256
            }).ToArray(),
            material.NativeClip, material.AdditionalPacketBytesPerPass,
            material.Allocation.Allocations, material.Allocation.ContainedAliases, material.Evidence,
            BeforeUploadSha256 = fresh.UploadSha256, BeforeDataSha256 = fresh.DataSha256,
            material.UploadSha256, material.DataSha256, material.ModelSha256, material.SceneSha256,
            bodySha256 = Hash(disc.CopyBody()), overlaySha256 = Hash(disc.CopyOverlay()),
            executableSha256 = Hash(disc.CopyExecutable()), wadDirectorySha256 = Hash(disc.CopyWadDirectory()),
            isoRootSha256 = Hash(disc.CopyIsoRootDirectory()), disc.FinalExecutableLba, disc.FinalWadByteLength,
            upstreamPendingOwners = disc.PendingOwners
        };
        string planSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, variant, saved = revision,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage, expectedImageSha256 = measured.OutputSha256, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueSha256 = Hash(_outputCue),
            wholeImageInverseRequired = true, savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
        byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, variant, planSha256, saved = revision,
            SourceImagePath, SourceCuePath, SourceCueSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256, lineage,
            imageFile = ImageName, imageSha256 = measured.OutputSha256, imageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueFile = CueName, cueSha256 = Hash(_outputCue), receiptFile = ReceiptName,
            wholeImageInverseRequired = true, savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            hqRuntimeProven = false, audioRuntimeProven = false, peakRuntimeMemoryProven = false,
            checkpointOrForeignStateSupported = false
        // The new selected variant preserves every field and array but omits
        // cosmetic whitespace. Keep the established two-material receipt's
        // exact formatting/bytes, including its existing companion budget.
        }, new JsonSerializerOptions { WriteIndented = material.MaterialSources.Count == 2 });
        if (receipt.Length is < 1 or > 65536)
            throw new InvalidDataException($"Complete saved-tiled material receipt is {receipt.Length} bytes and exceeds the 65536-byte companion bound; no evidence is truncated.");
        return (planSha256, receipt);
    }
}
