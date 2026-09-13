using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// Creates a new private input for the existing normal export transaction. ON
/// is flight-only; the dispatcher must reject ordinary edits before calling.
/// OFF starts from the complete original image, retaining the original layout
/// for subsequent ordinary writers. This does not publish or certify gameplay.
/// Source/output ancestors must be trusted, as with the existing exporter.
/// </summary>
public static class NativeFreeFlightExportStage
{
    public static Task<NativeFreeFlightExportStageResult> CreateAsync(NativeFreeFlightProjectExportIntent intent,
        string workspaceRoot, string originalImagePath, string originalCuePath, string outputPrefix,
        CancellationToken cancellationToken = default) => NativeFreeFlightExportStageResult.CreateAsync(intent,
            workspaceRoot, originalImagePath, originalCuePath, outputPrefix, cancellationToken);
}

/// <summary>
/// Evidence for exactly one disposable stage, not a final export receipt. The
/// retained source/intent check is separate from VerifyAsync because an ordinary
/// OFF writer may legitimately consume this stage before final publication.
/// </summary>
public sealed class NativeFreeFlightExportStageResult
{
    private readonly NativeFreeFlightProjectExportIntent _intent;
    private readonly string _workspaceRoot;
    private readonly NativeFreeFlightImageComposition _composition;
    private readonly NativeFreeFlightResearchSourceCue _cue;
    private readonly byte[] _outputCue;

    private NativeFreeFlightExportStageResult(NativeFreeFlightProjectExportIntent intent, string workspaceRoot,
        NativeFreeFlightImageComposition composition, NativeFreeFlightResearchSourceCue cue,
        string imagePath, string cuePath, (string Hash, int Changed) verified)
    {
        _intent = intent; _workspaceRoot = workspaceRoot; _composition = composition; _cue = cue;
        _outputCue = cue.CopyOutputBytes(); OutputImagePath = imagePath; OutputCuePath = cuePath;
        OutputImageSha256 = verified.Hash; ChangedRawSectors = verified.Changed; EnabledLevelId = intent.EnabledLevelId;
    }

    public string OutputImagePath { get; }
    public string OutputCuePath { get; }
    public string SourceImageSha256 => NativeFreeFlightPatchPlan.SourceImageSha256;
    public string SourceCueSha256 => _cue.SourceSha256;
    public string OutputImageSha256 { get; }
    public int? EnabledLevelId { get; }
    public int ChangedRawSectors { get; }

