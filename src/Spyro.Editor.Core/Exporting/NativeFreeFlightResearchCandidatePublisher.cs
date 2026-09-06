using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeFreeFlightResearchPublication(string OutputDirectory, string ImagePath,
    string CuePath, string ReceiptPath, string ImageSha256, string PlanSha256, int ChangedRawSectors,
    string RetainedRecoveryDirectory = "")
{
    public bool WholeImageReadback => true;
    public bool RuntimeVerified => false;
    public bool NormalCreateBinApproved => false;
}

internal enum NativeFreeFlightResearchFault { None, AfterRelocation, AfterExecutable, BeforePublication }

/// <summary>
/// Explicit immutable research output only. Rebuilds from original source and
/// refuses occupied/partial destinations. It is not an ordinary editor exporter.
/// Source/output directories must be trusted; arbitrary hostile ancestor races
/// are not certified by leaf-link checks or cooperating-writer leases.
/// </summary>
public static class NativeFreeFlightResearchCandidatePublisher
{
    public static Task<NativeFreeFlightResearchPublication> PublishAsync(NativeFreeFlightResearchCandidatePlan plan,
        string outputDirectory, CancellationToken cancellationToken = default) =>
        PublishCoreAsync(plan, outputDirectory, NativeFreeFlightResearchFault.None, null, cancellationToken);

    internal static Task<NativeFreeFlightResearchPublication> PublishForSmokeAsync(NativeFreeFlightResearchCandidatePlan plan,
        string outputDirectory, NativeFreeFlightResearchFault fault, Action<NativeFreeFlightResearchFault, string>? checkpoint = null,
        CancellationToken cancellationToken = default) => PublishCoreAsync(plan, outputDirectory, fault, checkpoint, cancellationToken);

    public static async Task<NativeFreeFlightResearchPublication> VerifyAsync(NativeFreeFlightResearchCandidatePlan plan,
        string outputDirectory, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        string root = ResolveOutput(plan, outputDirectory);
        if (!IsCompleteSet(plan, root)) throw new InvalidDataException("Research candidate set is missing or incomplete.");
        await plan.RequireFreshAsync(cancellationToken);
        var result = VerifySet(plan, root);
        await plan.RequireFreshAsync(cancellationToken);
        return Result(plan, root, result);
    }

    private static async Task<NativeFreeFlightResearchPublication> PublishCoreAsync(NativeFreeFlightResearchCandidatePlan plan,
        string outputDirectory, NativeFreeFlightResearchFault fault, Action<NativeFreeFlightResearchFault, string>? checkpoint,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!Enum.IsDefined(fault)) throw new ArgumentException("Unknown research fault.", nameof(fault));
        string root = ResolveOutput(plan, outputDirectory);
        if (IsCompleteSet(plan, root)) return await VerifyAsync(plan, root, cancellationToken);
        await plan.RequireFreshAsync(cancellationToken);
        Directory.CreateDirectory(root);
        RequireNoLink(root);
        string leasePath = Path.Combine(root, ".free-flight-research.writer.lock");
        RequireNoLink(leasePath);
        using FileStream lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        string stage = Path.Combine(root, ".free-flight-research-stage-" + Guid.NewGuid().ToString("N"));
        string analysis = Path.Combine(stage, "source-wad-analysis.json");
        bool retainedRecovery = false, published = false;
        try
        {
            if (Directory.EnumerateFileSystemEntries(root).Any(p => p != leasePath))
                throw new IOException("Research output became occupied while acquiring its writer lease.");
            Directory.CreateDirectory(stage);
            WriteNew(analysis, plan.CopyAnalysisBytes());
            var payload = plan.CopyInsertionPayload();
            NativeSkyWadRelocationPlan relocation = NativeSkyWadRelocator.BuildPlan(plan.SourceImagePath, analysis, [payload]);
            plan.RequireRelocation(relocation);
            string imagePath = Path.Combine(stage, plan.OutputImageName);
            // Shared typed insertion/writer; the separately constructed complete
            // target and raw-sector oracle do not borrow this writer's output.
            NativeSkyWadRelocator.WriteExpandedImage(plan.SourceImagePath, imagePath, analysis, relocation, [payload]);
            Checkpoint(NativeFreeFlightResearchFault.AfterRelocation, imagePath);
            DiscLayout liveLayout = DiscImage.DetectLayout(imagePath);
            using (FileStream output = new(imagePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                DiscFileRecord liveExe = DiscImage.FindRootFileRecord(output, liveLayout, name => name == "SCUS_942.28");
                if (liveExe.Lba != plan.RelocatedExecutableLba || liveExe.Size != 0x66000)
                    throw new InvalidDataException("Research relocation did not install the expected live executable owner.");
                byte[] current = DiscImage.ReadFileBytes(output, liveLayout, liveExe.Lba, 0, liveExe.Size);
                byte[] changed = plan.ApplyToExecutable(current);
                if (!changed.AsSpan().SequenceEqual(plan.CopyExecutable()))
                    throw new InvalidDataException("Research executable differs from its independently bound byte plan.");
                DiscImage.WriteFileBytes(output, liveLayout, liveExe.Lba, 0, changed);
                output.Flush(flushToDisk: true);
            }
            Checkpoint(NativeFreeFlightResearchFault.AfterExecutable, imagePath);
            var verified = plan.VerifyImage(imagePath);
            WriteNew(Path.Combine(stage, NativeFreeFlightResearchCandidatePlan.ReceiptName), plan.BuildReceipt(verified));
            WriteNew(Path.Combine(stage, plan.OutputCueName), plan.CopyCueBytes());
            _ = VerifySet(plan, stage);
            Checkpoint(NativeFreeFlightResearchFault.BeforePublication, imagePath);
            await plan.RequireFreshAsync(cancellationToken);
            if (VerifySet(plan, stage) != verified)
                throw new InvalidDataException("Research staging changed before publication.");
            ExportArtifactPublicationResult publication = ExportArtifactTransaction.PublishNew(root,
                plan.OutputNames.Select(name => new ExportArtifactReplacement(Path.Combine(root, name), Path.Combine(stage, name))).ToArray(),
                cancellationToken);
            published = true;
            retainedRecovery = !string.IsNullOrEmpty(publication.RetainedRecoveryDirectory);
            var final = VerifySet(plan, root);
            if (final != verified) throw new InvalidDataException("Research final image differs from staging.");
            await plan.RequireFreshAsync(cancellationToken);
            return Result(plan, root, final, publication.RetainedRecoveryDirectory);
        }
        catch (ExportArtifactRollbackException)
        {
            retainedRecovery = true;
            throw;
        }
        catch (Exception error) when (published)
        {
            retainedRecovery = true;
            throw new IOException("Research artifacts were published but final verification failed. Preserve " + root, error);
        }
        finally
        {
            if (!retainedRecovery)
            {
                // Never enumerate/delete another owner's stage or recovery data.
                foreach (string name in plan.OutputNames.Append("source-wad-analysis.json"))
                {
                    string path = Path.Combine(stage, name);
                    if (File.Exists(path)) File.Delete(path);
                }
                if (Directory.Exists(stage) && !Directory.EnumerateFileSystemEntries(stage).Any()) Directory.Delete(stage);
            }
            lease.Dispose();
            if (!retainedRecovery && File.Exists(leasePath)) File.Delete(leasePath);
        }

        void Checkpoint(NativeFreeFlightResearchFault point, string imagePath)
        {
            checkpoint?.Invoke(point, imagePath);
            cancellationToken.ThrowIfCancellationRequested();
            if (fault == point) throw new IOException("Deliberate research publication fault: " + point);
        }
    }

