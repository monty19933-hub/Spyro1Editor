using System.Text;
using Spyro.Editor.Core.Exporting;

Id65AuthoringAggregateV1RuntimeEvidencePackage pending =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.CreatePending();

if (args.Contains("--print-closures", StringComparer.Ordinal))
{
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeSourceClosureSha256(
            pending.SourceIdentity));
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeRuntimeProfileClosureSha256(
            pending.RuntimeProfile));
    Console.WriteLine(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.RecomputeToolSetClosureSha256(
            pending.ToolPins));
    return;
}

byte[] canonical =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(pending);
Id65AuthoringAggregateV1RuntimeEvidenceValidation validation =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(canonical);
Require(validation.StructurallyValid, "canonical pending evidence must validate");
Require(validation.State == Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
    "canonical pending evidence must stay pending-no-run");
Require(validation.Package is not null, "canonical pending evidence must deserialize");
Require(validation.CanonicalJson == Encoding.UTF8.GetString(canonical),
    "canonical validator must return exact canonical JSON");

Id65AuthoringAggregateV1RuntimeEvidenceValidation fullReadback =
    Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(
        canonical,
        Id65AuthoringAggregateV1RuntimeEvidenceValidationMode.FullReadback);
Require(fullReadback.StructurallyValid, "pending package must remain statically valid in full mode");
Require(fullReadback.State == Id65AuthoringAggregateV1RuntimeEvidenceState.PendingNoRun,
    "full mode must not promote pending evidence");
Require(fullReadback.Package!.DerivedResult is
    {
        FullReadbackAccepted: false,
        RuntimeProven: false,
        CreateBinAuthorized: false,
        ReleaseAuthorized: false,
        Publishable: false
    }, "pending derived gates must stay false");
Require(pending.SourceIdentity is
    {
        RuntimeCandidateAuthorized: false,
        RuntimeProven: false,
        CreateBinEnabled: false,
        ReleaseAuthorized: false,
        Publishable: false
    }, "pending source authorization gates must stay false");
Require(pending.RuntimeProfile is
    {
        ProfileId: "duckstation-id65-authoring-aggregate-v1",
        ExternalHomeBiosByteLength: 524_288,
        ExternalHomeBiosSha256:
            "71af94d1e47a68c11e8fdb9f8368040601514a42a5a399cda48c7d3bff1e99d3",
        HostNormalSettingsSentinelId: "duckstation-normal-user-settings.ini",
        HostNormalSettingsByteLength: 10_058,
        HostNormalSettingsSha256:
            "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9",
        HostNormalSettingsMode: "0600",
        BuilderLaunchedDuckStation: false,
        SaveLoadStateHotkeysPresent: false,
        TextureReplacementOrDumpEnabled: false
    }, "runtime profile identity, isolation sentinels, and launch gates changed");
Require(pending.RuntimeProfile.HandoffReceiptFormat ==
        "spyro-id65-unattended-external-home-handoff" &&
    pending.RuntimeProfile.HandoffReceiptFormatVersion == 1 &&
    pending.RuntimeProfile.AppContentManifestFormat ==
        "spyro-id65-duckstation-app-content-manifest-v1" &&
    pending.RuntimeProfile.OutputTreeManifestFormat ==
        "spyro-id65-output-tree-manifest-v1" &&
    pending.RuntimeProfile.PackageEnvelopeFormat ==
        "spyro-id65-package-envelope-manifest-v1" &&
    pending.RuntimeProfile.OutputXattrPolicyVersion ==
        "apple-provenance-11-prefix-0102-v1",
    "runtime profile manifest formats or xattr policy changed");
Require(pending.ScopeBoundary is
    {
        AggregateChecklistSha256:
            "874226d92bd9a86a61cc754938261e4374dfade0067748169f69a86a6d9b8d79",
        CaptureRuntimeProfileSha256:
            "9c9154924e0dfb7de57d2a4cbb9ced1fddf046a5bbf4fa53cf1b3e258252b71d",
        CaptureSourceClosureSha256:
            "2f1fbe145226c7c588fa5150ec65939a04e94fde30d14371a9858c21debb0571",
        SaveCapableProfilePinned: false,
        ProductionCaptureEnabled: false,
        ProductionCaptureBlockingCheckId: "candidate.save-persistence",
        ProductionCaptureBlockersSha256:
            "b44799a97c8da063bd339fdc9e54b66d29e6a5f075bd95f7146c1311c08c3686",
        FrozenWorkflowCompatibleWithAggregateScope: false,
        FrozenWorkflowSha256:
            "6ae9d047746dba64e0156de1ef547a4c8454654f8aec9f7cd32b0598d6955a69",
        RuntimeObserved: false,
        EditorIntegrated: false,
        CreateBinAuthorized: false,
        NormalCreateBinEnabled: false,
        ReleaseAuthorized: false,
        PromotionAuthorized: false,
        Publishable: false
    }, "pending scope authorization gates must stay false");
