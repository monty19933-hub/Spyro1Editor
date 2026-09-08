using Spyro.Editor.Core.Persistence;
using Spyro.Editor.Core.Editing;
using System.Security.Cryptography;
using System.Runtime.ExceptionServices;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeId65FreshResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }


// Fresh-only workflow: unlike the older Town Wizard workflow, occupied complete sets are
// refused, companions have bounded exact EOF checks, and cleanup contributes to
// the returned outcome. Existing serializers and physical writers stay in plans.
internal static class NativeId65FreshResearchPublicationEngine
{
    internal static Task<NativeId65FreshDiscResearchPublication> PublishAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        NativeId65FreshResearchFault fault,
        Action<NativeId65FreshResearchFault, string>? checkpoint, CancellationToken token) =>
        plan?.SavedLevelPlan != null
            ? PublishSavedLevelAsync(plan, directory, fault, checkpoint, token)
            : PublishCoreAsync(plan!, directory, fault, checkpoint, token);

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishDocumentForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory, NativeId65FreshDocumentResearchFault fault,
        Action<NativeId65FreshDocumentResearchFault, string>? checkpoint, CancellationToken token)
    {
        if (fault == NativeId65FreshDocumentResearchFault.None)
            throw new InvalidDataException("A saved-document staging exercise must force a precommit fault.");
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token, fault, checkpoint);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishDocumentPostcommitForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        Action<NativeId65FreshDocumentPostcommitPoint, string> checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.SavedContext == null)
            throw new InvalidDataException("Postcommit exercise requires an actual saved-document binding.");
        ArgumentNullException.ThrowIfNull(checkpoint);
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token,
            postcommitCheckpoint: checkpoint);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(NativeId65FreshResearchPublicationBinding plan,
        string outputDirectory, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        token.ThrowIfCancellationRequested();
        string root = Resolve(plan, outputDirectory);
        RequireCompleteSet(plan, root);
        await plan.RequireFreshAsync(token);
        var result = VerifySet(plan, root, token);
        await plan.RequireFreshAsync(token);
        return Result(plan, root, result);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishWizardPostcommitForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        Action<NativeId65FreshWizardPostcommitPoint, string> checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.WizardPlan == null)
            throw new InvalidDataException("Postcommit exercise requires an actual fresh Wizard binding.");
        ArgumentNullException.ThrowIfNull(checkpoint);
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token,
            wizardPostcommitCheckpoint: checkpoint);
    }

    // Distinct from the old always-forced precommit routes. This instrumented
    // exercise must install a real blocking transaction lock, then lets the
    // unchanged PublishNew make the actual call and determine its own failure.
    internal static async Task<NativeId65FreshDiscResearchPublication> PublishWizardUnconfirmedForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        Action<string> checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.WizardPlan == null)
            throw new InvalidDataException("Unconfirmed exercise requires an actual fresh Wizard binding.");
        ArgumentNullException.ThrowIfNull(checkpoint);
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token,
            wizardUnconfirmedCheckpoint: checkpoint);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishTexturedPostcommitForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        Action<NativeId65FreshTexturedPostcommitPoint, string> checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.TexturedPlan == null)
            throw new InvalidDataException("Postcommit exercise requires an actual fresh textured binding.");
        ArgumentNullException.ThrowIfNull(checkpoint);
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token,
            texturedPostcommitCheckpoint: checkpoint);
    }

    internal static async Task<NativeId65FreshDiscResearchPublication> PublishTexturedUnconfirmedForSmokeAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory, Action<string> checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.TexturedPlan == null)
            throw new InvalidDataException("Unconfirmed exercise requires an actual fresh textured binding.");
        ArgumentNullException.ThrowIfNull(checkpoint);
        return await PublishCoreAsync(plan, directory, NativeId65FreshResearchFault.None, null, token,
            texturedUnconfirmedCheckpoint: checkpoint);
    }

    // Called only by PublishAsync's actual SavedLevelPlan arm. The original
    // PublishCore preflight stays in one place, and old five entry paths bypass
    // this wrapper entirely. No caller-supplied serializer/IO/state authority.
    private static async Task<NativeId65FreshDiscResearchPublication> PublishSavedLevelAsync(
        NativeId65FreshResearchPublicationBinding binding, string directory,
        NativeId65FreshResearchFault fault, Action<NativeId65FreshResearchFault, string>? checkpoint,
        CancellationToken token)
    {
        var plan = binding.SavedLevelPlan!;
        token.ThrowIfCancellationRequested();
        NativeId65FreshDiscResearchPublication? result = null;
        Exception? primary = null, finalFailure = null;
        try { result = await PublishCoreAsync(binding, directory, fault, checkpoint, token); }
        catch (Exception error) { primary = error; }
        // Independently finalizes all held/current sources and actual saved raw
        // version, even when preflight/artifact/guard/cleanup failed. No IO retry.
        try { await plan.RequireFreshAsync(CancellationToken.None); }
        catch (Exception error) { finalFailure = error; }
        if (finalFailure == null)
        {
            if (primary != null) ExceptionDispatchInfo.Capture(primary).Throw();
            return result!;
        }
        Exception failure = primary == null ? finalFailure : new IOException(
            "Saved-level publication has additional final source or saved-version failures.",
            new AggregateException(primary, finalFailure));
        // Actual typed outcomes/results, not file contents or caller claims.
        if (primary is NativeId65FreshResearchPublicationUnconfirmedException unconfirmed)
            throw new NativeId65FreshResearchPublicationUnconfirmedException(binding, unconfirmed.OutputDirectory,
                unconfirmed.RetainedStageDirectory, unconfirmed.RetainedOutputLeasePath,
                unconfirmed.RetainedProjectLeasePath, failure);
        if (primary is NativeFreshSavedLevelResearchCommittedException committed)
            throw new NativeFreshSavedLevelResearchCommittedException(committed.OutputDirectory, plan,
                committed.RetainedRecoveryDirectory, committed.RetainedStageDirectory,
                committed.RetainedOutputLeasePath, committed.RetainedProjectLeasePath, failure);
        if (result != null)
            throw new NativeFreshSavedLevelResearchCommittedException(result.OutputDirectory, plan,
                result.RetainedRecoveryDirectory, "", "", "", failure);
        ExceptionDispatchInfo.Capture(failure).Throw();
        throw new InvalidOperationException("Unreachable saved-level failure dispatch.");
    }

    // Tests may supply an ACTUAL acquired Store owner and an exception only.
    // This seam cannot acquire a plan, inject IO or set publication state.
    internal static Exception? CompleteSavedLevelGuard(NativeFreshLevelDocumentStore.ReadLease guard,
        Exception? primary)
    {
        ArgumentNullException.ThrowIfNull(guard);
        try { guard.Complete(primary); return null; }
        catch (Exception outcome) { return outcome; }
    }

    private sealed record SavedLevelCall(ExportArtifactPublicationResult? Publication,
        Exception? Failure, string ProjectLeaseEvidence, bool UnresolvedGuard);

    private static SavedLevelCall InvokeSavedLevel(NativeId65FreshResearchPublicationBinding binding,
        string root, string stage, NativeFreshResearchPublicationAttempt attempt, CancellationToken token)
    {
        var plan = binding.SavedLevelPlan!;
        var replacements = binding.OutputNames.Select(name => new ExportArtifactReplacement(
            Path.Combine(root, name), Path.Combine(stage, name))).ToArray();
        string inspection = Path.Combine(plan.Saved.Snapshot.RootPath,
            NativeFreshDocumentStorage.StorageDescriptor.For(
                NativeFreshDocumentStorage.StorageProfile.LevelAggregateV1).LeaseName);
        NativeFreshLevelDocumentStore.ReadLease? guard = null;
        ExportArtifactPublicationResult? publication = null; Exception? failure = null;
        try
        {
            guard = plan.Saved.AcquireFinalReadGuard(token);
            // No awaits, source readback, callback or injected success here.
            publication = attempt.Invoke(root, replacements, token);
        }
        catch (Exception error) { failure = error; }
        if (guard == null)
        {
            // Acquire already finalized any internal partially acquired owner.
            // No returned owner: preserve conservatively, do not inspect errors
            // to invent ownership or attempt a second Complete.
            return new(publication, failure, inspection, true);
        }
        failure = CompleteSavedLevelGuard(guard, failure); // REPLACES primary, never appends it.
        bool unresolved = guard.TerminalFailure != null || guard.RetainedEvidencePath.Length != 0;
        string evidence = guard.RetainedEvidencePath.Length != 0 ? guard.RetainedEvidencePath :
            unresolved ? guard.LeasePath : "";
        return new(publication, failure, evidence, unresolved);
    }

    private static async Task<NativeId65FreshDiscResearchPublication> PublishCoreAsync(NativeId65FreshResearchPublicationBinding plan,
        string outputDirectory, NativeId65FreshResearchFault fault,
        Action<NativeId65FreshResearchFault, string>? checkpoint, CancellationToken token,
        NativeId65FreshDocumentResearchFault documentFault = NativeId65FreshDocumentResearchFault.None,
        Action<NativeId65FreshDocumentResearchFault, string>? documentCheckpoint = null,
        Action<NativeId65FreshDocumentPostcommitPoint, string>? postcommitCheckpoint = null,
        Action<NativeId65FreshWizardPostcommitPoint, string>? wizardPostcommitCheckpoint = null,
        Action<string>? wizardUnconfirmedCheckpoint = null,
        Action<NativeId65FreshTexturedPostcommitPoint, string>? texturedPostcommitCheckpoint = null,
        Action<string>? texturedUnconfirmedCheckpoint = null)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(fault)) throw new InvalidDataException($"Unknown {plan.Label} research fault.");
        if (!Enum.IsDefined(documentFault) || (documentFault != NativeId65FreshDocumentResearchFault.None && plan.SavedContext == null))
            throw new InvalidDataException("Unknown saved-document precommit fault or binding.");
        if (postcommitCheckpoint != null && plan.SavedContext == null)
            throw new InvalidDataException("Postcommit exercise cannot bind a fixed publisher.");
        if (wizardPostcommitCheckpoint != null && plan.WizardPlan == null)
            throw new InvalidDataException("Wizard postcommit exercise cannot bind another publisher kind.");
        if (wizardUnconfirmedCheckpoint != null && plan.WizardPlan == null)
            throw new InvalidDataException("Wizard unconfirmed exercise cannot bind another publisher kind.");
        if ((texturedPostcommitCheckpoint != null || texturedUnconfirmedCheckpoint != null) && plan.TexturedPlan == null)
            throw new InvalidDataException("Textured exercise cannot bind another publisher kind.");
        token.ThrowIfCancellationRequested();
        string root = Resolve(plan, outputDirectory);
        RequireEmpty(plan, root); // Complete sets may only be separately verified.
        await plan.RequireFreshAsync(token);
        Directory.CreateDirectory(root);
        RequireNoLink(plan, root);
        string leasePath = Path.Combine(root, plan.LeaseName);
        RequireNoLink(plan, leasePath);
        string stage = Path.Combine(root, plan.StagePrefix + Guid.NewGuid().ToString("N"));
        bool preserve = false;
        NativeId65FreshDiscResearchPublication? completed = null;
        var attempt = new NativeFreshResearchPublicationAttempt();
        string transactionRecovery = "", projectLeaseEvidence = "";
        NativeId65FreshDiscVerification? aggregateStaged = null;
        bool aggregateFinalArtifactAttempted = false;
        byte[]? documentLeaseOwner = !plan.TracksCommittedFailure ? null : RandomNumberGenerator.GetBytes(32);
        // One explicit terminal Dispose inside accounting, never a second using
        // disposal outside the reported outcome. All setup above owns no handle.
        FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        try
        {
            if (documentLeaseOwner != null) { lease.Write(documentLeaseOwner); lease.Flush(flushToDisk: true); }
            RequireOnly(plan, root, [leasePath]);
            Directory.CreateDirectory(stage);
            string image = Path.Combine(stage, plan.ImageName);
            plan.WriteNewImage(image, token, () => Checkpoint(NativeId65FreshResearchFault.AfterFirstSector, image));
            Checkpoint(NativeId65FreshResearchFault.AfterImageWrite, image);
            var verified = plan.VerifyImage(image, token);
            if (plan.SavedLevelPlan != null) aggregateStaged = verified;
            WriteNew(Path.Combine(stage, plan.ReceiptName), plan.BuildReceipt(verified), token);
            WriteNew(Path.Combine(stage, plan.CueName), plan.CopyCueBytes(), token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException($"{plan.Label} staging readback changed.");

            // Callback BEFORE final guards; forced exception AFTER guards.
            // A missing freshness guard still cannot let fault tests publish.
            checkpoint?.Invoke(NativeId65FreshResearchFault.BeforePublication, image);
            documentCheckpoint?.Invoke(NativeId65FreshDocumentResearchFault.BeforePublication, image);
            token.ThrowIfCancellationRequested();
            await plan.RequireFreshAsync(token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException($"{plan.Label} staging changed before publication.");
            RequireOnly(plan, root, [leasePath, stage]);
            RequireNoLink(plan, root);
            token.ThrowIfCancellationRequested();
            if (fault == NativeId65FreshResearchFault.BeforePublication)
                throw new IOException($"Deliberate {plan.Label} publication fault: BeforePublication");

            ExportArtifactPublicationResult? publication = null;
            NativeId65FreshDocumentStore.ReadLease? savedGuard = null;
            Exception? callFailure = null, guardFailure = null;
            bool aggregateGuardUnresolved = false;
            if (plan.SavedLevelPlan != null)
            {
                var savedCall = InvokeSavedLevel(plan, root, stage, attempt, token);
                publication = savedCall.Publication;
                callFailure = savedCall.Failure;
                projectLeaseEvidence = savedCall.ProjectLeaseEvidence;
                aggregateGuardUnresolved = savedCall.UnresolvedGuard;
                if (publication != null) transactionRecovery = publication.RetainedRecoveryDirectory;
            }
            else
            {
                try
                {
                    // LAST saved-state check, after full source/staging/root guards.
                    // No awaits or image work while excluding cooperating saves.
                    savedGuard = plan.SavedContext?.AcquireFinalPrecommitGuard(token);
                    DocumentCheckpoint(NativeId65FreshDocumentResearchFault.AfterSavedGuard, image);
                    if (documentFault == NativeId65FreshDocumentResearchFault.BeforePublication)
                        throw new IOException("Deliberate saved-document publication fault: BeforePublication");
                    if (wizardUnconfirmedCheckpoint != null)
                    {
                        string blockingLock = Path.Combine(root, ".export-publication.lock");
                        wizardUnconfirmedCheckpoint(blockingLock);
                        RequireNoLink(plan, blockingLock);
                        if (!File.Exists(blockingLock) || Directory.Exists(blockingLock))
                            throw new InvalidDataException("Unconfirmed exercise must install a real non-linked transaction lock before invocation.");
                    }
                    if (texturedUnconfirmedCheckpoint != null)
                    {
                        string blockingLock = Path.Combine(root, ".export-publication.lock");
                        texturedUnconfirmedCheckpoint(blockingLock);
                        RequireNoLink(plan, blockingLock);
                        if (!File.Exists(blockingLock) || Directory.Exists(blockingLock))
                            throw new InvalidDataException("Textured unconfirmed exercise must install a real non-linked transaction lock before invocation.");
                    }
                    publication = attempt.Invoke(root,
                        plan.OutputNames.Select(name => new ExportArtifactReplacement(
                            Path.Combine(root, name), Path.Combine(stage, name))).ToArray(), token);
                    transactionRecovery = publication.RetainedRecoveryDirectory;
                }
                catch (Exception error) { callFailure = error; }
                finally
                {
                    try { savedGuard?.Dispose(); }
                    catch (Exception error) { guardFailure = error; }
                    projectLeaseEvidence = savedGuard?.RetainedEvidencePath ?? "";
                }
            }
            if (callFailure != null) attempt.RecordFailure(callFailure);
            if (guardFailure != null) attempt.RecordFailure(guardFailure);
            if (plan.SavedLevelPlan == null && projectLeaseEvidence.Length != 0 && guardFailure == null &&
                attempt.State != NativeFreshResearchPublicationState.NotInvoked)
                attempt.RecordFailure(new IOException("Saved-document guard retained ownership evidence at " + projectLeaseEvidence));
            preserve = attempt.State is NativeFreshResearchPublicationState.Unconfirmed or NativeFreshResearchPublicationState.RollbackIncomplete ||
                transactionRecovery.Length != 0 || guardFailure != null || projectLeaseEvidence.Length != 0 || aggregateGuardUnresolved ||
                callFailure is ExportArtifactRollbackException ||
                (attempt.State == NativeFreshResearchPublicationState.Committed && attempt.Failure != null);
            if (attempt.Failure == null)
            {
            if (attempt.State != NativeFreshResearchPublicationState.Committed || publication == null)
                throw new InvalidDataException("Fresh publication has no actual returned transaction result.");
            // PublishNew actually returned, and the Store guard was released.
            // Callback cancellation must not change the generic final token.
            postcommitCheckpoint?.Invoke(NativeId65FreshDocumentPostcommitPoint.AfterCommit,
                Path.Combine(root, plan.ImageName));
            wizardPostcommitCheckpoint?.Invoke(NativeId65FreshWizardPostcommitPoint.AfterCommit,
                Path.Combine(root, plan.ImageName));
            texturedPostcommitCheckpoint?.Invoke(NativeId65FreshTexturedPostcommitPoint.AfterCommit,
                Path.Combine(root, plan.ImageName));
            // Own lease/empty stage and typed retained recovery can exist here.
            // Separate Verify permits only the exact three-artifact set.
            // Preserve the old starter policy exactly. The exact edited profile
            // completes final readback/freshness noncancellably after commit.
            CancellationToken finalToken = plan.PostCommitToken(token);
            if (plan.SavedLevelPlan != null) aggregateFinalArtifactAttempted = true;
            var final = VerifySet(plan, root, finalToken, requireOnlyArtifacts: false);
            if (final != verified) throw new InvalidDataException($"{plan.Label} final output differs from verified staging.");
            await plan.RequireFreshAsync(finalToken);
            List<string> allowed = plan.OutputNames
                .Select(name => Path.Combine(root, name)).Concat([leasePath, stage]).ToList();
            if (preserve)
            {
                allowed.Add(publication.RetainedRecoveryDirectory);
                string transactionLock = Path.Combine(root, ".export-publication.lock");
                if (File.Exists(transactionLock)) allowed.Add(transactionLock);
            }
            RequireOnly(plan, root, allowed.ToArray());
            completed = Result(plan, root, final, publication.RetainedRecoveryDirectory);
            }
        }
        catch (Exception error)
        {
            attempt.RecordFailure(error);
            if (attempt.State != NativeFreshResearchPublicationState.NotInvoked || error is ExportArtifactRollbackException)
                preserve = true;
        }
        finally
        {
            if (attempt.State == NativeFreshResearchPublicationState.Unconfirmed)
            {
                preserve = true;
                // An independent source observation cannot supply a missing
                // transaction acknowledgement or suppress its primary failure.
                if (plan.SavedLevelPlan == null)
                    await attempt.CaptureAsync(() => plan.RequireFreshAsync(CancellationToken.None));
            }
            bool published = attempt.State == NativeFreshResearchPublicationState.Committed;
            if (plan.SavedLevelPlan != null && published && !aggregateFinalArtifactAttempted)
            {
                // A failed Complete must not suppress the actual committed files.
                // This is the first final artifact attempt, never a retry of one
                // which already succeeded or failed on the normal path.
                aggregateFinalArtifactAttempted = true;
                attempt.Capture(() =>
                {
                    var final = VerifySet(plan, root, CancellationToken.None, requireOnlyArtifacts: false);
                    if (aggregateStaged == null || final != aggregateStaged)
                        throw new InvalidDataException("Saved-level final output differs from its actual staged verification.");
                });
                if (attempt.Failure != null) preserve = true;
            }
            if (plan.TexturedPlan != null && published)
            {
                // Artifact failure must not suppress the new role's independent,
                // uncancellable final BIN/CUE observations or evidence retention.
                await attempt.CaptureAsync(() => plan.RequireFreshAsync(CancellationToken.None));
                if (attempt.Failure != null) preserve = true;
            }
            if (!preserve)
            {
                try
                {
                    if (plan.SavedContext != null && published && completed != null)
                        postcommitCheckpoint?.Invoke(NativeId65FreshDocumentPostcommitPoint.BeforeStageCleanup, stage);
                    if (plan.WizardPlan != null && published && completed != null)
                        wizardPostcommitCheckpoint?.Invoke(NativeId65FreshWizardPostcommitPoint.BeforeStageCleanup, stage);
                    if (plan.TexturedPlan != null && published && completed != null)
                        texturedPostcommitCheckpoint?.Invoke(NativeId65FreshTexturedPostcommitPoint.BeforeStageCleanup, stage);
                    RequireNoLink(plan, stage);
                    // PublishNew moved all three owned files. ANY subsequently
                    // present Wizard/textured stage entry is a new owner, even a reused
                    // output basename; never delete it as an old partial file.
                    if (plan.PreserveAnyPostcommitStageOwner && published && File.Exists(stage))
                        throw new IOException(plan.SavedLevelPlan != null
                            ? "Saved-level committed stage was replaced by a file; preserve it."
                            : plan.TexturedPlan != null
                            ? "Fresh textured committed stage was replaced by a file; preserve it."
                            : "Fresh Wizard committed stage was replaced by a file; preserve it.");
                    if (plan.PreserveAnyPostcommitStageOwner && published && Directory.Exists(stage) &&
                        Directory.EnumerateFileSystemEntries(stage).Any())
                        throw new IOException(plan.SavedLevelPlan != null
                            ? "Saved-level committed stage contains a subsequent owner; preserve it."
                            : plan.TexturedPlan != null
                            ? "Fresh textured committed stage contains a subsequent owner; preserve it."
                            : "Fresh Wizard committed stage contains a subsequent owner; preserve it.");
                    foreach (string name in plan.OutputNames)
                    {
                        string path = Path.Combine(stage, name);
                        if (File.Exists(path) && new FileInfo(path).LinkTarget == null) File.Delete(path);
                    }
                    if (Directory.Exists(stage))
                    {
                        if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
                        else
                        {
                            preserve = true; // Unknown owners remain with our lease for explicit recovery.
                            if (plan.TracksCommittedFailure && published)
                                attempt.RecordFailure(new IOException(plan.SavedLevelPlan != null
                                    ? "Saved-level stage retains an unknown owner after commit."
                                    : plan.WizardPlan != null
                                    ? "Fresh Wizard stage retains an unknown owner after commit."
                                    : plan.TexturedPlan != null
                                        ? "Fresh textured stage retains an unknown owner after commit."
                                        : "Saved-document stage retains an unknown owner after commit."));
                        }
                    }
                }
                catch (Exception error)
                {
                    preserve = true;
                    // Preserve historical fixed-profile IO cleanup warnings.
                    // Other failures must not bypass output-handle accounting.
                    if (!published || plan.TracksCommittedFailure ||
                        error is not (IOException or InvalidDataException or UnauthorizedAccessException))
                        attempt.RecordFailure(error);
                }
            }
            if (!attempt.Capture(lease.Dispose)) preserve = true;
            if (!preserve)
            {
                try
                {
                    if (documentLeaseOwner != null)
                    {
                        RequireNoLink(plan, leasePath);
                        RequireCompanionFile(leasePath, documentLeaseOwner, CancellationToken.None);
                    }
                    if (File.Exists(leasePath)) File.Delete(leasePath);
                }
                catch (Exception error)
                {
                    preserve = true;
                    if (!published || plan.TracksCommittedFailure ||
                        error is not (IOException or UnauthorizedAccessException))
                        attempt.RecordFailure(error);
                }
            }
        }
        Exception? failure = attempt.Failure;
        if (attempt.State == NativeFreshResearchPublicationState.Unconfirmed)
            throw new NativeId65FreshResearchPublicationUnconfirmedException(plan, root, stage, leasePath,
                projectLeaseEvidence, failure!);
        // Only this actual known-commit branch creates the typed failure, after
        // all project/output lease, stage and transaction accounting completed.
        if (plan.SavedContext != null && attempt.State == NativeFreshResearchPublicationState.Committed && failure != null)
            throw new NativeId65FreshDocumentResearchCommittedException(root, plan.PlanSha256,
                ExistingEvidence(transactionRecovery), ExistingEvidence(stage), ExistingEvidence(leasePath),
                projectLeaseEvidence, failure);
        if (plan.WizardPlan != null && attempt.State == NativeFreshResearchPublicationState.Committed && failure != null)
            throw new NativeId65FreshWizardResearchCommittedException(root, plan.WizardPlan,
                ExistingEvidence(transactionRecovery), ExistingEvidence(stage), ExistingEvidence(leasePath),
                failure);
        if (failure != null)
        {
            if (plan.SavedLevelPlan != null && attempt.State == NativeFreshResearchPublicationState.Committed)
                throw new NativeFreshSavedLevelResearchCommittedException(root, plan.SavedLevelPlan,
                    ExistingEvidence(transactionRecovery), ExistingEvidence(stage), ExistingEvidence(leasePath),
                    projectLeaseEvidence, failure);
            if (plan.TexturedPlan != null && attempt.State == NativeFreshResearchPublicationState.Committed)
                throw new NativeId65FreshTexturedResearchCommittedException(root, plan.TexturedPlan,
                    ExistingEvidence(transactionRecovery), ExistingEvidence(stage), ExistingEvidence(leasePath), failure);
            if (attempt.State == NativeFreshResearchPublicationState.Committed)
                throw new IOException($"{plan.Label} artifacts were published but final verification failed; preserve " + root, failure);
            // Existing UI catches IOException, not a bare AggregateException.
            // Keep every original object and order beneath that catchable outer.
            if (failure is AggregateException)
                throw new IOException(plan.Label + " publication failed with additional accounting errors; preserve " + root, failure);
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        // Cleanup warnings must remain discoverable, even if our own stage or
        // lease (rather than the transaction recovery journal) was retained.
        return completed! with
        {
            RetainedRecoveryDirectory = !string.IsNullOrEmpty(completed!.RetainedRecoveryDirectory)
                ? completed.RetainedRecoveryDirectory
                : plan.TracksCommittedFailure
                    ? ExistingEvidence(stage).Length != 0 ? stage : ExistingEvidence(leasePath).Length != 0 ? root : ""
                    : Directory.Exists(stage) ? stage : File.Exists(leasePath) ? root : ""
        };

        void Checkpoint(NativeId65FreshResearchFault point, string image)
        {
            checkpoint?.Invoke(point, image);
            token.ThrowIfCancellationRequested();
            if (fault == point) throw new IOException($"Deliberate {plan.Label} publication fault: " + point);
            if (point == NativeId65FreshResearchFault.AfterFirstSector) DocumentCheckpoint(NativeId65FreshDocumentResearchFault.AfterFirstSector, image);
            if (point == NativeId65FreshResearchFault.AfterImageWrite) DocumentCheckpoint(NativeId65FreshDocumentResearchFault.AfterImageWrite, image);
        }

        void DocumentCheckpoint(NativeId65FreshDocumentResearchFault point, string image)
        {
            if (plan.SavedContext == null) return;
            documentCheckpoint?.Invoke(point, image);
            token.ThrowIfCancellationRequested();
            if (documentFault == point) throw new IOException("Deliberate saved-document publication fault: " + point);
        }
    }

    private static string ExistingEvidence(string path)
    {
        if (path.Length == 0) return "";
        try { return File.Exists(path) || Directory.Exists(path) || new FileInfo(path).LinkTarget != null ? path : ""; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { return path; }
    }

    private static NativeId65FreshDiscVerification VerifySet(NativeId65FreshResearchPublicationBinding plan,
        string root, CancellationToken token, bool requireOnlyArtifacts = true)
    {
        if (requireOnlyArtifacts) RequireCompleteSet(plan, root);
        foreach (string name in plan.OutputNames)
        {
            string path = Path.Combine(root, name);
            RequireNoLink(plan, path);
            if (!File.Exists(path) || Directory.Exists(path)) throw new InvalidDataException($"{plan.Label} artifact is not a file.");
        }
        var result = plan.VerifyImage(Path.Combine(root, plan.ImageName), token);
        byte[] cue = plan.CopyCueBytes();
        RequireCompanionFile(Path.Combine(root, plan.CueName), cue, token);
        RequireCompanionFile(Path.Combine(root, plan.ReceiptName), plan.BuildReceipt(result), token);
        if (!DiscCueSheet.ReferencesDataImageSnapshot(cue, Path.Combine(root, plan.CueName),
            Path.Combine(root, plan.ImageName)))
            throw new InvalidDataException($"{plan.Label} output CUE binding changed.");
        if (requireOnlyArtifacts) RequireCompleteSet(plan, root);
        return result;
    }

    private static void RequireCompanionFile(string path, byte[] expected, CancellationToken token)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        RequireCompanion(input, expected, token);
    }

    internal static void RequireCompanion(Stream input, ReadOnlySpan<byte> expected, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(input);
        token.ThrowIfCancellationRequested();
        if (expected.Length is < 1 or > 65536 || !input.CanRead || !input.CanSeek ||
            input.Position != 0 || input.Length != expected.Length)
            throw new InvalidDataException("Fresh LAB companion readable owner/length changed before allocation.");
        byte[] actual = new byte[expected.Length];
        input.ReadExactly(actual);
        token.ThrowIfCancellationRequested();
        if (!actual.AsSpan().SequenceEqual(expected) || input.Length != expected.Length || input.Position != expected.Length ||
            input.ReadByte() != -1 || input.Length != expected.Length || input.Position != expected.Length)
            throw new InvalidDataException("Fresh LAB companion exact bytes, length, position or actual EOF changed.");
        token.ThrowIfCancellationRequested();
    }

    private static string Resolve(NativeId65FreshResearchPublicationBinding plan, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath, root, $"{plan.Label} output overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath, root, $"{plan.Label} output overlaps source CUE.");
        if (plan.SavedContext is { } saved)
        {
            PersistencePathSafety.EnsureTreesDoNotOverlap(saved.Snapshot.Path, root, "Saved fresh LAB output overlaps its captured document.");
            string manifest = new EditorProjectLayout(saved.Snapshot.RootPath, saved.Snapshot.ProjectId).ManifestPath;
            PersistencePathSafety.EnsureTreesDoNotOverlap(manifest, root, "Saved fresh LAB output overlaps its captured manifest.");
        }
        if (plan.SavedLevelPlan is { } aggregate)
        {
            var snapshot = aggregate.Saved.Snapshot;
            PersistencePathSafety.EnsureTreesDoNotOverlap(snapshot.Path, root, "Saved whole-level output overlaps its captured aggregate.");
            string manifest = new EditorProjectLayout(snapshot.RootPath, snapshot.ProjectId).ManifestPath;
            PersistencePathSafety.EnsureTreesDoNotOverlap(manifest, root, "Saved whole-level output overlaps its captured manifest.");
            string marker = Path.Combine(snapshot.RootPath, NativeFreshDocumentStorage.StorageDescriptor.For(
                NativeFreshDocumentStorage.StorageProfile.LevelAggregateV1).LeaseName);
            PersistencePathSafety.EnsureTreesDoNotOverlap(marker, root, "Saved whole-level output overlaps its project marker.");
        }
        RequireNoLink(plan, root);
        if (File.Exists(root)) throw new InvalidDataException($"{plan.Label} output directory is a file.");
        return root;
    }

    private static void RequireEmpty(NativeId65FreshResearchPublicationBinding plan, string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException($"{plan.Label} output is occupied; Publish never replaces or implicitly reuses it.");
    }

    private static void RequireCompleteSet(NativeId65FreshResearchPublicationBinding plan, string root)
    {
        RequireNoLink(plan, root);
        if (!Directory.Exists(root)) throw new InvalidDataException($"{plan.Label} complete artifact set is missing.");
        string[] expected = plan.OutputNames.Select(name => Path.Combine(root, name)).ToArray();
        RequireOnly(plan, root, expected);
    }

    private static void RequireOnly(NativeId65FreshResearchPublicationBinding plan, string root, string[] expected)
    {
        if (!Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new IOException($"{plan.Label} output is incomplete, occupied or retains recovery evidence.");
    }

    private static void RequireNoLink(NativeId65FreshResearchPublicationBinding plan, string path)
    {
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException($"{plan.Label} output may not be a symbolic link; trusted ancestors are required.");
    }

    private static void WriteNew(string path, byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(flushToDisk: true);
        token.ThrowIfCancellationRequested();
    }

    private static NativeId65FreshDiscResearchPublication Result(NativeId65FreshResearchPublicationBinding plan,
        string root, NativeId65FreshDiscVerification result, string recovery = "") => new(root,
        Path.Combine(root, plan.ImageName),
        Path.Combine(root, plan.CueName),
        Path.Combine(root, plan.ReceiptName),
        result.OutputSha256, plan.PlanSha256, result.ChangedSectors, recovery);
}
