using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Spyro.Editor.Core.Exporting;
using Spyro.Editor.Core.Persistence;

if (!OperatingSystem.IsMacOS())
    throw new PlatformNotSupportedException(
        "The schema-3 Darwin materializer smoke requires macOS.");

bool emitStatePinsOnly =
    args.Contains("--emit-state-pins-only", StringComparer.Ordinal);
bool emitStatePins = emitStatePinsOnly ||
    args.Contains("--emit-state-pins", StringComparer.Ordinal);
string repositoryRoot = FindRepositoryRoot(
    args.FirstOrDefault(item => !item.StartsWith("--", StringComparison.Ordinal)));
string lockedImagePath = Path.Combine(
    repositoryRoot,
    "_local",
    "id65-authoring-aggregate-workspace",
    "_research",
    "unusedlevel65blank",
    "v1",
    "locked-base",
    "unusedlevel65blank-locked-base-v1.bin");
string lockedCuePath = Path.ChangeExtension(lockedImagePath, ".cue");
Require(File.Exists(lockedImagePath) && File.Exists(lockedCuePath),
    "Missing exact schema-3 materializer fixture.");
FileState lockedBefore = SnapshotFile(lockedImagePath);
FileState cueBefore = SnapshotFile(lockedCuePath);
IReadOnlyList<string> directoryBefore =
    SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!);
