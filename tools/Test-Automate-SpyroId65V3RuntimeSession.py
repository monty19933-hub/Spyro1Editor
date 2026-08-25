#!/usr/bin/env python3
"""Offline and non-posting native tests for the ID65 V3 input foundation."""

from __future__ import annotations

import dataclasses
import hashlib
import importlib.util
import json
import os
import pathlib
import select
import shutil
import stat
import subprocess
import sys
import tempfile
import types
from collections.abc import Sequence
from typing import Any


TOOL_PATH = pathlib.Path(__file__).with_name(
    "Automate-SpyroId65V3RuntimeSession.py"
)
SPEC = importlib.util.spec_from_file_location("id65_v3_unattended", TOOL_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError("Cannot import unattended foundation.")
automation = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = automation
SPEC.loader.exec_module(automation)

ACTUAL_DUCKSTATION_CALLS = 0
ACTUAL_GDB_CONNECTION_ATTEMPTS = 0
PRODUCTION_EVENT_POSTS = 0
NATIVE_EVENT_POST_ATTEMPTS = 0


def _command_launches_production_duckstation(
    command: object, repository_root: pathlib.Path
) -> bool:
    executable = os.fspath(
        repository_root / automation.DUCKSTATION_EXECUTABLE_RELATIVE_PATH
    )
    app = os.fspath(repository_root / automation.PACKAGED_APP_RELATIVE_PATH)
    if isinstance(command, (str, os.PathLike)):
        text = os.fspath(command)
        return text == executable or (
            executable in text and any(token in text for token in ("sh ", "bash ", "zsh "))
        )
    if not isinstance(command, Sequence) or not command:
        return False
    tokens = [os.fspath(value) for value in command if isinstance(value, (str, os.PathLike))]
    if not tokens:
        return False
    if tokens[0] == executable:
        return True
    return pathlib.Path(tokens[0]).name == "open" and app in tokens[1:]


def expect_refusal(callback: Any, fragment: str) -> None:
    try:
        callback()
    except automation.AutomationRefusal as exc:
        if fragment.lower() not in str(exc).lower():
            raise AssertionError(
                f"Expected refusal containing {fragment!r}, observed {str(exc)!r}."
            ) from exc
    else:
        raise AssertionError(f"Expected refusal containing {fragment!r}.")


def plan_bytes(
    events: Sequence[tuple[str, str]],
    *,
    nonce: str = "00112233445566778899aabbccddeeff",
) -> bytes:
    return automation.canonical_json({
        "events": [
            {
                "action": action,
                "expectedPadState": state,
                "nonce": f"{nonce}-{index:08x}",
                "sequence": index,
            }
            for index, (action, state) in enumerate(events)
        ],
        "format": automation.PLAN_FORMAT,
        "formatVersion": automation.PLAN_FORMAT_VERSION,
        "planNonce": nonce,
        "profileId": automation.PROFILE_ID,
    })


def parse_plan(
    *events: tuple[str, str],
    nonce: str = "00112233445566778899aabbccddeeff",
) -> Any:
    return automation.InputPlan.parse(plan_bytes(events, nonce=nonce))


def make_file(
    path: str = "/pinned/DuckStation",
    *,
    digest: str = automation.EXPECTED_DUCKSTATION_SHA256,
) -> Any:
    return automation.FileIdentity(
        real_path=path,
        device=5,
        inode=7,
        byte_length=123456,
        owner_uid=501,
        mode=0o755,
        link_count=1,
        sha256=digest,
    )


def make_profile(
    *,
    enabled_actions: frozenset[Any] | None = None,
    executable: Any | None = None,
) -> Any:
    resource = make_file("/pinned/settings.ini", digest="1" * 64)
    cue = make_file("/pinned/candidate.cue", digest="2" * 64)
    candidate_bin = make_file("/pinned/candidate.bin", digest="3" * 64)
    return automation.VerifiedProfile(
        profile_id=automation.PROFILE_ID,
        effective_uid=501,
        executable=executable or make_file(),
        settings=resource,
        candidate_cue=cue,
        candidate_bin=candidate_bin,
        handoff_receipt_sha256=automation.EXPECTED_HANDOFF_RECEIPT_SHA256,
        app_content_manifest_sha256=automation.EXPECTED_APP_CONTENT_MANIFEST_SHA256,
        app_output_tree_manifest_sha256=(
            automation.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256
        ),
        package_envelope_sha256=automation.EXPECTED_PACKAGE_ENVELOPE_SHA256,
        external_home_bios_sha256=automation.EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
        host_normal_settings_sentinel_sha256=(
            automation.EXPECTED_HOST_SETTINGS_SENTINEL_SHA256
        ),
        enabled_actions=(enabled_actions if enabled_actions is not None else frozenset({
            automation.KeyAction.PAD_R1_TOGGLE,
            automation.KeyAction.PAD_R2_TOGGLE,
            automation.KeyAction.PAD_L1_TOGGLE,
            automation.KeyAction.PAD_L2_TOGGLE,
            automation.KeyAction.PAD_LEFT_TOGGLE,
            automation.KeyAction.PAD_DOWN_TOGGLE,
            automation.KeyAction.TOGGLE_PAUSE,
            automation.KeyAction.POWER_OFF,
        })),
    )


def make_profile_snapshot(**changes: object) -> dict[str, object]:
    captured_at_utc = "2026-08-24T12:34:56.789Z"
    data_files = [
        {
            "relativePath": automation.BIOS_DATA_RELATIVE_PATH,
            "byteLength": automation.EXPECTED_BIOS_SIZE,
            "sha256": automation.EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
        },
        {
            "relativePath": "settings.ini",
            "byteLength": automation.EXPECTED_SETTINGS_SIZE,
            "sha256": automation.EXPECTED_SETTINGS_SHA256,
        },
    ]
    payload: dict[str, object] = {
        "format": automation.PROFILE_SNAPSHOT_FORMAT,
        "formatVersion": automation.PROFILE_SNAPSHOT_VERSION,
        "snapshotId": (
            f"{automation.PROFILE_SNAPSHOT_SESSION_ID}.profile-before"
        ),
        "phase": "before",
        "sessionId": automation.PROFILE_SNAPSHOT_SESSION_ID,
        "processInstanceId": automation.PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID,
        "profileId": automation.PROFILE_ID,
        "capturedAtUtc": captured_at_utc,
        "duckStationExecutableSha256": automation.EXPECTED_DUCKSTATION_SHA256,
        "handoffReceiptSha256": automation.EXPECTED_HANDOFF_RECEIPT_SHA256,
        "appContentManifestSha256": (
            automation.EXPECTED_APP_CONTENT_MANIFEST_SHA256
        ),
        "appOutputTreeManifestSha256": (
            automation.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256
        ),
        "packageEnvelopeSha256": automation.EXPECTED_PACKAGE_ENVELOPE_SHA256,
        "externalHomeSettingsSha256": automation.EXPECTED_SETTINGS_SHA256,
        "externalHomeBiosSha256": automation.EXPECTED_EXTERNAL_HOME_BIOS_SHA256,
        "hostNormalSettingsSentinelId": automation.HOST_SETTINGS_SENTINEL_ID,
        "hostNormalSettingsSentinelSha256": (
            automation.EXPECTED_HOST_SETTINGS_SENTINEL_SHA256
        ),
        "dataFiles": data_files,
    }
    payload.update(changes)
    return payload


class SnapshotVerifierProxy:
    def __init__(
        self, snapshot: dict[str, object] | None = None, error: Exception | None = None
    ) -> None:
        self.snapshot = snapshot
        self.error = error
        self.calls: list[tuple[pathlib.Path, str, str, str, str | None]] = []

    def capture_runtime_snapshot(
        self,
        repository_root: pathlib.Path,
        *,
        phase: str,
        session_id: str,
        process_instance_id: str,
        captured_at_utc: str | None = None,
    ) -> dict[str, object]:
        self.calls.append((
            repository_root,
            phase,
            session_id,
            process_instance_id,
            captured_at_utc,
        ))
        if self.error is not None:
            raise self.error
        if self.snapshot is None:
            raise RuntimeError("Snapshot proxy has no result.")
        return self.snapshot


def make_process(
    *,
    pid: int = 700,
    effective_uid: int = 501,
    real_uid: int = 501,
    saved_uid: int = 501,
    executable: Any | None = None,
) -> Any:
    return automation.ProcessIdentity(
        pid=pid,
        effective_uid=effective_uid,
        real_uid=real_uid,
        saved_uid=saved_uid,
        start_seconds=1_700_000_000,
        start_microseconds=123456,
        executable=executable or make_file(),
    )


class FakeVerifier:
    def __init__(self, profile: Any) -> None:
        self.profile = profile
        self.calls = 0

    def verify(self) -> Any:
        self.calls += 1
        return self.profile


class FakeObserver:
    def __init__(
        self,
        identities: Sequence[Any],
        *,
        release_identities: Sequence[Any] | None = None,
    ) -> None:
        if not identities:
            raise AssertionError("Fake observer requires an identity.")
        self.identities = list(identities)
        self.release_identities = list(release_identities or (identities[-1],))
        self.calls: list[int] = []
        self.release_calls: list[int] = []

    def observe(self, pid: int) -> Any:
        self.calls.append(pid)
        if not self.identities:
            raise AssertionError("Fake observer exhausted its identity sequence.")
        if len(self.identities) == 1:
            return self.identities[0]
        return self.identities.pop(0)

    def observe_release_identity(self, pid: int, baseline: Any) -> Any:
        self.release_calls.append(pid)
        if not self.release_identities:
            raise AssertionError("Fake release observer exhausted its identity sequence.")
        if len(self.release_identities) == 1:
            observed = self.release_identities[0]
        else:
            observed = self.release_identities.pop(0)
        if observed.executable.device != baseline.executable.device:
            raise automation.AutomationRefusal("Fake mapped vnode differs at release.")
        return observed


class FakeBackend:
    def __init__(
        self,
        *,
        permission: bool = True,
        permission_values: Sequence[bool] | None = None,
        fail_down: bool = False,
        fail_up_count: int = 0,
        frontmost_pids: Sequence[int] = (700,),
        simulate_macro_toggles: bool = False,
    ) -> None:
        self.permission = permission
        self.permission_values = list(permission_values or ())
        self.fail_down = fail_down
        self.fail_up_count = fail_up_count
        self.posts: list[tuple[int, int, bool]] = []
        self.preflight_calls = 0
        self.frontmost_pids = list(frontmost_pids)
        self.frontmost_checks: list[tuple[int, int]] = []
        self.simulate_macro_toggles = simulate_macro_toggles
        self.macro_held: dict[int, bool] = {
            key_code: False for key_code in (18, 19, 20, 21, 22, 23)
        }

    def preflight_access(self) -> bool:
        self.preflight_calls += 1
        if self.permission_values:
            if len(self.permission_values) == 1:
                return self.permission_values[0]
            return self.permission_values.pop(0)
        return self.permission

    def require_frontmost_pid(self, expected_pid: int) -> None:
        observed_pid = self._observe_frontmost_pid(expected_pid)
        if observed_pid != expected_pid:
            raise automation.AutomationRefusal(
                "Exact DuckStation PID is not the frontmost application; no event was posted."
            )

    def _observe_frontmost_pid(self, expected_pid: int) -> int:
        if not self.frontmost_pids:
            raise AssertionError("Fake frontmost PID sequence was empty.")
        if len(self.frontmost_pids) == 1:
            observed_pid = self.frontmost_pids[0]
        else:
            observed_pid = self.frontmost_pids.pop(0)
        self.frontmost_checks.append((expected_pid, observed_pid))
        return observed_pid

    def post_key_down(self, pid: int, key_code: int) -> None:
        try:
            if not self.preflight_access():
                raise automation.AutomationRefusal(
                    "Accessibility post-event permission is not already granted."
                )
            self.require_frontmost_pid(pid)
        except automation.AutomationRefusal as exc:
            raise automation.KeyDownNotPostedRefusal(
                "Key-down was not posted because DuckStation was not the frontmost PID."
            ) from exc
        self.posts.append((pid, key_code, True))
        if self.simulate_macro_toggles and key_code in self.macro_held:
            self.macro_held[key_code] = not self.macro_held[key_code]
        if self.fail_down:
            raise OSError("injected key-down failure")

    def force_key_up(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Any,
    ) -> Any:
        before_pid = self._observe_frontmost_pid(pid)
        access_before = self.preflight_access()
        identity_validator()
        self.posts.append((pid, key_code, False))
        if self.fail_up_count > 0:
            self.fail_up_count -= 1
            raise OSError("injected key-up failure")
        after_pid = self._observe_frontmost_pid(pid)
        access_after = self.preflight_access()
        return automation.ReleaseEnvironmentObservation(
            before_pid=before_pid,
            after_pid=after_pid,
            before_error=None,
            after_error=None,
            access_before=access_before,
            access_after=access_after,
            access_before_error=None,
            access_after_error=None,
        )

    def force_compensating_key_down(
        self,
        pid: int,
        key_code: int,
        *,
        identity_validator: Any,
    ) -> Any:
        if not self.preflight_access():
            raise automation.KeyDownNotPostedRefusal(
                "Compensating key-down lacked Accessibility permission."
            )
        before_pid = self._observe_frontmost_pid(pid)
        access_before = self.preflight_access()
        identity_validator()
        self.posts.append((pid, key_code, True))
        if self.simulate_macro_toggles and key_code in self.macro_held:
            self.macro_held[key_code] = not self.macro_held[key_code]
        after_pid = self._observe_frontmost_pid(pid)
        access_after = self.preflight_access()
        return automation.ReleaseEnvironmentObservation(
            before_pid=before_pid,
            after_pid=after_pid,
            before_error=None,
            after_error=None,
            access_before=access_before,
            access_after=access_after,
            access_before_error=None,
            access_after_error=None,
        )


class NoopPadConfirmer:
    def __init__(self) -> None:
        self.preflight_calls = 0
        self.confirmed: list[str] = []

    def readiness(self) -> Any:
        return automation.PadConfirmationReadiness(
            "offline-noop-pad-confirmer", True, None
        )

    def preflight(self, events: Sequence[Any], process: Any) -> None:
        del process
        self.preflight_calls += 1

    def confirm_initial_released(
        self, actions: Sequence[Any], process: Any
    ) -> None:
        del process
        self.baseline_actions = tuple(actions)

    def observe_state(self, action: Any, process: Any) -> Any:
        del action, process
        return automation.PadState.RELEASED

    def confirm(self, event: Any, process: Any) -> None:
        del process
        self.confirmed.append(event.nonce)


class ReadyOfflineRuntimeAttestor:
    def __init__(self, *, fail_on_call: int | None = None) -> None:
        self.attested: list[int] = []
        self.calls = 0
        self.fail_on_call = fail_on_call

    def readiness(self) -> Any:
        return automation.RuntimeSessionAttestationReadiness(
            "offline-ready-runtime-attestor", True, None
        )

    def attest(self, profile: Any, process: Any) -> None:
        self.calls += 1
        if self.calls == self.fail_on_call:
            raise automation.AutomationRefusal(
                "Injected post-down runtime-session attestation drift."
            )
        if process.executable != profile.executable:
            raise automation.AutomationRefusal("Offline runtime executable mismatch.")
        self.attested.append(process.pid)


class StatefulPadConfirmer(NoopPadConfirmer):
    def __init__(
        self,
        backend: FakeBackend,
        *,
        fail_confirmation_number: int | None = None,
    ) -> None:
        super().__init__()
        self.backend = backend
        self.fail_confirmation_number = fail_confirmation_number
        self.confirmation_count = 0
        self.observations: list[tuple[Any, Any]] = []

    @staticmethod
    def _key_code(action: Any) -> int:
        return automation.ACTION_SPECS[action].key_code

    def confirm_initial_released(
        self, actions: Sequence[Any], process: Any
    ) -> None:
        del process
        for action in actions:
            if self.backend.macro_held[self._key_code(action)]:
                raise automation.AutomationRefusal(
                    f"Macro-toggle {action.value} initial pad baseline is not RELEASED."
                )

    def observe_state(self, action: Any, process: Any) -> Any:
        del process
        state = (
            automation.PadState.HELD
            if self.backend.macro_held[self._key_code(action)]
            else automation.PadState.RELEASED
        )
        self.observations.append((action, state))
        return state

    def confirm(self, event: Any, process: Any) -> None:
        del process
        self.confirmation_count += 1
        if self.confirmation_count == self.fail_confirmation_number:
            raise automation.AutomationRefusal("Injected pad confirmation failure.")
        observed = self.observe_state(event.action, make_process())
        if observed is not event.expected_pad_state:
            raise automation.AutomationRefusal("Stateful pad confirmation mismatch.")
        self.confirmed.append(event.nonce)


def make_foundation(
    *,
    profile: Any | None = None,
    verifier: Any | None = None,
    process: Any | None = None,
    observer: Any | None = None,
    backend: FakeBackend | None = None,
    confirmer: Any | None = None,
    guard: Any | None = None,
    attestor: Any | None = None,
    coordinator_lease_factory: Any | None = None,
    sleeper: Any = lambda _: None,
) -> tuple[Any, FakeBackend, Any]:
    selected_profile = profile or make_profile()
    selected_process = process or make_process()
    selected_backend = backend or FakeBackend()
    selected_confirmer = confirmer or NoopPadConfirmer()
    selected_attestor = attestor or ReadyOfflineRuntimeAttestor()
    state_temp: tempfile.TemporaryDirectory[str] | None = None
    if guard is None:
        state_temp = tempfile.TemporaryDirectory(prefix="id65-unattended-state-")
        guard = automation.DurableRunGuard(
            pathlib.Path(state_temp.name).resolve() / "private-state",
            expected_uid=os.getuid(),
        )
    lease_temp: tempfile.TemporaryDirectory[str] | None = None
    if coordinator_lease_factory is None:
        lease_temp = tempfile.TemporaryDirectory(prefix="id65-shared-lease-")
        lease_root = pathlib.Path(lease_temp.name).resolve()
        coordinator_lease_factory = lambda: automation.SharedCaptureCoordinatorLease(
            lease_root, expected_uid=os.getuid()
        )
    selected_observer = observer or FakeObserver([selected_process])
    foundation = automation.UnattendedFoundation(
        verifier=verifier or FakeVerifier(selected_profile),
        observer=selected_observer,
        transport=automation.TargetedKeyTransport(
            selected_backend, sleeper=sleeper
        ),
        pad_confirmer=selected_confirmer,
        runtime_attestor=selected_attestor,
        run_guard=guard,
        coordinator_lease_factory=coordinator_lease_factory,
    )
    foundation._offline_state_temp = state_temp
    foundation._offline_lease_temp = lease_temp
    return foundation, selected_backend, selected_confirmer


def test_canonical_plan_and_allowlist() -> None:
    plan = parse_plan(
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
    )
    if [event.sequence for event in plan.events] != [0, 1, 2]:
        raise AssertionError("Canonical event sequence changed.")
    if 53 in {spec.key_code for spec in automation.ACTION_SPECS.values()}:
        raise AssertionError("Escape entered the fixed allowlist.")
    if automation.ACTION_SPECS[automation.KeyAction.POWER_OFF].key_code != 111:
        raise AssertionError("Verified PowerOff transport is not F12.")
    if automation.ACTION_SPECS[automation.KeyAction.TOGGLE_PAUSE].key_code != 49:
        raise AssertionError("Verified TogglePause transport is not Space.")

    arbitrary = json.loads(plan_bytes((("TOGGLE_PAUSE", "NOT_APPLICABLE"),)))
    arbitrary["events"][0]["action"] = "ARBITRARY_KEY_42"
    expect_refusal(
        lambda: automation.InputPlan.parse(automation.canonical_json(arbitrary)),
        "allowlist",
    )
    for forbidden in ("SAVE_STATE", "LOAD_STATE", "F1", "Escape"):
        payload = json.loads(plan_bytes((("TOGGLE_PAUSE", "NOT_APPLICABLE"),)))
        payload["events"][0]["action"] = forbidden
        expect_refusal(
            lambda payload=payload: automation.InputPlan.parse(
                automation.canonical_json(payload)
            ),
            "forbidden",
        )
    expect_refusal(
        lambda: parse_plan(
            ("POWER_OFF", "NOT_APPLICABLE"),
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ),
        "terminal",
    )
    expect_refusal(
        lambda: parse_plan(
            ("POWER_OFF", "NOT_APPLICABLE"),
            ("POWER_OFF", "NOT_APPLICABLE"),
        ),
        "at most once",
    )
    expect_refusal(
        lambda: automation.InputPlan.parse(
            plan_bytes((("PAD_LEFT_TOGGLE", "NOT_APPLICABLE"),))
        ),
        "requires held/released",
    )
    expect_refusal(
        lambda: parse_plan(("PAD_LEFT_TOGGLE", "HELD")),
        "ends HELD",
    )
    expect_refusal(
        lambda: parse_plan(
            ("PAD_LEFT_TOGGLE", "RELEASED"),
            ("PAD_LEFT_TOGGLE", "HELD"),
        ),
        "must start HELD",
    )
    expect_refusal(
        lambda: parse_plan(
            ("PAD_LEFT_TOGGLE", "HELD"),
            ("PAD_LEFT_TOGGLE", "HELD"),
        ),
        "alternate",
    )


def test_plan_v1_and_retired_profile_are_refused() -> None:
    v1 = json.loads(plan_bytes((("TOGGLE_PAUSE", "NOT_APPLICABLE"),)))
    v1["formatVersion"] = 1
    expect_refusal(
        lambda: automation.InputPlan.parse(automation.canonical_json(v1)),
        "format version",
    )
    retired_profile = json.loads(
        plan_bytes((("TOGGLE_PAUSE", "NOT_APPLICABLE"),))
    )
    retired_profile["profileId"] = "duckstation-id65-isolated-v1"
    expect_refusal(
        lambda: automation.InputPlan.parse(
            automation.canonical_json(retired_profile)
        ),
        "profile",
    )


def test_verifier_hash_before_import_root_and_contract_pins() -> None:
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    expect_refusal(
        lambda: automation._load_external_home_verifier(repository_root.parent),
        "audited Automation checkout",
    )

    source_path = repository_root / automation.PROFILE_VERIFIER_RELATIVE_PATH
    if hashlib.sha256(source_path.read_bytes()).hexdigest() != (
        automation.EXPECTED_PROFILE_VERIFIER_SHA256
    ):
        raise AssertionError("Production verifier source hash does not match Automation pin.")

    with tempfile.TemporaryDirectory(prefix="id65-verifier-hash-first-") as temp:
        fake_root = pathlib.Path(temp).resolve()
        fake_tools = fake_root / "tools"
        fake_tools.mkdir(mode=0o700)
        marker = fake_root / "source-executed"
        malicious = fake_tools / "Verify-SpyroId65UnattendedProfile.py"
        malicious.write_text(
            "import pathlib\n"
            f"pathlib.Path({str(marker)!r}).write_text('executed')\n",
            encoding="utf-8",
        )
        original_root = automation.AUTOMATION_REPOSITORY_ROOT
        original_hash = automation.EXPECTED_PROFILE_VERIFIER_SHA256
        automation.AUTOMATION_REPOSITORY_ROOT = fake_root
        automation.EXPECTED_PROFILE_VERIFIER_SHA256 = "0" * 64
        try:
            expect_refusal(
                lambda: automation._load_external_home_verifier(fake_root),
                "SHA-256",
            )
        finally:
            automation.AUTOMATION_REPOSITORY_ROOT = original_root
            automation.EXPECTED_PROFILE_VERIFIER_SHA256 = original_hash
        if marker.exists():
            raise AssertionError("Unhashed verifier source executed before pin validation.")

    verifier = automation._load_external_home_verifier(repository_root)
    drift_cases = (
        ("profile_id", "wrong-profile"),
        ("receipt_sha256", "0" * 64),
        ("settings_sha256", "1" * 64),
        ("app_content_manifest_sha256", "2" * 64),
        ("package_envelope_sha256", "3" * 64),
        ("host_settings_sha256", "4" * 64),
    )
    for field, value in drift_cases:
        proxy = types.SimpleNamespace(
            PRODUCTION_REPOSITORY_ROOT=repository_root,
            PROFILE_FORMAT=automation.PROFILE_SNAPSHOT_FORMAT,
            PROFILE_FORMAT_VERSION=automation.PROFILE_SNAPSHOT_VERSION,
            PRODUCTION_PINS=dataclasses.replace(
                verifier.PRODUCTION_PINS, **{field: value}
            ),
            capture_runtime_snapshot=verifier.capture_runtime_snapshot,
        )
        expect_refusal(
            lambda proxy=proxy: automation._require_external_home_verifier_contract(
                proxy, repository_root
            ),
            field,
        )


def test_snapshot_chain_drift_and_exception_are_zero_post() -> None:
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    captured_at_utc = "2026-08-24T12:34:56.789Z"
    original_loader = automation._load_external_home_verifier
    original_clock = automation._canonical_utc_now
    plan = parse_plan(("POWER_OFF", "NOT_APPLICABLE"))
    cases: list[dict[str, object]] = [
        {"profileId": "wrong-profile"},
        {"handoffReceiptSha256": "0" * 64},
        {"externalHomeSettingsSha256": "1" * 64},
        {"appContentManifestSha256": "2" * 64},
        {"packageEnvelopeSha256": "3" * 64},
        {"externalHomeBiosSha256": "4" * 64},
        {"hostNormalSettingsSentinelSha256": "5" * 64},
    ]
    try:
        automation._canonical_utc_now = lambda: captured_at_utc
        for changes in cases:
            proxy = SnapshotVerifierProxy(make_profile_snapshot(**changes))
            automation._load_external_home_verifier = lambda _root, proxy=proxy: proxy
            backend = FakeBackend()
            foundation, _, _ = make_foundation(
                verifier=automation.FrozenProfileVerifier(repository_root),
                backend=backend,
            )
            expect_refusal(
                lambda foundation=foundation: foundation.run(
                    plan, 700, execute=True
                ),
                "profile snapshot",
            )
            if backend.posts:
                raise AssertionError("Drifted verifier chain allowed F12 input.")
            if len(proxy.calls) != 1 or proxy.calls[0][1:] != (
                "before",
                automation.PROFILE_SNAPSHOT_SESSION_ID,
                automation.PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID,
                captured_at_utc,
            ):
                raise AssertionError("Automation snapshot call contract changed.")

        bad_data = make_profile_snapshot()
        data_files = [dict(entry) for entry in bad_data["dataFiles"]]
        for entry in data_files:
            if entry["relativePath"] == "settings.ini":
                entry["sha256"] = "6" * 64
        bad_data["dataFiles"] = data_files
        data_proxy = SnapshotVerifierProxy(bad_data)
        automation._load_external_home_verifier = lambda _root: data_proxy
        data_backend = FakeBackend()
        data_foundation, _, _ = make_foundation(
            verifier=automation.FrozenProfileVerifier(repository_root),
            backend=data_backend,
        )
        expect_refusal(
            lambda: data_foundation.run(plan, 700, execute=True),
            "settings.ini",
        )
        if data_backend.posts:
            raise AssertionError("Unpinned snapshot settings allowed F12 input.")

        error_proxy = SnapshotVerifierProxy(error=RuntimeError("injected snapshot failure"))
        automation._load_external_home_verifier = lambda _root: error_proxy
        error_backend = FakeBackend()
        error_foundation, _, _ = make_foundation(
            verifier=automation.FrozenProfileVerifier(repository_root),
            backend=error_backend,
        )
        expect_refusal(
            lambda: error_foundation.run(plan, 700, execute=True),
            "runtime snapshot",
        )
        if error_backend.posts:
            raise AssertionError("Snapshot exception allowed F12 input.")
    finally:
        automation._load_external_home_verifier = original_loader
        automation._canonical_utc_now = original_clock


def test_reordered_and_duplicate_events_refused() -> None:
    payload = json.loads(plan_bytes((
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_ANALOG", "NOT_APPLICABLE"),
    )))
    payload["events"][0]["sequence"] = 1
    payload["events"][1]["sequence"] = 0
    expect_refusal(
        lambda: automation.InputPlan.parse(automation.canonical_json(payload)),
        "reordered",
    )
    duplicate_member = (
        b'{"events":[],"events":[],"format":"x","formatVersion":2,'
        b'"planNonce":"00112233445566778899aabbccddeeff",'
        b'"profileId":"duckstation-id65-unattended-v1"}\n'
    )
    expect_refusal(
        lambda: automation.InputPlan.parse(duplicate_member),
        "duplicate",
    )
    replayed_nonce = json.loads(plan_bytes((
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("PAD_ANALOG", "NOT_APPLICABLE"),
    )))
    replayed_nonce["events"][1]["nonce"] = replayed_nonce["events"][0]["nonce"]
    expect_refusal(
        lambda: automation.InputPlan.parse(automation.canonical_json(replayed_nonce)),
        "replayed",
    )


def test_exact_pid_path_uid_and_hash_refusals() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    wrong_pid, _, _ = make_foundation(process=make_process(pid=701))
    expect_refusal(lambda: wrong_pid.run(plan, 700, execute=False), "PID differs")

    wrong_path_file = dataclasses.replace(make_file(), real_path="/wrong/DuckStation")
    wrong_path, _, _ = make_foundation(
        process=make_process(executable=wrong_path_file)
    )
    expect_refusal(lambda: wrong_path.run(plan, 700, execute=False), "path, inode")

    wrong_uid, _, _ = make_foundation(
        process=make_process(effective_uid=502)
    )
    expect_refusal(lambda: wrong_uid.run(plan, 700, execute=False), "UID")

    wrong_real_uid, _, _ = make_foundation(
        process=make_process(real_uid=502)
    )
    expect_refusal(lambda: wrong_real_uid.run(plan, 700, execute=False), "UID")

    wrong_hash_file = dataclasses.replace(make_file(), sha256="f" * 64)
    wrong_hash, _, _ = make_foundation(
        process=make_process(executable=wrong_hash_file)
    )
    expect_refusal(lambda: wrong_hash.run(plan, 700, execute=False), "hash")


def test_accessibility_permission_refusal_is_non_posting() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    backend = FakeBackend(permission=False)
    foundation, _, confirmer = make_foundation(backend=backend)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "permission",
    )
    if backend.posts:
        raise AssertionError("Permission refusal still posted an event.")
    if confirmer.preflight_calls != 1:
        raise AssertionError("Pad readiness/preflight did not precede Accessibility gating.")


