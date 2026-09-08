using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using Spyro.Editor.Core.Persistence;

[assembly: InternalsVisibleTo("Spyro.Editor.NativeFreshLevelDocumentSmoke")]

namespace Spyro.Editor.Core.Editing;

/// <summary>A raw project-bound aggregate observation, not native or export authority.</summary>
public sealed class NativeFreshLevelDocumentSnapshot
{
    internal NativeFreshLevelDocumentSnapshot(NativeFreshDocumentStorage.StorageObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.RequireProfile(NativeFreshDocumentStorage.StorageProfile.LevelAggregateV1);
        Observation = observation;
    }

    internal NativeFreshDocumentStorage.StorageObservation Observation { get; }
    public string RootPath => Observation.RootPath;
    public string ProjectId => Observation.ProjectId;
    public string Path => Observation.Path;
    public string ManifestSha256 => Observation.ManifestSha256;
    public string? ContentSha256 => Observation.ContentSha256;
    public bool Exists => ContentSha256 != null;
    public NativeFreshLevelDocumentIntent? Intent => Observation.Payload?.AggregateIntent;
    public string RetainedRecoveryDirectory => Observation.RetainedRecoveryDirectory;
}

/// <summary>PublishNew returned, but actual fork verification or owner accounting failed.</summary>
public sealed class NativeFreshLevelDocumentForkCommittedException : IOException
{
    internal NativeFreshLevelDocumentForkCommittedException(string destinationPath, string recoveryDirectory,
        string sourceLease, string destinationLease, string stage, Exception inner)
        : base("Document fork committed but verification or owner cleanup did not complete. Preserve evidence in " + recoveryDirectory, inner)
    {
        DestinationPath = destinationPath; RetainedRecoveryDirectory = recoveryDirectory;
        SourceLeaseEvidencePath = sourceLease; DestinationLeaseEvidencePath = destinationLease; RetainedStagePath = stage;
    }
    public string DestinationPath { get; }
    public string RetainedRecoveryDirectory { get; }
    public string SourceLeaseEvidencePath { get; }
    public string DestinationLeaseEvidencePath { get; }
    public string RetainedStagePath { get; }
}

/// <summary>
/// PublishNew was invoked but did not return an outcome. Actual observations and
/// inspection paths do not establish commit, abort, ownership or retry permission.
/// Null observation means capture failed; a captured absent document is different.
/// </summary>
public sealed class NativeFreshLevelDocumentForkPublicationUnconfirmedException : IOException
{
    internal NativeFreshLevelDocumentForkPublicationUnconfirmedException(string destinationPath, string recoveryDirectory,
        string sourceLease, string destinationLease, string stage, string publicationLock,
        NativeId65FreshDocumentSnapshot? observedSource, NativeFreshLevelDocumentSnapshot? observedDestination, Exception inner)
        : base("Document fork publication was attempted but its outcome is unconfirmed. Do not retry or remove evidence in " + recoveryDirectory, inner)
    {
        DestinationPath = destinationPath; RetainedRecoveryDirectory = recoveryDirectory;
        SourceLeaseEvidencePath = sourceLease; DestinationLeaseEvidencePath = destinationLease; RetainedStagePath = stage;
        PublicationLockPath = publicationLock; ObservedSource = observedSource; ObservedDestination = observedDestination;
    }
    public string DestinationPath { get; }
    public string RetainedRecoveryDirectory { get; }
    public string SourceLeaseEvidencePath { get; }
    public string DestinationLeaseEvidencePath { get; }
    public string RetainedStagePath { get; }
    public string PublicationLockPath { get; }
    public NativeId65FreshDocumentSnapshot? ObservedSource { get; }
    public NativeFreshLevelDocumentSnapshot? ObservedDestination { get; }
}

/// <summary>
/// Captures or saves one whole aggregate in an existing project using raw-byte
/// compare-and-save and its own cooperating-writer lease. Legacy documents and
/// manifest bytes remain untouched. No automatic v1 fork, migration, source or
/// publication authority is granted. Trusted ancestor directories are required;
/// hostile races and crash atomicity are not certified.
/// </summary>
public static class NativeFreshLevelDocumentStore
{
    public const string FileName = "native-fresh-level.json";

