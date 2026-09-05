using System.Security.Cryptography;
using System.Text.Json;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string parentDirectory = FindParentDirectory(args.ElementAtOrDefault(1));
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
    "Missing exact schema-8 publisher fixture: " + lockedImagePath);

IReadOnlyList<EntryState> parentBefore = SnapshotTree(parentDirectory);
FileState lockedBefore = SnapshotFile(lockedImagePath);
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
Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled =
    Id65AuthoringMinimalCycloramaAndVisibilityCompiler.Compile(new(
        document,
        lockedSource,
        v3Template,
        textureWitness,
        grassBundle));

Require(
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.Format ==
        "spyro-editor-id65-authoring-schema8-minimal-cyclorama-and-visibility-guarded-publication-plan" &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .ReceiptSchemaVersion == 1 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.ProfileId ==
        "id65-authoring-schema8-minimal-cyclorama-and-visibility-guarded-publisher-plan-v1" &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.WadLba == 37 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .TargetDataWadOffset == 0x6936800 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .TargetDataByteLength == 0x2E2000 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .RawSectorByteLength == 2352 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .LogicalSectorByteLength == 2048 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .UserDataOffset == 24 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .VisibilityDirectoryRawSectorLba == 54_438 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .FirstCycloramaRawSectorLba == 54_630 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .CycloramaRawSectorCount == 17 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .ExpectedRawSectorPatchCount == 18 &&
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .ExpectedRawSectorRunCount == 2,
    "The schema-8 guarded publisher profile or raw layout pins changed.");

Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublishRequest request = new(
    parentDirectory,
    compiled);
Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan first =
    await Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .PlanAsync(request);
Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan repeat =
    await Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .PlanAsync(request);

Require(
    first.PublicationIdentitySha256 ==
        repeat.PublicationIdentitySha256 &&
    first.ProspectiveCandidateDirectoryName ==
        repeat.ProspectiveCandidateDirectoryName &&
    first.ReceiptContentSha256 == repeat.ReceiptContentSha256 &&
    first.Receipt == repeat.Receipt &&
    first.RawSectorPatches.Count == repeat.RawSectorPatches.Count &&
    first.RawSectorPatches.Zip(repeat.RawSectorPatches).All(pair =>
        pair.First.Lba == pair.Second.Lba &&
        pair.First.BeforeRawSectorSha256 ==
            pair.Second.BeforeRawSectorSha256 &&
        pair.First.AfterRawSectorSha256 ==
            pair.Second.AfterRawSectorSha256 &&
        pair.First.CopyBeforeRawSector().SequenceEqual(
            pair.Second.CopyBeforeRawSector()) &&
        pair.First.CopyAfterRawSector().SequenceEqual(
            pair.Second.CopyAfterRawSector())),
    "The schema-8 content-addressed publication plan is not deterministic.");

Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt receipt =
    first.Receipt;
Require(
    receipt.Format ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.Format &&
    receipt.SchemaVersion == 1 &&
    receipt.ProfileId ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher.ProfileId &&
    receipt.PublicationIdentitySha256 == first.PublicationIdentitySha256 &&
    receipt.ProspectiveCandidateDirectoryName ==
        first.ProspectiveCandidateDirectoryName &&
    receipt.ParentPublicationIdentitySha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedParentPublicationIdentitySha256 &&
    receipt.ParentImageSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedParentImageSha256 &&
    receipt.ParentCueSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedParentCueSha256 &&
    receipt.ParentReceiptFileSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedParentReceiptFileSha256 &&
    receipt.ParentReceiptContentSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedParentReceiptContentSha256 &&
    receipt.DocumentCanonicalSha256 == compiled.DocumentCanonicalSha256 &&
    receipt.CompilerProfileId == compiled.ProfileId &&
    receipt.CompilerSchemaVersion == compiled.SchemaVersion &&
    receipt.CompilerDeterministicPlanSha256 ==
        compiled.DeterministicPlanSha256 &&
    receipt.Schema7BaselineRow80Sha256 ==
        compiled.Schema7BaselineRow80Sha256 &&
    receipt.OutputRow80Sha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedOutputRow80Sha256 &&
    receipt.OutputModelSha256 == compiled.OutputModelSha256 &&
    receipt.OutputCycloramaSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedOutputCycloramaSha256 &&
    receipt.OutputVisibilityDirectorySha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedOutputVisibilityDirectorySha256 &&
    receipt.OutputOcclusionSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityCompiler
            .ExpectedOutputOcclusionSha256 &&
    receipt.AtomicOwnedRangeCount == 2 &&
    receipt.AtomicOwnedByteCount == 34_616 &&
    receipt.AtomicChangedLogicalByteCount == 32_294 &&
    receipt.RawSectorPatchCount == 18 &&
    receipt.RawSectorRunCount == 2 &&
    receipt.RawSectorLbasCanonical ==
        "54438,54630,54631,54632,54633,54634,54635,54636,54637," +
        "54638,54639,54640,54641,54642,54643,54644,54645,54646" &&
    receipt.VisibilityDirectoryRawSectorLba == 54_438 &&
    receipt.FirstCycloramaRawSectorLba == 54_630 &&
    receipt.CycloramaRawSectorCount == 17 &&
    receipt.PhysicalChangedByteCount > receipt.AtomicChangedLogicalByteCount &&
    receipt.PhysicalChangedByteCount ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedPhysicalChangedByteCount &&
    receipt.RawSectorPatchMapSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedRawSectorPatchMapSha256 &&
    receipt.PhysicalDiffSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedPhysicalDiffSha256 &&
    receipt.PublicationIdentitySha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedPublicationIdentitySha256 &&
    receipt.ReceiptContentSha256 ==
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .ExpectedReceiptContentSha256 &&
    IsSha(receipt.RawSectorPatchMapSha256) &&
    IsSha(receipt.PhysicalDiffSha256) &&
    IsSha(receipt.PublicationIdentitySha256) &&
    IsSha(receipt.ReceiptContentSha256),
    "The schema-8 guarded receipt lost an exact identity or sector pin.");
