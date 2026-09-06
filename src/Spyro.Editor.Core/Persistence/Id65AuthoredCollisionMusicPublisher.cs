using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65AuthoredCollisionMusicRequest(UnusedLevel65BlankLevelLabWorkspacePaths Paths,
    UnusedLevel65BlankLevelLabManifest Manifest, Id65AuthoredCollisionMusicInput Input);
internal sealed record Id65AuthoredCollisionMusicResult(string Directory, string Image, string Cue, string Input,
    string Receipt, Id65AuthoredCollisionMusicReadback Readback, bool Reused, string RetainedRecoveryDirectory);
internal enum Id65AuthoredCollisionMusicFault { None, AfterCopy, AfterWrite, AfterReadback, BeforePublication, AfterPublication }

/// <summary>
/// Rebuilds a new immutable research candidate from the frozen source, never
/// from an old candidate. Its single combined input is published with the
/// image, evidence and CUE. Existing document/music inputs remain unchanged.
/// Uses the shared exception-rollback transaction; power-loss and hostile
/// filesystem atomicity are not claimed. No editable sidecar or UI capability.
/// </summary>
internal static class Id65AuthoredCollisionMusicPublisher
{
    public const string RootName = "materialized-source-bound-collision-music-candidates";
    public const string ImageName = "id65-authored-collision-music.bin";
    public const string CueName = "id65-authored-collision-music.cue";
    public const string InputName = "authored-input.json";
    public const string ReceiptName = "candidate.receipt.json";
    private static readonly string[] Names = [ImageName, InputName, ReceiptName, CueName];

    public static Task<Id65AuthoredCollisionMusicResult> PublishAsync(Id65AuthoredCollisionMusicRequest request,
        CancellationToken cancellationToken = default) => PublishCoreAsync(request, Id65AuthoredCollisionMusicFault.None, null, cancellationToken);
    internal static Task<Id65AuthoredCollisionMusicResult> PublishForSmokeAsync(Id65AuthoredCollisionMusicRequest request,
        Id65AuthoredCollisionMusicFault fault, Action<string>? beforeCommit = null, CancellationToken cancellationToken = default) =>
        PublishCoreAsync(request, fault, beforeCommit, cancellationToken);