def test_mode_specific_statuses_and_counts() -> None:
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="1234123412341234abcdabcdabcdabcd",
    )
    foundation, backend, _ = make_foundation()
    preflight = foundation.run(plan, 700, execute=False)
    if (
        preflight.status != automation.PREFLIGHT_STATUS
        or preflight.mode != "preflight"
        or (preflight.planned_count, preflight.posted_count, preflight.confirmed_count)
        != (1, 0, 0)
    ):
        raise AssertionError("Preflight status or counts are not truthful.")
    executed = foundation.run(plan, 700, execute=True)
    if (
        executed.status != automation.INPUT_STATUS
        or executed.mode != "execute"
        or (executed.planned_count, executed.posted_count, executed.confirmed_count)
        != (1, 1, 0)
    ):
        raise AssertionError("Execution status or counts are not truthful.")
    if backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Status/count test posted an unexpected pulse sequence.")
    if b"CAPTURED_PENDING_HUMAN" in TOOL_PATH.read_bytes():
        raise AssertionError("Tool still claims media capture before media exists.")


def test_wrong_or_changed_frontmost_pid_is_non_posting() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))

    wrong_backend = FakeBackend(frontmost_pids=(701,))
    wrong_foundation, _, _ = make_foundation(backend=wrong_backend)
    expect_refusal(
        lambda: wrong_foundation.run(plan, 700, execute=True),
        "not the frontmost",
    )
    if wrong_backend.posts:
        raise AssertionError("Wrong frontmost PID still received a posted event.")

    # First observation is the execution preflight.  DuckStation then loses
    # foreground ownership before the backend's immediate pre-post check.
    changed_backend = FakeBackend(frontmost_pids=(700, 701))
    changed_foundation, _, _ = make_foundation(backend=changed_backend)
    expect_refusal(
        lambda: changed_foundation.run(plan, 700, execute=True),
        "not the frontmost",
    )
    if changed_backend.posts:
        raise AssertionError("Changed frontmost PID still received a posted event.")
    if changed_backend.frontmost_checks[:2] != [(700, 700), (700, 701)]:
        raise AssertionError("Frontmost PID was not freshly checked at post time.")