string smokeRoot = Path.Combine(
    repositoryRoot,
    "_local",
    "Spyro.Editor.Id65AuthoringRemotePadAggregateCandidateMaterializerSmoke-" +
        Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(smokeRoot);

try
{
    UnusedLevel65BlankLevelLabWorkspacePaths paths =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateWorkspacePaths(
            Path.Combine(smokeRoot, "workspace-container"));
    Directory.CreateDirectory(paths.LockedBaseDirectoryPath);
    Directory.CreateDirectory(paths.AuthoredEditsDirectoryPath);
    File.Copy(lockedImagePath, paths.LockedBaseImagePath, overwrite: false);
    await File.WriteAllTextAsync(
        paths.LockedBaseCuePath,
        DiscImage.BuildCueText(lockedCuePath, Path.GetFileName(paths.LockedBaseImagePath)),
        Encoding.ASCII);
    UnusedLevel65BlankLevelLabManifest manifest =
        UnusedLevel65BlankLevelLabProfileRegistry.CreateManifest(paths);
    await File.WriteAllTextAsync(
        paths.ManifestPath,
        JsonSerializer.Serialize(manifest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter() }
        }) + "\n",
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    Require(
        await UnusedLevel65BlankLevelLabBootstrapper
            .ValidatePublishedWorkspaceReadOnlyAsync(paths) == manifest,
        "The schema-3 materializer smoke workspace did not validate.");
    FileState workspaceImageBefore = SnapshotFile(paths.LockedBaseImagePath);
    FileState workspaceCueBefore = SnapshotFile(paths.LockedBaseCuePath);
    FileState workspaceManifestBefore = SnapshotFile(paths.ManifestPath);

    string? priorEnvelope = null;
    int materialized = 0;
    int positiveMaterializations = 0;
    int failClosedRejects = 0;
    int faultRejects = 0;
    int tamperRejects = 0;
    foreach (bool terrainOn in new[] { false, true })
        foreach (bool t107On in new[] { false, true })
            foreach (Id65RemotePadGrassT107Position position in
                     Id65RemotePadGrassT107Catalog.Positions)
            {
                Id65AuthoringEditorDocument document =
                    Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                        position.PositionId,
                        includeOptionalTerrain: terrainOn,
                        placementIds: t107On
                            ? [Id65RemotePadGrassT107Catalog.PlacementId]
                            : [],
                        mandatoryBaseFaceTextureId: 66);
                Id65AuthoringEditorDocumentStoreResult positionStored =
                    await Id65AuthoringEditorDocumentStore.SaveAsync(
                        paths,
                        manifest,
                        document,
                        new Id65AuthoringEditorDocumentProjectionInputs(null),
                        priorEnvelope);
                priorEnvelope = positionStored.EnvelopeCanonicalSha256;
                Id65AuthoringRemotePadAggregateCandidateMaterializationResult result =
                    await Id65AuthoringRemotePadAggregateCandidateMaterializer
                        .MaterializeAsync(new(paths, manifest, positionStored));
                Id65AuthoringRemotePadAggregateCandidateStateFrozenPin statePin =
                    Id65AuthoringRemotePadAggregateCandidateMaterializer
                        .GetRequiredStateFrozenPin(terrainOn, t107On, position.PositionId);
                Require(result.Receipt.PositionId == position.PositionId &&
                        result.Plan.Placement.PositionId == position.PositionId &&
                        result.Plan.StructuralTerrainPresent == terrainOn &&
                        result.Plan.GrassT107Present == t107On &&
                        result.Plan.Placement.Present == t107On &&
                        result.Receipt.OutputImageSha256 == statePin.OutputImageSha256 &&
                        result.Receipt.ChangedPhysicalImageBytes ==
                            statePin.ChangedPhysicalImageBytes &&
                        result.Receipt.ChangedRawSectorCount ==
                            statePin.ChangedRawSectorCount &&
                        result.Receipt.PhysicalDiffSha256 == statePin.PhysicalDiffSha256 &&
                        result.Receipt.ReceiptContentSha256 ==
                            statePin.ReceiptContentSha256 &&
                        result.CandidateArtifactsWrittenThisCall &&
                        !result.ReusedExistingCandidate &&
                        result.WritesDiscImage && result.WritesBin && result.WritesCue &&
                        File.Exists(result.Paths.CandidateImagePath) &&
                        File.Exists(result.Paths.CandidateCuePath) &&
                        File.Exists(result.Paths.CandidateReceiptPath),
                    "The schema-3 materializer did not publish exact position " +
                    position.PositionId + ".");
                if (emitStatePins && !(terrainOn && t107On))
                {
                    Console.WriteLine(string.Join('|',
                    [
                        "STATE_CANDIDATE_PIN",
                terrainOn ? "true" : "false",
                t107On ? "true" : "false",
                position.PositionId,
                result.Receipt.OutputImageSha256,
                result.Receipt.ChangedPhysicalImageBytes.ToString(),
                result.Receipt.ChangedRawSectorCount.ToString(),
                result.Receipt.PhysicalDiffSha256,
                result.Receipt.ReceiptContentSha256
                    ]));
                }
                materialized++;
                positiveMaterializations++;
                Directory.Delete(result.Paths.CandidateDirectoryPath, recursive: true);
            }
    Require(materialized == 36,
        "The schema-3 materializer pin matrix did not cover all 36 exact states.");
    if (emitStatePinsOnly)
    {
        Console.WriteLine(
            "ID65_REMOTE_PAD_CANDIDATE_STATE_PIN_GENERATION_OK pins=27 states=36");
        return;
    }

    Id65AuthoringEditorDocument defaultDocument =
        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
            Id65RemotePadGrassT107Catalog.DefaultPositionId,
            includeOptionalTerrain: true,
            placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
            mandatoryBaseFaceTextureId: 66);
    Id65AuthoringEditorDocumentStoreResult stored =
        await Id65AuthoringEditorDocumentStore.SaveAsync(
            paths,
            manifest,
            defaultDocument,
            new Id65AuthoringEditorDocumentProjectionInputs(null),
            priorEnvelope);
    priorEnvelope = stored.EnvelopeCanonicalSha256;
    Id65AuthoringRemotePadAggregateCandidateMaterializationRequest request =
        new(paths, manifest, stored);

    IReadOnlyList<string> beforeEarlyRejects = SnapshotDirectory(paths.RootPath);
    await ExpectRejectedAsync(
        "legacy document profile",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer.MaterializeAsync(
            request with
            {
                ExpectedStoredDocument = stored with
                {
                    Document = Id65AuthoringEditorDocument.CreateExact()
                }
            }),
        "schema 3, optional terrain");
    failClosedRejects++;
    await ExpectRejectedAsync(
        "schema-3 T88 on",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer.MaterializeAsync(
            request with
            {
                ExpectedStoredDocument = stored with
                {
                    Document = Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                        Id65RemotePadGrassT107Catalog.DefaultPositionId,
                        includeOptionalTerrain: true,
                        placementIds:
                        [
                            Id65AuthoringMobyDependencyBundleCompiler.T88PlacementId,
                            Id65RemotePadGrassT107Catalog.PlacementId
                        ],
                        mandatoryBaseFaceTextureId: 66)
                }
            }),
        "T88 OFF");
    failClosedRejects++;
    string otherPositionId = Id65RemotePadGrassT107Catalog.Positions
        .Select(item => item.PositionId)
        .First(item => item != Id65RemotePadGrassT107Catalog.DefaultPositionId);
    await ExpectRejectedAsync(
        "stale cross-position expected store",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer.MaterializeAsync(
            request with
            {
                ExpectedStoredDocument = stored with
                {
                    Document = Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                        otherPositionId,
                        includeOptionalTerrain: true,
                        placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
                        mandatoryBaseFaceTextureId: 66)
                }
            }),
        "persisted ID65 editor document changed");
    failClosedRejects++;
    Require(SnapshotDirectory(paths.RootPath).SequenceEqual(beforeEarlyRejects),
        "An early schema/store rejection changed workspace state.");

    await ExpectRejectedAsync(
        "smoke-only entrypoint without a fault",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadAggregateCandidateFaultPoint.None),
        "requires an injected failure point");
    failClosedRejects++;

    string materializedRoot = Path.Combine(
        paths.RootPath,
        "materialized-remote-pad-candidates");
    string operationsRoot = Path.Combine(
        paths.RootPath,
        ".remote-pad-candidate-operations");
    Id65AuthoringRemotePadAggregateCandidateFaultPoint[] prepublishFaults =
        Enum.GetValues<Id65AuthoringRemotePadAggregateCandidateFaultPoint>()
            .Where(item =>
                item is not Id65AuthoringRemotePadAggregateCandidateFaultPoint.None and
                not Id65AuthoringRemotePadAggregateCandidateFaultPoint
                    .AfterAtomicRenameBeforeCleanup)
            .ToArray();
    Require(prepublishFaults.Length == 7,
        "The schema-3 materializer prepublication fault matrix changed.");
    foreach (Id65AuthoringRemotePadAggregateCandidateFaultPoint fault in
             prepublishFaults)
    {
        await ExpectRejectedAsync(
            "injected prepublication fault " + fault,
            () => Id65AuthoringRemotePadAggregateCandidateMaterializer
                .MaterializeForSmokeAsync(request, fault),
            "Injected aggregate candidate materialization fault");
        faultRejects++;
        Require(
            (!Directory.Exists(materializedRoot) ||
             !Directory.EnumerateDirectories(materializedRoot).Any()) &&
            (!Directory.Exists(operationsRoot) ||
             !Directory.EnumerateFileSystemEntries(operationsRoot).Any()),
            "A prepublication fault exposed a candidate or retained owned residue.");
    }

    Id65AuthoringEditorDocumentStoreResult? inCallMutation = null;
    await ExpectRejectedAsync(
        "in-call document mutation before publication",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadAggregateCandidateFaultPoint
                    .AfterStagedReadbackBeforePrepublishAudit,
                async () =>
                {
                    inCallMutation = await Id65AuthoringEditorDocumentStore.SaveAsync(
                        paths,
                        manifest,
                        Id65AuthoringEditorDocument.CreateRemotePadGridExact(
                            otherPositionId,
                            includeOptionalTerrain: true,
                            placementIds: [Id65RemotePadGrassT107Catalog.PlacementId],
                            mandatoryBaseFaceTextureId: 66),
                        new Id65AuthoringEditorDocumentProjectionInputs(null),
                        priorEnvelope);
                }),
        "persisted ID65 editor document changed");
    failClosedRejects++;
    Require(inCallMutation is not null &&
            (!Directory.Exists(materializedRoot) ||
             !Directory.EnumerateDirectories(materializedRoot).Any()) &&
            (!Directory.Exists(operationsRoot) ||
             !Directory.EnumerateFileSystemEntries(operationsRoot).Any()),
        "The in-call store mutation was not rejected cleanly.");
    stored = await Id65AuthoringEditorDocumentStore.SaveAsync(
        paths,
        manifest,
        defaultDocument,
        new Id65AuthoringEditorDocumentProjectionInputs(null),
        inCallMutation!.EnvelopeCanonicalSha256);
    priorEnvelope = stored.EnvelopeCanonicalSha256;
    request = request with { ExpectedStoredDocument = stored };

    Id65AuthoringRemotePadAggregateCandidateMaterializationResult first =
        await Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(first, reused: false, filesystemMutated: true,
        candidateWritten: true, renamedThisCall: true);
    Id65AuthoringRemotePadAggregateCandidateFrozenPin frozen =
        Id65AuthoringRemotePadAggregateCandidateMaterializer
            .GetRequiredFrozenPin(first.Receipt.PositionId);
    Require(first.Receipt.OutputImageSha256 == frozen.OutputImageSha256 &&
            first.Receipt.ChangedPhysicalImageBytes ==
                frozen.ChangedPhysicalImageBytes &&
            first.Receipt.ChangedRawSectorCount == frozen.ChangedRawSectorCount &&
            first.Receipt.PhysicalDiffSha256 == frozen.PhysicalDiffSha256 &&
            first.Receipt.ReceiptContentSha256 == frozen.ReceiptContentSha256,
        "The default materialization did not match its frozen physical contract.");
    FileState imageBeforeReuse = SnapshotFile(first.Paths.CandidateImagePath);
    FileState candidateCueBeforeReuse = SnapshotFile(first.Paths.CandidateCuePath);
    FileState receiptBeforeReuse = SnapshotFile(first.Paths.CandidateReceiptPath);
    Id65AuthoringRemotePadAggregateCandidateMaterializationResult reused =
        await Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(reused, reused: true, filesystemMutated: false,
        candidateWritten: false, renamedThisCall: false);
    Require(reused.Receipt == first.Receipt &&
            SnapshotFile(first.Paths.CandidateImagePath) == imageBeforeReuse &&
            SnapshotFile(first.Paths.CandidateCuePath) == candidateCueBeforeReuse &&
            SnapshotFile(first.Paths.CandidateReceiptPath) == receiptBeforeReuse,
        "Exact candidate reuse changed the immutable artifacts.");

    byte[] exactReceipt = await File.ReadAllBytesAsync(
        first.Paths.CandidateReceiptPath);
    byte[] tamperedReceipt = exactReceipt.ToArray();
    tamperedReceipt[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, tamperedReceipt);
    await ExpectRejectedAsync(
        "tampered receipt reuse",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "receipt");
    tamperRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateReceiptPath, exactReceipt);

    byte[] exactCue = await File.ReadAllBytesAsync(first.Paths.CandidateCuePath);
    byte[] tamperedCue = exactCue.ToArray();
    tamperedCue[^2] ^= 1;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, tamperedCue);
    await ExpectRejectedAsync(
        "tampered cue reuse",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "CUE");
    tamperRejects++;
    await File.WriteAllBytesAsync(first.Paths.CandidateCuePath, exactCue);

    long imageTamperOffset = new FileInfo(first.Paths.CandidateImagePath).Length - 1;
    byte exactImageByte = ToggleByte(first.Paths.CandidateImagePath, imageTamperOffset);
    await ExpectRejectedAsync(
        "tampered image reuse",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request),
        "outside WAD80");
    tamperRejects++;
    WriteByte(first.Paths.CandidateImagePath, imageTamperOffset, exactImageByte);
    Id65AuthoringRemotePadAggregateCandidateMaterializationResult restoredReuse =
        await Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(restoredReuse, reused: true, filesystemMutated: false,
        candidateWritten: false, renamedThisCall: false);

    Directory.Delete(first.Paths.CandidateDirectoryPath, recursive: true);
    await ExpectRejectedAsync(
        "post-rename pre-cleanup fault",
        () => Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeForSmokeAsync(
                request,
                Id65AuthoringRemotePadAggregateCandidateFaultPoint
                    .AfterAtomicRenameBeforeCleanup),
        "Injected aggregate candidate materialization fault");
    faultRejects++;
    Require(Directory.Exists(first.Paths.CandidateDirectoryPath) &&
            Directory.Exists(operationsRoot) &&
            Directory.EnumerateDirectories(operationsRoot).Count() == 1,
        "The post-rename fault did not leave one recoverable publication journal.");
    Id65AuthoringRemotePadAggregateCandidateMaterializationResult recovered =
        await Id65AuthoringRemotePadAggregateCandidateMaterializer
            .MaterializeAsync(request);
    positiveMaterializations++;
    AssertResult(recovered, reused: true, filesystemMutated: true,
        candidateWritten: false, renamedThisCall: false);
    Require(recovered.Receipt == first.Receipt &&
            !Directory.EnumerateFileSystemEntries(operationsRoot).Any(),
        "Post-rename recovery changed the candidate or retained its journal.");

    Id65AuthoringRemotePadAggregateCandidateWorkspaceResult workspaceResult =
        await Id65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer
            .MaterializeExactAsync(paths, manifest);
    positiveMaterializations++;
    Require(workspaceResult.ReusedExistingCandidate &&
            !workspaceResult.CandidateArtifactsWrittenThisCall &&
            !workspaceResult.FileSystemMutationPerformed &&
            workspaceResult.CandidateDirectoryPath ==
                recovered.Paths.CandidateDirectoryPath &&
            workspaceResult.DocumentEnvelopeSha256 ==
                recovered.Receipt.DocumentEnvelopeSha256 &&
            workspaceResult.DocumentCanonicalSha256 ==
                recovered.Receipt.DocumentCanonicalSha256 &&
            workspaceResult.StaticAuthoringProjectionSha256 ==
                recovered.Receipt.StaticAuthoringProjectionSha256 &&
            workspaceResult.NativeInputSetSha256 ==
                recovered.Receipt.NativeInputSetSha256 &&
            workspaceResult.AggregatePlanSha256 ==
                recovered.Receipt.AggregatePlanSha256 &&
            workspaceResult.OutputImageSha256 ==
                recovered.Receipt.OutputImageSha256 &&
            workspaceResult.ReceiptContentSha256 ==
                recovered.Receipt.ReceiptContentSha256 &&
            workspaceResult.PositionId == recovered.Receipt.PositionId &&
            workspaceResult.StructuralTerrainPresent &&
            workspaceResult.GrassT107Present &&
            workspaceResult.ExactStoreReloadVerified &&
            workspaceResult.ExactStaticAuthoringProjectionReloadVerified &&
            workspaceResult.ExactNativeInputGenerationRecaptured &&
            workspaceResult.Schema3CompilerPinsVerified &&
            workspaceResult.T107PositionReadbackVerified &&
            workspaceResult.T88ExcludedVerified &&
            workspaceResult.RepairedT13995SupportVerified &&
            workspaceResult.RequiresExactRemotePadSchema3Document &&
            workspaceResult.RequiresFocusedRuntimeValidation &&
            !workspaceResult.CarriesAppOrUiAuthority &&
            !workspaceResult.CarriesCreateBinAuthority &&
            !workspaceResult.NormalRetailCreateBinComposed &&
            !workspaceResult.RuntimeCandidateAuthorized &&
            !workspaceResult.RuntimeProven &&
            !workspaceResult.ReleaseIntegrated &&
            !workspaceResult.PromotionAuthorized &&
            !workspaceResult.Publishable,
        "The public schema-3 workspace bridge lost its exact offline boundary.");

    Require(SnapshotFile(paths.LockedBaseImagePath) == workspaceImageBefore &&
            SnapshotFile(paths.LockedBaseCuePath) == workspaceCueBefore &&
            SnapshotFile(paths.ManifestPath) == workspaceManifestBefore,
        "The schema-3 materializer changed a workspace source fixture.");
    VerifyApiAndTopology(repositoryRoot);
    Console.WriteLine(
        "ID65_AUTHORING_REMOTE_PAD_AGGREGATE_CANDIDATE_MATERIALIZER_SMOKE_OK " +
        $"positions={materialized} positive={positiveMaterializations} " +
        $"rejects={failClosedRejects} faultRejects={faultRejects} " +
        $"tamperRejects={tamperRejects} image={first.Receipt.OutputImageSha256} " +
        $"physical={first.Receipt.ChangedPhysicalImageBytes} " +
        $"changedSectors={first.Receipt.ChangedRawSectorCount} " +
        $"receipt={first.Receipt.ReceiptContentSha256}");
}
finally
{
    if (Directory.Exists(smokeRoot))
        Directory.Delete(smokeRoot, recursive: true);
}

