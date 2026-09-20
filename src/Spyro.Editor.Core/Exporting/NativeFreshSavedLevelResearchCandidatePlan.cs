using System.Runtime.ExceptionServices;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Editing;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshSavedLevelResearchSmoke")]

namespace Spyro.Editor.Core.Exporting;

// Saved whole-level exports share source/revision guards and publication.
// Concrete compiled arms do not confer universal or persistent-save proof.
public sealed partial class NativeFreshSavedLevelResearchCandidatePlan
{
    internal const string ProfileId = "native-fresh-saved-level-research-r01";
    internal const string Variant = "schema2-two-retail-materials-p0-n16";
    internal const string ImageName = "native-fresh-saved-level-r01.bin";
    internal const string CueName = "native-fresh-saved-level-r01.cue";
    internal const string ReceiptName = "native-fresh-saved-level-r01.receipt.json";
    internal static string[] OutputNames => [ImageName, ReceiptName, CueName];
    private readonly byte[] _sourceCue, _outputCue, _canonical, _receipt;
    private readonly string _imageIdentity, _cueIdentity;
    private readonly NativeId65FreshDiscVerification _expected;
    private readonly NativeFreshSavedMaterialCompilation? _material;

    private NativeFreshSavedLevelResearchCandidatePlan(NativeFreshSavedMaterialCompilation material,
        NativeFreshResearchSourceOwners owners, NativeId65FreshDiscVerification measured)
    {
        Saved = material.Saved; _material = material;
        SourceImagePath = owners.ImagePath; SourceCuePath = owners.CuePath;
        _imageIdentity = owners.ImageIdentity!; _cueIdentity = owners.CueIdentity!;
        _sourceCue = owners.CueSnapshot!.ToArray(); SourceCueSha256 = Hash(_sourceCue);
        _outputCue = Encoding.UTF8.GetBytes("FILE \"" + ImageName + "\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n");
        _expected = measured; _canonical = material.CopyCanonicalDocument();
        RequireAncestry(Saved, material);
        var disc = material.Disc; var texture = material.Texture; var body = material.Body;
        var fresh = material.BaseCompilation.FreshResourceAncestor;
        using var document = JsonDocument.Parse(_canonical);
        var snapshot = Saved.Snapshot;
        var saved = new
        {
            projectId = snapshot.ProjectId, root = snapshot.RootPath, path = snapshot.Path,
            rawSha256 = snapshot.ContentSha256, manifestSha256 = snapshot.ManifestSha256,
            canonicalSha256 = Hash(_canonical), canonical = document.RootElement
        };
        var lineage = new
        {
            discPlan = disc.PlanSha256, bodyPlan = body.PlanSha256, stagingPlan = material.Staging.PlanSha256,
            texturePlan = texture.PlanSha256, freshPlan = fresh.PlanSha256,
            floor = fresh.ResourceChainAncestor.Resources.Surface.Floor, spawn = fresh.SceneLayout.Spawn,
            placedCount = fresh.SceneLayout.PlacedCount, nativeNominalCapacity = fresh.SceneLayout.NativeNominalCapacity,
            materials = texture.MaterialSources.Select((s, i) => new
            {
                slot = i, s.Handle, s.SourceImageSha256, s.SourceWadEntry, s.SourceTextureId,
                s.SourceDataSha256, sourceRecordSha256 = s.Layout.RecordSha256,
                relocatedRecordSha256 = texture.Allocation.Records[i].RecordSha256
            }).ToArray(),
            texture.NativeClip, texture.AdditionalPacketBytesPerPass,
            texture.Allocation.Allocations, texture.Allocation.ContainedAliases, texture.Evidence,
            texture.UploadSha256, texture.DataSha256, texture.ModelSha256, texture.SceneSha256,
            bodySha256 = Hash(disc.CopyBody()), overlaySha256 = Hash(disc.CopyOverlay()),
            executableSha256 = Hash(disc.CopyExecutable()), wadDirectorySha256 = Hash(disc.CopyWadDirectory()),
            isoRootSha256 = Hash(disc.CopyIsoRootDirectory()), disc.FinalExecutableLba, disc.FinalWadByteLength,
            upstreamPendingOwners = new { texture = texture.PendingOwners, body = body.PendingOwners }
        };
        PlanSha256 = Hash(JsonSerializer.SerializeToUtf8Bytes(new
        {
            profile = ProfileId, variant = Variant, saved,
            sourceImageSha256 = NativeId65FreshDiscSource.OriginalSha256,
            SourceImagePath, SourceCuePath, SourceCueSha256, imageIdentity = _imageIdentity, cueIdentity = _cueIdentity,
            lineage, expectedImageSha256 = measured.OutputSha256, expectedImageByteLength = ExpectedImageByteLength,
            changedRawSectors = measured.ChangedSectors, cueSha256 = Hash(_outputCue),
            savedRevisionBound = true, nativePersistentSaveOwnerProven = false,
            runtimeVerified = false, normalCreateBinApproved = false, completeNativeDependencyClosureProven = false
        }));
        _receipt = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schema = 1, profile = ProfileId, variant = Variant, planSha256 = PlanSha256, saved,
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

    internal NativeFreshSavedLevelDocumentReadContext Saved { get; }
    internal NativeFreshSavedMaterialCompilation MaterialCompilation => _material ??
        throw new InvalidDataException("This saved level does not have a material-only compilation ancestor.");
    private NativeId65FreshDiscPlan NormalDisc => _tiledCompilation?.DiscPlan ?? MaterialCompilation.Disc;
    public NativeFreshLevelDocumentSnapshot SavedSnapshot => Saved.Snapshot;
    public string SourceImagePath { get; }
    public string SourceCuePath { get; }
    public string SourceCueSha256 { get; }
    public string PlanSha256 { get; }
    public string ExpectedImageSha256 => _expected.OutputSha256;
    public int ExpectedChangedRawSectors => _expected.ChangedSectors;
    public long ExpectedImageByteLength => NativeId65FreshDiscSource.ImageByteLength;
    public bool RuntimeVerified => false;

    /// <summary>Compile the actual supported saved whole-level revision; no runtime or normal-export approval.</summary>
    public static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureSavedAsync(string originalBin,
        string sourceCue, NativeFreshLevelDocumentSnapshot savedSnapshot, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var saved = NativeFreshSavedLevelDocumentReadContext.Capture(savedSnapshot, token);
        return await CaptureAsync(originalBin, sourceCue, saved, token);
    }

    internal static async Task<NativeFreshSavedLevelResearchCandidatePlan> CaptureAsync(string originalBin,
        string sourceCue, NativeFreshSavedLevelDocumentReadContext saved, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(saved);
        if (saved.Document.TiledContent != null)
            return await CaptureTiledAsync(originalBin, sourceCue, saved, token);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalBin); ArgumentException.ThrowIfNullOrWhiteSpace(sourceCue);
        if (saved.Document.SchemaVersion == 3)
            return await CaptureFamilyAsync(originalBin, sourceCue, saved, token);
        if (saved.Document.Placements.Count != 0)
            return await CaptureMixedAsync(originalBin, sourceCue, saved, token);
        RequireProfile(saved.Document);
        NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, token);
        var owners = new NativeFreshResearchSourceOwners(Path.GetFullPath(originalBin), Path.GetFullPath(sourceCue));
        NativeFreshSavedLevelResearchCandidatePlan? result = null; Exception? primary = null;
        try
        {
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
            var material = await NativeFreshSavedMaterialCompilation.CaptureAsync(owners.ImagePath, saved, token);
            owners.Bin!.Position = 0;
            var measured = NativeId65FreshDiscStreaming.Measure(material.Disc, owners.Bin, token);
            result = new(material, owners, measured);
            token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, saved, primary);
        return result!;
    }

    internal byte[] CopyCanonicalDocument() => _canonical.ToArray();
    internal byte[] CopyCueBytes() => _outputCue.ToArray();
    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified)
    { RequireVerification(verified); return _receipt.ToArray(); }

    internal void RequireAncestry(NativeFreshSavedLevelDocumentReadContext saved,
        NativeFreshSavedMaterialCompilation material)
    {
        ArgumentNullException.ThrowIfNull(saved); ArgumentNullException.ThrowIfNull(material);
        if (!ReferenceEquals(saved, Saved) || !ReferenceEquals(material, MaterialCompilation))
            throw new InvalidDataException("Saved-level Candidate belongs to its actual saved context and compiled arm, not equal hashes.");
        RequireProfile(saved.Document); saved.RequireDocument(material.Document);
        material.RequireAncestry(saved, material.BaseCompilation, material.Material0, material.Material1,
            material.Texture, material.Staging, material.Body, material.Disc);
        if (material.SourceImagePath != SourceImagePath ||
            !_canonical.AsSpan().SequenceEqual(NativeFreshLevelDocumentCodec.SerializeCanonical(saved.Document)))
            throw new InvalidDataException("Actual saved-level source path or canonical document changed.");
    }

    internal async Task RequireFreshAsync(CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null;
        try
        {
            RequireCurrentAncestry();
            owners.Open(token);
            await NativeId65FreshDiscSource.RequireWholeOriginalAsync(owners.Bin!, token);
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, Saved, primary);
    }

    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null; NativeId65FreshDiscVerification? result = null;
        try
        {
            RequireCurrentAncestry(); owners.Open(token);
            owners.Output = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            result = _mixedDisc is { } mixed ? mixed.VerifyImage(owners.Bin!, owners.Output, token) :
                NormalDisc.VerifyImage(owners.Bin!, owners.Output, token);
            RequireVerification(result);
            if (_tiledCompilation is { } tiled)
            {
                // Read the actual physical file a second time. A regenerated
                // afterimage or a successful forward check is not this inverse.
                owners.Output.Position = 0;
                RequireVerification(NativeId65FreshDiscStreaming.VerifyInverse(tiled.DiscPlan, owners.Output, token));
            }
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, Saved, primary); return result!;
    }

    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        token.ThrowIfCancellationRequested();
        var owners = NewOwners(); Exception? primary = null;
        try
        {
            RequireCurrentAncestry(); owners.Open(token);
            owners.Output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            RequireVerification(_mixedDisc is { } mixed
                ? NativeId65FreshDiscStreaming.Measure(mixed, owners.Bin!, token, owners.Output, firstSector)
                : NativeId65FreshDiscStreaming.Measure(NormalDisc,
                    owners.Bin!, token, owners.Output, firstSector));
            owners.Output.Flush(flushToDisk: true); token.ThrowIfCancellationRequested();
        }
        catch (Exception error) { primary = error; }
        CompleteOperation(owners, Saved, primary);
    }

    private NativeFreshResearchSourceOwners NewOwners() => new(SourceImagePath, SourceCuePath,
        _sourceCue, _imageIdentity, _cueIdentity);

    // Wrap the concrete helper's ALREADY folded primary only once; source/close
    // failure never suppresses the independent raw document/manifest observation.
    private static void CompleteOperation(NativeFreshResearchSourceOwners owners,
        NativeFreshSavedLevelDocumentReadContext saved, Exception? primary)
    {
        List<Exception> failures = [];
        try { owners.Complete(primary); } catch (Exception error) { failures.Add(error); }
        try { NativeFreshDocumentStorage.RequireAggregateCurrent(saved.Snapshot, CancellationToken.None); }
        catch (Exception error) { failures.Add(error); }
        ThrowFailures(failures);
    }

    internal static void ThrowFailures(IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 1) ExceptionDispatchInfo.Capture(failures[0]).Throw();
        if (failures.Count > 1)
            throw new IOException("Saved-level operation failed with additional final source, saved-version or close failures.",
                new AggregateException(failures));
    }

    private void RequireVerification(NativeId65FreshDiscVerification verified)
    {
        ArgumentNullException.ThrowIfNull(verified);
        if (verified != _expected)
            throw new InvalidDataException("Saved-level readback differs from its actual source-issued measured image.");
    }
    private static void RequireProfile(NativeFreshLevelDocumentIntent document)
    {
        if (document.SchemaVersion != 2 || document.Placements.Count != 0 || document.FloorMaterials == null ||
            document.FloorMaterials.Triangle0 is not NativeFreshRetailTerrainMaterialIntent first ||
            document.FloorMaterials.Triangle1 is not NativeFreshRetailTerrainMaterialIntent second ||
            first.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant ||
            second.SourceVariant != NativeFreshRetailTerrainMaterialIntent.SupportedSourceVariant)
            throw new InvalidDataException("Saved-level research currently requires schema2, zero placements and two retail materials.");
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}
