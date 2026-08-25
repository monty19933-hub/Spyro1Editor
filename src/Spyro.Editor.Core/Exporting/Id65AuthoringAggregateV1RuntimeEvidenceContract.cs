using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Spyro.Editor.Core.Exporting;

internal enum Id65AuthoringAggregateV1RuntimeEvidenceState
{
    PendingNoRun,
    PendingIncomplete,
    RuntimeRejected,
    Invalid,
    FocusedRuntimePass
}

internal enum Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus
{
    NotObserved,
    Passed,
    Failed,
    Inconclusive
}

internal enum Id65AuthoringAggregateV1RuntimeEvidenceValidationMode
{
    StaticOnly,
    FullReadback
}

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity(
    string CandidateProfileId,
    string CandidateDirectoryRelativePath,
    string CandidateBinRelativePath,
    long CandidateBinByteLength,
    string CandidateBinSha256,
    string CandidateCueRelativePath,
    long CandidateCueByteLength,
    string CandidateCueSha256,
    string CandidateReceiptRelativePath,
    long CandidateReceiptByteLength,
    string CandidateReceiptSha256,
    string CandidateReceiptContentSha256,
    string AuthoringDocumentRelativePath,
    long AuthoringDocumentByteLength,
    string AuthoringDocumentEnvelopeSha256,
    string AuthoringDocumentCanonicalSha256,
    string AuthoringDocumentContentSha256,
    string LockedBaseImageSha256,
    string AggregatePlanSha256,
    string CompositeManifestSha256,
    string CompositeOutputRow80Sha256,
    string PhysicalDiffSha256,
    int ChangedRawSectorCount,
    long ChangedPhysicalImageBytes,
    string CandidateScusName,
    int CandidateScusLba,
    int CandidateScusByteLength,
    string CandidateScusSha256,
    bool RetailTownSquarePreserved,
    bool ProtectedComplementVerified,
    bool RuntimeCandidateAuthorized,
    bool RuntimeProven,
    bool CreateBinEnabled,
    bool ReleaseAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceProfile(
    string ProfileId,
    string PackageRelativePath,
    string AppRelativePath,
    string ExecutableRelativePath,
    string ExternalHomeRelativePath,
    string ExternalHomeDataRelativePath,
    string DuckStationVersion,
    long ExecutableByteLength,
    string ExecutableSha256,
    string HandoffReceiptRelativePath,
    string HandoffReceiptFormat,
    int HandoffReceiptFormatVersion,
    long HandoffReceiptByteLength,
    string HandoffReceiptSha256,
    string HandoffReceiptClosureSha256,
    int CandidateManifestEntryCount,
    string CandidateManifestSha256,
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
    string HostNormalSettingsAbsolutePath,
    string HostNormalSettingsSentinelId,
    long HostNormalSettingsByteLength,
    string HostNormalSettingsSha256,
    string HostNormalSettingsMode,
    string LaunchGateStatus,
    bool BuilderLaunchedDuckStation,
    bool SaveLoadStateHotkeysPresent,
    bool TextureReplacementOrDumpEnabled,
    string TrustModel);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceToolPin(
    string Role,
    string RelativePath,
    long? ByteLength,
    string Sha256,
    string Mode);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceCheck(
    string CheckId,
    string Requirement,
    Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus Status,
    string? SessionId,
    string[] EvidenceArtifactIds);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceArtifact(
    string ArtifactId,
    string Kind,
    string RepoRelativePath,
    long ByteLength,
    string Sha256,
    string CapturedAtUtc);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker(
    string BlockerId,
    string RequiredGate,
    string Reason);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary(
    string FocusedEvidenceRelativePath,
    string RuntimeAssetRelativePrefix,
    string CaptureStagingRelativePath,
    string CaptureCoordinatorAuthorityLockRelativePath,
    string AggregateChecklistSha256,
    string CaptureRuntimeProfileSha256,
    string CaptureSourceClosureSha256,
    bool SaveCapableProfilePinned,
    bool ProductionCaptureEnabled,
    Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] ProductionCaptureBlockers,
    string ProductionCaptureBlockersSha256,
    string ProductionCaptureBlockingCheckId,
    string ProductionCaptureBlockingReason,
    bool FrozenWorkflowCompatibleWithAggregateScope,
    string FrozenWorkflowSha256,
    bool CandidateOnly,
    bool RetailTownSquareControlRequired,
    bool RuntimeObserved,
    bool EditorIntegrated,
    bool CreateBinAuthorized,
    bool NormalCreateBinEnabled,
    bool ReleaseAuthorized,
    bool PromotionAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult(
    Id65AuthoringAggregateV1RuntimeEvidenceState State,
    bool SourceClosureValid,
    bool RuntimeProfileClosureValid,
    bool ToolPinsFinalized,
    bool RuntimeEvidencePinned,
    bool AllEventualChecksPassed,
    bool FullReadbackAccepted,
    bool RuntimeProven,
    bool CreateBinAuthorized,
    bool ReleaseAuthorized,
    bool Publishable);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceIntegrity(
    string Canonicalization,
    string SourceClosureSha256,
    string RuntimeProfileClosureSha256,
    string ToolSetClosureSha256,
    string ArtifactSetClosureSha256,
    string PayloadSha256,
    string ContractSha256);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidencePackage(
    string Format,
    int FormatVersion,
    string EvidenceId,
    string? RecordedAtUtc,
    Id65AuthoringAggregateV1RuntimeEvidenceState DeclaredState,
    Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity SourceIdentity,
    Id65AuthoringAggregateV1RuntimeEvidenceProfile RuntimeProfile,
    Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] ToolPins,
    Id65AuthoringAggregateV1RuntimeEvidenceArtifact[] ArtifactCatalog,
    Id65AuthoringAggregateV1RuntimeEvidenceCheck[] EventualChecks,
    Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult DerivedResult,
    Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary ScopeBoundary,
    Id65AuthoringAggregateV1RuntimeEvidenceIntegrity Integrity);

