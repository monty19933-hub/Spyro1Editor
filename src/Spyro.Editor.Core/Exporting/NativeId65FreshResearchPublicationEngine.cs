using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

internal enum NativeId65FreshResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }

// Only actual sealed, source-issued fixed plans can bind the workflow. No
// caller-supplied filenames, hashes, callbacks, profile labels or serializers.
internal sealed class NativeId65FreshResearchPublicationBinding
{
    private readonly NativeId65FreshDiscResearchCandidatePlan? _starter;
    private readonly NativeId65FreshEditedDocumentResearchPlan? _edited;

    private NativeId65FreshResearchPublicationBinding(NativeId65FreshDiscResearchCandidatePlan plan) => _starter = plan;
    private NativeId65FreshResearchPublicationBinding(NativeId65FreshEditedDocumentResearchPlan plan) => _edited = plan;

    internal static NativeId65FreshResearchPublicationBinding FromStarter(NativeId65FreshDiscResearchCandidatePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(plan);
    }

    internal static NativeId65FreshResearchPublicationBinding FromEdited(NativeId65FreshEditedDocumentResearchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return new(plan);
    }

    internal string SourceImagePath => _starter != null ? _starter.SourceImagePath : _edited!.SourceImagePath;
    internal string SourceCuePath => _starter != null ? _starter.SourceCuePath : _edited!.SourceCuePath;
    internal string PlanSha256 => _starter != null ? _starter.PlanSha256 : _edited!.PlanSha256;
    internal string Label => _starter != null ? "Fresh LAB" : "Edited fresh LAB";
    internal string ImageName => _starter != null ? NativeId65FreshDiscResearchCandidatePlan.ImageName : NativeId65FreshEditedDocumentResearchPlan.ImageName;
    internal string CueName => _starter != null ? NativeId65FreshDiscResearchCandidatePlan.CueName : NativeId65FreshEditedDocumentResearchPlan.CueName;
    internal string ReceiptName => _starter != null ? NativeId65FreshDiscResearchCandidatePlan.ReceiptName : NativeId65FreshEditedDocumentResearchPlan.ReceiptName;
    internal string[] OutputNames => _starter != null ? NativeId65FreshDiscResearchCandidatePlan.OutputNames : NativeId65FreshEditedDocumentResearchPlan.OutputNames;
    internal string LeaseName => _starter != null ? ".native-id65-fresh.writer.lock" : ".native-id65-fresh-edited.writer.lock";
    internal string StagePrefix => _starter != null ? ".native-id65-fresh-stage-" : ".native-id65-fresh-edited-stage-";

    // This historical distinction belongs to the closed plan kind, not a caller flag.
    internal CancellationToken PostCommitToken(CancellationToken supplied) => _starter != null ? supplied : CancellationToken.None;
    internal Task RequireFreshAsync(CancellationToken token) => _starter != null ? _starter.RequireFreshAsync(token) : _edited!.RequireFreshAsync(token);
    internal byte[] CopyCueBytes() => _starter != null ? _starter.CopyCueBytes() : _edited!.CopyCueBytes();
    internal byte[] BuildReceipt(NativeId65FreshDiscVerification verified) => _starter != null ? _starter.BuildReceipt(verified) : _edited!.BuildReceipt(verified);
    internal NativeId65FreshDiscVerification VerifyImage(string path, CancellationToken token) => _starter != null ? _starter.VerifyImage(path, token) : _edited!.VerifyImage(path, token);
    internal void WriteNewImage(string path, CancellationToken token, Action firstSector)
    {
        if (_starter != null) _starter.WriteNewImage(path, token, firstSector);
        else _edited!.WriteNewImage(path, token, firstSector);
    }
}

// Fresh-only workflow: unlike Wizard publishing, occupied complete sets are
// refused, companions have bounded exact EOF checks, and cleanup contributes to
// the returned outcome. Existing serializers and physical writers stay in plans.
internal static class NativeId65FreshResearchPublicationEngine
{
    internal static Task<NativeId65FreshDiscResearchPublication> PublishAsync(
        NativeId65FreshResearchPublicationBinding plan, string directory,
        NativeId65FreshResearchFault fault,
        Action<NativeId65FreshResearchFault, string>? checkpoint, CancellationToken token) =>
        PublishCoreAsync(plan, directory, fault, checkpoint, token);

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

