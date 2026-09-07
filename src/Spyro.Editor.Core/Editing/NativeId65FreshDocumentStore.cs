using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Editing;

/// <summary>A project-bound observation of user intent, not native or export authority.</summary>
public sealed class NativeId65FreshDocumentSnapshot
{
    internal NativeId65FreshDocumentSnapshot(string rootPath, string projectId, string manifestHash,
        string? contentHash, NativeId65FreshDocumentIntent? intent, string retainedRecoveryDirectory = "")
    {
        RootPath = rootPath;
        ProjectId = projectId;
        ManifestSha256 = manifestHash;
        ContentSha256 = contentHash;
        Intent = intent;
        RetainedRecoveryDirectory = retainedRecoveryDirectory;
    }

    public string RootPath { get; }
    public string ProjectId { get; }
    public string Path => System.IO.Path.Combine(RootPath, NativeId65FreshDocumentStore.FileName);
    public string ManifestSha256 { get; }
    public string? ContentSha256 { get; }
    public bool Exists => ContentSha256 != null;
    public NativeId65FreshDocumentIntent? Intent { get; }
    public string RetainedRecoveryDirectory { get; }
}

/// <summary>
/// Saves only the distinct fresh-native intent document in an existing project.
/// Raw-byte compare-and-save and a cooperating-writer lease protect stale edits.
/// Trusted ancestor directories are required; hostile races and crash atomicity
/// are not certified. Legacy manifests, Lab documents and outputs are untouched.
/// </summary>
public static class NativeId65FreshDocumentStore
{
    public const string FileName = "native-id65-fresh-document-v1.json";
    private const int MaximumManifestBytes = 65536;
    private const int MaximumDocumentBytes = 16384;
    private const string LeaseName = ".native-id65-fresh-document.writer.lock";

    public static NativeId65FreshDocumentSnapshot Capture(EditorProjectLayout project,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EditorProjectLayout bound = Resolve(project);
        byte[] manifest = ReadFile(bound.ManifestPath, MaximumManifestBytes, cancellationToken);
        RequireManifest(manifest, bound.ProjectId);
        string documentPath = System.IO.Path.Combine(bound.RootPath, FileName);
        byte[]? bytes;
        try { bytes = ReadFile(documentPath, MaximumDocumentBytes, cancellationToken); }
        catch (FileNotFoundException) { bytes = null; }
        NativeId65FreshDocumentIntent? intent = bytes == null ? null : NativeId65FreshDocumentCodec.Parse(bytes);
        // Do not combine a document with a manifest which changed during capture.
        if (!ReadFile(bound.ManifestPath, MaximumManifestBytes, cancellationToken).AsSpan().SequenceEqual(manifest))
            throw new InvalidDataException("The project manifest changed during document capture. Reopen the project.");
        Resolve(bound);
        cancellationToken.ThrowIfCancellationRequested();
        return new(bound.RootPath, bound.ProjectId, Hash(manifest), bytes == null ? null : Hash(bytes), intent);
    }

    public static NativeId65FreshDocumentSnapshot Save(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, CancellationToken cancellationToken = default) =>
        SaveCore(expected, intent, cancellationToken, null);