Require(
    receipt.ExactParentSchema7SiblingVerified &&
    receipt.ExactSchema7PreimageVerified &&
    receipt.ExactSchema7InverseVerified &&
    receipt.ExactSchema8AfterimageProjected &&
    receipt.ExactlyTwoFullAtomicRangesVerified &&
    receipt.EighteenNoncontiguousRawSectorsVerified &&
    receipt.RawMode2Form1IntegrityVerified && receipt.BcdMsfVerified &&
    receipt.DuplicatedSubheadersVerified &&
    receipt.PhysicalComplementPlanningVerified && receipt.PlanningOnly &&
    !receipt.PublicationPerformed &&
    !receipt.FileSystemMutationPerformed &&
    !receipt.ContainsFullDiscImage && !receipt.ContainsCue &&
    !receipt.WritesFileSystem && !receipt.WritesDiscImage &&
    !receipt.WritesCue &&
    !receipt.SeparateGuardedCandidatePublisherAuthorized &&
    !receipt.RuntimeCandidateAuthorized && !receipt.RuntimeProven &&
    !receipt.AppIntegrated && !receipt.UiIntegrated &&
    !receipt.DocumentPersistenceIntegrated && !receipt.CreateBinEnabled &&
    !receipt.NormalCreateBinEnabled && !receipt.ReleaseIntegrated &&
    !receipt.PromotionAuthorized && !receipt.Publishable &&
    first.PlanningOnly && !first.PublicationPerformed &&
    !first.FileSystemMutationPerformed && !first.RuntimeCandidateAuthorized &&
    !first.RuntimeProven && !first.NormalCreateBinEnabled &&
    !first.ReleaseIntegrated && !first.Publishable,
    "The schema-8 plan opened a filesystem/runtime/release gate.");

VerifyReceiptContentHash(receipt);
VerifyRawPatches(compiled, first);
VerifyDefensiveCopies(first);
VerifyPatchTamperGuards(compiled, first);

RejectAsync("null request", async () =>
    await Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .PlanAsync(null!));
RejectAsync("missing parent", async () =>
    await Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .PlanAsync(new(
            Path.Combine(parentDirectory, "missing-schema7-sibling"),
            compiled)));

Require(SnapshotTree(parentDirectory).SequenceEqual(parentBefore),
    "The planning-only publisher changed the schema-7 sibling tree.");
Require(SnapshotFile(lockedImagePath) == lockedBefore,
    "The planning-only publisher changed the locked fixture.");
string prospectivePath = Path.Combine(
    Directory.GetParent(parentDirectory)!.FullName,
    first.ProspectiveCandidateDirectoryName);
Require(!Directory.Exists(prospectivePath) && !File.Exists(prospectivePath),
    "The planning-only publisher created its prospective destination.");

