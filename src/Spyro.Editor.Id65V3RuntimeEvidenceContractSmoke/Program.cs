using System.Buffers.Binary;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.IO.Compression;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Spyro.Editor.Core.Exporting;

string repositoryRoot = FindRepositoryRoot(args.ElementAtOrDefault(0));
Require(
    Assembly.GetExecutingAssembly().GetName().Name ==
        Id65V3RuntimeEvidenceContract.RequiredSmokeAssemblyName &&
    Id65V3RuntimeEvidenceContract.RequiredSmokeAssemblyName ==
        "Spyro.Editor.Id65V3RuntimeEvidenceContractSmoke",
    "The runtime-evidence smoke assembly identity changed.");
Require(
    Id65V3RuntimeEvidenceContract.MaximumCaptureRecordingDurationMicroseconds ==
        1_800_000_000L &&
    Id65V3RuntimeEvidenceContract.AssistedReadinessMinimumMilliseconds == 2_000 &&
    Id65V3RuntimeEvidenceContract.AssistedReadinessMinimumSamples == 19 &&
    Id65V3RuntimeEvidenceContract.AssistedPrimaryDepartureMaximumMilliseconds == 300_000 &&
    Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingMaximumMilliseconds == 30_000 &&
    Id65V3RuntimeEvidenceContract.MaximumFallAuthorizationBindingMilliseconds == 30_000 &&
    Id65V3RuntimeEvidenceContract.MaximumPostDeathRecoveryMilliseconds == 15_000 &&
    Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(0, 1) &&
    Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(
        1_000_000,
        1_801_000_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(
        1_000_000,
        1_801_000_001) &&
    !Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(
        1_000_000,
        1_000_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(-1, 0) &&
    !Id65V3RuntimeEvidenceContract.CaptureRecordingDurationWithinBound(2, 1),
    "The exact inclusive 30-minute recording-duration boundary changed.");

string[] retiredV2AuthorityPaths =
[
    "src/Spyro.Editor.Core/Exporting/Id65V2RuntimeEvidenceContract.cs",
    "src/Spyro.Editor.Core/Exporting/Id65V2RuntimeEvidenceFinalizer.cs",
    "src/Spyro.Editor.Core/Exporting/Id65V2RuntimeEvidenceFinalizer.Filesystem.cs",
    "src/Spyro.Editor.Id65V2RuntimeEvidenceContractSmoke",
    "src/Spyro.Editor.Id65V2RuntimeEvidenceFinalizer",
    "tools/Capture-SpyroId65V2RuntimeSession.py",
    "tools/Test-Capture-SpyroId65V2RuntimeSession.py",
    "docs/runtime-evidence/unused-level-65-remote-blank-collision-winding-repair-v2-runtime-evidence.json"
];
Require(retiredV2AuthorityPaths.All(relative =>
        !File.Exists(Path.Combine(repositoryRoot, relative)) &&
        !Directory.Exists(Path.Combine(repositoryRoot, relative))),
    "A retired V2 runtime contract, finalizer, coordinator, test, project, or evidence path remains callable.");
string activeV3AuthoritySource = string.Join('\n', new[]
{
    File.ReadAllText(Path.Combine(repositoryRoot,
        "src/Spyro.Editor.Core/Exporting/Id65V3RuntimeEvidenceContract.cs"), Encoding.UTF8),
    File.ReadAllText(Path.Combine(repositoryRoot,
        "src/Spyro.Editor.Core/Exporting/Id65V3RuntimeEvidenceFinalizer.cs"), Encoding.UTF8),
    File.ReadAllText(Path.Combine(repositoryRoot,
        "src/Spyro.Editor.Core/Exporting/Id65V3RuntimeEvidenceFinalizer.Filesystem.cs"), Encoding.UTF8),
    File.ReadAllText(Path.Combine(repositoryRoot,
        "src/Spyro.Editor.Id65V3RuntimeEvidenceFinalizer/Program.cs"), Encoding.UTF8),
    File.ReadAllText(Path.Combine(repositoryRoot,
        "tools/Capture-SpyroId65V3RuntimeSession.py"), Encoding.UTF8)
    ,File.ReadAllText(Path.Combine(repositoryRoot,
        "tools/Verify-SpyroId65UnattendedProfile.py"), Encoding.UTF8)
    ,File.ReadAllText(Path.Combine(repositoryRoot,
        "tools/Automate-SpyroId65V3RuntimeSession.py"), Encoding.UTF8)
});
string[] retiredV2FinalAuthorityTokens =
[
    "Id65V2RuntimeEvidence",
    "spyro-editor-id65-v2-runtime-human-attestation",
    "spyro-editor-id65-v2-runtime-check-evidence-binding",
    "id65-v2-runtime-source-closure-v1",
    "id65-v2-runtime-artifact-set-v1",
    "id65-v2-runtime-evidence-contract-v1",
    "id65-v2-runtime-finalizer-stable-input-v1",
    "id65-v2-runtime-profile-v1",
    "spyro-editor-id65-v2-runtime-capture-event-journal",
    "spyro-editor-id65-v2-runtime-capture-run-origin",
    "spyro-editor-id65-v2-runtime-profile-snapshot",
    "id65v2-",
    "id65v2-process-",
    "WindingV2RuntimeAccepted"
];
Require(retiredV2FinalAuthorityTokens.All(token =>
        !activeV3AuthoritySource.Contains(token, StringComparison.Ordinal)),
    "A retired V2 final-evidence domain, type, or acceptance authority remains active.");

Id65V3RuntimeEvidencePackage pending = Id65V3RuntimeEvidenceContract.CreatePending();
string[] capturePinNames = CapturePinNames();
Require(capturePinNames.Length == 22 &&
        capturePinNames.Distinct(StringComparer.Ordinal).Count() == capturePinNames.Length,
    "The external-HOME capture pin closure is not exactly 22 unique keys.");
string[] snapshotRecordProperties = typeof(Id65V3RuntimeProfileSnapshot)
    .GetProperties(BindingFlags.Instance | BindingFlags.Public)
    .Select(property => property.Name)
    .ToArray();
string[] expectedSnapshotRecordProperties =
[
    "Format", "FormatVersion", "SnapshotId", "Phase", "SessionId",
    "ProcessInstanceId", "ProfileId", "CapturedAtUtc", "DuckStationExecutableSha256",
    "HandoffReceiptSha256", "AppContentManifestSha256", "AppOutputTreeManifestSha256",
    "PackageEnvelopeSha256", "ExternalHomeSettingsSha256", "ExternalHomeBiosSha256",
    "HostNormalSettingsSentinelId", "HostNormalSettingsSentinelSha256", "DataFiles"
];
Require(snapshotRecordProperties.Length == 18 &&
        snapshotRecordProperties.ToHashSet(StringComparer.Ordinal).SetEquals(
            expectedSnapshotRecordProperties),
    "The runtime-profile snapshot model is not the exact closed 18-key v2 schema.");
string canonical = Id65V3RuntimeEvidenceContract.SerializeCanonical(pending);
byte[] canonicalBytes = Encoding.UTF8.GetBytes(canonical);
MethodInfo sourceClosureManifestMethod = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
    "BuildSourceClosureManifest",
    BindingFlags.Static | BindingFlags.NonPublic) ??
    throw new InvalidOperationException("The exact V3 source-closure manifest builder is missing.");
string sourceClosureManifest = (string)(sourceClosureManifestMethod.Invoke(
    null,
    [pending.SourceIdentity]) ?? throw new InvalidOperationException(
        "The exact V3 source-closure manifest builder returned null."));
string[] sourceClosureLines = sourceClosureManifest.Split('\n', StringSplitOptions.RemoveEmptyEntries);
string[] sourceClosureLabels = sourceClosureLines.Skip(1)
    .Select(line => line[..line.IndexOf('=')])
    .ToArray();
Require(sourceClosureLines[0] == "id65-v3-runtime-source-closure-v2" &&
        sourceClosureLabels.Length == sourceClosureLabels.Distinct(StringComparer.Ordinal).Count() &&
        sourceClosureLabels.Contains("collisionWindingV2RepairDiffManifestSha256", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3BeforeRow80DataSha256", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3AfterRow80DataSha256", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3AfterSectorSha256", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3AfterEnvironmentSha256", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3BeforeByte", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("environmentHeaderRepairV3AfterByte", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("failedV1RetestForbidden", StringComparer.Ordinal) &&
        sourceClosureLabels.Contains("supersededV2RetestForbidden", StringComparer.Ordinal),
    "The V3 source closure is missing unique, repair-specific lineage labels.");
byte[] verifierUnicodeGolden = VerifierCanonicalArtifactUtf8(
    new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dataFiles"] = new[]
        {
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["byteLength"] = 7,
                ["relativePath"] = "cache/caf\u00e9/\U0001f409.bin",
                ["sha256"] = new string('a', 64)
            }
        },
        ["z\U0001f600"] = "\u007f<>&\u00e9\U0001f409",
        ["\ue000"] = "bmp",
        ["\U00010000"] = "astral"
    });
byte[] expectedVerifierUnicodeGolden = Encoding.ASCII.GetBytes(
    "{\"dataFiles\":[{\"byteLength\":7,\"relativePath\":\"cache/caf\\u00e9/" +
    "\\ud83d\\udc09.bin\",\"sha256\":\"" + new string('a', 64) +
    "\"}],\"z\\ud83d\\ude00\":\"\\u007f<>&\\u00e9\\ud83d\\udc09\"," +
    "\"\\ue000\":\"bmp\",\"\\ud800\\udc00\":\"astral\"}\n");
Require(verifierUnicodeGolden.AsSpan().SequenceEqual(expectedVerifierUnicodeGolden) &&
        !verifierUnicodeGolden.AsSpan().Contains((byte)0xC3),
    "Verifier canonical LF serialization differs from Python ensure_ascii/code-point ordering.");
byte[] captureCanonicalUnicodeGolden = CanonicalArtifactUtf8(
    new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["paths"] = new[]
        {
            "cache/c1-\u0085.bin", "cache/\ue000.bin", "cache/\U0001F600.bin"
        },
        ["z\U0001F600"] = "\u0085|",
        ["\ue000"] = "bmp",
        ["\U00010000"] = "astral"
    });
byte[] expectedCaptureCanonicalUnicodeGolden = Encoding.UTF8.GetBytes(
    "{\"paths\":[\"cache/c1-\u0085.bin\",\"cache/\ue000.bin\",\"cache/\U0001F600.bin\"]," +
    "\"z\U0001F600\":\"\u0085|\",\"\ue000\":\"bmp\",\"\U00010000\":\"astral\"}");
Require(captureCanonicalUnicodeGolden.AsSpan().SequenceEqual(
            expectedCaptureCanonicalUnicodeGolden) &&
        !Encoding.UTF8.GetString(captureCanonicalUnicodeGolden)
            .Contains("\\u0085", StringComparison.OrdinalIgnoreCase) &&
        !Encoding.UTF8.GetString(captureCanonicalUnicodeGolden)
            .Contains("\\ue000", StringComparison.OrdinalIgnoreCase),
    "Capture canonical JSON differs from Python ensure_ascii=False/code-point ordering.");
string snapshotPathGoldenSha256 = new('b', 64);
string[] pythonOrderedVolatilePaths = UnicodeVolatileFixturePaths()
    .Order(SmokePythonUnicodeComparer.Instance)
    .ToArray();
Require(pythonOrderedVolatilePaths.SequenceEqual(
        ["cache/a|b.bin", "cache/c1-\u0085.bin", "cache/\ue000.bin", "cache/\U0001F600.bin"]),
    "Snapshot volatile paths are not ordered by Python Unicode scalar values.");
byte[] verifierSnapshotPathGolden = VerifierCanonicalArtifactUtf8(
    new Dictionary<string, object?>(StringComparer.Ordinal)
    {
        ["dataFiles"] = pythonOrderedVolatilePaths.Select((relativePath, index) =>
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["byteLength"] = index + 1,
                ["relativePath"] = relativePath,
                ["sha256"] = snapshotPathGoldenSha256
            }).ToArray()
    });
byte[] expectedVerifierSnapshotPathGolden = Encoding.ASCII.GetBytes(
    "{\"dataFiles\":[" +
    "{\"byteLength\":1,\"relativePath\":\"cache/a|b.bin\",\"sha256\":\"" +
    snapshotPathGoldenSha256 + "\"}," +
    "{\"byteLength\":2,\"relativePath\":\"cache/c1-\\u0085.bin\",\"sha256\":\"" +
    snapshotPathGoldenSha256 + "\"}," +
    "{\"byteLength\":3,\"relativePath\":\"cache/\\ue000.bin\",\"sha256\":\"" +
    snapshotPathGoldenSha256 + "\"}," +
    "{\"byteLength\":4,\"relativePath\":\"cache/\\ud83d\\ude00.bin\",\"sha256\":\"" +
    snapshotPathGoldenSha256 + "\"}]}\n");
Require(verifierSnapshotPathGolden.AsSpan().SequenceEqual(expectedVerifierSnapshotPathGolden) &&
        verifierSnapshotPathGolden.All(value => value < 0x80),
    "Snapshot-path verifier golden differs from Python ensure_ascii LF bytes.");
MethodInfo profileManifestPathGuard = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
    "GuardProfileManifestPath",
    BindingFlags.Static | BindingFlags.NonPublic) ??
    throw new InvalidOperationException("The snapshot profile-path guard is missing.");
foreach (string acceptedPath in new[]
{
    "cache/a|b.bin", "cache/c1-\u0085.bin", "cache/\ue000.bin",
    "cache/\U0001F600.bin", "cache/ "
})
{
    RequireProfileManifestPathAccepted(profileManifestPathGuard, acceptedPath);
}
foreach (string refusedPath in new[]
{
    "", "/cache/file.bin", "cache\\file.bin", "cache/c0-\u001f.bin",
    "cache/del-\u007f.bin", "cache/./file.bin", "cache/../file.bin",
    "cache//file.bin", "cache/cafe\u0301.bin"
})
{
    RequireProfileManifestPathRefused(profileManifestPathGuard, refusedPath);
}
Id65V3RuntimeEvidenceValidation staticValidation =
    Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(canonicalBytes, repositoryRoot);
Require(staticValidation.StructurallyValid &&
    staticValidation.State == Id65V3RuntimeEvidenceState.PendingNoRun &&
        staticValidation.Errors.Length == 0 && staticValidation.CanonicalJson == canonical,
    "The exact canonical pending contract did not validate as pending-no-run: " +
    string.Join(" | ", staticValidation.Errors));
Require(pending.DeclaredState == Id65V3RuntimeEvidenceState.PendingNoRun &&
        pending.DerivedResult.State == Id65V3RuntimeEvidenceState.PendingNoRun &&
        !pending.DerivedResult.EnvironmentHeaderRepairV3RuntimeAccepted &&
        !pending.DerivedResult.RuntimeEvidenceAccepted &&
        !pending.DerivedResult.FarLowDetailAccepted &&
        !pending.DerivedResult.SaveRuntimeAccepted &&
        !pending.DerivedResult.Core4RewardRuntimeAccepted &&
        !pending.DerivedResult.Total200RuntimeAccepted &&
        !pending.DerivedResult.PublisherAuthorized &&
        !pending.DerivedResult.AppIntegrated &&
        !pending.DerivedResult.NormalCreateBinEnabled &&
        !pending.DerivedResult.PromotionAuthorized &&
        !pending.DerivedResult.ReleaseIntegrated &&
        !pending.DerivedResult.PinnedRuntimeFilesReadbackValid &&
        !pending.DerivedResult.ProcessUseCryptographicallyProven &&
        pending.ScopeBoundary.PinnedRuntimeFilesAreCurrentReadbackOnly &&
        pending.ScopeBoundary.CaptureJournalOsProcessProvenanceCaptured &&
        pending.ScopeBoundary.CaptureJournalHashChainIsUnauthenticated &&
        !pending.ScopeBoundary.RawCaptureIsFinalEvidence &&
        !pending.ScopeBoundary.ProcessUseCryptographicallyProven &&
        pending.ScopeBoundary.ExecutableTextVnodeObserved &&
        !pending.ScopeBoundary.OperatorPromptIntervalsContinuouslyObserved &&
        !pending.ScopeBoundary.SystemCaptureAndDiagnosticToolsCryptographicallyPinned &&
        !pending.ScopeBoundary.SameUidRenameRacesCryptographicallyExcluded &&
        pending.ScopeBoundary.VisualMediaContainersStructurallyValidated &&
        !pending.ScopeBoundary.VisualMetadataDimensionsMachineVerified &&
        pending.ScopeBoundary.VisualContentIsHumanAttestation &&
        !pending.ScopeBoundary.AudioContentMachineVerified &&
        !pending.ScopeBoundary.EditorIntegrationAuthorized &&
        !pending.ScopeBoundary.CreateBinAuthorized &&
        !pending.ScopeBoundary.PublisherAuthorized &&
        !pending.ScopeBoundary.PromotionAuthorized &&
        !pending.ScopeBoundary.ReleaseAuthorized,
    "A pending-only runtime or release authorization flag became true.");

Require(Id65V3RuntimeEvidenceContract.ExpectedWatcherSha256 ==
            "eac3953a0ff7ff8acdefbe43cf987f296d44091ad51619efd225690ba1dc0677" &&
        Id65V3RuntimeEvidenceContract.ExpectedRuntimeProfileSha256 ==
            "29d01f1b791e4dc08ebfc4d62cf5a1b91600a5c55ec175ac6da45b13123100ad" &&
        Id65V3RuntimeEvidenceContract.ExpectedSourceClosureSha256 ==
            "1004799a4a41bd4222a5d6843f3ab495d9bef4d57e3b0698b9bb66f11279bbff" &&
        pending.SourceIdentity.CaptureCoordinatorRelativePath ==
            "tools/Capture-SpyroId65V3RuntimeSession.py" &&
        pending.SourceIdentity.CaptureCoordinatorSha256 ==
            Id65V3RuntimeEvidenceContract.ExpectedCaptureCoordinatorSha256 &&
        pending.SourceIdentity.CandidateBinSha256 ==
            "42e4d5cae98d4786023bef26517d0435b43aff365b797495386813f44ecb0c45" &&
        pending.SourceIdentity.CandidateCueSha256 ==
            "ab91adb4ffcbad70bf7a226a2a4a6d0f6efe6b54fbed0b8126e59d1b8ecaf6df" &&
        pending.SourceIdentity.StaticReadbackReceiptSha256 ==
            "e4494a9364165951f6a7e54676574c085495a3f800b65e616da2193fa0d01be4" &&
        pending.SourceIdentity.RuntimeChecklistSha256 ==
            "7023eb070d6ee1c840bd52013fb851ab53f8de52d2bc68f80a79323a0392363b" &&
        pending.SourceIdentity.LocationGuideSha256 ==
            "bab8c2851ca8743c43c91d70a9ec218796706e68c6fdfa50d977d013520c99a7" &&
        pending.SourceIdentity.RevealCommandSha256 ==
            "e5e5c9485666e36aed50e6b0e9f43158f58fcf91f1fbb07f3207f8ccdc0be52a" &&
        pending.SourceIdentity.CollisionWindingV2BinSha256 ==
            "23ce483a29187996827148d986fb12f483fc2caa82fc953106882568ea9254a6" &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3BeforeRow80DataSha256 ==
            pending.SourceIdentity.CollisionWindingV2OutputDataSha256 &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3BeforeModelSha256 ==
            pending.SourceIdentity.CollisionWindingV2OutputModelSha256 &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3AfterSectorSha256 ==
            "7d71ebc9558c10896b83d1d8a79e6224cd6a48630b7a335d25b46935a572759e" &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3AfterEnvironmentSha256 ==
            "c4097bfdc54cf9a29864f5195e52c44151478d67e515fdb27f7c4c4f24d1ff06" &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3BeforeByte == 9 &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3AfterByte == 8 &&
        pending.SourceIdentity.EnvironmentHeaderRepairV3RawSectorLba == 54_437 &&
        pending.SourceIdentity.FailedV1RetestForbidden &&
        pending.SourceIdentity.SupersededV2RetestForbidden &&
        pending.SourceIdentity.ProfileBuilderSha256 ==
            "0591f1b6bf3f61dac4bfa61d407df65a2a9862569c8d2a7678d216b95929b95c" &&
        pending.SourceIdentity.ProfileVerifierSha256 ==
            "74f4ed279a3e3f60af6c4c0a0b1fa8ffc0a67886c478d8cc7d1a288b093c3671" &&
        pending.SourceIdentity.AutomationSha256 ==
            "049b4fbeecb4b176d415614165589162c10d8322f349af9c1ef6e93c6e1c2899" &&
        pending.RuntimeProfile.ProfileId == "duckstation-id65-unattended-v1" &&
        pending.RuntimeProfile.PackageRelativePath ==
            "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1" &&
        pending.RuntimeProfile.HostNormalSettingsSentinelId ==
            "duckstation-normal-user-settings.ini" &&
        pending.RuntimeProfile.ExternalHomeSettingsSha256 ==
            "469ef98771f26395352bac1d43f90e4be14693fd4ba5316e4d71e0557a7f74f9" &&
        pending.RuntimeProfile.HostNormalSettingsSha256 ==
            "896daa612142659aa950c16d2435f81cb034aa8b56768b91e60a4ec8f0a5ccc9" &&
        pending.RuntimeProfile.HandoffReceiptSha256 ==
            "a748153bdd554187fb30ee79e75d25d36c606663bd4aee8444826b7612aa27d5" &&
        pending.RuntimeProfile.PowerOffBinding == "Keyboard/F12" &&
        pending.RuntimeProfile.TogglePauseBinding == "Keyboard/Space" &&
        pending.RuntimeProfile.ExpectedPatchFileCount == 0 &&
        pending.RuntimeProfile.AllowedVolatilePolicy ==
            "playtime.dat,cache/**,screenshots/**,videos/**" &&
        pending.RuntimeProfile.SafetyDirectories.SequenceEqual(
            ["cheats", "gamesettings", "memcards", "patches", "savestates"]) &&
        pending.Integrity.SourceClosureSha256 ==
            Id65V3RuntimeEvidenceContract.RecomputeSourceClosureSha256(pending.SourceIdentity) &&
        pending.Integrity.RuntimeProfileSha256 ==
            Id65V3RuntimeEvidenceContract.RecomputeRuntimeProfileSha256(pending.RuntimeProfile),
    "A frozen source, watcher, normal-settings, profile, or closure pin changed.");
Require(
    Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(
        "candidate.primary-edge-4-to-3-same-pad-yaw") == "E0=A-B" &&
    Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(
        "candidate.edge-1-same-pad-yaw") == "E1=B-C" &&
    Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(
        "candidate.edge-2-same-pad-yaw") == "E2=C-A" &&
    Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(
        "candidate.lone-pad-visibility") is null,
    "The exact edge-check-to-physical-segment attestation map changed.");
Require(!canonical.Contains("/Users/", StringComparison.Ordinal) &&
        canonical.StartsWith("{\"artifactCatalog\":", StringComparison.Ordinal) &&
        !canonical.Contains('\n') &&
        canonical == Encoding.UTF8.GetString(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(pending)),
    "Canonical JSON is not path-independent exact RFC 8785 UTF-8.");

string watcherPath = Path.Combine(repositoryRoot, "tools", "Watch-SpyroId65RuntimeState.py");
Require(File.Exists(watcherPath) && HashFile(watcherPath) ==
            Id65V3RuntimeEvidenceContract.ExpectedWatcherSha256,
    "The final frozen watcher bytes differ from the runtime-evidence pin.");
string captureCoordinatorPath = Path.Combine(
    repositoryRoot,
    "tools",
    "Capture-SpyroId65V3RuntimeSession.py");
UnixFileMode executable0755 = UnixFileMode.UserRead | UnixFileMode.UserWrite |
    UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
    UnixFileMode.OtherRead | UnixFileMode.OtherExecute;
if (!OperatingSystem.IsMacOS())
    throw new InvalidOperationException("The frozen capture-evidence smoke is macOS-only.");
Require(File.Exists(captureCoordinatorPath) &&
        HashFile(captureCoordinatorPath) ==
            Id65V3RuntimeEvidenceContract.ExpectedCaptureCoordinatorSha256 &&
        File.GetUnixFileMode(captureCoordinatorPath) == executable0755,
    "The final capture coordinator bytes or exact executable 0755 mode differ from the source closure.");
string captureCoordinatorSource = File.ReadAllText(captureCoordinatorPath, Encoding.UTF8);
Require(captureCoordinatorSource.Contains("PROC_UID_ONLY = 4", StringComparison.Ordinal) &&
        !captureCoordinatorSource.Contains(
            "PROC_" + "ALL" + "_PIDS", StringComparison.Ordinal) &&
        captureCoordinatorSource.Contains(
            "def _require_exact_console_session(", StringComparison.Ordinal) &&
        captureCoordinatorSource.Contains(
            "kCGSSessionUserIDKey", StringComparison.Ordinal) &&
        captureCoordinatorSource.Contains(
            "returned_byte_count == buffer_byte_count", StringComparison.Ordinal) &&
        captureCoordinatorSource.Contains(
            "require_real_and_saved_uid=True", StringComparison.Ordinal),
    "The frozen macOS console or current-UID process boundary regressed.");
Id65V3RuntimeEvidenceValidation fullPendingValidation =
    Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        canonicalBytes,
        repositoryRoot,
        Id65V3RuntimeEvidenceValidationMode.FullReadback);
Require(fullPendingValidation.StructurallyValid &&
        fullPendingValidation.State == Id65V3RuntimeEvidenceState.PendingNoRun,
    "Full readback did not preserve the no-run package as pending-no-run.");

int pinnedSourceBoundaryRejects = 0;
string candidateDirectoryPath = Path.GetDirectoryName(Path.Combine(
    repositoryRoot,
    pending.SourceIdentity.CandidateBinRelativePath)) ??
    throw new InvalidOperationException("The V3 candidate BIN has no directory.");
string candidateExtraPath = Path.Combine(candidateDirectoryPath, ".v3-source-topology-smoke-extra");
Require(!File.Exists(candidateExtraPath) && !Directory.Exists(candidateExtraPath),
    "The candidate topology refusal sentinel already exists.");
try
{
    File.WriteAllBytes(candidateExtraPath, [0x65]);
    File.SetUnixFileMode(candidateExtraPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead |
        UnixFileMode.OtherRead);
    RejectPinnedSourceBoundary("candidate extra entry", "exactly its six data leaves and one Finder helper");
}
finally
{
    if (File.Exists(candidateExtraPath))
        File.Delete(candidateExtraPath);
}

string candidateDataPath = Path.Combine(repositoryRoot,
    pending.SourceIdentity.ConstructionPlanRelativePath);
UnixFileMode candidateDataMode = File.GetUnixFileMode(candidateDataPath);
try
{
    File.SetUnixFileMode(candidateDataPath, candidateDataMode | UnixFileMode.GroupWrite);
    RejectPinnedSourceBoundary("candidate data mode", "without group/other write permission");
}
finally
{
    File.SetUnixFileMode(candidateDataPath, candidateDataMode);
}

string candidateHelperPath = Path.Combine(repositoryRoot,
    pending.SourceIdentity.RevealCommandRelativePath);
UnixFileMode candidateHelperMode = File.GetUnixFileMode(candidateHelperPath);
try
{
    File.SetUnixFileMode(candidateHelperPath,
        candidateHelperMode & ~UnixFileMode.UserExecute &
        ~UnixFileMode.GroupExecute & ~UnixFileMode.OtherExecute);
    RejectPinnedSourceBoundary("candidate Finder-helper mode", "mode 755");
}
finally
{
    File.SetUnixFileMode(candidateHelperPath, candidateHelperMode);
}

UnixFileMode candidateDirectoryMode = File.GetUnixFileMode(candidateDirectoryPath);
try
{
    File.SetUnixFileMode(candidateDirectoryPath,
        candidateDirectoryMode & ~UnixFileMode.GroupExecute & ~UnixFileMode.OtherExecute);
    RejectPinnedSourceBoundary("candidate directory mode", "mode 755");
}
finally
{
    File.SetUnixFileMode(candidateDirectoryPath, candidateDirectoryMode);
}

UnixFileMode watcherMode = File.GetUnixFileMode(watcherPath);
try
{
    File.SetUnixFileMode(watcherPath, watcherMode | UnixFileMode.GroupWrite);
    RejectPinnedSourceBoundary("watcher exact mode", "without group/other write permission");
}
finally
{
    File.SetUnixFileMode(watcherPath, watcherMode);
}

UnixFileMode coordinatorMode = File.GetUnixFileMode(captureCoordinatorPath);
try
{
    File.SetUnixFileMode(captureCoordinatorPath,
        coordinatorMode & ~UnixFileMode.UserExecute &
        ~UnixFileMode.GroupExecute & ~UnixFileMode.OtherExecute);
    RejectPinnedSourceBoundary("coordinator exact mode", "mode 755");
}
finally
{
    File.SetUnixFileMode(captureCoordinatorPath, coordinatorMode);
}

Id65V3RuntimeEvidencePackage unicodePackage = Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
    pending with { EvidenceId = "jcs-\u000f-🐉-é" });