Require(SnapshotFile(lockedImagePath) == lockedBefore &&
        SnapshotFile(lockedCuePath) == cueBefore &&
        SnapshotDirectory(Path.GetDirectoryName(lockedImagePath)!)
            .SequenceEqual(directoryBefore),
    "The schema-3 materializer smoke changed its locked fixture.");

static void AssertResult(
    Id65AuthoringRemotePadAggregateCandidateMaterializationResult result,
    bool reused,
    bool filesystemMutated,
    bool candidateWritten,
    bool renamedThisCall)
{
    Require(result.ReusedExistingCandidate == reused &&
            result.CandidateArtifactsWrittenThisCall == candidateWritten &&
            result.FileSystemMutationPerformed == filesystemMutated &&
            result.WritesDiscImage == candidateWritten &&
            result.WritesBin == candidateWritten &&
            result.WritesCue == candidateWritten &&
            result.AtomicNoReplaceRenameCompletedThisCall == renamedThisCall &&
            result.VerifiedAtomicNoReplacePublicationProvenance &&
            result.OwnedTemporaryResidueRemoved &&
            !result.FinderHandoffPerformed && !result.AppIntegrated &&
            !result.UiIntegrated && !result.CreateBinEnabled &&
            !result.NormalCreateBinEnabled &&
            !result.RuntimeCandidateAuthorized && !result.RuntimeProven &&
            !result.ReleaseIntegrated && !result.PromotionAuthorized &&
            !result.Publishable && !result.CurrentOnDiskFreshnessVerified &&
            !result.HostileFilesystemRaceSafe &&
            result.RequiresTrustedExclusiveWorkspaceDirectory &&
            !result.CommonWorkspaceLeaseOwned &&
            File.Exists(result.Paths.CandidateImagePath) &&
            File.Exists(result.Paths.CandidateCuePath) &&
            File.Exists(result.Paths.CandidateReceiptPath),
        "The schema-3 result overclaimed an unproven integration or runtime boundary.");
    Id65AuthoringRemotePadAggregateCandidateReceipt receipt = result.Receipt;
    Require(receipt.ExactStoreReloadVerified &&
            receipt.ExactStaticAuthoringProjectionReloadVerified &&
            receipt.ExactNativeInputGenerationRecaptured &&
            receipt.ExactLockedBaseRecaptured &&
            receipt.Schema3CompilerPinsVerified &&
            receipt.T107PositionReadbackVerified && receipt.T88ExcludedVerified &&
            receipt.RepairedT13995SupportVerified &&
            receipt.ExactRow80ReadbackVerified &&
            receipt.ExactInverseReadbackVerified &&
            receipt.ProtectedComplementVerified &&
            receipt.RetailTownSquarePreserved && receipt.TargetOverlayPreserved &&
            receipt.ExecutablePreserved && receipt.RawSectorIntegrityVerified &&
            receipt.PhysicalComplementVerified &&
            receipt.SourceLockedBasePreserved &&
            receipt.AtomicNoReplaceRenameCompleted &&
            receipt.ContainsFullDiscImage && receipt.ContainsCue &&
            receipt.WorkspaceCandidateWriterAuthorized &&
            !receipt.RuntimeCandidateAuthorized && !receipt.RuntimeProven &&
            !receipt.SaveAuthorized && !receipt.SavePersistenceProven &&
            !receipt.TreasureTotal200Proven && !receipt.MusicLongPlayProven &&
            !receipt.ExitDestination65Proven && !receipt.AppIntegrated &&
            !receipt.UiIntegrated && !receipt.CreateBinEnabled &&
            !receipt.NormalCreateBinEnabled &&
            !receipt.RetailLevelMutationAuthorized &&
            !receipt.ReleaseIntegrated && !receipt.ReleaseAuthorized &&
            !receipt.PromotionAuthorized && !receipt.Publishable &&
            !receipt.CurrentOnDiskFreshnessVerified &&
            !receipt.HostileFilesystemRaceSafe &&
            receipt.RequiresTrustedExclusiveWorkspaceDirectory &&
            !receipt.CommonWorkspaceLeaseOwned,
        "The schema-3 receipt overclaimed an unproven capability.");
}

