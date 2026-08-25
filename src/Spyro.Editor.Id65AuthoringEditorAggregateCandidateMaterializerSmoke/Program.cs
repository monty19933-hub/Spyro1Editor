using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

const string SmokeRootPrefix =
    "Spyro.Editor.Id65AuthoringEditorAggregateCandidateMaterializerSmoke-";

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-display-name",
    "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
string foundationImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "v5-stone-hill-level-replacement",
    "unused-level-65-full-authoring-foundation-native-membership",
    "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin");
foreach (string fixture in new[] { lockedImagePath, lockedCuePath, foundationImagePath })
    Require(File.Exists(fixture), $"Missing aggregate materializer fixture: {fixture}");

FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState lockedCueBefore = SnapshotFile(lockedCuePath);
FileState foundationBefore = SnapshotFile(foundationImagePath);
IReadOnlyList<string> lockedDirectoryBefore = SnapshotDirectory(
    Path.GetDirectoryName(lockedImagePath)!);
IReadOnlyList<string> foundationDirectoryBefore = SnapshotDirectory(
    Path.GetDirectoryName(foundationImagePath)!);

string smokeRoot = Path.Combine(
    Path.GetTempPath(),
    SmokeRootPrefix + Guid.NewGuid().ToString("N"));
string workspaceContainer = Path.Combine(smokeRoot, "workspace-container");
Directory.CreateDirectory(smokeRoot);

