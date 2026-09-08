using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Editing;

/// <summary>
/// One storage lifecycle for two closed authoring profiles. Fixed paths, typed
/// codecs and raw versions are owned here, never supplied by callers. Neither
/// profile grants native compilation or artifact publication authority. Explicit
/// legacy forking is a separate closed new-only operation over these two profiles.
/// </summary>
internal static class NativeFreshDocumentStorage
{
    private const int MaximumManifestBytes = 65536;

    internal enum StorageProfile { LegacyFloorV1, LevelAggregateV1 }

    internal sealed class StorageDescriptor
    {
        private StorageDescriptor(string fileName, int maximumBytes, string leaseName, string stagePrefix)
        { FileName = fileName; MaximumBytes = maximumBytes; LeaseName = leaseName; StagePrefix = stagePrefix; }
        internal string FileName { get; }
        internal int MaximumBytes { get; }
        internal string LeaseName { get; }
        internal string StagePrefix { get; }
        private static readonly StorageDescriptor Legacy = new(NativeId65FreshDocumentStore.FileName, 16384,
            ".native-id65-fresh-document.writer.lock", ".native-id65-fresh-document-");
        private static readonly StorageDescriptor Aggregate = new(NativeFreshLevelDocumentStore.FileName, 65536,
            ".native-fresh-level.writer.lock", ".native-fresh-level-");
        internal static StorageDescriptor For(StorageProfile profile) => profile switch
        {
            StorageProfile.LegacyFloorV1 => Legacy,
            StorageProfile.LevelAggregateV1 => Aggregate,
            _ => throw new InvalidDataException("Unknown fresh document storage profile.")
        };
    }

    // A sealed typed union: no object, caller-selected codec or constructible mixed arm.
    internal sealed class StoragePayload
    {
        private StoragePayload(NativeId65FreshDocumentIntent intent)
        { Profile = StorageProfile.LegacyFloorV1; LegacyIntent = intent; }
        private StoragePayload(NativeFreshLevelDocumentIntent intent)
        { Profile = StorageProfile.LevelAggregateV1; AggregateIntent = intent; }
        internal StorageProfile Profile { get; }
        internal NativeId65FreshDocumentIntent? LegacyIntent { get; }
        internal NativeFreshLevelDocumentIntent? AggregateIntent { get; }
        internal static StoragePayload Legacy(NativeId65FreshDocumentIntent intent)
        { ArgumentNullException.ThrowIfNull(intent); return new(intent); }
        internal static StoragePayload Aggregate(NativeFreshLevelDocumentIntent intent)
        { ArgumentNullException.ThrowIfNull(intent); return new(intent); }
        internal static StoragePayload Parse(StorageProfile profile, byte[] bytes) => profile switch
        {
            StorageProfile.LegacyFloorV1 => Legacy(NativeId65FreshDocumentCodec.Parse(bytes)),
            StorageProfile.LevelAggregateV1 => Aggregate(NativeFreshLevelDocumentCodec.Parse(bytes)),
            _ => throw new InvalidDataException("Unknown fresh document storage profile.")
        };
        internal byte[] SerializeCanonical() => Profile switch
        {
            StorageProfile.LegacyFloorV1 => NativeId65FreshDocumentCodec.SerializeCanonical(LegacyIntent!),
            StorageProfile.LevelAggregateV1 => NativeFreshLevelDocumentCodec.SerializeCanonical(AggregateIntent!),
            _ => throw new InvalidDataException("Unknown fresh document storage profile.")
        };
    }

