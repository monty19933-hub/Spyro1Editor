using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

/// <param name="StagedPath">A complete staged file, or null to remove an obsolete companion artifact on successful publication.</param>
public sealed record ExportArtifactReplacement(string DestinationPath, string? StagedPath);

public sealed record ExportArtifactPublicationResult(string RetainedRecoveryDirectory);

/// <summary>Rollback is incomplete. Preserve staged files, journal, previous artifacts and publication lock for recovery.</summary>
public sealed class ExportArtifactRollbackException : IOException
{
    internal ExportArtifactRollbackException(string message, string retainedRecoveryDirectory, Exception innerException)
        : base(message, innerException) => RetainedRecoveryDirectory = retainedRecoveryDirectory;
    public string RetainedRecoveryDirectory { get; }
}

/// <summary>
/// Publishes a prepared artifact set using same-volume renames and exception
/// rollback. Cancellation is observed before the short, noncancellable commit.
/// This is not filesystem-wide crash atomicity: an interrupted process leaves
/// the recovery journal, previous files, and lock for explicit recovery.
/// </summary>
public static class ExportArtifactTransaction
{
    public static ExportArtifactPublicationResult Publish(
        string outputDirectory,
        IReadOnlyList<ExportArtifactReplacement> artifacts,
        CancellationToken cancellationToken = default) =>
        PublishCore(outputDirectory, artifacts, cancellationToken, null);

    /// <summary>
    /// Publishes an immutable new set. All destinations must remain absent;
    /// existence is checked under the publication lock and renames never replace.
    /// Duplicate callers must verify/reuse a complete set outside this primitive.
    /// Null staged paths (deletion) are not meaningful in this mode.
    /// </summary>
    public static ExportArtifactPublicationResult PublishNew(
        string outputDirectory,
        IReadOnlyList<ExportArtifactReplacement> artifacts,
        CancellationToken cancellationToken = default) =>
        PublishCoreWithPolicy(outputDirectory, artifacts, cancellationToken, null, createNewOnly: true);

    private static ExportArtifactPublicationResult PublishCore(
        string outputDirectory,
        IReadOnlyList<ExportArtifactReplacement> artifacts,
        CancellationToken cancellationToken,
        Action<string>? checkpoint) =>
        PublishCoreWithPolicy(outputDirectory, artifacts, cancellationToken, checkpoint, createNewOnly: false);

