using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record Id65CollisionCandidateReadback(string ParentSha256, string OutputSha256,
    int OwnedRawSectors, int ChangedRawSectors, long ChangedBytes);
internal sealed record Id65CollisionCandidateResult(string Directory, string Image, string Cue,
    string Receipt, string PlanSha256, Id65CollisionCandidateReadback Readback, string RetainedRecoveryDirectory);

/// <summary>
/// Guarded candidate only, not normal Create BIN. Replays the exact existing
/// collapsed-sky substrate and retires collision lookup references. All other
/// physical bytes must remain equal to the separately certified parent image.
/// Requires a trusted exclusive local output directory; no hostile-filesystem
/// or power-loss atomicity assertion is made.
/// </summary>
internal static class Id65CollisionCandidatePublisher
{
    public const string ProfileId = "id65-collapsed-sky-isolated-collision-candidate-v1";
    public const string ParentImageSha256 = "65d70f5e91ed049a7a48d117a2e30223b77f37853a9a961120acb660c8cbc232";
    public const string ImageFileName = "id65-collision-isolated-candidate.bin";
    public const string CueFileName = "id65-collision-isolated-candidate.cue";
    private const string InputFileName = "authoring-input.json";
    private const string ReceiptFileName = "candidate.receipt.json";
    private const int RawBytes = 2352, LogicalBytes = 2048;
    private const long ImageBytes = 661_547_040;
    private const int WadLba = 37;
    private const long RowWadOffset = Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher.TargetDataWadOffset;

