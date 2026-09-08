using System.Runtime.ExceptionServices;
using System.Text;
using Spyro.Editor.Core.Exporting;
using static SavedLevelPublicationCases;

// Internal explicit reviewed physical gates. Never default/source simulations.
internal static class SavedLevelFaultCases
{
    internal sealed record Outcome(string Kind, int AfterReturnCount, int FinalArtifactCount,
        Exception? Failure, byte[]? ExpectedRawAfter, string[] Evidence);
    internal static async Task<Outcome> Run(NativeFreshSavedLevelResearchCandidatePlan plan,
        string root, string mode, SavedFiles input, Action? publishReturned = null)
    {
        using var cancel = new CancellationTokenSource();
        int after = 0, finals = 0; List<string> evidence = [];
        byte[]? changedRaw = null, cueBefore = null, cueAfter = null;
        string marker = Path.Combine(plan.Saved.Snapshot.RootPath, ".native-fresh-level.writer.lock");
        string heldMarker = marker + ".retained-" + Path.GetFileName(root);
        byte[] laterOwner = Enumerable.Repeat((byte)0xCD, 32).ToArray();
        var notificationFailure = new IOException("Deliberate saved-level final notification failure.");
        var fault = mode == Modes[2] ? NativeId65FreshResearchFault.AfterFirstSector :
            mode == Modes[3] ? NativeId65FreshResearchFault.BeforePublication : NativeId65FreshResearchFault.None;
        Action<string>? lockPreparation = mode == Modes[4] ? path =>
        {
            Check(path == Path.Combine(root, ".export-publication.lock"), "Exact Engine-nominated blocking lock.");
            WriteNewSmall(path, Encoding.ASCII.GetBytes("saved-level-unconfirmed-r01-lock")); evidence.Add(path);
        } : null;
        Action<NativeId65FreshResearchFault, string>? precommit = mode == Modes[3] ? (point, image) =>
        {
            if (point != NativeId65FreshResearchFault.BeforePublication) return;
            Check(Path.GetFileName(image) == NativeFreshSavedLevelResearchCandidatePlan.ImageName, "Actual image checkpoint.");
            string aggregate = plan.Saved.Snapshot.Path;
            Check(ReadSmall(aggregate, 65536).SequenceEqual(input.Raw), "Only exact dedicated fixture raw is changed.");
            string backup = Sibling(root, ".aggregate-before");
            WriteNewSmall(backup, input.Raw); evidence.Add(backup);
            changedRaw = input.Raw.Concat(new byte[] { 32 }).ToArray();
            ReplaceSmall(aggregate, input.Raw, changedRaw);
        } : null;
        Action<NativeFreshSavedLevelResearchPoint, string> observation = (point, actualRoot) =>
        {
            Check(actualRoot == root, "Actual output root.");
            if (point == NativeFreshSavedLevelResearchPoint.AfterReturnBeforeGuardComplete)
            {
                after++;
                if (mode == Modes[5])
                {
                    // No read of the FileShare.None held marker.
                    Unlinked(marker); Absent(heldMarker);
                    Check(new FileInfo(marker).Length == 32, "Actual acquired marker metadata.");
                    File.Move(marker, heldMarker); evidence.Add(heldMarker);
                    WriteNewSmall(marker, laterOwner); evidence.Add(marker);
                }
                else if (mode == Modes[6] || mode == Modes[7])
                {
                    string cue = Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.CueName);
                    cueBefore = ReadSmall(cue, 65536);
                    Check(cueBefore.SequenceEqual(plan.CopyCueBytes()), "Only actual newly published CUE is perturbed.");
                    string backup = Sibling(root, ".cue-before");
                    WriteNewSmall(backup, cueBefore); evidence.Add(backup);
                    cueAfter = cueBefore.ToArray(); cueAfter[0] ^= 1;
                    ReplaceSmall(cue, cueBefore, cueAfter);
                }
                else if (mode == Modes[8]) cancel.Cancel();
            }
            else if (point == NativeFreshSavedLevelResearchPoint.AfterFinalArtifactAttempt)
            { finals++; if (mode == Modes[7]) throw notificationFailure; }
            else throw new InvalidDataException("Unknown Engine-produced point.");
        };
        Exception? failure = null; NativeId65FreshDiscResearchPublication? result = null;
        try
        {
            result = await NativeFreshSavedLevelResearchCandidatePublisher.PublishForSmokeAsync(plan,
                root, fault, precommit, lockPreparation, observation, cancel.Token);
            publishReturned?.Invoke(); // Bookkeeping only AFTER actual API return.
        }
        catch (Exception error) { failure = error; }
        try
        {
            if (mode == Modes[2] || mode == Modes[3])
            {
                Check(failure != null && result == null && after == 0 && finals == 0, "No precommit return/final attempt.");
                Check(!Errors(failure!).Any(e => e is NativeFreshSavedLevelResearchCommittedException or NativeId65FreshResearchPublicationUnconfirmedException),
                    "No relabeled commit or uncertainty.");
                Check(Directory.Exists(root) && !Directory.EnumerateFileSystemEntries(root).Any(), "Owned staging cleaned.");
                if (mode == Modes[3]) Check(changedRaw != null && Errors(failure!).Any(e => e is InvalidDataException), "Actual stale version refusal.");
                return new("not-invoked", after, finals, failure, changedRaw, evidence.ToArray());
            }
            if (mode == Modes[4])
            {
                Check(failure is NativeId65FreshResearchPublicationUnconfirmedException && result == null && after == 0 && finals == 0,
                    "Real call escaped without acknowledgement, never upgraded by later observations.");
                var unknown = (NativeId65FreshResearchPublicationUnconfirmedException)failure!;
                Check(unknown.OutputDirectory == root && Directory.Exists(unknown.RetainedStageDirectory) &&
                    File.Exists(unknown.RetainedOutputLeasePath), "Actual unconfirmed owner evidence retained.");
                Check(ReadSmall(Path.Combine(root, ".export-publication.lock"), 32).SequenceEqual(
                    Encoding.ASCII.GetBytes("saved-level-unconfirmed-r01-lock")), "Real test lock untouched.");
                evidence.Add(unknown.RetainedStageDirectory); evidence.Add(unknown.RetainedOutputLeasePath);
                return new("unconfirmed", after, finals, failure, null, evidence.ToArray());
            }
            if (mode == Modes[8])
            {
                Check(failure == null && result != null && cancel.IsCancellationRequested && after == 1 && finals == 1,
                    "Cancellation after actual return cannot cancel guard/artifact/source finals.");
                RequireResult(plan, root, result!); _ = ReadComplete(plan, root);
                return new("committed-clean-after-cancel", after, finals, null, null, []);
            }
            Check(failure is NativeFreshSavedLevelResearchCommittedException && result == null && after == 1 && finals == 1,
                "Known commit, exactly one final artifact attempt, never retry.");
            var committed = (NativeFreshSavedLevelResearchCommittedException)failure!;
            Check(committed.OutputDirectory == root && Directory.Exists(committed.RetainedStageDirectory) &&
                File.Exists(committed.RetainedOutputLeasePath), "Known-commit evidence retained.");
            var leaves = Cases.Leaves(committed.InnerException!);
            if (mode == Modes[5])
            {
                Check(leaves.Count == 1 && committed.RetainedProjectLeasePath.Length != 0,
                    "Only actual guard release leaf: final artifact verification added no error.");
                Check(ReadSmall(marker, 32).SequenceEqual(laterOwner) && ReadSmall(heldMarker, 32).Length == 32,
                    "Old and subsequent marker evidence retained after close.");
            }
            else
            {
                Check(cueBefore != null && cueAfter != null && ReadSmall(Sibling(root, ".cue-before"), 65536).SequenceEqual(cueBefore) &&
                    ReadSmall(Path.Combine(root, NativeFreshSavedLevelResearchCandidatePlan.CueName), 65536).SequenceEqual(cueAfter),
                    "Original new CUE and changed CUE preserved; no repair/VerifySet retry.");
                Check(leaves.Count == (mode == Modes[7] ? 2 : 1) && leaves[0] is InvalidDataException,
                    "Actual companion readback failure is the first complete leaf.");
                if (mode == Modes[7]) Check(ReferenceEquals(leaves[1], notificationFailure), "Notification error follows actual readback once.");
            }
            evidence.Add(committed.RetainedStageDirectory); evidence.Add(committed.RetainedOutputLeasePath);
            return new("committed-with-failure", after, finals, failure, null, evidence.ToArray());
        }
        catch (Exception assertion)
        {
            if (failure != null) throw new IOException("Actual physical outcome plus failed gate assertion; retain evidence.",
                new AggregateException(failure, assertion));
            ExceptionDispatchInfo.Capture(assertion).Throw(); throw;
        }
    }
    private static string Sibling(string root, string suffix) =>
        Path.Combine(Directory.GetParent(root)!.FullName, Path.GetFileName(root) + suffix);
    private static void ReplaceSmall(string path, byte[] before, byte[] after)
    {
        Check(before.Length is > 0 and <= 65536 && after.Length is > 0 and <= 65536 &&
            ReadSmall(path, 65536).SequenceEqual(before), "Only exact bounded test-owned bytes may change.");
        FileStream? stream = null; List<Exception> errors = [];
        try
        {
            stream = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
            Check(stream.Length == before.Length, "Exact test-owner length before mutation.");
            stream.Write(after); stream.SetLength(after.Length); stream.Flush(true);
        }
        catch (Exception error) { errors.Add(error); }
        try { stream?.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count != 0) ExceptionDispatchInfo.Capture(Fold(errors)).Throw();
    }
}
