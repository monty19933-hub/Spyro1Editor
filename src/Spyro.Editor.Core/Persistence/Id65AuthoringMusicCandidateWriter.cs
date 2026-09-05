using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoringMusicCandidateRequest(
    UnusedLevel65BlankLevelLabWorkspacePaths Paths,
    UnusedLevel65BlankLevelLabManifest ExpectedManifest,
    Id65AuthoringMusicBuildInput Input);

internal sealed record Id65AuthoringMusicCandidateReceipt(
    string Format, int SchemaVersion, string BuildInputSha256,
    string DocumentCanonicalSha256, string DocumentEnvelopeSha256,
    string NativeInputSetSha256, string BaselineAggregatePlanSha256,
    string BaselineImageSha256, string BaselineReceiptSha256,
    string SourceImageSha256, string OutputImageSha256,
    string MusicDiscPlanSha256, int SelectedTrackId,
    string SourceExecutableSha256, string OutputExecutableSha256,
    string OutputRow80Sha256, string CueSha256,
    int VerifiedOwnedRawSectorCount, int ChangedRawSectorCount,
    long ChangedPhysicalByteCount, bool ExceptionRollbackPublication,
    bool CrashAtomicPublication, bool RuntimeProven, bool NormalCreateBinEnabled);

internal sealed record Id65AuthoringMusicCandidateResult(
    string CandidateDirectoryPath, string CandidateImagePath, string CandidateCuePath,
    string CandidateInputPath, string CandidateReceiptPath,
    Id65AuthoringMusicCandidateReceipt Receipt, bool ReusedExistingCandidate,
    bool PublishedThisCall, string RetainedRecoveryDirectory);

internal enum Id65AuthoringMusicCandidateFaultPoint { None, AfterCopy, AfterMusicWrite, AfterReadback, BeforePublication, AfterPublication }

/// <summary>
/// Writes an immutable workspace-local test candidate using the shared
/// create-new artifact transaction. The canonical build input is published
/// with its BIN/receipt/CUE, not installed as an independently mutable project
/// sidecar. Old schema-6 candidates and editable documents are never changed.
/// Requires a trusted exclusive workspace; exception rollback is provided,
/// but a process interruption can require explicit journal recovery. This is
/// not normal Create BIN, editable-project integration or runtime acceptance.
/// </summary>
internal static class Id65AuthoringMusicCandidateWriter
{
    public const string RootDirectoryName = "materialized-schema6-music-candidates";
    public const string ImageFileName = "unusedlevel65blank-authored-music-candidate-v1.bin";
    public const string CueFileName = "unusedlevel65blank-authored-music-candidate-v1.cue";
    public const string InputFileName = "unusedlevel65blank-authored-music-build-input-v1.json";
    public const string ReceiptFileName = "unusedlevel65blank-authored-music-candidate-v1.receipt.json";
    private static readonly string[] ArtifactNames = [ImageFileName, InputFileName, ReceiptFileName, CueFileName];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static Task<Id65AuthoringMusicCandidateResult> MaterializeAsync(
        Id65AuthoringMusicCandidateRequest request, CancellationToken cancellationToken = default) =>
        MaterializeCoreAsync(request, Id65AuthoringMusicCandidateFaultPoint.None, null, cancellationToken);

    internal static Task<Id65AuthoringMusicCandidateResult> MaterializeForSmokeAsync(
        Id65AuthoringMusicCandidateRequest request, Id65AuthoringMusicCandidateFaultPoint fault,
        Action<string>? beforePublication = null, CancellationToken cancellationToken = default)
    {
        if (fault == Id65AuthoringMusicCandidateFaultPoint.None && beforePublication is null)
            throw new ArgumentException("The smoke entrypoint requires an injected fault or prepublication hook.");
        return MaterializeCoreAsync(request, fault, beforePublication, cancellationToken);
    }

