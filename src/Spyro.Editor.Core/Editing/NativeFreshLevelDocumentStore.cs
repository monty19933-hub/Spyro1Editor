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

    internal static NativeFreshLevelDocumentSnapshot SaveForSmoke(NativeFreshLevelDocumentSnapshot expected,
        NativeFreshLevelDocumentIntent intent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        NativeFreshDocumentStorage.SaveAggregate(expected, intent, cancellationToken, checkpoint);
}
