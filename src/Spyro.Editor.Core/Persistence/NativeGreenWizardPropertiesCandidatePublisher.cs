using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;

namespace Spyro.Editor.Core.Persistence;

internal sealed record NativeGreenWizardCandidateReadback(
    string SourceSha256, string OutputSha256, int OwnedRawSectors, int ChangedRawSectors, long ChangedBytes);
internal sealed record NativeGreenWizardCandidatePublication(
    string Directory, string Image, string Cue, string Input, string Receipt, string PlanSha256,
    NativeGreenWizardCandidateReadback Readback, string RetainedRecoveryDirectory);

/// <summary>Exact fixed-scene candidate projection; no scene growth or runtime authority.</summary>
internal sealed class NativeGreenWizardCandidateDiscPlan
{
    private const int RawBytes = 2352, LogicalBytes = 2048;
    private readonly byte[] _sourceScene, _outputScene;
    private readonly HashSet<int> _affected;
    private NativeGreenWizardCandidateDiscPlan(NativeGreenWizardPropertiesCandidatePlan candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        Candidate = candidate;
        _sourceScene = candidate.CopySourceScene(); _outputScene = candidate.CopyOutputScene();
        if (!candidate.CandidateOnly || candidate.RuntimeVerified || candidate.NormalCreateBinReady ||
            candidate.SourceImageSha256 != NativeGreenWizardCandidateSource.OriginalImageSha256 ||
            _sourceScene.Length != candidate.Target.SceneByteLength || _sourceScene.Length != _outputScene.Length ||
            candidate.Writes.Count != candidate.Targets.Count * 2 + 1 ||
            Hash(_sourceScene) != candidate.Target.SceneSha256 || Hash(_outputScene) != candidate.OutputSceneSha256)
            throw new InvalidDataException("The native candidate no longer has its complete fixed source-bound scene contract.");
        long start = 37L * LogicalBytes + candidate.TargetSceneWadOffset;
        NativeAddressSpace original = candidate.Writes[0].Address.Original.Space;
        if (original.Kind != NativeAddressKind.Scene || original.Revision != NativeAddressRevision.Original ||
            original.ByteLength != _sourceScene.Length || candidate.Writes.Any(write => write.Address.Original.Space != original))
            throw new InvalidDataException("Native candidate leaves do not share one exact original scene owner.");
        DiscSpace = new(NativeAddressKind.DiscLogical, candidate.SourceImageSha256, NativeAddressRevision.Final,
            candidate.SourceImageByteLength / RawBytes * LogicalBytes);
        SceneDiscExtent = new(DiscSpace, start, _sourceScene.Length);
        NativeRelocationMap map = new(original, DiscSpace,
            [new("source-bound-native-wizard-scene", new(original, 0, _sourceScene.Length), SceneDiscExtent, LogicalBytes)]);
        DiscWrites = Array.AsReadOnly(candidate.Writes.Select(write =>
            write with { Address = map.Map(write.Address.Original) }).OrderBy(write => write.Address.Final.Offset).ToArray());
        NativeRelocationMap.RequireDisjoint(DiscWrites.Select(write => write.Address.Final));
        NativeRelocationMap.RequireDisjoint(DiscWrites.Select(write => write.Address.Original));
        byte[] applied = _sourceScene.ToArray();
        foreach (NativeGreenWizardCandidateWrite write in DiscWrites)
        {
            int offset = checked((int)write.Address.Original.Offset);
            byte[] before = Convert.FromHexString(write.BeforeHex), after = Convert.FromHexString(write.AfterHex);
            if (before.Length != after.Length || before.Length != write.Address.Final.ByteLength ||
                !applied.AsSpan(offset, before.Length).SequenceEqual(before))
                throw new InvalidDataException("A native scene leaf has a stale preimage or changed owner length.");
            after.CopyTo(applied, offset);
        }
        if (!applied.AsSpan().SequenceEqual(_outputScene))
            throw new InvalidDataException("Native scene leaves do not preserve the complete logical complement.");
        foreach (NativeGreenWizardCandidateWrite write in DiscWrites)
            Convert.FromHexString(write.BeforeHex).CopyTo(applied, checked((int)write.Address.Original.Offset));
        if (!applied.AsSpan().SequenceEqual(_sourceScene))
            throw new InvalidDataException("The native candidate's exact scene inverse is incomplete.");
        _affected = DiscWrites.SelectMany(write =>
        {
            int first = checked((int)(write.Address.Final.Offset / LogicalBytes));
            int last = checked((int)((write.Address.Final.EndExclusive - 1) / LogicalBytes));
            return Enumerable.Range(first, last - first + 1);
        }).ToHashSet();
        AffectedRawSectors = Array.AsReadOnly(_affected.Order().ToArray());
    }
    public NativeGreenWizardPropertiesCandidatePlan Candidate { get; }
    public NativeAddressSpace DiscSpace { get; }
    public NativeByteExtent SceneDiscExtent { get; }
    public IReadOnlyList<NativeGreenWizardCandidateWrite> DiscWrites { get; }
    public IReadOnlyList<int> AffectedRawSectors { get; }
    public static NativeGreenWizardCandidateDiscPlan Create(NativeGreenWizardPropertiesCandidatePlan candidate) => new(candidate);