    // Produced only from actual typed snapshots or an actual bounded capture.
    internal sealed class StorageObservation
    {
        private StorageObservation(StorageProfile profile, string root, string projectId, string manifestHash,
            string? contentHash, StoragePayload? payload, string recovery = "")
        {
            _ = StorageDescriptor.For(profile);
            if ((contentHash == null) != (payload == null) || (payload != null && payload.Profile != profile))
                throw new InvalidDataException("Fresh document observation has an incompatible payload.");
            Profile = profile; RootPath = root; ProjectId = projectId; ManifestSha256 = manifestHash;
            ContentSha256 = contentHash; Payload = payload; RetainedRecoveryDirectory = recovery;
        }
        internal StorageProfile Profile { get; }
        internal string RootPath { get; }
        internal string ProjectId { get; }
        internal string ManifestSha256 { get; }
        internal string? ContentSha256 { get; }
        internal StoragePayload? Payload { get; }
        internal string RetainedRecoveryDirectory { get; }
        internal string Path => System.IO.Path.Combine(RootPath, StorageDescriptor.For(Profile).FileName);
        internal static StorageObservation FromLegacy(NativeId65FreshDocumentSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            return new(StorageProfile.LegacyFloorV1, snapshot.RootPath, snapshot.ProjectId, snapshot.ManifestSha256,
                snapshot.ContentSha256, snapshot.Intent == null ? null : StoragePayload.Legacy(snapshot.Intent), snapshot.RetainedRecoveryDirectory);
        }
        internal static StorageObservation FromAggregate(NativeFreshLevelDocumentSnapshot snapshot)
        { ArgumentNullException.ThrowIfNull(snapshot); return snapshot.Observation; }
        internal void RequireProfile(StorageProfile expected)
        {
            _ = StorageDescriptor.For(expected);
            if (Profile != expected) throw new InvalidDataException("Fresh document observation belongs to another storage profile.");
        }
        internal NativeId65FreshDocumentSnapshot ToLegacy()
        {
            RequireProfile(StorageProfile.LegacyFloorV1);
            return new(RootPath, ProjectId, ManifestSha256, ContentSha256, Payload?.LegacyIntent, RetainedRecoveryDirectory);
        }
        internal NativeFreshLevelDocumentSnapshot ToAggregate()
        { RequireProfile(StorageProfile.LevelAggregateV1); return new(this); }
        internal StorageObservation WithRecovery(string recovery) =>
            new(Profile, RootPath, ProjectId, ManifestSha256, ContentSha256, Payload, recovery);

        internal static StorageObservation Capture(StorageProfile profile, EditorProjectLayout project, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            StorageDescriptor descriptor = StorageDescriptor.For(profile);
            EditorProjectLayout bound = Resolve(project, descriptor);
            byte[] manifest = ReadFile(bound.ManifestPath, MaximumManifestBytes, token);
            RequireManifest(manifest, bound.ProjectId);
            string documentPath = System.IO.Path.Combine(bound.RootPath, descriptor.FileName);
            byte[]? bytes;
            try { bytes = ReadFile(documentPath, descriptor.MaximumBytes, token); }
            catch (FileNotFoundException) { bytes = null; }
            StoragePayload? payload = bytes == null ? null : StoragePayload.Parse(profile, bytes);
            // Do not combine a document with a manifest which changed during capture.
            if (!ReadFile(bound.ManifestPath, MaximumManifestBytes, token).AsSpan().SequenceEqual(manifest))
                throw new InvalidDataException("The project manifest changed during document capture. Reopen the project.");
            Resolve(bound, descriptor);
            token.ThrowIfCancellationRequested();
            return new(profile, bound.RootPath, bound.ProjectId, Hash(manifest), bytes == null ? null : Hash(bytes), payload);
        }
    }

    internal static void RequireCompatible(StorageObservation observation, StoragePayload payload)
    {
        ArgumentNullException.ThrowIfNull(observation);
        ArgumentNullException.ThrowIfNull(payload);
        observation.RequireProfile(payload.Profile);
    }

    internal static NativeId65FreshDocumentSnapshot CaptureLegacy(EditorProjectLayout project,
        CancellationToken cancellationToken = default) =>
        StorageObservation.Capture(StorageProfile.LegacyFloorV1, project, cancellationToken).ToLegacy();

    internal static NativeFreshLevelDocumentSnapshot CaptureAggregate(EditorProjectLayout project,
        CancellationToken cancellationToken = default) =>
        StorageObservation.Capture(StorageProfile.LevelAggregateV1, project, cancellationToken).ToAggregate();

    internal static NativeId65FreshDocumentSnapshot SaveLegacy(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, CancellationToken cancellationToken = default) =>
        SaveLegacyCore(expected, intent, cancellationToken, null);