int positiveMaterializations = 0;
int failClosedRejects = 0;
int atomicFaultRejects = 0;
int shapeRejects = 0;
int reprojectionRejects = 0;

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(workspaceContainer);
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(lockedImagePath, paths.LockedBaseImagePath, overwrite: false);
    string lockedCueText = DiscImage.BuildCueText(
        lockedCuePath,
        Path.GetFileName(paths.LockedBaseImagePath));
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        lockedCueText,
        Encoding.ASCII);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    JsonSerializerOptions manifestOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, manifestOptions) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    UnusedLevel65BlankLevelLabManifest validated =
        await UnusedLevel65BlankLevelLabBootstrapper.ValidatePublishedWorkspaceReadOnlyAsync(paths);
    Require(validated == manifest, "The aggregate materializer smoke lab did not validate exactly.");
    FileState labLockedImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState labLockedCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState labManifestBefore = SnapshotFile(paths.ManifestPath);
    byte[] labLockedCueBytes = await File.ReadAllBytesAsync(paths.LockedBaseCuePath);

    UnusedLevel65NativeTextureStaticPlan oldTextureProof =
        UnusedLevel65NativeTextureAuthoringContract.BuildFirstStaticPlan(foundationImagePath);
    Id65V2NativeTextureWitness textureWitness =
        Id65V2NativeTextureCompositionCompiler.ImportPinnedT66Witness(oldTextureProof);
    UnusedLevel65MobyDependencyBundleContract mobyContract =
        UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(
            foundationImagePath);
    byte[] package = ReadRootFile(foundationImagePath, "WAD.WAD", 0x9C1D20, 0x174);
    Id65MobyDependencyBundleDescriptor grassBundle =
        Id65AuthoringMobyDependencyBundleCompiler.CreateArtisansGrassBundle(
            mobyContract,
            package);
    Id65AuthoringEditorAggregateBoundSource boundSource =
        Id65AuthoringEditorAggregateBoundSource.Capture(paths.LockedBaseImagePath);
    Id65AuthoringEditorDocumentProjectionInputs projectionInputs = new(
        boundSource.LockedSource,
        textureWitness,
        grassBundle);
    Id65AuthoringEditorDocument document = Id65AuthoringEditorDocument.CreateExact();
    Id65AuthoringEditorDocumentStoreResult stored =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            document,
            projectionInputs,
            expectedPriorEnvelopeCanonicalSha256: null);
    Id65AuthoringEditorAggregateCandidateMaterializationRequest request = new(
        paths,
        manifest,
        stored,
        textureWitness,
        grassBundle);

    await ExpectRejectedAsync(
        "smoke-only entrypoint without a fault",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringEditorAggregateCandidateFaultPoint.None),
        requiredText: "requires an injected failure point");
    failClosedRejects++;

    string materializedRoot = Path.Combine(paths.RootPath, "materialized-candidates");
    string operationsRoot = Path.Combine(paths.RootPath, ".aggregate-candidate-operations");
    Id65AuthoringEditorAggregateCandidateFaultPoint[] prepublishFaults =
        Enum.GetValues<Id65AuthoringEditorAggregateCandidateFaultPoint>()
            .Where(item => item is not Id65AuthoringEditorAggregateCandidateFaultPoint.None and
                not Id65AuthoringEditorAggregateCandidateFaultPoint.AfterAtomicRenameBeforeCleanup)
            .ToArray();
    Require(prepublishFaults.Length == 7,
        $"The aggregate materializer prepublish fault enum changed to {prepublishFaults.Length} cases.");
    foreach (Id65AuthoringEditorAggregateCandidateFaultPoint fault in prepublishFaults)
    {
        await ExpectRejectedAsync(
            $"prepublish injected fault {fault}",
            async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer
                .MaterializeForSmokeAsync(request, fault),
            requiredText: "Injected aggregate candidate materialization fault");
        atomicFaultRejects++;
        Require(!Directory.Exists(materializedRoot) ||
                !Directory.EnumerateDirectories(materializedRoot).Any(),
            $"The prepublish materializer fault {fault} exposed a candidate directory.");
        Require(!Directory.Exists(operationsRoot) ||
                !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
            $"The prepublish materializer fault {fault} retained owned operation residue.");
        Require(SnapshotFile(paths.LockedBaseImagePath) == labLockedImageBefore &&
                SnapshotFile(paths.LockedBaseCuePath) == labLockedCueBefore &&
                SnapshotFile(paths.ManifestPath) == labManifestBefore,
            $"The prepublish materializer fault {fault} changed a locked lab fixture.");
    }

    Id65AuthoringEditorDocumentStoreResult? inCallMutatedStore = null;
    await ExpectRejectedAsync(
        "in-call store mutation before prepublish audit",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringEditorAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit,
                async () =>
                {
                    inCallMutatedStore = await Id65AuthoringEditorDocumentStore.SaveAsync(
                        paths,
                        manifest,
                        Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false),
                        projectionInputs,
                        stored.EnvelopeCanonicalSha256);
                }),
        requiredText: "persisted ID65 editor document changed");
    reprojectionRejects++;
    Require(inCallMutatedStore is not null &&
            (!Directory.Exists(materializedRoot) ||
                !Directory.EnumerateDirectories(materializedRoot).Any()) &&
            (!Directory.Exists(operationsRoot) ||
                !Directory.EnumerateFileSystemEntries(operationsRoot).Any()),
        "The in-call store mutation was not performed or left published/operation residue.");
    stored = await Id65AuthoringEditorDocumentStore.SaveAsync(
        paths,
        manifest,
        document,
        projectionInputs,
        inCallMutatedStore!.EnvelopeCanonicalSha256);
    request = request with { ExpectedStoredDocument = stored };

    Id65AuthoringEditorAggregateCandidateMaterializationResult first =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        first,
        reused: false,
        filesystemMutated: true,
        candidateWritten: true,
        renamedThisCall: true);
    Require(first.Plan.DeterministicAggregatePlanSha256 ==
            Id65AuthoringEditorAggregatePlanProjector.ExpectedDeterministicAggregatePlanSha256 &&
        first.Receipt.OutputRow80Sha256 ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedOutputRow80Sha256 &&
        first.Receipt.FirstRebuiltRawSectorLba == 53_906 &&
        first.Receipt.RebuiltRawSectorCount == 1_476 &&
        first.Receipt.ChangedLogicalWadBytes ==
            Id65AuthoringCompositeCompiler.ExpectedCombinedChangedByteCount &&
        first.Receipt.OutputImageSha256 ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedOutputImageSha256 &&
        first.Receipt.CandidateCueSha256 ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedCandidateCueSha256 &&
        first.Receipt.ChangedRawSectorCount ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedChangedRawSectorCount &&
        first.Receipt.ChangedPhysicalImageBytes ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedChangedPhysicalImageBytes &&
        first.Receipt.PhysicalDiffSha256 ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedPhysicalDiffSha256 &&
        first.Receipt.ReceiptContentSha256 ==
            Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedReceiptContentSha256,
        "The aggregate materializer receipt lost an exact physical or logical pin.");
    byte[] independentCandidateRow80 = ReadRootFile(
        first.Paths.CandidateImagePath,
        "WAD.WAD",
        Id65AuthoringEditorAggregateCandidateMaterializer.TargetDataWadOffset,
        Id65AuthoringEditorAggregateCandidateMaterializer.TargetDataByteLength);
    byte[] independentCandidateWad16 = ReadRootFile(
        first.Paths.CandidateImagePath,
        "WAD.WAD",
        first.Plan.Destination.RejectedRetailDataWadOffset,
        Id65AuthoringEditorAggregateCandidateMaterializer.TargetDataByteLength);
    byte[] independentSourceWad16 = ReadRootFile(
        paths.LockedBaseImagePath,
        "WAD.WAD",
        first.Plan.Destination.RejectedRetailDataWadOffset,
        Id65AuthoringEditorAggregateCandidateMaterializer.TargetDataByteLength);
    Require(HashBytes(independentCandidateRow80) == first.Plan.OutputRow80Sha256 &&
            independentCandidateWad16.SequenceEqual(independentSourceWad16),
        "Independent candidate WAD80/WAD16 readback did not preserve the aggregate/retail split.");
    FileState candidateBeforeReuse = SnapshotFile(first.Paths.CandidateImagePath);
    FileState cueBeforeReuse = SnapshotFile(first.Paths.CandidateCuePath);
    FileState receiptBeforeReuse = SnapshotFile(first.Paths.CandidateReceiptPath);

    Id65AuthoringEditorAggregateCandidateMaterializationResult reused =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        reused,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);
    Require(reused.Receipt == first.Receipt &&
            SnapshotFile(reused.Paths.CandidateImagePath) == candidateBeforeReuse &&
            SnapshotFile(reused.Paths.CandidateCuePath) == cueBeforeReuse &&
            SnapshotFile(reused.Paths.CandidateReceiptPath) == receiptBeforeReuse,
        "Exact content-addressed candidate reuse changed a published artifact.");

    await File.AppendAllTextAsync(
        paths.LockedBaseCuePath,
        "REM benign source CUE comment must not alter candidate identity\n",
        Encoding.ASCII);
    Id65AuthoringEditorAggregateCandidateMaterializationResult cueVariantReuse =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        cueVariantReuse,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);
    Require(cueVariantReuse.Receipt == first.Receipt &&
            SnapshotFile(cueVariantReuse.Paths.CandidateImagePath) == candidateBeforeReuse &&
            SnapshotFile(cueVariantReuse.Paths.CandidateCuePath) == cueBeforeReuse &&
            SnapshotFile(cueVariantReuse.Paths.CandidateReceiptPath) == receiptBeforeReuse,
        "A semantically valid locked-base CUE variant changed the content-addressed candidate.");
    await File.WriteAllBytesAsync(paths.LockedBaseCuePath, labLockedCueBytes);
    File.SetLastWriteTimeUtc(paths.LockedBaseCuePath, labLockedCueBefore.LastWriteUtc);

    Id65AuthoringEditorDocumentStoreResult noTerrain =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            Id65AuthoringEditorDocument.CreateExact(includeOptionalTerrain: false),
            projectionInputs,
            stored.EnvelopeCanonicalSha256);
    await ExpectRejectedAsync(
        "stale expected store result",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "persisted ID65 editor document changed");
    failClosedRejects++;
    await ExpectRejectedAsync(
        "matching no-terrain store",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(
            request with { ExpectedStoredDocument = noTerrain }),
        requiredText: "requires one genuine full-document store readback");
    shapeRejects++;
    string t88PlacementId = document.MobyPlacements
        .Single(item => item.TargetTrueIndex == 88)
        .Id;
    string t107PlacementId = document.MobyPlacements
        .Single(item => item.TargetTrueIndex == 107)
        .Id;
    Id65AuthoringEditorDocumentStoreResult t88Only =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            Id65AuthoringEditorDocument.CreateExact(placementIds: [t88PlacementId]),
            projectionInputs,
            noTerrain.EnvelopeCanonicalSha256);
    await ExpectRejectedAsync(
        "matching T88-only store",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(
            request with { ExpectedStoredDocument = t88Only }),
        requiredText: "requires one genuine full-document store readback");
    shapeRejects++;
    Id65AuthoringEditorDocumentStoreResult t107Only =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            Id65AuthoringEditorDocument.CreateExact(placementIds: [t107PlacementId]),
            projectionInputs,
            t88Only.EnvelopeCanonicalSha256);
    await ExpectRejectedAsync(
        "matching T107-only store",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(
            request with { ExpectedStoredDocument = t107Only }),
        requiredText: "requires one genuine full-document store readback");
    shapeRejects++;
    stored = await Id65AuthoringEditorDocumentStore.SaveAsync(
        paths,
        manifest,
        document,
        projectionInputs,
        t107Only.EnvelopeCanonicalSha256);
    request = request with { ExpectedStoredDocument = stored };
    Id65AuthoringEditorAggregateCandidateMaterializationResult restoredReuse =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        restoredReuse,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);
    Require(restoredReuse.Receipt == first.Receipt,
        "Restoring the exact canonical full document changed its content-addressed candidate receipt.");

    string unknownOperation = Path.Combine(operationsRoot, "not-an-owned-operation");
    Directory.CreateDirectory(unknownOperation);
    await File.WriteAllTextAsync(Path.Combine(unknownOperation, "sentinel.txt"), "preserve\n");
    await ExpectRejectedAsync(
        "unknown operation residue",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "unknown reserved entry");
    failClosedRejects++;
    Require(File.Exists(Path.Combine(unknownOperation, "sentinel.txt")),
        "Unknown aggregate operation residue was deleted instead of preserved.");
    Directory.Delete(unknownOperation, recursive: true);

    byte[] exactReceiptBytes = await File.ReadAllBytesAsync(first.Paths.CandidateReceiptPath);
    byte[] tamperedReceiptBytes = exactReceiptBytes.ToArray();
    tamperedReceiptBytes[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, tamperedReceiptBytes);
    await ExpectRejectedAsync(
        "tampered existing receipt",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "receipt");
    failClosedRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, exactReceiptBytes);
    File.SetLastWriteTimeUtc(first.Paths.CandidateReceiptPath, receiptBeforeReuse.LastWriteUtc);

    byte[] exactCandidateCueBytes = await File.ReadAllBytesAsync(first.Paths.CandidateCuePath);
    byte[] tamperedCandidateCueBytes = exactCandidateCueBytes.ToArray();
    tamperedCandidateCueBytes[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, tamperedCandidateCueBytes);
    await ExpectRejectedAsync(
        "tampered existing CUE",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "CUE");
    failClosedRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, exactCandidateCueBytes);
    File.SetLastWriteTimeUtc(first.Paths.CandidateCuePath, cueBeforeReuse.LastWriteUtc);

    string unexpectedCandidateEntry = Path.Combine(
        first.Paths.CandidateDirectoryPath,
        "unexpected-sentinel.txt");
    await File.WriteAllTextAsync(unexpectedCandidateEntry, "preserve-until-refusal\n");
    await ExpectRejectedAsync(
        "unexpected existing candidate entry",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "unexpected entry set");
    failClosedRejects++;
    Require(File.Exists(unexpectedCandidateEntry),
        "The materializer deleted an unexpected candidate entry instead of failing closed.");
    File.Delete(unexpectedCandidateEntry);

    long retailWadOffset = first.Plan.Destination.RejectedRetailDataWadOffset;
    long retailPhysicalOffset =
        ((Id65AuthoringEditorAggregateCandidateMaterializer.WadLba +
            (retailWadOffset / 2048)) * 2352L) + 24 + (retailWadOffset % 2048);
    byte retailOriginal;
    using (FileStream candidateWrite = new(
               first.Paths.CandidateImagePath,
               FileMode.Open,
               FileAccess.ReadWrite,
               FileShare.None))
    {
        candidateWrite.Position = retailPhysicalOffset;
        int value = candidateWrite.ReadByte();
        Require(value >= 0, "Could not read the candidate WAD16 tamper byte.");
        retailOriginal = checked((byte)value);
        candidateWrite.Position = retailPhysicalOffset;
        candidateWrite.WriteByte((byte)(retailOriginal ^ 1));
        candidateWrite.Flush(flushToDisk: true);
    }
    await ExpectRejectedAsync(
        "tampered existing retail WAD16 byte",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request),
        requiredText: "retail Town Square WAD16");
    failClosedRejects++;
    using (FileStream candidateRestore = new(
               first.Paths.CandidateImagePath,
               FileMode.Open,
               FileAccess.Write,
               FileShare.None))
    {
        candidateRestore.Position = retailPhysicalOffset;
        candidateRestore.WriteByte(retailOriginal);
        candidateRestore.Flush(flushToDisk: true);
    }
    File.SetLastWriteTimeUtc(first.Paths.CandidateImagePath, candidateBeforeReuse.LastWriteUtc);
    Id65AuthoringEditorAggregateCandidateMaterializationResult repairedReuse =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        repairedReuse,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);
    Require(SnapshotFile(first.Paths.CandidateImagePath) == candidateBeforeReuse &&
            SnapshotFile(first.Paths.CandidateCuePath) == cueBeforeReuse &&
            SnapshotFile(first.Paths.CandidateReceiptPath) == receiptBeforeReuse,
        "Exact restoration after candidate tamper tests changed the published artifacts.");

    using (FileStream heldWriterLease = new(
               first.Paths.WriterLeasePath,
               FileMode.Open,
               FileAccess.ReadWrite,
               FileShare.None))
    {
        await ExpectRejectedAsync(
            "exclusive writer lease",
            async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer
                .MaterializeAsync(request),
            requiredText: "owns the workspace writer lease");
        failClosedRejects++;
    }
    Require(SnapshotFile(first.Paths.CandidateImagePath) == candidateBeforeReuse,
        "A competing writer-lease refusal changed the candidate image.");

    File.Delete(first.Paths.WriterLeasePath);
    Directory.Delete(operationsRoot);
    Id65AuthoringEditorAggregateCandidateMaterializationResult repairedBoundaryReuse =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        repairedBoundaryReuse,
        reused: true,
        filesystemMutated: true,
        candidateWritten: false,
        renamedThisCall: false);
    Require(SnapshotFile(first.Paths.CandidateImagePath) == candidateBeforeReuse,
        "Recreating the fixed operation/lease boundary changed the candidate image.");

    Id65AuthoringEditorAggregateCandidateMaterializationResult cleanBoundaryReuse =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        cleanBoundaryReuse,
        reused: true,
        filesystemMutated: false,
        candidateWritten: false,
        renamedThisCall: false);

    string partialBootstrapOperation = Path.Combine(
        operationsRoot,
        "materialize-11111111111111111111111111111111");
    Directory.CreateDirectory(Path.Combine(partialBootstrapOperation, "candidate"));
    await File.WriteAllTextAsync(
        Path.Combine(partialBootstrapOperation, "operation-journal.json.tmp"),
        "{partial-owned-bootstrap\n");
    Id65AuthoringEditorAggregateCandidateMaterializationResult partialRecovery =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        partialRecovery,
        reused: true,
        filesystemMutated: true,
        candidateWritten: false,
        renamedThisCall: false);
    Require(!Directory.Exists(partialBootstrapOperation),
        "A recognized partial bootstrap operation was not recovered.");

    string stalePlanSha256 = new('1', 64);
    string priorPlanOperation = Path.Combine(
        operationsRoot,
        "materialize-22222222222222222222222222222222");
    Directory.CreateDirectory(priorPlanOperation);
    string priorCandidateDirectoryName =
        $"aggregate-{stalePlanSha256}-receipt-" +
        Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedReceiptContentSha256;
    await File.WriteAllTextAsync(
        Path.Combine(priorPlanOperation, "operation-journal.json"),
        JsonSerializer.Serialize(
            new
            {
                SchemaVersion = 1,
                OperationKind = "id65-authoring-editor-aggregate-candidate-materialization",
                AggregatePlanSha256 = stalePlanSha256,
                CandidateReceiptContentSha256 =
                    Id65AuthoringEditorAggregateCandidateMaterializer.ExpectedReceiptContentSha256,
                CandidateDirectoryName = priorCandidateDirectoryName,
                Phase = "staging"
            },
            manifestOptions) + "\n");
    Id65AuthoringEditorAggregateCandidateMaterializationResult priorPlanRecovery =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        priorPlanRecovery,
        reused: true,
        filesystemMutated: true,
        candidateWritten: false,
        renamedThisCall: false);
    Require(!Directory.Exists(priorPlanOperation),
        "A canonical owned operation from a prior document plan wedged recovery.");

    Require(Path.GetFullPath(first.Paths.CandidateDirectoryPath).StartsWith(
            Path.GetFullPath(smokeRoot) + Path.DirectorySeparatorChar,
            StringComparison.Ordinal),
        "The post-rename recovery test candidate escaped its temporary smoke root.");
    Directory.Delete(first.Paths.CandidateDirectoryPath, recursive: true);
    await ExpectRejectedAsync(
        "post-rename pre-cleanup injected fault",
        async () => _ = await Id65AuthoringEditorAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringEditorAggregateCandidateFaultPoint.AfterAtomicRenameBeforeCleanup),
        requiredText: "Injected aggregate candidate materialization fault");
    atomicFaultRejects++;
    Require(Directory.Exists(first.Paths.CandidateDirectoryPath) &&
            Directory.EnumerateDirectories(operationsRoot).Count() == 1,
        "The post-rename fault did not preserve exactly one published candidate plus recovery journal.");
    Id65AuthoringEditorAggregateCandidateMaterializationResult recoveredPublication =
        await Id65AuthoringEditorAggregateCandidateMaterializer.MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(
        recoveredPublication,
        reused: true,
        filesystemMutated: true,
        candidateWritten: false,
        renamedThisCall: false);
    Require(recoveredPublication.Receipt == first.Receipt &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any() &&
            SnapshotFile(recoveredPublication.Paths.CandidateImagePath).Sha256 ==
                candidateBeforeReuse.Sha256 &&
            SnapshotFile(recoveredPublication.Paths.CandidateCuePath).Sha256 ==
                cueBeforeReuse.Sha256 &&
            SnapshotFile(recoveredPublication.Paths.CandidateReceiptPath).Sha256 ==
                receiptBeforeReuse.Sha256,
        "Post-rename recovery changed candidate contents or retained its owned journal.");

    VerifyApiAndTopology(repositoryRoot);
    Require(positiveMaterializations == 10,
        $"The aggregate materializer positive matrix ran {positiveMaterializations}, expected 10.");
    Require(failClosedRejects == 8,
        $"The aggregate materializer rejection matrix ran {failClosedRejects}, expected 8.");
    Require(shapeRejects == 3,
        $"The aggregate materializer shape matrix ran {shapeRejects}, expected 3.");
    Require(reprojectionRejects == 1,
        $"The aggregate materializer reprojection matrix ran {reprojectionRejects}, expected 1.");
    Require(atomicFaultRejects == 8,
        $"The aggregate materializer fault matrix ran {atomicFaultRejects}, expected 8.");
    Require(SnapshotFile(paths.LockedBaseImagePath) == labLockedImageBefore &&
            SnapshotFile(paths.LockedBaseCuePath) == labLockedCueBefore &&
            SnapshotFile(paths.ManifestPath) == labManifestBefore &&
            SnapshotFile(lockedImagePath) == lockedBefore &&
            SnapshotFile(lockedCuePath) == lockedCueBefore &&
            SnapshotFile(foundationImagePath) == foundationBefore &&
            SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
                .SequenceEqual(lockedDirectoryBefore) &&
            SnapshotDirectory(Path.GetDirectoryName(foundationImagePath)!)
                .SequenceEqual(foundationDirectoryBefore),
        "The aggregate materializer changed a locked source, donor, or fixture directory.");

    Console.WriteLine(
        "ID65_AUTHORING_EDITOR_AGGREGATE_CANDIDATE_MATERIALIZER_SMOKE_OK " +
        $"positive={positiveMaterializations} rejects={failClosedRejects} " +
        $"shapeRejects={shapeRejects} reprojectionRejects={reprojectionRejects} " +
        $"faultRejects={atomicFaultRejects} " +
        $"image={first.Receipt.OutputImageSha256} " +
        $"row80={first.Receipt.OutputRow80Sha256} logical={first.Receipt.ChangedLogicalWadBytes} " +
        $"physical={first.Receipt.ChangedPhysicalImageBytes} " +
        $"changedSectors={first.Receipt.ChangedRawSectorCount} " +
        $"rebuiltSectors={first.Receipt.RebuiltRawSectorCount} " +
        $"physicalDiff={first.Receipt.PhysicalDiffSha256} " +
        $"receipt={first.Receipt.ReceiptContentSha256}");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
    Require(!Directory.Exists(smokeRoot), "The aggregate materializer smoke retained temporary game data.");
}

