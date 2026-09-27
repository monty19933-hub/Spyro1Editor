using System.Security.Cryptography;
using Spyro.Editor.Core.Persistence;

namespace Spyro.Editor.Core.Exporting;

public sealed record NativeCollectibleStartupResearchPublication(string OutputDirectory, string ImagePath,
    string CuePath, string ReceiptPath, string ImageSha256, string PlanSha256, int ChangedRawSectors,
    string RetainedRecoveryDirectory)
{
    public bool ResearchOnly => true;
    public bool NativeInstallationApproved => false;
    public bool RuntimeVerified => false;
}

/// <summary>Actual transaction state, never inferred from artifact contents.</summary>
public enum NativeCollectibleStartupResearchOutcome { NotInvoked, RollbackIncomplete, Unconfirmed, Committed }

/// <summary>All evidence remains for inspection; failure never grants automatic retry/cleanup permission.</summary>
public sealed class NativeCollectibleStartupResearchFailure : IOException
{
    internal NativeCollectibleStartupResearchFailure(string root, string stage, string lease,
        NativeFreshResearchPublicationState state, Exception failure)
        : base($"Startup research publication failed ({state}); preserve evidence in {root}.", failure)
    {
        OutputDirectory = root; RetainedStageDirectory = stage; RetainedLeasePath = lease;
        Outcome = state switch
        {
            NativeFreshResearchPublicationState.NotInvoked => NativeCollectibleStartupResearchOutcome.NotInvoked,
            NativeFreshResearchPublicationState.RollbackIncomplete => NativeCollectibleStartupResearchOutcome.RollbackIncomplete,
            NativeFreshResearchPublicationState.Unconfirmed => NativeCollectibleStartupResearchOutcome.Unconfirmed,
            NativeFreshResearchPublicationState.Committed => NativeCollectibleStartupResearchOutcome.Committed,
            _ => throw new InvalidOperationException("Unknown actual transaction outcome.")
        };
    }
    public string OutputDirectory { get; }
    public string RetainedStageDirectory { get; }
    public string RetainedLeasePath { get; }
    public NativeCollectibleStartupResearchOutcome Outcome { get; }
}

internal enum NativeCollectibleStartupResearchFault { None, AfterFirstSector, AfterImageWrite, BeforePublication }

/// <summary>
/// Opt-in source-bound research publication, not an editor/export installation
/// path. New-only transaction, CUE last, whole raw-image readback and exact
/// companions. Trusted output ancestors required; no crash/race safety claim.
/// </summary>
public static class NativeCollectibleStartupResearchPublisher
{
    private const string LeaseName = ".collectible-startup-research.lease";
    public static NativeCollectibleStartupResearchPublication Publish(NativeCollectibleStartupResearchPlan plan,
        string newDirectory, CancellationToken token = default) => PublishCore(plan, newDirectory, token);

    public static NativeCollectibleStartupResearchPublication Verify(NativeCollectibleStartupResearchPlan plan,
        string directory, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(plan); token.ThrowIfCancellationRequested();
        string root = Resolve(plan, directory); plan.RequireFresh();
        try
        {
            RequireOnly(root, NativeCollectibleStartupResearchPlan.OutputNames.Select(n => Path.Combine(root, n)).ToArray());
            VerifySet(plan, root); token.ThrowIfCancellationRequested(); return Result(plan, root, "");
        }
        finally { plan.RequireFresh(); }
    }

    // An opt-in staging exercise is forced to stop before the transaction. It
    // accepts observations/mutations only, never source bytes or injected IO.
    internal static NativeCollectibleStartupResearchPublication ExercisePrecommitFailure(
        NativeCollectibleStartupResearchPlan plan, string directory, NativeCollectibleStartupResearchFault fault,
        Action<string>? checkpoint = null)
    {
        if (fault == NativeCollectibleStartupResearchFault.None || !Enum.IsDefined(fault))
            throw new InvalidDataException("Staging failure exercise requires a known forced precommit fault.");
        return PublishCore(plan, directory, default, fault, checkpoint);
    }