    internal static NativeId65FreshDocumentSnapshot SaveLegacyForSmoke(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, Action<string> checkpoint, CancellationToken cancellationToken = default) =>
        SaveLegacyCore(expected, intent, cancellationToken, checkpoint);

    private static NativeId65FreshDocumentSnapshot SaveLegacyCore(NativeId65FreshDocumentSnapshot expected,
        NativeId65FreshDocumentIntent intent, CancellationToken token, Action<string>? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(intent);
        token.ThrowIfCancellationRequested();
        return SaveCore(StorageObservation.FromLegacy(expected), StoragePayload.Legacy(intent), token, checkpoint).ToLegacy();
    }

    internal static NativeFreshLevelDocumentSnapshot SaveAggregate(NativeFreshLevelDocumentSnapshot expected,
        NativeFreshLevelDocumentIntent intent, CancellationToken token, Action<string>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(intent);
        token.ThrowIfCancellationRequested();
        return SaveCore(StorageObservation.FromAggregate(expected), StoragePayload.Aggregate(intent), token, checkpoint).ToAggregate();
    }

    internal static NativeFreshLevelDocumentSnapshot ForkLegacyFloor(NativeId65FreshDocumentSnapshot source,
        NativeFreshLevelDocumentSnapshot expectedAbsent, CancellationToken token, Action<string>? checkpoint = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(expectedAbsent);
        token.ThrowIfCancellationRequested();
        var fork = LegacyForkContext.Create(source, expectedAbsent);
        return SaveCore(fork.Expected, fork.Payload, token, checkpoint, fork).ToAggregate();
    }