static void AssertResult(
    Id65AuthoringEditorAggregateCandidateMaterializationResult result,
    bool reused,
    bool filesystemMutated,
    bool candidateWritten,
    bool renamedThisCall)
{
    Require(result.ReusedExistingCandidate == reused &&
            result.CandidateArtifactsWrittenThisCall == candidateWritten &&
            result.FileSystemMutationPerformed == filesystemMutated &&
            result.WritesDiscImage == candidateWritten && result.WritesBin == candidateWritten &&
            result.WritesCue == candidateWritten &&
            result.AtomicNoReplaceRenameCompletedThisCall == renamedThisCall &&
            result.VerifiedAtomicNoReplacePublicationProvenance &&
            result.OwnedTemporaryResidueRemoved && !result.FinderHandoffPerformed &&
            !result.AppIntegrated && !result.UiIntegrated && !result.CreateBinEnabled &&
            !result.NormalCreateBinEnabled && !result.RuntimeCandidateAuthorized &&
            !result.RuntimeProven && !result.ReleaseIntegrated && !result.PromotionAuthorized &&
            !result.Publishable && !result.CurrentOnDiskFreshnessVerified &&
            !result.HostileFilesystemRaceSafe &&
            result.RequiresTrustedExclusiveWorkspaceDirectory &&
            !result.CommonWorkspaceLeaseOwned &&
            File.Exists(result.Paths.CandidateImagePath) &&
            File.Exists(result.Paths.CandidateCuePath) &&
            File.Exists(result.Paths.CandidateReceiptPath),
        "The aggregate materializer result overclaimed a production, runtime, or filesystem boundary.");
    Id65AuthoringEditorAggregateCandidateReceipt receipt = result.Receipt;
    Require(receipt.ExactStoreReloadVerified && receipt.ExactLockedBaseRecaptured &&
            receipt.ExactRow80ReadbackVerified && receipt.ExactInverseReadbackVerified &&
            receipt.ProtectedComplementVerified && receipt.RetailTownSquarePreserved &&
            receipt.TargetOverlayPreserved && receipt.ExecutablePreserved &&
            receipt.RawSectorIntegrityVerified && receipt.PhysicalComplementVerified &&
            receipt.SourceLockedBasePreserved && receipt.AtomicNoReplaceRenameCompleted &&
            receipt.ContainsFullDiscImage && receipt.ContainsCue &&
            receipt.WorkspaceCandidateWriterAuthorized && !receipt.RuntimeCandidateAuthorized &&
            !receipt.RuntimeProven && !receipt.SaveAuthorized &&
            !receipt.SavePersistenceProven && !receipt.TreasureTotal200Proven &&
            !receipt.MusicLongPlayProven && !receipt.ExitDestination65Proven &&
            !receipt.AppIntegrated && !receipt.UiIntegrated && !receipt.CreateBinEnabled &&
            !receipt.NormalCreateBinEnabled && !receipt.RetailLevelMutationAuthorized &&
            !receipt.ReleaseIntegrated && !receipt.ReleaseAuthorized &&
            !receipt.PromotionAuthorized && !receipt.Publishable &&
            !receipt.CurrentOnDiskFreshnessVerified && !receipt.HostileFilesystemRaceSafe &&
            receipt.RequiresTrustedExclusiveWorkspaceDirectory &&
            !receipt.CommonWorkspaceLeaseOwned,
        "The aggregate candidate receipt overclaimed an unproven capability.");
}

