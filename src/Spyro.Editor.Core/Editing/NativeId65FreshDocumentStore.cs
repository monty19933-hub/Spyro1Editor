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

    public static NativeId65FreshDocumentSnapshot Capture(EditorProjectLayout project,
        CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.CaptureLegacy(project, cancellationToken);

    public static NativeId65FreshDocumentSnapshot Save(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.SaveLegacy(expected, intent, cancellationToken);

    internal static NativeId65FreshDocumentSnapshot SaveForSmoke(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.SaveLegacyForSmoke(expected, intent, checkpoint, cancellationToken);

    // Keep the exact nested guard consumed by existing saved publication code.
    internal static ReadLease AcquireReadLease(NativeId65FreshDocumentSnapshot expected,
        CancellationToken cancellationToken = default) => ReadLease.Acquire(expected, cancellationToken);

    internal sealed class ReadLease : IDisposable
    {
        private readonly NativeFreshDocumentStorage.ReadLease _inner;
        private ReadLease(NativeFreshDocumentStorage.ReadLease inner) => _inner = inner;
        internal string LeasePath => _inner.LeasePath;
        internal string RetainedEvidencePath => _inner.RetainedEvidencePath;
        internal static ReadLease Acquire(NativeId65FreshDocumentSnapshot expected, CancellationToken token) =>
            new(NativeFreshDocumentStorage.AcquireLegacyReadLease(expected, token));
        public void Dispose() => _inner.Dispose();
    }

    internal static byte[] ReadBounded(Stream input, int maximum, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.ReadBounded(input, maximum, cancellationToken);
}