string unicodeCanonical = Id65V3RuntimeEvidenceContract.SerializeCanonical(unicodePackage);
Require(unicodeCanonical.Contains("\"evidenceId\":\"jcs-\\u000f-🐉-é\"", StringComparison.Ordinal) &&
        !unicodeCanonical.Contains("\\u000F", StringComparison.Ordinal) &&
        !unicodeCanonical.Contains("\\ud83d", StringComparison.OrdinalIgnoreCase),
    "RFC 8785 string serialization changed control escaping or escaped required Unicode scalar values.");
Id65V3RuntimeEvidenceValidation unicodeValidation =
    Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        Encoding.UTF8.GetBytes(unicodeCanonical),
        repositoryRoot);
Require(unicodeValidation.StructurallyValid &&
        unicodeValidation.State == Id65V3RuntimeEvidenceState.PendingNoRun,
    "RFC 8785 Unicode/control canonical JSON did not survive exact contract readback.");
foreach (string invalidScalar in new[] { "\uD800", "\uDC00" })
{
    try
    {
        _ = Id65V3RuntimeEvidenceContract.SerializeCanonical(
            pending with { EvidenceId = "jcs-unpaired-" + invalidScalar });
        throw new InvalidOperationException("RFC 8785 serialization accepted an unpaired UTF-16 surrogate.");
    }
    catch (InvalidDataException)
    {
    }
}
Id65V3RuntimeEvidencePackage unsafeIntegerPackage = pending with
{
    ArtifactCatalog =
    [
        new(
            "unsafe-jcs-integer",
            "candidate-primary-edge-0",
            "frame",
            "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/unsafe.png",
            new string('0', 64),
            9_007_199_254_740_992,
            "2026-08-10T20:00:00.000Z",
            MediaStartAtUtc: null,
            MediaEndAtUtc: null)
    ]
};
try
{
    _ = Id65V3RuntimeEvidenceContract.SerializeCanonical(unsafeIntegerPackage);
    throw new InvalidOperationException("RFC 8785 serialization accepted an integer outside the exact I-JSON range.");
}
catch (InvalidDataException)
{
}

Id65V3RuntimeEvidenceDerivedResult incompleteDerived = pending.DerivedResult with
{
    State = Id65V3RuntimeEvidenceState.PendingIncomplete,
    SourceValid = false
};
int stateGateRejects = 0;
Id65V3RuntimeEvidencePackage unsafeProfilePackage = Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
    pending with
    {
        EvidenceId = "unsafe-profile-observation",
        DeclaredState = Id65V3RuntimeEvidenceState.PendingIncomplete,
        RuntimeProfile = pending.RuntimeProfile with { SaveStateLoaded = true },
        DerivedResult = incompleteDerived
    });
RejectSealedPackage("unsafe profile observation", unsafeProfilePackage);

Id65V3RuntimeEvidencePackage malformedFailedResetPackage =
    Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
        pending with
        {
            EvidenceId = "malformed-failed-cold-reset",
            DeclaredState = Id65V3RuntimeEvidenceState.RuntimeRejected,
            ColdReset = pending.ColdReset with { Status = Id65V3RuntimeEvidenceCheckStatus.Failed },
            DerivedResult = pending.DerivedResult with
            {
                State = Id65V3RuntimeEvidenceState.RuntimeRejected,
                SourceValid = false
            }
        });
RejectSealedPackage("malformed failed cold reset", malformedFailedResetPackage);

Id65V3RuntimeEvidenceProfile completeSafeProfile = pending.RuntimeProfile with
{
    SettingsBeforeSha256 = pending.RuntimeProfile.ExternalHomeSettingsSha256,
    SettingsAfterSha256 = pending.RuntimeProfile.ExternalHomeSettingsSha256,
    NormalUserSettingsBeforeSha256 = pending.RuntimeProfile.HostNormalSettingsSha256,
    NormalUserSettingsAfterSha256 = pending.RuntimeProfile.HostNormalSettingsSha256,
    CheatFileCountBefore = 0,
    CheatFileCountAfter = 0,
    GameSettingsFileCountBefore = 0,
    GameSettingsFileCountAfter = 0,
    MemoryCardFileCountBefore = 0,
    MemoryCardFileCountAfter = 0,
    SaveStateFileCountBefore = 0,
    SaveStateFileCountAfter = 0,
    PatchFileCountBefore = 0,
    PatchFileCountAfter = 0,
    ChangedProfilePaths = [],
    SaveStateLoaded = false,
    SaveStateCreated = false,
    InGameSaveUsed = false,
    ActiveCheatCount = 0
};
Id65V3RuntimeEvidencePackage safeProfileOnlyPackage = Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
    pending with
    {
        EvidenceId = "safe-profile-observation-only",
        DeclaredState = Id65V3RuntimeEvidenceState.PendingIncomplete,
        RuntimeProfile = completeSafeProfile,
        DerivedResult = incompleteDerived
    });
Id65V3RuntimeEvidenceValidation safeProfileOnlyValidation =
    Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(safeProfileOnlyPackage),
        repositoryRoot);
Require(safeProfileOnlyValidation.StructurallyValid &&
        safeProfileOnlyValidation.State == Id65V3RuntimeEvidenceState.PendingIncomplete,
    "A complete safe profile observation was misclassified as pending-no-run or invalid.");

int canonicalRejects = 0;
RejectContract("leading whitespace", " " + canonical);
RejectContract("trailing LF", canonical + "\n");
RejectContract("UTF-8 BOM", "\uFEFF" + canonical);
RejectContract("duplicate root key", canonical.Insert(1, "\"format\":\"duplicate\","));
RejectContract("unknown root key", canonical.Insert(1, "\"unknown\":0,"));
RejectContract("case alias", ReplaceOnce(canonical, "\"format\":", "\"Format\":"));
RejectContract("missing root field", ReplaceOnce(canonical, "\"artifactCatalog\":[],", string.Empty));
RejectContract("wrong root type", ReplaceOnce(canonical, "\"artifactCatalog\":[]", "\"artifactCatalog\":{}"));
RejectContract(
    "wrong string token",
    ReplaceOnce(
        canonical,
        "\"format\":\"spyro-editor-id65-v3-environment-header-runtime-evidence\"",
        "\"format\":0"));
RejectContract(
    "retired V2 evidence format",
    ReplaceOnce(
        canonical,
        "spyro-editor-id65-v3-environment-header-runtime-evidence",
        "spyro-editor-id65-v2-runtime-evidence"));
RejectContract("null root array", ReplaceOnce(canonical, "\"artifactCatalog\":[]", "\"artifactCatalog\":null"));
RejectContract(
    "case-varied state",
    ReplaceOnce(canonical, "\"declaredState\":\"pending-no-run\"", "\"declaredState\":\"PendingNoRun\""));
RejectContract(
    "watcher pin",
    ReplaceOnce(
        canonical,
        Id65V3RuntimeEvidenceContract.ExpectedWatcherSha256,
        MutateNibble(Id65V3RuntimeEvidenceContract.ExpectedWatcherSha256)));
RejectContract(
    "payload hash",
    ReplaceOnce(canonical, pending.Integrity.PayloadSha256, MutateNibble(pending.Integrity.PayloadSha256)));
RejectContract(
    "declared pass",
    ReplaceOnce(canonical, "\"declaredState\":\"pending-no-run\"", "\"declaredState\":\"focused-runtime-pass\""));
Require(canonicalRejects == 15, "The canonical contract reject matrix did not execute completely.");

int sourceIdentityRejects = 0;
RejectSourceIdentity(
    "retired V2 evidence ID",
    pending with
    {
        EvidenceId =
            "unused-level-65-remote-blank-collision-winding-repair-v2-runtime-evidence-pending"
    },
    "retired V2 candidate evidence ID");
RejectSourceIdentity(
    "V2 candidate BIN promoted into V3",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            CandidateBinRelativePath = pending.SourceIdentity.CollisionWindingV2BinRelativePath,
            CandidateBinSha256 = pending.SourceIdentity.CollisionWindingV2BinSha256
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "V2 candidate CUE namespace",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            CandidateCueRelativePath =
                "_local/v5-stone-hill-level-replacement/unused-level-65-remote-blank-collision-winding-repair-v2/Unused-Level-65-Remote-Blank-Collision-Winding-Repair-v2-Sector216-RUNTIME-CANDIDATE.cue"
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "mixed V2/V3 lineage",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            EnvironmentHeaderRepairV3BeforeModelSha256 =
                pending.SourceIdentity.FailedV1OutputModelSha256
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "failed V1 retest permitted",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with { FailedV1RetestForbidden = false }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "superseded V2 retest permitted",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with { SupersededV2RetestForbidden = false }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "header repair direction reversed",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            EnvironmentHeaderRepairV3BeforeByte = 8,
            EnvironmentHeaderRepairV3AfterByte = 9
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "mixed V3 sector lineage",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            EnvironmentHeaderRepairV3AfterSectorSha256 =
                pending.SourceIdentity.SupersededV2SectorSha256
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "missing V3 environment identity",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            EnvironmentHeaderRepairV3AfterEnvironmentSha256 = new string('0', 64)
        }
    },
    "Pinned V3 candidate source identity");
RejectSourceIdentity(
    "retired source closure field",
    pending with
    {
        SourceIdentity = pending.SourceIdentity with
        {
            SourceClosureSha256 = new string('2', 64)
        }
    },
    "Pinned V3 candidate source identity");
Require(sourceIdentityRejects == 10,
    "The V3 source-identity and retired-V2 reject matrix did not execute completely.");

const string Warning =
    "WARNING: connecting to this DuckStation GDB server pauses a running game exactly once.";
const string ReadOnly =
    "This watcher sends only the three approved m reads and protocol ACKs; it cannot resume the game.";
const string Connected = "Connected once to 127.0.0.1:2345. DuckStation is now paused.";
const string Prompt =
    "Manually Resume in DuckStation, leave this watcher connected, then press Return here to begin the 0.1-second-period watch: ";
const string Watching =
    "Watching one numbered near-contemporaneous tuple per period with monotonic elapsed milliseconds; press Ctrl-C to stop cleanly.";
const string Stopped =
    "Stopped cleanly; the socket is closed and no control packet was sent. If DuckStation was not manually resumed, it remains paused.";

string[] stableLines =
[
    Sample("2026-08-10T19:34:56.000Z", 0, 0, 4, 0, 1200),
    Sample("2026-08-10T19:34:56.100Z", 1, 100, 4, 0, 1200),
    Sample("2026-08-10T19:34:56.200Z", 2, 200, 4, 0, 1200)
];
foreach (string separator in new[] { string.Empty, "\n", "\r\n" })
{
    Id65V3TelemetryParseResult parsed = Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(stableLines, separator));
    Require(parsed.CleanStopObserved && parsed.Samples.Length == 3 &&
            parsed.Samples.Select(sample => sample.SampleOrdinal).SequenceEqual([0, 1, 2]) &&
            parsed.Samples.Select(sample => sample.ElapsedMilliseconds).SequenceEqual([0L, 100L, 200L]),
        "A frozen prompt separator or numbered monotonic sample sequence did not parse.");
}

Id65V3TelemetryParseResult stableParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(stableLines, string.Empty));
Id65V3RuntimeEvidenceSession stableSession = SyntheticSession(
    landingSampleOrdinal: 0,
    holdStartSampleOrdinal: 0,
    holdEndSampleOrdinal: 2,
    fallStartSampleOrdinal: null,
    respawnSampleOrdinal: null);
Id65V3TelemetrySummary stableSummary =
    Id65V3RuntimeEvidenceContract.RecomputeTelemetrySummary(stableParsed, stableSession);
Require(stableSummary.MaximumGapMilliseconds == 100 && stableSummary.TotalSamples == 3 &&
        stableSummary.StableHold is
        {
            StartSampleOrdinal: 0,
            EndSampleOrdinal: 2,
            StartElapsedMilliseconds: 0,
            EndElapsedMilliseconds: 200,
            DurationMilliseconds: 200,
            SampleCount: 3,
            MaximumGapMilliseconds: 100,
            AllLivesFour: true,
            AllStateZero: true,
            AllZAtLeast1024: true
        },
    "Stable-hold summary was not recomputed exclusively from sample ordinals and elapsed milliseconds.");

string assistedEpoch = "2026-08-10T19:34:00.000Z";
string assistedRecordingStartedAt = "2026-08-10T19:33:59.900Z";
DateTimeOffset tiedClockAt = ParseUtc("2026-08-10T19:33:59.000Z");
Require(
    Id65V3RuntimeEvidenceContract.CaptureWithinSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt, 1_250_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureWithinSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt, 1_250_001) &&
    !Id65V3RuntimeEvidenceContract.CaptureWithinSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt.AddMilliseconds(-1), 1_001_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureWithinSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt, 1_000_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureWithinSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt, 999_999),
    "Within-session dual-clock boundary changed.");
Require(
    Id65V3RuntimeEvidenceContract.CaptureCrossSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt.AddMilliseconds(1), 1_251_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureCrossSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt.AddMilliseconds(1), 1_251_001) &&
    !Id65V3RuntimeEvidenceContract.CaptureCrossSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt, 1_000_001) &&
    !Id65V3RuntimeEvidenceContract.CaptureCrossSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt.AddMilliseconds(1), 1_000_000) &&
    !Id65V3RuntimeEvidenceContract.CaptureCrossSessionClockStepIsAccepted(
        tiedClockAt, 1_000_000, tiedClockAt.AddMilliseconds(1), 999_999),
    "Cross-session dual-clock boundary changed.");
string[] assistedLandingLines = Enumerable.Range(0, 143)
    .Select(ordinal =>
    {
        int state = ordinal <= 29 || ordinal >= 120
            ? 0
            : ordinal <= 69 ? 3 : 1;
        return Sample(
            FormatUtc(ParseUtc(assistedEpoch).AddMilliseconds(ordinal * 100L)),
            ordinal,
            ordinal * 100L,
            4,
            state,
            8_548);
    })
    .ToArray();
Id65V3TelemetryParseResult assistedLandingParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(assistedLandingLines, string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedLandingParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142),
    "Assisted landing rejected the exact selector-bound 3/1/0 transition and safe suffix.");

Id65V3TelemetryParseResult AssistedLandingMutation(
    int ordinal,
    int lives,
    int state,
    int z)
{
    string[] changed = [.. assistedLandingLines];
    changed[ordinal] = Sample(
        FormatUtc(ParseUtc(assistedEpoch).AddMilliseconds(ordinal * 100L)),
        ordinal,
        ordinal * 100L,
        lives,
        state,
        z);
    return Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(changed, string.Empty));
}
Require(
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedLandingParsed,
        "2026-08-10T19:34:00.500Z",
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedLandingParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.100Z",
        6_100_000,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedLandingParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_001,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedLandingParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        AssistedLandingMutation(5, 4, 13, 8_548),
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        AssistedLandingMutation(30, 4, 1, 8_548),
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        AssistedLandingMutation(130, 4, 3, 8_548),
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:03.000Z",
        6_000_000,
        142),
    "Assisted landing admitted a pre-recording readiness suffix, forged start, shifted selector, stale suffix, skipped state, or second transition.");

string[] assistedBoundaryLines = Enumerable.Range(0, 342)
    .Select(ordinal =>
    {
        int state = ordinal <= 20 || ordinal >= 321
            ? 0
            : ordinal <= 120 ? 3 : 1;
        return Sample(
            FormatUtc(ParseUtc(assistedEpoch).AddMilliseconds(ordinal * 100L)),
            ordinal,
            ordinal * 100L,
            4,
            state,
            8_548);
    })
    .ToArray();
Id65V3TelemetryParseResult assistedBoundaryParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(assistedBoundaryLines, string.Empty));
string[] assistedOverBoundaryLines = [.. assistedBoundaryLines];
assistedOverBoundaryLines[321] = Sample(
    "2026-08-10T19:34:32.101Z", 321, 32_101, 4, 0, 8_548);
Id65V3TelemetryParseResult assistedOverBoundaryParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(assistedOverBoundaryLines, string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedBoundaryParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:02.100Z",
        5_100_000,
        341) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedOverBoundaryParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:34:02.100Z",
        5_100_000,
        341),
    "Assisted landing did not enforce the inclusive 30,000ms transition bound.");

string[] assistedDepartureBoundaryLines = Enumerable.Range(0, 3_043)
    .Select(ordinal =>
    {
        int state = ordinal <= 3_019 || ordinal >= 3_022
            ? 0
            : ordinal == 3_020 ? 3 : 1;
        return Sample(
            FormatUtc(ParseUtc(assistedEpoch).AddMilliseconds(ordinal * 100L)),
            ordinal,
            ordinal * 100L,
            4,
            state,
            8_548);
    })
    .ToArray();
Id65V3TelemetryParseResult assistedDepartureBoundaryParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(assistedDepartureBoundaryLines, string.Empty));
string[] assistedDepartureOverLines = [.. assistedDepartureBoundaryLines];
assistedDepartureOverLines[3_020] = Sample(
    "2026-08-10T19:39:02.001Z", 3_020, 302_001, 4, 3, 8_548);
Id65V3TelemetryParseResult assistedDepartureOverParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
        WatcherLog(assistedDepartureOverLines, string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedDepartureBoundaryParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:39:02.000Z",
        305_000_000,
        3_042) &&
    !Id65V3RuntimeEvidenceContract.AssistedPrimaryLandingEvidenceIsAccepted(
        assistedDepartureOverParsed,
        assistedRecordingStartedAt,
        "2026-08-10T19:34:02.000Z",
        5_000_000,
        "2026-08-10T19:39:02.001Z",
        305_001_000,
        3_042),
    "Assisted landing did not enforce the inclusive 5-minute departure bound.");

Id65V3RuntimeEvidenceArtifact syntheticWatcherArtifact = new(
    "synthetic-watcher",
    "candidate-primary-edge-0",
    "watcher-stdout",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/synthetic-watcher.log",
    new string('0', 64),
    ByteLength: 1,
    "2026-08-10T20:00:00.000Z",
    MediaStartAtUtc: null,
    MediaEndAtUtc: null);
Id65V3RuntimeEvidenceSession nullNestedSummarySession = SyntheticSession(
    landingSampleOrdinal: null,
    holdStartSampleOrdinal: null,
    holdEndSampleOrdinal: null,
    fallStartSampleOrdinal: null,
    respawnSampleOrdinal: null) with
{
    SessionId = "candidate-primary-edge-0",
    LoadCode = "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down",
    WatcherArtifactId = syntheticWatcherArtifact.ArtifactId,
    SampleSummary = stableSummary with { LivesValues = null! }
};
Id65V3RuntimeEvidencePackage nullNestedSummaryPackage =
    Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
        pending with
        {
            EvidenceId = "null-nested-telemetry-summary",
            DeclaredState = Id65V3RuntimeEvidenceState.PendingIncomplete,
            ArtifactCatalog = [syntheticWatcherArtifact],
            Sessions = [nullNestedSummarySession],
            DerivedResult = incompleteDerived
        });
RejectSealedPackage("null nested telemetry array", nullNestedSummaryPackage);
Require(stateGateRejects == 3, "The state-precedence and nested-shape reject matrix did not execute completely.");

string[] deathLines =
[
    Sample("2026-08-10T19:35:00.000Z", 0, 0, 0, 13, 0),
    Sample("2026-08-10T19:35:00.100Z", 1, 100, 4, 1, 1200),
    Sample("2026-08-10T19:35:00.200Z", 2, 200, 4, 0, 1200),
    Sample("2026-08-10T19:35:00.300Z", 3, 300, 3, 4, 800),
    Sample("2026-08-10T19:35:00.400Z", 4, 400, 3, 4, 900),
    Sample("2026-08-10T19:35:00.500Z", 5, 500, 3, 0, 1200),
    Sample("2026-08-10T19:35:00.600Z", 6, 600, 3, 0, 1200),
    Sample("2026-08-10T19:35:00.700Z", 7, 700, 3, 0, 1200)
];
Id65V3TelemetryParseResult deathParsed = Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
    WatcherLog(deathLines, string.Empty));
Id65V3RuntimeEvidenceSession deathSession = SyntheticSession(2, null, null, 3, 6);
Id65V3TelemetrySummary deathSummary =
    Id65V3RuntimeEvidenceContract.RecomputeTelemetrySummary(deathParsed, deathSession);
Require(deathSummary.DeathEpisode is
        {
            PreFallSample.Lives: 4,
            FirstZBelow1024.SampleOrdinal: 3,
            FirstLivesThree.SampleOrdinal: 3,
            FirstStateFour.SampleOrdinal: 3,
            FirstPostRespawnStable.SampleOrdinal: 5,
            LivesTransitionCount: 1,
            SecondDecrementObserved: false
        } && deathSummary.LivesTransitions.SequenceEqual(["0->4", "4->3"]) &&
        Id65V3RuntimeEvidenceContract.CandidateDeathTelemetryIsAccepted(
            deathParsed,
            deathSession,
            deathSummary.DeathEpisode) &&
        Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
            deathParsed,
            3,
            "2026-08-10T19:35:00.600Z",
            6),
    "Scoped death acceptance rejected valid pre-landing noise or fast recovery before the marker.");
string[] fallBoundaryLines = Enumerable.Range(0, 300)
    .Select(ordinal => Sample(
        FormatUtc(ParseUtc("2026-08-10T19:36:00.000Z").AddMilliseconds(ordinal * 100L)),
        ordinal,
        ordinal * 100L,
        4,
        0,
        1162))
    .Append(Sample("2026-08-10T19:36:30.000Z", 300, 30_000, 3, 4, 882))
    .ToArray();
Id65V3TelemetryParseResult fallBoundaryParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        fallBoundaryLines,
        string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.FallBindingIsAccepted(
        fallBoundaryParsed,
        "2026-08-10T19:36:00.000Z",
        300) &&
    Id65V3RuntimeEvidenceContract.FallBindingIsAccepted(
        fallBoundaryParsed,
        "2026-08-10T19:36:30.000Z",
        300) &&
    !Id65V3RuntimeEvidenceContract.FallBindingIsAccepted(
        fallBoundaryParsed,
        "2026-08-10T19:35:59.999Z",
        300) &&
    !Id65V3RuntimeEvidenceContract.FallBindingIsAccepted(
        fallBoundaryParsed,
        "2026-08-10T19:36:30.001Z",
        300),
    "Fall authorization did not enforce inclusive 0/30,000ms and refuse 30,001ms or pre-cue transition.");
string[] unsafeFallInteriorLines = [.. fallBoundaryLines];
unsafeFallInteriorLines[150] = Sample(
    "2026-08-10T19:36:15.000Z", 150, 15_000, 4, 13, 1162);
Id65V3TelemetryParseResult unsafeFallInterior =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        unsafeFallInteriorLines,
        string.Empty));
Require(
    !Id65V3RuntimeEvidenceContract.FallBindingIsAccepted(
        unsafeFallInterior,
        "2026-08-10T19:36:00.000Z",
        300),
    "Fall authorization admitted an unsafe intervening sample.");

Id65V3TelemetryParseResult UnsafeDeathVariant(int ordinal, int lives, int state, int z)
{
    string[] changed = [.. deathLines];
    changed[ordinal] = Sample(
        $"2026-08-10T19:35:00.{ordinal}00Z", ordinal, ordinal * 100L, lives, state, z);
    return Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(changed, string.Empty));
}
foreach ((string Label, int Lives, int State, int Z) variant in new[]
{
    ("post-bound state4", 3, 4, 1200),
    ("post-bound lives2", 2, 0, 1200),
    ("post-bound low-Z", 3, 0, 900)
})
{
    Id65V3TelemetryParseResult changed = UnsafeDeathVariant(
        7, variant.Lives, variant.State, variant.Z);
    Id65V3TelemetrySummary summary =
        Id65V3RuntimeEvidenceContract.RecomputeTelemetrySummary(changed, deathSession);
    Require(!Id65V3RuntimeEvidenceContract.CandidateDeathTelemetryIsAccepted(
            changed, deathSession, summary.DeathEpisode),
        $"Scoped death acceptance admitted {variant.Label} after respawn binding.");
}
string[] skippedUnsafeRespawnLines = [.. deathLines];
skippedUnsafeRespawnLines[6] = Sample(
    "2026-08-10T19:35:00.600Z", 6, 600, 3, 4, 1200);
Id65V3TelemetryParseResult skippedUnsafeRespawn =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        skippedUnsafeRespawnLines,
        string.Empty));
Require(
    !Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        skippedUnsafeRespawn,
        3,
        "2026-08-10T19:35:00.600Z",
        7),
    "Respawn binding skipped an unsafe first post-marker sample for a later safe sample.");
Id65V3TelemetryParseResult exactMarkerRecovery =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        deathLines[..6],
        string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        exactMarkerRecovery,
        3,
        "2026-08-10T19:35:00.500Z",
        5),
    "Respawn binding rejected S=R=N.");
string[] lateRecoveryLines = [.. deathLines];
lateRecoveryLines[5] = Sample(
    "2026-08-10T19:35:00.500Z", 5, 500, 3, 4, 900);
Id65V3TelemetryParseResult lateRecovery =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        lateRecoveryLines[..6],
        string.Empty));
Require(
    !Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        lateRecovery,
        3,
        "2026-08-10T19:35:00.500Z",
        5),
    "Respawn binding admitted S after R=N.");
string[] recoveryLimitLines =
[
    Sample("2026-08-10T19:37:00.000Z", 0, 0, 4, 0, 1162),
    .. Enumerable.Range(1, 150).Select(ordinal => Sample(
        FormatUtc(ParseUtc("2026-08-10T19:37:00.000Z").AddMilliseconds(ordinal * 100L)),
        ordinal,
        ordinal * 100L,
        3,
        4,
        882)),
    Sample("2026-08-10T19:37:15.100Z", 151, 15_100, 3, 0, 1200)
];
Id65V3TelemetryParseResult exactRecoveryLimit =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        recoveryLimitLines,
        string.Empty));
string[] afterRecoveryLimitLines = [.. recoveryLimitLines];
afterRecoveryLimitLines[151] = Sample(
    "2026-08-10T19:37:15.101Z", 151, 15_101, 3, 0, 1200);
Id65V3TelemetryParseResult afterRecoveryLimit =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
        afterRecoveryLimitLines,
        string.Empty));
Require(
    Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        exactRecoveryLimit, 1, "2026-08-10T19:37:15.100Z", 151) &&
    !Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        afterRecoveryLimit, 1, "2026-08-10T19:37:15.101Z", 151) &&
    !Id65V3RuntimeEvidenceContract.RespawnBindingIsAccepted(
        exactRecoveryLimit, 1, "2026-08-10T19:37:15.100Z", 152),
    "Respawn binding did not enforce exact 15,000ms recovery or out-of-range R>N refusal.");

Id65V3TelemetryParseResult coldResetParsed =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
    [
        Sample("2026-08-10T19:35:01.000Z", 0, 0, 0, 13, 0),
        Sample("2026-08-10T19:35:01.100Z", 1, 100, 4, 1, 1200),
        Sample("2026-08-10T19:35:01.200Z", 2, 200, 4, 0, 1200),
        Sample("2026-08-10T19:35:01.300Z", 3, 300, 4, 0, 1200)
    ], string.Empty));
Id65V3RuntimeEvidenceSession coldResetSession = SyntheticSession(2, null, null, null, null);
Require(
    Id65V3RuntimeEvidenceContract.CandidateColdResetTelemetryIsAccepted(
        coldResetParsed,
        coldResetSession),
    "Cold-reset telemetry incorrectly treated valid pre-landing noise as post-landing failure.");
Id65V3TelemetryParseResult unsafeColdReset =
    Id65V3RuntimeEvidenceContract.ParseTelemetryLog(WatcherLog(
    [
        Sample("2026-08-10T19:35:01.000Z", 0, 0, 0, 13, 0),
        Sample("2026-08-10T19:35:01.100Z", 1, 100, 4, 1, 1200),
        Sample("2026-08-10T19:35:01.200Z", 2, 200, 4, 0, 1200),
        Sample("2026-08-10T19:35:01.300Z", 3, 300, 4, 4, 1200)
    ], string.Empty));
Require(
    !Id65V3RuntimeEvidenceContract.CandidateColdResetTelemetryIsAccepted(
        unsafeColdReset,
        coldResetSession),
    "Cold-reset telemetry admitted an unsafe post-landing sample.");

int telemetryRejects = 0;
RejectTelemetry("duplicate ordinal", [stableLines[0], ReplaceOnce(stableLines[1], "sample=1", "sample=0")]);
RejectTelemetry("skipped ordinal", [stableLines[0], ReplaceOnce(stableLines[1], "sample=1", "sample=2")]);
RejectTelemetry("equal elapsed", [stableLines[0], ReplaceOnce(stableLines[1], "elapsed-ms=100", "elapsed-ms=0")]);
RejectTelemetry("regressing elapsed", [ReplaceOnce(stableLines[0], "elapsed-ms=0", "elapsed-ms=101"), stableLines[1]]);
RejectTelemetry("duplicate UTC", [stableLines[0], ReplaceOnce(stableLines[1], "19:34:56.100", "19:34:56.000")]);
RejectTelemetry("regressing UTC", [ReplaceOnce(stableLines[0], "19:34:56.000", "19:34:56.200"), stableLines[1]]);
RejectTelemetry("UTC/elapsed clock divergence", [
    stableLines[0],
    ReplaceOnce(stableLines[1], "elapsed-ms=100", "elapsed-ms=1000")]);
