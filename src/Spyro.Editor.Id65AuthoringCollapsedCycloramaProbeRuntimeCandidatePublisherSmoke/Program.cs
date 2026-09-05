using System.Security.Cryptography;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
Require(File.Exists(lockedImagePath),
    "Missing exact collapsed-probe publisher fixture: " + lockedImagePath);

FileState lockedBefore = SnapshotFile(lockedImagePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
Id65AuthoringEditorAggregateBoundSource boundSource =
    Id65AuthoringEditorAggregateBoundSource.Capture(lockedImagePath);
Id65AuthoringCompositeLockedSource lockedSource = boundSource.LockedSource;
UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3StaticPlan v3Template =
    UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction
        .BuildStaticPlan(lockedImagePath);
Id65V2NativeTextureWitness textureWitness =
    Id65V2NativeTextureCompositionCompiler
        .DerivePinnedT66WitnessFromLockedBase(lockedImagePath);
Id65MobyDependencyBundleDescriptor grassBundle =
    Id65AuthoringMobyDependencyBundleCompiler
        .CreateArtisansGrassBundleFromLockedBase(lockedImagePath);
Id65AuthoringEditorDocument document =
    Id65AuthoringEditorDocument
        .CreateRemotePadMobyChoiceTerrainSetBlankCycloramaExact(
            Id65RemotePadMobyKind.GrassT107,
            Id65RemotePadGrassT107Catalog.CenterPositionId,
            [Id65AuthoringTerrainSetCatalog.Tile1Id]);
Id65CompiledAuthoringCollapsedCycloramaGeometryProbe compiled =
    Id65AuthoringCollapsedCycloramaGeometryProbeCompiler.Compile(new(
        document,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));

Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
    .ValidateCompiledProbeForSmoke(compiled);
Require(
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher.Format ==
        "spyro-editor-id65-authoring-collapsed-35-part-cyclorama-probe-schema7-runtime-candidate-receipt" &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .ReceiptSchemaVersion == 1 &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher.ProfileId ==
        "id65-authoring-collapsed-35-part-cyclorama-probe-schema7-runtime-candidate-publisher-v1" &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .CandidateImageFileName.Contains(
            "collapsed-35-part-cyclorama-probe",
            StringComparison.Ordinal) &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .CandidateCueFileName.Contains(
            "collapsed-35-part-cyclorama-probe",
            StringComparison.Ordinal) &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .CandidateReceiptFileName.Contains(
            "collapsed-35-part-cyclorama-probe",
            StringComparison.Ordinal) &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .FirstSkyRawSectorLba == 54_630 &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .SkyRawSectorCount == 17 &&
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .LastSkyRawSectorLba == 54_646,
    "The guarded publisher profile, naming, or exact sky-sector envelope changed.");
Require(
    compiled.Schema7BaselineRow80Sha256 ==
        "3689e111b027ce07fac6fe0b0f6b1e9a1a3868f4a852b0928678645e7045d819" &&
    compiled.OutputRow80Sha256 ==
        "478850353edc16945cf9498c38aa3de4716a52b1531aa89108fcdbfbe610edf6" &&
    compiled.BaselineCycloramaSha256 ==
        "a9da1343ef6493cd46ab57d84ae78f27495affc564bcc9464396752914648ac2" &&
    compiled.OutputCycloramaSha256 ==
        "6024a1bb86c4132998c835c7ef87f18bc90add554371871239a1e99e26475de9" &&
    compiled.OutputGeometrySha256 ==
        "f3e5d6c705733f8db639bea5de4682ec186d80e84b2e1c1691df495b2dee0a89" &&
    compiled.OutputModelSha256 ==
        "8b1999117eee41e5083d12ef79f60a5c8628361c96cf8108f87cc529c213155f" &&
    compiled.CandidateCollapsedVertexWordCount == 1_880 &&
    compiled.ChangedVertexWordCount == 1_879 &&
    compiled.ChangedLogicalByteCount == 7_367 &&
    compiled.OwnedRanges.Count == 1_941 &&
    compiled.PolygonChangedByteCount == 0 &&
    compiled.OcclusionChangedByteCount == 0 &&
    compiled.OcclusionTailChangedByteCount == 0,
    "The focused compiler hashes or exact diff census changed.");

string temporaryRoot = Path.Combine(
    Path.GetTempPath(),
    "spyro-id65-collapsed-probe-publisher-smoke-" +
        Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(temporaryRoot);
try
{
    Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
        .AtomicPublisherSelfTestResult atomic =
            await Id65AuthoringCollapsedCycloramaProbeRuntimeCandidatePublisher
                .RunAtomicPublisherSelfTestAsync(temporaryRoot);
    Require(
        atomic.DeterministicReuseVerified &&
        atomic.ExistingTamperRejected &&
        atomic.InjectedFaultCleanupVerified &&
        IsCanonicalSha256(atomic.PublishedValueSha256),
        "The atomic publisher reuse/tamper/fault-cleanup smoke failed.");
}
finally
{
    if (Directory.Exists(temporaryRoot))
        Directory.Delete(temporaryRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The no-BIN publisher smoke changed the locked fixture file.");
Require(SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
        .SequenceEqual(directoryBefore),
    "The no-BIN publisher smoke changed the locked fixture directory.");

Console.WriteLine(
    "ID65_AUTHORING_COLLAPSED_CYCLORAMA_PROBE_RUNTIME_CANDIDATE_PUBLISHER_SMOKE_OK " +
    $"baselineRowSha={compiled.Schema7BaselineRow80Sha256} " +
    $"outputRowSha={compiled.OutputRow80Sha256} " +
    $"outputModelSha={compiled.OutputModelSha256} " +
    $"outputCycloramaSha={compiled.OutputCycloramaSha256} " +
    $"geometrySha={compiled.OutputGeometrySha256} " +
    $"parts={compiled.OutputLayout.PartCount} " +
    $"candidateWords={compiled.CandidateCollapsedVertexWordCount} " +
    $"changedWords={compiled.ChangedVertexWordCount} " +
    $"changedBytes={compiled.ChangedLogicalByteCount} " +
    $"ranges={compiled.OwnedRanges.Count} " +
    "atomicReuse=1 tamperRejected=1 faultCleanup=1 " +
    "realBinPublished=0 transitionalProbeOnly=1 fullyOriginalCyclorama=0 " +
    "runtime=0 ui=0 app=0 document=0 createBin=0 release=0 promotion=0 publishable=0");
return;

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
        return Path.GetFullPath(explicitRoot);
    DirectoryInfo? cursor = new(AppContext.BaseDirectory);
    while (cursor is not null)
    {
        if (Directory.Exists(Path.Combine(cursor.FullName, "src")) &&
            Directory.Exists(Path.Combine(cursor.FullName, "_local")))
            return cursor.FullName;
        cursor = cursor.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the Spyro Editor repository root.");
}

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    using FileStream stream = File.OpenRead(path);
    return new(
        info.Length,
        info.LastWriteTimeUtc.Ticks,
        Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly)
        .Select(entry => Path.GetFileName(entry))
        .OrderBy(name => name, StringComparer.Ordinal)
        .ToArray()!;

static bool IsCanonicalSha256(string value) =>
    value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidDataException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