def test_frontmost_pid_is_rechecked_after_key_up() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    backend = FakeBackend(frontmost_pids=(700, 700, 700, 701))
    foundation, _, _ = make_foundation(backend=backend)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "key-up",
    )
    if backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Post-key-up foreground drift changed the release sequence.")
    if foundation.transport.possibly_held:
        raise AssertionError("Successfully posted key-up remained marked as held.")
    if backend.frontmost_checks != [
        (700, 700),
        (700, 700),
        (700, 700),
        (700, 701),
    ]:
        raise AssertionError("Missing the exact post-key-up frontmost PID check.")


def test_focus_change_after_down_forces_exact_release_then_refuses() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    # Initial target check and key-down see DuckStation.  Focus then moves
    # before the owed release and remains elsewhere after it.
    backend = FakeBackend(frontmost_pids=(700, 700, 701, 701))
    foundation, _, _ = make_foundation(backend=backend)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "key was released",
    )
    if backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Focus drift blocked or duplicated the mandatory release.")
    if foundation.transport.possibly_held:
        raise AssertionError("Forced release left a false possibly-held marker.")
    if (
        foundation.observer.calls != [700, 700, 700]
        or foundation.observer.release_calls != [700]
    ):
        raise AssertionError("Forced release skipped exact process identity revalidation.")
    if backend.frontmost_checks != [
        (700, 700),
        (700, 700),
        (700, 701),
        (700, 701),
    ]:
        raise AssertionError("Forced-release focus observations were not exact.")


def test_exact_f12_and_space_bindings_and_empty_action_set() -> None:
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("POWER_OFF", "NOT_APPLICABLE"),
    )
    foundation, backend, _ = make_foundation()
    result = foundation.run(plan, 700, execute=True)
    if (
        result.posted_count != 2
        or backend.posts != [
            (700, 49, True),
            (700, 49, False),
            (700, 111, True),
            (700, 111, False),
        ]
    ):
        raise AssertionError("Verified F12/Space fake pulse sequence changed.")

    without_poweroff = frozenset(
        action
        for action in make_profile().enabled_actions
        if action is not automation.KeyAction.POWER_OFF
    )
    unavailable_profile = make_profile(enabled_actions=without_poweroff)
    unavailable_foundation, unavailable_backend, _ = make_foundation(
        profile=unavailable_profile
    )
    poweroff_plan = parse_plan(
        ("POWER_OFF", "NOT_APPLICABLE"),
        nonce="ab00ab00ab00ab00cd00cd00cd00cd00",
    )
    expect_refusal(
        lambda: unavailable_foundation.run(poweroff_plan, 700, execute=True),
        "absent from the verified profile",
    )
    if unavailable_backend.posts:
        raise AssertionError("Unverified PowerOff action posted F12.")

    empty_profile = make_profile(enabled_actions=frozenset())
    empty_foundation, empty_backend, _ = make_foundation(profile=empty_profile)
    hotkey_plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="eeee1111dddd2222cccc3333bbbb4444",
    )
    expect_refusal(
        lambda: empty_foundation.run(hotkey_plan, 700, execute=False),
        "absent from the verified profile",
    )
    if empty_backend.posts:
        raise AssertionError("Empty enabled_actions helper silently restored defaults.")


def test_post_down_attestation_and_accessibility_drift_release_then_refuse() -> None:
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="ab12ab12ab12ab12cd34cd34cd34cd34",
    )
    attestor = ReadyOfflineRuntimeAttestor(fail_on_call=3)
    foundation, backend, _ = make_foundation(attestor=attestor)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "post-release",
    )
    if backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Runtime-attestation drift vetoed or duplicated owed key-up.")
    if foundation.transport.possibly_held:
        raise AssertionError("Runtime-attestation drift left a false held marker.")
    if attestor.calls != 3:
        raise AssertionError("Runtime drift was not observed only after owed key-up.")

    access_backend = FakeBackend(
        permission_values=(True, True, False, False)
    )
    access_foundation, _, _ = make_foundation(backend=access_backend)
    access_plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="dc34dc34dc34dc34ba12ba12ba12ba12",
    )
    expect_refusal(
        lambda: access_foundation.run(access_plan, 700, execute=True),
        "access-before=False",
    )
    if access_backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Accessibility drift vetoed or duplicated owed key-up.")
    if access_foundation.transport.possibly_held:
        raise AssertionError("Accessibility drift left a false held marker.")
    if access_backend.preflight_calls != 4:
        raise AssertionError("Accessibility drift was not observed around exact key-up.")

    baseline = make_process()
    swapped = dataclasses.replace(
        baseline,
        executable=dataclasses.replace(
            baseline.executable, real_path="/replacement/DuckStation"
        ),
    )
    swap_observer = FakeObserver(
        [baseline, baseline, swapped], release_identities=[baseline]
    )
    swap_foundation, swap_backend, _ = make_foundation(observer=swap_observer)
    swap_plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="fe12fe12fe12fe12dc34dc34dc34dc34",
    )
    expect_refusal(
        lambda: swap_foundation.run(swap_plan, 700, execute=True),
        "post-release",
    )
    if swap_backend.posts != [(700, 49, True), (700, 49, False)]:
        raise AssertionError("Executable pathname replacement vetoed owed key-up.")
    if swap_foundation.transport.possibly_held or swap_observer.release_calls != [700]:
        raise AssertionError("Release-only mapped identity left a false held marker.")


def test_exception_path_releases_key() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))

    def faulting_sleep(_: float) -> None:
        raise KeyboardInterrupt("injected pulse interruption")

    foundation, backend, _ = make_foundation(sleeper=faulting_sleep)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "failed before release",
    )
    expected = [
        (700, 49, True),
        (700, 49, False),
    ]
    if backend.posts != expected:
        raise AssertionError(f"Interrupted pulse release mismatch: {backend.posts!r}")
    if foundation.transport.possibly_held:
        raise AssertionError("Interrupted pulse left a key marked held.")


def test_stuck_key_is_retried_and_refused() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    backend = FakeBackend(fail_up_count=2)
    foundation, _, _ = make_foundation(backend=backend)
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "release or macro cleanup",
    )
    if backend.posts != [
        (700, 49, True),
        (700, 49, False),
        (700, 49, False),
        (700, 49, False),
    ]:
        raise AssertionError("Stuck key did not receive the mandatory cleanup retry.")
    if foundation.transport.possibly_held:
        raise AssertionError("Successful final cleanup retry remained marked held.")
    if (
        foundation.observer.calls != [700, 700, 700]
        or foundation.observer.release_calls != [700, 700, 700]
    ):
        raise AssertionError("Cleanup retry skipped exact process identity revalidation.")


def test_transport_rejects_non_enum_action() -> None:
    backend = FakeBackend()
    transport = automation.TargetedKeyTransport(backend, sleeper=lambda _: None)
    expect_refusal(
        lambda: transport.tap(
            700,
            "SAVE_SELECTED_STATE",
            release_identity_validator=lambda: None,
            post_release_drift_observer=lambda: None,
            pre_key_down_validator=lambda: None,
        ),
        "enum allowlist",
    )
    if backend.posts:
        raise AssertionError("Non-enum action reached the backend.")


class FakeClock:
    def __init__(self) -> None:
        self.value = 0.0

    def clock(self) -> float:
        return self.value

    def sleep(self, seconds: float) -> None:
        self.value += seconds


class FixedPadReader:
    def __init__(self, words: Sequence[int]) -> None:
        self.words = list(words)
        self.calls = 0

    def read_pad_word(self, process: Any) -> int:
        del process
        self.calls += 1
        if len(self.words) == 1:
            return self.words[0]
        return self.words.pop(0)


class DeadPadReader:
    def __init__(self) -> None:
        self.processes: list[Any] = []

    def read_pad_word(self, process: Any) -> int:
        self.processes.append(process)
        raise OSError("injected unavailable read-only GDB reader")


def expect_any_refusal(callback: Any) -> str:
    try:
        callback()
    except automation.AutomationRefusal as exc:
        return str(exc)
    else:
        raise AssertionError("Expected a fail-closed AutomationRefusal.")


def gdb_rsp_packet(payload: bytes) -> bytes:
    if type(payload) is not bytes:
        raise AssertionError("Offline RSP fixture payload must be bytes.")
    checksum = sum(payload) & 0xFF
    return b"$" + payload + b"#" + f"{checksum:02x}".encode("ascii")


def gdb_lsof_records(
    *records: Sequence[bytes], terminal: bytes = b"\x00\n"
) -> bytes:
    if not records:
        return b""
    return b"\x00\n".join(b"\x00".join(record) for record in records) + terminal


def gdb_listener_fields(**changes: bytes) -> tuple[bytes, ...]:
    fields = {
        "descriptor": b"f3",
        "access": b"au",
        "socket_type": b"tIPv4",
        "device": b"d0x2cb90ff55f15ccdc",
        "protocol": b"PTCP",
        "endpoint": b"n127.0.0.1:2345",
        "state": b"TST=LISTEN",
        "receive_queue": b"TQR=0",
        "send_queue": b"TQS=0",
    }
    unknown = set(changes).difference(fields)
    if unknown:
        raise AssertionError(f"Unknown lsof fixture fields: {sorted(unknown)!r}")
    fields.update(changes)
    return (
        fields["descriptor"],
        fields["access"],
        fields["socket_type"],
        fields["device"],
        fields["protocol"],
        fields["endpoint"],
        fields["state"],
        fields["receive_queue"],
        fields["send_queue"],
    )


def make_gdb_listener(**changes: object) -> Any:
    values: dict[str, object] = {
        "pid": 700,
        "descriptor": 3,
        "access": "u",
        "socket_type": "IPv4",
        "device_token": "0x2cb90ff55f15ccdc",
        "protocol": "TCP",
        "endpoint": "127.0.0.1:2345",
        "tcp_state": "LISTEN",
        "receive_queue": 0,
        "send_queue": 0,
    }
    values.update(changes)
    return automation.GdbListenerIdentity(**values)


class FakeGdbEndpointAttestor:
    def __init__(self, listeners: Sequence[Any]) -> None:
        if not listeners:
            raise AssertionError("Fake GDB attestor requires an identity.")
        self.listeners = list(listeners)
        self.processes: list[Any] = []

    def observe(self, process: Any) -> Any:
        self.processes.append(process)
        if len(self.listeners) == 1:
            return self.listeners[0]
        return self.listeners.pop(0)


class FakeFragmentedGdbSocket:
    def __init__(
        self,
        chunks: Sequence[bytes | BaseException],
        *,
        peer: object = ("127.0.0.1", 2345),
        fail_send_number: int | None = None,
        fail_close: bool = False,
    ) -> None:
        self.chunks = list(chunks)
        self.peer = peer
        self.fail_send_number = fail_send_number
        self.fail_close = fail_close
        self.timeouts: list[float | None] = []
        self.sent: list[bytes] = []
        self.recv_sizes: list[int] = []
        self.close_calls = 0
        self.closed = False

    def settimeout(self, value: float | None) -> None:
        self.timeouts.append(value)

    def sendall(self, data: bytes) -> None:
        if self.closed:
            raise OSError("injected send on closed fake GDB socket")
        self.sent.append(data)
        if len(self.sent) == self.fail_send_number:
            raise OSError("injected GDB send failure")

    def recv(self, maximum_bytes: int) -> bytes:
        self.recv_sizes.append(maximum_bytes)
        if self.closed:
            raise OSError("injected receive on closed fake GDB socket")
        if not self.chunks:
            return b""
        current = self.chunks.pop(0)
        if isinstance(current, BaseException):
            raise current
        if len(current) <= maximum_bytes:
            return current
        self.chunks.insert(0, current[maximum_bytes:])
        return current[:maximum_bytes]

    def getpeername(self) -> object:
        return self.peer

    def close(self) -> None:
        self.close_calls += 1
        self.closed = True
        if self.fail_close:
            raise OSError("injected GDB close failure")