RejectTelemetry("offset timestamp", [ReplaceOnce(stableLines[0], "Z sample", "-07:00 sample")]);
RejectTelemetry("raw mismatch", [ReplaceOnce(stableLines[0], "raw=04000000", "raw=03000000")]);
RejectTelemetry("state label mismatch", [ReplaceOnce(stableLines[0], "gameplay/non-death", "death-respawn")]);
RejectTelemetry("Z marker mismatch", [ReplaceOnce(stableLines[0], "Z<0x400:no", "Z<0x400:YES")]);
RejectTelemetry("ERROR banner", ["ERROR: injected"]);
RejectTelemetry("self-test banner", ["WATCH_SPYRO_ID65_RUNTIME_STATE_SELF_TEST_OK"]);
RejectTelemetryText("missing trailer", WatcherLog(stableLines, string.Empty).Replace("\n\n" + Stopped + "\n", "\n", StringComparison.Ordinal));
RejectTelemetryText("extra trailer text", WatcherLog(stableLines, string.Empty) + "extra\n");
RejectTelemetryText("changed preamble", ReplaceOnce(WatcherLog(stableLines, string.Empty), Warning, Warning + " changed"));
RejectTelemetryText("CRLF sample", ReplaceOnce(WatcherLog(stableLines, string.Empty), stableLines[0] + "\n", stableLines[0] + "\r\n"));
Require(telemetryRejects == 17, "The final watcher grammar reject matrix did not execute completely.");