static void VerifyApiAndTopology(string repositoryRoot)
{
    MethodInfo[] publicMethods = typeof(Id65AuthoringEditorAggregateCandidateMaterializer)
        .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly);
    Require(publicMethods.Length == 1 && publicMethods[0].Name == "MaterializeAsync" &&
            publicMethods[0].GetParameters().Select(item => item.ParameterType).SequenceEqual(new[]
            {
                typeof(Id65AuthoringEditorAggregateCandidateMaterializationRequest),
                typeof(CancellationToken)
            }),
        "The aggregate materializer gained a public API beyond MaterializeAsync(request, cancellation).");
    RequireExactPropertyShape(
        typeof(Id65AuthoringEditorAggregateCandidateMaterializationRequest),
        [
            ("Paths", typeof(UnusedLevel65BlankLevelLabWorkspacePaths)),
            ("ExpectedManifest", typeof(UnusedLevel65BlankLevelLabManifest)),
            ("ExpectedStoredDocument", typeof(Id65AuthoringEditorDocumentStoreResult)),
            ("PrivateTextureWitness", typeof(Id65V2NativeTextureWitness)),
            ("GrassBundle", typeof(Id65MobyDependencyBundleDescriptor))
        ]);
    Require(typeof(Id65AuthoringEditorAggregateCandidateMaterializationRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .All(property => property.PropertyType != typeof(byte[]) &&
                !typeof(Stream).IsAssignableFrom(property.PropertyType) &&
                !typeof(Delegate).IsAssignableFrom(property.PropertyType) &&
                !property.Name.Contains("Output", StringComparison.OrdinalIgnoreCase) &&
                property.PropertyType != typeof(Id65AuthoringEditorAggregatePlan) &&
                property.PropertyType != typeof(Id65CompiledAuthoringComposite)),
        "The aggregate materializer request gained raw bytes, outputs, delegates, or compiled afterimages.");
    RequireExactBooleanProperties(
        typeof(Id65AuthoringEditorAggregateCandidateMaterializationResult),
        [
            "ReusedExistingCandidate",
            "CandidateArtifactsWrittenThisCall",
            "FileSystemMutationPerformed",
            "WritesDiscImage",
            "WritesBin",
            "WritesCue",
            "AtomicNoReplaceRenameCompletedThisCall",
            "VerifiedAtomicNoReplacePublicationProvenance",
            "OwnedTemporaryResidueRemoved",
            "FinderHandoffPerformed",
            "AppIntegrated",
            "UiIntegrated",
            "CreateBinEnabled",
            "NormalCreateBinEnabled",
            "RuntimeCandidateAuthorized",
            "RuntimeProven",
            "ReleaseIntegrated",
            "PromotionAuthorized",
            "Publishable",
            "CurrentOnDiskFreshnessVerified",
            "HostileFilesystemRaceSafe",
            "RequiresTrustedExclusiveWorkspaceDirectory",
            "CommonWorkspaceLeaseOwned"
        ]);
    RequireExactBooleanProperties(
        typeof(Id65AuthoringEditorAggregateCandidateReceipt),
        [
            "ExactStoreReloadVerified",
            "ExactLockedBaseRecaptured",
            "ExactRow80ReadbackVerified",
            "ExactInverseReadbackVerified",
            "ProtectedComplementVerified",
            "RetailTownSquarePreserved",
            "TargetOverlayPreserved",
            "ExecutablePreserved",
            "RawSectorIntegrityVerified",
            "PhysicalComplementVerified",
            "SourceLockedBasePreserved",
            "AtomicNoReplaceRenameCompleted",
            "ContainsFullDiscImage",
            "ContainsCue",
            "WorkspaceCandidateWriterAuthorized",
            "RuntimeCandidateAuthorized",
            "RuntimeProven",
            "SaveAuthorized",
            "SavePersistenceProven",
            "TreasureTotal200Proven",
            "MusicLongPlayProven",
            "ExitDestination65Proven",
            "AppIntegrated",
            "UiIntegrated",
            "CreateBinEnabled",
            "NormalCreateBinEnabled",
            "RetailLevelMutationAuthorized",
            "ReleaseIntegrated",
            "ReleaseAuthorized",
            "PromotionAuthorized",
            "Publishable",
            "CurrentOnDiskFreshnessVerified",
            "HostileFilesystemRaceSafe",
            "RequiresTrustedExclusiveWorkspaceDirectory",
            "CommonWorkspaceLeaseOwned"
        ]);

    string[] files = Directory.EnumerateFiles(
            Path.Combine(repositoryRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
        .Where(path => !HasPathSegment(path, "bin") && !HasPathSegment(path, "obj"))
        .ToArray();
    Regex declaration = new(
        @"\binternal\s+static\s+class\s+Id65AuthoringEditorAggregateCandidateMaterializer\b",
        RegexOptions.CultureInvariant);
    Regex call = new(
        @"\bId65AuthoringEditorAggregateCandidateMaterializer\s*\.\s*MaterializeAsync\s*\(",
        RegexOptions.CultureInvariant);
    Regex smokeCall = new(
        @"\bId65AuthoringEditorAggregateCandidateMaterializer\s*\.\s*MaterializeForSmokeAsync\s*\(",
        RegexOptions.CultureInvariant);
    Regex qualifiedMember = new(
        @"\bId65AuthoringEditorAggregateCandidateMaterializer\s*\.\s*(?:MaterializeAsync|MaterializeForSmokeAsync)\b",
        RegexOptions.CultureInvariant);
    string[] declarations = files.Where(path => declaration.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] calls = files.Where(path => call.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] smokeCalls = files.Where(path => smokeCall.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] qualifiedMemberOwners = files
        .Where(path => qualifiedMember.IsMatch(File.ReadAllText(path)))
        .Select(path => Path.GetRelativePath(repositoryRoot, path))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string smokeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Id65AuthoringEditorAggregateCandidateMaterializerSmoke",
        "Program.cs");
    Require(declarations.SequenceEqual(new[]
        {
            Path.Combine("src", "Spyro.Editor.Core", "Persistence",
                "Id65AuthoringEditorAggregateCandidateMaterializer.cs")
        }) && calls.SequenceEqual(new[]
        {
            Path.Combine("src",
                "Spyro.Editor.Id65AuthoringEditorAggregateCandidateMaterializerSmoke",
                "Program.cs")
        }) && smokeCalls.SequenceEqual(new[]
        {
            smokeRelative
        }) && qualifiedMemberOwners.SequenceEqual(new[] { smokeRelative }),
        $"Aggregate materializer topology changed: defs={string.Join(',', declarations)}; " +
        $"calls={string.Join(',', calls)}; smokeCalls={string.Join(',', smokeCalls)}; " +
        $"members={string.Join(',', qualifiedMemberOwners)}.");

    Regex usingStaticMaterializer = new(
        @"\busing\s+static\s+(?:(?:global::)?Spyro\.Editor\.Core\.Persistence\.)?Id65AuthoringEditorAggregateCandidateMaterializer\s*;",
        RegexOptions.CultureInvariant);
    Regex materializerAlias = new(
        @"\busing\s+[A-Za-z_][A-Za-z0-9_]*\s*=\s*(?:(?:global::)?Spyro\.Editor\.Core\.Persistence\.)?Id65AuthoringEditorAggregateCandidateMaterializer\s*;",
        RegexOptions.CultureInvariant);
    Regex materializerTypeOf = new(
        @"\btypeof\s*\(\s*(?:(?:global::)?Spyro\.Editor\.Core\.Persistence\.)?Id65AuthoringEditorAggregateCandidateMaterializer\s*\)",
        RegexOptions.CultureInvariant);
    Regex lateBoundKeyword = new(@"\bdynam[i]c\b", RegexOptions.CultureInvariant);
    Regex singularMethodLookup = new(@"\bGetMeth[o]d\s*\(", RegexOptions.CultureInvariant);
    Regex lateBoundInvoke = new(
        @"(?:\.\s*Invok[e]\s*\(|\bInvokeMembe[r]\s*\(|\bDynamicInvok[e]\s*\(|\bCreateDelegat[e]\s*\()",
        RegexOptions.CultureInvariant);
    foreach (string path in files)
    {
        string relative = Path.GetRelativePath(repositoryRoot, path);
        string sourceText = File.ReadAllText(path);
        if (!sourceText.Contains(
                "Id65AuthoringEditorAggregateCandidateMaterializer",
                StringComparison.Ordinal))
        {
            continue;
        }
        Require(!usingStaticMaterializer.IsMatch(sourceText) &&
                !materializerAlias.IsMatch(sourceText) &&
                !lateBoundKeyword.IsMatch(sourceText) &&
                !singularMethodLookup.IsMatch(sourceText) &&
                !lateBoundInvoke.IsMatch(sourceText),
            $"The aggregate materializer acquired an alias, using-static, late-bound, or reflection call path in {relative}.");
        int typeOfCount = materializerTypeOf.Matches(sourceText).Count;
        Require(typeOfCount == (relative == smokeRelative ? 1 : 0),
            $"The aggregate materializer reflection surface changed in {relative}.");
    }
}

static void RequireExactPropertyShape(
    Type type,
    IReadOnlyList<(string Name, Type PropertyType)> expected)
{
    (string Name, Type PropertyType)[] actual = type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Select(property => (property.Name, property.PropertyType))
        .OrderBy(item => item.Name, StringComparer.Ordinal)
        .ToArray();
    (string Name, Type PropertyType)[] orderedExpected = expected
        .OrderBy(item => item.Name, StringComparer.Ordinal)
        .ToArray();
    Require(actual.SequenceEqual(orderedExpected),
        $"The exact public property shape changed for {type.Name}.");
}

static void RequireExactBooleanProperties(Type type, IReadOnlyList<string> expected)
{
    string[] actual = type
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(property => property.PropertyType == typeof(bool))
        .Select(property => property.Name)
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] orderedExpected = expected.Order(StringComparer.Ordinal).ToArray();
    Require(actual.SequenceEqual(orderedExpected),
        $"The exact public Boolean capability shape changed for {type.Name}: " +
        $"{string.Join(',', actual)}.");
}