class FakeGdbConnector:
    def __init__(
        self,
        fake_socket: FakeFragmentedGdbSocket | None = None,
        *,
        error: BaseException | None = None,
    ) -> None:
        self.fake_socket = fake_socket
        self.error = error
        self.calls: list[tuple[tuple[object, ...], dict[str, object]]] = []

    def __call__(self, *args: object, **kwargs: object) -> Any:
        self.calls.append((args, kwargs))
        if self.error is not None:
            raise self.error
        if self.fake_socket is None:
            raise AssertionError("Fake GDB connector has no socket.")
        return self.fake_socket


def make_gdb_reader(
    chunks: Sequence[bytes | BaseException],
    *,
    process: Any | None = None,
    observer: Any | None = None,
    listeners: Sequence[Any] | None = None,
    peer: object = ("127.0.0.1", 2345),
    fail_send_number: int | None = None,
    connector_error: BaseException | None = None,
) -> tuple[Any, FakeFragmentedGdbSocket, FakeGdbConnector, FakeGdbEndpointAttestor]:
    selected_process = process or make_process()
    fake_socket = FakeFragmentedGdbSocket(
        chunks,
        peer=peer,
        fail_send_number=fail_send_number,
    )
    connector = FakeGdbConnector(fake_socket, error=connector_error)
    attestor = FakeGdbEndpointAttestor(
        listeners or (make_gdb_listener(pid=selected_process.pid),)
    )
    reader = automation.PersistentReadOnlyDuckStationGdbReader(
        observer or FakeObserver([selected_process]),
        attestor,
        connector=connector,
    )
    return reader, fake_socket, connector, attestor


def test_strict_gdb_listener_parser_and_native_negative() -> None:
    literal_lsof_command = (
        "/usr/sbin/lsof",
        "-nP",
        "-a",
        "-iTCP:2345",
        "-sTCP:LISTEN",
        "-F0pfadDikstnPT",
    )
    if automation.GDB_LSOF_COMMAND != literal_lsof_command:
        raise AssertionError("Fixed global GDB listener lsof command changed.")
    process_record = (b"p700",)
    listener_record = gdb_listener_fields()
    valid = gdb_lsof_records(process_record, listener_record)
    observed = automation._parse_gdb_listener_lsof(valid)
    if observed != make_gdb_listener():
        raise AssertionError("Strict lsof parser changed the pinned listener identity.")

    reordered = list(listener_record)
    reordered[1], reordered[2] = reordered[2], reordered[1]
    invalid_outputs = (
        b"",
        valid[:-1],
        b"\xef\xbb\xbf" + valid,
        valid.replace(b"d0x2cb", b"d\xff2cb", 1),
        gdb_lsof_records((b"p0",), listener_record),
        gdb_lsof_records((b"p0700",), listener_record),
        gdb_lsof_records((b"p2147483648",), listener_record),
        gdb_lsof_records((b"p" + b"9" * 5000,), listener_record),
        gdb_lsof_records(process_record, listener_record, listener_record),
        gdb_lsof_records(process_record, process_record, listener_record),
        gdb_lsof_records(process_record, listener_record[:-1]),
        gdb_lsof_records(process_record, listener_record + (b"zunknown",)),
        gdb_lsof_records(process_record, tuple(reordered)),
        gdb_lsof_records(
            process_record,
            listener_record[:1] + (listener_record[0],) + listener_record[2:],
        ),
        gdb_lsof_records(process_record, gdb_listener_fields(descriptor=b"f-1")),
        gdb_lsof_records(
            process_record, gdb_listener_fields(descriptor=b"f2147483648")
        ),
        gdb_lsof_records(
            process_record, gdb_listener_fields(descriptor=b"f" + b"9" * 5000)
        ),
        gdb_lsof_records(process_record, gdb_listener_fields(access=b"ar")),
        gdb_lsof_records(process_record, gdb_listener_fields(socket_type=b"tIPv6")),
        gdb_lsof_records(process_record, gdb_listener_fields(device=b"d1234")),
        gdb_lsof_records(process_record, gdb_listener_fields(protocol=b"PUDP")),
        gdb_lsof_records(
            process_record, gdb_listener_fields(endpoint=b"n0.0.0.0:2345")
        ),
        gdb_lsof_records(
            process_record, gdb_listener_fields(endpoint=b"n127.0.0.1:1234")
        ),
        gdb_lsof_records(
            process_record, gdb_listener_fields(state=b"TST=ESTABLISHED")
        ),
        gdb_lsof_records(
            process_record, gdb_listener_fields(receive_queue=b"TQR=1")
        ),
        gdb_lsof_records(
            process_record, gdb_listener_fields(send_queue=b"TQS=1")
        ),
    )
    for index, raw in enumerate(invalid_outputs):
        message = expect_any_refusal(
            lambda raw=raw: automation._parse_gdb_listener_lsof(raw)
        )
        if not message:
            raise AssertionError(f"lsof refusal {index} had no diagnostic.")

    runner_calls: list[tuple[str, ...]] = []

    def exact_runner(command: Sequence[str]) -> subprocess.CompletedProcess[bytes]:
        runner_calls.append(tuple(command))
        return subprocess.CompletedProcess(tuple(command), 0, valid, b"")

    attestor = automation.MacGdbEndpointAttestor(runner=exact_runner)
    if attestor.observe(make_process()) != make_gdb_listener():
        raise AssertionError("Injected lsof attestor did not return the exact listener.")
    if runner_calls != [automation.GDB_LSOF_COMMAND]:
        raise AssertionError("GDB attestor changed the fixed global lsof command.")
    expect_any_refusal(lambda: attestor.observe(make_process(pid=701)))

    def noisy_runner(command: Sequence[str]) -> subprocess.CompletedProcess[bytes]:
        return subprocess.CompletedProcess(tuple(command), 0, valid, b"warning")

    expect_any_refusal(
        lambda: automation.MacGdbEndpointAttestor(runner=noisy_runner).observe(
            make_process()
        )
    )

    # This native check is read-only.  It never opens a socket: whether port
    # 2345 is free or owned by some other process, this test PID cannot own its
    # listener and therefore the global attestor must refuse it.
    if sys.platform == "darwin":
        native_process = make_process(pid=os.getpid())
        expect_any_refusal(
            lambda: automation.MacGdbEndpointAttestor().observe(native_process)
        )


def test_persistent_gdb_reader_fixed_packets_and_fragmentation() -> None:
    process = make_process()
    literal_pad_packet = b"$m800786a0,4#9b"
    literal_vblank_packet = b"$m800758c8,4#a4"
    if (
        automation.GDB_PAD_READ_PACKET != literal_pad_packet
        or automation.GDB_VBLANK_READ_PACKET != literal_vblank_packet
        or automation.GDB_ALLOWED_OUTBOUND
        != frozenset({literal_pad_packet, literal_vblank_packet, b"+", b"-"})
    ):
        raise AssertionError("Fixed GDB addresses, checksums, or outbound set changed.")
    stop_s = gdb_rsp_packet(b"S05")
    stop_t = gdb_rsp_packet(b"T05thread:1;")
    first_reply = b"++" + stop_s + gdb_rsp_packet(b"00417fff")
    second_reply = b"+" + stop_t + gdb_rsp_packet(b"78563412")
    # One-byte fragments prove the parser does not depend on recv boundaries.
    chunks = [bytes((value,)) for value in first_reply + second_reply]
    reader, fake_socket, connector, attestor = make_gdb_reader(
        chunks, process=process
    )
    if reader.read_pad_word(process) != 0xFF7F:
        raise AssertionError("Digital-pad reply was not decoded little-endian.")
    if reader.read_vblank_tick(process) != 0x12345678:
        raise AssertionError("Vblank reply was not decoded as an unsigned uint32.")
    expected_outbound = [
        automation.GDB_PAD_READ_PACKET,
        b"+",
        b"+",
        automation.GDB_VBLANK_READ_PACKET,
        b"+",
        b"+",
    ]
    if fake_socket.sent != expected_outbound:
        raise AssertionError(
            f"Persistent GDB outbound surface changed: {fake_socket.sent!r}"
        )
    if any(packet not in automation.GDB_ALLOWED_OUTBOUND for packet in fake_socket.sent):
        raise AssertionError("GDB reader emitted bytes outside the fixed allowlist.")
    if connector.calls != [
        (((automation.GDB_HOST, automation.GDB_PORT), automation.GDB_SOCKET_TIMEOUT_SECONDS), {})
    ]:
        raise AssertionError(f"Persistent GDB connector calls changed: {connector.calls!r}")
    if fake_socket.timeouts != [automation.GDB_SOCKET_TIMEOUT_SECONDS]:
        raise AssertionError("Persistent GDB socket timeout was not pinned once.")
    if not fake_socket.recv_sizes or set(fake_socket.recv_sizes) != {1}:
        raise AssertionError("GDB reader performed transaction-unsafe socket read-ahead.")
    if len(attestor.processes) != 6 or any(item != process for item in attestor.processes):
        raise AssertionError("Persistent reads skipped exact listener re-attestation.")
    reader.close()
    reader.close()
    if fake_socket.close_calls != 1:
        raise AssertionError("Normal GDB close is not idempotent.")
    sent_before = tuple(fake_socket.sent)
    expect_any_refusal(lambda: reader.read_pad_word(process))
    if tuple(fake_socket.sent) != sent_before or len(connector.calls) != 1:
        raise AssertionError("Closed GDB reader sent or reconnected.")

    # Only the exact three-byte Ehh form is an RSP error.  Eight hexadecimal
    # digits beginning with E are valid four-byte memory data and must not be
    # misclassified by a prefix-only check.
    e_data_reader, e_data_socket, e_data_connector, _ = make_gdb_reader(
        (b"+" + gdb_rsp_packet(b"E0000000"),), process=process
    )
    if e_data_reader.read_vblank_tick(process) != 0x000000E0:
        raise AssertionError("Valid E0000000 memory data was mistaken for an RSP error.")
    if e_data_socket.sent != [automation.GDB_VBLANK_READ_PACKET, b"+"]:
        raise AssertionError("Valid E-prefixed data changed the fixed vblank exchange.")
    e_data_reader.close()
    if len(e_data_connector.calls) != 1 or e_data_socket.close_calls != 1:
        raise AssertionError("Valid E-prefixed data did not use one persistent socket.")

    # An asynchronous stop notification may arrive before the server's ACK for
    # this request.  It is still checksum-validated and client-ACKed, while the
    # actual memory data remains forbidden until the request '+' is observed.
    early_stop_reader, early_stop_socket, early_stop_connector, _ = make_gdb_reader(
        (
            gdb_rsp_packet(b"S05")
            + b"+"
            + gdb_rsp_packet(b"00417fff"),
        ),
        process=process,
    )
    if early_stop_reader.read_pad_word(process) != 0xFF7F:
        raise AssertionError("A valid pre-ACK stop notification blocked pad data.")
    if early_stop_socket.sent != [automation.GDB_PAD_READ_PACKET, b"+", b"+"]:
        raise AssertionError("Pre-ACK stop notification acknowledgement changed.")
    early_stop_reader.close()
    if len(early_stop_connector.calls) != 1 or early_stop_socket.close_calls != 1:
        raise AssertionError("Pre-ACK stop fixture did not use one persistent socket.")

    released_reader, released_socket, released_connector, _ = make_gdb_reader(
        (b"+" + gdb_rsp_packet(b"0041ffff"),), process=process
    )
    if released_reader.read_pad_word(process) != 0xFFFF:
        raise AssertionError("Fully released digital-pad word was decoded incorrectly.")
    released_reader.close()
    if len(released_connector.calls) != 1 or released_socket.close_calls != 1:
        raise AssertionError("Released-pad fixture did not use one persistent socket.")


def test_persistent_gdb_reader_rsp_refusal_matrix() -> None:
    good_data = gdb_rsp_packet(b"00417fff")
    too_many_stops = b"+" + gdb_rsp_packet(b"S05") * (
        automation.GDB_MAX_RSP_EVENTS_PER_READ + 1
    )
    oversized_payload = (
        b"+"
        + b"$"
        + b"a" * (automation.GDB_MAX_RSP_PAYLOAD_BYTES + 1)
    )
    cases: tuple[tuple[str, Sequence[bytes | BaseException], bool], ...] = (
        ("negative-ack", (b"-",), False),
        ("missing-ack", (good_data,), False),
        ("unexpected-marker", (b"+?",), False),
        ("eof", (b"+",), False),
        ("eof-mid-payload", (b"+$0041",), False),
        ("eof-mid-checksum", (b"+$00417fff#8",), False),
        ("socket-error", (OSError("injected receive failure"),), False),
        ("bad-checksum", (b"+$00417fff#00",), True),
        ("malformed-checksum", (b"+$00417fff#gg",), True),
        ("oversized", (oversized_payload[:4096], oversized_payload[4096:]), True),
        ("memory-error", (b"+" + gdb_rsp_packet(b"E01"),), False),
        ("nonhex-data", (b"+" + gdb_rsp_packet(b"zzzzzzzz"),), False),
        ("short-data", (b"+" + gdb_rsp_packet(b"0000"),), False),
        ("long-data", (b"+" + gdb_rsp_packet(b"0000000000"),), False),
        ("malformed-s-stop", (b"+" + gdb_rsp_packet(b"S0g"),), False),
        (
            "malformed-t-stop",
            (b"+" + gdb_rsp_packet(b"T0gthread:1;"),),
            False,
        ),
        ("event-bound", (too_many_stops,), False),
    )
    process = make_process()
    for name, chunks, requires_nack in cases:
        reader, fake_socket, connector, attestor = make_gdb_reader(
            chunks, process=process
        )
        message = expect_any_refusal(lambda: reader.read_pad_word(process))
        if not message or fake_socket.close_calls != 1 or not fake_socket.closed:
            raise AssertionError(f"RSP case {name} did not poison and close.")
        if len(connector.calls) != 1:
            raise AssertionError(f"RSP case {name} did not use one connection.")
        if not fake_socket.sent or fake_socket.sent[0] != automation.GDB_PAD_READ_PACKET:
            raise AssertionError(f"RSP case {name} changed the fixed read packet.")
        if requires_nack and b"-" not in fake_socket.sent:
            raise AssertionError(f"RSP case {name} omitted its one negative ACK.")
        if fake_socket.sent.count(b"-") > 1:
            raise AssertionError(f"RSP case {name} retransmitted a negative ACK.")
        sent_before = tuple(fake_socket.sent)
        attests_before = len(attestor.processes)
        expect_any_refusal(lambda: reader.read_pad_word(process))
        if (
            tuple(fake_socket.sent) != sent_before
            or len(connector.calls) != 1
            or len(attestor.processes) != attests_before
            or fake_socket.close_calls != 1
        ):
            raise AssertionError(f"Poisoned RSP case {name} retried native work.")

    # Socket setup and writes share the same irreversible poison semantics.
    setup_reader, setup_socket, setup_connector, setup_attestor = make_gdb_reader(
        (), process=process, connector_error=OSError("injected connect failure")
    )
    expect_any_refusal(lambda: setup_reader.read_pad_word(process))
    expect_any_refusal(lambda: setup_reader.read_pad_word(process))
    if len(setup_connector.calls) != 1 or setup_socket.sent or len(setup_attestor.processes) != 1:
        raise AssertionError("Failed GDB connect was retried or sent bytes.")

    send_reader, send_socket, send_connector, _ = make_gdb_reader(
        (), process=process, fail_send_number=1
    )
    expect_any_refusal(lambda: send_reader.read_pad_word(process))
    expect_any_refusal(lambda: send_reader.read_pad_word(process))
    if len(send_connector.calls) != 1 or send_socket.close_calls != 1 or len(send_socket.sent) != 1:
        raise AssertionError("Failed GDB send was retried after poison.")