    private static async Task<Id65AuthoringMusicCandidateResult> MaterializeCoreAsync(
        Id65AuthoringMusicCandidateRequest request, Id65AuthoringMusicCandidateFaultPoint fault,
        Action<string>? beforePublication, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.ExpectedManifest);
        ArgumentNullException.ThrowIfNull(request.Input);
        if (!Enum.IsDefined(fault)) throw new ArgumentOutOfRangeException(nameof(fault));
        cancellationToken.ThrowIfCancellationRequested();
        Id65AuthoringMusicBuildInput input = Id65AuthoringMusicBuildInput.Parse(request.Input.SerializeUtf8());
        Id65AuthoringMusicDiscPlan plan = await CaptureFreshPlanAsync(request, input, cancellationToken);
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult baseline =
            await Id65AuthoringBlankCycloramaAggregateCandidateMaterializer.MaterializeAsync(
                new(request.Paths, request.ExpectedManifest, input.BaselineEnvelopeCanonicalSha256), cancellationToken);
        if (baseline.Plan.DeterministicAggregatePlanSha256 != plan.BaselinePlan.DeterministicAggregatePlanSha256 ||
            baseline.Receipt.OutputRow80Sha256 != plan.OutputRow80Sha256)
            throw new InvalidDataException("The immutable baseline candidate differs from the authored music baseline plan.");
        string root = Path.Combine(request.Paths.RootPath, RootDirectoryName);
        string output = Path.Combine(root, "music-" + plan.DeterministicPlanSha256);
        RequireSafePath(request.Paths.RootPath, output);
        byte[] cueBytes = DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(
            baseline.Paths.CandidateCuePath, baseline.Paths.CandidateImagePath, ImageFileName));
        string baselineReceiptSha256 = HashFile(baseline.Paths.CandidateReceiptPath);

        if (InspectExistingSet(output))
        {
            Id65AuthoringMusicCandidateReceipt receipt = VerifySet(request, input, plan, baseline,
                baselineReceiptSha256, output, cueBytes, cancellationToken);
            await RequireFreshPlanAsync(request, input, plan, cancellationToken);
            _ = VerifySet(request, input, plan, baseline, baselineReceiptSha256, output, cueBytes, cancellationToken);
            return Result(output, receipt, reused: true, published: false, recovery: "");
        }

        Directory.CreateDirectory(output);
        RequireSafePath(request.Paths.RootPath, output);
        string preparationLease = Path.Combine(output, ".music-preparation.lock");
        using FileStream preparation = new(preparationLease, FileMode.CreateNew, FileAccess.ReadWrite,
            FileShare.None, bufferSize: 1, FileOptions.DeleteOnClose);
        string stage = Path.Combine(output, ".music-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        RequireSafePath(output, stage);
        bool published = false;
        string recovery = "";
        try
        {
            string stagedImage = Path.Combine(stage, ImageFileName);
            await Task.Run(() => File.Copy(baseline.Paths.CandidateImagePath, stagedImage, overwrite: false), cancellationToken);
            Fault(fault, Id65AuthoringMusicCandidateFaultPoint.AfterCopy);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyMusicToStagedCopy(stagedImage, plan);
            Fault(fault, Id65AuthoringMusicCandidateFaultPoint.AfterMusicWrite);
            Id65AuthoringMusicPhysicalReadback readback = VerifyImage(request.Paths.LockedBaseImagePath, stagedImage, plan, cancellationToken);
            Id65AuthoringMusicCandidateReceipt receipt = BuildReceipt(input, plan, baseline, baselineReceiptSha256, cueBytes, readback);
            await WriteNewDurablyAsync(Path.Combine(stage, InputFileName), input.SerializeUtf8(), cancellationToken);
            await WriteNewDurablyAsync(Path.Combine(stage, ReceiptFileName), SerializeReceipt(receipt), cancellationToken);
            await WriteNewDurablyAsync(Path.Combine(stage, CueFileName), cueBytes, cancellationToken);
            Fault(fault, Id65AuthoringMusicCandidateFaultPoint.AfterReadback);
            beforePublication?.Invoke(output);
            await RequireFreshPlanAsync(request, input, plan, cancellationToken);
            _ = VerifySet(request, input, plan, baseline, baselineReceiptSha256, stage, cueBytes, cancellationToken);
            Fault(fault, Id65AuthoringMusicCandidateFaultPoint.BeforePublication);
            RequireSafePath(request.Paths.RootPath, output);
            if (Directory.EnumerateFileSystemEntries(output).Any(path => path != stage && path != preparationLease))
                throw new InvalidDataException("The music output namespace gained a competing or incomplete artifact set before publication.");
            // The shared boundary checks all final destinations under its lock,
            // never overwrites a competing set, and publishes the CUE last.
            ExportArtifactPublicationResult publication = ExportArtifactTransaction.PublishNew(output,
                ArtifactNames.Select(name => new ExportArtifactReplacement(Path.Combine(output, name), Path.Combine(stage, name))).ToArray(),
                cancellationToken);
            published = true;
            recovery = publication.RetainedRecoveryDirectory;
            Fault(fault, Id65AuthoringMusicCandidateFaultPoint.AfterPublication);
            _ = VerifySet(request, input, plan, baseline, baselineReceiptSha256, output, cueBytes, CancellationToken.None,
                allowedResidue: [stage, preparationLease, Path.Combine(output, ".export-publication.lock"), recovery]);
            CleanupOwnedStage(stage);
            if (Directory.Exists(stage) && recovery.Length == 0) recovery = stage;
            return Result(output, receipt, reused: false, published: true, recovery);
        }
        catch (Exception exception) when (published)
        {
            throw new IOException($"Music candidate files were published at {output}, but a postpublication check failed. Preserve this directory for inspection; do not treat the candidate as verified. {exception.Message}", exception);
        }
        finally
        {
            // Recovery journals can still refer to staged files after a failed
            // rollback. Never delete that evidence or a published candidate.
            if (!HasRecoveryEvidence(output))
                CleanupOwnedStage(stage);
        }
    }

    private static async Task<Id65AuthoringMusicDiscPlan> CaptureFreshPlanAsync(
        Id65AuthoringMusicCandidateRequest request, Id65AuthoringMusicBuildInput input, CancellationToken cancellationToken)
    {
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard.RequireExactCanonicalAuthoredLayer(request.Paths);
        Id65AuthoringEditorDocumentStoreResult stored = await Id65AuthoringEditorDocumentStore.LoadAsync(
            request.Paths, request.ExpectedManifest, new(null), cancellationToken)
            ?? throw new InvalidDataException("The canonical music baseline document is missing.");
        input.RequireExactBaseline(stored.Document, stored.EnvelopeCanonicalSha256);
        Id65AuthoringEditorNativeInputProvider.InputSet native = await Id65AuthoringEditorNativeInputProvider.LoadAsync(
            request.Paths, request.ExpectedManifest, cancellationToken);
        UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3 =
            UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(request.Paths.LockedBaseImagePath);
        cancellationToken.ThrowIfCancellationRequested();
        return Id65AuthoringMusicDiscPlan.Compile(input,
            new(stored, input.BaselineEnvelopeCanonicalSha256, request.ExpectedManifest, native, v3));
    }

    private static async Task RequireFreshPlanAsync(Id65AuthoringMusicCandidateRequest request,
        Id65AuthoringMusicBuildInput input, Id65AuthoringMusicDiscPlan expected, CancellationToken cancellationToken)
    {
        Id65AuthoringMusicDiscPlan fresh = await CaptureFreshPlanAsync(request, input, cancellationToken);
        if (fresh.DeterministicPlanSha256 != expected.DeterministicPlanSha256)
            throw new InvalidDataException("The authored music plan changed before candidate publication or reuse.");
    }

    private static void ApplyMusicToStagedCopy(string stagedImage, Id65AuthoringMusicDiscPlan plan)
    {
        DiscLayout layout = DiscImage.DetectLayout(stagedImage);
        if (layout.SectorSize != 2352 || layout.UserOffset != 24)
            throw new InvalidDataException("Music candidates require the exact frozen MODE2/2352 layout.");
        using FileStream image = new(stagedImage, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        DiscFileRecord executable = DiscImage.FindRootFileRecord(image, layout, name => name == "SCUS_942.28");
        if (executable.Lba != Id65AuthoringMusicDiscPlan.ExecutableLba || executable.Size != Id65AuthoringMusicDiscPlan.ExecutableByteLength)
            throw new InvalidDataException("The staged music executable has a different ISO extent.");
        byte[] source = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size);
        Id65MusicSourceCertificate certificate = Id65MusicSourceCertificate.FromFrozenExecutable(source);
        if (certificate.SourceExecutableSha256 != plan.SourceExecutableSha256)
            throw new InvalidDataException("The staged music executable certificate changed.");
        Id65AuthoringMusicDiscWrite[] writes = plan.Writes.Where(write => write.Address.Original.Space.Kind == NativeAddressKind.ExecutableFile).ToArray();
        if (writes.Length != 5) throw new InvalidDataException("The exact music five-window transaction changed.");
        foreach (Id65AuthoringMusicDiscWrite write in writes)
        {
            byte[] before = DiscImage.ReadFileBytes(image, layout, 0, write.Address.Final.Offset, checked((int)write.Address.Final.ByteLength));
            if (!before.SequenceEqual(write.CopyBefore())) throw new InvalidDataException("A staged music window has a stale preimage.");
            DiscImage.WriteFileBytes(image, layout, 0, write.Address.Final.Offset, write.CopyAfter());
        }
        image.Flush(flushToDisk: true);
        plan.RequireLogicalReadback(
            DiscImage.ReadFileBytes(image, layout, 0, plan.Wad80DiscExtent.Offset, checked((int)plan.Wad80DiscExtent.ByteLength)),
            DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size));
    }

    private static Id65AuthoringMusicCandidateReceipt VerifySet(Id65AuthoringMusicCandidateRequest request,
        Id65AuthoringMusicBuildInput input, Id65AuthoringMusicDiscPlan plan,
        Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult baseline,
        string baselineReceiptSha256, string directory, byte[] cueBytes, CancellationToken cancellationToken,
        IReadOnlyList<string>? allowedResidue = null)
    {
        RequireSafePath(request.Paths.RootPath, directory);
        if (!InspectExistingSet(directory, allowedResidue)) throw new InvalidDataException("The music candidate set is incomplete.");
        Id65AuthoringMusicPhysicalReadback readback = VerifyImage(request.Paths.LockedBaseImagePath, Path.Combine(directory, ImageFileName), plan, cancellationToken);
        Id65AuthoringMusicCandidateReceipt expected = BuildReceipt(input, plan, baseline, baselineReceiptSha256, cueBytes, readback);
        RequireFileBytes(Path.Combine(directory, InputFileName), input.SerializeUtf8());
        RequireFileBytes(Path.Combine(directory, CueFileName), cueBytes);
        RequireFileBytes(Path.Combine(directory, ReceiptFileName), SerializeReceipt(expected));
        return expected;
    }

    private static Id65AuthoringMusicPhysicalReadback VerifyImage(string source, string image, Id65AuthoringMusicDiscPlan plan, CancellationToken cancellationToken)
    {
        using FileStream sourceStream = File.OpenRead(source), imageStream = File.OpenRead(image);
        return Id65AuthoringMusicDiscReadback.Verify(plan, sourceStream, imageStream, cancellationToken);
    }

    private static Id65AuthoringMusicCandidateReceipt BuildReceipt(Id65AuthoringMusicBuildInput input,
        Id65AuthoringMusicDiscPlan plan, Id65AuthoringBlankCycloramaAggregateCandidateMaterializationResult baseline,
        string baselineReceiptSha256, byte[] cueBytes, Id65AuthoringMusicPhysicalReadback readback) => new(
        "spyro-editor-id65-authored-music-candidate", 1, input.CanonicalSha256,
        input.BaselineDocumentCanonicalSha256, input.BaselineEnvelopeCanonicalSha256,
        plan.BaselinePlan.NativeInputSetSha256, plan.BaselinePlan.DeterministicAggregatePlanSha256,
        baseline.Receipt.OutputImageSha256, baselineReceiptSha256,
        readback.SourceImageSha256, readback.OutputImageSha256, plan.DeterministicPlanSha256, input.SelectedTrackId,
        plan.SourceExecutableSha256, plan.OutputExecutableSha256, plan.OutputRow80Sha256, Id65AuthoringMusicDiscPlan.Hash(cueBytes),
        readback.VerifiedOwnedRawSectorCount, readback.ChangedRawSectorCount, readback.ChangedPhysicalByteCount,
        ExceptionRollbackPublication: true, CrashAtomicPublication: false, RuntimeProven: false, NormalCreateBinEnabled: false);

    private static Id65AuthoringMusicCandidateResult Result(string directory, Id65AuthoringMusicCandidateReceipt receipt,
        bool reused, bool published, string recovery) => new(directory, Path.Combine(directory, ImageFileName), Path.Combine(directory, CueFileName),
        Path.Combine(directory, InputFileName), Path.Combine(directory, ReceiptFileName), receipt, reused, published, recovery);

    private static bool InspectExistingSet(string directory, IReadOnlyList<string>? allowedResidue = null)
    {
        FileInfo file = new(directory); DirectoryInfo folder = new(directory);
        if (file.LinkTarget is not null || folder.LinkTarget is not null || File.Exists(directory))
            throw new InvalidDataException("The music candidate directory is an alias or file.");
        if (!Directory.Exists(directory)) return false;
        string[] entries = Directory.EnumerateFileSystemEntries(directory)
            .Where(path => allowedResidue is null || !allowedResidue.Contains(path, StringComparer.Ordinal)).ToArray();
        if (entries.Length == 0) return false;
        if (entries.Length != ArtifactNames.Length || entries.Any(path => !ArtifactNames.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
            throw new InvalidDataException("The music candidate set is incomplete, conflicting or retains recovery evidence; it will not be overwritten.");
        foreach (string entry in entries)
        {
            RequireSafePath(directory, entry);
            if (Directory.Exists(entry) || !File.Exists(entry)) throw new InvalidDataException("A music candidate artifact is not a regular file.");
        }
        return true;
    }

    private static void RequireSafePath(string root, string path)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        root = Path.GetFullPath(root); path = Path.GetFullPath(path);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current);
            info.Refresh();
            if (info.LinkTarget is not null || info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A music candidate path must not contain symbolic links or reparse points.");
            if (string.Equals(current, root, StringComparison.Ordinal)) break;
        }
    }

    private static void RequireFileBytes(string path, byte[] expected)
    {
        FileInfo file = new(path);
        if (!file.Exists || file.Length != expected.Length || !File.ReadAllBytes(path).SequenceEqual(expected))
            throw new InvalidDataException("A music candidate input, CUE or receipt has a different exact identity.");
    }
    private static async Task WriteNewDurablyAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await stream.WriteAsync(bytes, cancellationToken); stream.Flush(flushToDisk: true);
    }
    private static void CleanupOwnedStage(string stage)
    {
        try
        {
            if (!Directory.Exists(stage)) return;
            if (new DirectoryInfo(stage).LinkTarget is not null) return;
            foreach (string name in ArtifactNames)
            {
                FileInfo file = new(Path.Combine(stage, name));
                if (file.Exists && file.LinkTarget is null) file.Delete();
            }
            Directory.Delete(stage, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
    private static bool HasRecoveryEvidence(string output)
    {
        try
        {
            return File.Exists(Path.Combine(output, ".export-publication.lock")) ||
                Directory.EnumerateDirectories(output, ".export-recovery-*").Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return true; }
    }
    private static byte[] SerializeReceipt(Id65AuthoringMusicCandidateReceipt receipt) => JsonSerializer.SerializeToUtf8Bytes(receipt, JsonOptions);
    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }
    private static void Fault(Id65AuthoringMusicCandidateFaultPoint selected, Id65AuthoringMusicCandidateFaultPoint current)
    {
        if (selected == current) throw new InvalidOperationException("Injected authored music candidate fault at " + current);
    }
}
