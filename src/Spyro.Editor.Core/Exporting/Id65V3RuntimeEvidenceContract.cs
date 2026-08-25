using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65V3RuntimeEvidenceState
{
    PendingNoRun,
    PendingIncomplete,
    RuntimeRejected,
    Invalid,
    FocusedRuntimePass
}

internal enum Id65V3RuntimeEvidenceCheckStatus
{
    NotObserved,
    Passed,
    Failed,
    Inconclusive
}

internal enum Id65V3RuntimeEvidenceValidationMode
{
    StaticOnly,
    FullReadback
}

internal sealed record Id65V3RuntimeEvidenceReadbackContext(
    string SourceRepositoryRootPath,
    string ProfilePackageRootPath,
    string DuckStationExecutablePath,
    string HandoffReceiptPath,
    string ExternalHomeRootPath,
    string ExternalHomeDataRootPath,
    string ExternalHomeSettingsPath,
    string ExternalHomeBiosPath,
    string HostNormalSettingsPath);

internal sealed record Id65V3RuntimeEvidenceSourceIdentity(
    string ProfileId,
    string BaseBinSha256,
    string BaseCueSha256,
    string CandidateBinRelativePath,
    string CandidateBinSha256,
    string CandidateCueRelativePath,
    string CandidateCueSha256,
    string LockedBaseRow80DataSha256,
    string LockedBaseModelSha256,
    string EnvironmentHeaderRepairV3BeforeRow80DataSha256,
    string EnvironmentHeaderRepairV3AfterRow80DataSha256,
    string EnvironmentHeaderRepairV3BeforeModelSha256,
    string EnvironmentHeaderRepairV3AfterModelSha256,
    string OutputCollisionTreeSha256,
    string OutputCollisionBlocksSha256,
    string StaticDiffManifestSha256,
    string DeterministicStaticPlanSha256,
    string RawSectorDiffSha256,
    string ConstructionPlanRelativePath,
    string ConstructionPlanSha256,
    string StaticReadbackReceiptRelativePath,
    string StaticReadbackReceiptSha256,
    string RuntimeChecklistRelativePath,
    string RuntimeChecklistSha256,
    string LocationGuideRelativePath,
    string LocationGuideSha256,
    string RevealCommandRelativePath,
    string RevealCommandSha256,
    string WatcherRelativePath,
    string WatcherSha256,
    string WatcherMode,
    string ProfileBuilderRelativePath,
    string ProfileBuilderSha256,
    string ProfileBuilderMode,
    string ProfileVerifierRelativePath,
    string ProfileVerifierSha256,
    string ProfileVerifierMode,
    string AutomationRelativePath,
    string AutomationSha256,
    string AutomationMode,
    string CaptureCoordinatorRelativePath,
    string CaptureCoordinatorSha256,
    string CaptureCoordinatorMode,
    string RuntimeProfileManifestDomain,
    string RuntimeProfileSha256,
    string SourceClosureSha256,
    string FailedV1ProfileId,
    string FailedV1BinRelativePath,
    string FailedV1BinSha256,
    string FailedV1OutputDataSha256,
    string FailedV1OutputModelSha256,
    string CollisionWindingV2ProfileId,
    string CollisionWindingV2BinRelativePath,
    string CollisionWindingV2BinSha256,
    string CollisionWindingV2OutputDataSha256,
    string CollisionWindingV2OutputModelSha256,
    string CollisionWindingV2RepairDiffManifestSha256,
    int CollisionWindingV2ChangedByteCount,
    int CollisionWindingV2RangeCount,
    int CollisionWindingV2RawSectorLba,
    bool CollisionWindingV2InverseVerified,
    bool FailedV1RetestForbidden,
    string SupersededV2SectorSha256,
    string EnvironmentHeaderRepairV3AfterSectorSha256,
    string EnvironmentHeaderRepairV3AfterEnvironmentSha256,
    string EnvironmentHeaderRepairV3DiffManifestSha256,
    int EnvironmentHeaderRepairV3ChangedByteCount,
    int EnvironmentHeaderRepairV3RangeCount,
    int EnvironmentHeaderRepairV3RawSectorLba,
    int EnvironmentHeaderRepairV3BeforeByte,
    int EnvironmentHeaderRepairV3AfterByte,
    bool EnvironmentHeaderRepairV3InverseVerified,
    bool SupersededV2RetestForbidden);

internal sealed record Id65V3RuntimeEvidenceProfile(
    string ProfileId,
    string PackageRelativePath,
    string AppRelativePath,
    string DuckStationExecutableRelativePath,
    string ExternalHomeRelativePath,
    string ExternalHomeDataRelativePath,
    string HandoffReceiptRelativePath,
    string DuckStationVersion,
    long DuckStationExecutableByteLength,
    string DuckStationExecutableSha256,
    string HandoffReceiptFormat,
    int HandoffReceiptFormatVersion,
    long HandoffReceiptByteLength,
    string HandoffReceiptSha256,
    string HandoffReceiptClosureSha256,
    string AppContentManifestFormat,
    int AppContentManifestEntryCount,
    string AppContentManifestSha256,
    string OutputTreeManifestFormat,
    int AppOutputTreeManifestEntryCount,
    string AppOutputTreeManifestSha256,
    int HomeOutputTreeManifestEntryCount,
    string HomeOutputTreeManifestSha256,
    string PackageEnvelopeFormat,
    int PackageEnvelopeEntryCount,
    string PackageEnvelopeSha256,
    string OutputXattrPolicyVersion,
    string ExternalHomeSettingsRelativePath,
    long ExternalHomeSettingsByteLength,
    string ExternalHomeSettingsSha256,
    string ExternalHomeBiosRelativePath,
    long ExternalHomeBiosByteLength,
    string ExternalHomeBiosSha256,
    string HostNormalSettingsSentinelId,
    long HostNormalSettingsByteLength,
    string HostNormalSettingsSha256,
    string HostNormalSettingsMode,
    string[] SafetyDirectories,
    string AllowedVolatilePolicy,
    string TrustModel,
    string LaunchGate,
    string PowerOffBinding,
    string TogglePauseBinding,
    int EmulationSpeed,
    bool LoadDevicesFromSaveStates,
    bool RewindEnabled,
    int RunaheadFrameCount,
    bool SaveStateOnExit,
    bool StartPaused,
    bool FastForwardBoot,
    bool PatchFastBoot,
    bool LoadImagePatches,
    bool LoadImageToRam,
    int CdReadSpeedup,
    int CdSeekSpeedup,
    string Card1Type,
    string Card2Type,
    string MultitapMode,
    string Pad1Type,
    string[] OtherPadTypes,
    string PioDeviceType,
    bool PioFlashWriteEnabled,
    bool PcDriveEnabled,
    bool PcDriveWritesEnabled,
    bool GdbServerEnabled,
    int GdbServerPort,
    int ExpectedCheatFileCount,
    int ExpectedGameSettingsFileCount,
    int ExpectedMemoryCardFileCount,
    int ExpectedSaveStateFileCount,
    int ExpectedPatchFileCount,
    bool SaveLoadStateHotkeysPresent,
    bool TextureReplacementOrDumpEnabled,
    string ProjectionSha256,
    string? SettingsBeforeSha256,
    string? SettingsAfterSha256,
    string? NormalUserSettingsBeforeSha256,
    string? NormalUserSettingsAfterSha256,
    int? CheatFileCountBefore,
    int? CheatFileCountAfter,
    int? GameSettingsFileCountBefore,
    int? GameSettingsFileCountAfter,
    int? MemoryCardFileCountBefore,
    int? MemoryCardFileCountAfter,
    int? SaveStateFileCountBefore,
    int? SaveStateFileCountAfter,
    int? PatchFileCountBefore,
    int? PatchFileCountAfter,
    string[]? ChangedProfilePaths,
    bool? SaveStateLoaded,
    bool? SaveStateCreated,
    bool? InGameSaveUsed,
    int? ActiveCheatCount);

internal sealed record Id65V3RuntimeEvidenceArtifact(
    string ArtifactId,
    string SessionId,
    string Kind,
    string RepoRelativePath,
    string Sha256,
    long ByteLength,
    string CapturedAtUtc,
    string? MediaStartAtUtc,
    string? MediaEndAtUtc);

internal sealed record Id65V3RuntimeProfileSnapshotEntry(
    string RelativePath,
    long ByteLength,
    string Sha256);

internal sealed record Id65V3RuntimeProfileSnapshot(
    string Format,
    int FormatVersion,
    string SnapshotId,
    string Phase,
    string SessionId,
    string ProcessInstanceId,
    string ProfileId,
    string CapturedAtUtc,
    string DuckStationExecutableSha256,
    string HandoffReceiptSha256,
    string AppContentManifestSha256,
    string AppOutputTreeManifestSha256,
    string PackageEnvelopeSha256,
    string ExternalHomeSettingsSha256,
    string ExternalHomeBiosSha256,
    string HostNormalSettingsSentinelId,
    string HostNormalSettingsSentinelSha256,
    Id65V3RuntimeProfileSnapshotEntry[] DataFiles);

internal sealed record Id65V3RuntimeCaptureJournalReference(
    string ArtifactId,
    string Format,
    int FormatVersion,
    string RunId,
    int SessionOrdinal,
    string SessionId,
    string BootIdentity,
    string ProcessInstanceId,
    string TerminalEventSha256);

internal sealed record Id65V3RuntimeEvidenceSlice(
    string ArtifactId,
    long? StartMilliseconds,
    long? EndMilliseconds,
    string? FrameSha256);

internal sealed record Id65V3RuntimeEvidenceCheck(
    string CheckId,
    string SessionId,
    Id65V3RuntimeEvidenceCheckStatus Status,
    string? ObservedAtUtc,
    string? Attestation,
    Id65V3RuntimeEvidenceSlice[] ArtifactRefs,
    bool TelemetryCorroborated);

internal sealed record Id65V3TelemetryTuple(
    string AtUtc,
    int SampleOrdinal,
    long ElapsedMilliseconds,
    int Lives,
    int DeathState,
    int PlayerZ,
    string LivesRaw,
    string DeathStateRaw,
    string PlayerZRaw);

internal sealed record Id65V3StableHoldSummary(
    string StartAtUtc,
    string EndAtUtc,
    int StartSampleOrdinal,
    int EndSampleOrdinal,
    long StartElapsedMilliseconds,
    long EndElapsedMilliseconds,
    long DurationMilliseconds,
    int SampleCount,
    long MaximumGapMilliseconds,
    bool AllLivesFour,
    bool AllStateZero,
    bool AllZAtLeast1024);

internal sealed record Id65V3DeathEpisodeSummary(
    Id65V3TelemetryTuple? PreFallSample,
    Id65V3TelemetryTuple? FirstZBelow1024,
    Id65V3TelemetryTuple? FirstLivesThree,
    Id65V3TelemetryTuple? FirstStateFour,
    Id65V3TelemetryTuple? LastStateFour,
    Id65V3TelemetryTuple? FirstPostRespawnStable,
    int LivesTransitionCount,
    bool SecondDecrementObserved);

internal sealed record Id65V3TelemetrySummary(
    string WatcherSha256,
    int PeriodMilliseconds,
    bool NearContemporaneousNotAtomic,
    int TotalSamples,
    int ParseErrors,
    string? FirstAtUtc,
    string? LastAtUtc,
    long MaximumGapMilliseconds,
    Id65V3TelemetryTuple? FirstTuple,
    Id65V3TelemetryTuple? LastTuple,
    int? MinimumZ,
    int? MaximumZ,
    int[] LivesValues,
    string[] LivesTransitions,
    int[] StateValues,
    int StateFourSamples,
    int StateFiveSamples,
    int BelowPlaneSamples,
    bool CleanStopObserved,
    Id65V3StableHoldSummary? StableHold,
    Id65V3DeathEpisodeSummary? DeathEpisode);

internal sealed record Id65V3RuntimeEvidenceSession(
    string SessionId,
    string ProcessInstanceId,
    int TargetLevelId,
    string LoadCode,
    bool ColdBoot,
    bool NewGame,
    bool CardsNone,
    bool CheatsOff,
    bool SaveStateLoaded,
    bool SaveStateCreated,
    bool InGameSaveUsed,
    string? ProcessStartedAtUtc,
    string? WatcherConnectedAtUtc,
    string? ManuallyResumedAtUtc,
    string? SelectorStartedAtUtc,
    string? LandingAtUtc,
    string? HoldStartedAtUtc,
    string? HoldEndedAtUtc,
    string? FallStartedAtUtc,
    string? RespawnAtUtc,
    string? ProcessExitedAtUtc,
    int? LandingSampleOrdinal,
    int? HoldStartSampleOrdinal,
    int? HoldEndSampleOrdinal,
    int? FallStartSampleOrdinal,
    int? RespawnSampleOrdinal,
    string? WatcherArtifactId,
    Id65V3RuntimeCaptureJournalReference? CaptureJournal,
    string? ProfileBeforeArtifactId,
    string? ProfileAfterArtifactId,
    Id65V3TelemetrySummary? SampleSummary);

internal sealed record Id65V3RuntimeEvidenceColdReset(
    Id65V3RuntimeEvidenceCheckStatus Status,
    string? PrimaryProcessExitedAtUtc,
    string? ResetProcessStartedAtUtc,
    string ColdResetSessionId,
    string SamePadYawCheckId);

internal sealed record Id65V3RuntimeEvidenceRetailControl(
    string ControlId,
    int LevelId,
    string LoadCode,
    Id65V3RuntimeEvidenceCheckStatus Status,
    string[] CheckIds);

internal sealed record Id65V3RuntimeEvidenceDerivedResult(
    Id65V3RuntimeEvidenceState State,
    bool SourceValid,
    bool PinnedRuntimeFilesReadbackValid,
    bool RuntimeProfileValid,
    bool ArtifactReadbackValid,
    bool DiscIdentityValid,
    bool TelemetryValid,
    bool VisualChecklistComplete,
    bool ColdResetValid,
    bool RetailControlsValid,
    bool EnvironmentHeaderRepairV3RuntimeAccepted,
    bool RuntimeEvidenceAccepted,
    bool FarLowDetailAccepted,
    bool SaveRuntimeAccepted,
    bool Core4RewardRuntimeAccepted,
    bool Total200RuntimeAccepted,
    bool PublisherAuthorized,
    bool AppIntegrated,
    bool NormalCreateBinEnabled,
    bool PromotionAuthorized,
    bool ReleaseIntegrated,
    bool ProcessUseCryptographicallyProven);

internal sealed record Id65V3RuntimeEvidenceScopeBoundary(
    bool BlankLookingNotByteEmpty,
    bool InheritedScaffoldingRetained,
    bool PinnedRuntimeFilesAreCurrentReadbackOnly,
    bool CaptureJournalOsProcessProvenanceCaptured,
    bool CaptureJournalHashChainIsUnauthenticated,
    bool RawCaptureIsFinalEvidence,
    bool ProcessUseCryptographicallyProven,
    bool ExecutableTextVnodeObserved,
    bool OperatorPromptIntervalsContinuouslyObserved,
    bool SystemCaptureAndDiagnosticToolsCryptographicallyPinned,
    bool SameUidRenameRacesCryptographicallyExcluded,
    bool VisualMediaContainersStructurallyValidated,
    bool VisualMetadataDimensionsMachineVerified,
    bool VisualContentIsHumanAttestation,
    bool AudioContentMachineVerified,
    bool FarLowDetailPending,
    bool SaveRuntimePending,
    bool Core4RewardRuntimePending,
    bool EditorIntegrationAuthorized,
    bool CreateBinAuthorized,
    bool PublisherAuthorized,
    bool PromotionAuthorized,
    bool ReleaseAuthorized);

internal sealed record Id65V3RuntimeEvidenceIntegrity(
    string Canonicalization,
    string SourceClosureSha256,
    string RuntimeProfileSha256,
    string ArtifactSetSha256,
    string PayloadSha256,
    string ContractSha256);

internal sealed record Id65V3RuntimeEvidenceLiveProjection(
    int SessionOrdinal,
    string SessionId,
    string RepoRelativePath,
    string Sha256,
    long ByteLength,
    string TerminalEventSha256);

internal sealed record Id65V3RuntimeEvidenceCaptureProvenance(
    string RunId,
    string RunOriginRepoRelativePath,
    string RunOriginSha256,
    long RunOriginByteLength,
    string FinalTerminalEventSha256,
    Id65V3RuntimeEvidenceLiveProjection[] LiveEventProjections,
    string CaptureClosureSha256);

internal sealed record Id65V3RuntimeEvidenceOperatorSafetyReview(
    bool SaveStateLoaded,
    bool SaveStateCreated,
    bool InGameSaveUsed,
    int ActiveCheatCount,
    string Statement);

internal sealed record Id65V3RuntimeEvidenceOperatorSessionReview(
    string SessionId,
    bool NewGame,
    bool CardsNone,
    bool CheatsOff,
    bool SaveStateLoaded,
    bool SaveStateCreated,
    bool InGameSaveUsed,
    string Statement);

internal sealed record Id65V3RuntimeEvidenceOperatorCheckReview(
    string CheckId,
    string SessionId,
    Id65V3RuntimeEvidenceCheckStatus Status,
    string EvidenceBindingSha256,
    string Statement);

internal sealed record Id65V3RuntimeEvidenceOperatorReview(
    string AttestationSha256,
    string Operator,
    string AttestedAtUtc,
    string RunId,
    string RunOriginSha256,
    string FinalTerminalEventSha256,
    string CaptureClosureSha256,
    Id65V3RuntimeEvidenceOperatorSafetyReview RuntimeSafety,
    Id65V3RuntimeEvidenceOperatorSessionReview[] Sessions,
    Id65V3RuntimeEvidenceOperatorCheckReview[] Checks);

internal sealed record Id65V3RuntimeEvidencePackage(
    string Format,
    int FormatVersion,
    string EvidenceId,
    string? RecordedAtUtc,
    Id65V3RuntimeEvidenceState DeclaredState,
    Id65V3RuntimeEvidenceSourceIdentity SourceIdentity,
    Id65V3RuntimeEvidenceProfile RuntimeProfile,
    Id65V3RuntimeEvidenceArtifact[] ArtifactCatalog,
    Id65V3RuntimeEvidenceSession[] Sessions,
    Id65V3RuntimeEvidenceCheck[] VisualChecks,
    Id65V3RuntimeEvidenceColdReset ColdReset,
    Id65V3RuntimeEvidenceRetailControl[] RetailControls,
    Id65V3RuntimeEvidenceCaptureProvenance? CaptureProvenance,
    Id65V3RuntimeEvidenceOperatorReview? OperatorReview,
    Id65V3RuntimeEvidenceDerivedResult DerivedResult,
    Id65V3RuntimeEvidenceScopeBoundary ScopeBoundary,
    Id65V3RuntimeEvidenceIntegrity Integrity);

internal sealed record Id65V3RuntimeEvidenceValidation(
    bool StructurallyValid,
    Id65V3RuntimeEvidenceState State,
    Id65V3RuntimeEvidencePackage? Package,
    string CanonicalJson,
    string[] Errors);

internal sealed record Id65V3TelemetryParseResult(
    Id65V3TelemetryTuple[] Samples,
    bool CleanStopObserved,
    string[] PreambleLines);

internal static partial class Id65V3RuntimeEvidenceContract
{
    internal const string Format = "spyro-editor-id65-v3-environment-header-runtime-evidence";
    internal const int FormatVersion = 3;
    internal const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke";
    internal const string ExpectedProfileId =
        "unused-level-65-remote-blank-environment-header-repair-sector216-clean-usa-disposable-v3";
    internal const string ExpectedRuntimeProfileSha256 =
        "29d01f1b791e4dc08ebfc4d62cf5a1b91600a5c55ec175ac6da45b13123100ad";
    internal const string ExpectedSourceClosureSha256 =
        "1ec8cbcc0590a25c865d36ecd682e218db0555dada84c68a865ea5415c7aaacf";
    internal const string ExpectedWatcherSha256 =
        "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677";
    internal const string ExpectedCaptureCoordinatorSha256 =
        "c7c2a10660aa36e69580be0886a1624cdb265030754c553a0b435047976e7e77";
    internal const string FocusedRuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-remote-blank-environment-header-repair-v3-runtime-evidence.json";
    internal static readonly string ExpectedFocusedRuntimeEvidenceSha256 = "PENDING";
    internal const string RequiredRewardPublisherSmokeAssemblyName =
        "Spyro.Editor.Id65AuthoringRewardStagingRuntimeCandidatePublisherSmoke";

    private const string CandidateDirectory =
        "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-environment-header-repair-v3";
    private const string CandidateStem =
        "Unused-Level-65-Remote-Blank-Environment-Header-Repair-v3-Sector216-RUNTIME-CANDIDATE";
    private const string RuntimeAssetPrefix =
        "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/";
    private const string ProfilePackageRelativePath =
        "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1";
    private const string PackagedAppRelativePath =
        ProfilePackageRelativePath + "/DuckStation-ID65-Unattended-v1.app";
    private const string DuckStationExecutableRelativePath =
        PackagedAppRelativePath + "/Contents/MacOS/DuckStation";
    private const string ExternalHomeRelativePath = ProfilePackageRelativePath + "/home";
    private const string ExternalHomeDataRelativePath =
        ExternalHomeRelativePath + "/Library/Application Support/DuckStation";
    private const string HandoffReceiptRelativePath =
        ProfilePackageRelativePath + "/handoff-receipt.json";
    private const string HostNormalSettingsAbsolutePath =
        "/Users/john/Library/Application Support/DuckStation/settings.ini";
    private const string ExternalHomeBiosManifestPath =
        "bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 (1995-12-04)(Sony)(US).bin";
    private const string ProfileBuilderRelativePath = "tools/Build-SpyroId65UnattendedProfile.py";
    private const string ProfileVerifierRelativePath = "tools/Verify-SpyroId65UnattendedProfile.py";
    private const string AutomationRelativePath = "tools/Automate-SpyroId65V3RuntimeSession.py";
    private const string CaptureCoordinatorRelativePath =
        "tools/Capture-SpyroId65V3RuntimeSession.py";
    private const string ExpectedProfileBuilderSha256 =
        "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c";
    private const string ExpectedProfileVerifierSha256 =
        "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671";
    private const string ExpectedAutomationSha256 =
        "2ea6ce2104b4dbbcdc116d84807ca2425bfb80436f01895b5b618d92ec6773bd";
    private const string Canonicalization = "rfc8785-jcs-utf8-v1";
    private const string ProfileSnapshotFormat = "spyro-editor-id65-v3-runtime-profile-snapshot";
    private const int ProfileSnapshotFormatVersion = 2;
    private const string CaptureJournalFormat =
        "spyro-editor-id65-v3-runtime-capture-event-journal";
    private const int CaptureJournalFormatVersion = 3;
    private const int MaximumCaptureJournalEvents = 512;
    private const int MaximumArtifactCount = 256;
    private const int MaximumTelemetryLogBytes = 64 * 1024 * 1024;
    private const int MaximumTelemetrySamples = 1_000_000;
    private const long MaximumPinnedFileBytes = 64L * 1024 * 1024 * 1024;
    private const long MaximumTelemetryClockDriftMilliseconds = 250;
    internal const long MaximumCaptureRecordingDurationMicroseconds = 30L * 60 * 1_000_000;
    internal const long MaximumFallAuthorizationBindingMilliseconds = 30_000;
    internal const long MaximumPostDeathRecoveryMilliseconds = 15_000;
    internal const long AssistedReadinessMinimumMilliseconds = 2_000;
    internal const int AssistedReadinessMinimumSamples = 19;
    internal const long AssistedPrimaryDepartureMaximumMilliseconds = 300_000;
    internal const long AssistedPrimaryLandingMaximumMilliseconds = 30_000;
    private const long MaximumJcsSafeInteger = 9_007_199_254_740_991;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    private static readonly string[] SessionIds =
    [
        "candidate-primary-edge-0",
        "candidate-edge-1",
        "candidate-edge-2",
        "candidate-cold-reset",
        "retail-town-square",
        "retail-gnastys-loot",
        "retail-sunny-flight"
    ];

    private static readonly string[] CandidateCheckIds =
    [
        "candidate.completed-landing-hud4",
        "candidate.hands-off-20-seconds",
        "candidate.lone-pad-visibility",
        "candidate.camera-360-twice",
        "candidate.walk-center-perimeter-both-directions",
        "candidate.jump-land-center-each-corner",
        "candidate.primary-edge-4-to-3-same-pad-yaw",
        "candidate.edge-1-same-pad-yaw",
        "candidate.edge-2-same-pad-yaw",
        "candidate.cold-reset-same-pad-yaw"
    ];

    private static readonly string[] TownSquareCheckIds =
    [
        "retail.town-square.terrain",
        "retail.town-square.collision",
        "retail.town-square.mobys",
        "retail.town-square.camera",
        "retail.town-square.death-respawn",
        "retail.town-square.music",
        "retail.town-square.exit",
        "retail.town-square.totals"
    ];

    private static readonly string[] GnastysLootCheckIds =
    [
        "retail.gnastys-loot.gameplay",
        "retail.gnastys-loot.exit"
    ];

    private static readonly string[] SunnyFlightCheckIds =
    [
        "retail.sunny-flight.gameplay",
        "retail.sunny-flight.timer",
        "retail.sunny-flight.exit"
    ];

    private static readonly string[] AllCheckIds =
        CandidateCheckIds.Concat(TownSquareCheckIds).Concat(GnastysLootCheckIds)
            .Concat(SunnyFlightCheckIds).ToArray();

    internal static string? RequiredEdgeAssignmentToken(string checkId) => checkId switch
    {
        "candidate.primary-edge-4-to-3-same-pad-yaw" => "E0=A-B",
        "candidate.edge-1-same-pad-yaw" => "E1=B-C",
        "candidate.edge-2-same-pad-yaw" => "E2=C-A",
        _ => null
    };

    private static readonly HashSet<string> TelemetryCorroboratedCheckIds =
        new(StringComparer.Ordinal)
        {
            "candidate.completed-landing-hud4",
            "candidate.hands-off-20-seconds",
            "candidate.primary-edge-4-to-3-same-pad-yaw",
            "candidate.edge-1-same-pad-yaw",
            "candidate.edge-2-same-pad-yaw"
        };

    private static readonly HashSet<string> AllowedArtifactKinds = new(StringComparer.Ordinal)
    {
        "watcher-stdout",
        "capture-event-journal",
        "runtime-profile-before",
        "runtime-profile-after",
        "screen-recording",
        "frame"
    };

    private static readonly IReadOnlyDictionary<string, long> MinimumRecordingSliceMilliseconds =
        new Dictionary<string, long>(StringComparer.Ordinal)
        {
            ["candidate.completed-landing-hud4"] = 3_000,
            ["candidate.hands-off-20-seconds"] = 20_000,
            ["candidate.camera-360-twice"] = 6_000,
            ["candidate.walk-center-perimeter-both-directions"] = 8_000,
            ["candidate.jump-land-center-each-corner"] = 8_000,
            ["candidate.primary-edge-4-to-3-same-pad-yaw"] = 5_000,
            ["candidate.edge-1-same-pad-yaw"] = 5_000,
            ["candidate.edge-2-same-pad-yaw"] = 5_000,
            ["candidate.cold-reset-same-pad-yaw"] = 5_000,
            ["retail.town-square.terrain"] = 3_000,
            ["retail.town-square.collision"] = 5_000,
            ["retail.town-square.mobys"] = 5_000,
            ["retail.town-square.camera"] = 5_000,
            ["retail.town-square.death-respawn"] = 5_000,
            ["retail.town-square.music"] = 10_000,
            ["retail.town-square.exit"] = 3_000,
            ["retail.town-square.totals"] = 2_000,
            ["retail.gnastys-loot.gameplay"] = 5_000,
            ["retail.gnastys-loot.exit"] = 3_000,
            ["retail.sunny-flight.gameplay"] = 5_000,
            ["retail.sunny-flight.timer"] = 3_000,
            ["retail.sunny-flight.exit"] = 3_000
        };

    private static readonly string[] ExactWatcherPreamble =
    [
        "WARNING: connecting to this DuckStation GDB server pauses a running game exactly once.",
        "This watcher sends only the three approved m reads and protocol ACKs; it cannot resume the game.",
        "Connected once to 127.0.0.1:2345. DuckStation is now paused."
    ];

    private const string WatcherPrompt =
        "Manually Resume in DuckStation, leave this watcher connected, then press Return here to begin the 0.1-second-period watch: ";
    private const string WatcherWatching =
        "Watching one numbered near-contemporaneous tuple per period with monotonic elapsed milliseconds; press Ctrl-C to stop cleanly.";
    private const string WatcherStopped =
        "Stopped cleanly; the socket is closed and no control packet was sent. If DuckStation was not manually resumed, it remains paused.";

    [GeneratedRegex(
        "^(?<timestamp>\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z) sample=(?<ordinal>0|[1-9]\\d*) elapsed-ms=(?<elapsed>0|[1-9]\\d*) lives=(?<lives>0|[1-9]\\d*) death-state=(?<state>0|[1-9]\\d*)\\((?<label>[^)]+)\\) player-Z=(?<z>-?(?:0|[1-9]\\d*)) \\[Z<0x400:(?<marker>YES|no)\\] raw=(?<livesRaw>[0-9a-f]{8})/(?<stateRaw>[0-9a-f]{8})/(?<zRaw>[0-9a-f]{8})$",
        RegexOptions.CultureInvariant)]
    private static partial Regex SampleRegex();

    [GeneratedRegex("^[a-z0-9][a-z0-9.-]{0,127}$", RegexOptions.CultureInvariant)]
    private static partial Regex ArtifactIdRegex();

    [GeneratedRegex("^[0-9a-f]{64}$", RegexOptions.CultureInvariant)]
    private static partial Regex Sha256Regex();

    [GeneratedRegex(
        "^id65v3-[0-9]{8}t[0-9]{6}z-[0-9a-f]{16}$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CaptureRunIdRegex();

    [GeneratedRegex("^id65v3-process-[0-9a-f]{32}$", RegexOptions.CultureInvariant)]
    private static partial Regex CaptureProcessIdRegex();

    [GeneratedRegex(
        "^\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}\\.\\d{3}Z$",
        RegexOptions.CultureInvariant)]
    private static partial Regex CanonicalUtcRegex();

    private sealed record CaptureJournalEventReadback(
        int Sequence,
        string EventType,
        string AtUtc,
        long MonotonicMicroseconds,
        string EventSha256,
        JsonElement Payload);

    private sealed record CaptureRawArtifactReadback(
        string ArtifactId,
        string Kind,
        string RepoRelativePath,
        string Sha256,
        long ByteLength,
        string CapturedAtUtc,
        string? CaptureRole,
        string? MediaStartAtUtc,
        string? MediaEndAtUtc);

    private sealed record CaptureJournalReadback(
        string ArtifactId,
        string RunId,
        int SessionOrdinal,
        string SessionId,
        string BootIdentity,
        string ProcessInstanceId,
        string RunOriginSha256,
        string PreviousSessionTerminalSha256,
        string TerminalEventSha256,
        string FirstAtUtc,
        long FirstMonotonicMicroseconds,
        string TerminalAtUtc,
        long TerminalMonotonicMicroseconds,
        string RawProcessFingerprintSha256,
        CaptureJournalEventReadback[] Events,
        CaptureRawArtifactReadback[] RawArtifacts);

    /// <summary>
    /// Opaque capability proving that the one pinned V3 evidence document was
    /// canonically validated with complete filesystem readback. Construction is
    /// private to this contract; the publisher can only ask the contract to
    /// revalidate an already issued capability immediately before mutation.
    /// </summary>
    internal sealed class Id65V3FocusedRuntimePassToken
    {
        private Id65V3FocusedRuntimePassToken(
            string repositoryRoot,
            string evidenceSha256,
            string sourceClosureSha256,
            string runtimeProfileSha256,
            string artifactSetSha256,
            string payloadSha256,
            string contractSha256,
            bool syntheticPublisherSmokeOnly,
            byte[]? syntheticCanonicalEvidenceUtf8,
            string? syntheticFullReadbackRepositoryRoot,
            Id65V3RuntimeEvidenceReadbackContext? syntheticReadbackContext)
        {
            RepositoryRoot = repositoryRoot;
            EvidenceSha256 = evidenceSha256;
            SourceClosureSha256 = sourceClosureSha256;
            RuntimeProfileSha256 = runtimeProfileSha256;
            ArtifactSetSha256 = artifactSetSha256;
            PayloadSha256 = payloadSha256;
            ContractSha256 = contractSha256;
            SyntheticPublisherSmokeOnly = syntheticPublisherSmokeOnly;
            SyntheticCanonicalEvidenceUtf8 = syntheticCanonicalEvidenceUtf8?.ToArray();
            SyntheticFullReadbackRepositoryRoot = syntheticFullReadbackRepositoryRoot;
            SyntheticReadbackContext = syntheticReadbackContext;
        }

        private string RepositoryRoot { get; }
        private string EvidenceSha256 { get; }
        private string SourceClosureSha256 { get; }
        private string RuntimeProfileSha256 { get; }
        private string ArtifactSetSha256 { get; }
        private string PayloadSha256 { get; }
        private string ContractSha256 { get; }
        private bool SyntheticPublisherSmokeOnly { get; }
        private byte[]? SyntheticCanonicalEvidenceUtf8 { get; }
        private string? SyntheticFullReadbackRepositoryRoot { get; }
        private Id65V3RuntimeEvidenceReadbackContext? SyntheticReadbackContext { get; }

        private FocusedRuntimePassTokenState ExportState() =>
            new(
                RepositoryRoot,
                EvidenceSha256,
                SourceClosureSha256,
                RuntimeProfileSha256,
                ArtifactSetSha256,
                PayloadSha256,
                ContractSha256,
                SyntheticPublisherSmokeOnly,
                SyntheticCanonicalEvidenceUtf8?.ToArray(),
                SyntheticFullReadbackRepositoryRoot,
                SyntheticReadbackContext);
    }

    private sealed record FocusedRuntimePassTokenState(
        string RepositoryRoot,
        string EvidenceSha256,
        string SourceClosureSha256,
        string RuntimeProfileSha256,
        string ArtifactSetSha256,
        string PayloadSha256,
        string ContractSha256,
        bool SyntheticPublisherSmokeOnly,
        byte[]? SyntheticCanonicalEvidenceUtf8,
        string? SyntheticFullReadbackRepositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext? SyntheticReadbackContext);

    private static Id65V3FocusedRuntimePassToken CreateFocusedRuntimePassToken(
        string repositoryRoot,
        string evidenceSha256,
        string sourceClosureSha256,
        string runtimeProfileSha256,
        string artifactSetSha256,
        string payloadSha256,
        string contractSha256,
        bool syntheticPublisherSmokeOnly,
        byte[]? syntheticCanonicalEvidenceUtf8,
        string? syntheticFullReadbackRepositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext? syntheticReadbackContext)
    {
        ConstructorInfo constructor = typeof(Id65V3FocusedRuntimePassToken).GetConstructors(
            BindingFlags.Instance | BindingFlags.NonPublic).Single();
        return (Id65V3FocusedRuntimePassToken)constructor.Invoke(
        [
            repositoryRoot,
            evidenceSha256,
            sourceClosureSha256,
            runtimeProfileSha256,
            artifactSetSha256,
            payloadSha256,
            contractSha256,
            syntheticPublisherSmokeOnly,
            syntheticCanonicalEvidenceUtf8,
            syntheticFullReadbackRepositoryRoot,
            syntheticReadbackContext
        ]);
    }

    private static FocusedRuntimePassTokenState ReadFocusedRuntimePassTokenState(
        Id65V3FocusedRuntimePassToken token)
    {
        MethodInfo exporter = typeof(Id65V3FocusedRuntimePassToken).GetMethod(
            "ExportState",
            BindingFlags.Instance | BindingFlags.NonPublic) ??
            throw new MissingMethodException("The opaque focused-runtime pass token exporter is missing.");
        return (FocusedRuntimePassTokenState)(exporter.Invoke(token, null) ??
            throw new InvalidOperationException("The opaque focused-runtime pass token exporter returned null."));
    }

    internal sealed record Id65V3FocusedRuntimePassIdentity(
        string EvidenceRelativePath,
        string EvidenceSha256,
        string SourceClosureSha256,
        string RuntimeProfileSha256,
        string ArtifactSetSha256,
        string PayloadSha256,
        string ContractSha256,
        bool SyntheticPublisherSmokeOnly);

    internal static Id65V3RuntimeEvidencePackage CreatePending()
    {
        Id65V3RuntimeEvidencePackage pending = new(
            Format,
            FormatVersion,
            "unused-level-65-remote-blank-environment-header-repair-v3-runtime-evidence-pending",
            RecordedAtUtc: null,
            Id65V3RuntimeEvidenceState.PendingNoRun,
            BuildExpectedSourceIdentity(),
            BuildExpectedRuntimeProfile(),
            ArtifactCatalog: [],
            Sessions: [],
            VisualChecks: [],
            new(
                Id65V3RuntimeEvidenceCheckStatus.NotObserved,
                PrimaryProcessExitedAtUtc: null,
                ResetProcessStartedAtUtc: null,
                "candidate-cold-reset",
                "candidate.cold-reset-same-pad-yaw"),
            RetailControls: [],
            CaptureProvenance: null,
            OperatorReview: null,
            BuildDerivedResult(Id65V3RuntimeEvidenceState.PendingNoRun, sourceValid: true),
            BuildScopeBoundary(),
            new(Canonicalization, ExpectedSourceClosureSha256, ExpectedRuntimeProfileSha256,
                string.Empty, string.Empty, string.Empty));
        return WithRecomputedIntegrity(pending);
    }

    internal static string SerializeCanonical(Id65V3RuntimeEvidencePackage package) =>
        SerializeCanonicalValue(package);

    internal static byte[] SerializeCanonicalUtf8(Id65V3RuntimeEvidencePackage package) =>
        StrictUtf8.GetBytes(SerializeCanonical(package));

    internal static Id65V3RuntimeEvidenceValidation ValidateCanonicalJson(
        ReadOnlySpan<byte> utf8Json,
        string repositoryRoot,
        Id65V3RuntimeEvidenceValidationMode mode = Id65V3RuntimeEvidenceValidationMode.StaticOnly,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext = null)
    {
        List<string> errors = [];
        Id65V3RuntimeEvidencePackage? package = null;
        string canonical = string.Empty;
        try
        {
            if (utf8Json.Length == 0 || utf8Json.Length > MaximumTelemetryLogBytes)
                throw new InvalidDataException("Runtime evidence JSON has an invalid byte length.");
            if (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB && utf8Json[2] == 0xBF)
                throw new InvalidDataException("Runtime evidence JSON must not contain a UTF-8 BOM.");
            _ = DecodeStrictUtf8(utf8Json, "runtime evidence JSON");

            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            RejectDuplicateProperties(document.RootElement, "$", errors);
            if (errors.Count != 0)
                throw new InvalidDataException(errors[0]);

            package = JsonSerializer.Deserialize<Id65V3RuntimeEvidencePackage>(utf8Json, JsonOptions)
                ?? throw new InvalidDataException("Runtime evidence JSON deserialized to null.");
            canonical = SerializeCanonical(package);
            if (!utf8Json.SequenceEqual(StrictUtf8.GetBytes(canonical)))
                throw new InvalidDataException("Runtime evidence JSON is not the exact canonical UTF-8 encoding.");

            ValidateStaticPackage(package);
            ValidateIntegrity(package);
            ValidateArtifactCatalogShape(repositoryRoot, package.ArtifactCatalog);
            ValidateRecordedAtBoundary(package);

            Dictionary<string, byte[]> artifactBytes = mode == Id65V3RuntimeEvidenceValidationMode.FullReadback
                ? ReadAndVerifyArtifacts(repositoryRoot, package.ArtifactCatalog)
                : new(StringComparer.Ordinal);
            Id65V3RuntimeEvidenceReadbackContext? effectiveReadbackContext =
                mode == Id65V3RuntimeEvidenceValidationMode.FullReadback
                    ? readbackContext ?? BuildExpectedReadbackContext(repositoryRoot)
                    : null;
            bool pinnedRuntimeFilesReadbackValid = effectiveReadbackContext is not null &&
                VerifyPinnedRuntimeFiles(package.RuntimeProfile, effectiveReadbackContext);
            bool sourceFilesValid = mode == Id65V3RuntimeEvidenceValidationMode.FullReadback &&
                VerifyPinnedSourceArtifacts(
                    effectiveReadbackContext!.SourceRepositoryRootPath,
                    package.SourceIdentity);

            Evaluation evaluation = Evaluate(
                package,
                artifactBytes,
                mode,
                sourceFilesValid,
                pinnedRuntimeFilesReadbackValid,
                effectiveReadbackContext,
                repositoryRoot);
            if (package.DeclaredState != evaluation.State)
                throw new InvalidDataException(
                    $"Declared evidence state {package.DeclaredState} does not match derived state {evaluation.State}.");
            if (SerializeCanonicalValue(package.DerivedResult) !=
                SerializeCanonicalValue(evaluation.DerivedResult))
            {
                throw new InvalidDataException("Stored derived result differs from deterministic recomputation.");
            }

            return new(true, evaluation.State, package, canonical, []);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or IOException or
                                         UnauthorizedAccessException or ArgumentException or OverflowException)
        {
            if (errors.Count == 0 || !errors.Contains(exception.Message, StringComparer.Ordinal))
                errors.Add(exception.Message);
            return new(false, Id65V3RuntimeEvidenceState.Invalid, package, canonical, errors.ToArray());
        }
    }

    internal static Id65V3FocusedRuntimePassToken AcquirePinnedFocusedRuntimePassToken(
        string repositoryRoot)
    {
        string root = RequireRepositoryRoot(repositoryRoot);
        if (ExpectedFocusedRuntimeEvidenceSha256 == "PENDING")
        {
            throw new InvalidOperationException(
                "The pinned ID65 V3 focused-runtime evidence SHA-256 is PENDING; " +
                "a real canonical seven-session DuckStation pass is required before reward publication.");
        }
        if (!Sha256Regex().IsMatch(ExpectedFocusedRuntimeEvidenceSha256))
            throw new InvalidOperationException("The pinned ID65 V3 focused-runtime evidence SHA-256 is malformed.");

        PinnedEvidenceBytes pinned = ReadPinnedFocusedRuntimeEvidence(root);
        if (pinned.Sha256 != ExpectedFocusedRuntimeEvidenceSha256)
        {
            throw new InvalidDataException(
                $"The pinned ID65 V3 focused-runtime evidence SHA-256 is {pinned.Sha256}, " +
                $"expected {ExpectedFocusedRuntimeEvidenceSha256}.");
        }
        Id65V3RuntimeEvidenceValidation validation = ValidateCanonicalJson(
            pinned.Bytes,
            root,
            Id65V3RuntimeEvidenceValidationMode.FullReadback);
        Id65V3RuntimeEvidencePackage package = RequireFocusedRuntimePass(validation);
        return CreateFocusedRuntimePassToken(
            root,
            pinned.Sha256,
            package.Integrity.SourceClosureSha256,
            package.Integrity.RuntimeProfileSha256,
            package.Integrity.ArtifactSetSha256,
            package.Integrity.PayloadSha256,
            package.Integrity.ContractSha256,
            syntheticPublisherSmokeOnly: false,
            syntheticCanonicalEvidenceUtf8: null,
            syntheticFullReadbackRepositoryRoot: null,
            syntheticReadbackContext: null);
    }

    internal static void RequireFreshFocusedRuntimePassToken(
        Id65V3FocusedRuntimePassToken token,
        string repositoryRoot)
    {
        ArgumentNullException.ThrowIfNull(token);
        string root = RequireRepositoryRoot(repositoryRoot);
        FocusedRuntimePassTokenState tokenState = ReadFocusedRuntimePassTokenState(token);
        if (tokenState.SyntheticPublisherSmokeOnly)
        {
            RequireExactRewardSmokeEntryAssembly();
            if (tokenState.RepositoryRoot != root ||
                tokenState.SourceClosureSha256 != ExpectedSourceClosureSha256 ||
                tokenState.RuntimeProfileSha256 != ExpectedRuntimeProfileSha256 ||
                tokenState.SyntheticCanonicalEvidenceUtf8 is null ||
                tokenState.SyntheticFullReadbackRepositoryRoot is null ||
                tokenState.SyntheticReadbackContext is null)
            {
                throw new InvalidDataException("The synthetic publisher-smoke token changed identity.");
            }
            Id65V3RuntimeEvidenceValidation refreshedValidation = ValidateCanonicalJson(
                tokenState.SyntheticCanonicalEvidenceUtf8,
                tokenState.SyntheticFullReadbackRepositoryRoot,
                Id65V3RuntimeEvidenceValidationMode.FullReadback,
                tokenState.SyntheticReadbackContext);
            Id65V3RuntimeEvidencePackage refreshedPackage = RequireFocusedRuntimePass(refreshedValidation);
            if (Hash(SerializeCanonicalUtf8(refreshedPackage)) != tokenState.EvidenceSha256 ||
                refreshedPackage.Integrity.SourceClosureSha256 != tokenState.SourceClosureSha256 ||
                refreshedPackage.Integrity.RuntimeProfileSha256 != tokenState.RuntimeProfileSha256 ||
                refreshedPackage.Integrity.ArtifactSetSha256 != tokenState.ArtifactSetSha256 ||
                refreshedPackage.Integrity.PayloadSha256 != tokenState.PayloadSha256 ||
                refreshedPackage.Integrity.ContractSha256 != tokenState.ContractSha256)
            {
                throw new InvalidDataException("The synthetic FullReadback fixture changed after capability issuance.");
            }
            return;
        }

        Id65V3FocusedRuntimePassToken fresh = AcquirePinnedFocusedRuntimePassToken(root);
        FocusedRuntimePassTokenState freshState = ReadFocusedRuntimePassTokenState(fresh);
        if (tokenState.RepositoryRoot != freshState.RepositoryRoot ||
            tokenState.EvidenceSha256 != freshState.EvidenceSha256 ||
            tokenState.SourceClosureSha256 != freshState.SourceClosureSha256 ||
            tokenState.RuntimeProfileSha256 != freshState.RuntimeProfileSha256 ||
            tokenState.ArtifactSetSha256 != freshState.ArtifactSetSha256 ||
            tokenState.PayloadSha256 != freshState.PayloadSha256 ||
            tokenState.ContractSha256 != freshState.ContractSha256)
        {
            throw new InvalidDataException(
                "The ID65 V3 focused-runtime evidence changed between capability issuance and immediate use.");
        }
    }

    internal static Id65V3FocusedRuntimePassIdentity DescribeFocusedRuntimePassToken(
        Id65V3FocusedRuntimePassToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        FocusedRuntimePassTokenState state = ReadFocusedRuntimePassTokenState(token);
        return new(
            FocusedRuntimeEvidenceRelativePath,
            state.EvidenceSha256,
            state.SourceClosureSha256,
            state.RuntimeProfileSha256,
            state.ArtifactSetSha256,
            state.PayloadSha256,
            state.ContractSha256,
            state.SyntheticPublisherSmokeOnly);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Id65V3FocusedRuntimePassToken CreateSyntheticFocusedRuntimePassTokenForRewardSmoke(
        string repositoryRoot,
        byte[] canonicalEvidenceUtf8,
        string fullReadbackRepositoryRoot,
        Id65V3RuntimeEvidenceReadbackContext readbackContext)
    {
        string? caller = Assembly.GetCallingAssembly().GetName().Name;
        if (caller != RequiredRewardPublisherSmokeAssemblyName)
        {
            throw new InvalidOperationException(
                "Synthetic focused-runtime capability issuance is restricted to the exact reward-publisher smoke assembly.");
        }
        RequireExactRewardSmokeEntryAssembly();
        ArgumentNullException.ThrowIfNull(canonicalEvidenceUtf8);
        ArgumentNullException.ThrowIfNull(readbackContext);
        string root = RequireRepositoryRoot(repositoryRoot);
        string validationRoot = RequireRepositoryRoot(fullReadbackRepositoryRoot);
        Id65V3RuntimeEvidenceValidation fullReadbackValidation = ValidateCanonicalJson(
            canonicalEvidenceUtf8,
            validationRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            readbackContext);
        Id65V3RuntimeEvidencePackage package = RequireFocusedRuntimePass(fullReadbackValidation);
        string evidenceSha256 = Hash(SerializeCanonicalUtf8(package));
        return CreateFocusedRuntimePassToken(
            root,
            evidenceSha256,
            package.Integrity.SourceClosureSha256,
            package.Integrity.RuntimeProfileSha256,
            package.Integrity.ArtifactSetSha256,
            package.Integrity.PayloadSha256,
            package.Integrity.ContractSha256,
            syntheticPublisherSmokeOnly: true,
            syntheticCanonicalEvidenceUtf8: canonicalEvidenceUtf8,
            syntheticFullReadbackRepositoryRoot: validationRoot,
            syntheticReadbackContext: readbackContext);
    }

    private static Id65V3RuntimeEvidencePackage RequireFocusedRuntimePass(
        Id65V3RuntimeEvidenceValidation validation)
    {
        if (!validation.StructurallyValid || validation.State != Id65V3RuntimeEvidenceState.FocusedRuntimePass ||
            validation.Errors.Length != 0 || validation.Package is null)
        {
            string detail = validation.Errors.FirstOrDefault() ?? validation.State.ToString();
            throw new InvalidDataException(
                $"The pinned ID65 V3 evidence is not a canonical FullReadback FocusedRuntimePass: {detail}");
        }
        Id65V3RuntimeEvidencePackage package = validation.Package;
        Id65V3RuntimeEvidenceDerivedResult result = package.DerivedResult;
        Id65V3RuntimeEvidenceIntegrity integrity = package.Integrity;
        if (package.DeclaredState != Id65V3RuntimeEvidenceState.FocusedRuntimePass ||
            result.State != Id65V3RuntimeEvidenceState.FocusedRuntimePass ||
            !result.SourceValid || !result.PinnedRuntimeFilesReadbackValid ||
            !result.RuntimeProfileValid || !result.ArtifactReadbackValid ||
            !result.DiscIdentityValid || !result.TelemetryValid ||
            !result.VisualChecklistComplete || !result.ColdResetValid ||
            !result.RetailControlsValid || !result.EnvironmentHeaderRepairV3RuntimeAccepted ||
            !result.RuntimeEvidenceAccepted || result.FarLowDetailAccepted ||
            result.SaveRuntimeAccepted || result.Core4RewardRuntimeAccepted ||
            result.Total200RuntimeAccepted || result.PublisherAuthorized ||
            result.AppIntegrated || result.NormalCreateBinEnabled ||
            result.PromotionAuthorized || result.ReleaseIntegrated ||
            result.ProcessUseCryptographicallyProven)
        {
            throw new InvalidDataException(
                "The pinned ID65 V3 evidence does not carry the exact accepted-header-repair-v3/downstream-closed derived gates.");
        }
        if (integrity.Canonicalization != Canonicalization ||
            integrity.SourceClosureSha256 != ExpectedSourceClosureSha256 ||
            integrity.RuntimeProfileSha256 != ExpectedRuntimeProfileSha256 ||
            !Sha256Regex().IsMatch(integrity.ArtifactSetSha256) ||
            !Sha256Regex().IsMatch(integrity.PayloadSha256) ||
            !Sha256Regex().IsMatch(integrity.ContractSha256))
        {
            throw new InvalidDataException("The pinned ID65 V3 evidence integrity identity changed.");
        }
        return package;
    }

    private static PinnedEvidenceBytes ReadPinnedFocusedRuntimeEvidence(string repositoryRoot)
    {
        string path = GuardPinnedSourcePath(repositoryRoot, FocusedRuntimeEvidenceRelativePath);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "The one pinned ID65 V3 focused-runtime evidence document is missing.",
                path);
        }
        Id65V3RuntimeEvidenceFinalizer.StableFileRead read =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                path,
                MaximumTelemetryLogBytes,
                keepBytes: true,
                context: "pinned ID65 V3 focused-runtime evidence");
        if (read.ByteLength <= 0 || read.Bytes is null)
            throw new InvalidDataException("The pinned ID65 V3 evidence document has an invalid byte length.");
        return new(read.Bytes, read.Sha256);
    }

    private static string RequireRepositoryRoot(string repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot) || !Path.IsPathFullyQualified(repositoryRoot))
            throw new InvalidDataException("The ID65 V3 evidence repository root must be absolute.");
        string root = Path.GetFullPath(repositoryRoot);
        if (!Directory.Exists(root))
            throw new DirectoryNotFoundException("The ID65 V3 evidence repository root is missing.");
        Id65V3RuntimeEvidenceFinalizer.RequireTrustedRepositoryRoot(
            root,
            "ID65 V3 evidence repository root");
        return root;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void RequireExactRewardSmokeEntryAssembly()
    {
        string? entry = Assembly.GetEntryAssembly()?.GetName().Name;
        if (entry != RequiredRewardPublisherSmokeAssemblyName)
        {
            throw new InvalidOperationException(
                "Synthetic focused-runtime capability issuance is restricted to the exact reward-publisher smoke assembly.");
        }
    }

    internal static Id65V3TelemetryParseResult ParseTelemetryLog(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (StrictUtf8.GetByteCount(text) > MaximumTelemetryLogBytes)
            throw new InvalidDataException("Watcher stdout exceeds the bounded log size.");

        if (text.Length == 0 || text[0] == '\uFEFF')
            throw new InvalidDataException("Watcher stdout is empty or contains a UTF-8 BOM.");

        string fixedPreamble = string.Join('\n', ExactWatcherPreamble) + "\n" + WatcherPrompt;
        if (!text.StartsWith(fixedPreamble, StringComparison.Ordinal))
            throw new InvalidDataException("Watcher stdout preamble or manual-resume prompt changed.");

        int cursor = fixedPreamble.Length;
        string promptSeparator;
        if (text.AsSpan(cursor).StartsWith(WatcherWatching, StringComparison.Ordinal))
        {
            promptSeparator = string.Empty;
        }
        else if (text.AsSpan(cursor).StartsWith("\n" + WatcherWatching, StringComparison.Ordinal))
        {
            promptSeparator = "\n";
            cursor += 1;
        }
        else if (text.AsSpan(cursor).StartsWith("\r\n" + WatcherWatching, StringComparison.Ordinal))
        {
            promptSeparator = "\r\n";
            cursor += 2;
        }
        else
        {
            throw new InvalidDataException("Watcher prompt separator or watching banner changed.");
        }

        cursor += WatcherWatching.Length;
        if (cursor >= text.Length || text[cursor] != '\n')
            throw new InvalidDataException("Watcher watching banner is not LF-terminated.");
        cursor++;

        string exactTrailer = "\n\n" + WatcherStopped + "\n";
        if (!text.EndsWith(exactTrailer, StringComparison.Ordinal))
            throw new InvalidDataException("Watcher stdout is missing the exact terminal clean-stop trailer.");
        string sampleBlock = text[cursor..^exactTrailer.Length];
        if (sampleBlock.Length == 0 || sampleBlock.Contains('\r'))
            throw new InvalidDataException("Watcher sample block is empty or uses an unapproved line ending.");

        string[] sampleLines = sampleBlock.Split('\n');
        if (sampleLines.Length > MaximumTelemetrySamples)
            throw new InvalidDataException("Watcher stdout exceeds the bounded sample count.");

        List<Id65V3TelemetryTuple> samples = new(sampleLines.Length);
        int expectedOrdinal = 0;
        long previousElapsed = -1;
        DateTimeOffset? previousUtc = null;
        long firstElapsed = 0;
        DateTimeOffset? firstUtc = null;
        for (int index = 0; index < sampleLines.Length; index++)
        {
            Match match = SampleRegex().Match(sampleLines[index]);
            if (!match.Success)
            {
                throw new InvalidDataException(
                    $"Watcher stdout contains an unapproved sample at sample line {index + 1}.");
            }

            Id65V3TelemetryTuple sample = DecodeSample(match);
            if (sample.SampleOrdinal != expectedOrdinal)
            {
                throw new InvalidDataException(
                    $"Watcher sample ordinal {sample.SampleOrdinal} is not the required contiguous ordinal {expectedOrdinal}.");
            }
            if (sample.ElapsedMilliseconds <= previousElapsed)
                throw new InvalidDataException("Watcher elapsed milliseconds did not strictly increase.");
            DateTimeOffset sampleUtc = ParseCanonicalUtc(sample.AtUtc, "watcher sample");
            if (previousUtc is not null && sampleUtc <= previousUtc.Value)
                throw new InvalidDataException("Watcher descriptive UTC timestamps did not strictly increase.");
            if (firstUtc is null)
            {
                firstUtc = sampleUtc;
                firstElapsed = sample.ElapsedMilliseconds;
            }
            else
            {
                long utcDelta = checked((long)(sampleUtc - firstUtc.Value).TotalMilliseconds);
                long elapsedDelta = checked(sample.ElapsedMilliseconds - firstElapsed);
                if (Math.Abs(checked(utcDelta - elapsedDelta)) > MaximumTelemetryClockDriftMilliseconds)
                {
                    throw new InvalidDataException(
                        "Watcher UTC and monotonic elapsed clocks diverged beyond 250 milliseconds.");
                }
            }
            expectedOrdinal = checked(expectedOrdinal + 1);
            previousElapsed = sample.ElapsedMilliseconds;
            previousUtc = sampleUtc;
            samples.Add(sample);
        }

        string[] preambleLines =
        [
            .. ExactWatcherPreamble,
            WatcherPrompt + promptSeparator + WatcherWatching
        ];
        return new(samples.ToArray(), CleanStopObserved: true, preambleLines);
    }

    internal static Id65V3TelemetrySummary RecomputeTelemetrySummary(
        Id65V3TelemetryParseResult parsed,
        Id65V3RuntimeEvidenceSession session)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        ArgumentNullException.ThrowIfNull(session);
        Id65V3TelemetryTuple[] samples = parsed.Samples;
        long maxGap = MaximumGap(samples);
        int[] livesValues = samples.Select(sample => sample.Lives).Distinct().Order().ToArray();
        int[] states = samples.Select(sample => sample.DeathState).Distinct().Order().ToArray();
        string[] transitions = samples.Zip(samples.Skip(1))
            .Where(pair => pair.First.Lives != pair.Second.Lives)
            .Select(pair => $"{pair.First.Lives}->{pair.Second.Lives}").ToArray();

        Id65V3StableHoldSummary? stable = null;
        if (session.HoldStartSampleOrdinal is not null || session.HoldEndSampleOrdinal is not null)
        {
            if (session.HoldStartSampleOrdinal is null || session.HoldEndSampleOrdinal is null)
                throw new InvalidDataException("Stable-hold sample ordinals must be supplied as a pair.");
            int startOrdinal = session.HoldStartSampleOrdinal.Value;
            int endOrdinal = session.HoldEndSampleOrdinal.Value;
            if (startOrdinal < 0 || endOrdinal < startOrdinal || endOrdinal >= samples.Length)
                throw new InvalidDataException("Stable-hold sample ordinal range is invalid.");
            Id65V3TelemetryTuple[] hold = samples[startOrdinal..(endOrdinal + 1)];
            Id65V3TelemetryTuple first = hold[0];
            Id65V3TelemetryTuple last = hold[^1];
            stable = new(
                first.AtUtc,
                last.AtUtc,
                first.SampleOrdinal,
                last.SampleOrdinal,
                first.ElapsedMilliseconds,
                last.ElapsedMilliseconds,
                checked(last.ElapsedMilliseconds - first.ElapsedMilliseconds),
                hold.Length,
                MaximumGap(hold),
                hold.All(sample => sample.Lives == 4),
                hold.All(sample => sample.DeathState == 0),
                hold.All(sample => sample.PlayerZ >= 1024));
        }

        Id65V3DeathEpisodeSummary? death = null;
        if (session.FallStartSampleOrdinal is not null)
        {
            int fallOrdinal = session.FallStartSampleOrdinal.Value;
            if (fallOrdinal <= 0 || fallOrdinal >= samples.Length)
                throw new InvalidDataException("Fall-start sample ordinal must have a preceding sample and remain in range.");
            Id65V3TelemetryTuple pre = samples[fallOrdinal - 1];
            Id65V3TelemetryTuple[] after = samples[fallOrdinal..];
            Id65V3TelemetryTuple? below = after.FirstOrDefault(sample => sample.PlayerZ < 1024);
            Id65V3TelemetryTuple? livesThree = after.FirstOrDefault(sample => sample.Lives == 3);
            Id65V3TelemetryTuple? stateFour = after.FirstOrDefault(sample => sample.DeathState == 4);
            Id65V3TelemetryTuple? lastStateFour = after.LastOrDefault(sample => sample.DeathState == 4);
            int transitionEndOrdinal = Math.Max(
                livesThree?.SampleOrdinal ?? int.MaxValue,
                stateFour?.SampleOrdinal ?? int.MaxValue);
            Id65V3TelemetryTuple? post = transitionEndOrdinal == int.MaxValue
                ? null
                : after.FirstOrDefault(sample => sample.SampleOrdinal > transitionEndOrdinal &&
                    sample.Lives == 3 && sample.DeathState == 0 && sample.PlayerZ >= 1024);
            Id65V3TelemetryTuple[] episode = [pre, .. after];
            int transitionCount = episode.Zip(episode.Skip(1))
                .Count(pair => pair.First.Lives != pair.Second.Lives);
            death = new(
                pre,
                below,
                livesThree,
                stateFour,
                lastStateFour,
                post,
                transitionCount,
                after.Any(sample => sample.Lives < 3));
        }

        return new(
            ExpectedWatcherSha256,
            PeriodMilliseconds: 100,
            NearContemporaneousNotAtomic: true,
            samples.Length,
            ParseErrors: 0,
            samples.Length == 0 ? null : samples[0].AtUtc,
            samples.Length == 0 ? null : samples[^1].AtUtc,
            maxGap,
            samples.FirstOrDefault(),
            samples.LastOrDefault(),
            samples.Length == 0 ? null : samples.Min(sample => sample.PlayerZ),
            samples.Length == 0 ? null : samples.Max(sample => sample.PlayerZ),
            livesValues,
            transitions,
            states,
            samples.Count(sample => sample.DeathState == 4),
            samples.Count(sample => sample.DeathState == 5),
            samples.Count(sample => sample.PlayerZ < 1024),
            parsed.CleanStopObserved,
            stable,
            death);
    }

    internal static void GuardArtifactRelativePath(string repositoryRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') ||
            relativePath.Contains('\0') || relativePath.Contains('|') ||
            relativePath.Any(char.IsControl))
        {
            throw new InvalidDataException("Artifact path must be a nonempty POSIX repository-relative path.");
        }
        string[] segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new InvalidDataException("Artifact path contains an empty, current, or parent segment.");
        if (!relativePath.StartsWith(RuntimeAssetPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Runtime evidence artifacts must remain under the fixed evidence asset root.");

        string root = Path.GetFullPath(repositoryRoot);
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        string rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(rootPrefix, StringComparison.Ordinal))
            throw new InvalidDataException("Artifact path escapes the repository root.");
        RejectSymlinkSegments(root, segments);
    }

    internal static string RecomputeSourceClosureSha256(Id65V3RuntimeEvidenceSourceIdentity source) =>
        HashUtf8(BuildSourceClosureManifest(source));

    internal static string RecomputeRuntimeProfileSha256(Id65V3RuntimeEvidenceProfile profile) =>
        HashUtf8(BuildRuntimeProfileManifest(profile));

    private static Evaluation Evaluate(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceValidationMode mode,
        bool sourceFilesValid,
        bool pinnedRuntimeFilesReadbackValid,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext,
        string artifactRepositoryRoot)
    {
        if (RuntimeProfileHasUnsafeObservation(package.RuntimeProfile))
            throw new InvalidDataException("Observed DuckStation profile violates a pinned safety boundary.");
        bool profileObservationComplete = RuntimeProfileObservationIsComplete(package.RuntimeProfile);
        ValidateColdResetShape(package);
        bool hasRunMaterial = package.ArtifactCatalog.Length != 0 || package.Sessions.Length != 0 ||
            package.VisualChecks.Length != 0 || package.RetailControls.Length != 0 ||
            package.RecordedAtUtc is not null || RuntimeProfileObservationHasAny(package.RuntimeProfile) ||
            package.CaptureProvenance is not null || package.OperatorReview is not null ||
            package.ColdReset.Status != Id65V3RuntimeEvidenceCheckStatus.NotObserved ||
            package.ColdReset.PrimaryProcessExitedAtUtc is not null ||
            package.ColdReset.ResetProcessStartedAtUtc is not null;
        if (!hasRunMaterial)
        {
            Id65V3RuntimeEvidenceDerivedResult pending = BuildDerivedResult(
                Id65V3RuntimeEvidenceState.PendingNoRun,
                sourceValid: true,
                runtimeProfileValid: false);
            return new(Id65V3RuntimeEvidenceState.PendingNoRun, pending);
        }

        bool artifactReadbackValid = mode == Id65V3RuntimeEvidenceValidationMode.FullReadback &&
            artifactBytes.Count == package.ArtifactCatalog.Length;
        bool sessionComplete = ValidateSessionSet(package.Sessions, out string? sessionError);
        if (sessionError is not null)
            throw new InvalidDataException(sessionError);
        bool checkComplete = ValidateCheckSet(package.VisualChecks, out string? checkError);
        if (checkError is not null)
            throw new InvalidDataException(checkError);
        bool retailSetComplete = ValidateRetailSet(package.RetailControls, out string? retailError);
        if (retailError is not null)
            throw new InvalidDataException(retailError);
        ValidateSessionArtifactReferenceShape(package);
        ValidateVisualEvidenceShape(package);
        ValidateChecklistEvidenceSemantics(package);
        ValidateRetailEvidenceShape(package);
        ValidateArtifactOwnership(package);

        bool profileArtifactsValid = artifactReadbackValid &&
            ValidateRuntimeProfileArtifacts(package, artifactBytes, mode, readbackContext);
        bool profileValid = profileObservationComplete &&
            RuntimeProfileObservationIsExact(package.RuntimeProfile) && profileArtifactsValid;
        bool discIdentityValid = artifactReadbackValid &&
            ValidateCaptureJournalArtifacts(
                package,
                artifactBytes,
                mode,
                readbackContext,
                artifactRepositoryRoot);

        bool telemetryComplete = sessionComplete && artifactReadbackValid;
        bool telemetryRejected = false;
        if (artifactReadbackValid)
        {
            foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
            {
                if (session.WatcherArtifactId is null ||
                    !artifactBytes.TryGetValue(session.WatcherArtifactId, out byte[]? bytes))
                {
                    telemetryComplete = false;
                    continue;
                }
                Id65V3RuntimeEvidenceArtifact watcherArtifact =
                    RequireArtifact(package, session.WatcherArtifactId, "watcher-stdout");
                string text = DecodeStrictUtf8(bytes, $"watcher artifact {session.WatcherArtifactId}");
                Id65V3TelemetryParseResult parsed = ParseTelemetryLog(text);
                ValidateTelemetryTimeBinding(session, watcherArtifact, parsed);
                Id65V3TelemetrySummary recomputed = RecomputeTelemetrySummary(parsed, session);
                if (session.SampleSummary is null)
                {
                    telemetryComplete = false;
                }
                else if (SerializeCanonicalValue(session.SampleSummary) != SerializeCanonicalValue(recomputed))
                {
                    throw new InvalidDataException($"Session {session.SessionId} telemetry summary differs from raw-log recomputation.");
                }

                Id65V3TelemetryTuple? landing = session.LandingSampleOrdinal is int landingOrdinal &&
                    landingOrdinal >= 0 && landingOrdinal < parsed.Samples.Length
                        ? parsed.Samples[landingOrdinal]
                        : null;
                bool landingAccepted = landing is { Lives: 4, DeathState: 0, PlayerZ: >= 1024 };
                telemetryComplete &= landingAccepted;
                telemetryRejected |= landing is not null && !landingAccepted;

                bool candidateDeathSession = session.SessionId is
                    "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2";
                if (candidateDeathSession)
                {
                    Id65V3DeathEpisodeSummary? death = recomputed.DeathEpisode;
                    bool deathAccepted = CandidateDeathTelemetryIsAccepted(
                        parsed,
                        session,
                        death);
                    telemetryComplete &= deathAccepted;
                    telemetryRejected |= death is not null && !deathAccepted;
                }
                if (session.SessionId == "candidate-primary-edge-0")
                {
                    Id65V3StableHoldSummary? hold = recomputed.StableHold;
                    telemetryComplete &= hold is not null && hold.DurationMilliseconds >= 20_000 &&
                        hold.SampleCount >= 190 && hold.MaximumGapMilliseconds <= 250 &&
                        hold.AllLivesFour && hold.AllStateZero && hold.AllZAtLeast1024;
                    telemetryRejected |= hold is not null &&
                        (!hold.AllLivesFour || !hold.AllStateZero || !hold.AllZAtLeast1024);
                }
                if (session.SessionId == "candidate-cold-reset")
                {
                    bool spontaneousDeath = !CandidateColdResetTelemetryIsAccepted(
                        parsed,
                        session);
                    telemetryRejected |= spontaneousDeath;
                    telemetryComplete &= !spontaneousDeath;
                }
                telemetryComplete &= recomputed.CleanStopObserved && recomputed.ParseErrors == 0 &&
                    recomputed.MaximumGapMilliseconds <= 250;
            }
        }

        bool explicitFailure = package.VisualChecks.Any(check => check.Status == Id65V3RuntimeEvidenceCheckStatus.Failed) ||
            package.RetailControls.Any(control => control.Status == Id65V3RuntimeEvidenceCheckStatus.Failed) ||
            package.ColdReset.Status == Id65V3RuntimeEvidenceCheckStatus.Failed;
        bool visualComplete = checkComplete && package.VisualChecks.All(CheckHasCompletePassedEvidence);
        bool retailComplete = retailSetComplete && package.RetailControls.All(control =>
            control.Status == Id65V3RuntimeEvidenceCheckStatus.Passed &&
            control.CheckIds.All(id => package.VisualChecks.Single(check => check.CheckId == id).Status ==
                Id65V3RuntimeEvidenceCheckStatus.Passed));
        bool coldResetComplete = ValidateColdReset(package);

        Id65V3RuntimeEvidenceState state;
        bool complete = mode == Id65V3RuntimeEvidenceValidationMode.FullReadback && sourceFilesValid &&
            pinnedRuntimeFilesReadbackValid && profileValid &&
            artifactReadbackValid && discIdentityValid && telemetryComplete && visualComplete && coldResetComplete &&
            retailComplete && package.RecordedAtUtc is not null;
        if (explicitFailure || telemetryRejected)
            state = Id65V3RuntimeEvidenceState.RuntimeRejected;
        else if (complete)
            state = Id65V3RuntimeEvidenceState.FocusedRuntimePass;
        else
            state = Id65V3RuntimeEvidenceState.PendingIncomplete;

        Id65V3RuntimeEvidenceDerivedResult derived = BuildDerivedResult(
            state,
            sourceValid: sourceFilesValid,
            pinnedRuntimeFilesReadbackValid,
            runtimeProfileValid: profileValid,
            artifactReadbackValid,
            discIdentityValid,
            telemetryValid: telemetryComplete,
            visualChecklistComplete: visualComplete,
            coldResetValid: coldResetComplete,
            retailControlsValid: retailComplete);
        return new(state, derived);
    }

    internal static bool CandidateDeathTelemetryIsAccepted(
        Id65V3TelemetryParseResult parsed,
        Id65V3RuntimeEvidenceSession session,
        Id65V3DeathEpisodeSummary? death)
    {
        if (session.LandingSampleOrdinal is not int landingOrdinal ||
            session.FallStartSampleOrdinal is not int fallOrdinal ||
            session.RespawnSampleOrdinal is not int respawnOrdinal ||
            death is not
            {
                PreFallSample: { Lives: 4, DeathState: 0, PlayerZ: >= 1024 },
                FirstZBelow1024: { } below,
                FirstLivesThree: { } livesThree,
                FirstStateFour: { } stateFour,
                FirstPostRespawnStable: { } postRespawn
            } ||
            landingOrdinal < 0 || fallOrdinal <= landingOrdinal ||
            respawnOrdinal <= fallOrdinal || respawnOrdinal >= parsed.Samples.Length ||
            death.PreFallSample.SampleOrdinal != fallOrdinal - 1 ||
            below.SampleOrdinal != fallOrdinal ||
            livesThree.SampleOrdinal != fallOrdinal ||
            stateFour.SampleOrdinal != fallOrdinal ||
            postRespawn.SampleOrdinal <= fallOrdinal ||
            postRespawn.SampleOrdinal > respawnOrdinal ||
            postRespawn.ElapsedMilliseconds < stateFour.ElapsedMilliseconds ||
            postRespawn.ElapsedMilliseconds - stateFour.ElapsedMilliseconds >
                MaximumPostDeathRecoveryMilliseconds)
        {
            return false;
        }

        Id65V3TelemetryTuple[] postLandingBeforeFall = parsed.Samples[landingOrdinal..fallOrdinal];
        Id65V3TelemetryTuple[] deathBeforeRecovery =
            parsed.Samples[fallOrdinal..postRespawn.SampleOrdinal];
        Id65V3TelemetryTuple[] recoveryThroughCleanStop =
            parsed.Samples[postRespawn.SampleOrdinal..];
        string[] scopedLivesTransitions = parsed.Samples[landingOrdinal..]
            .Zip(parsed.Samples[(landingOrdinal + 1)..])
            .Where(pair => pair.First.Lives != pair.Second.Lives)
            .Select(pair => $"{pair.First.Lives}->{pair.Second.Lives}")
            .ToArray();
        return postLandingBeforeFall.All(item =>
                item is { Lives: 4, DeathState: 0, PlayerZ: >= 1024 }) &&
            parsed.Samples[fallOrdinal] is { Lives: 3, DeathState: 4, PlayerZ: < 1024 } &&
            deathBeforeRecovery.All(item =>
                item.Lives == 3 && item.DeathState is 0 or 4) &&
            recoveryThroughCleanStop.All(item =>
                item is { Lives: 3, DeathState: 0, PlayerZ: >= 1024 }) &&
            scopedLivesTransitions.SequenceEqual(["4->3"]);
    }

    internal static bool CandidateColdResetTelemetryIsAccepted(
        Id65V3TelemetryParseResult parsed,
        Id65V3RuntimeEvidenceSession session) =>
        session.LandingSampleOrdinal is int landingOrdinal &&
        landingOrdinal >= 0 && landingOrdinal < parsed.Samples.Length &&
        parsed.Samples[landingOrdinal..].All(item =>
            item is { Lives: 4, DeathState: 0, PlayerZ: >= 1024 });

    private static void ValidateStaticPackage(Id65V3RuntimeEvidencePackage package)
    {
        if (package.SourceIdentity is null || package.RuntimeProfile is null ||
            package.ArtifactCatalog is null || package.Sessions is null ||
            package.VisualChecks is null || package.ColdReset is null ||
            package.RetailControls is null || package.DerivedResult is null ||
            package.ScopeBoundary is null || package.Integrity is null ||
            package.ArtifactCatalog.Any(item => item is null) ||
            package.Sessions.Any(item => item is null) ||
            package.VisualChecks.Any(item => item is null) ||
            package.RetailControls.Any(item => item is null))
        {
            throw new InvalidDataException("Runtime evidence root graph contains a missing object or array member.");
        }
        ValidateNestedGraph(package);
        if (package.Format != Format || package.FormatVersion != FormatVersion)
            throw new InvalidDataException("Runtime evidence format identity changed.");
        if (string.IsNullOrWhiteSpace(package.EvidenceId) || package.EvidenceId.Length > 160)
            throw new InvalidDataException("Runtime evidence ID is missing or oversized.");
        if (package.EvidenceId.StartsWith("id65-v2-", StringComparison.Ordinal) ||
            package.EvidenceId.Contains(
                "unused-level-65-remote-blank-collision-winding-repair-v2-runtime-evidence",
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "A retired V2 candidate evidence ID cannot identify V3 runtime evidence.");
        }
        if (package.RecordedAtUtc is not null)
            _ = ParseCanonicalUtc(package.RecordedAtUtc, "recordedAtUtc");
        Id65V3RuntimeEvidenceSourceIdentity expectedSource = BuildExpectedSourceIdentity();
        if (SerializeCanonicalValue(package.SourceIdentity) != SerializeCanonicalValue(expectedSource) ||
            RecomputeSourceClosureSha256(package.SourceIdentity) != ExpectedSourceClosureSha256)
        {
            throw new InvalidDataException("Pinned V3 candidate source identity or source closure changed.");
        }
        ValidateRuntimeProfileIdentity(package.RuntimeProfile);
        if (SerializeCanonicalValue(package.ScopeBoundary) != SerializeCanonicalValue(BuildScopeBoundary()))
            throw new InvalidDataException("Runtime evidence scope boundary changed.");
        ValidateCaptureProvenanceAndOperatorReview(package);
        ValidateAllCanonicalTimestamps(package);
    }

    private static void ValidateCaptureProvenanceAndOperatorReview(
        Id65V3RuntimeEvidencePackage package)
    {
        bool hasCaptureMaterial = package.ArtifactCatalog.Length != 0 ||
            package.Sessions.Length != 0 || package.VisualChecks.Length != 0 ||
            package.RetailControls.Length != 0 || package.RecordedAtUtc is not null ||
            package.ColdReset.Status != Id65V3RuntimeEvidenceCheckStatus.NotObserved ||
            package.ColdReset.PrimaryProcessExitedAtUtc is not null ||
            package.ColdReset.ResetProcessStartedAtUtc is not null;
        if ((package.CaptureProvenance is null) != (package.OperatorReview is null))
        {
            throw new InvalidDataException(
                "Capture provenance and operator review must be both absent or both present.");
        }
        if (!hasCaptureMaterial)
        {
            if (package.CaptureProvenance is not null || package.OperatorReview is not null)
            {
                throw new InvalidDataException(
                    "A pending no-run package cannot carry capture provenance or operator review.");
            }
            return;
        }
        Id65V3RuntimeEvidenceCaptureProvenance provenance = package.CaptureProvenance ??
            throw new InvalidDataException("Captured run material lacks durable capture provenance.");
        Id65V3RuntimeEvidenceOperatorReview review = package.OperatorReview ??
            throw new InvalidDataException("Captured run material lacks durable operator review.");
        if (!CaptureRunIdRegex().IsMatch(provenance.RunId) ||
            provenance.RunOriginRepoRelativePath != RuntimeAssetPrefix + "capture-staging/" +
                provenance.RunId + "/run-origin.json" ||
            !Sha256Regex().IsMatch(provenance.RunOriginSha256) ||
            provenance.RunOriginByteLength <= 0 ||
            !Sha256Regex().IsMatch(provenance.FinalTerminalEventSha256) ||
            !Sha256Regex().IsMatch(provenance.CaptureClosureSha256) ||
            provenance.LiveEventProjections is null ||
            provenance.LiveEventProjections.Any(item => item is null) ||
            provenance.LiveEventProjections.Length != SessionIds.Length)
        {
            throw new InvalidDataException("Capture provenance has an invalid identity or live-projection set.");
        }
        for (int ordinal = 0; ordinal < SessionIds.Length; ordinal++)
        {
            Id65V3RuntimeEvidenceLiveProjection live = provenance.LiveEventProjections[ordinal];
            string sessionId = SessionIds[ordinal];
            string expectedPath = RuntimeAssetPrefix + "capture-staging/" + provenance.RunId + "/" +
                ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + sessionId +
                "/capture-events-live.ndjson";
            if (live.SessionOrdinal != ordinal || live.SessionId != sessionId ||
                live.RepoRelativePath != expectedPath || !Sha256Regex().IsMatch(live.Sha256) ||
                live.ByteLength <= 0 || !Sha256Regex().IsMatch(live.TerminalEventSha256))
            {
                throw new InvalidDataException(
                    $"Capture live projection {ordinal} has an invalid durable identity.");
            }
            GuardArtifactRelativePath("/", live.RepoRelativePath);
        }

        if (!Sha256Regex().IsMatch(review.AttestationSha256) ||
            string.IsNullOrWhiteSpace(review.Operator) || review.Operator.Length > 128 ||
            review.Operator != review.Operator.Trim() || review.Operator.Any(char.IsControl) ||
            review.AttestedAtUtc is null || review.RunId != provenance.RunId ||
            review.RunOriginSha256 != provenance.RunOriginSha256 ||
            review.FinalTerminalEventSha256 != provenance.FinalTerminalEventSha256 ||
            review.CaptureClosureSha256 != provenance.CaptureClosureSha256 ||
            review.RuntimeSafety is null || review.Sessions is null || review.Checks is null ||
            review.Sessions.Any(item => item is null) || review.Checks.Any(item => item is null) ||
            review.Sessions.Length != SessionIds.Length || review.Checks.Length != AllCheckIds.Length)
        {
            throw new InvalidDataException("Operator review has an invalid durable capture binding.");
        }
        _ = ParseCanonicalUtc(review.AttestedAtUtc, "operator-review attestation time");
        if (package.RecordedAtUtc != review.AttestedAtUtc)
            throw new InvalidDataException("recordedAtUtc differs from the operator-review attestation time.");
        ValidateReviewStatement(review.RuntimeSafety.Statement, "runtime safety");
        if (review.RuntimeSafety.SaveStateLoaded || review.RuntimeSafety.SaveStateCreated ||
            review.RuntimeSafety.InGameSaveUsed || review.RuntimeSafety.ActiveCheatCount != 0)
        {
            throw new InvalidDataException("Operator review reports an unsafe runtime-safety observation.");
        }
        for (int ordinal = 0; ordinal < SessionIds.Length; ordinal++)
        {
            Id65V3RuntimeEvidenceOperatorSessionReview item = review.Sessions[ordinal];
            if (item.SessionId != SessionIds[ordinal])
                throw new InvalidDataException("Operator session reviews are not the exact ordered seven sessions.");
            ValidateReviewStatement(item.Statement, $"session {item.SessionId}");
            if (!item.Statement.Contains(item.SessionId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Operator session review {item.SessionId} is a blanket statement without its exact session ID.");
            }
            Id65V3RuntimeEvidenceSession? session = package.Sessions.SingleOrDefault(value =>
                value.SessionId == item.SessionId);
            if (session is null || item.NewGame != session.NewGame || item.CardsNone != session.CardsNone ||
                item.CheatsOff != session.CheatsOff ||
                item.SaveStateLoaded != session.SaveStateLoaded ||
                item.SaveStateCreated != session.SaveStateCreated ||
                item.InGameSaveUsed != session.InGameSaveUsed)
            {
                throw new InvalidDataException(
                    $"Operator session review {item.SessionId} differs from the typed session.");
            }
        }
        for (int index = 0; index < AllCheckIds.Length; index++)
        {
            Id65V3RuntimeEvidenceOperatorCheckReview item = review.Checks[index];
            if (item.CheckId != AllCheckIds[index] ||
                item.Status == Id65V3RuntimeEvidenceCheckStatus.NotObserved ||
                !Sha256Regex().IsMatch(item.EvidenceBindingSha256))
            {
                throw new InvalidDataException("Operator check reviews are not the exact ordered observed matrix.");
            }
            ValidateReviewStatement(item.Statement, $"check {item.CheckId}");
            if (!item.Statement.Contains(item.CheckId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Operator check review {item.CheckId} is a blanket statement without its exact check ID.");
            }
            string? requiredEdgeToken = RequiredEdgeAssignmentToken(item.CheckId);
            if (requiredEdgeToken is not null &&
                !item.Statement.Contains(requiredEdgeToken, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Operator check review {item.CheckId} must include exact edge-assignment token " +
                    $"'{requiredEdgeToken}'.");
            }
            Id65V3RuntimeEvidenceCheck? check = package.VisualChecks.SingleOrDefault(value =>
                value.CheckId == item.CheckId);
            if (check is null || item.SessionId != check.SessionId || item.Status != check.Status ||
                item.Statement != check.Attestation ||
                item.EvidenceBindingSha256 !=
                    Id65V3RuntimeEvidenceFinalizer.RecomputeEvidenceBindingSha256(
                        provenance.CaptureClosureSha256,
                        check,
                        package.ArtifactCatalog))
            {
                throw new InvalidDataException(
                    $"Operator check review {item.CheckId} differs from its typed evidence binding.");
            }
        }
        if (package.RuntimeProfile.SaveStateLoaded != review.RuntimeSafety.SaveStateLoaded ||
            package.RuntimeProfile.SaveStateCreated != review.RuntimeSafety.SaveStateCreated ||
            package.RuntimeProfile.InGameSaveUsed != review.RuntimeSafety.InGameSaveUsed ||
            package.RuntimeProfile.ActiveCheatCount != review.RuntimeSafety.ActiveCheatCount)
        {
            throw new InvalidDataException("Operator runtime-safety review differs from the observed profile.");
        }
        if (package.DeclaredState == Id65V3RuntimeEvidenceState.FocusedRuntimePass &&
            package.EvidenceId != "id65-v3-focused-runtime-pass-attestation-" + review.AttestationSha256)
        {
            throw new InvalidDataException(
                "Focused-pass evidence ID does not preserve the full external-attestation SHA-256.");
        }
        if (review.AttestationSha256 !=
            Id65V3RuntimeEvidenceFinalizer.RecomputeEmbeddedAttestationSha256(review))
        {
            throw new InvalidDataException(
                "Operator-review attestation SHA-256 does not match its reconstructed exact external attestation.");
        }
    }

    private static void ValidateReviewStatement(string value, string context)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length is < 16 or > 512 ||
            value != value.Trim() || value.Any(char.IsControl))
        {
            throw new InvalidDataException($"Operator-review {context} statement is not bounded clean text.");
        }
    }

    private static void ValidateNestedGraph(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceProfile profile = package.RuntimeProfile;
        if (profile.OtherPadTypes is null || profile.OtherPadTypes.Any(value => value is null) ||
            profile.SafetyDirectories is null ||
            profile.SafetyDirectories.Any(value => value is null) ||
            (profile.ChangedProfilePaths is not null && profile.ChangedProfilePaths.Any(value => value is null)))
        {
            throw new InvalidDataException("Runtime profile contains a null nested array or string member.");
        }

        foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
        {
            if (session.SessionId is null || session.ProcessInstanceId is null || session.LoadCode is null)
                throw new InvalidDataException("Runtime session contains a null identity member.");
            if (session.CaptureJournal is { } journal &&
                (journal.ArtifactId is null || journal.Format is null || journal.RunId is null ||
                 journal.SessionId is null || journal.BootIdentity is null ||
                 journal.ProcessInstanceId is null || journal.TerminalEventSha256 is null ||
                 journal.SessionOrdinal < 0))
            {
                throw new InvalidDataException(
                    $"Session {session.SessionId} capture-journal reference has a null or invalid member.");
            }
            if (session.SampleSummary is not null)
            {
                if (session.WatcherArtifactId is null)
                    throw new InvalidDataException($"Session {session.SessionId} summary is not bound to a watcher artifact.");
                ValidateTelemetrySummaryShape(session.SampleSummary, session.SessionId);
            }
        }

        foreach (Id65V3RuntimeEvidenceCheck check in package.VisualChecks)
        {
            if (check.CheckId is null || check.SessionId is null || check.ArtifactRefs is null ||
                check.ArtifactRefs.Any(reference => reference is null || reference.ArtifactId is null))
            {
                throw new InvalidDataException("Visual check contains a null identity or artifact-reference member.");
            }
        }
        foreach (Id65V3RuntimeEvidenceRetailControl control in package.RetailControls)
        {
            if (control.ControlId is null || control.LoadCode is null || control.CheckIds is null ||
                control.CheckIds.Any(value => value is null))
            {
                throw new InvalidDataException("Retail control contains a null nested member.");
            }
        }
    }

    private static void ValidateTelemetrySummaryShape(Id65V3TelemetrySummary summary, string sessionId)
    {
        if (summary.WatcherSha256 != ExpectedWatcherSha256 || summary.PeriodMilliseconds != 100 ||
            !summary.NearContemporaneousNotAtomic || summary.TotalSamples <= 0 || summary.ParseErrors != 0 ||
            summary.MaximumGapMilliseconds < 0 || summary.LivesValues is null ||
            summary.LivesTransitions is null || summary.StateValues is null ||
            summary.LivesTransitions.Any(value => value is null) ||
            summary.StateFourSamples < 0 || summary.StateFiveSamples < 0 || summary.BelowPlaneSamples < 0 ||
            summary.FirstAtUtc is null || summary.LastAtUtc is null ||
            summary.FirstTuple is null || summary.LastTuple is null)
        {
            throw new InvalidDataException($"Session {sessionId} telemetry summary has an invalid nested shape.");
        }
        _ = ParseCanonicalUtc(summary.FirstAtUtc, $"session {sessionId} first sample");
        _ = ParseCanonicalUtc(summary.LastAtUtc, $"session {sessionId} last sample");
        if (!summary.LivesValues.SequenceEqual(summary.LivesValues.Distinct().Order()) ||
            !summary.StateValues.SequenceEqual(summary.StateValues.Distinct().Order()) ||
            summary.LivesTransitions.Any(value => !Regex.IsMatch(
                value,
                "^(?:0|[1-9]\\d*)->(?:0|[1-9]\\d*)$",
                RegexOptions.CultureInvariant)))
        {
            throw new InvalidDataException($"Session {sessionId} telemetry value sets or transitions are noncanonical.");
        }

        ValidateTelemetryTupleShape(summary.FirstTuple, sessionId);
        ValidateTelemetryTupleShape(summary.LastTuple, sessionId);
        if (summary.StableHold is not null)
        {
            Id65V3StableHoldSummary hold = summary.StableHold;
            _ = ParseCanonicalUtc(hold.StartAtUtc, $"session {sessionId} hold start");
            _ = ParseCanonicalUtc(hold.EndAtUtc, $"session {sessionId} hold end");
            if (hold.StartSampleOrdinal < 0 || hold.EndSampleOrdinal < hold.StartSampleOrdinal ||
                hold.StartElapsedMilliseconds < 0 ||
                hold.EndElapsedMilliseconds < hold.StartElapsedMilliseconds ||
                hold.DurationMilliseconds != hold.EndElapsedMilliseconds - hold.StartElapsedMilliseconds ||
                hold.SampleCount != hold.EndSampleOrdinal - hold.StartSampleOrdinal + 1 ||
                hold.MaximumGapMilliseconds < 0)
            {
                throw new InvalidDataException($"Session {sessionId} stable-hold summary is arithmetically invalid.");
            }
        }
        if (summary.DeathEpisode is not null)
        {
            foreach (Id65V3TelemetryTuple? tuple in new[]
            {
                summary.DeathEpisode.PreFallSample,
                summary.DeathEpisode.FirstZBelow1024,
                summary.DeathEpisode.FirstLivesThree,
                summary.DeathEpisode.FirstStateFour,
                summary.DeathEpisode.LastStateFour,
                summary.DeathEpisode.FirstPostRespawnStable
            })
            {
                if (tuple is not null)
                    ValidateTelemetryTupleShape(tuple, sessionId);
            }
            if (summary.DeathEpisode.LivesTransitionCount < 0)
                throw new InvalidDataException($"Session {sessionId} death summary has a negative transition count.");
        }
    }

    private static void ValidateTelemetryTupleShape(Id65V3TelemetryTuple tuple, string sessionId)
    {
        if (tuple.AtUtc is null || tuple.SampleOrdinal < 0 || tuple.ElapsedMilliseconds < 0 ||
            tuple.Lives < 0 || tuple.DeathState < 0 || tuple.LivesRaw is null ||
            tuple.DeathStateRaw is null || tuple.PlayerZRaw is null ||
            !Regex.IsMatch(tuple.LivesRaw, "^[0-9a-f]{8}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(tuple.DeathStateRaw, "^[0-9a-f]{8}$", RegexOptions.CultureInvariant) ||
            !Regex.IsMatch(tuple.PlayerZRaw, "^[0-9a-f]{8}$", RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException($"Session {sessionId} telemetry tuple has an invalid nested shape.");
        }
        _ = ParseCanonicalUtc(tuple.AtUtc, $"session {sessionId} tuple");
        if (BinaryPrimitives.ReadUInt32LittleEndian(Convert.FromHexString(tuple.LivesRaw)) != (uint)tuple.Lives ||
            BinaryPrimitives.ReadUInt32LittleEndian(Convert.FromHexString(tuple.DeathStateRaw)) !=
                (uint)tuple.DeathState ||
            BinaryPrimitives.ReadInt32LittleEndian(Convert.FromHexString(tuple.PlayerZRaw)) != tuple.PlayerZ)
        {
            throw new InvalidDataException($"Session {sessionId} telemetry tuple raw words disagree with decoded values.");
        }
    }

    private static void ValidateIntegrity(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceIntegrity expected = ComputeIntegrity(package);
        if (SerializeCanonicalValue(package.Integrity) != SerializeCanonicalValue(expected))
            throw new InvalidDataException("Runtime evidence canonical payload, artifact set, or contract closure hash changed.");
    }

    internal static Id65V3RuntimeEvidencePackage WithRecomputedIntegrity(Id65V3RuntimeEvidencePackage package) =>
        package with { Integrity = ComputeIntegrity(package) };

    private static Id65V3RuntimeEvidenceIntegrity ComputeIntegrity(Id65V3RuntimeEvidencePackage package)
    {
        string source = RecomputeSourceClosureSha256(package.SourceIdentity);
        string profile = RecomputeRuntimeProfileSha256(package.RuntimeProfile);
        string artifacts = HashUtf8(BuildArtifactSetManifest(package.ArtifactCatalog));
        string payload = HashUtf8(SerializeCanonicalPayload(package));
        string contract = HashUtf8(
            "id65-v3-runtime-evidence-contract-v1\n" +
            $"sourceClosureSha256={source}\n" +
            $"runtimeProfileSha256={profile}\n" +
            $"artifactSetSha256={artifacts}\n" +
            $"payloadSha256={payload}\n" +
            $"state={ToKebab(package.DeclaredState)}\n");
        return new(Canonicalization, source, profile, artifacts, payload, contract);
    }

    private static string SerializeCanonicalPayload(Id65V3RuntimeEvidencePackage package)
    {
        var payload = new
        {
            package.Format,
            package.FormatVersion,
            package.EvidenceId,
            package.RecordedAtUtc,
            package.DeclaredState,
            package.SourceIdentity,
            package.RuntimeProfile,
            package.ArtifactCatalog,
            package.Sessions,
            package.VisualChecks,
            package.ColdReset,
            package.RetailControls,
            package.CaptureProvenance,
            package.OperatorReview,
            package.DerivedResult,
            package.ScopeBoundary
        };
        return SerializeCanonicalValue(payload);
    }

    private static void ValidateArtifactCatalogShape(
        string repositoryRoot,
        Id65V3RuntimeEvidenceArtifact[] artifacts)
    {
        if (artifacts is null || artifacts.Length > MaximumArtifactCount)
            throw new InvalidDataException("Runtime evidence artifact catalog is null or oversized.");
        if (artifacts.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
            throw new InvalidDataException("Runtime evidence artifact IDs must be unique.");
        if (artifacts.Select(item => item.RepoRelativePath).Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
            throw new InvalidDataException("Runtime evidence artifact paths must be unique.");
        if (!artifacts.Select(item => item.ArtifactId).SequenceEqual(
                artifacts.Select(item => item.ArtifactId).Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Runtime evidence artifacts must be ordered by artifact ID.");
        }
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts)
        {
            if (artifact.ArtifactId is null || artifact.SessionId is null || artifact.Kind is null ||
                artifact.RepoRelativePath is null ||
                artifact.Sha256 is null || artifact.CapturedAtUtc is null ||
                !ArtifactIdRegex().IsMatch(artifact.ArtifactId) || !AllowedArtifactKinds.Contains(artifact.Kind) ||
                !SessionIds.Contains(artifact.SessionId, StringComparer.Ordinal) ||
                !Sha256Regex().IsMatch(artifact.Sha256) || artifact.ByteLength <= 0)
            {
                throw new InvalidDataException($"Artifact {artifact.ArtifactId} has an invalid identity, kind, hash, or length.");
            }
            GuardArtifactRelativePath(repositoryRoot, artifact.RepoRelativePath);
            DateTimeOffset captured = ParseCanonicalUtc(
                artifact.CapturedAtUtc,
                $"artifact {artifact.ArtifactId} capture");
            if (artifact.Kind == "screen-recording" &&
                (artifact.MediaStartAtUtc is null || artifact.MediaEndAtUtc is null))
                throw new InvalidDataException($"Recording {artifact.ArtifactId} lacks both media boundaries.");
            if (artifact.Kind == "frame" &&
                (artifact.MediaStartAtUtc is not null || artifact.MediaEndAtUtc is not null))
                throw new InvalidDataException($"Frame {artifact.ArtifactId} carries forbidden media boundaries.");
            if (artifact.Kind is not ("screen-recording" or "frame") &&
                (artifact.MediaStartAtUtc is not null || artifact.MediaEndAtUtc is not null))
            {
                throw new InvalidDataException(
                    $"Nonrecording artifact {artifact.ArtifactId} carries forbidden media boundaries.");
            }
            if (artifact.MediaStartAtUtc is not null && artifact.MediaEndAtUtc is not null)
            {
                DateTimeOffset start = ParseCanonicalUtc(
                    artifact.MediaStartAtUtc,
                    $"artifact {artifact.ArtifactId} media start");
                DateTimeOffset end = ParseCanonicalUtc(
                    artifact.MediaEndAtUtc,
                    $"artifact {artifact.ArtifactId} media end");
                if (end <= start || artifact.Kind != "screen-recording" || captured < end)
                    throw new InvalidDataException($"Artifact {artifact.ArtifactId} has invalid media boundaries.");
            }
        }
    }

    private static void ValidateRecordedAtBoundary(Id65V3RuntimeEvidencePackage package)
    {
        if (package.RecordedAtUtc is null)
            return;
        List<DateTimeOffset> boundaries = [];
        boundaries.AddRange(package.Sessions
            .Where(session => session.ProcessExitedAtUtc is not null)
            .Select(session => ParseCanonicalUtc(
                session.ProcessExitedAtUtc!,
                $"session {session.SessionId} process exit")));
        boundaries.AddRange(package.ArtifactCatalog.Select(artifact =>
            ParseCanonicalUtc(artifact.CapturedAtUtc, $"artifact {artifact.ArtifactId} capture")));
        boundaries.AddRange(package.ArtifactCatalog
            .Where(artifact => artifact.MediaEndAtUtc is not null)
            .Select(artifact => ParseCanonicalUtc(
                artifact.MediaEndAtUtc!,
                $"artifact {artifact.ArtifactId} media end")));
        if (boundaries.Count == 0)
            throw new InvalidDataException("recordedAtUtc is present without any captured run boundary.");
        DateTimeOffset recorded = ParseCanonicalUtc(package.RecordedAtUtc, "recordedAtUtc");
        if (recorded < boundaries.Max())
            throw new InvalidDataException("recordedAtUtc predates the final process or artifact boundary.");
    }

    private static Dictionary<string, byte[]> ReadAndVerifyArtifacts(
        string repositoryRoot,
        Id65V3RuntimeEvidenceArtifact[] artifacts)
    {
        Dictionary<string, byte[]> result = new(StringComparer.Ordinal);
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts)
        {
            GuardArtifactRelativePath(repositoryRoot, artifact.RepoRelativePath);
            string path = Path.Combine(repositoryRoot, artifact.RepoRelativePath);
            if (!File.Exists(path))
                throw new InvalidDataException($"Runtime evidence artifact {artifact.ArtifactId} is missing.");
            bool textArtifact = artifact.Kind is
                "watcher-stdout" or "capture-event-journal" or
                "runtime-profile-before" or "runtime-profile-after";
            Id65V3RuntimeEvidenceFinalizer.StableFileRead stable =
                Id65V3RuntimeEvidenceFinalizer.ReadStableOwnedFile(
                    path,
                    0x180,
                    textArtifact ? MaximumTelemetryLogBytes : 64L * 1024 * 1024 * 1024,
                    keepBytes: textArtifact);
            if (stable.ByteLength != artifact.ByteLength || stable.Sha256 != artifact.Sha256)
                throw new InvalidDataException($"Runtime evidence artifact {artifact.ArtifactId} identity changed.");
            byte[] bytes;
            if (textArtifact)
            {
                if (artifact.ByteLength > MaximumTelemetryLogBytes)
                    throw new InvalidDataException($"Text artifact {artifact.ArtifactId} exceeds the bounded size.");
                bytes = stable.Bytes ??
                    throw new InvalidDataException($"Text artifact {artifact.ArtifactId} bytes are unavailable.");
            }
            else
            {
                using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                ValidateVisualMediaArtifact(artifact, stream);
                Id65V3RuntimeEvidenceFinalizer.StableFileRead stableAfter =
                    Id65V3RuntimeEvidenceFinalizer.ReadStableOwnedFile(
                        path,
                        0x180,
                        64L * 1024 * 1024 * 1024,
                        keepBytes: false);
                if (stableAfter != stable)
                    throw new InvalidDataException(
                        $"Runtime evidence artifact {artifact.ArtifactId} changed around media validation.");
                bytes = [];
            }
            result.Add(artifact.ArtifactId, bytes);
        }
        return result;
    }

    private static void ValidateVisualMediaArtifact(
        Id65V3RuntimeEvidenceArtifact artifact,
        FileStream stream)
    {
        stream.Position = 0;
        if (artifact.Kind == "frame")
            ValidatePngContainer(artifact.ArtifactId, stream);
        else if (artifact.Kind == "screen-recording")
            ValidateIsoBmffContainer(artifact.ArtifactId, stream);
        else
            throw new InvalidDataException($"Binary artifact {artifact.ArtifactId} has an unsupported media kind.");
    }

    private static void ValidatePngContainer(string artifactId, Stream stream)
    {
        ReadOnlySpan<byte> expectedSignature = [137, 80, 78, 71, 13, 10, 26, 10];
        Span<byte> signature = stackalloc byte[8];
        if (stream.Length < 8)
            throw new InvalidDataException($"Frame {artifactId} is shorter than the PNG signature.");
        stream.ReadExactly(signature);
        if (!signature.SequenceEqual(expectedSignature))
            throw new InvalidDataException($"Frame {artifactId} does not have the exact PNG signature.");

        bool sawIhdr = false;
        bool sawIdat = false;
        bool sawIend = false;
        int chunkCount = 0;
        Span<byte> header = stackalloc byte[8];
        Span<byte> storedCrcBytes = stackalloc byte[4];
        byte[] buffer = new byte[64 * 1024];
        while (stream.Position < stream.Length)
        {
            if (++chunkCount > 100_000 || stream.Length - stream.Position < 12)
                throw new InvalidDataException($"Frame {artifactId} has an invalid or excessive PNG chunk table.");
            stream.ReadExactly(header);
            uint dataLength = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            string chunkType = Encoding.ASCII.GetString(header[4..8]);
            long remaining = stream.Length - stream.Position;
            if (dataLength > 64 * 1024 * 1024 || remaining < checked((long)dataLength + 4))
                throw new InvalidDataException($"Frame {artifactId} has an out-of-bounds PNG chunk.");
            if (chunkCount == 1 && (chunkType != "IHDR" || dataLength != 13))
                throw new InvalidDataException($"Frame {artifactId} does not start with a 13-byte PNG IHDR.");
            if (chunkType == "IHDR" && (sawIhdr || chunkCount != 1 || dataLength != 13))
                throw new InvalidDataException($"Frame {artifactId} has a duplicate or malformed PNG IHDR.");
            if (chunkType == "IEND" && dataLength != 0)
                throw new InvalidDataException($"Frame {artifactId} has a nonempty PNG IEND.");

            uint crc = 0xFFFFFFFF;
            crc = UpdatePngCrc(crc, header[4..8]);
            byte[]? ihdr = chunkType == "IHDR" ? new byte[13] : null;
            long unread = dataLength;
            int copied = 0;
            while (unread > 0)
            {
                int count = checked((int)Math.Min(unread, buffer.Length));
                stream.ReadExactly(buffer.AsSpan(0, count));
                crc = UpdatePngCrc(crc, buffer.AsSpan(0, count));
                if (ihdr is not null)
                {
                    buffer.AsSpan(0, count).CopyTo(ihdr.AsSpan(copied));
                    copied += count;
                }
                unread -= count;
            }
            stream.ReadExactly(storedCrcBytes);
            uint storedCrc = BinaryPrimitives.ReadUInt32BigEndian(storedCrcBytes);
            if ((crc ^ 0xFFFFFFFF) != storedCrc)
                throw new InvalidDataException($"Frame {artifactId} has a PNG chunk CRC mismatch.");

            if (chunkType == "IHDR")
            {
                sawIhdr = true;
                uint width = BinaryPrimitives.ReadUInt32BigEndian(ihdr!.AsSpan(0, 4));
                uint height = BinaryPrimitives.ReadUInt32BigEndian(ihdr.AsSpan(4, 4));
                if (width == 0 || height == 0)
                    throw new InvalidDataException($"Frame {artifactId} has zero PNG dimensions.");
            }
            else if (chunkType == "IDAT")
            {
                if (!sawIhdr || dataLength == 0)
                    throw new InvalidDataException($"Frame {artifactId} has an empty or misplaced PNG IDAT.");
                sawIdat = true;
            }
            else if (chunkType == "IEND")
            {
                sawIend = true;
                if (stream.Position != stream.Length)
                    throw new InvalidDataException($"Frame {artifactId} has bytes after the final PNG IEND.");
            }
            if (sawIend)
                break;
        }
        if (!sawIhdr || !sawIdat || !sawIend)
            throw new InvalidDataException($"Frame {artifactId} lacks required PNG IHDR, IDAT, or final IEND chunks.");
    }

    private static uint UpdatePngCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (byte value in bytes)
            crc = PngCrcTable[checked((int)((crc ^ value) & 0xFF))] ^ (crc >> 8);
        return crc;
    }

    private static uint[] BuildPngCrcTable()
    {
        uint[] table = new uint[256];
        for (uint index = 0; index < table.Length; index++)
        {
            uint value = index;
            for (int bit = 0; bit < 8; bit++)
                value = (value & 1) != 0 ? 0xEDB88320 ^ (value >> 1) : value >> 1;
            table[index] = value;
        }
        return table;
    }

    private static readonly uint[] PngCrcTable = BuildPngCrcTable();

    private static void ValidateIsoBmffContainer(string artifactId, Stream stream)
    {
        if (stream.Length < 16)
            throw new InvalidDataException($"Recording {artifactId} is shorter than a bounded ISO-BMFF ftyp box.");
        bool first = true;
        bool recognizedBrand = false;
        bool sawMoov = false;
        bool sawMdat = false;
        int boxCount = 0;
        Span<byte> header = stackalloc byte[16];
        while (stream.Position < stream.Length)
        {
            if (++boxCount > 1_000_000 || stream.Length - stream.Position < 8)
                throw new InvalidDataException($"Recording {artifactId} has an invalid or excessive ISO-BMFF box table.");
            long boxStart = stream.Position;
            stream.ReadExactly(header[..8]);
            uint size32 = BinaryPrimitives.ReadUInt32BigEndian(header[..4]);
            string boxType = Encoding.ASCII.GetString(header[4..8]);
            long headerLength = 8;
            ulong boxSize;
            if (size32 == 1)
            {
                if (stream.Length - stream.Position < 8)
                    throw new InvalidDataException($"Recording {artifactId} has a truncated 64-bit ISO-BMFF box.");
                stream.ReadExactly(header[..8]);
                boxSize = BinaryPrimitives.ReadUInt64BigEndian(header[..8]);
                headerLength = 16;
            }
            else if (size32 == 0)
            {
                boxSize = checked((ulong)(stream.Length - boxStart));
            }
            else
            {
                boxSize = size32;
            }
            if (boxSize < (ulong)headerLength || boxSize > long.MaxValue ||
                boxStart > stream.Length - (long)boxSize)
            {
                throw new InvalidDataException($"Recording {artifactId} has an out-of-bounds ISO-BMFF box.");
            }
            long boxEnd = checked(boxStart + (long)boxSize);
            long payloadLength = checked((long)boxSize - headerLength);
            if (first)
            {
                if (boxType != "ftyp" || payloadLength < 8 || payloadLength > 1_048_576 ||
                    (payloadLength - 8) % 4 != 0)
                {
                    throw new InvalidDataException(
                        $"Recording {artifactId} does not start with a bounded ISO-BMFF ftyp box.");
                }
                byte[] brands = new byte[checked((int)payloadLength)];
                stream.ReadExactly(brands);
                recognizedBrand = IsRecognizedIsoBmffBrand(Encoding.ASCII.GetString(brands, 0, 4));
                for (int offset = 8; offset < brands.Length; offset += 4)
                    recognizedBrand |= IsRecognizedIsoBmffBrand(Encoding.ASCII.GetString(brands, offset, 4));
                first = false;
            }
            else
            {
                if (boxType == "moov" && payloadLength > 0)
                    sawMoov = true;
                if (boxType == "mdat" && payloadLength > 0)
                    sawMdat = true;
                stream.Position = boxEnd;
            }
            if (stream.Position != boxEnd)
                stream.Position = boxEnd;
        }
        if (first || !recognizedBrand || !sawMoov || !sawMdat)
        {
            throw new InvalidDataException(
                $"Recording {artifactId} lacks a recognized ISO-BMFF brand or nonempty top-level moov and mdat boxes.");
        }
    }

    private static bool IsRecognizedIsoBmffBrand(string brand) => brand is
        "isom" or "iso2" or "iso3" or "iso4" or "iso5" or "iso6" or
        "mp41" or "mp42" or "avc1" or "M4V " or "qt  ";

    private static Id65V3RuntimeEvidenceReadbackContext BuildExpectedReadbackContext(string repositoryRoot)
    {
        string sourceRoot = Path.GetFullPath(repositoryRoot);
        string packageRoot = Path.Combine(sourceRoot, ProfilePackageRelativePath);
        string dataRoot = Path.Combine(sourceRoot, ExternalHomeDataRelativePath);
        return new(
            sourceRoot,
            packageRoot,
            Path.Combine(sourceRoot, DuckStationExecutableRelativePath),
            Path.Combine(sourceRoot, HandoffReceiptRelativePath),
            Path.Combine(sourceRoot, ExternalHomeRelativePath),
            dataRoot,
            Path.Combine(dataRoot, "settings.ini"),
            Path.Combine(dataRoot, ExternalHomeBiosManifestPath.Replace('/', Path.DirectorySeparatorChar)),
            HostNormalSettingsAbsolutePath);
    }

    private static bool VerifyPinnedRuntimeFiles(
        Id65V3RuntimeEvidenceProfile profile,
        Id65V3RuntimeEvidenceReadbackContext context)
    {
        string sourceRoot = RequireAbsoluteDirectory(context.SourceRepositoryRootPath, "source repository root");
        string packageRoot = RequireAbsoluteDirectory(context.ProfilePackageRootPath, "profile package root");
        string externalHome = RequireAbsoluteDirectory(context.ExternalHomeRootPath, "external HOME root");
        string dataRoot = RequireAbsoluteDirectory(context.ExternalHomeDataRootPath, "external-HOME data root");
        _ = sourceRoot;
        RequireExactDescendant(packageRoot, context.DuckStationExecutablePath,
            "DuckStation-ID65-Unattended-v1.app/Contents/MacOS/DuckStation", "DuckStation executable");
        RequireExactDescendant(packageRoot, context.HandoffReceiptPath,
            "handoff-receipt.json", "handoff receipt");
        RequireExactDescendant(packageRoot, externalHome, "home", "external HOME root");
        RequireExactDescendant(externalHome, dataRoot,
            "Library/Application Support/DuckStation", "external-HOME data root");
        RequireExactDescendant(dataRoot, context.ExternalHomeSettingsPath,
            profile.ExternalHomeSettingsRelativePath, "external-HOME settings");
        RequireExactDescendant(dataRoot, context.ExternalHomeBiosPath,
            profile.ExternalHomeBiosRelativePath, "external-HOME BIOS");
        (string Path, long ByteLength, string Hash, string Label)[] pinned =
        [
            (context.DuckStationExecutablePath, profile.DuckStationExecutableByteLength,
                profile.DuckStationExecutableSha256, "DuckStation executable"),
            (context.HandoffReceiptPath, profile.HandoffReceiptByteLength,
                profile.HandoffReceiptSha256, "handoff receipt"),
            (context.ExternalHomeSettingsPath, profile.ExternalHomeSettingsByteLength,
                profile.ExternalHomeSettingsSha256, "external-HOME settings"),
            (context.ExternalHomeBiosPath, profile.ExternalHomeBiosByteLength,
                profile.ExternalHomeBiosSha256, "external-HOME BIOS"),
            (context.HostNormalSettingsPath, profile.HostNormalSettingsByteLength,
                profile.HostNormalSettingsSha256, "host normal-user settings sentinel")
        ];
        foreach ((string path, long expectedLength, string expectedHash, string label) in pinned)
        {
            string full = RequireAbsoluteFile(path, label);
            FileIdentity identity = ReadFileIdentity(full, label);
            if (identity.ByteLength != expectedLength || identity.Sha256 != expectedHash)
                throw new InvalidDataException($"Current {label} bytes differ from the pinned runtime profile.");
        }

        foreach (string safetyDirectory in SafetyDirectoryNames)
        {
            string path = Path.Combine(dataRoot, safetyDirectory);
            string trusted = RequireAbsoluteDirectory(
                path,
                $"external-HOME safety directory '{safetyDirectory}'");
            RequireExactDescendant(dataRoot, trusted, safetyDirectory,
                $"external-HOME safety directory '{safetyDirectory}'");
            if (Directory.EnumerateFileSystemEntries(trusted).Any())
                throw new InvalidDataException($"External-HOME safety directory '{safetyDirectory}' is not empty.");
        }
        return true;
    }

    private static void ValidatePostSnapshotAgainstLiveProfile(
        Id65V3RuntimeProfileSnapshot snapshot,
        Id65V3RuntimeEvidenceArtifact artifact,
        Id65V3RuntimeEvidenceReadbackContext context)
    {
        Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> claimed = snapshot.DataFiles
            .Where(entry => !IsAllowedVolatileProfilePath(entry.RelativePath))
            .ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
        Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> current =
            BuildCurrentNonvolatileProfileManifest(context);
        if (claimed.Count != current.Count || claimed.Any(pair =>
                !current.TryGetValue(pair.Key, out Id65V3RuntimeProfileSnapshotEntry? actual) ||
                pair.Value.ByteLength != actual.ByteLength || pair.Value.Sha256 != actual.Sha256))
        {
            throw new InvalidDataException(
                $"Post snapshot {artifact.ArtifactId} does not match the current nonvolatile external-HOME manifest.");
        }
    }

    private static Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> BuildCurrentNonvolatileProfileManifest(
        Id65V3RuntimeEvidenceReadbackContext context)
    {
        string root = RequireAbsoluteDirectory(context.ExternalHomeDataRootPath, "external-HOME data root");
        Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> entries = new(StringComparer.Ordinal);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string trustedPath = RequireAbsoluteFile(path, "external-HOME manifest file");
            string relative = Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/');
            GuardProfileManifestPath(relative);
            if (IsAllowedVolatileProfilePath(relative))
                continue;
            FileIdentity identity = ReadFileIdentity(
                trustedPath,
                "external-HOME manifest file");
            entries.Add(relative, new(relative, identity.ByteLength, identity.Sha256));
        }
        return entries;
    }

    private static string RequireAbsoluteDirectory(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"The {label} path is not absolute.");
        string full = Path.GetFullPath(path);
        if (!Directory.Exists(full))
            throw new InvalidDataException($"The {label} directory is missing.");
        Id65V3RuntimeEvidenceFinalizer.RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: true,
            allowMissingLeaf: false,
            label);
        return full;
    }

    private static string RequireAbsoluteFile(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw new InvalidDataException($"The {label} path is not absolute.");
        string full = Path.GetFullPath(path);
        if (!File.Exists(full))
            throw new InvalidDataException($"The {label} file is missing.");
        Id65V3RuntimeEvidenceFinalizer.RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: false,
            label);
        return full;
    }

    private static void RequireExactDescendant(string root, string path, string relativePath, string label)
    {
        if (string.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath) ||
            relativePath.Contains('\\') || relativePath.Contains('\0') || relativePath.Any(char.IsControl))
        {
            throw new InvalidDataException($"Pinned {label} relative path is unsafe.");
        }
        string[] segments = relativePath.Split('/');
        if (segments.Any(segment => segment.Length == 0 || segment is "." or ".." ||
                segment.Normalize(NormalizationForm.FormC) != segment))
        {
            throw new InvalidDataException($"Pinned {label} relative path is not canonical segment-safe text.");
        }
        string expected = Path.GetFullPath(Path.Combine(root, Path.Combine(segments)));
        if (Path.GetFullPath(path) != expected)
            throw new InvalidDataException($"Pinned {label} path changed.");
    }

    private static void RejectAbsoluteSymlinkSegments(string path)
    {
        string full = Path.GetFullPath(path);
        string root = Path.GetPathRoot(full) ?? throw new InvalidDataException("Absolute path has no root.");
        string current = root;
        foreach (string segment in full[root.Length..].Split(Path.DirectorySeparatorChar,
                     StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
                throw new InvalidDataException("Pinned runtime readback path traverses a symbolic link.");
        }
    }

    private static readonly string[] SafetyDirectoryNames =
        ["cheats", "gamesettings", "memcards", "patches", "savestates"];

    private static bool VerifyPinnedSourceArtifacts(
        string repositoryRoot,
        Id65V3RuntimeEvidenceSourceIdentity source)
    {
        (string Path, string Hash, int Mode)[] pinned =
        [
            (source.CandidateBinRelativePath, source.CandidateBinSha256, 0x1A4),
            (source.CandidateCueRelativePath, source.CandidateCueSha256, 0x1A4),
            (source.ConstructionPlanRelativePath, source.ConstructionPlanSha256, 0x1A4),
            (source.StaticReadbackReceiptRelativePath, source.StaticReadbackReceiptSha256, 0x1A4),
            (source.RuntimeChecklistRelativePath, source.RuntimeChecklistSha256, 0x1A4),
            (source.LocationGuideRelativePath, source.LocationGuideSha256, 0x1A4),
            (source.RevealCommandRelativePath, source.RevealCommandSha256, 0x1ED),
            (source.WatcherRelativePath, source.WatcherSha256, 0x1A4),
            (source.ProfileBuilderRelativePath, source.ProfileBuilderSha256, 0x1A4),
            (source.ProfileVerifierRelativePath, source.ProfileVerifierSha256, 0x1A4),
            (source.AutomationRelativePath, source.AutomationSha256, 0x1ED),
            (source.CaptureCoordinatorRelativePath, source.CaptureCoordinatorSha256, 0x1ED)
        ];
        if (!OperatingSystem.IsMacOS())
            throw new InvalidDataException("This frozen Finder/DuckStation evidence profile is macOS-only.");

        string candidateDirectory = Path.GetDirectoryName(GuardPinnedSourcePath(
            repositoryRoot,
            source.CandidateBinRelativePath)) ??
            throw new InvalidDataException("Pinned V3 candidate BIN has no parent directory.");
        Id65V3RuntimeEvidenceFinalizer.RequireOwnedDirectoryPath(
            candidateDirectory,
            0x1ED,
            "pinned V3 candidate directory");
        string[] expectedCandidatePaths = pinned[..7]
            .Select(item => Path.GetFullPath(Path.Combine(repositoryRoot, item.Path)))
            .Order(StringComparer.Ordinal)
            .ToArray();
        string[] actualCandidatePaths = Directory.EnumerateFileSystemEntries(candidateDirectory)
            .Select(Path.GetFullPath)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (!actualCandidatePaths.SequenceEqual(expectedCandidatePaths))
        {
            throw new InvalidDataException(
                "Pinned V3 candidate directory must contain exactly its six data leaves and one Finder helper.");
        }

        foreach ((string relative, string expected, int mode) in pinned)
        {
            string full = GuardPinnedSourcePath(repositoryRoot, relative);
            if (!File.Exists(full))
                throw new InvalidDataException($"Pinned source artifact '{relative}' is missing.");
            Id65V3RuntimeEvidenceFinalizer.StableFileRead identity =
                Id65V3RuntimeEvidenceFinalizer.ReadStableOwnedFile(
                full,
                mode,
                MaximumPinnedFileBytes,
                keepBytes: false);
            if (identity.Sha256 != expected)
                throw new InvalidDataException($"Pinned source artifact '{relative}' hash changed.");
        }
        return true;
    }

    private static string GuardPinnedSourcePath(string repositoryRoot, string relativePath)
    {
        if (string.IsNullOrWhiteSpace(repositoryRoot))
            throw new ArgumentException("Repository root is required.", nameof(repositoryRoot));
        if (Path.IsPathRooted(relativePath) || relativePath.Contains('\\') ||
            relativePath.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
        {
            throw new InvalidDataException("Pinned source path is not repository-relative.");
        }
        string root = Path.GetFullPath(repositoryRoot);
        Id65V3RuntimeEvidenceFinalizer.RequireTrustedRepositoryRoot(
            root,
            "pinned source repository root");
        string full = Path.GetFullPath(Path.Combine(root, relativePath));
        string prefix = root.EndsWith(Path.DirectorySeparatorChar) ? root : root + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Pinned source path escapes the repository root.");
        RejectSymlinkSegments(root, relativePath.Split('/'));
        Id65V3RuntimeEvidenceFinalizer.RequireTrustedAbsolutePath(
            full,
            targetIsDirectory: false,
            allowMissingLeaf: true,
            "pinned source artifact");
        return full;
    }

    private static bool ValidateSessionSet(Id65V3RuntimeEvidenceSession[] sessions, out string? error)
    {
        error = null;
        if (sessions is null || sessions.Length > SessionIds.Length ||
            !sessions.Select(session => session.SessionId).SequenceEqual(SessionIds.Take(sessions.Length)))
        {
            error = "Runtime sessions are not a strict ordered prefix of the seven fixed cold-run sessions.";
            return false;
        }
        if (sessions.Select(session => session.ProcessInstanceId).Distinct(StringComparer.Ordinal).Count() != sessions.Length)
        {
            error = "Every runtime session must have a distinct process instance ID.";
            return false;
        }
        (string Id, int Level, string Code)[] expected =
        [
            ("candidate-primary-edge-0", 65, Id65LoadCode),
            ("candidate-edge-1", 65, Id65LoadCode),
            ("candidate-edge-2", 65, Id65LoadCode),
            ("candidate-cold-reset", 65, Id65LoadCode),
            ("retail-town-square", 13, TownSquareLoadCode),
            ("retail-gnastys-loot", 64, GnastysLootLoadCode),
            ("retail-sunny-flight", 15, SunnyFlightLoadCode)
        ];
        bool complete = sessions.Length == SessionIds.Length;
        DateTimeOffset? previousProcessExit = null;
        for (int index = 0; index < sessions.Length; index++)
        {
            Id65V3RuntimeEvidenceSession session = sessions[index];
            if (session.ProcessInstanceId is null || session.LoadCode is null)
            {
                error = $"Runtime session {index} has a null identity member.";
                return false;
            }
            if (session.SessionId != expected[index].Id || session.TargetLevelId != expected[index].Level ||
                session.LoadCode != expected[index].Code)
            {
                error = $"Runtime session {index} target or exact load code changed.";
                return false;
            }
            if (!session.ColdBoot || !session.NewGame || !session.CardsNone || !session.CheatsOff ||
                session.SaveStateLoaded || session.SaveStateCreated || session.InGameSaveUsed)
            {
                error = $"Runtime session {session.SessionId} violates the cold/no-card/no-cheat/no-save boundary.";
                return false;
            }
            if (!SessionHasRequiredTimes(session))
            {
                complete = false;
            }
            else if (!SessionTimesAreOrdered(session))
            {
                error = $"Runtime session {session.SessionId} timestamps are out of order.";
                return false;
            }
            else
            {
                DateTimeOffset processStart = ParseCanonicalUtc(
                    session.ProcessStartedAtUtc!,
                    $"session {session.SessionId} process start");
                if (previousProcessExit is not null && processStart <= previousProcessExit.Value)
                {
                    error = $"Runtime session {session.SessionId} overlaps or reorders a prior cold process.";
                    return false;
                }
                previousProcessExit = ParseCanonicalUtc(
                    session.ProcessExitedAtUtc!,
                    $"session {session.SessionId} process exit");
            }
            bool markersComplete = ValidateSessionMarkers(session, out string? markerError);
            if (markerError is not null)
            {
                error = markerError;
                return false;
            }
            complete &= markersComplete;
        }
        return complete;
    }

    private static bool ValidateSessionMarkers(
        Id65V3RuntimeEvidenceSession session,
        out string? error)
    {
        error = null;
        bool complete = session.LandingSampleOrdinal is not null;
        int?[] ordinals =
        [
            session.LandingSampleOrdinal,
            session.HoldStartSampleOrdinal,
            session.HoldEndSampleOrdinal,
            session.FallStartSampleOrdinal,
            session.RespawnSampleOrdinal
        ];
        if (ordinals.Any(value => value < 0))
        {
            error = $"Session {session.SessionId} has a negative sample marker.";
            return false;
        }

        bool hasAnyHold = session.HoldStartedAtUtc is not null || session.HoldEndedAtUtc is not null ||
            session.HoldStartSampleOrdinal is not null || session.HoldEndSampleOrdinal is not null;
        bool hasFullHold = session.HoldStartedAtUtc is not null && session.HoldEndedAtUtc is not null &&
            session.HoldStartSampleOrdinal is not null && session.HoldEndSampleOrdinal is not null;
        if (hasAnyHold != hasFullHold)
        {
            error = $"Session {session.SessionId} has a partial stable-hold marker set.";
            return false;
        }
        if (session.SessionId == "candidate-primary-edge-0")
            complete &= hasFullHold;
        else if (hasAnyHold)
        {
            error = $"Session {session.SessionId} carries an unexpected stable-hold marker set.";
            return false;
        }

        bool hasAnyDeath = session.FallStartedAtUtc is not null || session.RespawnAtUtc is not null ||
            session.FallStartSampleOrdinal is not null || session.RespawnSampleOrdinal is not null;
        bool hasFullDeath = session.FallStartedAtUtc is not null && session.RespawnAtUtc is not null &&
            session.FallStartSampleOrdinal is not null && session.RespawnSampleOrdinal is not null;
        if (hasAnyDeath != hasFullDeath)
        {
            error = $"Session {session.SessionId} has a partial fall/respawn marker set.";
            return false;
        }
        bool candidateDeath = session.SessionId is
            "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2";
        if (candidateDeath)
            complete &= hasFullDeath;
        else if (hasAnyDeath)
        {
            error = $"Session {session.SessionId} carries an unexpected fall/respawn marker set.";
            return false;
        }

        if (!complete || !SessionHasRequiredTimes(session))
            return complete;
        DateTimeOffset landingAt = ParseCanonicalUtc(session.LandingAtUtc!, "landing");
        DateTimeOffset exitAt = ParseCanonicalUtc(session.ProcessExitedAtUtc!, "process exit");
        if (landingAt >= exitAt)
        {
            error = $"Session {session.SessionId} landing does not precede process exit.";
            return false;
        }
        if (hasFullHold)
        {
            DateTimeOffset holdStartAt = ParseCanonicalUtc(session.HoldStartedAtUtc!, "hold start");
            DateTimeOffset holdEndAt = ParseCanonicalUtc(session.HoldEndedAtUtc!, "hold end");
            if (holdStartAt < landingAt || holdEndAt <= holdStartAt ||
                session.HoldStartSampleOrdinal <= session.LandingSampleOrdinal ||
                session.HoldEndSampleOrdinal <= session.HoldStartSampleOrdinal)
            {
                error = $"Session {session.SessionId} stable-hold markers are out of order.";
                return false;
            }
        }
        if (hasFullDeath)
        {
            DateTimeOffset fallAt = ParseCanonicalUtc(session.FallStartedAtUtc!, "fall start");
            DateTimeOffset respawnAt = ParseCanonicalUtc(session.RespawnAtUtc!, "respawn");
            if (fallAt <= landingAt || respawnAt <= fallAt || respawnAt >= exitAt ||
                session.FallStartSampleOrdinal <= session.LandingSampleOrdinal ||
                session.RespawnSampleOrdinal <= session.FallStartSampleOrdinal ||
                (hasFullHold && (fallAt <= ParseCanonicalUtc(session.HoldEndedAtUtc!, "hold end") ||
                                 session.FallStartSampleOrdinal <= session.HoldEndSampleOrdinal)))
            {
                error = $"Session {session.SessionId} fall/respawn markers are out of order.";
                return false;
            }
        }
        return true;
    }

    private static void ValidateTelemetryTimeBinding(
        Id65V3RuntimeEvidenceSession session,
        Id65V3RuntimeEvidenceArtifact watcherArtifact,
        Id65V3TelemetryParseResult parsed)
    {
        if (!SessionHasRequiredTimes(session) || parsed.Samples.Length == 0)
            throw new InvalidDataException($"Session {session.SessionId} telemetry lacks a complete process window.");
        DateTimeOffset manualResume = ParseCanonicalUtc(
            session.ManuallyResumedAtUtc!,
            $"session {session.SessionId} manual resume");
        DateTimeOffset processExit = ParseCanonicalUtc(
            session.ProcessExitedAtUtc!,
            $"session {session.SessionId} process exit");
        DateTimeOffset firstSample = ParseCanonicalUtc(
            parsed.Samples[0].AtUtc,
            $"session {session.SessionId} first watcher sample");
        DateTimeOffset lastSample = ParseCanonicalUtc(
            parsed.Samples[^1].AtUtc,
            $"session {session.SessionId} last watcher sample");
        DateTimeOffset captured = ParseCanonicalUtc(
            watcherArtifact.CapturedAtUtc,
            $"session {session.SessionId} watcher capture");
        if (firstSample <= manualResume || lastSample >= processExit || captured < lastSample || captured > processExit)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} watcher samples or capture are outside the resumed process window.");
        }

        RequireMarkerTimestamp(
            session,
            parsed.Samples,
            session.LandingAtUtc,
            session.LandingSampleOrdinal,
            "landing");
        RequireMarkerTimestamp(
            session,
            parsed.Samples,
            session.HoldStartedAtUtc,
            session.HoldStartSampleOrdinal,
            "hold start");
        RequireMarkerTimestamp(
            session,
            parsed.Samples,
            session.HoldEndedAtUtc,
            session.HoldEndSampleOrdinal,
            "hold end");
        RequireMarkerTimestamp(
            session,
            parsed.Samples,
            session.FallStartedAtUtc,
            session.FallStartSampleOrdinal,
            "fall start");
        RequireMarkerTimestamp(
            session,
            parsed.Samples,
            session.RespawnAtUtc,
            session.RespawnSampleOrdinal,
            "respawn");
    }

    private static void RequireMarkerTimestamp(
        Id65V3RuntimeEvidenceSession session,
        Id65V3TelemetryTuple[] samples,
        string? markerAtUtc,
        int? markerOrdinal,
        string markerName)
    {
        if (markerAtUtc is null && markerOrdinal is null)
            return;
        if (markerAtUtc is null || markerOrdinal is null || markerOrdinal < 0 || markerOrdinal >= samples.Length ||
            markerAtUtc != samples[markerOrdinal.Value].AtUtc)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} {markerName} timestamp is not exactly bound to its watcher sample.");
        }
    }

    private static bool ValidateCheckSet(Id65V3RuntimeEvidenceCheck[] checks, out string? error)
    {
        error = null;
        if (checks is null || checks.Length > AllCheckIds.Length ||
            !checks.Select(check => check.CheckId).SequenceEqual(AllCheckIds.Take(checks.Length)))
        {
            error = "Visual/runtime checkbox set is not a strict ordered prefix of the frozen checklist.";
            return false;
        }
        for (int index = 0; index < checks.Length; index++)
        {
            if (checks[index].SessionId != SessionIdForCheck(checks[index].CheckId))
            {
                error = $"Check {checks[index].CheckId} is assigned to the wrong cold process.";
                return false;
            }
            bool expectedTelemetry = TelemetryCorroboratedCheckIds.Contains(checks[index].CheckId);
            if (checks[index].TelemetryCorroborated != expectedTelemetry)
            {
                error = $"Check {checks[index].CheckId} telemetry-corroborated flag is not exact.";
                return false;
            }
        }
        if (checks.Any(check => check.ArtifactRefs is null || check.ArtifactRefs.Any(reference => reference is null)))
        {
            error = "Visual/runtime checkbox contains a null artifact-reference array or member.";
            return false;
        }
        return checks.Length == AllCheckIds.Length;
    }

    private static string SessionIdForCheck(string checkId)
    {
        if (checkId is "candidate.edge-1-same-pad-yaw")
            return "candidate-edge-1";
        if (checkId is "candidate.edge-2-same-pad-yaw")
            return "candidate-edge-2";
        if (checkId is "candidate.cold-reset-same-pad-yaw")
            return "candidate-cold-reset";
        if (checkId.StartsWith("candidate.", StringComparison.Ordinal))
            return "candidate-primary-edge-0";
        if (checkId.StartsWith("retail.town-square.", StringComparison.Ordinal))
            return "retail-town-square";
        if (checkId.StartsWith("retail.gnastys-loot.", StringComparison.Ordinal))
            return "retail-gnastys-loot";
        if (checkId.StartsWith("retail.sunny-flight.", StringComparison.Ordinal))
            return "retail-sunny-flight";
        throw new InvalidDataException($"Check {checkId} is outside the frozen runtime matrix.");
    }

    private static bool ValidateRetailSet(Id65V3RuntimeEvidenceRetailControl[] controls, out string? error)
    {
        error = null;
        if (controls is null || controls.Length > 3)
        {
            error = "Retail control set exceeds the three fixed controls.";
            return false;
        }
        if (controls.Any(control => control.CheckIds is null))
        {
            error = "Retail control contains a null checklist.";
            return false;
        }
        (string Id, int Level, string Code, string[] CheckIds)[] expected =
        [
            ("town-square", 13, TownSquareLoadCode, TownSquareCheckIds),
            ("gnastys-loot", 64, GnastysLootLoadCode, GnastysLootCheckIds),
            ("sunny-flight", 15, SunnyFlightLoadCode, SunnyFlightCheckIds)
        ];
        for (int index = 0; index < controls.Length; index++)
        {
            if (controls[index].ControlId != expected[index].Id ||
                controls[index].LevelId != expected[index].Level ||
                controls[index].LoadCode != expected[index].Code ||
                !controls[index].CheckIds.SequenceEqual(expected[index].CheckIds))
            {
                error = $"Retail control {index} identity or checklist changed.";
                return false;
            }
        }
        return controls.Length == 3;
    }

    private static void ValidateRetailEvidenceShape(Id65V3RuntimeEvidencePackage package)
    {
        foreach (Id65V3RuntimeEvidenceRetailControl control in package.RetailControls)
        {
            Id65V3RuntimeEvidenceCheck[] witnesses = package.VisualChecks
                .Where(check => control.CheckIds.Contains(check.CheckId, StringComparer.Ordinal)).ToArray();
            if (control.Status == Id65V3RuntimeEvidenceCheckStatus.Passed &&
                (witnesses.Length != control.CheckIds.Length ||
                 witnesses.Any(check => check.Status != Id65V3RuntimeEvidenceCheckStatus.Passed)))
            {
                throw new InvalidDataException($"Retail control {control.ControlId} claims pass without all witnesses.");
            }
            if (control.Status == Id65V3RuntimeEvidenceCheckStatus.Failed &&
                !witnesses.Any(check => check.Status == Id65V3RuntimeEvidenceCheckStatus.Failed))
            {
                throw new InvalidDataException($"Retail control {control.ControlId} claims failure without a failed witness.");
            }
        }
    }

    private static bool CheckHasCompletePassedEvidence(Id65V3RuntimeEvidenceCheck check)
    {
        if (check.Status != Id65V3RuntimeEvidenceCheckStatus.Passed || check.ObservedAtUtc is null ||
            string.IsNullOrWhiteSpace(check.Attestation) || check.ArtifactRefs.Length == 0)
        {
            return false;
        }
        if (MinimumRecordingSliceMilliseconds.TryGetValue(check.CheckId, out long minimumMilliseconds) &&
            !check.ArtifactRefs.Any(reference =>
                reference.StartMilliseconds is long start && reference.EndMilliseconds is long end &&
                end - start >= minimumMilliseconds))
        {
            return false;
        }
        bool samePadYaw = check.CheckId.Contains("same-pad-yaw", StringComparison.Ordinal);
        if (samePadYaw && check.ArtifactRefs.Length < 2)
            return false;
        bool telemetryRequired = check.CheckId is
            "candidate.completed-landing-hud4" or
            "candidate.hands-off-20-seconds" or
            "candidate.primary-edge-4-to-3-same-pad-yaw" or
            "candidate.edge-1-same-pad-yaw" or
            "candidate.edge-2-same-pad-yaw";
        if (telemetryRequired && !check.TelemetryCorroborated)
            return false;
        return true;
    }

    private static void ValidateVisualEvidenceShape(Id65V3RuntimeEvidencePackage package)
    {
        Dictionary<string, string> visualArtifactSessions = new(StringComparer.Ordinal);
        Dictionary<string, string> visualContentSessions = new(StringComparer.Ordinal);
        foreach (Id65V3RuntimeEvidenceCheck check in package.VisualChecks)
        {
            bool hasObservation = check.ObservedAtUtc is not null ||
                !string.IsNullOrWhiteSpace(check.Attestation) || check.ArtifactRefs.Length != 0;
            if (check.Status == Id65V3RuntimeEvidenceCheckStatus.NotObserved)
            {
                if (hasObservation || check.TelemetryCorroborated)
                    throw new InvalidDataException($"Unobserved check {check.CheckId} carries claimed evidence.");
                continue;
            }
            if (!hasObservation || check.ObservedAtUtc is null || string.IsNullOrWhiteSpace(check.Attestation) ||
                check.Attestation.Length > 2_000 || check.ArtifactRefs.Length == 0)
            {
                throw new InvalidDataException($"Observed check {check.CheckId} lacks bounded evidence.");
            }
            Id65V3RuntimeEvidenceSession session = package.Sessions.SingleOrDefault(item =>
                    item.SessionId == check.SessionId) ??
                throw new InvalidDataException($"Observed check {check.CheckId} has no matching runtime session.");
            if (!SessionHasRequiredTimes(session))
                throw new InvalidDataException($"Observed check {check.CheckId} has no complete process window.");
            DateTimeOffset processStart = ParseCanonicalUtc(
                session.ProcessStartedAtUtc!,
                $"check {check.CheckId} process start");
            DateTimeOffset processExit = ParseCanonicalUtc(
                session.ProcessExitedAtUtc!,
                $"check {check.CheckId} process exit");
            DateTimeOffset observed = ParseCanonicalUtc(check.ObservedAtUtc, $"check {check.CheckId}");
            if (observed < processStart || observed > processExit)
                throw new InvalidDataException($"Check {check.CheckId} observation is outside its process window.");
            if (check.ArtifactRefs.Select(reference => reference.ArtifactId)
                    .Distinct(StringComparer.Ordinal).Count() != check.ArtifactRefs.Length)
            {
                throw new InvalidDataException($"Check {check.CheckId} reuses an artifact reference.");
            }

            bool observedBackedByReference = false;
            foreach (Id65V3RuntimeEvidenceSlice reference in check.ArtifactRefs)
            {
                if (string.IsNullOrWhiteSpace(reference.ArtifactId))
                    throw new InvalidDataException($"Check {check.CheckId} has an empty artifact reference.");
                Id65V3RuntimeEvidenceArtifact artifact = package.ArtifactCatalog.SingleOrDefault(item =>
                    item.ArtifactId == reference.ArtifactId) ??
                    throw new InvalidDataException(
                        $"Check {check.CheckId} references missing artifact {reference.ArtifactId}.");
                if (artifact.Kind is not ("screen-recording" or "frame"))
                    throw new InvalidDataException(
                        $"Check {check.CheckId} references nonvisual artifact {artifact.ArtifactId}.");
                bool coldResetComparison = check.CheckId == "candidate.cold-reset-same-pad-yaw";
                if ((!coldResetComparison && artifact.SessionId != check.SessionId) ||
                    (coldResetComparison && artifact.SessionId is not
                        ("candidate-edge-2" or "candidate-cold-reset")))
                {
                    throw new InvalidDataException(
                        $"Check {check.CheckId} references artifact {artifact.ArtifactId} from " +
                        $"unapproved session {artifact.SessionId}.");
                }
                if (visualArtifactSessions.TryGetValue(artifact.ArtifactId, out string? ownerSession) &&
                    ownerSession != artifact.SessionId)
                {
                    throw new InvalidDataException(
                        $"Visual artifact {artifact.ArtifactId} has conflicting session ownership.");
                }
                visualArtifactSessions[artifact.ArtifactId] = artifact.SessionId;
                if (visualContentSessions.TryGetValue(artifact.Sha256, out string? contentOwnerSession) &&
                    contentOwnerSession != artifact.SessionId)
                {
                    throw new InvalidDataException(
                        $"Visual artifacts with identical bytes are reused across cold processes " +
                        $"{contentOwnerSession} and {artifact.SessionId}.");
                }
                visualContentSessions[artifact.Sha256] = artifact.SessionId;
                Id65V3RuntimeEvidenceSession artifactSession = package.Sessions.SingleOrDefault(item =>
                        item.SessionId == artifact.SessionId) ??
                    throw new InvalidDataException(
                        $"Visual artifact {artifact.ArtifactId} has no matching runtime session.");
                if (!SessionHasRequiredTimes(artifactSession))
                    throw new InvalidDataException(
                        $"Visual artifact {artifact.ArtifactId} has no complete process window.");
                DateTimeOffset artifactProcessStart = ParseCanonicalUtc(
                    artifactSession.ProcessStartedAtUtc!,
                    $"artifact {artifact.ArtifactId} process start");
                DateTimeOffset artifactProcessExit = ParseCanonicalUtc(
                    artifactSession.ProcessExitedAtUtc!,
                    $"artifact {artifact.ArtifactId} process exit");
                DateTimeOffset captured = ParseCanonicalUtc(
                    artifact.CapturedAtUtc,
                    $"artifact {artifact.ArtifactId} capture");
                if (captured < artifactProcessStart || captured > artifactProcessExit)
                {
                    throw new InvalidDataException(
                        $"Visual artifact {artifact.ArtifactId} is outside session {artifact.SessionId}.");
                }

                bool hasStart = reference.StartMilliseconds is not null;
                bool hasEnd = reference.EndMilliseconds is not null;
                if (hasStart != hasEnd)
                    throw new InvalidDataException($"Check {check.CheckId} has a half-open media slice.");
                if (artifact.Kind == "screen-recording")
                {
                    if (!hasStart || artifact.MediaStartAtUtc is null || artifact.MediaEndAtUtc is null)
                        throw new InvalidDataException($"Check {check.CheckId} recording reference lacks a bounded slice.");
                    long sliceStartMilliseconds = reference.StartMilliseconds ??
                        throw new InvalidDataException($"Check {check.CheckId} recording slice lacks a start.");
                    long sliceEndMilliseconds = reference.EndMilliseconds ??
                        throw new InvalidDataException($"Check {check.CheckId} recording slice lacks an end.");
                    long mediaDuration = checked((long)(
                        ParseCanonicalUtc(artifact.MediaEndAtUtc, "media end") -
                        ParseCanonicalUtc(artifact.MediaStartAtUtc, "media start")).TotalMilliseconds);
                    DateTimeOffset mediaStart = ParseCanonicalUtc(artifact.MediaStartAtUtc, "media start");
                    DateTimeOffset mediaEnd = ParseCanonicalUtc(artifact.MediaEndAtUtc, "media end");
                    if (mediaStart < artifactProcessStart || mediaEnd > artifactProcessExit)
                        throw new InvalidDataException(
                            $"Recording {artifact.ArtifactId} media is outside session {artifact.SessionId}.");
                    if (sliceStartMilliseconds < 0 ||
                        sliceEndMilliseconds <= sliceStartMilliseconds ||
                        sliceEndMilliseconds > mediaDuration || reference.FrameSha256 is not null)
                    {
                        throw new InvalidDataException($"Check {check.CheckId} recording slice is out of bounds.");
                    }
                    DateTimeOffset sliceStart = mediaStart.AddMilliseconds(sliceStartMilliseconds);
                    DateTimeOffset sliceEnd = mediaStart.AddMilliseconds(sliceEndMilliseconds);
                    observedBackedByReference |= observed >= sliceStart && observed <= sliceEnd;
                }
                else if (hasStart || reference.FrameSha256 != artifact.Sha256 ||
                         !Sha256Regex().IsMatch(reference.FrameSha256 ?? string.Empty))
                {
                    throw new InvalidDataException($"Check {check.CheckId} frame reference is not hash-bound.");
                }
                else
                {
                    observedBackedByReference |= observed == captured;
                }
            }
            if (!observedBackedByReference)
                throw new InvalidDataException(
                    $"Check {check.CheckId} observation is outside every referenced frame or recording slice.");

            bool samePadYaw = check.CheckId.Contains("same-pad-yaw", StringComparison.Ordinal);
            Id65V3RuntimeEvidenceArtifact[] samePadFrames = samePadYaw
                ? check.ArtifactRefs.Select(reference => package.ArtifactCatalog.Single(artifact =>
                        artifact.ArtifactId == reference.ArtifactId))
                    .Where(artifact => artifact.Kind == "frame").ToArray()
                : [];
            if (check.Status == Id65V3RuntimeEvidenceCheckStatus.Passed && samePadYaw &&
                (samePadFrames.Length < 2 || samePadFrames.Select(artifact => artifact.Sha256)
                    .Distinct(StringComparer.Ordinal).Count() != samePadFrames.Length))
            {
                throw new InvalidDataException(
                    $"Same-pad/yaw check {check.CheckId} lacks distinct before/after frame hashes.");
            }
        }
    }

    private static void ValidateChecklistEvidenceSemantics(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceSession? primary = package.Sessions.SingleOrDefault(session =>
            session.SessionId == "candidate-primary-edge-0");
        if (primary is not null && SessionHasRequiredTimes(primary))
        {
            DateTimeOffset landing = ParseCanonicalUtc(primary.LandingAtUtc!, "primary landing");
            Id65V3RuntimeEvidenceCheck? landingCheck = FindObservedCheck(
                package,
                "candidate.completed-landing-hud4");
            if (landingCheck is not null && !EvidenceIntervals(package, landingCheck).Any(interval =>
                    interval.Kind == "screen-recording" && interval.Start <= landing && interval.End >= landing &&
                    interval.End - interval.Start >= TimeSpan.FromMilliseconds(3_000)))
            {
                throw new InvalidDataException(
                    "Completed-landing evidence lacks a three-second recording spanning the indexed landing sample.");
            }

            if (primary.HoldStartedAtUtc is not null && primary.HoldEndedAtUtc is not null)
            {
                DateTimeOffset holdStart = ParseCanonicalUtc(primary.HoldStartedAtUtc, "primary hold start");
                DateTimeOffset holdEnd = ParseCanonicalUtc(primary.HoldEndedAtUtc, "primary hold end");
                Id65V3RuntimeEvidenceCheck? handsOff = FindObservedCheck(
                    package,
                    "candidate.hands-off-20-seconds");
                if (handsOff is not null && !EvidenceIntervals(package, handsOff).Any(interval =>
                        interval.Kind == "screen-recording" && interval.Start <= holdStart &&
                        interval.End >= holdEnd))
                {
                    throw new InvalidDataException(
                        "Hands-off evidence does not contain one recording slice spanning the full indexed hold.");
                }

                DateTimeOffset previousEnd = holdEnd;
                foreach (string checkId in new[]
                {
                    "candidate.lone-pad-visibility",
                    "candidate.camera-360-twice",
                    "candidate.walk-center-perimeter-both-directions",
                    "candidate.jump-land-center-each-corner"
                })
                {
                    Id65V3RuntimeEvidenceCheck? check = FindObservedCheck(package, checkId);
                    if (check is null)
                        continue;
                    EvidenceInterval bounds = EvidenceBounds(package, check);
                    if (bounds.Start <= previousEnd)
                    {
                        throw new InvalidDataException(
                            $"Primary checklist evidence {checkId} overlaps or precedes its required predecessor.");
                    }
                    previousEnd = bounds.End;
                }
                if (primary.FallStartedAtUtc is not null &&
                    previousEnd >= ParseCanonicalUtc(primary.FallStartedAtUtc, "primary fall"))
                {
                    throw new InvalidDataException(
                        "Primary movement evidence does not finish before the deliberate fall.");
                }
            }
        }

        RequireDeathComparison(
            package,
            "candidate-primary-edge-0",
            "candidate.primary-edge-4-to-3-same-pad-yaw");
        RequireDeathComparison(package, "candidate-edge-1", "candidate.edge-1-same-pad-yaw");
        RequireDeathComparison(package, "candidate-edge-2", "candidate.edge-2-same-pad-yaw");
        RequireColdResetComparison(package);
        ValidateRetailRecordingOrder(package);
    }

    private static void ValidateRetailRecordingOrder(Id65V3RuntimeEvidencePackage package)
    {
        foreach (Id65V3RuntimeEvidenceRetailControl control in package.RetailControls)
        {
            DateTimeOffset? previousEnd = null;
            foreach (string checkId in control.CheckIds)
            {
                Id65V3RuntimeEvidenceCheck? check = FindObservedCheck(package, checkId);
                if (check is null || check.Status != Id65V3RuntimeEvidenceCheckStatus.Passed)
                    continue;
                if (!MinimumRecordingSliceMilliseconds.TryGetValue(checkId, out long minimumMilliseconds))
                    throw new InvalidDataException($"Retail check {checkId} has no frozen recording minimum.");
                EvidenceInterval[] recordings = EvidenceIntervals(package, check)
                    .Where(interval => interval.Kind == "screen-recording").ToArray();
                if (!recordings.Any(interval =>
                        interval.End - interval.Start >= TimeSpan.FromMilliseconds(minimumMilliseconds)))
                {
                    throw new InvalidDataException(
                        $"Retail check {checkId} lacks its own recording interval meeting the frozen minimum.");
                }
                DateTimeOffset currentStart = recordings.Min(interval => interval.Start);
                DateTimeOffset currentEnd = recordings.Max(interval => interval.End);
                if (previousEnd is DateTimeOffset priorEnd && currentStart < priorEnd)
                {
                    throw new InvalidDataException(
                        $"Retail check {checkId} recording overlaps or precedes its fixed-order predecessor.");
                }
                previousEnd = currentEnd;
            }
        }
    }

    private static void RequireDeathComparison(
        Id65V3RuntimeEvidencePackage package,
        string sessionId,
        string checkId)
    {
        Id65V3RuntimeEvidenceSession? session = package.Sessions.SingleOrDefault(item =>
            item.SessionId == sessionId);
        Id65V3RuntimeEvidenceCheck? check = FindObservedCheck(package, checkId);
        if (session is null || check is null || session.FallStartedAtUtc is null || session.RespawnAtUtc is null)
            return;
        DateTimeOffset landing = ParseCanonicalUtc(session.LandingAtUtc!, $"{sessionId} landing");
        DateTimeOffset fall = ParseCanonicalUtc(session.FallStartedAtUtc, $"{sessionId} fall");
        DateTimeOffset respawn = ParseCanonicalUtc(session.RespawnAtUtc, $"{sessionId} respawn");
        DateTimeOffset exit = ParseCanonicalUtc(session.ProcessExitedAtUtc!, $"{sessionId} exit");
        EvidenceInterval[] intervals = EvidenceIntervals(package, check);
        EvidenceInterval[] frames = intervals.Where(interval => interval.Kind == "frame").ToArray();
        bool before = frames.Any(frame => frame.SessionId == sessionId && frame.Start >= landing && frame.Start < fall);
        DateTimeOffset respawnLowerBound = respawn.AddMilliseconds(-MaximumTelemetryClockDriftMilliseconds);
        bool after = frames.Any(frame => frame.SessionId == sessionId &&
            frame.Start >= respawnLowerBound && frame.Start < exit);
        bool episode = intervals.Any(interval => interval.Kind == "screen-recording" &&
            interval.SessionId == sessionId && interval.Start <= fall &&
            interval.End >= respawnLowerBound);
        if (!before || !after || !episode)
        {
            throw new InvalidDataException(
                $"Death/respawn check {checkId} does not contain pre-fall and post-respawn frames plus a spanning recording.");
        }
    }

    private static void RequireColdResetComparison(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceCheck? check = FindObservedCheck(
            package,
            "candidate.cold-reset-same-pad-yaw");
        Id65V3RuntimeEvidenceSession? edge2 = package.Sessions.SingleOrDefault(session =>
            session.SessionId == "candidate-edge-2");
        Id65V3RuntimeEvidenceSession? reset = package.Sessions.SingleOrDefault(session =>
            session.SessionId == "candidate-cold-reset");
        if (check is null || edge2 is null || reset is null)
            return;
        EvidenceInterval[] intervals = EvidenceIntervals(package, check);
        EvidenceInterval[] frames = intervals.Where(interval => interval.Kind == "frame").ToArray();
        if (frames.Length != 2 || frames.Any(frame => frame.Kind != "frame") ||
            frames.Select(frame => frame.ArtifactId).Distinct(StringComparer.Ordinal).Count() != 2)
        {
            throw new InvalidDataException(
                "Cold-reset comparison must contain exactly two distinct session-bound frames.");
        }
        DateTimeOffset edge2Respawn = ParseCanonicalUtc(edge2.RespawnAtUtc!, "edge-2 respawn");
        DateTimeOffset edge2Exit = ParseCanonicalUtc(edge2.ProcessExitedAtUtc!, "edge-2 exit");
        DateTimeOffset resetSelector = ParseCanonicalUtc(reset.SelectorStartedAtUtc!, "reset selector start");
        DateTimeOffset resetLanding = ParseCanonicalUtc(reset.LandingAtUtc!, "reset landing");
        DateTimeOffset resetExit = ParseCanonicalUtc(reset.ProcessExitedAtUtc!, "reset exit");
        bool preReset = frames.Any(frame => frame.SessionId == "candidate-edge-2" &&
            frame.Start >= edge2Respawn.AddMilliseconds(-MaximumTelemetryClockDriftMilliseconds) &&
            frame.Start < edge2Exit);
        bool postReset = frames.Any(frame => frame.SessionId == "candidate-cold-reset" &&
            frame.Start >= resetLanding.AddMilliseconds(-MaximumTelemetryClockDriftMilliseconds) &&
            frame.Start < resetExit);
        bool resetRecording = intervals.Any(interval => interval.Kind == "screen-recording" &&
            interval.SessionId == "candidate-cold-reset" && interval.Start <= resetSelector &&
            interval.End >= resetLanding.AddMilliseconds(-MaximumTelemetryClockDriftMilliseconds) &&
            interval.End - interval.Start >= TimeSpan.FromMilliseconds(5_000));
        if (!preReset || !postReset || !resetRecording)
        {
            throw new InvalidDataException(
                "Cold-reset comparison is not bound to an edge-2 pre-reset frame, post-landing reset frame, " +
                "and five-second reset recording spanning selector entry through landing.");
        }
    }

    private static Id65V3RuntimeEvidenceCheck? FindObservedCheck(
        Id65V3RuntimeEvidencePackage package,
        string checkId) =>
        package.VisualChecks.SingleOrDefault(check => check.CheckId == checkId &&
            check.Status != Id65V3RuntimeEvidenceCheckStatus.NotObserved);

    private static EvidenceInterval EvidenceBounds(
        Id65V3RuntimeEvidencePackage package,
        Id65V3RuntimeEvidenceCheck check)
    {
        EvidenceInterval[] intervals = EvidenceIntervals(package, check);
        if (intervals.Length == 0)
            throw new InvalidDataException($"Check {check.CheckId} has no evidence interval.");
        return new(
            check.CheckId,
            check.SessionId,
            intervals.Min(interval => interval.Start),
            intervals.Max(interval => interval.End),
            "bounds");
    }

    private static EvidenceInterval[] EvidenceIntervals(
        Id65V3RuntimeEvidencePackage package,
        Id65V3RuntimeEvidenceCheck check) =>
        check.ArtifactRefs.Select(reference =>
        {
            Id65V3RuntimeEvidenceArtifact artifact = package.ArtifactCatalog.Single(item =>
                item.ArtifactId == reference.ArtifactId);
            if (artifact.Kind == "frame")
            {
                DateTimeOffset captured = ParseCanonicalUtc(artifact.CapturedAtUtc, "frame capture");
                return new EvidenceInterval(
                    artifact.ArtifactId,
                    artifact.SessionId,
                    captured,
                    captured,
                    artifact.Kind);
            }
            DateTimeOffset mediaStart = ParseCanonicalUtc(artifact.MediaStartAtUtc!, "recording media start");
            return new EvidenceInterval(
                artifact.ArtifactId,
                artifact.SessionId,
                mediaStart.AddMilliseconds(reference.StartMilliseconds!.Value),
                mediaStart.AddMilliseconds(reference.EndMilliseconds!.Value),
                artifact.Kind);
        }).ToArray();

    private static void ValidateSessionArtifactReferenceShape(Id65V3RuntimeEvidencePackage package)
    {
        Dictionary<string, string> owners = new(StringComparer.Ordinal);
        foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
        {
            (string? ArtifactId, string Kind, string Role)[] references =
            [
                (session.WatcherArtifactId, "watcher-stdout", "watcher"),
                (session.CaptureJournal?.ArtifactId, "capture-event-journal", "capture-journal"),
                (session.ProfileBeforeArtifactId, "runtime-profile-before", "profile-before"),
                (session.ProfileAfterArtifactId, "runtime-profile-after", "profile-after")
            ];
            foreach ((string? artifactId, string kind, string role) in references)
            {
                if (artifactId is null)
                    continue;
                Id65V3RuntimeEvidenceArtifact artifact = RequireArtifact(package, artifactId, kind);
                if (artifact.SessionId != session.SessionId)
                {
                    throw new InvalidDataException(
                        $"Artifact {artifactId} is assigned to {artifact.SessionId}, not session {session.SessionId}.");
                }
                string owner = $"{session.SessionId}:{role}";
                if (!owners.TryAdd(artifactId, owner))
                {
                    throw new InvalidDataException(
                        $"Artifact {artifactId} is reused by incompatible session roles {owners[artifactId]} and {owner}.");
                }
            }
        }
        Id65V3RuntimeEvidenceArtifact[] watcherArtifacts = package.Sessions
            .Where(session => session.WatcherArtifactId is not null)
            .Select(session => RequireArtifact(package, session.WatcherArtifactId!, "watcher-stdout"))
            .ToArray();
        if (watcherArtifacts.Select(artifact => artifact.Sha256).Distinct(StringComparer.Ordinal).Count() !=
            watcherArtifacts.Length)
        {
            throw new InvalidDataException("Two runtime sessions reuse identical watcher-log bytes.");
        }
    }

    private static void ValidateArtifactOwnership(Id65V3RuntimeEvidencePackage package)
    {
        HashSet<string> referenced = new(StringComparer.Ordinal);
        foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
        {
            foreach (string? artifactId in new[]
            {
                session.WatcherArtifactId,
                session.CaptureJournal?.ArtifactId,
                session.ProfileBeforeArtifactId,
                session.ProfileAfterArtifactId
            })
            {
                if (artifactId is not null)
                    referenced.Add(artifactId);
            }
        }
        foreach (Id65V3RuntimeEvidenceCheck check in package.VisualChecks)
        {
            foreach (Id65V3RuntimeEvidenceSlice reference in check.ArtifactRefs)
                referenced.Add(reference.ArtifactId);
        }
        string[] unowned = package.ArtifactCatalog.Select(artifact => artifact.ArtifactId)
            .Where(artifactId => !referenced.Contains(artifactId)).ToArray();
        if (unowned.Length != 0)
            throw new InvalidDataException($"Runtime evidence catalog contains unowned artifact {unowned[0]}.");
    }

    private static bool ValidateColdReset(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceColdReset reset = package.ColdReset;
        if (reset.Status != Id65V3RuntimeEvidenceCheckStatus.Passed ||
            reset.PrimaryProcessExitedAtUtc is null || reset.ResetProcessStartedAtUtc is null)
        {
            return false;
        }
        DateTimeOffset exit = ParseCanonicalUtc(reset.PrimaryProcessExitedAtUtc, "primary process exit");
        DateTimeOffset start = ParseCanonicalUtc(reset.ResetProcessStartedAtUtc, "reset process start");
        Id65V3RuntimeEvidenceSession? predecessor = package.Sessions.SingleOrDefault(session =>
            session.SessionId == "candidate-edge-2");
        Id65V3RuntimeEvidenceSession? resetSession = package.Sessions.SingleOrDefault(session =>
            session.SessionId == reset.ColdResetSessionId);
        Id65V3RuntimeEvidenceCheck? witness = package.VisualChecks.SingleOrDefault(check =>
            check.CheckId == reset.SamePadYawCheckId);
        return start > exit && reset.ColdResetSessionId == "candidate-cold-reset" &&
            reset.SamePadYawCheckId == "candidate.cold-reset-same-pad-yaw" &&
            predecessor?.ProcessExitedAtUtc == reset.PrimaryProcessExitedAtUtc &&
            resetSession?.ProcessStartedAtUtc == reset.ResetProcessStartedAtUtc &&
            witness?.Status == Id65V3RuntimeEvidenceCheckStatus.Passed;
    }

    private static void ValidateColdResetShape(Id65V3RuntimeEvidencePackage package)
    {
        Id65V3RuntimeEvidenceColdReset reset = package.ColdReset;
        if (reset.ColdResetSessionId != "candidate-cold-reset" ||
            reset.SamePadYawCheckId != "candidate.cold-reset-same-pad-yaw")
        {
            throw new InvalidDataException("Cold-reset identity changed.");
        }
        if (reset.Status == Id65V3RuntimeEvidenceCheckStatus.NotObserved)
        {
            if (reset.PrimaryProcessExitedAtUtc is not null || reset.ResetProcessStartedAtUtc is not null)
                throw new InvalidDataException("Unobserved cold reset carries timestamps.");
            return;
        }
        if (reset.PrimaryProcessExitedAtUtc is null || reset.ResetProcessStartedAtUtc is null)
            throw new InvalidDataException("Observed cold reset lacks process-boundary timestamps.");
        DateTimeOffset exit = ParseCanonicalUtc(reset.PrimaryProcessExitedAtUtc, "primary process exit");
        DateTimeOffset start = ParseCanonicalUtc(reset.ResetProcessStartedAtUtc, "reset process start");
        if (start <= exit)
            throw new InvalidDataException("Cold-reset process start does not follow primary-process exit.");
        Id65V3RuntimeEvidenceSession? predecessor = package.Sessions.SingleOrDefault(session =>
            session.SessionId == "candidate-edge-2");
        Id65V3RuntimeEvidenceSession? resetSession = package.Sessions.SingleOrDefault(session =>
            session.SessionId == reset.ColdResetSessionId);
        if (predecessor?.ProcessExitedAtUtc != reset.PrimaryProcessExitedAtUtc ||
            resetSession?.ProcessStartedAtUtc != reset.ResetProcessStartedAtUtc)
        {
            throw new InvalidDataException(
                "Cold-reset fields do not equal the immediately preceding exit and reset-process start.");
        }
        if (reset.Status is Id65V3RuntimeEvidenceCheckStatus.Passed or Id65V3RuntimeEvidenceCheckStatus.Failed)
        {
            Id65V3RuntimeEvidenceCheck? witness = package.VisualChecks.SingleOrDefault(check =>
                check.CheckId == reset.SamePadYawCheckId);
            if (witness is null || witness.Status != reset.Status)
                throw new InvalidDataException("Cold-reset status is not corroborated by its visual witness.");
        }
    }

    private static bool SessionTimesAreOrdered(Id65V3RuntimeEvidenceSession session)
    {
        string?[] required =
        [
            session.ProcessStartedAtUtc,
            session.WatcherConnectedAtUtc,
            session.ManuallyResumedAtUtc,
            session.SelectorStartedAtUtc,
            session.LandingAtUtc,
            session.ProcessExitedAtUtc
        ];
        if (required.Any(value => value is null))
            return false;
        DateTimeOffset[] times = required.Select(value => ParseCanonicalUtc(value!, $"session {session.SessionId}")).ToArray();
        return times.Zip(times.Skip(1)).All(pair => pair.Second > pair.First);
    }

    private static bool SessionHasRequiredTimes(Id65V3RuntimeEvidenceSession session) =>
        session.ProcessStartedAtUtc is not null &&
        session.WatcherConnectedAtUtc is not null &&
        session.ManuallyResumedAtUtc is not null &&
        session.SelectorStartedAtUtc is not null &&
        session.LandingAtUtc is not null &&
        session.ProcessExitedAtUtc is not null;

    private static bool HasArtifactKind(
        Id65V3RuntimeEvidencePackage package,
        string? artifactId,
        string kind) =>
        artifactId is not null && package.ArtifactCatalog.Any(artifact =>
            artifact.ArtifactId == artifactId && artifact.Kind == kind);

    private static bool ValidateRuntimeProfileArtifacts(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceValidationMode mode,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext)
    {
        if (mode != Id65V3RuntimeEvidenceValidationMode.FullReadback || readbackContext is null)
            return false;

        bool complete = package.Sessions.Length == SessionIds.Length;
        SortedSet<string> changedPaths = new(UnicodeScalarStringComparer.Instance);
        DateTimeOffset? previousAfterAt = null;
        string? previousSessionId = null;
        Id65V3RuntimeProfileSnapshot? previousAfter = null;
        foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
        {
            bool hasRequiredTimes = SessionHasRequiredTimes(session);
            if (!hasRequiredTimes)
                complete = false;
            if (session.ProfileBeforeArtifactId is not null &&
                session.ProfileBeforeArtifactId == session.ProfileAfterArtifactId)
                throw new InvalidDataException($"Session {session.SessionId} reuses one profile artifact for both phases.");
            Id65V3RuntimeProfileSnapshot? before = session.ProfileBeforeArtifactId is null
                ? null
                : ReadAndValidateProfileSnapshot(
                    package,
                    artifactBytes,
                    session,
                    session.ProfileBeforeArtifactId,
                    "before",
                    readbackContext);
            Id65V3RuntimeProfileSnapshot? after = session.ProfileAfterArtifactId is null
                ? null
                : ReadAndValidateProfileSnapshot(
                    package,
                    artifactBytes,
                    session,
                    session.ProfileAfterArtifactId,
                    "after",
                    readbackContext);
            if (before is null || after is null)
            {
                complete = false;
                continue;
            }

            DateTimeOffset beforeAt = ParseCanonicalUtc(before.CapturedAtUtc, "profile-before capture");
            DateTimeOffset afterAt = ParseCanonicalUtc(after.CapturedAtUtc, "profile-after capture");
            if (afterAt <= beforeAt)
            {
                throw new InvalidDataException(
                    $"Session {session.SessionId} profile snapshot order is invalid.");
            }
            if (previousAfterAt is DateTimeOffset priorAfter && beforeAt <= priorAfter)
            {
                throw new InvalidDataException(
                    $"Adjacent profile snapshots are not strictly chained from {previousSessionId} after-capture " +
                    $"to {session.SessionId} before-capture.");
            }
            if (previousAfter is not null &&
                SerializeCanonicalValue(previousAfter.DataFiles) != SerializeCanonicalValue(before.DataFiles))
            {
                throw new InvalidDataException(
                    $"Session {session.SessionId} profile-before manifest does not equal the preceding " +
                    $"{previousSessionId} profile-after manifest.");
            }
            if (hasRequiredTimes)
            {
                DateTimeOffset processStart = ParseCanonicalUtc(session.ProcessStartedAtUtc!, "session process start");
                DateTimeOffset processExit = ParseCanonicalUtc(session.ProcessExitedAtUtc!, "session process exit");
                if (beforeAt > processStart || afterAt < processExit)
                {
                    throw new InvalidDataException(
                    $"Session {session.SessionId} profile snapshots do not bookend the cold process.");
                }
            }
            previousAfterAt = afterAt;
            previousSessionId = session.SessionId;
            previousAfter = after;

            Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> beforeFiles =
                before.DataFiles.ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
            Dictionary<string, Id65V3RuntimeProfileSnapshotEntry> afterFiles =
                after.DataFiles.ToDictionary(entry => entry.RelativePath, StringComparer.Ordinal);
            foreach (string path in beforeFiles.Keys.Union(afterFiles.Keys, StringComparer.Ordinal))
            {
                bool beforeFound = beforeFiles.TryGetValue(path, out Id65V3RuntimeProfileSnapshotEntry? beforeEntry);
                bool afterFound = afterFiles.TryGetValue(path, out Id65V3RuntimeProfileSnapshotEntry? afterEntry);
                if (!beforeFound || !afterFound || beforeEntry!.ByteLength != afterEntry!.ByteLength ||
                    beforeEntry.Sha256 != afterEntry.Sha256)
                {
                    if (!IsAllowedVolatileProfilePath(path))
                    {
                        throw new InvalidDataException(
                            $"Session {session.SessionId} changed nonvolatile profile path '{path}'.");
                    }
                    changedPaths.Add(path);
                }
            }
        }

        if (complete && (package.RuntimeProfile.ChangedProfilePaths is null ||
            !package.RuntimeProfile.ChangedProfilePaths.SequenceEqual(changedPaths)))
        {
            throw new InvalidDataException(
                "Declared volatile profile changes differ from snapshot-manifest recomputation.");
        }
        return complete;
    }

    private static Id65V3RuntimeProfileSnapshot ReadAndValidateProfileSnapshot(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceSession session,
        string artifactId,
        string phase,
        Id65V3RuntimeEvidenceReadbackContext readbackContext)
    {
        string kind = phase == "before" ? "runtime-profile-before" : "runtime-profile-after";
        Id65V3RuntimeEvidenceArtifact artifact = RequireArtifact(package, artifactId, kind);
        if (!artifactBytes.TryGetValue(artifact.ArtifactId, out byte[]? bytes))
            throw new InvalidDataException($"Session {session.SessionId} profile artifact bytes are unavailable.");
        Id65V3RuntimeProfileSnapshot snapshot = DeserializeVerifierCanonicalArtifact<Id65V3RuntimeProfileSnapshot>(
            bytes,
            $"profile snapshot {artifact.ArtifactId}");
        ValidateProfileSnapshot(
            snapshot,
            artifact,
            session,
            phase,
            package.RuntimeProfile,
            readbackContext);
        return snapshot;
    }

    private static void ValidateProfileSnapshot(
        Id65V3RuntimeProfileSnapshot snapshot,
        Id65V3RuntimeEvidenceArtifact artifact,
        Id65V3RuntimeEvidenceSession session,
        string expectedPhase,
        Id65V3RuntimeEvidenceProfile profile,
        Id65V3RuntimeEvidenceReadbackContext readbackContext)
    {
        if (snapshot.Format != ProfileSnapshotFormat || snapshot.FormatVersion != ProfileSnapshotFormatVersion ||
            snapshot.SnapshotId != artifact.ArtifactId || snapshot.Phase != expectedPhase ||
            snapshot.SessionId != session.SessionId || snapshot.ProcessInstanceId != session.ProcessInstanceId ||
            snapshot.ProfileId != profile.ProfileId ||
            snapshot.DuckStationExecutableSha256 != profile.DuckStationExecutableSha256 ||
            snapshot.HandoffReceiptSha256 != profile.HandoffReceiptSha256 ||
            snapshot.AppContentManifestSha256 != profile.AppContentManifestSha256 ||
            snapshot.AppOutputTreeManifestSha256 != profile.AppOutputTreeManifestSha256 ||
            snapshot.PackageEnvelopeSha256 != profile.PackageEnvelopeSha256 ||
            snapshot.ExternalHomeSettingsSha256 != profile.ExternalHomeSettingsSha256 ||
            snapshot.ExternalHomeBiosSha256 != profile.ExternalHomeBiosSha256 ||
            snapshot.HostNormalSettingsSentinelId != profile.HostNormalSettingsSentinelId ||
            snapshot.HostNormalSettingsSentinelSha256 != profile.HostNormalSettingsSha256 ||
            snapshot.CapturedAtUtc != artifact.CapturedAtUtc)
        {
            throw new InvalidDataException($"Profile snapshot {artifact.ArtifactId} identity or pinned hash changed.");
        }
        _ = ParseCanonicalUtc(snapshot.CapturedAtUtc, $"profile snapshot {artifact.ArtifactId} timestamp");
        if (snapshot.DataFiles is null || snapshot.DataFiles.Length == 0 || snapshot.DataFiles.Length > 100_000 ||
            snapshot.DataFiles.Any(entry => entry is null) ||
            !snapshot.DataFiles.Select(entry => entry.RelativePath).SequenceEqual(
                snapshot.DataFiles.Select(entry => entry.RelativePath)
                    .Order(UnicodeScalarStringComparer.Instance)) ||
            snapshot.DataFiles.Select(entry => entry.RelativePath).Distinct(StringComparer.Ordinal).Count() !=
                snapshot.DataFiles.Length)
        {
            throw new InvalidDataException($"Profile snapshot {artifact.ArtifactId} file manifest is invalid.");
        }
        foreach (Id65V3RuntimeProfileSnapshotEntry entry in snapshot.DataFiles)
        {
            GuardProfileManifestPath(entry.RelativePath);
            if (!IsAllowedRuntimeDataPath(entry.RelativePath) ||
                entry.ByteLength < 0 || entry.ByteLength > MaximumJcsSafeInteger ||
                !Sha256Regex().IsMatch(entry.Sha256))
                throw new InvalidDataException($"Profile snapshot entry '{entry.RelativePath}' is invalid.");
        }

        Id65V3RuntimeProfileSnapshotEntry settings = snapshot.DataFiles.SingleOrDefault(entry =>
            entry.RelativePath == "settings.ini") ??
            throw new InvalidDataException($"Profile snapshot {artifact.ArtifactId} omits settings.ini.");
        Id65V3RuntimeProfileSnapshotEntry bios = snapshot.DataFiles.SingleOrDefault(entry =>
            entry.RelativePath == profile.ExternalHomeBiosRelativePath) ??
            throw new InvalidDataException($"Profile snapshot {artifact.ArtifactId} omits the pinned BIOS.");
        if (settings.ByteLength != profile.ExternalHomeSettingsByteLength ||
            settings.Sha256 != profile.ExternalHomeSettingsSha256 ||
            bios.ByteLength != profile.ExternalHomeBiosByteLength ||
            bios.Sha256 != profile.ExternalHomeBiosSha256)
        {
            throw new InvalidDataException(
                $"Profile snapshot {artifact.ArtifactId} omits or changes a pinned profile component.");
        }
        string[] safetyPrefixes = ["cheats/", "gamesettings/", "memcards/", "patches/", "savestates/"];
        if (snapshot.DataFiles.Any(entry => safetyPrefixes.Any(prefix =>
                entry.RelativePath.StartsWith(prefix, StringComparison.Ordinal))))
        {
            throw new InvalidDataException(
                $"Profile snapshot {artifact.ArtifactId} contains a forbidden safety-directory file.");
        }
        if (expectedPhase == "after")
            ValidatePostSnapshotAgainstLiveProfile(snapshot, artifact, readbackContext);
    }

    private static bool ValidateCaptureJournalArtifacts(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceValidationMode mode,
        Id65V3RuntimeEvidenceReadbackContext? readbackContext,
        string artifactRepositoryRoot)
    {
        if (mode != Id65V3RuntimeEvidenceValidationMode.FullReadback || readbackContext is null)
            return false;
        bool complete = package.Sessions.Length == SessionIds.Length;
        string previousTerminalHash = new('0', 64);
        string? runId = null;
        string? bootIdentity = null;
        string? runOriginSha256 = null;
        string? previousTerminalAtUtc = null;
        long? previousTerminalMonotonic = null;
        List<CaptureJournalReadback> journals = [];

        for (int ordinal = 0; ordinal < package.Sessions.Length; ordinal++)
        {
            Id65V3RuntimeEvidenceSession session = package.Sessions[ordinal];
            if (session.CaptureJournal is null)
            {
                complete = false;
                continue;
            }
            string expectedJournalId = $"{session.SessionId}.capture-journal";
            if (session.CaptureJournal.ArtifactId != expectedJournalId)
            {
                throw new InvalidDataException(
                    $"Session {session.SessionId} capture-journal ID is not exact.");
            }
            Id65V3RuntimeEvidenceArtifact artifact = RequireArtifact(
                package,
                session.CaptureJournal.ArtifactId,
                "capture-event-journal");
            if (!artifactBytes.TryGetValue(artifact.ArtifactId, out byte[]? bytes))
                throw new InvalidDataException(
                    $"Session {session.SessionId} capture-journal bytes are unavailable.");
            CaptureJournalReadback journal = ParseAndValidateCaptureJournal(
                bytes,
                artifact,
                session,
                ordinal,
                previousTerminalHash,
                previousTerminalAtUtc,
                previousTerminalMonotonic,
                package,
                artifactBytes,
                readbackContext);
            if (runId is not null && journal.RunId != runId)
                throw new InvalidDataException("Capture journals cross a run namespace.");
            if (bootIdentity is not null && journal.BootIdentity != bootIdentity)
                throw new InvalidDataException("Capture journals cross a machine boot identity.");
            if (runOriginSha256 is not null && journal.RunOriginSha256 != runOriginSha256)
                throw new InvalidDataException("Capture journals cross a run-origin identity.");
            runId ??= journal.RunId;
            bootIdentity ??= journal.BootIdentity;
            runOriginSha256 ??= journal.RunOriginSha256;
            previousTerminalHash = journal.TerminalEventSha256;
            previousTerminalAtUtc = journal.TerminalAtUtc;
            previousTerminalMonotonic = journal.TerminalMonotonicMicroseconds;
            journals.Add(journal);
        }

        if (journals.Count != package.Sessions.Length)
            return false;
        ValidateCaptureScaffoldingReadback(package, journals, artifactRepositoryRoot);
        ValidateUniqueRawProcessFingerprintHashes(
            journals.Select(item => item.RawProcessFingerprintSha256));
        ValidateCaptureJournalRawArtifacts(package, journals);
        ValidateCaptureJournalChecklistSlices(package, journals);
        return complete;
    }

    private static void ValidateCaptureScaffoldingReadback(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyList<CaptureJournalReadback> journals,
        string artifactRepositoryRoot)
    {
        Id65V3RuntimeEvidenceCaptureProvenance provenance = package.CaptureProvenance ??
            throw new InvalidDataException("FullReadback capture run lacks durable provenance.");
        if (journals.Count != SessionIds.Length || provenance.LiveEventProjections.Length != SessionIds.Length)
            throw new InvalidDataException("FullReadback capture provenance is not the exact seven-session matrix.");
        string root = RequireRepositoryRoot(artifactRepositoryRoot);
        string expectedOriginPath = RuntimeAssetPrefix + "capture-staging/" +
            provenance.RunId + "/run-origin.json";
        if (provenance.RunOriginRepoRelativePath != expectedOriginPath)
            throw new InvalidDataException("Capture provenance run-origin path changed.");
        Id65V3RuntimeEvidenceFinalizer.StableFileRead originRead =
            Id65V3RuntimeEvidenceFinalizer.ReadStableOwnedFile(
                Path.Combine(root, expectedOriginPath),
                0x180,
                MaximumTelemetryLogBytes,
                keepBytes: true);
        if (originRead.Sha256 != provenance.RunOriginSha256 ||
            originRead.ByteLength != provenance.RunOriginByteLength)
        {
            throw new InvalidDataException("Real run-origin bytes differ from durable capture provenance.");
        }
        JsonElement origin = ParseCanonicalArtifactElement(originRead.Bytes ?? [], "capture run origin");
        RequireCaptureObjectKeys(
            origin,
            [
                "bootIdentity", "captureStagingRepoRelativePath", "createdAtUtc",
                "createdMonotonicMicroseconds", "format", "formatVersion",
                "orderedSessionIds", "prelaunchProof", "prelaunchProofSha256",
                "repositoryRootRealPath", "runId", .. CapturePinPropertyNames
            ],
            "capture run origin");
        JsonElement orderedSessions = RequireCaptureProperty(
            origin,
            "orderedSessionIds",
            "capture run origin");
        if (orderedSessions.ValueKind != JsonValueKind.Array ||
            !orderedSessions.EnumerateArray().Select(item => item.GetString()).SequenceEqual(SessionIds) ||
            RequireCaptureString(origin, "format", "capture run origin") !=
                "spyro-editor-id65-v3-runtime-capture-run-origin" ||
            RequireCaptureInt32(origin, "formatVersion", "capture run origin") != 2 ||
            RequireCaptureString(origin, "runId", "capture run origin") != provenance.RunId ||
            RequireCaptureString(origin, "repositoryRootRealPath", "capture run origin") != root ||
            RequireCaptureString(origin, "captureStagingRepoRelativePath", "capture run origin") !=
                RuntimeAssetPrefix + "capture-staging/" + provenance.RunId ||
            RequireCaptureSha256(origin, "bootIdentity", "capture run origin") != journals[0].BootIdentity ||
            RequireCaptureInt64(origin, "createdMonotonicMicroseconds", "capture run origin") <= 0)
        {
            throw new InvalidDataException("Real run-origin identity or current frozen pins changed.");
        }
        ValidateCapturePinClosure(origin, package, "capture run origin");
        JsonElement prelaunchProof = RequireCaptureProperty(
            origin,
            "prelaunchProof",
            "capture run origin");
        ValidatePrelaunchProof(prelaunchProof, package);
        if (RequireCaptureSha256(origin, "prelaunchProofSha256", "capture run origin") !=
            Hash(SerializeVerifierCanonicalLf(prelaunchProof)))
        {
            throw new InvalidDataException("Capture run-origin prelaunch proof hash changed.");
        }
        DateTimeOffset originAt = ParseCanonicalUtc(
            RequireCaptureString(origin, "createdAtUtc", "capture run origin"),
            "capture run origin timestamp");
        long originMonotonic = RequireCaptureInt64(
            origin,
            "createdMonotonicMicroseconds",
            "capture run origin");
        CaptureJournalReadback firstJournal = journals[0];
        if (!CaptureCrossSessionClockStepIsAccepted(
                originAt,
                originMonotonic,
                ParseCanonicalUtc(firstJournal.FirstAtUtc, "first capture event"),
                firstJournal.FirstMonotonicMicroseconds))
        {
            throw new InvalidDataException(
                "Capture run-origin clock does not strictly precede the first session within drift tolerance.");
        }
        if (journals.Any(journal => journal.RunId != provenance.RunId ||
                journal.RunOriginSha256 != provenance.RunOriginSha256))
        {
            throw new InvalidDataException("Capture journals do not bind the real run-origin hash.");
        }

        List<string> liveHashes = [];
        for (int ordinal = 0; ordinal < journals.Count; ordinal++)
        {
            CaptureJournalReadback journal = journals[ordinal];
            Id65V3RuntimeEvidenceLiveProjection live = provenance.LiveEventProjections[ordinal];
            string expectedPath = RuntimeAssetPrefix + "capture-staging/" + provenance.RunId + "/" +
                ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + journal.SessionId +
                "/capture-events-live.ndjson";
            if (live.SessionOrdinal != ordinal || live.SessionId != journal.SessionId ||
                live.RepoRelativePath != expectedPath ||
                live.TerminalEventSha256 != journal.TerminalEventSha256)
            {
                throw new InvalidDataException(
                    $"Durable live projection {ordinal} differs from its capture journal.");
            }
            Id65V3RuntimeEvidenceFinalizer.StableFileRead liveRead =
                Id65V3RuntimeEvidenceFinalizer.ReadStableOwnedFile(
                    Path.Combine(root, expectedPath),
                    0x180,
                    MaximumTelemetryLogBytes,
                    keepBytes: true);
            using MemoryStream projection = new();
            string previous = journal.PreviousSessionTerminalSha256;
            foreach (CaptureJournalEventReadback item in journal.Events)
            {
                byte[] canonical = StrictUtf8.GetBytes(SerializeCanonicalValue(new
                {
                    item.AtUtc,
                    item.EventSha256,
                    item.EventType,
                    item.MonotonicMicroseconds,
                    item.Payload,
                    previousEventSha256 = previous,
                    item.Sequence
                }));
                projection.Write(canonical);
                projection.WriteByte((byte)'\n');
                previous = item.EventSha256;
            }
            byte[] expectedLive = projection.ToArray();
            if (!(liveRead.Bytes ?? []).AsSpan().SequenceEqual(expectedLive) ||
                liveRead.Sha256 != live.Sha256 || liveRead.ByteLength != live.ByteLength)
            {
                throw new InvalidDataException(
                    $"Live NDJSON for {journal.SessionId} differs from its exact LF journal projection.");
            }
            liveHashes.Add(liveRead.Sha256);
        }
        if (provenance.FinalTerminalEventSha256 != journals[^1].TerminalEventSha256)
            throw new InvalidDataException("Capture provenance final terminal hash changed.");
        string closure = BuildCaptureClosureSha256(
            provenance.RunId,
            provenance.RunOriginSha256,
            journals,
            liveHashes,
            package.ArtifactCatalog);
        if (closure != provenance.CaptureClosureSha256 ||
            package.OperatorReview?.CaptureClosureSha256 != closure)
        {
            throw new InvalidDataException("Durable capture-closure SHA-256 differs from FullReadback.");
        }
    }

    private static byte[] SerializeVerifierCanonicalLf(JsonElement value)
    {
        StringBuilder canonical = new();
        WritePythonCanonicalElement(canonical, value);
        canonical.Append('\n');
        return StrictUtf8.GetBytes(canonical.ToString());
    }

    private static void WritePythonCanonicalElement(StringBuilder builder, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in value.EnumerateObject()
                    .OrderBy(item => item.Name, UnicodeScalarStringComparer.Instance))
                {
                    if (!firstProperty)
                        builder.Append(',');
                    firstProperty = false;
                    AppendPythonJsonString(builder, property.Name);
                    builder.Append(':');
                    WritePythonCanonicalElement(builder, property.Value);
                }
                builder.Append('}');
                return;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in value.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');
                    firstItem = false;
                    WritePythonCanonicalElement(builder, item);
                }
                builder.Append(']');
                return;
            case JsonValueKind.String:
                AppendPythonJsonString(
                    builder,
                    value.GetString() ?? throw new InvalidDataException("Verifier JSON string is null."));
                return;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out long number) || Math.Abs((decimal)number) > MaximumJcsSafeInteger)
                    throw new InvalidDataException("Verifier JSON number is not an exact safe integer.");
                builder.Append(number.ToString(CultureInfo.InvariantCulture));
                return;
            case JsonValueKind.True:
                builder.Append("true");
                return;
            case JsonValueKind.False:
                builder.Append("false");
                return;
            case JsonValueKind.Null:
                builder.Append("null");
                return;
            default:
                throw new InvalidDataException("Verifier JSON contains an unsupported value kind.");
        }
    }

    private static void AppendPythonJsonString(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            switch (character)
            {
                case '"': builder.Append("\\\""); continue;
                case '\\': builder.Append("\\\\"); continue;
                case '\b': builder.Append("\\b"); continue;
                case '\f': builder.Append("\\f"); continue;
                case '\n': builder.Append("\\n"); continue;
                case '\r': builder.Append("\\r"); continue;
                case '\t': builder.Append("\\t"); continue;
            }
            if (character is >= ' ' and <= '~')
            {
                builder.Append(character);
                continue;
            }
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    throw new InvalidDataException("Verifier JSON contains an unpaired high surrogate.");
                AppendPythonUnicodeEscape(builder, character);
                AppendPythonUnicodeEscape(builder, value[++index]);
                continue;
            }
            if (char.IsLowSurrogate(character))
                throw new InvalidDataException("Verifier JSON contains an unpaired low surrogate.");
            AppendPythonUnicodeEscape(builder, character);
        }
        builder.Append('"');
    }

    private static void AppendPythonUnicodeEscape(StringBuilder builder, char value) =>
        builder.Append("\\u").Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));

    private sealed class UnicodeScalarStringComparer : IComparer<string>
    {
        internal static readonly UnicodeScalarStringComparer Instance = new();

        public int Compare(string? left, string? right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left is null)
                return -1;
            if (right is null)
                return 1;
            int leftIndex = 0;
            int rightIndex = 0;
            while (leftIndex < left.Length && rightIndex < right.Length)
            {
                int leftScalar = NextUnicodeScalar(left, ref leftIndex);
                int rightScalar = NextUnicodeScalar(right, ref rightIndex);
                int comparison = leftScalar.CompareTo(rightScalar);
                if (comparison != 0)
                    return comparison;
            }
            return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
        }

        private static int NextUnicodeScalar(string value, ref int index)
        {
            char first = value[index++];
            if (!char.IsSurrogate(first))
                return first;
            if (!char.IsHighSurrogate(first) || index >= value.Length ||
                !char.IsLowSurrogate(value[index]))
            {
                throw new InvalidDataException("Verifier JSON property name contains an unpaired surrogate.");
            }
            return char.ConvertToUtf32(first, value[index++]);
        }
    }

    private static void ValidatePrelaunchProof(
        JsonElement proof,
        Id65V3RuntimeEvidencePackage package)
    {
        RequireCaptureObjectKeys(
            proof,
            [
                "format", "formatVersion", "status", "profileId", "receiptSha256",
                "appContentManifestSha256", "appOutputTreeManifestSha256",
                "homeOutputTreeManifestSha256", "packageEnvelopeSha256", "settingsSha256",
                "biosSha256", "candidateCueSha256", "candidateBinSha256",
                "hostNormalSettingsSentinelSha256", "launchGate", "snapshot"
            ],
            "capture prelaunch proof");
        if (RequireCaptureString(proof, "format", "capture prelaunch proof") !=
                "spyro-id65-unattended-profile-preflight" ||
            RequireCaptureInt32(proof, "formatVersion", "capture prelaunch proof") != 1 ||
            RequireCaptureString(proof, "status", "capture prelaunch proof") != "PREFLIGHT_READY" ||
            RequireCaptureString(proof, "profileId", "capture prelaunch proof") !=
                package.RuntimeProfile.ProfileId ||
            RequireCaptureSha256(proof, "receiptSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.HandoffReceiptSha256 ||
            RequireCaptureSha256(proof, "appContentManifestSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.AppContentManifestSha256 ||
            RequireCaptureSha256(proof, "appOutputTreeManifestSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.AppOutputTreeManifestSha256 ||
            RequireCaptureSha256(proof, "homeOutputTreeManifestSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.HomeOutputTreeManifestSha256 ||
            RequireCaptureSha256(proof, "packageEnvelopeSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.PackageEnvelopeSha256 ||
            RequireCaptureSha256(proof, "settingsSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.ExternalHomeSettingsSha256 ||
            RequireCaptureSha256(proof, "biosSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.ExternalHomeBiosSha256 ||
            RequireCaptureSha256(proof, "candidateCueSha256", "capture prelaunch proof") !=
                package.SourceIdentity.CandidateCueSha256 ||
            RequireCaptureSha256(proof, "candidateBinSha256", "capture prelaunch proof") !=
                package.SourceIdentity.CandidateBinSha256 ||
            RequireCaptureSha256(proof, "hostNormalSettingsSentinelSha256", "capture prelaunch proof") !=
                package.RuntimeProfile.HostNormalSettingsSha256 ||
            RequireCaptureString(proof, "launchGate", "capture prelaunch proof") !=
                package.RuntimeProfile.LaunchGate)
        {
            throw new InvalidDataException("Capture prelaunch proof identity changed.");
        }
        ValidateEmbeddedProfileSnapshot(
            RequireCaptureProperty(proof, "snapshot", "capture prelaunch proof"),
            package.RuntimeProfile,
            expectedSnapshotId: "preflight.profile-preflight",
            expectedPhase: "preflight",
            expectedSessionId: "preflight",
            expectedProcessInstanceId: "not-applicable",
            expectedCapturedAtUtc: "1970-01-01T00:00:00.000Z");
    }

    private static void ValidateEmbeddedProfileSnapshot(
        JsonElement snapshot,
        Id65V3RuntimeEvidenceProfile profile,
        string expectedSnapshotId,
        string expectedPhase,
        string expectedSessionId,
        string expectedProcessInstanceId,
        string expectedCapturedAtUtc)
    {
        RequireCaptureObjectKeys(
            snapshot,
            [
                "format", "formatVersion", "snapshotId", "phase", "sessionId",
                "processInstanceId", "profileId", "capturedAtUtc",
                "duckStationExecutableSha256", "handoffReceiptSha256",
                "appContentManifestSha256", "appOutputTreeManifestSha256",
                "packageEnvelopeSha256", "externalHomeSettingsSha256",
                "externalHomeBiosSha256", "hostNormalSettingsSentinelId",
                "hostNormalSettingsSentinelSha256", "dataFiles"
            ],
            "embedded runtime profile snapshot");
        if (RequireCaptureString(snapshot, "format", "embedded runtime profile snapshot") !=
                ProfileSnapshotFormat ||
            RequireCaptureInt32(snapshot, "formatVersion", "embedded runtime profile snapshot") !=
                ProfileSnapshotFormatVersion ||
            RequireCaptureString(snapshot, "snapshotId", "embedded runtime profile snapshot") !=
                expectedSnapshotId ||
            RequireCaptureString(snapshot, "phase", "embedded runtime profile snapshot") != expectedPhase ||
            RequireCaptureString(snapshot, "sessionId", "embedded runtime profile snapshot") !=
                expectedSessionId ||
            RequireCaptureString(snapshot, "processInstanceId", "embedded runtime profile snapshot") !=
                expectedProcessInstanceId ||
            RequireCaptureString(snapshot, "profileId", "embedded runtime profile snapshot") !=
                profile.ProfileId ||
            RequireCaptureString(snapshot, "capturedAtUtc", "embedded runtime profile snapshot") !=
                expectedCapturedAtUtc ||
            RequireCaptureSha256(snapshot, "duckStationExecutableSha256", "embedded runtime profile snapshot") !=
                profile.DuckStationExecutableSha256 ||
            RequireCaptureSha256(snapshot, "handoffReceiptSha256", "embedded runtime profile snapshot") !=
                profile.HandoffReceiptSha256 ||
            RequireCaptureSha256(snapshot, "appContentManifestSha256", "embedded runtime profile snapshot") !=
                profile.AppContentManifestSha256 ||
            RequireCaptureSha256(snapshot, "appOutputTreeManifestSha256", "embedded runtime profile snapshot") !=
                profile.AppOutputTreeManifestSha256 ||
            RequireCaptureSha256(snapshot, "packageEnvelopeSha256", "embedded runtime profile snapshot") !=
                profile.PackageEnvelopeSha256 ||
            RequireCaptureSha256(snapshot, "externalHomeSettingsSha256", "embedded runtime profile snapshot") !=
                profile.ExternalHomeSettingsSha256 ||
            RequireCaptureSha256(snapshot, "externalHomeBiosSha256", "embedded runtime profile snapshot") !=
                profile.ExternalHomeBiosSha256 ||
            RequireCaptureString(snapshot, "hostNormalSettingsSentinelId", "embedded runtime profile snapshot") !=
                profile.HostNormalSettingsSentinelId ||
            RequireCaptureSha256(snapshot, "hostNormalSettingsSentinelSha256", "embedded runtime profile snapshot") !=
                profile.HostNormalSettingsSha256)
        {
            throw new InvalidDataException("Embedded runtime profile snapshot identity changed.");
        }
        _ = ParseCanonicalUtc(expectedCapturedAtUtc, "embedded runtime profile snapshot time");
        JsonElement dataFiles = RequireCaptureProperty(snapshot, "dataFiles", "embedded runtime profile snapshot");
        if (dataFiles.ValueKind != JsonValueKind.Array || dataFiles.GetArrayLength() is < 1 or > 100_000)
            throw new InvalidDataException("Embedded runtime profile snapshot dataFiles is invalid.");
        List<string> paths = [];
        Dictionary<string, (long Length, string Sha256)> pinned = new(StringComparer.Ordinal);
        foreach (JsonElement entry in dataFiles.EnumerateArray())
        {
            RequireCaptureObjectKeys(entry, ["relativePath", "byteLength", "sha256"],
                "embedded runtime profile snapshot entry");
            string relativePath = RequireCaptureString(
                entry,
                "relativePath",
                "embedded runtime profile snapshot entry");
            GuardProfileManifestPath(relativePath);
            long byteLength = RequireCaptureInt64(
                entry,
                "byteLength",
                "embedded runtime profile snapshot entry");
            string sha256 = RequireCaptureSha256(
                entry,
                "sha256",
                "embedded runtime profile snapshot entry");
            if (!IsAllowedRuntimeDataPath(relativePath) || byteLength < 0 ||
                byteLength > MaximumJcsSafeInteger)
            {
                throw new InvalidDataException("Embedded runtime profile snapshot entry is unapproved.");
            }
            paths.Add(relativePath);
            if (!pinned.TryAdd(relativePath, (byteLength, sha256)))
                throw new InvalidDataException("Embedded runtime profile snapshot path is duplicated.");
        }
        if (!paths.SequenceEqual(paths.Order(UnicodeScalarStringComparer.Instance)) ||
            !pinned.TryGetValue("settings.ini", out var settings) ||
            settings != (profile.ExternalHomeSettingsByteLength, profile.ExternalHomeSettingsSha256) ||
            !pinned.TryGetValue(profile.ExternalHomeBiosRelativePath, out var bios) ||
            bios != (profile.ExternalHomeBiosByteLength, profile.ExternalHomeBiosSha256))
        {
            throw new InvalidDataException(
                "Embedded runtime profile snapshot dataFiles changed its sorted pinned closure.");
        }
    }

    private static string BuildCaptureClosureSha256(
        string runId,
        string runOriginSha256,
        IReadOnlyList<CaptureJournalReadback> journals,
        IReadOnlyList<string> liveHashes,
        IReadOnlyList<Id65V3RuntimeEvidenceArtifact> artifacts)
    {
        StringBuilder manifest = new();
        manifest.Append("id65-v3-runtime-capture-closure-v2\n")
            .Append("runId=").Append(runId).Append('\n')
            .Append("runOriginSha256=").Append(runOriginSha256).Append('\n');
        for (int ordinal = 0; ordinal < journals.Count; ordinal++)
        {
            CaptureJournalReadback journal = journals[ordinal];
            string journalSha256 = artifacts.Single(item =>
                item.ArtifactId == journal.ArtifactId).Sha256;
            manifest.Append("session=")
                .Append(ordinal.ToString(CultureInfo.InvariantCulture)).Append('|')
                .Append(journal.SessionId).Append('|')
                .Append(journalSha256).Append('|')
                .Append(liveHashes[ordinal]).Append('|')
                .Append(journal.TerminalEventSha256).Append('\n');
        }
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts.OrderBy(
                     item => item.ArtifactId,
                     StringComparer.Ordinal))
        {
            manifest.Append("artifact=").Append(artifact.ArtifactId).Append('|')
                .Append(artifact.Sha256).Append('|')
                .Append(artifact.ByteLength.ToString(CultureInfo.InvariantCulture)).Append('\n');
        }
        return HashUtf8(manifest.ToString());
    }

    private static CaptureJournalReadback ParseAndValidateCaptureJournal(
        byte[] bytes,
        Id65V3RuntimeEvidenceArtifact artifact,
        Id65V3RuntimeEvidenceSession session,
        int ordinal,
        string expectedPreviousTerminalHash,
        string? previousTerminalAtUtc,
        long? previousTerminalMonotonic,
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceReadbackContext readbackContext)
    {
        JsonElement root = ParseCanonicalArtifactElement(
            bytes,
            $"capture journal {artifact.ArtifactId}");
        RequireCaptureObjectKeys(
            root,
            [
                "bootIdentity", "events", "format", "formatVersion",
                "processInstanceId", "runOriginSha256", "runId", "sessionId",
                "sessionOrdinal", .. CapturePinPropertyNames
            ],
            "capture-journal root");
        string format = RequireCaptureString(root, "format", "capture journal");
        int version = RequireCaptureInt32(root, "formatVersion", "capture journal");
        string runId = RequireCaptureString(root, "runId", "capture journal");
        string sessionId = RequireCaptureString(root, "sessionId", "capture journal");
        int sessionOrdinal = RequireCaptureInt32(root, "sessionOrdinal", "capture journal");
        string processInstanceId = RequireCaptureString(
            root,
            "processInstanceId",
            "capture journal");
        string bootIdentity = RequireCaptureSha256(root, "bootIdentity", "capture journal");
        string runOriginSha256 = RequireCaptureSha256(
            root,
            "runOriginSha256",
            "capture journal");
        if (format != CaptureJournalFormat || version != CaptureJournalFormatVersion ||
            !CaptureRunIdRegex().IsMatch(runId) || sessionId != session.SessionId ||
            sessionOrdinal != ordinal || !CaptureProcessIdRegex().IsMatch(processInstanceId) ||
            processInstanceId != session.ProcessInstanceId)
        {
            throw new InvalidDataException(
                $"Capture journal {artifact.ArtifactId} header identity or source pin changed.");
        }
        ValidateCapturePinClosure(root, package, "capture journal");
        Id65V3RuntimeCaptureJournalReference reference = session.CaptureJournal ??
            throw new InvalidDataException($"Session {session.SessionId} has no typed capture-journal reference.");
        List<string> changedReferenceFields = [];
        if (reference.ArtifactId != artifact.ArtifactId) changedReferenceFields.Add("artifactId");
        if (reference.Format != format) changedReferenceFields.Add("format");
        if (reference.FormatVersion != version)
            changedReferenceFields.Add($"formatVersion({reference.FormatVersion}!={version})");
        if (reference.RunId != runId) changedReferenceFields.Add("runId");
        if (reference.SessionOrdinal != ordinal) changedReferenceFields.Add("sessionOrdinal");
        if (reference.SessionId != sessionId) changedReferenceFields.Add("sessionId");
        if (reference.BootIdentity != bootIdentity) changedReferenceFields.Add("bootIdentity");
        if (reference.ProcessInstanceId != processInstanceId) changedReferenceFields.Add("processInstanceId");
        if (changedReferenceFields.Count != 0)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} typed capture-journal reference changed: " +
                string.Join(",", changedReferenceFields) + ".");
        }
        string expectedJournalPath = RuntimeAssetPrefix + "capture-staging/" + runId + "/" +
            ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + sessionId +
            "/capture-event-journal.json";
        if (artifact.SessionId != sessionId || artifact.RepoRelativePath != expectedJournalPath)
            throw new InvalidDataException(
                $"Capture journal {artifact.ArtifactId} is outside its exact staged session path.");

        JsonElement eventsElement = RequireCaptureProperty(root, "events", "capture journal");
        if (eventsElement.ValueKind != JsonValueKind.Array ||
            eventsElement.GetArrayLength() is < 1 or > MaximumCaptureJournalEvents)
        {
            throw new InvalidDataException("Capture journal event array is empty or oversized.");
        }
        List<CaptureJournalEventReadback> events = [];
        string previousHash = expectedPreviousTerminalHash;
        DateTimeOffset? priorAt = null;
        long? priorMonotonic = null;
        foreach (JsonElement eventElement in eventsElement.EnumerateArray())
        {
            int sequence = events.Count;
            RequireCaptureObjectKeys(
                eventElement,
                [
                    "atUtc", "eventSha256", "eventType", "monotonicMicroseconds",
                    "payload", "previousEventSha256", "sequence"
                ],
                $"capture event {sequence}");
            if (RequireCaptureInt32(eventElement, "sequence", "capture event") != sequence)
                throw new InvalidDataException("Capture journal event sequence changed.");
            string eventType = RequireCaptureString(eventElement, "eventType", "capture event");
            string atUtc = RequireCaptureString(eventElement, "atUtc", "capture event");
            DateTimeOffset parsedAt = ParseCanonicalUtc(atUtc, $"capture event {sequence}");
            long monotonic = RequireCaptureInt64(
                eventElement,
                "monotonicMicroseconds",
                "capture event");
            if (monotonic < 0)
                throw new InvalidDataException("Capture journal monotonic clock is negative.");
            string eventPrevious = RequireCaptureSha256(
                eventElement,
                "previousEventSha256",
                "capture event");
            string eventHash = RequireCaptureSha256(
                eventElement,
                "eventSha256",
                "capture event");
            if (eventPrevious != previousHash)
                throw new InvalidDataException("Capture journal previous-event hash chain changed.");
            if (priorAt is DateTimeOffset previousAt && priorMonotonic is long previousMono)
            {
                if (!CaptureWithinSessionClockStepIsAccepted(
                        previousAt,
                        previousMono,
                        parsedAt,
                        monotonic))
                {
                    throw new InvalidDataException("Capture journal UTC and monotonic clocks diverged.");
                }
            }
            else if (previousTerminalAtUtc is not null && previousTerminalMonotonic is long priorTerminalMono)
            {
                DateTimeOffset terminalAt = ParseCanonicalUtc(
                    previousTerminalAtUtc,
                    "previous capture terminal");
                long utcDelta = checked(
                    (long)(parsedAt - terminalAt).TotalMilliseconds * 1_000);
                long monotonicDelta = checked(monotonic - priorTerminalMono);
                if (utcDelta <= 0 || monotonicDelta <= 0)
                    throw new InvalidDataException("Capture journal overlaps the preceding cold session.");
                if (!CaptureCrossSessionClockStepIsAccepted(
                        terminalAt,
                        priorTerminalMono,
                        parsedAt,
                        monotonic))
                {
                    throw new InvalidDataException("Cross-session capture clocks diverged.");
                }
            }
            JsonElement payload = RequireCaptureProperty(eventElement, "payload", "capture event");
            if (payload.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Capture journal event payload is not an object.");
            ValidateCaptureEventPayloadShape(eventType, payload, package, session);
            string expectedEventHash = RecomputeCaptureEventSha256(root, eventElement, payload);
            if (eventHash != expectedEventHash)
                throw new InvalidDataException("Capture journal event hash changed.");
            events.Add(new(
                sequence,
                eventType,
                atUtc,
                monotonic,
                eventHash,
                payload.Clone()));
            previousHash = eventHash;
            priorAt = parsedAt;
            priorMonotonic = monotonic;
        }

        ValidateCaptureJournalFsm(sessionId, events);
        if (RequireCaptureString(
                events[0].Payload,
                "previousSessionTerminalSha256",
                "capture session-open") != expectedPreviousTerminalHash)
        {
            throw new InvalidDataException(
                $"Capture journal {artifact.ArtifactId} session-open previous-terminal link changed.");
        }
        if (events[^1].EventType != "session-sealed" ||
            reference.TerminalEventSha256 != events[^1].EventSha256 ||
            artifact.CapturedAtUtc != events[^1].AtUtc)
        {
            throw new InvalidDataException(
                $"Capture journal {artifact.ArtifactId} terminal reference changed.");
        }
        CaptureRawArtifactReadback[] rawArtifacts = ExtractCaptureRawArtifacts(
            events,
            session,
            runId,
            ordinal);
        ValidateCaptureJournalRuntimeBindings(
            events,
            session,
            runId,
            bootIdentity,
            rawArtifacts,
            package,
            artifactBytes,
            readbackContext);
        return new(
            artifact.ArtifactId,
            runId,
            ordinal,
            sessionId,
            bootIdentity,
            processInstanceId,
            runOriginSha256,
            expectedPreviousTerminalHash,
            events[^1].EventSha256,
            events[0].AtUtc,
            events[0].MonotonicMicroseconds,
            events[^1].AtUtc,
            events[^1].MonotonicMicroseconds,
            CaptureRawProcessFingerprint(
                RequireOneCaptureEvent(events, "process-start-observed").Payload),
            events.ToArray(),
            rawArtifacts);
    }

    internal static bool CaptureWithinSessionClockStepIsAccepted(
        DateTimeOffset previousAtUtc,
        long previousMonotonicMicroseconds,
        DateTimeOffset currentAtUtc,
        long currentMonotonicMicroseconds)
    {
        long utcDelta = checked(
            (long)(currentAtUtc - previousAtUtc).TotalMilliseconds * 1_000);
        long monotonicDelta = checked(
            currentMonotonicMicroseconds - previousMonotonicMicroseconds);
        return utcDelta >= 0 && monotonicDelta > 0 &&
            Math.Abs(utcDelta - monotonicDelta) <= 250_000;
    }

    internal static bool CaptureCrossSessionClockStepIsAccepted(
        DateTimeOffset previousAtUtc,
        long previousMonotonicMicroseconds,
        DateTimeOffset currentAtUtc,
        long currentMonotonicMicroseconds)
    {
        long utcDelta = checked(
            (long)(currentAtUtc - previousAtUtc).TotalMilliseconds * 1_000);
        long monotonicDelta = checked(
            currentMonotonicMicroseconds - previousMonotonicMicroseconds);
        return utcDelta > 0 && monotonicDelta > 0 &&
            Math.Abs(utcDelta - monotonicDelta) <= 250_000;
    }

    private static JsonElement ParseCanonicalArtifactElement(byte[] bytes, string context)
    {
        if (bytes.Length == 0 || bytes.Length > MaximumTelemetryLogBytes ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException($"{context} is empty, oversized, or has a UTF-8 BOM.");
        }
        _ = DecodeStrictUtf8(bytes, context);
        using JsonDocument document = JsonDocument.Parse(
            bytes,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = 64
            });
        List<string> errors = [];
        RejectDuplicateProperties(document.RootElement, "$", errors);
        if (errors.Count != 0)
            throw new InvalidDataException(errors[0]);
        StringBuilder canonical = new();
        WriteJcsElement(canonical, document.RootElement);
        if (!bytes.AsSpan().SequenceEqual(StrictUtf8.GetBytes(canonical.ToString())))
            throw new InvalidDataException($"{context} is not exact canonical UTF-8 JSON.");
        return document.RootElement.Clone();
    }

    private static string RecomputeCaptureEventSha256(
        JsonElement root,
        JsonElement eventElement,
        JsonElement payload)
    {
        StringBuilder canonicalPayload = new();
        WriteJcsElement(canonicalPayload, payload);
        string payloadSha256 = HashUtf8(canonicalPayload.ToString());
        StringBuilder manifest = new(
            "id65-v3-runtime-capture-event-v2\n" +
            $"format={RequireCaptureString(root, "format", "capture journal")}\n" +
            $"formatVersion={RequireCaptureInt32(root, "formatVersion", "capture journal")}\n" +
            $"runId={RequireCaptureString(root, "runId", "capture journal")}\n" +
            $"sessionId={RequireCaptureString(root, "sessionId", "capture journal")}\n" +
            $"sessionOrdinal={RequireCaptureInt32(root, "sessionOrdinal", "capture journal")}\n" +
            $"processInstanceId={RequireCaptureString(root, "processInstanceId", "capture journal")}\n" +
            $"runOriginSha256={RequireCaptureString(root, "runOriginSha256", "capture journal")}\n" +
            $"bootIdentity={RequireCaptureString(root, "bootIdentity", "capture journal")}\n");
        foreach (string name in CapturePinPropertyNames)
        {
            manifest.Append(name).Append('=')
                .Append(RequireCaptureString(root, name, "capture journal")).Append('\n');
        }
        manifest.Append(
            $"sequence={RequireCaptureInt32(eventElement, "sequence", "capture event")}\n" +
            $"eventType={RequireCaptureString(eventElement, "eventType", "capture event")}\n" +
            $"atUtc={RequireCaptureString(eventElement, "atUtc", "capture event")}\n" +
            $"monotonicMicroseconds={RequireCaptureInt64(eventElement, "monotonicMicroseconds", "capture event")}\n" +
            $"previousEventSha256={RequireCaptureString(eventElement, "previousEventSha256", "capture event")}\n" +
            $"payloadSha256={payloadSha256}\n");
        return HashUtf8(manifest.ToString());
    }

    private static void ValidateCaptureEventPayloadShape(
        string eventType,
        JsonElement payload,
        Id65V3RuntimeEvidencePackage package,
        Id65V3RuntimeEvidenceSession session)
    {
        switch (eventType)
        {
            case "session-open":
                RequireCaptureObjectKeys(payload, ["previousSessionTerminalSha256"], eventType);
                _ = RequireCaptureSha256(payload, "previousSessionTerminalSha256", eventType);
                return;
            case "profile-before-captured":
            case "profile-after-captured":
                RequireCaptureObjectKeys(payload, CaptureArtifactPayloadKeys, eventType);
                ValidateCaptureArtifactPayload(
                    payload,
                    eventType == "profile-before-captured"
                        ? "runtime-profile-before"
                        : "runtime-profile-after");
                return;
            case "process-start-observed":
                RequireCaptureObjectKeys(payload, CaptureProcessPayloadKeys, eventType);
                if (RequireCaptureString(payload, "provider", eventType) !=
                    "macos-libproc+kern-procargs2-external-home-v2")
                    throw new InvalidDataException("Capture process provider changed.");
                foreach (string name in new[]
                {
                    "pid", "startSeconds", "executableDevice", "executableInode",
                    "executableByteLength"
                })
                {
                    if (RequireCaptureInt64(payload, name, eventType) <= 0)
                        throw new InvalidDataException($"Capture process {name} is not positive.");
                }
                _ = RequireCaptureInt64(payload, "parentPid", eventType);
                long processUsec = RequireCaptureInt64(payload, "startMicroseconds", eventType);
                if (processUsec is < 0 or >= 1_000_000)
                    throw new InvalidDataException("Capture process start microseconds are out of range.");
                _ = RequireCaptureSha256(payload, "argvNulSha256", eventType);
                _ = RequireCaptureSha256(payload, "environmentNulSha256", eventType);
                _ = RequireCaptureSha256(payload, "executableSha256", eventType);
                _ = RequireCaptureString(payload, "externalHomeRealPath", eventType);
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "observedAtUtc", eventType),
                    "capture process observation");
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "processStartedAtUtc", eventType),
                    "capture process start");
                JsonElement argv = RequireCaptureProperty(payload, "argv", eventType);
                if (argv.ValueKind != JsonValueKind.Array || argv.GetArrayLength() == 0 ||
                    argv.EnumerateArray().Any(item =>
                        item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString())))
                {
                    throw new InvalidDataException("Capture process argv is invalid.");
                }
                return;
            case "disc-open-observed":
                RequireCaptureObjectKeys(payload, CaptureDiscPayloadKeys, eventType);
                if (RequireCaptureString(payload, "provider", eventType) !=
                        "macos-libproc-argv-plus-open-bin-v1" ||
                    RequireCaptureString(payload, "bindingMode", eventType) !=
                        "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1" ||
                    !RequireCaptureBoolean(payload, "binOpen", eventType) ||
                    !RequireCaptureBoolean(payload, "failedV1Excluded", eventType) ||
                    RequireCaptureString(payload, "cueRepoRelativePath", eventType) !=
                        package.SourceIdentity.CandidateCueRelativePath ||
                    RequireCaptureString(payload, "binRepoRelativePath", eventType) !=
                        package.SourceIdentity.CandidateBinRelativePath ||
                    RequireCaptureSha256(payload, "cueSha256", eventType) !=
                        package.SourceIdentity.CandidateCueSha256 ||
                    RequireCaptureSha256(payload, "binSha256", eventType) !=
                        package.SourceIdentity.CandidateBinSha256)
                {
                    throw new InvalidDataException("Capture disc-open observation changed its fixed identity.");
                }
                foreach (string name in new[]
                {
                    "pid", "gdbListenerPid", "startSeconds", "cueDevice", "cueInode",
                    "cueByteLength", "cueSourceLinkCount", "binDevice", "binInode",
                    "binByteLength", "binLinkCount"
                })
                {
                    if (RequireCaptureInt64(payload, name, eventType) <= 0)
                        throw new InvalidDataException($"Capture disc {name} is not positive.");
                }
                _ = RequireCaptureInt64(payload, "startMicroseconds", eventType);
                foreach (string name in new[]
                {
                    "binFd", "cueRealPath", "binRealPath", "observedAtUtc"
                })
                    _ = RequireCaptureString(payload, name, eventType);
                return;
            case "watcher-connected":
                RequireCaptureObjectKeys(payload, ["ownedWatcherPid", "provider"], eventType);
                if (RequireCaptureInt64(payload, "ownedWatcherPid", eventType) <= 0 ||
                    RequireCaptureString(payload, "provider", eventType) !=
                        "pinned-watcher-subprocess-v1")
                {
                    throw new InvalidDataException("Capture watcher connection identity changed.");
                }
                return;
            case "manually-resumed":
                RequireCaptureObjectKeys(payload, ["operatorConfirmed"], eventType);
                if (!RequireCaptureBoolean(payload, "operatorConfirmed", eventType))
                    throw new InvalidDataException("Capture manual-resume confirmation is false.");
                return;
            case "selector-started":
            case "landing-marked":
            case "hold-started":
            case "hold-ended":
            case "fall-started":
            case "respawn-marked":
            case "check-started":
            case "check-ended":
                RequireCaptureObjectKeys(payload, ["markerId"], eventType);
                _ = RequireCaptureString(payload, "markerId", eventType);
                return;
            case "frame-captured":
                RequireCaptureObjectKeys(payload, ["artifactId", "captureRole"], eventType);
                _ = RequireCaptureString(payload, "artifactId", eventType);
                _ = RequireCaptureString(payload, "captureRole", eventType);
                return;
            case "recording-started":
                RequireCaptureObjectKeys(payload, CaptureRecordingStartPayloadKeys, eventType);
                ValidateCaptureWindowPayload(payload, eventType);
                if (RequireCaptureString(payload, "backend", eventType) !=
                    "macos-screencapture-window-v1")
                {
                    throw new InvalidDataException("Capture recording backend changed.");
                }
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "mediaStartAtUtc", eventType),
                    "recording media start");
                _ = RequireCaptureInt64(payload, "mediaStartMonotonicMicroseconds", eventType);
                if (RequireCaptureInt64(payload, "ownedRecorderPid", eventType) <= 0)
                    throw new InvalidDataException("Capture recorder PID is not positive.");
                return;
            case "recording-stopped":
                RequireCaptureObjectKeys(
                    payload,
                    [
                        "artifactId", "mediaEndAtUtc", "mediaEndMonotonicMicroseconds",
                        "ownedRecorderPid"
                    ],
                    eventType);
                _ = RequireCaptureString(payload, "artifactId", eventType);
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "mediaEndAtUtc", eventType),
                    "recording media end");
                _ = RequireCaptureInt64(payload, "mediaEndMonotonicMicroseconds", eventType);
                if (RequireCaptureInt64(payload, "ownedRecorderPid", eventType) <= 0)
                    throw new InvalidDataException("Capture recorder PID is not positive.");
                return;
            case "watcher-stopped":
                RequireCaptureObjectKeys(
                    payload,
                    CaptureArtifactPayloadKeys.Append("ownedWatcherPid"),
                    eventType);
                ValidateCaptureArtifactPayload(payload, "watcher-stdout");
                if (RequireCaptureInt64(payload, "ownedWatcherPid", eventType) <= 0)
                    throw new InvalidDataException("Capture watcher PID is not positive.");
                return;
            case "watcher-marker-bound":
                RequireCaptureObjectKeys(
                    payload,
                    [
                        "markerRole", "operatorEventSequence", "sampleAtUtc",
                        "sampleElapsedMilliseconds", "sampleOrdinal"
                    ],
                    eventType);
                _ = RequireCaptureString(payload, "markerRole", eventType);
                foreach (string name in new[]
                {
                    "operatorEventSequence", "sampleElapsedMilliseconds", "sampleOrdinal"
                })
                {
                    if (RequireCaptureInt64(payload, name, eventType) < 0)
                        throw new InvalidDataException($"Capture watcher binding {name} is negative.");
                }
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "sampleAtUtc", eventType),
                    "watcher marker sample");
                return;
            case "process-exit-observed":
                RequireCaptureObjectKeys(
                    payload,
                    ["pid", "processExitedAtUtc", "startMicroseconds", "startSeconds"],
                    eventType);
                if (RequireCaptureInt64(payload, "pid", eventType) <= 0 ||
                    RequireCaptureInt64(payload, "startSeconds", eventType) <= 0)
                {
                    throw new InvalidDataException("Capture process-exit identity is invalid.");
                }
                _ = RequireCaptureInt64(payload, "startMicroseconds", eventType);
                _ = ParseCanonicalUtc(
                    RequireCaptureString(payload, "processExitedAtUtc", eventType),
                    "capture process exit");
                return;
            case "media-artifact-bound":
                string kind = RequireCaptureString(payload, "kind", eventType);
                RequireCaptureObjectKeys(
                    payload,
                    kind == "screen-recording"
                        ? CaptureRecordingMediaPayloadKeys
                        : CaptureMediaPayloadKeys,
                    eventType);
                if (kind is not ("frame" or "screen-recording"))
                    throw new InvalidDataException("Capture media artifact kind is invalid.");
                ValidateCaptureArtifactPayload(payload, kind);
                ValidateCaptureWindowPayload(payload, eventType);
                if (RequireCaptureString(payload, "backend", eventType) !=
                    "macos-screencapture-window-v1")
                {
                    throw new InvalidDataException("Capture media backend changed.");
                }
                if (kind == "screen-recording")
                {
                    _ = ParseCanonicalUtc(
                        RequireCaptureString(payload, "mediaStartAtUtc", eventType),
                        "recording media start");
                    _ = ParseCanonicalUtc(
                        RequireCaptureString(payload, "mediaEndAtUtc", eventType),
                        "recording media end");
                    _ = RequireCaptureInt64(payload, "mediaStartMonotonicMicroseconds", eventType);
                    _ = RequireCaptureInt64(payload, "mediaEndMonotonicMicroseconds", eventType);
                    if (RequireCaptureInt64(payload, "ownedRecorderPid", eventType) <= 0)
                        throw new InvalidDataException("Capture recorder PID is not positive.");
                }
                return;
            case "session-sealed":
                RequireCaptureObjectKeys(payload, ["rawArtifactIds"], eventType);
                JsonElement ids = RequireCaptureProperty(payload, "rawArtifactIds", eventType);
                if (ids.ValueKind != JsonValueKind.Array || ids.EnumerateArray().Any(item =>
                        item.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(item.GetString())))
                {
                    throw new InvalidDataException("Capture terminal raw artifact IDs are invalid.");
                }
                string[] values = ids.EnumerateArray().Select(item => item.GetString()!).ToArray();
                if (!values.SequenceEqual(values.Order(StringComparer.Ordinal)) ||
                    values.Distinct(StringComparer.Ordinal).Count() != values.Length)
                {
                    throw new InvalidDataException("Capture terminal raw artifact IDs are not sorted unique.");
                }
                return;
            default:
                throw new InvalidDataException(
                    $"Capture journal contains unknown event type '{eventType}'.");
        }
    }

    private static readonly string[] CaptureArtifactPayloadKeys =
    [
        "artifactId", "byteLength", "capturedAtUtc", "kind", "repoRelativePath", "sha256"
    ];

    private static readonly string[] CaptureProcessPayloadKeys =
    [
        "argv", "argvNulSha256", "environmentNulSha256", "executableByteLength", "executableDevice",
        "executableInode", "executableRealPath", "executableSha256", "observedAtUtc",
        "externalHomeRealPath", "parentPid", "pid", "processStartedAtUtc", "provider",
        "startMicroseconds", "startSeconds"
    ];

    private static readonly string[] CapturePinPropertyNames =
    [
        "watcherRelativePath", "watcherSha256",
        "profileBuilderRelativePath", "profileBuilderSha256",
        "profileVerifierRelativePath", "profileVerifierSha256",
        "automationRelativePath", "automationSha256",
        "captureCoordinatorRelativePath", "captureCoordinatorSha256",
        "sourceClosureSha256", "runtimeProfileSha256",
        "profilePackageRelativePath", "profileId", "handoffReceiptSha256",
        "appContentManifestSha256", "appOutputTreeManifestSha256",
        "homeOutputTreeManifestSha256", "packageEnvelopeSha256",
        "externalHomeSettingsSha256", "externalHomeBiosSha256",
        "hostNormalSettingsSentinelSha256"
    ];

    private static readonly string[] CaptureDiscPayloadKeys =
    [
        "bindingMode", "binByteLength", "binDevice", "binFd", "binInode", "binLinkCount",
        "binOpen", "binRealPath", "binRepoRelativePath", "binSha256", "cueByteLength",
        "cueDevice", "cueInode", "cueRealPath", "cueRepoRelativePath", "cueSha256",
        "cueSourceLinkCount", "failedV1Excluded", "gdbListenerPid", "observedAtUtc",
        "pid", "provider", "startMicroseconds", "startSeconds"
    ];

    private static readonly string[] CaptureWindowPayloadKeys =
    [
        "backend", "boundsHeight", "boundsWidth", "boundsX", "boundsY", "captureRole",
        "ownerPid", "ownerStartMicroseconds", "ownerStartSeconds", "windowId",
        "windowOwnerPid"
    ];

    private static readonly string[] CaptureRecordingStartPayloadKeys =
    [
        "artifactId", "mediaStartAtUtc", "mediaStartMonotonicMicroseconds",
        "ownedRecorderPid", .. CaptureWindowPayloadKeys
    ];

    private static readonly string[] CaptureMediaPayloadKeys =
        [.. CaptureArtifactPayloadKeys, .. CaptureWindowPayloadKeys];

    private static readonly string[] CaptureRecordingMediaPayloadKeys =
    [
        .. CaptureMediaPayloadKeys,
        "mediaEndAtUtc", "mediaEndMonotonicMicroseconds", "mediaStartAtUtc",
        "mediaStartMonotonicMicroseconds", "ownedRecorderPid"
    ];

    private static void ValidateCapturePinClosure(
        JsonElement value,
        Id65V3RuntimeEvidencePackage package,
        string context)
    {
        foreach (string name in CapturePinPropertyNames)
        {
            string expected = name switch
            {
                "watcherRelativePath" => package.SourceIdentity.WatcherRelativePath,
                "watcherSha256" => package.SourceIdentity.WatcherSha256,
                "profileBuilderRelativePath" => package.SourceIdentity.ProfileBuilderRelativePath,
                "profileBuilderSha256" => package.SourceIdentity.ProfileBuilderSha256,
                "profileVerifierRelativePath" => package.SourceIdentity.ProfileVerifierRelativePath,
                "profileVerifierSha256" => package.SourceIdentity.ProfileVerifierSha256,
                "automationRelativePath" => package.SourceIdentity.AutomationRelativePath,
                "automationSha256" => package.SourceIdentity.AutomationSha256,
                "captureCoordinatorRelativePath" => package.SourceIdentity.CaptureCoordinatorRelativePath,
                "captureCoordinatorSha256" => package.SourceIdentity.CaptureCoordinatorSha256,
                "sourceClosureSha256" => ExpectedSourceClosureSha256,
                "runtimeProfileSha256" => ExpectedRuntimeProfileSha256,
                "profilePackageRelativePath" => package.RuntimeProfile.PackageRelativePath,
                "profileId" => package.RuntimeProfile.ProfileId,
                "handoffReceiptSha256" => package.RuntimeProfile.HandoffReceiptSha256,
                "appContentManifestSha256" => package.RuntimeProfile.AppContentManifestSha256,
                "appOutputTreeManifestSha256" => package.RuntimeProfile.AppOutputTreeManifestSha256,
                "homeOutputTreeManifestSha256" => package.RuntimeProfile.HomeOutputTreeManifestSha256,
                "packageEnvelopeSha256" => package.RuntimeProfile.PackageEnvelopeSha256,
                "externalHomeSettingsSha256" => package.RuntimeProfile.ExternalHomeSettingsSha256,
                "externalHomeBiosSha256" => package.RuntimeProfile.ExternalHomeBiosSha256,
                "hostNormalSettingsSentinelSha256" => package.RuntimeProfile.HostNormalSettingsSha256,
                _ => throw new InvalidDataException("Capture pin closure contains an unknown field.")
            };
            if (RequireCaptureString(value, name, context) != expected)
                throw new InvalidDataException($"{context} pin changed: {name}.");
        }
    }

    private static void ValidateCaptureArtifactPayload(JsonElement payload, string expectedKind)
    {
        _ = RequireCaptureString(payload, "artifactId", "capture artifact");
        if (RequireCaptureInt64(payload, "byteLength", "capture artifact") <= 0)
            throw new InvalidDataException("Capture artifact byte length is not positive.");
        _ = ParseCanonicalUtc(
            RequireCaptureString(payload, "capturedAtUtc", "capture artifact"),
            "capture artifact timestamp");
        if (RequireCaptureString(payload, "kind", "capture artifact") != expectedKind)
            throw new InvalidDataException("Capture artifact kind changed.");
        string path = RequireCaptureString(payload, "repoRelativePath", "capture artifact");
        if (!path.StartsWith(RuntimeAssetPrefix + "capture-staging/", StringComparison.Ordinal))
            throw new InvalidDataException("Capture artifact escaped its raw staging prefix.");
        _ = RequireCaptureSha256(payload, "sha256", "capture artifact");
    }

    private static void ValidateCaptureWindowPayload(JsonElement payload, string context)
    {
        foreach (string name in new[]
        {
            "boundsHeight", "boundsWidth", "ownerPid", "ownerStartSeconds",
            "windowId", "windowOwnerPid"
        })
        {
            if (RequireCaptureInt64(payload, name, context) <= 0)
                throw new InvalidDataException($"Capture window {name} is not positive.");
        }
        foreach (string name in new[] { "boundsX", "boundsY", "ownerStartMicroseconds" })
            _ = RequireCaptureInt64(payload, name, context);
        _ = RequireCaptureString(payload, "captureRole", context);
    }

    private static void RequireCaptureObjectKeys(
        JsonElement value,
        IEnumerable<string> expected,
        string context)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException($"{context} is not an object.");
        string[] actual = value.EnumerateObject().Select(property => property.Name)
            .Order(StringComparer.Ordinal).ToArray();
        string[] wanted = expected.Order(StringComparer.Ordinal).ToArray();
        if (!actual.SequenceEqual(wanted, StringComparer.Ordinal))
            throw new InvalidDataException($"{context} has an unknown or missing property.");
    }

    private static JsonElement RequireCaptureProperty(
        JsonElement value,
        string name,
        string context) =>
        value.TryGetProperty(name, out JsonElement property)
            ? property
            : throw new InvalidDataException($"{context} omits {name}.");

    private static string RequireCaptureString(
        JsonElement value,
        string name,
        string context)
    {
        JsonElement property = RequireCaptureProperty(value, name, context);
        if (property.ValueKind != JsonValueKind.String || string.IsNullOrEmpty(property.GetString()))
            throw new InvalidDataException($"{context} {name} is not nonempty text.");
        return property.GetString()!;
    }

    private static long RequireCaptureInt64(
        JsonElement value,
        string name,
        string context)
    {
        JsonElement property = RequireCaptureProperty(value, name, context);
        if (property.ValueKind != JsonValueKind.Number || !property.TryGetInt64(out long result) ||
            result is < -MaximumJcsSafeInteger or > MaximumJcsSafeInteger)
        {
            throw new InvalidDataException($"{context} {name} is not an exact I-JSON integer.");
        }
        return result;
    }

    private static int RequireCaptureInt32(
        JsonElement value,
        string name,
        string context)
    {
        long result = RequireCaptureInt64(value, name, context);
        if (result is < int.MinValue or > int.MaxValue)
            throw new InvalidDataException($"{context} {name} is outside Int32.");
        return checked((int)result);
    }

    private static bool RequireCaptureBoolean(
        JsonElement value,
        string name,
        string context)
    {
        JsonElement property = RequireCaptureProperty(value, name, context);
        return property.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException($"{context} {name} is not Boolean.")
        };
    }

    private static string RequireCaptureSha256(
        JsonElement value,
        string name,
        string context)
    {
        string result = RequireCaptureString(value, name, context);
        if (!Sha256Regex().IsMatch(result))
            throw new InvalidDataException($"{context} {name} is not lowercase SHA-256.");
        return result;
    }

    private static void ValidateCaptureJournalFsm(
        string sessionId,
        IReadOnlyList<CaptureJournalEventReadback> events)
    {
        List<(string Type, string? Discriminator)> expected =
        [
            ("session-open", null),
            ("profile-before-captured", null),
            ("process-start-observed", null),
            ("disc-open-observed", null),
            ("watcher-connected", null),
            ("manually-resumed", null),
            ("recording-started", null),
            ("selector-started", "selector")
        ];
        if (sessionId == "candidate-primary-edge-0")
        {
            expected.AddRange(
            [
                ("check-started", "candidate.completed-landing-hud4"),
                ("landing-marked", "landing"),
                ("check-ended", "candidate.completed-landing-hud4")
            ]);
        }
        else
        {
            expected.Add(("landing-marked", "landing"));
        }
        expected.AddRange(ExpectedCaptureMarkerSequence(sessionId));
        expected.Add(("recording-stopped", null));
        expected.Add(("watcher-stopped", null));
        expected.AddRange(ExpectedCaptureWatcherRoles(sessionId).Select(role =>
            ("watcher-marker-bound", (string?)role)));
        expected.Add(("process-exit-observed", null));
        expected.Add(("profile-after-captured", null));
        string[] mediaIds = events.Where(item => item.EventType == "media-artifact-bound")
            .Select(item => RequireCaptureString(item.Payload, "artifactId", "capture media"))
            .ToArray();
        if (!mediaIds.SequenceEqual(mediaIds.Order(StringComparer.Ordinal)) ||
            mediaIds.Distinct(StringComparer.Ordinal).Count() != mediaIds.Length)
        {
            throw new InvalidDataException("Capture journal media bindings are not sorted unique.");
        }
        expected.AddRange(mediaIds.Select(artifactId =>
            ("media-artifact-bound", (string?)artifactId)));
        expected.Add(("session-sealed", null));

        (string Type, string? Discriminator)[] actual = events.Select(item =>
            (item.EventType, CaptureEventDiscriminator(item))).ToArray();
        if (!actual.SequenceEqual(expected))
            throw new InvalidDataException($"Capture journal {sessionId} does not match its exact FSM.");
    }

    private static string? CaptureEventDiscriminator(CaptureJournalEventReadback item) =>
        item.EventType switch
        {
            "selector-started" or "landing-marked" or "hold-started" or "hold-ended" or
                "fall-started" or "respawn-marked" or "check-started" or "check-ended" =>
                RequireCaptureString(item.Payload, "markerId", item.EventType),
            "frame-captured" => RequireCaptureString(item.Payload, "captureRole", item.EventType),
            "watcher-marker-bound" => RequireCaptureString(item.Payload, "markerRole", item.EventType),
            "media-artifact-bound" => RequireCaptureString(item.Payload, "artifactId", item.EventType),
            _ => null
        };

    private static IEnumerable<(string Type, string? Discriminator)> ExpectedCaptureMarkerSequence(
        string sessionId)
    {
        if (sessionId == "candidate-primary-edge-0")
        {
            yield return ("hold-started", "stable-hold");
            yield return ("hold-ended", "stable-hold");
            yield return ("frame-captured", "candidate.lone-pad-visibility");
            foreach (string checkId in CandidateCheckIds[3..6])
            {
                yield return ("check-started", checkId);
                yield return ("check-ended", checkId);
            }
        }
        else if (sessionId.StartsWith("retail-", StringComparison.Ordinal))
        {
            IEnumerable<string> checkIds = sessionId switch
            {
                "retail-town-square" => TownSquareCheckIds,
                "retail-gnastys-loot" => GnastysLootCheckIds,
                "retail-sunny-flight" => SunnyFlightCheckIds,
                _ => throw new InvalidDataException("Unknown retail capture session.")
            };
            foreach (string checkId in checkIds)
            {
                yield return ("check-started", checkId);
                yield return ("check-ended", checkId);
            }
        }
        if (sessionId is "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2")
        {
            string checkId = sessionId switch
            {
                "candidate-primary-edge-0" => "candidate.primary-edge-4-to-3-same-pad-yaw",
                "candidate-edge-1" => "candidate.edge-1-same-pad-yaw",
                "candidate-edge-2" => "candidate.edge-2-same-pad-yaw",
                _ => throw new InvalidDataException("Unknown edge capture session.")
            };
            yield return ("frame-captured", checkId + ".pre-fall-frame");
            yield return ("fall-started", "edge-fall");
            yield return ("respawn-marked", "edge-respawn");
            yield return (
                "frame-captured",
                sessionId == "candidate-edge-2"
                    ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                    : checkId + ".post-respawn-frame");
        }
        else if (sessionId == "candidate-cold-reset")
        {
            yield return (
                "frame-captured",
                "candidate.cold-reset-same-pad-yaw.post-reset-frame");
        }
    }

    private static string[] ExpectedCaptureWatcherRoles(string sessionId)
    {
        List<string> result = ["landing-marked"];
        if (sessionId == "candidate-primary-edge-0")
            result.AddRange(["hold-started", "hold-ended"]);
        if (sessionId is "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2")
            result.AddRange(["fall-started", "respawn-marked"]);
        return result.ToArray();
    }

    private static CaptureRawArtifactReadback[] ExtractCaptureRawArtifacts(
        IReadOnlyList<CaptureJournalEventReadback> events,
        Id65V3RuntimeEvidenceSession session,
        string runId,
        int ordinal)
    {
        List<CaptureRawArtifactReadback> raw = [];
        foreach (CaptureJournalEventReadback item in events)
        {
            string? expectedKind = item.EventType switch
            {
                "profile-before-captured" => "runtime-profile-before",
                "profile-after-captured" => "runtime-profile-after",
                "watcher-stopped" => "watcher-stdout",
                "media-artifact-bound" => RequireCaptureString(
                    item.Payload,
                    "kind",
                    "capture media"),
                _ => null
            };
            if (expectedKind is null)
                continue;
            string artifactId = RequireCaptureString(item.Payload, "artifactId", "capture raw artifact");
            string kind = RequireCaptureString(item.Payload, "kind", "capture raw artifact");
            if (kind != expectedKind)
                throw new InvalidDataException("Capture raw artifact kind changed.");
            raw.Add(new(
                artifactId,
                kind,
                RequireCaptureString(item.Payload, "repoRelativePath", "capture raw artifact"),
                RequireCaptureSha256(item.Payload, "sha256", "capture raw artifact"),
                RequireCaptureInt64(item.Payload, "byteLength", "capture raw artifact"),
                RequireCaptureString(item.Payload, "capturedAtUtc", "capture raw artifact"),
                kind is "frame" or "screen-recording"
                    ? RequireCaptureString(item.Payload, "captureRole", "capture media")
                    : null,
                kind == "screen-recording"
                    ? RequireCaptureString(item.Payload, "mediaStartAtUtc", "capture recording")
                    : null,
                kind == "screen-recording"
                    ? RequireCaptureString(item.Payload, "mediaEndAtUtc", "capture recording")
                    : null));
        }
        if (raw.Select(item => item.ArtifactId).Distinct(StringComparer.Ordinal).Count() != raw.Count)
            throw new InvalidDataException("Capture journal repeats raw artifact metadata.");
        Dictionary<string, (string Kind, string Leaf, string? Role)> expected =
            BuildExpectedCaptureRawTable(session.SessionId);
        if (!raw.Select(item => item.ArtifactId).Order(StringComparer.Ordinal)
                .SequenceEqual(expected.Keys.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException(
                $"Capture journal {session.SessionId} raw artifact set changed.");
        }
        string prefix = RuntimeAssetPrefix + "capture-staging/" + runId + "/" +
            ordinal.ToString("D2", CultureInfo.InvariantCulture) + "-" + session.SessionId + "/";
        foreach (CaptureRawArtifactReadback item in raw)
        {
            (string kind, string leaf, string? role) = expected[item.ArtifactId];
            if (item.Kind != kind || item.RepoRelativePath != prefix + leaf ||
                (role is not null && item.CaptureRole != role))
            {
                throw new InvalidDataException(
                    $"Capture raw artifact {item.ArtifactId} differs from its fixed table.");
            }
        }
        CaptureJournalEventReadback terminal = events[^1];
        string[] sealedIds = RequireCaptureProperty(
                terminal.Payload,
                "rawArtifactIds",
                "capture terminal")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!sealedIds.SequenceEqual(raw.Select(item => item.ArtifactId).Order(StringComparer.Ordinal)))
            throw new InvalidDataException("Capture terminal raw artifact IDs differ from typed metadata.");
        return raw.ToArray();
    }

    private static Dictionary<string, (string Kind, string Leaf, string? Role)>
        BuildExpectedCaptureRawTable(string sessionId)
    {
        Dictionary<string, (string Kind, string Leaf, string? Role)> result =
            new(StringComparer.Ordinal)
            {
                [$"{sessionId}.profile-before"] =
                    ("runtime-profile-before", "runtime-profile-before.json", null),
                [$"{sessionId}.profile-after"] =
                    ("runtime-profile-after", "runtime-profile-after.json", null),
                [$"{sessionId}.watcher"] =
                    ("watcher-stdout", "watcher-stdout.log", null),
                [$"{sessionId}.screen-recording"] =
                    ("screen-recording", "session-screen-recording.mov",
                        $"{sessionId}.full-window-recording")
            };
        if (sessionId == "candidate-primary-edge-0")
        {
            result[$"{sessionId}.lone-pad"] =
                ("frame", "lone-pad.png", "candidate.lone-pad-visibility");
        }
        if (sessionId is "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2")
        {
            string checkId = sessionId switch
            {
                "candidate-primary-edge-0" => "candidate.primary-edge-4-to-3-same-pad-yaw",
                "candidate-edge-1" => "candidate.edge-1-same-pad-yaw",
                "candidate-edge-2" => "candidate.edge-2-same-pad-yaw",
                _ => throw new InvalidDataException("Unknown edge capture session.")
            };
            result[$"{sessionId}.edge-before"] =
                ("frame", "edge-before.png", checkId + ".pre-fall-frame");
            result[$"{sessionId}.edge-after"] =
                ("frame", "edge-after.png",
                    sessionId == "candidate-edge-2"
                        ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                        : checkId + ".post-respawn-frame");
        }
        if (sessionId == "candidate-cold-reset")
        {
            result[$"{sessionId}.cold-reset-after"] =
                ("frame", "cold-reset-after.png",
                    "candidate.cold-reset-same-pad-yaw.post-reset-frame");
        }
        return result;
    }

    private static void ValidateCaptureJournalRuntimeBindings(
        IReadOnlyList<CaptureJournalEventReadback> events,
        Id65V3RuntimeEvidenceSession session,
        string runId,
        string bootIdentity,
        IReadOnlyList<CaptureRawArtifactReadback> rawArtifacts,
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyDictionary<string, byte[]> artifactBytes,
        Id65V3RuntimeEvidenceReadbackContext readbackContext)
    {
        CaptureJournalEventReadback sessionOpen = RequireOneCaptureEvent(events, "session-open");
        CaptureJournalEventReadback beforeEvent = RequireOneCaptureEvent(events, "profile-before-captured");
        CaptureJournalEventReadback processEvent = RequireOneCaptureEvent(events, "process-start-observed");
        CaptureJournalEventReadback discEvent = RequireOneCaptureEvent(events, "disc-open-observed");
        CaptureJournalEventReadback watcherConnected = RequireOneCaptureEvent(events, "watcher-connected");
        CaptureJournalEventReadback resumed = RequireOneCaptureEvent(events, "manually-resumed");
        CaptureJournalEventReadback recordingStarted = RequireOneCaptureEvent(events, "recording-started");
        CaptureJournalEventReadback selector = RequireOneCaptureEvent(events, "selector-started");
        CaptureJournalEventReadback landingEvent = RequireOneCaptureEvent(events, "landing-marked");
        CaptureJournalEventReadback recordingStopped = RequireOneCaptureEvent(events, "recording-stopped");
        CaptureJournalEventReadback watcherStopped = RequireOneCaptureEvent(events, "watcher-stopped");
        CaptureJournalEventReadback processExit = RequireOneCaptureEvent(events, "process-exit-observed");
        CaptureJournalEventReadback afterEvent = RequireOneCaptureEvent(events, "profile-after-captured");
        CaptureJournalEventReadback terminal = RequireOneCaptureEvent(events, "session-sealed");
        JsonElement process = processEvent.Payload;
        JsonElement disc = discEvent.Payload;

        string processStartedAtUtc = RequireCaptureString(
            process,
            "processStartedAtUtc",
            "capture process");
        string processExitedAtUtc = RequireCaptureString(
            processExit.Payload,
            "processExitedAtUtc",
            "capture process exit");
        if (processEvent.AtUtc != RequireCaptureString(process, "observedAtUtc", "capture process") ||
            processExit.AtUtc != processExitedAtUtc ||
            session.ProcessStartedAtUtc != processStartedAtUtc ||
            session.WatcherConnectedAtUtc != watcherConnected.AtUtc ||
            session.ManuallyResumedAtUtc != resumed.AtUtc ||
            session.SelectorStartedAtUtc != selector.AtUtc ||
            session.ProcessExitedAtUtc != processExitedAtUtc)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} timestamps differ from its capture journal.");
        }
        DateTimeOffset processStart = ParseCanonicalUtc(processStartedAtUtc, "capture process start");
        DateTimeOffset processEnd = ParseCanonicalUtc(processExitedAtUtc, "capture process exit");
        if (!(processStart < ParseCanonicalUtc(sessionOpen.AtUtc, "capture session open") &&
            ParseCanonicalUtc(terminal.AtUtc, "capture terminal") >= processEnd))
        {
            throw new InvalidDataException("Capture journal does not bracket one cold process.");
        }

        long pid = RequireCaptureInt64(process, "pid", "capture process");
        long startSeconds = RequireCaptureInt64(process, "startSeconds", "capture process");
        long startMicroseconds = RequireCaptureInt64(process, "startMicroseconds", "capture process");
        if (processStartedAtUtc != CaptureProcessStartUtc(startSeconds, startMicroseconds))
        {
            throw new InvalidDataException(
                "Capture process start UTC is not derived from its kernel epoch fields.");
        }
        string executablePath = Path.GetFullPath(readbackContext.DuckStationExecutablePath);
        if (RequireCaptureString(process, "executableRealPath", "capture process") != executablePath ||
            RequireCaptureSha256(process, "executableSha256", "capture process") !=
                package.RuntimeProfile.DuckStationExecutableSha256)
        {
            throw new InvalidDataException("Capture process differs from the pinned DuckStation executable.");
        }
        FileIdentity executableIdentity = ReadFileIdentity(
            executablePath,
            "capture process executable");
        if (RequireCaptureInt64(process, "executableByteLength", "capture process") !=
                executableIdentity.ByteLength ||
            RequireCaptureInt64(process, "executableDevice", "capture process") !=
                executableIdentity.Device ||
            RequireCaptureInt64(process, "executableInode", "capture process") !=
                checked((long)executableIdentity.Inode) ||
            executableIdentity.Sha256 != package.RuntimeProfile.DuckStationExecutableSha256)
        {
            throw new InvalidDataException(
                "Capture process executable length, vnode identity, or bytes changed.");
        }
        string cuePath = Path.GetFullPath(Path.Combine(
            readbackContext.SourceRepositoryRootPath,
            package.SourceIdentity.CandidateCueRelativePath));
        string binPath = Path.GetFullPath(Path.Combine(
            readbackContext.SourceRepositoryRootPath,
            package.SourceIdentity.CandidateBinRelativePath));
        string[] argv = RequireCaptureProperty(process, "argv", "capture process")
            .EnumerateArray().Select(item => item.GetString()!).ToArray();
        if (!argv.SequenceEqual([executablePath, cuePath]))
        {
            throw new InvalidDataException("Capture process argv is not exactly executable plus pinned CUE.");
        }
        byte[] argvBytes = StrictUtf8.GetBytes(string.Join('\0', argv) + '\0');
        if (Hash(argvBytes) != RequireCaptureSha256(process, "argvNulSha256", "capture process"))
            throw new InvalidDataException("Capture process argv byte hash changed.");
        string externalHomePath = Path.GetFullPath(readbackContext.ExternalHomeRootPath);
        string externalHomeObserved = RequireCaptureString(
            process,
            "externalHomeRealPath",
            "capture process");
        string environmentNulSha256 = RequireCaptureSha256(
            process,
            "environmentNulSha256",
            "capture process");
        if (externalHomeObserved != externalHomePath ||
            environmentNulSha256 != RecomputeCaptureEnvironmentNulSha256(externalHomePath))
        {
            throw new InvalidDataException(
                "Capture process external HOME or exact environment byte hash changed.");
        }
        string expectedProcessId = RecomputeCaptureProcessInstanceId(
            runId,
            bootIdentity,
            pid,
            startSeconds,
            startMicroseconds,
            RequireCaptureInt64(process, "executableDevice", "capture process"),
            RequireCaptureInt64(process, "executableInode", "capture process"),
            RequireCaptureSha256(process, "executableSha256", "capture process"),
            RequireCaptureSha256(process, "argvNulSha256", "capture process"),
            environmentNulSha256,
            externalHomeObserved);
        if (expectedProcessId != session.ProcessInstanceId)
            throw new InvalidDataException("Capture process instance ID is not derived from its OS identity.");

        FileIdentity cueIdentity = ReadFileIdentity(cuePath, "capture disc CUE");
        FileIdentity binIdentity = ReadFileIdentity(binPath, "capture disc BIN");
        if (RequireCaptureString(disc, "observedAtUtc", "capture disc") != discEvent.AtUtc ||
            RequireCaptureInt64(disc, "pid", "capture disc") != pid ||
            RequireCaptureInt64(disc, "gdbListenerPid", "capture disc") != pid ||
            RequireCaptureInt64(disc, "startSeconds", "capture disc") != startSeconds ||
            RequireCaptureInt64(disc, "startMicroseconds", "capture disc") != startMicroseconds ||
            RequireCaptureString(disc, "cueRealPath", "capture disc") != cuePath ||
            RequireCaptureString(disc, "binRealPath", "capture disc") != binPath ||
            RequireCaptureInt64(disc, "cueByteLength", "capture disc") != cueIdentity.ByteLength ||
            RequireCaptureInt64(disc, "binByteLength", "capture disc") != binIdentity.ByteLength ||
            RequireCaptureInt64(disc, "cueDevice", "capture disc") != cueIdentity.Device ||
            RequireCaptureInt64(disc, "cueInode", "capture disc") !=
                checked((long)cueIdentity.Inode) ||
            RequireCaptureInt64(disc, "binDevice", "capture disc") != binIdentity.Device ||
            RequireCaptureInt64(disc, "binInode", "capture disc") !=
                checked((long)binIdentity.Inode) ||
            RequireCaptureString(disc, "bindingMode", "capture disc") !=
                "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1" ||
            RequireCaptureInt64(disc, "cueSourceLinkCount", "capture disc") !=
                cueIdentity.LinkCount ||
            !RequireCaptureBoolean(disc, "binOpen", "capture disc") ||
            RequireCaptureInt64(disc, "binLinkCount", "capture disc") !=
                binIdentity.LinkCount ||
            cueIdentity.Sha256 != package.SourceIdentity.CandidateCueSha256 ||
            binIdentity.Sha256 != package.SourceIdentity.CandidateBinSha256 ||
            !Regex.IsMatch(
                RequireCaptureString(disc, "binFd", "capture disc"),
                "^[0-9]+r$",
                RegexOptions.CultureInvariant))
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} disc FD, listener, inode projection, or pinned bytes changed.");
        }
        foreach (string identityName in new[] { "cueDevice", "cueInode", "binDevice", "binInode" })
        {
            if (RequireCaptureInt64(disc, identityName, "capture disc") <= 0)
                throw new InvalidDataException("Capture disc device/inode identity is not positive.");
        }

        if (RequireCaptureInt64(processExit.Payload, "pid", "capture exit") != pid ||
            RequireCaptureInt64(processExit.Payload, "startSeconds", "capture exit") != startSeconds ||
            RequireCaptureInt64(processExit.Payload, "startMicroseconds", "capture exit") != startMicroseconds)
        {
            throw new InvalidDataException("Capture process exit is not bound to its start identity.");
        }
        string beforeCapturedAt = RequireCaptureString(
            beforeEvent.Payload,
            "capturedAtUtc",
            "profile-before capture");
        string afterCapturedAt = RequireCaptureString(
            afterEvent.Payload,
            "capturedAtUtc",
            "profile-after capture");
        if (ParseCanonicalUtc(beforeCapturedAt, "profile-before capture") >= processStart ||
            ParseCanonicalUtc(afterCapturedAt, "profile-after capture") < processEnd ||
            afterCapturedAt != afterEvent.AtUtc || beforeEvent.Sequence <= sessionOpen.Sequence)
        {
            throw new InvalidDataException("Capture profile clocks do not bookend the process.");
        }

        ValidateCaptureRecordingLifecycle(
            events,
            process,
            recordingStarted,
            recordingStopped,
            rawArtifacts);
        ValidateCaptureMarkerDurations(events, session.SessionId);
        ValidateCaptureWatcherBindings(
            events,
            session,
            recordingStarted,
            rawArtifacts,
            artifactBytes);
        ValidateCaptureSessionArtifactIds(session, rawArtifacts);
    }

    private static string RecomputeCaptureProcessInstanceId(
        string runId,
        string bootIdentity,
        long pid,
        long startSeconds,
        long startMicroseconds,
        long executableDevice,
        long executableInode,
        string executableSha256,
        string argvNulSha256,
        string environmentNulSha256,
        string externalHomeRealPath)
    {
        string manifest =
            "id65-v3-runtime-process-instance-v2\n" +
            $"runId={runId}\n" +
            $"bootIdentity={bootIdentity}\n" +
            $"pid={pid}\n" +
            $"startSeconds={startSeconds}\n" +
            $"startMicroseconds={startMicroseconds}\n" +
            $"executableDevice={executableDevice}\n" +
            $"executableInode={executableInode}\n" +
            $"executableSha256={executableSha256}\n" +
            $"argvNulSha256={argvNulSha256}\n" +
            $"environmentNulSha256={environmentNulSha256}\n" +
            $"externalHomeRealPath={externalHomeRealPath}\n";
        return "id65v3-process-" + HashUtf8(manifest)[..32];
    }

    private static string RecomputeCaptureEnvironmentNulSha256(string externalHomeRealPath)
    {
        string manifest =
            $"HOME={externalHomeRealPath}\0" +
            "LANG=C\0" +
            "LC_ALL=C\0" +
            "PATH=/usr/bin:/bin:/usr/sbin:/sbin\0" +
            "SPYRO_ID65_CAPTURE_ENVIRONMENT_END_V1=duckstation-id65-unattended-v1\0";
        return Hash(StrictUtf8.GetBytes(manifest));
    }

    private static string CaptureProcessStartUtc(long startSeconds, long startMicroseconds)
    {
        if (startSeconds <= 0 || startMicroseconds is < 0 or >= 1_000_000)
            throw new InvalidDataException("Capture process kernel start epoch is invalid.");
        try
        {
            DateTimeOffset value = DateTimeOffset.FromUnixTimeSeconds(startSeconds)
                .AddTicks((startMicroseconds / 1_000) * TimeSpan.TicksPerMillisecond);
            return FormatUtc(value);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException(
                "Capture process kernel start epoch is outside canonical UTC.",
                exception);
        }
    }

    private static string CaptureRawProcessFingerprint(JsonElement process)
    {
        string[] fields =
        [
            "argv", "argvNulSha256", "environmentNulSha256", "executableByteLength",
            "executableDevice", "executableInode", "executableRealPath", "executableSha256",
            "externalHomeRealPath", "parentPid", "pid", "processStartedAtUtc",
            "startMicroseconds", "startSeconds"
        ];
        StringBuilder manifest = new("id65-v3-runtime-raw-os-process-fingerprint-v2\n{");
        bool first = true;
        foreach (string field in fields.Order(StringComparer.Ordinal))
        {
            if (!first)
                manifest.Append(',');
            first = false;
            manifest.Append(JsonSerializer.Serialize(field, JsonOptions)).Append(':')
                .Append(CanonicalCaptureProperty(process, field));
        }
        manifest.Append('}');
        return HashUtf8(manifest.ToString());
    }

    private static void ValidateUniqueRawProcessFingerprintHashes(
        IEnumerable<string> fingerprints)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (string fingerprint in fingerprints)
        {
            if (!Sha256Regex().IsMatch(fingerprint) || !seen.Add(fingerprint))
            {
                throw new InvalidDataException(
                    "Capture journals reuse one raw OS process fingerprint across sessions.");
            }
        }
    }

    private static CaptureJournalEventReadback RequireOneCaptureEvent(
        IReadOnlyList<CaptureJournalEventReadback> events,
        string eventType)
    {
        CaptureJournalEventReadback[] matches = events.Where(item => item.EventType == eventType).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException($"Capture journal does not contain one exact {eventType} event.");
    }

    private static void ValidateCaptureRecordingLifecycle(
        IReadOnlyList<CaptureJournalEventReadback> events,
        JsonElement process,
        CaptureJournalEventReadback recordingStarted,
        CaptureJournalEventReadback recordingStopped,
        IReadOnlyList<CaptureRawArtifactReadback> rawArtifacts)
    {
        JsonElement start = recordingStarted.Payload;
        JsonElement stop = recordingStopped.Payload;
        string recordingId = RequireCaptureString(start, "artifactId", "recording start");
        string startAtUtc = RequireCaptureString(start, "mediaStartAtUtc", "recording start");
        string endAtUtc = RequireCaptureString(stop, "mediaEndAtUtc", "recording stop");
        long startMono = RequireCaptureInt64(start, "mediaStartMonotonicMicroseconds", "recording start");
        long endMono = RequireCaptureInt64(stop, "mediaEndMonotonicMicroseconds", "recording stop");
        long recorderPid = RequireCaptureInt64(start, "ownedRecorderPid", "recording start");
        if (recordingId != RequireCaptureString(stop, "artifactId", "recording stop") ||
            recorderPid != RequireCaptureInt64(stop, "ownedRecorderPid", "recording stop") ||
            startAtUtc != recordingStarted.AtUtc || startMono != recordingStarted.MonotonicMicroseconds ||
            ParseCanonicalUtc(endAtUtc, "recording media end") <=
                ParseCanonicalUtc(startAtUtc, "recording media start") ||
            ParseCanonicalUtc(endAtUtc, "recording media end") >
                ParseCanonicalUtc(recordingStopped.AtUtc, "recording stop") ||
            endMono <= startMono || endMono > recordingStopped.MonotonicMicroseconds ||
            !CaptureRecordingDurationWithinBound(startMono, endMono))
        {
            throw new InvalidDataException("Capture recording lifecycle is reordered or out of bounds.");
        }
        long processPid = RequireCaptureInt64(process, "pid", "capture process");
        long processStartSeconds = RequireCaptureInt64(process, "startSeconds", "capture process");
        long processStartMicroseconds = RequireCaptureInt64(
            process,
            "startMicroseconds",
            "capture process");
        if (RequireCaptureInt64(start, "ownerPid", "recording start") != processPid ||
            RequireCaptureInt64(start, "windowOwnerPid", "recording start") != processPid ||
            RequireCaptureInt64(start, "ownerStartSeconds", "recording start") != processStartSeconds ||
            RequireCaptureInt64(start, "ownerStartMicroseconds", "recording start") !=
                processStartMicroseconds)
        {
            throw new InvalidDataException("Capture recording is not bound to its process window.");
        }
        CaptureJournalEventReadback[] mediaEvents = events.Where(item =>
            item.EventType == "media-artifact-bound").ToArray();
        CaptureJournalEventReadback recordingMedia = mediaEvents.SingleOrDefault(item =>
                RequireCaptureString(item.Payload, "kind", "capture media") == "screen-recording") ??
            throw new InvalidDataException("Capture journal lacks one full-session recording metadata event.");
        if (mediaEvents.Count(item =>
                RequireCaptureString(item.Payload, "kind", "capture media") == "screen-recording") != 1)
        {
            throw new InvalidDataException("Capture journal has more than one session recording.");
        }
        JsonElement recording = recordingMedia.Payload;
        foreach (string name in CaptureRecordingStartPayloadKeys)
        {
            if (CanonicalCaptureProperty(recording, name) != CanonicalCaptureProperty(start, name))
                throw new InvalidDataException("Capture recording metadata differs from its start event.");
        }
        foreach (string name in new[]
        {
            "artifactId", "mediaEndAtUtc", "mediaEndMonotonicMicroseconds", "ownedRecorderPid"
        })
        {
            if (CanonicalCaptureProperty(recording, name) != CanonicalCaptureProperty(stop, name))
                throw new InvalidDataException("Capture recording metadata differs from its stop event.");
        }
        CaptureRawArtifactReadback rawRecording = rawArtifacts.Single(item =>
            item.Kind == "screen-recording");
        if (rawRecording.ArtifactId != recordingId || rawRecording.CapturedAtUtc != recordingStopped.AtUtc ||
            rawRecording.MediaStartAtUtc != startAtUtc || rawRecording.MediaEndAtUtc != endAtUtc)
        {
            throw new InvalidDataException("Capture recording raw metadata changed.");
        }

        Dictionary<string, CaptureJournalEventReadback> frameEvents = events
            .Where(item => item.EventType == "frame-captured")
            .ToDictionary(
                item => RequireCaptureString(item.Payload, "artifactId", "capture frame"),
                StringComparer.Ordinal);
        Dictionary<string, CaptureJournalEventReadback> frameMedia = mediaEvents
            .Where(item => RequireCaptureString(item.Payload, "kind", "capture media") == "frame")
            .ToDictionary(
                item => RequireCaptureString(item.Payload, "artifactId", "capture frame media"),
                StringComparer.Ordinal);
        if (!frameEvents.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                frameMedia.Keys.Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Capture frame events and media metadata differ.");
        }
        foreach ((string frameId, CaptureJournalEventReadback frameEvent) in frameEvents)
        {
            JsonElement media = frameMedia[frameId].Payload;
            string role = RequireCaptureString(frameEvent.Payload, "captureRole", "capture frame");
            if (RequireCaptureString(media, "captureRole", "capture frame media") != role ||
                RequireCaptureString(media, "capturedAtUtc", "capture frame media") != frameEvent.AtUtc ||
                RequireCaptureInt64(media, "ownerPid", "capture frame media") != processPid ||
                RequireCaptureInt64(media, "windowOwnerPid", "capture frame media") != processPid ||
                RequireCaptureInt64(media, "ownerStartSeconds", "capture frame media") != processStartSeconds ||
                RequireCaptureInt64(media, "ownerStartMicroseconds", "capture frame media") !=
                    processStartMicroseconds ||
                RequireCaptureInt64(media, "windowId", "capture frame media") !=
                    RequireCaptureInt64(start, "windowId", "recording start") ||
                ParseCanonicalUtc(frameEvent.AtUtc, "capture frame") <
                    ParseCanonicalUtc(startAtUtc, "recording start") ||
                ParseCanonicalUtc(frameEvent.AtUtc, "capture frame") >
                    ParseCanonicalUtc(endAtUtc, "recording end"))
            {
                throw new InvalidDataException("Capture frame is not bound to its exact process window.");
            }
            foreach (string name in new[]
            {
                "backend", "boundsHeight", "boundsWidth", "boundsX", "boundsY"
            })
            {
                if (CanonicalCaptureProperty(media, name) != CanonicalCaptureProperty(start, name))
                    throw new InvalidDataException("Capture frame window geometry changed.");
            }
        }
    }

    internal static bool CaptureRecordingDurationWithinBound(
        long startMonotonicMicroseconds,
        long endMonotonicMicroseconds) =>
        startMonotonicMicroseconds >= 0 &&
        endMonotonicMicroseconds > startMonotonicMicroseconds &&
        endMonotonicMicroseconds - startMonotonicMicroseconds <=
            MaximumCaptureRecordingDurationMicroseconds;

    private static string CanonicalCaptureProperty(JsonElement value, string name)
    {
        StringBuilder builder = new();
        WriteJcsElement(builder, RequireCaptureProperty(value, name, "capture payload"));
        return builder.ToString();
    }

    private static void ValidateCaptureMarkerDurations(
        IReadOnlyList<CaptureJournalEventReadback> events,
        string sessionId)
    {
        CaptureJournalEventReadback selector = RequireCaptureMarker(
            events,
            "selector-started",
            "selector");
        CaptureJournalEventReadback landing = RequireCaptureMarker(
            events,
            "landing-marked",
            "landing");
        if (landing.MonotonicMicroseconds <= selector.MonotonicMicroseconds)
            throw new InvalidDataException("Capture landing does not follow selector entry.");
        if (sessionId == "candidate-primary-edge-0")
        {
            CaptureJournalEventReadback completedStart = RequireCaptureMarker(
                events,
                "check-started",
                "candidate.completed-landing-hud4");
            CaptureJournalEventReadback completedEnd = RequireCaptureMarker(
                events,
                "check-ended",
                "candidate.completed-landing-hud4");
            if (completedStart.MonotonicMicroseconds >= landing.MonotonicMicroseconds ||
                completedEnd.MonotonicMicroseconds <= landing.MonotonicMicroseconds ||
                completedEnd.MonotonicMicroseconds - completedStart.MonotonicMicroseconds < 3_500_000)
            {
                throw new InvalidDataException(
                    "Capture completed-landing interval is too short or reordered.");
            }
            CaptureJournalEventReadback holdStart = RequireCaptureMarker(
                events,
                "hold-started",
                "stable-hold");
            CaptureJournalEventReadback holdEnd = RequireCaptureMarker(
                events,
                "hold-ended",
                "stable-hold");
            if (holdEnd.MonotonicMicroseconds - holdStart.MonotonicMicroseconds < 20_500_000)
                throw new InvalidDataException("Capture hands-off hold is shorter than 20.5 seconds.");
        }
        if (sessionId == "candidate-cold-reset" &&
            landing.MonotonicMicroseconds - selector.MonotonicMicroseconds < 5_500_000)
        {
            throw new InvalidDataException(
                "Capture cold-reset selector-to-landing interval is shorter than 5.5 seconds.");
        }

        IEnumerable<string> timedChecks = sessionId switch
        {
            "candidate-primary-edge-0" => CandidateCheckIds[3..6],
            "retail-town-square" => TownSquareCheckIds,
            "retail-gnastys-loot" => GnastysLootCheckIds,
            "retail-sunny-flight" => SunnyFlightCheckIds,
            _ => []
        };
        foreach (string checkId in timedChecks)
        {
            CaptureJournalEventReadback start = RequireCaptureMarker(events, "check-started", checkId);
            CaptureJournalEventReadback end = RequireCaptureMarker(events, "check-ended", checkId);
            long expected = checked(MinimumRecordingSliceMilliseconds[checkId] * 1_000 + 500_000);
            if (end.MonotonicMicroseconds - start.MonotonicMicroseconds < expected)
                throw new InvalidDataException($"Capture check interval is too short: {checkId}.");
        }
        if (sessionId is "candidate-primary-edge-0" or "candidate-edge-1" or "candidate-edge-2")
        {
            CaptureJournalEventReadback fall = RequireCaptureMarker(events, "fall-started", "edge-fall");
            CaptureJournalEventReadback respawn = RequireCaptureMarker(
                events,
                "respawn-marked",
                "edge-respawn");
            if (respawn.MonotonicMicroseconds - fall.MonotonicMicroseconds < 5_500_000)
                throw new InvalidDataException("Capture death-respawn interval is shorter than 5.5 seconds.");
        }
    }

    private static CaptureJournalEventReadback RequireCaptureMarker(
        IReadOnlyList<CaptureJournalEventReadback> events,
        string eventType,
        string markerId)
    {
        CaptureJournalEventReadback[] matches = events.Where(item =>
            item.EventType == eventType &&
            RequireCaptureString(item.Payload, "markerId", eventType) == markerId).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Capture journal lacks one exact {eventType}:{markerId} marker.");
    }

    private static void ValidateCaptureWatcherBindings(
        IReadOnlyList<CaptureJournalEventReadback> events,
        Id65V3RuntimeEvidenceSession session,
        CaptureJournalEventReadback recordingStarted,
        IReadOnlyList<CaptureRawArtifactReadback> rawArtifacts,
        IReadOnlyDictionary<string, byte[]> artifactBytes)
    {
        CaptureRawArtifactReadback watcher = rawArtifacts.Single(item => item.Kind == "watcher-stdout");
        if (session.WatcherArtifactId != watcher.ArtifactId ||
            !artifactBytes.TryGetValue(watcher.ArtifactId, out byte[]? watcherBytes))
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} watcher reference differs from its journal.");
        }
        Id65V3TelemetryParseResult parsed = ParseTelemetryLog(
            DecodeStrictUtf8(watcherBytes, $"watcher artifact {watcher.ArtifactId}"));
        if (parsed.Samples.Length == 0)
            throw new InvalidDataException("Capture watcher log has no sample.");
        CaptureJournalEventReadback resumed = RequireOneCaptureEvent(events, "manually-resumed");
        CaptureJournalEventReadback watcherStopped = RequireOneCaptureEvent(events, "watcher-stopped");
        if (watcher.CapturedAtUtc != watcherStopped.AtUtc ||
            ParseCanonicalUtc(parsed.Samples[0].AtUtc, "first watcher sample") <
                ParseCanonicalUtc(resumed.AtUtc, "manual resume") ||
            ParseCanonicalUtc(parsed.Samples[^1].AtUtc, "last watcher sample") >
                ParseCanonicalUtc(watcherStopped.AtUtc, "watcher stop"))
        {
            throw new InvalidDataException("Capture watcher samples escape their resumed process window.");
        }
        long connectedPid = RequireCaptureInt64(
            RequireOneCaptureEvent(events, "watcher-connected").Payload,
            "ownedWatcherPid",
            "watcher connection");
        if (connectedPid != RequireCaptureInt64(
                watcherStopped.Payload,
                "ownedWatcherPid",
                "watcher stop"))
        {
            throw new InvalidDataException("Capture watcher child PID changed across its lifecycle.");
        }

        Dictionary<string, CaptureJournalEventReadback> bindings = events
            .Where(item => item.EventType == "watcher-marker-bound")
            .ToDictionary(
                item => RequireCaptureString(item.Payload, "markerRole", "watcher binding"),
                StringComparer.Ordinal);
        if (!bindings.Keys.Order(StringComparer.Ordinal).SequenceEqual(
                ExpectedCaptureWatcherRoles(session.SessionId).Order(StringComparer.Ordinal)))
        {
            throw new InvalidDataException("Capture watcher marker-role set changed.");
        }
        foreach ((string role, CaptureJournalEventReadback binding) in bindings)
        {
            JsonElement payload = binding.Payload;
            int operatorSequence = checked((int)RequireCaptureInt64(
                payload,
                "operatorEventSequence",
                "watcher binding"));
            int sampleOrdinal = checked((int)RequireCaptureInt64(
                payload,
                "sampleOrdinal",
                "watcher binding"));
            if (operatorSequence < 0 || operatorSequence >= events.Count ||
                sampleOrdinal < 0 || sampleOrdinal >= parsed.Samples.Length)
            {
                throw new InvalidDataException("Capture watcher binding index is out of range.");
            }
            CaptureJournalEventReadback operatorEvent = events[operatorSequence];
            Id65V3TelemetryTuple sample = parsed.Samples[sampleOrdinal];
            string sampleAtUtc = RequireCaptureString(payload, "sampleAtUtc", "watcher binding");
            long sampleElapsed = RequireCaptureInt64(
                payload,
                "sampleElapsedMilliseconds",
                "watcher binding");
            DateTimeOffset sampleAt = ParseCanonicalUtc(sampleAtUtc, "watcher binding sample");
            DateTimeOffset operatorAt = ParseCanonicalUtc(operatorEvent.AtUtc, "watcher operator marker");
            double operatorDeltaMilliseconds = (sampleAt - operatorAt).TotalMilliseconds;
            bool withinMarkerTolerance = role == "fall-started"
                ? operatorDeltaMilliseconds is >= 0 and <= MaximumFallAuthorizationBindingMilliseconds
                : Math.Abs(operatorDeltaMilliseconds) <= 250;
            if (operatorEvent.EventType != role || sample.SampleOrdinal != sampleOrdinal ||
                sample.AtUtc != sampleAtUtc || sample.ElapsedMilliseconds != sampleElapsed ||
                !withinMarkerTolerance ||
                (role == "hold-ended" && sampleAt > operatorAt) ||
                (role != "hold-ended" && sampleAt < operatorAt))
            {
                throw new InvalidDataException("Capture watcher binding changed its exact raw sample.");
            }
            if (role is "landing-marked" or "hold-started" or "hold-ended")
            {
                if (sample is not { Lives: 4, DeathState: 0, PlayerZ: >= 1024 })
                    throw new InvalidDataException("Capture stable marker has an unsafe watcher tuple.");
            }
            else if (role == "fall-started")
            {
                if (!FallBindingIsAccepted(
                        parsed,
                        operatorEvent.AtUtc,
                        sampleOrdinal))
                {
                    throw new InvalidDataException(
                        "Capture fall marker lacks its safe authorization-to-death watcher edge.");
                }
            }
            else if (role == "respawn-marked")
            {
                if (session.FallStartSampleOrdinal is not int boundFallOrdinal ||
                    !RespawnBindingIsAccepted(
                        parsed,
                        boundFallOrdinal,
                        operatorEvent.AtUtc,
                        sampleOrdinal))
                {
                    throw new InvalidDataException(
                        "Capture respawn binding is not the first stable raw sample after its marker and fall.");
                }
            }
        }

        (string AtUtc, int Ordinal) landing = CaptureBoundSample(bindings, "landing-marked");
        if (session.SessionId == "candidate-primary-edge-0")
        {
            CaptureJournalEventReadback completedStart = RequireCaptureMarker(
                events,
                "check-started",
                "candidate.completed-landing-hud4");
            CaptureJournalEventReadback selector = RequireCaptureMarker(
                events,
                "selector-started",
                "selector");
            if (!AssistedPrimaryLandingEvidenceIsAccepted(
                    parsed,
                    recordingStarted.AtUtc,
                    selector.AtUtc,
                    selector.MonotonicMicroseconds,
                    completedStart.AtUtc,
                    completedStart.MonotonicMicroseconds,
                    landing.Ordinal))
            {
                throw new InvalidDataException(
                    "Primary landing evidence lacks the exact automated " +
                    "state-0/state-3/state-1/state-0 transition and safe suffix.");
            }
        }
        (string AtUtc, int Ordinal)? holdStart = bindings.ContainsKey("hold-started")
            ? CaptureBoundSample(bindings, "hold-started")
            : null;
        (string AtUtc, int Ordinal)? holdEnd = bindings.ContainsKey("hold-ended")
            ? CaptureBoundSample(bindings, "hold-ended")
            : null;
        (string AtUtc, int Ordinal)? fall = bindings.ContainsKey("fall-started")
            ? CaptureBoundSample(bindings, "fall-started")
            : null;
        (string AtUtc, int Ordinal)? respawn = bindings.ContainsKey("respawn-marked")
            ? CaptureBoundSample(bindings, "respawn-marked")
            : null;
        if (session.LandingAtUtc != landing.AtUtc || session.LandingSampleOrdinal != landing.Ordinal ||
            session.HoldStartedAtUtc != holdStart?.AtUtc ||
            session.HoldStartSampleOrdinal != holdStart?.Ordinal ||
            session.HoldEndedAtUtc != holdEnd?.AtUtc ||
            session.HoldEndSampleOrdinal != holdEnd?.Ordinal ||
            session.FallStartedAtUtc != fall?.AtUtc || session.FallStartSampleOrdinal != fall?.Ordinal ||
            session.RespawnAtUtc != respawn?.AtUtc ||
            session.RespawnSampleOrdinal != respawn?.Ordinal)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} marker timestamps/ordinals differ from journal-bound watcher samples.");
        }
    }

    internal static bool AssistedPrimaryLandingEvidenceIsAccepted(
        Id65V3TelemetryParseResult parsed,
        string recordingStartedAtUtc,
        string selectorAtUtc,
        long selectorMonotonicMicroseconds,
        string completedStartAtUtc,
        long completedStartMonotonicMicroseconds,
        int landingBoundOrdinal)
    {
        if (landingBoundOrdinal <= 0 || landingBoundOrdinal >= parsed.Samples.Length)
            return false;
        DateTimeOffset recordingStartedAt = ParseCanonicalUtc(
            recordingStartedAtUtc,
            "assisted recording start");
        DateTimeOffset selectorAt = ParseCanonicalUtc(selectorAtUtc, "assisted selector");
        if (recordingStartedAt >= selectorAt)
            return false;
        int lastAtOrBeforeRecordingOrdinal = Array.FindLastIndex(
            parsed.Samples,
            landingBoundOrdinal,
            landingBoundOrdinal + 1,
            item => ParseCanonicalUtc(
                item.AtUtc,
                "assisted recording-start sample") <= recordingStartedAt);
        int baselineOrdinal = Array.FindLastIndex(
            parsed.Samples,
            landingBoundOrdinal,
            landingBoundOrdinal + 1,
            item => ParseCanonicalUtc(item.AtUtc, "assisted selector sample") <= selectorAt);
        if (baselineOrdinal < 0 ||
            parsed.Samples[baselineOrdinal] is not
                { Lives: 4, DeathState: 0, PlayerZ: >= 1024 })
        {
            return false;
        }
        double baselineLag =
            (selectorAt - ParseCanonicalUtc(
                parsed.Samples[baselineOrdinal].AtUtc,
                "assisted selector baseline")).TotalMilliseconds;
        if (baselineLag is < 0 or > 250)
            return false;
        long safeStartElapsed = parsed.Samples[baselineOrdinal].ElapsedMilliseconds;
        int safeStartOrdinal = baselineOrdinal;
        while (safeStartOrdinal > 0 &&
            parsed.Samples[baselineOrdinal].ElapsedMilliseconds -
                parsed.Samples[safeStartOrdinal].ElapsedMilliseconds <
                    AssistedReadinessMinimumMilliseconds)
        {
            safeStartOrdinal--;
            safeStartElapsed = parsed.Samples[safeStartOrdinal].ElapsedMilliseconds;
        }
        if (baselineOrdinal - safeStartOrdinal + 1 < AssistedReadinessMinimumSamples ||
            safeStartOrdinal <= lastAtOrBeforeRecordingOrdinal ||
            parsed.Samples[baselineOrdinal].ElapsedMilliseconds - safeStartElapsed <
                AssistedReadinessMinimumMilliseconds ||
            !parsed.Samples[safeStartOrdinal..(baselineOrdinal + 1)].All(item =>
                item is { Lives: 4, DeathState: 0, PlayerZ: >= 1024 }))
        {
            return false;
        }
        int departureOrdinal = Array.FindIndex(
            parsed.Samples,
            baselineOrdinal + 1,
            landingBoundOrdinal - baselineOrdinal,
            item => item is not { Lives: 4, DeathState: 0, PlayerZ: >= 1024 });
        if (departureOrdinal <= baselineOrdinal ||
            parsed.Samples[departureOrdinal] is not
                { Lives: 4, DeathState: 3, PlayerZ: >= 1024 } ||
            parsed.Samples[departureOrdinal].AtUtc != completedStartAtUtc)
        {
            return false;
        }
        long departureDeltaMicroseconds = checked(
            (ParseCanonicalUtc(completedStartAtUtc, "assisted departure") - selectorAt)
                .Ticks / 10L);
        if (departureDeltaMicroseconds <= 0 ||
            departureDeltaMicroseconds >
                AssistedPrimaryDepartureMaximumMilliseconds * 1_000L)
            return false;
        long derivedStartMonotonic = checked(
            selectorMonotonicMicroseconds + departureDeltaMicroseconds);
        if (derivedStartMonotonic != completedStartMonotonicMicroseconds)
            return false;

        bool sawStateThree = false;
        bool sawStateOne = false;
        int landingOrdinal = -1;
        for (int ordinal = departureOrdinal; ordinal <= landingBoundOrdinal; ordinal++)
        {
            Id65V3TelemetryTuple sample = parsed.Samples[ordinal];
            if (sample.Lives != 4 || sample.PlayerZ < 1024)
                return false;
            if (landingOrdinal >= 0)
            {
                if (sample.DeathState != 0)
                    return false;
                continue;
            }
            if (!sawStateOne)
            {
                if (sample.DeathState == 3)
                {
                    sawStateThree = true;
                    continue;
                }
                if (sawStateThree && sample.DeathState == 1)
                {
                    sawStateOne = true;
                    continue;
                }
                return false;
            }
            if (sample.DeathState == 1)
                continue;
            if (sample.DeathState == 0 &&
                parsed.Samples[ordinal - 1].DeathState == 1)
            {
                landingOrdinal = ordinal;
                continue;
            }
            return false;
        }
        return sawStateThree && sawStateOne && landingOrdinal > departureOrdinal &&
            parsed.Samples[landingOrdinal].ElapsedMilliseconds -
                parsed.Samples[departureOrdinal].ElapsedMilliseconds <=
                    AssistedPrimaryLandingMaximumMilliseconds &&
            landingBoundOrdinal - landingOrdinal + 1 >= AssistedReadinessMinimumSamples &&
            parsed.Samples[landingBoundOrdinal].ElapsedMilliseconds -
                parsed.Samples[landingOrdinal].ElapsedMilliseconds >=
                    AssistedReadinessMinimumMilliseconds;
    }

    internal static bool RespawnBindingIsAccepted(
        Id65V3TelemetryParseResult parsed,
        int fallOrdinal,
        string operatorAtUtc,
        int boundOrdinal)
    {
        if (fallOrdinal < 0 || fallOrdinal >= parsed.Samples.Length ||
            boundOrdinal <= fallOrdinal || boundOrdinal >= parsed.Samples.Length)
        {
            return false;
        }
        DateTimeOffset operatorAt = ParseCanonicalUtc(
            operatorAtUtc,
            "respawn operator marker");
        int firstDirectionalOrdinal = Array.FindIndex(
            parsed.Samples,
            item => ParseCanonicalUtc(item.AtUtc, "respawn watcher sample") >= operatorAt);
        Id65V3TelemetryTuple? firstRecovery = parsed.Samples[(fallOrdinal + 1)..]
            .FirstOrDefault(item => item is { Lives: 3, DeathState: 0, PlayerZ: >= 1024 });
        return firstDirectionalOrdinal == boundOrdinal &&
            parsed.Samples[boundOrdinal] is { Lives: 3, DeathState: 0, PlayerZ: >= 1024 } &&
            firstRecovery is not null && firstRecovery.SampleOrdinal <= boundOrdinal &&
            firstRecovery.ElapsedMilliseconds - parsed.Samples[fallOrdinal].ElapsedMilliseconds <=
                MaximumPostDeathRecoveryMilliseconds &&
            ParseCanonicalUtc(firstRecovery.AtUtc, "first respawn recovery") <= operatorAt;
    }

    internal static bool FallBindingIsAccepted(
        Id65V3TelemetryParseResult parsed,
        string operatorAtUtc,
        int boundOrdinal)
    {
        if (boundOrdinal <= 0 || boundOrdinal >= parsed.Samples.Length)
            return false;
        DateTimeOffset operatorAt = ParseCanonicalUtc(operatorAtUtc, "fall operator marker");
        Id65V3TelemetryTuple sample = parsed.Samples[boundOrdinal];
        double deltaMilliseconds =
            (ParseCanonicalUtc(sample.AtUtc, "fall watcher sample") - operatorAt)
            .TotalMilliseconds;
        return deltaMilliseconds is >= 0 and <= MaximumFallAuthorizationBindingMilliseconds &&
            parsed.Samples[boundOrdinal - 1] is
                { Lives: 4, DeathState: 0, PlayerZ: >= 1024 } &&
            sample is { Lives: 3, DeathState: 4, PlayerZ: < 1024 } &&
            parsed.Samples[..boundOrdinal].Where(item =>
                    ParseCanonicalUtc(item.AtUtc, "fall authorization watcher sample") >= operatorAt)
                .All(item => item is { Lives: 4, DeathState: 0, PlayerZ: >= 1024 });
    }

    private static (string AtUtc, int Ordinal) CaptureBoundSample(
        IReadOnlyDictionary<string, CaptureJournalEventReadback> bindings,
        string role)
    {
        JsonElement payload = bindings[role].Payload;
        return (
            RequireCaptureString(payload, "sampleAtUtc", "watcher binding"),
            checked((int)RequireCaptureInt64(payload, "sampleOrdinal", "watcher binding")));
    }

    private static void ValidateCaptureSessionArtifactIds(
        Id65V3RuntimeEvidenceSession session,
        IReadOnlyList<CaptureRawArtifactReadback> rawArtifacts)
    {
        if (session.ProfileBeforeArtifactId != rawArtifacts.Single(item =>
                item.Kind == "runtime-profile-before").ArtifactId ||
            session.ProfileAfterArtifactId != rawArtifacts.Single(item =>
                item.Kind == "runtime-profile-after").ArtifactId ||
            session.WatcherArtifactId != rawArtifacts.Single(item =>
                item.Kind == "watcher-stdout").ArtifactId)
        {
            throw new InvalidDataException(
                $"Session {session.SessionId} raw artifact references differ from its capture journal.");
        }
    }

    private static void ValidateCaptureJournalRawArtifacts(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyList<CaptureJournalReadback> journals)
    {
        HashSet<string> journalIds = journals
            .Select(journal => journal.ArtifactId)
            .ToHashSet(StringComparer.Ordinal);
        HashSet<string> rawIds = journals
            .SelectMany(journal => journal.RawArtifacts)
            .Select(raw => raw.ArtifactId)
            .ToHashSet(StringComparer.Ordinal);
        if (rawIds.Count != journals.Sum(journal => journal.RawArtifacts.Length) ||
            rawIds.Overlaps(journalIds))
        {
            throw new InvalidDataException(
                "Capture journals repeat a raw artifact or include a journal in the raw seal.");
        }

        string[] expectedCatalogIds = rawIds.Concat(journalIds).Order(StringComparer.Ordinal).ToArray();
        if (!package.ArtifactCatalog.Select(artifact => artifact.ArtifactId)
                .Order(StringComparer.Ordinal).SequenceEqual(expectedCatalogIds))
        {
            throw new InvalidDataException(
                "Runtime artifact catalog differs from the exact journal-plus-raw capture set.");
        }

        foreach (CaptureJournalReadback journal in journals)
        {
            Id65V3RuntimeEvidenceArtifact journalArtifact = RequireArtifact(
                package,
                journal.ArtifactId,
                "capture-event-journal");
            if (journalArtifact.SessionId != journal.SessionId ||
                journalArtifact.MediaStartAtUtc is not null ||
                journalArtifact.MediaEndAtUtc is not null)
            {
                throw new InvalidDataException(
                    $"Capture journal {journal.ArtifactId} carries invalid outer metadata.");
            }

            foreach (CaptureRawArtifactReadback raw in journal.RawArtifacts)
            {
                Id65V3RuntimeEvidenceArtifact artifact = RequireArtifact(
                    package,
                    raw.ArtifactId,
                    raw.Kind);
                if (artifact.SessionId != journal.SessionId ||
                    artifact.RepoRelativePath != raw.RepoRelativePath ||
                    artifact.Sha256 != raw.Sha256 ||
                    artifact.ByteLength != raw.ByteLength ||
                    artifact.CapturedAtUtc != raw.CapturedAtUtc ||
                    artifact.MediaStartAtUtc != raw.MediaStartAtUtc ||
                    artifact.MediaEndAtUtc != raw.MediaEndAtUtc)
                {
                    throw new InvalidDataException(
                        $"Outer artifact {raw.ArtifactId} differs from its sealed journal metadata.");
                }
            }
        }

        CaptureRawArtifactReadback[] allRaw = journals.SelectMany(journal => journal.RawArtifacts).ToArray();
        if (allRaw.Select(raw => raw.Sha256).Distinct(StringComparer.Ordinal).Count() != allRaw.Length)
        {
            throw new InvalidDataException(
                "Two cold sessions or raw roles reuse identical captured bytes.");
        }
        if (journals.Select(journal => RequireArtifact(package, journal.ArtifactId, "capture-event-journal").Sha256)
                .Distinct(StringComparer.Ordinal).Count() != journals.Count)
        {
            throw new InvalidDataException("Two cold sessions reuse identical capture-journal bytes.");
        }
    }

    private static void ValidateCaptureJournalChecklistSlices(
        Id65V3RuntimeEvidencePackage package,
        IReadOnlyList<CaptureJournalReadback> journals)
    {
        Dictionary<string, CaptureJournalReadback> bySession = journals.ToDictionary(
            journal => journal.SessionId,
            StringComparer.Ordinal);
        foreach (Id65V3RuntimeEvidenceCheck check in package.VisualChecks)
        {
            if (check.Status == Id65V3RuntimeEvidenceCheckStatus.NotObserved)
                continue;
            CaptureJournalReadback journal = bySession[check.SessionId];
            CaptureRawArtifactReadback recording = journal.RawArtifacts.Single(raw =>
                raw.Kind == "screen-recording");
            string expectedObservedAt;

            if (check.CheckId == "candidate.lone-pad-visibility")
            {
                CaptureRawArtifactReadback frame = RequireCaptureRawRole(
                    journal,
                    "candidate.lone-pad-visibility");
                RequireExactCaptureFrameRefs(check, [frame]);
                expectedObservedAt = frame.CapturedAtUtc;
            }
            else if (check.CheckId.Contains("same-pad-yaw", StringComparison.Ordinal))
            {
                if (check.CheckId == "candidate.cold-reset-same-pad-yaw")
                {
                    CaptureJournalReadback edge2 = bySession["candidate-edge-2"];
                    CaptureRawArtifactReadback before = RequireCaptureRawRole(
                        edge2,
                        "candidate.edge-2-same-pad-yaw.post-respawn-and-cold-reset-pre-frame");
                    CaptureRawArtifactReadback after = RequireCaptureRawRole(
                        journal,
                        "candidate.cold-reset-same-pad-yaw.post-reset-frame");
                    CaptureJournalEventReadback selector = RequireCaptureMarker(
                        journal.Events,
                        "selector-started",
                        "selector");
                    CaptureJournalEventReadback landing = RequireCaptureMarker(
                        journal.Events,
                        "landing-marked",
                        "landing");
                    RequireExactCaptureRecordingAndFrames(
                        check,
                        recording,
                        selector.AtUtc,
                        landing.AtUtc,
                        [before, after]);
                    expectedObservedAt = after.CapturedAtUtc;
                }
                else
                {
                    string prefix = check.CheckId;
                    CaptureRawArtifactReadback before = RequireCaptureRawRole(
                        journal,
                        prefix + ".pre-fall-frame");
                    CaptureRawArtifactReadback after = RequireCaptureRawRole(
                        journal,
                        check.SessionId == "candidate-edge-2"
                            ? prefix + ".post-respawn-and-cold-reset-pre-frame"
                            : prefix + ".post-respawn-frame");
                    CaptureJournalEventReadback fall = RequireCaptureMarker(
                        journal.Events,
                        "fall-started",
                        "edge-fall");
                    CaptureJournalEventReadback respawn = RequireCaptureMarker(
                        journal.Events,
                        "respawn-marked",
                        "edge-respawn");
                    RequireExactCaptureRecordingAndFrames(
                        check,
                        recording,
                        fall.AtUtc,
                        respawn.AtUtc,
                        [before, after]);
                    expectedObservedAt = after.CapturedAtUtc;
                }
            }
            else
            {
                CaptureJournalEventReadback start;
                CaptureJournalEventReadback end;
                if (check.CheckId == "candidate.hands-off-20-seconds")
                {
                    start = RequireCaptureMarker(journal.Events, "hold-started", "stable-hold");
                    end = RequireCaptureMarker(journal.Events, "hold-ended", "stable-hold");
                }
                else
                {
                    start = RequireCaptureMarker(journal.Events, "check-started", check.CheckId);
                    end = RequireCaptureMarker(journal.Events, "check-ended", check.CheckId);
                }
                RequireExactCaptureRecordingSlice(check, recording, start.AtUtc, end.AtUtc);
                expectedObservedAt = end.AtUtc;
            }

            if (check.ObservedAtUtc != expectedObservedAt)
            {
                throw new InvalidDataException(
                    $"Check {check.CheckId} observation time differs from its journal marker or frame.");
            }
        }
    }

    private static CaptureRawArtifactReadback RequireCaptureRawRole(
        CaptureJournalReadback journal,
        string role)
    {
        CaptureRawArtifactReadback[] matches = journal.RawArtifacts.Where(raw =>
            raw.Kind == "frame" && raw.CaptureRole == role).ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                $"Capture journal {journal.SessionId} lacks one exact frame role {role}.");
    }

    private static void RequireExactCaptureRecordingSlice(
        Id65V3RuntimeEvidenceCheck check,
        CaptureRawArtifactReadback recording,
        string startAtUtc,
        string endAtUtc)
    {
        if (check.ArtifactRefs.Length != 1)
            throw new InvalidDataException($"Check {check.CheckId} does not use one exact recording slice.");
        RequireExactCaptureRecordingRef(check, check.ArtifactRefs[0], recording, startAtUtc, endAtUtc);
    }

    private static void RequireExactCaptureRecordingAndFrames(
        Id65V3RuntimeEvidenceCheck check,
        CaptureRawArtifactReadback recording,
        string startAtUtc,
        string endAtUtc,
        IReadOnlyList<CaptureRawArtifactReadback> frames)
    {
        Id65V3RuntimeEvidenceSlice[] recordingRefs = check.ArtifactRefs.Where(reference =>
            reference.ArtifactId == recording.ArtifactId).ToArray();
        if (recordingRefs.Length != 1 || check.ArtifactRefs.Length != frames.Count + 1)
            throw new InvalidDataException($"Check {check.CheckId} has a noncanonical recording/frame set.");
        RequireExactCaptureRecordingRef(check, recordingRefs[0], recording, startAtUtc, endAtUtc);
        RequireExactCaptureFrameRefs(
            check,
            frames,
            check.ArtifactRefs.Where(reference => reference.ArtifactId != recording.ArtifactId).ToArray());
    }

    private static void RequireExactCaptureRecordingRef(
        Id65V3RuntimeEvidenceCheck check,
        Id65V3RuntimeEvidenceSlice reference,
        CaptureRawArtifactReadback recording,
        string startAtUtc,
        string endAtUtc)
    {
        if (recording.MediaStartAtUtc is null || reference.ArtifactId != recording.ArtifactId ||
            reference.FrameSha256 is not null ||
            reference.StartMilliseconds != CaptureOffsetMilliseconds(recording.MediaStartAtUtc, startAtUtc) ||
            reference.EndMilliseconds != CaptureOffsetMilliseconds(recording.MediaStartAtUtc, endAtUtc))
        {
            throw new InvalidDataException(
                $"Check {check.CheckId} recording slice differs from its exact journal interval.");
        }
    }

    private static void RequireExactCaptureFrameRefs(
        Id65V3RuntimeEvidenceCheck check,
        IReadOnlyList<CaptureRawArtifactReadback> frames,
        IReadOnlyList<Id65V3RuntimeEvidenceSlice>? references = null)
    {
        references ??= check.ArtifactRefs;
        if (references.Count != frames.Count)
            throw new InvalidDataException($"Check {check.CheckId} frame set differs from its journal.");
        for (int index = 0; index < frames.Count; index++)
        {
            CaptureRawArtifactReadback frame = frames[index];
            Id65V3RuntimeEvidenceSlice reference = references[index];
            if (reference.ArtifactId != frame.ArtifactId || reference.StartMilliseconds is not null ||
                reference.EndMilliseconds is not null || reference.FrameSha256 != frame.Sha256)
            {
                throw new InvalidDataException(
                    $"Check {check.CheckId} frame reference differs from its journal role and hash.");
            }
        }
    }

    private static long CaptureOffsetMilliseconds(string mediaStartAtUtc, string markerAtUtc)
    {
        double milliseconds = (
            ParseCanonicalUtc(markerAtUtc, "capture marker") -
            ParseCanonicalUtc(mediaStartAtUtc, "capture media start")).TotalMilliseconds;
        if (milliseconds < 0 || milliseconds > long.MaxValue || milliseconds != Math.Truncate(milliseconds))
            throw new InvalidDataException("Capture marker is outside an integral recording-millisecond offset.");
        return checked((long)milliseconds);
    }

    private static Id65V3RuntimeEvidenceArtifact RequireArtifact(
        Id65V3RuntimeEvidencePackage package,
        string artifactId,
        string kind)
    {
        Id65V3RuntimeEvidenceArtifact artifact = package.ArtifactCatalog.SingleOrDefault(item =>
            item.ArtifactId == artifactId) ??
            throw new InvalidDataException($"Artifact reference '{artifactId}' does not resolve.");
        if (artifact.Kind != kind)
            throw new InvalidDataException($"Artifact '{artifactId}' has kind '{artifact.Kind}', expected '{kind}'.");
        return artifact;
    }

    private static T DeserializeCanonicalArtifact<T>(byte[] bytes, string context) where T : class
    {
        if (bytes.Length == 0 || bytes.Length > MaximumTelemetryLogBytes ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException($"{context} has an invalid length or UTF-8 BOM.");
        }
        _ = DecodeStrictUtf8(bytes, context);
        List<string> errors = [];
        using (JsonDocument document = JsonDocument.Parse(
                   bytes,
                   new JsonDocumentOptions
                   {
                       AllowTrailingCommas = false,
                       CommentHandling = JsonCommentHandling.Disallow,
                       MaxDepth = 64
                   }))
        {
            RejectDuplicateProperties(document.RootElement, "$", errors);
        }
        if (errors.Count != 0)
            throw new InvalidDataException($"{context}: {errors[0]}");
        T value = JsonSerializer.Deserialize<T>(bytes, JsonOptions) ??
            throw new InvalidDataException($"{context} deserialized to null.");
        if (!bytes.AsSpan().SequenceEqual(StrictUtf8.GetBytes(SerializeCanonicalValue(value))))
            throw new InvalidDataException($"{context} is not exact RFC 8785 canonical UTF-8 JSON.");
        return value;
    }

    private static T DeserializeVerifierCanonicalArtifact<T>(byte[] bytes, string context) where T : class
    {
        if (bytes.Length < 2 || bytes.Length > MaximumTelemetryLogBytes ||
            bytes[^1] != (byte)'\n' || bytes[^2] == (byte)'\n' ||
            bytes.AsSpan().Contains((byte)'\r') ||
            (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF))
        {
            throw new InvalidDataException(
                $"{context} is not one exact LF-terminated verifier JSON object.");
        }
        byte[] canonicalObject = bytes[..^1];
        _ = DecodeStrictUtf8(canonicalObject, context);
        List<string> errors = [];
        using (JsonDocument document = JsonDocument.Parse(
                   canonicalObject,
                   new JsonDocumentOptions
                   {
                       AllowTrailingCommas = false,
                       CommentHandling = JsonCommentHandling.Disallow,
                       MaxDepth = 64
                   }))
        {
            RejectDuplicateProperties(document.RootElement, "$", errors);
        }
        if (errors.Count != 0)
            throw new InvalidDataException($"{context}: {errors[0]}");
        T value = JsonSerializer.Deserialize<T>(canonicalObject, JsonOptions) ??
            throw new InvalidDataException($"{context} deserialized to null.");
        JsonElement serialized = JsonSerializer.SerializeToElement(value, JsonOptions);
        byte[] expected = SerializeVerifierCanonicalLf(serialized);
        if (!bytes.AsSpan().SequenceEqual(expected))
            throw new InvalidDataException($"{context} differs from the frozen verifier serializer.");
        return value;
    }

    private static string DecodeStrictUtf8(ReadOnlySpan<byte> bytes, string context)
    {
        try
        {
            return StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InvalidDataException($"{context} is not valid UTF-8.", exception);
        }
    }

    private static void GuardProfileManifestPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath) || relativePath[0] == '/' ||
            relativePath.Contains('\\') ||
            relativePath.Any(character => character < 0x20 || character == 0x7F) ||
            relativePath.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".." ||
                segment.Normalize(NormalizationForm.FormC) != segment))
        {
            throw new InvalidDataException("Profile snapshot path is not a strict POSIX profile-relative path.");
        }
    }

    private static bool RuntimeProfileObservationIsExact(Id65V3RuntimeEvidenceProfile profile) =>
        profile.SettingsBeforeSha256 == profile.ExternalHomeSettingsSha256 &&
        profile.SettingsAfterSha256 == profile.ExternalHomeSettingsSha256 &&
        profile.NormalUserSettingsBeforeSha256 == profile.HostNormalSettingsSha256 &&
        profile.NormalUserSettingsAfterSha256 == profile.HostNormalSettingsSha256 &&
        profile.CheatFileCountBefore == 0 && profile.CheatFileCountAfter == 0 &&
        profile.GameSettingsFileCountBefore == 0 && profile.GameSettingsFileCountAfter == 0 &&
        profile.MemoryCardFileCountBefore == 0 && profile.MemoryCardFileCountAfter == 0 &&
        profile.SaveStateFileCountBefore == 0 && profile.SaveStateFileCountAfter == 0 &&
        profile.PatchFileCountBefore == 0 && profile.PatchFileCountAfter == 0 &&
        profile.ChangedProfilePaths is not null &&
        profile.ChangedProfilePaths.SequenceEqual(
            profile.ChangedProfilePaths.Distinct(StringComparer.Ordinal)
                .Order(UnicodeScalarStringComparer.Instance)) &&
        profile.ChangedProfilePaths.All(IsAllowedVolatileProfilePath) &&
        profile.SaveStateLoaded == false &&
        profile.SaveStateCreated == false && profile.InGameSaveUsed == false && profile.ActiveCheatCount == 0;

    private static bool RuntimeProfileObservationIsComplete(Id65V3RuntimeEvidenceProfile profile) =>
        profile.SettingsBeforeSha256 is not null && profile.SettingsAfterSha256 is not null &&
        profile.NormalUserSettingsBeforeSha256 is not null && profile.NormalUserSettingsAfterSha256 is not null &&
        profile.CheatFileCountBefore is not null && profile.CheatFileCountAfter is not null &&
        profile.GameSettingsFileCountBefore is not null && profile.GameSettingsFileCountAfter is not null &&
        profile.MemoryCardFileCountBefore is not null && profile.MemoryCardFileCountAfter is not null &&
        profile.SaveStateFileCountBefore is not null && profile.SaveStateFileCountAfter is not null &&
        profile.PatchFileCountBefore is not null && profile.PatchFileCountAfter is not null &&
        profile.ChangedProfilePaths is not null && profile.SaveStateLoaded is not null &&
        profile.SaveStateCreated is not null && profile.InGameSaveUsed is not null &&
        profile.ActiveCheatCount is not null;

    private static bool RuntimeProfileObservationHasAny(Id65V3RuntimeEvidenceProfile profile) =>
        profile.SettingsBeforeSha256 is not null || profile.SettingsAfterSha256 is not null ||
        profile.NormalUserSettingsBeforeSha256 is not null || profile.NormalUserSettingsAfterSha256 is not null ||
        profile.CheatFileCountBefore is not null || profile.CheatFileCountAfter is not null ||
        profile.GameSettingsFileCountBefore is not null || profile.GameSettingsFileCountAfter is not null ||
        profile.MemoryCardFileCountBefore is not null || profile.MemoryCardFileCountAfter is not null ||
        profile.SaveStateFileCountBefore is not null || profile.SaveStateFileCountAfter is not null ||
        profile.PatchFileCountBefore is not null || profile.PatchFileCountAfter is not null ||
        profile.ChangedProfilePaths is not null || profile.SaveStateLoaded is not null ||
        profile.SaveStateCreated is not null || profile.InGameSaveUsed is not null ||
        profile.ActiveCheatCount is not null;

    private static bool RuntimeProfileHasUnsafeObservation(Id65V3RuntimeEvidenceProfile profile) =>
        (profile.SettingsBeforeSha256 is not null && profile.SettingsBeforeSha256 != profile.ExternalHomeSettingsSha256) ||
        (profile.SettingsAfterSha256 is not null && profile.SettingsAfterSha256 != profile.ExternalHomeSettingsSha256) ||
        (profile.NormalUserSettingsBeforeSha256 is not null &&
            profile.NormalUserSettingsBeforeSha256 != profile.HostNormalSettingsSha256) ||
        (profile.NormalUserSettingsAfterSha256 is not null &&
            profile.NormalUserSettingsAfterSha256 != profile.HostNormalSettingsSha256) ||
        profile.CheatFileCountBefore is not (null or 0) || profile.CheatFileCountAfter is not (null or 0) ||
        profile.GameSettingsFileCountBefore is not (null or 0) ||
        profile.GameSettingsFileCountAfter is not (null or 0) ||
        profile.MemoryCardFileCountBefore is not (null or 0) ||
        profile.MemoryCardFileCountAfter is not (null or 0) ||
        profile.SaveStateFileCountBefore is not (null or 0) || profile.SaveStateFileCountAfter is not (null or 0) ||
        profile.PatchFileCountBefore is not (null or 0) || profile.PatchFileCountAfter is not (null or 0) ||
        profile.SaveStateLoaded == true || profile.SaveStateCreated == true || profile.InGameSaveUsed == true ||
        profile.ActiveCheatCount is not (null or 0) ||
        (profile.ChangedProfilePaths is not null &&
            (!profile.ChangedProfilePaths.SequenceEqual(
                 profile.ChangedProfilePaths.Distinct(StringComparer.Ordinal)
                     .Order(UnicodeScalarStringComparer.Instance)) ||
             profile.ChangedProfilePaths.Any(path => !IsAllowedVolatileProfilePath(path))));

    private static void ValidateRuntimeProfileIdentity(Id65V3RuntimeEvidenceProfile profile)
    {
        Id65V3RuntimeEvidenceProfile expected = BuildExpectedRuntimeProfile();
        Id65V3RuntimeEvidenceProfile identityOnly = profile with
        {
            SettingsBeforeSha256 = null,
            SettingsAfterSha256 = null,
            NormalUserSettingsBeforeSha256 = null,
            NormalUserSettingsAfterSha256 = null,
            CheatFileCountBefore = null,
            CheatFileCountAfter = null,
            GameSettingsFileCountBefore = null,
            GameSettingsFileCountAfter = null,
            MemoryCardFileCountBefore = null,
            MemoryCardFileCountAfter = null,
            SaveStateFileCountBefore = null,
            SaveStateFileCountAfter = null,
            PatchFileCountBefore = null,
            PatchFileCountAfter = null,
            ChangedProfilePaths = null,
            SaveStateLoaded = null,
            SaveStateCreated = null,
            InGameSaveUsed = null,
            ActiveCheatCount = null
        };
        if (SerializeCanonicalValue(identityOnly) != SerializeCanonicalValue(expected) ||
            RecomputeRuntimeProfileSha256(profile) != ExpectedRuntimeProfileSha256)
        {
            throw new InvalidDataException("Pinned isolated DuckStation runtime profile changed.");
        }
    }

    private static bool IsAllowedVolatileProfilePath(string relativePath)
    {
        try
        {
            GuardProfileManifestPath(relativePath);
        }
        catch (InvalidDataException)
        {
            return false;
        }
        string[] segments = relativePath.Split('/');
        if (relativePath == "playtime.dat")
            return true;
        return segments.Length > 1 && segments[0] is "cache" or "screenshots" or "videos";
    }

    private static bool IsAllowedRuntimeDataPath(string relativePath) =>
        relativePath is "settings.ini" or ExternalHomeBiosManifestPath ||
        IsAllowedVolatileProfilePath(relativePath);

    private static Id65V3TelemetryTuple DecodeSample(Match match)
    {
        int ordinal = int.Parse(match.Groups["ordinal"].Value, CultureInfo.InvariantCulture);
        long elapsed = long.Parse(match.Groups["elapsed"].Value, CultureInfo.InvariantCulture);
        int lives = int.Parse(match.Groups["lives"].Value, CultureInfo.InvariantCulture);
        int state = int.Parse(match.Groups["state"].Value, CultureInfo.InvariantCulture);
        int z = int.Parse(match.Groups["z"].Value, CultureInfo.InvariantCulture);
        string livesRaw = match.Groups["livesRaw"].Value;
        string stateRaw = match.Groups["stateRaw"].Value;
        string zRaw = match.Groups["zRaw"].Value;
        byte[] livesBytes = Convert.FromHexString(livesRaw);
        byte[] stateBytes = Convert.FromHexString(stateRaw);
        byte[] zBytes = Convert.FromHexString(zRaw);
        if (BinaryPrimitives.ReadUInt32LittleEndian(livesBytes) != (uint)lives ||
            BinaryPrimitives.ReadUInt32LittleEndian(stateBytes) != (uint)state ||
            BinaryPrimitives.ReadInt32LittleEndian(zBytes) != z)
        {
            throw new InvalidDataException("Watcher decoded values do not match their raw little-endian words.");
        }
        string label = state switch
        {
            0 => "gameplay/non-death",
            4 => "death-respawn",
            5 => "game-over",
            _ => $"other-{state}"
        };
        if (match.Groups["label"].Value != label)
            throw new InvalidDataException("Watcher death-state label does not match the decoded word.");
        string marker = z < 1024 ? "YES" : "no";
        if (match.Groups["marker"].Value != marker)
            throw new InvalidDataException("Watcher Z threshold marker does not match the decoded word.");
        DateTimeOffset at = ParseRawTimestamp(match.Groups["timestamp"].Value);
        return new(FormatUtc(at), ordinal, elapsed, lives, state, z, livesRaw, stateRaw, zRaw);
    }

    private static DateTimeOffset ParseRawTimestamp(string value)
    {
        if (!CanonicalUtcRegex().IsMatch(value) ||
            !DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"Watcher timestamp '{value}' is not the frozen UTC-millisecond form.");
        }
        return parsed;
    }

    private static DateTimeOffset ParseCanonicalUtc(string value, string context)
    {
        if (!CanonicalUtcRegex().IsMatch(value) ||
            !DateTimeOffset.TryParseExact(
                value,
                "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
        {
            throw new InvalidDataException($"{context} timestamp '{value}' is not canonical UTC milliseconds.");
        }
        return parsed;
    }

    private static string FormatUtc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static long MaximumGap(IReadOnlyList<Id65V3TelemetryTuple> samples)
    {
        long max = 0;
        for (int index = 1; index < samples.Count; index++)
        {
            long gap = checked(samples[index].ElapsedMilliseconds - samples[index - 1].ElapsedMilliseconds);
            if (gap < 0)
                throw new InvalidDataException("Watcher elapsed milliseconds regressed.");
            max = Math.Max(max, gap);
        }
        return max;
    }

    private static void ValidateAllCanonicalTimestamps(Id65V3RuntimeEvidencePackage package)
    {
        foreach (Id65V3RuntimeEvidenceSession session in package.Sessions)
        {
            foreach (string? value in new[]
            {
                session.ProcessStartedAtUtc, session.WatcherConnectedAtUtc, session.ManuallyResumedAtUtc,
                session.SelectorStartedAtUtc, session.LandingAtUtc, session.HoldStartedAtUtc,
                session.HoldEndedAtUtc, session.FallStartedAtUtc, session.RespawnAtUtc, session.ProcessExitedAtUtc
            })
            {
                if (value is not null)
                    _ = ParseCanonicalUtc(value, $"session {session.SessionId}");
            }
        }
        foreach (Id65V3RuntimeEvidenceCheck check in package.VisualChecks)
        {
            if (check.ObservedAtUtc is not null)
                _ = ParseCanonicalUtc(check.ObservedAtUtc, $"check {check.CheckId}");
        }
    }

    private static void RejectDuplicateProperties(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    errors.Add($"Duplicate JSON property '{property.Name}' at {path}.");
                RejectDuplicateProperties(property.Value, path + "." + property.Name, errors);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
                RejectDuplicateProperties(item, $"{path}[{index++}]", errors);
        }
    }

    private static void RejectSymlinkSegments(string root, IReadOnlyList<string> segments)
    {
        string current = root;
        foreach (string segment in segments)
        {
            current = Path.Combine(current, segment);
            FileSystemInfo info = Directory.Exists(current)
                ? new DirectoryInfo(current)
                : new FileInfo(current);
            if (info.Exists && info.LinkTarget is not null)
                throw new InvalidDataException("Runtime evidence artifact path traverses a symbolic link.");
        }
    }

    private static string BuildArtifactSetManifest(IEnumerable<Id65V3RuntimeEvidenceArtifact> artifacts)
    {
        StringBuilder builder = new("id65-v3-runtime-artifact-set-v1\n");
        foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts.OrderBy(item => item.ArtifactId, StringComparer.Ordinal))
        {
            builder.Append(artifact.ArtifactId).Append('|').Append(artifact.SessionId).Append('|')
                .Append(artifact.Kind).Append('|')
                .Append(artifact.RepoRelativePath).Append('|').Append(artifact.ByteLength).Append('|')
                .Append(artifact.Sha256).Append('\n');
        }
        return builder.ToString();
    }

    private static string BuildSourceClosureManifest(Id65V3RuntimeEvidenceSourceIdentity source) =>
        "id65-v3-runtime-source-closure-v2\n" +
        $"watcherRelativePath={source.WatcherRelativePath}\n" +
        $"watcherSha256={source.WatcherSha256}\n" +
        $"watcherMode={source.WatcherMode}\n" +
        $"profileBuilderRelativePath={source.ProfileBuilderRelativePath}\n" +
        $"profileBuilderSha256={source.ProfileBuilderSha256}\n" +
        $"profileBuilderMode={source.ProfileBuilderMode}\n" +
        $"profileVerifierRelativePath={source.ProfileVerifierRelativePath}\n" +
        $"profileVerifierSha256={source.ProfileVerifierSha256}\n" +
        $"profileVerifierMode={source.ProfileVerifierMode}\n" +
        $"automationRelativePath={source.AutomationRelativePath}\n" +
        $"automationSha256={source.AutomationSha256}\n" +
        $"automationMode={source.AutomationMode}\n" +
        $"captureCoordinatorRelativePath={source.CaptureCoordinatorRelativePath}\n" +
        $"captureCoordinatorMode={source.CaptureCoordinatorMode}\n" +
        $"captureCoordinatorSha256={source.CaptureCoordinatorSha256}\n" +
        $"runtimeProfileManifestDomain={source.RuntimeProfileManifestDomain}\n" +
        $"runtimeProfileSha256={source.RuntimeProfileSha256}\n" +
        $"profileId={source.ProfileId}\n" +
        $"baseBinSha256={source.BaseBinSha256}\n" +
        $"baseCueSha256={source.BaseCueSha256}\n" +
        $"candidateBinRelativePath={source.CandidateBinRelativePath}\n" +
        $"candidateBinSha256={source.CandidateBinSha256}\n" +
        $"candidateCueRelativePath={source.CandidateCueRelativePath}\n" +
        $"candidateCueSha256={source.CandidateCueSha256}\n" +
        $"lockedBaseRow80DataSha256={source.LockedBaseRow80DataSha256}\n" +
        $"lockedBaseModelSha256={source.LockedBaseModelSha256}\n" +
        $"environmentHeaderRepairV3BeforeRow80DataSha256={source.EnvironmentHeaderRepairV3BeforeRow80DataSha256}\n" +
        $"environmentHeaderRepairV3AfterRow80DataSha256={source.EnvironmentHeaderRepairV3AfterRow80DataSha256}\n" +
        $"environmentHeaderRepairV3BeforeModelSha256={source.EnvironmentHeaderRepairV3BeforeModelSha256}\n" +
        $"environmentHeaderRepairV3AfterModelSha256={source.EnvironmentHeaderRepairV3AfterModelSha256}\n" +
        $"outputCollisionTreeSha256={source.OutputCollisionTreeSha256}\n" +
        $"outputCollisionBlocksSha256={source.OutputCollisionBlocksSha256}\n" +
        $"staticDiffManifestSha256={source.StaticDiffManifestSha256}\n" +
        $"deterministicStaticPlanSha256={source.DeterministicStaticPlanSha256}\n" +
        $"rawSectorDiffSha256={source.RawSectorDiffSha256}\n" +
        $"constructionPlanRelativePath={source.ConstructionPlanRelativePath}\n" +
        $"constructionPlanSha256={source.ConstructionPlanSha256}\n" +
        $"staticReadbackReceiptRelativePath={source.StaticReadbackReceiptRelativePath}\n" +
        $"staticReadbackReceiptSha256={source.StaticReadbackReceiptSha256}\n" +
        $"runtimeChecklistRelativePath={source.RuntimeChecklistRelativePath}\n" +
        $"runtimeChecklistSha256={source.RuntimeChecklistSha256}\n" +
        $"locationGuideRelativePath={source.LocationGuideRelativePath}\n" +
        $"locationGuideSha256={source.LocationGuideSha256}\n" +
        $"revealCommandRelativePath={source.RevealCommandRelativePath}\n" +
        $"revealCommandSha256={source.RevealCommandSha256}\n" +
        $"failedV1ProfileId={source.FailedV1ProfileId}\n" +
        $"failedV1BinRelativePath={source.FailedV1BinRelativePath}\n" +
        $"failedV1BinSha256={source.FailedV1BinSha256}\n" +
        $"failedV1OutputDataSha256={source.FailedV1OutputDataSha256}\n" +
        $"failedV1OutputModelSha256={source.FailedV1OutputModelSha256}\n" +
        $"collisionWindingV2ProfileId={source.CollisionWindingV2ProfileId}\n" +
        $"collisionWindingV2BinRelativePath={source.CollisionWindingV2BinRelativePath}\n" +
        $"collisionWindingV2BinSha256={source.CollisionWindingV2BinSha256}\n" +
        $"collisionWindingV2OutputDataSha256={source.CollisionWindingV2OutputDataSha256}\n" +
        $"collisionWindingV2OutputModelSha256={source.CollisionWindingV2OutputModelSha256}\n" +
        $"collisionWindingV2RepairDiffManifestSha256={source.CollisionWindingV2RepairDiffManifestSha256}\n" +
        $"collisionWindingV2ChangedByteCount={source.CollisionWindingV2ChangedByteCount}\n" +
        $"collisionWindingV2RangeCount={source.CollisionWindingV2RangeCount}\n" +
        $"collisionWindingV2RawSectorLba={source.CollisionWindingV2RawSectorLba}\n" +
        $"collisionWindingV2InverseVerified={Lower(source.CollisionWindingV2InverseVerified)}\n" +
        $"failedV1RetestForbidden={Lower(source.FailedV1RetestForbidden)}\n" +
        $"supersededV2SectorSha256={source.SupersededV2SectorSha256}\n" +
        $"environmentHeaderRepairV3AfterSectorSha256={source.EnvironmentHeaderRepairV3AfterSectorSha256}\n" +
        $"environmentHeaderRepairV3AfterEnvironmentSha256={source.EnvironmentHeaderRepairV3AfterEnvironmentSha256}\n" +
        $"environmentHeaderRepairV3DiffManifestSha256={source.EnvironmentHeaderRepairV3DiffManifestSha256}\n" +
        $"environmentHeaderRepairV3ChangedByteCount={source.EnvironmentHeaderRepairV3ChangedByteCount}\n" +
        $"environmentHeaderRepairV3RangeCount={source.EnvironmentHeaderRepairV3RangeCount}\n" +
        $"environmentHeaderRepairV3RawSectorLba={source.EnvironmentHeaderRepairV3RawSectorLba}\n" +
        $"environmentHeaderRepairV3BeforeByte={source.EnvironmentHeaderRepairV3BeforeByte}\n" +
        $"environmentHeaderRepairV3AfterByte={source.EnvironmentHeaderRepairV3AfterByte}\n" +
        $"environmentHeaderRepairV3InverseVerified={Lower(source.EnvironmentHeaderRepairV3InverseVerified)}\n" +
        $"supersededV2RetestForbidden={Lower(source.SupersededV2RetestForbidden)}\n";

    private static string BuildRuntimeProfileManifest(Id65V3RuntimeEvidenceProfile profile) =>
        "id65-v3-runtime-profile-v2\n" +
        $"profileId={profile.ProfileId}\n" +
        $"packageRelativePath={profile.PackageRelativePath}\n" +
        $"appRelativePath={profile.AppRelativePath}\n" +
        $"duckStationExecutableRelativePath={profile.DuckStationExecutableRelativePath}\n" +
        $"externalHomeRelativePath={profile.ExternalHomeRelativePath}\n" +
        $"externalHomeDataRelativePath={profile.ExternalHomeDataRelativePath}\n" +
        $"handoffReceiptRelativePath={profile.HandoffReceiptRelativePath}\n" +
        $"duckStationVersion={profile.DuckStationVersion}\n" +
        $"duckStationExecutableByteLength={profile.DuckStationExecutableByteLength}\n" +
        $"duckStationExecutableSha256={profile.DuckStationExecutableSha256}\n" +
        $"handoffReceiptFormat={profile.HandoffReceiptFormat}\n" +
        $"handoffReceiptFormatVersion={profile.HandoffReceiptFormatVersion}\n" +
        $"handoffReceiptByteLength={profile.HandoffReceiptByteLength}\n" +
        $"handoffReceiptSha256={profile.HandoffReceiptSha256}\n" +
        $"handoffReceiptClosureSha256={profile.HandoffReceiptClosureSha256}\n" +
        $"appContentManifestFormat={profile.AppContentManifestFormat}\n" +
        $"appContentManifestEntryCount={profile.AppContentManifestEntryCount}\n" +
        $"appContentManifestSha256={profile.AppContentManifestSha256}\n" +
        $"outputTreeManifestFormat={profile.OutputTreeManifestFormat}\n" +
        $"appOutputTreeManifestEntryCount={profile.AppOutputTreeManifestEntryCount}\n" +
        $"appOutputTreeManifestSha256={profile.AppOutputTreeManifestSha256}\n" +
        $"homeOutputTreeManifestEntryCount={profile.HomeOutputTreeManifestEntryCount}\n" +
        $"homeOutputTreeManifestSha256={profile.HomeOutputTreeManifestSha256}\n" +
        $"packageEnvelopeFormat={profile.PackageEnvelopeFormat}\n" +
        $"packageEnvelopeEntryCount={profile.PackageEnvelopeEntryCount}\n" +
        $"packageEnvelopeSha256={profile.PackageEnvelopeSha256}\n" +
        $"outputXattrPolicyVersion={profile.OutputXattrPolicyVersion}\n" +
        $"externalHomeSettingsRelativePath={profile.ExternalHomeSettingsRelativePath}\n" +
        $"externalHomeSettingsByteLength={profile.ExternalHomeSettingsByteLength}\n" +
        $"externalHomeSettingsSha256={profile.ExternalHomeSettingsSha256}\n" +
        $"externalHomeBiosRelativePath={profile.ExternalHomeBiosRelativePath}\n" +
        $"externalHomeBiosByteLength={profile.ExternalHomeBiosByteLength}\n" +
        $"externalHomeBiosSha256={profile.ExternalHomeBiosSha256}\n" +
        $"hostNormalSettingsSentinelId={profile.HostNormalSettingsSentinelId}\n" +
        $"hostNormalSettingsByteLength={profile.HostNormalSettingsByteLength}\n" +
        $"hostNormalSettingsSha256={profile.HostNormalSettingsSha256}\n" +
        $"hostNormalSettingsMode={profile.HostNormalSettingsMode}\n" +
        $"safetyDirectories={string.Join(',', profile.SafetyDirectories)}\n" +
        $"allowedVolatilePolicy={profile.AllowedVolatilePolicy}\n" +
        $"trustModel={profile.TrustModel}\n" +
        $"launchGate={profile.LaunchGate}\n" +
        $"powerOffBinding={profile.PowerOffBinding}\n" +
        $"togglePauseBinding={profile.TogglePauseBinding}\n" +
        $"emulationSpeed={profile.EmulationSpeed}\n" +
        $"loadDevicesFromSaveStates={Lower(profile.LoadDevicesFromSaveStates)}\n" +
        $"rewindEnabled={Lower(profile.RewindEnabled)}\n" +
        $"runaheadFrameCount={profile.RunaheadFrameCount}\n" +
        $"saveStateOnExit={Lower(profile.SaveStateOnExit)}\n" +
        $"startPaused={Lower(profile.StartPaused)}\n" +
        $"fastForwardBoot={Lower(profile.FastForwardBoot)}\n" +
        $"patchFastBoot={Lower(profile.PatchFastBoot)}\n" +
        $"loadImagePatches={Lower(profile.LoadImagePatches)}\n" +
        $"loadImageToRam={Lower(profile.LoadImageToRam)}\n" +
        $"cdReadSpeedup={profile.CdReadSpeedup}\n" +
        $"cdSeekSpeedup={profile.CdSeekSpeedup}\n" +
        $"card1Type={profile.Card1Type}\n" +
        $"card2Type={profile.Card2Type}\n" +
        $"multitapMode={profile.MultitapMode}\n" +
        $"pad1Type={profile.Pad1Type}\n" +
        $"otherPadTypes={string.Join(',', profile.OtherPadTypes)}\n" +
        $"pioDeviceType={profile.PioDeviceType}\n" +
        $"pioFlashWriteEnabled={Lower(profile.PioFlashWriteEnabled)}\n" +
        $"pcDriveEnabled={Lower(profile.PcDriveEnabled)}\n" +
        $"pcDriveWritesEnabled={Lower(profile.PcDriveWritesEnabled)}\n" +
        $"gdbServerEnabled={Lower(profile.GdbServerEnabled)}\n" +
        $"gdbServerPort={profile.GdbServerPort}\n" +
        $"expectedCheatFileCount={profile.ExpectedCheatFileCount}\n" +
        $"expectedGameSettingsFileCount={profile.ExpectedGameSettingsFileCount}\n" +
        $"expectedMemoryCardFileCount={profile.ExpectedMemoryCardFileCount}\n" +
        $"expectedSaveStateFileCount={profile.ExpectedSaveStateFileCount}\n" +
        $"expectedPatchFileCount={profile.ExpectedPatchFileCount}\n" +
        $"saveLoadStateHotkeysPresent={Lower(profile.SaveLoadStateHotkeysPresent)}\n" +
        $"textureReplacementOrDumpEnabled={Lower(profile.TextureReplacementOrDumpEnabled)}\n";

    private static Id65V3RuntimeEvidenceSourceIdentity BuildExpectedSourceIdentity()
    {
        string bin = $"{CandidateDirectory}/{CandidateStem}.bin";
        string cue = $"{CandidateDirectory}/{CandidateStem}.cue";
        return new(
            ProfileId: ExpectedProfileId,
            BaseBinSha256: "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8",
            BaseCueSha256: "3c8e28a8dac7b6a4621a5e72ba305047b5267e78720331893b9af80b8940dfa2",
            CandidateBinRelativePath: bin,
            CandidateBinSha256: "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45",
            CandidateCueRelativePath: cue,
            CandidateCueSha256: "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df",
            LockedBaseRow80DataSha256: "7ddbf6d9a6ee6c0f64c8564a89e374ca0ca234ef608ec812aae68e8176de1dc0",
            LockedBaseModelSha256: "1aa6950fe78e71ef4506d823fd33806c13a32838cdb00aadc4e35daffcf17f47",
            EnvironmentHeaderRepairV3BeforeRow80DataSha256: "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9",
            EnvironmentHeaderRepairV3AfterRow80DataSha256: "a759fba359722aedde2e5af4559f8bfe94a83cdeb263c364ebab5a8af7b92dcb",
            EnvironmentHeaderRepairV3BeforeModelSha256: "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79",
            EnvironmentHeaderRepairV3AfterModelSha256: "00c88deb64eed8d829b504422e1dd21b1d6b4449c75126bdb2c0821d077c17b1",
            OutputCollisionTreeSha256: "c77618c5a80fce590c802b197945348d0b4fc04ab666e9b0b911e3a6ff9b2cb9",
            OutputCollisionBlocksSha256: "2d5838ffd9c991d017733f48cadbb9739131fbd6cec75f69a831c03442c1f34a",
            StaticDiffManifestSha256: "c37a00572bd023b9457d2a24944a4864c639c8e160958af135ac4c4f2e0b6a78",
            DeterministicStaticPlanSha256: "ea78191299f6f59c41a3bd09858b9ad6bc18dc5c783baca6455f6c5de67ae9a4",
            RawSectorDiffSha256: "902a27be55ddb0cdfea96bde6b5c309c8e6fa4f7b587c27d45cf4afab3642c91",
            ConstructionPlanRelativePath: $"{CandidateDirectory}/{CandidateStem}-construction-plan.json",
            ConstructionPlanSha256: "e3054b63b4753399203a5eed0ef5b42f7d219957750d186532c5713c6aa906ae",
            StaticReadbackReceiptRelativePath: $"{CandidateDirectory}/{CandidateStem}-static-readback-receipt.json",
            StaticReadbackReceiptSha256: "e4494a9364165951f6a7e54676574c085495a3f800b65e616da2193fa0d01be4",
            RuntimeChecklistRelativePath: $"{CandidateDirectory}/{CandidateStem}-runtime-checklist.md",
            RuntimeChecklistSha256: "7023eb070d6ee1c840bd52013fb851ab53f8de52d2bc68f80a79323a0392363b",
            LocationGuideRelativePath: $"{CandidateDirectory}/{CandidateStem}-location-guide.svg",
            LocationGuideSha256: "bab8c2851ca8743c43c91d70a9ec218796706e68c6fdfa50d977d013520c99a7",
            RevealCommandRelativePath: $"{CandidateDirectory}/{CandidateStem}-Reveal-in-Finder.command",
            RevealCommandSha256: "e5e5c9485666e36aed50e6b0e9f43158f58fcf91f1fbb07f3207f8ccdc0be52a",
            WatcherRelativePath: "tools/Watch-SpyroId65RuntimeState.py",
            WatcherSha256: ExpectedWatcherSha256,
            WatcherMode: "0644",
            ProfileBuilderRelativePath: ProfileBuilderRelativePath,
            ProfileBuilderSha256: ExpectedProfileBuilderSha256,
            ProfileBuilderMode: "0644",
            ProfileVerifierRelativePath: ProfileVerifierRelativePath,
            ProfileVerifierSha256: ExpectedProfileVerifierSha256,
            ProfileVerifierMode: "0644",
            AutomationRelativePath: AutomationRelativePath,
            AutomationSha256: ExpectedAutomationSha256,
            AutomationMode: "0755",
            CaptureCoordinatorRelativePath: CaptureCoordinatorRelativePath,
            CaptureCoordinatorSha256: ExpectedCaptureCoordinatorSha256,
            CaptureCoordinatorMode: "0755",
            RuntimeProfileManifestDomain: "id65-v3-runtime-profile-v2",
            RuntimeProfileSha256: ExpectedRuntimeProfileSha256,
            SourceClosureSha256: ExpectedSourceClosureSha256,
            FailedV1ProfileId: "unused-level-65-remote-blank-isolation-sector216-static-clean-usa-v1",
            FailedV1BinRelativePath: "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-isolation/Unused-Level-65-Remote-Blank-Isolation-Sector216-RUNTIME-CANDIDATE.bin",
            FailedV1BinSha256: "8020947d4ab5e7e4b0eac4bc6409ff3d6f11212b0e118bbf007d870812014a8e",
            FailedV1OutputDataSha256: "8d10aa62b134414aec80eec10d1fcede13bd55806ee13f6eb7c752f0fa1a9061",
            FailedV1OutputModelSha256: "72c3fb268f63952ad37707e33be4de4039d4bb73af42e25b52e618e7ec9a5820",
            CollisionWindingV2ProfileId: "unused-level-65-remote-blank-collision-winding-repair-sector216-static-clean-usa-v2",
            CollisionWindingV2BinRelativePath: "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-collision-winding-repair-v2/Unused-Level-65-Remote-Blank-Collision-Winding-Repair-v2-Sector216-RUNTIME-CANDIDATE.bin",
            CollisionWindingV2BinSha256: "23ce483a29187996827148d986fb12f483fc2caa82fc953106882568ea9254a6",
            CollisionWindingV2OutputDataSha256: "a21e3ace8f16e981dc1d75c4d2210e96ba2833200bd4e3054b2a1c0bb540ade9",
            CollisionWindingV2OutputModelSha256: "2ea39a0c19f51f16abfd3cae73d13df01191e16d70f31644ae8c14c977581b79",
            CollisionWindingV2RepairDiffManifestSha256: "0fae1b1035fa0bae3faf82e1b4d172a39c51d1cd5ce425a49321e3f01cf66e25",
            CollisionWindingV2ChangedByteCount: 4,
            CollisionWindingV2RangeCount: 2,
            CollisionWindingV2RawSectorLba: 54_581,
            CollisionWindingV2InverseVerified: true,
            FailedV1RetestForbidden: true,
            SupersededV2SectorSha256: "08f1de7187887ee4bc05bb0dbb0d9fd0c819ad015a39b6f1ae83a1e745ddc07a",
            EnvironmentHeaderRepairV3AfterSectorSha256: "7d71ebc9558c10896b83d1d8a79e6224cd6a48630b7a335d25b46935a572759e",
            EnvironmentHeaderRepairV3AfterEnvironmentSha256: "c4097bfdc54cf9a29864f5195e52c44151478d67e515fdb27f7c4c4f24d1ff06",
            EnvironmentHeaderRepairV3DiffManifestSha256: "fe85cd987e9d39c3ca10b944371c76fd7f7247e11db15e9eb6baed0d1925d875",
            EnvironmentHeaderRepairV3ChangedByteCount: 1,
            EnvironmentHeaderRepairV3RangeCount: 1,
            EnvironmentHeaderRepairV3RawSectorLba: 54_437,
            EnvironmentHeaderRepairV3BeforeByte: 9,
            EnvironmentHeaderRepairV3AfterByte: 8,
            EnvironmentHeaderRepairV3InverseVerified: true,
            SupersededV2RetestForbidden: true);
    }

    private static Id65V3RuntimeEvidenceProfile BuildExpectedRuntimeProfile() => new(
        ProfileId: "duckstation-id65-unattended-v1",
        PackageRelativePath: ProfilePackageRelativePath,
        AppRelativePath: PackagedAppRelativePath,
        DuckStationExecutableRelativePath: DuckStationExecutableRelativePath,
        ExternalHomeRelativePath: ExternalHomeRelativePath,
        ExternalHomeDataRelativePath: ExternalHomeDataRelativePath,
        HandoffReceiptRelativePath: HandoffReceiptRelativePath,
        DuckStationVersion: "0.1-11580-ge7f2f101c",
        DuckStationExecutableByteLength: 22_354_208,
        DuckStationExecutableSha256: "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645",
        HandoffReceiptFormat: "spyro-id65-unattended-external-home-handoff",
        HandoffReceiptFormatVersion: 1,
        HandoffReceiptByteLength: 73_908,
        HandoffReceiptSha256: "a748153bdd554187fb30ee79e75d25d36c606663bd4aee8444826b7612aa27d5",
        HandoffReceiptClosureSha256: "722a7ac322fa88749f17762185256281a4d6a930a290e35cdb955937fe57aead",
        AppContentManifestFormat: "spyro-id65-duckstation-app-content-manifest-v1",
        AppContentManifestEntryCount: 292,
        AppContentManifestSha256: "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e",
        OutputTreeManifestFormat: "spyro-id65-output-tree-manifest-v1",
        AppOutputTreeManifestEntryCount: 292,
        AppOutputTreeManifestSha256: "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7",
        HomeOutputTreeManifestEntryCount: 12,
        HomeOutputTreeManifestSha256: "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0",
        PackageEnvelopeFormat: "spyro-id65-package-envelope-manifest-v1",
        PackageEnvelopeEntryCount: 4,
        PackageEnvelopeSha256: "03e6f465647c04ee7d86add2550e1ba6818972233be419ab98290f05158ffff3",
        OutputXattrPolicyVersion: "apple-provenance-11-prefix-0102-v1",
        ExternalHomeSettingsRelativePath: "settings.ini",
        ExternalHomeSettingsByteLength: 2_143,
        ExternalHomeSettingsSha256: "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9",
        ExternalHomeBiosRelativePath: ExternalHomeBiosManifestPath,
        ExternalHomeBiosByteLength: 524_288,
        ExternalHomeBiosSha256: "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
        HostNormalSettingsSentinelId: "duckstation-normal-user-settings.ini",
        HostNormalSettingsByteLength: 10_058,
        HostNormalSettingsSha256: "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9",
        HostNormalSettingsMode: "0600",
        SafetyDirectories: ["cheats", "gamesettings", "memcards", "patches", "savestates"],
        AllowedVolatilePolicy: "playtime.dat,cache/**,screenshots/**,videos/**",
        TrustModel: "cooperative-non-hostile-same-uid",
        LaunchGate: "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
        PowerOffBinding: "Keyboard/F12",
        TogglePauseBinding: "Keyboard/Space",
        EmulationSpeed: 1,
        LoadDevicesFromSaveStates: false,
        RewindEnabled: false,
        RunaheadFrameCount: 0,
        SaveStateOnExit: false,
        StartPaused: false,
        FastForwardBoot: false,
        PatchFastBoot: false,
        LoadImagePatches: false,
        LoadImageToRam: false,
        CdReadSpeedup: 1,
        CdSeekSpeedup: 1,
        Card1Type: "None",
        Card2Type: "None",
        MultitapMode: "Disabled",
        Pad1Type: "DigitalController",
        OtherPadTypes: ["None", "None", "None", "None", "None", "None", "None"],
        PioDeviceType: "None",
        PioFlashWriteEnabled: false,
        PcDriveEnabled: false,
        PcDriveWritesEnabled: false,
        GdbServerEnabled: true,
        GdbServerPort: 2345,
        ExpectedCheatFileCount: 0,
        ExpectedGameSettingsFileCount: 0,
        ExpectedMemoryCardFileCount: 0,
        ExpectedSaveStateFileCount: 0,
        ExpectedPatchFileCount: 0,
        SaveLoadStateHotkeysPresent: false,
        TextureReplacementOrDumpEnabled: false,
        ProjectionSha256: ExpectedRuntimeProfileSha256,
        SettingsBeforeSha256: null,
        SettingsAfterSha256: null,
        NormalUserSettingsBeforeSha256: null,
        NormalUserSettingsAfterSha256: null,
        CheatFileCountBefore: null,
        CheatFileCountAfter: null,
        GameSettingsFileCountBefore: null,
        GameSettingsFileCountAfter: null,
        MemoryCardFileCountBefore: null,
        MemoryCardFileCountAfter: null,
        SaveStateFileCountBefore: null,
        SaveStateFileCountAfter: null,
        PatchFileCountBefore: null,
        PatchFileCountAfter: null,
        ChangedProfilePaths: null,
        SaveStateLoaded: null,
        SaveStateCreated: null,
        InGameSaveUsed: null,
        ActiveCheatCount: null);

    private static Id65V3RuntimeEvidenceScopeBoundary BuildScopeBoundary() => new(
        BlankLookingNotByteEmpty: true,
        InheritedScaffoldingRetained: true,
        PinnedRuntimeFilesAreCurrentReadbackOnly: true,
        CaptureJournalOsProcessProvenanceCaptured: true,
        CaptureJournalHashChainIsUnauthenticated: true,
        RawCaptureIsFinalEvidence: false,
        ProcessUseCryptographicallyProven: false,
        ExecutableTextVnodeObserved: true,
        OperatorPromptIntervalsContinuouslyObserved: false,
        SystemCaptureAndDiagnosticToolsCryptographicallyPinned: false,
        SameUidRenameRacesCryptographicallyExcluded: false,
        VisualMediaContainersStructurallyValidated: true,
        VisualMetadataDimensionsMachineVerified: false,
        VisualContentIsHumanAttestation: true,
        AudioContentMachineVerified: false,
        FarLowDetailPending: true,
        SaveRuntimePending: true,
        Core4RewardRuntimePending: true,
        EditorIntegrationAuthorized: false,
        CreateBinAuthorized: false,
        PublisherAuthorized: false,
        PromotionAuthorized: false,
        ReleaseAuthorized: false);

    private static Id65V3RuntimeEvidenceDerivedResult BuildDerivedResult(
        Id65V3RuntimeEvidenceState state,
        bool sourceValid = false,
        bool pinnedRuntimeFilesReadbackValid = false,
        bool runtimeProfileValid = false,
        bool artifactReadbackValid = false,
        bool discIdentityValid = false,
        bool telemetryValid = false,
        bool visualChecklistComplete = false,
        bool coldResetValid = false,
        bool retailControlsValid = false)
    {
        bool accepted = state == Id65V3RuntimeEvidenceState.FocusedRuntimePass && sourceValid &&
            pinnedRuntimeFilesReadbackValid && runtimeProfileValid && artifactReadbackValid &&
            discIdentityValid && telemetryValid &&
            visualChecklistComplete && coldResetValid && retailControlsValid;
        return new(
            state,
            sourceValid,
            pinnedRuntimeFilesReadbackValid,
            runtimeProfileValid,
            artifactReadbackValid,
            discIdentityValid,
            telemetryValid,
            visualChecklistComplete,
            coldResetValid,
            retailControlsValid,
            EnvironmentHeaderRepairV3RuntimeAccepted: accepted,
            RuntimeEvidenceAccepted: accepted,
            FarLowDetailAccepted: false,
            SaveRuntimeAccepted: false,
            Core4RewardRuntimeAccepted: false,
            Total200RuntimeAccepted: false,
            PublisherAuthorized: false,
            AppIntegrated: false,
            NormalCreateBinEnabled: false,
            PromotionAuthorized: false,
            ReleaseIntegrated: false,
            ProcessUseCryptographicallyProven: false);
    }

    private static FileIdentity ReadFileIdentity(string path, string context)
    {
        Id65V3RuntimeEvidenceFinalizer.StableFileRead read =
            Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
                path,
                MaximumPinnedFileBytes,
                keepBytes: false,
                context: context);
        return new(
            read.ByteLength,
            read.Sha256,
            (UnixFileMode)(read.Mode & 0x0FFF),
            read.Device,
            read.Inode,
            read.LinkCount);
    }

    private static string Hash(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string HashUtf8(string text) => Hash(StrictUtf8.GetBytes(text));

    private static string SerializeCanonicalValue<T>(T value)
    {
        JsonElement element = JsonSerializer.SerializeToElement(value, JsonOptions);
        StringBuilder builder = new();
        WriteJcsElement(builder, element);
        return builder.ToString();
    }

    private static void WriteJcsElement(StringBuilder builder, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                builder.Append('{');
                bool firstProperty = true;
                foreach (JsonProperty property in element.EnumerateObject()
                             .OrderBy(property => property.Name, StringComparer.Ordinal))
                {
                    if (!firstProperty)
                        builder.Append(',');
                    firstProperty = false;
                    WriteJcsString(builder, property.Name);
                    builder.Append(':');
                    WriteJcsElement(builder, property.Value);
                }
                builder.Append('}');
                break;
            case JsonValueKind.Array:
                builder.Append('[');
                bool firstItem = true;
                foreach (JsonElement item in element.EnumerateArray())
                {
                    if (!firstItem)
                        builder.Append(',');
                    firstItem = false;
                    WriteJcsElement(builder, item);
                }
                builder.Append(']');
                break;
            case JsonValueKind.String:
                WriteJcsString(builder, element.GetString() ?? string.Empty);
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out long signed) &&
                    signed is >= -MaximumJcsSafeInteger and <= MaximumJcsSafeInteger)
                {
                    builder.Append(signed.ToString(CultureInfo.InvariantCulture));
                }
                else if (element.TryGetUInt64(out ulong unsigned) &&
                         unsigned <= (ulong)MaximumJcsSafeInteger)
                {
                    builder.Append(unsigned.ToString(CultureInfo.InvariantCulture));
                }
                else
                {
                    throw new InvalidDataException(
                        "Runtime evidence canonical JSON permits only exact I-JSON safe integers.");
                }
                break;
            case JsonValueKind.True:
                builder.Append("true");
                break;
            case JsonValueKind.False:
                builder.Append("false");
                break;
            case JsonValueKind.Null:
                builder.Append("null");
                break;
            default:
                throw new InvalidDataException($"Unsupported JSON kind {element.ValueKind} in canonical payload.");
        }
    }

    private static void WriteJcsString(StringBuilder builder, string value)
    {
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                    throw new InvalidDataException("Runtime evidence JSON contains an unpaired high surrogate.");
                builder.Append(character).Append(value[++index]);
                continue;
            }
            if (char.IsLowSurrogate(character))
                throw new InvalidDataException("Runtime evidence JSON contains an unpaired low surrogate.");

            switch (character)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\b':
                    builder.Append("\\b");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\f':
                    builder.Append("\\f");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                default:
                    if (character <= 0x1F)
                    {
                        builder.Append("\\u")
                            .Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }
        builder.Append('"');
    }

    private static void ValidateIJsonString(string value)
    {
        try
        {
            _ = StrictUtf8.GetByteCount(value);
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException(
                "Runtime evidence JSON contains an unpaired UTF-16 surrogate.",
                exception);
        }
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        JsonSerializerOptions options = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = false,
            NumberHandling = JsonNumberHandling.Strict,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true,
            MaxDepth = 64
        };
        options.Converters.Add(new StrictIJsonStringConverter());
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.KebabCaseLower, allowIntegerValues: false));
        return options;
    }

    private sealed class StrictIJsonStringConverter : JsonConverter<string>
    {
        public override string? Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Runtime evidence string member has a non-string JSON token.");
            string? value = reader.GetString();
            if (value is not null)
                ValidateIJsonString(value);
            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            string value,
            JsonSerializerOptions options)
        {
            ValidateIJsonString(value);
            writer.WriteStringValue(value);
        }
    }

    private static string Lower(bool value) => value ? "true" : "false";

    private static string ToKebab(Id65V3RuntimeEvidenceState state) => state switch
    {
        Id65V3RuntimeEvidenceState.PendingNoRun => "pending-no-run",
        Id65V3RuntimeEvidenceState.PendingIncomplete => "pending-incomplete",
        Id65V3RuntimeEvidenceState.RuntimeRejected => "runtime-rejected",
        Id65V3RuntimeEvidenceState.Invalid => "invalid",
        Id65V3RuntimeEvidenceState.FocusedRuntimePass => "focused-runtime-pass",
        _ => throw new ArgumentOutOfRangeException(nameof(state))
    };

    private const string Id65LoadCode =
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down";
    private const string TownSquareLoadCode =
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Triangle";
    private const string GnastysLootLoadCode =
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Right";
    private const string SunnyFlightLoadCode =
        "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Down";

    private sealed record Evaluation(
        Id65V3RuntimeEvidenceState State,
        Id65V3RuntimeEvidenceDerivedResult DerivedResult);

    private sealed record FileIdentity(
        long ByteLength,
        string Sha256,
        UnixFileMode Mode,
        int Device,
        ulong Inode,
        ushort LinkCount);

    private sealed record PinnedEvidenceBytes(byte[] Bytes, string Sha256);

    private sealed record EvidenceInterval(
        string ArtifactId,
        string SessionId,
        DateTimeOffset Start,
        DateTimeOffset End,
        string Kind);
}