def test_persistent_gdb_reader_semantic_and_binding_refusals() -> None:
    process = make_process()
    changed_process = dataclasses.replace(process, start_microseconds=654321)
    listener = make_gdb_listener()
    changed_listener = dataclasses.replace(listener, device_token="0xfeedface")
    valid = b"+" + gdb_rsp_packet(b"00417fff")

    main_names = frozenset(automation.main.__code__.co_names)
    if not callable(automation.PersistentReadOnlyDuckStationGdbReader):
        raise AssertionError("The reviewed read-only GDB reader is not public.")
    if (
        "PersistentReadOnlyDuckStationGdbReader" in main_names
        or "UnavailableNativePadConfirmer" not in main_names
    ):
        raise AssertionError(
            "Production main wired the persistent reader before pulse-order review."
        )

    bad_header_reader, bad_header_socket, bad_header_connector, _ = make_gdb_reader(
        (b"+" + gdb_rsp_packet(b"01417fff"),), process=process
    )
    expect_any_refusal(lambda: bad_header_reader.read_pad_word(process))
    if bad_header_socket.close_calls != 1 or len(bad_header_connector.calls) != 1:
        raise AssertionError("Bad PS1 pad header did not poison the connection.")

    pre_process_reader, pre_process_socket, pre_process_connector, pre_process_attestor = (
        make_gdb_reader(
            (), process=process, observer=FakeObserver([changed_process])
        )
    )
    expect_any_refusal(lambda: pre_process_reader.read_pad_word(process))
    if pre_process_connector.calls or pre_process_socket.sent or pre_process_attestor.processes:
        raise AssertionError("Pre-connect process drift reached listener or socket work.")

    pre_listener_reader, pre_listener_socket, pre_listener_connector, _ = make_gdb_reader(
        (), process=process, listeners=(dataclasses.replace(listener, pid=701),)
    )
    expect_any_refusal(lambda: pre_listener_reader.read_pad_word(process))
    if pre_listener_connector.calls or pre_listener_socket.sent:
        raise AssertionError("Wrong listener owner reached the connector.")

    malformed_peers: tuple[tuple[str, object], ...] = (
        ("wrong-host", ("127.0.0.2", 2345)),
        ("wrong-port", ("127.0.0.1", 2346)),
        ("list-not-tuple", ["127.0.0.1", 2345]),
        ("short-tuple", ("127.0.0.1",)),
        ("long-tuple", ("127.0.0.1", 2345, 0)),
        ("boolean-port", ("127.0.0.1", True)),
        ("text-port", ("127.0.0.1", "2345")),
    )
    for name, peer in malformed_peers:
        peer_reader, peer_socket, peer_connector, _ = make_gdb_reader(
            (), process=process, peer=peer
        )
        expect_any_refusal(lambda: peer_reader.read_pad_word(process))
        if (
            len(peer_connector.calls) != 1
            or peer_socket.sent
            or peer_socket.close_calls != 1
        ):
            raise AssertionError(
                f"Malformed GDB peer {name} did not close before sending."
            )

    post_connect_process_reader, post_connect_process_socket, _, _ = make_gdb_reader(
        (),
        process=process,
        observer=FakeObserver([process, changed_process]),
    )
    expect_any_refusal(lambda: post_connect_process_reader.read_pad_word(process))
    if post_connect_process_socket.sent or post_connect_process_socket.close_calls != 1:
        raise AssertionError("Post-connect process replacement reached a read packet.")

    post_connect_listener_reader, post_connect_listener_socket, _, _ = make_gdb_reader(
        (), process=process, listeners=(listener, changed_listener)
    )
    expect_any_refusal(lambda: post_connect_listener_reader.read_pad_word(process))
    if post_connect_listener_socket.sent or post_connect_listener_socket.close_calls != 1:
        raise AssertionError("Post-connect listener replacement reached a read packet.")

    pre_read_process_reader, pre_read_process_socket, _, _ = make_gdb_reader(
        (),
        process=process,
        observer=FakeObserver([process, process, changed_process]),
    )
    expect_any_refusal(lambda: pre_read_process_reader.read_pad_word(process))
    if pre_read_process_socket.sent:
        raise AssertionError("Pre-read process drift sent a packet.")

    pre_read_listener_reader, pre_read_listener_socket, _, _ = make_gdb_reader(
        (), process=process, listeners=(listener, listener, changed_listener)
    )
    expect_any_refusal(lambda: pre_read_listener_reader.read_pad_word(process))
    if pre_read_listener_socket.sent:
        raise AssertionError("Pre-read listener drift sent a packet.")

    post_read_process_reader, post_read_process_socket, _, _ = make_gdb_reader(
        (valid,),
        process=process,
        observer=FakeObserver([process, process, process, changed_process]),
    )
    expect_any_refusal(lambda: post_read_process_reader.read_pad_word(process))
    if post_read_process_socket.sent != [automation.GDB_PAD_READ_PACKET, b"+"]:
        raise AssertionError("Post-read process drift did not discard an exact reply.")

    post_read_listener_reader, post_read_listener_socket, _, _ = make_gdb_reader(
        (valid,),
        process=process,
        listeners=(listener, listener, listener, changed_listener),
    )
    expect_any_refusal(lambda: post_read_listener_reader.read_pad_word(process))
    if post_read_listener_socket.sent != [automation.GDB_PAD_READ_PACKET, b"+"]:
        raise AssertionError("Post-read listener drift did not discard an exact reply.")

    rebound_reader, rebound_socket, rebound_connector, rebound_attestor = make_gdb_reader(
        (valid,), process=process
    )
    if rebound_reader.read_pad_word(process) != 0xFF7F:
        raise AssertionError("Rebind fixture did not complete its first read.")
    sent_before = tuple(rebound_socket.sent)
    attests_before = len(rebound_attestor.processes)
    expect_any_refusal(lambda: rebound_reader.read_vblank_tick(changed_process))
    if (
        tuple(rebound_socket.sent) != sent_before
        or len(rebound_connector.calls) != 1
        or len(rebound_attestor.processes) != attests_before
        or rebound_socket.close_calls != 1
    ):
        raise AssertionError("Process-rebind refusal performed new native work.")


def test_hotkey_only_plan_requires_process_bound_pad_probe() -> None:
    reader = DeadPadReader()
    confirmer = automation.PollingReadOnlyPadConfirmer(reader)
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="51515151515151516262626262626262",
    )
    process = make_process()
    foundation, backend, _ = make_foundation(
        process=process, confirmer=confirmer
    )
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "process-bound preflight probe",
    )
    if backend.posts:
        raise AssertionError("Dead pad reader allowed a hotkey-only event to post.")
    if reader.processes != [process]:
        raise AssertionError("Pad preflight did not make one exact process-bound probe.")


def test_read_only_pad_confirmation_and_timeout() -> None:
    event_plan = parse_plan(
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
    )
    clock = FakeClock()
    reader = FixedPadReader([
        0xFFFF, 0xFFFF, 0xFFFF,
        0xFF7F, 0xFF7F,
        0xFFFF, 0xFFFF,
    ])
    confirmer = automation.PollingReadOnlyPadConfirmer(
        reader,
        clock=clock.clock,
        sleeper=clock.sleep,
        timeout_seconds=0.1,
        interval_seconds=0.01,
        stable_samples=2,
    )
    foundation, backend, _ = make_foundation(confirmer=confirmer)
    result = foundation.run(event_plan, 700, execute=True)
    if result.status != automation.INPUT_STATUS or result.machine_finalized:
        raise AssertionError("Confirmed pad event claimed invalid terminal status.")
    if backend.posts != [
        (700, 23, True), (700, 23, False),
        (700, 23, True), (700, 23, False),
    ]:
        raise AssertionError("Mapped left macro did not use the pinned key code.")
    if (result.planned_count, result.posted_count, result.confirmed_count) != (2, 2, 2):
        raise AssertionError("Macro execution result counts are not truthful.")
    if reader.calls != 7:
        raise AssertionError("Macro plan did not perform one distinct preflight reader probe.")

    timeout_clock = FakeClock()
    timeout_reader = FixedPadReader([0xFFFF])
    timeout_confirmer = automation.PollingReadOnlyPadConfirmer(
        timeout_reader,
        clock=timeout_clock.clock,
        sleeper=timeout_clock.sleep,
        timeout_seconds=0.025,
        interval_seconds=0.01,
        stable_samples=2,
    )
    timeout_foundation, timeout_backend, _ = make_foundation(
        confirmer=timeout_confirmer
    )
    expect_refusal(
        lambda: timeout_foundation.run(event_plan, 700, execute=True),
        "timed out",
    )
    if timeout_backend.posts != [(700, 23, True), (700, 23, False)]:
        raise AssertionError("Pad timeout did not leave the host key released.")


def test_macro_baseline_and_compensating_untoggle() -> None:
    plan = parse_plan(
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        nonce="aaaa2222bbbb3333cccc4444dddd5555",
    )

    multi_action_plan = parse_plan(
        ("PAD_LEFT_TOGGLE", "HELD"),
        ("PAD_DOWN_TOGGLE", "HELD"),
        ("PAD_LEFT_TOGGLE", "RELEASED"),
        ("PAD_DOWN_TOGGLE", "RELEASED"),
        nonce="aaaabbbbccccddddeeeeffff00001111",
    )
    held_backend = FakeBackend(simulate_macro_toggles=True)
    held_backend.macro_held[22] = True
    held_confirmer = StatefulPadConfirmer(held_backend)
    held_foundation, _, _ = make_foundation(
        backend=held_backend, confirmer=held_confirmer
    )
    expect_refusal(
        lambda: held_foundation.run(multi_action_plan, 700, execute=True),
        "initial pad baseline is not RELEASED",
    )
    if held_backend.posts:
        raise AssertionError("Held macro baseline reached the event transport.")

    cleanup_backend = FakeBackend(simulate_macro_toggles=True)
    cleanup_confirmer = StatefulPadConfirmer(
        cleanup_backend, fail_confirmation_number=1
    )
    cleanup_foundation, _, _ = make_foundation(
        backend=cleanup_backend, confirmer=cleanup_confirmer
    )
    expect_refusal(
        lambda: cleanup_foundation.run(plan, 700, execute=True),
        "Injected pad confirmation failure",
    )
    if cleanup_backend.posts != [
        (700, 23, True),
        (700, 23, False),
        (700, 23, True),
        (700, 23, False),
    ]:
        raise AssertionError("Macro cleanup did not perform exactly one compensating untoggle.")
    if cleanup_backend.macro_held[23]:
        raise AssertionError("Compensating untoggle did not restore RELEASED.")
    if cleanup_foundation.transport.possibly_held:
        raise AssertionError("Compensating untoggle left a host key marked held.")
    if not cleanup_confirmer.observations or cleanup_confirmer.observations[-1][1] is not automation.PadState.RELEASED:
        raise AssertionError("Compensating untoggle was not read-only confirmed RELEASED.")


def test_native_pad_source_refuses_before_input() -> None:
    plans = (
        parse_plan(
            ("PAD_DOWN_TOGGLE", "HELD"),
            ("PAD_DOWN_TOGGLE", "RELEASED"),
        ),
        parse_plan(
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
            nonce="11112222333344445555666677778888",
        ),
    )
    for plan in plans:
        for execute in (False, True):
            confirmer = automation.UnavailableNativePadConfirmer()
            foundation, backend, _ = make_foundation(confirmer=confirmer)
            expect_refusal(
                lambda plan=plan, execute=execute: foundation.run(
                    plan, 700, execute=execute
                ),
                "entire input plan",
            )
            if backend.posts or backend.preflight_calls or backend.frontmost_checks:
                raise AssertionError(
                    "Unavailable production GDB readiness reached the event transport."
                )
            if foundation.observer.calls:
                raise AssertionError(
                    "Unavailable production GDB readiness reached live process observation."
                )


def test_runtime_session_attestor_refuses_before_input() -> None:
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="99990000aaaabbbbccccddddeeeeffff",
    )
    for execute in (False, True):
        backend = FakeBackend()
        foundation, _, confirmer = make_foundation(
            backend=backend,
            attestor=automation.UnavailableRuntimeSessionAttestor(),
        )
        expect_refusal(
            lambda execute=execute: foundation.run(plan, 700, execute=execute),
            "exact argv",
        )
        if backend.posts or backend.preflight_calls or backend.frontmost_checks:
            raise AssertionError("Unavailable runtime attestor reached event transport.")
        if foundation.observer.calls:
            raise AssertionError("Unavailable runtime attestor reached process observation.")
        if confirmer.preflight_calls:
            raise AssertionError("Runtime attestor readiness refusal ran pad preflight.")


def test_replay_guard_refuses_second_execution() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    with tempfile.TemporaryDirectory(prefix="id65-durable-replay-") as temp:
        guard = automation.DurableRunGuard(
            pathlib.Path(temp).resolve() / "private-state",
            expected_uid=os.getuid(),
        )
        foundation, backend, _ = make_foundation(guard=guard)
        first = foundation.run(plan, 700, execute=True)
        if first.status != automation.INPUT_STATUS:
            raise AssertionError("First run returned an unexpected status.")
        expect_refusal(
            lambda: foundation.run(plan, 700, execute=True),
            "replay",
        )
        if backend.posts != [(700, 49, True), (700, 49, False)]:
            raise AssertionError("Replayed plan posted a second key pulse.")


