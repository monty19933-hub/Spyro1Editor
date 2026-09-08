using System.Runtime.CompilerServices;
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
}