Id65V3RuntimeEvidenceContract.GuardArtifactRelativePath(
    repositoryRoot,
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/example.log");
int pathRejects = 0;
foreach (string badPath in new[]
{
    "/tmp/example.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/../example.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/./example.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3//example.log",
    "docs\\runtime-evidence\\assets\\example.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-collision-winding-repair-v2-bad/example.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/example|bad.log",
    "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/example\nbad.log"
})
{
    try
    {
        Id65V3RuntimeEvidenceContract.GuardArtifactRelativePath(repositoryRoot, badPath);
        throw new InvalidOperationException($"Artifact path guard accepted '{badPath}'.");
    }
    catch (InvalidDataException)
    {
        pathRejects++;
    }
}
Require(pathRejects == 8, "The repository-relative artifact path reject matrix did not execute completely.");

int fullPassTamperRejects = 0;
int captureJournalTamperRejects = 0;
int processFingerprintRejects = 0;
int finalizerRefusals = 0;
string fixtureRoot = Path.Combine(
    "/private/tmp",
    "spyro-id65-v3-runtime-contract-smoke-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtureRoot);
try
{
    CompleteFixture fixture = BuildCompleteFixture(
        repositoryRoot,
        fixtureRoot,
        pending,
        completeSafeProfile);
    Id65V3RuntimeEvidenceValidation fullPassValidation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(fixture.Package),
            fixtureRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            fixture.ReadbackContext);
    Require(fullPassValidation.StructurallyValid &&
            fullPassValidation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass &&
            fullPassValidation.Package?.DerivedResult is
            {
                SourceValid: true,
                PinnedRuntimeFilesReadbackValid: true,
                RuntimeProfileValid: true,
                ArtifactReadbackValid: true,
                DiscIdentityValid: true,
                TelemetryValid: true,
                VisualChecklistComplete: true,
                ColdResetValid: true,
                RetailControlsValid: true,
                EnvironmentHeaderRepairV3RuntimeAccepted: true,
                RuntimeEvidenceAccepted: true,
                ProcessUseCryptographicallyProven: false
            },
        "The complete seven-session temp-root fixture did not reach the focused-runtime-pass evaluator: " +
        string.Join(" | ", fullPassValidation.Errors));

    string[] unicodeChangedPaths = ["cache/\ue000.bin", "cache/\U0001F600.bin"];
    Id65V3RuntimeEvidenceProfile unicodeChangedProfile = completeSafeProfile with
    {
        ChangedProfilePaths = unicodeChangedPaths
    };
    CompleteFixture unicodeChangedFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "unicode-changed-path-order"),
        pending,
        unicodeChangedProfile,
        exerciseUnicodeChangedPaths: true);
    Id65V3RuntimeEvidenceValidation unicodeChangedValidation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(unicodeChangedFixture.Package),
            unicodeChangedFixture.ArtifactRepositoryRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            unicodeChangedFixture.ReadbackContext);
    Require(unicodeChangedValidation.StructurallyValid &&
            unicodeChangedValidation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass,
        "Python-scalar ordered E000/astral ChangedProfilePaths were not accepted: " +
        string.Join(" | ", unicodeChangedValidation.Errors));
    RejectFullReadback(
        "UTF-16 ordered changed profile paths",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            unicodeChangedFixture.Package with
            {
                RuntimeProfile = unicodeChangedFixture.Package.RuntimeProfile with
                {
                    ChangedProfilePaths = unicodeChangedPaths.Reverse().ToArray()
                }
            }),
        unicodeChangedFixture,
        "Observed DuckStation profile violates a pinned safety boundary",
        allowProjectionFallback: false);

    CompleteFixture tiedClockFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "within-session-utc-ties"),
        pending,
        completeSafeProfile,
        exerciseWithinSessionUtcTies: true);
    Id65V3RuntimeEvidenceValidation tiedClockValidation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(tiedClockFixture.Package),
            tiedClockFixture.ArtifactRepositoryRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            tiedClockFixture.ReadbackContext);
    Require(tiedClockValidation.StructurallyValid &&
            tiedClockValidation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass,
        "The complete tied-UTC journal fixture was not accepted: " +
        string.Join(" | ", tiedClockValidation.Errors));

    CompleteFixture originThresholdFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "origin-clock-threshold"),
        pending,
        completeSafeProfile,
        runOriginMonotonicOffsetMicroseconds: -250_000);
    Id65V3RuntimeEvidenceValidation originThresholdValidation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(
                originThresholdFixture.Package),
            originThresholdFixture.ArtifactRepositoryRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            originThresholdFixture.ReadbackContext);
    Require(originThresholdValidation.StructurallyValid &&
            originThresholdValidation.State == Id65V3RuntimeEvidenceState.FocusedRuntimePass,
        "The exact 250 ms run-origin drift boundary was not accepted: " +
        string.Join(" | ", originThresholdValidation.Errors));

    CompleteFixture originDriftFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "origin-clock-drift"),
        pending,
        completeSafeProfile,
        runOriginMonotonicOffsetMicroseconds: -250_001);
    RejectFullReadback(
        "run-origin clock drift above 250 ms",
        originDriftFixture.Package,
        originDriftFixture,
        "run-origin clock does not strictly precede",
        allowProjectionFallback: false);

    CompleteFixture originTieFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "origin-clock-tie"),
        pending,
        completeSafeProfile,
        runOriginUtcOffsetMilliseconds: 2_000);
    RejectFullReadback(
        "run-origin UTC tie",
        originTieFixture.Package,
        originTieFixture,
        "run-origin clock does not strictly precede",
        allowProjectionFallback: false);

    CompleteFixture profileBracketFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "profile-before-process-bracket"),
        pending,
        completeSafeProfile,
        profileBeforeCapturedMilliseconds: 100);
    RejectFullReadback(
        "profile-before capture does not precede process",
        profileBracketFixture.Package,
        profileBracketFixture,
        "profile clocks do not bookend the process",
        allowProjectionFallback: false);

    CompleteFixture processBracketFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "process-start-session-bracket"),
        pending,
        completeSafeProfile,
        processStartedMilliseconds: 1_000);
    RejectFullReadback(
        "process start does not precede session-open",
        processBracketFixture.Package,
        processBracketFixture,
        "does not bracket one cold process",
        allowProjectionFallback: false);

    CompleteFixture recordingBoundaryFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "recording-start-crosses-readiness"),
        pending,
        completeSafeProfile,
        primaryRecordingStartMilliseconds: 4_100);
    RejectFullReadback(
        "fully rechained recording start crosses assisted readiness suffix",
        recordingBoundaryFixture.Package,
        recordingBoundaryFixture,
        "Primary landing evidence lacks the exact automated",
        allowProjectionFallback: false);

    Id65V3RuntimeEvidenceOperatorReview fixtureReview = fixture.Package.OperatorReview ??
        throw new InvalidOperationException("Complete fixture lacks its operator review.");
    string forgedAttestationHash = MutateNibble(fixtureReview.AttestationSha256);
    RejectFullReadback(
        "embedded operator-review attestation hash self-consistency",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with
            {
                EvidenceId = "id65-v3-focused-runtime-pass-attestation-" + forgedAttestationHash,
                OperatorReview = fixtureReview with { AttestationSha256 = forgedAttestationHash }
            }),
        fixture,
        "reconstructed exact external attestation");

    Id65V3RuntimeEvidenceCheck telemetryOverclaimCheck = fixture.Package.VisualChecks.Single(item =>
        item.CheckId == "candidate.lone-pad-visibility") with
    {
        TelemetryCorroborated = true
    };
    string telemetryOverclaimBinding = Id65V3RuntimeEvidenceFinalizer.RecomputeEvidenceBindingSha256(
        fixture.Package.CaptureProvenance!.CaptureClosureSha256,
        telemetryOverclaimCheck,
        fixture.Package.ArtifactCatalog);
    Id65V3RuntimeEvidenceOperatorReview telemetryOverclaimReview = fixtureReview with
    {
        Checks = fixtureReview.Checks.Select(item =>
            item.CheckId == telemetryOverclaimCheck.CheckId
                ? item with { EvidenceBindingSha256 = telemetryOverclaimBinding }
                : item).ToArray()
    };
    string telemetryOverclaimAttestationSha =
        Id65V3RuntimeEvidenceFinalizer.RecomputeEmbeddedAttestationSha256(telemetryOverclaimReview);
    telemetryOverclaimReview = telemetryOverclaimReview with
    {
        AttestationSha256 = telemetryOverclaimAttestationSha
    };
    RejectFullReadback(
        "rehash-consistent telemetry overclaim",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with
            {
                EvidenceId = "id65-v3-focused-runtime-pass-attestation-" +
                    telemetryOverclaimAttestationSha,
                VisualChecks = fixture.Package.VisualChecks.Select(item =>
                    item.CheckId == telemetryOverclaimCheck.CheckId ? telemetryOverclaimCheck : item)
                    .ToArray(),
                OperatorReview = telemetryOverclaimReview
            }),
        fixture,
        "telemetry-corroborated flag is not exact");

    const string edgeTokenCheckId = "candidate.primary-edge-4-to-3-same-pad-yaw";
    const string edgeTokenOmissionStatement =
        "Synthetic operator explicitly reviewed the bound " + edgeTokenCheckId +
        " fixture evidence.";
    Id65V3RuntimeEvidenceCheck edgeTokenOmissionCheck =
        fixture.Package.VisualChecks.Single(item => item.CheckId == edgeTokenCheckId) with
        {
            Attestation = edgeTokenOmissionStatement
        };
    string edgeTokenOmissionBinding =
        Id65V3RuntimeEvidenceFinalizer.RecomputeEvidenceBindingSha256(
            fixture.Package.CaptureProvenance!.CaptureClosureSha256,
            edgeTokenOmissionCheck,
            fixture.Package.ArtifactCatalog);
    Id65V3RuntimeEvidenceOperatorReview edgeTokenOmissionReview = fixtureReview with
    {
        Checks = fixtureReview.Checks.Select(item => item.CheckId == edgeTokenCheckId
            ? item with
            {
                EvidenceBindingSha256 = edgeTokenOmissionBinding,
                Statement = edgeTokenOmissionStatement
            }
            : item).ToArray()
    };
    string edgeTokenOmissionAttestationSha =
        Id65V3RuntimeEvidenceFinalizer.RecomputeEmbeddedAttestationSha256(
            edgeTokenOmissionReview);
    edgeTokenOmissionReview = edgeTokenOmissionReview with
    {
        AttestationSha256 = edgeTokenOmissionAttestationSha
    };
    RejectFullReadback(
        "rehash-consistent edge assignment token omission",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with
            {
                EvidenceId = "id65-v3-focused-runtime-pass-attestation-" +
                    edgeTokenOmissionAttestationSha,
                VisualChecks = fixture.Package.VisualChecks.Select(item =>
                        item.CheckId == edgeTokenCheckId ? edgeTokenOmissionCheck : item)
                    .ToArray(),
                OperatorReview = edgeTokenOmissionReview
            }),
        fixture,
        "edge-assignment token");

    Id65V3RuntimeEvidenceFinalizationPreview finalizerPreview =
        Id65V3RuntimeEvidenceFinalizer.Inspect(
            fixtureRoot,
            "id65v3-20260810t190000z-0123456789abcdef",
            fixture.ReadbackContext);
    Require(finalizerPreview.ArtifactCount == 43 && finalizerPreview.SessionCount == 7 &&
            finalizerPreview.CheckCount == 23 && finalizerPreview.Checks.Length == 23,
        "The finalizer did not inspect the exact 43/7/23 coordinator-shaped fixture.");
    const string finalizerRunId = "id65v3-20260810t190000z-0123456789abcdef";
    string finalizerAttestedAtUtc = "2026-08-10T20:00:00.000Z";
    Id65V3RuntimeHumanAttestation finalizerAttestation = new(
        Id65V3RuntimeEvidenceFinalizer.HumanAttestationFormat,
        Id65V3RuntimeEvidenceFinalizer.HumanAttestationFormatVersion,
        finalizerRunId,
        finalizerPreview.RunOriginSha256,
        finalizerPreview.FinalTerminalEventSha256,
        finalizerPreview.CaptureClosureSha256,
        "synthetic-finalizer-smoke-operator",
        finalizerAttestedAtUtc,
        new(
            false,
            false,
            false,
            0,
            "The synthetic operator reviewed the exact safe runtime profile and found no save or cheat use."),
        new[]
        {
            "candidate-primary-edge-0", "candidate-edge-1", "candidate-edge-2",
            "candidate-cold-reset", "retail-town-square", "retail-gnastys-loot",
            "retail-sunny-flight"
        }.Select(sessionId => new Id65V3RuntimeHumanSessionAttestation(
            sessionId,
            true,
            true,
            true,
            false,
            false,
            false,
            $"The synthetic operator reviewed the complete safe cold-run session {sessionId}."))
        .ToArray(),
        finalizerPreview.Checks.Select(check => new Id65V3RuntimeHumanCheckAttestation(
            check.CheckId,
            check.SessionId,
            "passed",
            check.EvidenceBindingSha256,
            SyntheticCheckAttestation(check.CheckId)))
        .ToArray());
    byte[] finalizerAttestationBytes =
        Id65V3RuntimeEvidenceFinalizer.SerializeCanonicalAttestation(finalizerAttestation);
    string finalizerAttestationPath = Path.Combine(fixtureRoot, "external-human-attestation.json");
    File.WriteAllBytes(finalizerAttestationPath, finalizerAttestationBytes);
    File.SetUnixFileMode(
        finalizerAttestationPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite);
    string finalizerAttestationSha256 = Hash(finalizerAttestationBytes);
    Id65V3RuntimeEvidenceFinalizationResult finalized =
        Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext);
    Require(!finalized.ExistingIdentical && finalized.ArtifactCount == 43 &&
            finalized.SessionCount == 7 && finalized.CheckCount == 23 &&
            finalized.PublisherPinBoundary == "PENDING",
        "The real finalizer did not atomically publish the exact focused-pass fixture while preserving PENDING.");
    Id65V3RuntimeEvidenceFinalizationResult verified =
        Id65V3RuntimeEvidenceFinalizer.VerifyExisting(fixtureRoot, fixture.ReadbackContext);
    Id65V3RuntimeEvidenceFinalizationResult idempotent =
        Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext);
    Require(verified.EvidenceSha256 == finalized.EvidenceSha256 &&
            idempotent.EvidenceSha256 == finalized.EvidenceSha256 &&
            verified.ExistingIdentical && idempotent.ExistingIdentical,
        "Verify/idempotent finalization did not preserve the exact canonical evidence bytes.");
    string finalizerEvidencePath = Path.Combine(
        fixtureRoot,
        Id65V3RuntimeEvidenceContract.FocusedRuntimeEvidenceRelativePath);
    Require(!Directory.EnumerateFiles(Path.GetDirectoryName(finalizerEvidencePath)!,
                ".id65-v3-runtime-evidence.tmp-*", SearchOption.TopDirectoryOnly).Any(),
        "Successful finalization left same-directory transaction debris.");

    void ExpectFinalizerRefusal(string name, Action action, string expectedFragment)
    {
        try
        {
            action();
            throw new InvalidOperationException($"Finalizer tamper '{name}' was accepted.");
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            Require(exception.Message.Contains(expectedFragment, StringComparison.OrdinalIgnoreCase),
                $"Finalizer tamper '{name}' missed gate '{expectedFragment}': {exception.Message}");
            finalizerRefusals++;
        }
    }

    void WriteAttestationBytes(byte[] bytes)
    {
        File.WriteAllBytes(finalizerAttestationPath, bytes);
        File.SetUnixFileMode(
            finalizerAttestationPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    void RejectAttestation(
        string name,
        Id65V3RuntimeHumanAttestation value,
        string expectedFragment)
    {
        byte[] bytes = Id65V3RuntimeEvidenceFinalizer.SerializeCanonicalAttestation(value);
        WriteAttestationBytes(bytes);
        ExpectFinalizerRefusal(
            name,
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                Hash(bytes),
                fixture.ReadbackContext),
            expectedFragment);
    }

    ExpectFinalizerRefusal(
        "attestation expected hash",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            MutateNibble(finalizerAttestationSha256),
            fixture.ReadbackContext),
        "SHA-256");
    byte[] noncanonicalAttestation = [.. finalizerAttestationBytes, (byte)'\n'];
    WriteAttestationBytes(noncanonicalAttestation);
    ExpectFinalizerRefusal(
        "attestation noncanonical trailing LF",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            Hash(noncanonicalAttestation),
            fixture.ReadbackContext),
        "canonical");
    byte[] malformedAttestation = Encoding.UTF8.GetBytes("{\"format\":");
    WriteAttestationBytes(malformedAttestation);
    ExpectFinalizerRefusal(
        "attestation truncated JSON is a clean refusal",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            Hash(malformedAttestation),
            fixture.ReadbackContext),
        "malformed JSON");
    string attestationJson = Encoding.UTF8.GetString(finalizerAttestationBytes);
    byte[] duplicateAttestation = Encoding.UTF8.GetBytes(
        attestationJson.Insert(1, $"\"format\":\"{Id65V3RuntimeEvidenceFinalizer.HumanAttestationFormat}\","));
    WriteAttestationBytes(duplicateAttestation);
    ExpectFinalizerRefusal(
        "attestation duplicate property",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            Hash(duplicateAttestation),
            fixture.ReadbackContext),
        "repeats JSON property");
    RejectAttestation(
        "attestation wrong run origin",
        finalizerAttestation with { RunOriginSha256 = MutateNibble(finalizerPreview.RunOriginSha256) },
        "capture identity");
    RejectAttestation(
        "attestation wrong terminal",
        finalizerAttestation with
        {
            FinalTerminalEventSha256 = MutateNibble(finalizerPreview.FinalTerminalEventSha256)
        },
        "capture identity");
    RejectAttestation(
        "attestation wrong capture closure",
        finalizerAttestation with
        {
            CaptureClosureSha256 = MutateNibble(finalizerPreview.CaptureClosureSha256)
        },
        "capture identity");
    RejectAttestation(
        "attestation reordered sessions",
        finalizerAttestation with { Sessions = finalizerAttestation.Sessions.Reverse().ToArray() },
        "ordered sessions");
    RejectAttestation(
        "attestation reordered checks",
        finalizerAttestation with { Checks = finalizerAttestation.Checks.Reverse().ToArray() },
        "ordered checks");
    RejectAttestation(
        "attestation wrong per-check binding",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select((item, index) => index == 0
                ? item with { EvidenceBindingSha256 = MutateNibble(item.EvidenceBindingSha256) }
                : item).ToArray()
        },
        "machine evidence binding");
    RejectAttestation(
        "attestation failed check",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select((item, index) => index == 0
                ? item with { Status = "failed" }
                : item).ToArray()
        },
        "Pass-only");
    RejectAttestation(
        "attestation inconclusive check",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select((item, index) => index == 0
                ? item with { Status = "inconclusive" }
                : item).ToArray()
        },
        "Pass-only");
    RejectAttestation(
        "attestation blank check statement",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select((item, index) => index == 0
                ? item with { Statement = string.Empty }
                : item).ToArray()
        },
        "nonempty text");
    RejectAttestation(
        "attestation missing exact physical edge-assignment token",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select(item =>
                item.CheckId == edgeTokenCheckId
                    ? item with { Statement = edgeTokenOmissionStatement }
                    : item).ToArray()
        },
        "edge-assignment token");
    RejectAttestation(
        "attestation blanket session statement",
        finalizerAttestation with
        {
            Sessions = finalizerAttestation.Sessions.Select((item, index) => index == 0
                ? item with { Statement = "The operator reviewed this complete safe cold-run session." }
                : item).ToArray()
        },
        "blanket");
    RejectAttestation(
        "attestation blanket check statement",
        finalizerAttestation with
        {
            Checks = finalizerAttestation.Checks.Select((item, index) => index == 0
                ? item with { Statement = "The operator reviewed all bound media for this observation." }
                : item).ToArray()
        },
        "blanket");
    RejectAttestation(
        "attestation placeholder safety statement",
        finalizerAttestation with
        {
            RuntimeSafety = finalizerAttestation.RuntimeSafety with { Statement = "placeholder text" }
        },
        "placeholder");
    RejectAttestation(
        "attestation unsafe save-state assertion",
        finalizerAttestation with
        {
            RuntimeSafety = finalizerAttestation.RuntimeSafety with { SaveStateLoaded = true }
        },
        "unsafe runtime-safety");
    RejectAttestation(
        "attestation unsafe session assertion",
        finalizerAttestation with
        {
            Sessions = finalizerAttestation.Sessions.Select((item, index) => index == 0
                ? item with { NewGame = false }
                : item).ToArray()
        },
        "safe cold-run");
    WriteAttestationBytes(finalizerAttestationBytes);
    File.SetUnixFileMode(
        finalizerAttestationPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead |
        UnixFileMode.OtherRead);
    ExpectFinalizerRefusal(
        "attestation unsafe mode",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext),
        "mode 600");
    WriteAttestationBytes(finalizerAttestationBytes);
    string attestationHardlink = Path.Combine(fixtureRoot, "attestation-hardlink.json");
    SmokeDarwin.CreateHardLink(attestationHardlink, finalizerAttestationPath);
    try
    {
        ExpectFinalizerRefusal(
            "attestation hardlink",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "st_nlink == 1");
    }
    finally
    {
        File.Delete(attestationHardlink);
    }
    string attestationSymlink = Path.Combine(fixtureRoot, "attestation-symlink.json");
    File.CreateSymbolicLink(attestationSymlink, finalizerAttestationPath);
    try
    {
        ExpectFinalizerRefusal(
            "attestation symlink",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                attestationSymlink,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "symlink");
    }
    finally
    {
        File.Delete(attestationSymlink);
    }

    string finalizerRunPath = Path.Combine(
        fixtureRoot,
        Id65V3RuntimeEvidenceFinalizer.CaptureStagingPrefix,
        finalizerRunId);
    File.SetUnixFileMode(
        finalizerRunPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
    ExpectFinalizerRefusal(
        "run directory mode",
        () => Id65V3RuntimeEvidenceFinalizer.Inspect(
            fixtureRoot,
            finalizerRunId,
            fixture.ReadbackContext),
        "mode 700");
    File.SetUnixFileMode(
        finalizerRunPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    string primaryWatcherPath = fixture.ArtifactPaths["candidate-primary-edge-0.watcher"];
    File.SetUnixFileMode(
        primaryWatcherPath,
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
    ExpectFinalizerRefusal(
        "raw artifact mode",
        () => Id65V3RuntimeEvidenceFinalizer.Inspect(
            fixtureRoot,
            finalizerRunId,
            fixture.ReadbackContext),
        "mode 600");
    File.SetUnixFileMode(primaryWatcherPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    string watcherHardlink = Path.Combine(fixtureRoot, "watcher-hardlink.log");
    SmokeDarwin.CreateHardLink(watcherHardlink, primaryWatcherPath);
    try
    {
        ExpectFinalizerRefusal(
            "raw artifact hardlink",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "st_nlink == 1");
    }
    finally
    {
        File.Delete(watcherHardlink);
    }
    string primarySessionPath = Path.GetDirectoryName(primaryWatcherPath)!;
    string sessionDebris = Path.Combine(primarySessionPath, "unexpected-debris.txt");
    File.WriteAllText(sessionDebris, "owned smoke debris");
    File.SetUnixFileMode(sessionDebris, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    try
    {
        ExpectFinalizerRefusal(
            "session debris",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "missing, extra, or renamed");
    }
    finally
    {
        File.Delete(sessionDebris);
    }
    string runOriginPath = Path.Combine(finalizerRunPath, "run-origin.json");
    byte[] originalOriginBytes = File.ReadAllBytes(runOriginPath);
    try
    {
        File.WriteAllBytes(runOriginPath, [.. originalOriginBytes, (byte)' ']);
        ExpectFinalizerRefusal(
            "run-origin noncanonical append",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "canonical");
    }
    finally
    {
        File.WriteAllBytes(runOriginPath, originalOriginBytes);
    }
    try
    {
        JsonObject legacyV1Origin = JsonNode.Parse(
            Encoding.UTF8.GetString(originalOriginBytes))?.AsObject() ??
            throw new InvalidOperationException("Synthetic run origin did not parse as an object.");
        legacyV1Origin["formatVersion"] = 1;
        File.WriteAllBytes(runOriginPath, CanonicalArtifactUtf8(legacyV1Origin));
        ExpectFinalizerRefusal(
            "legacy V1 run-origin version",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "run origin identity");
    }
    finally
    {
        File.WriteAllBytes(runOriginPath, originalOriginBytes);
    }
    try
    {
        JsonObject retiredV2Origin = JsonNode.Parse(
            Encoding.UTF8.GetString(originalOriginBytes))?.AsObject() ??
            throw new InvalidOperationException("Synthetic run origin did not parse as an object.");
        retiredV2Origin["format"] =
            "spyro-editor-id65-v2-runtime-capture-run-origin";
        File.WriteAllBytes(runOriginPath, CanonicalArtifactUtf8(retiredV2Origin));
        ExpectFinalizerRefusal(
            "retired V2 run-origin format",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "run origin identity");
    }
    finally
    {
        File.WriteAllBytes(runOriginPath, originalOriginBytes);
    }
    try
    {
        File.WriteAllBytes(runOriginPath, Encoding.UTF8.GetBytes("{\"format\":"));
        ExpectFinalizerRefusal(
            "run-origin truncated JSON is a clean refusal",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "malformed JSON");
    }
    finally
    {
        File.WriteAllBytes(runOriginPath, originalOriginBytes);
    }
    string primaryJournalPath = fixture.ArtifactPaths["candidate-primary-edge-0.capture-journal"];
    byte[] originalPrimaryJournalBytes = File.ReadAllBytes(primaryJournalPath);
    try
    {
        File.WriteAllBytes(primaryJournalPath, Encoding.UTF8.GetBytes("{\"events\":"));
        ExpectFinalizerRefusal(
            "capture journal truncated JSON is a clean refusal",
            () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                fixtureRoot,
                finalizerRunId,
                fixture.ReadbackContext),
            "malformed JSON");
    }
    finally
    {
        File.WriteAllBytes(primaryJournalPath, originalPrimaryJournalBytes);
    }
    string primaryLivePath = Path.Combine(primarySessionPath, "capture-events-live.ndjson");
    byte[] originalLiveBytes = File.ReadAllBytes(primaryLivePath);
    foreach ((string name, byte[] bytes) in new[]
    {
        ("live truncation", originalLiveBytes[..^1]),
        ("live appended line", originalLiveBytes.Concat(Encoding.UTF8.GetBytes("{}\n")).ToArray()),
        ("live CRLF", Encoding.UTF8.GetBytes(
            Encoding.UTF8.GetString(originalLiveBytes).Replace("\n", "\r\n", StringComparison.Ordinal)))
    })
    {
        try
        {
            File.WriteAllBytes(primaryLivePath, bytes);
            ExpectFinalizerRefusal(
                name,
                () => Id65V3RuntimeEvidenceFinalizer.Inspect(
                    fixtureRoot,
                    finalizerRunId,
                    fixture.ReadbackContext),
                "Live NDJSON");
        }
        finally
        {
            File.WriteAllBytes(primaryLivePath, originalLiveBytes);
        }
    }

    byte[] canonicalEvidenceBytes = File.ReadAllBytes(finalizerEvidencePath);
    File.WriteAllBytes(finalizerEvidencePath, Encoding.UTF8.GetBytes("{}"));
    ExpectFinalizerRefusal(
        "existing differing destination",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext),
        "different bytes");
    File.WriteAllBytes(finalizerEvidencePath, canonicalEvidenceBytes);

    File.Delete(finalizerEvidencePath);
    string finalizerEvidenceParent = Path.GetDirectoryName(finalizerEvidencePath)!;
    void RequireNoFinalizerTransactionOutput(string context)
    {
        Require(!File.Exists(finalizerEvidencePath) &&
                !Directory.EnumerateFiles(
                    finalizerEvidenceParent,
                    ".id65-v3-runtime-evidence.tmp-*",
                    SearchOption.TopDirectoryOnly).Any(),
            $"{context} created a temporary or canonical output.");
    }

    UnixFileMode originalRepositoryRootMode = File.GetUnixFileMode(fixtureRoot);
    try
    {
        File.SetUnixFileMode(
            fixtureRoot,
            originalRepositoryRootMode | UnixFileMode.GroupWrite);
        ExpectFinalizerRefusal(
            "group-writable repository root",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "unsafe group/other write permission");
        RequireNoFinalizerTransactionOutput("Unsafe repository-root mode refusal");
    }
    finally
    {
        File.SetUnixFileMode(fixtureRoot, originalRepositoryRootMode);
    }

    string writableInRepositoryAncestor = Path.Combine(fixtureRoot, "docs");
    UnixFileMode originalInRepositoryAncestorMode =
        File.GetUnixFileMode(writableInRepositoryAncestor);
    try
    {
        File.SetUnixFileMode(
            writableInRepositoryAncestor,
            originalInRepositoryAncestorMode | UnixFileMode.GroupWrite);
        ExpectFinalizerRefusal(
            "group-writable in-repository ancestor",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "unsafe group/other write permission");
        RequireNoFinalizerTransactionOutput("Unsafe in-repository ancestor refusal");
    }
    finally
    {
        File.SetUnixFileMode(
            writableInRepositoryAncestor,
            originalInRepositoryAncestorMode);
    }

    string externalAttestationDirectory = Path.Combine(
        "/private/tmp",
        "spyro-id65-v3-attestation-parent-" + Guid.NewGuid().ToString("N"));
    string externalAttestationPath = Path.Combine(
        externalAttestationDirectory,
        "external-human-attestation.json");
    Directory.CreateDirectory(externalAttestationDirectory);
    try
    {
        File.SetUnixFileMode(
            externalAttestationDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute);
        File.WriteAllBytes(externalAttestationPath, finalizerAttestationBytes);
        File.SetUnixFileMode(
            externalAttestationPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite);
        ExpectFinalizerRefusal(
            "group-writable external attestation parent",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                externalAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "unsafe group/other write permission");
        RequireNoFinalizerTransactionOutput("Unsafe external-attestation parent refusal");
    }
    finally
    {
        if (File.Exists(externalAttestationPath))
            File.Delete(externalAttestationPath);
        if (Directory.Exists(externalAttestationDirectory))
            Directory.Delete(externalAttestationDirectory);
    }

    UnixFileMode originalEvidenceParentMode = File.GetUnixFileMode(finalizerEvidenceParent);
    try
    {
        File.SetUnixFileMode(
            finalizerEvidenceParent,
            originalEvidenceParentMode | UnixFileMode.GroupWrite);
        ExpectFinalizerRefusal(
            "group-writable fixed evidence parent",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext),
            "unsafe group/other write permission");
        RequireNoFinalizerTransactionOutput("Unsafe evidence-parent mode refusal");
    }
    finally
    {
        File.SetUnixFileMode(finalizerEvidenceParent, originalEvidenceParentMode);
    }

    try
    {
        ExpectFinalizerRefusal(
            "staging mutation before final input readback",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext,
                new(
                    BeforeFinalInputReadback: () => File.WriteAllBytes(
                        primaryLivePath,
                        originalLiveBytes.Concat(Encoding.UTF8.GetBytes("{}\n")).ToArray()),
                    BeforeAtomicRename: null,
                    AfterAtomicRenameBeforeDirectoryFsync: null,
                    AfterAtomicRename: null)),
            "Live NDJSON");
        Require(!File.Exists(finalizerEvidencePath) &&
                !Directory.EnumerateFiles(Path.GetDirectoryName(finalizerEvidencePath)!,
                    ".id65-v3-runtime-evidence.tmp-*", SearchOption.TopDirectoryOnly).Any(),
            "Prepublication input mutation refusal published evidence or left a temporary file.");
    }
    finally
    {
        File.WriteAllBytes(primaryLivePath, originalLiveBytes);
    }

    byte[] originalExternalHomeSettings =
        File.ReadAllBytes(fixture.ReadbackContext.ExternalHomeSettingsPath);
    try
    {
        byte[] changedExternalHomeSettings = (byte[])originalExternalHomeSettings.Clone();
        changedExternalHomeSettings[0] ^= 0x01;
        ExpectFinalizerRefusal(
            "live-profile drift before final hook-free FullReadback",
            () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
                fixtureRoot,
                finalizerRunId,
                finalizerAttestationPath,
                finalizerAttestationSha256,
                fixture.ReadbackContext,
                new(
                    BeforeFinalInputReadback: null,
                    BeforeAtomicRename: _ => File.WriteAllBytes(
                        fixture.ReadbackContext.ExternalHomeSettingsPath,
                        changedExternalHomeSettings),
                    AfterAtomicRenameBeforeDirectoryFsync: null,
                    AfterAtomicRename: null)),
            "Current external-HOME settings bytes differ");
        Require(!File.Exists(finalizerEvidencePath),
            "Final hook-free FullReadback drift refusal published canonical evidence.");
    }
    finally
    {
        File.WriteAllBytes(
            fixture.ReadbackContext.ExternalHomeSettingsPath,
            originalExternalHomeSettings);
    }

    ExpectFinalizerRefusal(
        "atomic no-replace destination race",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: _ =>
                {
                    File.WriteAllBytes(finalizerEvidencePath, Encoding.UTF8.GetBytes("competitor"));
                    File.SetUnixFileMode(
                        finalizerEvidencePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                },
                AfterAtomicRenameBeforeDirectoryFsync: null,
                AfterAtomicRename: null)),
        "appeared before atomic rename");
    Require(Encoding.UTF8.GetString(File.ReadAllBytes(finalizerEvidencePath)) == "competitor" &&
            !Directory.EnumerateFiles(Path.GetDirectoryName(finalizerEvidencePath)!,
                ".id65-v3-runtime-evidence.tmp-*", SearchOption.TopDirectoryOnly).Any(),
        "Atomic no-replace refusal changed the competitor or left a temporary file.");
    File.Delete(finalizerEvidencePath);

    string? swappedTempPath = null;
    string? swappedOriginalPath = null;
    byte[] tempReplacement = Encoding.UTF8.GetBytes("temp-replacement-sentinel");
    ExpectFinalizerRefusal(
        "temp-path replacement preserves sentinel",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: tempPath =>
                {
                    swappedTempPath = tempPath;
                    swappedOriginalPath = tempPath + ".original";
                    File.Move(tempPath, swappedOriginalPath);
                    File.WriteAllBytes(tempPath, tempReplacement);
                    File.SetUnixFileMode(
                        tempPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                },
                AfterAtomicRenameBeforeDirectoryFsync: null,
                AfterAtomicRename: null)),
        "no longer names the created inode");
    Require(swappedTempPath is not null && File.Exists(swappedTempPath) &&
            File.ReadAllBytes(swappedTempPath).AsSpan().SequenceEqual(tempReplacement) &&
            swappedOriginalPath is not null && !File.Exists(swappedOriginalPath) &&
            !File.Exists(finalizerEvidencePath),
        "Temp-path swap refusal deleted the replacement, retained owned bytes, or published output.");
    File.Delete(swappedTempPath!);

    string? contestedTempPath = null;
    string? contestedOriginalPath = null;
    byte[] contestedTempReplacement = Encoding.UTF8.GetBytes("contested-temp-sentinel");
    byte[] contestedDestination = Encoding.UTF8.GetBytes("contested-destination-sentinel");
    ExpectFinalizerRefusal(
        "temp replacement plus competing destination preserves both",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: tempPath =>
                {
                    contestedTempPath = tempPath;
                    contestedOriginalPath = tempPath + ".original";
                    File.Move(tempPath, contestedOriginalPath);
                    File.WriteAllBytes(tempPath, contestedTempReplacement);
                    File.SetUnixFileMode(
                        tempPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    File.WriteAllBytes(finalizerEvidencePath, contestedDestination);
                    File.SetUnixFileMode(
                        finalizerEvidencePath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                },
                AfterAtomicRenameBeforeDirectoryFsync: null,
                AfterAtomicRename: null)),
        "no longer names the created inode");
    Require(contestedTempPath is not null && File.Exists(contestedTempPath) &&
            File.ReadAllBytes(contestedTempPath).AsSpan().SequenceEqual(contestedTempReplacement) &&
            contestedOriginalPath is not null && !File.Exists(contestedOriginalPath) &&
            File.ReadAllBytes(finalizerEvidencePath).AsSpan().SequenceEqual(contestedDestination),
        "Contested temp refusal deleted a replacement/competitor or retained owned bytes.");
    File.Delete(contestedTempPath!);
    File.Delete(finalizerEvidencePath);

    ExpectFinalizerRefusal(
        "post-rename pre-fsync failure rolls back own output",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: null,
                AfterAtomicRenameBeforeDirectoryFsync: () =>
                    throw new InvalidDataException("injected pre-fsync failure"),
                AfterAtomicRename: null)),
        "injected pre-fsync failure");
    Require(!File.Exists(finalizerEvidencePath),
        "Pre-fsync failure left the operation-owned canonical output behind.");

    ExpectFinalizerRefusal(
        "post-rename FullReadback failure rolls back own output",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: null,
                AfterAtomicRenameBeforeDirectoryFsync: null,
                AfterAtomicRename: _ =>
                    throw new InvalidDataException("injected post-rename readback failure"))),
        "injected post-rename readback failure");
    Require(!File.Exists(finalizerEvidencePath),
        "Post-rename readback failure left the operation-owned canonical output behind.");

    string? movedPublishedPath = null;
    byte[] destinationReplacement = Encoding.UTF8.GetBytes("destination-replacement-sentinel");
    ExpectFinalizerRefusal(
        "post-rename destination replacement is preserved",
        () => Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
            fixtureRoot,
            finalizerRunId,
            finalizerAttestationPath,
            finalizerAttestationSha256,
            fixture.ReadbackContext,
            new(
                BeforeFinalInputReadback: null,
                BeforeAtomicRename: null,
                AfterAtomicRenameBeforeDirectoryFsync: null,
                AfterAtomicRename: path =>
                {
                    movedPublishedPath = path + ".owned-original";
                    File.Move(path, movedPublishedPath);
                    File.WriteAllBytes(path, destinationReplacement);
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                })),
        "replacement was preserved");
    Require(File.Exists(finalizerEvidencePath) &&
            File.ReadAllBytes(finalizerEvidencePath).AsSpan().SequenceEqual(destinationReplacement) &&
            movedPublishedPath is not null && !File.Exists(movedPublishedPath),
        "Destination-drift rollback deleted the replacement or stranded owned published bytes.");
    File.Delete(finalizerEvidencePath);

    Require(!Directory.EnumerateFiles(Path.GetDirectoryName(finalizerEvidencePath)!,
            ".id65-v3-runtime-evidence.tmp-*", SearchOption.TopDirectoryOnly).Any() &&
            !Directory.EnumerateFiles(Path.GetDirectoryName(finalizerEvidencePath)!,
                "*.owned-original", SearchOption.TopDirectoryOnly).Any(),
        "Transaction refusal tests left operation-owned temporary/original debris.");

    finalized = Id65V3RuntimeEvidenceFinalizer.FinalizeFocusedPass(
        fixtureRoot,
        finalizerRunId,
        finalizerAttestationPath,
        finalizerAttestationSha256,
        fixture.ReadbackContext);
    Require(finalized.EvidenceSha256 == Hash(canonicalEvidenceBytes),
        "Finalizer did not recover deterministically after fault-injection smoke tests.");

    string primaryJournalId = "candidate-primary-edge-0.capture-journal";
    string edge1JournalId = "candidate-edge-1.capture-journal";
    string finalJournalId = "retail-sunny-flight.capture-journal";
    RejectJournalTamper(
        "journal unknown root property",
        fixture,
        primaryJournalId,
        root => root["unknown"] = true,
        rechain: false,
        "capture-journal root has an unknown or missing property");
    RejectJournalTamper(
        "journal legacy v1 format",
        fixture,
        primaryJournalId,
        root => root["formatVersion"] = 1,
        rechain: true,
        "header identity or source pin changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal legacy v2 format version",
        fixture,
        primaryJournalId,
        root => root["formatVersion"] = 2,
        rechain: true,
        "header identity or source pin changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal legacy capture-event-v1 hash domain",
        fixture,
        primaryJournalId,
        root =>
        {
            JsonObject first = root["events"]!.AsArray()[0]!.AsObject();
            first["eventSha256"] = CaptureEventHashNodeLegacyV1(
                root,
                first,
                first["payload"]!.AsObject());
        },
        rechain: false,
        "event hash changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal retired V2 inner format",
        fixture,
        primaryJournalId,
        root => root["format"] =
            "spyro-editor-id65-v2-runtime-capture-event-journal",
        rechain: true,
        "header identity or source pin changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal retired V2 run ID",
        fixture,
        primaryJournalId,
        root => root["runId"] =
            "id65v2-20260810t190000z-fedcba9876543210",
        rechain: true,
        "header identity or source pin changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal retired V2 process ID",
        fixture,
        primaryJournalId,
        root => root["processInstanceId"] =
            "id65v2-process-00000000000000000000000000000000",
        rechain: true,
        "header identity or source pin changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal source-tool pin",
        fixture,
        primaryJournalId,
        root => root["captureCoordinatorSha256"] = MutateNibble(
            root["captureCoordinatorSha256"]!.GetValue<string>()),
        rechain: false,
        "capture journal pin changed: captureCoordinatorSha256");
    RejectJournalTamper(
        "journal run binding",
        fixture,
        primaryJournalId,
        root => root["runId"] = "id65v3-20260810t190000z-fedcba9876543210",
        rechain: false,
        "typed capture-journal reference changed");
    RejectJournalTamper(
        "journal boot binding",
        fixture,
        primaryJournalId,
        root => root["bootIdentity"] = MutateNibble(root["bootIdentity"]!.GetValue<string>()),
        rechain: false,
        "typed capture-journal reference changed");
    RejectJournalTamper(
        "journal process binding",
        fixture,
        primaryJournalId,
        root => root["processInstanceId"] = "id65v3-process-00000000000000000000000000000000",
        rechain: false,
        "header identity or source pin changed");
    RejectJournalTamper(
        "journal dual-clock binding",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "disc-open-observed")["monotonicMicroseconds"] =
            CaptureEvent(root, "disc-open-observed")["monotonicMicroseconds"]!.GetValue<long>() + 1_000_000,
        rechain: false,
        "UTC and monotonic clocks diverged");
    RejectJournalTamper(
        "journal UTC regression",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "media-artifact-bound", 1)["atUtc"] = FormatUtc(
            ParseUtc(CaptureEvent(root, "media-artifact-bound", 0)["atUtc"]!
                .GetValue<string>()).AddMilliseconds(-1)),
        rechain: true,
        "UTC and monotonic clocks diverged",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal monotonic equality",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "media-artifact-bound", 1)["monotonicMicroseconds"] =
            CaptureEvent(root, "media-artifact-bound", 0)["monotonicMicroseconds"]!
                .GetValue<long>(),
        rechain: true,
        "UTC and monotonic clocks diverged",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal monotonic regression",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "media-artifact-bound", 1)["monotonicMicroseconds"] =
            CaptureEvent(root, "media-artifact-bound", 0)["monotonicMicroseconds"]!
                .GetValue<long>() - 1,
        rechain: true,
        "UTC and monotonic clocks diverged",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal previous-event chain",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "disc-open-observed")["previousEventSha256"] = new string('f', 64),
        rechain: false,
        "previous-event hash chain changed");
    RejectJournalTamper(
        "journal fully rechained event-array reorder",
        fixture,
        primaryJournalId,
        root => SwapCaptureEvents(
            root, "process-start-observed", "disc-open-observed"),
        rechain: true,
        "UTC and monotonic clocks diverged",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal fully rechained tied-clock FSM reorder",
        tiedClockFixture,
        primaryJournalId,
        root => SwapCaptureEventBodies(
            root, "watcher-connected", "manually-resumed"),
        rechain: true,
        "does not match its exact FSM",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal unknown event type",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "manually-resumed")["eventType"] = "resume-ish",
        rechain: false,
        "unknown event type 'resume-ish'");
    RejectJournalTamper(
        "journal marker FSM",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "landing-marked")["markerId"] = "wrong-landing",
        rechain: true,
        "does not match its exact FSM");
    RejectJournalTamper(
        "journal exact process argv",
        fixture,
        primaryJournalId,
        root => ((JsonArray)CaptureEventPayload(root, "process-start-observed")["argv"]!)
            .Add("/tmp/unpinned.bin"),
        rechain: true,
        "process argv is not exactly executable plus pinned CUE");
    RejectJournalTamper(
        "journal wrong CUE argv",
        fixture,
        primaryJournalId,
        root => ((JsonArray)CaptureEventPayload(root, "process-start-observed")["argv"]!)[1] =
            "/tmp/wrong.cue",
        rechain: true,
        "process argv is not exactly executable plus pinned CUE");
    RejectJournalTamper(
        "journal missing CUE argv",
        fixture,
        primaryJournalId,
        root => ((JsonArray)CaptureEventPayload(root, "process-start-observed")["argv"]!)
            .RemoveAt(1),
        rechain: true,
        "process argv is not exactly executable plus pinned CUE");
    RejectJournalTamper(
        "journal duplicate CUE argv",
        fixture,
        primaryJournalId,
        root =>
        {
            JsonArray argv = (JsonArray)CaptureEventPayload(
                root, "process-start-observed")["argv"]!;
            argv.Add(argv[1]!.GetValue<string>());
        },
        rechain: true,
        "process argv is not exactly executable plus pinned CUE");
    RejectJournalTamper(
        "journal argv NUL hash",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "process-start-observed")["argvNulSha256"] =
            new string('f', 64),
        rechain: true,
        "process argv byte hash changed");
    RejectJournalTamper(
        "journal legacy HOME-only environment NUL hash",
        fixture,
        primaryJournalId,
        root =>
        {
            JsonObject payload = CaptureEventPayload(root, "process-start-observed");
            string home = payload["externalHomeRealPath"]!.GetValue<string>();
            payload["environmentNulSha256"] = Hash(
                Encoding.UTF8.GetBytes($"HOME={home}\0"));
        },
        rechain: true,
        "exact environment byte hash changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal extra environment entry NUL hash",
        fixture,
        primaryJournalId,
        root =>
        {
            JsonObject payload = CaptureEventPayload(root, "process-start-observed");
            string home = payload["externalHomeRealPath"]!.GetValue<string>();
            string environment =
                $"HOME={home}\0" +
                "LANG=C\0" +
                "LC_ALL=C\0" +
                "PATH=/usr/bin:/bin:/usr/sbin:/sbin\0" +
                "SPYRO_ID65_CAPTURE_ENVIRONMENT_END_V1=duckstation-id65-unattended-v1\0" +
                "EXTRA=bad\0";
            payload["environmentNulSha256"] = Hash(Encoding.UTF8.GetBytes(environment));
        },
        rechain: true,
        "exact environment byte hash changed",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal executable vnode identity",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "process-start-observed")["executableInode"] =
            CaptureEventPayload(root, "process-start-observed")["executableInode"]!
                .GetValue<long>() + 1,
        rechain: true,
        "process executable length, vnode identity, or bytes changed");
    RejectJournalTamper(
        "journal listener pid",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["gdbListenerPid"] = 99_999,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal process start epoch projection",
        fixture,
        primaryJournalId,
        root =>
        {
            JsonObject payload = CaptureEventPayload(root, "process-start-observed");
            payload["startMicroseconds"] =
                payload["startMicroseconds"]!.GetValue<long>() + 1_000;
        },
        rechain: true,
        "start UTC is not derived from its kernel epoch fields");
    RejectJournalTamper(
        "journal legacy CUE-open injection",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueOpen"] = true,
        rechain: true,
        "unknown or missing property");
    RejectJournalTamper(
        "journal legacy CUE-fd injection",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueFd"] = "12r",
        rechain: true,
        "unknown or missing property");
    RejectJournalTamper(
        "journal disc binding mode",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["bindingMode"] = "legacy",
        rechain: true,
        "disc-open observation changed its fixed identity");
    RejectJournalTamper(
        "journal disc provider",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["provider"] =
            "macos-open-file-v1",
        rechain: true,
        "disc-open observation changed its fixed identity");
    RejectJournalTamper(
        "journal failed-v1 exclusion false",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["failedV1Excluded"] = false,
        rechain: true,
        "disc-open observation changed its fixed identity",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal CUE repo-relative path",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueRepoRelativePath"] =
            "wrong/candidate.cue",
        rechain: true,
        "disc-open observation changed its fixed identity",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal BIN repo-relative path",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binRepoRelativePath"] =
            "wrong/candidate.bin",
        rechain: true,
        "disc-open observation changed its fixed identity",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal CUE source path",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueRealPath"] =
            "/tmp/wrong.cue",
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal CUE source hash",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueSha256"] =
            new string('f', 64),
        rechain: true,
        "disc-open observation changed its fixed identity");
    RejectJournalTamper(
        "journal CUE source length",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueByteLength"] =
            CaptureEventPayload(root, "disc-open-observed")["cueByteLength"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal CUE source device",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueDevice"] =
            CaptureEventPayload(root, "disc-open-observed")["cueDevice"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal CUE source inode",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueInode"] =
            CaptureEventPayload(root, "disc-open-observed")["cueInode"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal CUE source link count",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["cueSourceLinkCount"] = 2,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal closed BIN claim",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binOpen"] = false,
        rechain: true,
        "disc-open observation changed its fixed identity");
    RejectJournalTamper(
        "journal malformed BIN descriptor",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binFd"] = "not-a-fd",
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal write-only disc descriptor",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binFd"] = "13w",
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal BIN descriptor path",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binRealPath"] =
            "/tmp/wrong.bin",
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal BIN descriptor inode",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binInode"] =
            CaptureEventPayload(root, "disc-open-observed")["binInode"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal BIN descriptor hash",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binSha256"] =
            new string('f', 64),
        rechain: true,
        "disc-open observation changed its fixed identity");
    RejectJournalTamper(
        "journal BIN descriptor length",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binByteLength"] =
            CaptureEventPayload(root, "disc-open-observed")["binByteLength"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal BIN descriptor device",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binDevice"] =
            CaptureEventPayload(root, "disc-open-observed")["binDevice"]!.GetValue<long>() + 1,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal BIN descriptor link count",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "disc-open-observed")["binLinkCount"] = 2,
        rechain: true,
        "disc FD, listener, inode projection, or pinned bytes changed");
    RejectJournalTamper(
        "journal fall watcher wrong direction",
        fixture,
        primaryJournalId,
        root =>
        {
            MoveCaptureOperatorMarker(root, "fall-started", 200);
            MoveCaptureOperatorMarker(root, "respawn-marked", 200);
        },
        rechain: true,
        "watcher binding changed its exact raw sample");
    RejectJournalTamper(
        "journal respawn watcher outside reaction tolerance",
        fixture,
        primaryJournalId,
        root => MoveCaptureOperatorMarker(root, "respawn-marked", 300),
        rechain: true,
        "watcher binding changed its exact raw sample");
    RejectJournalTamper(
        "journal respawn watcher skipped first directional sample",
        fixture,
        primaryJournalId,
        root => MoveCaptureWatcherBindingSample(root, "respawn-marked", 100),
        rechain: true,
        "first stable raw sample after its marker and fall");
    RejectJournalTamper(
        "journal recorder pid lifecycle",
        fixture,
        primaryJournalId,
        root => CaptureEventPayload(root, "recording-stopped")["ownedRecorderPid"] = 99_999,
        rechain: true,
        "recording lifecycle is reordered or out of bounds");
    RejectJournalTamper(
        "journal watcher sample binding",
        fixture,
        primaryJournalId,
        root => CaptureEvent(root, "watcher-marker-bound", occurrence: 0)
            ["payload"]!["sampleOrdinal"] = 0,
        rechain: true,
        "watcher binding changed its exact raw sample");
    RejectJournalTamper(
        "journal raw artifact metadata",
        fixture,
        finalJournalId,
        root => CaptureEventPayload(root, "watcher-stopped")["byteLength"] = 9_999,
        rechain: true,
        "differs from its sealed journal metadata");
    RejectJournalTamper(
        "journal cross-session terminal payload",
        fixture,
        edge1JournalId,
        root => CaptureEventPayload(root, "session-open")["previousSessionTerminalSha256"] = new string('0', 64),
        rechain: true,
        "session-open previous-terminal link changed");
    JsonObject primaryJournal = JsonNode.Parse(
        File.ReadAllBytes(fixture.ArtifactPaths[primaryJournalId]))!.AsObject();
    JsonObject primaryTerminal = primaryJournal["events"]!.AsArray()[^1]!.AsObject();
    string primaryTerminalAtUtc = primaryTerminal["atUtc"]!.GetValue<string>();
    long primaryTerminalMonotonic =
        primaryTerminal["monotonicMicroseconds"]!.GetValue<long>();
    RejectJournalTamper(
        "journal cross-session clock tie",
        fixture,
        edge1JournalId,
        root =>
        {
            JsonObject first = root["events"]!.AsArray()[0]!.AsObject();
            first["atUtc"] = primaryTerminalAtUtc;
            first["monotonicMicroseconds"] = primaryTerminalMonotonic;
        },
        rechain: true,
        "overlaps the preceding cold session",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal cross-session UTC tie with monotonic advance",
        fixture,
        edge1JournalId,
        root =>
        {
            JsonObject first = root["events"]!.AsArray()[0]!.AsObject();
            first["atUtc"] = primaryTerminalAtUtc;
            first["monotonicMicroseconds"] = primaryTerminalMonotonic + 1;
        },
        rechain: true,
        "overlaps the preceding cold session",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal cross-session monotonic equality",
        fixture,
        edge1JournalId,
        root => root["events"]!.AsArray()[0]!["monotonicMicroseconds"] =
            primaryTerminalMonotonic,
        rechain: true,
        "overlaps the preceding cold session",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal cross-session monotonic regression",
        fixture,
        edge1JournalId,
        root => root["events"]!.AsArray()[0]!["monotonicMicroseconds"] =
            primaryTerminalMonotonic - 1,
        rechain: true,
        "overlaps the preceding cold session",
        allowProjectionFallback: false);
    RejectJournalTamper(
        "journal cross-session drift above 250 ms",
        fixture,
        edge1JournalId,
        root =>
        {
            JsonObject first = root["events"]!.AsArray()[0]!.AsObject();
            first["atUtc"] = FormatUtc(
                ParseUtc(primaryTerminalAtUtc).AddMilliseconds(1));
            first["monotonicMicroseconds"] = primaryTerminalMonotonic + 251_001;
        },
        rechain: true,
        "Cross-session capture clocks diverged",
        allowProjectionFallback: false);
    Require(captureJournalTamperRejects == 64,
        "The closed capture-journal tamper matrix did not execute completely.");

    CompleteFixture duplicateProcessFixture = BuildCompleteFixture(
        repositoryRoot,
        Path.Combine(fixtureRoot, "duplicate-process-fingerprint"),
        pending,
        completeSafeProfile,
        reuseFirstProcessFingerprint: true);
    RejectFullReadback(
        "cross-session raw OS process fingerprint reuse",
        duplicateProcessFixture.Package,
        duplicateProcessFixture,
        "distinct process instance ID");
    MethodInfo uniqueFingerprintGate = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
        "ValidateUniqueRawProcessFingerprintHashes",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Raw process-fingerprint uniqueness gate is missing.");
    try
    {
        _ = uniqueFingerprintGate.Invoke(
            null,
            [new[] { new string('a', 64), new string('a', 64) }]);
        throw new InvalidOperationException("Duplicate raw process fingerprints were accepted.");
    }
    catch (TargetInvocationException exception) when (
        exception.InnerException is InvalidDataException dataException &&
        dataException.Message.Contains(
            "reuse one raw OS process fingerprint across sessions",
            StringComparison.Ordinal))
    {
        processFingerprintRejects++;
    }
    Require(processFingerprintRejects == 1,
        "The raw OS process-fingerprint duplicate gate was not exercised.");

    foreach ((string name, string path, string errorFragment) in new[]
    {
        ("live executable", fixture.ReadbackContext.DuckStationExecutablePath,
            "Current DuckStation executable bytes differ"),
        ("live handoff receipt", fixture.ReadbackContext.HandoffReceiptPath,
            "Current handoff receipt bytes differ"),
        ("live external-HOME settings", fixture.ReadbackContext.ExternalHomeSettingsPath,
            "Current external-HOME settings bytes differ"),
        ("live external-HOME BIOS", fixture.ReadbackContext.ExternalHomeBiosPath,
            "Current external-HOME BIOS bytes differ"),
        ("live host normal-user settings sentinel", fixture.ReadbackContext.HostNormalSettingsPath,
            "Current host normal-user settings sentinel bytes differ")
    })
    {
        byte[] original = File.ReadAllBytes(path);
        byte[] tampered = (byte[])original.Clone();
        tampered[0] ^= 0x01;
        try
        {
            File.WriteAllBytes(path, tampered);
            RejectFullReadback(name, fixture.Package, fixture, errorFragment);
        }
        finally
        {
            File.WriteAllBytes(path, original);
        }
    }

    string externalRuntimeParent = Path.GetDirectoryName(fixture.ReadbackContext.ExternalHomeBiosPath)!;
    UnixFileMode originalExternalRuntimeParentMode =
        File.GetUnixFileMode(externalRuntimeParent);
    try
    {
        File.SetUnixFileMode(
            externalRuntimeParent,
            originalExternalRuntimeParentMode | UnixFileMode.GroupWrite);
        RejectFullReadback(
            "group-writable external runtime-file parent",
            fixture.Package,
            fixture,
            "unsafe group/other write permission");
    }
    finally
    {
        File.SetUnixFileMode(externalRuntimeParent, originalExternalRuntimeParentMode);
    }

    UnixFileMode expectedTrustedLeafMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    foreach ((string label, string path) in new[]
    {
        ("handoff receipt", fixture.ReadbackContext.HandoffReceiptPath),
        ("external-HOME settings", fixture.ReadbackContext.ExternalHomeSettingsPath),
        ("external-HOME BIOS", fixture.ReadbackContext.ExternalHomeBiosPath)
    })
    {
        Require(File.GetUnixFileMode(path) == expectedTrustedLeafMode,
            $"The pinned {label} refusal fixture is not exact mode 0600.");
    }
    string trustedLeafModePath = fixture.ReadbackContext.ExternalHomeBiosPath;
    UnixFileMode originalTrustedLeafMode = File.GetUnixFileMode(trustedLeafModePath);
    try
    {
        File.SetUnixFileMode(
            trustedLeafModePath,
            originalTrustedLeafMode | UnixFileMode.GroupWrite);
        RejectFullReadback(
            "group-writable pinned runtime leaf",
            fixture.Package,
            fixture,
            "without group/other write permission");
    }
    finally
    {
        File.SetUnixFileMode(trustedLeafModePath, originalTrustedLeafMode);
    }

    string trustedLeafHardlink = trustedLeafModePath + ".hardlink";
    SmokeDarwin.CreateHardLink(trustedLeafHardlink, trustedLeafModePath);
    try
    {
        RejectFullReadback(
            "hardlinked pinned runtime leaf",
            fixture.Package,
            fixture,
            "st_nlink == 1");
    }
    finally
    {
        File.Delete(trustedLeafHardlink);
    }

    const string foreignOwnedRuntimeLeaf = "/private/etc/hosts";
    Require(File.Exists(foreignOwnedRuntimeLeaf),
        "The safe foreign-owner refusal fixture /private/etc/hosts is unavailable.");
    CompleteFixture foreignOwnedRuntimeFixture = fixture with
    {
        ReadbackContext = fixture.ReadbackContext with
        {
            ExternalHomeBiosPath = foreignOwnedRuntimeLeaf
        }
    };
    RejectFullReadback(
        "foreign path for pinned runtime leaf",
        fixture.Package,
        foreignOwnedRuntimeFixture,
        "Pinned external-HOME BIOS path changed");

    string forbiddenSafetyPath = Path.Combine(
        fixture.ReadbackContext.ExternalHomeDataRootPath,
        "memcards",
        "forbidden.mcd");
    try
    {
        File.WriteAllBytes(forbiddenSafetyPath, [0x65]);
        RejectFullReadback(
            "extant safety-directory file",
            fixture.Package,
            fixture,
            "External-HOME safety directory 'memcards' is not empty");
    }
    finally
    {
        if (File.Exists(forbiddenSafetyPath))
            File.Delete(forbiddenSafetyPath);
    }

    string unexpectedNonvolatilePath = Path.Combine(
        fixture.ReadbackContext.ExternalHomeDataRootPath,
        "unexpected-nonvolatile.bin");
    try
    {
        File.WriteAllBytes(unexpectedNonvolatilePath, [0x65]);
        RejectFullReadback(
            "post snapshot omits current nonvolatile file",
            fixture.Package,
            fixture,
            "does not match the current nonvolatile external-HOME manifest");
    }
    finally
    {
        if (File.Exists(unexpectedNonvolatilePath))
            File.Delete(unexpectedNonvolatilePath);
    }

    string forgedProfileArtifactId = "candidate-primary-edge-0.profile-after";
    Id65V3RuntimeProfileSnapshot forgedProfile = fixture.ProfileSnapshots[forgedProfileArtifactId] with
    {
        DuckStationExecutableSha256 = MutateNibble(
            fixture.ProfileSnapshots[forgedProfileArtifactId].DuckStationExecutableSha256)
    };
    RejectArtifactBytes(
        "forged profile snapshot pin",
        fixture,
        forgedProfileArtifactId,
        VerifierCanonicalArtifactUtf8(forgedProfile),
        "identity or pinned hash changed");
    Id65V3RuntimeProfileSnapshot legacyV1Profile =
        fixture.ProfileSnapshots[forgedProfileArtifactId] with { FormatVersion = 1 };
    RejectArtifactBytes(
        "legacy V1 profile snapshot version",
        fixture,
        forgedProfileArtifactId,
        VerifierCanonicalArtifactUtf8(legacyV1Profile),
        "identity or pinned hash changed");
    Id65V3RuntimeProfileSnapshot retiredV2Profile =
        fixture.ProfileSnapshots[forgedProfileArtifactId] with
        {
            Format = "spyro-editor-id65-v2-runtime-profile-snapshot"
        };
    RejectArtifactBytes(
        "retired V2 profile snapshot format",
        fixture,
        forgedProfileArtifactId,
        VerifierCanonicalArtifactUtf8(retiredV2Profile),
        "identity or pinned hash changed");
    Id65V3RuntimeProfileSnapshot utf16OrderedProfile =
        fixture.ProfileSnapshots[forgedProfileArtifactId] with
        {
            DataFiles = fixture.ProfileSnapshots[forgedProfileArtifactId].DataFiles
                .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
                .ToArray()
        };
    Require(!utf16OrderedProfile.DataFiles.Select(entry => entry.RelativePath).SequenceEqual(
            fixture.ProfileSnapshots[forgedProfileArtifactId].DataFiles.Select(entry => entry.RelativePath)),
        "The UTF-16 snapshot-order refusal fixture did not reorder E000 and the astral path.");
    RejectArtifactBytes(
        "UTF-16 ordered profile snapshot paths",
        fixture,
        forgedProfileArtifactId,
        VerifierCanonicalArtifactUtf8(utf16OrderedProfile),
        "file manifest is invalid");
    foreach ((string name, string relativePath, string expectedError) in new[]
    {
        ("snapshot C0 control path", "cache/c0-\u001f.bin",
            "Profile snapshot path is not a strict POSIX profile-relative path"),
        ("snapshot DEL control path", "cache/del-\u007f.bin",
            "Profile snapshot path is not a strict POSIX profile-relative path"),
        ("snapshot backslash path", "cache\\backslash.bin",
            "Profile snapshot path is not a strict POSIX profile-relative path"),
        ("snapshot dot-segment path", "cache/./dot.bin",
            "Profile snapshot path is not a strict POSIX profile-relative path"),
        ("snapshot non-NFC path", "cache/cafe\u0301.bin",
            "Profile snapshot path is not a strict POSIX profile-relative path"),
        ("snapshot unknown root path", "unknown/file.bin",
            "Profile snapshot entry 'unknown/file.bin' is invalid"),
        ("snapshot safety-directory path", "memcards/unsafe.mcd",
            "Profile snapshot entry 'memcards/unsafe.mcd' is invalid")
    })
    {
        Id65V3RuntimeProfileSnapshot invalidPathSnapshot = SnapshotWithAdditionalDataFile(
            fixture.ProfileSnapshots[forgedProfileArtifactId],
            relativePath);
        RejectArtifactBytes(
            name,
            fixture,
            forgedProfileArtifactId,
            VerifierCanonicalArtifactUtf8(invalidPathSnapshot),
            expectedError);
    }

    string edge1BeforeArtifactId = "candidate-edge-1.profile-before";
    string primaryAfterArtifactId = "candidate-primary-edge-0.profile-after";
    string brokenChainAtUtc = fixture.ProfileSnapshots[primaryAfterArtifactId].CapturedAtUtc;
    Id65V3RuntimeProfileSnapshot brokenProfileChain = fixture.ProfileSnapshots[edge1BeforeArtifactId] with
    {
        CapturedAtUtc = brokenChainAtUtc
    };
    RejectArtifactBytes(
        "adjacent profile snapshot chain",
        fixture,
        edge1BeforeArtifactId,
        VerifierCanonicalArtifactUtf8(brokenProfileChain),
        "Adjacent profile snapshots are not strictly chained",
        artifact => artifact with { CapturedAtUtc = brokenChainAtUtc });

    Id65V3RuntimeEvidenceCheck primarySamePad = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.primary-edge-4-to-3-same-pad-yaw");
    Id65V3RuntimeEvidenceCheck edge1 = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.edge-1-same-pad-yaw");
    RejectFullReadback(
        "cross-session visual artifact reuse",
        ReplaceCheck(
            fixture.Package,
            edge1 with { ArtifactRefs = primarySamePad.ArtifactRefs }),
        fixture,
        "unapproved session candidate-primary-edge-0");
    RejectCopiedVisualBytesAcrossSessions(fixture, primarySamePad, edge1);

    Id65V3RuntimeEvidenceCheck landingCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.completed-landing-hud4");
    Id65V3RuntimeEvidenceSession primarySession = fixture.Package.Sessions.Single(session =>
        session.SessionId == "candidate-primary-edge-0");
    Id65V3RuntimeEvidenceArtifact primaryJournalArtifact = fixture.Package.ArtifactCatalog.Single(
        artifact => artifact.ArtifactId == "candidate-primary-edge-0.capture-journal");
    const string aliasedJournalId = "candidate-primary-edge-0.capture-journal-alias";
    Id65V3RuntimeEvidencePackage aliasedJournalPackage =
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with
            {
                ArtifactCatalog = fixture.Package.ArtifactCatalog.Select(artifact =>
                    artifact.ArtifactId == primaryJournalArtifact.ArtifactId
                        ? artifact with { ArtifactId = aliasedJournalId }
                        : artifact).OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal).ToArray()
            });
    aliasedJournalPackage = ReplaceSession(
        aliasedJournalPackage,
        primarySession with
        {
            CaptureJournal = primarySession.CaptureJournal! with
            {
                ArtifactId = aliasedJournalId
            }
        });
    RejectFullReadback(
        "session capture-journal artifact ID alias",
        aliasedJournalPackage,
        fixture,
        "capture-journal ID is not exact");
    RejectFullReadback(
        "observation outside referenced frame time",
        ReplaceCheck(
            fixture.Package,
            landingCheck with { ObservedAtUtc = primarySession.SelectorStartedAtUtc }),
        fixture,
        "outside every referenced frame or recording slice");

    string landingRecordingId = landingCheck.ArtifactRefs.Single().ArtifactId;
    Id65V3RuntimeEvidenceArtifact landingRecording = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == landingRecordingId);
    Id65V3RuntimeEvidenceSlice landingSlice = landingCheck.ArtifactRefs.Single();
    RejectFullReadback(
        "landing recording does not span indexed sample",
        ReplaceCheck(
            fixture.Package,
            landingCheck with
            {
                ArtifactRefs =
                [
                    landingSlice with
                    {
                        StartMilliseconds = landingSlice.StartMilliseconds + 501,
                        EndMilliseconds = landingSlice.EndMilliseconds + 1
                    }
                ]
            }),
        fixture,
        "recording slice differs from its exact journal interval");

    Id65V3RuntimeEvidenceCheck lonePadCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.lone-pad-visibility");
    string lonePadFrameId = lonePadCheck.ArtifactRefs.Single().ArtifactId;
    Id65V3RuntimeEvidenceArtifact lonePadFrame = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == lonePadFrameId);
    RejectFullReadback(
        "visual capture outside process window",
        ReplaceArtifactMetadata(
            fixture.Package,
            lonePadFrame with
            {
                CapturedAtUtc = FormatUtc(
                    ParseUtc(primarySession.ProcessStartedAtUtc!).AddMilliseconds(-1))
            }),
        fixture,
        "is outside session candidate-primary-edge-0");

    RejectArtifactBytes(
        "malformed PNG frame",
        fixture,
        lonePadFrameId,
        Encoding.UTF8.GetBytes("not-a-png"),
        "PNG signature");
    Id65V3RuntimeEvidenceCheck cameraCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.camera-360-twice");
    string cameraRecordingId = cameraCheck.ArtifactRefs.Single(reference =>
        reference.StartMilliseconds is not null).ArtifactId;
    RejectArtifactBytes(
        "malformed ISO-BMFF recording",
        fixture,
        cameraRecordingId,
        [0x00, 0x01, 0x02],
        "shorter than a bounded ISO-BMFF ftyp box");

    Id65V3RuntimeEvidenceArtifact cameraRecording = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == cameraRecordingId);
    RejectFullReadback(
        "recording lacks one media boundary",
        ReplaceArtifactMetadata(
            fixture.Package,
            cameraRecording with { MediaEndAtUtc = null }),
        fixture,
        "lacks both media boundaries");
    RejectFullReadback(
        "recording capture predates media end",
        ReplaceArtifactMetadata(
            fixture.Package,
            cameraRecording with { CapturedAtUtc = cameraRecording.MediaStartAtUtc! }),
        fixture,
        "has invalid media boundaries");
    RejectFullReadback(
        "frame carries media boundaries",
        ReplaceArtifactMetadata(
            fixture.Package,
            lonePadFrame with
            {
                MediaStartAtUtc = primarySession.LandingAtUtc,
                MediaEndAtUtc = FormatUtc(ParseUtc(primarySession.LandingAtUtc!).AddMilliseconds(1))
            }),
        fixture,
        "carries forbidden media boundaries");
    Id65V3RuntimeEvidenceArtifact primaryWatcherArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == primarySession.WatcherArtifactId);
    RejectFullReadback(
        "nonrecording artifact carries a half-open media boundary",
        ReplaceArtifactMetadata(
            fixture.Package,
            primaryWatcherArtifact with { MediaStartAtUtc = primarySession.ProcessStartedAtUtc }),
        fixture,
        "Nonrecording artifact candidate-primary-edge-0.watcher carries forbidden media boundaries");

    Id65V3RuntimeEvidenceSlice cameraSlice = cameraCheck.ArtifactRefs.Single();
    RejectFullReadback(
        "camera recording shorter than frozen minimum",
        ReplaceCheck(
            fixture.Package,
            cameraCheck with
            {
                ObservedAtUtc = FormatUtc(ParseUtc(cameraRecording.MediaStartAtUtc!).AddMilliseconds(
                    cameraSlice.StartMilliseconds!.Value + 5_000)),
                ArtifactRefs =
                [cameraSlice with { EndMilliseconds = cameraSlice.StartMilliseconds + 5_000 }]
            }),
        fixture,
        "recording slice differs from its exact journal interval");

    Id65V3RuntimeEvidenceCheck musicCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "retail.town-square.music");
    Id65V3RuntimeEvidenceSlice musicSlice = musicCheck.ArtifactRefs.Single();
    Id65V3RuntimeEvidenceArtifact musicRecording = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == musicSlice.ArtifactId);
    RejectFullReadback(
        "retail music recording shorter than frozen minimum",
        ReplaceCheck(
            fixture.Package,
            musicCheck with
            {
                ObservedAtUtc = FormatUtc(ParseUtc(musicRecording.MediaStartAtUtc!).AddMilliseconds(
                    musicSlice.StartMilliseconds!.Value + 9_000)),
                ArtifactRefs =
                [musicSlice with { EndMilliseconds = musicSlice.StartMilliseconds + 9_000 }]
            }),
        fixture,
        "lacks its own recording interval meeting the frozen minimum");

    Id65V3RuntimeEvidenceCheck terrainCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "retail.town-square.terrain");
    Id65V3RuntimeEvidenceCheck collisionCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "retail.town-square.collision");
    RejectFullReadback(
        "retail checks share an overlapping recording slice",
        ReplaceCheck(
            fixture.Package,
            collisionCheck with
            {
                ObservedAtUtc = FormatUtc(ParseUtc(musicRecording.MediaStartAtUtc!).AddMilliseconds(
                    terrainCheck.ArtifactRefs.Single().EndMilliseconds!.Value + 4_500)),
                ArtifactRefs =
                [
                    collisionCheck.ArtifactRefs.Single() with
                    {
                        StartMilliseconds = terrainCheck.ArtifactRefs.Single().EndMilliseconds - 1_000,
                        EndMilliseconds = terrainCheck.ArtifactRefs.Single().EndMilliseconds + 4_500
                    }
                ]
            }),
        fixture,
        "recording overlaps or precedes its fixed-order predecessor");

    Id65V3RuntimeEvidenceCheck walkCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.walk-center-perimeter-both-directions");
    Id65V3RuntimeEvidenceSlice walkSlice = walkCheck.ArtifactRefs.Single();
    Id65V3RuntimeEvidencePackage overlappingWalkPackage = ReplaceCheck(
        fixture.Package,
        walkCheck with
        {
            ObservedAtUtc = cameraCheck.ObservedAtUtc,
            ArtifactRefs =
            [
                walkSlice with
                {
                    StartMilliseconds = cameraSlice.StartMilliseconds,
                    EndMilliseconds = cameraSlice.EndMilliseconds
                }
            ]
        });
    RejectFullReadback(
        "primary movement evidence overlaps predecessor",
        overlappingWalkPackage,
        fixture,
        "overlaps or precedes its required predecessor");

    Id65V3RuntimeEvidenceSlice primaryPreFrame = primarySamePad.ArtifactRefs.First(reference =>
        reference.FrameSha256 is not null);
    Id65V3RuntimeEvidenceArtifact primaryPreArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == primaryPreFrame.ArtifactId);
    RejectFullReadback(
        "death comparison lacks pre-fall frame",
        ReplaceArtifactMetadata(
            fixture.Package,
            primaryPreArtifact with
            {
                CapturedAtUtc = FormatUtc(ParseUtc(primarySession.RespawnAtUtc!).AddMilliseconds(1))
            }),
        fixture,
        "does not contain pre-fall and post-respawn frames plus a spanning recording");

    Id65V3RuntimeEvidenceCheck coldCheck = fixture.Package.VisualChecks.Single(check =>
        check.CheckId == "candidate.cold-reset-same-pad-yaw");
    Id65V3RuntimeEvidenceSlice coldPreReference = coldCheck.ArtifactRefs[0];
    Id65V3RuntimeEvidenceArtifact coldPreArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
        artifact.ArtifactId == coldPreReference.ArtifactId);
    Id65V3RuntimeEvidenceSession coldSession = fixture.Package.Sessions.Single(session =>
        session.SessionId == "candidate-cold-reset");
    RejectFullReadback(
        "cold-reset comparison lacks pre-reset session frame",
        ReplaceArtifactMetadata(
            fixture.Package,
            coldPreArtifact with
            {
                SessionId = "candidate-cold-reset",
                CapturedAtUtc = FormatUtc(ParseUtc(coldSession.LandingAtUtc!).AddMilliseconds(200))
            }),
        fixture,
        "references artifact candidate-edge-2.edge-after from unapproved session candidate-cold-reset");
    RejectFullReadback(
        "cold-reset comparison lacks reset recording",
        ReplaceCheck(
            fixture.Package,
            coldCheck with
            {
                ArtifactRefs = coldCheck.ArtifactRefs.Where(reference =>
                    reference.FrameSha256 is not null).ToArray()
            }),
        fixture,
        "five-second reset recording spanning selector entry through landing");

    RejectFullReadback(
        "landing timestamp differs from indexed sample",
        ReplaceSession(
            fixture.Package,
            primarySession with
            {
                LandingAtUtc = FormatUtc(
                    ParseUtc(primarySession.LandingAtUtc!).AddMilliseconds(1))
            }),
        fixture,
        "marker timestamps/ordinals differ from journal-bound watcher samples");

    Id65V3RuntimeEvidenceSession sunnySession = fixture.Package.Sessions.Single(session =>
        session.SessionId == "retail-sunny-flight");
    RejectFullReadback(
        "watcher samples outside process window",
        ReplaceSession(
            fixture.Package,
            sunnySession with { ProcessExitedAtUtc = sunnySession.SampleSummary!.LastAtUtc }),
        fixture,
        "Session retail-sunny-flight timestamps differ from its capture journal");

    RejectFullReadback(
        "cold-reset fields differ from session endpoints",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with
            {
                ColdReset = fixture.Package.ColdReset with
                {
                    PrimaryProcessExitedAtUtc = FormatUtc(
                        ParseUtc(fixture.Package.ColdReset.PrimaryProcessExitedAtUtc!).AddMilliseconds(1))
                }
            }),
        fixture,
        "Cold-reset fields do not equal the immediately preceding exit");

    RejectFullReadback(
        "recordedAt predates final run boundary",
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
            fixture.Package with { RecordedAtUtc = primarySession.ProcessStartedAtUtc }),
        fixture,
        "recordedAtUtc predates the final process or artifact boundary");

    RejectArtifactBytes(
        "invalid UTF-8 watcher artifact",
        fixture,
        "retail-sunny-flight.watcher",
        [0xFF, 0xFE, 0xFA],
        "is not valid UTF-8");
}
finally
{
    if (Directory.Exists(fixtureRoot))
        Directory.Delete(fixtureRoot, recursive: true);
}