internal sealed record Id65AuthoringAggregateV1RuntimeEvidenceValidation(
    bool StructurallyValid,
    Id65AuthoringAggregateV1RuntimeEvidenceState State,
    Id65AuthoringAggregateV1RuntimeEvidencePackage? Package,
    string CanonicalJson,
    string[] Errors);

internal static partial class Id65AuthoringAggregateV1RuntimeEvidenceContract
{
    internal const string Format =
        "spyro-editor-id65-authoring-aggregate-v1-runtime-evidence";
    internal const int FormatVersion = 1;
    internal const string RequiredSmokeAssemblyName =
        "Spyro.Editor.Id65AuthoringAggregateV1RuntimeEvidenceContractSmoke";
    internal const string ExpectedProfileId = "duckstation-id65-authoring-aggregate-v1";
    internal const string FocusedRuntimeEvidenceRelativePath =
        "docs/runtime-evidence/unused-level-65-authoring-aggregate-v1-runtime-evidence.json";
    internal const string RuntimeAssetRelativePrefix =
        "docs/runtime-evidence/assets/unused-level-65-authoring-aggregate-v1/";
    internal const string CaptureStagingRelativePath =
        RuntimeAssetRelativePrefix + "capture-staging";
    internal static readonly string ExpectedFocusedRuntimeEvidenceSha256 = "PENDING";
    internal static readonly string ExpectedCaptureCoordinatorSha256 =
        "f0d0baf60f7578174b05ef499cd9f94378f67b85405a5491ec029d4e0f650af3";
    internal const string ExpectedAggregateChecklistSha256 =
        "874226d92bd9a86a61cc754938261e4374dfade0067748169f69a86a6d9b8d79";
    internal const string ExpectedCaptureRuntimeProfileSha256 =
        "9c9154924e0dfb7de57d2a4cbb9ced1fddf046a5bbf4fa53cf1b3e258252b71d";
    internal const string ExpectedCaptureSourceClosureSha256 =
        "2f1fbe145226c7c588fa5150ec65939a04e94fde30d14371a9858c21debb0571";
    internal const string ExpectedProductionCaptureBlockersSha256 =
        "b44799a97c8da063bd339fdc9e54b66d29e6a5f075bd95f7146c1311c08c3686";
    internal const string ExpectedFrozenWorkflowSha256 =
        "6ae9d047746dba64e0156de1ef547a4c8454654f8aec9f7cd32b0598d6955a69";
    internal const string ExpectedSourceClosureSha256 =
        "5f178ebd345424529d4c5d80f40bd7d72ecbf942b1c6fd5d5096c45125533985";
    internal const string ExpectedRuntimeProfileClosureSha256 =
        "42996f468b9c2f129203b24f400d7244ca361c05fd30e61288011211579996cb";
    internal const string ExpectedToolSetClosureSha256 =
        "bc8bccaa102dce9f67ee92a229b62407ff365066c55dfcacbef0b60559078bb3";