def test_durable_lock_ledger_concurrency_and_tamper() -> None:
    base_plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        nonce="ababababababababcdcdcdcdcdcdcdcd",
    )

    def consume(guard: Any, plan: Any) -> None:
        with guard.execution(plan):
            pass

    with tempfile.TemporaryDirectory(prefix="id65-guard-direct-") as temp:
        root = pathlib.Path(temp).resolve() / "state"
        guard = automation.DurableRunGuard(root, expected_uid=os.getuid())
        expect_refusal(
            lambda: consume(
                guard, dataclasses.replace(base_plan, plan_nonce="../../escape")
            ),
            "invalid plan identity",
        )
        with guard.execution(base_plan):
            lock = root / "active-run.lock"
            if stat.S_IMODE(lock.stat().st_mode) != 0o600:
                raise AssertionError("Exclusive run lock mode is not 0600.")
            expect_refusal(
                lambda: consume(
                    automation.DurableRunGuard(root, expected_uid=os.getuid()),
                    base_plan,
                ),
                "namespace authority",
            )
        if (root / "active-run.lock").exists():
            raise AssertionError("Exclusive run lock survived clean context exit.")
        expect_refusal(lambda: consume(guard, base_plan), "replay")
        expect_refusal(
            lambda: consume(
                guard, dataclasses.replace(base_plan, sha256="5" * 64)
            ),
            "plan nonce",
        )
        expect_refusal(
            lambda: consume(
                guard,
                dataclasses.replace(
                    base_plan,
                    plan_nonce="56565656565656567878787878787878",
                ),
            ),
            "plan hash",
        )
        nonce_claim = root / "nonces" / f"{base_plan.plan_nonce}.claim"
        hash_claim = root / "plan-hashes" / f"{base_plan.sha256}.claim"
        if (
            stat.S_IMODE(nonce_claim.stat().st_mode) != 0o600
            or stat.S_IMODE(hash_claim.stat().st_mode) != 0o600
        ):
            raise AssertionError("Durable ledger claims are not mode 0600.")

    with tempfile.TemporaryDirectory(prefix="id65-guard-concurrent-") as temp:
        root = pathlib.Path(temp).resolve() / "state"
        script = r'''
import importlib.util, pathlib, sys
tool_path = pathlib.Path(sys.argv[1])
spec = importlib.util.spec_from_file_location("id65_subprocess_guard", tool_path)
module = importlib.util.module_from_spec(spec)
sys.modules[spec.name] = module
spec.loader.exec_module(module)
plan = module.InputPlan.parse(bytes.fromhex(sys.argv[3]))
guard = module.DurableRunGuard(pathlib.Path(sys.argv[2]), expected_uid=__import__("os").getuid())
with guard.execution(plan):
    print("LOCKED", flush=True)
    sys.stdin.readline()
'''
        child = subprocess.Popen(
            [
                sys.executable,
                "-c",
                script,
                str(TOOL_PATH),
                str(root),
                plan_bytes((("TOGGLE_PAUSE", "NOT_APPLICABLE"),),
                    nonce="01010101010101010202020202020202").hex(),
            ],
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
        )
        try:
            if child.stdout is None or child.stdin is None:
                raise AssertionError("Concurrent guard subprocess pipes are unavailable.")
            ready, _, _ = select.select([child.stdout], [], [], 10.0)
            if not ready or child.stdout.readline() != b"LOCKED\n":
                raise AssertionError("Concurrent guard subprocess did not acquire its lock.")
            child_plan = automation.InputPlan.parse(
                plan_bytes(
                    (("TOGGLE_PAUSE", "NOT_APPLICABLE"),),
                    nonce="01010101010101010202020202020202",
                )
            )
            expect_refusal(
                lambda: consume(
                    automation.DurableRunGuard(root, expected_uid=os.getuid()),
                    child_plan,
                ),
                "namespace authority",
            )
            child.stdin.write(b"\n")
            child.stdin.flush()
            stdout_tail, stderr = child.communicate(timeout=10)
            if child.returncode != 0 or stdout_tail or stderr:
                raise AssertionError(
                    f"Concurrent guard subprocess failed: {child.returncode} {stderr!r}"
                )
            expect_refusal(
                lambda: consume(
                    automation.DurableRunGuard(root, expected_uid=os.getuid()),
                    child_plan,
                ),
                "replay",
            )
        finally:
            if child.poll() is None:
                child.kill()
                child.wait(timeout=10)

    with tempfile.TemporaryDirectory(prefix="id65-guard-symlink-") as temp:
        base = pathlib.Path(temp).resolve()
        real_root = base / "real"
        real_root.mkdir(mode=0o700)
        alias = base / "alias"
        alias.symlink_to(real_root, target_is_directory=True)
        expect_refusal(
            lambda: consume(
                automation.DurableRunGuard(alias, expected_uid=os.getuid()), base_plan
            ),
            "symlink",
        )

        child_root = base / "child-root"
        child_root.mkdir(mode=0o700)
        target = base / "target-ledger"
        target.mkdir(mode=0o700)
        (child_root / "nonces").symlink_to(target, target_is_directory=True)
        expect_refusal(
            lambda: consume(
                automation.DurableRunGuard(child_root, expected_uid=os.getuid()),
                dataclasses.replace(
                    base_plan,
                    plan_nonce="12121212121212123434343434343434",
                    sha256="4" * 64,
                ),
            ),
            "open private nonces ledger",
        )

    with tempfile.TemporaryDirectory(prefix="id65-guard-mode-") as temp:
        root = pathlib.Path(temp).resolve() / "state"
        root.mkdir(mode=0o755)
        os.chmod(root, 0o755)
        expect_refusal(
            lambda: consume(
                automation.DurableRunGuard(root, expected_uid=os.getuid()), base_plan
            ),
            "mode is unsafe",
        )

    with tempfile.TemporaryDirectory(prefix="id65-guard-tamper-") as temp:
        root = pathlib.Path(temp).resolve() / "state"
        guard = automation.DurableRunGuard(root, expected_uid=os.getuid())
        consume(guard, base_plan)
        nonce_claim = root / "nonces" / f"{base_plan.plan_nonce}.claim"
        nonce_claim.write_bytes(b"tampered\n")
        os.chmod(nonce_claim, 0o600)
        expect_refusal(lambda: consume(guard, base_plan), "tamper")
        if (root / "active-run.lock").exists():
            raise AssertionError("Ledger-tamper refusal leaked the exclusive run lock.")

        os.chmod(nonce_claim, 0o644)
        expect_refusal(lambda: consume(guard, base_plan), "unsafe")

    with tempfile.TemporaryDirectory(prefix="id65-lock-tamper-") as temp:
        root = pathlib.Path(temp).resolve() / "state"
        guard = automation.DurableRunGuard(root, expected_uid=os.getuid())
        lock_plan = dataclasses.replace(
            base_plan,
            plan_nonce="90909090909090908080808080808080",
            sha256="8" * 64,
        )

        def tamper_active_lock() -> None:
            with guard.execution(lock_plan):
                (root / "active-run.lock").write_bytes(b"tampered lock\n")
                os.chmod(root / "active-run.lock", 0o600)

        expect_refusal(tamper_active_lock, "replaced or tampered")

    with tempfile.TemporaryDirectory(prefix="id65-root-rebind-") as temp:
        parent = pathlib.Path(temp).resolve()
        root = parent / "state"
        guard = automation.DurableRunGuard(root, expected_uid=os.getuid())
        rebind_plan = dataclasses.replace(
            base_plan,
            plan_nonce="45454545454545456767676767676767",
            sha256="9" * 64,
        )

        def rebind_root_during_run() -> None:
            with guard.execution(rebind_plan):
                root.rename(parent / "detached-state")
                root.mkdir(mode=0o700)
                expect_refusal(
                    lambda: consume(
                        automation.DurableRunGuard(root, expected_uid=os.getuid()),
                        rebind_plan,
                    ),
                    "namespace authority",
                )

        expect_refusal(rebind_root_during_run, "root path was rebound")
        expect_refusal(
            lambda: consume(
                automation.DurableRunGuard(root, expected_uid=os.getuid()),
                rebind_plan,
            ),
            "authority identity",
        )


def test_process_identity_drift_refuses() -> None:
    plan = parse_plan(("TOGGLE_PAUSE", "NOT_APPLICABLE"))
    stable = make_process()
    drifted = dataclasses.replace(stable, start_microseconds=stable.start_microseconds + 1)
    backend = FakeBackend()
    state_temp = tempfile.TemporaryDirectory(prefix="id65-drift-state-")
    lease_temp = tempfile.TemporaryDirectory(prefix="id65-drift-lease-")
    lease_root = pathlib.Path(lease_temp.name).resolve()
    foundation = automation.UnattendedFoundation(
        verifier=FakeVerifier(make_profile()),
        observer=FakeObserver([stable, drifted]),
        transport=automation.TargetedKeyTransport(backend, sleeper=lambda _: None),
        pad_confirmer=NoopPadConfirmer(),
        runtime_attestor=ReadyOfflineRuntimeAttestor(),
        run_guard=automation.DurableRunGuard(
            pathlib.Path(state_temp.name).resolve() / "private-state",
            expected_uid=os.getuid(),
        ),
        coordinator_lease_factory=lambda: automation.SharedCaptureCoordinatorLease(
            lease_root, expected_uid=os.getuid()
        ),
    )
    foundation._offline_state_temp = state_temp
    foundation._offline_lease_temp = lease_temp
    expect_refusal(
        lambda: foundation.run(plan, 700, execute=True),
        "identity changed before input",
    )
    if backend.posts:
        raise AssertionError("Process drift before input still posted an event.")


def test_profile_and_path_hash_hooks() -> None:
    with tempfile.TemporaryDirectory(prefix="id65-unattended-pin-") as temp:
        root = pathlib.Path(temp).resolve()
        pinned = root / "pinned.bin"
        pinned.write_bytes(b"exact pinned bytes")
        digest = hashlib.sha256(pinned.read_bytes()).hexdigest()
        identity = automation.read_pinned_file(
            pinned,
            digest,
            maximum_bytes=1024,
            expected_owner_uid=os.getuid(),
        )
        if identity.sha256 != digest or identity.real_path != str(pinned):
            raise AssertionError("Pinned resource hook returned a wrong identity.")
        expect_refusal(
            lambda: automation.read_pinned_file(
                pinned,
                "0" * 64,
                maximum_bytes=1024,
                expected_owner_uid=os.getuid(),
            ),
            "SHA-256 changed",
        )
        symlink = root / "alias.bin"
        symlink.symlink_to(pinned)
        expect_refusal(
            lambda: automation.read_pinned_file(
                symlink,
                digest,
                maximum_bytes=1024,
                expected_owner_uid=os.getuid(),
            ),
            "symlink",
        )


def test_mapped_executable_vnode_binding_and_path_replacement() -> None:
    def record(*fields: str) -> bytes:
        return b"\x00".join(field.encode("utf-8") for field in fields) + b"\x00\n"

    with tempfile.TemporaryDirectory(prefix="id65-mapped-vnode-") as temp:
        root = pathlib.Path(temp).resolve()
        executable = root / "DuckStation"
        executable.write_bytes(b"mapped executable bytes")
        os.chmod(executable, 0o755)
        info = executable.stat(follow_symlinks=False)
        mapped = automation.MappedExecutableVnode(
            device=info.st_dev,
            inode=info.st_ino,
            byte_length=info.st_size,
            link_count=info.st_nlink,
            real_path=str(executable),
        )
        lsof_bytes = b"".join((
            record("p700"),
            record(
                "ftxt", "D0x1", "i2", "s3", "k1", "tREG", "n/usr/lib/dyld"
            ),
            record(
                "ftxt", f"D{info.st_dev:#x}", f"i{info.st_ino}",
                f"s{info.st_size}", f"k{info.st_nlink}", "tREG",
                f"n{executable}",
            ),
        ))
        parsed = automation._parse_mapped_text_vnode(
            lsof_bytes, pid=700, executable_real_path=str(executable)
        )
        if parsed != mapped:
            raise AssertionError("lsof parser did not select the exact executable txt vnode.")
        identity = automation._read_observed_executable(
            str(executable), mapped_vnode=mapped
        )
        if (
            identity.device != info.st_dev
            or identity.inode != info.st_ino
            or identity.owner_uid != info.st_uid
            or identity.mode != 0o755
            or identity.sha256 != hashlib.sha256(executable.read_bytes()).hexdigest()
        ):
            raise AssertionError("Mapped executable identity was not descriptor-derived.")

        expect_refusal(
            lambda: automation._parse_mapped_text_vnode(
                lsof_bytes + lsof_bytes.split(record("p700"), 1)[1],
                pid=700,
                executable_real_path=str(executable),
            ),
            "one exact mapped",
        )
        expect_refusal(
            lambda: automation._parse_mapped_text_vnode(
                lsof_bytes.replace(b"p700", b"p701", 1),
                pid=700,
                executable_real_path=str(executable),
            ),
            "different process",
        )
        wrong_mapped = dataclasses.replace(mapped, inode=mapped.inode + 1)
        expect_refusal(
            lambda: automation._read_observed_executable(
                str(executable), mapped_vnode=wrong_mapped
            ),
            "differs from the mapped text vnode",
        )

        replacement = root / "replacement"
        replacement.write_bytes(b"replacement executable bytes")
        os.chmod(replacement, 0o755)
        os.replace(replacement, executable)
        expect_refusal(
            lambda: automation._read_observed_executable(
                str(executable), mapped_vnode=mapped
            ),
            "differs from the mapped text vnode",
        )

        # Recreate a matching mapping, then replace the pathname after the held
        # descriptor begins reading.  Stable post-fstat/path identity must fail.
        current = executable.stat(follow_symlinks=False)
        current_mapped = automation.MappedExecutableVnode(
            current.st_dev, current.st_ino, current.st_size, current.st_nlink,
            str(executable),
        )
        midread_replacement = root / "midread-replacement"
        midread_replacement.write_bytes(b"midread replacement")
        os.chmod(midread_replacement, 0o755)
        original_read = automation.os.read
        swapped = False

        def swapping_read(descriptor: int, count: int) -> bytes:
            nonlocal swapped
            value = original_read(descriptor, count)
            if value and not swapped:
                swapped = True
                os.replace(midread_replacement, executable)
            return value

        automation.os.read = swapping_read
        try:
            expect_refusal(
                lambda: automation._read_observed_executable(
                    str(executable), mapped_vnode=current_mapped
                ),
                "changed while hashing",
            )
        finally:
            automation.os.read = original_read


