using Spyro.Editor.Core.Levels;

namespace Spyro.Editor.Core.Exporting;

/// <summary>
/// One process-local proof that a full retail BIN hash and the destination
/// texture-component/level-data hashes were captured while the source file
/// identity was stable. The constructor is internal so callers can reuse only
/// proofs produced by the checked factory. It is accepted by staging preflight
/// only; normal Create BIN continues to inspect and hash the source afresh.
/// </summary>
public sealed class NativeTerrainTexturePrivateRecordStagingSourceProof
{
    internal NativeTerrainTexturePrivateRecordStagingSourceProof(
        string sourceImagePath,
        string resolvedSourceImagePath,
        long sourceImageLength,
        long sourceCreationTimeUtcTicks,
        long sourceLastWriteTimeUtcTicks,
        NativeTerrainTextureRecordAppendSourceBinding sourceBinding)
    {
        SourceImagePath = sourceImagePath;
        ResolvedSourceImagePath = resolvedSourceImagePath;
        SourceImageLength = sourceImageLength;
        SourceCreationTimeUtcTicks = sourceCreationTimeUtcTicks;
        SourceLastWriteTimeUtcTicks = sourceLastWriteTimeUtcTicks;
        SourceBinding = sourceBinding;
    }

    public string SourceImagePath { get; }
    public NativeTerrainTextureRecordAppendSourceBinding SourceBinding { get; }

    internal string ResolvedSourceImagePath { get; }
    internal long SourceImageLength { get; }
    internal long SourceCreationTimeUtcTicks { get; }
    internal long SourceLastWriteTimeUtcTicks { get; }

    public static NativeTerrainTexturePrivateRecordStagingSourceProof Capture(
        string sourceImagePath,
        LevelDefinition targetLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceImagePath);
        ArgumentNullException.ThrowIfNull(targetLevel);

        SourceStamp before = CaptureStamp(sourceImagePath);
        NativeTerrainTextureRecordAppendSourceBinding binding =
            NativeTerrainTextureRecordAppendBuilder.InspectSourceBinding(
                before.RequestedFullPath,
                targetLevel);
        SourceStamp after = CaptureStamp(sourceImagePath);
        if (before != after)
        {
            throw new IOException(
                "The source BIN changed while its private-texture staging proof was being captured.");
        }
        return new NativeTerrainTexturePrivateRecordStagingSourceProof(
            before.RequestedFullPath,
            before.ResolvedFullPath,
            before.Length,
            before.CreationTimeUtcTicks,
            before.LastWriteTimeUtcTicks,
            binding);
    }

    internal bool TryValidate(
        string sourceImagePath,
        LevelDefinition targetLevel,
        NativeTerrainTextureRecordAppendSourceBinding expectedBinding,
        out string failureReason)
    {
        failureReason = "";
        ArgumentNullException.ThrowIfNull(targetLevel);
        ArgumentNullException.ThrowIfNull(expectedBinding);
        SourceStamp current;
        try
        {
            current = CaptureStamp(sourceImagePath);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException)
        {
            failureReason = ex.Message;
            return false;
        }

        SourceStamp expected = new(
            SourceImagePath,
            ResolvedSourceImagePath,
            SourceImageLength,
            SourceCreationTimeUtcTicks,
            SourceLastWriteTimeUtcTicks);
        if (current != expected)
        {
            failureReason =
                "The source BIN path, resolved target, length, creation time, or modification time changed after the staging proof was captured.";
            return false;
        }
        if (!Equals(SourceBinding, expectedBinding))
        {
            failureReason =
                "The requested private-texture source binding differs from the captured staging proof.";
            return false;
        }
        if (!string.Equals(
                LevelCatalog.NormalizeKey(SourceBinding.TargetLevelKey),
                LevelCatalog.NormalizeKey(targetLevel.Key),
                StringComparison.OrdinalIgnoreCase) ||
            SourceBinding.TargetWadEntry != targetLevel.SourceWadEntry)
        {
            failureReason =
                "The captured private-texture staging proof belongs to a different destination level.";
            return false;
        }
        return true;
    }

    private static SourceStamp CaptureStamp(string sourceImagePath)
    {
        string fullPath = Path.GetFullPath(sourceImagePath);
        FileInfo source = new(fullPath);
        source.Refresh();
        if (!source.Exists)
            throw new FileNotFoundException("Missing retail source image.", fullPath);

        string resolvedPath = fullPath;
        FileInfo identity = source;
        try
        {
            if (source.ResolveLinkTarget(returnFinalTarget: true) is FileInfo resolved)
            {
                resolved.Refresh();
                if (resolved.Exists)
                {
                    resolvedPath = Path.GetFullPath(resolved.FullName);
                    identity = resolved;
                }
            }
        }
        catch (IOException)
        {
            // Keep the requested path identity. A later resolution change
            // still invalidates the proof if it becomes observable.
        }

        return new SourceStamp(
            fullPath,
            resolvedPath,
            identity.Length,
            identity.CreationTimeUtc.Ticks,
            identity.LastWriteTimeUtc.Ticks);
    }

    private sealed record SourceStamp(
        string RequestedFullPath,
        string ResolvedFullPath,
        long Length,
        long CreationTimeUtcTicks,
        long LastWriteTimeUtcTicks);
}