string[] forbiddenMethodFragments = ["Publish", "CreateBin", "Launch", "ConnectDuckStation", "WriteFile"];
string[] readOnlyMethodNameAllowlist = ["ValidatePrelaunchProof"];
Require(!typeof(Id65V3RuntimeEvidenceContract)
        .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
        .Any(method => !readOnlyMethodNameAllowlist.Contains(method.Name, StringComparer.Ordinal) &&
            forbiddenMethodFragments.Any(fragment =>
                method.Name.Contains(fragment, StringComparison.OrdinalIgnoreCase))),
    "The evidence contract exposed a writer, publisher, Create BIN, launcher, or DuckStation connection API.");
Require(fullPassTamperRejects == 121 && finalizerRefusals == 47,
    $"Exact finalizer/full-readback refusal coverage drifted: " +
    $"fullPassTamperRejects={fullPassTamperRejects}, finalizerRefusals={finalizerRefusals}.");

Console.WriteLine(
    "ID65_V3_RUNTIME_EVIDENCE_CONTRACT_SMOKE_OK " +
    $"state=pending-no-run stateGateRejects={stateGateRejects} canonicalRejects={canonicalRejects} " +
    $"telemetryRejects={telemetryRejects} " +
    $"pathRejects={pathRejects} captureJournalTamperRejects={captureJournalTamperRejects} " +
    $"processFingerprintRejects={processFingerprintRejects} " +
    $"sourceIdentityRejects={sourceIdentityRejects} " +
    $"pinnedSourceBoundaryRejects={pinnedSourceBoundaryRejects} " +
    $"fullPassTamperRejects={fullPassTamperRejects} finalizerRefusals={finalizerRefusals} " +
    $"watcher={Id65V3RuntimeEvidenceContract.ExpectedWatcherSha256} " +
    $"profile={Id65V3RuntimeEvidenceContract.ExpectedRuntimeProfileSha256} " +
    $"source={Id65V3RuntimeEvidenceContract.ExpectedSourceClosureSha256}");

void RejectPinnedSourceBoundary(string name, string expectedErrorFragment)
{
    Id65V3RuntimeEvidenceValidation validation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            canonicalBytes,
            repositoryRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback);
    Require(!validation.StructurallyValid &&
            validation.State == Id65V3RuntimeEvidenceState.Invalid &&
            validation.Errors.Any(error =>
                error.Contains(expectedErrorFragment, StringComparison.Ordinal)),
        $"Pinned-source boundary tamper '{name}' did not reach '{expectedErrorFragment}': " +
        string.Join(" | ", validation.Errors));
    pinnedSourceBoundaryRejects++;
}

void RejectSourceIdentity(
    string name,
    Id65V3RuntimeEvidencePackage package,
    string expectedErrorFragment)
{
    Id65V3RuntimeEvidencePackage sealedPackage =
        Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(package);
    Id65V3RuntimeEvidenceValidation validation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(sealedPackage),
            repositoryRoot);
    Require(!validation.StructurallyValid &&
            validation.State == Id65V3RuntimeEvidenceState.Invalid &&
            validation.Errors.Any(error =>
                error.Contains(expectedErrorFragment, StringComparison.Ordinal)),
        $"Source-identity tamper '{name}' did not reach '{expectedErrorFragment}': " +
        string.Join(" | ", validation.Errors));
    sourceIdentityRejects++;
}

void RejectFullReadback(
    string name,
    Id65V3RuntimeEvidencePackage package,
    CompleteFixture fixture,
    string expectedErrorFragment,
    bool allowProjectionFallback = true)
{
    Id65V3RuntimeEvidenceValidation validation =
        Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
            Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
            fixture.ArtifactRepositoryRoot,
            Id65V3RuntimeEvidenceValidationMode.FullReadback,
            fixture.ReadbackContext);
    Require(!validation.StructurallyValid && validation.State == Id65V3RuntimeEvidenceState.Invalid &&
            validation.Errors.Any(error =>
                error.Contains(expectedErrorFragment, StringComparison.Ordinal) ||
                (allowProjectionFallback &&
                    (error.Contains("Operator check review", StringComparison.Ordinal) ||
                     error.Contains("operator-review", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("capture-closure", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("live projection", StringComparison.OrdinalIgnoreCase) ||
                     error.Contains("Live NDJSON", StringComparison.Ordinal)))),
        $"Full-readback tamper '{name}' did not reach its intended fail-closed gate " +
        $"'{expectedErrorFragment}': {string.Join(" | ", validation.Errors)}");
    fullPassTamperRejects++;
}

void RejectArtifactBytes(
    string name,
    CompleteFixture fixture,
    string artifactId,
    byte[] tamperedBytes,
    string expectedErrorFragment,
    Func<Id65V3RuntimeEvidenceArtifact, Id65V3RuntimeEvidenceArtifact>? mutateMetadata = null)
{
    string path = fixture.ArtifactPaths[artifactId];
    byte[] original = File.ReadAllBytes(path);
    try
    {
        File.WriteAllBytes(path, tamperedBytes);
        Id65V3RuntimeEvidenceArtifact originalArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
            artifact.ArtifactId == artifactId);
        Id65V3RuntimeEvidenceArtifact tamperedArtifact = originalArtifact with
        {
            Sha256 = Hash(tamperedBytes),
            ByteLength = tamperedBytes.LongLength
        };
        if (mutateMetadata is not null)
            tamperedArtifact = mutateMetadata(tamperedArtifact);
        Id65V3RuntimeEvidencePackage tamperedPackage = ReplaceArtifactMetadata(
            fixture.Package,
            tamperedArtifact);
        RejectFullReadback(name, tamperedPackage, fixture, expectedErrorFragment);
    }
    finally
    {
        File.WriteAllBytes(path, original);
    }
}

void RejectJournalTamper(
    string name,
    CompleteFixture fixture,
    string journalArtifactId,
    Action<JsonObject> mutate,
    bool rechain,
    string expectedErrorFragment,
    bool allowProjectionFallback = true)
{
    string path = fixture.ArtifactPaths[journalArtifactId];
    byte[] original = File.ReadAllBytes(path);
    try
    {
        JsonObject root = JsonNode.Parse(original)?.AsObject() ??
            throw new InvalidOperationException("Synthetic capture journal did not parse as an object.");
        mutate(root);
        if (rechain)
            RechainCaptureJournal(root);
        byte[] tampered = CanonicalArtifactUtf8(root);
        File.WriteAllBytes(path, tampered);
        Id65V3RuntimeEvidenceArtifact journalArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
            artifact.ArtifactId == journalArtifactId);
        Id65V3RuntimeEvidencePackage package = ReplaceArtifactMetadata(
            fixture.Package,
            journalArtifact with
            {
                Sha256 = Hash(tampered),
                ByteLength = tampered.LongLength
            });
        if (rechain)
        {
            JsonArray events = root["events"]!.AsArray();
            string terminalHash = events[^1]!["eventSha256"]!.GetValue<string>();
            Id65V3RuntimeEvidenceSession session = package.Sessions.Single(item =>
                item.CaptureJournal?.ArtifactId == journalArtifactId);
            package = ReplaceSession(
                package,
                session with
                {
                    CaptureJournal = session.CaptureJournal! with
                    {
                        TerminalEventSha256 = terminalHash
                    }
                });
        }
        Id65V3RuntimeEvidenceValidation validation =
            Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
                Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
                fixture.ArtifactRepositoryRoot,
                Id65V3RuntimeEvidenceValidationMode.FullReadback,
                fixture.ReadbackContext);
        Require(!validation.StructurallyValid && validation.State == Id65V3RuntimeEvidenceState.Invalid &&
                validation.Errors.Any(error =>
                    error.Contains(expectedErrorFragment, StringComparison.Ordinal) ||
                    (allowProjectionFallback &&
                        (error.Contains("live projection", StringComparison.OrdinalIgnoreCase) ||
                         error.Contains("Live NDJSON", StringComparison.Ordinal)))),
            $"Capture-journal tamper '{name}' did not reach its intended gate or the stronger " +
            $"origin/live projection gate: {string.Join(" | ", validation.Errors)}");
        fullPassTamperRejects++;
        captureJournalTamperRejects++;
    }
    finally
    {
        File.WriteAllBytes(path, original);
    }
}