    internal static NativeId65FreshDocumentSnapshot SaveForSmoke(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        SaveCore(expected, intent, cancellationToken, checkpoint);

    // A final publication guard shares the existing cooperating-writer namespace.
    // It does not change Save's policy or grant artifact publication authority.
    internal static ReadLease AcquireReadLease(NativeId65FreshDocumentSnapshot expected,
        CancellationToken cancellationToken = default) => ReadLease.Acquire(expected, cancellationToken);

    internal sealed class ReadLease : IDisposable
    {
        private readonly FileStream _stream;
        private readonly byte[] _ownerToken;
        private readonly string _root;
        private int _released;

        private ReadLease(string root, string path, FileStream stream, byte[] ownerToken)
        {
            _root = root;
            LeasePath = path;
            _stream = stream;
            _ownerToken = ownerToken;
        }

        internal string LeasePath { get; }
        // Nonempty means release did not remove a positively identified owner.
        // Missing-path cases retain the existing project root as the inspection
        // location; this is evidence accounting, not an inode/race certificate.
        internal string RetainedEvidencePath { get; private set; } = "";

        internal static ReadLease Acquire(NativeId65FreshDocumentSnapshot expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(expected);
            EditorProjectLayout project = Resolve(new(expected.RootPath, expected.ProjectId));
            string path = System.IO.Path.Combine(project.RootPath, LeaseName);
            RefuseLinkOrDirectory(path);
            byte[] owner = RandomNumberGenerator.GetBytes(32);
            // CreateNew failure never supplies ownership of a foreign lock.
            FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            var lease = new ReadLease(project.RootPath, path, stream, owner);
            try
            {
                stream.Write(owner);
                stream.Flush(flushToDisk: true);
                RequireCurrent(expected, token); // Recheck only after excluding cooperating saves.
                token.ThrowIfCancellationRequested();
                return lease;
            }
            catch (Exception error)
            {
                lease.Dispose();
                if (lease.RetainedEvidencePath.Length != 0)
                    throw new IOException("Saved-document guard failed and its lease could not be safely released. Preserve evidence at " +
                        lease.RetainedEvidencePath, error);
                throw;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            // No caller token: release must still account for ownership after a
            // cancellation or a completed artifact commit. Unknown replacements
            // are retained, never followed, recursively removed or retried.
            try
            {
                _stream.Dispose();
                byte[] actual = ReadFile(LeasePath, _ownerToken.Length, CancellationToken.None);
                if (!actual.AsSpan().SequenceEqual(_ownerToken) || !TryDeleteOwnedFile(LeasePath))
                    RetainedEvidencePath = LeasePath;
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                RetainedEvidencePath = _root;
            }
            catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                RetainedEvidencePath = LeasePath;
            }
        }
    }

    private static NativeId65FreshDocumentSnapshot SaveCore(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, CancellationToken cancellationToken, Action<string>? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(intent);
        cancellationToken.ThrowIfCancellationRequested();
        EditorProjectLayout project = Resolve(new(expected.RootPath, expected.ProjectId));
        string leasePath = System.IO.Path.Combine(project.RootPath, LeaseName);
        RefuseLinkOrDirectory(leasePath);
        // A foreign lease is never deleted or treated as an automatically stale lock.
        using FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string? stage = null;
        string recovery = "";
        bool committed = false;
        bool retain = false;
        Exception? failure = null;
        NativeId65FreshDocumentSnapshot? saved = null;
        try
        {
            NativeId65FreshDocumentSnapshot current = RequireCurrent(expected, cancellationToken);
            byte[] prepared = NativeId65FreshDocumentCodec.SerializeCanonical(intent);
            string preparedHash = Hash(prepared);
            if (preparedHash == current.ContentSha256)
            {
                cancellationToken.ThrowIfCancellationRequested();
                saved = current;
            }
            else
            {
                stage = System.IO.Path.Combine(project.RootPath, $".native-id65-fresh-document-{Guid.NewGuid():N}.tmp");
                using (FileStream output = new(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    output.Write(prepared);
                    output.Flush(flushToDisk: true);
                }
                RequireStage(stage, prepared, cancellationToken);
                checkpoint?.Invoke("staged");
                RequireCurrent(expected, cancellationToken);
                RequireStage(stage, prepared, cancellationToken);
                ExportArtifactPublicationResult publication = ExportArtifactTransaction.Publish(project.RootPath,
                    [new(expected.Path, stage)], cancellationToken);
                committed = true;
                recovery = publication.RetainedRecoveryDirectory;
                retain = recovery.Length != 0;
                checkpoint?.Invoke("published");
                // Publish's commit is noncancellable. Once it succeeds, finish real
                // close/reopen verification even if the caller cancels just now.
                saved = Capture(project, CancellationToken.None);
                if (saved.ManifestSha256 != expected.ManifestSha256 || saved.ContentSha256 != preparedHash)
                    throw new InvalidDataException("The saved document or project manifest differs at final readback.");
            }
        }
        catch (ExportArtifactRollbackException error)
        {
            retain = true;
            recovery = error.RetainedRecoveryDirectory;
            failure = error;
        }
        catch (Exception error)
        {
            failure = error;
            if (committed)
            {
                retain = true;
                if (recovery.Length == 0) recovery = project.RootPath;
            }
        }
        finally
        {
            lease.Dispose();
            if (!retain)
            {
                // Delete only our known regular stage and lease, never a directory
                // or link replacing them. Retain the lease if stage cleanup fails.
                bool cleaned = stage == null || TryDeleteOwnedFile(stage);
                if (cleaned) cleaned = TryDeleteOwnedFile(leasePath);
                if (!cleaned)
                {
                    retain = true;
                    recovery = project.RootPath;
                }
            }
        }
        if (failure != null)
        {
            if (failure is ExportArtifactRollbackException)
                ExceptionDispatchInfo.Capture(failure).Throw();
            if (retain)
                throw new IOException((committed ? "Document save committed but readback/cleanup did not complete. " :
                    "Document save failed and cleanup did not complete. ") + $"Preserve recovery files in {recovery}", failure);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        return new(saved!.RootPath, saved.ProjectId, saved.ManifestSha256, saved.ContentSha256, saved.Intent, recovery);
    }

    private static NativeId65FreshDocumentSnapshot RequireCurrent(NativeId65FreshDocumentSnapshot expected,
        CancellationToken cancellationToken)
    {
        var current = Capture(new(expected.RootPath, expected.ProjectId), cancellationToken);
        if (current.ManifestSha256 != expected.ManifestSha256 || current.ContentSha256 != expected.ContentSha256)
            throw new InvalidDataException("The document or project manifest changed since capture. Reopen before saving.");
        return current;
    }

    private static void RequireStage(string stage, byte[] prepared, CancellationToken cancellationToken)
    {
        if (!ReadFile(stage, MaximumDocumentBytes, cancellationToken).AsSpan().SequenceEqual(prepared))
            throw new InvalidDataException("Fresh document staging readback differs from the canonical intent.");
    }

    private static EditorProjectLayout Resolve(EditorProjectLayout project)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.RootPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(project.ProjectId);
        if (project.ProjectId.Length > 64 || EditorUserDataLayout.NormalizeProjectId(project.ProjectId) != project.ProjectId)
            throw new InvalidDataException("Fresh document persistence requires a normalized project identity.");
        string root = System.IO.Path.TrimEndingDirectorySeparator(System.IO.Path.GetFullPath(project.RootPath));
        if (!Directory.Exists(root) || new DirectoryInfo(root).LinkTarget != null)
            throw new InvalidDataException("Fresh documents require an existing, unlinked project directory.");
        var bound = new EditorProjectLayout(root, project.ProjectId);
        foreach (string path in new[] { bound.ManifestPath, System.IO.Path.Combine(root, FileName) })
        {
            RefuseLinkOrDirectory(path);
            PersistencePathSafety.EnsureWritePathWithinRoot(root, path);
        }
        return bound;
    }

    // Validate the actual manifest's identity/header and ambiguous top-level
    // names, not every unrelated project field. Preserve its entire raw bytes.
    private static void RequireManifest(byte[] bytes, string projectId)
    {
        try
        {
            _ = new UTF8Encoding(false, true).GetCharCount(bytes);
            using JsonDocument document = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 8 });
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("An actual project manifest object is required.");
            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            foreach (JsonProperty property in root.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    throw new InvalidDataException("Project manifest contains duplicate or case-ambiguous fields.");
            }
            // The existing project writer emits these exact camel-case names.
            // No deserializer defaults or case-insensitive identity fallbacks.
            if (!root.TryGetProperty("schemaVersion", out JsonElement schema) || schema.ValueKind != JsonValueKind.Number ||
                !schema.TryGetInt32(out int version) || version != EditorUserDataLayout.CurrentProjectSchemaVersion ||
                !root.TryGetProperty("projectId", out JsonElement id) || id.ValueKind != JsonValueKind.String ||
                id.GetString() != projectId)
                throw new InvalidDataException("Project manifest schema or identity does not match this project.");
        }
        catch (Exception error) when (error is JsonException or DecoderFallbackException or InvalidOperationException)
        {
            throw new InvalidDataException("Project manifest is malformed; no default project was inferred.", error);
        }
    }