def test_shared_manual_capture_coordinator_lease() -> None:
    manual_path = TOOL_PATH.with_name("Capture-SpyroId65V3RuntimeSession.py")
    module_name = "id65_manual_capture_lock_test"
    spec = importlib.util.spec_from_file_location(module_name, manual_path)
    if spec is None or spec.loader is None:
        raise AssertionError("Cannot import the manual V3 capture coordinator.")
    manual = importlib.util.module_from_spec(spec)
    sys.modules[module_name] = manual
    spec.loader.exec_module(manual)

    with tempfile.TemporaryDirectory(prefix="id65-shared-coordinator-") as temp:
        repository_root = pathlib.Path(temp).resolve()
        lease_factory = lambda: automation.SharedCaptureCoordinatorLease(
            repository_root, expected_uid=os.getuid()
        )
        plan = parse_plan(
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
            nonce="71717171717171718282828282828282",
        )
        foundation, backend, confirmer = make_foundation(
            coordinator_lease_factory=lease_factory
        )
        with manual.CoordinatorLease(repository_root):
            expect_refusal(
                lambda: foundation.run(plan, 700, execute=True),
                "already owns the lease",
            )
            if (
                foundation.verifier.calls
                or foundation.observer.calls
                or backend.posts
                or confirmer.preflight_calls
                or foundation.run_guard.state_root.exists()
            ):
                raise AssertionError(
                    "Shared-lease contention crossed into automation or replay state."
                )
        # The same plan remains usable because lock contention occurred before
        # durable nonce and plan-hash claims.
        result = foundation.run(plan, 700, execute=True)
        if result.status != automation.INPUT_STATUS:
            raise AssertionError("Released manual lease did not permit the same plan.")

        with automation.SharedCaptureCoordinatorLease(
            repository_root, expected_uid=os.getuid()
        ):
            try:
                manual.CoordinatorLease(repository_root).__enter__()
            except manual.CaptureRefusal as exc:
                if "already owns the lease" not in str(exc):
                    raise AssertionError("Manual coordinator reported wrong lock refusal.") from exc
            else:
                raise AssertionError("Manual coordinator overlapped unattended ownership.")

        probed_during_pulse = False

        def probe_manual_lease(_: float) -> None:
            nonlocal probed_during_pulse
            candidate = manual.CoordinatorLease(repository_root)
            try:
                candidate.__enter__()
            except manual.CaptureRefusal:
                probed_during_pulse = True
                return
            candidate.__exit__(None, None, None)
            raise AssertionError("Shared coordinator lease was absent during key pulse.")

        pulse_plan = parse_plan(
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
            nonce="9393939393939393a4a4a4a4a4a4a4a4",
        )
        pulse_foundation, _, _ = make_foundation(
            coordinator_lease_factory=lease_factory,
            sleeper=probe_manual_lease,
        )
        pulse_foundation.run(pulse_plan, 700, execute=True)
        if not probed_during_pulse:
            raise AssertionError("Full input execution did not retain the shared lease.")

        lock_path = repository_root / automation.CAPTURE_STAGING_PREFIX / (
            ".capture-coordinator.lock"
        )
        lock_info = lock_path.stat(follow_symlinks=False)
        if (
            not stat.S_ISREG(lock_info.st_mode)
            or stat.S_IMODE(lock_info.st_mode) != 0o600
            or lock_info.st_uid != os.getuid()
            or lock_info.st_nlink != 1
        ):
            raise AssertionError("Shared persistent coordinator lock metadata drifted.")

        authority_path = repository_root / (
            automation.CAPTURE_COORDINATOR_AUTHORITY_DIRECTORY
        ) / automation.CAPTURE_COORDINATOR_AUTHORITY_NAME
        authority_info = authority_path.stat(follow_symlinks=False)
        if (
            not stat.S_ISREG(authority_info.st_mode)
            or stat.S_IMODE(authority_info.st_mode) != 0o600
            or authority_info.st_uid != os.getuid()
            or authority_info.st_nlink != 1
        ):
            raise AssertionError("Stable coordinator authority metadata drifted.")

        os.chmod(authority_path, 0o644)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "stable authority inode is unsafe",
            )
        finally:
            os.chmod(authority_path, 0o600)

        authority_link = authority_path.with_name("authority-hardlink-tamper")
        os.link(authority_path, authority_link)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "stable authority inode is unsafe",
            )
        finally:
            authority_link.unlink()

        authority_parent = authority_path.parent
        os.chmod(authority_parent, 0o755)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "authority parent is not owned mode 0700",
            )
        finally:
            os.chmod(authority_parent, 0o700)

        os.chmod(lock_path, 0o644)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "inode is unsafe",
            )
        finally:
            os.chmod(lock_path, 0o600)

        extra_link = lock_path.with_name("lock-hardlink-tamper")
        os.link(lock_path, extra_link)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "inode is unsafe",
            )
        finally:
            extra_link.unlink()

        staging_root = repository_root / automation.CAPTURE_STAGING_PREFIX
        os.chmod(staging_root, 0o755)
        try:
            expect_refusal(
                lambda: automation.SharedCaptureCoordinatorLease(
                    repository_root, expected_uid=os.getuid()
                ).__enter__(),
                "owned mode 0700",
            )
        finally:
            os.chmod(staging_root, 0o700)

        class RebindingRuntimeAttestor(ReadyOfflineRuntimeAttestor):
            def __init__(self) -> None:
                super().__init__()
                self.manual_lease: Any | None = None

            def attest(self, profile: Any, process: Any) -> None:
                super().attest(profile, process)
                if self.calls == 2:
                    staging_root.rename(staging_root.with_name("detached-capture-staging"))
                    staging_root.mkdir(mode=0o700)
                    self.manual_lease = manual.CoordinatorLease(repository_root)
                    self.manual_lease.__enter__()

        rebinding_attestor = RebindingRuntimeAttestor()
        rebind_foundation, rebind_backend, _ = make_foundation(
            coordinator_lease_factory=lease_factory,
            attestor=rebinding_attestor,
        )
        rebind_plan = parse_plan(
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
            nonce="b5b5b5b5b5b5b5b5c6c6c6c6c6c6c6c6",
        )

        def run_with_staging_rebind() -> None:
            try:
                rebind_foundation.run(rebind_plan, 700, execute=True)
            finally:
                if rebinding_attestor.manual_lease is not None:
                    rebinding_attestor.manual_lease.__exit__(None, None, None)

        expect_refusal(run_with_staging_rebind, "rebound")
        if rebind_backend.posts or rebind_foundation.transport.possibly_held:
            raise AssertionError("Capture-staging rebind reached input transport.")

        # If the fixed staging path is rebound after a posted down, validate
        # the retained directory/lock anchor before the owed up as well as
        # after it.  Namespace drift must not strand the host key.
        after_down_root = repository_root / "after-down-rebind-root"
        after_down_root.mkdir(mode=0o700)
        after_down_trace: list[str] = []
        leases: list[Any] = []

        class TrackingLease(automation.SharedCaptureCoordinatorLease):
            def assert_current(self) -> None:
                try:
                    super().assert_current()
                except BaseException:
                    after_down_trace.append("ownership-refused")
                    raise
                after_down_trace.append("ownership-valid")

        def tracking_lease_factory() -> Any:
            lease = TrackingLease(after_down_root, expected_uid=os.getuid())
            leases.append(lease)
            return lease

        after_down_staging = (
            after_down_root / automation.CAPTURE_STAGING_PREFIX
        )
        replacement_manual_lease: Any | None = None

        def rebind_after_down(_: float) -> None:
            nonlocal replacement_manual_lease
            after_down_trace.append("down-returned")
            after_down_staging.rename(
                after_down_staging.with_name("detached-after-down")
            )
            after_down_staging.mkdir(mode=0o700)
            replacement_manual_lease = manual.CoordinatorLease(after_down_root)
            try:
                replacement_manual_lease.__enter__()
            except manual.CaptureRefusal as exc:
                if "already owns the lease" not in str(exc):
                    raise
                replacement_manual_lease = None
                after_down_trace.append("replacement-manual-refused")
            else:
                raise AssertionError(
                    "Manual coordinator acquired replacement staging authority."
                )

        class KeyUpTracingBackend(FakeBackend):
            def force_key_up(
                self,
                pid: int,
                key_code: int,
                *,
                identity_validator: Any,
            ) -> Any:
                after_down_trace.append("forced-key-up")
                return super().force_key_up(
                    pid, key_code, identity_validator=identity_validator
                )

        after_down_backend = KeyUpTracingBackend()
        after_down_foundation, _, _ = make_foundation(
            coordinator_lease_factory=tracking_lease_factory,
            backend=after_down_backend,
            sleeper=rebind_after_down,
        )
        after_down_plan = parse_plan(
            ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
            nonce="d8d8d8d8d8d8d8d8e9e9e9e9e9e9e9e9",
        )
        try:
            expect_refusal(
                lambda: after_down_foundation.run(
                    after_down_plan, 700, execute=True
                ),
                "rebound",
            )
        finally:
            if replacement_manual_lease is not None:
                replacement_manual_lease.__exit__(None, None, None)
        if after_down_backend.posts != [(700, 49, True), (700, 49, False)]:
            raise AssertionError(
                "Post-down coordinator drift vetoed or duplicated the owed key-up."
            )
        if after_down_foundation.transport.possibly_held:
            raise AssertionError(
                "Post-down coordinator drift left a false possibly-held marker."
            )
        if (
            "replacement-manual-refused" not in after_down_trace
            or "ownership-refused" not in after_down_trace
            or "forced-key-up" not in after_down_trace
            or after_down_trace.index("ownership-refused")
            > after_down_trace.index("forced-key-up")
        ):
            raise AssertionError(
                "Coordinator anchor was not revalidated before the owed key-up."
            )
        if len(leases) != 1:
            raise AssertionError("Coordinator tracking lease count changed.")


def test_single_descriptor_plan_and_settings_swap_tamper() -> None:
    with tempfile.TemporaryDirectory(prefix="id65-one-fd-") as temp:
        root = pathlib.Path(temp).resolve()
        plan_path = root / "plan.json"
        original_plan = plan_bytes(
            (("TOGGLE_PAUSE", "NOT_APPLICABLE"),),
            nonce="10101010101010102020202020202020",
        )
        replacement_plan = plan_bytes(
            (("PAD_ANALOG", "NOT_APPLICABLE"),),
            nonce="30303030303030304040404040404040",
        )
        plan_path.write_bytes(original_plan)
        replacement = root / "replacement.json"
        replacement.write_bytes(replacement_plan)

        original_read = automation.os.read
        swapped = False

        def swapping_read(descriptor: int, count: int) -> bytes:
            nonlocal swapped
            value = original_read(descriptor, count)
            if value and not swapped:
                swapped = True
                os.replace(replacement, plan_path)
            return value

        automation.os.read = swapping_read
        try:
            expect_refusal(lambda: automation._read_plan(plan_path), "while reading")
        finally:
            automation.os.read = original_read

        plan_path.write_bytes(original_plan)
        tampered = False

        def tampering_read(descriptor: int, count: int) -> bytes:
            nonlocal tampered
            value = original_read(descriptor, count)
            if value and not tampered:
                tampered = True
                with open(plan_path, "r+b", buffering=0) as stream:
                    stream.seek(0)
                    stream.write(b"{")
                    stream.seek(1)
                    stream.write(b"X")
                    os.fsync(stream.fileno())
            return value

        automation.os.read = tampering_read
        try:
            expect_refusal(lambda: automation._read_plan(plan_path), "changed while reading")
        finally:
            automation.os.read = original_read

        settings_path = root / "settings.ini"
        settings_bytes = b"[Pad1]\nMacro1=Keyboard/1\n"
        settings_path.write_bytes(settings_bytes)
        settings_hash = hashlib.sha256(settings_bytes).hexdigest()
        replacement_settings = root / "replacement-settings.ini"
        replacement_settings.write_bytes(b"[Pad1]\nMacro1=Keyboard/F1\n")
        swapped_settings = False

        def swapping_settings_read(descriptor: int, count: int) -> bytes:
            nonlocal swapped_settings
            value = original_read(descriptor, count)
            if value and not swapped_settings:
                swapped_settings = True
                os.replace(replacement_settings, settings_path)
            return value

        automation.os.read = swapping_settings_read
        try:
            expect_refusal(
                lambda: automation.read_pinned_bytes(
                    settings_path,
                    settings_hash,
                    maximum_bytes=4096,
                    expected_owner_uid=os.getuid(),
                ),
                "while reading",
            )
        finally:
            automation.os.read = original_read

        settings_path.write_bytes(settings_bytes)
        tampered_settings = False

        def tampering_settings_read(descriptor: int, count: int) -> bytes:
            nonlocal tampered_settings
            value = original_read(descriptor, count)
            if value and not tampered_settings:
                tampered_settings = True
                with open(settings_path, "r+b", buffering=0) as stream:
                    stream.seek(1)
                    stream.write(b"X")
                    os.fsync(stream.fileno())
            return value

        automation.os.read = tampering_settings_read
        try:
            expect_refusal(
                lambda: automation.read_pinned_bytes(
                    settings_path,
                    settings_hash,
                    maximum_bytes=4096,
                    expected_owner_uid=os.getuid(),
                ),
                "changed while reading",
            )
        finally:
            automation.os.read = original_read


def test_no_machine_finalization_surface() -> None:
    result = automation.run_self_test()
    if result.status != automation.SELF_TEST_STATUS or result.machine_finalized:
        raise AssertionError("Self-test result claimed final machine acceptance.")
    expect_refusal(
        lambda: automation.AutomationResult(
            automation.SELF_TEST_STATUS,
            "forged",
            automation.PROFILE_ID,
            None,
            None,
            0,
            0,
            0,
            True,
        ),
        "final acceptance",
    )
    forbidden_public = {
        name for name in dir(automation)
        if not name.startswith("_") and any(token in name.lower() for token in (
            "finalize", "publish", "create_bin", "launch_duckstation"
        ))
    }
    if forbidden_public:
        raise AssertionError(f"Machine-finalization surface appeared: {forbidden_public!r}")
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    production_executable = os.fspath(
        repository_root / automation.DUCKSTATION_EXECUTABLE_RELATIVE_PATH
    )
    production_app = os.fspath(
        repository_root / automation.PACKAGED_APP_RELATIVE_PATH
    )
    if (
        not _command_launches_production_duckstation(
            [production_executable], repository_root
        )
        or not _command_launches_production_duckstation(
            ["/usr/bin/open", production_app], repository_root
        )
        or _command_launches_production_duckstation(["/bin/sleep", "1"], repository_root)
    ):
        raise AssertionError("Actual DuckStation launch-attempt counter is not exact.")


def test_cli_self_test_terminal_contract() -> None:
    completed = subprocess.run(
        [sys.executable, str(TOOL_PATH), "--self-test"],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=30,
    )
    if completed.returncode != 0 or completed.stderr:
        raise AssertionError(
            f"CLI self-test failed: code={completed.returncode} stderr={completed.stderr!r}"
        )
    payload = json.loads(completed.stdout)
    if payload != {
        "confirmedCount": 0,
        "machineFinalized": False,
        "mode": "self-test",
        "planSha256": automation.InputPlan.parse(
            automation._example_plan_bytes()
        ).sha256,
        "plannedCount": 1,
        "postedCount": 0,
        "processPid": None,
        "profileId": automation.PROFILE_ID,
        "status": automation.SELF_TEST_STATUS,
    }:
        raise AssertionError("CLI self-test terminal payload changed.")

    refused = subprocess.run(
        [sys.executable, str(TOOL_PATH), "--execute"],
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        check=False,
        timeout=30,
    )
    if (
        refused.returncode != 2
        or refused.stdout
        or not refused.stderr.startswith(b"REFUSED: ")
    ):
        raise AssertionError("Incomplete CLI execution did not terminate as a refusal.")


