using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

namespace Spyro.Editor.Core.Exporting;

public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    private const string MixedVariant = "schema2-two-retail-materials-wizard-p1-n32";
    private readonly NativeId65FreshWizardResearchDiscPlan? _mixedDisc;
    private readonly NativeFreshInitialSpawnClearancePlan? _mixedClearance;

    internal NativeId65FreshWizardResearchDiscPlan? MixedDisc => _mixedDisc;
    internal NativeFreshInitialSpawnClearancePlan? MixedClearance => _mixedClearance;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshWizardResearchDiscPlan disc, NativeFreshInitialSpawnClearancePlan clearance,
        NativeFreshResearchSourceOwners owners, NativeId65FreshDiscVerification measured)
    {
        Saved = saved; _mixedDisc = disc; _mixedClearance = clearance;
        SourceImagePath = owners.ImagePath; SourceCuePath = owners.CuePath;
        _imageIdentity = owners.ImageIdentity!; _cueIdentity = owners.CueIdentity!;
        _sourceCue = owners.CueSnapshot!.ToArray(); SourceCueSha256 = Hash(_sourceCue);
        _outputCue = Encoding.UTF8.GetBytes("FILE \"" + ImageName + "\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected = measured; _canonical = NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document);
        RequireMixedAncestry(saved, disc, clearance);

        var exe = disc.ExecutableAncestor; var body = exe.BodyAncestor;
        var host = body.HostAncestor; var scene = host.SceneAncestor;
        var resource = body.MixedResourceAncestor!;
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
            discPlan = disc.PlanSha256, executablePlan = exe.PlanSha256, bodyPlan = body.PlanSha256,
            hostPlan = host.PlanSha256, scenePlan = scene.PlanSha256, mixedResourcePlan = resource.PlanSha256,
            familyPlan = scene.FamilyAncestor.PlanSha256, clearancePlan = clearance.PlanSha256,
            floor = saved.Document.Base, placedCount = saved.Document.Placements.Count,
            nativeNominalCapacity = 32,
            materials = resource.MaterialSources.Select((s, i) => new
            {
                slot = i, s.Handle, s.SourceImageSha256, s.SourceWadEntry, s.SourceTextureId,
                s.SourceDataSha256, sourceRecordSha256 = s.Layout.RecordSha256
            }).ToArray(),
            resource.UploadSha256, resource.DataSha256,
            bodySha256 = disc.BodySha256, overlaySha256 = disc.OverlaySha256,
            executableSha256 = disc.ExecutableSha256, wadDirectorySha256 = disc.WadDirectorySha256,
            isoRootSha256 = disc.IsoRootSha256, disc.FinalExecutableLba, disc.FinalWadByteLength,
            staticCensus = exe.Census, upstreamPendingOwners = disc.PendingOwners
        };
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, variant = MixedVariant, saved = revision,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage, expectedImageSha256 = measured.OutputSha256, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueSha256 = Hash(_outputCue),
            savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
        _receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, variant = MixedVariant, planSha256 = PlanSha256, saved = revision,
            SourceImagePath, SourceCuePath, SourceCueSha256,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256, lineage,
            imageFile = ImageName, imageSha256 = measured.OutputSha256, imageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueFile = CueName, cueSha256 = Hash(_outputCue), receiptFile = ReceiptName,
            savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false,
            hqRuntimeProven = false, audioRuntimeProven = false, peakRuntimeMemoryProven = false,
            checkpointOrForeignStateSupported = false
        }, new JsonSerializerOptions { WriteIndented = true });
        if (_receipt.Length is < 1 or > 65536)
            throw new InvalidDataException("Saved-level complete research receipt exceeds the companion bound; no evidence is truncated.");
    }

    private static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureMixedAsync(string originalBin,
        string sourceCue, NativeFreshSavedLevelDocumentReadContext saved, CancellationToken token)
    {
        NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin), Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result = null; Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
            string image = owners.ImagePath;
            var (body, clearance) = await CaptureMixedBodyAsync(image, saved.Document, token);
            var exe = await NativeId65FreshWizardResearchExecutablePlan.CaptureMaterialAuthoredAsync(image, body, token);
            var disc = await NativeId65FreshWizardResearchDiscPlan.CaptureMaterialAuthoredAsync(image, exe, token);
            owners.Bin!.Position = 0;
            var measured = NativeId65FreshDiscStreaming.Measure(disc, owners.Bin, token);
            result = new(saved, disc, clearance, owners, measured);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, saved, primary);
        token.ThrowIfCancellationRequested();
        return result!;
    }

    // The actual source compilation is shared; only the full saved-document arm
    // decides which complete placement list is admissible and retains its binding.
    internal static async Task<(NativeId65FreshWizardResearchBodyPlan Body, NativeFreshInitialSpawnClearancePlan Clearance)>
        CaptureMixedBodyAsync(string image, NativeFreshLevelDocumentIntent document, CancellationToken token)
    {
        NativeFreshWizardAuthoringShape.RequireMaterialDocumentShape(document);
        return await CaptureMixedBodyCoreAsync(image, document, false, token);
    }

    // Explicit subordinate same-layout producer. Admission of any complete
    // saved family document belongs to its separate whole-document compiler.
    internal static async Task<(NativeId65FreshWizardResearchBodyPlan Body, NativeFreshInitialSpawnClearancePlan Clearance)>
        CaptureSameLayoutMixedBodyAsync(string image, NativeFreshLevelDocumentIntent document, CancellationToken token)
    {
        NativeFreshWizardAuthoringShape.RequireSameLayoutMaterialDocumentShape(document);
        return await CaptureMixedBodyCoreAsync(image, document, true, token);
    }

    private static async Task<(NativeId65FreshWizardResearchBodyPlan Body, NativeFreshInitialSpawnClearancePlan Clearance)>
        CaptureMixedBodyCoreAsync(string image, NativeFreshLevelDocumentIntent document, bool sameLayout, CancellationToken token)
    {
        var compilation = await NativeId65FreshDocumentCompilation.CaptureAsync(image, document.Base, token);
        var family = await NativeWizardFamilyAssetPlan.CaptureAsync(image, token);
        var resources = await NativeId65FreshWizardResearchResourcePlan.CaptureAuthoredAsync(image, compilation, family, token);
        var scene = sameLayout
            ? await NativeId65FreshWizardResearchScenePlan.CaptureSameLayoutMaterialAuthoredAsync(image, document, compilation, family, resources, token)
            : await NativeId65FreshWizardResearchScenePlan.CaptureMaterialAuthoredAsync(image, document, compilation, family, resources, token);
        var clearance = await NativeFreshInitialSpawnClearancePlan.CaptureWizardAsync(image, document, scene, token);
        _ = clearance.RequireInitialFloorFootprint(document, scene);
        var fallback = await NativeId65FreshWizardResearchFallbackResourcePlan.CaptureAsync(image, resources, token);
        var host = await NativeId65FreshWizardResearchHostPlan.CaptureAsync(image, scene, fallback, token);
        var first = (NativeFreshRetailTerrainMaterialIntent)document.FloorMaterials!.Triangle0;
        var second = (NativeFreshRetailTerrainMaterialIntent)document.FloorMaterials.Triangle1;
        var material0 = NativeStaticTerrainTextureSource.Capture(image, first.SourceWadEntry, first.TextureId);
        token.ThrowIfCancellationRequested();
        var material1 = first.SourceWadEntry == second.SourceWadEntry && first.TextureId == second.TextureId
            ? material0 : NativeStaticTerrainTextureSource.Capture(image, second.SourceWadEntry, second.TextureId);
        token.ThrowIfCancellationRequested();
        var mixed = sameLayout
            ? await NativeFreshWizardMaterialResource.CaptureSameLayoutMaterialAuthoredAsync(image, host, material0, material1, token)
            : await NativeFreshWizardMaterialResource.CaptureMaterialAuthoredAsync(image, host, material0, material1, token);
        var body = await NativeId65FreshWizardResearchBodyPlan.CaptureMaterialAuthoredAsync(image, mixed, token);
        return (body, clearance);
    }

    internal void RequireMixedAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeId65FreshWizardResearchDiscPlan disc, NativeFreshInitialSpawnClearancePlan clearance)
    {
        if (!ReferenceEquals(saved, Saved) || !ReferenceEquals(disc, _mixedDisc) ||
            !ReferenceEquals(clearance, _mixedClearance) || _material != null || _familyCompilation != null ||
            _tiledCompilation != null || _lockedChestCompilation != null || _tiledFamilyCompilation != null || disc is null || clearance is null)
            throw new InvalidDataException("Saved mixed Candidate belongs to its actual saved context, Disc and initial clearance, not equal hashes.");
        disc.RequireAncestry(disc.ExecutableAncestor);
        if (!disc.IsMaterialAuthored)
            throw new InvalidDataException("Saved mixed Candidate requires the actual material-authored Disc.");
        var body = disc.ExecutableAncestor.BodyAncestor; var host = body.HostAncestor;
        var scene = host.SceneAncestor; var mixed = body.MixedResourceAncestor!;
        saved.RequireDocument(mixed.DocumentAncestor);
        NativeFreshWizardAuthoringShape.RequireMaterialDocument(saved.Document, scene.CompilationAncestor);
        mixed.RequireAncestry(host, saved.Document, mixed.MaterialSources[0], mixed.MaterialSources[1]);
        _ = clearance.RequireInitialFloorFootprint(saved.Document, scene);
        if (!_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Actual saved mixed canonical document changed.");
    }

    private void RequireCurrentAncestry()
    {
        if (_tiledFamilyCompilation is { } tiledFamily) RequireTiledFamilyAncestry(Saved, tiledFamily);
        else if (_lockedChestCompilation is { } chest) RequireLockedChestAncestry(Saved, chest);
        else if (_tiledCompilation is { } tiled) RequireTiledAncestry(Saved, tiled);
        else if (_familyCompilation is { } family) RequireFamilyAncestry(Saved, family);
        else if (_mixedDisc is { } mixed) RequireMixedAncestry(Saved, mixed, _mixedClearance!);
        else RequireAncestry(Saved, MaterialCompilation);
    }
}