    public byte[] CreateExpectedRawSector(int lba, ReadOnlySpan<byte> sourceRawSector)
    {
        if (!_affected.Contains(lba) || sourceRawSector.Length != RawBytes)
            throw new InvalidDataException("The requested raw sector is outside the native candidate's complete owned set.");
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(sourceRawSector, lba);
        long offset = (long)lba * LogicalBytes - SceneDiscExtent.Offset;
        if (offset < 0 || offset > _sourceScene.Length - LogicalBytes ||
            !sourceRawSector.Slice(24, LogicalBytes).SequenceEqual(_sourceScene.AsSpan((int)offset, LogicalBytes)))
            throw new InvalidDataException("The native candidate's complete raw-sector payload has a stale source preimage.");
        byte[] expected = sourceRawSector.ToArray();
        _outputScene.AsSpan((int)offset, LogicalBytes).CopyTo(expected.AsSpan(24, LogicalBytes));
        return RawMode2Form1SectorIntegrity.RebuildRawSectorCopy(expected, lba);
    }

    /// <summary>
    /// Reads the complete source and candidate, including every protected
    /// physical byte. Pairwise equality is not a source certificate: the final
    /// independent whole-source hash must still match the original retail disc.
    /// </summary>
    public NativeGreenWizardCandidateReadback Verify(Stream source, Stream candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source); ArgumentNullException.ThrowIfNull(candidate);
        cancellationToken.ThrowIfCancellationRequested();
        long imageLength = Candidate.SourceImageByteLength;
        if (!source.CanRead || !source.CanSeek || !candidate.CanRead || !candidate.CanSeek ||
            source.Length != imageLength || candidate.Length != imageLength)
            throw new InvalidDataException("Native candidate readback requires two complete exact-length seekable images.");
        byte[] before = new byte[RawBytes * 256], after = new byte[RawBytes * 256];
        using IncrementalHash sourceHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash outputHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        source.Position = candidate.Position = 0;
        int sector = 0, ownedSeen = 0, changedSectors = 0; long changedBytes = 0;
        while (source.Position < imageLength)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = (int)Math.Min(before.Length, imageLength - source.Position);
            source.ReadExactly(before.AsSpan(0, count)); candidate.ReadExactly(after.AsSpan(0, count));
            sourceHash.AppendData(before.AsSpan(0, count)); outputHash.AppendData(after.AsSpan(0, count));
            for (int offset = 0; offset < count; offset += RawBytes, sector++)
            {
                ReadOnlySpan<byte> original = before.AsSpan(offset, RawBytes), actual = after.AsSpan(offset, RawBytes);
                if (_affected.Contains(sector))
                {
                    ownedSeen++;
                    if (!actual.SequenceEqual(CreateExpectedRawSector(sector, original)))
                        throw new InvalidDataException("Native candidate output differs from the complete exact raw-sector afterimage.");
                }
                else if (!original.SequenceEqual(actual))
                    throw new InvalidDataException("Native candidate output changed a protected physical sector.");
                if (!original.SequenceEqual(actual))
                {
                    changedSectors++;
                    for (int index = 0; index < RawBytes; index++) if (original[index] != actual[index]) changedBytes++;
                }
            }
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (source.Length != imageLength || candidate.Length != imageLength || source.Position != imageLength ||
            candidate.Position != imageLength || ownedSeen != _affected.Count ||
            Convert.ToHexStringLower(sourceHash.GetHashAndReset()) != Candidate.SourceImageSha256)
            throw new InvalidDataException("Native candidate readback lost complete original source identity, ownership or final lengths.");
        return new(Candidate.SourceImageSha256, Convert.ToHexStringLower(outputHash.GetHashAndReset()), ownedSeen, changedSectors, changedBytes);
    }
    private static string Hash(ReadOnlySpan<byte> bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));
}