static JsonObject CaptureEvent(JsonObject root, string eventType, int occurrence = 0) =>
    root["events"]!.AsArray()
        .Select(item => item!.AsObject())
        .Where(item => item["eventType"]!.GetValue<string>() == eventType)
        .ElementAt(occurrence);

static void MoveCaptureOperatorMarker(
    JsonObject root,
    string eventType,
    int milliseconds)
{
    JsonObject item = CaptureEvent(root, eventType);
    item["atUtc"] = FormatUtc(
        ParseUtc(item["atUtc"]!.GetValue<string>()).AddMilliseconds(milliseconds));
    item["monotonicMicroseconds"] =
        item["monotonicMicroseconds"]!.GetValue<long>() + milliseconds * 1_000L;
}

static void MoveCaptureWatcherBindingSample(
    JsonObject root,
    string markerRole,
    int milliseconds)
{
    JsonObject payload = root["events"]!.AsArray()
        .Select(item => item!.AsObject())
        .Where(item => item["eventType"]!.GetValue<string>() == "watcher-marker-bound")
        .Select(item => item["payload"]!.AsObject())
        .Single(item => item["markerRole"]!.GetValue<string>() == markerRole);
    payload["sampleAtUtc"] = FormatUtc(
        ParseUtc(payload["sampleAtUtc"]!.GetValue<string>()).AddMilliseconds(milliseconds));
    payload["sampleElapsedMilliseconds"] =
        payload["sampleElapsedMilliseconds"]!.GetValue<long>() + milliseconds;
    payload["sampleOrdinal"] =
        payload["sampleOrdinal"]!.GetValue<long>() + milliseconds / 100;
}

static JsonObject CaptureEventPayload(JsonObject root, string eventType, int occurrence = 0) =>
    CaptureEvent(root, eventType, occurrence)["payload"]!.AsObject();

static void SwapCaptureEvents(
    JsonObject root,
    string firstEventType,
    string secondEventType)
{
    JsonArray events = root["events"]!.AsArray();
    int firstIndex = events
        .Select((item, index) => (Item: item!.AsObject(), Index: index))
        .Single(pair => pair.Item["eventType"]!.GetValue<string>() == firstEventType)
        .Index;
    int secondIndex = events
        .Select((item, index) => (Item: item!.AsObject(), Index: index))
        .Single(pair => pair.Item["eventType"]!.GetValue<string>() == secondEventType)
        .Index;
    JsonNode first = events[firstIndex]!.DeepClone();
    JsonNode second = events[secondIndex]!.DeepClone();
    events[firstIndex] = second;
    events[secondIndex] = first;
}

static void SwapCaptureEventBodies(
    JsonObject root,
    string firstEventType,
    string secondEventType)
{
    JsonObject first = CaptureEvent(root, firstEventType);
    JsonObject second = CaptureEvent(root, secondEventType);
    JsonNode firstPayload = first["payload"]!.DeepClone();
    JsonNode secondPayload = second["payload"]!.DeepClone();
    first["eventType"] = secondEventType;
    first["payload"] = secondPayload;
    second["eventType"] = firstEventType;
    second["payload"] = firstPayload;
}

static void RechainCaptureJournal(JsonObject root)
{
    JsonArray events = root["events"]!.AsArray();
    string current = events[0]!["previousEventSha256"]!.GetValue<string>();
    for (int index = 0; index < events.Count; index++)
    {
        JsonObject item = events[index]!.AsObject();
        item["sequence"] = index;
        item["previousEventSha256"] = current;
        string eventHash = CaptureEventHashNode(root, item, item["payload"]!.AsObject());
        item["eventSha256"] = eventHash;
        current = eventHash;
    }
}

void RejectCopiedVisualBytesAcrossSessions(
    CompleteFixture fixture,
    Id65V3RuntimeEvidenceCheck sourceCheck,
    Id65V3RuntimeEvidenceCheck destinationCheck)
{
    Id65V3RuntimeEvidenceSlice sourceReference = sourceCheck.ArtifactRefs.First(reference =>
        reference.FrameSha256 is not null);
    Id65V3RuntimeEvidenceSlice destinationReference = destinationCheck.ArtifactRefs.First(reference =>
        reference.FrameSha256 is not null);
    string sourcePath = fixture.ArtifactPaths[sourceReference.ArtifactId];
    string destinationPath = fixture.ArtifactPaths[destinationReference.ArtifactId];
    byte[] copiedBytes = File.ReadAllBytes(sourcePath);
    byte[] originalDestinationBytes = File.ReadAllBytes(destinationPath);
    try
    {
        File.WriteAllBytes(destinationPath, copiedBytes);
        string copiedHash = Hash(copiedBytes);
        Id65V3RuntimeEvidenceArtifact destinationArtifact = fixture.Package.ArtifactCatalog.Single(artifact =>
            artifact.ArtifactId == destinationReference.ArtifactId);
        Id65V3RuntimeEvidencePackage package = ReplaceArtifactMetadata(
            fixture.Package,
            destinationArtifact with
            {
                Sha256 = copiedHash,
                ByteLength = copiedBytes.LongLength
            });
        Id65V3RuntimeEvidenceCheck changedCheck = destinationCheck with
        {
            ArtifactRefs = destinationCheck.ArtifactRefs.Select(reference =>
                reference.ArtifactId == destinationReference.ArtifactId
                    ? reference with { FrameSha256 = copiedHash }
                    : reference).ToArray()
        };
        package = ReplaceCheck(package, changedCheck);
        RejectFullReadback(
            "copied visual bytes under a distinct cross-session artifact ID",
            package,
            fixture,
            "Visual artifacts with identical bytes are reused across cold processes");
    }
    finally
    {
        File.WriteAllBytes(destinationPath, originalDestinationBytes);
    }
}

static Id65V3RuntimeEvidencePackage ReplaceArtifactMetadata(
    Id65V3RuntimeEvidencePackage package,
    Id65V3RuntimeEvidenceArtifact replacement) =>
    Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(package with
    {
        ArtifactCatalog = package.ArtifactCatalog
            .Select(artifact => artifact.ArtifactId == replacement.ArtifactId ? replacement : artifact)
            .OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal)
            .ToArray()
    });

static Id65V3RuntimeEvidencePackage ReplaceSession(
    Id65V3RuntimeEvidencePackage package,
    Id65V3RuntimeEvidenceSession replacement) =>
    Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(package with
    {
        Sessions = package.Sessions
            .Select(session => session.SessionId == replacement.SessionId ? replacement : session)
            .ToArray()
    });

static Id65V3RuntimeEvidencePackage ReplaceCheck(
    Id65V3RuntimeEvidencePackage package,
    Id65V3RuntimeEvidenceCheck replacement) =>
    Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(package with
    {
        VisualChecks = package.VisualChecks
            .Select(check => check.CheckId == replacement.CheckId ? replacement : check)
            .ToArray()
    });