    public async Task RequireCurrentAsync(string workspaceRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _intent.RequireCurrent(workspaceRoot);
        _cue.RequireFresh();
        await _composition.RequireOriginalAsync(cancellationToken);
        _cue.RequireFresh();
        _intent.RequireCurrent(workspaceRoot);
    }

    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        await RequireCurrentAsync(_workspaceRoot, cancellationToken);
        RequirePlainOutputFile(OutputImagePath);
        RequirePlainOutputFile(OutputCuePath);
        RequireCue(OutputCuePath, OutputImagePath, _outputCue);
        var verified = await Task.Run(() => _composition.VerifyImage(OutputImagePath), cancellationToken);
        if (verified.Hash != OutputImageSha256 || verified.Changed != ChangedRawSectors)
            throw new InvalidDataException("Disposable flight image differs from its complete retained afterimage.");
        RequirePlainOutputFile(OutputImagePath);
        RequireCue(OutputCuePath, OutputImagePath, _outputCue);
        await RequireCurrentAsync(_workspaceRoot, cancellationToken);
    }

    internal static async Task<NativeFreeFlightExportStageResult> CreateAsync(NativeFreeFlightProjectExportIntent intent,
        string workspaceRoot, string originalImagePath, string originalCuePath, string outputPrefix,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        intent.RequireCurrent(workspaceRoot);
        if (!intent.RequiresRebuild)
            throw new InvalidDataException("A disposable flight stage requires an explicit saved ON or OFF choice.");
        if (intent.EnabledLevelId != null && intent.EnabledLevelId != NativeFreeFlightImageComposition.TargetLevelId)
            throw new InvalidDataException("Production flight ON requires the exact Toasty destination.");
        ArgumentException.ThrowIfNullOrWhiteSpace(originalImagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(originalCuePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPrefix);
        string workspace = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workspaceRoot));
        string source = Path.GetFullPath(originalImagePath), sourceCue = Path.GetFullPath(originalCuePath);
        string prefix = Path.GetFullPath(outputPrefix), imagePath = prefix + ".bin", cuePath = prefix + ".cue";
        string parent = Path.GetDirectoryName(prefix)!;
        RequireOutputDirectory(parent);
        foreach (string leaf in new[] { imagePath, cuePath })
        {
            PersistencePathSafety.EnsureWritePathWithinRoot(parent, leaf);
            PersistencePathSafety.EnsureTreesDoNotOverlap(source, leaf, "Disposable flight output overlaps its original BIN.");
            PersistencePathSafety.EnsureTreesDoNotOverlap(sourceCue, leaf, "Disposable flight output overlaps its original CUE.");
            RequireAbsentOutput(leaf);
        }
        var cue = NativeFreeFlightResearchSourceCue.Capture(sourceCue, source, Path.GetFileName(imagePath));
        var composition = await NativeFreeFlightImageComposition.CaptureProductionAsync(source, intent.EnabledLevelId, cancellationToken);
        if (composition.TestSelector != null || composition.ExecutablePatch.Enabled != (intent.EnabledLevelId != null))
            throw new InvalidDataException("Production flight composition cannot include a diagnostic selector or a different saved choice.");
        cue.RequireFresh();
        intent.RequireCurrent(workspace);
        cancellationToken.ThrowIfCancellationRequested();

        // The relocator's replacement behavior is confined to this invocation's
        // new private directory. Final disposable leaves are moved without any
        // overwrite; the App's existing transaction alone publishes to users.
        string scratch = Path.Combine(parent, ".free-flight-stage-" + Guid.NewGuid().ToString("N"));
        RequireAbsentOutput(scratch);
        Directory.CreateDirectory(scratch);
        string scratchImage = Path.Combine(scratch, Path.GetFileName(imagePath));
        string scratchCue = Path.Combine(scratch, Path.GetFileName(cuePath));
        string analysis = Path.Combine(scratch, "source-wad-analysis.json");
        bool movedImage = false, movedCue = false, completed = false;
        try
        {
            if (composition.AnimationMerge != null) WriteNew(analysis, composition.CopyAnalysisBytes());
            await composition.WriteInitialImageAsync(scratchImage, analysis, cancellationToken);
            composition.ApplyExecutableToImage(scratchImage);
            WriteNew(scratchCue, cue.CopyOutputBytes());
            var verified = await Task.Run(() => composition.VerifyImage(scratchImage), cancellationToken);
            RequireCue(scratchCue, scratchImage, cue.CopyOutputBytes());
            if (intent.EnabledLevelId == null && (verified.Changed != 0 || verified.Hash != NativeFreeFlightPatchPlan.SourceImageSha256))
                throw new InvalidDataException("OFF must rebuild the complete original image, not just restore executable instructions.");
            cue.RequireFresh();
            await composition.RequireOriginalAsync(cancellationToken);
            cue.RequireFresh();
            intent.RequireCurrent(workspace);
            cancellationToken.ThrowIfCancellationRequested();
            RequireOutputDirectory(parent);
            RequireAbsentOutput(imagePath);
            RequireAbsentOutput(cuePath);
            File.Move(scratchImage, imagePath);
            movedImage = true;
            File.Move(scratchCue, cuePath);
            movedCue = true;
            var result = new NativeFreeFlightExportStageResult(intent, workspace, composition, cue, imagePath, cuePath, verified);
            await result.VerifyAsync(cancellationToken);
            completed = true;
            return result;
        }
        finally
        {
            if (!completed)
            {
                if (movedCue) DeleteOwned(cuePath);
                if (movedImage) DeleteOwned(imagePath);
            }
            foreach (string owned in new[] { scratchImage, scratchCue, analysis }) DeleteOwned(owned);
            if (Directory.Exists(scratch) && !Directory.EnumerateFileSystemEntries(scratch).Any()) Directory.Delete(scratch);
        }
    }

    private static void RequireOutputDirectory(string path)
    {
        if (!Directory.Exists(path) || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("Disposable flight output requires an existing, unlinked private directory.");
    }

    private static void RequireAbsentOutput(string path)
    {
        if (File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget != null)
            throw new IOException("Disposable flight output is occupied or linked and will not be replaced: " + path);
    }

    private static void RequirePlainOutputFile(string path)
    {
        RequireOutputDirectory(Path.GetDirectoryName(path)!);
        if (!File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget != null)
            throw new InvalidDataException("Disposable flight output is missing, linked or not a file.");
    }

    private static void RequireCue(string cuePath, string imagePath, byte[] expected)
    {
        RequirePlainOutputFile(cuePath);
        using FileStream input = new(cuePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != expected.Length)
            throw new InvalidDataException("Disposable flight CUE changed its exact byte length.");
        byte[] actual = new byte[expected.Length];
        input.ReadExactly(actual);
        if (input.Length != actual.Length || !actual.AsSpan().SequenceEqual(expected) ||
            !DiscCueSheet.ReferencesDataImage(cuePath, imagePath))
            throw new InvalidDataException("Disposable flight CUE changed its exact bytes or image binding.");
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(flushToDisk: true);
    }

    private static void DeleteOwned(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
