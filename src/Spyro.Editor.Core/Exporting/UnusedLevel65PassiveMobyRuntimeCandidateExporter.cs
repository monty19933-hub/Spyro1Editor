using System.Buffers.Binary;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Spyro.Editor.Core.Exporting;

internal sealed record UnusedLevel65PassiveMobyRuntimeCandidateRequest(
    string WorkspaceRoot,
    string LockedBaseImagePath,
    string FoundationImagePath,
    string FoundationCuePath,
    string OutputDirectoryPath,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record UnusedLevel65PassiveMobyRuntimeCandidatePaths(
    string PublicationParentPath,
    string OutputDirectoryPath,
    string OperationsDirectoryPath,
    string WriterLeasePath,
    string ImagePath,
    string CuePath,
    string PlanPath,
    string ReceiptPath,
    string ChecklistPath,
    string GuidePath,
    string RevealHelperPath);

internal sealed record UnusedLevel65PassiveMobyPatch(
    string Name,
    long WadOffset,
    string BeforeHex,
    string AfterHex,
    int ChangedByteCount);

internal readonly record struct UnusedLevel65PassiveMobyPoint(int X, int Y, int Z);

internal readonly record struct UnusedLevel65PassiveMobyCollisionCell(int X, int Y, int Z);

internal sealed record UnusedLevel65PassiveMobySupportProof(
    int TerrainSectorIndex,
    int TerrainFaceIndex,
    IReadOnlyList<UnusedLevel65PassiveMobyPoint> NativeFacePoints,
    int CollisionTriangleIndex,
    string CollisionTriangleHex,
    IReadOnlyList<UnusedLevel65PassiveMobyPoint> CollisionPoints,
    int CollisionAssignment,
    int CollisionFlags,
    long CollisionNormalZ,
    UnusedLevel65PassiveMobyCollisionCell DestinationCell,
    IReadOnlyList<long> CollisionLookupWadOffsets,
    int NativeLeftEdgeMarginRaw,
    int NativeDiagonalMarginRaw,
    int FoundationTriangleSeparationRaw,
    int NativeGroundRawZ,
    int PlacementRawX,
    int PlacementRawY,
    int PlacementRawZ,
    int DistanceFromFoundationSpawnRaw,
    int DistanceFromReservedStandaloneSpawnRaw,
    int MinimumSpawnSeparationRaw,
    bool DestinationStrictlyInsideNativeCollision,
    bool DestinationStrictlyOutsideFoundationTriangle,
    bool SupportIsTopmostAtDestination,
    bool TerrainFaceReadbackVerified,
    bool RuntimeEvidenceBoundToExactNativeFan,
    string RuntimeEvidenceSha256);

internal sealed record UnusedLevel65PassiveMobyRuntimeCandidatePlan(
    int SchemaVersion,
    string ProfileId,
    string FoundationImageSha256,
    string LockedBaseImageSha256,
    string ClosedContractProfileId,
    string ClosedContractSha256,
    int LevelId,
    int ContinuousLevelIndex,
    string DonorLevelName,
    int DonorTrueIndex,
    int TargetTrueIndex,
    int ActorId,
    int TargetActorRootIndex,
    IReadOnlyList<UnusedLevel65PassiveMobyPatch> Patches,
    int DonorRawX,
    int DonorRawY,
    int DonorRawZ,
    int AuthoredRawX,
    int AuthoredRawY,
    int AuthoredRawZ,
    int DonorYawByte,
    string SourceRowSha256,
    string SourcePropertiesSha256,
    string SourceActorPackageSha256,
    string DonorDispatchTraceSha256,
    string TargetDispatchTraceSha256,
    UnusedLevel65PassiveMobySupportProof Support,
    IReadOnlyList<int> AffectedRawSectorLbas,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    bool ClosedDependencyContractVerified,
    bool LandingAndPlayerAnchorPreserved,
    bool FoundationTerrainPreserved,
    bool PassiveNoControllerDependencies,
    bool MusicPreserved,
    bool TotalsPreserved,
    bool ExitPreserved,
    bool SaveCodePreserved,
    bool RetailLevelsPreserved,
    bool RequiresDuckStationRuntimeProof,
    bool RuntimePassed,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65PassiveMobyRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record UnusedLevel65PassiveMobyRuntimeCandidateReceipt(
    int SchemaVersion,
    string ProfileId,
    string LockedBaseImagePath,
    string FoundationImagePath,
    string FoundationCuePath,
    string OutputDirectoryPath,
    string OutputImagePath,
    string OutputCuePath,
    string PlanPath,
    string ReceiptPath,
    string ChecklistPath,
    string GuidePath,
    string? FinderHelperPath,
    string LockedBaseImageSha256,
    string FoundationImageSha256,
    string OutputImageSha256,
    string OutputId65DataSha256,
    string OutputCueSha256,
    string PlanSha256,
    string ChecklistSha256,
    string GuideSha256,
    string? FinderHelperSha256,
    IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes,
    long ChangedLogicalWadBytes,
    long ChangedPhysicalImageBytes,
    IReadOnlyList<UnusedLevel65PassiveMobyRawSectorDiff> RawSectorDiffs,
    string RawSectorDiffSha256,
    bool ExactPatchReadbackVerified,
    bool ClosedDependencyContractVerified,
    bool LandingAndPlayerAnchorPreserved,
    bool DestinationSupportVerified,
    bool FoundationUnrelatedBytesPreserved,
    bool ExecutablePreserved,
    bool RetailTownSquarePreserved,
    bool MusicTotalsExitSavePreserved,
    bool RawSectorIntegrityVerified,
    bool AtomicDirectoryPublicationCompleted,
    bool RollbackGuardsEnabled,
    bool StartupRecoveryPerformed,
    bool RuntimePending,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized);

internal sealed record UnusedLevel65PassiveMobyPublishedArtifactHashes(
    string CueSha256,
    string PlanSha256,
    string ReceiptSha256,
    string ChecklistSha256,
    string GuideSha256,
    string? FinderHelperSha256);

internal sealed record UnusedLevel65PassiveMobyRuntimeCandidateResult(
    UnusedLevel65PassiveMobyRuntimeCandidatePaths Paths,
    UnusedLevel65PassiveMobyRuntimeCandidatePlan Plan,
    UnusedLevel65PassiveMobyRuntimeCandidateReceipt Receipt,
    UnusedLevel65PassiveMobyPublishedArtifactHashes ArtifactHashes);

/// <summary>
/// Disposable first passive cross-level Moby gate for ID65. It installs the
/// exact Artisans T121 Grass row, scene properties/fixup, and actor 0x01F5
/// package into the pinned T107/root-37 allocations and places it on the
/// runtime-proven native entry apron. Landing, T92, totals, save, overlays,
/// executable, and every retail level remain byte-identical.
/// </summary>
internal static class UnusedLevel65PassiveMobyRuntimeCandidateExporter
{
    public const string ProfileId =
        "unused-level-65-artisans-grass-01f5-native-apron-clean-usa-disposable-v1";
    public const string OutputDirectoryName = "unused-level-65-passive-moby-artisans-grass";
    public const string OutputPrefix =
        "Unused-Level-65-Artisans-Grass-01F5-Native-Apron-RUNTIME-CANDIDATE";

    public const string FoundationImageSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.FoundationImageSha256;
    public const string LockedBaseImageSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.LockedBaseImageSha256;

    public const string ExpectedOutputImageSha256 =
        "e4bfebdca057b5f90e0aa5fe94b0983e2d7338fd13d82a2cdfbb21d4f07ca081";
    public const string ExpectedOutputId65DataSha256 =
        "75b5d7561d38efd6fe3ac08142f444c181406d80577d3a74c5c33dc329827635";
    public const string ExpectedRawSectorDiffSha256 =
        "28c89ddf46477f5c7151375c46311274607bfd35f5f4754f5fd6f241439f87b8";

    private const int PlanSchemaVersion = 1;
    private const int ReceiptSchemaVersion = 1;
    private const int JournalSchemaVersion = 2;
    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const byte XaDataSubmode = 0x08;
    private const long Id65DataWadOffset = 0x6936800;
    private const int Id65DataByteLength = 0x2E2000;
    private const long Id65OverlayWadOffset = 0x6927000;
    private const int Id65OverlayByteLength = 0xF800;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int RetailTownSquareOverlayByteLength = 0xF800;
    private const long RetailTownSquareDataWadOffset = 0x119E000;
    private const int RetailTownSquareDataByteLength = 0x2E2000;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const string ExecutableSha256 =
        UnusedLevel65StandaloneBehaviorOwnershipInspector.ExecutableSha256;

    private const long ArtisansGrassRowWadOffset = 0x9D6C44;
    private const int MobyRowByteLength = 0x58;
    private const long ArtisansGrassPropertiesWadOffset = 0x9DBACC;
    private const int GrassPropertiesByteLength = 8;
    private const long ArtisansGrassPackageWadOffset = 0x9C1D20;
    private const int GrassPackageByteLength = 0x174;
    private const long Id65ObjectCountWadOffset = 0x6B0696C;
    private const long Id65TargetRowWadOffset = 0x6B08E38;
    private const long Id65FixupCountWadOffset = 0x6B0E728;
    private const long Id65FixupAppendWadOffset = 0x6B0E930;
    private const long Id65PropertiesWadOffset = 0x6B0E938;
    private const long Id65ActorRootSlotWadOffset = 0x69368E4;
    private const long Id65ActorIdSlotWadOffset = 0x693699A;
    private const long Id65ActorPackageWadOffset = 0x6B06244;
    private const int DonorTrueIndex = 121;
    private const int TargetTrueIndex = 107;
    private const int TargetActorRootIndex = 37;
    private const ushort GrassActorId = 0x01F5;
    private const int ObjectCountBefore = 107;
    private const int ObjectCountAfter = 108;
    private const int FixupCountBefore = 0x81;
    private const int FixupCountAfter = 0x82;
    private const uint TargetPropertiesSceneOffset = 0x8138;
    private const uint TargetPointerFieldSceneOffset = 0x2638;
    private const uint TargetActorPackageEntryOffset = 0x1CFA44;
    private const string ZeroRowSha256 =
        "10eef285deef7a4b7c82b22aa53589b7833df29de3814649c772bbd5c832f365";
    private const string GrassRowSha256 =
        "de8450049c0bea92fba8fe4e7f9f9cb749a514d1318dbd83dc9b6b1b18a0b190";
    private const string GrassPropertiesSha256 =
        "9eeeff662fd5b77dbc35de8ed01e0d1fd149cee49126625b69f65553c4b7c20b";
    private const string GrassPackageSha256 =
        "90ca71a190c4567817d728753f25df667d56515e1721d24e19e6da9dc07edcc3";
    private const string ClosedContractSha256 =
        "8ec952c5972d7e2a37e5e89f8ec2be26144635b92b58cba85f745f7d05a76d4f";

    private const long LandingWadOffset = 0x6B06800;
    private const long PlayerAnchorWadOffset = 0x6B08910;
    private const int PlayerAnchorByteLength = 0x58;
    private const int PlayerAnchorTrueIndex = 92;
    private const string BeforeLandingHex = "29E901009A8801006621000000004000";
    private const string BeforePlayerAnchorSha256 =
        "4987c539f3178c555da26e4ec2f6cdc25094a27382b9465c9845846a39679bb2";
    private const int BeforeRawX = 125_225;
    private const int BeforeRawY = 100_506;
    private const int LandingRawZ = 8_550;
    private const int PlayerRawZ = 8_704;
    private const int DonorYawByte = 0x04;

    // Exact scene point (7774, 6394), safely inside native collision T1353 and
    // outside the authored foundation triangle. It is deliberately distinct
    // from both the unchanged foundation spawn and the separately reserved
    // standalone-spawn candidate so the two runtime gates remain composable.
    private const int AuthoredRawX = 124_384;
    private const int AuthoredRawY = 102_304;
    private const int ReservedStandaloneSpawnRawX = 124_585;
    private const int ReservedStandaloneSpawnRawY = 102_954;
    private const int NativeGroundRawZ = 8_192;
    private const int CollisionTriangleIndex = 1_353;
    private const int FoundationCollisionTriangleIndex = 13_995;
    private const long FoundationCollisionComponentWadOffset = 0x6A40DF0;
    private const int CollisionComponentByteLength = 0x5FAE8;
    private const int CollisionTreeRelativeOffset = 4 + 0x1C;
    private const int CollisionTreeByteLength = 0x6A60;
    private const int CollisionBlocksRelativeOffset = 4 + 0x6A7C;
    private const int CollisionBlocksByteLength = 0x17984;
    private const int CollisionTrianglesRelativeOffset = 4 + 0x1E400;
    private const int CollisionAssignmentsRelativeOffset = 4 + 0x58480;
    private const int CollisionFlagsRelativeOffset = 4 + 0x5D1E0;
    private const int CollisionTriangleCount = 19_808;
    private const string ExpectedSupportTriangleHex = "521E20004A1960C000020000";
    private const string ExpectedFoundationTriangleHex = "521E2020CA18004000020080";
    private const string FoundationCollisionSha256 =
        "5e7b4430c9bfbd2793df1d9833d8d3af005924d7c110b66f0b0e1f8bc818c056";
    private const string FoundationCollisionTreeSha256 =
        "0318210317487c34a7c25cf487805a203dbe4b03ee1250141cab05f624ab09dd";
    private const string FoundationCollisionBlocksSha256 =
        "7c414761af050eabc226b81cb4f57e248f2f3b2a43fc28b0fe9bc591532656d4";
    private const string RuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-town-square-authored-terrain-solid-entry-ramp-control-focused-pass-2026-08-09.json";
    private const string RuntimeEvidenceSha256 =
        "3ddfb471fd316c68459c6c09f5d3caa85ab0b134b9720e2f86025aa398b1ea87";

    private const string OperationsDirectoryName = ".unused-level-65-passive-moby-operations";
    private const string WriterLeaseFileName = ".unused-level-65-passive-moby-writer.lease";
    private const string JournalFileName = "operation-journal.json";
    private const string JournalOperationKind = "unused-level-65-passive-moby-publication";
    private const string StagePrefix = "moby-stage-";
    private const string BackupPrefix = "moby-backup-";
    private const string PhaseStaged = "staged";
    private const string PhaseBackupIntent = "backup-intent";
    private const string PhasePreviousBackedUp = "previous-backed-up";
    private const string PhasePublishIntent = "publish-intent";
    private const string PhasePublished = "published";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static UnusedLevel65PassiveMobyRuntimeCandidatePaths CreatePaths(string outputDirectoryPath)
    {
        if (string.IsNullOrWhiteSpace(outputDirectoryPath))
            throw new ArgumentException("The passive-moby output directory is missing.", nameof(outputDirectoryPath));
        string output = Path.GetFullPath(outputDirectoryPath);
        if (!string.Equals(Path.GetFileName(output), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The passive-moby writer owns only a directory named '{OutputDirectoryName}'.");
        }
        string parent = Path.GetDirectoryName(output)
            ?? throw new InvalidOperationException("The passive-moby publication parent is missing.");
        return new(
            parent,
            output,
            Path.Combine(parent, OperationsDirectoryName),
            Path.Combine(parent, WriterLeaseFileName),
            Path.Combine(output, OutputPrefix + ".bin"),
            Path.Combine(output, OutputPrefix + ".cue"),
            Path.Combine(output, OutputPrefix + "-moby-plan.json"),
            Path.Combine(output, OutputPrefix + "-static-readback-receipt.json"),
            Path.Combine(output, OutputPrefix + "-runtime-checklist.md"),
            Path.Combine(output, OutputPrefix + "-location-guide.svg"),
            Path.Combine(output, OutputPrefix + "-Reveal-in-Finder.command"));
    }

    public static async Task<UnusedLevel65PassiveMobyRuntimeCandidateResult> CreateAsync(
        UnusedLevel65PassiveMobyRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        string root = RequireDirectory(request.WorkspaceRoot, "Spyro Editor workspace");
        string lockedBase = RequireFile(request.LockedBaseImagePath, "exact locked ID65 base BIN");
        string foundation = RequireFile(request.FoundationImagePath, "exact ID65 foundation BIN");
        string foundationCue = RequireFile(request.FoundationCuePath, "exact ID65 foundation CUE");
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths = CreatePaths(request.OutputDirectoryPath);
        RequireSafeRoles(lockedBase, foundation, foundationCue, paths);
        Directory.CreateDirectory(paths.PublicationParentPath);

        using FileStream writerLease = AcquireWriterLease(paths.WriterLeasePath);
        request.TestStageHook?.Invoke("after-global-writer-lease-acquired");
        bool rollbackRecoveryVerified = RecoverOwnedOperations(paths);
        request.TestStageHook?.Invoke("after-startup-recovery");
        if (Directory.Exists(paths.OutputDirectoryPath) && !request.ReplaceExistingCandidate)
        {
            throw new InvalidOperationException(
                "The passive-moby runtime candidate already exists. Set ReplaceExistingCandidate only for an intentional deterministic rebuild.");
        }

        NativeLevelReplacementBaselineExporter.ValidateCue(foundationCue, foundation, "MODE2/2352");
        await RequireFileHashAsync(lockedBase, LockedBaseImageSha256, cancellationToken);
        await RequireFileHashAsync(foundation, FoundationImageSha256, cancellationToken);
        UnusedLevel65StandaloneBehaviorOwnershipContract ownership =
            await UnusedLevel65StandaloneBehaviorOwnershipInspector.InspectAsync(
                root,
                lockedBase,
                foundation,
                cancellationToken);
        if (!ownership.StaticReadbackOnly || ownership.PromotionAuthorized ||
            ownership.Spawn.PlayerAnchorTrueIndex != PlayerAnchorTrueIndex)
        {
            throw new InvalidDataException("The committed standalone behavior ownership boundary changed.");
        }

        Prepared prepared = await PrepareAsync(root, lockedBase, foundation, foundationCue, ownership, cancellationToken);
        string operationId = Guid.NewGuid().ToString("N");
        string stage = Path.Combine(paths.OperationsDirectoryPath, StagePrefix + operationId);
        string backup = Path.Combine(paths.OperationsDirectoryPath, BackupPrefix + operationId);
        Journal journal = new(
            JournalSchemaVersion,
            JournalOperationKind,
            operationId,
            paths.OutputDirectoryPath,
            PhaseStaged,
            stage,
            backup,
            Directory.Exists(paths.OutputDirectoryPath));
        bool priorBackedUp = false;
        bool candidatePublished = false;
        try
        {
            Directory.CreateDirectory(stage);
            await BuildStageAsync(
                prepared,
                foundation,
                foundationCue,
                paths,
                stage,
                rollbackRecoveryVerified,
                cancellationToken);
            WriteJournal(paths, journal);
            request.TestStageHook?.Invoke("after-stage-complete");

            if (journal.HadPreviousCandidate)
            {
                journal = journal with { Phase = PhaseBackupIntent };
                WriteJournal(paths, journal);
                Directory.Move(paths.OutputDirectoryPath, backup);
                priorBackedUp = true;
                journal = journal with { Phase = PhasePreviousBackedUp };
                WriteJournal(paths, journal);
                request.TestStageHook?.Invoke("after-previous-candidate-backup");
            }

            journal = journal with { Phase = PhasePublishIntent };
            WriteJournal(paths, journal);
            Directory.Move(stage, paths.OutputDirectoryPath);
            candidatePublished = true;
            journal = journal with { Phase = PhasePublished };
            WriteJournal(paths, journal);
            request.TestStageHook?.Invoke("after-candidate-publication");

            UnusedLevel65PassiveMobyRuntimeCandidateResult result =
                await ReadPublishedResultAsync(paths, cancellationToken);
            if (priorBackedUp)
            {
                DeleteOwnedDirectory(backup, paths.OperationsDirectoryPath, BackupPrefix);
                priorBackedUp = false;
            }
            DeleteJournal(paths);
            return result;
        }
        catch (Exception failure)
        {
            List<Exception> recoveryFailures = [];
            try
            {
                if (candidatePublished && Directory.Exists(paths.OutputDirectoryPath))
                    DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            try
            {
                if (priorBackedUp && Directory.Exists(backup) && !Directory.Exists(paths.OutputDirectoryPath))
                {
                    Directory.Move(backup, paths.OutputDirectoryPath);
                    priorBackedUp = false;
                }
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            try
            {
                if (Directory.Exists(stage))
                    DeleteOwnedDirectory(stage, paths.OperationsDirectoryPath, StagePrefix);
            }
            catch (Exception ex)
            {
                recoveryFailures.Add(ex);
            }
            if (recoveryFailures.Count == 0)
                DeleteJournal(paths);
            if (recoveryFailures.Count > 0)
            {
                throw new IOException(
                    "Passive-Moby publication failed and exact prior-candidate rollback was incomplete.",
                    new AggregateException([failure, .. recoveryFailures]));
            }
            throw;
        }
    }

    private static async Task<Prepared> PrepareAsync(
        string workspaceRoot,
        string lockedBase,
        string foundation,
        string foundationCue,
        UnusedLevel65StandaloneBehaviorOwnershipContract ownership,
        CancellationToken cancellationToken)
    {
        string evidencePath = Path.Combine(workspaceRoot, RuntimeEvidenceRelativePath);
        await RequireFileHashAsync(
            RequireFile(evidencePath, "focused entrance-fan runtime evidence"),
            RuntimeEvidenceSha256,
            cancellationToken);
        using (JsonDocument evidence = JsonDocument.Parse(await File.ReadAllTextAsync(evidencePath, cancellationToken)))
        {
            JsonElement root = evidence.RootElement;
            if (root.GetProperty("evidenceStatus").GetString() != "focused-runtime-pass" ||
                root.GetProperty("evidenceScope").GetString() != "complete-edited-surface-solidity-only" ||
                root.GetProperty("profileId").GetString() !=
                    UnusedLevel65AuthoredTerrainSolidEntryRampControlCandidateExporter.ProfileId ||
                root.GetProperty("promotionAuthorized").GetBoolean() ||
                root.GetProperty("normalCreateBinIntegrationAuthorized").GetBoolean())
            {
                throw new InvalidDataException("The exact focused entrance-fan runtime evidence boundary changed.");
            }
            int[] central = root.GetProperty("exactCandidateStaticBinding")
                .GetProperty("centralCollisionFootprintTriangleIndexes")
                .EnumerateArray()
                .Select(value => value.GetInt32())
                .ToArray();
            if (!central.SequenceEqual(new[] { 1_353, 1_354 }))
                throw new InvalidDataException("The runtime evidence is no longer bound to native collision triangles 1353/1354.");
        }

        UnusedLevel65ConstructionCollidableTerrainPlan construction =
            await UnusedLevel65FullAuthoringConstructionTemplate.BuildFirstCollidableTerrainPlanAsync(
                lockedBase,
                cancellationToken);
        if (!construction.CollisionComposed || !construction.OcclusionOwnershipVerified ||
            construction.CollisionBinding.ReusedTriangleIndex != FoundationCollisionTriangleIndex ||
            construction.PromotionAuthorized || construction.NormalCreateBinEnabled)
        {
            throw new InvalidDataException("The exact static-only foundation composition contract changed.");
        }
        UnusedLevel65MobyDependencyBundleContract closedContract =
            UnusedLevel65MobyDependencyBundleFoundation.InspectFirstCrossLevelDonor(foundation);
        if (closedContract.ProfileId != UnusedLevel65MobyDependencyBundleFoundation.ProfileId ||
            closedContract.DeterministicContractSha256 != ClosedContractSha256 ||
            !closedContract.StructurallyCompleteAllocation ||
            !closedContract.StaticRuntimeDependencyClosureComplete ||
            closedContract.RunnableBundleSupport || !closedContract.StaticInspectionOnly ||
            closedContract.HardRuntimePublicationBlockers.Count != 1 ||
            !closedContract.HardRuntimePublicationBlockers[0].StartsWith("runtime-acceptance: ", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The committed passive-Moby dependency closure changed.");
        }

        DiscLayout layout = DiscImage.DetectLayout(foundation);
        RequireMode2(layout, "foundation");
        using FileStream image = File.OpenRead(foundation);
        DiscFileRecord wad = RequireFileRecord(image, layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord executable = RequireFileRecord(
            image,
            layout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);
        _ = wad;
        byte[] executableBytes = DiscImage.ReadFileBytes(image, layout, executable.Lba, 0, executable.Size);
        RequireHash(executableBytes, ExecutableSha256, "foundation executable");

        byte[] model = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset,
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileByteLength);
        if (!model.SequenceEqual(construction.AfterModelBytes))
            throw new InvalidDataException("The exact foundation model does not equal the committed construction composition.");
        const long sector213WadOffset = 0x6A3E8B4;
        const long authoredHpVertexTableWadOffset = 0x6A3EAD8;
        const long authoredHpFace37WadOffset = 0x6A3F52C;
        int sectorRelative = checked((int)(sector213WadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        int vertexRelative = checked((int)(authoredHpVertexTableWadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        int faceRelative = checked((int)(authoredHpFace37WadOffset -
            UnusedLevel65FullAuthoringConstructionTemplate.ModelSubfileWadOffset));
        byte[] sectorHeader = model.AsSpan(sectorRelative, 28).ToArray();
        byte[] face37 = model.AsSpan(faceRelative, 16).ToArray();
        RequireHex(face37, "283930272D3D352D1C0680FF08F4CFFF", "foundation HP face 213:37");
        if (sectorHeader[20] != 143 || sectorHeader[22] != 114 ||
            !face37.AsSpan(0, 4).SequenceEqual(new byte[] { 40, 57, 48, 39 }))
        {
            throw new InvalidDataException("The foundation sector-213 HP counts/face-37 slots changed.");
        }
        UnusedLevel65PassiveMobyPoint[] nativeFacePoints = face37.AsSpan(0, 4)
            .ToArray()
            .Select(vertexIndex => DecodeSceneVertex(
                model.AsSpan(vertexRelative + (vertexIndex * 4), 4),
                sectorHeader))
            .ToArray();
        UnusedLevel65PassiveMobyPoint[] expectedNativeFacePoints =
        [
            new(7_762, 6_346, 512),
            new(7_890, 6_346, 512),
            new(7_890, 6_474, 512),
            new(7_762, 6_474, 512)
        ];
        if (!nativeFacePoints.SequenceEqual(expectedNativeFacePoints))
            throw new InvalidDataException("The exact foundation HP face 213:37 geometry changed.");
        byte[] collision = DiscImage.ReadFileBytes(
            image,
            layout,
            WadLba,
            FoundationCollisionComponentWadOffset,
            CollisionComponentByteLength);
        RequireHash(collision, FoundationCollisionSha256, "foundation collision component");
        if (BinaryPrimitives.ReadInt32LittleEndian(collision.AsSpan(0, 4)) != CollisionComponentByteLength ||
            BinaryPrimitives.ReadInt32LittleEndian(collision.AsSpan(4, 4)) != CollisionTriangleCount)
        {
            throw new InvalidDataException("The foundation collision header changed.");
        }
        RequireHash(
            collision.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength),
            FoundationCollisionTreeSha256,
            "foundation collision tree");
        RequireHash(
            collision.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength),
            FoundationCollisionBlocksSha256,
            "foundation collision blocks");

        byte[] supportTriangleBytes = collision.AsSpan(
            CollisionTrianglesRelativeOffset + (CollisionTriangleIndex * 12),
            12).ToArray();
        byte[] foundationTriangleBytes = collision.AsSpan(
            CollisionTrianglesRelativeOffset + (FoundationCollisionTriangleIndex * 12),
            12).ToArray();
        RequireHex(supportTriangleBytes, ExpectedSupportTriangleHex, "native support triangle 1353");
        RequireHex(foundationTriangleBytes, ExpectedFoundationTriangleHex, "foundation triangle 13995");
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> supportPoints = DecodeCollisionTriangle(supportTriangleBytes);
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> expectedSupportPoints =
        [
            new(7_762, 6_474, 512),
            new(7_890, 6_346, 512),
            new(7_762, 6_346, 512)
        ];
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> foundationPoints = DecodeCollisionTriangle(foundationTriangleBytes);
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> expectedFoundationPoints =
        [
            new(7_762, 6_346, 512),
            new(7_890, 6_346, 512),
            new(7_826, 6_474, 640)
        ];
        if (!supportPoints.SequenceEqual(expectedSupportPoints) ||
            !foundationPoints.SequenceEqual(expectedFoundationPoints))
        {
            throw new InvalidDataException("The exact native/foundation collision geometry changed.");
        }

        int assignment = collision[CollisionAssignmentsRelativeOffset + CollisionTriangleIndex];
        uint supportZWord = BinaryPrimitives.ReadUInt32LittleEndian(supportTriangleBytes.AsSpan(8, 4));
        int flags = checked((int)(supportZWord & 0xC000u));
        if (assignment != 0 || flags != 0)
            throw new InvalidDataException("The destination support triangle lost ordinary assignment/flags zero.");

        CollisionIndex index = DecodeCollisionIndex(
            collision.AsSpan(CollisionTreeRelativeOffset, CollisionTreeByteLength),
            collision.AsSpan(CollisionBlocksRelativeOffset, CollisionBlocksByteLength),
            construction.CollisionBlocksUsedBytes,
            CollisionTriangleCount);
        UnusedLevel65PassiveMobyCollisionCell destinationCell = new(30, 25, 2);
        if (!index.Cells.TryGetValue(destinationCell, out CollisionCell? cell) ||
            !cell.TriangleIndexes.Contains(CollisionTriangleIndex) ||
            !cell.TriangleIndexes.Contains(FoundationCollisionTriangleIndex))
        {
            throw new InvalidDataException("The exact destination collision cell lost its native/foundation memberships.");
        }
        int supportPosition = Array.IndexOf(cell.TriangleIndexes, CollisionTriangleIndex);
        if (supportPosition < 0)
            throw new InvalidDataException("The native support triangle is missing from the exact destination group.");
        long supportLookupWadOffset = FoundationCollisionComponentWadOffset +
                                      CollisionBlocksRelativeOffset +
                                      ((cell.BlockWordOffset + supportPosition) * 2L);

        const int nativeLeftMarginRaw = AuthoredRawX - (7_762 * 16);
        const int nativeDiagonalMarginRaw = (128 * 16) -
                                            ((AuthoredRawX - (7_762 * 16)) +
                                             (AuthoredRawY - (6_346 * 16)));
        int foundationLeftBoundaryRaw = (7_762 * 16) +
                                        ((AuthoredRawY - (6_346 * 16)) / 2);
        int foundationSeparationRaw = foundationLeftBoundaryRaw - AuthoredRawX;
        const int distanceFromFoundationSpawnRaw = 1_985;
        const int distanceFromReservedStandaloneSpawnRaw = 680;
        const int minimumSpawnSeparationRaw = 680;
        if (nativeLeftMarginRaw != 192 || nativeDiagonalMarginRaw != 1_088 ||
            foundationSeparationRaw != 192 ||
            Distance2dRaw(AuthoredRawX, AuthoredRawY, BeforeRawX, BeforeRawY) != distanceFromFoundationSpawnRaw ||
            Distance2dRaw(AuthoredRawX, AuthoredRawY, ReservedStandaloneSpawnRawX, ReservedStandaloneSpawnRawY) != distanceFromReservedStandaloneSpawnRaw ||
            Math.Min(distanceFromFoundationSpawnRaw, distanceFromReservedStandaloneSpawnRaw) != minimumSpawnSeparationRaw ||
            !PointInsideTriangleStrictRaw16(supportPoints, AuthoredRawX, AuthoredRawY) ||
            PointInsideTriangleInclusiveRaw16(foundationPoints, AuthoredRawX, AuthoredRawY))
        {
            throw new InvalidDataException("The passive-Moby placement lost its exact native/foundation/spawn separation proof.");
        }

        List<CollisionSurfaceHit> hits = [];
        for (int triangleIndex = 0; triangleIndex < CollisionTriangleCount; triangleIndex++)
        {
            byte[] bytes = collision.AsSpan(CollisionTrianglesRelativeOffset + (triangleIndex * 12), 12).ToArray();
            IReadOnlyList<UnusedLevel65PassiveMobyPoint> points = DecodeCollisionTriangle(bytes);
            if (!PointInsideTriangleInclusiveRaw16(points, AuthoredRawX, AuthoredRawY))
                continue;
            if (!TryInterpolateRawZ(points, AuthoredRawX, AuthoredRawY, out int rawZ, out long normalZ))
                continue;
            hits.Add(new(triangleIndex, rawZ, normalZ));
        }
        CollisionSurfaceHit[] topOrHigher = hits
            .Where(hit => hit.RawZ >= NativeGroundRawZ)
            .OrderByDescending(hit => hit.RawZ)
            .ThenBy(hit => hit.TriangleIndex)
            .ToArray();
        if (topOrHigher.Length == 0 || topOrHigher[0].RawZ != NativeGroundRawZ ||
            topOrHigher.Any(hit => hit.RawZ > NativeGroundRawZ) ||
            !topOrHigher.Any(hit => hit.TriangleIndex == CollisionTriangleIndex && hit.NormalZ == -16_384))
        {
            throw new InvalidDataException(
                "The destination does not have exact triangle 1353 as an exposed topmost native support surface: " +
                string.Join(",", topOrHigher.Select(hit => $"T{hit.TriangleIndex}:Z{hit.RawZ}:N{hit.NormalZ}")));
        }

        byte[] beforeLanding = DiscImage.ReadFileBytes(image, layout, WadLba, LandingWadOffset, 0x10);
        byte[] beforePlayerAnchor = DiscImage.ReadFileBytes(image, layout, WadLba, PlayerAnchorWadOffset, PlayerAnchorByteLength);
        RequireHex(beforeLanding, BeforeLandingHex, "foundation landing");
        RequireHash(beforePlayerAnchor, BeforePlayerAnchorSha256, "foundation T92 player anchor");
        int playerX = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x0C, 4));
        int playerY = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x10, 4));
        int playerZ = BinaryPrimitives.ReadInt32LittleEndian(beforePlayerAnchor.AsSpan(0x14, 4));
        if (playerX != BeforeRawX || playerY != BeforeRawY || playerZ != PlayerRawZ)
            throw new InvalidDataException("The foundation T92 coordinate preimage changed.");

        byte[] sourceRow = DiscImage.ReadFileBytes(image, layout, WadLba, ArtisansGrassRowWadOffset, MobyRowByteLength);
        byte[] sourceProperties = DiscImage.ReadFileBytes(image, layout, WadLba, ArtisansGrassPropertiesWadOffset, GrassPropertiesByteLength);
        byte[] sourcePackage = DiscImage.ReadFileBytes(image, layout, WadLba, ArtisansGrassPackageWadOffset, GrassPackageByteLength);
        RequireHash(sourceRow, GrassRowSha256, "Artisans T121 Grass row");
        RequireHash(sourceProperties, GrassPropertiesSha256, "Artisans Grass properties");
        RequireHash(sourcePackage, GrassPackageSha256, "Artisans actor 0x01F5 package");
        if (BinaryPrimitives.ReadUInt16LittleEndian(sourceRow.AsSpan(0x36, 2)) != GrassActorId ||
            sourceRow[0x47] != DonorYawByte ||
            !sourceProperties.SequenceEqual(Convert.FromHexString("040000008A000000")))
        {
            throw new InvalidDataException("The exact passive Grass donor identity changed.");
        }

        byte[] beforeObjectCount = DiscImage.ReadFileBytes(image, layout, WadLba, Id65ObjectCountWadOffset, 4);
        byte[] beforeTargetRow = DiscImage.ReadFileBytes(image, layout, WadLba, Id65TargetRowWadOffset, MobyRowByteLength);
        byte[] beforeFixupCount = DiscImage.ReadFileBytes(image, layout, WadLba, Id65FixupCountWadOffset, 4);
        byte[] beforeFixupAppend = DiscImage.ReadFileBytes(image, layout, WadLba, Id65FixupAppendWadOffset, 4);
        byte[] beforeProperties = DiscImage.ReadFileBytes(image, layout, WadLba, Id65PropertiesWadOffset, GrassPropertiesByteLength);
        byte[] beforeRoot = DiscImage.ReadFileBytes(image, layout, WadLba, Id65ActorRootSlotWadOffset, 4);
        byte[] beforeActorId = DiscImage.ReadFileBytes(image, layout, WadLba, Id65ActorIdSlotWadOffset, 2);
        byte[] beforePackage = DiscImage.ReadFileBytes(image, layout, WadLba, Id65ActorPackageWadOffset, GrassPackageByteLength);
        RequireUInt32(beforeObjectCount, ObjectCountBefore, "ID65 object count");
        RequireHash(beforeTargetRow, ZeroRowSha256, "ID65 T107 blank row");
        RequireUInt32(beforeFixupCount, FixupCountBefore, "ID65 pointer-fixup count");
        RequireZero(beforeFixupAppend, "ID65 fixup append slot");
        RequireZero(beforeProperties, "ID65 Grass properties allocation");
        RequireZero(beforeRoot, "ID65 actor root 37 slot");
        RequireZero(beforeActorId, "ID65 actor id 37 slot");
        RequireZero(beforePackage, "ID65 actor package tail allocation");

        byte[] authoredRow = sourceRow.ToArray();
        int donorRawX = BinaryPrimitives.ReadInt32LittleEndian(authoredRow.AsSpan(0x0C, 4));
        int donorRawY = BinaryPrimitives.ReadInt32LittleEndian(authoredRow.AsSpan(0x10, 4));
        int donorRawZ = BinaryPrimitives.ReadInt32LittleEndian(authoredRow.AsSpan(0x14, 4));
        BinaryPrimitives.WriteUInt32LittleEndian(authoredRow.AsSpan(0, 4), TargetPropertiesSceneOffset);
        BinaryPrimitives.WriteInt32LittleEndian(authoredRow.AsSpan(0x0C, 4), AuthoredRawX);
        BinaryPrimitives.WriteInt32LittleEndian(authoredRow.AsSpan(0x10, 4), AuthoredRawY);
        BinaryPrimitives.WriteInt32LittleEndian(authoredRow.AsSpan(0x14, 4), NativeGroundRawZ);
        if (!authoredRow.AsSpan(4, 8).SequenceEqual(sourceRow.AsSpan(4, 8)) ||
            !authoredRow.AsSpan(0x18).SequenceEqual(sourceRow.AsSpan(0x18)) ||
            BinaryPrimitives.ReadUInt16LittleEndian(authoredRow.AsSpan(0x36, 2)) != GrassActorId)
        {
            throw new InvalidDataException("The destination row changed outside m_Props and XYZ placement fields.");
        }

        byte[] afterObjectCount = UInt32Bytes(ObjectCountAfter);
        byte[] afterFixupCount = UInt32Bytes(FixupCountAfter);
        byte[] afterFixupAppend = UInt32Bytes(TargetPointerFieldSceneOffset);
        byte[] afterRoot = UInt32Bytes(TargetActorPackageEntryOffset);
        byte[] afterActorId = UInt16Bytes(GrassActorId);
        UnusedLevel65PassiveMobyPatch[] patches =
        [
            Patch("ID65 actor root 37", Id65ActorRootSlotWadOffset, beforeRoot, afterRoot),
            Patch("ID65 actor id 37", Id65ActorIdSlotWadOffset, beforeActorId, afterActorId),
            Patch("ID65 actor 0x01F5 package", Id65ActorPackageWadOffset, beforePackage, sourcePackage),
            Patch("ID65 object count 107 to 108", Id65ObjectCountWadOffset, beforeObjectCount, afterObjectCount),
            Patch("ID65 T107 Artisans Grass row and native-apron placement", Id65TargetRowWadOffset, beforeTargetRow, authoredRow),
            Patch("ID65 pointer-fixup count 0x81 to 0x82", Id65FixupCountWadOffset, beforeFixupCount, afterFixupCount),
            Patch("ID65 T107 m_Props pointer fixup", Id65FixupAppendWadOffset, beforeFixupAppend, afterFixupAppend),
            Patch("ID65 exact Grass properties", Id65PropertiesWadOffset, beforeProperties, sourceProperties)
        ];
        if (patches.Select(patch => patch.WadOffset).Distinct().Count() != patches.Length ||
            patches.Any(patch => patch.ChangedByteCount <= 0))
        {
            throw new InvalidDataException("The passive-Moby patch set is empty, overlapping, or redundant.");
        }

        int[] affectedLbas = patches
            .SelectMany(patch => RawSectorLbasForWadRange(layout, patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length))
            .Distinct()
            .Order()
            .ToArray();
        if (!affectedLbas.SequenceEqual(new[] { 53_906, 54_833, 54_834, 54_838, 54_849, 54_850 }))
            throw new InvalidDataException($"The passive-Moby patch escaped its exact six raw sectors: [{string.Join(',', affectedLbas)}].");
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes =
            RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);

        UnusedLevel65PassiveMobySupportProof support = new(
            213,
            37,
            nativeFacePoints,
            CollisionTriangleIndex,
            Convert.ToHexString(supportTriangleBytes),
            supportPoints,
            assignment,
            flags,
            CollisionNormalZ: -16_384,
            destinationCell,
            [supportLookupWadOffset],
            nativeLeftMarginRaw,
            nativeDiagonalMarginRaw,
            foundationSeparationRaw,
            NativeGroundRawZ,
            AuthoredRawX,
            AuthoredRawY,
            NativeGroundRawZ,
            distanceFromFoundationSpawnRaw,
            distanceFromReservedStandaloneSpawnRaw,
            minimumSpawnSeparationRaw,
            DestinationStrictlyInsideNativeCollision: true,
            DestinationStrictlyOutsideFoundationTriangle: true,
            SupportIsTopmostAtDestination: true,
            TerrainFaceReadbackVerified: true,
            RuntimeEvidenceBoundToExactNativeFan: true,
            RuntimeEvidenceSha256);
        UnusedLevel65PassiveMobyRuntimeCandidatePlan plan = new(
            PlanSchemaVersion,
            ProfileId,
            FoundationImageSha256,
            LockedBaseImageSha256,
            closedContract.ProfileId,
            closedContract.DeterministicContractSha256,
            65,
            35,
            "Artisans",
            DonorTrueIndex,
            TargetTrueIndex,
            GrassActorId,
            TargetActorRootIndex,
            patches,
            donorRawX,
            donorRawY,
            donorRawZ,
            AuthoredRawX,
            AuthoredRawY,
            NativeGroundRawZ,
            DonorYawByte,
            GrassRowSha256,
            GrassPropertiesSha256,
            GrassPackageSha256,
            closedContract.BehaviorClosure.DonorDispatch.ExecutedTraceSha256,
            closedContract.BehaviorClosure.TargetDispatch.ExecutedTraceSha256,
            support,
            affectedLbas,
            loadCodes,
            ClosedDependencyContractVerified: true,
            LandingAndPlayerAnchorPreserved: true,
            FoundationTerrainPreserved: true,
            PassiveNoControllerDependencies: true,
            MusicPreserved: true,
            TotalsPreserved: true,
            ExitPreserved: true,
            SaveCodePreserved: true,
            RetailLevelsPreserved: true,
            RequiresDuckStationRuntimeProof: true,
            RuntimePassed: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
        return new(
            layout,
            plan,
            ownership,
            closedContract,
            executableBytes,
            beforeLanding,
            beforePlayerAnchor,
            lockedBase,
            foundation,
            foundationCue);
    }

    private static async Task BuildStageAsync(
        Prepared prepared,
        string foundation,
        string foundationCue,
        UnusedLevel65PassiveMobyRuntimeCandidatePaths finalPaths,
        string stageDirectory,
        bool rollbackRecoveryVerified,
        CancellationToken cancellationToken)
    {
        string imagePath = Path.Combine(stageDirectory, OutputPrefix + ".bin");
        string cuePath = Path.Combine(stageDirectory, OutputPrefix + ".cue");
        string planPath = Path.Combine(stageDirectory, OutputPrefix + "-moby-plan.json");
        string receiptPath = Path.Combine(stageDirectory, OutputPrefix + "-static-readback-receipt.json");
        string checklistPath = Path.Combine(stageDirectory, OutputPrefix + "-runtime-checklist.md");
        string guidePath = Path.Combine(stageDirectory, OutputPrefix + "-location-guide.svg");
        string revealPath = Path.Combine(stageDirectory, OutputPrefix + "-Reveal-in-Finder.command");

        await DiscImageWorkingCopy.StageAsync(foundation, imagePath, false, cancellationToken);
        DiscLayout layout = DiscImage.DetectLayout(imagePath);
        RequireMode2(layout, "staged passive-moby candidate");
        int rebuilt;
        await using (FileStream output = new(
                         imagePath,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         bufferSize: 1 << 20,
                         FileOptions.Asynchronous))
        {
            foreach (UnusedLevel65PassiveMobyPatch patch in prepared.Plan.Patches)
            {
                byte[] before = Convert.FromHexString(patch.BeforeHex);
                byte[] after = Convert.FromHexString(patch.AfterHex);
                RequireEqual(
                    DiscImage.ReadFileBytes(output, layout, WadLba, patch.WadOffset, before.Length),
                    before,
                    $"staged {patch.Name} preimage");
                DiscImage.WriteFileBytes(output, layout, WadLba, patch.WadOffset, after);
            }
            rebuilt = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                output,
                layout,
                WadLba,
                prepared.Plan.Patches
                    .Select(patch => (patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length))
                    .ToArray());
            if (rebuilt != prepared.Plan.AffectedRawSectorLbas.Count)
                throw new InvalidDataException("The passive-Moby writer did not rebuild exactly its six owned raw sectors.");
            VerifyRawSectors(output, layout, prepared.Plan.AffectedRawSectorLbas);
            await output.FlushAsync(cancellationToken);
            output.Flush(flushToDisk: true);
        }

        Readback readback = await VerifyOutputAsync(
            prepared,
            foundation,
            imagePath,
            cancellationToken);
        if (!IsPendingHash(ExpectedOutputImageSha256))
            RequireTextHash(readback.OutputImageSha256, ExpectedOutputImageSha256, "passive-moby output BIN");
        if (!IsPendingHash(ExpectedOutputId65DataSha256))
            RequireTextHash(readback.OutputId65DataSha256, ExpectedOutputId65DataSha256, "passive-moby ID65 data");
        if (!IsPendingHash(ExpectedRawSectorDiffSha256))
            RequireTextHash(readback.RawSectorDiffSha256, ExpectedRawSectorDiffSha256, "passive-moby raw diff");

        string cueText = DiscImage.BuildCueText(foundationCue, Path.GetFileName(imagePath));
        await WriteTextAsync(cuePath, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(cuePath, imagePath, "MODE2/2352");

        await WriteJsonAsync(planPath, prepared.Plan, cancellationToken);
        await WriteTextAsync(
            checklistPath,
            BuildChecklist(prepared.Plan, readback, finalPaths),
            new UTF8Encoding(false),
            cancellationToken);
        await WriteTextAsync(
            guidePath,
            BuildGuide(prepared.Plan, readback.OutputImageSha256),
            new UTF8Encoding(false),
            cancellationToken);
        string cueName = Path.GetFileName(finalPaths.CuePath);
        string reveal =
            "#!/bin/zsh\n" +
            "set -euo pipefail\n\n" +
            $"cue_name={ShellSingleQuote(cueName)}\n" +
            "cue_path=\"${0:A:h}/${cue_name}\"\n" +
            "if [[ ! -f \"$cue_path\" ]]; then\n" +
            "  print -u2 -- \"Missing runtime candidate CUE: $cue_path\"\n" +
            "  exit 1\n" +
            "fi\n" +
            "/usr/bin/open -R \"$cue_path\"\n";
        await WriteTextAsync(revealPath, reveal, new UTF8Encoding(false), cancellationToken);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                revealPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
                UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        }

        string cueSha256 = await HashFileAsync(cuePath, cancellationToken);
        string planSha256 = await HashFileAsync(planPath, cancellationToken);
        string checklistSha256 = await HashFileAsync(checklistPath, cancellationToken);
        string guideSha256 = await HashFileAsync(guidePath, cancellationToken);
        string helperSha256 = await HashFileAsync(revealPath, cancellationToken);

        UnusedLevel65PassiveMobyRuntimeCandidateReceipt receipt = new(
            ReceiptSchemaVersion,
            ProfileId,
            prepared.LockedBaseImagePath,
            prepared.FoundationImagePath,
            prepared.FoundationCuePath,
            finalPaths.OutputDirectoryPath,
            finalPaths.ImagePath,
            finalPaths.CuePath,
            finalPaths.PlanPath,
            finalPaths.ReceiptPath,
            finalPaths.ChecklistPath,
            finalPaths.GuidePath,
            finalPaths.RevealHelperPath,
            LockedBaseImageSha256,
            FoundationImageSha256,
            readback.OutputImageSha256,
            readback.OutputId65DataSha256,
            cueSha256,
            planSha256,
            checklistSha256,
            guideSha256,
            helperSha256,
            prepared.Plan.LoadCodes,
            readback.ChangedLogicalWadBytes,
            readback.ChangedPhysicalImageBytes,
            readback.RawSectorDiffs,
            readback.RawSectorDiffSha256,
            ExactPatchReadbackVerified: true,
            ClosedDependencyContractVerified: true,
            LandingAndPlayerAnchorPreserved: true,
            DestinationSupportVerified: true,
            FoundationUnrelatedBytesPreserved: true,
            ExecutablePreserved: true,
            RetailTownSquarePreserved: true,
            MusicTotalsExitSavePreserved: true,
            RawSectorIntegrityVerified: true,
            AtomicDirectoryPublicationCompleted: true,
            RollbackGuardsEnabled: true,
            StartupRecoveryPerformed: rollbackRecoveryVerified,
            RuntimePending: true,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false);
        await WriteJsonAsync(receiptPath, receipt, cancellationToken);
        await VerifyHandoffArtifactsAsync(
            prepared.Plan,
            receipt,
            imagePath,
            cuePath,
            planPath,
            receiptPath,
            checklistPath,
            guidePath,
            revealPath,
            cancellationToken);
    }

    private static async Task<Readback> VerifyOutputAsync(
        Prepared prepared,
        string foundation,
        string outputPath,
        CancellationToken cancellationToken)
    {
        DiscLayout outputLayout = DiscImage.DetectLayout(outputPath);
        if (outputLayout != prepared.Layout)
            throw new InvalidDataException("The passive-moby writer changed the MODE2 disc layout.");
        await using FileStream before = File.OpenRead(foundation);
        await using FileStream after = File.OpenRead(outputPath);
        if (before.Length != after.Length)
            throw new InvalidDataException("The passive-moby writer changed the disc image length.");
        DiscFileRecord beforeWad = RequireFileRecord(before, prepared.Layout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord afterWad = RequireFileRecord(after, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord afterExecutable = RequireFileRecord(
            after,
            outputLayout,
            "SCUS_942.28",
            ExecutableLba,
            ExecutableByteLength);
        if (beforeWad != afterWad)
            throw new InvalidDataException("The passive-moby writer moved WAD.WAD.");

        foreach (UnusedLevel65PassiveMobyPatch patch in prepared.Plan.Patches)
        {
            RequireEqual(
                DiscImage.ReadFileBytes(
                    after,
                    outputLayout,
                    WadLba,
                    patch.WadOffset,
                    Convert.FromHexString(patch.AfterHex).Length),
                Convert.FromHexString(patch.AfterHex),
                $"authored {patch.Name} readback");
        }
        byte[] landing = DiscImage.ReadFileBytes(after, outputLayout, WadLba, LandingWadOffset, 0x10);
        byte[] player = DiscImage.ReadFileBytes(after, outputLayout, WadLba, PlayerAnchorWadOffset, PlayerAnchorByteLength);
        RequireEqual(landing, prepared.BeforeLanding, "unchanged foundation landing");
        RequireEqual(player, prepared.BeforePlayerAnchor, "unchanged foundation T92 player anchor");
        RequireHash(
            DiscImage.ReadFileBytes(after, outputLayout, afterExecutable.Lba, 0, afterExecutable.Size),
            ExecutableSha256,
            "passive-moby executable");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, RetailTownSquareOverlayWadOffset, RetailTownSquareOverlayByteLength),
            "retail Town Square overlay");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, RetailTownSquareDataWadOffset, RetailTownSquareDataByteLength),
            "retail Town Square data");
        RequireEqual(
            DiscImage.ReadFileBytes(before, prepared.Layout, WadLba, Id65OverlayWadOffset, Id65OverlayByteLength),
            DiscImage.ReadFileBytes(after, outputLayout, WadLba, Id65OverlayWadOffset, Id65OverlayByteLength),
            "ID65 overlay");

        LogicalDiff logical = CompareLogicalWad(
            before,
            after,
            prepared.Layout,
            beforeWad,
            prepared.Plan.Patches,
            cancellationToken);
        long expectedChangedBytes = prepared.Plan.Patches.Sum(patch => (long)patch.ChangedByteCount);
        if (logical.ChangedBytes != expectedChangedBytes || logical.OutsideAllowedBytes != 0)
        {
            throw new InvalidDataException(
                $"The passive-Moby logical diff changed {logical.ChangedBytes} bytes, expected {expectedChangedBytes}, including {logical.OutsideAllowedBytes} outside its eight exact fields.");
        }
        PhysicalDiff physical = ComparePhysicalImages(
            before,
            after,
            prepared.Plan.AffectedRawSectorLbas,
            cancellationToken);
        VerifyRawSectors(after, outputLayout, prepared.Plan.AffectedRawSectorLbas);
        string imageHash = await HashFileAsync(outputPath, cancellationToken);
        string dataHash = Hash(DiscImage.ReadFileBytes(
            after,
            outputLayout,
            WadLba,
            Id65DataWadOffset,
            Id65DataByteLength));
        string rawDiffHash = Hash(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(physical.RawSectorDiffs, JsonOptions)));
        return new(
            imageHash,
            dataHash,
            logical.ChangedBytes,
            physical.ChangedBytes,
            physical.RawSectorDiffs,
            rawDiffHash);
    }

    internal static async Task<UnusedLevel65PassiveMobyRuntimeCandidateResult> VerifyPublishedAsync(
        string outputDirectoryPath,
        CancellationToken cancellationToken = default) =>
        await ReadPublishedResultAsync(CreatePaths(outputDirectoryPath), cancellationToken);

    private static async Task<UnusedLevel65PassiveMobyRuntimeCandidateResult> ReadPublishedResultAsync(
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths,
        CancellationToken cancellationToken)
    {
        string[] required =
        [
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath
        ];
        if (required.Any(path => !File.Exists(path)))
            throw new InvalidDataException("The published passive-moby directory is incomplete.");
        NativeLevelReplacementBaselineExporter.ValidateCue(paths.CuePath, paths.ImagePath, "MODE2/2352");
        UnusedLevel65PassiveMobyRuntimeCandidatePlan plan =
            JsonSerializer.Deserialize<UnusedLevel65PassiveMobyRuntimeCandidatePlan>(
                await File.ReadAllTextAsync(paths.PlanPath, cancellationToken),
                JsonOptions)
            ?? throw new InvalidDataException("The published passive-moby plan is invalid.");
        UnusedLevel65PassiveMobyRuntimeCandidateReceipt receipt =
            JsonSerializer.Deserialize<UnusedLevel65PassiveMobyRuntimeCandidateReceipt>(
                await File.ReadAllTextAsync(paths.ReceiptPath, cancellationToken),
                JsonOptions)
            ?? throw new InvalidDataException("The published passive-moby receipt is invalid.");
        VerifyReceiptPaths(receipt, paths);
        await VerifyHandoffArtifactsAsync(
            plan,
            receipt,
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath,
            cancellationToken);
        string actualHash = await HashFileAsync(paths.ImagePath, cancellationToken);
        if (plan.ProfileId != ProfileId || receipt.ProfileId != ProfileId ||
            !string.Equals(actualHash, receipt.OutputImageSha256, StringComparison.OrdinalIgnoreCase) ||
            !receipt.RuntimePending || receipt.NormalCreateBinEnabled || receipt.PromotionAuthorized ||
            plan.RuntimePassed || plan.NormalCreateBinEnabled || plan.PromotionAuthorized)
        {
            throw new InvalidDataException("The published passive-moby runtime-pending boundary changed.");
        }
        UnusedLevel65PassiveMobyPublishedArtifactHashes hashes = new(
            await HashFileAsync(paths.CuePath, cancellationToken),
            await HashFileAsync(paths.PlanPath, cancellationToken),
            await HashFileAsync(paths.ReceiptPath, cancellationToken),
            await HashFileAsync(paths.ChecklistPath, cancellationToken),
            await HashFileAsync(paths.GuidePath, cancellationToken),
            await HashFileAsync(paths.RevealHelperPath, cancellationToken));
        return new(paths, plan, receipt, hashes);
    }

    private static async Task VerifyHandoffArtifactsAsync(
        UnusedLevel65PassiveMobyRuntimeCandidatePlan plan,
        UnusedLevel65PassiveMobyRuntimeCandidateReceipt receipt,
        string imagePath,
        string cuePath,
        string planPath,
        string receiptPath,
        string checklistPath,
        string guidePath,
        string helperPath,
        CancellationToken cancellationToken)
    {
        string expectedPlan = JsonSerializer.Serialize(plan, JsonOptions) + "\n";
        string expectedReceipt = JsonSerializer.Serialize(receipt, JsonOptions) + "\n";
        if (await File.ReadAllTextAsync(planPath, cancellationToken) != expectedPlan ||
            await File.ReadAllTextAsync(receiptPath, cancellationToken) != expectedReceipt)
        {
            throw new InvalidDataException("The passive-moby plan or receipt failed exact serialized readback.");
        }
        RequireTextHash(await HashFileAsync(imagePath, cancellationToken), receipt.OutputImageSha256, "handoff BIN");
        RequireTextHash(await HashFileAsync(cuePath, cancellationToken), receipt.OutputCueSha256, "handoff CUE");
        RequireTextHash(await HashFileAsync(planPath, cancellationToken), receipt.PlanSha256, "handoff plan");
        RequireTextHash(await HashFileAsync(checklistPath, cancellationToken), receipt.ChecklistSha256, "handoff checklist");
        RequireTextHash(await HashFileAsync(guidePath, cancellationToken), receipt.GuideSha256, "handoff guide");
        if (string.IsNullOrWhiteSpace(receipt.FinderHelperSha256) ||
            string.IsNullOrWhiteSpace(receipt.FinderHelperPath))
        {
            throw new InvalidDataException("The passive-moby handoff lost its requested Finder helper identity.");
        }
        RequireTextHash(await HashFileAsync(helperPath, cancellationToken), receipt.FinderHelperSha256, "handoff Finder helper");
        NativeLevelReplacementBaselineExporter.ValidateCue(cuePath, imagePath, "MODE2/2352");
        VerifyLoadCodes(plan.LoadCodes);
        if (!plan.LoadCodes.SequenceEqual(receipt.LoadCodes))
            throw new InvalidDataException("The passive-moby plan/receipt load codes differ.");
        if (plan.ClosedContractSha256 != ClosedContractSha256 ||
            plan.DonorTrueIndex != DonorTrueIndex || plan.TargetTrueIndex != TargetTrueIndex ||
            plan.ActorId != GrassActorId || plan.TargetActorRootIndex != TargetActorRootIndex ||
            plan.AuthoredRawX != AuthoredRawX || plan.AuthoredRawY != AuthoredRawY ||
            plan.AuthoredRawZ != NativeGroundRawZ || plan.Patches.Count != 8 ||
            !plan.ClosedDependencyContractVerified || !plan.LandingAndPlayerAnchorPreserved ||
            !plan.FoundationTerrainPreserved || !plan.PassiveNoControllerDependencies ||
            plan.Patches.Any(patch =>
                RangesOverlap(patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length, LandingWadOffset, 0x10) ||
                RangesOverlap(patch.WadOffset, Convert.FromHexString(patch.AfterHex).Length, PlayerAnchorWadOffset, PlayerAnchorByteLength)))
        {
            throw new InvalidDataException("The passive-moby handoff lost its exact T107/0x01F5 Moby-only patch boundary.");
        }
        if (!receipt.ExactPatchReadbackVerified || !receipt.ClosedDependencyContractVerified ||
            !receipt.LandingAndPlayerAnchorPreserved || !receipt.DestinationSupportVerified ||
            !receipt.FoundationUnrelatedBytesPreserved || !receipt.ExecutablePreserved ||
            !receipt.RetailTownSquarePreserved || !receipt.MusicTotalsExitSavePreserved ||
            !receipt.RawSectorIntegrityVerified)
        {
            throw new InvalidDataException("The passive-moby receipt lost a fail-closed preservation/readback proof.");
        }

        string checklist = await File.ReadAllTextAsync(checklistPath, cancellationToken);
        string guide = await File.ReadAllTextAsync(guidePath, cancellationToken);
        string helper = await File.ReadAllTextAsync(helperPath, cancellationToken);
        string cueName = Path.GetFileName(receipt.OutputCuePath);
        string[] commonIdentity = [ProfileId, cueName, receipt.OutputImageSha256, "RUNTIME PENDING", "UNPROMOTED"];
        foreach (string identity in commonIdentity)
        {
            if (!guide.Contains(identity, StringComparison.Ordinal))
                throw new InvalidDataException($"The passive-moby guide omitted '{identity}'.");
        }
        if (!checklist.Contains(ProfileId, StringComparison.Ordinal) ||
            !checklist.Contains(cueName, StringComparison.Ordinal) ||
            !checklist.Contains(receipt.OutputImageSha256, StringComparison.Ordinal) ||
            !checklist.Contains("Runtime status: **pending / unpromoted**", StringComparison.Ordinal) ||
            !checklist.Contains("T107", StringComparison.Ordinal) ||
            !checklist.Contains("0x01F5", StringComparison.Ordinal) ||
            !checklist.Contains("landing and T92 player anchor are byte-for-byte unchanged", StringComparison.Ordinal) ||
            !checklist.Contains("far LOD", StringComparison.Ordinal) ||
            !checklist.Contains("60 seconds", StringComparison.Ordinal) ||
            !checklist.Contains("NO CARDS / DO NOT SAVE", StringComparison.Ordinal) ||
            checklist.Contains("authored landing", StringComparison.OrdinalIgnoreCase) ||
            checklist.Contains("death respawn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The passive-moby checklist lost its exact Moby-only/preservation/runtime boundary.");
        }
        if (!guide.Contains("T107 GRASS 0x01F5", StringComparison.Ordinal) ||
            !guide.Contains($"raw ({AuthoredRawX}, {AuthoredRawY}, {NativeGroundRawZ})", StringComparison.Ordinal) ||
            !guide.Contains("UNCHANGED SPAWN / T92", StringComparison.Ordinal) ||
            !guide.Contains("<text x=\"650\" y=\"150\" fill=\"#f0dcff\"", StringComparison.Ordinal) ||
            !guide.Contains("<circle cx=\"300\" cy=\"205\" r=\"14\"", StringComparison.Ordinal) ||
            !guide.Contains("<text x=\"330\" y=\"205\" fill=\"#ffdf98\"", StringComparison.Ordinal) ||
            !guide.Contains("RESERVED STANDALONE-SPAWN", StringComparison.Ordinal) ||
            !guide.Contains("<text x=\"330\" y=\"228\" fill=\"#ffdf98\"", StringComparison.Ordinal) ||
            !guide.Contains("680 raw from T107", StringComparison.Ordinal) ||
            guide.Contains("<circle cx=\"360\" cy=\"205\"", StringComparison.Ordinal) ||
            guide.Contains("<text x=\"390\" y=\"210\"", StringComparison.Ordinal) ||
            !guide.Contains("far/near LOD", StringComparison.Ordinal) ||
            !guide.Contains("NO CARDS / DO NOT SAVE", StringComparison.Ordinal) ||
            guide.Contains("AUTHORED START", StringComparison.Ordinal) ||
            guide.Contains("death respawn", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The passive-moby guide lost its distinct Moby-only placement/runtime boundary.");
        }
        foreach (RuntimeCandidateLoadCode code in plan.LoadCodes)
        {
            if (!checklist.Contains(code.TestName, StringComparison.Ordinal) ||
                !checklist.Contains(code.InputCode, StringComparison.Ordinal) ||
                !guide.Contains(XmlEscape(code.TestName), StringComparison.Ordinal) ||
                !guide.Contains(XmlEscape(code.InputCode), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"The passive-moby handoff omitted the full {code.TestName} load code.");
            }
        }
        if (!helper.Contains($"cue_name={ShellSingleQuote(cueName)}", StringComparison.Ordinal) ||
            !helper.Contains("/usr/bin/open -R \"$cue_path\"", StringComparison.Ordinal))
        {
            throw new InvalidDataException("The passive-moby Finder helper lost exact relative CUE targeting.");
        }
        if (!OperatingSystem.IsWindows() &&
            File.GetUnixFileMode(helperPath) !=
            (UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
             UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
             UnixFileMode.OtherRead | UnixFileMode.OtherExecute))
        {
            throw new InvalidDataException("The passive-moby Finder helper is not exact 0755.");
        }
    }

    private static void VerifyReceiptPaths(
        UnusedLevel65PassiveMobyRuntimeCandidateReceipt receipt,
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths)
    {
        string[] actual =
        [
            receipt.OutputDirectoryPath,
            receipt.OutputImagePath,
            receipt.OutputCuePath,
            receipt.PlanPath,
            receipt.ReceiptPath,
            receipt.ChecklistPath,
            receipt.GuidePath,
            receipt.FinderHelperPath ?? ""
        ];
        string[] expected =
        [
            paths.OutputDirectoryPath,
            paths.ImagePath,
            paths.CuePath,
            paths.PlanPath,
            paths.ReceiptPath,
            paths.ChecklistPath,
            paths.GuidePath,
            paths.RevealHelperPath
        ];
        if (!actual.Zip(expected).All(pair => PathEquals(pair.First, pair.Second)))
            throw new InvalidDataException("The passive-moby receipt artifact paths changed.");
    }

    private static UnusedLevel65PassiveMobyPoint DecodeSceneVertex(
        ReadOnlySpan<byte> vertexWord,
        ReadOnlySpan<byte> sectorHeader)
    {
        uint word = BinaryPrimitives.ReadUInt32LittleEndian(vertexWord);
        uint xyPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.Slice(8, 4));
        uint zPos = BinaryPrimitives.ReadUInt32LittleEndian(sectorHeader.Slice(12, 4));
        int sectorX = (int)(xyPos >> 16);
        int sectorY = (int)(xyPos & 0xFFFF);
        int sectorZ = (int)((zPos >> 14) & 0xFFFF) >> 2;
        int x = sectorX + (int)(((word >> 19) & 0x1FFC) >> 2);
        int y = sectorY + (int)(((word >> 8) & 0x1FFC) >> 2);
        int z = sectorZ + (int)(((word << 3) & 0x1FFC) >> 3);
        if (((BinaryPrimitives.ReadUInt16LittleEndian(sectorHeader.Slice(4, 2)) >> 12) & 1) == 1)
            z >>= 3;
        return new(x, y, z);
    }

    private static IReadOnlyList<UnusedLevel65PassiveMobyPoint> DecodeCollisionTriangle(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != 12)
            throw new InvalidDataException("A passive-moby collision triangle is truncated.");
        uint xWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(0, 4));
        uint yWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(4, 4));
        uint zWord = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(8, 4));
        UnusedLevel65PassiveMobyPoint p1 = new(
            (int)(xWord & 0x3FFF),
            (int)(yWord & 0x3FFF),
            (int)(zWord & 0x3FFF));
        UnusedLevel65PassiveMobyPoint p2 = new(
            p1.X + SignedBits((int)((xWord >> 14) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 14) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 16) & 0xFF));
        UnusedLevel65PassiveMobyPoint p3 = new(
            p1.X + SignedBits((int)((xWord >> 23) & 0x1FF), 9),
            p1.Y + SignedBits((int)((yWord >> 23) & 0x1FF), 9),
            p1.Z + (int)((zWord >> 24) & 0xFF));
        return [p1, p2, p3];
    }

    private static int SignedBits(int value, int bits)
    {
        int sign = 1 << (bits - 1);
        return (value ^ sign) - sign;
    }

    private static bool PointInsideTriangleStrictRaw16(
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> points,
        int rawX,
        int rawY) =>
        PointInsideTriangleRaw16(points, rawX, rawY, strict: true);

    private static bool PointInsideTriangleInclusiveRaw16(
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> points,
        int rawX,
        int rawY) =>
        PointInsideTriangleRaw16(points, rawX, rawY, strict: false);

    private static bool PointInsideTriangleRaw16(
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> points,
        int rawX,
        int rawY,
        bool strict)
    {
        if (points.Count != 3)
            return false;
        long Cross(UnusedLevel65PassiveMobyPoint a, UnusedLevel65PassiveMobyPoint b) =>
            ((long)((b.X * 16) - (a.X * 16)) * (rawY - (a.Y * 16))) -
            ((long)((b.Y * 16) - (a.Y * 16)) * (rawX - (a.X * 16)));
        long c1 = Cross(points[0], points[1]);
        long c2 = Cross(points[1], points[2]);
        long c3 = Cross(points[2], points[0]);
        if (strict)
            return (c1 > 0 && c2 > 0 && c3 > 0) || (c1 < 0 && c2 < 0 && c3 < 0);
        return (c1 >= 0 && c2 >= 0 && c3 >= 0) || (c1 <= 0 && c2 <= 0 && c3 <= 0);
    }

    private static bool TryInterpolateRawZ(
        IReadOnlyList<UnusedLevel65PassiveMobyPoint> points,
        int rawX,
        int rawY,
        out int rawZ,
        out long normalZ)
    {
        UnusedLevel65PassiveMobyPoint a = points[0];
        UnusedLevel65PassiveMobyPoint b = points[1];
        UnusedLevel65PassiveMobyPoint c = points[2];
        long abX = b.X - a.X;
        long abY = b.Y - a.Y;
        long abZ = b.Z - a.Z;
        long acX = c.X - a.X;
        long acY = c.Y - a.Y;
        long acZ = c.Z - a.Z;
        long normalX = (abY * acZ) - (abZ * acY);
        long normalY = (abZ * acX) - (abX * acZ);
        normalZ = (abX * acY) - (abY * acX);
        if (normalZ == 0)
        {
            rawZ = 0;
            return false;
        }
        long numerator = ((long)a.Z * 16 * normalZ) -
                         (normalX * (rawX - (a.X * 16L))) -
                         (normalY * (rawY - (a.Y * 16L)));
        rawZ = checked((int)Math.Round((double)numerator / normalZ, MidpointRounding.AwayFromZero));
        return true;
    }

    private static CollisionIndex DecodeCollisionIndex(
        ReadOnlySpan<byte> treeCapacity,
        ReadOnlySpan<byte> blockCapacity,
        int usedBlockBytes,
        int triangleCount)
    {
        if ((treeCapacity.Length & 1) != 0 || (blockCapacity.Length & 1) != 0 ||
            usedBlockBytes <= 0 || (usedBlockBytes & 1) != 0 || usedBlockBytes > blockCapacity.Length)
        {
            throw new InvalidDataException("The passive-moby collision index capacities are invalid.");
        }
        byte[] tree = treeCapacity.ToArray();
        byte[] blocks = blockCapacity.ToArray();
        int usedWords = usedBlockBytes / 2;
        List<int> starts = [];
        for (int wordOffset = 0; wordOffset < usedWords; wordOffset++)
        {
            ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(wordOffset * 2, 2));
            if ((word & 0x8000) != 0)
                starts.Add(wordOffset);
        }
        if (starts.Count < 2 || starts[^1] != usedWords - 1 ||
            BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan(starts[^1] * 2, 2)) != 0x8000)
        {
            throw new InvalidDataException("The passive-moby collision blocks lost their terminal sentinel.");
        }
        Dictionary<int, int[]> groups = [];
        for (int group = 0; group < starts.Count - 1; group++)
        {
            int start = starts[group];
            int end = starts[group + 1];
            int[] indexes = new int[end - start];
            for (int index = 0; index < indexes.Length; index++)
            {
                ushort word = BinaryPrimitives.ReadUInt16LittleEndian(blocks.AsSpan((start + index) * 2, 2));
                if ((index == 0) != ((word & 0x8000) != 0))
                    throw new InvalidDataException("A collision group has an invalid start marker.");
                indexes[index] = word & 0x7FFF;
                if (indexes[index] >= triangleCount)
                    throw new InvalidDataException("A collision group references outside the triangle table.");
            }
            groups.Add(start, indexes);
        }

        int ReadWord(int offset)
        {
            if (offset < 0 || (offset & 1) != 0 || offset + 2 > tree.Length)
                throw new InvalidDataException("A collision tree pointer escaped its capacity.");
            return BinaryPrimitives.ReadUInt16LittleEndian(tree.AsSpan(offset, 2));
        }
        int ReadLength(int offset)
        {
            int length = ReadWord(offset);
            if (length > 256 || offset + ((length + 1) * 2L) > tree.Length)
                throw new InvalidDataException("A collision tree segment length is invalid.");
            return length;
        }

        Dictionary<UnusedLevel65PassiveMobyCollisionCell, CollisionCell> cells = [];
        int zLength = ReadLength(0);
        for (int z = 0; z < zLength; z++)
        {
            int yOffset = ReadWord((z + 1) * 2);
            if (yOffset == 0xFFFF)
                continue;
            int yLength = ReadLength(yOffset);
            for (int y = 0; y < yLength; y++)
            {
                int xOffset = ReadWord(yOffset + ((y + 1) * 2));
                if (xOffset == 0xFFFF)
                    continue;
                int xLength = ReadLength(xOffset);
                for (int x = 0; x < xLength; x++)
                {
                    int blockWordOffset = ReadWord(xOffset + ((x + 1) * 2));
                    if (blockWordOffset == 0xFFFF)
                        continue;
                    if (!groups.TryGetValue(blockWordOffset, out int[]? indexes))
                        throw new InvalidDataException("A collision tree cell points outside an exact group start.");
                    UnusedLevel65PassiveMobyCollisionCell key = new(x, y, z);
                    if (!cells.TryAdd(key, new(blockWordOffset, indexes)))
                        throw new InvalidDataException("A collision cell is duplicated in the tree.");
                }
            }
        }
        if (!cells.Values.Select(value => value.BlockWordOffset).Distinct().Order().SequenceEqual(groups.Keys.Order()))
            throw new InvalidDataException("The collision index has an unreferenced group.");
        return new(cells);
    }

    private static LogicalDiff CompareLogicalWad(
        FileStream before,
        FileStream after,
        DiscLayout layout,
        DiscFileRecord wad,
        IReadOnlyList<UnusedLevel65PassiveMobyPatch> patches,
        CancellationToken cancellationToken)
    {
        long changed = 0;
        long outside = 0;
        const int chunkBytes = 1 << 20;
        for (long offset = 0; offset < wad.Size; offset += chunkBytes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkBytes, wad.Size - offset));
            byte[] left = DiscImage.ReadFileBytes(before, layout, wad.Lba, offset, length);
            byte[] right = DiscImage.ReadFileBytes(after, layout, wad.Lba, offset, length);
            for (int index = 0; index < length; index++)
            {
                if (left[index] == right[index])
                    continue;
                changed++;
                long wadOffset = offset + index;
                bool allowed = patches.Any(patch =>
                    wadOffset >= patch.WadOffset &&
                    wadOffset < patch.WadOffset + Convert.FromHexString(patch.AfterHex).Length);
                if (!allowed)
                    outside++;
            }
        }
        return new(changed, outside);
    }

    private static PhysicalDiff ComparePhysicalImages(
        FileStream before,
        FileStream after,
        IReadOnlyList<int> allowedRawSectorLbas,
        CancellationToken cancellationToken)
    {
        HashSet<int> allowed = allowedRawSectorLbas.ToHashSet();
        byte[] left = new byte[RawSectorByteLength];
        byte[] right = new byte[RawSectorByteLength];
        List<UnusedLevel65PassiveMobyRawSectorDiff> diffs = [];
        long changedBytes = 0;
        int sectorCount = checked((int)(before.Length / RawSectorByteLength));
        before.Position = 0;
        after.Position = 0;
        for (int lba = 0; lba < sectorCount; lba++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ReadExactly(before, left);
            ReadExactly(after, right);
            if (left.AsSpan().SequenceEqual(right))
                continue;
            if (!allowed.Contains(lba))
                throw new InvalidDataException($"The passive-moby physical diff escaped to raw sector {lba}.");
            int header = CountDiff(left, right, 0, 16);
            int subheader = CountDiff(left, right, 16, 8);
            int payload = CountDiff(left, right, 24, 2048);
            int edc = CountDiff(left, right, 2072, 4);
            const int reserved = 0;
            int eccP = CountDiff(left, right, 2076, 172);
            int eccQ = CountDiff(left, right, 2248, 104);
            int total = header + subheader + payload + edc + reserved + eccP + eccQ;
            int actualTotal = CountDiff(left, right, 0, RawSectorByteLength);
            if (header != 0 || subheader != 0 || reserved != 0)
            {
                throw new InvalidDataException(
                    $"Passive-Moby sector {lba} changed forbidden MODE2 header/subheader/reserved bytes.");
            }
            if (total != actualTotal)
                throw new InvalidDataException($"Passive-Moby sector {lba} raw regions do not partition all changed bytes.");
            changedBytes += total;
            diffs.Add(new(lba, header, subheader, payload, edc, reserved, eccP, eccQ, total));
        }
        if (!diffs.Select(diff => diff.RawSectorLba).SequenceEqual(allowedRawSectorLbas))
            throw new InvalidDataException("The passive-moby writer did not change all six exact owned raw sectors.");
        return new(changedBytes, diffs);
    }

    private static void VerifyRawSectors(
        FileStream stream,
        DiscLayout layout,
        IReadOnlyList<int> rawSectorLbas)
    {
        int verified = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            stream,
            layout,
            rawSectorLbas.Select(lba => (lba, 1)).ToArray());
        if (verified != rawSectorLbas.Count)
            throw new InvalidDataException("The passive-moby raw-sector verification count changed.");
        foreach (int lba in rawSectorLbas)
            RawMode2Form1SectorIntegrity.VerifyDuplicatedSubmode(stream, layout, lba, XaDataSubmode);
    }

    private static int[] RawSectorLbasForWadRange(DiscLayout layout, long wadOffset, int byteLength)
    {
        long firstLogical = wadOffset / LogicalSectorByteLength;
        long lastLogical = (wadOffset + byteLength - 1) / LogicalSectorByteLength;
        return Enumerable.Range(
                checked(WadLba + (int)firstLogical),
                checked((int)(lastLogical - firstLogical + 1)))
            .ToArray();
    }

    private static string BuildChecklist(
        UnusedLevel65PassiveMobyRuntimeCandidatePlan plan,
        Readback readback,
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths)
    {
        VerifyLoadCodes(plan.LoadCodes);
        StringBuilder text = new();
        text.AppendLine("# ID65 T107 Artisans Grass 0x01F5 - Disposable Runtime Checklist");
        text.AppendLine();
        text.AppendLine("This Moby-only candidate installs the exact Artisans T121 Grass row, properties, pointer fixup, actor 0x01F5 package, root/id registration, count, and T107 placement into ID65. The foundation landing and T92 player anchor are byte-for-byte unchanged; music, totals, exit, save code, the executable, retail levels, overlays, and foundation terrain are also unchanged.");
        text.AppendLine();
        text.AppendLine($"- Profile: `{ProfileId}`");
        text.AppendLine($"- CUE: `{Path.GetFileName(paths.CuePath)}` (load the CUE, not the BIN)");
        text.AppendLine($"- Finder helper: `{Path.GetFileName(paths.RevealHelperPath)}`");
        text.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`");
        text.AppendLine($"- ID65 data SHA-256: `{readback.OutputId65DataSha256}`");
        text.AppendLine($"- Grass T107 placement: raw ({plan.AuthoredRawX}, {plan.AuthoredRawY}, {plan.AuthoredRawZ}), donor yaw byte `0x{plan.DonorYawByte:X2}`");
        text.AppendLine($"- Spawn separation: {plan.Support.DistanceFromFoundationSpawnRaw} raw from unchanged foundation spawn; {plan.Support.DistanceFromReservedStandaloneSpawnRaw} raw from the reserved standalone-spawn point; minimum {plan.Support.MinimumSpawnSeparationRaw} raw");
        text.AppendLine("- Closed dependency result: passive default dispatch; no controller/property-read, sound, particle, spawn, or dynamic-link dependency was found in the pinned static contract.");
        text.AppendLine("- Runtime status: **pending / unpromoted**. This is not editor/Create BIN/release integration.");
        text.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(text, plan.LoadCodes);
        text.AppendLine("## Safety setup");
        text.AppendLine();
        text.AppendLine("1. Disable DuckStation cheats and save states. Disable both memory-card slots. NO CARDS / DO NOT SAVE.");
        text.AppendLine("2. Boot this exact CUE. Reach controllable gameplay normally.");
        text.AppendLine("3. Load ID65: Select; R1, R2, L1, L2, R1, L1, R2, L2; Left; Down.");
        text.AppendLine();
        text.AppendLine("## Focused T107 Grass gate");
        text.AppendLine();
        text.AppendLine("- [ ] Entry and Spyro position are unchanged. Find exactly one conspicuous Grass model at the green T107 marker on the native apron; it must not appear at either spawn marker.");
        text.AppendLine("- [ ] At normal camera distance, the Grass is correctly shaped/textured and stable: no wrong actor, missing geometry, animation corruption, freeze, or crash.");
        text.AppendLine("- [ ] Walk far enough away to force far LOD, then return. The Grass remains the same actor with a valid far/near transition and no pop into an unrelated model.");
        text.AppendLine("- [ ] Remain nearby and move/rotate the camera for at least 60 seconds. The passive object does not begin controller behavior, emit sound/particles, spawn another Moby, or destabilize the level.");
        text.AppendLine("- [ ] Approach and pass around/through the passive decoration normally. Record what happens; this gate makes no collision/solidity claim for Grass.");
        text.AppendLine("- [ ] Reset/cold boot this same CUE, re-enter ID65, and confirm the same one Grass returns at T107 without a save state or card.");
        text.AppendLine("- [ ] Open pause/Inventory only to confirm normal responsiveness and TOWN SQUARE identity. DO NOT SAVE and do not exit the level.");
        text.AppendLine();
        text.AppendLine("## Isolation controls");
        text.AppendLine();
        text.AppendLine("- [ ] Retail Town Square loads at its normal landing.");
        text.AppendLine("- [ ] Gnasty's Loot and Sunny Flight still load normally.");
        text.AppendLine("- [ ] ID65 music, totals, exit behavior, and save ownership are outside this focused runtime gate; do not exercise saving.");
        text.AppendLine();
        text.AppendLine("PASS requires the exact one T107 Grass to survive normal view, far/near LOD, 60-second passive stability, reset/cold boot, and all three isolation controls. Any wrong/missing model, update behavior, sound/particle/spawn side effect, freeze, crash, or reset mismatch is a FAIL. The candidate remains runtime-pending and unpromoted until this checklist is completed.");
        return text.ToString();
    }

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        if (loadCodes.Count != 4 ||
            loadCodes[0].TestName != "ID65 candidate" || loadCodes[0].LevelId != 65 ||
            loadCodes[1].TestName != "Retail Town Square" || loadCodes[1].LevelId != 13 ||
            loadCodes[2].TestName != "Gnasty's Loot" || loadCodes[2].LevelId != 64 ||
            loadCodes[3].TestName != "Sunny Flight" || loadCodes[3].LevelId != 15 ||
            loadCodes.Any(code => string.IsNullOrWhiteSpace(code.InputCode)))
        {
            throw new InvalidDataException("The exact four passive-moby comparison load codes changed.");
        }
    }

    private static string BuildGuide(
        UnusedLevel65PassiveMobyRuntimeCandidatePlan plan,
        string outputImageSha256)
    {
        VerifyLoadCodes(plan.LoadCodes);
        string cueName = OutputPrefix + ".cue";
        StringBuilder svg = new();
        svg.AppendLine("<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"1000\" viewBox=\"0 0 1200 1000\">");
        svg.AppendLine("<rect width=\"1200\" height=\"1000\" fill=\"#10172a\"/>");
        svg.AppendLine("<text x=\"60\" y=\"58\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"30\" font-weight=\"700\">ID65 T107 Artisans Grass 0x01F5</text>");
        svg.AppendLine("<rect x=\"770\" y=\"25\" width=\"370\" height=\"48\" rx=\"9\" fill=\"#6f2433\" stroke=\"#ff9cac\" stroke-width=\"2\"/>");
        svg.AppendLine("<text x=\"955\" y=\"55\" text-anchor=\"middle\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"14\" font-weight=\"700\">RUNTIME PENDING / UNPROMOTED</text>");
        svg.AppendLine("<text x=\"60\" y=\"92\" fill=\"#b9c7e8\" font-family=\"sans-serif\" font-size=\"17\">Top-down location guide - Moby only; landing and T92 remain unchanged</text>");

        svg.AppendLine("<rect x=\"120\" y=\"120\" width=\"960\" height=\"365\" rx=\"12\" fill=\"#33445b\" stroke=\"#8ea2c8\" stroke-width=\"4\"/>");
        svg.AppendLine("<polygon points=\"210,360 950,360 580,140\" fill=\"#7b4bc4\" fill-opacity=\"0.72\" stroke=\"#d5b8ff\" stroke-width=\"4\"/>");
        svg.AppendLine("<text x=\"650\" y=\"150\" fill=\"#f0dcff\" font-family=\"sans-serif\" font-size=\"16\">foundation terrain (unchanged)</text>");
        svg.AppendLine("<circle cx=\"280\" cy=\"292\" r=\"34\" fill=\"#5cffad\" stroke=\"#ffffff\" stroke-width=\"6\"/>");
        svg.AppendLine("<path d=\"M280 326 L280 390\" stroke=\"#5cffad\" stroke-width=\"7\"/>");
        svg.AppendLine("<text x=\"330\" y=\"282\" fill=\"#5cffad\" font-family=\"sans-serif\" font-size=\"25\" font-weight=\"700\">T107 GRASS 0x01F5</text>");
        svg.AppendLine($"<text x=\"330\" y=\"312\" fill=\"#dcecff\" font-family=\"monospace\" font-size=\"15\">raw ({plan.AuthoredRawX}, {plan.AuthoredRawY}, {plan.AuthoredRawZ})</text>");
        svg.AppendLine("<text x=\"330\" y=\"338\" fill=\"#dcecff\" font-family=\"sans-serif\" font-size=\"15\">native T1353 support; 192 raw outside foundation</text>");
        svg.AppendLine("<circle cx=\"520\" cy=\"438\" r=\"16\" fill=\"#ff7f7f\" stroke=\"#ffffff\" stroke-width=\"3\"/>");
        svg.AppendLine("<text x=\"548\" y=\"444\" fill=\"#ffb1b1\" font-family=\"sans-serif\" font-size=\"15\">UNCHANGED SPAWN / T92 (1,985 raw away)</text>");
        svg.AppendLine("<circle cx=\"300\" cy=\"205\" r=\"14\" fill=\"#ffcf70\" stroke=\"#ffffff\" stroke-width=\"3\"/>");
        svg.AppendLine("<text x=\"330\" y=\"205\" fill=\"#ffdf98\" font-family=\"sans-serif\" font-size=\"15\">RESERVED STANDALONE-SPAWN</text>");
        svg.AppendLine("<text x=\"330\" y=\"228\" fill=\"#ffdf98\" font-family=\"sans-serif\" font-size=\"14\">680 raw from T107</text>");
        svg.AppendLine("<text x=\"155\" y=\"468\" fill=\"#b9c7e8\" font-family=\"sans-serif\" font-size=\"13\">Schematic markers; use the exact raw coordinates above.</text>");

        svg.AppendLine("<rect x=\"60\" y=\"510\" width=\"1080\" height=\"128\" rx=\"10\" fill=\"#18243d\" stroke=\"#425a82\" stroke-width=\"2\"/>");
        svg.AppendLine($"<text x=\"82\" y=\"540\" fill=\"#8fe1ff\" font-family=\"monospace\" font-size=\"13\">PROFILE: {XmlEscape(ProfileId)}</text>");
        svg.AppendLine($"<text x=\"82\" y=\"570\" fill=\"#ffffff\" font-family=\"monospace\" font-size=\"13\">CUE: {XmlEscape(cueName)}</text>");
        svg.AppendLine($"<text x=\"82\" y=\"600\" fill=\"#ffffff\" font-family=\"monospace\" font-size=\"13\">BIN SHA-256: {XmlEscape(outputImageSha256)}</text>");
        svg.AppendLine("<text x=\"82\" y=\"625\" fill=\"#ffcf70\" font-family=\"sans-serif\" font-size=\"13\">Load the exact CUE, never the BIN. Cheats/save states off. NO CARDS / DO NOT SAVE.</text>");

        svg.AppendLine("<text x=\"60\" y=\"674\" fill=\"#ffffff\" font-family=\"sans-serif\" font-size=\"21\" font-weight=\"700\">Exact comparison load codes</text>");
        int codeY = 706;
        foreach (RuntimeCandidateLoadCode code in plan.LoadCodes)
        {
            string label = $"{code.TestName} (ID{code.LevelId}): {code.InputCode}";
            svg.AppendLine($"<text x=\"72\" y=\"{codeY}\" fill=\"#dcecff\" font-family=\"monospace\" font-size=\"12\">{XmlEscape(label)}</text>");
            codeY += 28;
        }

        svg.AppendLine("<rect x=\"60\" y=\"826\" width=\"1080\" height=\"130\" rx=\"10\" fill=\"#192f2c\" stroke=\"#55b99b\" stroke-width=\"2\"/>");
        svg.AppendLine("<text x=\"82\" y=\"858\" fill=\"#8fffd4\" font-family=\"sans-serif\" font-size=\"18\" font-weight=\"700\">Runtime pass boundary</text>");
        svg.AppendLine("<text x=\"82\" y=\"885\" fill=\"#e6fff6\" font-family=\"sans-serif\" font-size=\"14\">Exactly one Grass: normal view, far/near LOD return, 60-second passive/no-update stability, reset/cold boot.</text>");
        svg.AppendLine("<text x=\"82\" y=\"910\" fill=\"#e6fff6\" font-family=\"sans-serif\" font-size=\"14\">No sound, particle, spawned-Moby, wrong-model, freeze, or crash; then load all three retail controls.</text>");
        svg.AppendLine("<text x=\"82\" y=\"937\" fill=\"#ffcf70\" font-family=\"sans-serif\" font-size=\"13\">Runtime pending only: no normal Create BIN, editor, save, card, or release promotion.</text>");
        svg.AppendLine("</svg>");
        return svg.ToString();
    }

    private static FileStream AcquireWriterLease(string leasePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(leasePath) ?? ".");
        FileStream lease;
        try
        {
            lease = new(
                leasePath,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.ReadWrite,
                bufferSize: 1,
                FileOptions.None);
        }
        catch (IOException ex) when (!OperatingSystem.IsWindows() &&
                                     (ex.HResult & 0xFFFF) is 11 or 35)
        {
            throw new IOException("Another passive-moby writer is active; no operation or output move was started.", ex);
        }
        try
        {
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    lease.Lock(0, 1);
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == 33)
                {
                    throw new IOException("Another passive-moby writer is active; no operation or output move was started.", ex);
                }
                return lease;
            }
            int fd = checked((int)lease.SafeFileHandle.DangerousGetHandle());
            if (NativeFlock(fd, 2 | 4) != 0)
            {
                int error = Marshal.GetLastPInvokeError();
                Win32Exception nativeError = new(error);
                if (error is 11 or 35)
                    throw new IOException("Another passive-moby writer is active; no operation or output move was started.", nativeError);
                throw new IOException($"The passive-moby OS lease failed (errno {error}: {nativeError.Message}).", nativeError);
            }
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int NativeFlock(int fileDescriptor, int operation);

    private static bool RecoverOwnedOperations(UnusedLevel65PassiveMobyRuntimeCandidatePaths paths)
    {
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        if (!File.Exists(journalPath))
            return false;
        RejectReparsePoint(journalPath, "passive-moby operation journal");
        Journal journal = JsonSerializer.Deserialize<Journal>(File.ReadAllText(journalPath), JsonOptions)
            ?? throw new InvalidDataException("The passive-moby recovery journal is invalid.");
        string expectedStage = Path.Combine(paths.OperationsDirectoryPath, StagePrefix + journal.OperationId);
        string expectedBackup = Path.Combine(paths.OperationsDirectoryPath, BackupPrefix + journal.OperationId);
        if (journal.SchemaVersion != JournalSchemaVersion ||
            journal.OperationKind != JournalOperationKind ||
            !Guid.TryParseExact(journal.OperationId, "N", out _) ||
            !PathEquals(journal.OutputDirectoryPath, paths.OutputDirectoryPath) ||
            !PathEquals(journal.StageDirectoryPath, expectedStage) ||
            !PathEquals(journal.BackupDirectoryPath, expectedBackup) ||
            !IsOwnedChild(journal.StageDirectoryPath, paths.OperationsDirectoryPath, StagePrefix) ||
            !IsOwnedChild(journal.BackupDirectoryPath, paths.OperationsDirectoryPath, BackupPrefix) ||
            journal.Phase is not (PhaseStaged or PhaseBackupIntent or PhasePreviousBackedUp or PhasePublishIntent or PhasePublished))
        {
            throw new InvalidDataException("The passive-moby recovery journal escaped its owned operation paths.");
        }

        bool outputExists = Directory.Exists(paths.OutputDirectoryPath);
        bool stageExists = Directory.Exists(journal.StageDirectoryPath);
        bool backupExists = Directory.Exists(journal.BackupDirectoryPath);
        if (outputExists)
            RejectReparsePoint(paths.OutputDirectoryPath, "published passive-moby candidate");
        if (stageExists)
            RejectReparsePoint(journal.StageDirectoryPath, "passive-moby stage");
        if (backupExists)
            RejectReparsePoint(journal.BackupDirectoryPath, "passive-moby backup");

        RecoveryAction action = journal.HadPreviousCandidate
            ? (journal.Phase, outputExists, stageExists, backupExists) switch
            {
                (PhaseStaged, true, true, false) => RecoveryAction.KeepOutputDeleteStage,
                (PhaseBackupIntent, true, true, false) => RecoveryAction.KeepOutputDeleteStage,
                (PhaseBackupIntent, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePreviousBackedUp, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePublishIntent, false, true, true) => RecoveryAction.RestoreBackupDeleteStage,
                (PhasePublishIntent, true, false, true) => RecoveryAction.DeleteOutputRestoreBackup,
                (PhasePublished, true, false, true) => RecoveryAction.DeleteOutputRestoreBackup,
                (PhasePublished, true, false, false) => RecoveryAction.KeepCommittedOutput,
                _ => RecoveryAction.FailClosed
            }
            : (journal.Phase, outputExists, stageExists, backupExists) switch
            {
                (PhaseStaged, false, true, false) => RecoveryAction.DeleteStage,
                (PhasePublishIntent, false, true, false) => RecoveryAction.DeleteStage,
                (PhasePublishIntent, true, false, false) => RecoveryAction.DeleteOutput,
                (PhasePublished, true, false, false) => RecoveryAction.DeleteOutput,
                _ => RecoveryAction.FailClosed
            };

        if (action == RecoveryAction.FailClosed)
        {
            throw new IOException(
                $"Passive-Moby recovery found an ambiguous state: phase={journal.Phase}, " +
                $"hadPrevious={journal.HadPreviousCandidate}, output={outputExists}, stage={stageExists}, backup={backupExists}.");
        }

        if (action == RecoveryAction.DeleteOutputRestoreBackup)
        {
            DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
            Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
        }
        else if (action == RecoveryAction.RestoreBackupDeleteStage)
        {
            Directory.Move(journal.BackupDirectoryPath, paths.OutputDirectoryPath);
        }
        else if (action == RecoveryAction.DeleteOutput)
        {
            DeleteOwnedDirectory(paths.OutputDirectoryPath, paths.PublicationParentPath, OutputDirectoryName);
        }
        if (action is RecoveryAction.KeepOutputDeleteStage or
            RecoveryAction.RestoreBackupDeleteStage or
            RecoveryAction.DeleteStage)
        {
            DeleteOwnedDirectory(journal.StageDirectoryPath, paths.OperationsDirectoryPath, StagePrefix);
        }

        if (Directory.Exists(journal.StageDirectoryPath) || Directory.Exists(journal.BackupDirectoryPath))
            throw new IOException("Passive-Moby recovery left an owned stage or backup directory.");
        File.Delete(journalPath);
        return true;
    }

    private static void WriteJournal(
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths,
        Journal journal)
    {
        Directory.CreateDirectory(paths.OperationsDirectoryPath);
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        string temporary = journalPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(journal, JsonOptions) + "\n", new UTF8Encoding(false));
        using (FileStream stream = new(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            stream.Flush(flushToDisk: true);
        File.Move(temporary, journalPath, overwrite: true);
    }

    private static void DeleteJournal(UnusedLevel65PassiveMobyRuntimeCandidatePaths paths)
    {
        string journalPath = Path.Combine(paths.OperationsDirectoryPath, JournalFileName);
        if (File.Exists(journalPath))
            File.Delete(journalPath);
    }

    private static void DeleteOwnedDirectory(string path, string expectedParent, string expectedNameOrPrefix)
    {
        string full = Path.GetFullPath(path);
        string parent = Path.GetDirectoryName(full) ?? "";
        string name = Path.GetFileName(full);
        bool exactOutput = string.Equals(expectedNameOrPrefix, OutputDirectoryName, StringComparison.Ordinal) &&
                           string.Equals(name, OutputDirectoryName, StringComparison.Ordinal);
        bool operation = !string.Equals(expectedNameOrPrefix, OutputDirectoryName, StringComparison.Ordinal) &&
                         name.StartsWith(expectedNameOrPrefix, StringComparison.Ordinal) &&
                         name.Length > expectedNameOrPrefix.Length;
        if (!PathEquals(parent, expectedParent) || (!exactOutput && !operation))
            throw new InvalidOperationException("Refused to delete a directory outside passive-moby ownership.");
        RejectReparsePoint(full, "owned passive-moby directory");
        Directory.Delete(full, recursive: true);
    }

    private static bool IsOwnedChild(string path, string parent, string prefix)
    {
        string full = Path.GetFullPath(path);
        return PathEquals(Path.GetDirectoryName(full), parent) &&
               Path.GetFileName(full).StartsWith(prefix, StringComparison.Ordinal) &&
               Path.GetFileName(full).Length > prefix.Length;
    }

    private static void RequireSafeRoles(
        string lockedBase,
        string foundation,
        string foundationCue,
        UnusedLevel65PassiveMobyRuntimeCandidatePaths paths)
    {
        foreach (string source in new[] { lockedBase, foundation, foundationCue })
        {
            if (IsDescendantOrEqual(source, paths.OutputDirectoryPath) ||
                IsDescendantOrEqual(paths.OutputDirectoryPath, source))
            {
                throw new InvalidOperationException("Passive-Moby source and output roles overlap.");
            }
        }
        foreach (string artifact in new[]
                 {
                     paths.ImagePath, paths.CuePath, paths.PlanPath, paths.ReceiptPath,
                     paths.ChecklistPath, paths.GuidePath, paths.RevealHelperPath
                 })
        {
            if (!IsDescendantOrEqual(artifact, paths.OutputDirectoryPath) || PathEquals(artifact, paths.OutputDirectoryPath))
                throw new InvalidOperationException("A passive-moby artifact escaped its output directory.");
        }
    }

    private static bool IsDescendantOrEqual(string candidate, string ancestor)
    {
        string child = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        string root = Path.GetFullPath(ancestor).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return child.StartsWith(root, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
               PathEquals(candidate, ancestor);
    }

    private static bool PathEquals(string? left, string? right) =>
        string.Equals(
            Path.GetFullPath(left ?? ""),
            Path.GetFullPath(right ?? ""),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void RejectReparsePoint(string path, string label)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidDataException($"The {label} is a reparse point.");
    }

    private static string RequireFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            throw new FileNotFoundException($"The {label} is missing.", path);
        string full = Path.GetFullPath(path);
        RejectReparsePoint(full, label);
        return full;
    }

    private static string RequireDirectory(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException($"The {label} is missing: {path}");
        string full = Path.GetFullPath(path);
        RejectReparsePoint(full, label);
        return full;
    }

    private static DiscFileRecord RequireFileRecord(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
            throw new InvalidDataException($"{name} resolved to LBA {record.Lba}/0x{record.Size:X}.");
        return record;
    }

    private static void RequireMode2(DiscLayout layout, string label)
    {
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException($"The {label} is not exact MODE2/2352.");
    }

    private static async Task RequireFileHashAsync(string path, string expected, CancellationToken cancellationToken) =>
        RequireTextHash(await HashFileAsync(path, cancellationToken), expected, Path.GetFileName(path));

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireHash(ReadOnlySpan<byte> bytes, string expected, string label) =>
        RequireTextHash(Hash(bytes), expected, label);

    private static void RequireTextHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"The {label} SHA-256 was {actual}, expected {expected}.");
    }

    private static void RequireHex(ReadOnlySpan<byte> bytes, string expected, string label)
    {
        if (!bytes.SequenceEqual(Convert.FromHexString(expected)))
            throw new InvalidDataException($"The exact {label} bytes changed.");
    }

    private static void RequireEqual(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected, string label)
    {
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"The exact {label} bytes changed.");
    }

    private static void RequireZero(ReadOnlySpan<byte> bytes, string label)
    {
        if (bytes.ContainsAnyExcept((byte)0))
            throw new InvalidDataException($"The exact {label} is no longer zero-filled.");
    }

    private static void RequireUInt32(ReadOnlySpan<byte> bytes, uint expected, string label)
    {
        uint actual = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        if (actual != expected)
            throw new InvalidDataException($"The {label} is 0x{actual:X}, expected 0x{expected:X}.");
    }

    private static byte[] UInt32Bytes(uint value)
    {
        byte[] bytes = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    private static byte[] UInt16Bytes(ushort value)
    {
        byte[] bytes = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes, value);
        return bytes;
    }

    private static UnusedLevel65PassiveMobyPatch Patch(
        string name,
        long wadOffset,
        byte[] before,
        byte[] after) =>
        new(name, wadOffset, Convert.ToHexString(before), Convert.ToHexString(after), CountDifferentBytes(before, after));

    private static int Distance2dRaw(int x1, int y1, int x2, int y2) =>
        checked((int)Math.Round(Math.Sqrt(Math.Pow(x1 - x2, 2) + Math.Pow(y1 - y2, 2)), MidpointRounding.AwayFromZero));

    private static int CountDifferentBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (left.Length != right.Length)
            throw new ArgumentException("Byte arrays must have equal length.");
        int count = 0;
        for (int index = 0; index < left.Length; index++)
            count += left[index] == right[index] ? 0 : 1;
        return count;
    }

    private static bool RangesOverlap(long leftOffset, int leftLength, long rightOffset, int rightLength) =>
        leftOffset < rightOffset + rightLength && rightOffset < leftOffset + leftLength;

    private static int CountDiff(byte[] left, byte[] right, int offset, int length)
    {
        int count = 0;
        for (int index = offset; index < offset + length; index++)
            count += left[index] == right[index] ? 0 : 1;
        return count;
    }

    private static void ReadExactly(Stream stream, byte[] buffer)
    {
        int total = 0;
        while (total < buffer.Length)
        {
            int read = stream.Read(buffer, total, buffer.Length - total);
            if (read == 0)
                throw new EndOfStreamException("The raw disc image ended early.");
            total += read;
        }
    }

    private static bool IsPendingHash(string hash) => hash.StartsWith("PENDING-", StringComparison.Ordinal);

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken) =>
        await WriteTextAsync(
            path,
            JsonSerializer.Serialize(value, JsonOptions) + "\n",
            new UTF8Encoding(false),
            cancellationToken);

    private static async Task WriteTextAsync(
        string path,
        string content,
        Encoding encoding,
        CancellationToken cancellationToken)
    {
        await File.WriteAllTextAsync(path, content, encoding, cancellationToken);
        await using FileStream stream = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.Read);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    private static string ShellSingleQuote(string value) => "'" + value.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string XmlEscape(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private sealed record Prepared(
        DiscLayout Layout,
        UnusedLevel65PassiveMobyRuntimeCandidatePlan Plan,
        UnusedLevel65StandaloneBehaviorOwnershipContract Ownership,
        UnusedLevel65MobyDependencyBundleContract ClosedContract,
        byte[] ExecutableBytes,
        byte[] BeforeLanding,
        byte[] BeforePlayerAnchor,
        string LockedBaseImagePath,
        string FoundationImagePath,
        string FoundationCuePath);

    private sealed record Journal(
        int SchemaVersion,
        string OperationKind,
        string OperationId,
        string OutputDirectoryPath,
        string Phase,
        string StageDirectoryPath,
        string BackupDirectoryPath,
        bool HadPreviousCandidate);

    private enum RecoveryAction
    {
        FailClosed,
        KeepOutputDeleteStage,
        RestoreBackupDeleteStage,
        DeleteOutputRestoreBackup,
        DeleteStage,
        DeleteOutput,
        KeepCommittedOutput
    }

    private sealed record CollisionCell(int BlockWordOffset, int[] TriangleIndexes);
    private sealed record CollisionIndex(IReadOnlyDictionary<UnusedLevel65PassiveMobyCollisionCell, CollisionCell> Cells);
    private sealed record CollisionSurfaceHit(int TriangleIndex, int RawZ, long NormalZ);
    private sealed record LogicalDiff(long ChangedBytes, long OutsideAllowedBytes);
    private sealed record PhysicalDiff(
        long ChangedBytes,
        IReadOnlyList<UnusedLevel65PassiveMobyRawSectorDiff> RawSectorDiffs);
    private sealed record Readback(
        string OutputImageSha256,
        string OutputId65DataSha256,
        long ChangedLogicalWadBytes,
        long ChangedPhysicalImageBytes,
        IReadOnlyList<UnusedLevel65PassiveMobyRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256);
}