    // This seam installs an ACTUAL transaction lock after final guards. Only
    // the unchanged PublishNew may determine the unconfirmed call outcome.
    internal static NativeCollectibleStartupResearchPublication ExerciseTransactionCollision(
        NativeCollectibleStartupResearchPlan plan, string directory) =>
        PublishCore(plan, directory, default, blockTransaction: true);

    private static NativeCollectibleStartupResearchPublication PublishCore(NativeCollectibleStartupResearchPlan plan,
        string directory, CancellationToken token, NativeCollectibleStartupResearchFault fault = NativeCollectibleStartupResearchFault.None,
        Action<string>? checkpoint = null, bool blockTransaction = false)
    {
        ArgumentNullException.ThrowIfNull(plan); token.ThrowIfCancellationRequested();
        string root = Resolve(plan, directory);
        RequireEmpty(root); plan.RequireFresh();
        string stage = Path.Combine(root, ".collectible-startup-stage-" + Guid.NewGuid().ToString("N"));
        string leasePath = Path.Combine(root, LeaseName);
        var attempt = new NativeFreshResearchPublicationAttempt();
        FileStream? lease = null; byte[] leaseOwner = RandomNumberGenerator.GetBytes(32);
        string recovery = "";
        try
        {
            Directory.CreateDirectory(root); RequireNoLinks(root); RequireEmpty(root);
            lease = new(leasePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            lease.Write(leaseOwner); lease.Flush(true); RequireOnly(root, [leasePath]);
            Directory.CreateDirectory(stage);
            string image = Path.Combine(stage, NativeCollectibleStartupResearchPlan.ImageName);
            plan.WriteNewImage(image, token, () =>
            {
                if (fault != NativeCollectibleStartupResearchFault.AfterFirstSector) return;
                checkpoint?.Invoke(image); plan.RequireFresh(); ThrowForced(fault);
            });
            if (fault == NativeCollectibleStartupResearchFault.AfterImageWrite) checkpoint?.Invoke(image);
            plan.VerifyImage(image); plan.RequireFresh();
            if (fault == NativeCollectibleStartupResearchFault.AfterImageWrite) ThrowForced(fault);
            WriteNew(Path.Combine(stage, NativeCollectibleStartupResearchPlan.ReceiptName), plan.BuildReceipt());
            WriteNew(Path.Combine(stage, NativeCollectibleStartupResearchPlan.CueName), plan.CopyCueBytes());
            if (fault == NativeCollectibleStartupResearchFault.BeforePublication) checkpoint?.Invoke(image);
            VerifySet(plan, stage); plan.RequireFresh();
            RequireOnly(root, [leasePath, stage]); RequireNoLinks(root); token.ThrowIfCancellationRequested();
            if (fault == NativeCollectibleStartupResearchFault.BeforePublication) ThrowForced(fault);
            if (blockTransaction) WriteNew(Path.Combine(root, ".export-publication.lock"), "startup-research-collision-test"u8.ToArray());
            var publication = attempt.Invoke(root, NativeCollectibleStartupResearchPlan.OutputNames.Select(name =>
                new ExportArtifactReplacement(Path.Combine(root, name), Path.Combine(stage, name))).ToArray(), token);
            recovery = publication.RetainedRecoveryDirectory;
            VerifySet(plan, root); // Any failure after returned transaction stays Committed.
            RequireOnly(root, [leasePath, stage, .. NativeCollectibleStartupResearchPlan.OutputNames.Select(n => Path.Combine(root, n)),
                .. recovery.Length == 0 ? Array.Empty<string>() : new[] { recovery }]);
        }
        catch (Exception failure) { attempt.RecordFailure(failure); }
        finally
        {
            // Final source checks apply to every admitted failure, even partial
            // writes, cancellation, actual transaction throws and postcommit.
            attempt.Capture(plan.RequireFresh);
            if (lease != null) attempt.Capture(lease.Dispose);
        }
        if (attempt.Failure == null && attempt.State == NativeFreshResearchPublicationState.Committed)
        {
            // Never recursively erase stages. Failure evidence is retained; on
            // success only our empty stage and exact owned lease may be removed.
            attempt.Capture(() => Directory.Delete(stage, recursive: false));
            if (attempt.Failure == null) attempt.Capture(() =>
            {
                RequireExactFile(leasePath, leaseOwner); File.Delete(leasePath);
            });
        }
        if (attempt.Failure is { } error)
            throw new NativeCollectibleStartupResearchFailure(root, stage, leasePath, attempt.State, error);
        return Result(plan, root, recovery);
    }

    private static void VerifySet(NativeCollectibleStartupResearchPlan plan, string root)
    {
        RequireNoLinks(root);
        foreach (string name in NativeCollectibleStartupResearchPlan.OutputNames) RequireNoLinks(Path.Combine(root, name));
        plan.VerifyImage(Path.Combine(root, NativeCollectibleStartupResearchPlan.ImageName));
        RequireExactFile(Path.Combine(root, NativeCollectibleStartupResearchPlan.CueName), plan.CopyCueBytes());
        RequireExactFile(Path.Combine(root, NativeCollectibleStartupResearchPlan.ReceiptName), plan.BuildReceipt());
    }
    internal static void RequireExactFile(string path, ReadOnlySpan<byte> expected)
    {
        RequireNoLinks(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length != expected.Length) throw new InvalidDataException("Research companion exact size changed.");
        byte[] bytes = new byte[expected.Length]; input.ReadExactly(bytes);
        if (!bytes.AsSpan().SequenceEqual(expected) || input.Length != expected.Length || input.Position != expected.Length ||
            input.ReadByte() != -1 || input.Length != expected.Length || input.Position != expected.Length)
            throw new InvalidDataException("Research companion exact bytes or EOF changed.");
    }
    private static string Resolve(NativeCollectibleStartupResearchPlan plan, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        string root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceImagePath, root, "Research output overlaps original BIN.");
        PersistencePathSafety.EnsureTreesDoNotOverlap(plan.SourceCuePath, root, "Research output overlaps original CUE.");
        ExportArtifactTransaction.RequireSourceSeparation(plan.SourceImagePath, plan.SourceCuePath,
            NativeCollectibleStartupResearchPlan.OutputNames.Select(n => Path.Combine(root, n)).ToArray());
        RequireNoLinks(root);
        if (File.Exists(root)) throw new InvalidDataException("Research output directory is a file.");
        return root;
    }
    private static void RequireNoLinks(string path)
    {
        for (string? current = Path.GetFullPath(path); current != null; current = Path.GetDirectoryName(current))
            if (new FileInfo(current).LinkTarget != null || new DirectoryInfo(current).LinkTarget != null)
                throw new InvalidDataException("Research output refuses symbolic links in the target and its ancestors.");
    }
    private static void RequireEmpty(string root)
    {
        if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
            throw new IOException("Research publication refuses occupied output; use explicit Verify on a complete set.");
    }
    private static void RequireOnly(string root, string[] expected)
    {
        if (!Directory.Exists(root) || !Directory.EnumerateFileSystemEntries(root).Order(StringComparer.Ordinal)
            .SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new IOException("Research directory is incomplete, occupied or retains failure evidence.");
    }
    private static void WriteNew(string path, byte[] bytes)
    {
        using var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        output.Write(bytes); output.Flush(true);
    }
    private static void ThrowForced(NativeCollectibleStartupResearchFault fault) =>
        throw new IOException("Deliberate startup research precommit fault: " + fault);
    private static NativeCollectibleStartupResearchPublication Result(NativeCollectibleStartupResearchPlan plan, string root, string recovery) =>
        new(root, Path.Combine(root, NativeCollectibleStartupResearchPlan.ImageName), Path.Combine(root, NativeCollectibleStartupResearchPlan.CueName),
            Path.Combine(root, NativeCollectibleStartupResearchPlan.ReceiptName), plan.ExpectedImageSha256, plan.PlanSha256, 2, recovery);
}
