#!/usr/bin/env python3
"""Offline adversarial smoke for the aggregate V1 manual capture adapter."""

from __future__ import annotations

import contextlib
import hashlib
import importlib.util
import os
import pathlib
import subprocess
import sys
import tempfile
from typing import Any


TOOL_PATH = pathlib.Path(__file__).with_name(
    "Capture-SpyroId65AuthoringAggregateV1RuntimeSession.py"
)
SPEC = importlib.util.spec_from_file_location(
    "id65_authoring_aggregate_v1_capture", TOOL_PATH
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Cannot load the aggregate V1 capture adapter.")
capture = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = capture
SPEC.loader.exec_module(capture)

PRODUCTION_DUCKSTATION = os.path.realpath(
    capture.PRODUCTION_REPOSITORY_ROOT
    / capture.DUCKSTATION_EXECUTABLE_RELATIVE_PATH
)
FORBIDDEN_AUTOMATION = os.path.realpath(
    capture.PRODUCTION_REPOSITORY_ROOT
    / capture.FORBIDDEN_AUTOMATION_RELATIVE_PATH
)
PROCESS_VIOLATIONS: list[tuple[str, tuple[Any, ...]]] = []


def _audit_no_production_process(event: str, args: tuple[Any, ...]) -> None:
    if event not in {"subprocess.Popen", "os.exec", "os.posix_spawn"} or not args:
        return
    executable = args[0]
    if not isinstance(executable, (str, bytes, os.PathLike)):
        return
    try:
        observed = os.path.realpath(os.fsdecode(executable))
    except (TypeError, ValueError):
        return
    if observed in {PRODUCTION_DUCKSTATION, FORBIDDEN_AUTOMATION}:
        PROCESS_VIOLATIONS.append((event, args))


sys.addaudithook(_audit_no_production_process)


def _expect_refusal(label: str, action: Any, refusal: type[BaseException]) -> None:
    try:
        action()
    except refusal:
        return
    raise AssertionError(f"Expected refusal was not raised: {label}")


def _snapshot(core: Any) -> dict[str, Any]:
    files = [
        {
            "byteLength": capture.EXPECTED_BIOS_SIZE,
            "relativePath": core.BIOS_MANIFEST_PATH,
            "sha256": capture.EXPECTED_BIOS_SHA256,
        },
        {
            "byteLength": 4,
            "relativePath": "playtime.dat",
            "sha256": "1" * 64,
        },
        {
            "byteLength": capture.EXPECTED_SETTINGS_SIZE,
            "relativePath": "settings.ini",
            "sha256": capture.EXPECTED_SETTINGS_SHA256,
        },
    ]
    files.sort(key=lambda item: item["relativePath"])
    return {
        "capturedAtUtc": "1970-01-01T00:00:00.000Z",
        "dataFiles": files,
        "duckStationExecutableSha256": capture.EXPECTED_DUCKSTATION_SHA256,
        "externalHomeBiosSha256": capture.EXPECTED_BIOS_SHA256,
        "externalHomeSettingsSha256": capture.EXPECTED_SETTINGS_SHA256,
        "format": capture.PROFILE_FORMAT,
        "formatVersion": capture.PROFILE_FORMAT_VERSION,
        "handoffReceiptSha256": capture.EXPECTED_HANDOFF_RECEIPT_SHA256,
        "hostNormalSettingsSentinelId": core.NORMAL_SETTINGS_RESOURCE_ID,
        "hostNormalSettingsSentinelSha256": capture.EXPECTED_NORMAL_SETTINGS_SHA256,
        "appContentManifestSha256": capture.EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": capture.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "packageEnvelopeSha256": capture.EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "phase": "preflight",
        "processInstanceId": "not-applicable",
        "profileId": capture.PROFILE_ID,
        "sessionId": "preflight",
        "snapshotId": "preflight.profile-preflight",
    }


def _verifier_preflight(core: Any) -> dict[str, Any]:
    return {
        "aggregatePlanSha256": capture.EXPECTED_AGGREGATE_PLAN_SHA256,
        "appContentManifestSha256": capture.EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        "appOutputTreeManifestSha256": capture.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256,
        "candidateBinSha256": capture.EXPECTED_CANDIDATE_BIN_SHA256,
        "candidateCueSha256": capture.EXPECTED_CANDIDATE_CUE_SHA256,
        "candidateManifestSha256": capture.EXPECTED_CANDIDATE_MANIFEST_SHA256,
        "candidateReceiptContentSha256": (
            capture.EXPECTED_CANDIDATE_RECEIPT_CONTENT_SHA256
        ),
        "candidateReceiptFileSha256": capture.EXPECTED_CANDIDATE_RECEIPT_FILE_SHA256,
        "createBinAuthorized": False,
        "documentCanonicalSha256": capture.EXPECTED_DOCUMENT_CANONICAL_SHA256,
        "documentEnvelopeSha256": capture.EXPECTED_DOCUMENT_ENVELOPE_SHA256,
        "duckStationLaunched": False,
        "evidenceAccepted": False,
        "format": capture.PREFLIGHT_FORMAT,
        "formatVersion": capture.PREFLIGHT_FORMAT_VERSION,
        "handoffReceiptClosureSha256": capture.EXPECTED_RECEIPT_CLOSURE_SHA256,
        "handoffReceiptSha256": capture.EXPECTED_HANDOFF_RECEIPT_SHA256,
        "homeOutputTreeManifestSha256": (
            capture.EXPECTED_HOME_OUTPUT_TREE_MANIFEST_SHA256
        ),
        "launchGate": capture.LAUNCH_GATE,
        "outputRow80Sha256": capture.EXPECTED_OUTPUT_ROW80_SHA256,
        "packageEnvelopeSha256": capture.EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "profileDestination": os.fspath(
            capture.PRODUCTION_REPOSITORY_ROOT
            / capture.PROFILE_PACKAGE_RELATIVE_PATH
        ),
        "profileId": capture.PROFILE_ID,
        "publishable": False,
        "releaseAuthorized": False,
        "runtimeProven": False,
        "snapshot": _snapshot(core),
        "status": "PREFLIGHT_READY",
    }


class FakeStaging:
    def __init__(self) -> None:
        self.inspect_calls = 0

    def inspect_prefix(self) -> None:
        self.inspect_calls += 1


class FakeBoundary:
    def __init__(self, repository_root: pathlib.Path) -> None:
        self.repository_root = repository_root
        self.environment_calls = 0
        self.processes: list[Any] = []
        self.listeners: set[int] = set()

    def require_capture_environment(self) -> None:
        self.environment_calls += 1

    def duckstation_processes(self) -> list[Any]:
        return list(self.processes)

    def listener_pids(self) -> set[int]:
        return set(self.listeners)


class FakeSource:
    def __init__(self, core: Any, pins: dict[str, str]) -> None:
        self.core = core
        self.pins = pins
        self.validate_calls = 0
        self.snapshot_calls = 0
        self.refuse = False

    def validate_all(self) -> dict[str, str]:
        self.validate_calls += 1
        if self.refuse:
            raise self.core.CaptureRefusal("injected aggregate source drift")
        return dict(self.pins)

    def capture_profile(self, **kwargs: Any) -> dict[str, Any]:
        self.snapshot_calls += 1
        if self.refuse:
            raise self.core.CaptureRefusal("injected aggregate snapshot drift")
        return _snapshot(self.core)


def _test_source_reader() -> int:
    checks = 0
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(
        prefix="id65-aggregate-capture-source-", dir=temporary_parent
    ) as temporary:
        root = pathlib.Path(temporary)
        source = root / "source.py"
        source.write_bytes(b"print('offline')\n")
        source.chmod(0o644)
        expected = hashlib.sha256(source.read_bytes()).hexdigest()
        assert capture._stable_read_source(
            source,
            expected_sha256=expected,
            expected_mode=0o644,
            label="temporary source",
        ) == source.read_bytes()
        checks += 1

        _expect_refusal(
            "source hash",
            lambda: capture._stable_read_source(
                source,
                expected_sha256="0" * 64,
                expected_mode=0o644,
                label="temporary source",
            ),
            capture.AggregateCaptureRefusal,
        )
        checks += 1
        source.chmod(0o664)
        _expect_refusal(
            "source mode",
            lambda: capture._stable_read_source(
                source,
                expected_sha256=expected,
                expected_mode=0o644,
                label="temporary source",
            ),
            capture.AggregateCaptureRefusal,
        )
        checks += 1
        source.chmod(0o644)
        hardlink = root / "hardlink.py"
        os.link(source, hardlink)
        _expect_refusal(
            "source hard link",
            lambda: capture._stable_read_source(
                source,
                expected_sha256=expected,
                expected_mode=0o644,
                label="temporary source",
            ),
            capture.AggregateCaptureRefusal,
        )
        checks += 1
        hardlink.unlink()
        symlink = root / "symlink.py"
        symlink.symlink_to(source.name)
        _expect_refusal(
            "source symlink",
            lambda: capture._stable_read_source(
                symlink,
                expected_sha256=expected,
                expected_mode=0o644,
                label="temporary source",
            ),
            capture.AggregateCaptureRefusal,
        )
        checks += 1
    return checks


def _test_authority_first_lock_and_staging(core: Any) -> int:
    checks = 0
    temporary_parent = pathlib.Path(tempfile.gettempdir()).resolve(strict=True)
    with tempfile.TemporaryDirectory(
        prefix="id65-aggregate-authority-first-", dir=temporary_parent
    ) as temporary:
        root = pathlib.Path(temporary)
        authority = root / capture.GLOBAL_AUTHORITY_DIRECTORY
        authority.mkdir(parents=True)
        authority.chmod(0o700)
        events: list[tuple[str, int | str]] = []
        original_flock = core.fcntl.flock
        original_open = core.os.open
        original_mkdir = core.os.mkdir
        aggregate_leaves = {
            capture.CAPTURE_STAGING_PREFIX.parts[-2],
            capture.CAPTURE_STAGING_PREFIX.parts[-1],
            capture.AGGREGATE_STAGING_LOCK_NAME,
        }

        def is_aggregate_staging_target(path: Any) -> bool:
            try:
                value = os.fsdecode(path)
            except (TypeError, ValueError):
                return False
            if value in aggregate_leaves:
                return True
            if not os.path.isabs(value):
                return False
            normalized = pathlib.Path(os.path.normpath(value))
            prefix = root / capture.CAPTURE_STAGING_PREFIX
            return normalized == prefix or prefix in normalized.parents

        def observed_flock(fd: int, operation: int) -> Any:
            if operation & core.fcntl.LOCK_EX:
                events.append(("exclusive-flock", fd))
            return original_flock(fd, operation)

        def observed_open(path: Any, *args: Any, **kwargs: Any) -> int:
            if is_aggregate_staging_target(path):
                events.append(("aggregate-staging-open", os.fsdecode(path)))
            return original_open(path, *args, **kwargs)

        def observed_mkdir(path: Any, *args: Any, **kwargs: Any) -> None:
            if is_aggregate_staging_target(path):
                events.append(("aggregate-staging-mkdir", os.fsdecode(path)))
            original_mkdir(path, *args, **kwargs)

        core.fcntl.flock = observed_flock
        core.os.open = observed_open
        core.os.mkdir = observed_mkdir
        lease: Any | None = None
        try:
            lease = core.CoordinatorLease(root)
            lease.__enter__()
            assert lease.__class__.__name__ == "AuthorityFirstAggregateCoordinatorLease"
            checks += 1
            exclusive = [
                (index, value)
                for index, (kind, value) in enumerate(events)
                if kind == "exclusive-flock"
            ]
            staging_indexes = [
                index
                for index, (kind, _) in enumerate(events)
                if kind.startswith("aggregate-staging-")
            ]
            assert len(exclusive) == 2
            assert exclusive[0][1] == lease.authority_fd
            assert exclusive[1][1] == lease.fd
            checks += 1
            assert staging_indexes
            assert exclusive[0][0] < min(staging_indexes) < exclusive[1][0]
            checks += 1
            before_contention = len(staging_indexes)
            _expect_refusal(
                "competing authority-first lease",
                lambda: core.CoordinatorLease(root).__enter__(),
                core.CaptureRefusal,
            )
            after_contention = sum(
                1
                for kind, _ in events
                if kind.startswith("aggregate-staging-")
            )
            assert after_contention == before_contention
            checks += 1
            lease.__exit__(None, None, None)
            lease = None

            events.clear()
            boundary = FakeBoundary(root)
            source = FakeSource(core, {})
            coordinator = core.CaptureCoordinator(
                boundary,
                source_guard=source,
                staging=core.AnchoredStaging(root),
            )
            assert coordinator.__class__.__name__ == (
                "AuthorityFirstAggregateCaptureCoordinator"
            )
            coordinator.preflight()
            coordinator_exclusive = [
                index
                for index, (kind, _) in enumerate(events)
                if kind == "exclusive-flock"
            ]
            coordinator_staging = [
                index
                for index, (kind, _) in enumerate(events)
                if kind.startswith("aggregate-staging-")
            ]
            assert len(coordinator_exclusive) == 2 and coordinator_staging
            assert coordinator_exclusive[0] < min(coordinator_staging)
            checks += 1

            events.clear()
            source.refuse = True
            _expect_refusal(
                "capture source refusal under outer authority",
                lambda: coordinator.capture_session(
                    "candidate-primary-edge-0"
                ),
                core.CaptureRefusal,
            )
            refused_exclusive = [
                index
                for index, (kind, _) in enumerate(events)
                if kind == "exclusive-flock"
            ]
            refused_staging = [
                index
                for index, (kind, _) in enumerate(events)
                if kind.startswith("aggregate-staging-")
            ]
            assert len(refused_exclusive) == 2 and refused_staging
            assert refused_exclusive[0] < min(refused_staging)
            checks += 1
        finally:
            if lease is not None:
                lease.__exit__(None, None, None)
            core.fcntl.flock = original_flock
            core.os.open = original_open
            core.os.mkdir = original_mkdir
        prefix = root / capture.CAPTURE_STAGING_PREFIX
        assert prefix.is_dir() and (prefix.stat().st_mode & 0o777) == 0o700
        checks += 1
    return checks


def _test_proof_adapter(core: Any) -> int:
    checks = 0
    raw = _verifier_preflight(core)
    compact = capture._compact_verifier_prelaunch(core, raw)
    assert compact["format"] == capture.PREFLIGHT_FORMAT
    assert compact["snapshot"]["profileId"] == capture.PROFILE_ID
    checks += 1

    for label, mutate in (
        ("verifier extra key", lambda value: value.__setitem__("extra", True)),
        (
            "verifier profile",
            lambda value: value.__setitem__("profileId", "wrong-profile"),
        ),
        (
            "verifier launch flag",
            lambda value: value.__setitem__("duckStationLaunched", True),
        ),
        (
            "verifier receipt",
            lambda value: value.__setitem__("handoffReceiptSha256", "0" * 64),
        ),
        (
            "verifier snapshot",
            lambda value: value["snapshot"].__setitem__("profileId", "wrong"),
        ),
    ):
        changed = _verifier_preflight(core)
        mutate(changed)
        _expect_refusal(
            label,
            lambda changed=changed: capture._compact_verifier_prelaunch(core, changed),
            core.CaptureRefusal,
        )
        checks += 1

    changed_compact = dict(compact)
    changed_compact["extra"] = "forbidden"
    _expect_refusal(
        "compact proof schema",
        lambda: capture._validate_compact_prelaunch_proof(core, changed_compact),
        core.CaptureRefusal,
    )
    checks += 1
    return checks


def _test_fake_preflight(core: Any, pins: dict[str, str]) -> int:
    checks = 0
    boundary = FakeBoundary(capture.PRODUCTION_REPOSITORY_ROOT)
    source = FakeSource(core, pins)
    staging = FakeStaging()
    coordinator = core.CaptureCoordinator(
        boundary,
        source_guard=source,
        staging=staging,
        lease_factory=lambda: contextlib.nullcontext(),
    )
    coordinator.preflight()
    assert (
        staging.inspect_calls == 1
        and boundary.environment_calls == 1
        and source.validate_calls == 1
        and source.snapshot_calls == 1
    )
    checks += 1

    boundary.processes = [object()]
    _expect_refusal("existing DuckStation", coordinator.preflight, core.CaptureRefusal)
    assert source.validate_calls == 1
    checks += 1
    boundary.processes = []
    boundary.listeners = {2345}
    _expect_refusal("existing listener", coordinator.preflight, core.CaptureRefusal)
    checks += 1
    boundary.listeners = set()
    source.refuse = True
    _expect_refusal("source drift", coordinator.preflight, core.CaptureRefusal)
    checks += 1
    return checks


def _test_aggregate_checklist(core: Any) -> int:
    expected = (
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
        "retail.town-square-preserved",
    )
    assert capture.EXPECTED_AGGREGATE_CHECK_IDS == expected
    assert tuple(capture.AGGREGATE_CHECK_BINDINGS) == expected
    assert capture.PRODUCTION_CAPTURE_ENABLED is False
    assert (
        capture.PRODUCTION_CAPTURE_BLOCKING_CHECK_ID
        == "candidate.save-persistence"
    )
    assert tuple(item[0] for item in capture.PRODUCTION_CAPTURE_BLOCKERS) == (
        "profile.no-memory-card-save-persistence",
        "workflow.frozen-v3-seven-session-lone-pad-incompatible",
    )
    assert capture.PRODUCTION_CAPTURE_BLOCKERS[1][1] == (
        "aggregate.exact-17-check-town-square-workflow"
    )
    assert capture.FROZEN_WORKFLOW_COMPATIBLE_WITH_AGGREGATE_SCOPE is False
    assert tuple(core.SESSION_IDS) == capture.FROZEN_WORKFLOW_SESSION_IDS
    assert core.RUN_ID_RE.pattern == capture.FROZEN_WORKFLOW_RUN_ID_PATTERN
    capture._require_aggregate_checklist(core)
    checks = 1

    for session_id, additions in (
        capture.AGGREGATE_ADDITIONAL_MARKER_PLANS.items()
    ):
        expected_sequence = capture._aggregate_expected_marker_sequence(
            core, session_id
        )
        for marker_id, _ in additions:
            assert ("check-started", marker_id) in expected_sequence
            assert ("check-ended", marker_id) in expected_sequence
        checks += 1

    original = core.CHECK_MARKER_PLANS
    changed = dict(original)
    changed["candidate-edge-1"] = tuple(
        item
        for item in changed["candidate-edge-1"]
        if item[0] != "candidate.authored-terrain-visible"
    )
    core.CHECK_MARKER_PLANS = changed
    try:
        _expect_refusal(
            "missing aggregate check marker",
            lambda: capture._require_aggregate_checklist(core),
            capture.AggregateCaptureRefusal,
        )
    finally:
        core.CHECK_MARKER_PLANS = original
    checks += 1

    _expect_refusal(
        "save-incompatible production profile",
        capture._require_production_capture_enabled,
        capture.AggregateCaptureRefusal,
    )
    checks += 1
    return checks


def _test_no_persistent_capture_access(core: Any) -> int:
    checks = 0
    protected = (
        capture.PRODUCTION_REPOSITORY_ROOT / capture.PROFILE_PACKAGE_RELATIVE_PATH,
        capture.PRODUCTION_REPOSITORY_ROOT / capture.CANDIDATE_DIRECTORY_RELATIVE_PATH,
        capture.PRODUCTION_REPOSITORY_ROOT / capture.CAPTURE_STAGING_PREFIX,
    )
    original_open = os.open
    original_mkdir = os.mkdir
    forbidden_opens: list[pathlib.Path] = []
    forbidden_mkdirs: list[pathlib.Path] = []

    def guarded_open(path: Any, *args: Any, **kwargs: Any) -> int:
        if isinstance(path, (str, bytes, os.PathLike)):
            candidate = pathlib.Path(os.path.abspath(os.fsdecode(path)))
            if any(candidate == root or root in candidate.parents for root in protected):
                forbidden_opens.append(candidate)
                raise AssertionError(f"offline test touched persistent boundary: {candidate}")
        return original_open(path, *args, **kwargs)

    def guarded_mkdir(path: Any, *args: Any, **kwargs: Any) -> None:
        if isinstance(path, (str, bytes, os.PathLike)):
            candidate = pathlib.Path(os.path.abspath(os.fsdecode(path)))
            if any(candidate == root or root in candidate.parents for root in protected):
                forbidden_mkdirs.append(candidate)
                raise AssertionError(
                    f"offline test mutated persistent boundary: {candidate}"
                )
        original_mkdir(path, *args, **kwargs)

    os.open = guarded_open
    os.mkdir = guarded_mkdir
    try:
        status = capture._static_status(capture.PRODUCTION_REPOSITORY_ROOT)
        assert status["manualCaptureOnly"] is True
        assert status["automationAllowed"] is False
        capture.run_offline_self_test(capture.PRODUCTION_REPOSITORY_ROOT)
    finally:
        os.open = original_open
        os.mkdir = original_mkdir
    assert not forbidden_opens and not forbidden_mkdirs
    checks += 1
    return checks


def main() -> int:
    root = capture.PRODUCTION_REPOSITORY_ROOT
    core = capture._load_configured_core(root)
    guard = capture.AggregateSourceGuard(root, core)
    pins = guard.validate_all()
    checks = 0

    assert set(pins) == set(capture.CAPTURE_PIN_KEYS)
    assert "automationRelativePath" not in pins
    assert "automationSha256" not in pins
    assert all("Automate-SpyroId65V3" not in value for value in pins.values())
    assert core.CAPTURE_STAGING_PREFIX == capture.CAPTURE_STAGING_PREFIX.as_posix()
    assert (
        core.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        == capture.GLOBAL_AUTHORITY_DIRECTORY.as_posix()
    )
    checks += 1

    checks += _test_source_reader()
    checks += _test_authority_first_lock_and_staging(core)
    checks += _test_proof_adapter(core)
    checks += _test_fake_preflight(core, pins)
    checks += _test_aggregate_checklist(core)
    checks += _test_no_persistent_capture_access(core)

    capture._exercise_temporary_lock_order(core)
    checks += 1

    environment = dict(os.environ)
    environment["PYTHONDONTWRITEBYTECODE"] = "1"
    completed = subprocess.run(
        [sys.executable, os.fspath(TOOL_PATH), "self-test"],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=root,
        env=environment,
        check=False,
        timeout=30,
    )
    if (
        completed.returncode != 0
        or completed.stdout.strip()
        != b"ID65_AUTHORING_AGGREGATE_CAPTURE_SELF_TEST_OK"
        or completed.stderr
    ):
        raise AssertionError(
            "aggregate capture CLI self-test failed: "
            f"code={completed.returncode} stdout={completed.stdout!r} "
            f"stderr={completed.stderr!r}"
        )
    checks += 1

    status = subprocess.run(
        [sys.executable, os.fspath(TOOL_PATH), "status"],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        cwd=root,
        env=environment,
        check=False,
        timeout=30,
    )
    if status.returncode != 0 or status.stderr:
        raise AssertionError(f"aggregate capture CLI status failed: {status.stderr!r}")
    decoded = capture.json.loads(status.stdout)
    if (
        decoded.get("manualCaptureOnly") is not True
        or decoded.get("automationAllowed") is not False
        or decoded.get("productionCaptureEnabled") is not False
        or decoded.get("productionCaptureBlockingCheckId")
        != "candidate.save-persistence"
        or decoded.get("productionCaptureBlockerCount") != 2
        or decoded.get("productionCaptureBlockers")
        != capture._production_capture_blocker_payload()
        or decoded.get("productionCaptureBlockersSha256")
        != capture.EXPECTED_PRODUCTION_CAPTURE_BLOCKERS_SHA256
        or decoded.get("productionCaptureBlockers", [{}, {}])[1].get(
            "blockerId"
        )
        != "workflow.frozen-v3-seven-session-lone-pad-incompatible"
        or decoded.get("productionCaptureBlockers", [{}, {}])[1].get(
            "requiredGate"
        )
        != "aggregate.exact-17-check-town-square-workflow"
        or decoded.get("frozenWorkflowCompatibleWithAggregateScope") is not False
        or decoded.get("frozenWorkflowSha256")
        != capture.EXPECTED_FROZEN_WORKFLOW_SHA256
        or tuple(decoded.get("frozenWorkflow", {}).get("sessionIds", ()))
        != capture.FROZEN_WORKFLOW_SESSION_IDS
        or decoded.get("eventualCheckCount") != 17
        or tuple(decoded.get("eventualCheckIds", ()))
        != capture.EXPECTED_AGGREGATE_CHECK_IDS
        or decoded.get("duckStationLaunched") is not False
        or decoded.get("liveProfilePreflightRun") is not False
        or decoded.get("evidenceAccepted") is not False
    ):
        raise AssertionError("aggregate capture CLI status overstated acceptance.")
    checks += 1

    blocked_operations = (
        ("preflight",),
        ("capture-session", "candidate-primary-edge-0"),
        (
            "verify-staging",
            "id65v3-20260825t000000z-0000000000000000",
        ),
    )
    for operation in blocked_operations:
        blocked = subprocess.run(
            [sys.executable, os.fspath(TOOL_PATH), *operation],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            cwd=root,
            env=environment,
            check=False,
            timeout=30,
        )
        if (
            blocked.returncode != 2
            or blocked.stdout
            or b"profile.no-memory-card-save-persistence" not in blocked.stderr
            or b"candidate.save-persistence" not in blocked.stderr
            or b"memory-card" not in blocked.stderr
            or (
                b"workflow.frozen-v3-seven-session-lone-pad-incompatible"
                not in blocked.stderr
            )
            or b"aggregate.exact-17-check-town-square-workflow" not in blocked.stderr
            or b"17-check aggregate/Town Square" not in blocked.stderr
        ):
            raise AssertionError(
                "production operation did not fail closed at both blockers: "
                f"operation={operation!r} code={blocked.returncode} "
                f"stdout={blocked.stdout!r} stderr={blocked.stderr!r}"
            )
        checks += 1

    if PROCESS_VIOLATIONS:
        raise AssertionError(
            f"offline smoke executed a forbidden production process: {PROCESS_VIOLATIONS!r}"
        )

    print(
        "ID65_AUTHORING_AGGREGATE_CAPTURE_DEEP_SMOKE_OK "
        f"checks={checks} sessions={len(core.SESSION_IDS)} "
        f"runtimeProfile={capture.EXPECTED_RUNTIME_PROFILE_SHA256}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