Console.WriteLine(
    "ID65_AUTHORING_MINIMAL_CYCLORAMA_AND_VISIBILITY_GUARDED_PUBLISHER_SMOKE_OK " +
    $"publication={receipt.PublicationIdentitySha256} " +
    $"receipt={receipt.ReceiptContentSha256} " +
    $"patchMap={receipt.RawSectorPatchMapSha256} " +
    $"physicalDiff={receipt.PhysicalDiffSha256} " +
    $"physicalChanged={receipt.PhysicalChangedByteCount} " +
    "rawSectors=18 runs=2 lbas=54438+54630..54646 " +
    "logicalChanged=32294 atomicRanges=2 owned=34616 " +
    "deterministic=1 inverse=1 tamperGuards=6 " +
    "writes=0 bin=0 cue=0 runtime=0 release=0");
return;

static void VerifyReceiptContentHash(
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationReceipt receipt)
{
    JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false
    };
    string hash = Hash(JsonSerializer.SerializeToUtf8Bytes(
        receipt with { ReceiptContentSha256 = "" },
        options));
    Require(hash == receipt.ReceiptContentSha256,
        "The in-memory receipt content hash changed.");
}

static void VerifyRawPatches(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan plan)
{
    int[] expectedLbas =
    [
        54_438,
        .. Enumerable.Range(54_630, 17)
    ];
    Require(plan.RawSectorPatches.Select(item => item.Lba)
        .SequenceEqual(expectedLbas),
        "The planned raw sectors are not the exact noncontiguous map.");
    int logicalChanged = 0;
    int physicalChanged = 0;
    foreach (Id65AuthoringSchema8RawSectorPatch patch in
             plan.RawSectorPatches)
    {
        byte[] before = patch.CopyBeforeRawSector();
        byte[] after = patch.CopyAfterRawSector();
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(
            before,
            patch.Lba);
        RawMode2Form1SectorIntegrity.RequireRawSectorIntegrity(
            after,
            patch.Lba);
        Require(
            patch.RawSectorByteLength == 2352 &&
            Hash(before) == patch.BeforeRawSectorSha256 &&
            Hash(after) == patch.AfterRawSectorSha256 &&
            !before.SequenceEqual(after) &&
            before.AsSpan(0, 24).SequenceEqual(after.AsSpan(0, 24)) &&
            before.AsSpan(16, 4).SequenceEqual(before.AsSpan(20, 4)) &&
            after.AsSpan(16, 4).SequenceEqual(after.AsSpan(20, 4)) &&
            patch.DuplicatedSubmode == 0x08 &&
            patch.UserDataChangedByteCount > 0 &&
            patch.PhysicalChangedByteCount >=
                patch.UserDataChangedByteCount &&
            patch.IntersectsVisibilityDirectory == (patch.Lba == 54_438) &&
            patch.IntersectsCyclorama == (patch.Lba >= 54_630),
            "A planned raw MODE2 patch lost identity or integrity.");
        logicalChanged += patch.UserDataChangedByteCount;
        physicalChanged += patch.PhysicalChangedByteCount;
    }
    Require(logicalChanged == compiled.ChangedByteCount &&
        logicalChanged == 32_294 &&
        physicalChanged == plan.Receipt.PhysicalChangedByteCount,
        "The raw-sector logical/physical census changed.");
}

static void VerifyDefensiveCopies(
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan plan)
{
    Id65AuthoringSchema8RawSectorPatch patch = plan.RawSectorPatches[0];
    byte[] before = patch.CopyBeforeRawSector();
    byte[] after = patch.CopyAfterRawSector();
    before[0] ^= 1;
    after[0] ^= 1;
    Require(Hash(patch.CopyBeforeRawSector()) ==
            patch.BeforeRawSectorSha256 &&
        Hash(patch.CopyAfterRawSector()) == patch.AfterRawSectorSha256,
        "A raw-sector patch exposed mutable backing bytes.");
}

static void VerifyPatchTamperGuards(
    Id65CompiledAuthoringMinimalCycloramaAndVisibility compiled,
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan plan)
{
    byte[] parentRow = compiled.CopySchema7BaselineRow80();
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
        .RequireExactPatchPlan(compiled, parentRow, plan.RawSectorPatches);

    Reject("missing raw sector", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                parentRow,
                plan.RawSectorPatches.Skip(1).ToArray()));
    Reject("reordered raw sectors", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                parentRow,
                plan.RawSectorPatches.Reverse().ToArray()));
    Id65AuthoringSchema8RawSectorPatch first = plan.RawSectorPatches[0];
    Reject("wrong raw LBA", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                parentRow,
                ReplaceFirst(plan, NewPatch(first, lba: first.Lba + 1))));
    byte[] badPayload = first.CopyAfterRawSector();
    badPayload[24] ^= 1;
    Reject("tampered raw payload", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                parentRow,
                ReplaceFirst(plan, NewPatch(first, after: badPayload))));
    byte[] badEcc = first.CopyAfterRawSector();
    badEcc[^1] ^= 1;
    Reject("tampered raw ECC", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                parentRow,
                ReplaceFirst(plan, NewPatch(first, after: badEcc))));
    byte[] badParent = parentRow.ToArray();
    badParent[0x400] ^= 1;
    Reject("tampered parent row", () =>
        Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublisher
            .RequireExactPatchPlan(
                compiled,
                badParent,
                plan.RawSectorPatches));
}