    private const string Canonicalization = "rfc8785-jcs-utf8-v1";
    private const string EvidenceId =
        "unused-level-65-authoring-aggregate-v1-runtime-evidence-pending";
    private const string CandidateDirectory =
        "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/v1/" +
        "materialized-candidates/aggregate-4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21-" +
        "receipt-d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f";
    private const string CandidateStem = "unusedlevel65blank-authoring-aggregate-candidate-v1";
    private const string AuthoringDocumentRelativePath =
        "_local/id65-authoring-aggregate-workspace/_research/unusedlevel65blank/v1/" +
        "authored-edits/unusedlevel65blank-authoring-document-v1.json";
    private const string ProfilePackageRelativePath =
        "_local/v5-stone-hill-level-replacement/duckstation-id65-authoring-aggregate-v1";
    private const string AppRelativePath =
        ProfilePackageRelativePath + "/DuckStation-ID65-Unattended-v1.app";
    private const string ExternalHomeRelativePath = ProfilePackageRelativePath + "/home";
    private const string ExternalHomeDataRelativePath =
        ExternalHomeRelativePath + "/Library/Application Support/DuckStation";
    private const string HostNormalSettingsAbsolutePath =
        "/Users/john/Library/Application Support/DuckStation/settings.ini";
    private const long MaximumJcsSafeInteger = 9_007_199_254_740_991;
    private const int MaximumEvidenceBytes = 16 * 1024 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    internal static Id65AuthoringAggregateV1RuntimeEvidencePackage CreatePending()
    {
        Id65AuthoringAggregateV1RuntimeEvidencePackage package = new(
            Format,
            FormatVersion,
            EvidenceId,
            RecordedAtUtc: null,
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
            BuildExpectedSourceIdentity(),
            BuildExpectedRuntimeProfile(),
            BuildExpectedToolPins(),
            ArtifactCatalog: [],
            BuildExpectedChecks(),
            BuildExpectedDerivedResult(),
            BuildExpectedScopeBoundary(),
            new(Canonicalization, string.Empty, string.Empty, string.Empty, string.Empty,
                string.Empty, string.Empty));
        return WithRecomputedIntegrity(package);
    }

    internal static string SerializeCanonical(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        SerializeCanonicalValue(package);

    internal static byte[] SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        StrictUtf8.GetBytes(SerializeCanonical(package));

    internal static Id65AuthoringAggregateV1RuntimeEvidenceValidation ValidateCanonicalJson(
        ReadOnlySpan<byte> utf8Json,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode mode =
            Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.StaticOnly)
    {
        List<string> errors = [];
        Id65AuthoringAggregateV1RuntimeEvidencePackage? package = null;
        string canonical = string.Empty;
        try
        {
            if (utf8Json.Length == 0 || utf8Json.Length > MaximumEvidenceBytes)
                throw new InvalidDataException("Aggregate runtime evidence JSON has an invalid byte length.");
            if (utf8Json.Length >= 3 && utf8Json[0] == 0xEF && utf8Json[1] == 0xBB &&
                utf8Json[2] == 0xBF)
            {
                throw new InvalidDataException("Aggregate runtime evidence JSON must not contain a UTF-8 BOM.");
            }
            _ = StrictUtf8.GetString(utf8Json);

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

            package = JsonSerializer.Deserialize<Id65AuthoringAggregateV1RuntimeEvidencePackage>(
                utf8Json,
                JsonOptions) ?? throw new InvalidDataException(
                    "Aggregate runtime evidence JSON deserialized to null.");
            canonical = SerializeCanonical(package);
            if (!utf8Json.SequenceEqual(StrictUtf8.GetBytes(canonical)))
            {
                throw new InvalidDataException(
                    "Aggregate runtime evidence JSON is not the exact canonical UTF-8 encoding.");
            }

            ValidateStaticPackage(package, mode);
            ValidateIntegrity(package);
            return new(true, package.DeclaredState, package, canonical, []);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or
                                         DecoderFallbackException or EncoderFallbackException or
                                         ArgumentException or OverflowException)
        {
            if (errors.Count == 0 || !errors.Contains(exception.Message, StringComparer.Ordinal))
                errors.Add(exception.Message);
            return new(false, Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid, package,
                canonical, errors.ToArray());
        }
    }

    internal static void RequireFocusedRuntimePass(
        Id65AuthoringAggregateV1RuntimeEvidenceValidation validation)
    {
        if (ExpectedCaptureCoordinatorSha256 == "PENDING" ||
            ExpectedFocusedRuntimeEvidenceSha256 == "PENDING")
        {
            throw new InvalidOperationException(
                "The aggregate V1 capture coordinator or focused runtime evidence is PENDING; " +
                "no runtime-pass capability can be issued.");
        }
        if (!validation.StructurallyValid ||
            validation.State != Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass)
        {
            throw new InvalidDataException("Aggregate V1 evidence is not a focused runtime pass.");
        }
        throw new InvalidOperationException(
            "This pending/static contract slice cannot issue a runtime-pass capability.");
    }

    internal static Id65AuthoringAggregateV1RuntimeEvidencePackage WithRecomputedIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        package with { Integrity = ComputeIntegrity(package) };

    internal static string RecomputeSourceClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity source) =>
        HashUtf8(SerializeCanonicalValue(source));

    internal static string RecomputeRuntimeProfileClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceProfile profile) =>
        HashUtf8(SerializeCanonicalValue(profile));

    internal static string RecomputeToolSetClosureSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] tools) =>
        HashUtf8(SerializeCanonicalValue(tools));

    internal static string RecomputeProductionCaptureBlockersSha256(
        Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] blockers) =>
        HashUtf8(SerializeCanonicalValue(blockers));

    private static void ValidateStaticPackage(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode mode)
    {
        if (package.SourceIdentity is null || package.RuntimeProfile is null ||
            package.ToolPins is null || package.ArtifactCatalog is null ||
            package.EventualChecks is null || package.DerivedResult is null ||
            package.ScopeBoundary is null || package.Integrity is null ||
            package.ToolPins.Any(item => item is null) ||
            package.ArtifactCatalog.Any(item => item is null) ||
            package.EventualChecks.Any(item => item is null))
        {
            throw new InvalidDataException(
                "Aggregate runtime evidence root graph contains a missing object or array member.");
        }
        if (package.Format != Format || package.FormatVersion != FormatVersion ||
            package.EvidenceId != EvidenceId)
        {
            throw new InvalidDataException("Aggregate runtime evidence format identity changed.");
        }
        if (package.RecordedAtUtc is not null ||
            package.DeclaredState != Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun)
        {
            throw new InvalidDataException(
                "This aggregate V1 contract slice accepts only the canonical pending no-run state.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity source =
            BuildExpectedSourceIdentity();
        if (SerializeCanonicalValue(package.SourceIdentity) != SerializeCanonicalValue(source) ||
            RecomputeSourceClosureSha256(package.SourceIdentity) != ExpectedSourceClosureSha256)
        {
            throw new InvalidDataException(
                "Pinned aggregate candidate, document, receipt, or source closure changed.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceProfile profile = BuildExpectedRuntimeProfile();
        if (SerializeCanonicalValue(package.RuntimeProfile) != SerializeCanonicalValue(profile) ||
            RecomputeRuntimeProfileClosureSha256(package.RuntimeProfile) !=
                ExpectedRuntimeProfileClosureSha256)
        {
            throw new InvalidDataException("Pinned aggregate runtime profile closure changed.");
        }

        Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] tools = BuildExpectedToolPins();
        if (SerializeCanonicalValue(package.ToolPins) != SerializeCanonicalValue(tools) ||
            RecomputeToolSetClosureSha256(package.ToolPins) != ExpectedToolSetClosureSha256)
        {
            throw new InvalidDataException("Pinned aggregate runtime tool closure changed.");
        }
        if (package.ArtifactCatalog.Length != 0 ||
            SerializeCanonicalValue(package.EventualChecks) !=
                SerializeCanonicalValue(BuildExpectedChecks()) ||
            package.EventualChecks.Any(check =>
                check.Status != Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.NotObserved ||
                check.SessionId is not null || check.EvidenceArtifactIds.Length != 0))
        {
            throw new InvalidDataException(
                "Runtime observations are not accepted while the capture/evidence pins are PENDING.");
        }
        if (SerializeCanonicalValue(package.ScopeBoundary) !=
            SerializeCanonicalValue(BuildExpectedScopeBoundary()) ||
            SerializeCanonicalValue(package.DerivedResult) !=
            SerializeCanonicalValue(BuildExpectedDerivedResult()))
        {
            throw new InvalidDataException("Aggregate pending scope or derived result changed.");
        }
        if (mode == Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.FullReadback &&
            package.DerivedResult.FullReadbackAccepted)
        {
            throw new InvalidDataException(
                "The pending aggregate V1 contract cannot accept a full-runtime readback.");
        }
    }

    private static void ValidateIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package)
    {
        Id65AuthoringAggregateV1RuntimeEvidenceIntegrity expected = ComputeIntegrity(package);
        if (SerializeCanonicalValue(package.Integrity) != SerializeCanonicalValue(expected))
            throw new InvalidDataException("Aggregate runtime evidence integrity closure changed.");
    }

    private static Id65AuthoringAggregateV1RuntimeEvidenceIntegrity ComputeIntegrity(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package)
    {
        string source = RecomputeSourceClosureSha256(package.SourceIdentity);
        string profile = RecomputeRuntimeProfileClosureSha256(package.RuntimeProfile);
        string tools = RecomputeToolSetClosureSha256(package.ToolPins);
        string artifacts = HashUtf8(SerializeCanonicalValue(package.ArtifactCatalog));
        string payload = HashUtf8(SerializeCanonicalPayload(package));
        string contract = HashUtf8(
            "id65-authoring-aggregate-v1-runtime-evidence-contract-v1\n" +
            $"sourceClosureSha256={source}\n" +
            $"runtimeProfileClosureSha256={profile}\n" +
            $"toolSetClosureSha256={tools}\n" +
            $"artifactSetClosureSha256={artifacts}\n" +
            $"payloadSha256={payload}\n" +
            $"state={ToKebab(package.DeclaredState)}\n");
        return new(Canonicalization, source, profile, tools, artifacts, payload, contract);
    }

    private static string SerializeCanonicalPayload(
        Id65AuthoringAggregateV1RuntimeEvidencePackage package) =>
        SerializeCanonicalValue(new
        {
            package.Format,
            package.FormatVersion,
            package.EvidenceId,
            package.RecordedAtUtc,
            package.DeclaredState,
            package.SourceIdentity,
            package.RuntimeProfile,
            package.ToolPins,
            package.ArtifactCatalog,
            package.EventualChecks,
            package.DerivedResult,
            package.ScopeBoundary
        });

    private static Id65AuthoringAggregateV1RuntimeEvidenceSourceIdentity
        BuildExpectedSourceIdentity() => new(
            "id65-authoring-editor-aggregate-candidate-darwin-anchored-workspace-v1",
            CandidateDirectory,
            CandidateDirectory + "/" + CandidateStem + ".bin",
            661_547_040,
            "083dd5b7e258b59010e87c3262274e4da9cb86b1a6a15e7730a8b5eeac001647",
            CandidateDirectory + "/" + CandidateStem + ".cue",
            114,
            "91c12af236642fc8ad601995cea86d22c414eb1b692a4957cc3f9fa3aedd6de8",
            CandidateDirectory + "/" + CandidateStem + ".receipt.json",
            3_064,
            "9f11a3811a54ef4932c5cb4d184877fe957f06eb003680c739d2478c6e7ed03b",
            "d6c14861cde023e9e100fe2828227da8252c9de37c36e84d33ba4bfbd3b0516f",
            AuthoringDocumentRelativePath,
            3_931,
            "5669f5e29010009fa7a698f4eed213dbd7163bd3483ec37beba7732bda321b70",
            "8f27bfc3f97238402c0b7dd59a081335ae19df1acfaefa9575d9ca7110dce808",
            "9d853ccfcf18627a46dc730844efc9eeeda28ee9a0d53c70ae3b7144652b4c3b",
            "9e42b43bd1341b40915748432d1b2dc760e22a81c0a320ec09ae6a71ca2efcd8",
            "4955c54b3eb7ea5c1871205e559f5d5dda0766613e55aa0de988b92e896d5c21",
            "62635a61a79a59a59d052f50c3f719b56ea1bcdecf09ae91030063be410cfa34",
            "dad25853f92e4f8300036edd85dae374672d265ba17c7b2682c6b524661b75f3",
            "443bfdeb7727b2a1877eef60027cc1fdb9b1b1224cb1165c4a9e26b33b480211",
            558,
            1_075_276,
            "SCUS_942.28",
            55_382,
            417_792,
            "fa5fc7981188b78fa7d7b78facca64c1f79dadb107515e9146ad178ade39d442",
            RetailTownSquarePreserved: true,
            ProtectedComplementVerified: true,
            RuntimeCandidateAuthorized: false,
            RuntimeProven: false,
            CreateBinEnabled: false,
            ReleaseAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceProfile
        BuildExpectedRuntimeProfile() => new(
            ProfileId: ExpectedProfileId,
            PackageRelativePath: ProfilePackageRelativePath,
            AppRelativePath: AppRelativePath,
            ExecutableRelativePath: AppRelativePath + "/Contents/MacOS/DuckStation",
            ExternalHomeRelativePath: ExternalHomeRelativePath,
            ExternalHomeDataRelativePath: ExternalHomeDataRelativePath,
            DuckStationVersion: "0.1-11580-ge7f2f101c",
            ExecutableByteLength: 22_354_208,
            ExecutableSha256:
                "14c22c6e1f4bbff13c00c24f17424ef8cf7645ee9917f7d65b17cd3fe1dd1645",
            HandoffReceiptRelativePath: ProfilePackageRelativePath + "/handoff-receipt.json",
            HandoffReceiptFormat: "spyro-id65-unattended-external-home-handoff",
            HandoffReceiptFormatVersion: 1,
            HandoffReceiptByteLength: 74_510,
            HandoffReceiptSha256:
                "c35c4c2b74f3481206737fb8f310bb5478a6f00df0d53655d9e4ac14230a1fe2",
            HandoffReceiptClosureSha256:
                "1f826b36b14ee330330cf574e8080a5f744d7b8c5da6e75475d593ec81b19c7f",
            CandidateManifestEntryCount: 2,
            CandidateManifestSha256:
                "44f74dcefab535670d4abe0e1b1bcec35b832dec421d9caea8c6c132b860681d",
            AppContentManifestFormat: "spyro-id65-duckstation-app-content-manifest-v1",
            AppContentManifestEntryCount: 292,
            AppContentManifestSha256:
                "95a8142e561d6b37f228728abc8dbf034c774887213eb59bc14518d56c2d222e",
            OutputTreeManifestFormat: "spyro-id65-output-tree-manifest-v1",
            AppOutputTreeManifestEntryCount: 292,
            AppOutputTreeManifestSha256:
                "08d0a74450b709ab7e702182d64db55f6bc3395728181b052fec307c4d320ba7",
            HomeOutputTreeManifestEntryCount: 12,
            HomeOutputTreeManifestSha256:
                "b00119fd7647647c81c7571d2507dbbce2075bd10cdcd8af8db8ae1e006fc2b0",
            PackageEnvelopeFormat: "spyro-id65-package-envelope-manifest-v1",
            PackageEnvelopeEntryCount: 4,
            PackageEnvelopeSha256:
                "31f24a6d8ea38fa19abeae54392fa96e2494b5cfb0a5621d2cc29771a617f866",
            OutputXattrPolicyVersion: "apple-provenance-11-prefix-0102-v1",
            ExternalHomeSettingsRelativePath: ExternalHomeDataRelativePath + "/settings.ini",
            ExternalHomeSettingsByteLength: 2_143,
            ExternalHomeSettingsSha256:
                "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9",
            ExternalHomeBiosRelativePath: ExternalHomeDataRelativePath +
                "/bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 (1995-12-04)(Sony)(US).bin",
            ExternalHomeBiosByteLength: 524_288,
            ExternalHomeBiosSha256:
                "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
            HostNormalSettingsAbsolutePath: HostNormalSettingsAbsolutePath,
            HostNormalSettingsSentinelId: "duckstation-normal-user-settings.ini",
            HostNormalSettingsByteLength: 10_058,
            HostNormalSettingsSha256:
                "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9",
            HostNormalSettingsMode: "0600",
            LaunchGateStatus: "EXTERNAL_RUNTIME_LAUNCH_REQUIRED",
            BuilderLaunchedDuckStation: false,
            SaveLoadStateHotkeysPresent: false,
            TextureReplacementOrDumpEnabled: false,
            TrustModel: "cooperative-non-hostile-same-uid");

    private static Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] BuildExpectedToolPins() =>
    [
        new(
            "profile-builder",
            "tools/Build-SpyroId65AuthoringAggregateV1Profile.py",
            20_966,
            "5e344a097b1cd79c50cdd5d07b0a3c818b457f35f33b46d1677fa55487aa85a2",
            "0644"),
        new(
            "profile-adapter-test",
            "tools/Test-SpyroId65AuthoringAggregateV1Profile.py",
            59_073,
            "4f41a1828f553f3bc6b223ffadf27350c0bd9390065abd9e3b2df3bf6f4d9d2a",
            "0644"),
        new(
            "profile-verifier",
            "tools/Verify-SpyroId65AuthoringAggregateV1Profile.py",
            35_753,
            "ba1b461193c7a67fd8522c72fd9141e725ec3063791d44d2a82d96e0a01a344c",
            "0644"),
        new(
            "runtime-watcher",
            "tools/Watch-SpyroId65RuntimeState.py",
            21_426,
            "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677",
            "0644"),
        new(
            "frozen-capture-core",
            "tools/Capture-SpyroId65V3RuntimeSession.py",
            473_144,
            "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec",
            "0755"),
        new(
            "capture-coordinator",
            "tools/Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py",
            78_915,
            ExpectedCaptureCoordinatorSha256,
            "0644"),
        new(
            "capture-deep-test",
            "tools/Test-Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py",
            28_214,
            "4ddee51100eb4b9108e9aa8d3a68127ec0199c7ee377c4c50c99a732cdc9161a",
            "0644")
    ];

    private static Id65AuthoringAggregateV1RuntimeEvidenceCheck[] BuildExpectedChecks() =>
    [
        PendingCheck("candidate.safe-load-and-spawn",
            "A fresh external-home launch safely loads ID65 and spawns Spyro on the authored pad."),
        PendingCheck("candidate.authored-terrain-visible",
            "The authored terrain tile is visible with the intended geometry."),
        PendingCheck("candidate.authored-collision-playable",
            "The authored collision supports stable traversal without fall-through."),
        PendingCheck("candidate.authored-occlusion-correct",
            "The authored occlusion group neither hides the pad nor reveals invalid geometry."),
        PendingCheck("candidate.private-texture-swap-visible",
            "The private destination texture swap is visibly applied to the authored face."),
        PendingCheck("candidate.grass-zero-egg-placement-present",
            "The row-replaced grass placement at true index 88 is present."),
        PendingCheck("candidate.grass-authored-placement-present",
            "The appended grass placement at true index 107 is present at its authored support."),
        PendingCheck("candidate.zero-egg-preserved",
            "Both grass placements preserve the intended zero-egg state."),
        PendingCheck("candidate.camera-360-stable",
            "A complete camera orbit remains stable around the authored area."),
        PendingCheck("candidate.walk-stable",
            "Walking the authored area remains stable in both directions."),
        PendingCheck("candidate.jump-land-stable",
            "Jumping and landing across the authored area remains stable."),
        PendingCheck("candidate.fall-respawn-stable",
            "An authorized edge fall respawns safely at the ID65 spawn without a crash or softlock."),
        PendingCheck("candidate.music-long-play-stable",
            "The authored music plays correctly through a sustained long-play observation."),
        PendingCheck("candidate.treasure-total-200",
            "The in-level and pause totals report exactly 200 treasure."),
        PendingCheck("candidate.exit-destination-65",
            "The level exit retains destination ID 65 and returns through the intended route."),
        PendingCheck("candidate.save-persistence",
            "An in-game save survives a clean process restart without save-state use."),
        PendingCheck("retail.town-square-preserved",
            "A fresh retail Town Square control run preserves terrain, collision, Mobys, totals, exit, music, and save behavior.")
    ];

    private static Id65AuthoringAggregateV1RuntimeEvidenceCheck PendingCheck(
        string id,
        string requirement) => new(
            id,
            requirement,
            Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.NotObserved,
            SessionId: null,
            EvidenceArtifactIds: []);

    private static Id65AuthoringAggregateV1RuntimeEvidenceDerivedResult
        BuildExpectedDerivedResult() => new(
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
            SourceClosureValid: true,
            RuntimeProfileClosureValid: true,
            ToolPinsFinalized: true,
            RuntimeEvidencePinned: false,
            AllEventualChecksPassed: false,
            FullReadbackAccepted: false,
            RuntimeProven: false,
            CreateBinAuthorized: false,
            ReleaseAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceScopeBoundary
        BuildExpectedScopeBoundary() => new(
            FocusedRuntimeEvidenceRelativePath,
            RuntimeAssetRelativePrefix,
            CaptureStagingRelativePath,
            "docs/runtime-evidence/assets/" +
                "unused-level-65-remote-blank-environment-header-repair-v3/" +
                ".capture-coordinator-authority.lock",
            ExpectedAggregateChecklistSha256,
            ExpectedCaptureRuntimeProfileSha256,
            ExpectedCaptureSourceClosureSha256,
            SaveCapableProfilePinned: false,
            ProductionCaptureEnabled: false,
            ProductionCaptureBlockers: BuildExpectedProductionCaptureBlockers(),
            ProductionCaptureBlockersSha256: ExpectedProductionCaptureBlockersSha256,
            ProductionCaptureBlockingCheckId: "candidate.save-persistence",
            ProductionCaptureBlockingReason:
                "the pinned aggregate profile disables both memory-card slots, so an ordinary " +
                "in-game save cannot survive a clean process restart without save-state use",
            FrozenWorkflowCompatibleWithAggregateScope: false,
            FrozenWorkflowSha256: ExpectedFrozenWorkflowSha256,
            CandidateOnly: true,
            RetailTownSquareControlRequired: true,
            RuntimeObserved: false,
            EditorIntegrated: false,
            CreateBinAuthorized: false,
            NormalCreateBinEnabled: false,
            ReleaseAuthorized: false,
            PromotionAuthorized: false,
            Publishable: false);

    private static Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[]
        BuildExpectedProductionCaptureBlockers() =>
    [
        new(
            "profile.no-memory-card-save-persistence",
            "candidate.save-persistence",
            "the pinned aggregate profile disables both memory-card slots, so an ordinary " +
                "in-game save cannot survive a clean process restart without save-state use"),
        new(
            "workflow.frozen-v3-seven-session-lone-pad-incompatible",
            "aggregate.exact-17-check-town-square-workflow",
            "the frozen capture engine still mandates seven sessions, the legacy lone-pad " +
                "visibility and triangular E0/E1/E2 workflow, and Gnasty's Loot and Sunny " +
                "Flight controls; that workflow is not the exact 17-check aggregate and " +
                "Town Square-only acceptance scope")
    ];

    private static void RejectDuplicateProperties(
        JsonElement element,
        string path,
        List<string> errors)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    errors.Add($"Duplicate JSON property at {path}.{property.Name}.");
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
                             .OrderBy(item => item.Name, StringComparer.Ordinal))
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
                        "Aggregate runtime evidence permits only exact I-JSON safe integers.");
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
                throw new InvalidDataException(
                    $"Unsupported JSON kind {element.ValueKind} in aggregate evidence.");
        }
    }

    private static void WriteJcsString(StringBuilder builder, string value)
    {
        ValidateIJsonString(value);
        builder.Append('"');
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (char.IsHighSurrogate(character))
            {
                builder.Append(character).Append(value[++index]);
                continue;
            }
            switch (character)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\b': builder.Append("\\b"); break;
                case '\t': builder.Append("\\t"); break;
                case '\n': builder.Append("\\n"); break;
                case '\f': builder.Append("\\f"); break;
                case '\r': builder.Append("\\r"); break;
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
                "Aggregate runtime evidence contains an unpaired UTF-16 surrogate.", exception);
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
        options.Converters.Add(new JsonStringEnumConverter(
            JsonNamingPolicy.KebabCaseLower,
            allowIntegerValues: false));
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
                throw new JsonException("Aggregate evidence string member has a non-string token.");
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

    private static string HashUtf8(string value) =>
        Convert.ToHexString(SHA256.HashData(StrictUtf8.GetBytes(value))).ToLowerInvariant();

    private static string ToKebab(Id65AuthoringAggregateV1RuntimeEvidenceState state) =>
        state switch
        {
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun => "pending-no-run",
            Id65AuthoringAggregateV1RuntimeEvidenceState.PendingIncomplete => "pending-incomplete",
            Id65AuthoringAggregateV1RuntimeEvidenceState.RuntimeRejected => "runtime-rejected",
            Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid => "invalid",
            Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass =>
                "focused-runtime-pass",
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        };
}