def test_production_profile_chain_refuses_before_f12_or_space_post() -> None:
    global PRODUCTION_EVENT_POSTS
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    verifier_module = automation._load_external_home_verifier(repository_root)
    snapshot_calls: list[tuple[pathlib.Path, str, str, str, str | None]] = []
    original_snapshot = verifier_module.capture_runtime_snapshot

    def tracked_snapshot(
        root: pathlib.Path,
        *,
        phase: str,
        session_id: str,
        process_instance_id: str,
        captured_at_utc: str | None = None,
    ) -> dict[str, object]:
        snapshot_calls.append((
            root, phase, session_id, process_instance_id, captured_at_utc
        ))
        return original_snapshot(
            root,
            phase=phase,
            session_id=session_id,
            process_instance_id=process_instance_id,
            captured_at_utc=captured_at_utc,
        )

    def forbidden_prelaunch(*_args: object, **_kwargs: object) -> object:
        raise AssertionError("Automation called full codesign/prelaunch verification.")

    def forbidden_subprocess(*_args: object, **_kwargs: object) -> object:
        raise AssertionError("Automation runtime snapshot attempted a subprocess.")

    verifier_module.capture_runtime_snapshot = tracked_snapshot
    verifier_module.verify_prelaunch = forbidden_prelaunch
    original_loader = automation._load_external_home_verifier
    original_run = automation.subprocess.run
    original_popen = automation.subprocess.Popen
    automation._load_external_home_verifier = lambda _root: verifier_module
    automation.subprocess.run = forbidden_subprocess
    automation.subprocess.Popen = forbidden_subprocess

    class RecordingFrozenVerifier:
        def __init__(self) -> None:
            self.frozen = automation.FrozenProfileVerifier(repository_root)
            self.profile: Any | None = None

        def verify(self) -> Any:
            self.profile = self.frozen.verify()
            return self.profile

    recording = RecordingFrozenVerifier()
    backend = FakeBackend()
    foundation, _, _ = make_foundation(
        verifier=recording,
        backend=backend,
        confirmer=automation.UnavailableNativePadConfirmer(),
        attestor=automation.UnavailableRuntimeSessionAttestor(),
    )
    plan = parse_plan(
        ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
        ("POWER_OFF", "NOT_APPLICABLE"),
        nonce="facefacefacefaceb00cb00cb00cb00c",
    )
    try:
        expect_refusal(
            lambda: foundation.run(plan, 700, execute=True),
            "GDB confirmation",
        )
    finally:
        automation._load_external_home_verifier = original_loader
        automation.subprocess.run = original_run
        automation.subprocess.Popen = original_popen
    PRODUCTION_EVENT_POSTS += len(backend.posts)
    if backend.posts or PRODUCTION_EVENT_POSTS != 0:
        raise AssertionError("Unavailable production readiness posted F12 or Space.")
    if recording.profile is None:
        raise AssertionError("Production external-HOME profile was not verified.")
    expected_actions = frozenset({
        automation.KeyAction.PAD_R1_TOGGLE,
        automation.KeyAction.PAD_R2_TOGGLE,
        automation.KeyAction.PAD_L1_TOGGLE,
        automation.KeyAction.PAD_L2_TOGGLE,
        automation.KeyAction.PAD_LEFT_TOGGLE,
        automation.KeyAction.PAD_DOWN_TOGGLE,
        automation.KeyAction.TOGGLE_PAUSE,
        automation.KeyAction.POWER_OFF,
    })
    if recording.profile.enabled_actions != expected_actions:
        raise AssertionError("Production enabled-action closure is not exact.")
    if len(snapshot_calls) != 1:
        raise AssertionError("Production Automation did not take one profile snapshot.")
    root, phase, session_id, process_instance_id, timestamp = snapshot_calls[0]
    if (
        root != repository_root
        or phase != "before"
        or session_id != automation.PROFILE_SNAPSHOT_SESSION_ID
        or process_instance_id != automation.PROFILE_SNAPSHOT_PROCESS_INSTANCE_ID
        or type(timestamp) is not str
        or automation.CANONICAL_UTC_RE.fullmatch(timestamp) is None
    ):
        raise AssertionError("Production snapshot-before invocation is not canonical.")


def test_native_read_only_boundaries(repository_root: pathlib.Path) -> str:
    if sys.platform != "darwin":
        return "native-skipped-non-macos"
    observer = automation.MacProcessObserver()
    current = observer.observe(os.getpid())
    if (
        current.pid != os.getpid()
        or current.effective_uid != os.geteuid()
        or current.real_uid != os.getuid()
        or current.saved_uid != os.getuid()
        or current.start_seconds <= 0
    ):
        raise AssertionError("Native libproc identity did not match this test process.")
    release_identity = observer.observe_release_identity(os.getpid(), current)
    if release_identity != current:
        raise AssertionError("Native release-only mapped identity changed the baseline.")

    # Real native regression: a temp owned /bin/sleep mapping survives atomic
    # pathname replacement between the fake key-down and owed fake key-up.  No
    # CGEvent is created or posted; only libproc/lsof read-only observation runs.
    with tempfile.TemporaryDirectory(prefix="id65-release-vnode-") as temp:
        root = pathlib.Path(temp).resolve()
        executable = root / "owned-sleep"
        replacement = root / "replacement-sleep"
        shutil.copyfile("/bin/sleep", executable)
        shutil.copyfile("/bin/sleep", replacement)
        os.chmod(executable, 0o755)
        os.chmod(replacement, 0o755)
        child = subprocess.Popen(
            [str(executable), "10"],
            stdin=subprocess.DEVNULL,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            close_fds=True,
        )
        try:
            child_baseline = observer.observe(child.pid)
            profile = dataclasses.replace(
                make_profile(executable=child_baseline.executable),
                effective_uid=os.geteuid(),
            )

            class PathReplacingFakeBackend(FakeBackend):
                def post_key_down(self, pid: int, key_code: int) -> None:
                    super().post_key_down(pid, key_code)
                    os.replace(replacement, executable)

            class CachedBaselineThenNativeReleaseObserver:
                """Avoid a second pre-swap lsof while exercising native release."""

                def __init__(self) -> None:
                    self.observe_calls = 0
                    self.release_calls = 0

                def observe(self, pid: int) -> Any:
                    self.observe_calls += 1
                    if pid != child_baseline.pid:
                        raise automation.AutomationRefusal(
                            "Native regression observer received the wrong PID."
                        )
                    if self.observe_calls <= 2:
                        return child_baseline
                    return observer.observe(pid)

                def observe_release_identity(self, pid: int, baseline: Any) -> Any:
                    self.release_calls += 1
                    return observer.observe_release_identity(pid, baseline)

            release_observer = CachedBaselineThenNativeReleaseObserver()
            fake_backend = PathReplacingFakeBackend(frontmost_pids=(child.pid,))
            foundation, _, _ = make_foundation(
                profile=profile,
                observer=release_observer,
                backend=fake_backend,
            )
            plan = parse_plan(
                ("TOGGLE_PAUSE", "NOT_APPLICABLE"),
                nonce="d7d7d7d7d7d7d7d7e8e8e8e8e8e8e8e8",
            )
            expect_refusal(
                lambda: foundation.run(plan, child.pid, execute=True),
                "post-release",
            )
            if fake_backend.posts != [
                (child.pid, 49, True), (child.pid, 49, False)
            ]:
                raise AssertionError(
                    "Real path replacement vetoed or duplicated the fake owed key-up."
                )
            if foundation.transport.possibly_held:
                raise AssertionError(
                    "Real path replacement left a false possibly-held marker."
                )
            if release_observer.release_calls != 1:
                raise AssertionError(
                    "Real path replacement did not exercise one native release read."
                )
        finally:
            child.terminate()
            try:
                child.wait(timeout=5)
            except subprocess.TimeoutExpired:
                child.kill()
                child.wait(timeout=5)
    event_backend = automation.MacCGEventBackend()
    access = event_backend.preflight_access()
    if type(access) is not bool:
        raise AssertionError("Native Accessibility preflight did not return bool.")
    frontmost_pid = event_backend.frontmost.frontmost_pid()
    if type(frontmost_pid) is not int or frontmost_pid <= 1:
        raise AssertionError("Native frontmost application PID lookup was invalid.")
    # Read-only hash verification of the real frozen resources.  No process is
    # launched and CGEventPostToPid is deliberately never called here.
    verified = automation.FrozenProfileVerifier(repository_root).verify()
    if (
        verified.executable.sha256 != automation.EXPECTED_DUCKSTATION_SHA256
        or verified.settings.sha256 != automation.EXPECTED_SETTINGS_SHA256
        or verified.candidate_cue.sha256 != automation.EXPECTED_CANDIDATE_CUE_SHA256
        or verified.candidate_bin.sha256 != automation.EXPECTED_CANDIDATE_BIN_SHA256
        or verified.handoff_receipt_sha256
        != automation.EXPECTED_HANDOFF_RECEIPT_SHA256
        or verified.app_content_manifest_sha256
        != automation.EXPECTED_APP_CONTENT_MANIFEST_SHA256
        or verified.app_output_tree_manifest_sha256
        != automation.EXPECTED_APP_OUTPUT_TREE_MANIFEST_SHA256
        or verified.package_envelope_sha256
        != automation.EXPECTED_PACKAGE_ENVELOPE_SHA256
        or verified.external_home_bios_sha256
        != automation.EXPECTED_EXTERNAL_HOME_BIOS_SHA256
        or verified.host_normal_settings_sentinel_sha256
        != automation.EXPECTED_HOST_SETTINGS_SENTINEL_SHA256
        or automation.KeyAction.POWER_OFF not in verified.enabled_actions
        or automation.KeyAction.TOGGLE_PAUSE not in verified.enabled_actions
        or automation.KeyAction.PAD_ANALOG in verified.enabled_actions
    ):
        raise AssertionError("Native frozen-profile verification returned wrong pins.")
    return f"native-verified-accessibility-{str(access).lower()}"


def main() -> int:
    global ACTUAL_DUCKSTATION_CALLS, ACTUAL_GDB_CONNECTION_ATTEMPTS
    global NATIVE_EVENT_POST_ATTEMPTS
    repository_root = pathlib.Path(__file__).resolve().parent.parent
    tests = (
        test_canonical_plan_and_allowlist,
        test_plan_v1_and_retired_profile_are_refused,
        test_verifier_hash_before_import_root_and_contract_pins,
        test_snapshot_chain_drift_and_exception_are_zero_post,
        test_reordered_and_duplicate_events_refused,
        test_exact_pid_path_uid_and_hash_refusals,
        test_accessibility_permission_refusal_is_non_posting,
        test_mode_specific_statuses_and_counts,
        test_wrong_or_changed_frontmost_pid_is_non_posting,
        test_frontmost_pid_is_rechecked_after_key_up,
        test_focus_change_after_down_forces_exact_release_then_refuses,
        test_exact_f12_and_space_bindings_and_empty_action_set,
        test_post_down_attestation_and_accessibility_drift_release_then_refuse,
        test_exception_path_releases_key,
        test_stuck_key_is_retried_and_refused,
        test_transport_rejects_non_enum_action,
        test_strict_gdb_listener_parser_and_native_negative,
        test_persistent_gdb_reader_fixed_packets_and_fragmentation,
        test_persistent_gdb_reader_rsp_refusal_matrix,
        test_persistent_gdb_reader_semantic_and_binding_refusals,
        test_hotkey_only_plan_requires_process_bound_pad_probe,
        test_read_only_pad_confirmation_and_timeout,
        test_macro_baseline_and_compensating_untoggle,
        test_native_pad_source_refuses_before_input,
        test_runtime_session_attestor_refuses_before_input,
        test_replay_guard_refuses_second_execution,
        test_durable_lock_ledger_concurrency_and_tamper,
        test_process_identity_drift_refuses,
        test_profile_and_path_hash_hooks,
        test_mapped_executable_vnode_binding_and_path_replacement,
        test_shared_manual_capture_coordinator_lease,
        test_single_descriptor_plan_and_settings_swap_tamper,
        test_no_machine_finalization_surface,
        test_cli_self_test_terminal_contract,
        test_production_profile_chain_refuses_before_f12_or_space_post,
    )
    original_run = subprocess.run
    original_popen = subprocess.Popen
    original_gdb_connector = automation.socket.create_connection

    def tracked_run(*args: object, **kwargs: object) -> Any:
        # subprocess.run delegates to the tracked Popen below, so counting here
        # as well would double-count one launch attempt.
        return original_run(*args, **kwargs)

    def tracked_popen(*args: object, **kwargs: object) -> Any:
        global ACTUAL_DUCKSTATION_CALLS
        command = args[0] if args else kwargs.get("args")
        if _command_launches_production_duckstation(command, repository_root):
            ACTUAL_DUCKSTATION_CALLS += 1
        return original_popen(*args, **kwargs)

    def refuse_real_gdb_connection(*args: object, **kwargs: object) -> Any:
        del args, kwargs
        global ACTUAL_GDB_CONNECTION_ATTEMPTS
        ACTUAL_GDB_CONNECTION_ATTEMPTS += 1
        raise AssertionError("Deep test attempted a real DuckStation GDB connection.")

    native_post_methods = (
        "post_key_down",
        "force_key_up",
        "force_compensating_key_down",
    )
    original_native_posts = {
        name: getattr(automation.MacCGEventBackend, name)
        for name in native_post_methods
    }

    def tracked_native_post(
        self: Any,
        *args: object,
        _method_name: str,
        **kwargs: object,
    ) -> Any:
        global NATIVE_EVENT_POST_ATTEMPTS
        NATIVE_EVENT_POST_ATTEMPTS += 1
        return original_native_posts[_method_name](self, *args, **kwargs)

    subprocess.run = tracked_run
    subprocess.Popen = tracked_popen
    automation.socket.create_connection = refuse_real_gdb_connection
    for method_name in native_post_methods:
        setattr(
            automation.MacCGEventBackend,
            method_name,
            lambda self, *args, _method_name=method_name, **kwargs: tracked_native_post(
                self, *args, _method_name=_method_name, **kwargs
            ),
        )
    try:
        for test in tests:
            test()
        native = test_native_read_only_boundaries(repository_root)
    finally:
        subprocess.run = original_run
        subprocess.Popen = original_popen
        automation.socket.create_connection = original_gdb_connector
        for method_name, method in original_native_posts.items():
            setattr(automation.MacCGEventBackend, method_name, method)
    if (
        ACTUAL_DUCKSTATION_CALLS != 0
        or ACTUAL_GDB_CONNECTION_ATTEMPTS != 0
        or NATIVE_EVENT_POST_ATTEMPTS != 0
        or PRODUCTION_EVENT_POSTS != 0
    ):
        raise AssertionError(
            "Deep test crossed a forbidden DuckStation launch/GDB/native-post boundary."
        )
    print(
        f"{automation.SELF_TEST_STATUS} tests={len(tests) + 1} "
        f"actualDuckStationCalls={ACTUAL_DUCKSTATION_CALLS} "
        f"actualGdbConnectionAttempts={ACTUAL_GDB_CONNECTION_ATTEMPTS} "
        f"nativeEventPostAttempts={NATIVE_EVENT_POST_ATTEMPTS} "
        f"productionFakeEventPosts={PRODUCTION_EVENT_POSTS} "
        f"machineFinalized=false {native}"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
