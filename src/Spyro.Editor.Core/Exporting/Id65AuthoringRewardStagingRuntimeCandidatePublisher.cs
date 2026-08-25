using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Exporting;

internal sealed record Id65AuthoringRewardStagingRuntimeCandidateRequest(
    string WorkspaceRoot,
    string BaseImagePath,
    string BaseCuePath,
    string OutputDirectoryPath,
    Id65RewardStagingCompileRequest CompileRequest,
    bool ReplaceExistingCandidate = false,
    Action<string>? TestStageHook = null);

internal sealed record Id65AuthoringRewardStagingRuntimeCandidatePaths(
    string OutputDirectoryPath,
    string OutputPrefix,
    string OutputImagePath,
    string OutputCuePath,
    string ConstructionPlanPath,
    string StaticReadbackReceiptPath,
    string RuntimeChecklistPath,
    string LocationGuidePath,
    string FinderHelperPath,
    string OperationsDirectoryPath,
    string GlobalWriterLeasePath);

internal sealed record Id65AuthoringRewardStagingRawSectorRun(int Lba, int SectorCount);

internal sealed record Id65AuthoringRewardStagingRawSectorDiff(
    int RawSectorLba,
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    int TotalChangedBytes);

internal sealed record Id65AuthoringRewardStagingPhysicalAggregate(
    int HeaderChangedBytes,
    int SubheaderChangedBytes,
    int PayloadChangedBytes,
    int EdcChangedBytes,
    int ReservedChangedBytes,
    int EccPChangedBytes,
    int EccQChangedBytes,
    long TotalChangedBytes);

internal sealed record Id65AuthoringRewardStagingRuntimeCandidatePlan
{
    public required int SchemaVersion { get; init; }
    public required string ProfileId { get; init; }
    public required string RuntimeEvidenceRelativePath { get; init; }
    public required string RuntimeEvidenceSha256 { get; init; }
    public required string RuntimeEvidenceSourceClosureSha256 { get; init; }
    public required string RuntimeEvidenceProfileSha256 { get; init; }
    public required string RuntimeEvidenceArtifactSetSha256 { get; init; }
    public required string RuntimeEvidencePayloadSha256 { get; init; }
    public required string RuntimeEvidenceContractSha256 { get; init; }
    public required bool SyntheticPublisherSmokeOnly { get; init; }
    public required string BaseImageSha256 { get; init; }
    public required string BaseCueSha256 { get; init; }
    public required string FoundationDonorImageSha256 { get; init; }
    public required string CollisionWindingRepairV2OutputDataSha256 { get; init; }
    public required string CollisionWindingRepairV2OutputModelSha256 { get; init; }
    public required string EnvironmentHeaderRepairV3OutputDataSha256 { get; init; }
    public required string EnvironmentHeaderRepairV3OutputModelSha256 { get; init; }
    public required string OutputImageSha256 { get; init; }
    public required string SourceWadSha256 { get; init; }
    public required string OutputWadSha256 { get; init; }
    public required string SourceRow80Sha256 { get; init; }
    public required string OutputRow80Sha256 { get; init; }
    public required string TargetOverlaySha256 { get; init; }
    public required string RetailTownSquareOverlaySha256 { get; init; }
    public required string GlobalExecutableSha256 { get; init; }
    public required string RetailTownSquareRowSha256 { get; init; }
    public required string CoreManifestSha256 { get; init; }
    public required string TextureWitnessSha256 { get; init; }
    public required string GrassBundleSha256 { get; init; }
    public required string GrassDescriptorFullFieldSha256 { get; init; }
    public required string DiffManifestSha256 { get; init; }
    public required string OwnedRangeMapSha256 { get; init; }
    public required string InverseRangeMapSha256 { get; init; }
    public required string TransactionSha256 { get; init; }
    public required string ProtectedComplementSha256 { get; init; }
    public required string DeterministicPlanSha256 { get; init; }
    public required string OutputModelSha256 { get; init; }
    public required int CompilerInvocationCount { get; init; }
    public required int LockedBinCopyCount { get; init; }
    public required int CompleteRow80WriteCount { get; init; }
    public required int WadLba { get; init; }
    public required int WadByteLength { get; init; }
    public required long Row80WadOffset { get; init; }
    public required int Row80ByteLength { get; init; }
    public required int Row80FirstRawSectorLba { get; init; }
    public required int Row80LastRawSectorLba { get; init; }
    public required int OwnedLeafCount { get; init; }
    public required int GuardedLogicalBytes { get; init; }
    public required int ChangedLogicalBytes { get; init; }
    public required int LogicalDiffRangeCount { get; init; }
    public required int ObjectCount { get; init; }
    public required int ActorRootCount { get; init; }
    public required int FixupCount { get; init; }
    public required int CollisionBlocksUsedBytes { get; init; }
    public required int RebuiltRawSectorCount { get; init; }
    public required int VerifiedRawSectorCount { get; init; }
    public required int[] AffectedRawSectorLbas { get; init; }
    public required Id65AuthoringRewardStagingRawSectorRun[] AffectedRawSectorRuns { get; init; }
    public required string AffectedRawSectorLbaSequenceSha256 { get; init; }
    public required string AffectedRawSectorRunListSha256 { get; init; }
    public required string RawSectorDiffSha256 { get; init; }
    public required long ChangedPhysicalImageBytes { get; init; }
    public required string OutsideAffectedRawSectorComplementSha256 { get; init; }
    public required string AffectedRawSectorsBeforeSha256 { get; init; }
    public required string AffectedRawSectorsAfterSha256 { get; init; }
    public required string AffectedPayloadsBeforeSha256 { get; init; }
    public required string AffectedPayloadsAfterSha256 { get; init; }
    public required Id65AuthoringRewardStagingPhysicalAggregate PhysicalAggregate { get; init; }
    public required int CoreSectorCount { get; init; }
    public required int ActiveCollisionCellCount { get; init; }
    public required int TextureCount { get; init; }
    public required int HighestTextureId { get; init; }
    public required int StationaryOwnerCount { get; init; }
    public required int StationaryRewardTotal { get; init; }
    public required int DragonCount { get; init; }
    public required int RequiredEggTarget { get; init; }
    public required IReadOnlyList<RuntimeCandidateLoadCode> LoadCodes { get; init; }
    public required bool FullReadbackFocusedRuntimePassPrerequisiteVerified { get; init; }
    public required bool CaptureJournalReadbackPrerequisiteVerified { get; init; }
    public required bool DirectLockedSourceCompilationVerified { get; init; }
    public required bool ExactForwardInverseVerified { get; init; }
    public required bool ExactLogicalDiffBoundaryVerified { get; init; }
    public required bool ExactPhysicalDiffBoundaryVerified { get; init; }
    public required bool RawMode2EdcEccVerified { get; init; }
    public required bool OverlayExecutableAndRetailControlsPreserved { get; init; }
    public required bool ProcessCrashRecoveryVerified { get; init; }
    public required bool PhaseBoundaryProcessCrashRecoveryVerified { get; init; }
    public required bool MidMutationProcessCrashRecoveryVerified { get; init; }
    public required bool PowerLossDurabilityVerified { get; init; }
    public required bool Full8AndMobile29Excluded { get; init; }
    public required bool Core4RuntimePending { get; init; }
    public required bool Total200RuntimeAccepted { get; init; }
    public required bool SaveRuntimeAccepted { get; init; }
    public required bool AppIntegrated { get; init; }
    public required bool NormalCreateBinEnabled { get; init; }
    public required bool PromotionAuthorized { get; init; }
    public required bool ReleaseAuthorized { get; init; }
}

internal sealed record Id65AuthoringRewardStagingRuntimeCandidateReceipt
{
    public required int SchemaVersion { get; init; }
    public required string ProfileId { get; init; }
    public required string WorkspaceRoot { get; init; }
    public required string BaseImagePath { get; init; }
    public required string BaseCuePath { get; init; }
    public required string FoundationDonorImagePath { get; init; }
    public required string OutputDirectoryPath { get; init; }
    public required string OutputImagePath { get; init; }
    public required string OutputCuePath { get; init; }
    public required string ConstructionPlanPath { get; init; }
    public required string StaticReadbackReceiptPath { get; init; }
    public required string RuntimeChecklistPath { get; init; }
    public required string LocationGuidePath { get; init; }
    public required string FinderHelperPath { get; init; }
    public required string RuntimeEvidenceRelativePath { get; init; }
    public required string RuntimeEvidenceSha256 { get; init; }
    public required string RuntimeEvidencePayloadSha256 { get; init; }
    public required string RuntimeEvidenceContractSha256 { get; init; }
    public required bool SyntheticPublisherSmokeOnly { get; init; }
    public required string BaseImageSha256 { get; init; }
    public required string BaseCueSha256 { get; init; }
    public required string FoundationDonorImageSha256 { get; init; }
    public required string CollisionWindingRepairV2OutputDataSha256 { get; init; }
    public required string CollisionWindingRepairV2OutputModelSha256 { get; init; }
    public required string EnvironmentHeaderRepairV3OutputDataSha256 { get; init; }
    public required string EnvironmentHeaderRepairV3OutputModelSha256 { get; init; }
    public required string OutputImageSha256 { get; init; }
    public required string SourceWadSha256 { get; init; }
    public required string OutputWadSha256 { get; init; }
    public required string SourceRow80Sha256 { get; init; }
    public required string OutputRow80Sha256 { get; init; }
    public required string OutputCueSha256 { get; init; }
    public required string ConstructionPlanSha256 { get; init; }
    public required string RuntimeChecklistSha256 { get; init; }
    public required string LocationGuideSha256 { get; init; }
    public required string FinderHelperSha256 { get; init; }
    public required string RawSectorDiffSha256 { get; init; }
    public required string TransactionSha256 { get; init; }
    public required string OutputModelSha256 { get; init; }
    public required int ChangedLogicalBytes { get; init; }
    public required long ChangedPhysicalImageBytes { get; init; }
    public required int RebuiltRawSectorCount { get; init; }
    public required int VerifiedRawSectorCount { get; init; }
    public required int RawSectorDiffCount { get; init; }
    public required Id65AuthoringRewardStagingRawSectorDiff[] RawSectorDiffs { get; init; }
    public required Id65AuthoringRewardStagingPhysicalAggregate PhysicalAggregate { get; init; }
    public required int ObjectCount { get; init; }
    public required int ActorRootCount { get; init; }
    public required int FixupCount { get; init; }
    public required int CollisionBlocksUsedBytes { get; init; }
    public required bool EvidenceTokenRevalidatedBeforeMutation { get; init; }
    public required bool CompilerInvokedExactlyTwice { get; init; }
    public required bool OneLockedBinCopyVerified { get; init; }
    public required bool OneCompleteRow80WriteVerified { get; init; }
    public required bool FullLogicalReadbackAndInverseVerified { get; init; }
    public required bool FullPhysicalImageReadbackVerified { get; init; }
    public required bool ExactMode2SectorSetRebuiltAndVerified { get; init; }
    public required bool BaseAndFoundationRepinnedAfterPublication { get; init; }
    public required bool OverlayExecutableAndRetailControlsPreserved { get; init; }
    public required bool CanonicalSidecarReadbackVerified { get; init; }
    public required bool SevenFilePublicationVerified { get; init; }
    public required bool FinderHandoffVerified { get; init; }
    public required bool RollbackRecoveryVerified { get; init; }
    public required bool ProcessCrashRecoveryVerified { get; init; }
    public required bool PhaseBoundaryProcessCrashRecoveryVerified { get; init; }
    public required bool MidMutationProcessCrashRecoveryVerified { get; init; }
    public required bool PowerLossDurabilityVerified { get; init; }
    public required bool DisposableRuntimeCandidateAuthorized { get; init; }
    public required bool Core4RuntimePending { get; init; }
    public required bool Total200RuntimeAccepted { get; init; }
    public required bool SaveRuntimeAccepted { get; init; }
    public required bool AppIntegrated { get; init; }
    public required bool NormalCreateBinEnabled { get; init; }
    public required bool PromotionAuthorized { get; init; }
    public required bool ReleaseAuthorized { get; init; }
}

internal sealed record Id65AuthoringRewardStagingRuntimeCandidateResult(
    Id65AuthoringRewardStagingRuntimeCandidatePaths Paths,
    Id65AuthoringRewardStagingRuntimeCandidatePlan Plan,
    Id65AuthoringRewardStagingRuntimeCandidateReceipt Receipt,
    RuntimeCandidateFinderReveal FinderReveal,
    IReadOnlyList<Id65AuthoringRewardStagingRawSectorDiff> RawSectorDiffs,
    string OutputImageSha256,
    string OutputWadSha256,
    string OutputRow80Sha256,
    string RawSectorDiffSha256,
    int ChangedLogicalBytes,
    long ChangedPhysicalImageBytes,
    int RebuiltRawSectorCount,
    int VerifiedRawSectorCount,
    bool RollbackRecoveryVerified,
    bool ProcessCrashRecoveryVerified,
    bool PhaseBoundaryProcessCrashRecoveryVerified,
    bool MidMutationProcessCrashRecoveryVerified,
    bool PowerLossDurabilityVerified,
    bool Core4RuntimePending,
    bool NormalCreateBinEnabled);

internal sealed record Id65AuthoringRewardStagingJournalFingerprintSmokeEntry(
    string FileName,
    string Sha256,
    int Device,
    ulong Inode,
    uint UserId,
    ushort Mode,
    ushort LinkCount,
    long ByteLength,
    long ModifiedSeconds,
    long ModifiedNanoseconds,
    long ChangedSeconds,
    long ChangedNanoseconds,
    int DirectoryDevice,
    ulong DirectoryInode,
    uint DirectoryUserId,
    ushort DirectoryMode);

internal static class Id65AuthoringRewardStagingRuntimeCandidatePublisher
{
    public const string ProfileId =
        "id65-authoring-reward-staging-core4-stationary171-v3-substrate-conditional-runtime-candidate-v2";
    public const string OutputDirectoryName =
        "unused-level-65-authoring-reward-staging-core4-v3-runtime-candidate";
    public const string OutputPrefix =
        "Unused-Level-65-Authoring-Reward-Staging-Core4-171-V3-RUNTIME-CANDIDATE";

    public const string ExpectedOutputImageSha256 =
        "85114fecd967cae824ecac0fdf4c890e704b728be56bd81e0665e5988e4d7863";
    public const string ExpectedOutputWadSha256 =
        "bea717f496d84607e51fc6a7940364099455759fe1e06b012de5e941211734d1";
    public const string ExpectedRawSectorDiffSha256 =
        "5b21133bdb75b33ebe25cb33cfe0e43a223d2dff6a3d22e28cf7a7b15b438121";
    public const string ExpectedAffectedLbaSequenceSha256 =
        "a895e1a8d6b2875206393a90a026f2e902248279735a31c4ae10932d1eba30f7";
    public const string ExpectedAffectedRunListSha256 =
        "cfcbcf152d410b8b73fb192cfb43278f679f1c297a5c7c8cdd014dcde353eb21";
    public const int ExpectedOutputCueByteLength = 134;
    public const string ExpectedOutputCueSha256 =
        "a490b43e1e823835a5d2a7fb3b056e8142e3147354fa394715ea4ec431804e1f";
    public const int ExpectedFinderHelperByteLength = 280;
    public const string ExpectedFinderHelperSha256 =
        "c460504293476b06edcf31e973dbb04562302af4f40631937d32b2100546f0bd";

    internal static uint FilesystemUserIdForSmoke() => RequireUnprivilegedCurrentIdentity();