    // A final publication guard shares the existing cooperating-writer namespace.
    // It does not change Save's policy or grant artifact publication authority.
    internal static ReadLease AcquireLegacyReadLease(NativeId65FreshDocumentSnapshot expected,
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
        internal bool DeliberatelyRetained { get; private set; }
        internal Exception? TerminalFailure { get; private set; }

        internal static ReadLease Acquire(NativeId65FreshDocumentSnapshot expected, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(expected);
            StorageObservation observation = StorageObservation.FromLegacy(expected);
            var lease = Open(observation);
            try
            {
                lease.Initialize(observation, token);
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

        // Assign ownership before initialization can fail, so Fork accounts for
        // every CreateNew success, including a failed second guard acquisition.
        internal static void AcquireForFork(StorageObservation observation, CancellationToken token, ref ReadLease? owner)
        {
            token.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(observation);
            if (owner != null) throw new InvalidOperationException("A guard owner is already assigned.");
            owner = Open(observation);
            owner.Initialize(observation, token);
        }

        private static ReadLease Open(StorageObservation observation)
        {
            StorageDescriptor descriptor = StorageDescriptor.For(observation.Profile);
            EditorProjectLayout project = Resolve(new(observation.RootPath, observation.ProjectId), descriptor);
            string path = System.IO.Path.Combine(project.RootPath, descriptor.LeaseName);
            RefuseLinkOrDirectory(path);
            byte[] owner = RandomNumberGenerator.GetBytes(32);
            // CreateNew failure never supplies ownership of a foreign lock.
            FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            return new ReadLease(project.RootPath, path, stream, owner);
        }

        private void Initialize(StorageObservation observation, CancellationToken token)
        {
            _stream.Write(_ownerToken);
            _stream.Flush(flushToDisk: true);
            RequireCurrent(observation, token); // Recheck only after excluding cooperating saves.
            token.ThrowIfCancellationRequested();
        }

        public void Dispose() => Finish(retain: false);

        internal void RetainForRecovery() => Finish(retain: true);

        private void Finish(bool retain)
        {
            if (Interlocked.Exchange(ref _released, 1) != 0) return;
            // No caller token: release must still account for ownership after a
            // cancellation or a completed artifact commit. Unknown replacements
            // are retained, never followed, recursively removed or retried.
            try
            {
                _stream.Dispose();
                byte[] actual = ReadFile(LeasePath, _ownerToken.Length, CancellationToken.None);
                if (!actual.AsSpan().SequenceEqual(_ownerToken))
                {
                    RetainedEvidencePath = LeasePath;
                    TerminalFailure = new InvalidDataException("Saved-document guard marker has another owner: " + LeasePath);
                }
                else if (retain)
                {
                    RetainedEvidencePath = LeasePath;
                    DeliberatelyRetained = true;
                }
                else if (!TryDeleteOwnedFile(LeasePath, out Exception? deletionFailure))
                {
                    RetainedEvidencePath = LeasePath;
                    TerminalFailure = deletionFailure;
                }
            }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            {
                RetainedEvidencePath = _root;
                TerminalFailure = error;
            }
            catch (Exception error) when (error is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                RetainedEvidencePath = LeasePath;
                TerminalFailure = error;
            }
            catch (Exception error)
            {
                // Old Dispose still throws unhandled exception kinds; Fork also
                // retains their exact object when continuing other owner cleanup.
                TerminalFailure = error;
                throw;
            }
        }
    }

    private sealed class LegacyForkContext
    {
        private enum PublicationState { NotAttempted, RollbackIncomplete, Unconfirmed, KnownCommitted }
        private readonly NativeId65FreshDocumentSnapshot _sourceSnapshot;
        private readonly NativeFreshLevelDocumentSnapshot _destinationSnapshot;
        private readonly StorageObservation _source;
        private readonly List<Exception> _errors = [];
        private ReadLease? _sourceLease;
        private ReadLease? _destinationLease;
        private PublicationState _state;
        private byte[]? _prepared;
        private string _returnedRecovery = "";
        private string _retainedStage = "";
        private string? _createdStage;
        private bool _cleanupFailed;
        private NativeId65FreshDocumentSnapshot? _observedSource;
        private NativeFreshLevelDocumentSnapshot? _observedDestination;

        private LegacyForkContext(NativeId65FreshDocumentSnapshot source, NativeFreshLevelDocumentSnapshot destination)
        {
            _sourceSnapshot = source; _destinationSnapshot = destination;
            _source = StorageObservation.FromLegacy(source);
            Expected = StorageObservation.FromAggregate(destination);
            Payload = StoragePayload.Aggregate(NativeFreshLevelDocumentIntent.Create(source.Intent!, []));
        }
        internal StorageObservation Expected { get; }
        internal StoragePayload Payload { get; }

        internal static LegacyForkContext Create(NativeId65FreshDocumentSnapshot source, NativeFreshLevelDocumentSnapshot destination)
        {
            if (!source.Exists || source.Intent == null || source.RetainedRecoveryDirectory.Length != 0)
                throw new InvalidDataException("An actual saved legacy floor with resolved recovery is required for an explicit fork.");
            if (destination.Exists || destination.Intent != null || destination.RetainedRecoveryDirectory.Length != 0)
                throw new InvalidDataException("An actual absent aggregate with resolved recovery is required; a fork never replaces an existing document.");
            if (source.RootPath != destination.RootPath || source.ProjectId != destination.ProjectId ||
                source.ManifestSha256 != destination.ManifestSha256)
                throw new InvalidDataException("Legacy source and absent aggregate must belong to the same raw project version.");
            return new(source, destination);
        }

        internal void RequirePair(StorageObservation expected, StoragePayload payload)
        {
            if (!ReferenceEquals(expected, Expected) || !ReferenceEquals(payload, Payload) ||
                !ReferenceEquals(Expected, _destinationSnapshot.Observation) ||
                !ReferenceEquals(Payload.AggregateIntent!.Base, _sourceSnapshot.Intent) || Payload.AggregateIntent.Placements.Count != 0)
                throw new InvalidDataException("Fork requires its exact captured source, destination and empty aggregate payload.");
        }

        internal void AcquireGuards(CancellationToken token, Action<string>? checkpoint)
        {
            ReadLease.AcquireForFork(_source, token, ref _sourceLease);
            checkpoint?.Invoke("after-source-guard");
            ReadLease.AcquireForFork(Expected, token, ref _destinationLease);
            checkpoint?.Invoke("after-both-guards");
        }
        internal void RequireSourceCurrent(CancellationToken token) => RequireCurrent(_source, token);
        internal void SetPrepared(byte[] prepared) => _prepared = prepared;
        internal void AcknowledgeStageCreated(string path) => _createdStage = path;
        internal void RecordFailure(Exception error) => _errors.Add(error);

        internal ExportArtifactPublicationResult PublishNew(string root, ExportArtifactReplacement[] artifacts, CancellationToken token)
        {
            // Arguments and the final caller-token check already completed. No
            // test callback occurs between this attempt and the actual call.
            _state = PublicationState.Unconfirmed;
            try
            {
                var result = ExportArtifactTransaction.PublishNew(root, artifacts, token);
                _state = PublicationState.KnownCommitted; // Before any checkpoint or finalization.
                _returnedRecovery = result.RetainedRecoveryDirectory;
                return result;
            }
            catch (ExportArtifactRollbackException)
            {
                _state = PublicationState.RollbackIncomplete;
                throw;
            }
            // ANY other escaping exception remains Unconfirmed. In particular,
            // lock Dispose can fail after renames and mask a previous outcome.
        }

        internal StorageObservation Finish(string? stage, Action<string>? checkpoint)
        {
            if (_state != PublicationState.NotAttempted)
            {
                Attempt(() => checkpoint?.Invoke("before-final-observations"));
                Attempt(() =>
                {
                    _observedSource = StorageObservation.Capture(StorageProfile.LegacyFloorV1,
                        new(Expected.RootPath, Expected.ProjectId), CancellationToken.None).ToLegacy();
                    if (_observedSource.ManifestSha256 != _source.ManifestSha256 || _observedSource.ContentSha256 != _source.ContentSha256)
                        throw new InvalidDataException("Legacy fork source differs at final observation.");
                });
                Attempt(() =>
                {
                    _observedDestination = StorageObservation.Capture(StorageProfile.LevelAggregateV1,
                        new(Expected.RootPath, Expected.ProjectId), CancellationToken.None).ToAggregate();
                    if (_state == PublicationState.KnownCommitted &&
                        (_observedDestination.ManifestSha256 != Expected.ManifestSha256 ||
                         _observedDestination.ContentSha256 != Hash(_prepared!)))
                        throw new InvalidDataException("Fork aggregate or project manifest differs at final readback.");
                });
            }
            Attempt(() => checkpoint?.Invoke("before-release"), cleanup: true);
            Attempt(() => AccountStage(stage), cleanup: true);
            FinishOwner(_destinationLease);
            FinishOwner(_sourceLease); // Always attempted, including a failed second acquisition/release.

            string sourceEvidence = OwnerEvidence(_sourceLease);
            string destinationEvidence = OwnerEvidence(_destinationLease);
            if (_state == PublicationState.Unconfirmed)
                throw new NativeFreshLevelDocumentForkPublicationUnconfirmedException(Expected.Path, Expected.RootPath,
                    sourceEvidence, destinationEvidence, _retainedStage, System.IO.Path.Combine(Expected.RootPath, ".export-publication.lock"),
                    _observedSource, _observedDestination, CombinedErrors());
            if (_state == PublicationState.KnownCommitted && _errors.Count != 0)
                throw new NativeFreshLevelDocumentForkCommittedException(Expected.Path, Expected.RootPath,
                    sourceEvidence, destinationEvidence, _retainedStage, CombinedErrors());
            if (_state == PublicationState.KnownCommitted)
            {
                bool retained = _returnedRecovery.Length != 0 || sourceEvidence.Length != 0 || destinationEvidence.Length != 0;
                return StorageObservation.FromAggregate(_observedDestination!).WithRecovery(retained ? Expected.RootPath : "");
            }
            if (_state == PublicationState.NotAttempted && _cleanupFailed)
                throw new IOException("Document fork did not invoke publication and cleanup did not complete. Preserve evidence in " +
                    Expected.RootPath, CombinedErrors());
            // A genuine incomplete rollback remains that exact primary object
            // when alone, or the first member of the ordered aggregate.
            ExceptionDispatchInfo.Capture(CombinedErrors()).Throw();
            throw new InvalidOperationException("Fork finished without an outcome.");
        }

        private void AccountStage(string? stage)
        {
            if (stage == null) return;
            _retainedStage = stage; // Unknown inspection outcome must not mean absent.
            FileAttributes attributes;
            try { attributes = File.GetAttributes(stage); }
            catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
            { _retainedStage = ""; return; }
            if (_state == PublicationState.KnownCommitted)
                throw new InvalidDataException("A new owner appeared at the consumed fork stage; preserve it: " + stage);
            if (_createdStage != stage)
                throw new InvalidDataException("Fork did not acquire this stage with CreateNew; preserve its foreign owner: " + stage);
            if ((attributes & (FileAttributes.Directory | FileAttributes.ReparsePoint)) != 0)
                throw new InvalidDataException("Fork stage has an unknown directory or link owner: " + stage);
            Resolve(new(Expected.RootPath, Expected.ProjectId), StorageDescriptor.For(StorageProfile.LevelAggregateV1));
            RequireStage(stage, _prepared!, StorageDescriptor.For(StorageProfile.LevelAggregateV1), CancellationToken.None);
            if (_state != PublicationState.NotAttempted) return; // Never delete attempted/unreturned evidence.
            if (!TryDeleteOwnedFile(stage, out Exception? deletionFailure)) ExceptionDispatchInfo.Capture(deletionFailure!).Throw();
            _retainedStage = "";
        }

        private bool RetainGuards => _state is PublicationState.Unconfirmed or PublicationState.RollbackIncomplete ||
            _cleanupFailed || (_state == PublicationState.KnownCommitted && (_returnedRecovery.Length != 0 || _errors.Count != 0));

        private void FinishOwner(ReadLease? owner)
        {
            if (owner == null) return;
            Exception? thrown = null;
            try { if (RetainGuards) owner.RetainForRecovery(); else owner.Dispose(); }
            catch (Exception error) { thrown = error; _errors.Add(error); _cleanupFailed = true; }
            if (owner.TerminalFailure != null && !ReferenceEquals(owner.TerminalFailure, thrown))
            { _errors.Add(owner.TerminalFailure); _cleanupFailed = true; }
            if (owner.RetainedEvidencePath.Length != 0 && !owner.DeliberatelyRetained && owner.TerminalFailure == null)
            { _errors.Add(new IOException("Fork guard release has unresolved evidence: " + owner.RetainedEvidencePath)); _cleanupFailed = true; }
        }

        private static string OwnerEvidence(ReadLease? owner) => owner == null ? "" :
            owner.RetainedEvidencePath.Length != 0 ? owner.RetainedEvidencePath : owner.TerminalFailure != null ? owner.LeasePath : "";

        private void Attempt(Action action, bool cleanup = false)
        {
            try { action(); }
            catch (Exception error) { _errors.Add(error); if (cleanup) _cleanupFailed = true; }
        }

        private Exception CombinedErrors() => _errors.Count switch
        {
            0 => new InvalidDataException("Fork has no acknowledged publication outcome."),
            1 => _errors[0],
            _ => new AggregateException("Fork failed; preserve evidence in " + Expected.RootPath, _errors)
        };
    }

    private static StorageObservation SaveCore(StorageObservation expected,
        StoragePayload payload, CancellationToken cancellationToken, Action<string>? checkpoint, LegacyForkContext? fork = null)
    {
        RequireCompatible(expected, payload); // Closed arm agreement is required before any IO.
        fork?.RequirePair(expected, payload);
        cancellationToken.ThrowIfCancellationRequested();
        StorageDescriptor descriptor = StorageDescriptor.For(expected.Profile);
        EditorProjectLayout project = Resolve(new(expected.RootPath, expected.ProjectId), descriptor);
        string leasePath = System.IO.Path.Combine(project.RootPath, descriptor.LeaseName);
        RefuseLinkOrDirectory(leasePath);
        // A foreign lease is never deleted or treated as an automatically stale lock.
        using FileStream? lease = fork == null ? new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None) : null;
        string? stage = null;
        string recovery = "";
        bool committed = false;
        bool retain = false;
        Exception? failure = null;
        StorageObservation? saved = null;
        try
        {
            fork?.AcquireGuards(cancellationToken, checkpoint);
            StorageObservation current = RequireCurrent(expected, cancellationToken);
            byte[] prepared = payload.SerializeCanonical();
            string preparedHash = Hash(prepared);
            fork?.SetPrepared(prepared);
            if (preparedHash == current.ContentSha256)
            {
                cancellationToken.ThrowIfCancellationRequested();
                saved = current;
            }
            else
            {
                stage = System.IO.Path.Combine(project.RootPath, $"{descriptor.StagePrefix}{Guid.NewGuid():N}.tmp");
                if (fork != null) checkpoint?.Invoke("before-stage-create:" + stage);
                using (FileStream output = new(stage, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    fork?.AcknowledgeStageCreated(stage);
                    output.Write(prepared);
                    output.Flush(flushToDisk: true);
                }
                RequireStage(stage, prepared, descriptor, cancellationToken);
                checkpoint?.Invoke("staged");
                fork?.RequireSourceCurrent(cancellationToken);
                RequireCurrent(expected, cancellationToken);
                RequireStage(stage, prepared, descriptor, cancellationToken);
                ExportArtifactReplacement[] artifacts = [new(expected.Path, stage)];
                ExportArtifactPublicationResult publication;
                if (fork == null)
                    publication = ExportArtifactTransaction.Publish(project.RootPath, artifacts, cancellationToken);
                else
                {
                    checkpoint?.Invoke("before-publish-new");
                    cancellationToken.ThrowIfCancellationRequested();
                    publication = fork.PublishNew(project.RootPath, artifacts, cancellationToken);
                }
                committed = true;
                recovery = publication.RetainedRecoveryDirectory;
                retain = recovery.Length != 0;
                checkpoint?.Invoke("published");
                // Publish's commit is noncancellable. Once it succeeds, finish real
                // close/reopen verification even if the caller cancels just now.
                if (fork == null)
                {
                    saved = StorageObservation.Capture(expected.Profile, project, CancellationToken.None);
                    if (saved.ManifestSha256 != expected.ManifestSha256 || saved.ContentSha256 != preparedHash)
                        throw new InvalidDataException("The saved document or project manifest differs at final readback.");
                }
            }
        }
        catch (ExportArtifactRollbackException error) when (fork == null)
        {
            retain = true;
            recovery = error.RetainedRecoveryDirectory;
            failure = error;
        }
        catch (Exception error)
        {
            if (fork != null) fork.RecordFailure(error);
            else
            {
                failure = error;
                if (committed)
                {
                    retain = true;
                    if (recovery.Length == 0) recovery = project.RootPath;
                }
            }
        }
        finally
        {
            if (fork != null) saved = fork.Finish(stage, checkpoint);
            else
            {
                lease!.Dispose();
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
        return fork == null ? saved!.WithRecovery(recovery) : saved!;
    }

    private static StorageObservation RequireCurrent(StorageObservation expected,
        CancellationToken cancellationToken)
    {
        var current = StorageObservation.Capture(expected.Profile, new(expected.RootPath, expected.ProjectId), cancellationToken);
        if (current.ManifestSha256 != expected.ManifestSha256 || current.ContentSha256 != expected.ContentSha256)
            throw new InvalidDataException("The document or project manifest changed since capture. Reopen before saving.");
        return current;
    }

    private static void RequireStage(string stage, byte[] prepared, StorageDescriptor descriptor, CancellationToken cancellationToken)
    {
        if (!ReadFile(stage, descriptor.MaximumBytes, cancellationToken).AsSpan().SequenceEqual(prepared))
            throw new InvalidDataException("Fresh document staging readback differs from the canonical intent.");
    }

    private static EditorProjectLayout Resolve(EditorProjectLayout project, StorageDescriptor descriptor)
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
        foreach (string path in new[] { bound.ManifestPath, System.IO.Path.Combine(root, descriptor.FileName) })
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

    private static bool TryDeleteOwnedFile(string path) => TryDeleteOwnedFile(path, out _);

    private static bool TryDeleteOwnedFile(string path, out Exception? failure)
    {
        failure = null;
        try
        {
            if (Directory.Exists(path) || new FileInfo(path).LinkTarget != null)
            { failure = new InvalidDataException("Cleanup path is a directory or link: " + path); return false; }
            File.Delete(path);
            return true;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { failure = error; return false; }
    }
}