    public static NativeFreshLevelDocumentSnapshot Capture(EditorProjectLayout project,
        CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.CaptureAggregate(project, cancellationToken);

    public static NativeFreshLevelDocumentSnapshot Save(NativeFreshLevelDocumentSnapshot expected,
        NativeFreshLevelDocumentIntent intent, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.SaveAggregate(expected, intent, cancellationToken);

    /// <summary>
    /// Explicitly forks an actual saved legacy floor into an actual absent aggregate
    /// in the same project. No placements are generated and the source is not changed.
    /// A returned aggregate is an independent authoring document, not export authority.
    /// </summary>
    public static NativeFreshLevelDocumentSnapshot ForkLegacyFloor(NativeId65FreshDocumentSnapshot source,
        NativeFreshLevelDocumentSnapshot expectedAbsent, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.ForkLegacyFloor(source, expectedAbsent, cancellationToken);

    internal static NativeFreshLevelDocumentSnapshot ForkForSmoke(NativeId65FreshDocumentSnapshot source,
        NativeFreshLevelDocumentSnapshot expectedAbsent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.ForkLegacyFloor(source, expectedAbsent, cancellationToken, checkpoint);

    internal static NativeFreshLevelDocumentSnapshot SaveForSmoke(NativeFreshLevelDocumentSnapshot expected,
        NativeFreshLevelDocumentIntent intent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.SaveAggregate(expected, intent, cancellationToken, checkpoint);

    internal static ReadLease AcquireReadLease(NativeFreshLevelDocumentSnapshot expected,
        CancellationToken token = default) => ReadLease.Acquire(expected, token, null);

    internal static ReadLease AcquireReadLeaseForSmoke(NativeFreshLevelDocumentSnapshot expected,
        Action<string> checkpoint, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(checkpoint);
        return ReadLease.Acquire(expected, token, checkpoint);
    }

    // Explicit primary-aware completion, not a masking using/Dispose contract.
    // This type excludes cooperating aggregate saves; it cannot publish anything.
    internal sealed class ReadLease
    {
        private readonly NativeFreshDocumentStorage.ReadLease _inner;
        private readonly object _completionLock = new();
        private bool _completed;
        private Exception? _primary, _outcome;
        private ReadLease(NativeFreshDocumentStorage.ReadLease inner) => _inner = inner;
        internal string LeasePath => _inner.LeasePath;
        internal string RetainedEvidencePath => _inner.RetainedEvidencePath;
        internal Exception? TerminalFailure => _inner.TerminalFailure;

        internal static ReadLease Acquire(NativeFreshLevelDocumentSnapshot expected,
            CancellationToken token, Action<string>? checkpoint)
        {
            NativeFreshDocumentStorage.ReadLease? owner = null;
            try
            {
                NativeFreshDocumentStorage.AcquireAggregateReadLease(expected, token, ref owner, checkpoint);
                return new(owner!);
            }
            catch (Exception primary)
            {
                if (owner != null) new ReadLease(owner).Complete(primary);
                throw; // No CreateNew owner, or unreachable after primary-aware Complete.
            }
        }

        internal void Complete(Exception? primary = null)
        {
            lock (_completionLock)
            {
                if (_completed)
                {
                    if (primary != null && !ReferenceEquals(primary, _primary))
                        throw new InvalidOperationException("A completed aggregate guard cannot accept another primary failure.");
                }
                else
                {
                    _completed = true;
                    _primary = primary;
                    var errors = new List<Exception>();
                    if (primary != null) errors.Add(primary);
                    Exception? thrown = null;
                    try { _inner.Dispose(); } // Always uncancellable, including failed acquisition.
                    catch (Exception error) { thrown = error; errors.Add(error); }
                    if (_inner.TerminalFailure != null && !ReferenceEquals(_inner.TerminalFailure, thrown))
                        errors.Add(_inner.TerminalFailure);
                    if (_inner.RetainedEvidencePath.Length != 0 && _inner.TerminalFailure == null && thrown == null)
                        errors.Add(new IOException("Aggregate guard release retained unresolved evidence: " + _inner.RetainedEvidencePath));
                    int primaryCount = primary == null ? 0 : 1;
                    _outcome = errors.Count == primaryCount ? primary :
                        new IOException("Aggregate final read guard cleanup failed; preserve evidence at " +
                            (_inner.RetainedEvidencePath.Length != 0 ? _inner.RetainedEvidencePath : _inner.LeasePath),
                            new AggregateException("Primary failure followed by aggregate guard release/accounting errors.", errors));
                }
                if (_outcome != null) ExceptionDispatchInfo.Capture(_outcome).Throw();
            }
        }
    }
}