/// <summary>
/// Explicit new-candidate publication only. It recompiles a request from the
/// actual complete source image; serialized plans/hashes carry no authority.
/// Uses shared exception-rollback publication with the launchable CUE last.
/// Requires a trusted exclusive output directory, not a hostile-filesystem or
/// power-loss atomicity guarantee. It never dispatches an old resident recipe.
/// </summary>
internal static class NativeGreenWizardPropertiesCandidatePublisher
{
    public const string ImageFileName = "native-green-wizard-owned-properties-candidate.bin";
    public const string CueFileName = "native-green-wizard-owned-properties-candidate.cue";
    private const string InputFileName = "candidate-input.json", ReceiptFileName = "candidate.receipt.json";

    public static NativeGreenWizardCandidateDiscPlan Compile(string sourceImagePath,
        NativeGreenWizardPropertiesCandidateRequest request) => NativeGreenWizardCandidateDiscPlan.Create(
            NativeGreenWizardPropertiesCandidateCompiler.Compile(NativeGreenWizardCandidateSource.Inspect(sourceImagePath), request));

    public static NativeGreenWizardCandidatePublication Publish(string sourceImagePath, string sourceCuePath, string outputRoot,
        NativeGreenWizardPropertiesCandidateRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        sourceImagePath = Path.GetFullPath(sourceImagePath); sourceCuePath = Path.GetFullPath(sourceCuePath);
        if (!File.Exists(sourceCuePath) || !DiscCueSheet.ReferencesDataImage(sourceCuePath, sourceImagePath))
            throw new InvalidDataException("Native Wizard publication requires an existing CUE bound to the exact selected source image.");
        NativeGreenWizardCandidateDiscPlan disc = Compile(sourceImagePath, request);
        NativeGreenWizardPropertiesCandidatePlan plan = disc.Candidate;
        using FileStream source = new(sourceImagePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (source.Length != plan.SourceImageByteLength || HashStream(source) != plan.SourceImageSha256)
            throw new InvalidDataException("The complete original Wizard source changed before publication.");
        DiscLayout layout = DiscImage.DetectLayout(sourceImagePath);
        DiscFileRecord wad = DiscImage.FindRootFileRecord(source, layout, name => name == "WAD.WAD");
        if (layout.SectorSize != 2352 || layout.UserOffset != 24 || wad.Lba != 37 ||
            plan.TargetSceneWadOffset + plan.Target.SceneByteLength > wad.Size ||
            !DiscImage.ReadFileBytes(source, layout, wad.Lba, plan.TargetSceneWadOffset, plan.Target.SceneByteLength)
                .SequenceEqual(plan.CopySourceScene()))
            throw new InvalidDataException("Native Wizard publication lost the complete original WAD/scene owner preimage.");
        byte[] cue = DiscCueSheet.TextEncoding.GetBytes(DiscCueSheet.BuildForImage(sourceCuePath, sourceImagePath, ImageFileName));
        byte[] input = JsonSerializer.SerializeToUtf8Bytes(new
        {
            schemaVersion = 1, profile = NativeGreenWizardPropertiesCandidatePlan.ProfileId,
            sourceImageSha256 = plan.SourceImageSha256, donorLevelKey = plan.Donor.LevelKey,
            donorTrueIndex = plan.DonorTrueIndex, targetLevelKey = plan.Target.LevelKey,
            targetTrueIndices = plan.Targets.Select(target => target.TrueIndex).ToArray()
        });
        string directory = Path.Combine(Path.GetFullPath(outputRoot), "wizard-" + plan.PlanSha256);
        RequireSafeOutputPath(directory, sourceImagePath, sourceCuePath);
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(directory) || File.Exists(directory))
            throw new IOException("This immutable native Wizard candidate or retained residue already exists; verify it or choose a new output root.");
        Directory.CreateDirectory(directory);
        string stage = Path.Combine(directory, ".stage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        string stagedImage = Path.Combine(stage, ImageFileName);
        bool published = false; string retainedRecovery = "";
        try
        {
            using (FileStream destination = new(stagedImage, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                source.Position = 0;
                byte[] copy = new byte[1024 * 1024]; int count;
                while ((count = source.Read(copy)) != 0)
                { cancellationToken.ThrowIfCancellationRequested(); destination.Write(copy.AsSpan(0, count)); }
                byte[] raw = new byte[2352];
                foreach (int lba in disc.AffectedRawSectors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    source.Position = (long)lba * 2352; source.ReadExactly(raw);
                    byte[] expected = disc.CreateExpectedRawSector(lba, raw);
                    destination.Position = (long)lba * 2352; destination.Write(expected);
                }
                destination.Flush(flushToDisk: true);
            }
            NativeGreenWizardCandidateReadback readback;
            using (FileStream candidate = File.OpenRead(stagedImage)) readback = disc.Verify(source, candidate, cancellationToken);
            byte[] receipt = JsonSerializer.SerializeToUtf8Bytes(new
            {
                schemaVersion = 1, profile = NativeGreenWizardPropertiesCandidatePlan.ProfileId, plan.PlanSha256,
                inputSha256 = Hash(input), plan.SourceImageSha256, plan.SourceImageByteLength,
                outputImageSha256 = readback.OutputSha256, plan.Donor, plan.DonorTrueIndex,
                plan.DonorRowSha256, plan.DonorPayloadSha256, plan.Target, plan.Targets,
                plan.ResidentDependencies, plan.OutputSceneSha256, plan.RuntimeRequirements,
                writes = disc.DiscWrites.Select(write => new
                {
                    write.Id, kind = write.Kind.ToString(), sceneOffset = write.Address.Original.Offset,
                    discLogicalOffset = write.Address.Final.Offset, byteLength = write.Address.Final.ByteLength,
                    write.BeforeHex, write.AfterHex
                }), readback, imageFileName = ImageFileName, cueFileName = CueFileName, cueSha256 = Hash(cue),
                privateMutableInstances = true, missingActorResourcesImported = false, behaviorOverlayImported = false,
                physicalComplementVerified = true, exactSceneInverseVerified = true,
                candidateOnly = true, runtimeVerified = false, normalCreateBinReady = false, appIntegrated = false,
                requiresTrustedExclusiveOutputDirectory = true, hostileFilesystemRaceSafe = false
            }, new JsonSerializerOptions { WriteIndented = true });
            WriteNew(Path.Combine(stage, InputFileName), input);
            WriteNew(Path.Combine(stage, ReceiptFileName), receipt);
            WriteNew(Path.Combine(stage, CueFileName), cue);
            string[] names = [ImageFileName, InputFileName, ReceiptFileName, CueFileName];
            ExportArtifactPublicationResult publication = ExportArtifactTransaction.PublishNew(directory, names.Select(name =>
                new ExportArtifactReplacement(Path.Combine(directory, name), Path.Combine(stage, name))).ToArray(), cancellationToken);
            published = true; retainedRecovery = publication.RetainedRecoveryDirectory;
            using (FileStream candidate = File.OpenRead(Path.Combine(directory, ImageFileName)))
                if (disc.Verify(source, candidate, CancellationToken.None) != readback)
                    throw new InvalidDataException("The published native Wizard candidate failed final exact whole-disc readback.");
            foreach ((string name, byte[] expected) in new[] { (InputFileName, input), (ReceiptFileName, receipt), (CueFileName, cue) })
                if (!File.ReadAllBytes(Path.Combine(directory, name)).SequenceEqual(expected))
                    throw new InvalidDataException("Published native Wizard candidate companion bytes changed.");
            if (!HasRecoveryEvidence(directory)) CleanupOwnedStage(stage);
            if (Directory.Exists(stage) && retainedRecovery.Length == 0) retainedRecovery = stage;
            return new(directory, Path.Combine(directory, ImageFileName), Path.Combine(directory, CueFileName),
                Path.Combine(directory, InputFileName), Path.Combine(directory, ReceiptFileName), plan.PlanSha256, readback, retainedRecovery);
        }
        catch (Exception exception) when (published)
        {
            throw new IOException("Native Wizard candidate was published and retained, but final verification failed. Inspect: " + directory, exception);
        }
        finally
        {
            // Never delete shared-transaction recovery evidence, final artifacts,
            // or an unknown temporary file. An interrupted stage is inspectable.
            if (!HasRecoveryEvidence(directory)) CleanupOwnedStage(stage);
        }
    }

    private static void RequireSafeOutputPath(string directory, params string[] protectedPaths)
    {
        foreach (string path in protectedPaths)
        {
            string parent = Path.GetDirectoryName(path)!;
            if (directory.Equals(parent, StringComparison.OrdinalIgnoreCase) ||
                parent.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                directory.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Native Wizard output overlaps a protected original source directory.");
        }
        for (DirectoryInfo? current = new(directory); current != null; current = current.Parent)
            if (current.LinkTarget != null || current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint))
                throw new InvalidDataException("Native Wizard output cannot traverse linked directories.");
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
