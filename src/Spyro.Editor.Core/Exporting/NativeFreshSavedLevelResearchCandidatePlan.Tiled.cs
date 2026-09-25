using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string TiledVariant = "schema4-base-color-flat-tiles-p0-n16-data1000";
    private readonly NativeId65TiledDocumentCompilation? _tiledCompilation;
    internal NativeId65TiledDocumentCompilation? TiledCompilation => _tiledCompilation;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65TiledDocumentCompilation tiled, NativeFreshResearchSourceOwners owners,
        NativeId65FreshDiscVerification measured)
    {
        Saved = saved; _tiledCompilation = tiled;
        SourceImagePath = owners.ImagePath; SourceCuePath = owners.CuePath;
        _imageIdentity = owners.ImageIdentity!; _cueIdentity = owners.CueIdentity!;
        _sourceCue = owners.CueSnapshot!.ToArray(); SourceCueSha256 = Hash(_sourceCue);
        _outputCue = Encoding.UTF8.GetBytes("FILE \"" + ImageName + "\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected = measured; _canonical = tiled.CopyCanonicalDocument();
        RequireTiledAncestry(saved, tiled);
        if (tiled.TiledMaterialAncestor is { } material)
        {
            var evidence = BuildTiledMaterialEvidence(tiled, material, measured);
            PlanSha256 = evidence.PlanSha256;
            _receipt = evidence.Receipt;
            return;
        }
        var disc = tiled.DiscPlan; var body = disc.BodyAncestor; var fresh = tiled.FreshResourceAncestor;
        var surface = fresh.TiledSurfaceAncestor!;
        using var document = JsonDocument.Parse(_canonical);
        var snapshot = saved.Snapshot;
        var revision = new
        {
            projectId = snapshot.ProjectId, root = snapshot.RootPath, path = snapshot.Path,
            rawSha256 = snapshot.ContentSha256, manifestSha256 = snapshot.ManifestSha256,
            canonicalSha256 = tiled.CanonicalSha256, canonical = document.RootElement
        };
        var lineage = new
        {
            discPlan = disc.PlanSha256, bodyPlan = body.PlanSha256, stagingPlan = body.StagingPlanSha256,
            freshPlan = fresh.PlanSha256, resourceChainPlan = fresh.ResourceChainAncestor.PlanSha256,
            surface.DocumentSha256, surface.BundleSha256,
            tileCount = tiled.Document.TiledContent!.Tiles.Count,
            spawn = fresh.SceneLayout.Spawn, placedCount = fresh.SceneLayout.PlacedCount,
            nativeNominalCapacity = fresh.SceneLayout.NativeNominalCapacity,
            body.ResourceProfile, body.DataUsedByteLength, body.DataSectorByteLength,
            body.ModelRuntimeAddress, body.SceneRuntimeAddress, body.KnownSceneEndAddress,
            body.Descriptors, body.Owners, body.PhaseOverlaps,
            fresh.UploadSha256, fresh.DataSha256, fresh.ModelSha256, fresh.SceneSha256,
            bodySha256 = Hash(disc.CopyBody()), overlaySha256 = Hash(disc.CopyOverlay()),
            executableSha256 = Hash(disc.CopyExecutable()), wadDirectorySha256 = Hash(disc.CopyWadDirectory()),
            isoRootSha256 = Hash(disc.CopyIsoRootDirectory()), disc.FinalExecutableLba, disc.FinalWadByteLength,
            upstreamPendingOwners = disc.PendingOwners
        };
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, variant = TiledVariant, saved = revision,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage, expectedImageSha256 = measured.OutputSha256, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueSha256 = Hash(_outputCue),
            wholeImageInverseRequired = true, savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
        _receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, variant = TiledVariant, planSha256 = PlanSha256, saved = revision,
            SourceImagePath, SourceCuePath, SourceCueSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256, lineage,
            imageFile = ImageName, imageSha256 = measured.OutputSha256, imageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueFile = CueName, cueSha256 = Hash(_outputCue), receiptFile = ReceiptName,
            wholeImageInverseRequired = true, savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            hqRuntimeProven = false, audioRuntimeProven = false, peakRuntimeMemoryProven = false,
            checkpointOrForeignStateSupported = false
        }, new JsonSerializerOptions { WriteIndented = true });
        if (_receipt.Length is < 1 or > 65536)
            throw new InvalidDataException("Complete saved-tiled receipt exceeds the companion bound; no evidence is truncated.");
    }

    private static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureTiledAsync(string originalBin,
        string sourceCue, NativeFreshSavedLevelDocumentReadContext saved, CancellationToken token)
    {
        // Reject an unsupported WHOLE document before opening any source. This
        // neutral preflight cannot issue a compiler or truncate unsupported rows.
        bool hasMaterials = saved.Document.TiledContent!.Tiles.Any(tile =>
            tile.Materials.Triangle0 is not NativeFreshBaseFloorColorMaterialIntent ||
            tile.Materials.Triangle1 is not NativeFreshBaseFloorColorMaterialIntent);
        if (hasMaterials)
            _ = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(saved.Document);
        else
        {
            var (_, surface) = NativeId65TiledSurfaceBundle.PrepareDocument(saved.Document);
            NativeId65TiledNormalDataLayout.RequireFixedSurfaceAllocation(surface.CopyBundle().Length);
        }
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin); ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin), Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result = null; Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
            var tiled = hasMaterials
                ? await NativeId65TiledDocumentCompilation.CaptureSelectedMaterialsAsync(owners.ImagePath, saved.Document, token)
                : await NativeId65TiledDocumentCompilation.CaptureAsync(owners.ImagePath, saved.Document, token);
            saved.RequireDocument(tiled.Document);
            owners.Bin!.Position = 0;
            var measured = NativeId65FreshDiscStreaming.Measure(tiled.DiscPlan, owners.Bin, token);
            result = new(saved, tiled, owners, measured);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, saved, primary);
        token.ThrowIfCancellationRequested();
        return result!;
    }

    internal void RequireTiledAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65TiledDocumentCompilation tiled)
    {
        if (!ReferenceEquals(saved, Saved) || tiled is null || !ReferenceEquals(tiled, _tiledCompilation) ||
            _material != null || _mixedDisc != null || _mixedClearance != null || _familyCompilation != null ||
            _lockedChestCompilation != null || _tiledFamilyCompilation != null)
            throw new InvalidDataException("Saved-tiled Candidate requires its actual saved context and complete compilation, not equal hashes or a projected document.");
        saved.RequireDocument(tiled.Document);
        var fresh = tiled.FreshResourceAncestor; var body = tiled.DiscPlan.BodyAncestor;
        if (!ReferenceEquals(fresh.TiledSurfaceAncestor?.Document, saved.Document) ||
            !ReferenceEquals(body.TiledSceneAncestor, fresh) ||
            !_canonical.AsSpan().SequenceEqual(tiled.CopyCanonicalDocument()) ||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)) ||
            Hash(_canonical) != tiled.CanonicalSha256)
            throw new InvalidDataException("Saved-tiled actual document, scene, body or complete canonical binding changed.");
        if (tiled.TiledMaterialAncestor is not { } material)
        {
            if (body.ResourceProfile != NativeId65NormalResourceProfile.UntexturedGouraudFlatTiles ||
                body.TiledMaterialAncestor != null || body.UpstreamPlanSha256 != fresh.PlanSha256)
                throw new InvalidDataException("Base-only saved-tiled Candidate cannot adopt a material revision.");
            var (_, surface) = NativeId65TiledSurfaceBundle.PrepareDocument(saved.Document);
            NativeId65TiledNormalDataLayout.RequireFixedSurfaceAllocation(surface.CopyBundle().Length);
            return;
        }
        if (body.ResourceProfile != material.ResourceProfile ||
            !ReferenceEquals(body.TiledMaterialAncestor, material) || body.TextureAncestor != null ||
            body.UpstreamPlanSha256 != material.PlanSha256 || material.UpstreamPlanSha256 != fresh.PlanSha256 ||
            material.CanonicalDocumentSha256 != tiled.CanonicalSha256 ||
            !_canonical.AsSpan().SequenceEqual(material.CopyCanonicalDocument()) || body.DataSectorByteLength != 0x1000)
            throw new InvalidDataException("Material saved-tiled Candidate requires its actual complete material revision and final data owner.");
        material.RequireSelectedAncestry(fresh, saved.Document, material.MaterialSources);
        var prepared = NativeId65TiledSurfaceBundle.PrepareGeometryForSelectedMaterials(saved.Document);
        if (!material.OrderedFaceMaterials.SequenceEqual(prepared.OrderedFaceMaterials) ||
            body.DataUsedByteLength != material.CopyDataComponents().Length ||
            body.DataUsedByteLength is < 1 or > 0x1000)
            throw new InvalidDataException("Saved-tiled material face bindings or final measured data endpoint changed.");
        byte[] bodyBytes = body.CopyLevelData();
        material.RequireExactOutputs(
            bodyBytes.AsSpan(body.Descriptors[0].Offset, body.Descriptors[0].ByteLength),
            bodyBytes.AsSpan(body.Descriptors[1].Offset, body.DataUsedByteLength),
            bodyBytes.AsSpan(body.Descriptors[2].Offset, body.Descriptors[2].ByteLength),
            bodyBytes.AsSpan(body.Descriptors[3].Offset, body.Descriptors[3].ByteLength));
        if (bodyBytes.AsSpan(body.Descriptors[1].Offset + body.DataUsedByteLength,
            body.DataSectorByteLength - body.DataUsedByteLength).IndexOfAnyExcept((byte)0) != -1)
            throw new InvalidDataException("Saved-tiled material data allocation padding changed.");
    }
}