    private static (string Hash, int Changed) VerifySet(NativeFreeFlightResearchCandidatePlan plan, string directory)
    {
        foreach (string name in plan.OutputNames) RequireNoLink(Path.Combine(directory, name));
        var result = plan.VerifyImage(Path.Combine(directory, plan.OutputImageName));
        if (!File.ReadAllBytes(Path.Combine(directory, NativeFreeFlightResearchCandidatePlan.ReceiptName))
                .AsSpan().SequenceEqual(plan.BuildReceipt(result)) ||
            !File.ReadAllBytes(Path.Combine(directory, plan.OutputCueName))
                .AsSpan().SequenceEqual(plan.CopyCueBytes()) ||
            !DiscCueSheet.ReferencesDataImage(Path.Combine(directory, plan.OutputCueName),
                Path.Combine(directory, plan.OutputImageName)))
            throw new InvalidDataException("Research CUE/receipt failed exact companion binding.");
        return result;
    }

    private static NativeFreeFlightResearchPublication Result(NativeFreeFlightResearchCandidatePlan plan,
        string root, (string Hash, int Changed) result, string retainedRecovery = "") => new(root,
            Path.Combine(root, plan.OutputImageName),
            Path.Combine(root, plan.OutputCueName),
            Path.Combine(root, NativeFreeFlightResearchCandidatePlan.ReceiptName), result.Hash, plan.PlanSha256, result.Changed,
            retainedRecovery);

    private static string ResolveOutput(NativeFreeFlightResearchCandidatePlan plan, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.GetFullPath(directory);
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath, root, "Research output overlaps source BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath, root, "Research output overlaps source CUE.");
        RequireNoLink(root);
        if (File.Exists(root)) throw new InvalidDataException("Research output directory is a file.");
        return root;
    }

    private static bool IsCompleteSet(NativeFreeFlightResearchCandidatePlan plan, string root)
    {
        if (!Directory.Exists(root)) return false;
        string[] names = Directory.EnumerateFileSystemEntries(root).Select(p => Path.GetFileName(p)).Order(StringComparer.Ordinal).ToArray();
        if (names.Length == 0) return false;
        if (!names.SequenceEqual(plan.OutputNames.Order(StringComparer.Ordinal)))
            throw new IOException("Research output is partial, occupied or retains recovery evidence; it will not be replaced.");
        foreach (string name in plan.OutputNames)
        {
            string path = Path.Combine(root, name);
            RequireNoLink(path);
            if (!File.Exists(path)) throw new InvalidDataException("Research artifact is not a file.");
        }
        return true;
    }

    private static void RequireNoLink(string path)
    {
        if (new FileInfo(path).LinkTarget != null || new DirectoryInfo(path).LinkTarget != null)
            throw new InvalidDataException("Research output artifact is a symbolic link; trusted ancestor directories are required.");
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes);
        stream.Flush(flushToDisk: true);
    }
}
