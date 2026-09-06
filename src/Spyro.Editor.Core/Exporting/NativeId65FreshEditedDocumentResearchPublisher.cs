using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeId65FreshEditedDocumentResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }

/// <summary>Exact new-only research artifacts. Trusted ancestors and cooperating
/// writers are required; this does not claim crash atomicity or native acceptance.</summary>
public static class NativeId65FreshEditedDocumentResearchPublisher
{
    public static Task<NativeId65FreshDiscResearchPublication> PublishAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, CancellationToken token = default) =>
        PublishCoreAsync(plan, outputDirectory, NativeId65FreshEditedDocumentResearchFault.None, null, token);

    internal static Task<NativeId65FreshDiscResearchPublication> PublishForSmokeAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, NativeId65FreshEditedDocumentResearchFault fault,
        Action<NativeId65FreshEditedDocumentResearchFault, string>? checkpoint = null, CancellationToken token = default)
    {
        if (fault == NativeId65FreshEditedDocumentResearchFault.None)
            throw new InvalidDataException("A staging exercise must force a precommit fault.");
        return PublishCoreAsync(plan, outputDirectory, fault, checkpoint, token);
    }

    public static async Task<NativeId65FreshDiscResearchPublication> VerifyAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        token.ThrowIfCancellationRequested();
        string root = Resolve(plan, outputDirectory);
        RequireCompleteSet(root);
        await plan.RequireFreshAsync(token);
        var result = VerifySet(plan, root, token);
        await plan.RequireFreshAsync(token);
        return Result(plan, root, result);
    }

    private static async Task<NativeId65FreshDiscResearchPublication> PublishCoreAsync(NativeId65FreshEditedDocumentResearchPlan plan,
        string outputDirectory, NativeId65FreshEditedDocumentResearchFault fault,
        Action<NativeId65FreshEditedDocumentResearchFault, string>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(fault)) throw new InvalidDataException("Unknown Edited fresh LAB research fault.");
        token.ThrowIfCancellationRequested();
        string root = Resolve(plan, outputDirectory);
        RequireEmpty(root); // Complete sets may only be separately verified.
        await plan.RequireFreshAsync(token);
        Directory.CreateDirectory(root);
        RequireNoLink(root);
        string leasePath = Path.Combine(root, ".native-id65-fresh-edited.writer.lock");
        RequireNoLink(leasePath);
        using FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string stage = Path.Combine(root, ".native-id65-fresh-edited-stage-" + Guid.NewGuid().ToString("N"));
        bool preserve = false, published = false;
        NativeId65FreshDiscResearchPublication? completed = null;
        try
        {
            RequireOnly(root, [leasePath]);
            Directory.CreateDirectory(stage);
            string image = Path.Combine(stage, NativeId65FreshEditedDocumentResearchPlan.ImageName);
            plan.WriteNewImage(image, token, () => Checkpoint(NativeId65FreshEditedDocumentResearchFault.AfterFirstSector, image));
            Checkpoint(NativeId65FreshEditedDocumentResearchFault.AfterImageWrite, image);
            var verified = plan.VerifyImage(image, token);
            WriteNew(Path.Combine(stage, NativeId65FreshEditedDocumentResearchPlan.ReceiptName), plan.BuildReceipt(verified), token);
            WriteNew(Path.Combine(stage, NativeId65FreshEditedDocumentResearchPlan.CueName), plan.CopyCueBytes(), token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException("Edited fresh LAB staging readback changed.");

            // Callback BEFORE final guards; forced exception AFTER guards.
            // A missing freshness guard still cannot let fault tests publish.
            checkpoint?.Invoke(NativeId65FreshEditedDocumentResearchFault.BeforePublication, image);
            token.ThrowIfCancellationRequested();
            await plan.RequireFreshAsync(token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException("Edited fresh LAB staging changed before publication.");
            RequireOnly(root, [leasePath, stage]);
            RequireNoLink(root);
            token.ThrowIfCancellationRequested();
            if (fault == NativeId65FreshEditedDocumentResearchFault.BeforePublication)
                throw new IOException("Deliberate Edited fresh LAB publication fault: BeforePublication");

            var publication = ExportArtifactTransaction.PublishNew(root,
                NativeId65FreshEditedDocumentResearchPlan.OutputNames.Select(name => new ExportArtifactReplacement(
                    Path.Combine(root, name), Path.Combine(stage, name))).ToArray(), token);
            published = true;
            preserve = !string.IsNullOrEmpty(publication.RetainedRecoveryDirectory);
            // Publication has committed. A late token must not turn success into
            // an apparent abort: BOTH final readback and source freshness finish
            // noncancellably. Actual failures report the committed artifact state.
            // Own lease/empty stage and typed retained recovery can exist here.
            // Separate Verify permits only the exact three-artifact set.
            var final = VerifySet(plan, root, CancellationToken.None, requireOnlyArtifacts: false);
            if (final != verified) throw new InvalidDataException("Edited fresh LAB final output differs from verified staging.");
            await plan.RequireFreshAsync(CancellationToken.None);
            List<string> allowed = NativeId65FreshEditedDocumentResearchPlan.OutputNames
                .Select(name => Path.Combine(root, name)).Concat([leasePath, stage]).ToList();
            if (preserve)
            {
                allowed.Add(publication.RetainedRecoveryDirectory);
                string transactionLock = Path.Combine(root, ".export-publication.lock");
                if (File.Exists(transactionLock)) allowed.Add(transactionLock);
            }
            RequireOnly(root, allowed.ToArray());
            completed = Result(plan, root, final, publication.RetainedRecoveryDirectory);
        }
        catch (ExportArtifactRollbackException) { preserve = true; throw; }
        catch (Exception error) when (published)
        {
            preserve = true;
            throw new IOException("Edited fresh LAB artifacts were published but final verification failed; preserve " + root, error);
        }
        finally
        {
            if (!preserve)
            {
                try
                {
                    RequireNoLink(stage);
                    foreach (string name in NativeId65FreshEditedDocumentResearchPlan.OutputNames)
                    {
                        string path = Path.Combine(stage, name);
                        if (File.Exists(path) && new FileInfo(path).LinkTarget == null) File.Delete(path);
                    }
                    if (Directory.Exists(stage))
                    {
                        if (!Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
                        else preserve = true; // Unknown owners remain with our lease for explicit recovery.
                    }
                }
                catch (IOException) { preserve = true; }
                catch (InvalidDataException) { preserve = true; }
                catch (UnauthorizedAccessException) { preserve = true; }
            }
            lease.Dispose();
            if (!preserve)
            {
                try { if (File.Exists(leasePath)) File.Delete(leasePath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        // Cleanup warnings must remain discoverable, even if our own stage or
        // lease (rather than the transaction recovery journal) was retained.
        return completed! with
        {
            RetainedRecoveryDirectory = !string.IsNullOrEmpty(completed!.RetainedRecoveryDirectory)
                ? completed.RetainedRecoveryDirectory
                : Directory.Exists(stage) ? stage : File.Exists(leasePath) ? root : ""
        };

        void Checkpoint(NativeId65FreshEditedDocumentResearchFault point, string image)
        {
            checkpoint?.Invoke(point, image);
            token.ThrowIfCancellationRequested();
            if (fault == point) throw new IOException("Deliberate Edited fresh LAB publication fault: " + point);
        }
    }

    private static NativeId65FreshDiscVerification VerifySet(NativeId65FreshEditedDocumentResearchPlan plan,
        string root, CancellationToken token, bool requireOnlyArtifacts = true)
    {
        if (requireOnlyArtifacts) RequireCompleteSet(root);
        foreach (string name in NativeId65FreshEditedDocumentResearchPlan.OutputNames)
        {
            string path = Path.Combine(root, name);
            RequireNoLink(path);
            if (!File.Exists(path) || Directory.Exists(path)) throw new InvalidDataException("Edited fresh LAB artifact is not a file.");
        }
        var result = plan.VerifyImage(Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.ImageName), token);
        byte[] cue = plan.CopyCueBytes();
        RequireCompanionFile(Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.CueName), cue, token);
        RequireCompanionFile(Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.ReceiptName), plan.BuildReceipt(result), token);
        if (!DiscCueSheet.ReferencesDataImageSnapshot(cue, Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.CueName),
            Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.ImageName)))
            throw new InvalidDataException("Edited fresh LAB output CUE binding changed.");
        if (requireOnlyArtifacts) RequireCompleteSet(root);
        return result;
    }

    private static void RequireCompanionFile(string path, byte[] expected, CancellationToken token)
    {
        using FileStream input = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        NativeId65FreshDiscResearchCandidatePublisher.RequireCompanion(input, expected, token);
    }

    private static string Resolve(NativeId65FreshEditedDocumentResearchPlan plan, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath, root, "Edited fresh LAB output overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath, root, "Edited fresh LAB output overlaps source CUE.");
        RequireNoLink(root);
        if (File.Exists(root)) throw new InvalidDataException("Edited fresh LAB output directory is a file.");
        return root;
    }

    private static void RequireEmpty(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException("Edited fresh LAB output is occupied; Publish never replaces or implicitly reuses it.");
    }

    private static void RequireCompleteSet(string root)
    {
        RequireNoLink(root);
        if (!Directory.Exists(root)) throw new InvalidDataException("Edited fresh LAB complete artifact set is missing.");
        string[] expected = NativeId65FreshEditedDocumentResearchPlan.OutputNames.Select(name => Path.Combine(root, name)).ToArray();
        RequireOnly(root, expected);
    }

    private static void RequireOnly(string root, string[] expected)
    {
        if (!Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new IOException("Edited fresh LAB output is incomplete, occupied or retains recovery evidence.");
    }

    private static void RequireNoLink(string path)
    {
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("Edited fresh LAB output may not be a symbolic link; trusted ancestors are required.");
    }

    private static void WriteNew(string path, byte[] bytes, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes);
        output.Flush(flushToDisk: true);
        token.ThrowIfCancellationRequested();
    }

    private static NativeId65FreshDiscResearchPublication Result(NativeId65FreshEditedDocumentResearchPlan plan,
        string root, NativeId65FreshDiscVerification result, string recovery = "") => new(root,
        Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.ImageName),
        Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.CueName),
        Path.Combine(root, NativeId65FreshEditedDocumentResearchPlan.ReceiptName),
        result.OutputSha256, plan.PlanSha256, result.ChangedSectors, recovery);
}