static Id65AuthoringSchema8RawSectorPatch[] ReplaceFirst(
    Id65AuthoringMinimalCycloramaAndVisibilityGuardedPublicationPlan plan,
    Id65AuthoringSchema8RawSectorPatch replacement) =>
[
    replacement,
    .. plan.RawSectorPatches.Skip(1).Select(item => item.DeepCopy())
];

static Id65AuthoringSchema8RawSectorPatch NewPatch(
    Id65AuthoringSchema8RawSectorPatch source,
    int? lba = null,
    byte[]? before = null,
    byte[]? after = null) => new(
        lba ?? source.Lba,
        before ?? source.CopyBeforeRawSector(),
        after ?? source.CopyAfterRawSector(),
        source.UserDataChangedByteCount,
        source.PhysicalChangedByteCount,
        source.IntersectsVisibilityDirectory,
        source.IntersectsCyclorama);

static void Reject(string label, Action action)
{
    try
    {
        action();
        throw new InvalidOperationException(
            label + " unexpectedly passed its guard.");
    }
    catch (Exception ex) when (ex is ArgumentNullException or
        InvalidDataException)
    {
    }
}

static void RejectAsync(string label, Func<Task> action)
{
    try
    {
        action().GetAwaiter().GetResult();
        throw new InvalidOperationException(
            label + " unexpectedly passed its guard.");
    }
    catch (Exception ex) when (ex is ArgumentNullException or
        ArgumentException or DirectoryNotFoundException or
        FileNotFoundException or InvalidDataException)
    {
    }
}

static bool IsSha(string value) =>
    value.Length == 64 && value.All(character =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f');

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static FileState SnapshotFile(string path)
{
    FileInfo info = new(path);
    return new(info.Length, info.LastWriteTimeUtc.Ticks);
}

static IReadOnlyList<EntryState> SnapshotTree(string root) =>
    Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories)
        .Select(path =>
        {
            FileAttributes attributes = File.GetAttributes(path);
            bool directory = (attributes & FileAttributes.Directory) != 0;
            FileInfo? file = directory ? null : new FileInfo(path);
            return new EntryState(
                Path.GetRelativePath(root, path),
                directory,
                file?.Length ?? 0,
                directory
                    ? Directory.GetLastWriteTimeUtc(path).Ticks
                    : file!.LastWriteTimeUtc.Ticks);
        })
        .OrderBy(item => item.RelativePath, StringComparer.Ordinal)
        .ToArray();

static string FindParentDirectory(string? supplied)
{
    if (!string.IsNullOrWhiteSpace(supplied))
        return Path.GetFullPath(supplied);
    string root = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "Spyro Editor",
        "Projects",
        "default-project",
        "_research",
        "unusedlevel65blank",
        "v1",
        "materialized-single-sector-substrate-schema7-candidates");
    if (!Directory.Exists(root))
        throw new DirectoryNotFoundException(
            "The schema-7 candidate root is missing: " + root);
    string[] candidates = Directory.EnumerateDirectories(
            root,
            "remote-pad-grid.x384-y352-schema7-single-sector-substrate-*",
            SearchOption.TopDirectoryOnly)
        .Where(path => File.Exists(Path.Combine(
            path,
            Id65AuthoringSingleSectorSubstrateRuntimeCandidatePublisher
                .CandidateReceiptFileName)))
        .ToArray();
    if (candidates.Length != 1)
        throw new InvalidDataException(
            "Expected exactly one focused schema-7 sibling candidate.");
    return candidates[0];
}

static string FindRepositoryRoot(string? explicitRoot)
{
    if (!string.IsNullOrWhiteSpace(explicitRoot))
        return Path.GetFullPath(explicitRoot);
    DirectoryInfo? current = new(AppContext.BaseDirectory);
    while (current is not null)
    {
        if (File.Exists(Path.Combine(
                current.FullName,
                "src",
                "Spyro.Editor.Core",
                "Spyro.Editor.Core.csproj")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException(
        "Could not locate the repository root.");
}

internal sealed record FileState(long Length, long LastWriteTicks);
internal sealed record EntryState(
    string RelativePath,
    bool Directory,
    long Length,
    long LastWriteTicks);