    private static async Task<NativeId65FreshDiscResearchPublication> PublishCoreAsync(NativeId65FreshResearchPublicationBinding plan,
        string outputDirectory, NativeId65FreshResearchFault fault,
        Action<NativeId65FreshResearchFault, string>? checkpoint, CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(fault)) throw new InvalidDataException($"Unknown {plan.Label} research fault.");
        token.ThrowIfCancellationRequested();
        string root = Resolve(plan, outputDirectory);
        RequireEmpty(plan, root); // Complete sets may only be separately verified.
        await plan.RequireFreshAsync(token);
        Directory.CreateDirectory(root);
        RequireNoLink(plan, root);
        string leasePath = Path.Combine(root, plan.LeaseName);
        RequireNoLink(plan, leasePath);
        using FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string stage = Path.Combine(root, plan.StagePrefix + Guid.NewGuid().ToString("N"));
        bool preserve = false, published = false;
        NativeId65FreshDiscResearchPublication? completed = null;
        try
        {
            RequireOnly(plan, root, [leasePath]);
            Directory.CreateDirectory(stage);
            string image = Path.Combine(stage, plan.ImageName);
            plan.WriteNewImage(image, token, () => Checkpoint(NativeId65FreshResearchFault.AfterFirstSector, image));
            Checkpoint(NativeId65FreshResearchFault.AfterImageWrite, image);
            var verified = plan.VerifyImage(image, token);
            WriteNew(Path.Combine(stage, plan.ReceiptName), plan.BuildReceipt(verified), token);
            WriteNew(Path.Combine(stage, plan.CueName), plan.CopyCueBytes(), token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException($"{plan.Label} staging readback changed.");

            // Callback BEFORE final guards; forced exception AFTER guards.
            // A missing freshness guard still cannot let fault tests publish.
            checkpoint?.Invoke(NativeId65FreshResearchFault.BeforePublication, image);
            token.ThrowIfCancellationRequested();
            await plan.RequireFreshAsync(token);
            if (VerifySet(plan, stage, token) != verified)
                throw new InvalidDataException($"{plan.Label} staging changed before publication.");
            RequireOnly(plan, root, [leasePath, stage]);
            RequireNoLink(plan, root);
            token.ThrowIfCancellationRequested();
            if (fault == NativeId65FreshResearchFault.BeforePublication)
                throw new IOException($"Deliberate {plan.Label} publication fault: BeforePublication");

            var publication = ExportArtifactTransaction.PublishNew(root,
                plan.OutputNames.Select(name => new ExportArtifactReplacement(
                    Path.Combine(root, name), Path.Combine(stage, name))).ToArray(), token);
            published = true;
            preserve = !string.IsNullOrEmpty(publication.RetainedRecoveryDirectory);
            // Own lease/empty stage and typed retained recovery can exist here.
            // Separate Verify permits only the exact three-artifact set.
            // Preserve the old starter policy exactly. The exact edited profile
            // completes final readback/freshness noncancellably after commit.
            CancellationToken finalToken = plan.PostCommitToken(token);
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
        catch (ExportArtifactRollbackException) { preserve = true; throw; }
        catch (Exception error) when (published)
        {
            preserve = true;
            throw new IOException($"{plan.Label} artifacts were published but final verification failed; preserve " + root, error);
        }
        finally
        {
            if (!preserve)
            {
                try
                {
                    RequireNoLink(plan, stage);
                    foreach (string name in plan.OutputNames)
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

        void Checkpoint(NativeId65FreshResearchFault point, string image)
        {
            checkpoint?.Invoke(point, image);
            token.ThrowIfCancellationRequested();
            if (fault == point) throw new IOException($"Deliberate {plan.Label} publication fault: " + point);
        }
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