Require(pending.ScopeBoundary.ProductionCaptureBlockingReason ==
    "the pinned aggregate profile disables both memory-card slots, so an ordinary " +
    "in-game save cannot survive a clean process restart without save-state use",
    "production capture blocker changed");
Require(pending.ScopeBoundary.ProductionCaptureBlockers is
    [
        {
            BlockerId: "profile.no-memory-card-save-persistence",
            RequiredGate: "candidate.save-persistence"
        },
        {
            BlockerId: "workflow.frozen-v3-seven-session-lone-pad-incompatible",
            RequiredGate: "aggregate.exact-17-check-town-square-workflow"
        }
    ], "the complete two-blocker production gate changed");
Require(Id65AuthoringAggregateV1RuntimeEvidenceContract
        .RecomputeProductionCaptureBlockersSha256(
            pending.ScopeBoundary.ProductionCaptureBlockers) ==
    Id65AuthoringAggregateV1RuntimeEvidenceContract
        .ExpectedProductionCaptureBlockersSha256,
    "production capture blocker closure changed");

RequireThrows<InvalidOperationException>(() =>
    Id65AuthoringAggregateV1RuntimeEvidenceContract.RequireFocusedRuntimePass(fullReadback),
    "PENDING capture/evidence pins must forbid a runtime-pass capability");

string json = Encoding.UTF8.GetString(canonical);
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "{",
        "{\"format\":\"spyro-editor-id65-authoring-aggregate-v1-runtime-evidence\",",
        StringComparison.Ordinal)),
    "duplicate properties must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "\"formatVersion\":1",
        "\"formatVersion\":NaN",
        StringComparison.Ordinal)),
    "non-finite numbers must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(json.Replace(
        "\"formatVersion\":1",
        "\"formatVersion\":1,\"unknownSchemaMember\":0",
        StringComparison.Ordinal)),
    "unknown schema members must be rejected");
RequireRejected(
    Encoding.UTF8.GetBytes(" \n" + json),
    "non-canonical whitespace must be rejected");
RequireRejected(
    [0x7B, 0x22, 0x78, 0x22, 0x3A, 0x22, 0xC3, 0x28, 0x22, 0x7D],
    "invalid UTF-8 must be rejected");
RequireRejected(
    [0xEF, 0xBB, 0xBF, .. canonical],
    "a UTF-8 BOM must be rejected");

Id65AuthoringAggregateV1RuntimeEvidencePackage sourceTampered = pending with
{
    SourceIdentity = pending.SourceIdentity with { ChangedRawSectorCount = 559 }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(sourceTampered)),
    "source pin and closure tampering must be rejected even after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage profileTampered = pending with
{
    RuntimeProfile = pending.RuntimeProfile with
    {
        HostNormalSettingsSha256 = new string('1', 64)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(profileTampered)),
    "runtime profile pin and closure tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidenceToolPin[] toolTampered = pending.ToolPins.ToArray();
toolTampered[0] = toolTampered[0] with { ByteLength = toolTampered[0].ByteLength + 1 };
Id65AuthoringAggregateV1RuntimeEvidencePackage toolPackageTampered = pending with
{
    ToolPins = toolTampered
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            toolPackageTampered)),
    "tool pin and closure tampering must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage derivedGateTampered = pending with
{
    DerivedResult = pending.DerivedResult with { CreateBinAuthorized = true }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            derivedGateTampered)),
    "a forged pending derived authorization must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage scopeGateTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        SaveCapableProfilePinned = true,
        ProductionCaptureEnabled = true,
        PromotionAuthorized = true
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            scopeGateTampered)),
    "a forged pending scope authorization must be rejected after integrity recomputation");

Id65AuthoringAggregateV1RuntimeEvidenceProductionBlocker[] blockerTampered =
    pending.ScopeBoundary.ProductionCaptureBlockers.ToArray();
blockerTampered[1] = blockerTampered[1] with { RequiredGate = "forged-workflow-gate" };
Id65AuthoringAggregateV1RuntimeEvidencePackage blockerPackageTampered = pending with
{
    ScopeBoundary = pending.ScopeBoundary with
    {
        ProductionCaptureBlockers = blockerTampered,
        ProductionCaptureBlockersSha256 =
            Id65AuthoringAggregateV1RuntimeEvidenceContract
                .RecomputeProductionCaptureBlockersSha256(blockerTampered)
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(
            blockerPackageTampered)),
    "a forged production blocker must be rejected after closure recomputation");

Id65AuthoringAggregateV1RuntimeEvidencePackage integrityTampered = pending with
{
    Integrity = pending.Integrity with { PayloadSha256 = new string('0', 64) }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(integrityTampered),
    "integrity tampering must be rejected");

Id65AuthoringAggregateV1RuntimeEvidenceCheck[] observedChecks =
    pending.EventualChecks.Select(check => check with
    {
        Status = Id65AuthoringAggregateV1RuntimeEvidenceCheckStatus.Passed,
        SessionId = "forbidden-unpinned-run",
        EvidenceArtifactIds = ["forged-runtime-artifact"]
    }).ToArray();