    private static byte[] ReadFile(string path, int maximum, CancellationToken cancellationToken)
    {
        RefuseLinkOrDirectory(path);
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ReadBounded(input, maximum, cancellationToken);
    }

    internal static byte[] ReadBounded(Stream input, int maximum, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        if (!input.CanRead || !input.CanSeek || input.Position != 0 || maximum is < 1 or > MaximumManifestBytes)
            throw new InvalidDataException("A bounded readable document stream starting at zero is required.");
        long length = input.Length;
        if (length < 1 || length > maximum)
            throw new InvalidDataException("The document exceeds its bounded byte length.");
        byte[] bytes = new byte[(int)length];
        int done = 0;
        while (done < bytes.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int read = input.Read(bytes.AsSpan(done, Math.Min(4096, bytes.Length - done)));
            if (read == 0) throw new EndOfStreamException("Document ended before its declared length.");
            done += read;
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (input.ReadByte() != -1 || input.Length != length || input.Position != length)
            throw new InvalidDataException("Document length, position or actual EOF changed during capture.");
        cancellationToken.ThrowIfCancellationRequested();
        return bytes;
    }

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    private static void RefuseLinkOrDirectory(string path)
    {
        if (Directory.Exists(path) || new FileInfo(path).LinkTarget != null)
            throw new InvalidDataException("A fresh document, manifest or writer path is a directory or symbolic link.");
    }

    private static bool TryDeleteOwnedFile(string path)
    {
        try
        {
            if (Directory.Exists(path) || new FileInfo(path).LinkTarget != null) return false;
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return false; }
    }
}