    internal static async Task<Id65AuthoredCollisionMusicPlan> CaptureAsync(Id65AuthoredCollisionMusicRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Paths);
        ArgumentNullException.ThrowIfNull(request.Manifest); ArgumentNullException.ThrowIfNull(request.Input);
        Id65AuthoringEditorAggregateCandidateWorkspaceGuard.RequireExactCanonicalAuthoredLayer(request.Paths);
        var stored = await Id65AuthoringEditorDocumentStore.LoadAsync(request.Paths, request.Manifest, new(null), cancellationToken)
            ?? throw new InvalidDataException("The exact authored collision/music baseline document is missing.");
        request.Input.Music.RequireExactBaseline(stored.Document, stored.EnvelopeCanonicalSha256);
        var native = await Id65AuthoringEditorNativeInputProvider.LoadAsync(request.Paths, request.Manifest, cancellationToken);
        var v3 = UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(request.Paths.LockedBaseImagePath);
        cancellationToken.ThrowIfCancellationRequested();
        return Id65AuthoredCollisionMusicPlan.Compile(request.Input, new(stored, stored.EnvelopeCanonicalSha256, request.Manifest, native, v3));
    }

    private static async Task<Id65AuthoredCollisionMusicResult> PublishCoreAsync(Id65AuthoredCollisionMusicRequest request,
        Id65AuthoredCollisionMusicFault fault, Action<string>? beforeCommit, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(fault)) throw new ArgumentOutOfRangeException(nameof(fault));
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(request); ArgumentNullException.ThrowIfNull(request.Paths);
        SourceCueSnapshot sourceCue = SourceCueSnapshot.Capture(request.Paths);
        Id65AuthoredCollisionMusicPlan plan = await CaptureAsync(request, cancellationToken);
        sourceCue.RequireFresh(request.Paths);
        string output = Path.Combine(request.Paths.RootPath, RootName, "composition-" + plan.DeterministicPlanSha256);
        RequireSafePath(request.Paths.RootPath, output);
        byte[] cue = sourceCue.CopyOutputBytes();
        if (InspectSet(output))
        {
            var readback = VerifySet(request, plan, output, sourceCue, cancellationToken);
            beforeCommit?.Invoke(output); // Smoke-only seam before exact reuse acceptance.
            await RequireFreshAsync(request, plan, sourceCue, cancellationToken);
            if (VerifySet(request, plan, output, sourceCue, cancellationToken) != readback)
                throw new InvalidDataException("An existing composition changed during exact reuse.");
            return Result(output, readback, true, "");
        }
        Directory.CreateDirectory(output);
        RequireSafePath(request.Paths.RootPath, output);
        string lease = Path.Combine(output, ".composition-preparation.lock");
        using FileStream preparation = new(lease, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.DeleteOnClose);
        string stage = Path.Combine(output, ".composition-stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage); RequireSafePath(output, stage);
        bool published = false; string recovery = "";
        try
        {
            string stagedImage = Path.Combine(stage, ImageName);
            using (FileStream source = new(request.Paths.LockedBaseImagePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (FileStream destination = new(stagedImage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                if (source.Length != Id65AuthoredCollisionMusicPlan.ImageBytes)
                    throw new InvalidDataException("The frozen composition source image length changed.");
                await source.CopyToAsync(destination, cancellationToken);
                Inject(fault, Id65AuthoredCollisionMusicFault.AfterCopy);
                byte[] raw = new byte[Id65AuthoredCollisionMusicPlan.RawBytes];
                foreach (int sector in plan.AffectedRawSectors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long offset = (long)sector * raw.Length;
                    source.Position = offset; source.ReadExactly(raw);
                    byte[] expected = plan.CreateExpectedRawSector(sector, raw);
                    destination.Position = offset; destination.Write(expected);
                }
                destination.Flush(flushToDisk: true);
            }
            Inject(fault, Id65AuthoredCollisionMusicFault.AfterWrite);
            var readback = VerifyImage(request, plan, stagedImage, cancellationToken);
            WriteNew(Path.Combine(stage, InputName), plan.Input.SerializeUtf8());
            WriteNew(Path.Combine(stage, ReceiptName), BuildReceipt(plan, readback, cue));
            WriteNew(Path.Combine(stage, CueName), cue);
            Inject(fault, Id65AuthoredCollisionMusicFault.AfterReadback);
            beforeCommit?.Invoke(output);
            await RequireFreshAsync(request, plan, sourceCue, cancellationToken);
            _ = VerifySet(request, plan, stage, sourceCue, cancellationToken);
            Inject(fault, Id65AuthoredCollisionMusicFault.BeforePublication);
            RequireSafePath(request.Paths.RootPath, output);
            if (Directory.EnumerateFileSystemEntries(output).Any(path => path != stage && path != lease))
                throw new InvalidDataException("A competing or incomplete composition artifact appeared before publication.");
            sourceCue.RequireFresh(request.Paths);
            var transaction = ExportArtifactTransaction.PublishNew(output,
                Names.Select(name => new ExportArtifactReplacement(Path.Combine(output, name), Path.Combine(stage, name))).ToArray(), cancellationToken);
            published = true; recovery = transaction.RetainedRecoveryDirectory;
            Inject(fault, Id65AuthoredCollisionMusicFault.AfterPublication);
            var final = VerifySet(request, plan, output, sourceCue, CancellationToken.None,
                [stage, lease, Path.Combine(output, ".export-publication.lock"), recovery]);
            if (final != readback) throw new InvalidDataException("Committed composition physical evidence changed.");
            if (!HasRecoveryEvidence(output)) Cleanup(stage);
            if (Directory.Exists(stage) && recovery.Length == 0) recovery = stage;
            return Result(output, final, false, recovery);
        }
        catch (Exception exception) when (published)
        {
            throw new IOException("The authored collision/music candidate was published but final verification failed. Preserve: " + output, exception);
        }
        finally
        {
            if (!HasRecoveryEvidence(output)) Cleanup(stage);
        }
    }
    private static async Task RequireFreshAsync(Id65AuthoredCollisionMusicRequest request, Id65AuthoredCollisionMusicPlan expected,
        SourceCueSnapshot sourceCue, CancellationToken cancellationToken)
    {
        sourceCue.RequireFresh(request.Paths);
        var fresh = await CaptureAsync(request, cancellationToken);
        sourceCue.RequireFresh(request.Paths);
        if (fresh.DeterministicPlanSha256 != expected.DeterministicPlanSha256)
            throw new InvalidDataException("The source-bound authored composition changed before publication or reuse.");
    }
    private static Id65AuthoredCollisionMusicReadback VerifyImage(Id65AuthoredCollisionMusicRequest request,
        Id65AuthoredCollisionMusicPlan plan, string image, CancellationToken cancellationToken)
    {
        using FileStream source = File.OpenRead(request.Paths.LockedBaseImagePath), output = File.OpenRead(image);
        return plan.Verify(source, output, cancellationToken);
    }
    private static Id65AuthoredCollisionMusicReadback VerifySet(Id65AuthoredCollisionMusicRequest request,
        Id65AuthoredCollisionMusicPlan plan, string directory, SourceCueSnapshot sourceCue, CancellationToken cancellationToken,
        IReadOnlyList<string>? allowedResidue = null)
    {
        sourceCue.RequireFresh(request.Paths);
        byte[] cue = sourceCue.CopyOutputBytes();
        RequireSafePath(request.Paths.RootPath, directory);
        if (!InspectSet(directory, allowedResidue)) throw new InvalidDataException("The composition artifact set is absent.");
        var readback = VerifyImage(request, plan, Path.Combine(directory, ImageName), cancellationToken);
        foreach (var (name, expected) in new[] { (InputName, plan.Input.SerializeUtf8()), (ReceiptName, BuildReceipt(plan, readback, cue)), (CueName, cue) })
        {
            FileInfo file = new(Path.Combine(directory, name));
            if (file.Length != expected.Length || !File.ReadAllBytes(file.FullName).SequenceEqual(expected))
                throw new InvalidDataException("A composition input, receipt or CUE has a different exact identity.");
        }
        sourceCue.RequireFresh(request.Paths);
        return readback;
    }

    // Publication companion identity is deliberately separate from the pure
    // BIN plan and historical receipt format. Bind the exact original bytes,
    // not merely the rewritten output CUE: an equivalent FILE spelling can
    // normalize to the same output while the source changes underneath us.
    internal sealed class SourceCueSnapshot
    {
        private readonly string _cuePath, _imagePath;
        private readonly byte[] _sourceBytes, _outputBytes;
        private SourceCueSnapshot(string cuePath, string imagePath, byte[] sourceBytes, byte[] outputBytes)
        { _cuePath = cuePath; _imagePath = imagePath; _sourceBytes = sourceBytes; _outputBytes = outputBytes; }

        internal static SourceCueSnapshot Capture(UnusedLevel65BlankLevelLabWorkspacePaths paths)
        {
            byte[] sourceBytes = ReadSourceBytes(paths);
            if (!DiscCueSheet.ReferencesDataImage(paths.LockedBaseCuePath, paths.LockedBaseImagePath))
                throw new InvalidDataException("The source CUE lost its exact locked data-image reference.");
            byte[] outputBytes = DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(
                paths.LockedBaseCuePath, paths.LockedBaseImagePath, ImageName));
            var result = new SourceCueSnapshot(Path.GetFullPath(paths.LockedBaseCuePath),
                Path.GetFullPath(paths.LockedBaseImagePath), sourceBytes, outputBytes);
            result.RequireFresh(paths);
            return result;
        }
        internal byte[] CopyOutputBytes() => _outputBytes.ToArray();
        internal void RequireFresh(UnusedLevel65BlankLevelLabWorkspacePaths paths)
        {
            if (!string.Equals(_cuePath, Path.GetFullPath(paths.LockedBaseCuePath), StringComparison.Ordinal) ||
                !string.Equals(_imagePath, Path.GetFullPath(paths.LockedBaseImagePath), StringComparison.Ordinal) ||
                !_sourceBytes.AsSpan().SequenceEqual(ReadSourceBytes(paths)))
                throw new InvalidDataException("The source CUE changed its exact bytes or data-image binding before publication or reuse.");
        }
        private static byte[] ReadSourceBytes(UnusedLevel65BlankLevelLabWorkspacePaths paths)
        {
            try
            {
                RequireSafePath(paths.RootPath, paths.LockedBaseCuePath);
                RequireSafePath(paths.RootPath, paths.LockedBaseImagePath);
                return File.ReadAllBytes(paths.LockedBaseCuePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException)
            { throw new InvalidDataException("The source CUE is missing, unreadable or no longer bound to regular locked-source paths.", exception); }
        }
    }
    private static byte[] BuildReceipt(Id65AuthoredCollisionMusicPlan plan, Id65AuthoredCollisionMusicReadback readback, byte[] cue) =>
        JsonSerializer.SerializeToUtf8Bytes(new
        {
            format = "spyro-editor-id65-authored-collision-music-candidate", schemaVersion = 1,
            compilerProfile = Id65AuthoredCollisionMusicPlan.ProfileId, inputProfile = Id65AuthoredCollisionMusicInput.ProfileId,
            inputSha256 = plan.Input.CanonicalSha256, documentSha256 = plan.Input.Music.BaselineDocumentCanonicalSha256,
            documentEnvelopeSha256 = plan.Input.Music.BaselineEnvelopeCanonicalSha256,
            substrate = plan.Input.Substrate.ToString(), selectedTrackId = plan.Input.Music.SelectedTrackId,
            sourceImageSha256 = plan.SourceImageSha256, nativeInputSetSha256 = plan.NativeInputSetSha256,
            baselineAggregatePlanSha256 = plan.BaselineAggregatePlanSha256, collisionPlanSha256 = plan.CollisionPlanSha256,
            planSha256 = plan.DeterministicPlanSha256, outputRow80Sha256 = plan.OutputRow80Sha256,
            sourceExecutableSha256 = plan.SourceExecutableSha256, outputExecutableSha256 = plan.OutputExecutableSha256,
            outputImageSha256 = readback.OutputImageSha256, cueSha256 = Id65AuthoredCollisionMusicPlan.Hash(cue),
            readback, collision = plan.Collision,
            protectedInheritedStorage = new[] { "native-triangle-rows-assignments-flags", "fixed-model-component-capacities",
                "source-object-rows-and-native-dependencies", "35-part-cyclorama-structure", "opaque-sky-visibility-tail",
                "special-surfaces-particles-sound", "source-player-save-totals-exit-code", "retail-music-tables-and-audio-streams" },
            rebuiltFromLockedSource = true, oldCandidateUsedAsInput = false,
            wholeImageReadback = true, exactLogicalInverse = true, exceptionRollbackPublication = true,
            crashAtomicPublication = false, hostileFilesystemRaceSafe = false,
            inputRoundtripSupported = true, editableProjectMusicPersistence = false,
            independentMinimalNativeLevel = false, runtimeProven = false, appIntegrated = false, normalCreateBinEnabled = false
        });
    private static bool InspectSet(string directory, IReadOnlyList<string>? allowedResidue = null)
    {
        if (File.Exists(directory)) throw new InvalidDataException("The composition destination is a file.");
        if (!Directory.Exists(directory)) return false;
        string[] entries = Directory.EnumerateFileSystemEntries(directory).Where(p => allowedResidue?.Contains(p, StringComparer.Ordinal) != true).ToArray();
        if (entries.Length == 0) return false;
        if (entries.Length != Names.Length || entries.Any(path => !Names.Contains(Path.GetFileName(path), StringComparer.Ordinal)))
            throw new InvalidDataException("The composition destination contains partial or competing artifacts.");
        foreach (string entry in entries)
        {
            FileInfo info = new(entry);
            if (!info.Exists || info.LinkTarget is not null || (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("A composition artifact is missing, linked or not a regular file.");
        }
        return true;
    }
    private static void RequireSafePath(string root, string path)
    {
        PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        root = Path.GetFullPath(root); path = Path.GetFullPath(path);
        for (string? current = path; current is not null; current = Path.GetDirectoryName(current))
        {
            FileSystemInfo info = Directory.Exists(current) ? new DirectoryInfo(current) : new FileInfo(current); info.Refresh();
            if (info.LinkTarget is not null || info.Exists && (info.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("An authored composition path traverses a linked directory.");
            if (string.Equals(current, root, StringComparison.Ordinal)) break;
        }
    }
    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(flushToDisk: true);
    }
    private static bool HasRecoveryEvidence(string output)
    {
        try { return File.Exists(Path.Combine(output, ".export-publication.lock")) || Directory.EnumerateDirectories(output, ".export-recovery-*").Any(); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return true; }
    }
    private static void Cleanup(string stage)
    {
        try
        {
            if (!Directory.Exists(stage) || new DirectoryInfo(stage).LinkTarget is not null) return;
            foreach (string name in Names)
            {
                FileInfo file = new(Path.Combine(stage, name));
                if (file.Exists && file.LinkTarget is null) file.Delete();
            }
            Directory.Delete(stage, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
    private static Id65AuthoredCollisionMusicResult Result(string directory, Id65AuthoredCollisionMusicReadback readback, bool reused, string recovery) =>
        new(directory, Path.Combine(directory, ImageName), Path.Combine(directory, CueName), Path.Combine(directory, InputName),
            Path.Combine(directory, ReceiptName), readback, reused, recovery);
    private static void Inject(Id65AuthoredCollisionMusicFault chosen, Id65AuthoredCollisionMusicFault current)
    {
        if (chosen == current) throw new InvalidOperationException("Injected authored collision/music fault at " + current);
    }
}