static async Task ExpectRejectedAsync(
    string label,
    Func<Task> action,
    string requiredText)
{
    Exception? captured = null;
    try
    {
        await action();
    }
    catch (Exception exception) when (
        exception is InvalidDataException or InvalidOperationException or IOException)
    {
        captured = exception;
    }
    Require(captured is not null &&
            captured.ToString().Contains(requiredText, StringComparison.OrdinalIgnoreCase),
        $"Aggregate materializer negative case '{label}' was accepted or returned the wrong refusal: {captured}");
}

static byte[] ReadRootFile(
    string imagePath,
    string exactName,
    long relativeOffset,
    int byteLength)
{
    DiscLayout layout = DiscImage.DetectLayout(imagePath);
    using FileStream image = File.OpenRead(imagePath);
    DiscFileRecord file = DiscImage.FindRootFileRecord(
        image,
        layout,
        name => string.Equals(name, exactName, StringComparison.OrdinalIgnoreCase));
    if (relativeOffset < 0 || relativeOffset + byteLength > file.Size)
        throw new InvalidDataException("An aggregate materializer smoke source read escaped its root file.");
    return DiscImage.ReadFileBytes(image, layout, file.Lba, relativeOffset, byteLength);
}

static FileState SnapshotFile(string path) =>
    new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), HashFile(path));

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.GetFileSystemEntries(path)
        .Select(entry => $"{Path.GetFileName(entry)}|{new FileInfo(entry).Length}|{File.GetLastWriteTimeUtc(entry):O}")
        .Order(StringComparer.Ordinal)
        .ToArray();

static string HashFile(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexStringLower(SHA256.HashData(stream));
}

static string HashBytes(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexStringLower(SHA256.HashData(bytes));

static bool HasPathSegment(string path, string segment) =>
    path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Contains(segment, StringComparer.OrdinalIgnoreCase);

static string FindRepositoryRoot(string? start)
{
    DirectoryInfo? current = new(Path.GetFullPath(start ?? Directory.GetCurrentDirectory()));
    while (current is not null)
    {
        if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
            return current.FullName;
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(long Length, DateTime LastWriteUtc, string Sha256);