Id65AuthoringAggregateV1RuntimeEvidencePackage observed = pending with
{
    RecordedAtUtc = "2026-08-25T00:00:00.000Z",
    DeclaredState = Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass,
    ArtifactCatalog =
    [
        new(
            "forged-runtime-artifact",
            "video",
            Id65AuthoringAggregateV1RuntimeEvidenceContract.RuntimeAssetRelativePrefix +
                "forged-runtime-artifact.mp4",
            1,
            new string('2', 64),
            "2026-08-25T00:00:00.000Z")
    ],
    EventualChecks = observedChecks,
    DerivedResult = pending.DerivedResult with
    {
        State = Id65AuthoringAggregateV1RuntimeEvidenceState.FocusedRuntimePass,
        ToolPinsFinalized = true,
        RuntimeEvidencePinned = true,
        AllEventualChecksPassed = true,
        FullReadbackAccepted = true,
        RuntimeProven = true,
        CreateBinAuthorized = true,
        ReleaseAuthorized = true,
        Publishable = true
    },
    ScopeBoundary = pending.ScopeBoundary with
    {
        SaveCapableProfilePinned = true,
        ProductionCaptureEnabled = true,
        RuntimeObserved = true,
        EditorIntegrated = true,
        CreateBinAuthorized = true,
        NormalCreateBinEnabled = true,
        ReleaseAuthorized = true,
        PromotionAuthorized = true,
        Publishable = true
    }
};
RequireRejected(
    Id65AuthoringAggregateV1RuntimeEvidenceContract.SerializeCanonicalUtf8(
        Id65AuthoringAggregateV1RuntimeEvidenceContract.WithRecomputedIntegrity(observed)),
    "a fully forged runtime pass must be rejected by the pending/static slice");

string[] expectedCheckIds =
[
    "candidate.safe-load-and-spawn",
    "candidate.authored-terrain-visible",
    "candidate.authored-collision-playable",
    "candidate.authored-occlusion-correct",
    "candidate.private-texture-swap-visible",
    "candidate.grass-zero-egg-placement-present",
    "candidate.grass-authored-placement-present",
    "candidate.zero-egg-preserved",
    "candidate.camera-360-stable",
    "candidate.walk-stable",
    "candidate.jump-land-stable",
    "candidate.fall-respawn-stable",
    "candidate.music-long-play-stable",
    "candidate.treasure-total-200",
    "candidate.exit-destination-65",
    "candidate.save-persistence",
    "retail.town-square-preserved"
];
Require(pending.EventualChecks.Select(check => check.CheckId).SequenceEqual(expectedCheckIds),
    "eventual runtime checklist identity changed");
Require(pending.ToolPins.Single(pin => pin.Role == "capture-coordinator") is
    {
        ByteLength: 78_915,
        Sha256: "f0d0baf60f7578174b05ef499cd9f94378f67b85405a5491ec029d4e0f650af3",
        Mode: "0644"
    }, "capture coordinator pin must stay finalized");
Require(pending.ToolPins.Single(pin => pin.Role == "frozen-capture-core") is
    {
        ByteLength: 473_144,
        Sha256: "153871778877a8736a1243ce2e84453e4d0d8e2fb945f9e96fdc01fbf00278ec",
        Mode: "0755"
    }, "frozen capture core pin must stay finalized");
Require(pending.ToolPins.Single(pin => pin.Role == "capture-deep-test") is
    {
        ByteLength: 28_214,
        Sha256: "4ddee51100eb4b9108e9aa8d3a68127ec0199c7ee377c4c50c99a732cdc9161a",
        Mode: "0644"
    }, "capture deep-test pin must stay finalized");
Require(pending.DerivedResult.ToolPinsFinalized,
    "the frozen aggregate tool set must remain finalized");
Require(Id65AuthoringAggregateV1RuntimeEvidenceContract.ExpectedFocusedRuntimeEvidenceSha256 ==
    "PENDING", "focused runtime evidence must remain explicitly PENDING");

Console.WriteLine(
    "ID65_AUTHORING_AGGREGATE_V1_RUNTIME_EVIDENCE_CONTRACT_SMOKE_OK " +
    $"checks={pending.EventualChecks.Length} state=pending-no-run " +
    $"source={pending.Integrity.SourceClosureSha256} " +
    $"profile={pending.Integrity.RuntimeProfileClosureSha256} " +
    $"tools={pending.Integrity.ToolSetClosureSha256} " +
    $"payload={pending.Integrity.PayloadSha256} " +
    $"contract={pending.Integrity.ContractSha256}");

static void RequireRejected(byte[] json, string message)
{
    Id65AuthoringAggregateV1RuntimeEvidenceValidation rejected =
        Id65AuthoringAggregateV1RuntimeEvidenceContract.ValidateCanonicalJson(json);
    Require(!rejected.StructurallyValid &&
        rejected.State == Id65AuthoringAggregateV1RuntimeEvidenceState.Invalid,
        message);
}

static void RequireThrows<TException>(Action action, string message)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }
    throw new InvalidOperationException(message);
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}