CompleteFixture BuildCompleteFixture(
    string sourceRepositoryRoot,
    string artifactRepositoryRoot,
    Id65V3RuntimeEvidencePackage pendingPackage,
    Id65V3RuntimeEvidenceProfile observedProfile,
    bool reuseFirstProcessFingerprint = false,
    int primaryRecordingStartMilliseconds = 3_500,
    bool exerciseWithinSessionUtcTies = false,
    int runOriginUtcOffsetMilliseconds = 0,
    long runOriginMonotonicOffsetMicroseconds = 0,
    int profileBeforeCapturedMilliseconds = -1_000,
    int processStartedMilliseconds = 100,
    bool exerciseUnicodeChangedPaths = false)
{
    const string assetPrefix =
        "docs/runtime-evidence/assets/unused-level-65-remote-blank-environment-header-repair-v3/";
    const string runId = "id65v3-20260810t190000z-0123456789abcdef";
    const string journalFormat = "spyro-editor-id65-v3-runtime-capture-event-journal";
    const string profilePackageRelative =
        "_local/v5-stone-hill-level-replacement/duckstation-id65-unattended-v1";
    const string externalBiosManifestPath =
        "bios/Sony PlayStation SCPH-1001 - DTLH-3000 BIOS v2.2 (1995-12-04)(Sony)(US).bin";
    string stagingRoot = Path.Combine(artifactRepositoryRoot, assetPrefix, "capture-staging");
    string runPath = Path.Combine(stagingRoot, runId);
    Directory.CreateDirectory(runPath);
    File.SetUnixFileMode(stagingRoot, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    File.SetUnixFileMode(runPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    string leasePath = Path.Combine(stagingRoot, ".capture-coordinator.lock");
    File.WriteAllBytes(leasePath, []);
    File.SetUnixFileMode(leasePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    string bootIdentity = Hash(Encoding.UTF8.GetBytes("offline-boot-identity"));
    string sourcePackageRoot = Path.Combine(sourceRepositoryRoot, profilePackageRelative);
    string sourceDataRoot = Path.Combine(
        sourcePackageRoot,
        "home", "Library", "Application Support", "DuckStation");
    string livePackageRoot = Path.Combine(artifactRepositoryRoot, profilePackageRelative);
    string liveAppRoot = Path.Combine(livePackageRoot, "DuckStation-ID65-Unattended-v1.app");
    string liveExternalHomeRoot = Path.Combine(livePackageRoot, "home");
    string liveProfileRoot = Path.Combine(
        liveExternalHomeRoot,
        "Library", "Application Support", "DuckStation");
    Directory.CreateDirectory(liveProfileRoot);
    foreach (string directory in new[] { "cheats", "gamesettings", "memcards", "patches", "savestates" })
        Directory.CreateDirectory(Path.Combine(liveProfileRoot, directory));

    string liveExecutable = CopyFixtureFile(
        Path.Combine(sourcePackageRoot, "DuckStation-ID65-Unattended-v1.app", "Contents", "MacOS", "DuckStation"),
        Path.Combine(liveAppRoot, "Contents", "MacOS", "DuckStation"));
    string liveReceipt = CopyFixtureFile(
        Path.Combine(sourcePackageRoot, "handoff-receipt.json"),
        Path.Combine(livePackageRoot, "handoff-receipt.json"));
    string liveSettings = CopyFixtureFile(
        Path.Combine(sourceDataRoot, "settings.ini"),
        Path.Combine(liveProfileRoot, "settings.ini"));
    string liveBios = CopyFixtureFile(
        Path.Combine(sourceDataRoot, externalBiosManifestPath.Replace('/', Path.DirectorySeparatorChar)),
        Path.Combine(liveProfileRoot, externalBiosManifestPath.Replace('/', Path.DirectorySeparatorChar)));
    string livePlaytime = Path.Combine(liveProfileRoot, "playtime.dat");
    File.WriteAllBytes(livePlaytime, Encoding.ASCII.GetBytes("offline-playtime-v2\n"));
    Dictionary<string, string> liveUnicodeVolatileFiles = new(StringComparer.Ordinal);
    foreach ((string relativePath, int index) in UnicodeVolatileFixturePaths()
                 .Select((relativePath, index) => (relativePath, index)))
    {
        string fullPath = Path.Combine(
            liveProfileRoot,
            relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllBytes(
            fullPath,
            Encoding.ASCII.GetBytes($"offline-unicode-volatile-{index}\n"));
        liveUnicodeVolatileFiles.Add(relativePath, fullPath);
    }
    string liveNormalSettings = CopyFixtureFile(
        "/Users/john/Library/Application Support/DuckStation/settings.ini",
        Path.Combine(artifactRepositoryRoot, "external", "normal-settings.ini"));

    Id65V3RuntimeEvidenceReadbackContext readbackContext = new(
        sourceRepositoryRoot,
        livePackageRoot,
        liveExecutable,
        liveReceipt,
        liveExternalHomeRoot,
        liveProfileRoot,
        liveSettings,
        liveBios,
        liveNormalSettings);

    List<Id65V3RuntimeProfileSnapshotEntry> snapshotEntryList =
    [
        SnapshotEntry(externalBiosManifestPath, liveBios),
        SnapshotEntry("playtime.dat", livePlaytime),
        SnapshotEntry("settings.ini", liveSettings)
    ];
    snapshotEntryList.AddRange(liveUnicodeVolatileFiles.Select(pair =>
        SnapshotEntry(pair.Key, pair.Value)));
    Id65V3RuntimeProfileSnapshotEntry[] snapshotEntries = snapshotEntryList
        .OrderBy(entry => entry.RelativePath, SmokePythonUnicodeComparer.Instance)
        .ToArray();
    Require(snapshotEntries.Where(entry => entry.RelativePath.StartsWith("cache/", StringComparison.Ordinal))
            .Select(entry => entry.RelativePath)
            .SequenceEqual(
                ["cache/a|b.bin", "cache/c1-\u0085.bin", "cache/\ue000.bin", "cache/\U0001F600.bin"]),
        "Synthetic snapshot dataFiles do not preserve Python Unicode path order.");

    Id65V3RuntimeProfileSnapshot preflightSnapshot = new(
        "spyro-editor-id65-v3-runtime-profile-snapshot",
        2,
        "preflight.profile-preflight",
        "preflight",
        "preflight",
        "not-applicable",
        observedProfile.ProfileId,
        "1970-01-01T00:00:00.000Z",
        observedProfile.DuckStationExecutableSha256,
        observedProfile.HandoffReceiptSha256,
        observedProfile.AppContentManifestSha256,
        observedProfile.AppOutputTreeManifestSha256,
        observedProfile.PackageEnvelopeSha256,
        observedProfile.ExternalHomeSettingsSha256,
        observedProfile.ExternalHomeBiosSha256,
        observedProfile.HostNormalSettingsSentinelId,
        observedProfile.HostNormalSettingsSha256,
        snapshotEntries);
    Dictionary<string, object?> prelaunchProof = new(StringComparer.Ordinal)
    {
        ["format"] = "spyro-id65-unattended-profile-preflight",
        ["formatVersion"] = 1,
        ["status"] = "PREFLIGHT_READY",
        ["profileId"] = observedProfile.ProfileId,
        ["receiptSha256"] = observedProfile.HandoffReceiptSha256,
        ["appContentManifestSha256"] = observedProfile.AppContentManifestSha256,
        ["appOutputTreeManifestSha256"] = observedProfile.AppOutputTreeManifestSha256,
        ["homeOutputTreeManifestSha256"] = observedProfile.HomeOutputTreeManifestSha256,
        ["packageEnvelopeSha256"] = observedProfile.PackageEnvelopeSha256,
        ["settingsSha256"] = observedProfile.ExternalHomeSettingsSha256,
        ["biosSha256"] = observedProfile.ExternalHomeBiosSha256,
        ["candidateCueSha256"] = pendingPackage.SourceIdentity.CandidateCueSha256,
        ["candidateBinSha256"] = pendingPackage.SourceIdentity.CandidateBinSha256,
        ["hostNormalSettingsSentinelSha256"] = observedProfile.HostNormalSettingsSha256,
        ["launchGate"] = observedProfile.LaunchGate,
        ["snapshot"] = preflightSnapshot
    };
    Dictionary<string, string> capturePins = BuildCapturePins(pendingPackage);
    Dictionary<string, object?> runOrigin = new(StringComparer.Ordinal)
    {
        ["bootIdentity"] = bootIdentity,
        ["captureStagingRepoRelativePath"] = assetPrefix + "capture-staging/" + runId,
        ["createdAtUtc"] = FormatUtc(
            ParseUtc("2026-08-10T18:59:59.000Z")
                .AddMilliseconds(runOriginUtcOffsetMilliseconds)),
        ["createdMonotonicMicroseconds"] =
            999_000_000L + runOriginMonotonicOffsetMicroseconds,
        ["format"] = "spyro-editor-id65-v3-runtime-capture-run-origin",
        ["formatVersion"] = 2,
        ["orderedSessionIds"] = new[]
        {
            "candidate-primary-edge-0", "candidate-edge-1", "candidate-edge-2",
            "candidate-cold-reset", "retail-town-square", "retail-gnastys-loot",
            "retail-sunny-flight"
        },
        ["prelaunchProof"] = prelaunchProof,
        ["prelaunchProofSha256"] = Hash(VerifierCanonicalArtifactUtf8(prelaunchProof)),
        ["repositoryRootRealPath"] = Path.GetFullPath(artifactRepositoryRoot),
        ["runId"] = runId
    };
    foreach ((string name, string value) in capturePins)
        runOrigin[name] = value;
    byte[] runOriginBytes = CanonicalArtifactUtf8(runOrigin);
    string runOriginPath = Path.Combine(runPath, "run-origin.json");
    File.WriteAllBytes(runOriginPath, runOriginBytes);
    File.SetUnixFileMode(runOriginPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
    string runOriginSha256 = Hash(runOriginBytes);

    List<Id65V3RuntimeEvidenceArtifact> artifacts = [];
    Dictionary<string, string> artifactPaths = new(StringComparer.Ordinal);
    Dictionary<string, Id65V3RuntimeProfileSnapshot> profileSnapshots = new(StringComparer.Ordinal);
    Dictionary<string, byte[]> journalBytes = new(StringComparer.Ordinal);
    Dictionary<string, byte[]> liveProjectionBytes = new(StringComparer.Ordinal);
    Dictionary<string, string> liveProjectionPaths = new(StringComparer.Ordinal);

    Id65V3RuntimeEvidenceArtifact AddArtifact(
        string artifactId,
        string sessionId,
        string kind,
        string relativePath,
        byte[] bytes,
        string capturedAtUtc,
        string? mediaStartAtUtc = null,
        string? mediaEndAtUtc = null)
    {
        string fullPath = Path.Combine(artifactRepositoryRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        if (fullPath.StartsWith(stagingRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            string? parent = Path.GetDirectoryName(fullPath);
            if (parent is not null)
            {
                File.SetUnixFileMode(
                    parent,
                    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
        }
        File.WriteAllBytes(fullPath, bytes);
        File.SetUnixFileMode(fullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Id65V3RuntimeEvidenceArtifact artifact = new(
            artifactId,
            sessionId,
            kind,
            relativePath,
            Hash(bytes),
            bytes.LongLength,
            capturedAtUtc,
            mediaStartAtUtc,
            mediaEndAtUtc);
        artifacts.Add(artifact);
        artifactPaths.Add(artifactId, fullPath);
        return artifact;
    }

    Dictionary<string, object?> RawMetadata(Id65V3RuntimeEvidenceArtifact artifact) => new(StringComparer.Ordinal)
    {
        ["artifactId"] = artifact.ArtifactId,
        ["byteLength"] = artifact.ByteLength,
        ["capturedAtUtc"] = artifact.CapturedAtUtc,
        ["kind"] = artifact.Kind,
        ["repoRelativePath"] = artifact.RepoRelativePath,
        ["sha256"] = artifact.Sha256
    };

    string SessionPath(int ordinal, string sessionId, string leaf) =>
        assetPrefix + "capture-staging/" + runId + "/" +
        ordinal.ToString("D2", System.Globalization.CultureInfo.InvariantCulture) + "-" +
        sessionId + "/" + leaf;

    (string Id, int LevelId, string LoadCode, string TelemetryShape)[] sessionSpecs =
    [
        ("candidate-primary-edge-0", 65,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down", "primary"),
        ("candidate-edge-1", 65,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down", "death"),
        ("candidate-edge-2", 65,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down", "death"),
        ("candidate-cold-reset", 65,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Down", "stable"),
        ("retail-town-square", 13,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Triangle", "stable"),
        ("retail-gnastys-loot", 64,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Right", "stable"),
        ("retail-sunny-flight", 15,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Down", "stable")
    ];
    IReadOnlyDictionary<string, (string CheckId, int DurationMilliseconds)[]> timedChecks =
        new Dictionary<string, (string, int)[]>(StringComparer.Ordinal)
        {
            ["candidate-primary-edge-0"] =
            [
                ("candidate.camera-360-twice", 6_500),
                ("candidate.walk-center-perimeter-both-directions", 8_500),
                ("candidate.jump-land-center-each-corner", 8_500)
            ],
            ["retail-town-square"] =
            [
                ("retail.town-square.terrain", 3_500),
                ("retail.town-square.collision", 5_500),
                ("retail.town-square.mobys", 5_500),
                ("retail.town-square.camera", 5_500),
                ("retail.town-square.death-respawn", 5_500),
                ("retail.town-square.music", 10_500),
                ("retail.town-square.exit", 3_500),
                ("retail.town-square.totals", 2_500)
            ],
            ["retail-gnastys-loot"] =
            [
                ("retail.gnastys-loot.gameplay", 5_500),
                ("retail.gnastys-loot.exit", 3_500)
            ],
            ["retail-sunny-flight"] =
            [
                ("retail.sunny-flight.gameplay", 5_500),
                ("retail.sunny-flight.timer", 3_500),
                ("retail.sunny-flight.exit", 3_500)
            ]
        };

    string cuePath = Path.GetFullPath(Path.Combine(
        sourceRepositoryRoot,
        pendingPackage.SourceIdentity.CandidateCueRelativePath));
    string binPath = Path.GetFullPath(Path.Combine(
        sourceRepositoryRoot,
        pendingPackage.SourceIdentity.CandidateBinRelativePath));
    FileInfo executableInfo = new(liveExecutable);
    FileInfo cueInfo = new(cuePath);
    FileInfo binInfo = new(binPath);
    Id65V3RuntimeEvidenceFinalizer.StableFileRead cueStable =
        Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
            cuePath, 4 * 1024, keepBytes: false, context: "fixture CUE");
    Id65V3RuntimeEvidenceFinalizer.StableFileRead binStable =
        Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
            binPath, 64L * 1024 * 1024 * 1024, keepBytes: false, context: "fixture BIN");
    Id65V3RuntimeEvidenceFinalizer.StableFileRead executableStable =
        Id65V3RuntimeEvidenceFinalizer.ReadStableTrustedFile(
            liveExecutable, 1024L * 1024 * 1024, keepBytes: false,
            context: "fixture DuckStation executable");
    long executableDevice = executableStable.Device;
    long executableInode = checked((long)executableStable.Inode);
    DateTimeOffset epoch = new(2026, 8, 10, 19, 0, 0, TimeSpan.Zero);
    List<Id65V3RuntimeEvidenceSession> sessions = [];
    Dictionary<(string SessionId, string EventType, string Marker), string> eventTimes = [];
    Dictionary<(string SessionId, string Role), Id65V3RuntimeEvidenceArtifact> frames = [];
    Dictionary<string, Id65V3RuntimeEvidenceArtifact> recordings = new(StringComparer.Ordinal);
    string previousTerminalHash = new('0', 64);
    string finalTerminalAtUtc = string.Empty;
    Id65V3RuntimeProfileSnapshotEntry[] currentSnapshotEntries = snapshotEntries;

    for (int index = 0; index < sessionSpecs.Length; index++)
    {
        (string sessionId, int levelId, string loadCode, string telemetryShape) = sessionSpecs[index];
        DateTimeOffset sessionEpoch = epoch.AddSeconds(index * 300L);
        long monotonicBase = 1_000_000_000L + index * 300_000_000L;
        string At(int milliseconds) => FormatUtc(sessionEpoch.AddMilliseconds(milliseconds));
        long Mono(int milliseconds) => monotonicBase + milliseconds * 1_000L;

        int processIndex = reuseFirstProcessFingerprint && index == 1 ? 0 : index;
        DateTimeOffset processEpoch = epoch.AddSeconds(processIndex * 300L);
        long pid = 10_000 + processIndex;
        long startSeconds = processEpoch.ToUnixTimeSeconds();
        long startMicroseconds = 100_000 + processIndex;
        string processStartedAtUtc = FormatUtc(
            processEpoch.AddMilliseconds(processStartedMilliseconds));
        string[] argv = [liveExecutable, cuePath];
        string argvNulSha256 = Hash(Encoding.UTF8.GetBytes(string.Join('\0', argv) + '\0'));
        string environmentNul =
            $"HOME={Path.GetFullPath(liveExternalHomeRoot)}\0" +
            "LANG=C\0" +
            "LC_ALL=C\0" +
            "PATH=/usr/bin:/bin:/usr/sbin:/sbin\0" +
            "SPYRO_ID65_CAPTURE_ENVIRONMENT_END_V1=duckstation-id65-unattended-v1\0";
        string environmentNulSha256 = Hash(Encoding.UTF8.GetBytes(environmentNul));
        string processManifest =
            "id65-v3-runtime-process-instance-v2\n" +
            $"runId={runId}\n" +
            $"bootIdentity={bootIdentity}\n" +
            $"pid={pid}\n" +
            $"startSeconds={startSeconds}\n" +
            $"startMicroseconds={startMicroseconds}\n" +
            $"executableDevice={executableDevice}\n" +
            $"executableInode={executableInode}\n" +
            $"executableSha256={observedProfile.DuckStationExecutableSha256}\n" +
            $"argvNulSha256={argvNulSha256}\n" +
            $"environmentNulSha256={environmentNulSha256}\n" +
            $"externalHomeRealPath={Path.GetFullPath(liveExternalHomeRoot)}\n";
        string processInstanceId = "id65v3-process-" +
            Hash(Encoding.UTF8.GetBytes(processManifest))[..32];

        string beforeArtifactId = sessionId + ".profile-before";
        Id65V3RuntimeProfileSnapshot beforeSnapshot = new(
            "spyro-editor-id65-v3-runtime-profile-snapshot",
            2,
            beforeArtifactId,
            "before",
            sessionId,
            processInstanceId,
            observedProfile.ProfileId,
            At(profileBeforeCapturedMilliseconds),
            observedProfile.DuckStationExecutableSha256,
            observedProfile.HandoffReceiptSha256,
            observedProfile.AppContentManifestSha256,
            observedProfile.AppOutputTreeManifestSha256,
            observedProfile.PackageEnvelopeSha256,
            observedProfile.ExternalHomeSettingsSha256,
            observedProfile.ExternalHomeBiosSha256,
            observedProfile.HostNormalSettingsSentinelId,
            observedProfile.HostNormalSettingsSha256,
            currentSnapshotEntries);
        profileSnapshots.Add(beforeArtifactId, beforeSnapshot);
        Id65V3RuntimeEvidenceArtifact beforeArtifact = AddArtifact(
            beforeArtifactId,
            sessionId,
            "runtime-profile-before",
            SessionPath(index, sessionId, "runtime-profile-before.json"),
            VerifierCanonicalArtifactUtf8(beforeSnapshot),
            beforeSnapshot.CapturedAtUtc);

        Dictionary<string, object?> header = new(StringComparer.Ordinal)
        {
            ["bootIdentity"] = bootIdentity,
            ["format"] = journalFormat,
            ["formatVersion"] = 3,
            ["processInstanceId"] = processInstanceId,
            ["runOriginSha256"] = runOriginSha256,
            ["runId"] = runId,
            ["sessionId"] = sessionId,
            ["sessionOrdinal"] = index
        };
        foreach ((string name, string value) in capturePins)
            header[name] = value;
        List<(string Type, Dictionary<string, object?> Payload, int Milliseconds)> scheduled = [];
        void Schedule(string type, Dictionary<string, object?> payload, int milliseconds)
        {
            if (scheduled.Count != 0 && milliseconds <= scheduled[^1].Milliseconds)
                throw new InvalidOperationException("Synthetic capture schedule is not strictly ordered.");
            scheduled.Add((type, payload, milliseconds));
        }

        List<Dictionary<string, object?>> rawArtifacts = [RawMetadata(beforeArtifact)];
        Schedule("session-open", new() { ["previousSessionTerminalSha256"] = previousTerminalHash }, 1_000);
        Schedule("profile-before-captured", RawMetadata(beforeArtifact), 1_100);
        Dictionary<string, object?> processPayload = new(StringComparer.Ordinal)
        {
            ["argv"] = argv,
            ["argvNulSha256"] = argvNulSha256,
            ["environmentNulSha256"] = environmentNulSha256,
            ["executableByteLength"] = executableInfo.Length,
            ["executableDevice"] = executableDevice,
            ["executableInode"] = executableInode,
            ["executableRealPath"] = liveExecutable,
            ["executableSha256"] = observedProfile.DuckStationExecutableSha256,
            ["externalHomeRealPath"] = Path.GetFullPath(liveExternalHomeRoot),
            ["observedAtUtc"] = At(1_200),
            ["parentPid"] = 1,
            ["pid"] = pid,
            ["processStartedAtUtc"] = processStartedAtUtc,
            ["provider"] = "macos-libproc+kern-procargs2-external-home-v2",
            ["startMicroseconds"] = startMicroseconds,
            ["startSeconds"] = startSeconds
        };
        Schedule("process-start-observed", processPayload, 1_200);
        Schedule("disc-open-observed", new(StringComparer.Ordinal)
        {
            ["bindingMode"] = "exact-argv-cue+pinned-source-cue+read-only-open-bin-v1",
            ["binByteLength"] = binInfo.Length,
            ["binDevice"] = binStable.Device,
            ["binFd"] = "13r",
            ["binInode"] = checked((long)binStable.Inode),
            ["binLinkCount"] = binStable.LinkCount,
            ["binOpen"] = true,
            ["binRealPath"] = binPath,
            ["binRepoRelativePath"] = pendingPackage.SourceIdentity.CandidateBinRelativePath,
            ["binSha256"] = pendingPackage.SourceIdentity.CandidateBinSha256,
            ["cueByteLength"] = cueInfo.Length,
            ["cueDevice"] = cueStable.Device,
            ["cueInode"] = checked((long)cueStable.Inode),
            ["cueRealPath"] = cuePath,
            ["cueRepoRelativePath"] = pendingPackage.SourceIdentity.CandidateCueRelativePath,
            ["cueSha256"] = pendingPackage.SourceIdentity.CandidateCueSha256,
            ["cueSourceLinkCount"] = cueStable.LinkCount,
            ["failedV1Excluded"] = true,
            ["gdbListenerPid"] = pid,
            ["observedAtUtc"] = At(1_300),
            ["pid"] = pid,
            ["provider"] = "macos-libproc-argv-plus-open-bin-v1",
            ["startMicroseconds"] = startMicroseconds,
            ["startSeconds"] = startSeconds
        }, 1_300);
        long watcherPid = 20_000 + index;
        Schedule("watcher-connected", new()
        {
            ["ownedWatcherPid"] = watcherPid,
            ["provider"] = "pinned-watcher-subprocess-v1"
        }, 2_000);
        Schedule("manually-resumed", new() { ["operatorConfirmed"] = true }, 3_000);

        string recordingId = sessionId + ".screen-recording";
        string recordingRole = sessionId + ".full-window-recording";
        long recorderPid = 40_000 + index;
        int recordingStartMilliseconds = sessionId == "candidate-primary-edge-0"
            ? primaryRecordingStartMilliseconds
            : 3_500;
        Dictionary<string, object?> recordingStart = new(StringComparer.Ordinal)
        {
            ["artifactId"] = recordingId,
            ["backend"] = "macos-screencapture-window-v1",
            ["boundsHeight"] = 720,
            ["boundsWidth"] = 960,
            ["boundsX"] = 20,
            ["boundsY"] = 30,
            ["captureRole"] = recordingRole,
            ["mediaStartAtUtc"] = At(recordingStartMilliseconds),
            ["mediaStartMonotonicMicroseconds"] = Mono(recordingStartMilliseconds),
            ["ownedRecorderPid"] = recorderPid,
            ["ownerPid"] = pid,
            ["ownerStartMicroseconds"] = startMicroseconds,
            ["ownerStartSeconds"] = startSeconds,
            ["windowId"] = 30_000 + index,
            ["windowOwnerPid"] = pid
        };
        Schedule("recording-started", recordingStart, recordingStartMilliseconds);
        int selectorMilliseconds = sessionId == "candidate-primary-edge-0" ? 6_000 : 4_000;
        Schedule("selector-started", new() { ["markerId"] = "selector" }, selectorMilliseconds);
        List<(string Leaf, string Role, int Milliseconds)> frameSpecs = [];
        int? fallMilliseconds = null;
        int? respawnMilliseconds = null;
        int? fallTransitionMilliseconds = null;
        int? respawnTransitionMilliseconds = null;
        if (sessionId == "candidate-primary-edge-0")
        {
            Schedule("check-started", new() { ["markerId"] = "candidate.completed-landing-hud4" }, 7_000);
            Schedule("landing-marked", new() { ["markerId"] = "landing" }, 18_300);
            Schedule("check-ended", new() { ["markerId"] = "candidate.completed-landing-hud4" }, 18_800);
            Schedule("hold-started", new() { ["markerId"] = "stable-hold" }, 19_300);
            Schedule("hold-ended", new() { ["markerId"] = "stable-hold" }, 39_800);
            frameSpecs.Add(("lone-pad", "candidate.lone-pad-visibility", 40_300));
            int cursor = 40_800;
            foreach ((string checkId, int duration) in timedChecks[sessionId])
            {
                Schedule("check-started", new() { ["markerId"] = checkId }, cursor);
                cursor += duration;
                Schedule("check-ended", new() { ["markerId"] = checkId }, cursor);
                cursor += 500;
            }
            frameSpecs.Add(("edge-before", "candidate.primary-edge-4-to-3-same-pad-yaw.pre-fall-frame", cursor));
            fallMilliseconds = cursor + 500;
            fallTransitionMilliseconds = fallMilliseconds.Value + 100;
            Schedule("fall-started", new() { ["markerId"] = "edge-fall" }, fallMilliseconds.Value);
            respawnMilliseconds = fallMilliseconds.Value + 5_500;
            respawnTransitionMilliseconds = fallTransitionMilliseconds.Value + 1_000;
            Schedule("respawn-marked", new() { ["markerId"] = "edge-respawn" }, respawnMilliseconds.Value);
            frameSpecs.Add(("edge-after", "candidate.primary-edge-4-to-3-same-pad-yaw.post-respawn-frame", respawnMilliseconds.Value + 500));
        }
        else if (sessionId is "candidate-edge-1" or "candidate-edge-2")
        {
            string checkId = sessionId == "candidate-edge-1"
                ? "candidate.edge-1-same-pad-yaw"
                : "candidate.edge-2-same-pad-yaw";
            Schedule("landing-marked", new() { ["markerId"] = "landing" }, 5_000);
            frameSpecs.Add(("edge-before", checkId + ".pre-fall-frame", 5_500));
            fallMilliseconds = 6_000;
            fallTransitionMilliseconds = fallMilliseconds.Value + 100;
            Schedule("fall-started", new() { ["markerId"] = "edge-fall" }, 6_000);
            respawnMilliseconds = 11_500;
            respawnTransitionMilliseconds = fallTransitionMilliseconds.Value + 1_000;
            Schedule("respawn-marked", new() { ["markerId"] = "edge-respawn" }, 11_500);
            frameSpecs.Add((
                "edge-after",
                sessionId == "candidate-edge-2"
                    ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                    : checkId + ".post-respawn-frame",
                12_000));
        }
        else if (sessionId == "candidate-cold-reset")
        {
            Schedule("landing-marked", new() { ["markerId"] = "landing" }, 9_500);
            frameSpecs.Add(("cold-reset-after", "candidate.cold-reset-same-pad-yaw.post-reset-frame", 10_000));
        }
        else
        {
            Schedule("landing-marked", new() { ["markerId"] = "landing" }, 5_000);
            int cursor = 5_500;
            foreach ((string checkId, int duration) in timedChecks[sessionId])
            {
                Schedule("check-started", new() { ["markerId"] = checkId }, cursor);
                cursor += duration;
                Schedule("check-ended", new() { ["markerId"] = checkId }, cursor);
                cursor += 500;
            }
        }

        foreach ((string leaf, string role, int milliseconds) in frameSpecs.OrderBy(item => item.Milliseconds))
        {
            string artifactId = sessionId + "." + leaf;
            Id65V3RuntimeEvidenceArtifact frame = AddArtifact(
                artifactId,
                sessionId,
                "frame",
                SessionPath(index, sessionId, leaf + ".png"),
                BuildPngFixture(sessionId + ":" + leaf),
                At(milliseconds));
            Dictionary<string, object?> metadata = RawMetadata(frame);
            metadata["backend"] = "macos-screencapture-window-v1";
            metadata["boundsHeight"] = 720;
            metadata["boundsWidth"] = 960;
            metadata["boundsX"] = 20;
            metadata["boundsY"] = 30;
            metadata["captureRole"] = role;
            metadata["ownerPid"] = pid;
            metadata["ownerStartMicroseconds"] = startMicroseconds;
            metadata["ownerStartSeconds"] = startSeconds;
            metadata["windowId"] = 30_000 + index;
            metadata["windowOwnerPid"] = pid;
            rawArtifacts.Add(metadata);
            frames.Add((sessionId, role), frame);
            scheduled.Add(("frame-captured", new()
            {
                ["artifactId"] = artifactId,
                ["captureRole"] = role
            }, milliseconds));
        }
        scheduled.Sort((left, right) => left.Milliseconds.CompareTo(right.Milliseconds));
        if (scheduled.Zip(scheduled.Skip(1)).Any(pair => pair.First.Milliseconds >= pair.Second.Milliseconds))
            throw new InvalidOperationException("Synthetic capture frame overlaps another event.");

        int recordingStopMilliseconds = scheduled[^1].Milliseconds + 500;
        int watcherStopMilliseconds = recordingStopMilliseconds + 500;
        Id65V3RuntimeEvidenceArtifact recordingArtifact = AddArtifact(
            recordingId,
            sessionId,
            "screen-recording",
            SessionPath(index, sessionId, "session-screen-recording.mov"),
            BuildIsoBmffFixture(sessionId),
            At(recordingStopMilliseconds),
            At(recordingStartMilliseconds),
            At(recordingStopMilliseconds));
        recordings.Add(sessionId, recordingArtifact);
        Dictionary<string, object?> recordingMetadata = RawMetadata(recordingArtifact);
        foreach ((string key, object? value) in recordingStart)
            recordingMetadata[key] = value;
        recordingMetadata["mediaEndAtUtc"] = At(recordingStopMilliseconds);
        recordingMetadata["mediaEndMonotonicMicroseconds"] = Mono(recordingStopMilliseconds);
        rawArtifacts.Add(recordingMetadata);
        Schedule("recording-stopped", new()
        {
            ["artifactId"] = recordingId,
            ["mediaEndAtUtc"] = At(recordingStopMilliseconds),
            ["mediaEndMonotonicMicroseconds"] = Mono(recordingStopMilliseconds),
            ["ownedRecorderPid"] = recorderPid
        }, recordingStopMilliseconds);

        List<Dictionary<string, object?>> sampleRows = [];
        List<string> sampleLines = [];
        for (int sampleMilliseconds = 3_100; sampleMilliseconds < watcherStopMilliseconds; sampleMilliseconds += 100)
        {
            int lives = 4;
            int state = 0;
            int playerZ = 1_200;
            if (sampleMilliseconds < 3_500)
            {
                lives = 0;
                state = 13;
                playerZ = 0;
            }
            else if (sampleMilliseconds < 4_000)
            {
                lives = 4;
                state = 1;
            }
            else if (sessionId == "candidate-primary-edge-0" &&
                sampleMilliseconds is >= 7_000 and < 11_000)
            {
                state = 3;
            }
            else if (sessionId == "candidate-primary-edge-0" &&
                sampleMilliseconds is >= 11_000 and < 16_000)
            {
                state = 1;
            }
            else if (fallTransitionMilliseconds is int fall && sampleMilliseconds >= fall)
            {
                lives = 3;
                state = 4;
                playerZ = 900;
                if (respawnTransitionMilliseconds is int respawn && sampleMilliseconds >= respawn)
                {
                    lives = 3;
                    state = 0;
                    playerZ = 1_200;
                }
            }
            int ordinal = sampleRows.Count;
            Dictionary<string, object?> row = new(StringComparer.Ordinal)
            {
                ["atUtc"] = At(sampleMilliseconds),
                ["elapsedMilliseconds"] = sampleMilliseconds - 3_100,
                ["lives"] = lives,
                ["ordinal"] = ordinal,
                ["playerZ"] = playerZ,
                ["state"] = state
            };
            sampleRows.Add(row);
            sampleLines.Add(Sample(At(sampleMilliseconds), ordinal, sampleMilliseconds - 3_100, lives, state, playerZ));
        }
        string watcherArtifactId = sessionId + ".watcher";
        Id65V3RuntimeEvidenceArtifact watcherArtifact = AddArtifact(
            watcherArtifactId,
            sessionId,
            "watcher-stdout",
            SessionPath(index, sessionId, "watcher-stdout.log"),
            Encoding.UTF8.GetBytes(WatcherLog(sampleLines, string.Empty)),
            At(watcherStopMilliseconds));
        Dictionary<string, object?> watcherMetadata = RawMetadata(watcherArtifact);
        rawArtifacts.Add(watcherMetadata);
        Dictionary<string, object?> watcherStoppedPayload = new(watcherMetadata, StringComparer.Ordinal)
        {
            ["ownedWatcherPid"] = watcherPid
        };
        Schedule("watcher-stopped", watcherStoppedPayload, watcherStopMilliseconds);

        List<Dictionary<string, object?>> events = [];
        string currentHash = previousTerminalHash;
        string? firstMediaEventAtUtc = null;
        Dictionary<string, object?> AddEvent(string type, Dictionary<string, object?> payload, int milliseconds)
        {
            string eventAtUtc = At(milliseconds);
            if (exerciseWithinSessionUtcTies && index == 0)
            {
                if (type is "session-open" or "profile-before-captured" or "process-start-observed")
                    eventAtUtc = At(1_200);
                else if (type == "media-artifact-bound")
                {
                    firstMediaEventAtUtc ??= eventAtUtc;
                    eventAtUtc = firstMediaEventAtUtc;
                }
            }
            Dictionary<string, object?> item = new(StringComparer.Ordinal)
            {
                ["atUtc"] = eventAtUtc,
                ["eventType"] = type,
                ["monotonicMicroseconds"] = Mono(milliseconds),
                ["payload"] = payload,
                ["previousEventSha256"] = currentHash,
                ["sequence"] = events.Count
            };
            item["eventSha256"] = CaptureEventHash(header, item, payload);
            events.Add(item);
            currentHash = (string)item["eventSha256"]!;
            string marker = payload.TryGetValue("markerId", out object? markerValue)
                ? (string)markerValue!
                : string.Empty;
            eventTimes[(sessionId, type, marker)] = At(milliseconds);
            return item;
        }
        foreach ((string type, Dictionary<string, object?> payload, int milliseconds) in scheduled)
            AddEvent(type, payload, milliseconds);

        string[] watcherRoles = sessionId switch
        {
            "candidate-primary-edge-0" =>
                ["landing-marked", "hold-started", "hold-ended", "fall-started", "respawn-marked"],
            "candidate-edge-1" or "candidate-edge-2" =>
                ["landing-marked", "fall-started", "respawn-marked"],
            _ => ["landing-marked"]
        };
        int bindingMilliseconds = watcherStopMilliseconds + 10;
        foreach (string role in watcherRoles)
        {
            Dictionary<string, object?> marker = events.Single(item => (string)item["eventType"]! == role);
            string markerAtUtc = (string)marker["atUtc"]!;
            int offsetMilliseconds = role switch
            {
                "hold-ended" => -100,
                "respawn-marked" => 0,
                _ => 100
            };
            string sampleAtUtc = FormatUtc(ParseUtc(markerAtUtc).AddMilliseconds(offsetMilliseconds));
            Dictionary<string, object?> row = sampleRows.Single(item =>
                (string)item["atUtc"]! == sampleAtUtc);
            AddEvent("watcher-marker-bound", new()
            {
                ["markerRole"] = role,
                ["operatorEventSequence"] = marker["sequence"],
                ["sampleAtUtc"] = row["atUtc"],
                ["sampleElapsedMilliseconds"] = row["elapsedMilliseconds"],
                ["sampleOrdinal"] = row["ordinal"]
            }, bindingMilliseconds);
            bindingMilliseconds += 10;
        }

        int processExitMilliseconds = bindingMilliseconds + 500;
        AddEvent("process-exit-observed", new()
        {
            ["pid"] = pid,
            ["processExitedAtUtc"] = At(processExitMilliseconds),
            ["startMicroseconds"] = startMicroseconds,
            ["startSeconds"] = startSeconds
        }, processExitMilliseconds);
        int afterMilliseconds = processExitMilliseconds + 500;
        string afterArtifactId = sessionId + ".profile-after";
        Id65V3RuntimeProfileSnapshotEntry[] afterSnapshotEntries = currentSnapshotEntries;
        if (exerciseUnicodeChangedPaths && index == 0)
        {
            afterSnapshotEntries = currentSnapshotEntries.Select(entry =>
                entry.RelativePath is "cache/\ue000.bin" or "cache/\U0001F600.bin"
                    ? entry with
                    {
                        ByteLength = entry.ByteLength + 1,
                        Sha256 = MutateNibble(entry.Sha256)
                    }
                    : entry).ToArray();
        }
        Id65V3RuntimeProfileSnapshot afterSnapshot = beforeSnapshot with
        {
            SnapshotId = afterArtifactId,
            Phase = "after",
            CapturedAtUtc = At(afterMilliseconds),
            DataFiles = afterSnapshotEntries
        };
        currentSnapshotEntries = afterSnapshotEntries;
        profileSnapshots.Add(afterArtifactId, afterSnapshot);
        Id65V3RuntimeEvidenceArtifact afterArtifact = AddArtifact(
            afterArtifactId,
            sessionId,
            "runtime-profile-after",
            SessionPath(index, sessionId, "runtime-profile-after.json"),
            VerifierCanonicalArtifactUtf8(afterSnapshot),
            afterSnapshot.CapturedAtUtc);
        Dictionary<string, object?> afterMetadata = RawMetadata(afterArtifact);
        rawArtifacts.Add(afterMetadata);
        AddEvent("profile-after-captured", afterMetadata, afterMilliseconds);
        int mediaMilliseconds = afterMilliseconds + 10;
        foreach (Dictionary<string, object?> media in rawArtifacts
                     .Where(item => (string)item["kind"]! is "frame" or "screen-recording")
                     .OrderBy(item => (string)item["artifactId"]!, StringComparer.Ordinal))
        {
            AddEvent("media-artifact-bound", media, mediaMilliseconds);
            mediaMilliseconds += 10;
        }
        AddEvent("session-sealed", new()
        {
            ["rawArtifactIds"] = rawArtifacts.Select(item => (string)item["artifactId"]!)
                .Order(StringComparer.Ordinal).ToArray()
        }, mediaMilliseconds);

        Dictionary<string, object?> journal = new(header, StringComparer.Ordinal)
        {
            ["events"] = events
        };
        byte[] liveBytes = events.SelectMany(item =>
            CanonicalArtifactUtf8(item).Concat(new byte[] { (byte)'\n' })).ToArray();
        string liveRelativePath = SessionPath(
            index,
            sessionId,
            "capture-events-live.ndjson");
        string liveFullPath = Path.Combine(artifactRepositoryRoot, liveRelativePath);
        File.WriteAllBytes(liveFullPath, liveBytes);
        File.SetUnixFileMode(liveFullPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        liveProjectionBytes.Add(sessionId, liveBytes);
        liveProjectionPaths.Add(sessionId, liveRelativePath);
        byte[] bytes = CanonicalArtifactUtf8(journal);
        string journalArtifactId = sessionId + ".capture-journal";
        Id65V3RuntimeEvidenceArtifact journalArtifact = AddArtifact(
            journalArtifactId,
            sessionId,
            "capture-event-journal",
            SessionPath(index, sessionId, "capture-event-journal.json"),
            bytes,
            At(mediaMilliseconds));
        journalBytes.Add(journalArtifactId, bytes);
        previousTerminalHash = currentHash;
        finalTerminalAtUtc = At(mediaMilliseconds);

        Dictionary<string, object?> bound(string role) => events.Single(item =>
            (string)item["eventType"]! == "watcher-marker-bound" &&
            (string)((Dictionary<string, object?>)item["payload"]!)["markerRole"]! == role);
        (string? AtUtc, int? Ordinal) BoundSample(string role)
        {
            if (!watcherRoles.Contains(role, StringComparer.Ordinal))
                return (null, null);
            Dictionary<string, object?> payload = (Dictionary<string, object?>)bound(role)["payload"]!;
            return ((string)payload["sampleAtUtc"]!, Convert.ToInt32(payload["sampleOrdinal"], System.Globalization.CultureInfo.InvariantCulture));
        }
        (string? landingAt, int? landingOrdinal) = BoundSample("landing-marked");
        (string? holdStartAt, int? holdStartOrdinal) = BoundSample("hold-started");
        (string? holdEndAt, int? holdEndOrdinal) = BoundSample("hold-ended");
        (string? fallAt, int? fallOrdinal) = BoundSample("fall-started");
        (string? respawnAt, int? respawnOrdinal) = BoundSample("respawn-marked");
        Id65V3RuntimeEvidenceSession session = new(
            sessionId,
            processInstanceId,
            levelId,
            loadCode,
            ColdBoot: true,
            NewGame: true,
            CardsNone: true,
            CheatsOff: true,
            SaveStateLoaded: false,
            SaveStateCreated: false,
            InGameSaveUsed: false,
            processStartedAtUtc,
            At(2_000),
            At(3_000),
            At(selectorMilliseconds),
            landingAt,
            holdStartAt,
            holdEndAt,
            fallAt,
            respawnAt,
            At(processExitMilliseconds),
            landingOrdinal,
            holdStartOrdinal,
            holdEndOrdinal,
            fallOrdinal,
            respawnOrdinal,
            watcherArtifactId,
            new(
                journalArtifact.ArtifactId,
                journalFormat,
                3,
                runId,
                index,
                sessionId,
                bootIdentity,
                processInstanceId,
                currentHash),
            beforeArtifactId,
            afterArtifactId,
            SampleSummary: null);
        Id65V3TelemetryParseResult parsed = Id65V3RuntimeEvidenceContract.ParseTelemetryLog(
            WatcherLog(sampleLines, string.Empty));
        session = session with
        {
            SampleSummary = Id65V3RuntimeEvidenceContract.RecomputeTelemetrySummary(parsed, session)
        };
        sessions.Add(session);
    }

    string[] checkIds =
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
        "candidate.cold-reset-same-pad-yaw",
        "retail.town-square.terrain",
        "retail.town-square.collision",
        "retail.town-square.mobys",
        "retail.town-square.camera",
        "retail.town-square.death-respawn",
        "retail.town-square.music",
        "retail.town-square.exit",
        "retail.town-square.totals",
        "retail.gnastys-loot.gameplay",
        "retail.gnastys-loot.exit",
        "retail.sunny-flight.gameplay",
        "retail.sunny-flight.timer",
        "retail.sunny-flight.exit"
    ];
    List<Id65V3RuntimeEvidenceCheck> checks = [];
    foreach (string checkId in checkIds)
    {
        string sessionId = SessionForSmokeCheck(checkId);
        List<Id65V3RuntimeEvidenceSlice> references = [];
        string observedAtUtc;
        if (checkId == "candidate.cold-reset-same-pad-yaw")
        {
            Id65V3RuntimeEvidenceArtifact before = frames[(
                "candidate-edge-2",
                "candidate.edge-2-same-pad-yaw.post-respawn-and-cold-reset-pre-frame")];
            Id65V3RuntimeEvidenceArtifact after = frames[(
                sessionId,
                "candidate.cold-reset-same-pad-yaw.post-reset-frame")];
            references.Add(new(before.ArtifactId, null, null, before.Sha256));
            references.Add(new(after.ArtifactId, null, null, after.Sha256));
            AddRecordingSlice(references, sessionId, "selector-started", "selector", "landing-marked", "landing");
            observedAtUtc = after.CapturedAtUtc;
        }
        else if (checkId.Contains("same-pad-yaw", StringComparison.Ordinal))
        {
            string afterRole = sessionId == "candidate-edge-2"
                ? checkId + ".post-respawn-and-cold-reset-pre-frame"
                : checkId + ".post-respawn-frame";
            Id65V3RuntimeEvidenceArtifact before = frames[(sessionId, checkId + ".pre-fall-frame")];
            Id65V3RuntimeEvidenceArtifact after = frames[(sessionId, afterRole)];
            references.Add(new(before.ArtifactId, null, null, before.Sha256));
            references.Add(new(after.ArtifactId, null, null, after.Sha256));
            AddRecordingSlice(references, sessionId, "fall-started", "edge-fall", "respawn-marked", "edge-respawn");
            observedAtUtc = after.CapturedAtUtc;
        }
        else if (checkId == "candidate.lone-pad-visibility")
        {
            Id65V3RuntimeEvidenceArtifact frame = frames[(sessionId, checkId)];
            references.Add(new(frame.ArtifactId, null, null, frame.Sha256));
            observedAtUtc = frame.CapturedAtUtc;
        }
        else
        {
            string startType = checkId == "candidate.hands-off-20-seconds" ? "hold-started" : "check-started";
            string endType = checkId == "candidate.hands-off-20-seconds" ? "hold-ended" : "check-ended";
            string marker = checkId == "candidate.hands-off-20-seconds" ? "stable-hold" : checkId;
            AddRecordingSlice(references, sessionId, startType, marker, endType, marker);
            observedAtUtc = eventTimes[(sessionId, endType, marker)];
        }
        bool telemetryCorroborated = checkId is
            "candidate.completed-landing-hud4" or
            "candidate.hands-off-20-seconds" or
            "candidate.primary-edge-4-to-3-same-pad-yaw" or
            "candidate.edge-1-same-pad-yaw" or
            "candidate.edge-2-same-pad-yaw";
        checks.Add(new(
            checkId,
            sessionId,
            Id65V3RuntimeEvidenceCheckStatus.Passed,
            observedAtUtc,
            SyntheticCheckAttestation(checkId),
            references.ToArray(),
            telemetryCorroborated));
    }

    void AddRecordingSlice(
        List<Id65V3RuntimeEvidenceSlice> references,
        string sessionId,
        string startType,
        string startMarker,
        string endType,
        string endMarker)
    {
        Id65V3RuntimeEvidenceArtifact recording = recordings[sessionId];
        DateTimeOffset mediaStart = ParseUtc(recording.MediaStartAtUtc!);
        long start = checked((long)(ParseUtc(eventTimes[(sessionId, startType, startMarker)]) - mediaStart)
            .TotalMilliseconds);
        long end = checked((long)(ParseUtc(eventTimes[(sessionId, endType, endMarker)]) - mediaStart)
            .TotalMilliseconds);
        references.Add(new(recording.ArtifactId, start, end, null));
    }

    Id65V3RuntimeEvidenceSession edge2Session = sessions.Single(session =>
        session.SessionId == "candidate-edge-2");
    Id65V3RuntimeEvidenceSession coldSession = sessions.Single(session =>
        session.SessionId == "candidate-cold-reset");
    Id65V3RuntimeEvidenceColdReset coldReset = new(
        Id65V3RuntimeEvidenceCheckStatus.Passed,
        edge2Session.ProcessExitedAtUtc,
        coldSession.ProcessStartedAtUtc,
        "candidate-cold-reset",
        "candidate.cold-reset-same-pad-yaw");
    Id65V3RuntimeEvidenceRetailControl[] retailControls =
    [
        new(
            "town-square",
            13,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Triangle",
            Id65V3RuntimeEvidenceCheckStatus.Passed,
            checkIds.Where(id => id.StartsWith("retail.town-square.", StringComparison.Ordinal)).ToArray()),
        new(
            "gnastys-loot",
            64,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Left, then Right",
            Id65V3RuntimeEvidenceCheckStatus.Passed,
            checkIds.Where(id => id.StartsWith("retail.gnastys-loot.", StringComparison.Ordinal)).ToArray()),
        new(
            "sunny-flight",
            15,
            "Select; then R1, R2, L1, L2, R1, L1, R2, L2; then Cross, then Down",
            Id65V3RuntimeEvidenceCheckStatus.Passed,
            checkIds.Where(id => id.StartsWith("retail.sunny-flight.", StringComparison.Ordinal)).ToArray())
    ];
    StringBuilder closureManifest = new();
    closureManifest.Append("id65-v3-runtime-capture-closure-v2\n")
        .Append("runId=").Append(runId).Append('\n')
        .Append("runOriginSha256=").Append(runOriginSha256).Append('\n');
    for (int ordinal = 0; ordinal < sessions.Count; ordinal++)
    {
        Id65V3RuntimeEvidenceSession session = sessions[ordinal];
        Id65V3RuntimeCaptureJournalReference journalReference = session.CaptureJournal ??
            throw new InvalidOperationException("Synthetic session lacks its capture-journal reference.");
        Id65V3RuntimeEvidenceArtifact journalArtifact = artifacts.Single(item =>
            item.ArtifactId == journalReference.ArtifactId);
        closureManifest.Append("session=")
            .Append(ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('|')
            .Append(session.SessionId).Append('|')
            .Append(journalArtifact.Sha256).Append('|')
            .Append(Hash(liveProjectionBytes[session.SessionId])).Append('|')
            .Append(journalReference.TerminalEventSha256).Append('\n');
    }
    foreach (Id65V3RuntimeEvidenceArtifact artifact in artifacts.OrderBy(
                 item => item.ArtifactId,
                 StringComparer.Ordinal))
    {
        closureManifest.Append("artifact=").Append(artifact.ArtifactId).Append('|')
            .Append(artifact.Sha256).Append('|')
            .Append(artifact.ByteLength.ToString(System.Globalization.CultureInfo.InvariantCulture)).Append('\n');
    }
    string captureClosureSha256 = Hash(Encoding.UTF8.GetBytes(closureManifest.ToString()));
    checks = checks.Select(check => check with
    {
        Attestation = SyntheticCheckAttestation(check.CheckId)
    }).ToList();
    Id65V3RuntimeEvidenceOperatorCheckReview[] operatorChecks = checks.Select(check =>
        new Id65V3RuntimeEvidenceOperatorCheckReview(
            check.CheckId,
            check.SessionId,
            check.Status,
            Id65V3RuntimeEvidenceFinalizer.RecomputeEvidenceBindingSha256(
                captureClosureSha256,
                check,
                artifacts),
            check.Attestation!)).ToArray();
    string attestedAtUtc = FormatUtc(ParseUtc(finalTerminalAtUtc).AddSeconds(1));
    string operatorName = "synthetic-contract-smoke-operator";
    string safetyStatement =
        "Synthetic operator reviewed all profile and session safety assertions individually.";
    Id65V3RuntimeEvidenceOperatorSessionReview[] operatorSessions = sessions.Select(session =>
        new Id65V3RuntimeEvidenceOperatorSessionReview(
            session.SessionId,
            session.NewGame,
            session.CardsNone,
            session.CheatsOff,
            session.SaveStateLoaded,
            session.SaveStateCreated,
            session.InGameSaveUsed,
            $"Synthetic operator reviewed the exact safe cold-run contract for {session.SessionId}."))
        .ToArray();
    byte[] externalAttestationBytes = CanonicalArtifactUtf8(new
    {
        format = Id65V3RuntimeEvidenceFinalizer.HumanAttestationFormat,
        formatVersion = Id65V3RuntimeEvidenceFinalizer.HumanAttestationFormatVersion,
        runId,
        runOriginSha256,
        finalTerminalEventSha256 = previousTerminalHash,
        captureClosureSha256,
        @operator = operatorName,
        attestedAtUtc,
        runtimeSafety = new
        {
            saveStateLoaded = false,
            saveStateCreated = false,
            inGameSaveUsed = false,
            activeCheatCount = 0,
            statement = safetyStatement
        },
        sessions = operatorSessions.Select(item => new
        {
            item.SessionId,
            item.NewGame,
            item.CardsNone,
            item.CheatsOff,
            item.SaveStateLoaded,
            item.SaveStateCreated,
            item.InGameSaveUsed,
            item.Statement
        }).ToArray(),
        checks = operatorChecks.Select(item => new
        {
            item.CheckId,
            item.SessionId,
            status = "passed",
            item.EvidenceBindingSha256,
            item.Statement
        }).ToArray()
    });
    string attestationSha256 = Hash(externalAttestationBytes);
    Id65V3RuntimeEvidenceCaptureProvenance captureProvenance = new(
        runId,
        assetPrefix + "capture-staging/" + runId + "/run-origin.json",
        runOriginSha256,
        runOriginBytes.LongLength,
        previousTerminalHash,
        sessions.Select((session, ordinal) => new Id65V3RuntimeEvidenceLiveProjection(
            ordinal,
            session.SessionId,
            liveProjectionPaths[session.SessionId],
            Hash(liveProjectionBytes[session.SessionId]),
            liveProjectionBytes[session.SessionId].LongLength,
            session.CaptureJournal!.TerminalEventSha256)).ToArray(),
        captureClosureSha256);
    Id65V3RuntimeEvidenceOperatorReview operatorReview = new(
        attestationSha256,
        operatorName,
        attestedAtUtc,
        runId,
        runOriginSha256,
        previousTerminalHash,
        captureClosureSha256,
        new(false, false, false, 0, safetyStatement),
        operatorSessions,
        operatorChecks);
    Id65V3RuntimeEvidenceDerivedResult derived = pendingPackage.DerivedResult with
    {
        State = Id65V3RuntimeEvidenceState.FocusedRuntimePass,
        SourceValid = true,
        PinnedRuntimeFilesReadbackValid = true,
        RuntimeProfileValid = true,
        ArtifactReadbackValid = true,
        DiscIdentityValid = true,
        TelemetryValid = true,
        VisualChecklistComplete = true,
        ColdResetValid = true,
        RetailControlsValid = true,
        EnvironmentHeaderRepairV3RuntimeAccepted = true,
        RuntimeEvidenceAccepted = true
    };
    Id65V3RuntimeEvidencePackage package = Id65V3RuntimeEvidenceContract.WithRecomputedIntegrity(
        pendingPackage with
        {
            EvidenceId = "id65-v3-focused-runtime-pass-attestation-" + attestationSha256,
            RecordedAtUtc = attestedAtUtc,
            DeclaredState = Id65V3RuntimeEvidenceState.FocusedRuntimePass,
            RuntimeProfile = observedProfile,
            ArtifactCatalog = artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal).ToArray(),
            Sessions = sessions.ToArray(),
            VisualChecks = checks.ToArray(),
            ColdReset = coldReset,
            RetailControls = retailControls,
            CaptureProvenance = captureProvenance,
            OperatorReview = operatorReview,
            DerivedResult = derived
        });
    return new(
        package,
        readbackContext,
        artifactRepositoryRoot,
        artifactPaths,
        profileSnapshots,
        journalBytes);
}

static string SessionForSmokeCheck(string checkId)
{
    if (checkId == "candidate.edge-1-same-pad-yaw")
        return "candidate-edge-1";
    if (checkId == "candidate.edge-2-same-pad-yaw")
        return "candidate-edge-2";
    if (checkId == "candidate.cold-reset-same-pad-yaw")
        return "candidate-cold-reset";
    if (checkId.StartsWith("candidate.", StringComparison.Ordinal))
        return "candidate-primary-edge-0";
    if (checkId.StartsWith("retail.town-square.", StringComparison.Ordinal))
        return "retail-town-square";
    if (checkId.StartsWith("retail.gnastys-loot.", StringComparison.Ordinal))
        return "retail-gnastys-loot";
    if (checkId.StartsWith("retail.sunny-flight.", StringComparison.Ordinal))
        return "retail-sunny-flight";
    throw new InvalidOperationException($"Unknown smoke check {checkId}.");
}

static Id65V3RuntimeProfileSnapshotEntry SnapshotEntry(string relativePath, string sourcePath)
{
    FileInfo info = new(sourcePath);
    return new(relativePath, info.Length, HashFile(sourcePath));
}

static Id65V3RuntimeProfileSnapshot SnapshotWithAdditionalDataFile(
    Id65V3RuntimeProfileSnapshot snapshot,
    string relativePath) =>
    snapshot with
    {
        DataFiles = snapshot.DataFiles
            .Append(new Id65V3RuntimeProfileSnapshotEntry(relativePath, 1, new string('c', 64)))
            .OrderBy(entry => entry.RelativePath, SmokePythonUnicodeComparer.Instance)
            .ToArray()
    };

static string[] UnicodeVolatileFixturePaths() =>
[
    "cache/\U0001F600.bin",
    "cache/\ue000.bin",
    "cache/c1-\u0085.bin",
    "cache/a|b.bin"
];

static void RequireProfileManifestPathAccepted(MethodInfo guard, string relativePath)
{
    try
    {
        _ = guard.Invoke(null, [relativePath]);
    }
    catch (TargetInvocationException exception)
    {
        throw new InvalidOperationException(
            $"Python-permitted snapshot path was refused: {JsonSerializer.Serialize(relativePath)}.",
            exception.InnerException ?? exception);
    }
}

static void RequireProfileManifestPathRefused(MethodInfo guard, string relativePath)
{
    try
    {
        _ = guard.Invoke(null, [relativePath]);
    }
    catch (TargetInvocationException exception) when (exception.InnerException is InvalidDataException)
    {
        return;
    }
    throw new InvalidOperationException(
        $"Python-forbidden snapshot path was accepted: {JsonSerializer.Serialize(relativePath)}.");
}

static string CopyFixtureFile(string source, string destination)
{
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    File.Copy(source, destination, overwrite: true);
    return Path.GetFullPath(destination);
}

static DateTimeOffset ParseUtc(string value) =>
    DateTimeOffset.ParseExact(
        value,
        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
        System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.AssumeUniversal |
            System.Globalization.DateTimeStyles.AdjustToUniversal);

static string FormatUtc(DateTimeOffset value) =>
    value.UtcDateTime.ToString(
        "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
        System.Globalization.CultureInfo.InvariantCulture);

static byte[] BuildPngFixture(string seed)
{
    byte[] color = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
    byte[] scanline = [0, color[0], color[1], color[2], 255];
    using MemoryStream compressed = new();
    using (ZLibStream zlib = new(compressed, CompressionLevel.SmallestSize, leaveOpen: true))
        zlib.Write(scanline);

    using MemoryStream png = new();
    png.Write([137, 80, 78, 71, 13, 10, 26, 10]);
    byte[] ihdr = new byte[13];
    BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(0, 4), 1);
    BinaryPrimitives.WriteUInt32BigEndian(ihdr.AsSpan(4, 4), 1);
    ihdr[8] = 8;
    ihdr[9] = 6;
    WritePngChunk(png, "IHDR", ihdr);
    WritePngChunk(png, "IDAT", compressed.ToArray());
    WritePngChunk(png, "IEND", []);
    return png.ToArray();
}

static void WritePngChunk(Stream destination, string type, byte[] data)
{
    byte[] typeBytes = Encoding.ASCII.GetBytes(type);
    Span<byte> word = stackalloc byte[4];
    BinaryPrimitives.WriteUInt32BigEndian(word, checked((uint)data.Length));
    destination.Write(word);
    destination.Write(typeBytes);
    destination.Write(data);
    uint crc = 0xFFFFFFFF;
    crc = UpdateFixturePngCrc(crc, typeBytes);
    crc = UpdateFixturePngCrc(crc, data) ^ 0xFFFFFFFF;
    BinaryPrimitives.WriteUInt32BigEndian(word, crc);
    destination.Write(word);
}

static uint UpdateFixturePngCrc(uint crc, ReadOnlySpan<byte> bytes)
{
    foreach (byte value in bytes)
    {
        crc ^= value;
        for (int bit = 0; bit < 8; bit++)
            crc = (crc & 1) != 0 ? 0xEDB88320 ^ (crc >> 1) : crc >> 1;
    }
    return crc;
}

static byte[] BuildIsoBmffFixture(string seed)
{
    using MemoryStream stream = new();
    byte[] ftypPayload =
    [
        .. Encoding.ASCII.GetBytes("isom"),
        0, 0, 0, 0,
        .. Encoding.ASCII.GetBytes("isom"),
        .. Encoding.ASCII.GetBytes("mp42")
    ];
    WriteIsoBmffBox(stream, "ftyp", ftypPayload);
    using MemoryStream moovPayload = new();
    WriteIsoBmffBox(moovPayload, "free", Encoding.UTF8.GetBytes("synthetic-smoke-moov:" + seed));
    WriteIsoBmffBox(stream, "moov", moovPayload.ToArray());
    WriteIsoBmffBox(stream, "mdat", Encoding.UTF8.GetBytes("synthetic-smoke-media:" + seed));
    return stream.ToArray();
}

static void WriteIsoBmffBox(Stream destination, string type, byte[] payload)
{
    Span<byte> header = stackalloc byte[8];
    BinaryPrimitives.WriteUInt32BigEndian(header[..4], checked((uint)(8 + payload.Length)));
    Encoding.ASCII.GetBytes(type, header[4..8]);
    destination.Write(header);
    destination.Write(payload);
}

static byte[] CanonicalArtifactUtf8<T>(T value)
{
    JsonSerializerOptions options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    JsonElement element = JsonSerializer.SerializeToElement(value, options);
    StringBuilder builder = new();
    WriteCanonicalArtifactElement(builder, element, options);
    return Encoding.UTF8.GetBytes(builder.ToString());
}

static byte[] VerifierCanonicalArtifactUtf8<T>(T value)
{
    JsonSerializerOptions options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
    JsonElement element = JsonSerializer.SerializeToElement(value, options);
    MethodInfo method = typeof(Id65V3RuntimeEvidenceContract).GetMethod(
        "SerializeVerifierCanonicalLf",
        BindingFlags.Static | BindingFlags.NonPublic) ??
        throw new InvalidOperationException("Frozen verifier serializer is missing.");
    return (byte[])(method.Invoke(null, [element]) ??
        throw new InvalidOperationException("Frozen verifier serializer returned null."));
}

static string[] CapturePinNames() =>
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

static Dictionary<string, string> BuildCapturePins(Id65V3RuntimeEvidencePackage package) =>
    new(StringComparer.Ordinal)
    {
        ["watcherRelativePath"] = package.SourceIdentity.WatcherRelativePath,
        ["watcherSha256"] = package.SourceIdentity.WatcherSha256,
        ["profileBuilderRelativePath"] = package.SourceIdentity.ProfileBuilderRelativePath,
        ["profileBuilderSha256"] = package.SourceIdentity.ProfileBuilderSha256,
        ["profileVerifierRelativePath"] = package.SourceIdentity.ProfileVerifierRelativePath,
        ["profileVerifierSha256"] = package.SourceIdentity.ProfileVerifierSha256,
        ["automationRelativePath"] = package.SourceIdentity.AutomationRelativePath,
        ["automationSha256"] = package.SourceIdentity.AutomationSha256,
        ["captureCoordinatorRelativePath"] = package.SourceIdentity.CaptureCoordinatorRelativePath,
        ["captureCoordinatorSha256"] = package.SourceIdentity.CaptureCoordinatorSha256,
        ["sourceClosureSha256"] = Id65V3RuntimeEvidenceContract.ExpectedSourceClosureSha256,
        ["runtimeProfileSha256"] = Id65V3RuntimeEvidenceContract.ExpectedRuntimeProfileSha256,
        ["profilePackageRelativePath"] = package.RuntimeProfile.PackageRelativePath,
        ["profileId"] = package.RuntimeProfile.ProfileId,
        ["handoffReceiptSha256"] = package.RuntimeProfile.HandoffReceiptSha256,
        ["appContentManifestSha256"] = package.RuntimeProfile.AppContentManifestSha256,
        ["appOutputTreeManifestSha256"] = package.RuntimeProfile.AppOutputTreeManifestSha256,
        ["homeOutputTreeManifestSha256"] = package.RuntimeProfile.HomeOutputTreeManifestSha256,
        ["packageEnvelopeSha256"] = package.RuntimeProfile.PackageEnvelopeSha256,
        ["externalHomeSettingsSha256"] = package.RuntimeProfile.ExternalHomeSettingsSha256,
        ["externalHomeBiosSha256"] = package.RuntimeProfile.ExternalHomeBiosSha256,
        ["hostNormalSettingsSentinelSha256"] = package.RuntimeProfile.HostNormalSettingsSha256
    };

static string CaptureEventHash(
    IReadOnlyDictionary<string, object?> header,
    IReadOnlyDictionary<string, object?> item,
    IReadOnlyDictionary<string, object?> payload)
{
    string payloadSha256 = Hash(CanonicalArtifactUtf8(payload));
    StringBuilder manifest = new(
        "id65-v3-runtime-capture-event-v2\n" +
        $"format={header["format"]}\n" +
        $"formatVersion={header["formatVersion"]}\n" +
        $"runId={header["runId"]}\n" +
        $"sessionId={header["sessionId"]}\n" +
        $"sessionOrdinal={header["sessionOrdinal"]}\n" +
        $"processInstanceId={header["processInstanceId"]}\n" +
        $"runOriginSha256={header["runOriginSha256"]}\n" +
        $"bootIdentity={header["bootIdentity"]}\n");
    foreach (string name in CapturePinNames())
        manifest.Append(name).Append('=').Append(header[name]).Append('\n');
    manifest.Append(
        $"sequence={item["sequence"]}\n" +
        $"eventType={item["eventType"]}\n" +
        $"atUtc={item["atUtc"]}\n" +
        $"monotonicMicroseconds={item["monotonicMicroseconds"]}\n" +
        $"previousEventSha256={item["previousEventSha256"]}\n" +
        $"payloadSha256={payloadSha256}\n");
    return Hash(Encoding.UTF8.GetBytes(manifest.ToString()));
}

static string CaptureEventHashNode(
    JsonObject header,
    JsonObject item,
    JsonObject payload)
{
    string Scalar(JsonObject value, string name)
    {
        JsonNode node = value[name] ??
            throw new InvalidOperationException($"Capture journal node omits {name}.");
        return node.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : node.ToJsonString();
    }

    string payloadSha256 = Hash(CanonicalArtifactUtf8(payload));
    StringBuilder manifest = new(
        "id65-v3-runtime-capture-event-v2\n" +
        $"format={Scalar(header, "format")}\n" +
        $"formatVersion={Scalar(header, "formatVersion")}\n" +
        $"runId={Scalar(header, "runId")}\n" +
        $"sessionId={Scalar(header, "sessionId")}\n" +
        $"sessionOrdinal={Scalar(header, "sessionOrdinal")}\n" +
        $"processInstanceId={Scalar(header, "processInstanceId")}\n" +
        $"runOriginSha256={Scalar(header, "runOriginSha256")}\n" +
        $"bootIdentity={Scalar(header, "bootIdentity")}\n");
    foreach (string name in CapturePinNames())
        manifest.Append(name).Append('=').Append(Scalar(header, name)).Append('\n');
    manifest.Append(
        $"sequence={Scalar(item, "sequence")}\n" +
        $"eventType={Scalar(item, "eventType")}\n" +
        $"atUtc={Scalar(item, "atUtc")}\n" +
        $"monotonicMicroseconds={Scalar(item, "monotonicMicroseconds")}\n" +
        $"previousEventSha256={Scalar(item, "previousEventSha256")}\n" +
        $"payloadSha256={payloadSha256}\n");
    return Hash(Encoding.UTF8.GetBytes(manifest.ToString()));
}

static string CaptureEventHashNodeLegacyV1(
    JsonObject header,
    JsonObject item,
    JsonObject payload)
{
    string Scalar(JsonObject value, string name)
    {
        JsonNode node = value[name] ??
            throw new InvalidOperationException($"Capture journal node omits {name}.");
        return node.GetValueKind() == JsonValueKind.String
            ? node.GetValue<string>()
            : node.ToJsonString();
    }

    string payloadSha256 = Hash(CanonicalArtifactUtf8(payload));
    string manifest =
        "id65-v3-runtime-capture-event-v1\n" +
        $"format={Scalar(header, "format")}\n" +
        $"formatVersion={Scalar(header, "formatVersion")}\n" +
        $"runId={Scalar(header, "runId")}\n" +
        $"sessionId={Scalar(header, "sessionId")}\n" +
        $"sessionOrdinal={Scalar(header, "sessionOrdinal")}\n" +
        $"processInstanceId={Scalar(header, "processInstanceId")}\n" +
        $"runOriginSha256={Scalar(header, "runOriginSha256")}\n" +
        $"bootIdentity={Scalar(header, "bootIdentity")}\n" +
        $"sourceClosureSha256={Scalar(header, "sourceClosureSha256")}\n" +
        $"runtimeProfileSha256={Scalar(header, "runtimeProfileSha256")}\n" +
        $"watcherSha256={Scalar(header, "watcherSha256")}\n" +
        $"captureCoordinatorRelativePath={Scalar(header, "captureCoordinatorRelativePath")}\n" +
        $"captureCoordinatorSha256={Scalar(header, "captureCoordinatorSha256")}\n" +
        $"sequence={Scalar(item, "sequence")}\n" +
        $"eventType={Scalar(item, "eventType")}\n" +
        $"atUtc={Scalar(item, "atUtc")}\n" +
        $"monotonicMicroseconds={Scalar(item, "monotonicMicroseconds")}\n" +
        $"previousEventSha256={Scalar(item, "previousEventSha256")}\n" +
        $"payloadSha256={payloadSha256}\n";
    return Hash(Encoding.UTF8.GetBytes(manifest));
}

static void WriteCanonicalArtifactElement(
    StringBuilder builder,
    JsonElement element,
    JsonSerializerOptions options)
{
    switch (element.ValueKind)
    {
        case JsonValueKind.Object:
            builder.Append('{');
            bool firstProperty = true;
            foreach (JsonProperty property in element.EnumerateObject()
                         .OrderBy(property => property.Name, SmokePythonUnicodeComparer.Instance))
            {
                if (!firstProperty)
                    builder.Append(',');
                firstProperty = false;
                WriteCanonicalArtifactString(builder, property.Name);
                builder.Append(':');
                WriteCanonicalArtifactElement(builder, property.Value, options);
            }
            builder.Append('}');
            return;
        case JsonValueKind.Array:
            builder.Append('[');
            bool firstItem = true;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (!firstItem)
                    builder.Append(',');
                firstItem = false;
                WriteCanonicalArtifactElement(builder, item, options);
            }
            builder.Append(']');
            return;
        case JsonValueKind.String:
            WriteCanonicalArtifactString(
                builder,
                element.GetString() ??
                    throw new InvalidOperationException("Canonical artifact JSON string is null."));
            return;
        case JsonValueKind.Number:
            builder.Append(element.GetRawText());
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
            throw new InvalidOperationException($"Unsupported artifact JSON kind {element.ValueKind}.");
    }
}

static void WriteCanonicalArtifactString(StringBuilder builder, string value)
{
    builder.Append('"');
    for (int index = 0; index < value.Length; index++)
    {
        char character = value[index];
        if (char.IsHighSurrogate(character))
        {
            if (index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1]))
                throw new InvalidOperationException(
                    "Canonical artifact JSON contains an unpaired high surrogate.");
            builder.Append(character).Append(value[++index]);
            continue;
        }
        if (char.IsLowSurrogate(character))
            throw new InvalidOperationException(
                "Canonical artifact JSON contains an unpaired low surrogate.");

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
                        .Append(((int)character).ToString(
                            "x4",
                            System.Globalization.CultureInfo.InvariantCulture));
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

void RejectContract(string name, string json)
{
    Id65V3RuntimeEvidenceValidation validation = Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        Encoding.UTF8.GetBytes(json),
        repositoryRoot);
    Require(!validation.StructurallyValid && validation.State == Id65V3RuntimeEvidenceState.Invalid &&
            validation.Errors.Length != 0,
        $"Canonical contract tamper '{name}' did not fail closed.");
    canonicalRejects++;
}

void RejectSealedPackage(string name, Id65V3RuntimeEvidencePackage package)
{
    Id65V3RuntimeEvidenceValidation validation = Id65V3RuntimeEvidenceContract.ValidateCanonicalJson(
        Id65V3RuntimeEvidenceContract.SerializeCanonicalUtf8(package),
        repositoryRoot);
    Require(!validation.StructurallyValid && validation.State == Id65V3RuntimeEvidenceState.Invalid &&
            validation.Errors.Length != 0,
        $"Sealed runtime package '{name}' did not fail closed as invalid.");
    stateGateRejects++;
}

void RejectTelemetry(string name, string[] lines) =>
    RejectTelemetryText(name, WatcherLog(lines, string.Empty));

void RejectTelemetryText(string name, string text)
{
    try
    {
        _ = Id65V3RuntimeEvidenceContract.ParseTelemetryLog(text);
        throw new InvalidOperationException($"Telemetry tamper '{name}' did not fail closed.");
    }
    catch (InvalidDataException)
    {
        telemetryRejects++;
    }
}

static Id65V3RuntimeEvidenceSession SyntheticSession(
    int? landingSampleOrdinal,
    int? holdStartSampleOrdinal,
    int? holdEndSampleOrdinal,
    int? fallStartSampleOrdinal,
    int? respawnSampleOrdinal) => new(
    SessionId: "synthetic-parser-fixture",
    ProcessInstanceId: "synthetic-process",
    TargetLevelId: 65,
    LoadCode: "synthetic",
    ColdBoot: true,
    NewGame: true,
    CardsNone: true,
    CheatsOff: true,
    SaveStateLoaded: false,
    SaveStateCreated: false,
    InGameSaveUsed: false,
    ProcessStartedAtUtc: null,
    WatcherConnectedAtUtc: null,
    ManuallyResumedAtUtc: null,
    SelectorStartedAtUtc: null,
    LandingAtUtc: null,
    HoldStartedAtUtc: null,
    HoldEndedAtUtc: null,
    FallStartedAtUtc: null,
    RespawnAtUtc: null,
    ProcessExitedAtUtc: null,
    LandingSampleOrdinal: landingSampleOrdinal,
    HoldStartSampleOrdinal: holdStartSampleOrdinal,
    HoldEndSampleOrdinal: holdEndSampleOrdinal,
    FallStartSampleOrdinal: fallStartSampleOrdinal,
    RespawnSampleOrdinal: respawnSampleOrdinal,
    WatcherArtifactId: null,
    CaptureJournal: null,
    ProfileBeforeArtifactId: null,
    ProfileAfterArtifactId: null,
    SampleSummary: null);

string WatcherLog(IEnumerable<string> samples, string promptSeparator) =>
    Warning + "\n" + ReadOnly + "\n" + Connected + "\n" + Prompt + promptSeparator + Watching + "\n" +
    string.Join('\n', samples) + "\n\n" + Stopped + "\n";

static string Sample(
    string atUtc,
    int ordinal,
    long elapsedMilliseconds,
    int lives,
    int deathState,
    int playerZ)
{
    string label = deathState switch
    {
        0 => "gameplay/non-death",
        4 => "death-respawn",
        5 => "game-over",
        _ => $"other-{deathState}"
    };
    string marker = playerZ < 1024 ? "YES" : "no";
    string livesRaw = Convert.ToHexString(BitConverter.GetBytes(lives)).ToLowerInvariant();
    string stateRaw = Convert.ToHexString(BitConverter.GetBytes(deathState)).ToLowerInvariant();
    string zRaw = Convert.ToHexString(BitConverter.GetBytes(playerZ)).ToLowerInvariant();
    return $"{atUtc} sample={ordinal} elapsed-ms={elapsedMilliseconds} lives={lives} " +
        $"death-state={deathState}({label}) player-Z={playerZ} [Z<0x400:{marker}] " +
        $"raw={livesRaw}/{stateRaw}/{zRaw}";
}

static string ReplaceOnce(string source, string oldValue, string newValue)
{
    int index = source.IndexOf(oldValue, StringComparison.Ordinal);
    if (index < 0)
        throw new InvalidOperationException($"Smoke fixture token '{oldValue}' was not found.");
    return source[..index] + newValue + source[(index + oldValue.Length)..];
}

static string SyntheticCheckAttestation(string checkId)
{
    string statement =
        $"Synthetic operator explicitly reviewed the bound {checkId} fixture evidence.";
    string? edgeToken = Id65V3RuntimeEvidenceContract.RequiredEdgeAssignmentToken(checkId);
    return edgeToken is null ? statement : $"{statement} Physical assignment {edgeToken}.";
}

static string MutateNibble(string hash) => (hash[0] == '0' ? "1" : "0") + hash[1..];

static string Hash(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

static string HashFile(string path)
{
    using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string FindRepositoryRoot(string? requested)
{
    if (!string.IsNullOrWhiteSpace(requested))
        return Path.GetFullPath(requested);
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "Directory.Build.props")) &&
            File.Exists(Path.Combine(directory.FullName, "src", "Spyro.Editor.Core", "Spyro.Editor.Core.csproj")) &&
            Directory.Exists(Path.Combine(directory.FullName, "src")))
        {
            return directory.FullName;
        }
        directory = directory.Parent;
    }
    throw new DirectoryNotFoundException("Could not locate the Spyro Editor repository root.");
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException(message);
}

internal sealed record CompleteFixture(
    Id65V3RuntimeEvidencePackage Package,
    Id65V3RuntimeEvidenceReadbackContext ReadbackContext,
    string ArtifactRepositoryRoot,
    IReadOnlyDictionary<string, string> ArtifactPaths,
    IReadOnlyDictionary<string, Id65V3RuntimeProfileSnapshot> ProfileSnapshots,
    IReadOnlyDictionary<string, byte[]> JournalBytes);

internal sealed class SmokePythonUnicodeComparer : IComparer<string>
{
    internal static readonly SmokePythonUnicodeComparer Instance = new();

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
            int leftScalar = ReadScalar(left, ref leftIndex);
            int rightScalar = ReadScalar(right, ref rightIndex);
            int comparison = leftScalar.CompareTo(rightScalar);
            if (comparison != 0)
                return comparison;
        }
        return (left.Length - leftIndex).CompareTo(right.Length - rightIndex);
    }

    private static int ReadScalar(string value, ref int index)
    {
        char first = value[index++];
        if (!char.IsSurrogate(first))
            return first;
        if (!char.IsHighSurrogate(first) || index >= value.Length ||
            !char.IsLowSurrogate(value[index]))
        {
            throw new InvalidDataException("Smoke path contains an unpaired UTF-16 surrogate.");
        }
        return char.ConvertToUtf32(first, value[index++]);
    }
}

internal static class SmokeDarwin
{
    internal static void CreateHardLink(string newPath, string existingPath)
    {
        if (Link(existingPath, newPath) != 0)
            throw new IOException($"Could not create smoke hardlink (errno {Marshal.GetLastPInvokeError()}).");
    }

    [DllImport("libc", EntryPoint = "link", SetLastError = true)]
    private static extern int Link(
        [MarshalAs(UnmanagedType.LPUTF8Str)] string existingPath,
        [MarshalAs(UnmanagedType.LPUTF8Str)] string newPath);
}