    private static ExportArtifactPublicationResult PublishCoreWithPolicy(
        string outputDirectory,
        IReadOnlyList<ExportArtifactReplacement> artifacts,
        CancellationToken cancellationToken,
        Action<string>? checkpoint,
        bool createNewOnly)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectory);
        ArgumentNullException.ThrowIfNull(artifacts);
        cancellationToken.ThrowIfCancellationRequested();
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outputDirectory));
        if (!Directory.Exists(root) || artifacts.Count == 0)
            throw new InvalidDataException("Artifact publication requires an existing output directory and at least one artifact.");
        RequireNoLinks(root, root);

        ExportArtifactReplacement[] entries = artifacts.Select(artifact => new ExportArtifactReplacement(
            Path.GetFullPath(artifact.DestinationPath),
            artifact.StagedPath == null ? null : Path.GetFullPath(artifact.StagedPath))).ToArray();
        HashSet<string> destinations = new(StringComparer.OrdinalIgnoreCase);
        HashSet<string> sources = new(StringComparer.OrdinalIgnoreCase);
        foreach (ExportArtifactReplacement artifact in entries)
        {
            if (!string.Equals(Path.GetDirectoryName(artifact.DestinationPath), root, StringComparison.OrdinalIgnoreCase) ||
                !destinations.Add(artifact.DestinationPath))
                throw new InvalidDataException("Artifact destinations must be distinct files directly inside the output directory.");
            string destinationName = Path.GetFileName(artifact.DestinationPath);
            if (destinationName.Equals(".export-publication.lock", StringComparison.OrdinalIgnoreCase) ||
                destinationName.StartsWith(".export-recovery-", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("An artifact cannot replace the publication lock or recovery namespace.");
            RequireRegularFileOrAbsent(root, artifact.DestinationPath);
            if (artifact.StagedPath == null)
            {
                if (createNewOnly)
                    throw new InvalidDataException("New-only publication cannot delete companion artifacts.");
                continue;
            }
            if (!artifact.StagedPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                !sources.Add(artifact.StagedPath))
                throw new InvalidDataException("Each staged artifact must be a distinct file within the output directory.");
            RequireRegularFileOrAbsent(root, artifact.StagedPath);
            if (!File.Exists(artifact.StagedPath))
                throw new FileNotFoundException("A required staged export artifact is missing.", artifact.StagedPath);
        }
        if (sources.Overlaps(destinations))
            throw new InvalidDataException("A staged artifact cannot overlap any final artifact path.");

        string lockPath = Path.Combine(root, ".export-publication.lock");
        RequireRegularFileOrAbsent(root, lockPath);
        string recovery = Path.Combine(root, $".export-recovery-{Guid.NewGuid():N}");
        // CreateNew also fences other editor processes using this publisher.
        // A surviving lock deliberately requires recovery, not an automatic
        // stale-lock deletion that might race another active editor.
        using FileStream publicationLock = new(lockPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        bool[] backedUp = new bool[entries.Length];
        bool[] published = new bool[entries.Length];
        string[] backups = Enumerable.Range(0, entries.Length)
            .Select(index => Path.Combine(recovery, $"previous-{index:D3}"))
            .ToArray();
        bool retainRecovery = false;
        try
        {
            if (createNewOnly && entries.Any(entry => File.Exists(entry.DestinationPath)))
                throw new IOException("New-only publication refuses an existing or incomplete candidate artifact set.");
            Directory.CreateDirectory(recovery);
            byte[] lockContents = Encoding.UTF8.GetBytes(recovery + "\n");
            publicationLock.Write(lockContents);
            publicationLock.Flush(flushToDisk: true);
            byte[] journal = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schema = 1,
                createdUtc = DateTimeOffset.UtcNow,
                note = "Exception rollback is automatic. After a process interruption, inspect this journal and restore previous files before removing the publication lock. A previous-NNN file exists only if that original was moved into recovery.",
                artifacts = entries.Select((entry, index) => new
                {
                    destination = entry.DestinationPath,
                    staged = entry.StagedPath,
                    backup = backups[index],
                    originallyExisted = File.Exists(entry.DestinationPath)
                })
            }, new JsonSerializerOptions { WriteIndented = true });
            using (FileStream journalStream = new(Path.Combine(recovery, "recovery.json"), FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                journalStream.Write(journal);
                journalStream.Flush(flushToDisk: true);
            }
            checkpoint?.Invoke("prepared");
            cancellationToken.ThrowIfCancellationRequested();

            // No awaits or cancellation checks between renames. Back up all
            // old artifacts first; publish CUE last in the caller's ordering.
            for (int index = 0; index < entries.Length; index++)
            {
                RequireRegularFileOrAbsent(root, entries[index].DestinationPath);
                if (File.Exists(entries[index].DestinationPath))
                {
                    if (createNewOnly)
                        throw new IOException("A candidate destination appeared during new-only publication; no replacement is allowed.");
                    File.Move(entries[index].DestinationPath, backups[index]);
                    backedUp[index] = true;
                }
                checkpoint?.Invoke($"after-backup:{index}");
            }
            for (int index = 0; index < entries.Length; index++)
            {
                if (entries[index].StagedPath is string staged)
                {
                    RequireRegularFileOrAbsent(root, staged);
                    File.Move(staged, entries[index].DestinationPath);
                    published[index] = true;
                }
                checkpoint?.Invoke($"after-publish:{index}");
            }
        }
        catch (Exception failure)
        {
            List<Exception> rollbackFailures = [];
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                if (!published[index])
                    continue;
                try
                {
                    File.Move(entries[index].DestinationPath, entries[index].StagedPath!);
                }
                catch (Exception exception)
                {
                    rollbackFailures.Add(exception);
                }
            }
            for (int index = entries.Length - 1; index >= 0; index--)
            {
                if (!backedUp[index])
                    continue;
                try
                {
                    File.Move(backups[index], entries[index].DestinationPath);
                }
                catch (Exception exception)
                {
                    rollbackFailures.Add(exception);
                }
            }
            if (rollbackFailures.Count > 0)
            {
                retainRecovery = true;
                throw new ExportArtifactRollbackException(
                    $"Export publication failed and rollback could not restore every artifact. Previous files and recovery instructions are preserved at {recovery}. The publication lock is retained until recovery is complete.",
                    recovery,
                    new AggregateException([failure, .. rollbackFailures]));
            }
            throw;
        }
        finally
        {
            publicationLock.Dispose();
            if (!retainRecovery)
            {
                // Cleanup must not convert a completed publication into a
                // reported failure, or hide the original rolled-back error.
                try { File.Delete(lockPath); }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                // Keep the recovery journal if the lock itself could not be
                // removed; it still points here and must remain meaningful.
                if (!File.Exists(lockPath))
                {
                    try { if (Directory.Exists(recovery)) Directory.Delete(recovery, recursive: true); }
                    catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
                }
            }
        }
        return new ExportArtifactPublicationResult(Directory.Exists(recovery) ? recovery : "");
    }

    private static void RequireRegularFileOrAbsent(string root, string path)
    {
        RequireNoLinks(root, path);
        if (Directory.Exists(path))
            throw new InvalidDataException($"An export artifact path is a directory: {path}");
    }

    private static void RequireNoLinks(string root, string path)
    {
        string? current = path;
        while (current != null)
        {
            try
            {
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException($"Export publication refuses symbolic-link paths: {current}");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
            if (string.Equals(current, root, StringComparison.OrdinalIgnoreCase))
                break;
            current = Path.GetDirectoryName(current);
        }
    }
}