static void VerifyApiAndTopology(string repositoryRoot)
{
    MethodInfo[] publicMethods =
        typeof(Id65AuthoringRemotePadAggregateCandidateMaterializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
    Require(publicMethods.Length == 1 &&
            publicMethods[0].Name == "MaterializeAsync" &&
            publicMethods[0].GetParameters()
                .Select(item => item.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(Id65AuthoringRemotePadAggregateCandidateMaterializationRequest),
                    typeof(CancellationToken)
                }),
        "The schema-3 materializer public API surface changed.");

    PropertyInfo[] requestProperties =
        typeof(Id65AuthoringRemotePadAggregateCandidateMaterializationRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);
    Require(requestProperties.Select(item => item.Name).SequenceEqual(new[]
            {
                "Paths",
                "ExpectedManifest",
                "ExpectedStoredDocument"
            }) &&
            requestProperties.All(item =>
                item.PropertyType != typeof(byte[]) &&
                item.PropertyType != typeof(Id65AuthoringRemotePadAggregatePlan)),
        "The schema-3 materializer request stopped requiring store/manifest identity only.");
    MethodInfo[] workspaceFacadeMethods =
        typeof(Id65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer)
            .GetMethods(BindingFlags.Public | BindingFlags.Static |
                BindingFlags.DeclaredOnly);
    Require(workspaceFacadeMethods.Length == 1 &&
            workspaceFacadeMethods[0].Name == "MaterializeExactAsync" &&
            workspaceFacadeMethods[0].GetParameters()
                .Select(item => item.ParameterType)
                .SequenceEqual(new[]
                {
                    typeof(UnusedLevel65BlankLevelLabWorkspacePaths),
                    typeof(UnusedLevel65BlankLevelLabManifest),
                    typeof(CancellationToken)
                }),
        "The schema-3 workspace materializer public API surface changed.");

    string sourceRoot = Path.Combine(repositoryRoot, "src");
    string materializerRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringRemotePadAggregateCandidateMaterializer.cs");
    string smokeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Id65AuthoringRemotePadAggregateCandidateMaterializerSmoke",
        "Program.cs");
    string workspaceFacadeRelative = Path.Combine(
        "src",
        "Spyro.Editor.Core",
        "Persistence",
        "Id65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer.cs");
    string appRelative = Path.Combine(
        "src",
        "Spyro.Editor.App",
        "Views",
        "MainWindow.Id65BlankLab.cs");
    List<(string Relative, string Text)> sources = Directory
        .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
        .Where(path => !path.Split(Path.DirectorySeparatorChar)
            .Any(segment => segment is "bin" or "obj"))
        .Select(path => (
            Path.GetRelativePath(repositoryRoot, path),
            File.ReadAllText(path)))
        .ToList();
    Regex declaration = new(
        @"\binternal\s+static\s+class\s+Id65AuthoringRemotePadAggregateCandidateMaterializer\b",
        RegexOptions.CultureInvariant);
    Require(sources.Sum(item => declaration.Matches(item.Text).Count) == 1 &&
            declaration.IsMatch(sources.Single(item =>
                item.Relative == materializerRelative).Text),
        "The schema-3 materializer declaration topology changed.");
    Regex call = new(
        @"\bId65AuthoringRemotePadAggregateCandidateMaterializer\s*\.\s*Materialize(?:ForSmoke)?Async\s*\(",
        RegexOptions.CultureInvariant);
    string[] callers = sources
        .Where(item => call.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(callers.SequenceEqual(new[]
        {
            workspaceFacadeRelative,
            smokeRelative
        }),
        "The isolated schema-3 candidate writer gained an unauthorized caller: " +
        string.Join(", ", callers));
    Regex facadeCall = new(
        @"\bId65AuthoringRemotePadAggregateCandidateWorkspaceMaterializer\s*\.\s*MaterializeExactAsync\s*\(",
        RegexOptions.CultureInvariant);
    string[] facadeCallers = sources
        .Where(item => facadeCall.IsMatch(item.Text))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(facadeCallers.SequenceEqual(new[]
        {
            appRelative,
            smokeRelative
        }),
        "The schema-3 workspace facade gained an unauthorized caller: " +
        string.Join(", ", facadeCallers));
    string[] appReferences = sources
        .Where(item => item.Relative.StartsWith(
            Path.Combine("src", "Spyro.Editor.App") +
                Path.DirectorySeparatorChar,
            StringComparison.Ordinal) &&
            item.Text.Contains(
                "Id65AuthoringRemotePadAggregateCandidate",
                StringComparison.Ordinal))
        .Select(item => item.Relative)
        .Order(StringComparer.Ordinal)
        .ToArray();
    Require(appReferences.SequenceEqual(new[] { appRelative }),
        "The schema-3 candidate UI route escaped its one explicit App boundary: " +
        string.Join(", ", appReferences));
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
        exception is InvalidDataException or InvalidOperationException or
            IOException or NotSupportedException)
    {
        captured = exception;
    }
    Require(captured is not null &&
            captured.ToString().Contains(
                requiredText,
                StringComparison.OrdinalIgnoreCase),
        $"The negative case '{label}' was accepted or returned the wrong refusal: " +
        captured);
}