    internal static UnixFileMode ObserveAtomicPrivateFileCreationModeForSmoke(string path)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        using FileStream stream = OpenNewExactFile(
            path,
            FileAccess.ReadWrite,
            FileShare.None,
            1,
            FileOptions.None,
            ExactPrivateFileMode());
        StableFileIdentity handle = RequireOwnedExactRegularHandleIdentity(
            stream,
            expectedUserId,
            ExactPrivateFileMode(),
            "atomic private-file creation smoke fixture");
        StableFileIdentity pathIdentity = RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            ExactPrivateFileMode(),
            "atomic private-file creation smoke fixture");
        if (!handle.SameFile(pathIdentity))
            throw new InvalidDataException("The atomic private-file creation smoke fixture changed identity.");
        return (UnixFileMode)(handle.Mode & PermissionMask);
    }

    internal static void ValidateUserIdentityForSmoke(uint userId, uint effectiveUserId) =>
        _ = RequireUnprivilegedCurrentIdentity(userId, effectiveUserId);

    internal static void ValidateRegularIdentityForSmoke(
        uint actualUserId,
        ushort linkCount,
        ushort fileType,
        UnixFileMode actualMode,
        UnixFileMode expectedMode)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        _ = RequireOwnedExactRegularIdentity(
            new(
                1,
                1,
                actualUserId,
                checked((ushort)(fileType | checked((ushort)actualMode))),
                linkCount,
                1,
                1,
                0,
                1,
                0),
            expectedUserId,
            expectedMode,
            "synthetic Core4 reward regular-file fixture");
    }

    internal static void ValidateDirectoryIdentityForSmoke(
        uint actualUserId,
        ushort fileType,
        UnixFileMode actualMode,
        UnixFileMode expectedMode)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        _ = RequireOwnedExactDirectoryIdentity(
            new(
                1,
                1,
                actualUserId,
                checked((ushort)(fileType | checked((ushort)actualMode))),
                2,
                1,
                1,
                0,
                1,
                0),
            expectedUserId,
            expectedMode,
            "synthetic Core4 reward directory fixture");
    }

    internal static void VerifyCandidateDirectoryRenameForSmoke(
        string stageDirectory,
        string outputDirectory,
        string backupDirectory)
    {
        CandidateTreeFingerprint[] staged = CaptureCandidateTreeFingerprint(stageDirectory);
        Directory.Move(stageDirectory, outputDirectory);
        RequireCandidateTreeFingerprint(outputDirectory, staged, "smoke stage-to-output rename");
        Directory.Move(outputDirectory, backupDirectory);
        RequireCandidateTreeFingerprint(backupDirectory, staged, "smoke output-to-backup rename");
    }

    internal static string HashCandidateTreeJournalFingerprintForSmoke(
        IReadOnlyList<Id65AuthoringRewardStagingJournalFingerprintSmokeEntry> entries) =>
        HashCandidateTreeJournalFingerprint(entries.Select(entry => new CandidateTreeFingerprint(
            entry.FileName,
            entry.Sha256,
            new(
                entry.Device,
                entry.Inode,
                entry.UserId,
                entry.Mode,
                entry.LinkCount,
                entry.ByteLength,
                entry.ModifiedSeconds,
                entry.ModifiedNanoseconds,
                entry.ChangedSeconds,
                entry.ChangedNanoseconds),
            new(
                entry.DirectoryDevice,
                entry.DirectoryInode,
                entry.DirectoryUserId,
                entry.DirectoryMode))).ToArray());

    internal static void VerifyLiveCandidateFingerprintForSmoke(string directory, Action mutate)
    {
        CandidateTreeFingerprint[] exact = CaptureCandidateTreeFingerprint(directory);
        mutate();
        RequireCandidateTreeFingerprint(directory, exact, "live mutation-restoration smoke candidate");
    }

    private const int PlanSchemaVersion = 2;
    private const int ReceiptSchemaVersion = 2;
    private const int JournalSchemaVersion = 2;
    private const string CandidateTreeJournalFingerprintDomain =
        "id65-core4-reward-candidate-tree-journal-fingerprint-v2\n";
    private const string OperationKind = "id65-authoring-reward-staging-core4-v3-runtime-candidate";
    private const string OperationDirectoryPrefix = "core4-reward-v3-runtime-candidate-";
    private const string OperationsDirectoryName = ".id65-core4-reward-v3-runtime-candidate-operations";
    private const string GlobalWriterLeaseName = ".id65-core4-reward-v3-runtime-candidate-writer.lease";
    private const string JournalFileName = "operation-journal.json";
    private const string JournalTemporaryFileName = JournalFileName + ".tmp";
    private const string PhaseCreated = "created";
    private const string PhaseStaged = "staged";
    private const string PhaseBackupIntent = "backup-intent";
    private const string PhasePreviousBackedUp = "previous-backed-up";
    private const string PhasePublishIntent = "publish-intent";
    private const string PhasePublished = "published";
    private const string PhaseRollbackRestoreIntent = "rollback-restore-intent";
    private const string PhaseRollbackRestored = "rollback-restored";
    private const string PhaseCommitted = "committed";
    private const string ActiveWriterLeaseMessage =
        "Another ID65 Core4 reward-staging runtime-candidate writer is active for this publication parent.";

    private const string LockedBaseImageRelativePath =
        "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/" +
        "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.bin";
    private const string LockedBaseCueRelativePath =
        "_local/v5-stone-hill-level-replacement/unused-level-65-display-name/" +
        "Unused-Level-65-Town-Square-independent-storage-with-Town-Square-display-name-RUNTIME-CANDIDATE.cue";
    private const string FoundationDonorImageRelativePath =
        "_local/v5-stone-hill-level-replacement/unused-level-65-full-authoring-foundation-native-membership/" +
        "Unused-Level-65-Full-Authoring-Foundation-HP-LP-45deg-NATIVE-MEMBERSHIP-RUNTIME-CANDIDATE.bin";
    private const string BaseImageSha256 =
        "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8";
    private const string BaseCueSha256 =
        "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2";
    private const string FoundationDonorImageSha256 =
        "92e4046ce4d14771ebb70a72c2a024b8e76f5575e38771f7067ff2b4303ac222";
    private const string SourceWadSha256 =
        "df09cdcfabd89eaad92deaab87ae467840330851906afa5924f19f36ced47bf8";
    private const string OutsideAffectedComplementSha256 =
        "00e4c3be54147800191fb40bb70a1bf674e3971059e6843c5524125eb1f47ed5";
    private const string AffectedRawBeforeSha256 =
        "66847b9c3c1cb1815750c751463adccf6698f0eb9f6a96d7857182a38f6ad697";
    private const string AffectedRawAfterSha256 =
        "212938b3f515476dbc784109f6c1ee98a0fca06cff3258904ed527aa262460d1";
    private const string AffectedPayloadBeforeSha256 =
        "8b80a6d9a4639a2f7d0d2cc4fbb231633c5a8821ecd68aef9fec80c1423de463";
    private const string AffectedPayloadAfterSha256 =
        "d8fe86c546fd3924cdebde46c74a1113ed62fc0dbce634598dc9d2598502288e";

    private const int RawSectorByteLength = 2352;
    private const int LogicalSectorByteLength = 2048;
    private const int UserDataOffset = 24;
    private const int WadLba = 37;
    private const int WadByteLength = 0x6C18800;
    private const long Row80WadOffset = 0x6936800;
    private const int Row80ByteLength = 0x2E2000;
    private const long OverlayWadOffset = 0x6927000;
    private const int OverlayByteLength = 0xF800;
    private const long RetailTownSquareRowWadOffset = 0x119E000;
    private const long RetailTownSquareOverlayWadOffset = 0x118E800;
    private const int ExecutableLba = 55_382;
    private const int ExecutableByteLength = 0x66000;
    private const int ExpectedAffectedRawSectorCount = 579;
    private const int ExpectedChangedLogicalBytes = 972_516;
    private const long ExpectedChangedPhysicalBytes = 1_132_455;
    private const int MaximumPlanBytes = 4 * 1024 * 1024;
    private const int MaximumReceiptBytes = 8 * 1024 * 1024;
    private const int MaximumChecklistBytes = 1024 * 1024;
    private const int MaximumLocationGuideBytes = 1024 * 1024;
    private const int MaximumCueBytes = 4096;
    private const int MaximumFinderHelperBytes = 4096;
    private const int MaximumJournalBytes = 64 * 1024;
    private const int LockExclusive = 2;
    private const int LockNonBlocking = 4;
    private const int LockUnlock = 8;
    private const int WindowsErrorLockViolation = 33;
    private const ushort PermissionMask = 0x0FFF;
    private const ushort GroupOrOtherWriteMask = 0x0012;
    private const ushort StickyBit = 0x0200;
    private const ushort RegularFileType = 0x8000;
    private const ushort DirectoryFileType = 0x4000;

    private static readonly Id65AuthoringRewardStagingRawSectorRun[] ExpectedAffectedRuns =
    [
        new(53_906, 257),
        new(54_351, 275),
        new(54_628, 20),
        new(54_833, 6),
        new(54_844, 2),
        new(54_850, 1),
        new(54_993, 5),
        new(55_127, 4),
        new(55_310, 7),
        new(55_380, 2)
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly JsonSerializerOptions JournalJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        WriteIndented = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true
    };

    public static Task<Id65AuthoringRewardStagingRuntimeCandidateResult> CreateAsync(
        Id65AuthoringRewardStagingRuntimeCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.TestStageHook is not null)
            throw new InvalidOperationException("TestStageHook is forbidden on the production publisher entrypoint.");
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token =
            Id65V3RuntimeEvidenceContract.AcquirePinnedFocusedRuntimePassToken(request.WorkspaceRoot);
        return CreateVerifiedAsync(request, token, cancellationToken);
    }

    public static Id65AuthoringRewardStagingRuntimeCandidatePaths CreatePaths(string outputDirectoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outputDirectoryPath);
        string output = Path.GetFullPath(outputDirectoryPath);
        if (!string.Equals(Path.GetFileName(output), OutputDirectoryName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Core4 reward-staging publication directory must be named exactly '{OutputDirectoryName}'.");
        }
        string parent = Path.GetDirectoryName(output) ??
            throw new InvalidOperationException("The Core4 reward-staging output has no parent directory.");
        return new(
            output,
            OutputPrefix,
            Path.Combine(output, OutputPrefix + ".bin"),
            Path.Combine(output, OutputPrefix + ".cue"),
            Path.Combine(output, OutputPrefix + "-construction-plan.json"),
            Path.Combine(output, OutputPrefix + "-static-readback-receipt.json"),
            Path.Combine(output, OutputPrefix + "-runtime-checklist.md"),
            Path.Combine(output, OutputPrefix + "-location-guide.svg"),
            Path.Combine(output, OutputPrefix + "-Reveal-in-Finder.command"),
            Path.Combine(parent, OperationsDirectoryName),
            Path.Combine(parent, GlobalWriterLeaseName));
    }

    private static async Task<Id65AuthoringRewardStagingRuntimeCandidateResult> CreateVerifiedAsync(
        Id65AuthoringRewardStagingRuntimeCandidateRequest request,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.CompileRequest);
        RequireAuthorizedTestStageHook(request.TestStageHook);
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, request.WorkspaceRoot);
        cancellationToken.ThrowIfCancellationRequested();

        Preflight preflight = await BuildReadOnlyPreflightAsync(request, expectedUserId, cancellationToken);
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths = CreatePaths(request.OutputDirectoryPath);
        ValidatePathTopology(preflight, paths);
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence =
            Id65V3RuntimeEvidenceContract.DescribeFocusedRuntimePassToken(token);
        cancellationToken.ThrowIfCancellationRequested();

        StableFileIdentity publicationParentIdentity = PreparePublicationParent(paths, expectedUserId);
        using GlobalWriterLease writerLease = AcquireGlobalWriterLease(
            paths.GlobalWriterLeasePath,
            publicationParentIdentity,
            expectedUserId);
        request.TestStageHook?.Invoke("after-global-writer-lease-acquired");
        writerLease.RequireCurrentIdentity("after-global-writer-lease-acquired");
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        ValidatePathTopology(preflight, paths);
        bool recoveryVerified = await RecoverOwnedOperationsAsync(
            paths,
            preflight,
            publicationParentIdentity,
            writerLease,
            evidence,
            token,
            cancellationToken);
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        request.TestStageHook?.Invoke("after-owned-recovery");
        writerLease.RequireCurrentIdentity("after-owned-recovery");
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        ValidatePathTopology(preflight, paths);
        await PrepareDestinationAsync(
            paths,
            request.ReplaceExistingCandidate,
            preflight,
            evidence,
            cancellationToken);
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        ValidatePathTopology(preflight, paths);

        RequirePinnedTrustedDirectory(
            Path.GetDirectoryName(paths.OutputDirectoryPath)!,
            publicationParentIdentity,
            expectedUserId,
            "Core4 reward publication parent before operation creation");
        StableFileIdentity operationsDirectoryIdentity = CreateOwnedExactDirectory(
            paths.OperationsDirectoryPath,
            expectedUserId,
            ExactPrivateDirectoryMode(),
            "new Core4 reward operations directory");
        string operationRoot = Path.Combine(
            paths.OperationsDirectoryPath,
            OperationDirectoryPrefix + Guid.NewGuid().ToString("N"));
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "Core4 reward operation");
        string stage = Path.Combine(operationRoot, "candidate");
        string backup = Path.Combine(operationRoot, "previous-candidate");
        string operationLeasePath = Path.Combine(operationRoot, "operation.lease");
        FileStream? operationLease = null;
        Publication? publication = null;
        StableFileIdentity? operationRootIdentity = null;
        bool operationLeaseDisposed = false;
        bool journalInitialized = false;
        bool committed = false;
        try
        {
            request.TestStageHook?.Invoke("before-operation-root-create");
            operationRootIdentity = CreateOwnedExactDirectory(
                operationRoot,
                expectedUserId,
                ExactPrivateDirectoryMode(),
                "new Core4 reward operation root");
            operationLease = OpenNewExactFile(
                operationLeasePath,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.Asynchronous | FileOptions.WriteThrough,
                ExactPrivateFileMode());
            SetExactHandleMode(operationLease, ExactPrivateFileMode(), "new Core4 reward operation lease");
            StableFileIdentity newLeaseHandle = RequireOwnedExactRegularHandleIdentity(
                operationLease,
                expectedUserId,
                ExactPrivateFileMode(),
                "new Core4 reward operation lease");
            StableFileIdentity newLeasePath = RequireOwnedExactRegularPathIdentity(
                operationLeasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                "new Core4 reward operation lease");
            if (!newLeaseHandle.SameFile(newLeasePath))
                throw new InvalidDataException("The new Core4 reward operation lease changed during creation.");
            publication = new(
                operationRoot,
                stage,
                backup,
                paths.OutputDirectoryPath,
                operationLeasePath,
                Directory.Exists(paths.OutputDirectoryPath),
                expectedUserId,
                publicationParentIdentity,
                operationsDirectoryIdentity,
                operationRootIdentity ?? throw new InvalidOperationException(
                    "The new Core4 reward operation lost its root identity."));
            publication.OperationLeaseIdentity = newLeaseHandle;
            request.TestStageHook?.Invoke("before-initial-journal-write");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "before-initial-journal-write");
            WriteJournal(publication, PhaseCreated, writerLease);
            journalInitialized = true;
            request.TestStageHook?.Invoke("after-created-journal");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-created-journal");
            publication.StagedDirectoryIdentity = CreateOwnedExactDirectory(
                stage,
                expectedUserId,
                ExactCandidateDirectoryMode(),
                "new staged Core4 reward candidate directory");
            StagedCandidate staged = await BuildAndVerifyStagedCandidateAsync(
                preflight,
                paths,
                stage,
                evidence,
                request.TestStageHook,
                cancellationToken);
            publication.StagedCandidateFingerprint = CaptureCandidateTreeFingerprint(stage);
            if (publication.StagedDirectoryIdentity is null ||
                publication.StagedDirectoryIdentity.Device !=
                    publication.StagedCandidateFingerprint[0].DirectoryIdentity.Device ||
                publication.StagedDirectoryIdentity.Inode !=
                    publication.StagedCandidateFingerprint[0].DirectoryIdentity.Inode)
            {
                throw new InvalidDataException(
                    "The staged Core4 reward candidate directory changed during construction.");
            }
            publication.StagedCandidateFingerprintSha256 = HashCandidateTreeJournalFingerprint(
                publication.StagedCandidateFingerprint);
            WriteJournal(publication, PhaseStaged, writerLease);
            publication.Phase = PhaseStaged;
            request.TestStageHook?.Invoke("after-staged-candidate-verified");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-staged-candidate-verified");
            cancellationToken.ThrowIfCancellationRequested();
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            if (publication.PreviousOutputExisted)
            {
                await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
                Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            }
            await BeginPublicationAsync(
                publication,
                request.ReplaceExistingCandidate,
                request.TestStageHook,
                paths,
                preflight,
                evidence,
                token,
                operationLease,
                writerLease,
                cancellationToken);
            await VerifyPublishedCandidateAsync(paths, staged, preflight, evidence, cancellationToken);
            await RepinInputsAsync(preflight, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            if (publication.StagedCandidateFingerprint is null)
                throw new InvalidOperationException("The published Core4 reward candidate lost its identity binding.");
            RequireCandidateTreeFingerprint(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprint,
                "published Core4 reward candidate immediately before commit");
            WriteJournal(publication, PhaseCommitted, writerLease);
            publication.Phase = PhaseCommitted;
            committed = true;
            request.TestStageHook?.Invoke("after-committed-journal");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-committed-journal");
            RequireCandidateTreeFingerprint(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprint,
                "committed Core4 reward candidate before backup cleanup");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, CancellationToken.None);
            await RepinInputsAsync(preflight, CancellationToken.None);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            request.TestStageHook?.Invoke("before-committed-backup-cleanup");
            writerLease.RequireCurrentIdentity("after before-committed-backup-cleanup hook");
            if (Directory.Exists(backup))
            {
                if (publication.PreviousCandidateFingerprint is null)
                    throw new IOException("The committed previous-candidate backup lost its identity binding.");
                RequireCandidateTreeFingerprint(
                    backup,
                    publication.PreviousCandidateFingerprint,
                    "committed previous Core4 reward backup before deletion");
                writerLease.RequireCurrentIdentity("immediately before committed backup deletion");
                DeleteOwnedDirectory(backup, operationRoot, "committed Core4 reward backup");
                writerLease.RequireCurrentIdentity("immediately after committed backup deletion");
            }
            request.TestStageHook?.Invoke("after-committed-backup-cleanup");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-committed-backup-cleanup");
            RequireCandidateTreeFingerprint(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprint,
                "committed Core4 reward candidate before return");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, CancellationToken.None);
            await RepinInputsAsync(preflight, CancellationToken.None);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "final committed cleanup boundary");
            Id65AuthoringRewardStagingRuntimeCandidateResult result = BuildResult(
                paths,
                staged,
                recoveryVerified);
            await operationLease.DisposeAsync();
            operationLeaseDisposed = true;
            writerLease.RequireCurrentIdentity("immediately before final committed operation cleanup");
            CleanupCompletedOperation(publication, writerLease);
            writerLease.RequireCurrentIdentity("immediately after final committed operation cleanup");
            writerLease.RequireCurrentIdentity("immediately before publisher return");
            return result;
        }
        catch (Exception failure)
        {
            if (!journalInitialized)
            {
                if (!operationLeaseDisposed && operationLease is not null)
                {
                    await operationLease.DisposeAsync();
                    operationLeaseDisposed = true;
                }
                try
                {
                    if (operationRootIdentity is null)
                    {
                        CleanupEmptyOperationsBeforeRootCreation(
                            operationRoot,
                            paths.OperationsDirectoryPath,
                            Path.GetDirectoryName(paths.OutputDirectoryPath)!,
                            expectedUserId,
                            publicationParentIdentity,
                            operationsDirectoryIdentity,
                            writerLease);
                    }
                    else
                    {
                        CleanupUnjournaledOperation(
                            operationRoot,
                            operationLeasePath,
                            Path.GetDirectoryName(paths.OutputDirectoryPath)!,
                            expectedUserId,
                            publicationParentIdentity,
                            operationsDirectoryIdentity,
                            operationRootIdentity,
                            writerLease);
                    }
                }
                catch (Exception cleanupFailure)
                {
                    throw new IOException(
                        "Core4 reward publication failed before its first atomic phase journal and cleanup is incomplete.",
                        new AggregateException(failure, cleanupFailure));
                }
                throw;
            }
            if (publication is null)
                throw new InvalidOperationException("A journaled Core4 reward operation lost its publication state.");
            if (operationLease is null)
                throw new InvalidOperationException("A journaled Core4 reward operation lost its live lease handle.");
            if (committed)
            {
                throw new IOException(
                    "The Core4 reward candidate committed successfully, but post-commit cleanup is incomplete; " +
                    "the committed output and journal were preserved for startup recovery.",
                    failure);
            }
            try
            {
                await RollBackPublicationAsync(
                    publication,
                    request.TestStageHook,
                    paths,
                    preflight,
                    evidence,
                    token,
                    operationLease,
                    writerLease,
                    CancellationToken.None);
            }
            catch (Exception recoveryFailure)
            {
                throw new IOException(
                    "Core4 reward-staging publication failed and recovery is incomplete; " +
                    "the owned journal/backup were preserved for the next call.",
                    new AggregateException(failure, recoveryFailure));
            }
            throw;
        }
        finally
        {
            if (!operationLeaseDisposed && operationLease is not null)
                await operationLease.DisposeAsync();
        }
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequireAuthorizedTestStageHook(Action<string>? testStageHook)
    {
        if (testStageHook is null)
            return;
        string? entryAssembly = Assembly.GetEntryAssembly()?.GetName().Name;
        if (entryAssembly != Id65V3RuntimeEvidenceContract.RequiredRewardPublisherSmokeAssemblyName)
        {
            throw new InvalidOperationException(
                "TestStageHook is restricted to the exact reward-publisher smoke entry assembly.");
        }
    }

    private static async Task<Preflight> BuildReadOnlyPreflightAsync(
        Id65AuthoringRewardStagingRuntimeCandidateRequest request,
        uint expectedUserId,
        CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("The exact seven-file Core4 Finder handoff requires macOS.");
        string workspace = RequireExistingDirectory(request.WorkspaceRoot, "Spyro Editor workspace root");
        string expectedBaseImage = GuardWorkspacePath(workspace, LockedBaseImageRelativePath);
        string expectedBaseCue = GuardWorkspacePath(workspace, LockedBaseCueRelativePath);
        string foundationImage = GuardWorkspacePath(workspace, FoundationDonorImageRelativePath);
        string baseImage = RequireExactRequestedFile(
            request.BaseImagePath,
            expectedBaseImage,
            "exact locked display-name BIN");
        string baseCue = RequireExactRequestedFile(
            request.BaseCuePath,
            expectedBaseCue,
            "exact locked display-name CUE");
        _ = RequireExactRequestedFile(
            foundationImage,
            foundationImage,
            "exact frozen foundation donor BIN");

        NativeLevelReplacementBaselineExporter.ValidateCue(baseCue, baseImage, "MODE2/2352");
        DiscLayout layout = DiscImage.DetectLayout(baseImage);
        RequireMode2Layout(layout, "locked display-name BIN");

        Task<FileIdentity> cueIdentityTask = ReadFileIdentityAsync(baseCue, cancellationToken);
        Task<FileIdentity> foundationIdentityTask = ReadFileIdentityAsync(foundationImage, cancellationToken);
        Id65RewardCensusSourceSnapshot snapshot = Id65RewardCensusContract.ReadLockedSource(baseImage);
        FileIdentity cueIdentity = await cueIdentityTask;
        FileIdentity foundationIdentity = await foundationIdentityTask;
        RequireHash(snapshot.SourceImageSha256, BaseImageSha256, "locked display-name BIN");
        RequireHash(cueIdentity.Sha256, BaseCueSha256, "locked display-name CUE");
        RequireHash(foundationIdentity.Sha256, FoundationDonorImageSha256, "frozen foundation donor BIN");
        if (snapshot.SectorSize != RawSectorByteLength || snapshot.UserDataOffset != UserDataOffset ||
            snapshot.WadLba != WadLba || snapshot.WadByteLength != WadByteLength)
        {
            throw new InvalidDataException("The locked display-name disc/WAD envelope changed.");
        }

        Id65RewardStagingCompileRequest desired = request.CompileRequest;
        if (!desired.Source.CopyRow80().SequenceEqual(snapshot.Id65Data.Span) ||
            !desired.Source.CopyTargetOverlay().SequenceEqual(snapshot.Id65Overlay.Span) ||
            !desired.Source.CopyGlobalExecutable().SequenceEqual(snapshot.Executable.Span))
        {
            throw new InvalidDataException(
                "The desired-state compile request source is not the exact locked display-name row/overlay/executable.");
        }
        if (desired.Source.Row80Sha256 != Id65RewardCensusContract.Id65DataSha256 ||
            desired.Source.TargetOverlaySha256 != Id65RewardCensusContract.Id65OverlaySha256 ||
            desired.Source.GlobalExecutableSha256 != Id65RewardCensusContract.ExecutableSha256 ||
            desired.Source.TextureWitnessSha256 != Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256 ||
            desired.GrassBundle.CanonicalSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256)
        {
            throw new InvalidDataException("The desired-state compile request dependency identity changed.");
        }

        Id65CompiledRewardStaging first = Id65AuthoringRewardStagingCompiler.Compile(desired);
        Id65CompiledRewardStaging second = Id65AuthoringRewardStagingCompiler.Compile(desired);
        RequireEquivalentCompilations(first, second);
        ValidateFrozenCompilation(first);

        FileIdentity baseAfterCompile = await ReadFileIdentityAsync(baseImage, cancellationToken);
        FileIdentity foundationAfterCompile = await ReadFileIdentityAsync(foundationImage, cancellationToken);
        RequireHash(baseAfterCompile.Sha256, BaseImageSha256, "locked display-name BIN after compilation");
        RequireHash(
            foundationAfterCompile.Sha256,
            FoundationDonorImageSha256,
            "frozen foundation donor BIN after compilation");

        LogicalDiffRun[] logicalRuns = BuildLogicalDiffRuns(
            first.CopySourceRow80(),
            first.CopyOutputRow80());
        ValidateLogicalRunsAgainstCompiler(first, logicalRuns);
        int[] affectedLbas = DeriveAffectedRawSectorLbas(logicalRuns);
        Id65AuthoringRewardStagingRawSectorRun[] affectedRuns = CoalesceRawSectorRuns(affectedLbas);
        ValidateFrozenAffectedSectorSet(affectedLbas, affectedRuns);
        return new(
            expectedUserId,
            workspace,
            baseImage,
            baseCue,
            foundationImage,
            first,
            logicalRuns,
            affectedLbas,
            affectedRuns,
            cueIdentity.ByteLength,
            foundationIdentity.ByteLength);
    }

    private static void ValidateFrozenCompilation(Id65CompiledRewardStaging compiled)
    {
        if (compiled.ProfileId != Id65AuthoringRewardStagingCompiler.ProfileId ||
            compiled.Manifest.Kind != Id65RewardStagingProfileKind.Core4Stationary171 ||
            compiled.ManifestSha256 != Id65AuthoringRewardStagingCompiler.ExpectedCoreManifestSha256 ||
            compiled.SourceRow80Sha256 != Id65RewardCensusContract.Id65DataSha256 ||
            compiled.OutputRow80Sha256 != Id65AuthoringRewardStagingCompiler.ExpectedOutputRow80Sha256 ||
            compiled.TargetOverlaySha256 != Id65RewardCensusContract.Id65OverlaySha256 ||
            compiled.GlobalExecutableSha256 != Id65RewardCensusContract.ExecutableSha256 ||
            compiled.Manifest.GrassBundleSha256 !=
                Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256 ||
            compiled.DescriptorFullFieldSha256 !=
                Id65AuthoringRewardStagingCompiler.ExpectedGrassDescriptorFullFieldSha256 ||
            compiled.OwnedLeafCount != Id65AuthoringRewardStagingCompiler.ExpectedOwnedLeafCount ||
            compiled.GuardedByteCount != Id65AuthoringRewardStagingCompiler.ExpectedGuardedByteCount ||
            compiled.ChangedByteCount != Id65AuthoringRewardStagingCompiler.ExpectedChangedByteCount ||
            compiled.DiffRangeCount != Id65AuthoringRewardStagingCompiler.ExpectedDiffRangeCount ||
            compiled.DiffManifestSha256 != Id65AuthoringRewardStagingCompiler.ExpectedDiffManifestSha256 ||
            compiled.OwnedRangeMapSha256 != Id65AuthoringRewardStagingCompiler.ExpectedOwnedRangeMapSha256 ||
            compiled.InverseRangeMapSha256 != Id65AuthoringRewardStagingCompiler.ExpectedInverseRangeMapSha256 ||
            compiled.TransactionSha256 != Id65AuthoringRewardStagingCompiler.ExpectedTransactionSha256 ||
            compiled.ProtectedComplementSha256 !=
                Id65AuthoringRewardStagingCompiler.ExpectedProtectedComplementSha256 ||
            compiled.DeterministicPlanSha256 !=
                Id65AuthoringRewardStagingCompiler.ExpectedDeterministicPlanSha256 ||
            compiled.Structure.ModelSha256 != Id65AuthoringSupportReplacementCompiler.ExpectedCoreModelSha256 ||
            compiled.Structure.ObjectCount != 107 || compiled.Structure.ActorRootCount != 38 ||
            compiled.Structure.FixupCount != 0x81 || compiled.Structure.CollisionBlocksUsedBytes != 0x82 ||
            compiled.Structure.CoreSectorCount != 4 || compiled.Structure.ActiveCollisionCellCount != 16 ||
            compiled.Structure.TextureCount != 67 || compiled.Structure.HighestTextureId != 66 ||
            compiled.Stationary.OwnerCount != 71 || compiled.Stationary.RewardTotal != 171 ||
            compiled.Dragons.Count != 4 || compiled.ZeroEgg.RequiredEggTarget != 0 ||
            !compiled.ExactInverseVerified || !compiled.ProtectedComplementVerified ||
            !compiled.DeterministicReadbackVerified || !compiled.Full8Excluded ||
            !compiled.ExecutableMutationExcluded ||
            !compiled.V3EnvironmentHeaderRepairSubstrateRequired ||
            compiled.AcceptsCompiledSupportAfterimage ||
            compiled.AcceptsCompiledCompositeAfterimage || compiled.AcceptsCompiledMobyAfterimage ||
            compiled.AfterimageStackingAuthorized || compiled.WriterAuthorized ||
            compiled.RuntimeCandidateAuthorized || compiled.RuntimeProven || compiled.Publishable ||
            compiled.TreasureTotal200Proven || compiled.SavePersistenceProven ||
            compiled.AppIntegrated || compiled.NormalCreateBinEnabled || compiled.PromotionAuthorized ||
            compiled.ReleaseAuthorized)
        {
            throw new InvalidDataException("The exact frozen Core4 reward compilation or safety boundary changed.");
        }
    }

    private static void RequireEquivalentCompilations(
        Id65CompiledRewardStaging first,
        Id65CompiledRewardStaging second)
    {
        if (first.ManifestSha256 != second.ManifestSha256 ||
            first.SourceRow80Sha256 != second.SourceRow80Sha256 ||
            first.OutputRow80Sha256 != second.OutputRow80Sha256 ||
            first.ChangedByteCount != second.ChangedByteCount ||
            first.DiffRangeCount != second.DiffRangeCount ||
            first.DiffManifestSha256 != second.DiffManifestSha256 ||
            first.OwnedRangeMapSha256 != second.OwnedRangeMapSha256 ||
            first.InverseRangeMapSha256 != second.InverseRangeMapSha256 ||
            first.TransactionSha256 != second.TransactionSha256 ||
            first.ProtectedComplementSha256 != second.ProtectedComplementSha256 ||
            first.DeterministicPlanSha256 != second.DeterministicPlanSha256 ||
            !first.CopyOutputRow80().SequenceEqual(second.CopyOutputRow80()))
        {
            throw new InvalidDataException("Two direct locked-source reward compilations were not byte-deterministic.");
        }
    }

    private static LogicalDiffRun[] BuildLogicalDiffRuns(byte[] before, byte[] after)
    {
        if (before.Length != Row80ByteLength || after.Length != Row80ByteLength)
            throw new InvalidDataException("The Core4 logical diff source/output row length changed.");
        List<LogicalDiffRun> runs = [];
        int index = 0;
        while (index < before.Length)
        {
            if (before[index] == after[index])
            {
                index++;
                continue;
            }
            int start = index++;
            while (index < before.Length && before[index] != after[index])
                index++;
            int length = index - start;
            runs.Add(new(
                start,
                length,
                Hash(before.AsSpan(start, length)),
                Hash(after.AsSpan(start, length))));
        }
        return runs.ToArray();
    }

    private static void ValidateLogicalRunsAgainstCompiler(
        Id65CompiledRewardStaging compiled,
        IReadOnlyList<LogicalDiffRun> runs)
    {
        if (runs.Count != compiled.DiffRangeCount || runs.Count != 42_015 ||
            runs.Sum(item => item.ByteLength) != compiled.ChangedByteCount ||
            runs.Sum(item => item.ByteLength) != ExpectedChangedLogicalBytes)
        {
            throw new InvalidDataException("The actual unequal-byte runs differ from the frozen compiler diff counts.");
        }
        for (int index = 0; index < runs.Count; index++)
        {
            LogicalDiffRun actual = runs[index];
            Id65RewardStagingDiffRange declared = compiled.DiffRanges[index];
            if (actual.DataRelativeOffset != declared.DataRelativeOffset ||
                actual.ByteLength != declared.ByteLength || actual.BeforeSha256 != declared.BeforeSha256 ||
                actual.AfterSha256 != declared.AfterSha256)
            {
                throw new InvalidDataException($"Compiler logical diff range {index} differs from actual row bytes.");
            }
        }
    }

    private static int[] DeriveAffectedRawSectorLbas(IEnumerable<LogicalDiffRun> logicalRuns)
    {
        SortedSet<int> lbas = [];
        foreach (LogicalDiffRun run in logicalRuns)
        {
            long wadStart = checked(Row80WadOffset + run.DataRelativeOffset);
            long wadEnd = checked(wadStart + run.ByteLength - 1L);
            int first = checked(WadLba + (int)(wadStart / LogicalSectorByteLength));
            int last = checked(WadLba + (int)(wadEnd / LogicalSectorByteLength));
            for (int lba = first; lba <= last; lba++)
                lbas.Add(lba);
        }
        return lbas.ToArray();
    }

    private static Id65AuthoringRewardStagingRawSectorRun[] CoalesceRawSectorRuns(int[] lbas)
    {
        if (lbas.Length == 0)
            return [];
        List<Id65AuthoringRewardStagingRawSectorRun> runs = [];
        int start = lbas[0];
        int previous = start;
        foreach (int lba in lbas.Skip(1))
        {
            if (lba == previous + 1)
            {
                previous = lba;
                continue;
            }
            runs.Add(new(start, previous - start + 1));
            start = previous = lba;
        }
        runs.Add(new(start, previous - start + 1));
        return runs.ToArray();
    }

    private static void ValidateFrozenAffectedSectorSet(
        int[] lbas,
        Id65AuthoringRewardStagingRawSectorRun[] runs)
    {
        string lbaManifest = string.Join(
            '\n',
            lbas.Select(lba => lba.ToString(CultureInfo.InvariantCulture))) + "\n";
        string runManifest = string.Join(
            '\n',
            runs.Select(run => string.Create(
                CultureInfo.InvariantCulture,
                $"{run.Lba}|{run.SectorCount}"))) + "\n";
        if (lbas.Length != ExpectedAffectedRawSectorCount || lbas[0] != 53_906 || lbas[^1] != 55_381 ||
            !runs.SequenceEqual(ExpectedAffectedRuns) || HashUtf8(lbaManifest) != ExpectedAffectedLbaSequenceSha256 ||
            HashUtf8(runManifest) != ExpectedAffectedRunListSha256)
        {
            throw new InvalidDataException("The exact compiler-derived 579-sector/ten-run rebuild set changed.");
        }
    }

    private static void ValidatePathTopology(
        Preflight preflight,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
    {
        string outputParent = Path.GetDirectoryName(paths.OutputDirectoryPath) ??
            throw new InvalidOperationException("The Core4 reward output has no parent.");
        string filesystemRoot = Path.GetPathRoot(paths.OutputDirectoryPath) ??
            throw new InvalidOperationException("The Core4 reward output has no filesystem root.");
        string userProfile = Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        if (PathEquals(paths.OutputDirectoryPath, filesystemRoot) || PathEquals(outputParent, filesystemRoot) ||
            PathEquals(paths.OutputDirectoryPath, userProfile) || PathEquals(outputParent, userProfile))
        {
            throw new InvalidOperationException(
                "The Core4 reward output and its lease/operations parent may not be a filesystem root or user profile.");
        }
        if (!PathEquals(Path.GetDirectoryName(paths.OperationsDirectoryPath), outputParent) ||
            !PathEquals(Path.GetDirectoryName(paths.GlobalWriterLeasePath), outputParent) ||
            PathEquals(paths.OutputDirectoryPath, paths.OperationsDirectoryPath) ||
            PathEquals(paths.OutputDirectoryPath, paths.GlobalWriterLeasePath) ||
            PathEquals(paths.OperationsDirectoryPath, paths.GlobalWriterLeasePath))
        {
            throw new InvalidOperationException("The Core4 reward output, operation, and lease roles overlap.");
        }
        RejectExistingAncestorReparsePoints(paths.OutputDirectoryPath, "Core4 reward output");
        RejectExistingAncestorReparsePoints(paths.OperationsDirectoryPath, "Core4 reward operations");
        RejectExistingAncestorReparsePoints(paths.GlobalWriterLeasePath, "Core4 reward writer lease");
        if (File.Exists(paths.OutputDirectoryPath))
            throw new InvalidDataException("The Core4 reward output path is a file instead of a directory.");
        if (File.Exists(paths.OperationsDirectoryPath))
            throw new InvalidDataException("The Core4 reward operations path is a file instead of a directory.");
        string[] sources = [preflight.BaseImagePath, preflight.BaseCuePath, preflight.FoundationImagePath];
        foreach (string source in sources)
        {
            if (IsDescendantOrEqual(source, paths.OutputDirectoryPath) ||
                IsDescendantOrEqual(paths.OutputDirectoryPath, source) ||
                IsDescendantOrEqual(source, paths.OperationsDirectoryPath) ||
                IsDescendantOrEqual(paths.OperationsDirectoryPath, source))
            {
                throw new InvalidOperationException("A Core4 reward source overlaps an owned output/operation path.");
            }
        }
    }

    private static async Task<StagedCandidate> BuildAndVerifyStagedCandidateAsync(
        Preflight preflight,
        Id65AuthoringRewardStagingRuntimeCandidatePaths finalPaths,
        string stagedDirectory,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        Action<string>? testStageHook,
        CancellationToken cancellationToken)
    {
        string stagedImage = StagedPath(stagedDirectory, finalPaths.OutputImagePath);
        string stagedCue = StagedPath(stagedDirectory, finalPaths.OutputCuePath);
        string stagedPlan = StagedPath(stagedDirectory, finalPaths.ConstructionPlanPath);
        string stagedReceipt = StagedPath(stagedDirectory, finalPaths.StaticReadbackReceiptPath);
        string stagedChecklist = StagedPath(stagedDirectory, finalPaths.RuntimeChecklistPath);
        string stagedGuide = StagedPath(stagedDirectory, finalPaths.LocationGuidePath);

        await StageOwnedImageAsync(
            preflight.BaseImagePath,
            stagedImage,
            preflight.ExpectedUserId,
            testStageHook,
            cancellationToken);
        FileIdentity stagedCopyIdentity = await ReadFileIdentityAsync(stagedImage, cancellationToken);
        RequireHash(stagedCopyIdentity.Sha256, BaseImageSha256, "single staged locked-BIN copy");

        int rebuiltRawSectorCount;
        int verifiedRawSectorCount;
        DiscLayout layout = DiscImage.DetectLayout(stagedImage);
        RequireMode2Layout(layout, "staged Core4 reward candidate");
        await using (FileStream image = new(
                         stagedImage,
                         FileMode.Open,
                         FileAccess.ReadWrite,
                         FileShare.None,
                         1024 * 1024,
                         FileOptions.Asynchronous | FileOptions.WriteThrough))
        {
            _ = RequireRootFile(image, layout, "WAD.WAD", WadLba, WadByteLength);
            byte[] sourceRow = DiscImage.ReadFileBytes(
                image,
                layout,
                WadLba,
                Row80WadOffset,
                Row80ByteLength);
            if (!sourceRow.SequenceEqual(preflight.Compiled.CopySourceRow80()))
                throw new InvalidDataException("The staged locked BIN does not contain the compiled row-80 preimage.");

            // This is deliberately the sole disc-image payload write: one complete row-80
            // afterimage derived directly from the immutable locked-source compile request.
            DiscImage.WriteFileBytes(
                image,
                layout,
                WadLba,
                Row80WadOffset,
                preflight.Compiled.CopyOutputRow80());
            rebuiltRawSectorCount = RawMode2Form1SectorIntegrity.RebuildFileRanges(
                image,
                layout,
                WadLba,
                preflight.LogicalRuns.Select(run =>
                    (Row80WadOffset + run.DataRelativeOffset, run.ByteLength)));
            if (rebuiltRawSectorCount != preflight.AffectedRawSectorLbas.Length)
            {
                throw new InvalidDataException(
                    $"The Core4 reward transaction rebuilt {rebuiltRawSectorCount} sectors, " +
                    $"expected {preflight.AffectedRawSectorLbas.Length}.");
            }
            verifiedRawSectorCount = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
                image,
                layout,
                preflight.AffectedRawSectorRuns.Select(run => (run.Lba, run.SectorCount)));
            if (verifiedRawSectorCount != preflight.AffectedRawSectorLbas.Length)
                throw new InvalidDataException("The Core4 reward rebuilt MODE2 sector set failed exact integrity verification.");
            await image.FlushAsync(cancellationToken);
            image.Flush(flushToDisk: true);
        }

        string cueText = DiscImage.BuildCueText(preflight.BaseCuePath, Path.GetFileName(stagedImage));
        await WriteTextAsync(stagedCue, cueText, Encoding.ASCII, cancellationToken);
        NativeLevelReplacementBaselineExporter.ValidateCue(stagedCue, stagedImage, "MODE2/2352");
        FileIdentity cueIdentity = await ReadFileIdentityAsync(stagedCue, cancellationToken);
        if (cueIdentity.ByteLength != ExpectedOutputCueByteLength ||
            cueIdentity.Sha256 != ExpectedOutputCueSha256)
            throw new InvalidDataException("The exact Core4 reward CUE bytes changed.");

        CandidateReadback readback = await VerifyCandidateImageAsync(
            preflight,
            stagedImage,
            rebuiltRawSectorCount,
            verifiedRawSectorCount,
            cancellationToken);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        Id65AuthoringRewardStagingRuntimeCandidatePlan plan = BuildPlan(
            preflight,
            evidence,
            readback,
            loadCodes);
        await WriteJsonAsync(stagedPlan, plan, cancellationToken);

        RuntimeCandidateFinderReveal stagedReveal =
            await RuntimeCandidateTestHandoff.WriteFinderRevealHelperAsync(stagedCue, cancellationToken);
        NormalizeNewOwnedRegularPath(
            stagedReveal.HelperPath,
            preflight.ExpectedUserId,
            ExactFinderMode(),
            "new staged Core4 reward Finder helper");
        FileIdentity helperIdentity = await ReadFileIdentityAsync(stagedReveal.HelperPath, cancellationToken);
        if (helperIdentity.ByteLength != ExpectedFinderHelperByteLength ||
            helperIdentity.Sha256 != ExpectedFinderHelperSha256 ||
            !OperatingSystem.IsMacOS() ||
            File.GetUnixFileMode(stagedReveal.HelperPath) != ExactFinderMode())
        {
            throw new InvalidDataException("The Core4 reward Finder helper bytes or exact 0755 mode changed.");
        }
        RuntimeCandidateFinderReveal finalReveal = BuildFinalFinderReveal(finalPaths);
        string checklist = BuildRuntimeChecklist(finalPaths, finalReveal, plan, readback, loadCodes);
        string guide = BuildLocationGuideSvg(plan, readback, loadCodes);
        await WriteTextAsync(stagedChecklist, checklist, new UTF8Encoding(false), cancellationToken);
        await WriteTextAsync(stagedGuide, guide, new UTF8Encoding(false), cancellationToken);

        FileIdentity planIdentity = await ReadFileIdentityAsync(stagedPlan, cancellationToken);
        FileIdentity checklistIdentity = await ReadFileIdentityAsync(stagedChecklist, cancellationToken);
        FileIdentity guideIdentity = await ReadFileIdentityAsync(stagedGuide, cancellationToken);
        Id65AuthoringRewardStagingRuntimeCandidateReceipt receipt = BuildReceipt(
            preflight,
            finalPaths,
            evidence,
            readback,
            cueIdentity.Sha256,
            planIdentity.Sha256,
            checklistIdentity.Sha256,
            guideIdentity.Sha256,
            helperIdentity.Sha256);
        await WriteJsonAsync(stagedReceipt, receipt, cancellationToken);

        VerifySevenFiles(stagedDirectory, finalPaths);
        VerifyCanonicalJsonReadback(stagedPlan, plan, "staged Core4 reward plan");
        VerifyCanonicalJsonReadback(stagedReceipt, receipt, "staged Core4 reward receipt");
        VerifyChecklist(checklist, finalReveal, loadCodes, readback);
        VerifyLocationGuide(guide, readback);
        return new(plan, receipt, readback, stagedReveal);
    }

    private static async Task<CandidateReadback> VerifyCandidateImageAsync(
        Preflight preflight,
        string outputImagePath,
        int rebuiltRawSectorCount,
        int verifiedRawSectorCount,
        CancellationToken cancellationToken)
    {
        DiscLayout baseLayout = DiscImage.DetectLayout(preflight.BaseImagePath);
        DiscLayout outputLayout = DiscImage.DetectLayout(outputImagePath);
        RequireMode2Layout(baseLayout, "Core4 reward base image");
        RequireMode2Layout(outputLayout, "Core4 reward output image");
        if (baseLayout != outputLayout)
            throw new InvalidDataException("The Core4 reward transaction changed the exact disc layout.");

        await using FileStream baseline = new(
            preflight.BaseImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await using FileStream output = new(
            outputImagePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (baseline.Length != output.Length || baseline.Length != 661_547_040)
            throw new InvalidDataException("The Core4 reward output changed the exact locked image length.");

        DiscFileRecord baseWad = RequireRootFile(baseline, baseLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord outputWad = RequireRootFile(output, outputLayout, "WAD.WAD", WadLba, WadByteLength);
        DiscFileRecord baseExecutable = RequireRootFile(
            baseline, baseLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        DiscFileRecord outputExecutable = RequireRootFile(
            output, outputLayout, "SCUS_942.28", ExecutableLba, ExecutableByteLength);
        if (baseWad != outputWad || baseExecutable != outputExecutable)
            throw new InvalidDataException("The Core4 reward transaction moved or resized WAD.WAD or SCUS_942.28.");

        byte[] sourceRow = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, Row80WadOffset, Row80ByteLength);
        byte[] outputRow = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, Row80WadOffset, Row80ByteLength);
        RequireHash(Hash(sourceRow), Id65RewardCensusContract.Id65DataSha256, "source row-80 readback");
        RequireHash(
            Hash(outputRow),
            Id65AuthoringRewardStagingCompiler.ExpectedOutputRow80Sha256,
            "output row-80 readback");
        if (!sourceRow.SequenceEqual(preflight.Compiled.CopySourceRow80()) ||
            !outputRow.SequenceEqual(preflight.Compiled.CopyOutputRow80()))
        {
            throw new InvalidDataException("The Core4 reward row-80 physical readback differs from the compiler transaction.");
        }
        byte[] forward = Id65AuthoringRewardStagingCompiler.ApplyTransactional(
            preflight.Compiled,
            sourceRow,
            reverse: false);
        byte[] inverse = Id65AuthoringRewardStagingCompiler.ApplyTransactional(
            preflight.Compiled,
            outputRow,
            reverse: true);
        if (!forward.SequenceEqual(outputRow) || !inverse.SequenceEqual(sourceRow))
            throw new InvalidDataException("The published Core4 reward row lost exact forward/inverse identity.");
        LogicalDiffRun[] readbackRuns = BuildLogicalDiffRuns(sourceRow, outputRow);
        if (!readbackRuns.SequenceEqual(preflight.LogicalRuns))
            throw new InvalidDataException("The published Core4 reward logical diff escaped its exact compiler ranges.");

        byte[] sourceOverlay = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, OverlayWadOffset, OverlayByteLength);
        byte[] outputOverlay = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, OverlayWadOffset, OverlayByteLength);
        byte[] retailSource = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, RetailTownSquareRowWadOffset, Row80ByteLength);
        byte[] retailOutput = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, RetailTownSquareRowWadOffset, Row80ByteLength);
        byte[] retailOverlaySource = DiscImage.ReadFileBytes(
            baseline, baseLayout, WadLba, RetailTownSquareOverlayWadOffset, OverlayByteLength);
        byte[] retailOverlayOutput = DiscImage.ReadFileBytes(
            output, outputLayout, WadLba, RetailTownSquareOverlayWadOffset, OverlayByteLength);
        byte[] sourceExecutable = DiscImage.ReadFileBytes(
            baseline, baseLayout, baseExecutable.Lba, 0, baseExecutable.Size);
        byte[] outputExecutableBytes = DiscImage.ReadFileBytes(
            output, outputLayout, outputExecutable.Lba, 0, outputExecutable.Size);
        RequireHash(Hash(sourceOverlay), Id65RewardCensusContract.Id65OverlaySha256, "source ID65 overlay");
        RequireHash(Hash(outputOverlay), Id65RewardCensusContract.Id65OverlaySha256, "output ID65 overlay");
        RequireHash(Hash(retailSource), Id65RewardCensusContract.Id65DataSha256, "source retail Town Square row");
        RequireHash(Hash(retailOutput), Id65RewardCensusContract.Id65DataSha256, "output retail Town Square row");
        RequireHash(
            Hash(retailOverlaySource),
            Id65RewardCensusContract.Id65OverlaySha256,
            "source retail Town Square overlay");
        RequireHash(
            Hash(retailOverlayOutput),
            Id65RewardCensusContract.Id65OverlaySha256,
            "output retail Town Square overlay");
        RequireHash(Hash(sourceExecutable), Id65RewardCensusContract.ExecutableSha256, "source executable");
        RequireHash(Hash(outputExecutableBytes), Id65RewardCensusContract.ExecutableSha256, "output executable");
        if (!sourceOverlay.SequenceEqual(outputOverlay) || !retailSource.SequenceEqual(retailOutput) ||
            !retailOverlaySource.SequenceEqual(retailOverlayOutput) ||
            !sourceExecutable.SequenceEqual(outputExecutableBytes))
        {
            throw new InvalidDataException("The Core4 reward transaction changed overlay/executable/retail controls.");
        }

        string sourceWadSha256 = HashDiscFile(baseline, baseLayout, baseWad, cancellationToken);
        string outputWadSha256 = HashDiscFile(output, outputLayout, outputWad, cancellationToken);
        RequireHash(sourceWadSha256, SourceWadSha256, "source WAD.WAD");
        RequireHash(outputWadSha256, ExpectedOutputWadSha256, "output WAD.WAD");

        PhysicalReadback physical = ComparePhysicalImages(
            baseline,
            output,
            preflight.AffectedRawSectorLbas,
            cancellationToken);
        ValidateFrozenPhysicalReadback(physical);
        int verifiedAgain = RawMode2Form1SectorIntegrity.VerifyAbsoluteSectors(
            output,
            outputLayout,
            preflight.AffectedRawSectorRuns.Select(run => (run.Lba, run.SectorCount)));
        if (rebuiltRawSectorCount != ExpectedAffectedRawSectorCount ||
            verifiedRawSectorCount != ExpectedAffectedRawSectorCount ||
            verifiedAgain != ExpectedAffectedRawSectorCount)
        {
            throw new InvalidDataException("The exact Core4 reward MODE2 rebuild/readback counts changed.");
        }

        return new(
            physical.OutputImageSha256,
            sourceWadSha256,
            outputWadSha256,
            Hash(sourceRow),
            Hash(outputRow),
            Hash(sourceOverlay),
            Hash(retailOverlayOutput),
            Hash(sourceExecutable),
            Hash(retailOutput),
            preflight.LogicalRuns.Sum(run => run.ByteLength),
            preflight.LogicalRuns.Length,
            rebuiltRawSectorCount,
            verifiedRawSectorCount,
            physical.ChangedPhysicalBytes,
            physical.RawSectorDiffs,
            physical.RawSectorDiffSha256,
            physical.OutsideComplementSha256,
            physical.AffectedBeforeSha256,
            physical.AffectedAfterSha256,
            physical.AffectedPayloadBeforeSha256,
            physical.AffectedPayloadAfterSha256,
            physical.Aggregate);
    }

    private static PhysicalReadback ComparePhysicalImages(
        FileStream baseline,
        FileStream output,
        int[] expectedAffectedLbas,
        CancellationToken cancellationToken)
    {
        if (baseline.Length != output.Length || baseline.Length % RawSectorByteLength != 0)
            throw new InvalidDataException("The Core4 reward physical image envelope changed.");
        HashSet<int> expected = expectedAffectedLbas.ToHashSet();
        HashSet<int> observed = [];
        List<Id65AuthoringRewardStagingRawSectorDiff> diffs = [];
        using IncrementalHash outputImageHash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash outsideBefore = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash outsideAfter = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash affectedBefore = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash affectedAfter = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash payloadBefore = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using IncrementalHash payloadAfter = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] before = new byte[RawSectorByteLength];
        byte[] after = new byte[RawSectorByteLength];
        int sectorCount = checked((int)(baseline.Length / RawSectorByteLength));
        baseline.Position = 0;
        output.Position = 0;
        for (int lba = 0; lba < sectorCount; lba++)
        {
            if ((lba & 0xFFF) == 0)
                cancellationToken.ThrowIfCancellationRequested();
            baseline.ReadExactly(before);
            output.ReadExactly(after);
            outputImageHash.AppendData(after);
            bool affected = expected.Contains(lba);
            if (affected)
            {
                affectedBefore.AppendData(before);
                affectedAfter.AppendData(after);
                payloadBefore.AppendData(before.AsSpan(UserDataOffset, LogicalSectorByteLength));
                payloadAfter.AppendData(after.AsSpan(UserDataOffset, LogicalSectorByteLength));
                if (before.AsSpan().SequenceEqual(after))
                    throw new InvalidDataException($"Expected affected raw sector {lba} did not change.");
                observed.Add(lba);
                diffs.Add(MeasureRawSectorDiff(lba, before, after));
                byte expectedSubmode = lba == 55_381 ? (byte)0x89 : (byte)0x08;
                if (before[18] != expectedSubmode || before[22] != expectedSubmode ||
                    after[18] != expectedSubmode || after[22] != expectedSubmode)
                {
                    throw new InvalidDataException($"Affected raw sector {lba} changed duplicated XA submode identity.");
                }
            }
            else
            {
                outsideBefore.AppendData(before);
                outsideAfter.AppendData(after);
                if (!before.AsSpan().SequenceEqual(after))
                    throw new InvalidDataException($"Physical image byte changes escaped to raw sector {lba}.");
            }
        }
        if (!observed.SetEquals(expected))
            throw new InvalidDataException("The observed Core4 reward raw-sector set differs from the derived set.");

        string rawManifest = string.Concat(diffs.Select(diff => string.Create(
            CultureInfo.InvariantCulture,
            $"{diff.RawSectorLba}:{diff.HeaderChangedBytes}:{diff.SubheaderChangedBytes}:" +
            $"{diff.PayloadChangedBytes}:{diff.EdcChangedBytes}:{diff.ReservedChangedBytes}:" +
            $"{diff.EccPChangedBytes}:{diff.EccQChangedBytes}:{diff.TotalChangedBytes}\n")));
        Id65AuthoringRewardStagingPhysicalAggregate aggregate = new(
            diffs.Sum(diff => diff.HeaderChangedBytes),
            diffs.Sum(diff => diff.SubheaderChangedBytes),
            diffs.Sum(diff => diff.PayloadChangedBytes),
            diffs.Sum(diff => diff.EdcChangedBytes),
            diffs.Sum(diff => diff.ReservedChangedBytes),
            diffs.Sum(diff => diff.EccPChangedBytes),
            diffs.Sum(diff => diff.EccQChangedBytes),
            diffs.Sum(diff => (long)diff.TotalChangedBytes));
        string outsideBeforeSha = Hex(outsideBefore.GetHashAndReset());
        string outsideAfterSha = Hex(outsideAfter.GetHashAndReset());
        if (outsideBeforeSha != outsideAfterSha)
            throw new InvalidDataException("The before/after outside-sector complement hashes differ.");
        return new(
            Hex(outputImageHash.GetHashAndReset()),
            aggregate.TotalChangedBytes,
            diffs.ToArray(),
            HashUtf8(rawManifest),
            outsideBeforeSha,
            Hex(affectedBefore.GetHashAndReset()),
            Hex(affectedAfter.GetHashAndReset()),
            Hex(payloadBefore.GetHashAndReset()),
            Hex(payloadAfter.GetHashAndReset()),
            aggregate);
    }

    private static Id65AuthoringRewardStagingRawSectorDiff MeasureRawSectorDiff(
        int lba,
        ReadOnlySpan<byte> before,
        ReadOnlySpan<byte> after)
    {
        int header = 0;
        int subheader = 0;
        int payload = 0;
        int edc = 0;
        int eccP = 0;
        int eccQ = 0;
        int total = 0;
        for (int index = 0; index < RawSectorByteLength; index++)
        {
            if (before[index] == after[index])
                continue;
            total++;
            if (index < 16)
                header++;
            else if (index < 24)
                subheader++;
            else if (index < 2072)
                payload++;
            else if (index < 2076)
                edc++;
            else if (index < 2248)
                eccP++;
            else
                eccQ++;
        }
        return new(lba, header, subheader, payload, edc, 0, eccP, eccQ, total);
    }

    private static void ValidateFrozenPhysicalReadback(PhysicalReadback physical)
    {
        Id65AuthoringRewardStagingPhysicalAggregate aggregate = physical.Aggregate;
        if (physical.OutputImageSha256 != ExpectedOutputImageSha256 ||
            physical.ChangedPhysicalBytes != ExpectedChangedPhysicalBytes ||
            physical.RawSectorDiffs.Count != ExpectedAffectedRawSectorCount ||
            physical.RawSectorDiffSha256 != ExpectedRawSectorDiffSha256 ||
            physical.OutsideComplementSha256 != OutsideAffectedComplementSha256 ||
            physical.AffectedBeforeSha256 != AffectedRawBeforeSha256 ||
            physical.AffectedAfterSha256 != AffectedRawAfterSha256 ||
            physical.AffectedPayloadBeforeSha256 != AffectedPayloadBeforeSha256 ||
            physical.AffectedPayloadAfterSha256 != AffectedPayloadAfterSha256 ||
            aggregate.HeaderChangedBytes != 0 || aggregate.SubheaderChangedBytes != 0 ||
            aggregate.PayloadChangedBytes != ExpectedChangedLogicalBytes ||
            aggregate.EdcChangedBytes != 2_308 || aggregate.ReservedChangedBytes != 0 ||
            aggregate.EccPChangedBytes != 97_998 || aggregate.EccQChangedBytes != 59_633 ||
            aggregate.TotalChangedBytes != ExpectedChangedPhysicalBytes)
        {
            throw new InvalidDataException("The complete frozen Core4 reward physical readback changed.");
        }
    }

    private static Id65AuthoringRewardStagingRuntimeCandidatePlan BuildPlan(
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes) =>
        new()
        {
            SchemaVersion = PlanSchemaVersion,
            ProfileId = ProfileId,
            RuntimeEvidenceRelativePath = evidence.EvidenceRelativePath,
            RuntimeEvidenceSha256 = evidence.EvidenceSha256,
            RuntimeEvidenceSourceClosureSha256 = evidence.SourceClosureSha256,
            RuntimeEvidenceProfileSha256 = evidence.RuntimeProfileSha256,
            RuntimeEvidenceArtifactSetSha256 = evidence.ArtifactSetSha256,
            RuntimeEvidencePayloadSha256 = evidence.PayloadSha256,
            RuntimeEvidenceContractSha256 = evidence.ContractSha256,
            SyntheticPublisherSmokeOnly = evidence.SyntheticPublisherSmokeOnly,
            BaseImageSha256 = BaseImageSha256,
            BaseCueSha256 = BaseCueSha256,
            FoundationDonorImageSha256 = FoundationDonorImageSha256,
            CollisionWindingRepairV2OutputDataSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputDataSha256,
            CollisionWindingRepairV2OutputModelSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputModelSha256,
            EnvironmentHeaderRepairV3OutputDataSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputDataSha256,
            EnvironmentHeaderRepairV3OutputModelSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputModelSha256,
            OutputImageSha256 = readback.OutputImageSha256,
            SourceWadSha256 = readback.SourceWadSha256,
            OutputWadSha256 = readback.OutputWadSha256,
            SourceRow80Sha256 = readback.SourceRow80Sha256,
            OutputRow80Sha256 = readback.OutputRow80Sha256,
            TargetOverlaySha256 = readback.TargetOverlaySha256,
            RetailTownSquareOverlaySha256 = readback.RetailTownSquareOverlaySha256,
            GlobalExecutableSha256 = readback.GlobalExecutableSha256,
            RetailTownSquareRowSha256 = readback.RetailTownSquareRowSha256,
            CoreManifestSha256 = preflight.Compiled.ManifestSha256,
            TextureWitnessSha256 = Id65V2NativeTextureCompositionCompiler.ExpectedWitnessSha256,
            GrassBundleSha256 = Id65AuthoringMobyDependencyBundleCompiler.ExpectedArtisansGrassBundleSha256,
            GrassDescriptorFullFieldSha256 = preflight.Compiled.DescriptorFullFieldSha256,
            DiffManifestSha256 = preflight.Compiled.DiffManifestSha256,
            OwnedRangeMapSha256 = preflight.Compiled.OwnedRangeMapSha256,
            InverseRangeMapSha256 = preflight.Compiled.InverseRangeMapSha256,
            TransactionSha256 = preflight.Compiled.TransactionSha256,
            ProtectedComplementSha256 = preflight.Compiled.ProtectedComplementSha256,
            DeterministicPlanSha256 = preflight.Compiled.DeterministicPlanSha256,
            OutputModelSha256 = preflight.Compiled.Structure.ModelSha256,
            CompilerInvocationCount = 2,
            LockedBinCopyCount = 1,
            CompleteRow80WriteCount = 1,
            WadLba = WadLba,
            WadByteLength = WadByteLength,
            Row80WadOffset = Row80WadOffset,
            Row80ByteLength = Row80ByteLength,
            Row80FirstRawSectorLba = 53_906,
            Row80LastRawSectorLba = 55_381,
            OwnedLeafCount = preflight.Compiled.OwnedLeafCount,
            GuardedLogicalBytes = preflight.Compiled.GuardedByteCount,
            ChangedLogicalBytes = readback.ChangedLogicalBytes,
            LogicalDiffRangeCount = readback.LogicalDiffRangeCount,
            ObjectCount = preflight.Compiled.Structure.ObjectCount,
            ActorRootCount = preflight.Compiled.Structure.ActorRootCount,
            FixupCount = preflight.Compiled.Structure.FixupCount,
            CollisionBlocksUsedBytes = preflight.Compiled.Structure.CollisionBlocksUsedBytes,
            RebuiltRawSectorCount = readback.RebuiltRawSectorCount,
            VerifiedRawSectorCount = readback.VerifiedRawSectorCount,
            AffectedRawSectorLbas = preflight.AffectedRawSectorLbas.ToArray(),
            AffectedRawSectorRuns = preflight.AffectedRawSectorRuns.ToArray(),
            AffectedRawSectorLbaSequenceSha256 = ExpectedAffectedLbaSequenceSha256,
            AffectedRawSectorRunListSha256 = ExpectedAffectedRunListSha256,
            RawSectorDiffSha256 = readback.RawSectorDiffSha256,
            ChangedPhysicalImageBytes = readback.ChangedPhysicalBytes,
            OutsideAffectedRawSectorComplementSha256 = readback.OutsideComplementSha256,
            AffectedRawSectorsBeforeSha256 = readback.AffectedBeforeSha256,
            AffectedRawSectorsAfterSha256 = readback.AffectedAfterSha256,
            AffectedPayloadsBeforeSha256 = readback.AffectedPayloadBeforeSha256,
            AffectedPayloadsAfterSha256 = readback.AffectedPayloadAfterSha256,
            PhysicalAggregate = readback.Aggregate,
            CoreSectorCount = preflight.Compiled.Structure.CoreSectorCount,
            ActiveCollisionCellCount = preflight.Compiled.Structure.ActiveCollisionCellCount,
            TextureCount = preflight.Compiled.Structure.TextureCount,
            HighestTextureId = preflight.Compiled.Structure.HighestTextureId,
            StationaryOwnerCount = preflight.Compiled.Stationary.OwnerCount,
            StationaryRewardTotal = preflight.Compiled.Stationary.RewardTotal,
            DragonCount = preflight.Compiled.Dragons.Count,
            RequiredEggTarget = preflight.Compiled.ZeroEgg.RequiredEggTarget,
            LoadCodes = loadCodes.ToArray(),
            FullReadbackFocusedRuntimePassPrerequisiteVerified = true,
            CaptureJournalReadbackPrerequisiteVerified = true,
            DirectLockedSourceCompilationVerified = true,
            ExactForwardInverseVerified = true,
            ExactLogicalDiffBoundaryVerified = true,
            ExactPhysicalDiffBoundaryVerified = true,
            RawMode2EdcEccVerified = true,
            OverlayExecutableAndRetailControlsPreserved = true,
            ProcessCrashRecoveryVerified = false,
            PhaseBoundaryProcessCrashRecoveryVerified = true,
            MidMutationProcessCrashRecoveryVerified = false,
            PowerLossDurabilityVerified = false,
            Full8AndMobile29Excluded = true,
            Core4RuntimePending = true,
            Total200RuntimeAccepted = false,
            SaveRuntimeAccepted = false,
            AppIntegrated = false,
            NormalCreateBinEnabled = false,
            PromotionAuthorized = false,
            ReleaseAuthorized = false
        };

    private static Id65AuthoringRewardStagingRuntimeCandidateReceipt BuildReceipt(
        Preflight preflight,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        CandidateReadback readback,
        string outputCueSha256,
        string constructionPlanSha256,
        string runtimeChecklistSha256,
        string locationGuideSha256,
        string finderHelperSha256) =>
        new()
        {
            SchemaVersion = ReceiptSchemaVersion,
            ProfileId = ProfileId,
            WorkspaceRoot = preflight.WorkspaceRoot,
            BaseImagePath = preflight.BaseImagePath,
            BaseCuePath = preflight.BaseCuePath,
            FoundationDonorImagePath = preflight.FoundationImagePath,
            OutputDirectoryPath = paths.OutputDirectoryPath,
            OutputImagePath = paths.OutputImagePath,
            OutputCuePath = paths.OutputCuePath,
            ConstructionPlanPath = paths.ConstructionPlanPath,
            StaticReadbackReceiptPath = paths.StaticReadbackReceiptPath,
            RuntimeChecklistPath = paths.RuntimeChecklistPath,
            LocationGuidePath = paths.LocationGuidePath,
            FinderHelperPath = paths.FinderHelperPath,
            RuntimeEvidenceRelativePath = evidence.EvidenceRelativePath,
            RuntimeEvidenceSha256 = evidence.EvidenceSha256,
            RuntimeEvidencePayloadSha256 = evidence.PayloadSha256,
            RuntimeEvidenceContractSha256 = evidence.ContractSha256,
            SyntheticPublisherSmokeOnly = evidence.SyntheticPublisherSmokeOnly,
            BaseImageSha256 = BaseImageSha256,
            BaseCueSha256 = BaseCueSha256,
            FoundationDonorImageSha256 = FoundationDonorImageSha256,
            CollisionWindingRepairV2OutputDataSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputDataSha256,
            CollisionWindingRepairV2OutputModelSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedSupersededV2OutputModelSha256,
            EnvironmentHeaderRepairV3OutputDataSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputDataSha256,
            EnvironmentHeaderRepairV3OutputModelSha256 =
                UnusedLevel65RemoteBlankEnvironmentHeaderRepairV3Construction.ExpectedOutputModelSha256,
            OutputImageSha256 = readback.OutputImageSha256,
            SourceWadSha256 = readback.SourceWadSha256,
            OutputWadSha256 = readback.OutputWadSha256,
            SourceRow80Sha256 = readback.SourceRow80Sha256,
            OutputRow80Sha256 = readback.OutputRow80Sha256,
            OutputCueSha256 = outputCueSha256,
            ConstructionPlanSha256 = constructionPlanSha256,
            RuntimeChecklistSha256 = runtimeChecklistSha256,
            LocationGuideSha256 = locationGuideSha256,
            FinderHelperSha256 = finderHelperSha256,
            RawSectorDiffSha256 = readback.RawSectorDiffSha256,
            TransactionSha256 = preflight.Compiled.TransactionSha256,
            OutputModelSha256 = preflight.Compiled.Structure.ModelSha256,
            ChangedLogicalBytes = readback.ChangedLogicalBytes,
            ChangedPhysicalImageBytes = readback.ChangedPhysicalBytes,
            RebuiltRawSectorCount = readback.RebuiltRawSectorCount,
            VerifiedRawSectorCount = readback.VerifiedRawSectorCount,
            RawSectorDiffCount = readback.RawSectorDiffs.Count,
            RawSectorDiffs = readback.RawSectorDiffs.ToArray(),
            PhysicalAggregate = readback.Aggregate,
            ObjectCount = preflight.Compiled.Structure.ObjectCount,
            ActorRootCount = preflight.Compiled.Structure.ActorRootCount,
            FixupCount = preflight.Compiled.Structure.FixupCount,
            CollisionBlocksUsedBytes = preflight.Compiled.Structure.CollisionBlocksUsedBytes,
            EvidenceTokenRevalidatedBeforeMutation = true,
            CompilerInvokedExactlyTwice = true,
            OneLockedBinCopyVerified = true,
            OneCompleteRow80WriteVerified = true,
            FullLogicalReadbackAndInverseVerified = true,
            FullPhysicalImageReadbackVerified = true,
            ExactMode2SectorSetRebuiltAndVerified = true,
            BaseAndFoundationRepinnedAfterPublication = true,
            OverlayExecutableAndRetailControlsPreserved = true,
            CanonicalSidecarReadbackVerified = true,
            SevenFilePublicationVerified = true,
            FinderHandoffVerified = true,
            RollbackRecoveryVerified = false,
            ProcessCrashRecoveryVerified = false,
            PhaseBoundaryProcessCrashRecoveryVerified = true,
            MidMutationProcessCrashRecoveryVerified = false,
            PowerLossDurabilityVerified = false,
            DisposableRuntimeCandidateAuthorized = true,
            Core4RuntimePending = true,
            Total200RuntimeAccepted = false,
            SaveRuntimeAccepted = false,
            AppIntegrated = false,
            NormalCreateBinEnabled = false,
            PromotionAuthorized = false,
            ReleaseAuthorized = false
        };

    private static string BuildRuntimeChecklist(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        RuntimeCandidateFinderReveal reveal,
        Id65AuthoringRewardStagingRuntimeCandidatePlan plan,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        StringBuilder builder = new();
        builder.AppendLine("# ID65 Core4 reward-staging focused runtime checklist");
        builder.AppendLine();
        builder.AppendLine("This is a disposable runtime candidate. It is **not** normal Create BIN output.");
        builder.AppendLine("The header-repaired-v3 seven-session FullReadback pass is only the prerequisite for publication; Core4 itself remains runtime-pending.");
        builder.AppendLine(
            "Journaled phase-boundary process-crash recovery is verified. A kill during helper creation or " +
            "recursive deletion is not verified and may leave preserved manual-audit residue; power-loss " +
            "durability is not verified.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendCandidateDiscSection(builder, reveal);
        builder.AppendLine("## Frozen identity");
        builder.AppendLine();
        builder.AppendLine($"- BIN SHA-256: `{readback.OutputImageSha256}`");
        builder.AppendLine($"- WAD.WAD SHA-256: `{readback.OutputWadSha256}`");
        builder.AppendLine($"- ID65 row 80 SHA-256: `{readback.OutputRow80Sha256}`");
        builder.AppendLine($"- compiler transaction: `{plan.TransactionSha256}`");
        builder.AppendLine($"- v3 evidence: `{plan.RuntimeEvidenceRelativePath}` / `{plan.RuntimeEvidenceSha256}`");
        builder.AppendLine();
        builder.AppendLine("## Isolation setup");
        builder.AppendLine();
        builder.AppendLine("1. Use the pinned isolated DuckStation portable profile only.");
        builder.AppendLine("2. Keep cheats, patches, memory cards, save states, rewind, and runahead disabled/empty.");
        builder.AppendLine("3. Cold boot each row. Do not save and do not load a save state.");
        builder.AppendLine();
        RuntimeCandidateTestHandoff.AppendLoadCodeTable(builder, loadCodes);
        builder.AppendLine("## ID65 Core4 checks (all pending)");
        builder.AppendLine();
        builder.AppendLine("- [ ] Land controllably on the authored four-sector Core4 terrain; walk, charge, jump, and circle all 16 active collision cells.");
        builder.AppendLine("- [ ] Confirm HP/LP terrain and T66 native texture remain paired at near and far camera distances with no missing-face or occlusion pop.");
        builder.AppendLine("- [ ] Confirm the imported Artisans Grass object renders and remains passive; no Thief/egg behavior or reward remains.");
        builder.AppendLine("- [ ] Collect all 71 stationary owners and confirm their authored reward subtotal is exactly 171 with no duplicate retirement.");
        builder.AppendLine("- [ ] Free all four dragons and verify camera/run-to/control return behavior; Dragon B is a deliberate precinct-route risk requiring close observation.");
        builder.AppendLine("- [ ] Die and respawn once, then cold-reset and repeat the landing/control check. Do not claim save persistence.");
        builder.AppendLine();
        builder.AppendLine("## Retail controls (all pending)");
        builder.AppendLine();
        builder.AppendLine("- [ ] Town Square: terrain, collision, Mobys, camera, death/respawn, music, exit, and totals remain retail-correct.");
        builder.AppendLine("- [ ] Gnasty's Loot: gameplay and exit remain retail-correct.");
        builder.AppendLine("- [ ] Sunny Flight: gameplay, timer, and exit remain retail-correct.");
        builder.AppendLine();
        builder.AppendLine("## Stop boundaries");
        builder.AppendLine();
        builder.AppendLine("Full8 mobile-enemy reservations (29 reward) are excluded. Total 200, saving, App integration, normal Create BIN, promotion, and release remain false.");
        if (!builder.ToString().Contains(Path.GetFileName(paths.OutputCuePath), StringComparison.Ordinal))
            throw new InvalidOperationException("The Core4 checklist lost its exact CUE identity.");
        return builder.ToString();
    }

    private static string BuildLocationGuideSvg(
        Id65AuthoringRewardStagingRuntimeCandidatePlan plan,
        CandidateReadback readback,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        string id65Code = EscapeXml(loadCodes.Single(code => code.LevelId == 65).InputCode);
        return "<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"1200\" height=\"820\" viewBox=\"0 0 1200 820\">\n" +
            "  <rect width=\"1200\" height=\"820\" fill=\"#101421\"/>\n" +
            "  <text x=\"56\" y=\"72\" fill=\"#f3c777\" font-family=\"Menlo,monospace\" font-size=\"30\">ID65 Core4 reward-staging runtime candidate</text>\n" +
            "  <text x=\"56\" y=\"118\" fill=\"#ff8f8f\" font-family=\"Menlo,monospace\" font-size=\"18\">RUNTIME PENDING — disposable CUE only; do not save or use Create BIN</text>\n" +
            "  <rect x=\"70\" y=\"175\" width=\"480\" height=\"390\" rx=\"24\" fill=\"#293954\" stroke=\"#8fc7ff\" stroke-width=\"4\"/>\n" +
            "  <rect x=\"155\" y=\"250\" width=\"135\" height=\"105\" fill=\"#4f7d55\" stroke=\"#b5e7a7\" stroke-width=\"3\"/>\n" +
            "  <rect x=\"310\" y=\"250\" width=\"135\" height=\"105\" fill=\"#4f7d55\" stroke=\"#b5e7a7\" stroke-width=\"3\"/>\n" +
            "  <rect x=\"155\" y=\"375\" width=\"135\" height=\"105\" fill=\"#4f7d55\" stroke=\"#b5e7a7\" stroke-width=\"3\"/>\n" +
            "  <rect x=\"310\" y=\"375\" width=\"135\" height=\"105\" fill=\"#4f7d55\" stroke=\"#b5e7a7\" stroke-width=\"3\"/>\n" +
            "  <text x=\"177\" y=\"515\" fill=\"#d8f4d0\" font-family=\"Menlo,monospace\" font-size=\"15\">4 sectors / 16 cells</text>\n" +
            "  <text x=\"620\" y=\"200\" fill=\"#f2f5ff\" font-family=\"Menlo,monospace\" font-size=\"18\">Core4 semantic targets</text>\n" +
            string.Create(CultureInfo.InvariantCulture, $"  <text x=\"620\" y=\"245\" fill=\"#b8c7e6\" font-family=\"Menlo,monospace\" font-size=\"15\">stationary: {plan.StationaryOwnerCount} owners / {plan.StationaryRewardTotal} reward</text>\n") +
            string.Create(CultureInfo.InvariantCulture, $"  <text x=\"620\" y=\"278\" fill=\"#b8c7e6\" font-family=\"Menlo,monospace\" font-size=\"15\">dragons: {plan.DragonCount}; eggs: {plan.RequiredEggTarget}; T66 + Grass</text>\n") +
            string.Create(CultureInfo.InvariantCulture, $"  <text x=\"620\" y=\"311\" fill=\"#b8c7e6\" font-family=\"Menlo,monospace\" font-size=\"15\">changed: {readback.ChangedLogicalBytes:N0} logical / {readback.ChangedPhysicalBytes:N0} raw bytes</text>\n") +
            string.Create(CultureInfo.InvariantCulture, $"  <text x=\"620\" y=\"344\" fill=\"#b8c7e6\" font-family=\"Menlo,monospace\" font-size=\"15\">MODE2 rebuild: {readback.RebuiltRawSectorCount} exact sectors</text>\n") +
            "  <text x=\"620\" y=\"420\" fill=\"#f2f5ff\" font-family=\"Menlo,monospace\" font-size=\"18\">ID65 complete load code</text>\n" +
            $"  <text x=\"620\" y=\"458\" fill=\"#f3c777\" font-family=\"Menlo,monospace\" font-size=\"12\">{id65Code}</text>\n" +
            "  <text x=\"56\" y=\"665\" fill=\"#ffb0b0\" font-family=\"Menlo,monospace\" font-size=\"16\">Validate ID65 plus Town Square, Gnasty's Loot, and Sunny Flight from cold boots.</text>\n" +
            $"  <text x=\"56\" y=\"710\" fill=\"#8da0c5\" font-family=\"Menlo,monospace\" font-size=\"12\">BIN {readback.OutputImageSha256}</text>\n" +
            $"  <text x=\"56\" y=\"744\" fill=\"#8da0c5\" font-family=\"Menlo,monospace\" font-size=\"12\">TX {plan.TransactionSha256}</text>\n" +
            "</svg>\n";
    }

    private static void VerifyChecklist(
        string checklist,
        RuntimeCandidateFinderReveal reveal,
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes,
        CandidateReadback readback)
    {
        RuntimeCandidateTestHandoff.VerifyChecklistReadback(checklist, reveal, loadCodes);
        string[] required =
        [
            "Core4 itself remains runtime-pending",
            "71 stationary owners",
            "exactly 171",
            "four dragons",
            "Dragon B",
            "Full8 mobile-enemy reservations (29 reward) are excluded",
            "Journaled phase-boundary process-crash recovery is verified",
            "kill during helper creation or recursive deletion is not verified",
            "preserved manual-audit residue",
            "power-loss durability is not verified",
            "Total 200, saving, App integration, normal Create BIN, promotion, and release remain false",
            readback.OutputImageSha256,
            readback.OutputRow80Sha256
        ];
        if (required.Any(value => !checklist.Contains(value, StringComparison.Ordinal)))
            throw new InvalidDataException("The Core4 reward runtime checklist omitted a frozen gate or identity.");
    }

    private static void VerifyLocationGuide(string guide, CandidateReadback readback)
    {
        string[] required =
        [
            "RUNTIME PENDING",
            "4 sectors / 16 cells",
            "71 owners / 171 reward",
            "dragons: 4; eggs: 0; T66 + Grass",
            readback.OutputImageSha256,
            readback.ChangedLogicalBytes.ToString("N0", CultureInfo.InvariantCulture),
            readback.RebuiltRawSectorCount.ToString(CultureInfo.InvariantCulture)
        ];
        if (!guide.StartsWith("<svg ", StringComparison.Ordinal) ||
            !guide.EndsWith("</svg>\n", StringComparison.Ordinal) ||
            required.Any(value => !guide.Contains(value, StringComparison.Ordinal)))
        {
            throw new InvalidDataException("The Core4 reward location guide omitted a frozen pending/runtime identity.");
        }
    }

    private static async Task PrepareDestinationAsync(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        bool replaceExistingCandidate,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.OutputDirectoryPath))
            return;
        RejectTreeReparsePoints(paths.OutputDirectoryPath, "existing Core4 reward candidate");
        if (!replaceExistingCandidate)
        {
            throw new IOException(
                "The Core4 reward-staging runtime-candidate directory already exists. " +
                "Set ReplaceExistingCandidate only for an intentional atomic replacement.");
        }
        await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
    }

    private static async Task BeginPublicationAsync(
        Publication publication,
        bool replaceExistingCandidate,
        Action<string>? testStageHook,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token,
        FileStream operationLease,
        GlobalWriterLease writerLease,
        CancellationToken cancellationToken)
    {
        if (publication.PreviousOutputExisted)
        {
            RequirePublicationControlDirectories(publication, "before previous-candidate backup");
            if (!replaceExistingCandidate || !Directory.Exists(publication.OutputDirectoryPath) ||
                Directory.Exists(publication.BackupDirectoryPath))
            {
                throw new IOException("The prior Core4 reward candidate cannot be backed up unambiguously.");
            }
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            publication.PreviousCandidateFingerprint = CaptureCandidateTreeFingerprint(
                publication.OutputDirectoryPath);
            publication.PreviousDirectoryIdentity = publication.PreviousCandidateFingerprint[0].DirectoryIdentity;
            publication.PreviousCandidateFingerprintSha256 = HashCandidateTreeJournalFingerprint(
                publication.PreviousCandidateFingerprint);
            WriteJournal(publication, PhaseBackupIntent, writerLease);
            testStageHook?.Invoke("after-backup-intent-journal");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-backup-intent-journal");
            RequireCandidateTreeFingerprint(
                publication.OutputDirectoryPath,
                publication.PreviousCandidateFingerprint,
                "previous Core4 reward candidate immediately before backup");
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            RequirePublicationControlDirectories(publication, "immediately before previous-candidate rename");
            writerLease.RequireCurrentIdentity("immediately before previous-candidate rename");
            Directory.Move(publication.OutputDirectoryPath, publication.BackupDirectoryPath);
            writerLease.RequireCurrentIdentity("after previous-candidate rename");
            RequireCandidateTreeFingerprint(
                publication.BackupDirectoryPath,
                publication.PreviousCandidateFingerprint,
                "backed-up previous Core4 reward candidate");
            RequirePublicationControlDirectories(publication, "after previous-candidate rename");
            WriteJournal(publication, PhasePreviousBackedUp, writerLease);
            testStageHook?.Invoke("after-previous-candidate-backup");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-previous-candidate-backup");
        }
        else if (Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The Core4 reward output appeared after staging and before publication.");
        }

        WriteJournal(publication, PhasePublishIntent, writerLease);
        testStageHook?.Invoke("after-publish-intent-journal");
        RequireLiveOperationAfterHook(
            publication,
            operationLease,
            writerLease,
            paths,
            preflight,
            token,
            "after-publish-intent-journal");
        if (!Directory.Exists(publication.StagedDirectoryPath) ||
            Directory.Exists(publication.OutputDirectoryPath))
        {
            throw new IOException("The Core4 reward atomic publication paths are not in their exact intent state.");
        }
        if (publication.StagedCandidateFingerprint is null)
            throw new InvalidOperationException("The verified Core4 reward stage lost its identity binding.");
        RequireCandidateTreeFingerprint(
            publication.StagedDirectoryPath,
            publication.StagedCandidateFingerprint,
            "verified staged Core4 reward candidate");
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        RequirePublicationControlDirectories(publication, "immediately before candidate publication rename");
        writerLease.RequireCurrentIdentity("immediately before candidate publication rename");
        Directory.Move(publication.StagedDirectoryPath, publication.OutputDirectoryPath);
        writerLease.RequireCurrentIdentity("after candidate publication rename");
        RequireCandidateTreeFingerprint(
            publication.OutputDirectoryPath,
            publication.StagedCandidateFingerprint,
            "newly published Core4 reward candidate");
        RequirePublicationControlDirectories(publication, "after candidate publication rename");
        WriteJournal(publication, PhasePublished, writerLease);
        testStageHook?.Invoke("after-candidate-publication");
        RequireLiveOperationAfterHook(
            publication,
            operationLease,
            writerLease,
            paths,
            preflight,
            token,
            "after-candidate-publication");
    }

    private static async Task RollBackPublicationAsync(
        Publication publication,
        Action<string>? testStageHook,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token,
        FileStream operationLease,
        GlobalWriterLease writerLease,
        CancellationToken cancellationToken)
    {
        if (publication.Phase == PhaseCommitted)
            throw new InvalidOperationException("A committed Core4 reward publication may not be rolled back.");
        testStageHook?.Invoke("before-rollback-start");
        RequireLiveOperationAfterHook(
            publication,
            operationLease,
            writerLease,
            paths,
            preflight,
            token,
            "before-rollback-start");

        bool outputExists = Directory.Exists(publication.OutputDirectoryPath);
        bool stageExists = Directory.Exists(publication.StagedDirectoryPath);
        bool backupExists = Directory.Exists(publication.BackupDirectoryPath);
        bool newCandidatePublished = publication.Phase == PhasePublished ||
            (publication.Phase == PhasePublishIntent && outputExists && !stageExists);
        bool backupMayHaveMoved = publication.Phase is
            PhaseBackupIntent or PhasePreviousBackedUp or PhasePublishIntent or PhasePublished;

        if (publication.PreviousOutputExisted && backupMayHaveMoved && !backupExists && !outputExists)
        {
            throw new IOException(
                "The prior Core4 reward candidate and its authoritative backup are both missing; recovery was preserved.");
        }
        if (newCandidatePublished && outputExists)
        {
            if (publication.StagedCandidateFingerprint is null)
            {
                throw new IOException(
                    "The published Core4 reward candidate has no in-memory staged identity; it was preserved.");
            }
            RequireCandidateTreeFingerprint(
                publication.OutputDirectoryPath,
                publication.StagedCandidateFingerprint,
                "newly published Core4 reward candidate before rollback deletion");
            writerLease.RequireCurrentIdentity("before newly published candidate rollback deletion");
            DeleteOwnedDirectory(
                publication.OutputDirectoryPath,
                Path.GetDirectoryName(publication.OutputDirectoryPath)!,
                "newly published Core4 reward candidate");
            writerLease.RequireCurrentIdentity("after newly published candidate rollback deletion");
            outputExists = false;
        }
        if (backupExists)
        {
            if (outputExists)
            {
                throw new IOException(
                    "A Core4 reward backup exists beside an ambiguous occupied output; recovery was preserved.");
            }
            if (publication.PreviousCandidateFingerprint is null)
                throw new IOException("The previous Core4 reward backup lost its in-memory identity binding.");
            RequireCandidateTreeFingerprint(
                publication.BackupDirectoryPath,
                publication.PreviousCandidateFingerprint,
                "previous Core4 reward backup before restore");
            WriteJournal(publication, PhaseRollbackRestoreIntent, writerLease);
            testStageHook?.Invoke("before-previous-candidate-restore");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "before-previous-candidate-restore");
            RequirePublicationControlDirectories(publication, "immediately before previous-candidate restore");
            writerLease.RequireCurrentIdentity("immediately before previous-candidate restore");
            Directory.Move(publication.BackupDirectoryPath, publication.OutputDirectoryPath);
            writerLease.RequireCurrentIdentity("after previous-candidate restore");
            RequireCandidateTreeFingerprint(
                publication.OutputDirectoryPath,
                publication.PreviousCandidateFingerprint,
                "restored previous Core4 reward candidate");
            RequirePublicationControlDirectories(publication, "after previous-candidate restore");
            WriteJournal(publication, PhaseRollbackRestored, writerLease);
            testStageHook?.Invoke("after-rollback-restored");
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "after-rollback-restored");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        }
        else if (publication.PreviousOutputExisted && backupMayHaveMoved && !outputExists)
        {
            throw new IOException("The prior Core4 reward candidate cannot be restored without its backup.");
        }
        else if (publication.PreviousOutputExisted && outputExists)
        {
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        }
        if (Directory.Exists(publication.StagedDirectoryPath))
        {
            RequireLiveOperationAfterHook(
                publication,
                operationLease,
                writerLease,
                paths,
                preflight,
                token,
                "rollback stage cleanup boundary");
            if (publication.StagedCandidateFingerprint is not null)
            {
                RequireCandidateTreeFingerprint(
                    publication.StagedDirectoryPath,
                    publication.StagedCandidateFingerprint,
                    "incomplete live Core4 reward stage before rollback deletion");
            }
            else if (publication.StagedCandidateFingerprintSha256 is null)
            {
                VerifyOwnedPartialStageTree(publication.StagedDirectoryPath, paths);
            }
            else
            {
                throw new InvalidDataException(
                    "The incomplete live Core4 reward stage lost its full in-memory identity binding.");
            }
            writerLease.RequireCurrentIdentity("before incomplete stage rollback deletion");
            DeleteOwnedDirectory(
                publication.StagedDirectoryPath,
                publication.OperationRootPath,
                "incomplete Core4 reward stage");
            writerLease.RequireCurrentIdentity("after incomplete stage rollback deletion");
        }
        writerLease.RequireCurrentIdentity("before rollback operation cleanup");
        CleanupCompletedOperation(publication, writerLease);
        writerLease.RequireCurrentIdentity("after rollback operation cleanup");
    }

    private static void RequireLiveOperationAfterHook(
        Publication publication,
        FileStream operationLease,
        GlobalWriterLease writerLease,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token,
        string boundary)
    {
        writerLease.RequireCurrentIdentity($"at live boundary {boundary}");
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        ValidatePathTopology(preflight, paths);
        RequirePinnedTrustedDirectory(
            Path.GetDirectoryName(publication.OutputDirectoryPath)!,
            publication.PublicationParentIdentity,
            publication.ExpectedUserId,
            $"Core4 reward publication parent after {boundary}");
        EnsureStrictDescendant(
            publication.OperationRootPath,
            paths.OperationsDirectoryPath,
            $"Core4 reward live operation after {boundary}");
        RejectExistingAncestorReparsePoints(
            publication.OperationRootPath,
            $"Core4 reward live operation after {boundary}");
        if (!Directory.Exists(paths.OperationsDirectoryPath) ||
            !Directory.Exists(publication.OperationRootPath))
        {
            throw new IOException($"The Core4 reward live operation disappeared after {boundary}.");
        }
        RejectTreeReparsePoints(
            publication.OperationRootPath,
            $"Core4 reward live operation after {boundary}");
        RequirePinnedExactDirectory(
            paths.OperationsDirectoryPath,
            publication.OperationsDirectoryIdentity,
            publication.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            $"Core4 reward operations directory after {boundary}");
        RequirePinnedExactDirectory(
            publication.OperationRootPath,
            publication.OperationRootIdentity,
            publication.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            $"Core4 reward operation root after {boundary}");
        string[] operationEntries = Directory.EnumerateFileSystemEntries(paths.OperationsDirectoryPath).ToArray();
        if (operationEntries.Length != 1 || !PathEquals(operationEntries[0], publication.OperationRootPath))
        {
            throw new InvalidDataException(
                $"The Core4 reward operations directory changed after {boundary}.");
        }
        ValidateOperationRootEntries(publication.OperationRootPath, publication.ExpectedUserId);

        StableFileIdentity expectedLease = publication.OperationLeaseIdentity ??
            throw new InvalidOperationException("The live Core4 reward operation has no lease identity binding.");
        if (!File.Exists(publication.OperationLeasePath))
            throw new IOException($"The Core4 reward operation lease disappeared after {boundary}.");
        StableFileIdentity handleLease = RequireOwnedExactRegularHandleIdentity(
            operationLease,
            publication.ExpectedUserId,
            ExactPrivateFileMode(),
            $"Core4 reward live operation lease after {boundary}");
        StableFileIdentity pathLease = RequireOwnedExactRegularPathIdentity(
            publication.OperationLeasePath,
            publication.ExpectedUserId,
            ExactPrivateFileMode(),
            $"Core4 reward live operation lease after {boundary}");
        if (handleLease != expectedLease || pathLease != expectedLease)
        {
            throw new InvalidDataException(
                $"The Core4 reward operation lease identity changed after {boundary}.");
        }

        bool outputExists = Directory.Exists(publication.OutputDirectoryPath);
        bool stageExists = Directory.Exists(publication.StagedDirectoryPath);
        bool backupExists = Directory.Exists(publication.BackupDirectoryPath);
        ValidateLivePublicationPhaseTopology(publication, outputExists, stageExists, backupExists, boundary);

        if (stageExists)
        {
            if (publication.StagedCandidateFingerprint is not null)
            {
                RequireCandidateTreeFingerprint(
                    publication.StagedDirectoryPath,
                    publication.StagedCandidateFingerprint,
                    $"live staged Core4 reward candidate after {boundary}");
            }
            else if (publication.Phase == PhaseCreated)
            {
                VerifyOwnedPartialStageTree(publication.StagedDirectoryPath, paths);
            }
            else
            {
                throw new InvalidDataException(
                    $"The live staged Core4 reward candidate lost its binding after {boundary}.");
            }
        }
        if (backupExists)
        {
            if (publication.PreviousCandidateFingerprint is null)
            {
                throw new InvalidDataException(
                    $"The live previous Core4 reward backup lost its binding after {boundary}.");
            }
            RequireCandidateTreeFingerprint(
                publication.BackupDirectoryPath,
                publication.PreviousCandidateFingerprint,
                $"live previous Core4 reward backup after {boundary}");
        }
        if (outputExists)
        {
            CandidateTreeFingerprint[]? expectedOutput = publication.Phase switch
            {
                PhasePublished or PhaseCommitted => publication.StagedCandidateFingerprint,
                PhaseRollbackRestored => publication.PreviousCandidateFingerprint,
                PhaseBackupIntent => publication.PreviousCandidateFingerprint,
                _ => null
            };
            if (expectedOutput is not null)
            {
                RequireCandidateTreeFingerprint(
                    publication.OutputDirectoryPath,
                    expectedOutput,
                    $"live Core4 reward output after {boundary}");
            }
            else
            {
                VerifyExactOwnedSevenFileTree(publication.OutputDirectoryPath);
            }
        }
        Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
    }

    private static void ValidateLivePublicationPhaseTopology(
        Publication publication,
        bool outputExists,
        bool stageExists,
        bool backupExists,
        string boundary)
    {
        bool invalid = publication.Phase switch
        {
            PhaseCreated => backupExists || outputExists != publication.PreviousOutputExisted,
            PhaseStaged => !stageExists || backupExists || outputExists != publication.PreviousOutputExisted,
            PhaseBackupIntent => !publication.PreviousOutputExisted || !stageExists || !outputExists || backupExists,
            PhasePreviousBackedUp =>
                !publication.PreviousOutputExisted || !stageExists || outputExists || !backupExists,
            PhasePublishIntent => !stageExists || outputExists || backupExists != publication.PreviousOutputExisted,
            PhasePublished => stageExists || !outputExists || backupExists != publication.PreviousOutputExisted,
            PhaseRollbackRestoreIntent =>
                !publication.PreviousOutputExisted || outputExists || !backupExists,
            PhaseRollbackRestored =>
                !publication.PreviousOutputExisted || !outputExists || backupExists,
            PhaseCommitted =>
                !outputExists || stageExists || (!publication.PreviousOutputExisted && backupExists),
            _ => true
        };
        if (invalid)
        {
            throw new InvalidDataException(
                $"The live Core4 reward {publication.Phase} state has an impossible topology after {boundary}.");
        }
    }

    private static async Task<bool> RecoverOwnedOperationsAsync(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Preflight preflight,
        StableFileIdentity publicationParentIdentity,
        GlobalWriterLease writerLease,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassToken token,
        CancellationToken cancellationToken)
    {
        writerLease.RequireCurrentIdentity("at recovery entry");
        RequirePinnedTrustedDirectory(
            Path.GetDirectoryName(paths.OutputDirectoryPath)!,
            publicationParentIdentity,
            preflight.ExpectedUserId,
            "Core4 reward publication parent before recovery");
        if (!Directory.Exists(paths.OperationsDirectoryPath))
            return false;
        StableFileIdentity operationsDirectoryIdentity = RequireOwnedExactDirectoryPathIdentity(
            paths.OperationsDirectoryPath,
            preflight.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            "Core4 reward operations directory");
        RejectTreeReparsePoints(paths.OperationsDirectoryPath, "Core4 reward operations directory");
        string[] entries = Directory.EnumerateFileSystemEntries(paths.OperationsDirectoryPath).ToArray();
        if (entries.Length == 0)
        {
            writerLease.RequireCurrentIdentity("before empty operations recovery cleanup");
            RequirePinnedTrustedDirectory(
                Path.GetDirectoryName(paths.OutputDirectoryPath)!,
                publicationParentIdentity,
                preflight.ExpectedUserId,
                "Core4 reward publication parent before empty recovery cleanup");
            RequirePinnedExactDirectory(
                paths.OperationsDirectoryPath,
                operationsDirectoryIdentity,
                preflight.ExpectedUserId,
                ExactPrivateDirectoryMode(),
                "empty Core4 reward operations directory");
            Directory.Delete(paths.OperationsDirectoryPath);
            writerLease.RequireCurrentIdentity("after empty operations recovery cleanup");
            return false;
        }
        if (entries.Length != 1 || !Directory.Exists(entries[0]) || !IsOwnedOperationDirectory(entries[0]))
        {
            throw new InvalidDataException(
                "The Core4 reward operations directory contains a foreign or competing entry; recovery was preserved.");
        }

        string operationRoot = Path.GetFullPath(entries[0]);
        EnsureStrictDescendant(operationRoot, paths.OperationsDirectoryPath, "stale Core4 reward operation");
        StableFileIdentity operationRootIdentity = RequireOwnedExactDirectoryPathIdentity(
            operationRoot,
            preflight.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            "stale Core4 reward operation root");
        RejectTreeReparsePoints(operationRoot, "stale Core4 reward operation");
        ValidateOperationRootEntries(operationRoot, preflight.ExpectedUserId);
        string operationLeasePath = Path.Combine(operationRoot, "operation.lease");
        string journalPath = Path.Combine(operationRoot, JournalFileName);
        string journalTemporaryPath = Path.Combine(operationRoot, JournalTemporaryFileName);
        bool journalExists = File.Exists(journalPath);
        bool journalTemporaryExists = File.Exists(journalTemporaryPath);
        bool stageDirectoryExists = Directory.Exists(Path.Combine(operationRoot, "candidate"));
        bool backupDirectoryExists = Directory.Exists(Path.Combine(operationRoot, "previous-candidate"));
        if (!journalExists)
        {
            if (stageDirectoryExists || backupDirectoryExists)
            {
                throw new InvalidDataException(
                    "A journal-free Core4 reward operation contains stage/backup state and was preserved.");
            }
            if (File.Exists(operationLeasePath))
                RequireInactiveOwnedOperationLease(operationLeasePath);
            if (journalTemporaryExists)
            {
                _ = ReadBoundedFileBytes(
                    journalTemporaryPath,
                    MaximumJournalBytes,
                    "stale Core4 reward journal temporary");
                writerLease.RequireCurrentIdentity(
                    "immediately before journal-free stale journal-temporary deletion");
                DeleteOwnedExactControlFile(
                    journalTemporaryPath,
                    preflight.ExpectedUserId,
                    "stale Core4 reward journal temporary");
                writerLease.RequireCurrentIdentity(
                    "immediately after journal-free stale journal-temporary deletion");
            }
            if (File.Exists(operationLeasePath))
            {
                writerLease.RequireCurrentIdentity(
                    "immediately before journal-free stale operation-lease deletion");
                DeleteOwnedExactControlFile(
                    operationLeasePath,
                    preflight.ExpectedUserId,
                    "stale Core4 reward operation lease");
                writerLease.RequireCurrentIdentity(
                    "immediately after journal-free stale operation-lease deletion");
            }
            writerLease.RequireCurrentIdentity("before journal-free operation cleanup");
            if (Directory.EnumerateFileSystemEntries(operationRoot).Any())
            {
                throw new InvalidDataException(
                    "A journal-free Core4 reward operation contains foreign residue and was preserved.");
            }
            RequirePinnedExactDirectory(
                operationRoot,
                operationRootIdentity,
                preflight.ExpectedUserId,
                ExactPrivateDirectoryMode(),
                "empty stale Core4 reward operation root before deletion");
            Directory.Delete(operationRoot);
            writerLease.RequireCurrentIdentity("after journal-free operation-root cleanup");
            if (!Directory.EnumerateFileSystemEntries(paths.OperationsDirectoryPath).Any())
            {
                RequirePinnedTrustedDirectory(
                    Path.GetDirectoryName(paths.OutputDirectoryPath)!,
                    publicationParentIdentity,
                    preflight.ExpectedUserId,
                    "Core4 reward publication parent before stale operations cleanup");
                RequirePinnedExactDirectory(
                    paths.OperationsDirectoryPath,
                    operationsDirectoryIdentity,
                    preflight.ExpectedUserId,
                    ExactPrivateDirectoryMode(),
                    "empty stale Core4 reward operations directory before deletion");
                Directory.Delete(paths.OperationsDirectoryPath);
                writerLease.RequireCurrentIdentity("after journal-free operations cleanup");
            }
            return true;
        }
        if (!File.Exists(operationLeasePath))
            throw new InvalidDataException("A stale Core4 reward operation is missing its lease identity.");
        RequireInactiveOwnedOperationLease(operationLeasePath);
        if (journalTemporaryExists)
        {
            _ = ReadBoundedFileBytes(
                journalTemporaryPath,
                MaximumJournalBytes,
                "stale Core4 reward journal temporary");
            writerLease.RequireCurrentIdentity(
                "immediately before stale recovery journal-temporary deletion");
            DeleteOwnedExactControlFile(
                journalTemporaryPath,
                preflight.ExpectedUserId,
                "stale Core4 reward journal temporary");
            writerLease.RequireCurrentIdentity(
                "immediately after stale recovery journal-temporary deletion");
        }
        CandidateOperationJournal journal = ReadJournal(journalPath);
        ValidateJournal(journal, operationRoot, paths);
        Publication publication = new(
            operationRoot,
            journal.StagedDirectoryPath,
            journal.BackupDirectoryPath,
            journal.OutputDirectoryPath,
            operationLeasePath,
            journal.PreviousOutputExisted,
            preflight.ExpectedUserId,
            publicationParentIdentity,
            operationsDirectoryIdentity,
            operationRootIdentity)
        {
            Phase = journal.Phase,
            StagedCandidateFingerprintSha256 = journal.StagedCandidateFingerprintSha256,
            PreviousCandidateFingerprintSha256 = journal.PreviousCandidateFingerprintSha256
        };

        if (journal.Phase == PhaseCommitted)
        {
            ValidateRecoveryPhaseTopology(
                journal,
                Directory.Exists(paths.OutputDirectoryPath),
                Directory.Exists(publication.StagedDirectoryPath),
                Directory.Exists(publication.BackupDirectoryPath));
            if (!Directory.Exists(paths.OutputDirectoryPath))
            {
                throw new IOException(
                    "A committed Core4 reward operation is missing its published directory; recovery was preserved.");
            }
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprintSha256,
                "committed stale Core4 reward output");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprintSha256,
                "committed stale Core4 reward output before cleanup");
            if (Directory.Exists(publication.BackupDirectoryPath))
            {
                writerLease.RequireCurrentIdentity("before committed stale backup deletion");
                RequireCandidateTreeJournalFingerprintSha256(
                    publication.BackupDirectoryPath,
                    publication.PreviousCandidateFingerprintSha256,
                    "committed stale Core4 reward backup before deletion");
                DeleteOwnedDirectory(
                    publication.BackupDirectoryPath,
                    operationRoot,
                    "committed stale Core4 reward backup");
                writerLease.RequireCurrentIdentity("after committed stale backup deletion");
            }
            if (Directory.Exists(publication.StagedDirectoryPath))
            {
                writerLease.RequireCurrentIdentity("before committed stale stage deletion");
                RequireCandidateTreeJournalFingerprintSha256(
                    publication.StagedDirectoryPath,
                    publication.StagedCandidateFingerprintSha256,
                    "committed stale Core4 reward stage before deletion");
                DeleteOwnedDirectory(
                    publication.StagedDirectoryPath,
                    operationRoot,
                    "committed stale Core4 reward stage");
                writerLease.RequireCurrentIdentity("after committed stale stage deletion");
            }
            writerLease.RequireCurrentIdentity("before committed stale operation cleanup");
            CleanupCompletedOperation(publication, writerLease);
            writerLease.RequireCurrentIdentity("after committed recovery cleanup");
            return true;
        }

        if (journal.Phase is PhaseRollbackRestoreIntent or PhaseRollbackRestored)
        {
            if (!journal.PreviousOutputExisted)
                throw new InvalidDataException("A rollback-restore journal cannot describe a no-previous operation.");
            bool restoreOutputExists = Directory.Exists(paths.OutputDirectoryPath);
            bool restoreBackupExists = Directory.Exists(publication.BackupDirectoryPath);
            ValidateRecoveryPhaseTopology(
                journal,
                restoreOutputExists,
                Directory.Exists(publication.StagedDirectoryPath),
                restoreBackupExists);
            if (restoreOutputExists && restoreBackupExists)
            {
                throw new IOException(
                    "A rollback-restore journal has both output and backup trees; recovery was preserved.");
            }
            if (journal.Phase == PhaseRollbackRestoreIntent)
            {
                if (restoreBackupExists)
                {
                    RequireCandidateTreeJournalFingerprintSha256(
                        publication.BackupDirectoryPath,
                        publication.PreviousCandidateFingerprintSha256,
                        "rollback-intent previous Core4 reward backup");
                    RequirePublicationControlDirectories(
                        publication,
                        "immediately before stale rollback-intent restore");
                    writerLease.RequireCurrentIdentity("before stale rollback-intent restore");
                    Directory.Move(publication.BackupDirectoryPath, paths.OutputDirectoryPath);
                    writerLease.RequireCurrentIdentity("after stale rollback-intent restore");
                    RequirePublicationControlDirectories(
                        publication,
                        "after stale rollback-intent restore");
                    restoreOutputExists = true;
                    restoreBackupExists = false;
                }
                else if (!restoreOutputExists)
                {
                    throw new IOException(
                        "A rollback-restore intent lost both its prior output and backup; recovery was preserved.");
                }
                RequireCandidateTreeJournalFingerprintSha256(
                    paths.OutputDirectoryPath,
                    publication.PreviousCandidateFingerprintSha256,
                    "rollback-intent restored Core4 reward output");
                writerLease.RequireCurrentIdentity("before stale rollback-restored journal write");
                WriteJournal(publication, PhaseRollbackRestored, writerLease);
                writerLease.RequireCurrentIdentity("after stale rollback-restored journal write");
            }
            if (!restoreOutputExists || restoreBackupExists)
                throw new IOException("A rollback-restored journal does not have one exact restored output.");
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.PreviousCandidateFingerprintSha256,
                "rollback-restored Core4 reward output");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.PreviousCandidateFingerprintSha256,
                "rollback-restored Core4 reward output before cleanup");
            if (Directory.Exists(publication.StagedDirectoryPath))
            {
                writerLease.RequireCurrentIdentity("before rollback-restored stale stage deletion");
                RequireCandidateTreeJournalFingerprintSha256(
                    publication.StagedDirectoryPath,
                    publication.StagedCandidateFingerprintSha256,
                    "rollback-restored stale Core4 reward stage before deletion");
                DeleteOwnedDirectory(
                    publication.StagedDirectoryPath,
                    operationRoot,
                    "rollback-restored stale Core4 reward stage");
                writerLease.RequireCurrentIdentity("after rollback-restored stale stage deletion");
            }
            writerLease.RequireCurrentIdentity("before rollback-restored stale operation cleanup");
            CleanupCompletedOperation(publication, writerLease);
            writerLease.RequireCurrentIdentity("after rollback-restored recovery cleanup");
            return true;
        }

        bool outputExists = Directory.Exists(paths.OutputDirectoryPath);
        bool stageExists = Directory.Exists(publication.StagedDirectoryPath);
        bool backupExists = Directory.Exists(publication.BackupDirectoryPath);
        ValidateRecoveryPhaseTopology(journal, outputExists, stageExists, backupExists);
        bool afterBackupIntent = journal.Phase is
            PhaseBackupIntent or PhasePreviousBackedUp or PhasePublishIntent or PhasePublished;
        bool publicationMayHaveMoved = journal.Phase is PhasePublishIntent or PhasePublished;

        if (journal.PreviousOutputExisted)
        {
            if (afterBackupIntent && !backupExists)
            {
                if (journal.Phase == PhaseBackupIntent && outputExists)
                {
                    // The atomic phase intent was recorded, but the output rename did not happen.
                }
                else
                {
                    throw new IOException(
                        "A stale Core4 reward operation lost its required previous-candidate backup; " +
                        "the current output was not deleted.");
                }
            }
            if (!afterBackupIntent && !outputExists)
            {
                throw new IOException(
                    "A pre-backup Core4 reward recovery is missing its prior output; recovery was preserved.");
            }
        }
        else if (!publicationMayHaveMoved && outputExists)
        {
            throw new IOException(
                "A no-previous Core4 reward operation cannot own an output before publish intent; recovery was preserved.");
        }

        bool outputIsNew = publicationMayHaveMoved && outputExists && !stageExists;
        if (outputIsNew)
        {
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprintSha256,
                "stale uncommitted Core4 reward output");
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.StagedCandidateFingerprintSha256,
                "stale uncommitted Core4 reward output before deletion");
            writerLease.RequireCurrentIdentity("before stale uncommitted output deletion");
            DeleteOwnedDirectory(
                paths.OutputDirectoryPath,
                Path.GetDirectoryName(paths.OutputDirectoryPath)!,
                "stale uncommitted Core4 reward output");
            writerLease.RequireCurrentIdentity("after stale uncommitted output deletion");
            outputExists = false;
        }
        if (backupExists)
        {
            if (outputExists)
                throw new IOException("A stale Core4 reward backup competes with an occupied output.");
            RequireCandidateTreeJournalFingerprintSha256(
                publication.BackupDirectoryPath,
                publication.PreviousCandidateFingerprintSha256,
                "stale previous Core4 reward backup before restore");
            writerLease.RequireCurrentIdentity("before stale rollback-restore-intent journal write");
            WriteJournal(publication, PhaseRollbackRestoreIntent, writerLease);
            writerLease.RequireCurrentIdentity("after stale rollback-restore-intent journal write");
            RequireCandidateTreeJournalFingerprintSha256(
                publication.BackupDirectoryPath,
                publication.PreviousCandidateFingerprintSha256,
                "rollback-intent previous Core4 reward backup before restore");
            RequirePublicationControlDirectories(
                publication,
                "immediately before stale previous-candidate restore");
            writerLease.RequireCurrentIdentity("before stale previous-candidate restore");
            Directory.Move(publication.BackupDirectoryPath, paths.OutputDirectoryPath);
            writerLease.RequireCurrentIdentity("after stale previous-candidate restore");
            RequirePublicationControlDirectories(
                publication,
                "after stale previous-candidate restore");
            outputExists = true;
            RequireCandidateTreeJournalFingerprintSha256(
                paths.OutputDirectoryPath,
                publication.PreviousCandidateFingerprintSha256,
                "restored previous Core4 reward output");
            writerLease.RequireCurrentIdentity("before stale rollback-restored journal write");
            WriteJournal(publication, PhaseRollbackRestored, writerLease);
            writerLease.RequireCurrentIdentity("after stale rollback-restored journal write");
        }
        if (journal.PreviousOutputExisted && !outputExists)
            throw new IOException("Stale Core4 reward recovery could not restore its prior output.");
        if (journal.PreviousOutputExisted)
        {
            if (publication.PreviousCandidateFingerprintSha256 is not null)
            {
                RequireCandidateTreeJournalFingerprintSha256(
                    paths.OutputDirectoryPath,
                    publication.PreviousCandidateFingerprintSha256,
                    "restored or preserved previous Core4 reward output");
            }
            await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
            Id65V3RuntimeEvidenceContract.RequireFreshFocusedRuntimePassToken(token, preflight.WorkspaceRoot);
        }
        if (Directory.Exists(publication.StagedDirectoryPath))
        {
            writerLease.RequireCurrentIdentity("before stale incomplete stage deletion");
            if (publication.StagedCandidateFingerprintSha256 is not null)
            {
                RequireCandidateTreeJournalFingerprintSha256(
                    publication.StagedDirectoryPath,
                    publication.StagedCandidateFingerprintSha256,
                    "stale incomplete Core4 reward stage before deletion");
            }
            else
            {
                VerifyOwnedPartialStageTree(publication.StagedDirectoryPath, paths);
            }
            DeleteOwnedDirectory(
                publication.StagedDirectoryPath,
                operationRoot,
                "stale incomplete Core4 reward stage");
            writerLease.RequireCurrentIdentity("after stale incomplete stage deletion");
        }
        writerLease.RequireCurrentIdentity("before stale operation cleanup");
        CleanupCompletedOperation(publication, writerLease);
        writerLease.RequireCurrentIdentity("after stale operation cleanup");
        return true;
    }

    private static void ValidateRecoveryPhaseTopology(
        CandidateOperationJournal journal,
        bool outputExists,
        bool stageExists,
        bool backupExists)
    {
        if (!journal.PreviousOutputExisted && backupExists)
            throw new InvalidDataException("A no-previous Core4 reward recovery contains an impossible backup.");
        bool invalid = journal.Phase switch
        {
            PhaseCreated => backupExists || outputExists != journal.PreviousOutputExisted,
            PhaseStaged => backupExists || outputExists != journal.PreviousOutputExisted,
            PhaseBackupIntent => !journal.PreviousOutputExisted || !stageExists || outputExists == backupExists,
            PhasePreviousBackedUp => !journal.PreviousOutputExisted || outputExists || !stageExists || !backupExists,
            PhasePublishIntent => outputExists && stageExists,
            PhasePublished => stageExists,
            PhaseRollbackRestoreIntent => !journal.PreviousOutputExisted || outputExists == backupExists,
            PhaseRollbackRestored => !journal.PreviousOutputExisted || !outputExists || backupExists,
            PhaseCommitted => !outputExists || stageExists || (!journal.PreviousOutputExisted && backupExists),
            _ => true
        };
        if (invalid)
        {
            throw new InvalidDataException(
                $"The stale Core4 reward {journal.Phase} journal has an impossible output/stage/backup topology.");
        }
    }

    private static void RequireInactiveOwnedOperationLease(string operationLeasePath)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        StableFileIdentity staleLeasePath = RequireOwnedExactRegularPathIdentity(
            operationLeasePath,
            expectedUserId,
            ExactPrivateFileMode(),
            "stale Core4 reward operation lease");
        try
        {
            using FileStream operationLease = new(
                operationLeasePath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);
            StableFileIdentity staleLeaseHandle = RequireOwnedExactRegularHandleIdentity(
                operationLease,
                expectedUserId,
                ExactPrivateFileMode(),
                "stale Core4 reward operation lease");
            StableFileIdentity staleLeasePathAfter = RequireOwnedExactRegularPathIdentity(
                operationLeasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                "stale Core4 reward operation lease");
            if (!staleLeasePath.SameFile(staleLeaseHandle) ||
                !staleLeaseHandle.SameFile(staleLeasePathAfter))
            {
                throw new InvalidDataException("The stale Core4 reward operation lease changed during open.");
            }
        }
        catch (IOException exception)
        {
            throw new IOException("A Core4 reward candidate operation is still active.", exception);
        }
    }

    private static async Task VerifyPublishedCandidateAsync(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        StagedCandidate staged,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        CancellationToken cancellationToken)
    {
        await VerifyOwnedCandidateDirectoryAsync(paths, preflight, evidence, cancellationToken);
        RequireHash(HashFile(paths.OutputImagePath), staged.Readback.OutputImageSha256, "published Core4 reward BIN");
        VerifyCanonicalJsonReadback(paths.ConstructionPlanPath, staged.Plan, "published Core4 reward plan");
        VerifyCanonicalJsonReadback(
            paths.StaticReadbackReceiptPath,
            staged.Receipt,
            "published Core4 reward receipt");
    }

    private static async Task VerifyOwnedCandidateDirectoryAsync(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        Preflight preflight,
        Id65V3RuntimeEvidenceContract.Id65V3FocusedRuntimePassIdentity evidence,
        CancellationToken cancellationToken)
    {
        VerifySevenFiles(paths.OutputDirectoryPath, paths);
        IReadOnlyDictionary<string, StableFileIdentity> initialTreeIdentity =
            CaptureSevenFileIdentities(paths);
        NativeLevelReplacementBaselineExporter.ValidateCue(
            paths.OutputCuePath,
            paths.OutputImagePath,
            "MODE2/2352");
        FileInfo outputImage = new(paths.OutputImagePath);
        if (outputImage.Length != 661_547_040)
            throw new InvalidDataException("The owned Core4 reward BIN length changed.");
        byte[] expectedCue = Encoding.ASCII.GetBytes(
            DiscImage.BuildCueText(preflight.BaseCuePath, Path.GetFileName(paths.OutputImagePath)));
        byte[] actualCue = ReadBoundedFileBytes(paths.OutputCuePath, MaximumCueBytes, "owned Core4 reward CUE");
        if (!actualCue.SequenceEqual(expectedCue) || actualCue.Length != ExpectedOutputCueByteLength)
            throw new InvalidDataException("The owned Core4 reward CUE bytes changed.");
        RequireHash(Hash(actualCue), ExpectedOutputCueSha256, "owned Core4 reward CUE");

        Id65AuthoringRewardStagingRuntimeCandidatePlan plan = ReadCanonicalJson<
            Id65AuthoringRewardStagingRuntimeCandidatePlan>(paths.ConstructionPlanPath, "Core4 reward plan");
        Id65AuthoringRewardStagingRuntimeCandidateReceipt receipt = ReadCanonicalJson<
            Id65AuthoringRewardStagingRuntimeCandidateReceipt>(
            paths.StaticReadbackReceiptPath,
            "Core4 reward receipt");

        CandidateReadback expectedReadback = await VerifyCandidateImageAsync(
            preflight,
            paths.OutputImagePath,
            ExpectedAffectedRawSectorCount,
            ExpectedAffectedRawSectorCount,
            cancellationToken);
        ValidateFrozenRawSectorDiffs(expectedReadback.RawSectorDiffs);
        IReadOnlyList<RuntimeCandidateLoadCode> loadCodes = RuntimeCandidateTestHandoff.Id65ComparisonLoadCodes;
        VerifyLoadCodes(loadCodes);
        Id65AuthoringRewardStagingRuntimeCandidatePlan expectedPlan = BuildPlan(
            preflight,
            evidence,
            expectedReadback,
            loadCodes);
        RequireCanonicalValue(plan, expectedPlan, "owned Core4 reward construction plan");

        RuntimeCandidateFinderReveal reveal = BuildFinalFinderReveal(paths);
        string expectedChecklist = BuildRuntimeChecklist(paths, reveal, expectedPlan, expectedReadback, loadCodes);
        byte[] actualChecklistBytes = ReadBoundedFileBytes(
            paths.RuntimeChecklistPath,
            MaximumChecklistBytes,
            "owned Core4 reward checklist");
        byte[] expectedChecklistBytes = StrictUtf8.GetBytes(expectedChecklist);
        if (!actualChecklistBytes.SequenceEqual(expectedChecklistBytes))
            throw new InvalidDataException("The owned Core4 reward checklist bytes changed.");
        string actualChecklist = StrictUtf8.GetString(actualChecklistBytes);
        VerifyChecklist(actualChecklist, reveal, loadCodes, expectedReadback);
        string expectedGuide = BuildLocationGuideSvg(expectedPlan, expectedReadback, loadCodes);
        byte[] actualGuideBytes = ReadBoundedFileBytes(
            paths.LocationGuidePath,
            MaximumLocationGuideBytes,
            "owned Core4 reward location guide");
        byte[] expectedGuideBytes = StrictUtf8.GetBytes(expectedGuide);
        if (!actualGuideBytes.SequenceEqual(expectedGuideBytes))
            throw new InvalidDataException("The owned Core4 reward location-guide bytes changed.");
        string actualGuide = StrictUtf8.GetString(actualGuideBytes);
        VerifyLocationGuide(actualGuide, expectedReadback);

        byte[] helperBytes = ReadBoundedFileBytes(
            paths.FinderHelperPath,
            MaximumFinderHelperBytes,
            "owned Core4 Finder helper");
        RequireHash(Hash(helperBytes), ExpectedFinderHelperSha256, "owned Core4 Finder helper");
        if (helperBytes.Length != ExpectedFinderHelperByteLength ||
            (OperatingSystem.IsMacOS() && File.GetUnixFileMode(paths.FinderHelperPath) != ExactFinderMode()))
            throw new InvalidDataException("The receipt-bound Core4 Finder helper is not exact 0755.");

        string planSha256 = HashFile(paths.ConstructionPlanPath);
        string checklistSha256 = HashFile(paths.RuntimeChecklistPath);
        string guideSha256 = HashFile(paths.LocationGuidePath);
        Id65AuthoringRewardStagingRuntimeCandidateReceipt expectedReceipt = BuildReceipt(
            preflight,
            paths,
            evidence,
            expectedReadback,
            ExpectedOutputCueSha256,
            planSha256,
            checklistSha256,
            guideSha256,
            ExpectedFinderHelperSha256);
        RequireCanonicalValue(receipt, expectedReceipt, "owned Core4 reward static-readback receipt");
        IReadOnlyDictionary<string, StableFileIdentity> finalTreeIdentity = CaptureSevenFileIdentities(paths);
        if (initialTreeIdentity.Count != finalTreeIdentity.Count || initialTreeIdentity.Any(pair =>
                !finalTreeIdentity.TryGetValue(pair.Key, out StableFileIdentity? finalIdentity) ||
                pair.Value != finalIdentity))
        {
            throw new InvalidDataException("The owned Core4 reward seven-file identity changed during verification.");
        }
    }

    private static IReadOnlyDictionary<string, StableFileIdentity> CaptureSevenFileIdentities(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
        ExpectedSevenFilePaths(paths).ToDictionary(
            path => path,
            path => RequireCandidateLeafPathIdentity(path, RequireUnprivilegedCurrentIdentity()),
            StringComparer.Ordinal);

    private static void RequireCanonicalValue<T>(T actual, T expected, string label)
    {
        string actualJson = JsonSerializer.Serialize(actual, JsonOptions);
        string expectedJson = JsonSerializer.Serialize(expected, JsonOptions);
        if (actualJson != expectedJson)
            throw new InvalidDataException($"The {label} differs from exact recomputation.");
    }

    private static void ValidateFrozenRawSectorDiffs(
        IReadOnlyList<Id65AuthoringRewardStagingRawSectorDiff> diffs)
    {
        if (diffs.Count != ExpectedAffectedRawSectorCount ||
            !diffs.Select(diff => diff.RawSectorLba).SequenceEqual(
                ExpectedAffectedRuns.SelectMany(run => Enumerable.Range(run.Lba, run.SectorCount))))
        {
            throw new InvalidDataException("The ordered Core4 raw-sector diff set changed.");
        }
        string manifest = string.Concat(diffs.Select(diff => string.Create(
            CultureInfo.InvariantCulture,
            $"{diff.RawSectorLba}:{diff.HeaderChangedBytes}:{diff.SubheaderChangedBytes}:" +
            $"{diff.PayloadChangedBytes}:{diff.EdcChangedBytes}:{diff.ReservedChangedBytes}:" +
            $"{diff.EccPChangedBytes}:{diff.EccQChangedBytes}:{diff.TotalChangedBytes}\n")));
        Id65AuthoringRewardStagingPhysicalAggregate aggregate = new(
            diffs.Sum(diff => diff.HeaderChangedBytes),
            diffs.Sum(diff => diff.SubheaderChangedBytes),
            diffs.Sum(diff => diff.PayloadChangedBytes),
            diffs.Sum(diff => diff.EdcChangedBytes),
            diffs.Sum(diff => diff.ReservedChangedBytes),
            diffs.Sum(diff => diff.EccPChangedBytes),
            diffs.Sum(diff => diff.EccQChangedBytes),
            diffs.Sum(diff => (long)diff.TotalChangedBytes));
        if (HashUtf8(manifest) != ExpectedRawSectorDiffSha256 || aggregate != BuildExpectedPhysicalAggregate())
            throw new InvalidDataException("The ordered Core4 raw-sector diff categories changed.");
    }

    private static Id65AuthoringRewardStagingPhysicalAggregate BuildExpectedPhysicalAggregate() =>
        new(0, 0, ExpectedChangedLogicalBytes, 2_308, 0, 97_998, 59_633, ExpectedChangedPhysicalBytes);

    private static void VerifySevenFiles(
        string directory,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
    {
        VerifyExactOwnedSevenFileTree(directory);
        string[] expected = ExpectedSevenFileNames(paths);
        string[] actual = Directory.EnumerateFiles(directory).Select(Path.GetFileName)
            .Order(StringComparer.Ordinal).ToArray()!;
        if (!actual.SequenceEqual(expected.Order(StringComparer.Ordinal)))
            throw new InvalidDataException("The Core4 reward candidate does not contain its exact seven files.");
    }

    private static void VerifyExactOwnedSevenFileTree(string directory)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        _ = RequireOwnedExactDirectoryPathIdentity(
            directory,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            "Core4 reward candidate directory");
        RejectTreeReparsePoints(directory, "Core4 reward candidate tree");
        string[] entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        if (entries.Length != 7)
            throw new InvalidDataException("The Core4 reward candidate tree is not one flat exact seven-file directory.");
        foreach (string file in entries)
            _ = RequireCandidateLeafPathIdentity(file, expectedUserId);
    }

    private static void VerifyOwnedPartialStageTree(
        string directory,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
    {
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        _ = RequireOwnedExactDirectoryPathIdentity(
            directory,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            "partial Core4 reward stage directory");
        RejectTreeReparsePoints(directory, "partial Core4 reward stage");
        HashSet<string> expected = ExpectedSevenFileNames(paths).ToHashSet(StringComparer.Ordinal);
        foreach (string file in Directory.EnumerateFileSystemEntries(directory))
        {
            if (!expected.Contains(Path.GetFileName(file)))
            {
                throw new InvalidDataException(
                    "An unbound partial Core4 reward stage contains a foreign filename and was preserved.");
            }
            _ = RequireCandidateLeafPathIdentity(file, expectedUserId);
        }
    }

    private static CandidateTreeFingerprint[] CaptureCandidateTreeFingerprint(string directory)
    {
        VerifyExactOwnedSevenFileTree(directory);
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        StableFileIdentity directoryBefore = RequireOwnedExactDirectoryPathIdentity(
            directory,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            "Core4 reward candidate fingerprint directory");
        List<CandidateTreeFingerprint> fingerprints = [];
        foreach (string path in Directory.EnumerateFileSystemEntries(directory).Order(StringComparer.Ordinal))
        {
            StableFileIdentity before = RequireCandidateLeafPathIdentity(path, expectedUserId);
            string sha256 = HashFile(path);
            StableFileIdentity after = RequireCandidateLeafPathIdentity(path, expectedUserId);
            if (before != after)
                throw new InvalidDataException("A Core4 reward candidate leaf changed while it was fingerprinted.");
            fingerprints.Add(new(
                Path.GetFileName(path),
                sha256,
                after,
                new(
                    directoryBefore.Device,
                    directoryBefore.Inode,
                    directoryBefore.UserId,
                    directoryBefore.Mode)));
        }
        StableFileIdentity directoryAfter = RequireOwnedExactDirectoryPathIdentity(
            directory,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            "Core4 reward candidate fingerprint directory after read");
        if (!directoryBefore.SameFile(directoryAfter) || directoryBefore.Mode != directoryAfter.Mode)
            throw new InvalidDataException("The Core4 reward candidate directory changed while fingerprinted.");
        return fingerprints.ToArray();
    }

    private static void RequireCandidateTreeFingerprint(
        string directory,
        IReadOnlyList<CandidateTreeFingerprint> expected,
        string label)
    {
        CandidateTreeFingerprint[] actual = CaptureCandidateTreeFingerprint(directory);
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"The {label} differs from its exact verified identity and bytes.");
    }

    private static string HashCandidateTreeJournalFingerprint(
        IReadOnlyList<CandidateTreeFingerprint> fingerprints)
    {
        string manifest = CandidateTreeJournalFingerprintDomain +
            string.Concat(fingerprints.Select(fingerprint => string.Create(
            CultureInfo.InvariantCulture,
            $"{fingerprint.FileName}|{fingerprint.Sha256}|{fingerprint.Identity.Device}|" +
            $"{fingerprint.Identity.Inode}|{fingerprint.Identity.UserId}|{fingerprint.Identity.Mode}|" +
            $"{fingerprint.Identity.LinkCount}|" +
            $"{fingerprint.Identity.ByteLength}|{fingerprint.DirectoryIdentity.Device}|" +
            $"{fingerprint.DirectoryIdentity.Inode}|{fingerprint.DirectoryIdentity.UserId}|" +
            $"{fingerprint.DirectoryIdentity.Mode}\n")));
        return HashUtf8(manifest);
    }

    private static StableFileIdentity RequireCandidateLeafPathIdentity(string path, uint expectedUserId)
    {
        string fileName = Path.GetFileName(path);
        bool finderHelper = string.Equals(
            fileName,
            OutputPrefix + "-Reveal-in-Finder.command",
            StringComparison.Ordinal);
        if (!finderHelper && !ExpectedCandidateDataFileNames().Contains(fileName, StringComparer.Ordinal))
            throw new InvalidDataException($"A Core4 reward candidate contains a foreign leaf: {fileName}");
        return RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            finderHelper ? ExactFinderMode() : ExactDataLeafMode(),
            finderHelper ? "Core4 reward Finder helper" : "Core4 reward data leaf");
    }

    private static string[] ExpectedCandidateDataFileNames() =>
    [
        OutputPrefix + ".bin",
        OutputPrefix + ".cue",
        OutputPrefix + "-construction-plan.json",
        OutputPrefix + "-static-readback-receipt.json",
        OutputPrefix + "-runtime-checklist.md",
        OutputPrefix + "-location-guide.svg"
    ];

    private static void RequireCandidateTreeJournalFingerprintSha256(
        string directory,
        string? expectedSha256,
        string label)
    {
        if (!IsCanonicalSha256(expectedSha256))
            throw new InvalidDataException($"The {label} has no canonical journal identity binding.");
        string actual = HashCandidateTreeJournalFingerprint(CaptureCandidateTreeFingerprint(directory));
        RequireHash(actual, expectedSha256!, label + " journal identity");
    }

    private static string[] ExpectedSevenFileNames(Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
        ExpectedSevenFilePaths(paths).Select(Path.GetFileName).ToArray()!;

    private static string[] ExpectedSevenFilePaths(Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
    [
        paths.OutputImagePath,
        paths.OutputCuePath,
        paths.ConstructionPlanPath,
        paths.StaticReadbackReceiptPath,
        paths.RuntimeChecklistPath,
        paths.LocationGuidePath,
        paths.FinderHelperPath
    ];

    private static void WriteJournal(
        Publication publication,
        string phase,
        GlobalWriterLease writerLease)
    {
        writerLease.RequireCurrentIdentity($"before {phase} journal write");
        CandidateOperationJournal journal = new(
            JournalSchemaVersion,
            OperationKind,
            phase,
            publication.OperationRootPath,
            publication.OutputDirectoryPath,
            publication.StagedDirectoryPath,
            publication.BackupDirectoryPath,
            publication.PreviousOutputExisted,
            publication.StagedCandidateFingerprintSha256,
            publication.PreviousCandidateFingerprintSha256);
        string path = Path.Combine(publication.OperationRootPath, JournalFileName);
        WriteJournalAtomically(publication, path, JsonSerializer.Serialize(journal, JournalJsonOptions) + "\n");
        writerLease.RequireCurrentIdentity($"after {phase} journal write");
        publication.Phase = phase;
    }

    private static CandidateOperationJournal ReadJournal(string path)
    {
        if (!File.Exists(path))
            throw new InvalidDataException("A stale Core4 reward operation is missing its canonical journal.");
        RejectReparsePoint(path, "stale Core4 reward journal");
        _ = RequireOwnedExactRegularPathIdentity(
            path,
            RequireUnprivilegedCurrentIdentity(),
            ExactPrivateFileMode(),
            "stale Core4 reward journal");
        byte[] bytes = ReadBoundedFileBytes(path, MaximumJournalBytes, "stale Core4 reward journal");
        if (HasUtf8Bom(bytes))
            throw new InvalidDataException("A stale Core4 reward operation journal contains a UTF-8 BOM.");
        string text = StrictUtf8.GetString(bytes);
        CandidateOperationJournal journal = JsonSerializer.Deserialize<CandidateOperationJournal>(
                text,
                JournalJsonOptions)
            ?? throw new InvalidDataException("A stale Core4 reward operation journal is empty.");
        if (text != JsonSerializer.Serialize(journal, JournalJsonOptions) + "\n")
            throw new InvalidDataException("A stale Core4 reward operation journal is not canonical JSON.");
        return journal;
    }

    private static void ValidateJournal(
        CandidateOperationJournal journal,
        string operationRoot,
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths)
    {
        string[] phases =
        [
            PhaseCreated,
            PhaseStaged,
            PhaseBackupIntent,
            PhasePreviousBackedUp,
            PhasePublishIntent,
            PhasePublished,
            PhaseRollbackRestoreIntent,
            PhaseRollbackRestored,
            PhaseCommitted
        ];
        if (journal.SchemaVersion != JournalSchemaVersion || journal.OperationKind != OperationKind ||
            !phases.Contains(journal.Phase, StringComparer.Ordinal) ||
            !PathEquals(journal.OperationRootPath, operationRoot) ||
            !PathEquals(journal.OutputDirectoryPath, paths.OutputDirectoryPath) ||
            !PathEquals(journal.StagedDirectoryPath, Path.Combine(operationRoot, "candidate")) ||
            !PathEquals(journal.BackupDirectoryPath, Path.Combine(operationRoot, "previous-candidate")))
        {
            throw new InvalidDataException("A stale Core4 reward journal escaped its exact owned paths or phases.");
        }
        EnsureStrictDescendant(journal.OperationRootPath, paths.OperationsDirectoryPath, "journal operation root");
        EnsureStrictDescendant(journal.StagedDirectoryPath, operationRoot, "journal staged candidate");
        EnsureStrictDescendant(journal.BackupDirectoryPath, operationRoot, "journal previous candidate");
        bool stageBindingRequired = journal.Phase != PhaseCreated;
        bool previousBindingRequired = journal.PreviousOutputExisted && journal.Phase is
            PhaseBackupIntent or PhasePreviousBackedUp or PhasePublishIntent or PhasePublished or
            PhaseRollbackRestoreIntent or PhaseRollbackRestored or PhaseCommitted;
        if (stageBindingRequired != IsCanonicalSha256(journal.StagedCandidateFingerprintSha256) ||
            previousBindingRequired != IsCanonicalSha256(journal.PreviousCandidateFingerprintSha256) ||
            (!journal.PreviousOutputExisted && journal.PreviousCandidateFingerprintSha256 is not null))
        {
            throw new InvalidDataException(
                "A stale Core4 reward journal lacks its phase-specific candidate identity bindings.");
        }
    }

    private static void CleanupCompletedOperation(
        Publication publication,
        GlobalWriterLease writerLease)
    {
        if (Directory.Exists(publication.StagedDirectoryPath) ||
            Directory.Exists(publication.BackupDirectoryPath))
        {
            throw new IOException("A Core4 reward operation cannot be cleaned while stage/backup ownership remains.");
        }
        RequirePublicationControlDirectories(publication, "before completed-operation cleanup");
        ValidateOperationRootEntries(publication.OperationRootPath, publication.ExpectedUserId);
        string journal = Path.Combine(publication.OperationRootPath, JournalFileName);
        if (File.Exists(journal))
        {
            writerLease.RequireCurrentIdentity("immediately before completed journal deletion");
            DeleteOwnedExactControlFile(journal, publication.ExpectedUserId, "completed Core4 reward journal");
            writerLease.RequireCurrentIdentity("immediately after completed journal deletion");
        }
        if (File.Exists(publication.OperationLeasePath))
        {
            writerLease.RequireCurrentIdentity("immediately before completed operation-lease deletion");
            DeleteOwnedExactControlFile(
                publication.OperationLeasePath,
                publication.ExpectedUserId,
                "completed Core4 reward operation lease");
            writerLease.RequireCurrentIdentity("immediately after completed operation-lease deletion");
        }
        if (Directory.EnumerateFileSystemEntries(publication.OperationRootPath).Any())
            throw new IOException("A completed Core4 reward operation contains unexpected cleanup debris.");
        RequirePinnedExactDirectory(
            publication.OperationRootPath,
            publication.OperationRootIdentity,
            publication.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            "empty Core4 reward operation root before deletion");
        writerLease.RequireCurrentIdentity("immediately before completed operation-root deletion");
        Directory.Delete(publication.OperationRootPath);
        writerLease.RequireCurrentIdentity("immediately after completed operation-root deletion");
        string operations = Path.GetDirectoryName(publication.OperationRootPath)!;
        RequirePinnedTrustedDirectory(
            Path.GetDirectoryName(publication.OutputDirectoryPath)!,
            publication.PublicationParentIdentity,
            publication.ExpectedUserId,
            "Core4 reward publication parent before operations cleanup");
        if (Directory.Exists(operations) && !Directory.EnumerateFileSystemEntries(operations).Any())
        {
            RequirePinnedExactDirectory(
                operations,
                publication.OperationsDirectoryIdentity,
                publication.ExpectedUserId,
                ExactPrivateDirectoryMode(),
                "empty Core4 reward operations directory before deletion");
            writerLease.RequireCurrentIdentity("immediately before completed operations-directory deletion");
            Directory.Delete(operations);
            writerLease.RequireCurrentIdentity("immediately after completed operations-directory deletion");
        }
    }

    private static void CleanupUnjournaledOperation(
        string operationRoot,
        string operationLeasePath,
        string publicationParentPath,
        uint expectedUserId,
        StableFileIdentity publicationParentIdentity,
        StableFileIdentity operationsDirectoryIdentity,
        StableFileIdentity operationRootIdentity,
        GlobalWriterLease writerLease)
    {
        if (!Directory.Exists(operationRoot))
            return;
        string operations = Path.GetDirectoryName(operationRoot) ??
            throw new InvalidOperationException("The unjournaled Core4 operation has no owned parent.");
        RequirePinnedTrustedDirectory(
            publicationParentPath,
            publicationParentIdentity,
            expectedUserId,
            "Core4 reward publication parent before unjournaled cleanup");
        RequirePinnedExactDirectory(
            operations,
            operationsDirectoryIdentity,
            expectedUserId,
            ExactPrivateDirectoryMode(),
            "Core4 reward operations directory before unjournaled cleanup");
        RequirePinnedExactDirectory(
            operationRoot,
            operationRootIdentity,
            expectedUserId,
            ExactPrivateDirectoryMode(),
            "Core4 reward operation root before unjournaled cleanup");
        EnsureStrictDescendant(operationRoot, operations, "unjournaled Core4 reward operation");
        RejectTreeReparsePoints(operationRoot, "unjournaled Core4 reward operation");
        string journalPath = Path.Combine(operationRoot, JournalFileName);
        if (File.Exists(journalPath) || Directory.Exists(Path.Combine(operationRoot, "candidate")) ||
            Directory.Exists(Path.Combine(operationRoot, "previous-candidate")))
        {
            throw new IOException(
                "An unjournaled Core4 reward operation contains ambiguous owned state and was preserved.");
        }
        string[] entries = Directory.EnumerateFileSystemEntries(operationRoot).ToArray();
        if (entries.Any(entry => !PathEquals(entry, operationLeasePath)))
        {
            throw new IOException(
                "An unjournaled Core4 reward operation contains a foreign entry and was preserved.");
        }
        if (File.Exists(operationLeasePath))
        {
            RejectReparsePoint(operationLeasePath, "unjournaled Core4 reward operation lease");
            writerLease.RequireCurrentIdentity("immediately before unjournaled operation-lease deletion");
            DeleteOwnedExactControlFile(
                operationLeasePath,
                expectedUserId,
                "unjournaled Core4 reward operation lease");
            writerLease.RequireCurrentIdentity("immediately after unjournaled operation-lease deletion");
        }
        RequirePinnedExactDirectory(
            operationRoot,
            operationRootIdentity,
            expectedUserId,
            ExactPrivateDirectoryMode(),
            "empty unjournaled Core4 reward operation root before deletion");
        writerLease.RequireCurrentIdentity("immediately before unjournaled operation-root deletion");
        Directory.Delete(operationRoot);
        writerLease.RequireCurrentIdentity("immediately after unjournaled operation-root deletion");
        if (Directory.Exists(operations) && !Directory.EnumerateFileSystemEntries(operations).Any())
        {
            RequirePinnedTrustedDirectory(
                publicationParentPath,
                publicationParentIdentity,
                expectedUserId,
                "Core4 reward publication parent before empty operations cleanup");
            RequirePinnedExactDirectory(
                operations,
                operationsDirectoryIdentity,
                expectedUserId,
                ExactPrivateDirectoryMode(),
                "empty Core4 reward operations directory before unjournaled deletion");
            writerLease.RequireCurrentIdentity("immediately before unjournaled operations-directory deletion");
            Directory.Delete(operations);
            writerLease.RequireCurrentIdentity("immediately after unjournaled operations-directory deletion");
        }
    }

    private static void CleanupEmptyOperationsBeforeRootCreation(
        string operationRoot,
        string operationsDirectory,
        string publicationParentPath,
        uint expectedUserId,
        StableFileIdentity publicationParentIdentity,
        StableFileIdentity operationsDirectoryIdentity,
        GlobalWriterLease writerLease)
    {
        if (Directory.Exists(operationRoot) || File.Exists(operationRoot))
        {
            throw new IOException(
                "An unbound Core4 reward operation root appeared before its identity was captured and was preserved.");
        }
        RequirePinnedTrustedDirectory(
            publicationParentPath,
            publicationParentIdentity,
            expectedUserId,
            "Core4 reward publication parent before pre-root cleanup");
        RequirePinnedExactDirectory(
            operationsDirectory,
            operationsDirectoryIdentity,
            expectedUserId,
            ExactPrivateDirectoryMode(),
            "Core4 reward operations directory before pre-root cleanup");
        if (Directory.EnumerateFileSystemEntries(operationsDirectory).Any())
            throw new IOException("The pre-root Core4 reward operations directory is not empty and was preserved.");
        writerLease.RequireCurrentIdentity("immediately before pre-root operations-directory deletion");
        Directory.Delete(operationsDirectory);
        writerLease.RequireCurrentIdentity("immediately after pre-root operations-directory deletion");
    }

    private static void ValidateOperationRootEntries(string operationRoot, uint expectedUserId)
    {
        HashSet<string> allowed = new(StringComparer.Ordinal)
        {
            "candidate",
            "previous-candidate",
            "operation.lease",
            JournalFileName,
            JournalTemporaryFileName
        };
        foreach (string entry in Directory.EnumerateFileSystemEntries(operationRoot))
        {
            RejectReparsePoint(entry, "Core4 reward operation entry");
            string name = Path.GetFileName(entry);
            if (!allowed.Contains(name))
                throw new InvalidDataException($"A Core4 reward operation contains an unowned entry: {entry}");
            bool isDirectory = Directory.Exists(entry);
            bool shouldBeDirectory = name is "candidate" or "previous-candidate";
            if (isDirectory != shouldBeDirectory)
                throw new InvalidDataException($"A Core4 reward operation entry has the wrong role: {entry}");
            if (shouldBeDirectory)
            {
                _ = RequireOwnedExactDirectoryPathIdentity(
                    entry,
                    expectedUserId,
                    ExactCandidateDirectoryMode(),
                    "Core4 reward operation candidate directory");
            }
            else
            {
                _ = RequireOwnedExactRegularPathIdentity(
                    entry,
                    expectedUserId,
                    ExactPrivateFileMode(),
                    "Core4 reward operation control file");
            }
        }
    }

    private static void RequirePublicationControlDirectories(Publication publication, string boundary)
    {
        RequirePinnedTrustedDirectory(
            Path.GetDirectoryName(publication.OutputDirectoryPath)!,
            publication.PublicationParentIdentity,
            publication.ExpectedUserId,
            $"Core4 reward publication parent {boundary}");
        RequirePinnedExactDirectory(
            Path.GetDirectoryName(publication.OperationRootPath)!,
            publication.OperationsDirectoryIdentity,
            publication.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            $"Core4 reward operations directory {boundary}");
        RequirePinnedExactDirectory(
            publication.OperationRootPath,
            publication.OperationRootIdentity,
            publication.ExpectedUserId,
            ExactPrivateDirectoryMode(),
            $"Core4 reward operation root {boundary}");
    }

    private static void DeleteOwnedExactControlFile(string path, uint expectedUserId, string label)
    {
        StableFileIdentity before = RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            ExactPrivateFileMode(),
            label);
        StableFileIdentity immediatelyBeforeDelete = RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            ExactPrivateFileMode(),
            label + " immediately before deletion");
        if (before != immediatelyBeforeDelete)
            throw new InvalidDataException($"The {label} changed before deletion and was preserved.");
        File.Delete(path);
    }

    private static bool IsOwnedOperationDirectory(string operationRoot)
    {
        string name = Path.GetFileName(operationRoot);
        return name.StartsWith(OperationDirectoryPrefix, StringComparison.Ordinal) &&
            Guid.TryParseExact(name[OperationDirectoryPrefix.Length..], "N", out _);
    }

    private static void DeleteOwnedDirectory(string path, string ownerParent, string label)
    {
        EnsureStrictDescendant(path, ownerParent, label);
        if (!Directory.Exists(path))
            return;
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        StableFileIdentity directoryIdentity = RequireOwnedExactDirectoryPathIdentity(
            path,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            label);
        RejectTreeReparsePoints(path, label);
        foreach (string entry in Directory.EnumerateFileSystemEntries(path).Order(StringComparer.Ordinal))
        {
            StableFileIdentity before = RequireCandidateLeafPathIdentity(entry, expectedUserId);
            StableFileIdentity immediatelyBeforeDelete = RequireCandidateLeafPathIdentity(entry, expectedUserId);
            if (before != immediatelyBeforeDelete)
                throw new InvalidDataException($"The {label} leaf changed before deletion and was preserved.");
            File.Delete(entry);
        }
        StableFileIdentity directoryBeforeDelete = RequireOwnedExactDirectoryPathIdentity(
            path,
            expectedUserId,
            ExactCandidateDirectoryMode(),
            label + " immediately before deletion");
        if (!directoryIdentity.SameFile(directoryBeforeDelete) ||
            Directory.EnumerateFileSystemEntries(path).Any())
        {
            throw new InvalidDataException($"The {label} changed before deletion and was preserved.");
        }
        Directory.Delete(path);
    }

    private static GlobalWriterLease AcquireGlobalWriterLease(
        string leasePath,
        StableFileIdentity publicationParentIdentity,
        uint expectedUserId)
    {
        RejectExistingAncestorReparsePoints(leasePath, "Core4 reward global writer lease");
        string parent = Path.GetDirectoryName(leasePath) ??
            throw new InvalidOperationException("The Core4 reward global lease has no parent.");
        RequirePinnedTrustedDirectory(
            parent,
            publicationParentIdentity,
            expectedUserId,
            "Core4 reward publication parent before global lease open");
        if (Directory.Exists(leasePath))
            throw new InvalidDataException("The Core4 reward global writer lease path is a directory.");
        StableFileIdentity? existingPathIdentity = null;
        if (File.Exists(leasePath))
        {
            RejectReparsePoint(leasePath, "Core4 reward global writer lease");
            existingPathIdentity = RequireOwnedExactRegularPathIdentity(
                leasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                "Core4 reward global writer lease");
        }

        FileStream lease;
        try
        {
            lease = existingPathIdentity is null
                ? OpenNewExactFile(
                    leasePath,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    1,
                    FileOptions.None,
                    ExactPrivateFileMode())
                : new FileStream(
                    leasePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    1,
                    FileOptions.None);
        }
        catch (IOException exception) when (existingPathIdentity is null && File.Exists(leasePath))
        {
            existingPathIdentity = RequireOwnedExactRegularPathIdentity(
                leasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                "raced Core4 reward global writer lease");
            try
            {
                lease = new FileStream(
                    leasePath,
                    FileMode.Open,
                    FileAccess.ReadWrite,
                    FileShare.ReadWrite,
                    1,
                    FileOptions.None);
            }
            catch (IOException racedOpenFailure)
            {
                throw new IOException(
                    "The raced Core4 reward global writer lease could not be opened.",
                    new AggregateException(exception, racedOpenFailure));
            }
        }
        catch (IOException exception)
        {
            throw new IOException("The Core4 reward global writer lease file could not be opened.", exception);
        }
        try
        {
            if (existingPathIdentity is null)
                SetExactHandleMode(lease, ExactPrivateFileMode(), "new Core4 reward global writer lease");
            RejectReparsePoint(leasePath, "Core4 reward global writer lease");
            StableFileIdentity leaseHandleIdentity = RequireOwnedExactRegularHandleIdentity(
                lease,
                expectedUserId,
                ExactPrivateFileMode(),
                "Core4 reward global writer lease");
            StableFileIdentity leasePathIdentity = RequireOwnedExactRegularPathIdentity(
                leasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                "Core4 reward global writer lease");
            if (!leaseHandleIdentity.SameFile(leasePathIdentity) ||
                (existingPathIdentity is not null && !existingPathIdentity.SameFile(leaseHandleIdentity)))
            {
                throw new InvalidDataException("The Core4 reward global writer lease changed during open.");
            }
            if (OperatingSystem.IsWindows())
            {
                try
                {
                    lease.Lock(0, 1);
                }
                catch (IOException exception) when (IsWindowsLockContention(exception))
                {
                    throw new IOException(ActiveWriterLeaseMessage, exception);
                }
            }
            else
            {
                int descriptor = checked((int)lease.SafeFileHandle.DangerousGetHandle());
                if (NativeFlock(descriptor, LockExclusive | LockNonBlocking) != 0)
                {
                    int error = Marshal.GetLastPInvokeError();
                    Win32Exception nativeError = new(error);
                    if (IsUnixWouldBlock(error))
                        throw new IOException(ActiveWriterLeaseMessage, nativeError);
                    throw new IOException(
                        $"The Core4 reward global writer OS lease failed (errno {error}: {nativeError.Message}).",
                        nativeError);
                }
            }
            RequirePinnedTrustedDirectory(
                parent,
                publicationParentIdentity,
                expectedUserId,
                "Core4 reward publication parent after global lease open");
            return new GlobalWriterLease(
                lease,
                leasePath,
                leaseHandleIdentity,
                parent,
                publicationParentIdentity,
                expectedUserId);
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private static bool IsUnixWouldBlock(int error)
    {
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsFreeBSD())
            return error == 35;
        if (OperatingSystem.IsLinux())
            return error == 11;
        return error is 11 or 35;
    }

    private static bool IsWindowsLockContention(IOException exception) =>
        (exception.HResult & 0xFFFF) == WindowsErrorLockViolation;

    [DllImport("libc", EntryPoint = "flock", SetLastError = true)]
    private static extern int NativeFlock(int fileDescriptor, int operation);

    private static StableFileIdentity ReadPathIdentity(string path, string label)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Core4 reward filesystem identity requires macOS lstat.");
        if (NativeLStat(path, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not lstat the {label} (errno {error}).", new Win32Exception(error));
        }
        return ReadIdentity(status);
    }

    private static StableFileIdentity ReadHandleIdentity(FileStream stream, string label)
    {
        int descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        if (NativeFStat(descriptor, out DarwinStat status) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not fstat the {label} (errno {error}).", new Win32Exception(error));
        }
        return ReadIdentity(status);
    }

    private static StableFileIdentity RequireRegularSingleLinkPathIdentity(string path, string label) =>
        RequireOwnedTrustedRegularIdentity(
            ReadPathIdentity(path, label),
            RequireUnprivilegedCurrentIdentity(),
            label);

    private static StableFileIdentity RequireRegularSingleLinkHandleIdentity(FileStream stream, string label) =>
        RequireOwnedTrustedRegularIdentity(
            ReadHandleIdentity(stream, label),
            RequireUnprivilegedCurrentIdentity(),
            label);

    private static StableFileIdentity RequireOwnedExactRegularPathIdentity(
        string path,
        uint expectedUserId,
        UnixFileMode expectedMode,
        string label) =>
        RequireOwnedExactRegularIdentity(
            ReadPathIdentity(path, label),
            expectedUserId,
            expectedMode,
            label);

    private static StableFileIdentity RequireOwnedExactRegularHandleIdentity(
        FileStream stream,
        uint expectedUserId,
        UnixFileMode expectedMode,
        string label) =>
        RequireOwnedExactRegularIdentity(
            ReadHandleIdentity(stream, label),
            expectedUserId,
            expectedMode,
            label);

    private static StableFileIdentity RequireOwnedExactDirectoryPathIdentity(
        string path,
        uint expectedUserId,
        UnixFileMode expectedMode,
        string label) =>
        RequireOwnedExactDirectoryIdentity(
            ReadPathIdentity(path, label),
            expectedUserId,
            expectedMode,
            label);

    private static StableFileIdentity RequireOwnedTrustedDirectoryPathIdentity(
        string path,
        uint expectedUserId,
        string label) =>
        RequireOwnedTrustedDirectoryIdentity(ReadPathIdentity(path, label), expectedUserId, label);

    private static StableFileIdentity RequireOwnedTrustedRegularIdentity(
        StableFileIdentity identity,
        uint expectedUserId,
        string label)
    {
        if ((identity.Mode & 0xF000) != RegularFileType || identity.LinkCount != 1)
            throw new InvalidDataException($"The {label} must be one regular file with st_nlink == 1.");
        RequireCurrentOwnedIdentity(identity, expectedUserId, label);
        if ((identity.Mode & GroupOrOtherWriteMask) != 0 ||
            (identity.Mode & 0x0E00) != 0 ||
            (identity.Mode & (ushort)UnixFileMode.UserRead) == 0)
        {
            throw new InvalidDataException(
                $"The {label} must be owner-readable with no special, group-write, or other-write bits.");
        }
        return identity;
    }

    private static StableFileIdentity RequireOwnedExactRegularIdentity(
        StableFileIdentity identity,
        uint expectedUserId,
        UnixFileMode expectedMode,
        string label)
    {
        RequireOwnedTrustedRegularIdentity(identity, expectedUserId, label);
        RequireExactPermissions(identity, expectedMode, label);
        return identity;
    }

    private static StableFileIdentity RequireOwnedTrustedDirectoryIdentity(
        StableFileIdentity identity,
        uint expectedUserId,
        string label)
    {
        if ((identity.Mode & 0xF000) != DirectoryFileType)
            throw new InvalidDataException($"The {label} must be a real directory.");
        RequireCurrentOwnedIdentity(identity, expectedUserId, label);
        if ((identity.Mode & GroupOrOtherWriteMask) != 0)
            throw new InvalidDataException($"The {label} must not be group- or other-writable.");
        return identity;
    }

    private static StableFileIdentity RequireOwnedExactDirectoryIdentity(
        StableFileIdentity identity,
        uint expectedUserId,
        UnixFileMode expectedMode,
        string label)
    {
        RequireOwnedTrustedDirectoryIdentity(identity, expectedUserId, label);
        RequireExactPermissions(identity, expectedMode, label);
        return identity;
    }

    private static void RequireCurrentOwnedIdentity(
        StableFileIdentity identity,
        uint expectedUserId,
        string label)
    {
        if (identity.UserId != expectedUserId || expectedUserId != RequireUnprivilegedCurrentIdentity())
            throw new InvalidDataException($"The {label} is not owned by the unchanged current user.");
    }

    private static void RequireExactPermissions(
        StableFileIdentity identity,
        UnixFileMode expectedMode,
        string label)
    {
        ushort expected = checked((ushort)expectedMode);
        if ((identity.Mode & PermissionMask) != expected)
        {
            throw new InvalidDataException(
                $"The {label} mode is 0{Convert.ToString(identity.Mode & PermissionMask, 8)}, " +
                $"expected exact 0{Convert.ToString(expected, 8)}.");
        }
    }

    private static StableFileIdentity ReadIdentity(DarwinStat status) => new(
        status.Device,
        status.Inode,
        status.UserId,
        status.Mode,
        status.LinkCount,
        status.ByteLength,
        status.ModifiedSeconds,
        status.ModifiedNanoseconds,
        status.ChangedSeconds,
        status.ChangedNanoseconds);

    private static uint RequireUnprivilegedCurrentIdentity() =>
        RequireUnprivilegedCurrentIdentity(NativeGetUserId(), NativeGetEffectiveUserId());

    private static uint RequireUnprivilegedCurrentIdentity(uint userId, uint effectiveUserId)
    {
        if (userId == 0 || userId != effectiveUserId)
            throw new InvalidOperationException("Core4 reward publication requires one unchanged unprivileged uid/euid.");
        return userId;
    }

    [StructLayout(LayoutKind.Explicit, Size = 144)]
    private struct DarwinStat
    {
        [FieldOffset(0)] internal int Device;
        [FieldOffset(4)] internal ushort Mode;
        [FieldOffset(6)] internal ushort LinkCount;
        [FieldOffset(8)] internal ulong Inode;
        [FieldOffset(16)] internal uint UserId;
        [FieldOffset(20)] internal uint GroupId;
        [FieldOffset(48)] internal long ModifiedSeconds;
        [FieldOffset(56)] internal long ModifiedNanoseconds;
        [FieldOffset(64)] internal long ChangedSeconds;
        [FieldOffset(72)] internal long ChangedNanoseconds;
        [FieldOffset(96)] internal long ByteLength;
    }

    [DllImport("libc", EntryPoint = "lstat", SetLastError = true)]
    private static extern int NativeLStat(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        out DarwinStat status);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int NativeFStat(int fileDescriptor, out DarwinStat status);

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint NativeGetUserId();

    [DllImport("libc", EntryPoint = "geteuid")]
    private static extern uint NativeGetEffectiveUserId();

    [DllImport("libc", EntryPoint = "fchmod", SetLastError = true)]
    private static extern int NativeFChmod(int fileDescriptor, ushort mode);

    [DllImport("libc", EntryPoint = "mkdir", SetLastError = true)]
    private static extern int NativeMkdir(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string path,
        ushort mode);

    private static async Task RepinInputsAsync(Preflight preflight, CancellationToken cancellationToken)
    {
        FileIdentity baseIdentity = await ReadFileIdentityAsync(preflight.BaseImagePath, cancellationToken);
        FileIdentity cueIdentity = await ReadFileIdentityAsync(preflight.BaseCuePath, cancellationToken);
        FileIdentity foundationIdentity = await ReadFileIdentityAsync(preflight.FoundationImagePath, cancellationToken);
        RequireHash(baseIdentity.Sha256, BaseImageSha256, "locked display-name BIN after publication");
        RequireHash(cueIdentity.Sha256, BaseCueSha256, "locked display-name CUE after publication");
        RequireHash(
            foundationIdentity.Sha256,
            FoundationDonorImageSha256,
            "frozen foundation donor BIN after publication");
        if (cueIdentity.ByteLength != preflight.BaseCueByteLength ||
            foundationIdentity.ByteLength != preflight.FoundationByteLength)
        {
            throw new InvalidDataException("A Core4 reward source length changed during publication.");
        }
    }

    private static Id65AuthoringRewardStagingRuntimeCandidateResult BuildResult(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        StagedCandidate staged,
        bool recoveryVerified) =>
        new(
            paths,
            staged.Plan,
            staged.Receipt,
            BuildFinalFinderReveal(paths),
            staged.Readback.RawSectorDiffs,
            staged.Readback.OutputImageSha256,
            staged.Readback.OutputWadSha256,
            staged.Readback.OutputRow80Sha256,
            staged.Readback.RawSectorDiffSha256,
            staged.Readback.ChangedLogicalBytes,
            staged.Readback.ChangedPhysicalBytes,
            staged.Readback.RebuiltRawSectorCount,
            staged.Readback.VerifiedRawSectorCount,
            recoveryVerified,
            ProcessCrashRecoveryVerified: false,
            PhaseBoundaryProcessCrashRecoveryVerified: true,
            MidMutationProcessCrashRecoveryVerified: false,
            PowerLossDurabilityVerified: false,
            Core4RuntimePending: true,
            NormalCreateBinEnabled: false);

    private static RuntimeCandidateFinderReveal BuildFinalFinderReveal(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths) =>
        new(
            paths.OutputCuePath,
            paths.OutputImagePath,
            paths.FinderHelperPath,
            $"/usr/bin/open -R {ShellSingleQuote(paths.OutputCuePath)}",
            CuePairingVerified: true,
            HelperIsExecutable: true);

    private static void VerifyLoadCodes(IReadOnlyList<RuntimeCandidateLoadCode> loadCodes)
    {
        (int LevelId, string Selection)[] expected =
        [
            (65, "Left, then Down"),
            (13, TestLevelWarpPatch.TargetSelectionText(13)),
            (64, TestLevelWarpPatch.TargetSelectionText(64)),
            (15, TestLevelWarpPatch.TargetSelectionText(15))
        ];
        if (loadCodes.Count != expected.Length || loadCodes.Where((code, index) =>
                code.LevelId != expected[index].LevelId ||
                code.TargetSelection != expected[index].Selection).Any())
        {
            throw new InvalidDataException("The exact four ID65/retail comparison load codes changed.");
        }
    }

    private static async Task WriteJsonAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
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
        byte[] bytes = encoding.GetBytes(content);
        await using FileStream stream = OpenNewExactFile(
            path,
            FileAccess.Write,
            FileShare.None,
            64 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough,
            ExactDataLeafMode());
        uint expectedUserId = RequireUnprivilegedCurrentIdentity();
        SetExactHandleMode(stream, ExactDataLeafMode(), "new Core4 reward data leaf");
        StableFileIdentity handleBefore = RequireOwnedExactRegularHandleIdentity(
            stream,
            expectedUserId,
            ExactDataLeafMode(),
            "new Core4 reward data leaf");
        StableFileIdentity pathBefore = RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            ExactDataLeafMode(),
            "new Core4 reward data leaf");
        if (!handleBefore.SameFile(pathBefore))
            throw new InvalidDataException("A new Core4 reward data leaf changed during creation.");
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        StableFileIdentity handleAfter = RequireOwnedExactRegularHandleIdentity(
            stream,
            expectedUserId,
            ExactDataLeafMode(),
            "new Core4 reward data leaf after write");
        StableFileIdentity pathAfter = RequireOwnedExactRegularPathIdentity(
            path,
            expectedUserId,
            ExactDataLeafMode(),
            "new Core4 reward data leaf after write");
        if (!handleBefore.SameFile(handleAfter) || !handleAfter.SameFile(pathAfter))
            throw new InvalidDataException("A new Core4 reward data leaf changed during durable write.");
    }

    private static async Task StageOwnedImageAsync(
        string sourcePath,
        string destinationPath,
        uint expectedUserId,
        Action<string>? testStageHook,
        CancellationToken cancellationToken)
    {
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        StableFileIdentity sourceBefore = RequireRegularSingleLinkHandleIdentity(
            source,
            "locked Core4 reward BIN copy source");
        await using FileStream destination = OpenNewExactFile(
            destinationPath,
            FileAccess.Write,
            FileShare.None,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.WriteThrough,
            ExactDataLeafMode());
        try
        {
            SetExactHandleMode(destination, ExactDataLeafMode(), "new staged Core4 reward BIN");
            StableFileIdentity destinationHandle = RequireOwnedExactRegularHandleIdentity(
                destination,
                expectedUserId,
                ExactDataLeafMode(),
                "new staged Core4 reward BIN");
            StableFileIdentity destinationPathIdentity = RequireOwnedExactRegularPathIdentity(
                destinationPath,
                expectedUserId,
                ExactDataLeafMode(),
                "new staged Core4 reward BIN");
            if (!destinationHandle.SameFile(destinationPathIdentity))
                throw new InvalidDataException("The new staged Core4 reward BIN changed during creation.");
            testStageHook?.Invoke("after-staged-image-created-exact-mode");
            await source.CopyToAsync(destination, 1024 * 1024, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            destination.Flush(flushToDisk: true);
            StableFileIdentity sourceAfter = RequireRegularSingleLinkHandleIdentity(
                source,
                "locked Core4 reward BIN copy source after copy");
            StableFileIdentity destinationAfter = RequireOwnedExactRegularHandleIdentity(
                destination,
                expectedUserId,
                ExactDataLeafMode(),
                "new staged Core4 reward BIN after copy");
            StableFileIdentity destinationPathAfter = RequireOwnedExactRegularPathIdentity(
                destinationPath,
                expectedUserId,
                ExactDataLeafMode(),
                "new staged Core4 reward BIN path after copy");
            if (sourceBefore != sourceAfter || !destinationHandle.SameFile(destinationAfter) ||
                !destinationAfter.SameFile(destinationPathAfter) ||
                destinationAfter.ByteLength != sourceAfter.ByteLength)
            {
                throw new InvalidDataException("The staged Core4 reward BIN copy changed identity or length.");
            }
        }
        catch
        {
            // A partial unique destination remains role-correct so the conservative rollback
            // verifier can identify and remove only this owned leaf, even under umask 000.
            StableFileIdentity handle = ReadHandleIdentity(destination, "partial staged Core4 reward BIN");
            StableFileIdentity path = ReadPathIdentity(destinationPath, "partial staged Core4 reward BIN");
            if (handle.SameFile(path) && handle.UserId == expectedUserId && handle.LinkCount == 1 &&
                (handle.Mode & 0xF000) == RegularFileType)
            {
                SetExactHandleMode(destination, ExactDataLeafMode(), "partial staged Core4 reward BIN");
            }
            throw;
        }
    }

    private static void WriteJournalAtomically(Publication publication, string path, string content)
    {
        string temporary = Path.Combine(Path.GetDirectoryName(path)!, JournalTemporaryFileName);
        if (File.Exists(temporary))
            throw new InvalidDataException("A Core4 reward journal temporary file already exists.");
        RequirePublicationControlDirectories(publication, "before journal write");
        StableFileIdentity? previousJournal = File.Exists(path)
            ? RequireOwnedExactRegularPathIdentity(
                path,
                publication.ExpectedUserId,
                ExactPrivateFileMode(),
                "existing Core4 reward journal before replacement")
            : null;
        byte[] bytes = new UTF8Encoding(false).GetBytes(content);
        StableFileIdentity? temporaryIdentity = null;
        try
        {
            using (FileStream stream = OpenNewExactFile(
                       temporary,
                       FileAccess.Write,
                       FileShare.None,
                       4096,
                       FileOptions.WriteThrough,
                       ExactPrivateFileMode()))
            {
                SetExactHandleMode(stream, ExactPrivateFileMode(), "new Core4 reward journal temporary");
                StableFileIdentity handleBefore = RequireOwnedExactRegularHandleIdentity(
                    stream,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "new Core4 reward journal temporary");
                StableFileIdentity pathBefore = RequireOwnedExactRegularPathIdentity(
                    temporary,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "new Core4 reward journal temporary");
                if (!handleBefore.SameFile(pathBefore))
                    throw new InvalidDataException("The new Core4 reward journal temporary changed during creation.");
                temporaryIdentity = handleBefore;
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
                StableFileIdentity handleAfter = RequireOwnedExactRegularHandleIdentity(
                    stream,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "new Core4 reward journal temporary after write");
                StableFileIdentity pathAfter = RequireOwnedExactRegularPathIdentity(
                    temporary,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "new Core4 reward journal temporary after write");
                if (!temporaryIdentity.SameFile(handleAfter) || !handleAfter.SameFile(pathAfter))
                    throw new InvalidDataException("The Core4 reward journal temporary changed during write.");
            }
            RequirePublicationControlDirectories(publication, "before journal rename");
            if (previousJournal is not null)
            {
                StableFileIdentity currentJournal = RequireOwnedExactRegularPathIdentity(
                    path,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "existing Core4 reward journal immediately before replacement");
                if (!previousJournal.SameFile(currentJournal))
                    throw new InvalidDataException("The existing Core4 reward journal changed before replacement.");
            }
            File.Move(temporary, path, overwrite: true);
            StableFileIdentity journalAfter = RequireOwnedExactRegularPathIdentity(
                path,
                publication.ExpectedUserId,
                ExactPrivateFileMode(),
                "Core4 reward journal after atomic replacement");
            if (temporaryIdentity is null || !temporaryIdentity.SameFile(journalAfter))
                throw new InvalidDataException("The Core4 reward journal rename did not preserve its exact vnode.");
            RequirePublicationControlDirectories(publication, "after journal rename");
        }
        finally
        {
            if (File.Exists(temporary))
            {
                StableFileIdentity remaining = RequireOwnedExactRegularPathIdentity(
                    temporary,
                    publication.ExpectedUserId,
                    ExactPrivateFileMode(),
                    "remaining Core4 reward journal temporary");
                if (temporaryIdentity is null || !temporaryIdentity.SameFile(remaining))
                    throw new InvalidDataException(
                        "A replacement Core4 reward journal temporary was preserved during cleanup.");
                File.Delete(temporary);
            }
        }
    }

    private static void VerifyCanonicalJsonReadback<T>(string path, T expected, string label)
    {
        int maximumBytes = typeof(T) == typeof(Id65AuthoringRewardStagingRuntimeCandidateReceipt)
            ? MaximumReceiptBytes
            : MaximumPlanBytes;
        byte[] bytes = ReadBoundedFileBytes(path, maximumBytes, label);
        byte[] canonical = StrictUtf8.GetBytes(JsonSerializer.Serialize(expected, JsonOptions) + "\n");
        if (!bytes.SequenceEqual(canonical))
            throw new InvalidDataException($"The {label} is not exact deterministic JSON.");
        _ = ReadCanonicalJson<T>(path, label);
    }

    private static T ReadCanonicalJson<T>(string path, string label)
    {
        int maximumBytes = typeof(T) == typeof(Id65AuthoringRewardStagingRuntimeCandidateReceipt)
            ? MaximumReceiptBytes
            : MaximumPlanBytes;
        byte[] bytes = ReadBoundedFileBytes(path, maximumBytes, label);
        if (HasUtf8Bom(bytes))
            throw new InvalidDataException($"The {label} must not contain a UTF-8 BOM.");
        string text = StrictUtf8.GetString(bytes);
        T value = JsonSerializer.Deserialize<T>(text, JsonOptions)
            ?? throw new InvalidDataException($"The {label} is empty.");
        byte[] canonical = StrictUtf8.GetBytes(JsonSerializer.Serialize(value, JsonOptions) + "\n");
        if (!bytes.SequenceEqual(canonical))
            throw new InvalidDataException($"The {label} is not exact deterministic JSON.");
        return value;
    }

    private static byte[] ReadBoundedFileBytes(string path, int maximumBytes, string label)
    {
        RejectReparsePoint(path, label);
        StableFileIdentity pathBefore = RequireRegularSingleLinkPathIdentity(path, label);
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        StableFileIdentity handleBefore = RequireRegularSingleLinkHandleIdentity(stream, label);
        if (!pathBefore.SameFile(handleBefore))
            throw new InvalidDataException($"The {label} changed between path and handle validation.");
        if (stream.Length < 0 || stream.Length > maximumBytes)
            throw new InvalidDataException($"The {label} has an invalid or oversized byte length.");
        byte[] bytes = new byte[checked((int)stream.Length)];
        stream.ReadExactly(bytes);
        if (stream.Position != stream.Length)
            throw new InvalidDataException($"The {label} bounded read did not consume its exact handle.");
        StableFileIdentity handleAfter = RequireRegularSingleLinkHandleIdentity(stream, label);
        StableFileIdentity pathAfter = RequireRegularSingleLinkPathIdentity(path, label);
        if (handleBefore != handleAfter || !handleAfter.SameFile(pathAfter) || handleAfter.ByteLength != bytes.Length)
            throw new InvalidDataException($"The {label} identity changed during bounded readback.");
        return bytes;
    }

    private static bool HasUtf8Bom(ReadOnlySpan<byte> bytes) =>
        bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;

    private static string HashDiscFile(
        FileStream image,
        DiscLayout layout,
        DiscFileRecord file,
        CancellationToken cancellationToken)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        const int chunkSize = 1024 * 1024;
        long offset = 0;
        while (offset < file.Size)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int length = checked((int)Math.Min(chunkSize, file.Size - offset));
            hash.AppendData(DiscImage.ReadFileBytes(image, layout, file.Lba, offset, length));
            offset += length;
        }
        return Hex(hash.GetHashAndReset());
    }

    private static async Task<FileIdentity> ReadFileIdentityAsync(
        string path,
        CancellationToken cancellationToken)
    {
        StableFileIdentity pathBefore = RequireRegularSingleLinkPathIdentity(path, "Core4 reward identity input");
        await using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        StableFileIdentity handleBefore = RequireRegularSingleLinkHandleIdentity(
            stream,
            "Core4 reward identity input");
        if (!pathBefore.SameFile(handleBefore))
            throw new InvalidDataException("A Core4 reward identity input changed during open.");
        long length = stream.Length;
        byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
        if (stream.Position != length)
            throw new InvalidDataException("A Core4 reward file identity read did not consume its exact open handle.");
        StableFileIdentity handleAfter = RequireRegularSingleLinkHandleIdentity(
            stream,
            "Core4 reward identity input");
        StableFileIdentity pathAfter = RequireRegularSingleLinkPathIdentity(path, "Core4 reward identity input");
        if (handleBefore != handleAfter || !handleAfter.SameFile(pathAfter) || handleAfter.ByteLength != length)
            throw new InvalidDataException("A Core4 reward identity input changed during readback.");
        return new(length, Hex(hash));
    }

    private static string HashFile(string path)
    {
        StableFileIdentity pathBefore = RequireRegularSingleLinkPathIdentity(path, "Core4 reward hash input");
        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        StableFileIdentity handleBefore = RequireRegularSingleLinkHandleIdentity(stream, "Core4 reward hash input");
        if (!pathBefore.SameFile(handleBefore))
            throw new InvalidDataException("A Core4 reward hash input changed between path and handle validation.");
        long length = stream.Length;
        string hash = Hex(SHA256.HashData(stream));
        if (stream.Position != length)
            throw new InvalidDataException("A Core4 reward hash read did not consume its exact open handle.");
        StableFileIdentity handleAfter = RequireRegularSingleLinkHandleIdentity(stream, "Core4 reward hash input");
        StableFileIdentity pathAfter = RequireRegularSingleLinkPathIdentity(path, "Core4 reward hash input");
        if (handleBefore != handleAfter || !handleAfter.SameFile(pathAfter) || handleAfter.ByteLength != length)
            throw new InvalidDataException("A Core4 reward hash input identity changed during readback.");
        return hash;
    }

    private static DiscFileRecord RequireRootFile(
        FileStream image,
        DiscLayout layout,
        string name,
        int expectedLba,
        int expectedSize)
    {
        DiscFileRecord record = DiscImage.FindRootFileRecord(
            image,
            layout,
            candidate => candidate.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (record.Lba != expectedLba || record.Size != expectedSize)
            throw new InvalidDataException($"The exact {name} ISO extent changed.");
        return record;
    }

    private static void RequireMode2Layout(DiscLayout layout, string label)
    {
        if (layout.SectorSize != RawSectorByteLength || layout.UserOffset != UserDataOffset)
            throw new InvalidDataException($"The {label} is not exact MODE2/2352 with user offset 24.");
    }

    private static string RequireExistingDirectory(string path, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"The {label} must be an absolute path.");
        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"The {label} is missing: {full}");
        RejectExistingAncestorReparsePoints(full, label);
        _ = RequireOwnedTrustedDirectoryPathIdentity(
            full,
            RequireUnprivilegedCurrentIdentity(),
            label);
        return full;
    }

    private static string GuardWorkspacePath(string workspaceRoot, string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\\') ||
            relativePath.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidOperationException("A frozen Core4 reward workspace path is not exact POSIX-relative.");
        }
        string full = Path.GetFullPath(Path.Combine(workspaceRoot, relativePath));
        EnsureStrictDescendant(full, workspaceRoot, "frozen Core4 reward workspace source");
        RejectExistingAncestorReparsePoints(full, "frozen Core4 reward workspace source");
        return full;
    }

    private static string RequireExactRequestedFile(string requested, string expected, string label)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requested);
        if (!Path.IsPathFullyQualified(requested))
            throw new InvalidDataException($"The {label} path must be absolute.");
        string full = Path.GetFullPath(requested);
        if (!PathEquals(full, expected))
            throw new InvalidDataException($"The {label} path is not the one frozen workspace source.");
        if (!File.Exists(full))
            throw new FileNotFoundException($"The {label} is missing.", full);
        RejectExistingAncestorReparsePoints(full, label);
        if ((File.GetAttributes(full) & FileAttributes.Directory) != 0)
            throw new InvalidDataException($"The {label} is not a regular file.");
        _ = RequireRegularSingleLinkPathIdentity(full, label);
        return full;
    }

    private static void RejectTreeReparsePoints(string root, string label)
    {
        RejectReparsePoint(root, label);
        Stack<string> pending = new();
        pending.Push(root);
        while (pending.Count > 0)
        {
            string directory = pending.Pop();
            foreach (string entry in Directory.EnumerateFileSystemEntries(directory))
            {
                RejectReparsePoint(entry, label);
                if (Directory.Exists(entry))
                    pending.Push(entry);
            }
        }
    }

    private static void RejectExistingAncestorReparsePoints(string path, string label)
    {
        string? current = Path.GetFullPath(path);
        while (current is not null)
        {
            RejectReparsePoint(current, label);
            current = Path.GetDirectoryName(current);
        }
    }

    private static StableFileIdentity PreparePublicationParent(
        Id65AuthoringRewardStagingRuntimeCandidatePaths paths,
        uint expectedUserId)
    {
        string parent = Path.GetDirectoryName(paths.OutputDirectoryPath) ??
            throw new InvalidOperationException("The Core4 reward publication has no parent directory.");
        bool parentExists = Directory.Exists(parent);
        if (!parentExists && File.Exists(parent))
            throw new InvalidDataException("The Core4 reward publication parent is a file.");
        ValidateTrustedPublicationAncestry(parent, expectedUserId, allowMissingFinal: !parentExists);
        StableFileIdentity identity;
        if (!parentExists)
        {
            identity = CreateOwnedExactDirectory(
                parent,
                expectedUserId,
                ExactCandidateDirectoryMode(),
                "new Core4 reward publication parent");
        }
        else
        {
            identity = RequireOwnedTrustedDirectoryPathIdentity(
                parent,
                expectedUserId,
                "Core4 reward publication parent");
            ushort permissions = (ushort)(identity.Mode & PermissionMask);
            ushort mode0700 = checked((ushort)ExactPrivateDirectoryMode());
            ushort mode0750 = checked((ushort)(
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                UnixFileMode.GroupRead | UnixFileMode.GroupExecute));
            ushort mode0755 = checked((ushort)ExactCandidateDirectoryMode());
            if (permissions != mode0700 && permissions != mode0750 && permissions != mode0755)
            {
                throw new InvalidDataException(
                    "The existing Core4 reward publication parent must be exact 0700, 0750, or 0755.");
            }
        }
        ValidateTrustedPublicationAncestry(parent, expectedUserId, allowMissingFinal: false);
        StableFileIdentity after = RequireOwnedTrustedDirectoryPathIdentity(
            parent,
            expectedUserId,
            "Core4 reward publication parent after preparation");
        if (!identity.SameFile(after) || identity.Mode != after.Mode || identity.UserId != after.UserId)
            throw new InvalidDataException("The Core4 reward publication parent changed during preparation.");
        return after;
    }

    private static void ValidateTrustedPublicationAncestry(
        string targetDirectory,
        uint expectedUserId,
        bool allowMissingFinal)
    {
        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(targetDirectory));
        string root = Path.GetPathRoot(full) ??
            throw new InvalidOperationException("The Core4 reward publication parent has no filesystem root.");
        List<string> paths = [Path.TrimEndingDirectorySeparator(root)];
        string relative = Path.GetRelativePath(root, full);
        if (relative != ".")
        {
            string cursor = Path.TrimEndingDirectorySeparator(root);
            foreach (string component in relative.Split(Path.DirectorySeparatorChar))
            {
                cursor = Path.Combine(cursor, component);
                paths.Add(cursor);
            }
        }

        List<(string Path, StableFileIdentity Identity)> existing = [];
        for (int index = 0; index < paths.Count; index++)
        {
            string path = paths[index];
            if (!Directory.Exists(path))
            {
                if (allowMissingFinal && index == paths.Count - 1 &&
                    index > 0 && Directory.Exists(paths[index - 1]))
                {
                    break;
                }
                throw new DirectoryNotFoundException(
                    $"The Core4 reward publication ancestry has an unsupported missing segment: {path}");
            }
            RejectReparsePoint(path, "Core4 reward publication ancestry");
            StableFileIdentity identity = ReadPathIdentity(path, "Core4 reward publication ancestry");
            if ((identity.Mode & 0xF000) != DirectoryFileType)
                throw new InvalidDataException("The Core4 reward publication ancestry contains a non-directory.");
            existing.Add((path, identity));
        }

        for (int index = 0; index < existing.Count; index++)
        {
            StableFileIdentity identity = existing[index].Identity;
            if (identity.UserId != 0 && identity.UserId != expectedUserId)
            {
                throw new InvalidDataException(
                    $"The Core4 reward publication ancestry has an untrusted owner: {existing[index].Path}");
            }
            if ((identity.Mode & GroupOrOtherWriteMask) == 0)
                continue;
            bool allowedStickyRootSegment = identity.UserId == 0 &&
                (identity.Mode & StickyBit) != 0 &&
                index + 1 < existing.Count &&
                existing[index + 1].Identity.UserId == expectedUserId &&
                (existing[index + 1].Identity.Mode & GroupOrOtherWriteMask) == 0;
            if (!allowedStickyRootSegment)
            {
                throw new InvalidDataException(
                    $"The Core4 reward publication ancestry is group- or other-writable: {existing[index].Path}");
            }
        }
    }

    private static StableFileIdentity CreateOwnedExactDirectory(
        string path,
        uint expectedUserId,
        UnixFileMode mode,
        string label)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Core4 reward exact directory modes require macOS.");
        string parent = Path.GetDirectoryName(path) ??
            throw new InvalidOperationException($"The {label} has no parent.");
        if (!Directory.Exists(parent) || Directory.Exists(path) || File.Exists(path))
            throw new InvalidDataException($"The {label} is not one absent direct child of an existing directory.");
        if (NativeMkdir(path, checked((ushort)mode)) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not create the {label} (errno {error}).", new Win32Exception(error));
        }
        File.SetUnixFileMode(path, mode);
        return RequireOwnedExactDirectoryPathIdentity(path, expectedUserId, mode, label);
    }

    private static void SetExactHandleMode(FileStream stream, UnixFileMode mode, string label)
    {
        int descriptor = checked((int)stream.SafeFileHandle.DangerousGetHandle());
        if (NativeFChmod(descriptor, checked((ushort)mode)) != 0)
        {
            int error = Marshal.GetLastPInvokeError();
            throw new IOException($"Could not set the exact mode for the {label} (errno {error}).", new Win32Exception(error));
        }
    }

    private static StableFileIdentity NormalizeNewOwnedRegularPath(
        string path,
        uint expectedUserId,
        UnixFileMode mode,
        string label)
    {
        if (!OperatingSystem.IsMacOS())
            throw new PlatformNotSupportedException("Core4 reward exact file modes require macOS.");
        StableFileIdentity before = ReadPathIdentity(path, label);
        if ((before.Mode & 0xF000) != RegularFileType || before.LinkCount != 1)
            throw new InvalidDataException($"The {label} must be one newly created regular file.");
        RequireCurrentOwnedIdentity(before, expectedUserId, label);
        File.SetUnixFileMode(path, mode);
        StableFileIdentity after = RequireOwnedExactRegularPathIdentity(path, expectedUserId, mode, label);
        if (!before.SameFile(after))
            throw new InvalidDataException($"The {label} changed while its new-object mode was normalized.");
        return after;
    }

    private static StableFileIdentity RequirePinnedExactDirectory(
        string path,
        StableFileIdentity expected,
        uint expectedUserId,
        UnixFileMode mode,
        string label)
    {
        ValidateTrustedPublicationAncestry(path, expectedUserId, allowMissingFinal: false);
        StableFileIdentity actual = RequireOwnedExactDirectoryPathIdentity(path, expectedUserId, mode, label);
        if (!expected.SameFile(actual))
            throw new InvalidDataException($"The {label} directory vnode changed.");
        return actual;
    }

    private static StableFileIdentity RequirePinnedTrustedDirectory(
        string path,
        StableFileIdentity expected,
        uint expectedUserId,
        string label)
    {
        ValidateTrustedPublicationAncestry(path, expectedUserId, allowMissingFinal: false);
        StableFileIdentity actual = RequireOwnedTrustedDirectoryPathIdentity(path, expectedUserId, label);
        if (!expected.SameFile(actual) || expected.Mode != actual.Mode)
            throw new InvalidDataException($"The {label} directory identity changed.");
        return actual;
    }

    private static void RejectReparsePoint(string path, string label)
    {
        try
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException($"The {label} cannot be a symbolic link or reparse point.");
        }
        catch (FileNotFoundException)
        {
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private static void EnsureStrictDescendant(string candidate, string parent, string label)
    {
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        if (!fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, PathComparison))
            throw new InvalidOperationException($"The {label} escaped its exact owned parent.");
    }

    private static bool IsDescendantOrEqual(string candidate, string parent)
    {
        string fullCandidate = Path.TrimEndingDirectorySeparator(Path.GetFullPath(candidate));
        string fullParent = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent));
        return string.Equals(fullCandidate, fullParent, PathComparison) ||
            fullCandidate.StartsWith(fullParent + Path.DirectorySeparatorChar, PathComparison);
    }

    private static bool PathEquals(string? left, string? right) =>
        left is not null && right is not null &&
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(left)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(right)),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private static string StagedPath(string stagedDirectory, string finalPath) =>
        Path.Combine(stagedDirectory, Path.GetFileName(finalPath));

    private static UnixFileMode ExactFinderMode() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static UnixFileMode ExactDataLeafMode() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite |
        UnixFileMode.GroupRead | UnixFileMode.OtherRead;

    private static UnixFileMode ExactCandidateDirectoryMode() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    private static UnixFileMode ExactPrivateDirectoryMode() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;

    private static UnixFileMode ExactPrivateFileMode() =>
        UnixFileMode.UserRead | UnixFileMode.UserWrite;

    private static FileStream OpenNewExactFile(
        string path,
        FileAccess access,
        FileShare share,
        int bufferSize,
        FileOptions options,
        UnixFileMode createMode)
    {
        if (OperatingSystem.IsWindows())
            throw new PlatformNotSupportedException("Exact Unix creation modes require a Unix host.");
        return new FileStream(
            path,
            new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = access,
                Share = share,
                BufferSize = bufferSize,
                Options = options,
                UnixCreateMode = createMode
            });
    }

    private static string ShellSingleQuote(string value) => "'" + value.Replace("'", "'\\''") + "'";

    private static string EscapeXml(string value) => value
        .Replace("&", "&amp;", StringComparison.Ordinal)
        .Replace("<", "&lt;", StringComparison.Ordinal)
        .Replace(">", "&gt;", StringComparison.Ordinal)
        .Replace("\"", "&quot;", StringComparison.Ordinal)
        .Replace("'", "&apos;", StringComparison.Ordinal);

    private static void RequireHash(string actual, string expected, string label)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"The {label} SHA-256 is {actual}, expected {expected}.");
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 } && value.All(character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));

    private static string Hash(ReadOnlySpan<byte> bytes) => Hex(SHA256.HashData(bytes));

    private static string HashUtf8(string value) => Hash(Encoding.UTF8.GetBytes(value));

    private static string Hex(ReadOnlySpan<byte> value) => Convert.ToHexString(value).ToLowerInvariant();

    private sealed record FileIdentity(long ByteLength, string Sha256);

    private sealed record StableFileIdentity(
        int Device,
        ulong Inode,
        uint UserId,
        ushort Mode,
        ushort LinkCount,
        long ByteLength,
        long ModifiedSeconds,
        long ModifiedNanoseconds,
        long ChangedSeconds,
        long ChangedNanoseconds)
    {
        internal bool SameFile(StableFileIdentity other) =>
            Device == other.Device && Inode == other.Inode;
    }

    private sealed record CandidateTreeFingerprint(
        string FileName,
        string Sha256,
        StableFileIdentity Identity,
        StableDirectoryIdentity DirectoryIdentity);

    private sealed record StableDirectoryIdentity(
        int Device,
        ulong Inode,
        uint UserId,
        ushort Mode);

    private sealed record LogicalDiffRun(
        int DataRelativeOffset,
        int ByteLength,
        string BeforeSha256,
        string AfterSha256);

    private sealed record Preflight(
        uint ExpectedUserId,
        string WorkspaceRoot,
        string BaseImagePath,
        string BaseCuePath,
        string FoundationImagePath,
        Id65CompiledRewardStaging Compiled,
        LogicalDiffRun[] LogicalRuns,
        int[] AffectedRawSectorLbas,
        Id65AuthoringRewardStagingRawSectorRun[] AffectedRawSectorRuns,
        long BaseCueByteLength,
        long FoundationByteLength);

    private sealed record CandidateReadback(
        string OutputImageSha256,
        string SourceWadSha256,
        string OutputWadSha256,
        string SourceRow80Sha256,
        string OutputRow80Sha256,
        string TargetOverlaySha256,
        string RetailTownSquareOverlaySha256,
        string GlobalExecutableSha256,
        string RetailTownSquareRowSha256,
        int ChangedLogicalBytes,
        int LogicalDiffRangeCount,
        int RebuiltRawSectorCount,
        int VerifiedRawSectorCount,
        long ChangedPhysicalBytes,
        IReadOnlyList<Id65AuthoringRewardStagingRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256,
        string OutsideComplementSha256,
        string AffectedBeforeSha256,
        string AffectedAfterSha256,
        string AffectedPayloadBeforeSha256,
        string AffectedPayloadAfterSha256,
        Id65AuthoringRewardStagingPhysicalAggregate Aggregate);

    private sealed record PhysicalReadback(
        string OutputImageSha256,
        long ChangedPhysicalBytes,
        IReadOnlyList<Id65AuthoringRewardStagingRawSectorDiff> RawSectorDiffs,
        string RawSectorDiffSha256,
        string OutsideComplementSha256,
        string AffectedBeforeSha256,
        string AffectedAfterSha256,
        string AffectedPayloadBeforeSha256,
        string AffectedPayloadAfterSha256,
        Id65AuthoringRewardStagingPhysicalAggregate Aggregate);

    private sealed record StagedCandidate(
        Id65AuthoringRewardStagingRuntimeCandidatePlan Plan,
        Id65AuthoringRewardStagingRuntimeCandidateReceipt Receipt,
        CandidateReadback Readback,
        RuntimeCandidateFinderReveal StagedFinderReveal);

    private sealed record CandidateOperationJournal(
        int SchemaVersion,
        string OperationKind,
        string Phase,
        string OperationRootPath,
        string OutputDirectoryPath,
        string StagedDirectoryPath,
        string BackupDirectoryPath,
        bool PreviousOutputExisted,
        string? StagedCandidateFingerprintSha256,
        string? PreviousCandidateFingerprintSha256);

    private sealed class Publication(
        string operationRootPath,
        string stagedDirectoryPath,
        string backupDirectoryPath,
        string outputDirectoryPath,
        string operationLeasePath,
        bool previousOutputExisted,
        uint expectedUserId,
        StableFileIdentity publicationParentIdentity,
        StableFileIdentity operationsDirectoryIdentity,
        StableFileIdentity operationRootIdentity)
    {
        public string OperationRootPath { get; } = operationRootPath;
        public string StagedDirectoryPath { get; } = stagedDirectoryPath;
        public string BackupDirectoryPath { get; } = backupDirectoryPath;
        public string OutputDirectoryPath { get; } = outputDirectoryPath;
        public string OperationLeasePath { get; } = operationLeasePath;
        public bool PreviousOutputExisted { get; } = previousOutputExisted;
        public uint ExpectedUserId { get; } = expectedUserId;
        public StableFileIdentity PublicationParentIdentity { get; } = publicationParentIdentity;
        public StableFileIdentity OperationsDirectoryIdentity { get; } = operationsDirectoryIdentity;
        public StableFileIdentity OperationRootIdentity { get; } = operationRootIdentity;
        public string Phase { get; set; } = PhaseCreated;
        public StableFileIdentity? OperationLeaseIdentity { get; set; }
        public StableFileIdentity? StagedDirectoryIdentity { get; set; }
        public StableDirectoryIdentity? PreviousDirectoryIdentity { get; set; }
        public CandidateTreeFingerprint[]? StagedCandidateFingerprint { get; set; }
        public CandidateTreeFingerprint[]? PreviousCandidateFingerprint { get; set; }
        public string? StagedCandidateFingerprintSha256 { get; set; }
        public string? PreviousCandidateFingerprintSha256 { get; set; }
    }

    private sealed class GlobalWriterLease(
        FileStream stream,
        string leasePath,
        StableFileIdentity identity,
        string publicationParentPath,
        StableFileIdentity publicationParentIdentity,
        uint expectedUserId) : IDisposable
    {
        private FileStream? _stream = stream;

        public void RequireCurrentIdentity(string boundary)
        {
            FileStream live = _stream ??
                throw new ObjectDisposedException(nameof(GlobalWriterLease));
            RequirePinnedTrustedDirectory(
                publicationParentPath,
                publicationParentIdentity,
                expectedUserId,
                $"Core4 reward publication parent {boundary}");
            StableFileIdentity handle = RequireOwnedExactRegularHandleIdentity(
                live,
                expectedUserId,
                ExactPrivateFileMode(),
                $"Core4 reward global writer lease {boundary}");
            StableFileIdentity path = RequireOwnedExactRegularPathIdentity(
                leasePath,
                expectedUserId,
                ExactPrivateFileMode(),
                $"Core4 reward global writer lease {boundary}");
            if (handle != identity || path != identity)
            {
                throw new InvalidDataException(
                    $"The Core4 reward global writer lease identity changed {boundary}.");
            }
        }

        public void Dispose()
        {
            FileStream? owned = Interlocked.Exchange(ref _stream, null);
            if (owned is null)
                return;
            bool prevalidationSucceeded = false;
            try
            {
                StableFileIdentity handleBefore = RequireOwnedExactRegularHandleIdentity(
                    owned,
                    expectedUserId,
                    ExactPrivateFileMode(),
                    "Core4 reward global writer lease before disposal");
                StableFileIdentity pathBefore = RequireOwnedExactRegularPathIdentity(
                    leasePath,
                    expectedUserId,
                    ExactPrivateFileMode(),
                    "Core4 reward global writer lease before disposal");
                if (handleBefore != identity || pathBefore != identity)
                    throw new InvalidDataException("The Core4 reward global writer lease changed before disposal.");
                prevalidationSucceeded = true;
                try
                {
                    if (OperatingSystem.IsWindows())
                        owned.Unlock(0, 1);
                    else
                        _ = NativeFlock(checked((int)owned.SafeFileHandle.DangerousGetHandle()), LockUnlock);
                }
                catch (IOException)
                {
                    // Closing the descriptor below releases either platform lease.
                }
            }
            finally
            {
                owned.Dispose();
            }
            if (prevalidationSucceeded)
            {
                RequirePinnedTrustedDirectory(
                    publicationParentPath,
                    publicationParentIdentity,
                    expectedUserId,
                    "Core4 reward publication parent after global lease disposal");
                StableFileIdentity pathAfter = RequireOwnedExactRegularPathIdentity(
                    leasePath,
                    expectedUserId,
                    ExactPrivateFileMode(),
                    "Core4 reward global writer lease after disposal");
                if (pathAfter != identity)
                    throw new InvalidDataException("The Core4 reward global writer lease changed during disposal.");
            }
        }
    }
}