    public static Id65CompiledAuthoringCollisionIsolation Compile(string lockedFixture)
    {
        var source = Id65AuthoringEditorAggregateBoundSource.Capture(lockedFixture).LockedSource;
        var v3 = UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.BuildStaticPlan(lockedFixture);
        var texture = Id65V2NativeTextureCompositionCompiler.DerivePinnedT66WitnessFromLockedBase(lockedFixture);
        var grass = Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundleFromLockedBase(lockedFixture);
        var document = Id65AuthoringEditorDocument.CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107, Id65RemotePadGrassT107Catalog.CenterPositionId,
            [Id65AuthoringTerrainSetCatalog.Tile1Id]);
        return Id65AuthoringCollisionIsolationCompiler.Compile(new(document, source, v3, texture, grass), collapsedCyclorama: true);
    }

    public static Id65CollisionCandidateResult Publish(string lockedFixture, string parentImage,
        string parentCue, string outputRoot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Id65CompiledAuthoringCollisionIsolation compiled = Compile(lockedFixture);
        RequireBoundary(compiled);
        parentImage = Path.GetFullPath(parentImage); parentCue = Path.GetFullPath(parentCue);
        using FileStream source = new(parentImage, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != ImageBytes || HashStream(source) != ParentImageSha256)
            throw new InvalidDataException("Collision publication requires the exact complete collapsed-sky parent image.");
        byte[] cue = DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(parentCue, parentImage, ImageFileName));
        if (!File.Exists(parentCue) || !DiscCueSheet.ReferencesDataImage(parentCue, parentImage))
            throw new InvalidDataException("The exact parent candidate requires its existing matching CUE.");
        DiscLayout layout = DiscImage.DetectLayout(parentImage);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(source, layout, name => name == "WAD.WAD");
        if (layout.SectorSize != RawBytes || layout.UserOffset != 24 || wad.Lba != WadLba ||
            RowWadOffset + compiled.CopyBaselineRow80().Length > wad.Size ||
            !DiscImage.ReadFileBytes(source, layout, wad.Lba, RowWadOffset, compiled.CopyBaselineRow80().Length)
                .SequenceEqual(compiled.CopyBaselineRow80()))
            throw new InvalidDataException("The certified parent does not contain the complete exact collapsed collision preimage.");

        outputRoot = Path.GetFullPath(outputRoot);
        string directory = Path.Combine(outputRoot, "collision-" + compiled.DeterministicPlanSha256);
        RequireSafeOutputPath(directory, parentImage, parentCue, Path.GetFullPath(lockedFixture));
        if (Directory.Exists(directory) || File.Exists(directory))
            throw new IOException("This immutable collision candidate already exists or has retained residue; verify it or choose a new output root.");
        Directory.CreateDirectory(directory);
        string stage = Path.Combine(directory, ".stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        string stagedImage = Path.Combine(stage, ImageFileName);
        bool published = false;
        string retainedRecovery = "";
        try
        {
            using (FileStream destination = new(stagedImage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                source.Position = 0; source.CopyTo(destination);
                foreach (var range in compiled.OwnedRanges)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    byte[] before = range.CopyBefore();
                    if (!DiscImage.ReadFileBytes(destination, layout, wad.Lba,
                            RowWadOffset + range.DataRelativeOffset, before.Length).SequenceEqual(before))
                        throw new InvalidDataException("Collision staging lost a complete native patch preimage.");
                    DiscImage.WriteFileBytes(destination, layout, wad.Lba,
                        RowWadOffset + range.DataRelativeOffset, range.CopyAfter());
                }
                destination.Flush(flushToDisk: true);
            }
            Id65CollisionCandidateReadback readback;
            using (FileStream candidate = File.OpenRead(stagedImage))
                readback = Verify(compiled, source, candidate, cancellationToken);
            byte[] input = compiled.Document.SerializeUtf8();
            byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
            {
                format = ProfileId, schemaVersion = 1,
                compilerProfile = Id65AuthoringCollisionIsolationCompiler.ProfileId,
                planSha256 = compiled.DeterministicPlanSha256,
                inputSha256 = Hash(input), parentImageSha256 = ParentImageSha256,
                outputImageSha256 = readback.OutputSha256,
                baselineRow80Sha256 = compiled.BaselineRow80Sha256,
                outputRow80Sha256 = compiled.OutputRow80Sha256,
                retainedTriangles = compiled.RetainedTriangleIndexes,
                readback, collision = compiled.Readback,
                imageFileName = ImageFileName, cueFileName = CueFileName, cueSha256 = Hash(cue),
                nativeTriangleTableRetained = true, inheritedLookupCellsRetired = true,
                physicalComplementVerified = true, exactInverseVerified = true,
                runtimeProven = false, jumpProven = false, savePersistenceProven = false,
                appIntegrated = false, normalCreateBinEnabled = false,
                requiresTrustedExclusiveDirectory = true, hostileFilesystemRaceSafe = false
            });
            WriteNew(Path.Combine(stage, InputFileName), input);
            WriteNew(Path.Combine(stage, ReceiptFileName), receipt);
            WriteNew(Path.Combine(stage, CueFileName), cue);
            // Shared exception-rollback publication; the launchable CUE is last.
            string[] names = [ImageFileName, InputFileName, ReceiptFileName, CueFileName];
            ExportArtifactPublicationResult publication = ExportArtifactTransaction.PublishNew(directory, names.Select(name =>
                new ExportArtifactReplacement(Path.Combine(directory, name), Path.Combine(stage, name))).ToArray(), cancellationToken);
            published = true;
            retainedRecovery = publication.RetainedRecoveryDirectory;
            // A committed candidate is retained if later verification fails.
            using (FileStream candidate = File.OpenRead(Path.Combine(directory, ImageFileName)))
                if (Verify(compiled, source, candidate, CancellationToken.None) != readback)
                    throw new InvalidDataException("Published collision candidate failed exact final physical readback.");
            foreach (var (name, expected) in new[] { (InputFileName, input), (ReceiptFileName, receipt), (CueFileName, cue) })
                if (!File.ReadAllBytes(Path.Combine(directory, name)).SequenceEqual(expected))
                    throw new InvalidDataException("Published collision companion bytes changed.");
            if (!HasRecoveryEvidence(directory)) CleanupOwnedStage(stage);
            if (Directory.Exists(stage) && retainedRecovery.Length == 0) retainedRecovery = stage;
            return new(directory, Path.Combine(directory, ImageFileName), Path.Combine(directory, CueFileName),
                Path.Combine(directory, ReceiptFileName), compiled.DeterministicPlanSha256, readback, retainedRecovery);
        }
        catch (Exception exception) when (published)
        {
            throw new IOException("Collision candidate was published and retained, but final verification failed. Inspect: " + directory, exception);
        }
        finally
        {
            // Never remove transaction recovery data or a published artifact.
            if (!HasRecoveryEvidence(directory)) CleanupOwnedStage(stage);
        }
    }

    public static Id65CollisionCandidateReadback Verify(Id65CompiledAuthoringCollisionIsolation compiled,
        Stream parent, Stream candidate, CancellationToken cancellationToken = default)
    {
        RequireBoundary(compiled);
        if (!parent.CanRead || !parent.CanSeek || !candidate.CanRead || !candidate.CanSeek ||
            parent.Length != ImageBytes || candidate.Length != ImageBytes)
            throw new InvalidDataException("Collision readback requires two complete seekable candidate images.");
        byte[] rowBefore = compiled.CopyBaselineRow80(), rowAfter = compiled.CopyOutputRow80();
        long rowStart = WadLba * (long)LogicalBytes + RowWadOffset;
        HashSet<int> owned = compiled.OwnedRanges.SelectMany(range =>
        {
            int first = checked((int)((rowStart + range.DataRelativeOffset) / LogicalBytes));
            int last = checked((int)((rowStart + range.DataRelativeOffset + range.CopyBefore().Length - 1) / LogicalBytes));
            return Enumerable.Range(first, last - first + 1);
        }).ToHashSet();
        byte[] before = new byte[RawBytes * 256], after = new byte[RawBytes * 256];
        using var parentHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        parent.Position = candidate.Position = 0;
        int sector = 0, changedSectors = 0; long changedBytes = 0;
        while (parent.Position < ImageBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(before.Length, ImageBytes - parent.Position);
            parent.ReadExactly(before.AsSpan(0, count)); candidate.ReadExactly(after.AsSpan(0, count));
            parentHash.AppendData(before.AsSpan(0, count)); outputHash.AppendData(after.AsSpan(0, count));
            for (int offset = 0; offset < count; offset += RawBytes, sector++)
            {
                ReadOnlySpan<byte> original = before.AsSpan(offset, RawBytes), actual = after.AsSpan(offset, RawBytes);
                if (owned.Contains(sector))
                {
                    RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(original, sector);
                    int rowOffset = checked((int)(sector * (long)LogicalBytes - rowStart));
                    if (rowOffset < 0 || rowOffset > rowBefore.Length - LogicalBytes ||
                        !original.Slice(24, LogicalBytes).SequenceEqual(rowBefore.AsSpan(rowOffset, LogicalBytes)))
                        throw new InvalidDataException("Collision readback lost the entire native sector preimage.");
                    byte[] expected = original.ToArray();
                    rowAfter.AsSpan(rowOffset, LogicalBytes).CopyTo(expected.AsSpan(24, LogicalBytes));
                    expected = RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, sector);
                    if (!actual.SequenceEqual(expected))
                        throw new InvalidDataException("Collision output differs from the exact complete expected raw sector.");
                }
                else if (!original.SequenceEqual(actual))
                    throw new InvalidDataException("Collision output changed a protected physical sector.");
                if (!original.SequenceEqual(actual))
                {
                    changedSectors++;
                    for (int i = 0; i < RawBytes; i++) if (original[i] != actual[i]) changedBytes++;
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (parent.Length != ImageBytes || candidate.Length != ImageBytes ||
            parent.Position != ImageBytes || candidate.Position != ImageBytes ||
            Convert.ToHexStringLower(parentHash.GetHashAndReset()) != ParentImageSha256)
            throw new InvalidDataException("Collision readback lost the complete exact parent identity or file lengths.");
        return new(ParentImageSha256, Convert.ToHexStringLower(outputHash.GetHashAndReset()), owned.Count, changedSectors, changedBytes);
    }

    private static void RequireBoundary(Id65CompiledAuthoringCollisionIsolation compiled)
    {
        ArgumentNullException.ThrowIfNull(compiled);
        if (!compiled.CollapsedCyclorama || compiled.BaselineRow80Sha256 !=
                Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.ExpectedOutputRow80Sha256 ||
            compiled.OutputRow80Sha256 != "9e9b8064516684eebbd86012621d20ceab60e39bfa7711ff427bda71da952fb1" ||
            !compiled.RetainedTriangleIndexes.SequenceEqual([13995, 19808]) ||
            !compiled.ApplyExact(compiled.CopyOutputRow80(), reverse: true).SequenceEqual(compiled.CopyBaselineRow80()))
            throw new InvalidDataException("Only the exact source-compiled collapsed-sky, tile-1 Grass candidate is publishable here.");
    }

    private static void RequireSafeOutputPath(string directory, params string[] protectedPaths)
    {
        foreach (string path in protectedPaths)
        {
            string parent = Path.GetDirectoryName(path)!;
            if (directory.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
                parent.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                directory.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Collision output overlaps a protected source/candidate directory.");
        }
        for (DirectoryInfo? current = new(directory); current != null; current = current.Parent)
            if (current.LinkTarget != null || current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Collision output cannot traverse linked directories.");
    }

    private static void WriteNew(string path, byte[] bytes)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        stream.Write(bytes); stream.Flush(flushToDisk: true);
    }
    private static void CleanupOwnedStage(string stage)
    {
        try
        {
            if (!Directory.Exists(stage) || new DirectoryInfo(stage).LinkTarget != null) return;
            foreach (string name in new[] { ImageFileName, InputFileName, ReceiptFileName, CueFileName })
            {
                FileInfo file = new(Path.Combine(stage, name));
                if (file.Exists && file.LinkTarget == null) file.Delete();
            }
            Directory.Delete(stage, recursive: false);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { }
    }
    private static bool HasRecoveryEvidence(string directory)
    {
        try
        {
            return File.Exists(Path.Combine(directory, ".export-publication.lock")) ||
                Directory.EnumerateDirectories(directory, ".export-recovery-*").Any();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException) { return true; }
    }
    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static string HashStream(Stream stream) { stream.Position = 0; return Convert.ToHexStringLower(SHA256.HashData(stream)); }
}