static byte ToggleByte(string path, long offset)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
    stream.Position = offset;
    int value = stream.ReadByte();
    Require(value >= 0, "The candidate image tamper probe reached end of file.");
    stream.Position = offset;
    stream.WriteByte((byte)(value ^ 1));
    stream.Flush(flushToDisk: true);
    return (byte)value;
}

static void WriteByte(string path, long offset, byte value)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.Write, FileShare.None);
    stream.Position = offset;
    stream.WriteByte(value);
    stream.Flush(flushToDisk: true);
}

static string FindRepositoryRoot(string? explicitRoot)
{
    string currentPath = Path.GetFullPath(explicitRoot ?? Directory.GetCurrentDirectory());
    DirectoryInfo? current = new(currentPath);
    while (current is not null)
    {
        if (Directory.Exists(Path.Combine(current.FullName, ".git")) &&
            Directory.Exists(Path.Combine(current.FullName, "src", "Spyro.Editor.Core")))
        {
            return current.FullName;
        }
        current = current.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository.");
}

static FileState SnapshotFile(string path)
{
    FileInfo file = new(path);
    using FileStream stream = File.OpenRead(path);
    return new(
        file.Length,
        file.LastWriteTimeUtc.Ticks,
        Convert.ToHexStringLower(SHA256.HashData(stream)));
}

static IReadOnlyList<string> SnapshotDirectory(string path) =>
    Directory.EnumerateFileSystemEntries(path)
        .Select(Path.GetFileName)
        .Where(name => name is not null)
        .Select(name => name!)
        .Order(StringComparer.Ordinal)
        .ToArray();

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record FileState(
    long ByteLength,
    long LastWriteUtcTicks,
    string Sha256);
