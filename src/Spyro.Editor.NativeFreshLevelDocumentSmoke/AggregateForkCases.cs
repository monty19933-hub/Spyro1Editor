using System.Reflection;
using System.Security.Cryptography;
using Spyro.Editor.Core.Editing;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

// Authoring-only fixtures. A successful document fork is not native publication.
internal static class AggregateForkCases
{
    private static int _checks;
    private static string _temporary = "";
    internal static void Run()
    {
        _temporary = Directory.CreateTempSubdirectory("spyro-aggregate-fork-").FullName;
        bool passed = false;
        try
        {
            PublicContract();
            StageAcquisitionOwnership();
            PreconditionsAndRawVersions();
            GuardsAndPrecommit();
            PartialAcquisitionAndMultipleOwners();
            UnconfirmedPublication();
            KnownCommit();
            RealRollbackNamedCallback();
            OwnerTerminalSemantics();
            passed = true;
        }
        finally
        {
            if (passed) Directory.Delete(_temporary, recursive: true);
            else Console.Error.WriteLine("Retained failed fork fixture: " + _temporary);
        }
        Console.WriteLine($"NATIVE_FRESH_LEVEL_DOCUMENT_FORK_OK checks={_checks} sourceFree=true");
    }

    private static readonly string SourceLock = ".native-id65-fresh-document.writer.lock";
    private static readonly string DestinationLock = ".native-fresh-level.writer.lock";
    private static byte[] Canonical(NativeId65FreshDocumentSnapshot source) =>
        NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentIntent.Create(source.Intent!, []));

    private static void StageAcquisitionOwnership()
    {
        var (project, source, absent) = Fixture("foreign-equal-stage");
        byte[] prepared = Canonical(source);
        string? actualStage = null;
        Exception error = Throws<IOException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
        {
            const string prefix = "before-stage-create:";
            if (!phase.StartsWith(prefix, StringComparison.Ordinal)) return;
            actualStage = phase[prefix.Length..]; // Observed random path; never caller-selected.
            Check(Path.GetDirectoryName(actualStage) == project.RootPath && !File.Exists(actualStage), "observe actual absent generated stage");
            File.WriteAllBytes(actualStage, prepared); // A foreign owner, even though its bytes match.
        }));
        Check(actualStage != null && File.Exists(actualStage) && File.ReadAllBytes(actualStage).SequenceEqual(prepared),
            "failed CreateNew must preserve byte-identical foreign stage");
        Check(error is not NativeFreshLevelDocumentForkPublicationUnconfirmedException &&
            error is not NativeFreshLevelDocumentForkCommittedException && !File.Exists(absent.Path), "stage acquisition refusal never attempted publication");
        Check(File.ReadAllBytes(Path.Combine(project.RootPath, SourceLock)).Length == 32 &&
            File.ReadAllBytes(Path.Combine(project.RootPath, DestinationLock)).Length == 32, "unknown stage retains both closed guard markers");
    }

    private static void GuardsAndPrecommit()
    {
        foreach (string lockName in new[] { SourceLock, DestinationLock })
        {
            var (project, source, absent) = Fixture("foreign-" + lockName);
            string foreign = Path.Combine(project.RootPath, lockName);
            File.WriteAllText(foreign, "foreign-lock");
            Throws<IOException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent));
            Check(File.ReadAllText(foreign) == "foreign-lock" && !File.Exists(absent.Path), "foreign lock is not owned or removed");
            Check(!Names(project).Any(p => Path.GetFileName(p).EndsWith(".tmp")) &&
                !File.Exists(Path.Combine(project.RootPath, lockName == SourceLock ? DestinationLock : SourceLock)), "failed second acquisition releases first real guard");
        }
        var (heldProject, heldSource, heldAbsent) = Fixture("both-guards");
        List<string> phases = [];
        NativeFreshLevelDocumentStore.ForkForSmoke(heldSource, heldAbsent, phase =>
        {
            phases.Add(phase.StartsWith("before-stage-create:", StringComparison.Ordinal) ? "before-stage-create" : phase);
            if (phase != "after-both-guards") return;
            Check(new FileInfo(Path.Combine(heldProject.RootPath, SourceLock)).Length == 32 &&
                new FileInfo(Path.Combine(heldProject.RootPath, DestinationLock)).Length == 32, "both actual marker guards are flushed before staging");
            Throws<IOException>(() => NativeId65FreshDocumentStore.Save(heldSource, heldSource.Intent!));
            Throws<IOException>(() => NativeFreshLevelDocumentStore.Save(heldAbsent, Cases.Document()));
            Throws<IOException>(() => NativeFreshDocumentStorage.AcquireLegacyReadLease(heldSource));
        });
        Check(phases.SequenceEqual(new[] { "after-source-guard", "after-both-guards", "before-stage-create", "staged", "before-publish-new", "published", "before-final-observations", "before-release" }), "exact fork checkpoint ordering");
        Clean(heldProject);

        foreach (string boundary in new[] { "after-source-guard", "after-both-guards", "before-stage-create", "staged", "before-publish-new" })
        foreach (bool cancellation in new[] { false, true })
        {
            var (project, source, absent) = Fixture("pre-" + boundary + cancellation);
            byte[] original = File.ReadAllBytes(source.Path), manifest = File.ReadAllBytes(project.ManifestPath);
            var failure = new IOException("actual checkpoint failure " + boundary);
            using var cancel = new CancellationTokenSource();
            Exception error = Throws<Exception>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase != boundary && !(boundary == "before-stage-create" && phase.StartsWith(boundary + ":", StringComparison.Ordinal))) return;
                if (cancellation) cancel.Cancel(); else throw failure;
            }, cancel.Token));
            Check(cancellation ? error is OperationCanceledException : ReferenceEquals(error, failure), "pre-attempt primary exception is preserved");
            Check(!File.Exists(absent.Path) && File.ReadAllBytes(source.Path).SequenceEqual(original) &&
                File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "pre-attempt failure preserves documents");
            Clean(project);
        }
        foreach (string changed in new[] { "source", "manifest", "destination" })
        {
            var (project, source, absent) = Fixture("staged-cas-" + changed);
            string path = changed == "source" ? source.Path : changed == "manifest" ? project.ManifestPath : absent.Path;
            byte[]? replacement = null;
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase != "staged") return;
                if (changed == "destination") File.WriteAllBytes(path, Canonical(source)); else File.AppendAllText(path, " ");
                replacement = File.ReadAllBytes(path);
            }));
            Check(File.ReadAllBytes(path).SequenceEqual(replacement!), "raw CAS refuses without reverting external document change");
            Clean(project);
        }
        var (sameProject, sameSource, sameAbsent) = Fixture("same-byte-replacement");
        NativeFreshLevelDocumentStore.ForkForSmoke(sameSource, sameAbsent, phase =>
        {
            if (phase != "staged") return;
            string replacement = Path.Combine(sameProject.RootPath, "replacement");
            File.WriteAllBytes(replacement, File.ReadAllBytes(sameSource.Path));
            File.Move(replacement, sameSource.Path, overwrite: true);
        });
        Check(File.Exists(sameAbsent.Path), "same-byte inode replacement preserves raw-CAS semantics");
        Clean(sameProject);
    }

    private static void PreconditionsAndRawVersions()
    {
        foreach (string mutation in new[] { "source-missing", "destination-malformed", "manifest-raw", "manifest-id", "root-moved" })
        {
            var (project, source, absent) = Fixture("before-" + mutation);
            if (mutation == "source-missing") File.Delete(source.Path);
            else if (mutation == "destination-malformed") File.WriteAllText(absent.Path, "bad aggregate");
            else if (mutation == "manifest-raw") File.AppendAllText(project.ManifestPath, " ");
            else if (mutation == "manifest-id") File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"another-project\"}");
            else Directory.Move(project.RootPath, project.RootPath + "-moved");
            string actualRoot = mutation == "root-moved" ? project.RootPath + "-moved" : project.RootPath;
            var before = Tree(actualRoot);
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent));
            Check(Tree(actualRoot).SequenceEqual(before), "invalid raw observation refuses without changing current tree: " + mutation);
        }
        var (manifestProject, manifestSource, _) = Fixture("different-raw-manifest");
        File.AppendAllText(manifestProject.ManifestPath, " ");
        var laterAbsent = NativeFreshLevelDocumentStore.Capture(manifestProject);
        var beforeDifferent = Tree(manifestProject.RootPath);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(manifestSource, laterAbsent));
        Check(Tree(manifestProject.RootPath).SequenceEqual(beforeDifferent), "same root/id but different captured raw manifest refuses before owners");
        foreach (string target in new[] { "root", "manifest", "source", "destination", "source-lock", "destination-lock" })
        {
            var (project, source, absent) = Fixture("link-" + target);
            string path = target switch
            {
                "root" => project.RootPath, "manifest" => project.ManifestPath, "source" => source.Path,
                "destination" => absent.Path, "source-lock" => Path.Combine(project.RootPath, SourceLock),
                _ => Path.Combine(project.RootPath, DestinationLock)
            };
            string held = path + "-held";
            if (target == "root") { Directory.Move(path, held); Directory.CreateSymbolicLink(path, held); }
            else { if (File.Exists(path)) File.Move(path, held); File.CreateSymbolicLink(path, held); }
            Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent));
            Check(new FileInfo(path).LinkTarget == held, "linked input/owner path refused and preserved: " + target);
        }
        var (recoveryProject, recoverySource, recoveryAbsent) = Fixture("actual-source-recovery");
        string consumed = "";
        var actualRetained = NativeFreshDocumentStorage.SaveLegacyForSmoke(recoverySource, Cases.Base(20), phase =>
        {
            if (phase == "staged") consumed = Names(recoveryProject).Single(p => p.EndsWith(".tmp", StringComparison.Ordinal));
            if (phase == "published") Directory.CreateDirectory(consumed); // Existing Save's real retained cleanup outcome.
        });
        Check(actualRetained.RetainedRecoveryDirectory == recoveryProject.RootPath && actualRetained.Exists,
            "actual legacy Save issued a retained-recovery snapshot; no forged metadata");
        string[] recoveryBefore = Tree(recoveryProject.RootPath);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(actualRetained, recoveryAbsent));
        Check(Tree(recoveryProject.RootPath).SequenceEqual(recoveryBefore), "unresolved genuine source recovery refuses before fork IO");
    }

    private static void PartialAcquisitionAndMultipleOwners()
    {
        var (partialProject, partialSource, partialAbsent) = Fixture("partial-acquire-and-cleanup-failure");
        string sourceLock = Path.Combine(partialProject.RootPath, SourceLock), destinationLock = Path.Combine(partialProject.RootPath, DestinationLock),
            moved = Path.Combine(partialProject.RootPath, "original-marker");
        var partial = Throws<IOException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(partialSource, partialAbsent, phase =>
        {
            if (phase != "after-source-guard") return;
            File.Move(sourceLock, moved);
            File.WriteAllText(sourceLock, "foreign-source-owner");
            File.WriteAllText(destinationLock, "foreign-destination-owner");
        }));
        Check(partial is not NativeFreshLevelDocumentForkPublicationUnconfirmedException && partial.InnerException is AggregateException errors &&
            errors.InnerExceptions.Count == 2 && errors.InnerExceptions[0] is IOException && errors.InnerExceptions[1] is InvalidDataException,
            "second acquire failure and first-owner cleanup failure are independently retained");
        Check(File.ReadAllText(sourceLock) == "foreign-source-owner" && File.ReadAllText(destinationLock) == "foreign-destination-owner" &&
            File.ReadAllBytes(moved).Length == 32, "partial acquisition closes actual first handle without deleting foreign owners");

        var (initializeProject, initializeSource, initializeAbsent) = Fixture("second-initialize-failure");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(initializeSource, initializeAbsent, phase =>
        {
            if (phase == "after-source-guard") File.WriteAllBytes(initializeAbsent.Path, Canonical(initializeSource));
        }));
        Check(File.ReadAllBytes(initializeAbsent.Path).SequenceEqual(Canonical(initializeSource)), "failed second initialization preserves arriving document");
        Clean(initializeProject); // Both real CreateNew owners were accounted, not just the first.

        var (bothProject, bothSource, bothAbsent) = Fixture("both-marker-release-failures");
        var both = Throws<NativeFreshLevelDocumentForkCommittedException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(bothSource, bothAbsent, phase =>
        {
            if (phase != "before-release") return;
            foreach (string name in new[] { SourceLock, DestinationLock })
            {
                string path = Path.Combine(bothProject.RootPath, name);
                File.Move(path, path + "-moved"); File.WriteAllBytes(path, new byte[32]);
            }
        }));
        Check(both.InnerException is AggregateException terminal && terminal.InnerExceptions.Count == 2 &&
            terminal.InnerExceptions[0].Message.Contains(DestinationLock, StringComparison.Ordinal) &&
            terminal.InnerExceptions[1].Message.Contains(SourceLock, StringComparison.Ordinal), "both marker failures retained in reverse acquisition order");
        Check(File.ReadAllBytes(both.SourceLeaseEvidencePath + "-moved").Length == 32 &&
            File.ReadAllBytes(both.DestinationLeaseEvidencePath + "-moved").Length == 32, "both drifted owners' streams actually closed");

        foreach (string kind in new[] { "corrupt", "directory", "dangling-link" })
        {
            var (project, source, absent) = Fixture("pre-stage-drift-" + kind);
            string stage = "";
            var primary = new OperationCanceledException("pre-attempt with unknown stage");
            var error = Throws<IOException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase != "staged") return;
                stage = Names(project).Single(p => p.EndsWith(".tmp", StringComparison.Ordinal));
                if (kind == "corrupt") File.WriteAllText(stage, "foreign bytes");
                else
                {
                    File.Delete(stage);
                    if (kind == "directory") Directory.CreateDirectory(stage);
                    else File.CreateSymbolicLink(stage, Path.Combine(project.RootPath, "absent-target"));
                }
                throw primary;
            }));
            Check(error.InnerException is AggregateException aggregate && ReferenceEquals(aggregate.InnerExceptions[0], primary) &&
                aggregate.InnerExceptions.Count == 2 && !File.Exists(absent.Path), "pre-attempt cancellation cannot hide unknown stage cleanup failure");
            Check(kind == "corrupt" ? File.ReadAllText(stage) == "foreign bytes" : kind == "directory" ? Directory.Exists(stage) :
                new FileInfo(stage).LinkTarget != null, "unknown pre-attempt stage is retained");
        }
    }

    private static void RealRollbackNamedCallback()
    {
        string root = Path.Combine(_temporary, "actual-transaction-rollback");
        Directory.CreateDirectory(root);
        ExportArtifactReplacement[] artifacts = Enumerable.Range(0, 2)
            .Select(i => new ExportArtifactReplacement(Path.Combine(root, "document-" + i), Path.Combine(root, "stage-" + i))).ToArray();
        foreach (var artifact in artifacts) File.WriteAllText(artifact.StagedPath!, "tiny document");
        Action<string> conflict = phase =>
        {
            if (phase != "after-publish:0") return;
            File.WriteAllText(artifacts[0].StagedPath!, "competing stage owner");
            throw new IOException("actual rollback conflict fixture");
        };
        var actualRollback = Throws<ExportArtifactRollbackException>(() =>
        {
            try
            {
                typeof(ExportArtifactTransaction).GetMethod("PublishCoreWithPolicy", BindingFlags.NonPublic | BindingFlags.Static)!
                    .Invoke(null, [root, artifacts, CancellationToken.None, conflict, true]);
            }
            catch (TargetInvocationException error) when (error.InnerException != null)
            { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); }
        });
        Check(Directory.Exists(actualRollback.RetainedRecoveryDirectory) && File.ReadAllText(artifacts[0].StagedPath!) == "competing stage owner",
            "existing unchanged transaction seam produced a genuine typed incomplete rollback");
        var (_, source, absent) = Fixture("rollback-named-postcommit-callback");
        var known = Throws<NativeFreshLevelDocumentForkCommittedException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
        { if (phase == "published") throw actualRollback; }));
        Check(ReferenceEquals(known.InnerException, actualRollback) && File.ReadAllBytes(absent.Path).SequenceEqual(Canonical(source)),
            "rollback-named callback after actual Fork PublishNew return remains known committed with exact original error");
        // This is an actual postcommit callback control, not a Fork transaction
        // rollback or post-move publication-lock Dispose fault reproduction.
    }

    private static void UnconfirmedPublication()
    {
        // Real PublishNew lock/occupied-output refusals. These prove call-boundary
        // accounting, NOT a synthetic post-rename Dispose failure or a commit.
        foreach (string mode in new[] { "lock", "equal-output", "different-output", "both-final-errors", "final-checkpoint-error" })
        {
            var (project, source, absent) = Fixture("unconfirmed-" + mode);
            byte[] prepared = Canonical(source), original = File.ReadAllBytes(source.Path);
            string transactionLock = Path.Combine(project.RootPath, ".export-publication.lock");
            var finalCheckpointError = new IOException("actual final checkpoint failure");
            using var cancel = new CancellationTokenSource();
            var error = Throws<NativeFreshLevelDocumentForkPublicationUnconfirmedException>(() =>
                NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
                {
                    if (phase == "before-publish-new")
                    {
                        if (mode is "lock" or "both-final-errors" or "final-checkpoint-error") File.WriteAllText(transactionLock, "foreign-publication-lock");
                        else File.WriteAllBytes(absent.Path, mode == "equal-output" ? prepared : NativeFreshLevelDocumentCodec.SerializeCanonical(Cases.Document()));
                    }
                    if (phase == "before-final-observations" && mode == "both-final-errors")
                    {
                        cancel.Cancel(); // Both real captures still execute with None.
                        File.Delete(source.Path);
                        File.WriteAllText(absent.Path, "invalid aggregate");
                    }
                    if (phase == "before-final-observations" && mode == "final-checkpoint-error") throw finalCheckpointError;
                }, cancel.Token));
            Check(error.DestinationPath == absent.Path && error.PublicationLockPath == transactionLock &&
                error.RetainedRecoveryDirectory == project.RootPath, "unconfirmed outcome carries exact inspection paths");
            Check(File.ReadAllBytes(error.RetainedStagePath).SequenceEqual(prepared), "attempted stage bytes always retained");
            Check(File.ReadAllBytes(error.SourceLeaseEvidencePath).Length == 32 &&
                File.ReadAllBytes(error.DestinationLeaseEvidencePath).Length == 32, "unconfirmed guards are closed and deliberately retained");
            if (mode == "both-final-errors")
            {
                Check(error.InnerException is AggregateException aggregate && aggregate.InnerExceptions.Count == 3 &&
                    aggregate.InnerExceptions[0] is IOException && aggregate.InnerExceptions[1] is InvalidDataException &&
                    aggregate.InnerExceptions[2] is InvalidDataException, "actual call failure stays first with both final failures attached");
                Check(error.ObservedSource is { Exists: false } && error.ObservedDestination == null,
                    "actual observed absence differs from failed destination capture");
            }
            else
            {
                Check((mode == "final-checkpoint-error" ? error.InnerException is AggregateException attached && attached.InnerExceptions.Count == 2 &&
                    attached.InnerExceptions[0] is IOException && ReferenceEquals(attached.InnerExceptions[1], finalCheckpointError) : error.InnerException is IOException) &&
                    error.ObservedSource!.ContentSha256 == source.ContentSha256 &&
                    File.ReadAllBytes(source.Path).SequenceEqual(original), "actual source recapture binds unchanged source");
                Check(error.ObservedDestination!.Exists == (mode is "equal-output" or "different-output"), "unconfirmed actual destination observation is not a commit certificate");
                if (mode == "equal-output") Check(error.ObservedDestination.ContentSha256 == Hash(prepared), "matching bytes do not upgrade unconfirmed to known committed");
            }
            if (mode is "lock" or "both-final-errors" or "final-checkpoint-error") Check(File.ReadAllText(transactionLock) == "foreign-publication-lock", "foreign transaction lock preserved");
        }
    }

    private static void KnownCommit()
    {
        var (lateProject, lateSource, lateAbsent) = Fixture("late-cancel");
        using var late = new CancellationTokenSource();
        var success = NativeFreshLevelDocumentStore.ForkForSmoke(lateSource, lateAbsent, phase =>
        { if (phase == "published") late.Cancel(); }, late.Token);
        Check(late.IsCancellationRequested && success.ContentSha256 == Hash(Canonical(lateSource)), "actual acknowledged commit finishes verification despite late token cancellation");
        Clean(lateProject);
        foreach (bool cancellation in new[] { false, true })
        {
            var (project, source, absent) = Fixture("known-callback-" + cancellation);
            Exception primary = cancellation ? new OperationCanceledException("late callback") : new IOException("late callback");
            var error = Throws<NativeFreshLevelDocumentForkCommittedException>(() =>
                NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase => { if (phase == "published") throw primary; }));
            Check(ReferenceEquals(error.InnerException, primary) && File.ReadAllBytes(absent.Path).SequenceEqual(Canonical(source)), "actual known commit preserves exact callback failure and bytes");
            Check(error.RetainedStagePath == "" && File.ReadAllBytes(error.SourceLeaseEvidencePath).Length == 32 &&
                File.ReadAllBytes(error.DestinationLeaseEvidencePath).Length == 32, "known failure retains guards without claiming missing stage exists");
        }
        var (bothProject, bothSource, bothAbsent) = Fixture("known-both-final-failures");
        var first = new IOException("published checkpoint failed");
        var combined = Throws<NativeFreshLevelDocumentForkCommittedException>(() =>
            NativeFreshLevelDocumentStore.ForkForSmoke(bothSource, bothAbsent, phase =>
            {
                if (phase != "published") return;
                File.WriteAllText(bothSource.Path, "invalid source");
                File.WriteAllText(bothAbsent.Path, "invalid aggregate");
                throw first;
            }));
        Check(combined.InnerException is AggregateException errors && errors.InnerExceptions.Count == 3 &&
            ReferenceEquals(errors.InnerExceptions[0], first) && errors.InnerExceptions.Skip(1).All(e => e is InvalidDataException),
            "both actual final captures run after acknowledged commit even when checkpoint and first capture fail");
        foreach (string target in new[] { "source", "destination", "manifest" })
        {
            var (project, source, absent) = Fixture("known-raw-change-" + target);
            string path = target == "source" ? source.Path : target == "destination" ? absent.Path : project.ManifestPath;
            byte[]? changed = null;
            var error = Throws<NativeFreshLevelDocumentForkCommittedException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase != "published") return;
                File.AppendAllText(path, " "); changed = File.ReadAllBytes(path);
            }));
            Check(File.ReadAllBytes(path).SequenceEqual(changed!) && File.Exists(absent.Path), "actual valid raw change after commit is preserved: " + target);
            Check(target == "manifest" ? error.InnerException is AggregateException manifestErrors && manifestErrors.InnerExceptions.Count == 2 :
                error.InnerException is InvalidDataException, "successful final parsing still checks both captured raw versions: " + target);
        }
        foreach (string replacement in new[] { "file", "directory", "link" })
        {
            var (project, source, absent) = Fixture("known-stage-" + replacement);
            string stage = "", sentinel = Path.Combine(project.RootPath, "sentinel");
            File.WriteAllText(sentinel, "foreign target");
            var error = Throws<NativeFreshLevelDocumentForkCommittedException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase == "staged") stage = Names(project).Single(p => p.EndsWith(".tmp", StringComparison.Ordinal));
                if (phase != "before-release") return;
                if (replacement == "file") File.WriteAllBytes(stage, Canonical(source));
                else if (replacement == "directory") Directory.CreateDirectory(stage);
                else File.CreateSymbolicLink(stage, sentinel);
            }));
            Check(error.RetainedStagePath == stage && File.ReadAllText(sentinel) == "foreign target" &&
                File.ReadAllBytes(absent.Path).SequenceEqual(Canonical(source)), "postcommit stage replacement is preserved, including equal bytes");
            Check(replacement == "directory" ? Directory.Exists(stage) : replacement == "link" ? new FileInfo(stage).LinkTarget != null :
                File.ReadAllBytes(stage).SequenceEqual(Canonical(source)), "unknown consumed-stage owner is not deleted");
        }
        foreach (string lockName in new[] { SourceLock, DestinationLock })
        foreach (string replacement in new[] { "regular", "oversized", "directory", "link", "missing" })
        {
            var (project, source, absent) = Fixture("known-marker-" + lockName + replacement);
            string path = Path.Combine(project.RootPath, lockName), moved = Path.Combine(project.RootPath, "owned-moved-marker"),
                sentinel = Path.Combine(project.RootPath, "sentinel");
            File.WriteAllText(sentinel, "untouched");
            var error = Throws<NativeFreshLevelDocumentForkCommittedException>(() => NativeFreshLevelDocumentStore.ForkForSmoke(source, absent, phase =>
            {
                if (phase != "before-release") return;
                File.Move(path, moved);
                if (replacement == "regular") File.WriteAllBytes(path, new byte[32]);
                else if (replacement == "oversized") File.WriteAllBytes(path, new byte[33]);
                else if (replacement == "directory") Directory.CreateDirectory(path);
                else if (replacement == "link") File.CreateSymbolicLink(path, sentinel);
            }));
            string evidence = lockName == SourceLock ? error.SourceLeaseEvidencePath : error.DestinationLeaseEvidencePath;
            Check(evidence == (replacement == "missing" ? project.RootPath : path) && File.ReadAllBytes(moved).Length == 32 &&
                File.ReadAllText(sentinel) == "untouched", "drifted marker owner is accounted after real handle closes");
            Check(error.InnerException != null && File.Exists(absent.Path), "release failure after PublishNew is known committed");
            if (lockName == SourceLock) Check(error.DestinationLeaseEvidencePath == "" && !File.Exists(Path.Combine(project.RootPath, DestinationLock)), "later source release failure never recreates already-released destination marker");
            else Check(File.ReadAllBytes(error.SourceLeaseEvidencePath).Length == 32, "destination release failure still accounts and retains source owner");
        }
    }

    private static void OwnerTerminalSemantics()
    {
        foreach (bool retainFirst in new[] { false, true })
        {
            var (project, source, _) = Fixture("terminal-" + retainFirst);
            var owner = NativeFreshDocumentStorage.AcquireLegacyReadLease(source);
            if (retainFirst) owner.RetainForRecovery(); else owner.Dispose();
            Check(owner.DeliberatelyRetained == retainFirst && owner.TerminalFailure == null &&
                File.Exists(owner.LeasePath) == retainFirst, "actual guard retain and release are distinct terminal outcomes");
            if (retainFirst) Check(File.ReadAllBytes(owner.LeasePath).Length == 32 && owner.RetainedEvidencePath == owner.LeasePath, "retention closes actual stream and preserves known owner");
            File.WriteAllText(owner.LeasePath, "later foreign owner");
            owner.Dispose(); owner.RetainForRecovery(); owner.Dispose();
            Check(File.ReadAllText(owner.LeasePath) == "later foreign owner", "all repeated terminal calls are idempotent and cannot reacquire delete authority");
            File.Delete(owner.LeasePath); owner.RetainForRecovery(); owner.Dispose();
            Check(!File.Exists(owner.LeasePath), "terminal owner does not recreate a later missing marker");
        }
    }

    private static void PublicContract()
    {
        var (project, source, absent) = Fixture("public");
        byte[] manifest = File.ReadAllBytes(project.ManifestPath);
        File.AppendAllText(source.Path, " \n");
        source = NativeId65FreshDocumentStore.Capture(project);
        byte[] rawSource = File.ReadAllBytes(source.Path);
        string[] original = Names(project);
        using (var cancel = new CancellationTokenSource())
        {
            cancel.Cancel();
            Check(Throws<ArgumentNullException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(null!, null!, cancel.Token)).ParamName == "source", "fork source null first");
            Check(Throws<ArgumentNullException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, null!, cancel.Token)).ParamName == "expectedAbsent", "fork destination null second");
            Throws<OperationCanceledException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent, cancel.Token));
            Check(Names(project).SequenceEqual(original), "null/precancel fork does not create owners");
        }
        var result = NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent);
        byte[] expected = NativeFreshLevelDocumentCodec.SerializeCanonical(NativeFreshLevelDocumentIntent.Create(source.Intent!, []));
        Check(result.Exists && result.Intent!.Placements.Count == 0 && result.ContentSha256 == Hash(expected) &&
            File.ReadAllBytes(result.Path).SequenceEqual(expected), "actual new-only fork writes exact empty-placement aggregate");
        Check(NativeId65FreshDocumentCodec.SerializeCanonical(result.Intent!.Base).SequenceEqual(
            NativeId65FreshDocumentCodec.SerializeCanonical(source.Intent!)), "fork retains actual source floor/colors/spawn/music");
        Check(File.ReadAllBytes(source.Path).SequenceEqual(rawSource) && File.ReadAllBytes(project.ManifestPath).SequenceEqual(manifest), "noncanonical source and whole manifest remain exact");
        Check(result.RetainedRecoveryDirectory == "" && result.RootPath == source.RootPath && result.ProjectId == source.ProjectId &&
            NativeFreshLevelDocumentStore.Capture(project).ContentSha256 == result.ContentSha256, "actual verified project-bound result");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, absent));
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, result));
        Check(File.ReadAllBytes(result.Path).SequenceEqual(expected), "repeat refuses even identical fork bytes");
        NativeId65FreshDocumentStore.Save(source, Cases.Base(20));
        Check(File.ReadAllBytes(result.Path).SequenceEqual(expected), "later legacy edits do not update aggregate");
        byte[] editedLegacy = File.ReadAllBytes(source.Path);
        NativeFreshLevelDocumentStore.Save(result, Cases.Document(Cases.Wizard()));
        Check(File.ReadAllBytes(source.Path).SequenceEqual(editedLegacy), "later aggregate edits do not update source");
        Clean(project);

        var (_, missing, destination) = Fixture("source-absent", withSource: false);
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(missing, destination));
        var (otherProject, otherSource, otherAbsent) = Fixture("other");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(source, otherAbsent));
        var (_, staleSource, staleDestination) = Fixture("stale-source");
        File.AppendAllText(staleSource.Path, " ");
        Throws<InvalidDataException>(() => NativeFreshLevelDocumentStore.ForkLegacyFloor(staleSource, staleDestination));
        Clean(otherProject);
        Check(typeof(NativeFreshLevelDocumentForkCommittedException).IsSealed &&
            typeof(NativeFreshLevelDocumentForkCommittedException).GetConstructors().Length == 0 &&
            typeof(NativeFreshLevelDocumentForkPublicationUnconfirmedException).IsSealed &&
            typeof(NativeFreshLevelDocumentForkPublicationUnconfirmedException).GetConstructors().Length == 0,
            "two distinct internally issued public error types");
        Check(typeof(NativeId65FreshSavedDocumentExportContext).GetProperty("Snapshot")!.PropertyType == typeof(NativeId65FreshDocumentSnapshot), "old saved publication context remains legacy typed");
    }

    private static (EditorProjectLayout Project, NativeId65FreshDocumentSnapshot Source, NativeFreshLevelDocumentSnapshot Destination)
        Fixture(string id, bool withSource = true)
    {
        string root = Path.Combine(_temporary, id);
        Directory.CreateDirectory(root);
        string projectId = "fork-" + Hash(System.Text.Encoding.UTF8.GetBytes(id))[..16];
        var project = new EditorProjectLayout(root, projectId);
        File.WriteAllText(project.ManifestPath, "{\"schemaVersion\":1,\"projectId\":\"" + projectId + "\",\"sentinel\":{\"keep\":true}}\n");
        var source = NativeId65FreshDocumentStore.Capture(project);
        if (withSource) source = NativeId65FreshDocumentStore.Save(source, Cases.Base());
        return (project, source, NativeFreshLevelDocumentStore.Capture(project));
    }
    private static string[] Names(EditorProjectLayout project) => Directory.GetFileSystemEntries(project.RootPath).Order().ToArray();
    private static string[] Tree(string root) => Directory.GetFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path => Path.GetRelativePath(root, path) + ":" + (Directory.Exists(path) ? "directory" : Hash(File.ReadAllBytes(path)))).Order().ToArray();
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static void Clean(EditorProjectLayout project) => Check(!Names(project).Any(p => Path.GetFileName(p).StartsWith('.')), "fork owned paths cleaned");
    private static void Check(bool condition, string label) { if (!condition) throw new InvalidDataException(label); _checks++; }
    private static T Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T error) { _checks++; return error; }
        throw new InvalidDataException("Expected " + typeof(T).Name);
    }
}
